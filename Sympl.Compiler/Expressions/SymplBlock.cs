using System;

namespace Sympl.Expressions
{
    public class SymplBlock : SymplExpression
    {
        public SymplBlock(SymplExpression[] body)
        {
            Body = body;
        }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Block* ({Body}>";
    }
}