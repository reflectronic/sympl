using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class SyntaxToken : Token
    {
        public SyntaxTokenKind Kind { get; }

        public static SyntaxToken Eof { get; } = new SyntaxToken(SyntaxTokenKind.Eof, SourceSpan.None);

        public SyntaxToken(SyntaxTokenKind kind, SourceSpan location) : base(location, false)
        {
            Kind = kind;
        }

        public SyntaxToken(SourceSpan location) : base(location, true)
        {
        }

        public override String ToString() => $"<SyntaxToken {Kind}>";
    }
}