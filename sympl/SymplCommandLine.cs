using System;
using Microsoft.Scripting.Hosting.Shell;

namespace Sympl
{
    sealed class SymplCommandLine : CommandLine
    {
        protected override ICommandDispatcher CreateCommandDispatcher() => new SymplCommandExecutor(Console);

        protected override String Logo => $"Type \"exit\" to exit.{Environment.NewLine}";
    }
}