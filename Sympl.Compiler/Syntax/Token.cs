using System;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public abstract class Token
    {
        protected Token(SourceSpan location, Boolean synthesized)
        {
            Location = location;
            IsSynthesized = synthesized;
        }

        public SourceSpan Location { get; }

        public Boolean IsSynthesized { get; }
    }
}