using System;

namespace Sympl.Expressions
{
    public class SymplAssignment : SymplExpression
    {
        public SymplAssignment(SymplExpression lhs, SymplExpression value)
        {
            Location = lhs;
            Value = value;
        }

        public SymplExpression Location { get; }

        public SymplExpression Value { get; }

        public override String ToString() => $"<AssignExpr {Location}={Value}>";
    }
}