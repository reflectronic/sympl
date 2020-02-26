using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public abstract class Token
    {
        protected Token(SourceSpan location)
        {
            SourceLocation = location;
        }

        public SourceSpan SourceLocation { get; }
    }
}