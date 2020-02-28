using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using Microsoft.Scripting;
using Sympl.Expressions;
using Sympl.Runtime;

namespace Sympl.Syntax
{
    public static class Parser
    {
        /// <summary>
        /// Returns an array of top-level expressions parsed in the <paramref name="reader"/>.
        /// </summary>
        public static SymplExpression[] ParseFile(TextReader reader)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            var lexer = new Lexer(reader);
            var body = new List<SymplExpression>();
            var token = lexer.GetToken();
            while (token != SyntaxToken.Eof)
            {
                lexer.PutToken(token);
                body.Add(ParseExpression(lexer));
                token = lexer.GetToken();
            }

            return body.ToArray();
        }

        /// <summary>
        /// Returns a single expression parsed from the <paramref name="reader"/>.
        /// </summary>
        public static SymplExpression ParseSingleExpression(TextReader reader)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            return ParseExpression(new Lexer(reader));
        }

        /// <summary>
        /// Parses an expression from the Lexer passed in.
        /// </summary>  
        static SymplExpression ParseExpression(Lexer lexer)
        {
            var token = lexer.GetToken();

            SymplExpression? res = null;
            if (token == SyntaxToken.Eof)
                throw new SymplParseException("Unexpected EOF encountered while parsing expression.", ScriptCodeParseResult.IncompleteToken);

            switch (token)
            {
                case SyntaxToken { Kind: SyntaxTokenKind.Quote }:
                    lexer.PutToken(token);
                    res = ParseQuoteExpression(lexer);
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                    lexer.PutToken(token);
                    res = ParseParentheticForm(lexer);
                    break;
                case KeywordToken keywordToken when keywordToken.Kind != KeywordTokenKind.Nil &&
                                                   keywordToken.Kind != KeywordTokenKind.True &&
                                                   keywordToken.Kind != KeywordTokenKind.False:
                    throw new InvalidCastException("Keyword cannot be an expression");
                case IdOrKeywordToken idOrKeywordToken:
                    res = new SymplIdentifier(idOrKeywordToken);
                    break;
                case LiteralToken literalToken:
                    res = new SymplLiteral(literalToken.Value);
                    break;
            }

            // check for dotted expression
            if (res is null)
                throw new SymplParseException(
                    $"Unexpected token when expecting beginning of expression -- {token} ... {lexer.GetToken()}{lexer.GetToken()}{lexer.GetToken()}{lexer.GetToken()}");
            
            var next = lexer.GetToken();
            lexer.PutToken(next);

            return next.IsSyntaxToken(SyntaxTokenKind.Dot) ? ParseDottedExpression(lexer, res) : res;
        }

        /// <summary>
        /// Parses a parenthetic form.
        /// </summary>
        /// <devdoc>
        /// If the first token after the paren is a keyword, then it something like defun, loop, if,
        /// try, etc. If the first sub expression is another parenthetic form, then it must be an
        /// expression that returns a callable object.
        /// </devdoc>
        static SymplExpression ParseParentheticForm(Lexer lexer)
        {
            MatchToken(lexer, SyntaxTokenKind.OpenParenthesis);

            var token = lexer.GetToken();
            lexer.PutToken(token);

            return token is KeywordToken ? ParseKeywordForm(lexer) : ParseFunctionCall(lexer);
        }

        /// <summary>
        /// Parses parenthetic built in forms such as defun, if, loop, etc.
        /// </summary>
        static SymplExpression ParseKeywordForm(Lexer lexer)
        {
            var name = lexer.GetToken();
            if (!(name is KeywordToken keyword))
                throw new SymplParseException("Internal error: parsing keyword form?");

            lexer.PutToken(name);

            if (keyword.Kind == KeywordTokenKind.Add ||
                keyword.Kind == KeywordTokenKind.Subtract ||
                keyword.Kind == KeywordTokenKind.Multiply ||
                keyword.Kind == KeywordTokenKind.Divide ||
                keyword.Kind == KeywordTokenKind.Equal ||
                keyword.Kind == KeywordTokenKind.NotEqual ||
                keyword.Kind == KeywordTokenKind.GreaterThan ||
                keyword.Kind == KeywordTokenKind.LessThan ||
                keyword.Kind == KeywordTokenKind.And ||
                keyword.Kind == KeywordTokenKind.Or)
                return ParseBinaryExpression(lexer);

            return keyword.Kind switch
            {
                KeywordTokenKind.Import => ParseImport(lexer),
                KeywordTokenKind.Defun => ParseDefun(lexer),
                KeywordTokenKind.Lambda => ParseLambda(lexer),
                KeywordTokenKind.Set => ParseSet(lexer),
                KeywordTokenKind.LetStar => ParseLetStar(lexer),
                KeywordTokenKind.Block => ParseBlock(lexer),
                KeywordTokenKind.Eq => ParseEq(lexer),
                KeywordTokenKind.Cons => ParseCons(lexer),
                KeywordTokenKind.List => ParseListCall(lexer),
                KeywordTokenKind.If => ParseIf(lexer),
                KeywordTokenKind.New => ParseNew(lexer),
                KeywordTokenKind.Loop => ParseLoop(lexer),
                KeywordTokenKind.Break => ParseBreak(lexer),
                KeywordTokenKind.Elt => ParseElt(lexer),
                KeywordTokenKind.Not => ParseUnaryExpression(lexer),
                _ => throw new SymplParseException("Internal: unrecognized keyword form?"),
            };
        }

        static SymplExpression ParseDefun(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Defun);

            var name = lexer.GetToken();

            if (name is KeywordToken || !(name is IdOrKeywordToken idOrKeyword))
                throw new SymplParseException($"Defun must have an ID for name -- {name}");

            return new SymplDefun(idOrKeyword.Name, ParseParams(lexer, "Defun"), ParseBody(lexer, $"Hit EOF in function body{idOrKeyword.Name}"));
        }

        static SymplExpression ParseLambda(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Lambda);
            return new SymplLambda(ParseParams(lexer, "Lambda"), ParseBody(lexer, "Hit EOF in function body"));
        }

        /// <summary>
        /// Parses a sequence of vars for Defuns and Lambdas, and always returns a list of IdTokens.
        /// </summary>
        static IdOrKeywordToken[] ParseParams(Lexer lexer, String definer)
        {
            var token = MatchToken(lexer, SyntaxTokenKind.OpenParenthesis);

            lexer.PutToken(token);
            return EnsureListOfIds(ParseList(lexer, "param list.").Elements, false, $"{definer} params must be valid IDs.");
        }

        /// <summary>
        /// Parses a sequence of expressions as for Defun, Let, etc., and always returns a list, even
        /// if empty. It gobbles the close paren too.
        /// </summary>
        static SymplExpression[] ParseBody(Lexer lexer, String error)
        {
            var token = lexer.GetToken();
            var body = new List<SymplExpression>();

            for (; token != SyntaxToken.Eof && !token.IsSyntaxToken(SyntaxTokenKind.CloseParenthesis); token = lexer.GetToken())
            {
                lexer.PutToken(token);
                body.Add(ParseExpression(lexer));
            }

            if (token == SyntaxToken.Eof)
                throw new SymplParseException(error, ScriptCodeParseResult.IncompleteToken);

            return body.ToArray();
        }

        // (import id[.id]* [{id | (id [id]*)} [{id | (id [id]*)}]] ) (import file-or-dotted-Ids
        // name-or-list-of-members reanme-or-list-of)
        static SymplExpression ParseImport(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Import);

            var nsOrModule = ParseImportNameOrModule(lexer);
            var members = ParseImportNames(lexer, "member names", true);
            var asNames = ParseImportNames(lexer, "renames", false);

            if (members.Length != asNames.Length && asNames.Length != 0)
                throw new SymplParseException("Import as-names must be same form as member names.");

            if (!lexer.GetToken().IsSyntaxToken(SyntaxTokenKind.CloseParenthesis))
                throw new SymplParseException("Import must end with closing paren.", ScriptCodeParseResult.IncompleteToken);

            return new SymplImport(nsOrModule, members, asNames);
        }

        /// <summary>
        /// Parses dotted namespaces or <see cref="CodeContext" /> members to import.
        /// </summary>
        static IdOrKeywordToken[] ParseImportNameOrModule(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (!(token is IdOrKeywordToken idOrKeyword))
                // Keywords are ok here.
                throw new SymplParseException("Id must follow Import symbol");

            var dot = lexer.GetToken();
            var nsOrModule = new List<IdOrKeywordToken>();
            if (dot.IsSyntaxToken(SyntaxTokenKind.Dot))
            {
                nsOrModule.Add(idOrKeyword);
                lexer.PutToken(dot);
                var tmp = ParseDottedExpression(lexer, new SymplIdentifier(idOrKeyword));
                foreach (var e in tmp.Expressions)
                {
                    if (!(e is SymplIdentifier id))
                    {
                        // Keywords are ok here.
                        throw new SymplParseException($"Import targets must be dotted identifiers.{e}{nsOrModule}");
                    }

                    nsOrModule.Add(id.IdToken);
                }

                token = lexer.GetToken();
            }
            else
            {
                nsOrModule.Add(idOrKeyword);
                token = dot;
            }

            lexer.PutToken(token);
            return nsOrModule.ToArray();
        }

        /// <summary> Parses list of member names to import from the object represented in the result
        /// of <see cref="ParseImportNameOrModule(Lexer)" />, which will be a file module or object
        /// from <see cref="CodeContext.Globals" />. This is also used to parse the list of renames for
        /// these same members. </summary>
        static IdOrKeywordToken[] ParseImportNames(Lexer lexer, String nameKinds, Boolean allowKeywords)
        {
            var token = lexer.GetToken();
            var names = new List<IdOrKeywordToken>();

            switch (token)
            {
                case IdOrKeywordToken idToken when idToken.GetType() == typeof(IdOrKeywordToken): // No keywords
                    names.Add(idToken);
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                    lexer.PutToken(token);
                    var memberTokens = ParseList(lexer, $"Import {nameKinds}.").Elements;

                    EnsureListOfIds(memberTokens, allowKeywords, $"Import {nameKinds} must be valid IDs.");
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis }:
                    lexer.PutToken(token);
                    break;
                default:
                    throw new SymplParseException("Import takes dotted names, then member vars.");
            }

            return names.ToArray();
        }

        static IdOrKeywordToken[] EnsureListOfIds(Object[] list, Boolean allowKeywords, String error)
        {
            foreach (var t in list)

                // if t is not an id or keyword, or id is a keyword token when it's not allowed
                if (!(t is IdOrKeywordToken id) || !allowKeywords && id is KeywordToken)
                    throw new SymplParseException(error);

            return Array.FindAll(Array.ConvertAll(list, l => l as IdOrKeywordToken), f => f is { })!;
        }

        /// <summary>
        /// Gathers infix dotted member access expressions.
        /// </summary>
        /// <devdoc>
        /// The object expression can be anything and is passed in via expression. Successive member
        /// accesses must be dotted identifier expressions or member invokes -- a.b.(c 3).d. The
        /// member invokes cannot have dotted expressions for the member name such as a.(b.c 3).
        /// </devdoc>
        static SymplDot ParseDottedExpression(Lexer lexer, SymplExpression objExpr)
        {
            MatchToken(lexer, SyntaxTokenKind.Dot);

            var exprs = new List<SymplExpression>();

            var token = lexer.GetToken();
            for (; token is IdOrKeywordToken || token.IsSyntaxToken(SyntaxTokenKind.OpenParenthesis); token = lexer.GetToken())
            {
                // Needs to be fun call or IDs
                SymplExpression expression;
                if (token is IdOrKeywordToken keywordToken)
                {
                    // Keywords are ok as member names.
                    expression = new SymplIdentifier(keywordToken);
                }
                else
                { 
                    lexer.PutToken(token);
                    expression = ParseParentheticForm(lexer);
                    if (expression is SymplCall funCall && !(funCall.Function is SymplIdentifier))
                        throw new SymplParseException($"Dotted expressions must be identifiers or function calls with identifiers as the function value -- {expression}");
                }

                exprs.Add(expression);
                if (!(token = lexer.GetToken()).IsSyntaxToken(SyntaxTokenKind.Dot))
                    break;
            }

            lexer.PutToken(token);
            return new SymplDot(objExpr, exprs.ToArray());
        }

        /// <summary>
        /// Parses a LHS expression and value expression. All analysis on the LHS is in etgen.py.
        /// </summary>
        static SymplAssignment ParseSet(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Set);

            var lhs = ParseExpression(lexer);
            var val = ParseExpression(lexer);
            if (!lexer.GetToken().IsSyntaxToken(SyntaxTokenKind.CloseParenthesis))
                throw new SymplParseException("Expected close paren for Set expression.", ScriptCodeParseResult.IncompleteToken);

            return new SymplAssignment(lhs, val);
        }

        /// <summary>
        /// Parses <c>(let* (([var] [expression])*) [body]).</c>
        /// </summary>
        static SymplLetStar ParseLetStar(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.LetStar);
            MatchToken(lexer, SyntaxTokenKind.OpenParenthesis);

            // Get bindings
            var bindings = new List<SymplLetStar.LetBinding>();

            var token = lexer.GetToken();
            for (; token.IsSyntaxToken(SyntaxTokenKind.OpenParenthesis); token = lexer.GetToken())
            {
                var e = ParseExpression(lexer);
                if (!(e is SymplIdentifier id) || id.IdToken is KeywordToken)
                    throw new SymplParseException("Let binding must be (<ID> <expression>) -- ");

                var init = ParseExpression(lexer);
                bindings.Add(new SymplLetStar.LetBinding(id.IdToken, init));

                MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);
            }

            MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);

            return new SymplLetStar(bindings.ToArray(), ParseBody(lexer, "Unexpected EOF in Let."));
        }

        /// <summary>
        /// Parses a block expression, a sequence of exprs to execute in order, returning the last
        /// expression's value.
        /// </summary>
        static SymplBlock ParseBlock(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Block);

            return new SymplBlock(ParseBody(lexer, "Unexpected EOF in Block."));
        }

        /// <devdoc>
        /// First sub form must be expression resulting in callable, but if it is dotted expression, then eval
        /// the first N-1 dotted exprs and use invoke member or get member on last of dotted exprs so
        /// that the 2..N sub forms are the arguments to the invoke member. It's as if the call
        /// breaks into a block of a temp assigned to the N-1 dotted exprs followed by an invoke
        /// member (or a get member and call, which the runtime binder decides). The non-dotted expression
        /// simply evals to an object that better be callable with the supplied args, which may be none.
        /// </devdoc>
        static SymplCall ParseFunctionCall(Lexer lexer)
        {
            // First sub expression is callable object or invoke member expression.
            var fun = ParseExpression(lexer);
            if (fun is SymplDot dottedExpr)
                // Keywords ok as members.
                if (!(dottedExpr.Expressions[^1] is SymplIdentifier))
                    throw new SymplParseException($"Function call with dotted expression for function must end with ID expression, not member invoke.{dottedExpr.Expressions[^1]}");

            // Tail exprs are args.
            return new SymplCall(fun, ParseBody(lexer, $"Unexpected EOF in arg list for {fun}"));
        }

        /// <summary>
        /// Parses a quoted list, ID/keyword, or literal.
        /// </summary>
        static SymplQuote ParseQuoteExpression(Lexer lexer)
        {
            MatchToken(lexer, SyntaxTokenKind.Quote);

            var token = lexer.GetToken();
            Object expression;

            switch (token)
            {
                case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                    lexer.PutToken(token);
                    expression = ParseList(lexer, "quoted list.");
                    break;
                case IdOrKeywordToken _:
                case LiteralToken _:
                    expression = token;
                    break;
                default:
                    throw new SymplParseException("Quoted expression can only be list, ID/Symbol, or literal.");
            }

            return new SymplQuote(expression);
        }

        static SymplEq ParseEq(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Eq);

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplEq(left, right);
        }

        static SymplCons ParseCons(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Cons);

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplCons(left, right);
        }

        /// <summary>
        /// Parses two exprs and a close paren, returning the two exprs.
        /// </summary>
        static void ParseBinaryRuntimeCall(Lexer lexer, out SymplExpression left, out SymplExpression right)
        {
            left = ParseExpression(lexer);
            right = ParseExpression(lexer);

            MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);
        }

        /// <summary>
        /// Parses a call to the List built-in keyword form that takes any number of arguments.
        /// </summary>
        static SymplListCall ParseListCall(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.List);

            return new SymplListCall(ParseBody(lexer, "Unexpected EOF in arg list for call to List."));
        }

        static SymplIf ParseIf(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.If);

            var args = ParseBody(lexer, "Unexpected EOF in If form.");
            return args.Length switch
            {
                2 => new SymplIf(args[0], args[1], null),
                3 => new SymplIf(args[0], args[1], args[2]),
                _ => throw new SymplParseException("IF must be (if <test> <consequent> [<alternative>]).")
            };
        }

        /// <summary>
        /// Parses a loop expression, a sequence of exprs to execute in order, forever. See Break for
        /// returning expression's value.
        /// </summary>
        static SymplLoop ParseLoop(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Loop);

            return new SymplLoop(ParseBody(lexer, "Unexpected EOF in Loop."));
        }

        /// <summary> Parses a Break expression, which has an optional value that becomes a loop
        /// expression's value. </summary>
        static SymplBreak ParseBreak(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Break);

            var token = lexer.GetToken();
            SymplExpression? value;
            if (token.IsSyntaxToken(SyntaxTokenKind.CloseParenthesis))
            {
                value = null;
            }
            else
            {
                lexer.PutToken(token);
                value = ParseExpression(lexer);
                MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);
            }

            return new SymplBreak(value);
        }

        /// <summary>
        /// Parse a New form for creating instances of types. Second sub expression (one after keyword New)
        /// evals to a type.
        /// </summary>
        /// <devdoc>
        /// Consider adding a new kwd form generic-type-args that could be the third sub expression and
        /// take any number of sub exprs that eval to types. These could be used to specific concrete
        /// generic type instances. Without this support SymPL programmers need to open code this as
        /// the examples show.
        /// </devdoc>
        static SymplNew ParseNew(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.New);

            return new SymplNew(ParseExpression(lexer), ParseBody(lexer, "Unexpected EOF in arg list for call to New."));
        }

        /// <summary>
        /// Parses pure list and atom structure.
        /// </summary>
        /// <devdoc>
        /// Atoms are IDs, strings, and nums. Need quoted form of dotted exprs, quote, etc., if want to
        /// have macros one day. This is used for Import name parsing, Defun/Lambda params, and
        /// quoted lists.
        /// </devdoc>
        static SymplList ParseList(Lexer lexer, String errStr)
        {
            MatchToken(lexer, SyntaxTokenKind.OpenParenthesis);

            var token = lexer.GetToken();
            var res = new List<Object>();
            while (token != SyntaxToken.Eof && !token.IsSyntaxToken(SyntaxTokenKind.CloseParenthesis))
            {
                lexer.PutToken(token);
                Object elt;

                switch (token)
                {
                    case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                        elt = ParseList(lexer, errStr);
                        break;
                    case IdOrKeywordToken _:
                    case LiteralToken _:
                        elt = token;
                        lexer.GetToken();
                        break;
                    case SyntaxToken { Kind: SyntaxTokenKind.Dot }:
                        throw new SymplParseException($"Can't have dotted syntax in {errStr}");
                    default:
                        throw new SymplParseException($"Unexpected token in list -- {token}");
                }

                if (elt is null)
                {
                    throw new SymplParseException("Internal: no next element in list?");
                }

                res.Add(elt);
                token = lexer.GetToken();
            }

            if (token == SyntaxToken.Eof)
            {
                throw new SymplParseException("Unexpected EOF encountered while parsing list.", ScriptCodeParseResult.IncompleteToken);
            }

            return new SymplList(res.ToArray());
        }

        static public SymplElt ParseElt(Lexer lexer)
        {
            MatchToken(lexer, SyntaxTokenKind.OpenParenthesis);
            MatchToken(lexer, KeywordTokenKind.Elt);

            return new SymplElt(ParseExpression(lexer), ParseBody(lexer, "Unexpected EOF in arg list for call to Elt."));
        }

        /// <summary>
        /// Parses a BinaryOp expression.
        /// </summary>
        static SymplBinary ParseBinaryExpression(Lexer lexer)
        {
            if (!(lexer.GetToken() is KeywordToken keyword))
                throw new SymplParseException("Internal error: parsing Binary?");

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplBinary(left, right, GetExpressionType(keyword));
        }

        /// <summary>
        /// Parses a UnaryOp expression.
        /// </summary>
        static SymplUnary ParseUnaryExpression(Lexer lexer)
        {
            if (!(lexer.GetToken() is KeywordToken keyword))
                throw new SymplParseException("Internal error: parsing Unary?");

            var op = GetExpressionType(keyword); 
            var operand = ParseExpression(lexer);

            MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);

            return new SymplUnary(operand, op);
        }

        static Token MatchToken(Lexer lexer, SyntaxTokenKind kind)
        {
            var token = lexer.GetToken();
            if (!token.IsSyntaxToken(kind))
                throw new SymplParseException($"Expected token {kind}.", ScriptCodeParseResult.IncompleteToken);

            return token;
        }

        static Token MatchToken(Lexer lexer, KeywordTokenKind kind)
        {
            var token = lexer.GetToken();
            if (!token.IsKeywordToken(kind))
                throw new SymplParseException($"Expected token {kind}.", ScriptCodeParseResult.IncompleteToken);

            return token;
        }

        /// <summary>
        /// Gets the <see cref="ExpressionType"/> for an operator.
        /// </summary>
        static ExpressionType GetExpressionType(KeywordToken keyword) => keyword.Kind switch
        {
            KeywordTokenKind.Add => ExpressionType.Add,
            KeywordTokenKind.Subtract => ExpressionType.Subtract,
            KeywordTokenKind.Multiply => ExpressionType.Multiply,
            KeywordTokenKind.Divide => ExpressionType.Divide,
            KeywordTokenKind.Equal => ExpressionType.Equal,
            KeywordTokenKind.NotEqual => ExpressionType.NotEqual,
            KeywordTokenKind.GreaterThan => ExpressionType.GreaterThan,
            KeywordTokenKind.LessThan => ExpressionType.LessThan,
            KeywordTokenKind.And => ExpressionType.And,
            KeywordTokenKind.Or => ExpressionType.Or,
            KeywordTokenKind.Not => ExpressionType.Not,
            _ => throw new SymplParseException("Unrecognized keyword for operators")
        };
    }
}