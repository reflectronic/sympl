using System;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Shell;

namespace Sympl
{
    sealed class SymplCommandExecutor : ICommandDispatcher
    {
        public SymplCommandExecutor(IConsole console)
        {
            Console = console;
        }

        public IConsole Console { get; }

        public Object? Execute(CompiledCode compiledCode, ScriptScope scope)
        {
            Object? result = compiledCode.Execute(scope);
            if (result is { })
                Console.WriteLine(result.ToString(), Style.Out);

            return result;
        }
    }
}