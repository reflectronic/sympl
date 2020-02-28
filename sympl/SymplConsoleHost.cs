using System;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Shell;
using Sympl.Hosting;

namespace Sympl
{
    sealed class SymplConsoleHost : ConsoleHost
    {
        protected override void ExecuteInternal()
        {
            Runtime.LoadAssembly(typeof(Console).Assembly);
            Runtime.LoadAssembly(typeof(System.IO.Directory).Assembly);

            base.ExecuteInternal();
        }

        protected override ScriptRuntimeSetup CreateRuntimeSetup() => new ScriptRuntimeSetup
        {
            LanguageSetups =
            {
                new LanguageSetup(typeof(SymplContext).AssemblyQualifiedName, "SymPL", new[] { "Sympl" }, new[] { ".sympl" })
            }
        };

        protected override CommandLine CreateCommandLine() => new SymplCommandLine();

        protected override Type Provider => typeof(SymplContext);

        protected override OptionsParser CreateOptionsParser() => new OptionsParser<SymplOptions>();
    }
}