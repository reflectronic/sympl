using Microsoft.Scripting.Hosting.Shell;

namespace Sympl
{
    public sealed class SymplOptions : ConsoleOptions
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