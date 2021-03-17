using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Net;
using System.Security;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using Sympl.Analysis;
using Sympl.Expressions;
using Sympl.Hosting;
using Sympl.Runtime;
using Sympl.Syntax;

namespace Sympl
{
    static class Program
    {
        static void Main(String[] args)
        {
            var setup = new ScriptRuntimeSetup()
            {
                LanguageSetups =
                {
                    new LanguageSetup(typeof(SymplContext).AssemblyQualifiedName, "SymPL", new[] { "Sympl" }, new[] { ".sympl " })
                }
            };

            var dlrRuntime = new ScriptRuntime(setup);
            // var scope = dlrRuntime.CreateScope();
            var engine = dlrRuntime.GetEngine("Sympl");


            // engine.Execute<object>("(import system)", scope);


            var ast = new SymplImport(new[] { new IdOrKeywordToken("system", default) }, Array.Empty<IdOrKeywordToken>(), Array.Empty<IdOrKeywordToken>(), default);

            var scope = new AnalysisScope(null, "__snippet__", null,
                                Expression.Parameter(typeof(CodeContext), "codeContext"),
                                Expression.Parameter(typeof(IDynamicMetaObjectProvider), "fileModule"));

            var lambda = Expression.Lambda<Func<CodeContext, IDynamicMetaObjectProvider, Object>>(
                Expression.Convert(Expression.Call(typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.Import))!, scope.Runtime!, scope.ThisModule!, Expression.Constant(new string[] { "system" }), Expression.Constant(Array.Empty<string>()), Expression.Constant(Array.Empty<string>())), typeof(Object)),
                scope.Runtime!,
                scope.ThisModule!);
            

            lambda.Compile().Invoke(new CodeContext(new Scope(), new SymplContext(new(new Host(), new DlrConfiguration(false, true, new Dictionary<string, object>())), new Dictionary<string, object>())), null);


            Console.WriteLine("Done!");


            /*
            new SymplConsoleHost().Run(args);
            */
        }
    }

    class Host : DynamicRuntimeHostingProvider
    {
        public override PlatformAdaptationLayer PlatformAdaptationLayer => new();
    }
}       