using System;

namespace Sympl.Expressions
{
    public class SymplDot : SymplExpression
    {
        public SymplExpression ObjectExpr { get; }

        public SymplExpression[] Expressions { get; }

        public SymplDot(SymplExpression expr, SymplExpression[] exprs)
        {
            ObjectExpr = expr;
            Expressions = exprs;
        }

        public override String ToString() => $"<DotExpr {ObjectExpr}.{Expressions}>";
    }
}