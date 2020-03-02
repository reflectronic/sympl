using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public class LiteralToken : Token
    {
        public Object Value { get; }

        public LiteralToken(Object val, SourceSpan location) : base(location, false)
        {
            Value = val;
        }

        public LiteralToken(SourceSpan location) : base(location, true)
        {
            // The expression tree generator would not be invoked in a scenario where this is null.
            Value = null!;
        }
    }
}