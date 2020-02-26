using Microsoft.Scripting.Hosting.Shell;

namespace Sympl
{
    sealed class SymplOptions : ConsoleOptions
    {
        public SymplOptions()
        {
            Introspection = false;
            AutoIndent = true;
            TabCompletion = true;
            ColorfulConsole = true;
            PrintVersion = true;
        }
    }
}