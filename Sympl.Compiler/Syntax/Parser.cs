using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using Microsoft.Scripting;
using Sympl.Expressions;
using Sympl.Runtime;

namespace Sympl.Syntax
{
    public class Parser
    {
        /// <summary>
        /// Returns an array of top-level expressions parsed in the <paramref name="reader"/>.
        /// </summary>
        public SymplExpression[] ParseFile(TextReader reader)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            var lexer = new Lexer(reader);
            var body = new List<SymplExpression>();
            var token = lexer.GetToken();
            while (token != SyntaxToken.EOF)
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
        public SymplExpression ParseSingleExpression(TextReader reader)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            return ParseExpression(new Lexer(reader));
        }

        /// <summary>
        /// Parses an expression from the Lexer passed in.
        /// </summary>  
        SymplExpression ParseExpression(Lexer lexer)
        {
            var token = lexer.GetToken();
            Debug.Assert(token is { });
            SymplExpression? res = null;
            if (token == SyntaxToken.EOF)
                throw new SymplParseException("Unexpected EOF encountered while parsing expression.", ScriptCodeParseResult.IncompleteToken);

            if (token == SyntaxToken.Quote)
            {
                lexer.PutToken(token);
                res = ParseQuoteExpression(lexer);
            }
            else if (token == SyntaxToken.Paren)
            {
                lexer.PutToken(token);
                res = ParseParentheticForm(lexer);
            }
            else
            {
                res = token switch
                {
                    // If we encounter literal kwd constants, they get turned into ID Exprs. Code that
                    // accepts Id Exprs, needs to check if the token is kwd or not when it matters.
                    IdOrKeywordToken keywordToken when keywordToken is KeywordToken && token != KeywordToken.Nil &&
                                                       token != KeywordToken.True &&
                                                       token != KeywordToken.False => throw new InvalidCastException(
                        "Keyword cannot be an expression"),
                    IdOrKeywordToken keywordToken => new SymplIdentifier(keywordToken),
                    LiteralToken literalToken => new SymplLiteral(literalToken.Value),
                    _ => res
                };
            }

            // check for dotted expression
            if (res is null)
                throw new SymplParseException(
                    $"Unexpected token when expecting beginning of expression -- {token} ... {lexer.GetToken()}{lexer.GetToken()}{lexer.GetToken()}{lexer.GetToken()}");
            
            var next = lexer.GetToken();
            lexer.PutToken(next);

            return next == SyntaxToken.Dot ? ParseDottedExpression(lexer, res) : res;
        }

        /// <summary>
        /// Parses a parenthetic form.
        /// </summary>
        /// <devdoc>
        /// If the first token after the paren is a keyword, then it something like defun, loop, if,
        /// try, etc. If the first sub expression is another parenthetic form, then it must be an
        /// expression that returns a callable object.
        /// </devdoc>
        SymplExpression ParseParentheticForm(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (token != SyntaxToken.Paren)
                throw new SymplParseException("List expression must start with '('.");

            token = lexer.GetToken();
            lexer.PutToken(token);

            return token is KeywordToken ? ParseKeywordForm(lexer) : ParseFunctionCall(lexer);
        }

        /// <summary>
        /// Parses parenthetic built in forms such as defun, if, loop, etc.
        /// </summary>
        SymplExpression ParseKeywordForm(Lexer lexer)
        {
            var name = lexer.GetToken();
            if (!(name is KeywordToken))
                throw new SymplParseException("Internal error: parsing keyword form?");

            lexer.PutToken(name);
            if (name == KeywordToken.Import)
                return ParseImport(lexer);
            if (name == KeywordToken.Defun)
                return ParseDefun(lexer);
            if (name == KeywordToken.Lambda)
                return ParseLambda(lexer);
            if (name == KeywordToken.Set)
                return ParseSet(lexer);
            if (name == KeywordToken.LetStar)
                return ParseLetStar(lexer);
            if (name == KeywordToken.Block)
                return ParseBlock(lexer);
            if (name == KeywordToken.Eq)
                return ParseEq(lexer);
            if (name == KeywordToken.Cons)
                return ParseCons(lexer);
            if (name == KeywordToken.List)
                return ParseListCall(lexer);
            if (name == KeywordToken.If)
                return ParseIf(lexer);
            if (name == KeywordToken.New)
                return ParseNew(lexer);
            if (name == KeywordToken.Loop)
                return ParseLoop(lexer);
            if (name == KeywordToken.Break)
                return ParseBreak(lexer);
            if (name == KeywordToken.Elt)
                return ParseElt(lexer);
            if (name == KeywordToken.Add || name == KeywordToken.Subtract || name == KeywordToken.Multiply || name == KeywordToken.Divide
                || name == KeywordToken.Equal || name == KeywordToken.NotEqual || name == KeywordToken.GreaterThan || name == KeywordToken.LessThan
                || name == KeywordToken.And || name == KeywordToken.Or)
                return ParseBinaryExpression(lexer);
            if (name == KeywordToken.Not)
                return ParseUnaryExpression(lexer);

            throw new SymplParseException("Internal: unrecognized keyword form?");
        }

        SymplExpression ParseDefun(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (token != KeywordToken.Defun)
                throw new SymplParseException("Internal: parsing Defun?");

            var name = lexer.GetToken() as IdOrKeywordToken;
            if (name == null || name.IsKeywordToken)
                throw new SymplParseException($"Defun must have an ID for name -- {name}");

            return new SymplDefun(name.Name, ParseParams(lexer, "Defun"), ParseBody(lexer, $"Hit EOF in function body{name.Name}"));
        }

        SymplExpression ParseLambda(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (token != KeywordToken.Lambda)
                throw new SymplParseException("Internal: parsing Lambda?");

            return new SymplLambda(ParseParams(lexer, "Lambda"), ParseBody(lexer, "Hit EOF in function body"));
        }

        /// <summary>
        /// Parses a sequence of vars for Defuns and Lambdas, and always returns a list of IdTokens.
        /// </summary>
        IdOrKeywordToken[] ParseParams(Lexer lexer, String definer)
        {
            var token = lexer.GetToken();
            if (token != SyntaxToken.Paren)
                throw new SymplParseException($"{definer} must have param list following name.");

            lexer.PutToken(token);
            return EnsureListOfIds(ParseList(lexer, "param list.").Elements, false, $"{definer} params must be valid IDs.");
        }

        /// <summary>
        /// Parses a sequence of expressions as for Defun, Let, etc., and always returns a list, even
        /// if empty. It gobbles the close paren too.
        /// </summary>
        SymplExpression[] ParseBody(Lexer lexer, String error)
        {
            var token = lexer.GetToken();
            var body = new List<SymplExpression>();

            for (; token != SyntaxToken.EOF && token != SyntaxToken.CloseParen; token = lexer.GetToken())
            {
                lexer.PutToken(token);
                body.Add(ParseExpression(lexer));
            }

            if (token == SyntaxToken.EOF)
                throw new SymplParseException(error, ScriptCodeParseResult.IncompleteToken);

            return body.ToArray();
        }

        // (import id[.id]* [{id | (id [id]*)} [{id | (id [id]*)}]] ) (import file-or-dotted-Ids
        // name-or-list-of-members reanme-or-list-of)
        SymplExpression ParseImport(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Import)
            {
                throw new SymplParseException("Internal: parsing Import call?");
            }

            var nsOrModule = ParseImportNameOrModule(lexer);
            var members = ParseImportNames(lexer, "member names", true);
            var asNames = ParseImportNames(lexer, "renames", false);

            if (members.Length != asNames.Length && asNames.Length != 0)
                throw new SymplParseException("Import as-names must be same form as member names.");

            if (lexer.GetToken() != SyntaxToken.CloseParen)
                throw new SymplParseException("Import must end with closing paren.", ScriptCodeParseResult.IncompleteToken);

            return new SymplImport(nsOrModule, members, asNames);
        }

        /// <summary>
        /// Parses dotted namespaces or <see cref="SymplRuntime" /> members to import.
        /// </summary>
        IdOrKeywordToken[] ParseImportNameOrModule(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (!(token is IdOrKeywordToken))
                // Keywords are ok here.
                throw new SymplParseException("Id must follow Import symbol");

            var dot = lexer.GetToken();
            var nsOrModule = new List<IdOrKeywordToken>();
            if (dot == SyntaxToken.Dot)
            {
                lexer.PutToken(dot);
                var tmp = ParseDottedExpression(lexer, new SymplIdentifier((IdOrKeywordToken) token));
                foreach (var e in tmp.Expressions)
                {
                    if (!(e is SymplIdentifier))
                    {
                        // Keywords are ok here.
                        throw new SymplParseException($"Import targets must be dotted identifiers.{e}{nsOrModule}");
                    }

                    nsOrModule.Add(((SymplIdentifier) e).IdToken);
                }

                token = lexer.GetToken();
            }
            else
            {
                nsOrModule.Add((IdOrKeywordToken) token);
                token = dot;
            }

            lexer.PutToken(token);
            return nsOrModule.ToArray();
        }

        /// <summary> Parses list of member names to import from the object represented in the result
        /// of <see cref="ParseImportNameOrModule(Lexer)" />, which will be a file module or object
        /// from <see cref="SymplRuntime.Globals" />. This is also used to parse the list of renames for
        /// these same members. </summary>
        IdOrKeywordToken[] ParseImportNames(Lexer lexer, String nameKinds, Boolean allowKeywords)
        {
            var token = lexer.GetToken();
            var names = new List<IdOrKeywordToken>();
            if (token is IdOrKeywordToken idToken)
            {
                if (!idToken.IsKeywordToken)
                {
                    names.Add(idToken);
                }
            }
            else if (token == SyntaxToken.Paren)
            {
                lexer.PutToken(token);
                var memberTokens = ParseList(lexer, $"Import {nameKinds}.").Elements;

                EnsureListOfIds(memberTokens, allowKeywords, $"Import {nameKinds} must be valid IDs.");
            }
            else if (token == SyntaxToken.CloseParen)
            {
                lexer.PutToken(token);
            }
            else
            {
                throw new SymplParseException("Import takes dotted names, then member vars.");
            }

            return names.ToArray();
        }

        static IdOrKeywordToken[] EnsureListOfIds(Object[] list, Boolean allowKeywords, String error)
        {
            foreach (var t in list)
                if (!(t is IdOrKeywordToken id) || !allowKeywords && id.IsKeywordToken)
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
        SymplDot ParseDottedExpression(Lexer lexer, SymplExpression objExpr)
        {
            if (lexer.GetToken() != SyntaxToken.Dot)
                throw new SymplParseException("Internal: parsing dotted expressions?");

            var exprs = new List<SymplExpression>();

            var token = lexer.GetToken();
            for (; token is IdOrKeywordToken || token == SyntaxToken.Paren; token = lexer.GetToken())
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
                if ((token = lexer.GetToken()) != SyntaxToken.Dot)
                    break;
            }

            lexer.PutToken(token);
            return new SymplDot(objExpr, exprs.ToArray());
        }

        /// <summary>
        /// Parses a LHS expression and value expression. All analysis on the LHS is in etgen.py.
        /// </summary>
        SymplAssignment ParseSet(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Set)
                throw new SymplParseException("Internal error: parsing Set?");

            var lhs = ParseExpression(lexer);
            var val = ParseExpression(lexer);
            if (lexer.GetToken() != SyntaxToken.CloseParen)
                throw new SymplParseException("Expected close paren for Set expression.", ScriptCodeParseResult.IncompleteToken);

            return new SymplAssignment(lhs, val);
        }

        /// <summary>
        /// Parses <c>(let* (([var] [expression])*) [body]).</c>
        /// </summary>
        SymplLetStar ParseLetStar(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.LetStar)
                throw new SymplParseException("Internal error: parsing Let?");

            if (lexer.GetToken() != SyntaxToken.Paren)
                throw new SymplParseException("Let expression has no bindings?  Missing '('.", ScriptCodeParseResult.IncompleteToken);

            // Get bindings
            var bindings = new List<SymplLetStar.LetBinding>();

            var token = lexer.GetToken();
            for (; token == SyntaxToken.Paren; token = lexer.GetToken())
            {
                var e = ParseExpression(lexer);
                if (!(e is SymplIdentifier id) || id.IdToken.IsKeywordToken)
                    throw new SymplParseException("Let binding must be (<ID> <expression>) -- ");

                var init = ParseExpression(lexer);
                bindings.Add(new SymplLetStar.LetBinding(id.IdToken, init));

                if (lexer.GetToken() != SyntaxToken.CloseParen)
                    throw new SymplParseException("Let binding missing close paren -- ", ScriptCodeParseResult.IncompleteToken);
            }

            if (token != SyntaxToken.CloseParen)
                throw new SymplParseException("Let bindings missing close paren.", ScriptCodeParseResult.IncompleteToken);

            return new SymplLetStar(bindings.ToArray(), ParseBody(lexer, "Unexpected EOF in Let."));
        }

        /// <summary>
        /// Parses a block expression, a sequence of exprs to execute in order, returning the last
        /// expression's value.
        /// </summary>
        SymplBlock ParseBlock(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Block)
                throw new SymplParseException("Internal error: parsing Block?");

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
        SymplCall ParseFunctionCall(Lexer lexer)
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
        SymplQuote ParseQuoteExpression(Lexer lexer)
        {
            if (lexer.GetToken() != SyntaxToken.Quote)
            {
                throw new SymplParseException("Internal: parsing Quote?.");
            }

            var token = lexer.GetToken();
            Object expression;

            if (token == SyntaxToken.Paren)
            {
                lexer.PutToken(token);
                expression = ParseList(lexer, "quoted list.");
            }
            else if (token is IdOrKeywordToken || token is LiteralToken)
            {
                expression = token;
            }
            else
            {
                throw new SymplParseException("Quoted expression can only be list, ID/Symbol, or literal.");
            }

            return new SymplQuote(expression);
        }

        SymplEq ParseEq(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Eq)
                throw new SymplParseException("Internal: parsing Eq?");

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplEq(left, right);
        }

        SymplCons ParseCons(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Cons)
                throw new SymplParseException("Internal: parsing Cons?");

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplCons(left, right);
        }

        /// <summary>
        /// Parses two exprs and a close paren, returning the two exprs.
        /// </summary>
        void ParseBinaryRuntimeCall(Lexer lexer, out SymplExpression left, out SymplExpression right)
        {
            left = ParseExpression(lexer);
            right = ParseExpression(lexer);
            if (lexer.GetToken() != SyntaxToken.CloseParen)
                throw new SymplParseException($"Expected close paren for Eq call.", ScriptCodeParseResult.IncompleteToken);
        }

        /// <summary>
        /// Parses a call to the List built-in keyword form that takes any number of arguments.
        /// </summary>
        SymplListCall ParseListCall(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.List)
                throw new SymplParseException("Internal: parsing List call?");

            return new SymplListCall(ParseBody(lexer, "Unexpected EOF in arg list for call to List."));
        }

        SymplIf ParseIf(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (token != KeywordToken.If)
                throw new SymplParseException("Internal: parsing If?");

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
        SymplLoop ParseLoop(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Loop)
                throw new SymplParseException("Internal error: parsing Loop?");

            return new SymplLoop(ParseBody(lexer, "Unexpected EOF in Loop."));
        }

        /// <summary> Parses a Break expression, which has an optional value that becomes a loop
        /// expression's value. </summary>
        SymplBreak ParseBreak(Lexer lexer)
        {
            if (lexer.GetToken() != KeywordToken.Break)
                throw new SymplParseException("Internal error: parsing Break?");

            var token = lexer.GetToken();
            SymplExpression? value;
            if (token == SyntaxToken.CloseParen)
            {
                value = null;
            }
            else
            {
                lexer.PutToken(token);
                value = ParseExpression(lexer);
                token = lexer.GetToken();
                if (token != SyntaxToken.CloseParen)
                    throw new SymplParseException("Break expression missing close paren.", ScriptCodeParseResult.IncompleteToken);
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
        SymplNew ParseNew(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (token != KeywordToken.New)
                throw new SymplParseException("Internal: parsing New?");

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
        SymplList ParseList(Lexer lexer, String errStr)
        {
            var token = lexer.GetToken();
            if (token != SyntaxToken.Paren)
                throw new SymplParseException("List expression must start with '('.");

            token = lexer.GetToken();
            var res = new List<Object>();
            while (token != SyntaxToken.EOF && token != SyntaxToken.CloseParen)
            {
                lexer.PutToken(token);
                Object elt;
                if (token == SyntaxToken.Paren)
                {
                    elt = ParseList(lexer, errStr);
                }
                else if (token is IdOrKeywordToken || token is LiteralToken)
                {
                    elt = token;
                    lexer.GetToken();
                }
                else if (token == SyntaxToken.Dot)
                {
                    throw new SymplParseException($"Can't have dotted syntax in {errStr}");
                }
                else
                {
                    throw new SymplParseException($"Unexpected token in list -- {token}");
                }

                if (elt is null)
                {
                    throw new SymplParseException("Internal: no next element in list?");
                }

                res.Add(elt);
                token = lexer.GetToken();
            }

            if (token == SyntaxToken.EOF)
            {
                throw new SymplParseException("Unexpected EOF encountered while parsing list.", ScriptCodeParseResult.IncompleteToken);
            }

            return new SymplList(res.ToArray());
        }

        public SymplElt ParseElt(Lexer lexer)
        {
            var token = lexer.GetToken();
            if (token != KeywordToken.Elt)
                throw new SymplParseException("Internal: parsing Elt?");

            return new SymplElt(ParseExpression(lexer), ParseBody(lexer, "Unexpected EOF in arg list for call to Elt."));
        }

        /// <summary>
        /// Parses a BinaryOp expression.
        /// </summary>
        SymplBinary ParseBinaryExpression(Lexer lexer)
        {
            if (!(lexer.GetToken() is KeywordToken keyword))
                throw new SymplParseException("Internal error: parsing Binary?");

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplBinary(left, right, GetExpressionType(keyword));
        }

        /// <summary>
        /// Parses a UnaryOp expression.
        /// </summary>
        SymplUnary ParseUnaryExpression(Lexer lexer)
        {
            if (!(lexer.GetToken() is KeywordToken keyword))
                throw new SymplParseException("Internal error: parsing Unary?");

            var op = GetExpressionType(keyword); 
            var operand = ParseExpression(lexer);
            if (lexer.GetToken() != SyntaxToken.CloseParen)
                throw new SymplParseException("Unary expression missing close paren.", ScriptCodeParseResult.IncompleteToken);

            return new SymplUnary(operand, op);
        }

        /// <summary>
        /// Gets the <see cref="ExpressionType"/> for an operator.
        /// </summary>
        static ExpressionType GetExpressionType(KeywordToken keyword)
        {
            if (keyword == KeywordToken.Add)
                return ExpressionType.Add;
            if (keyword == KeywordToken.Subtract)
                return ExpressionType.Subtract;
            if (keyword == KeywordToken.Multiply)
                return ExpressionType.Multiply;
            if (keyword == KeywordToken.Divide)
                return ExpressionType.Divide;
            if (keyword == KeywordToken.Equal)
                return ExpressionType.Equal;
            if (keyword == KeywordToken.NotEqual)
                return ExpressionType.NotEqual;
            if (keyword == KeywordToken.GreaterThan)
                return ExpressionType.GreaterThan;
            if (keyword == KeywordToken.LessThan)
                return ExpressionType.LessThan;
            if (keyword == KeywordToken.And)
                return ExpressionType.And;
            if (keyword == KeywordToken.Or)
                return ExpressionType.Or;
            if (keyword == KeywordToken.Not)
                return ExpressionType.Not;

            throw new SymplParseException("Unrecognized keyword for operators");
        }
    }
}