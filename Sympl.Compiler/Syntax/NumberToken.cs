using System;

namespace Sympl.Syntax
{
    class NumberToken : LiteralToken
    {
        public NumberToken(Int32 val) : base(val)
        {
        }
    }
}