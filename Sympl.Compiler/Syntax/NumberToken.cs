using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class NumberToken : LiteralToken
    {
        public NumberToken(Double val, SourceSpan location) : base(val, location)
        {
        }

        public NumberToken(SourceSpan location) : base(location)
        {
        }
    }
}