using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public class SyntaxToken : Token
    {
        public SyntaxTokenKind Kind { get; }

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