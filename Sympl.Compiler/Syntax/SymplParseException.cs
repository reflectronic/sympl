using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public class SymplParseException : Exception
    {
        public ScriptCodeParseResult ParseError { get; }

        public SymplParseException(String msg, ScriptCodeParseResult error = ScriptCodeParseResult.Invalid) : base(msg)
        {
            ParseError = error;
        }
    }
}