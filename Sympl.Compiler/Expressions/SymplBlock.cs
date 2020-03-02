using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplBlock : SymplExpression
    {
        public SymplBlock(SymplExpression[] body, SourceSpan location) : base(location)
        {
            Body = body;
        }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Block* ({Body}>";
    }
}