using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class LiteralToken : Token
    {
        static readonly Object Sentinel = new Object();

        public Object Value { get; }

        public LiteralToken(Object val, SourceSpan location) : base(location, false)
        {
            Value = val;
        }

        public LiteralToken(SourceSpan location) : base(location, true)
        {
            Value = Sentinel;
        }
    }
}