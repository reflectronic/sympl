using Microsoft.Scripting;
using System;

namespace Sympl.Hosting
{
    sealed class SymplCompilerOptions : CompilerOptions
    {
        public Boolean ShowStackTrace { get; set; }
        public Boolean SetIncompleteTokenParseResult { get; set; }
    } 
}