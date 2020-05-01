using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using Sympl.Expressions;
using Sympl.Hosting;
using Sympl.Runtime;

namespace Sympl.Syntax
{
    public class Parser
    {
        Lexer lexer;
        CompilerContext compilerContext;

        Token current;
        Token? peek;

        public Parser(CompilerContext context)
        {
            compilerContext = context;
            lexer = new Lexer(context);

            current = lexer.GetToken();
        }

        /// <summary>
        /// Returns an array of top-level expressions parsed in the <paramref name="reader"/>.
        /// </summary>
        public static SymplExpression[] ParseFile(CompilerContext compilerContext)
        {
            var parser = new Parser(compilerContext);
            var body = new List<SymplExpression>();

            while (parser.current is not SyntaxToken { Kind: SyntaxTokenKind.Eof })
            {
                body.Add(parser.ParseExpression());
            }

            return body.ToArray();
        }

        /// <summary>
        /// Returns a single expression parsed from the <paramref name="reader"/>.
        /// </summary>
        public static SymplExpression ParseSingleExpression(CompilerContext compilerContext)
        {
            var parser = new Parser(compilerContext);
            return parser.ParseExpression();
        }

        /// <summary>
        /// Parses an expression from the Lexer passed in.
        /// </summary>  
        SymplExpression ParseExpression()
        {
            SymplExpression res;
            switch (current)
            {
                case SyntaxToken { Kind: SyntaxTokenKind.Quote }:
                    res = ParseQuoteExpression();
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                    res = ParseParentheticForm();
                    break;
                case KeywordToken { Kind: not(KeywordTokenKind.Nil or KeywordTokenKind.True or KeywordTokenKind.False) } keywordToken:
                    res = new SymplIdentifier(keywordToken);
                    ReportDiagnostic("Keyword cannot be an identifier.", keywordToken.Location, 100);
                    break;
                case IdOrKeywordToken idOrKeywordToken:
                    NextToken();
                    res = new SymplIdentifier(idOrKeywordToken);
                    break;
                case LiteralToken literalToken:
                    NextToken();
                    res = new SymplLiteral(literalToken.Value, literalToken.Location);
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.Eof } eof:
                    NextToken();
                    res = new SymplIdentifier(new KeywordToken(KeywordTokenKind.Nil, eof.Location));
                    lexer.Context.SourceUnit.CodeProperties = ScriptCodeParseResult.Empty;
                    break;
                default:
                    NextToken();
                    res = new SymplIdentifier(MatchToken<IdOrKeywordToken>());
                    break;
            }

            return current is SyntaxToken { Kind: SyntaxTokenKind.Dot } ? ParseDottedExpression(res) : res;
        }

        /// <summary>
        /// Parses a parenthetic form.
        /// </summary>
        /// <devdoc>
        /// If the first token after the paren is a keyword, then it something like defun, loop, if,
        /// try, etc. If the first sub expression is another parenthetic form, then it must be an
        /// expression that returns a callable object.
        /// </devdoc>
        SymplExpression ParseParentheticForm()
        {
            return Peek() is KeywordToken ? ParseKeywordForm() : ParseFunctionCall();
        }

        /// <summary>
        /// Parses parenthetic built in forms such as defun, if, loop, etc.
        /// </summary>
        SymplExpression ParseKeywordForm() => ((KeywordToken) Peek()).Kind switch
        {
            KeywordTokenKind.Add or
            KeywordTokenKind.Subtract or
            KeywordTokenKind.Multiply or
            KeywordTokenKind.Divide or
            KeywordTokenKind.Equal or
            KeywordTokenKind.NotEqual or
            KeywordTokenKind.GreaterThan or
            KeywordTokenKind.LessThan or
            KeywordTokenKind.And or
            KeywordTokenKind.Or => ParseBinaryExpression(),
            KeywordTokenKind.Invalid => throw Assert.Unreachable,
            KeywordTokenKind.Import => ParseImport(),
            KeywordTokenKind.Defun => ParseDefun(),
            KeywordTokenKind.Lambda => ParseLambda(),
            KeywordTokenKind.Set => ParseSet(),
            KeywordTokenKind.LetStar => ParseLetStar(),
            KeywordTokenKind.Block => ParseBlock(),
            KeywordTokenKind.Eq => ParseEq(),
            KeywordTokenKind.Cons => ParseCons(),
            KeywordTokenKind.List => ParseListCall(),
            KeywordTokenKind.If => ParseIf(),
            KeywordTokenKind.New => ParseNew(),
            KeywordTokenKind.Loop => ParseLoop(),
            KeywordTokenKind.Break => ParseBreak(),
            KeywordTokenKind.Elt => ParseElt(),
            KeywordTokenKind.Not => ParseUnaryExpression(),
            _ => throw new InvalidOperationException("Internal: unrecognized keyword form?"),
        };

        SymplExpression ParseDefun()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Defun);

            var idOrKeyword = MatchToken<IdOrKeywordToken>();

            return new SymplDefun(idOrKeyword.Name, ParseParams("Defun"), ParseBody(out var close), From(open, close));
        }

        SymplExpression ParseLambda()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Lambda);
            return new SymplLambda(ParseParams("Lambda"), ParseBody(out var close), From(open, close));
        }

        /// <summary>
        /// Parses a sequence of vars for Defuns and Lambdas, and always returns a list of IdTokens.
        /// </summary>
        IdOrKeywordToken[] ParseParams(String definer)
        {
            return EnsureListOfIds(ParseList("param list.").Elements, false, $"{definer} params must be valid IDs.");
        }

        /// <summary>
        /// Parses a sequence of expressions as for Defun, Let, etc., and always returns a list, even
        /// if empty. It gobbles the close paren too.
        /// </summary>
        SymplExpression[] ParseBody(out SourceLocation closeParenthesis)
        {
            var body = new List<SymplExpression>();

            while (current is not SyntaxToken { Kind: SyntaxTokenKind.Eof or SyntaxTokenKind.CloseParenthesis })
            {
                body.Add(ParseExpression());
            }

            var close = MatchToken(SyntaxTokenKind.CloseParenthesis);

            closeParenthesis = close.Location.End;
            return body.ToArray();
        }

        // (import id[.id]* [{id | (id [id]*)} [{id | (id [id]*)}]] ) (import file-or-dotted-Ids
        // name-or-list-of-members reanme-or-list-of)
        SymplExpression ParseImport()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Import);

            var nsOrModule = ParseImportNameOrModule();
            var members = ParseImportNames("member names", true);
            var asNames = ParseImportNames("renames", false);

            var close = MatchToken(SyntaxTokenKind.CloseParenthesis);

            if (members.Length != asNames.Length && asNames.Length != 0)
                ReportDiagnostic("Import as-names must be same form as member names.", From(open, close), 100);

            return new SymplImport(nsOrModule, members, asNames, From(open, close));
        }

        /// <summary>
        /// Parses dotted namespaces or <see cref="CodeContext" /> members to import.
        /// </summary>
        IdOrKeywordToken[] ParseImportNameOrModule()
        {
            var idOrKeyword = MatchToken<IdOrKeywordToken>();

            var nsOrModule = new List<IdOrKeywordToken>();
            if (current is SyntaxToken { Kind: SyntaxTokenKind.Dot })
            {
                nsOrModule.Add(idOrKeyword);
                var tmp = ParseDottedExpression(new SymplIdentifier(idOrKeyword));
                foreach (var e in tmp.Expressions)
                {
                    // Keywords are ok here.
                    if (e is SymplIdentifier id)
                        nsOrModule.Add(id.IdToken);
                    else
                        ReportDiagnostic("Import targets must be dotted identifiers.", e.Location, 100);
                }

            }
            else
            {
                nsOrModule.Add(idOrKeyword);
            }

            return nsOrModule.ToArray();
        }

        /// <summary> Parses list of member names to import from the object represented in the result
        /// of <see cref="ParseImportNameOrModule()" />, which will be a file module or object
        /// from <see cref="CodeContext.Globals" />. This is also used to parse the list of renames for
        /// these same members. </summary>
        IdOrKeywordToken[] ParseImportNames(String nameKinds, Boolean allowKeywords)
        {
            var names = new List<IdOrKeywordToken>();

            switch (current)
            {
                case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                    var memberTokens = ParseList($"Import {nameKinds}.").Elements;

                    EnsureListOfIds(memberTokens, allowKeywords, $"Import {nameKinds} must be valid IDs.");
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis }:
                    break;
                default:
                    var idToken = MatchToken<IdOrKeywordToken>();
                    names.Add(idToken);
                    break;
            }

            return names.ToArray();
        }

        IdOrKeywordToken[] EnsureListOfIds(Object[] list, Boolean allowKeywords, String error)
        {
            foreach (var o in list)
                // if t is not an id or keyword, or id is a keyword token when it's not allowed
                if (o is not IdOrKeywordToken || (o is KeywordToken && !allowKeywords))
                    ReportDiagnostic(error, o switch
                    {
                        Token t => t.Location,
                        SymplExpression e => e.Location,
                        _ => throw Assert.Unreachable
                    }, 100);

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
        SymplDot ParseDottedExpression(SymplExpression objExpr)
        {
            var exprs = new List<SymplExpression>();

            while (current is SyntaxToken { Kind: SyntaxTokenKind.Dot })
            {
                NextToken();
                // Needs to be fun call or IDs
                SymplExpression expression;
                if (current is IdOrKeywordToken keywordToken)
                {
                    NextToken();
                    // Keywords are ok as member names.
                    expression = new SymplIdentifier(keywordToken);
                }
                else
                {
                    expression = ParseParentheticForm();
                    // if (!(expression is SymplCall { Function: SymplIdentifier _ }) && !(expression is SymplIdentifier))
                    if (expression is not SymplCall { Function: SymplIdentifier }
                    or SymplIdentifier)
                        ReportDiagnostic("Dotted expressions must be identifiers or function calls with identifiers as the function value.", expression.Location, 100);

                }
                exprs.Add(expression);
            }

            return new SymplDot(objExpr, exprs.ToArray(), new SourceSpan(objExpr.Location.Start, current.Location.End));
        }

        /// <summary>
        /// Parses a LHS expression and value expression.
        /// </summary>
        SymplSet ParseSet()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Set);

            var lhs = ParseExpression();
            var val = ParseExpression();

            var closeParen = MatchToken(SyntaxTokenKind.CloseParenthesis);

            return new SymplSet(lhs, val, From(open, closeParen));
        }

        /// <summary>
        /// Parses <c>(let* (([var] [expression])*) [body]).</c>
        /// </summary>
        SymplLetStar ParseLetStar()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.LetStar);

            // Get bindings
            var bindings = new List<SymplLetStar.LetBinding>();

            MatchToken(SyntaxTokenKind.OpenParenthesis);

            while (current is SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis } o)
            {
                var body = ParseBody(out var end);

                switch (body.Length)
                {
                    case 0:
                    case 1:
                        ReportDiagnostic("Let binding must be (<id> <expression>)", From(o, end), 100);
                        break;
                    default:
                        ReportDiagnostic("Let binding must be (<id> <expression>)", From(o, end), 100);
                        goto case 2;
                    case 2:
                        if (body[0] is SymplIdentifier id)
                            bindings.Add(new SymplLetStar.LetBinding(id.IdToken, body[1]));
                        else
                            ReportDiagnostic("Let binding must be (<id> <expression>)", From(o, end), 100);
                        break;
                }
            }

            return new SymplLetStar(bindings.ToArray(), ParseBody(out var close), From(open, close));
        }

        /// <summary>
        /// Parses a block expression, a sequence of exprs to execute in order, returning the last
        /// expression's value.
        /// </summary>
        SymplBlock ParseBlock()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Block);

            return new SymplBlock(ParseBody(out var close), From(open, close));
        }

        /// <devdoc>
        /// First sub form must be expression resulting in callable, but if it is dotted expression, then eval
        /// the first N-1 dotted exprs and use invoke member or get member on last of dotted exprs so
        /// that the 2..N sub forms are the arguments to the invoke member. It's as if the call
        /// breaks into a block of a temp assigned to the N-1 dotted exprs followed by an invoke
        /// member (or a get member and call, which the runtime binder decides). The non-dotted expression
        /// simply evals to an object that better be callable with the supplied args, which may be none.
        /// </devdoc>
        SymplCall ParseFunctionCall()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            // First sub expression is callable object or invoke member expression.
            var fun = ParseExpression();
            if (fun is SymplDot dottedExpr && dottedExpr.Expressions[^1] is not SymplIdentifier)
                ReportDiagnostic("Function call with dotted expression for function must end with ID expression, not member invoke.", From(open, fun.Location.End), 100);

            // Tail exprs are args.
            return new SymplCall(fun, ParseBody(out var close), From(open, close));
        }

        /// <summary>
        /// Parses a quoted list, ID/keyword, or literal.
        /// </summary>
        SymplQuote ParseQuoteExpression()
        {
            var quote = MatchToken(SyntaxTokenKind.Quote);

            Object expression;

            switch (current)
            {
                case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                    expression = ParseList("quoted list.");
                    break;
                case IdOrKeywordToken _:
                case LiteralToken _:
                    expression = current;
                    NextToken();
                    break;
                default:
                    // The expression tree generator would not be invoked in a scenario where this is null.
                    expression = null!;
                    ReportDiagnostic("Quoted expression can only be list, ID/Symbol, or literal.", current.Location, 100);
                    NextToken();
                    break;
            }

            return new SymplQuote(expression, From(quote, current));
        }

        SymplEq ParseEq()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Eq);

            ParseBinaryRuntimeCall(out var left, out var right, out var close);
            return new SymplEq(left, right, From(open, close));
        }

        SymplCons ParseCons()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Cons);

            ParseBinaryRuntimeCall(out var left, out var right, out var close);
            return new SymplCons(left, right, From(open, close));
        }

        /// <summary>
        /// Parses two exprs and a close paren, returning the two exprs.
        /// </summary>
        void ParseBinaryRuntimeCall(out SymplExpression left, out SymplExpression right, out SourceLocation closeParenthesis)
        {
            left = ParseExpression();
            right = ParseExpression();

            var closeParen = MatchToken(SyntaxTokenKind.CloseParenthesis);
            closeParenthesis = closeParen.Location.End;
        }

        /// <summary>
        /// Parses a call to the List built-in keyword form that takes any number of arguments.
        /// </summary>
        SymplListCall ParseListCall()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.List);

            return new SymplListCall(ParseBody(out var close), From(open, close));
        }

        SymplIf ParseIf()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            var token = MatchToken(KeywordTokenKind.If);

            var args = ParseBody(out var close);

            if (args.Length != 2 && args.Length != 3)
            {
                ReportDiagnostic("If must be (if <test> <consequent> [<alternative>]).", From(open, close), 100);
            }

            return args.Length switch
            {
                // The expression tree generator would not be invoked in a scenario where these are null.
                0 => new SymplIf(null!, null!, null, From(open, close)),
                1 => new SymplIf(args[0], null!, null, From(open, close)),
                2 => new SymplIf(args[0], args[1], null, From(open, close)),
                _ => new SymplIf(args[0], args[1], args[2], From(open, close))
            };
        }

        /// <summary>
        /// Parses a loop expression, a sequence of exprs to execute in order, forever. See Break for
        /// returning expression's value.
        /// </summary>
        SymplLoop ParseLoop()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Loop);

            return new SymplLoop(ParseBody(out var close), From(open, close));
        }

        /// <summary> Parses a Break expression, which has an optional value that becomes a loop
        /// expression's value. </summary>
        SymplBreak ParseBreak()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Break);

            SymplExpression? value;
            if (current is SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis })
            {
                value = null;
            }
            else
            {
                value = ParseExpression();
            }

            return new SymplBreak(value, From(open, current));
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
        SymplNew ParseNew()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.New);

            return new SymplNew(ParseExpression(), ParseBody(out var close), From(open, close));
        }

        /// <summary>
        /// Parses pure list and atom structure.
        /// </summary>
        /// <devdoc>
        /// Atoms are IDs, strings, and nums. Need quoted form of dotted exprs, quote, etc., if want to
        /// have macros one day. This is used for Import name parsing, Defun/Lambda params, and
        /// quoted lists.
        /// </devdoc>
        SymplList ParseList(String errStr)
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);

            var res = new List<Object>();
            // while (!(current is SyntaxToken { Kind: SyntaxTokenKind.Eof }) && !(current is SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis }))
            while (current is not SyntaxToken { Kind: SyntaxTokenKind.Eof or SyntaxTokenKind.CloseParenthesis })
            {
                Object? elt = null;

                switch (current)
                {
                    case SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }:
                        elt = ParseList(errStr);
                        break;
                    case IdOrKeywordToken _:
                    case LiteralToken _:
                        elt = NextToken();
                        break;
                    case SyntaxToken { Kind: SyntaxTokenKind.Dot }:
                        ReportDiagnostic($"Can't have dotted tyntax in {errStr}", current.Location, 100);
                        NextToken();
                        break;
                    default:
                        ReportDiagnostic($"Unexpected token in list -- {current}", current, 100);
                        NextToken();
                        break;
                }

                if (elt is null) break;
                res.Add(elt);
            }

            if (current is SyntaxToken { Kind: SyntaxTokenKind.Eof })
            {
                ReportDiagnostic("Unexpected EOF encountered while parsing list.", From(open, current), 100, Severity.Error);
            }

            var close = MatchToken(SyntaxTokenKind.CloseParenthesis);

            return new SymplList(res.ToArray(), From(open, close));
        }

        public SymplElt ParseElt()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            MatchToken(KeywordTokenKind.Elt);

            return new SymplElt(ParseExpression(), ParseBody(out var close), From(open, close));
        }

        /// <summary>
        /// Parses a BinaryOp expression.
        /// </summary>
        SymplBinary ParseBinaryExpression()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            var keyword = MatchToken<KeywordToken>();

            ParseBinaryRuntimeCall(out var left, out var right, out var close);
            return new SymplBinary(left, right, GetExpressionType(keyword), From(open, close));
        }

        /// <summary>
        /// Parses a UnaryOp expression.
        /// </summary>
        SymplUnary ParseUnaryExpression()
        {
            var open = MatchToken(SyntaxTokenKind.OpenParenthesis);
            var keyword = MatchToken<KeywordToken>();

            var op = GetExpressionType(keyword);
            var operand = ParseExpression();

            var close = MatchToken(SyntaxTokenKind.CloseParenthesis);

            return new SymplUnary(operand, op, From(open, close));
        }

        String AppendStackTrace(String diagnostic) => ((SymplCompilerOptions) lexer.Context.Options).ShowStackTrace
                ? diagnostic + Environment.NewLine + Environment.StackTrace
                : diagnostic;


        SourceSpan From(Token left, SourceLocation right) => new SourceSpan(left.Location.Start, right);

        SourceSpan From(Token left, Token right) => new SourceSpan(left.Location.Start, right.Location.End);

        void ReportDiagnostic(String diagnostic, SourceSpan span, int errorCode, Severity severity = Severity.FatalError)
        {
            lexer.Context.Errors.Add(lexer.Context.SourceUnit, AppendStackTrace(diagnostic), span, errorCode, severity);
        }

        void ReportDiagnostic(String diagnostic, Token token, int errorCode, Severity severity = Severity.FatalError)
        {
            lexer.Context.Errors.Add(lexer.Context.SourceUnit, AppendStackTrace(diagnostic), token.Location, errorCode, severity);
        }

        Token NextToken()
        {
            var current = this.current;
            this.current = peek ?? lexer.GetToken();

            if (peek is { })
                peek = null;

            return current;
        }

        Token Peek()
        {
            peek ??= lexer.GetToken();
            return peek;
        }

        SyntaxToken MatchToken(SyntaxTokenKind kind)
        {
            if (current is SyntaxToken token && token.Kind == kind)
            {
                return (SyntaxToken) NextToken();
            }
            else
            {
                ReportDiagnostic($"{SyntaxFacts.GetString(kind)} expected", current, 1000, current is SyntaxToken { Kind: SyntaxTokenKind.Eof } ? Severity.Error : Severity.FatalError);
                return new SyntaxToken(kind, current.Location);
            }
        }

        KeywordToken MatchToken(KeywordTokenKind kind)
        {
            if (current is KeywordToken token && token.Kind == kind)
            {
                return (KeywordToken) NextToken();
            }
            else
            {
                ReportDiagnostic($"{SyntaxFacts.GetString(kind)} expected", current, 1000, current is SyntaxToken { Kind: SyntaxTokenKind.Eof } ? Severity.Error : Severity.FatalError);
                return new KeywordToken(kind, current.Location);
            }
        }

        TToken MatchToken<TToken>()
            where TToken : Token
        {
            if (current.GetType() == typeof(TToken))
            {
                return (TToken) NextToken();
            }
            else
            {
                ReportDiagnostic($"{SyntaxFacts.GetString(typeof(TToken))} expected", current, 100, current is SyntaxToken { Kind: SyntaxTokenKind.Eof } ? Severity.Error : Severity.FatalError);
                return (TToken) Activator.CreateInstance(typeof(TToken), current.Location)!;
            }
        }

        /// <summary>
        /// Gets the <see cref="ExpressionType"/> for an operator.
        /// </summary>
        ExpressionType GetExpressionType(KeywordToken keyword) => keyword.Kind switch
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
            KeywordTokenKind.Invalid => throw Assert.Unreachable,
            _ => throw new ArgumentException("Unrecognized keyword for operators", nameof(keyword))
        };
    }
}