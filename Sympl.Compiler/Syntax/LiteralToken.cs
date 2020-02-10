using System;

namespace Sympl.Syntax
{
    class LiteralToken : Token
    {
        public Object Value { get; }

        public LiteralToken(Object val)
        {
            Value = val;
        }
    }
}