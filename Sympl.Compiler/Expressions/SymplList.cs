using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplList : SymplExpression
    {
        /// <devdoc>
        /// This is always a list of Tokens or <see cref="SymplList" />s.
        /// </devdoc>
        public Object[] Elements { get; }

        public SymplList(Object[] elements, SourceSpan location) : base(location)
        {
            Elements = elements;
        }

        public override String ToString() => $"<ListExpr {Elements}>";
    }
}