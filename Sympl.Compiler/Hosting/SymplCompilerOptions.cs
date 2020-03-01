using Microsoft.Scripting;

using System;

namespace Sympl.Hosting
{
    sealed class SymplCompilerOptions : CompilerOptions
    {
        public SymplCompilerOptions(Boolean showStackTrace)
        {
            ShowStackTrace = showStackTrace;
        }

        public Boolean ShowStackTrace { get; }
    }
}