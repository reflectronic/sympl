using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace Sympl.Syntax
{
    public class Lexer
    {
        readonly Queue<Token> putTokens = new Queue<Token>(2);
        readonly TokenizerBuffer reader;
        readonly SourceCodeReader sourceCode;

        public CompilerContext Context { get; }

        const Char EOF = unchecked((Char) (-1));

        public Lexer(CompilerContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            Context = context;
            sourceCode = context.SourceUnit.GetReader();
            reader = new TokenizerBuffer(sourceCode, new SourceLocation(0, 1, 1), 4, true);
        }

        public void PutToken(Token token)
        {
            putTokens.Enqueue(token);
        }
        /// <summary>
        /// Returns any saved token, else skips whitespace and returns next token from input stream.
        /// </summary>
        /// <remarks>
        /// If returning token directly based on char, need to gobble char, but if calling helper
        /// function to read more, then they gobble as needed.
        /// </remarks>
        public Token GetToken()
        {
            if (putTokens.TryDequeue(out var putToken))
            {
                return putToken;
            }

            SkipWhitespace();
            var ch = (Char) reader.Peek();

            if (ch != EOF) { reader.DiscardToken(); }

            switch (ch)
            {
                case EOF:
                    return new SyntaxToken(SyntaxTokenKind.Eof, reader.TokenSpan);
                case '(':
                    reader.Read();
                    reader.MarkSingleLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.OpenParenthesis, reader.TokenSpan);
                case ')':
                    reader.Read();
                    reader.MarkSingleLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.CloseParenthesis, reader.TokenSpan);
                case '.':
                    reader.Read();
                    reader.MarkSingleLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.Dot, reader.TokenSpan);
                case '\'':
                    reader.Read();
                    reader.MarkSingleLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.Quote, reader.TokenSpan);
                case var _ when IsNumChar(ch):
                    return GetNumber();
                case '"':
                    return GetString();
                case '-':
                    return GetIdOrNumber();
                case var _ when StartsId(ch):
                    return GetIdOrKeyword();
                default:
                    throw new InvalidOperationException("Internal: couldn't get token?");
            }
        }

        /// <devdoc>Expects a hyphen as the current char, and returns an Id or number token.</devdoc>
        Token GetIdOrNumber()
        {
            var ch = (Char) reader.Read();
            if (ch == EOF || ch != '-')
            {
                throw new InvalidOperationException("Internal: parsing ID or number without hyphen.");
            }

            ch = (Char) reader.Peek();
            if (IsNumChar(ch))
            {
                var token = GetNumber();
                return new NumberToken(-(Double) token.Value, reader.TokenSpan);
            }
            else if (!IsIdTerminator(ch))
            {
                return GetIdOrKeyword();
            }
            else
            {
                return MakeIdOrKeywordToken(false);
            }
        }

        IdOrKeywordToken GetIdOrKeyword()
        {
            var first = (Char) reader.Read();
            var isQuoted = false;
            if (first == EOF || !StartsId(first))
            {
                throw new InvalidOperationException("Internal: getting Id or keyword?");
            }

            if (first == '\\')
            {
                isQuoted = true;
                reader.Read();
                reader.DiscardToken();
            }

            while (!IsIdTerminator((Char) reader.Peek())) { reader.Read(); }

            return MakeIdOrKeywordToken(isQuoted);

        }

        /// <devdoc>
        /// Keep whatever casing we found in the source program so that when the IDs are used a
        /// member names and metadata on binders, then if some MO doesn't respect the IgnoreCase
        /// flag, there's an out for still binding.
        /// </devdoc>
        IdOrKeywordToken MakeIdOrKeywordToken(Boolean isQuoted)
        {
            reader.MarkSingleLineTokenEnd();
            var name = reader.GetTokenString();
            if (!isQuoted && KeywordToken.KeywordTypes.ContainsKey(name))
            {
                return KeywordToken.MakeKeywordToken(name, reader.TokenSpan);
            }
            else
            {
                if (name.Equals("let", StringComparison.OrdinalIgnoreCase))
                {
                    Context.Errors.Add(Context.SourceUnit, "Using 'let'? You probably meant 'let*'.", reader.TokenSpan, 200, Severity.Warning);
                }

                return new IdOrKeywordToken(name, reader.TokenSpan);
            }
        }

        /// <devdoc>Must not be called on EOF</devdoc>
        static Boolean StartsId(Char c) => c == '\\' || !IsIdTerminator(c);

        /// <devdoc>
        /// Restrict macro syntax chars in case try to add macros later, but need to allow backquote
        /// in IDs to support .NET type names that come from reflection. We can fix this later with
        /// more machinery around type names.
        /// </devdoc>
        static readonly Char[] IdTerminators = { '(', ')', '"', ';', ',', /*'`',*/ '@', '\'', '.' };

        static Boolean IsIdTerminator(Char c) => Array.IndexOf(IdTerminators, c) != -1 || (c < (Char) 0x21) || c == EOF;

        /// <summary>
        /// Returns parsed integers as NumberTokens.
        /// </summary>
        NumberToken GetNumber()
        {
            // Check integrity before loop to avoid accidentally returning zero.
            while (IsNumChar((Char) reader.Peek())) { reader.Read(); }
            reader.MarkSingleLineTokenEnd();

            var str = reader.GetTokenString();

            if (Int32.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var @int))
                return new NumberToken(@int, reader.TokenSpan);
            else if (Double.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var @double))
                return new NumberToken(@double, reader.TokenSpan);
            else
            {
                Context.Errors.Add(Context.SourceUnit, "Invalid numeric literal", reader.TokenSpan, 100, Severity.FatalError);
                return new NumberToken(reader.TokenSpan);
            }

        }

        StringToken GetString()
        {
            var c = reader.Read();
            if (c == EOF || c != '\"')
            {
                throw new InvalidOperationException("Internal: parsing string?");
            }

            while (true)
            {
                var ch = (Char) reader.Peek();

                if (ch == EOF)
                {
                    reader.MarkSingleLineTokenEnd();
                    Context.Errors.Add(Context.SourceUnit, "Unexpected EOF in string literal", reader.TokenSpan, 100, Severity.FatalError);
                    break;
                }

                if (reader.IsEoln(ch))
                {
                    reader.MarkSingleLineTokenEnd();
                    Context.Errors.Add(Context.SourceUnit, "Unexpected newline in string literal", reader.TokenSpan, 100, Severity.FatalError);
                    break;
                }

                reader.Read();

                if (ch == '"') break;
            }

            reader.MarkSingleLineTokenEnd();
            return new StringToken(reader.GetTokenString()[1..^1], reader.TokenSpan);
        }

        static readonly Char[] WhitespaceChars = { ' ', '\r', '\n', ';', '\t' };

        void SkipWhitespace()
        {
            for (var ch = reader.Peek(); Array.IndexOf(WhitespaceChars, (Char) ch) != -1; ch = reader.Peek())
            {
                if (ch == ';')
                {
                    reader.ReadLine();
                }
                else
                {
                    reader.Read();
                }

            }

            reader.MarkMultiLineTokenEnd();
        }

        static Boolean IsNumChar(Char c) => c == '.' || c >= '0' && c <= '9';
    }
}
