using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class SyntaxToken : Token
    {
        public SyntaxTokenKind Kind { get; }

        public static SyntaxToken Eof { get; } = new SyntaxToken(SyntaxTokenKind.Eof, default);

        public SyntaxToken(SyntaxTokenKind kind, SourceSpan location) : base(location)
        {
            Kind = kind;
        }

        public override String ToString() => $"<SyntaxToken {Kind}>";
    }
}