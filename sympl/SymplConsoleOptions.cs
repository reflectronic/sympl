using Microsoft.Scripting.Hosting.Shell;

namespace Sympl
{
    sealed class SymplConsoleOptions : ConsoleOptions
    {
        public SymplConsoleOptions()
        {
            Introspection = false;
            AutoIndent = true;
            TabCompletion = true;
            ColorfulConsole = true;
            PrintVersion = true;
            HandleExceptions = true;
        }
    }
}