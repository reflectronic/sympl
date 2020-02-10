using System;

namespace Sympl.Expressions
{
    public class SymplBreak : SymplExpression
    {
        public SymplBreak(SymplExpression? value)
        {
            Value = value;
        }

        public SymplExpression? Value { get; }

        public override String ToString() => "<Break ...)>";
    }
}