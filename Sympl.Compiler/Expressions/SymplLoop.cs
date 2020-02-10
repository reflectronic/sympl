using System;

namespace Sympl.Expressions
{
    public class SymplLoop : SymplExpression
    {
        public SymplLoop(SymplExpression[] body)
        {
            Body = body;
        }

        public SymplExpression[] Body { get; }

        public override String ToString() => "<Loop ...>";
    }
}