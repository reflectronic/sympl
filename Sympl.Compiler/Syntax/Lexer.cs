using System;
using System.IO;
using System.Text;

namespace Sympl.Syntax
{
    public class Lexer
    {
        Token? putToken;
        readonly TextReader reader;
        const Char EOF = unchecked((Char) (-1));

        public Lexer(TextReader reader)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public void PutToken(Token token)
        {
            if (putToken != null)
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
        ///
        /// TODO: Maintain source location info and store in token.
        /// </remarks>
        public Token GetToken()
        {
            var token = Lex();
            return token;
        }

        Token Lex()
        {
            if (putToken != null)
            {
                var tmp = putToken;
                putToken = null;
                return tmp;
            }

            SkipWhitespace();
            var ch = PeekChar();

            switch (ch)
            {
                case EOF:
                    return SyntaxToken.EOF;
                case '(':
                    GetChar();
                    return SyntaxToken.Paren;
                case ')':
                    GetChar();
                    return SyntaxToken.CloseParen;
                case var _ when IsNumChar(ch):
                    return GetNumber();
                case '"':
                    return GetString();
                case '\'':
                    GetChar();
                    return SyntaxToken.Quote;
                case '-':
                    return GetIdOrNumber();
                case var _ when StartsId(ch):
                    return GetIdOrKeyword();
                case '.':
                    GetChar();
                    return SyntaxToken.Dot;
                default:
                    throw new InvalidOperationException("Internal: couldn't get token?");
            }
        }

        /// <devdoc>Expects a hyphen as the current char, and returns an Id or number token.</devdoc>
        Token GetIdOrNumber()
        {
            var ch = GetChar();
            if (ch == EOF || ch != '-')
            {
                throw new InvalidOperationException("Internal: parsing ID or number without hyphen.");
            }

            ch = PeekChar();
            if (IsNumChar(ch))
            {
                var token = GetNumber();
                return new NumberToken(-(Int32) token.Value);
            }
            else if (!IsIdTerminator(ch))
            {
                return GetIdOrKeyword('-');
            }
            else
            {
                return MakeIdOrKeywordToken("-", false);
            }
        }

        /// <devdoc>
        /// GetIdOrKeyword has first param to handle call from GetIdOrNumber where ID started with a
        /// hyphen (and looked like a number). Didn't want to add hyphen to StartId test since it
        /// normally appears as the keyword minus. Usually the overload without the first param is called.
        ///
        /// Must not call when the next char is EOF.
        /// </devdoc>
        IdOrKeywordToken GetIdOrKeyword() => GetIdOrKeyword(GetChar());

        IdOrKeywordToken GetIdOrKeyword(Char first)
        {
            var quotedId = false;
            if (first == EOF || !StartsId(first))
            {
                throw new InvalidOperationException("Internal: getting Id or keyword?");
            }

            var res = new StringBuilder();
            Char c;
            if (first == '\\')
            {
                quotedId = true;
                c = GetChar();
                
                if (c == EOF)
                    throw new InvalidOperationException("Unexpected EOF when getting Id.");
                if (!StartsId(first))
                    throw new InvalidOperationException(
                        "Don't support quoted Ids that have non Id constituent characters.");

                res.Append(c);
            }
            else
            {
                res.Append(first);
            }

            // See if there's more chars to Id
            c = PeekChar();
            while (c != EOF && !IsIdTerminator(c))
            {
                res.Append(c);
                GetChar();
                c = PeekChar();
            }

            return MakeIdOrKeywordToken(res.ToString(), quotedId);
        }

        /// <devdoc>
        /// Keep whatever casing we found in the source program so that when the IDs are used a
        /// member names and metadata on binders, then if some MO doesn't respect the IgnoreCase
        /// flag, there's an out for still binding.
        /// </devdoc>
        static IdOrKeywordToken MakeIdOrKeywordToken(String name, Boolean quotedId)
        {
            if (!quotedId && KeywordToken.IsKeywordName(name))
            {
                return KeywordToken.GetKeywordToken(name);
            }
            else
            {
                if (name.Equals("let", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine();
                    Console.WriteLine("WARNING: using 'let'?  You probably meant let*.");
                    Console.WriteLine();
                }

                return new IdOrKeywordToken(name);
            }
        }

        /// <devdoc>Must not be called on EOF</devdoc>
        static Boolean StartsId(Char c) => c == '\\' || !IsIdTerminator(c);

        /// <devdoc>
        /// Restrict macro syntax chars in case try to add macros later, but need to allow backquote
        /// in IDs to support .NET type names that come from reflection. We can fix this later with
        /// more machinery around type names.
        /// </devdoc>
        static readonly Char[] IdTerminators = { '(', ')', '\"', ';', ',', /*'`',*/ '@', '\'', '.' };

        static Boolean IsIdTerminator(Char c) => Array.IndexOf(IdTerminators, c) != -1 || (c < (Char) 33);

        /// <summary>
        /// Returns parsed integers as NumberTokens.
        /// </summary>
        /// <devdoc>TODO: Update and use .NET's <see cref="Double.Parse(String)"/> after scanning to non-constituent char.</devdoc>
        NumberToken GetNumber()
        {
            // Check integrity before loop to avoid accidentally returning zero.
            var c = GetChar();
            if (c == EOF || !IsNumChar(c))
            {
                throw new InvalidOperationException("Internal: lexing number?");
            }

            var digit = c - '0';
            var res = digit;
            c = PeekChar();
            while (c != EOF && IsNumChar(c))
            {
                res = res * 10 + (c - '0');
                GetChar();
                c = PeekChar();
            }

            return new NumberToken(res);
        }

        StringToken GetString()
        {
            var c = GetChar();
            if (c == EOF || c != '\"')
            {
                throw new InvalidOperationException("Internal: parsing string?");
            }

            var res = new StringBuilder();
            var escape = false;
            c = PeekChar();
            while (true)
            {
                if (c == EOF)
                {
                    throw new InvalidOperationException("Hit EOF in string literal.");
                }
                else if (c == '\n' || c == '\r')
                {
                    throw new InvalidOperationException("Hit newline in string literal");
                }
                else if (c == '\\' && !escape)
                {
                    GetChar();
                    escape = true;
                }
                else if (c == '"' && !escape)
                {
                    GetChar();
                    return new StringToken(res.ToString());
                }
                else if (escape)
                {
                    escape = false;
                    GetChar();
                    switch (c)
                    {
                        case 'n':
                            res.Append('\n');
                            break;
                        case 't':
                            res.Append('\t');
                            break;
                        case 'r':
                            res.Append('\r');
                            break;
                        case '\"':
                            res.Append('\"');
                            break;
                        case '\\':
                            res.Append('\\');
                            break;
                    }
                }
                else
                {
                    GetChar();
                    res.Append(c);
                }

                c = PeekChar();
            }
        }

        public Int32 Line { get; private set; } = 1;

        static readonly Char[] WhitespaceChars = { ' ', '\r', '\n', ';', '\t' };

        void SkipWhitespace()
        {
            for (var ch = PeekChar(); Array.IndexOf(WhitespaceChars, ch) != -1; ch = PeekChar())
            {
                if (ch == '\n') Line++;
                if (ch == ';')
                {
                    do
                    {
                        GetChar();
                        ch = PeekChar();
                        if (ch == EOF) return;
                        // If newline seq is two chars, second gets eaten in outer loop.
                    } while (ch != '\n' && ch != '\r');
                }
                else
                {
                    GetChar();
                }
            }
        }

        Char GetChar() => unchecked((Char) reader.Read());

        Char PeekChar() => unchecked((Char) reader.Peek());

        static Boolean IsNumChar(Char c) => c >= '0' && c <= '9';
    }
}