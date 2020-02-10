using System;

namespace Sympl.Expressions
{
    public class SymplElt : SymplExpression
    {
        public SymplElt(SymplExpression expr, SymplExpression[] indexes)
        {
            ObjectExpr = expr;
            Indexes = indexes;
        }

        public SymplExpression ObjectExpr { get; }

        public SymplExpression[] Indexes { get; }

        public override String ToString() => $"<EltExpr {ObjectExpr}[{Indexes}] >";
    }
}