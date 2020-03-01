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
    public static class Parser
    {
        /// <summary>
        /// Returns an array of top-level expressions parsed in the <paramref name="reader"/>.
        /// </summary>
        public static SymplExpression[] ParseFile(CompilerContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            var lexer = new Lexer(context);
            var body = new List<SymplExpression>();
            var token = lexer.GetToken();
            do
            {
                lexer.PutToken(token);
                body.Add(ParseExpression(lexer));
                token = lexer.GetToken();
            } while (token != SyntaxToken.Eof);

            return body.ToArray();
        }

        /// <summary>
        /// Returns a single expression parsed from the <paramref name="reader"/>.
        /// </summary>
        public static SymplExpression ParseSingleExpression(CompilerContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return ParseExpression(new Lexer(context));
        }

        /// <summary>
        /// Parses an expression from the Lexer passed in.
        /// </summary>  
        static SymplExpression ParseExpression(Lexer lexer)
        {
            var token = lexer.GetToken();

            SymplExpression res;
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
                    res = new SymplIdentifier(keywordToken);
                    ReportDiagnostic(lexer, "Keyword cannot be an identifier.", keywordToken.Location, 100);
                    break;
                case IdOrKeywordToken idOrKeywordToken:
                    res = new SymplIdentifier(idOrKeywordToken);
                    break;
                case LiteralToken literalToken:
                    res = new SymplLiteral(literalToken.Value);
                    break;
                case var eof when eof == SyntaxToken.Eof:
                    res = new SymplIdentifier(new KeywordToken(KeywordTokenKind.Nil, SourceSpan.None));
                    lexer.Context.SourceUnit.CodeProperties = ScriptCodeParseResult.Empty;
                    break;
                default:
                    lexer.PutToken(token);
                    res = new SymplIdentifier(MatchToken<IdOrKeywordToken>(lexer));
                    break;
            }
            
            var next = lexer.GetToken();
            lexer.PutToken(next);

            return next is SyntaxToken { Kind: SyntaxTokenKind.Dot } ? ParseDottedExpression(lexer, res) : res;
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
            var keyword = MatchToken<KeywordToken>(lexer);

            lexer.PutToken(keyword);

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
                _ => throw new InvalidOperationException("Internal: unrecognized keyword form?"),
            };
        }

        static SymplExpression ParseDefun(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Defun);

            var idOrKeyword = MatchToken<IdOrKeywordToken>(lexer);

            if (idOrKeyword is KeywordToken)
                ReportDiagnostic(lexer, "Defun must not have a keyword for name.", idOrKeyword, 100);

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
            return EnsureListOfIds(lexer, ParseList(lexer, "param list.").Elements, false, $"{definer} params must be valid IDs.");    
        }

        /// <summary>
        /// Parses a sequence of expressions as for Defun, Let, etc., and always returns a list, even
        /// if empty. It gobbles the close paren too.
        /// </summary>
        static SymplExpression[] ParseBody(Lexer lexer, String error)
        {
            var token = lexer.GetToken();
            var body = new List<SymplExpression>();

            for (; token != SyntaxToken.Eof && !(token is SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis }); token = lexer.GetToken())
            {
                lexer.PutToken(token);
                body.Add(ParseExpression(lexer));
            }

            if (token == SyntaxToken.Eof)
                ReportDiagnostic(lexer, error, SourceSpan.None, 100, Severity.Error);

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
                ReportDiagnostic(lexer, "Import as-names must be same form as member names.", SourceSpan.None, 100);

            MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);

            return new SymplImport(nsOrModule, members, asNames);
        }

        /// <summary>
        /// Parses dotted namespaces or <see cref="CodeContext" /> members to import.
        /// </summary>
        static IdOrKeywordToken[] ParseImportNameOrModule(Lexer lexer)
        {
            var idOrKeyword = MatchToken<IdOrKeywordToken>(lexer);

            Token token;
            var dot = lexer.GetToken();
            var nsOrModule = new List<IdOrKeywordToken>();
            if (dot is SyntaxToken { Kind: SyntaxTokenKind.Dot })
            {
                nsOrModule.Add(idOrKeyword);
                lexer.PutToken(dot);
                var tmp = ParseDottedExpression(lexer, new SymplIdentifier(idOrKeyword));
                foreach (var e in tmp.Expressions)
                {
                    // Keywords are ok here.
                    if (e is SymplIdentifier id)
                    {
                        nsOrModule.Add(id.IdToken);
                    }
                    else
                    {
                        ReportDiagnostic(lexer, "Import targets must be dotted identifiers.", SourceSpan.None, 100);
                    }
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

                    EnsureListOfIds(lexer, memberTokens, allowKeywords, $"Import {nameKinds} must be valid IDs.");
                    break;
                case SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis }:
                    lexer.PutToken(token);
                    break;
                case var eof when eof == SyntaxToken.Eof:
                    MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);
                    break;
                default:
                    ReportDiagnostic(lexer, "Import takes dotted names, then member vars.", SourceSpan.None, 100);
                    break;
            }

            return names.ToArray();
        }

        static IdOrKeywordToken[] EnsureListOfIds(Lexer lexer, Object[] list, Boolean allowKeywords, String error)
        {
            foreach (var t in list)

                // if t is not an id or keyword, or id is a keyword token when it's not allowed
                if (!(t is IdOrKeywordToken id) || !allowKeywords && id is KeywordToken)
                    ReportDiagnostic(lexer, error, SourceSpan.None, 100);

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
            for (; token is IdOrKeywordToken || token is SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }; token = lexer.GetToken())
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
                        ReportDiagnostic(lexer, "Dotted expressions must be identifiers or function calls with identifiers as the function value", SourceSpan.None, 100);
                }

                exprs.Add(expression);

                if (!((token = lexer.GetToken()) is SyntaxToken { Kind: SyntaxTokenKind.Dot }))
                    break;
            }

            lexer.PutToken(token);
            return new SymplDot(objExpr, exprs.ToArray());
        }

        /// <summary>
        /// Parses a LHS expression and value expression.
        /// </summary>
        static SymplAssignment ParseSet(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Set);

            var lhs = ParseExpression(lexer);
            var val = ParseExpression(lexer);

            MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);

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
            for (; token is SyntaxToken { Kind: SyntaxTokenKind.OpenParenthesis }; token = lexer.GetToken())
            {
                var e = ParseExpression(lexer);
                if (!(e is SymplIdentifier id) || id.IdToken is KeywordToken)
                {
                    // TODO: Add source span to expression
                    ReportDiagnostic(lexer, "Let binding must be (<id> <expression>)", SourceSpan.None, 100);
                }
                else
                {
                    var init = ParseExpression(lexer);
                    bindings.Add(new SymplLetStar.LetBinding(id.IdToken, init));
                }

                MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);
            }

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
            if (fun is SymplDot dottedExpr && !(dottedExpr.Expressions[^1] is SymplIdentifier))
                ReportDiagnostic(lexer, "Function call with dotted expression for function must end with ID expression, not member invoke.", SourceSpan.None, 100);

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
            Object? expression = null;

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
                    ReportDiagnostic(lexer, "Quoted expression can only be list, ID/Symbol, or literal.", SourceSpan.None, 100);
                    break;
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
            var token = MatchToken(lexer, KeywordTokenKind.If);

            var args = ParseBody(lexer, "Unexpected EOF in If form.");

            if (args.Length != 2 && args.Length != 3)
            {
                ReportDiagnostic(lexer, "IF must be (if <test> <consequent> [<alternative>]).", token.Location, 100);
            }

            return args.Length switch
            {
                0 => new SymplIf(null, null, null),
                1 => new SymplIf(args[0], null, null),
                2 => new SymplIf(args[0], args[1], null),
                _ => new SymplIf(args[0], args[1], args[2]),
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
            if (token is SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis })
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
            while (token != SyntaxToken.Eof && !(token is SyntaxToken { Kind: SyntaxTokenKind.CloseParenthesis }))
            {
                lexer.PutToken(token);
                Object? elt = null;

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
                        ReportDiagnostic(lexer, $"Can't have dotted tyntax in {errStr}", token.Location, 100);
                        break;
                    default:
                        ReportDiagnostic(lexer, $"Unexpected token in list -- {token}", token, 100);
                        break;
                }

                if (elt is null) break;

                res.Add(elt);
                token = lexer.GetToken();
            }

            if (token == SyntaxToken.Eof)
            {
                ReportDiagnostic(lexer, "Unexpected EOF encountered while parsing list.", SourceSpan.None, 100, Severity.Error);
            }

            return new SymplList(res.ToArray());
        }

        static public SymplElt ParseElt(Lexer lexer)
        {
            MatchToken(lexer, KeywordTokenKind.Elt);

            return new SymplElt(ParseExpression(lexer), ParseBody(lexer, "Unexpected EOF in arg list for call to Elt."));
        }

        /// <summary>
        /// Parses a BinaryOp expression.
        /// </summary>
        static SymplBinary ParseBinaryExpression(Lexer lexer)
        {
            var keyword = MatchToken<KeywordToken>(lexer);

            ParseBinaryRuntimeCall(lexer, out var left, out var right);
            return new SymplBinary(left, right, GetExpressionType(keyword));
        }

        /// <summary>
        /// Parses a UnaryOp expression.
        /// </summary>
        static SymplUnary ParseUnaryExpression(Lexer lexer)
        {
            var keyword = MatchToken<KeywordToken>(lexer);

            var op = GetExpressionType(keyword); 
            var operand = ParseExpression(lexer);

            MatchToken(lexer, SyntaxTokenKind.CloseParenthesis);

            return new SymplUnary(operand, op);
        }

        static String AppendStackTrace(Lexer lexer, String diagnostic) => ((SymplCompilerOptions) lexer.Context.Options).ShowStackTrace
                ? diagnostic + Environment.NewLine + Environment.StackTrace
                : diagnostic;


        static void ReportDiagnostic(Lexer lexer, String diagnostic, SourceSpan span, int errorCode, Severity severity = Severity.FatalError)
        {
            lexer.Context.Errors.Add(lexer.Context.SourceUnit, AppendStackTrace(lexer, diagnostic), span, errorCode, severity);
        }

        static void ReportDiagnostic(Lexer lexer, String diagnostic, Token token, int errorCode, Severity severity = Severity.FatalError)
        {
            lexer.Context.Errors.Add(lexer.Context.SourceUnit, AppendStackTrace(lexer, diagnostic), token.Location, errorCode, severity);
        }

        static SyntaxToken MatchToken(Lexer lexer, SyntaxTokenKind kind)
        {
            var token = lexer.GetToken();

            if (token is SyntaxToken t && t.Kind == kind)
            {
                return t;
            }
            else
            {
                lexer.PutToken(token);
                ReportDiagnostic(lexer, $"Expected token {kind}, instead got token {token}.", token, 1000, token == SyntaxToken.Eof ? Severity.Error : Severity.FatalError);
                return new SyntaxToken(kind, token.Location);
            }
        }

        static KeywordToken MatchToken(Lexer lexer, KeywordTokenKind kind)
        {
            var token = lexer.GetToken();

            if (token is KeywordToken t && t.Kind == kind)
            {
                return t;
            }
            else
            {
                lexer.PutToken(token);
                ReportDiagnostic(lexer, $"Expected token {kind}, instead got token {token}.", token, 1000, token == SyntaxToken.Eof ? Severity.Error : Severity.FatalError);
                return new KeywordToken(kind, token.Location);
            }
        }

        static TToken MatchToken<TToken>(Lexer lexer)
            where TToken : Token
        {
            var token = lexer.GetToken();
            if (token is TToken t)
            {
                return t;
            }
            else
            {
                lexer.PutToken(token);
                var func = constructors.GetOrAdd(typeof(TToken), (type) =>
                {
                    var d = new DynamicMethod($"Create{type.Name}", type, new[] { typeof(SourceSpan) });
                    var il = d.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Newobj, typeof(TToken).GetConstructor(new[] { typeof(SourceSpan) })
                        ?? throw new ArgumentException($"Type {type.Name} does not have SourceSpan accepting constructor."));
                    il.Emit(OpCodes.Ret);

                    return (Func<SourceSpan, TToken>) d.CreateDelegate(typeof(Func<SourceSpan, TToken>));
                });

                ReportDiagnostic(lexer, $"Expected token {typeof(TToken).Name}, instead got token {token}.", token, 100, token == SyntaxToken.Eof ? Severity.Error : Severity.FatalError);

                return (TToken) func(token.Location);
            }
        }

        static ConcurrentDictionary<Type, Func<SourceSpan, Object>> constructors = new ConcurrentDictionary<Type, Func<SourceSpan, object>>();

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
            KeywordTokenKind.Invalid => throw Assert.Unreachable,
            _ => throw new ArgumentException("Unrecognized keyword for operators", nameof(keyword))
        };
    }
}