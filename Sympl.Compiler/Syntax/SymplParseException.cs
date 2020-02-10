using System;

namespace Sympl.Syntax
{
    public class SymplParseException : Exception
    {
        public SymplParseException(String msg) : base(msg)
        {
        }

        public SymplParseException()
        {
        }

        public SymplParseException(String message, Exception innerException) : base(message, innerException)
        {
        }
    }
}