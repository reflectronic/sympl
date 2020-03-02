using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplLoop : SymplExpression
    {
        public SymplLoop(SymplExpression[] body, SourceSpan location) : base(location)
        {
            Body = body;
        }

        public SymplExpression[] Body { get; }

        public override String ToString() => "<Loop ...>";
    }
}