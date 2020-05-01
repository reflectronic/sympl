using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public class NumberToken : LiteralToken
    {
        public NumberToken(Int32 val, SourceSpan location) : base(val, location)
        {
        }

        public NumberToken(Double val, SourceSpan location) : base(val, location)
        {
        }

        public NumberToken(SourceSpan location) : base(location)
        {
        }
    }
}