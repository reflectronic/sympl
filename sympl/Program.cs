using System;
using System.Diagnostics;
using System.IO;
using Sympl.Hosting;
using Microsoft.Scripting.Hosting;
using Sympl.Syntax;
using Sympl.Runtime;

namespace Sympl
{
    static class Program
    {
        static void Main(String[] args)
        {
            // Setup DLR ScriptRuntime with our languages. We hardcode them here but a .NET app
            // looking for general language scripting would use an app.config file and ScriptRuntime.CreateFromConfiguration.
            var setup = new ScriptRuntimeSetup();
            var qualifiedName = typeof(SymplContext).AssemblyQualifiedName;
            setup.LanguageSetups.Add(new LanguageSetup(qualifiedName, "SymPL", new[] { "Sympl" } , new[] { ".sympl" }));
            var dlrRuntime = new ScriptRuntime(setup);
            dlrRuntime.LoadAssembly(typeof(Console).Assembly);

            // Get an Sympl engine and run stuff ...
            var engine = dlrRuntime.GetEngine("Sympl");
            var path = Path.Join("Sympl.Samples", "test.sympl");
            Console.WriteLine($"Executing {path}");
            var feo = engine.ExecuteFile(path);
            Console.WriteLine("ExecuteExpr ... ");
            engine.Execute("(print 5)", feo);

            // Consume host supplied globals via DLR Hosting.
            dlrRuntime.Globals.SetVariable("DlrGlobal", new[] { 3, 7 });
            engine.Execute("(import dlrglobal)", feo);
            engine.Execute("(print (elt dlrglobal 1))", feo);

            // Drop into the REPL ...
            if (args.Length > 0 && args[0] == "norepl") 
                return;
            
            var expression = "";
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            
            Console.WriteLine("Enter expressions.  Enter blank line to abort input.");
            Console.WriteLine("Enter \"exit\" to exit.");
            Console.WriteLine();
            var prompt = "> ";
            var s = engine.GetService<SymplRuntime>();
            s.MakeSymbol("exit");

            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(input))
                {
                    expression = "";
                    prompt = "> ";
                    continue;
                }

                expression = $"{expression} {input}";

                // See if we have complete input.
                try
                {
                    new Parser().ParseSingleExpression(new StringReader(expression));
                }
                catch (Exception)
                {
                    prompt = "... ";
                    continue;
                }

                // We do, so execute.
                try
                {
                    Object res = engine.Execute(expression, feo);
                    expression = "";
                    prompt = "> ";
                    if (res == s.MakeSymbol("exit")) return;
                    Console.WriteLine(res);
                }
                catch (Exception e)
                {
                    expression = "";
                    prompt = "> ";
                    Console.Write("ERROR: ");
                    Console.WriteLine(e);
                }
            }
        }
    }
}