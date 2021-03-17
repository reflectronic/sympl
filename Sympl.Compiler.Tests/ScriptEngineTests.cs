using System;
using Microsoft.Scripting.Hosting;
using Sympl.Hosting;
using Xunit;

namespace Sympl.Compiler.Tests
{
    public abstract class ScriptEngineTests
    {
        protected ScriptEngine Engine { get; }
        protected ScriptScope Scope { get; }

        protected ScriptEngineTests()
        {
            var setup = new ScriptRuntimeSetup()
            {
                LanguageSetups =
                {
                    new LanguageSetup(typeof(SymplContext).AssemblyQualifiedName, "SymPL", new[] { "Sympl" }, new[] { ".sympl " })
                }
            };

            var dlrRuntime = new ScriptRuntime(setup);
            Scope = dlrRuntime.CreateScope();
            Engine = dlrRuntime.GetEngine("Sympl");
        }

        protected T Execute<T>(string expression)
        {
            return Engine.Execute<T>(expression, Scope);
        }

        protected object Execute(string expression)
        {
            return Engine.Execute<object>(expression, Scope);
        }

    }
}
