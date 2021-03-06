using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplListCall : SymplExpression
    {
        /// <devdoc>
        /// This is always a list of Tokens or <see cref="SymplList" />s.
        /// </devdoc>
        public SymplExpression[] Elements { get; }

        public SymplListCall(SymplExpression[] elements, SourceSpan location) : base(location)
        {
            Elements = elements;
        }
    }
}