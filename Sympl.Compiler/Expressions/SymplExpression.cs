using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public abstract class SymplExpression
    {
        protected SymplExpression(SourceSpan location)
        {
            Location = location;
        }

        public SourceSpan Location { get; }
    }
}