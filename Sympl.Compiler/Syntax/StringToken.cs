using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public class StringToken : LiteralToken
    {
        public StringToken(String str, SourceSpan location) : base(str, location)
        {
        }
    }
}