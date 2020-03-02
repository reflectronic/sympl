using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplDot : SymplExpression
    {
        public SymplExpression Target { get; }

        public SymplExpression[] Expressions { get; }

        public SymplDot(SymplExpression expr, SymplExpression[] exprs, SourceSpan location) : base(location)
        {
            Target = expr;
            Expressions = exprs;
        }

        public override String ToString() => $"<DotExpr {Target}.{Expressions}>";
    }
}