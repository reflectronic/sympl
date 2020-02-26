using System;
using System.Globalization;
using System.IO;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace Sympl.Syntax
{
    public class Lexer
    {
        Token? putToken;
        readonly TokenizerBuffer reader;
        const Char EOF = unchecked((Char) (-1));

        public Lexer(TextReader reader)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            this.reader = new TokenizerBuffer(reader, new SourceLocation(0, 1, 1), 4, true);
        }

        public void PutToken(Token token)
        {
            if (putToken is { })
            {
                throw new InvalidOperationException("Internal Error: putting token when there is one?");
            }
            
            putToken = token;
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
            if (putToken is { })
            {
                var tmp = putToken;
                putToken = null;
                return tmp;
            }

            SkipWhitespace();
            var ch = (Char) reader.Peek();
            reader.DiscardToken();

            switch (ch)
            {
                case EOF:
                    return SyntaxToken.Eof;
                case '(':
                    reader.Read();
                    reader.MarkMultiLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.OpenParenthesis, reader.TokenSpan);
                case ')':
                    reader.Read();
                    reader.MarkMultiLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.CloseParenthesis, reader.TokenSpan);
                case '.':
                    reader.Read();
                    reader.MarkMultiLineTokenEnd();
                    return new SyntaxToken(SyntaxTokenKind.Dot, reader.TokenSpan);
                case '\'':
                    reader.Read();
                    reader.MarkMultiLineTokenEnd();
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

            reader.MarkMultiLineTokenEnd();
            return MakeIdOrKeywordToken(isQuoted);
        }

        /// <devdoc>
        /// Keep whatever casing we found in the source program so that when the IDs are used a
        /// member names and metadata on binders, then if some MO doesn't respect the IgnoreCase
        /// flag, there's an out for still binding.
        /// </devdoc>
        IdOrKeywordToken MakeIdOrKeywordToken(Boolean isQuoted)
        {
            var name = reader.GetTokenString();
            if (!isQuoted && KeywordToken.IsKeywordName(name))
            {
                return KeywordToken.MakeKeywordToken(name, reader.TokenSpan);
            }
            else
            {
                // TODO: Add to error sink
                if (name.Equals("let", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine();
                    Console.WriteLine("WARNING: using 'let'?  You probably meant let*.");
                    Console.WriteLine();
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

        static Boolean IsIdTerminator(Char c) => Array.IndexOf(IdTerminators, c) != -1 || (c < (Char) 0x21);

        /// <summary>
        /// Returns parsed integers as NumberTokens.
        /// </summary>
        NumberToken GetNumber()
        {
            // Check integrity before loop to avoid accidentally returning zero.
            while (IsNumChar((Char) reader.Peek())) { reader.Read();  }
            reader.MarkMultiLineTokenEnd();

            return new NumberToken(Double.Parse(reader.GetTokenString(), CultureInfo.InvariantCulture), reader.TokenSpan);
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
                var ch = (Char) reader.Read();

                if (ch == EOF)
                {
                    throw new SymplParseException("Hit EOF in string literal.");
                }

                if (reader.IsEoln(ch))
                {
                    throw new SymplParseException("Hit error in error error.");
                }

                if (ch == '"') break;
            }

            reader.MarkMultiLineTokenEnd();
            return new StringToken(reader.GetTokenString(), reader.TokenSpan);
        }

        static readonly Char[] WhitespaceChars = { ' ', '\r', '\n', ';', '\t' };

        void SkipWhitespace()
        {
            for (var ch = reader.Peek(); Array.IndexOf(WhitespaceChars, (Char) ch) != -1; ch = reader.Peek())
            {
                reader.Read();
            }

            reader.MarkMultiLineTokenEnd();
        }

        static Boolean IsNumChar(Char c) => c == '.' || c >= '0' && c <= '9';
    }
}