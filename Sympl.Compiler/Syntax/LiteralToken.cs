using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class LiteralToken : Token
    {
        public Object Value { get; }

        public LiteralToken(Object val, SourceSpan location) : base(location)
        {
            Value = val;
        }
    }
}