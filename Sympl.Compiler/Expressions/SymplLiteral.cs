using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    /// <summary>
    /// Used to represent numbers and strings, but not Quote.
    /// </summary>
    public class SymplLiteral : SymplExpression
    {
        public Object Value { get; }

        public SymplLiteral(Object val, SourceSpan location) : base(location)
        {
            Value = val;
        }

        public override String ToString() => $"<LiteralExpr {Value}>";
    }
}