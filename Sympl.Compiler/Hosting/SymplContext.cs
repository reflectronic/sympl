using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using System;
using System.Collections.Generic;
using Sympl.Runtime;

namespace Sympl.Hosting
{
    /// <summary>
    /// Represents the language and the workhorse at the language implementation level for supporting
    /// the DLR Hosting APIs.
    /// </summary>
    /// <remarks>
    /// The Sympl LanguageContext is the representation of the language and the workhorse at the
    /// language implementation level for supporting the DLR Hosting APIs. It has many members on it,
    /// but we only have to override a couple to get basic DLR hosting support enabled.
    ///
    /// One extra override we provide is <see cref="GetService{TService}(Object[])"/> so that we can
    /// return the original Sympl hosting object we build before supporting DLR hosting. program.cs
    /// uses this to create symbols in it's little REPL.
    ///
    /// Other things a LanguageContext might do are provide an implementation for ObjectOperations,
    /// offer other services (exception formatting, colorization, tokenization, etc), provide
    /// ExecuteProgram semantics, and so on.
    /// </remarks>
    public sealed class SymplContext : LanguageContext
    {
        readonly SymplRuntime sympl;

        public SymplContext(ScriptDomainManager manager, IDictionary<String, Object> options) : base(manager)
        {
            // TODO: Parse options
            // TODO: Register event manager.AssemblyLoaded
            sympl = new SymplRuntime(manager.GetLoadedAssemblyList(), manager.Globals);
        }

        /// <remarks>
        /// This is all that's needed to run code on behalf of language-independent DLR hosting.
        /// We define our own subtype of ScriptCode.
        /// </remarks>
        public override ScriptCode? CompileSourceCode(SourceUnit sourceUnit, CompilerOptions options, ErrorSink errorSink)
        {
            using var reader = sourceUnit.GetReader();
            
            try
            {
                return sourceUnit.Kind switch
                {
                    SourceCodeKind.SingleStatement => new SymplCode(sympl, sympl.ParseExprToLambda(reader),
                        sourceUnit),
                    SourceCodeKind.Expression => new SymplCode(sympl, sympl.ParseExprToLambda(reader),
                        sourceUnit),
                    SourceCodeKind.AutoDetect => new SymplCode(sympl, sympl.ParseExprToLambda(reader),
                        sourceUnit),
                    SourceCodeKind.InteractiveCode => new SymplCode(sympl, sympl.ParseExprToLambda(reader),
                        sourceUnit),
                    SourceCodeKind.Statements => new SymplCode(sympl,
                        sympl.ParseFileToLambda(sourceUnit.Path, reader), sourceUnit),
                    SourceCodeKind.File => new SymplCode(sympl, sympl.ParseFileToLambda(sourceUnit.Path, reader),
                        sourceUnit),
                    _ => throw Assert.Unreachable
                };
            }
            catch (Exception e)
            {
                // TODO: Propagate error sink to parser
                // Real language implementation would have a specific type of exception. Also,
                // they would pass errorSink down into the parser and add messages while doing
                // tighter error recovery and continuing to parse.
                errorSink.Add(sourceUnit, e.Message, SourceSpan.None, 0, Severity.FatalError);
                return null;
            }
        }

        /// <remarks>
        /// We expose the original hosting object for creating Symbols or other pre-existing uses.
        /// </remarks>
        public override TService GetService<TService>(params Object[] args) => typeof(TService) == typeof(SymplRuntime)
                ? (TService) (Object) sympl
                : base.GetService<TService>(args);
    }
}