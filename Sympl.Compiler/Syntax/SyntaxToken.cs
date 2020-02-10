using System;

namespace Sympl.Syntax
{
    class SyntaxToken : Token
    {
        readonly SyntaxTokenKind kind;

        SyntaxToken(SyntaxTokenKind kind)
        {
            this.kind = kind;
        }

        public override String ToString() => $"<SyntaxToken {kind.ToString()}>";

        public static SyntaxToken Paren = new SyntaxToken(SyntaxTokenKind.Paren);

        public static SyntaxToken CloseParen = new SyntaxToken(SyntaxTokenKind.CloseParen);

        public static SyntaxToken EOF = new SyntaxToken(SyntaxTokenKind.EOF);
        public static SyntaxToken Quote = new SyntaxToken(SyntaxTokenKind.Quote);
        public static SyntaxToken Dot = new SyntaxToken(SyntaxTokenKind.Dot);
    }
}