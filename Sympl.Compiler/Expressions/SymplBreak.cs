using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplBreak : SymplExpression
    {
        public SymplBreak(SymplExpression? value, SourceSpan location) : base(location)
        {
            Value = value;
        }

        public SymplExpression? Value { get; }

        public override String ToString() => "<Break ...)>";
    }
}