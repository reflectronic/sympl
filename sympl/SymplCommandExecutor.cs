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
            if (compiledCode is null) return null;

            Object? result = compiledCode.Execute(scope);
            if (result is not null)
                Console.WriteLine(result.ToString(), Style.Out);

            return result;
        }
    }
}