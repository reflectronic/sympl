using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class StringToken : LiteralToken
    {
        public StringToken(String str, SourceSpan location) : base(str, location)
        {
        }
    }
}