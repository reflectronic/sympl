using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

using System;
using System.Collections.Generic;
using Sympl.Runtime;
using Sympl.Syntax;
using System.Collections.Concurrent;
using Sympl.Binders;
using System.Dynamic;
using System.Linq.Expressions;
using Sympl.Analysis;
using System.IO;
using System.Reflection;

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
        internal CodeContext Context { get; private set; }

        public SymplContext(ScriptDomainManager manager, IDictionary<String, Object> options) : base(manager)
        {
            Context = new CodeContext(manager.Globals, this);

            // TODO: Parse options
            manager.AssemblyLoaded += (sender, e) =>
            {
                RegisterAssembly(e.Assembly);
            };

            foreach (var assembly in manager.GetLoadedAssemblyList())
            {
                RegisterAssembly(assembly);
            }
        }

        /// <remarks>
        /// This is all that's needed to run code on behalf of language-independent DLR hosting.
        /// We define our own subtype of ScriptCode.
        /// </remarks>
        public override ScriptCode? CompileSourceCode(SourceUnit sourceUnit, CompilerOptions options, ErrorSink errorSink)
        {
            using var reader = sourceUnit.GetReader();
                        // TODO: Find a way to avoid generating the expression when we aren't interested in it
                        // (e.g. REPL newline handling/tab completion)

            try
            {
                switch (sourceUnit.Kind)
                {
                    case SourceCodeKind.Expression:
                    case SourceCodeKind.SingleStatement:
                    case SourceCodeKind.InteractiveCode:
                        {
                            var ast = Parser.ParseSingleExpression(reader);

                            var scope = new AnalysisScope(null, "__snippet__", Context.LanguageContext,
                                Expression.Parameter(typeof(CodeContext), nameof(CodeContext)),
                                Expression.Parameter(typeof(IDynamicMetaObjectProvider), "fileModule"));

                            var lambda = Expression.Lambda<Func<CodeContext, IDynamicMetaObjectProvider, Object>>(
                                Expression.Convert(ExpressionTreeGenerator.AnalyzeExpression(ast, scope), typeof(Object)),
                                scope.Runtime,
                                scope.Module);

                            return new SymplCode(this, lambda, sourceUnit);
                        }
                    case SourceCodeKind.File:
                    case SourceCodeKind.AutoDetect:
                    case SourceCodeKind.Statements:
                        {
                            var asts = Parser.ParseFile(reader);

                            var scope = new AnalysisScope(null, Path.GetFileNameWithoutExtension(sourceUnit.Path), Context.LanguageContext,
                                Expression.Parameter(typeof(CodeContext), nameof(CodeContext)),
                                Expression.Parameter(typeof(IDynamicMetaObjectProvider), "fileModule"));

                            var body = new Expression[asts.Length + 1];
                            for (var i = 0; i < asts.Length; i++)
                            {
                                body[i] = ExpressionTreeGenerator.AnalyzeExpression(asts[i], scope);
                            }

                            body[^1] = Expression.Constant(null);

                            var lambda = Expression.Lambda<Func<CodeContext, IDynamicMetaObjectProvider, Object>>(Expression.Block(body),
                                    scope.Runtime, scope.Module);

                            return new SymplCode(this, lambda, sourceUnit);
                        }
                    default:
                        throw Assert.Unreachable;
                }
            }
            catch (Exception e)
            {
                if (e is SymplParseException pex)
                {
                    sourceUnit.CodeProperties = pex.ParseError;
                }

                // TODO: Propagate error sink to parser
                // Real language implementation would have a specific type of exception. Also,
                // they would pass errorSink down into the parser and add messages while doing
                // tighter error recovery and continuing to parse.

                errorSink.Add(sourceUnit, e.Message, SourceSpan.None, 0, Severity.FatalError);
                return null;
            }
        }

        public override Version LanguageVersion { get; } = typeof(SymplContext).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

        /// <remarks>
        /// We expose the original hosting object for creating Symbols or other pre-existing uses.
        /// </remarks>
        public override TService GetService<TService>(params Object[] args)
        {
            return base.GetService<TService>(args);
        }

        /// <summary>
        /// Builds a tree of ExpandoObjects representing .NET namespaces, with TypeModel objects at
        /// the leaves.
        /// </summary>
        /// <remarks>
        /// Though Sympl is case-insensitive, we store the names as they appear in .NET reflection in
        /// case our globals object or a namespace object gets passed as an IDO to another language
        /// or library, where they may be looking for names case-sensitively using EO's default lookup.
        /// </remarks>
        void RegisterAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                var names = type.FullName!.Split('.');
                var table = Context.Globals;
                for (var i = 0; i < names.Length - 1; i++)
                {
                    var name = names[i];
                    if (DynamicObjectHelpers.HasMember(table, name))
                    {
                        // Must be Expando since only we have put objs in the tables so far.
                        table = (ExpandoObject) DynamicObjectHelpers.GetMember(table, name);
                    }
                    else
                    {
                        var tmp = new ExpandoObject();
                        DynamicObjectHelpers.SetMember(table, name, tmp);
                        table = tmp;
                    }
                }

                DynamicObjectHelpers.SetMember(table, names[^1], new TypeModel(type));
            }
        }

        /*
        /// <summary>
        /// Executes the file in a new module scope and stores the scope on Globals, using either the
        /// provided name, globalVar, or the file's base name.
        /// </summary>
        /// <returns>The module scope.</returns>
        public IDynamicMetaObjectProvider ExecuteFile(String filename, String? globalVar = null)
        {
            var module = NewScope();

            ExecuteFileInScope(filename, module);

            DynamicObjectHelpers.SetMember(Context.Globals, globalVar ?? Path.GetFileNameWithoutExtension(filename), module);

            return module;
        }


        /// <summary>
        /// Executes the file in the given module scope. This does NOT store the module scope on Globals.
        /// </summary>
        public void ExecuteFileInScope(String filename, IDynamicMetaObjectProvider module)
        {
            using var f = new StreamReader(filename);
            // Simple way to convey script rundir for RuntimeHelpers.Import to load .js files.
            DynamicObjectHelpers.SetMember(module, "__file__", Path.GetFullPath(filename));

            var moduleFun = ParseFileToLambda(filename, f);
            moduleFun.Compile().Invoke(Context, module);
        }*/

        public static ExpandoObject NewScope() => new ExpandoObject();

        /// <summary>
        /// Returns the Symbol interned in this runtime if it is already there. If not, this makes
        /// the Symbol and interns it.
        /// </summary>
        public Symbol MakeSymbol(String name) => Context.Symbols.GetOrAdd(name, name => new Symbol(name));

        /////////////////////////
        // Canonicalizing Binders
        /////////////////////////

        // We need to canonicalize binders so that we can share L2 dynamic dispatch caching across
        // common call sites. Every call site with the same operation and same metadata on their
        // binders should return the same rules whenever presented with the same kinds of inputs. The
        // DLR saves the L2 cache on the binder instance. If one site somewhere produces a rule,
        // another call site performing the same operation with the same metadata could get the L2
        // cached rule rather than computing it again. For this to work, we need to place the same
        // binder instance on those functionally equivalent call sites.

        readonly ConcurrentDictionary<String, SymplGetMemberBinder> getMemberBinders = new ConcurrentDictionary<String, SymplGetMemberBinder>(StringComparer.OrdinalIgnoreCase);
        public SymplGetMemberBinder GetGetMemberBinder(String name) => getMemberBinders.GetOrAdd(name, name => new SymplGetMemberBinder(name));

        readonly ConcurrentDictionary<String, SymplSetMemberBinder> setMemberBinders = new ConcurrentDictionary<String, SymplSetMemberBinder>(StringComparer.OrdinalIgnoreCase);
        public SymplSetMemberBinder GetSetMemberBinder(String name) => setMemberBinders.GetOrAdd(name, name => new SymplSetMemberBinder(name));

        readonly ConcurrentDictionary<CallInfo, SymplInvokeBinder> invokeBinders = new ConcurrentDictionary<CallInfo, SymplInvokeBinder>();
        public SymplInvokeBinder GetInvokeBinder(CallInfo info) => invokeBinders.GetOrAdd(info, info => new SymplInvokeBinder(info));

        readonly ConcurrentDictionary<InvokeMemberBinderKey, SymplInvokeMemberBinder> invokeMemberBinders = new ConcurrentDictionary<InvokeMemberBinderKey, SymplInvokeMemberBinder>();
        public SymplInvokeMemberBinder GetInvokeMemberBinder(InvokeMemberBinderKey info) => invokeMemberBinders.GetOrAdd(info, info => new SymplInvokeMemberBinder(info.Name, info.Info));

        readonly ConcurrentDictionary<CallInfo, SymplCreateInstanceBinder> createInstanceBinders = new ConcurrentDictionary<CallInfo, SymplCreateInstanceBinder>();
        public SymplCreateInstanceBinder GetCreateInstanceBinder(CallInfo info) => createInstanceBinders.GetOrAdd(info, info => new SymplCreateInstanceBinder(info));

        readonly ConcurrentDictionary<CallInfo, SymplGetIndexBinder> getIndexBinders = new ConcurrentDictionary<CallInfo, SymplGetIndexBinder>();
        public SymplGetIndexBinder GetGetIndexBinder(CallInfo info) => getIndexBinders.GetOrAdd(info, info => new SymplGetIndexBinder(info));

        readonly ConcurrentDictionary<CallInfo, SymplSetIndexBinder> setIndexBinders = new ConcurrentDictionary<CallInfo, SymplSetIndexBinder>();
        public SymplSetIndexBinder GetSetIndexBinder(CallInfo info) => setIndexBinders.GetOrAdd(info, info => new SymplSetIndexBinder(info));

        readonly ConcurrentDictionary<ExpressionType, SymplBinaryOperationBinder> binaryOperationBinders = new ConcurrentDictionary<ExpressionType, SymplBinaryOperationBinder>();
        public SymplBinaryOperationBinder GetBinaryOperationBinder(ExpressionType op) => binaryOperationBinders.GetOrAdd(op, op => new SymplBinaryOperationBinder(op));

        readonly ConcurrentDictionary<ExpressionType, SymplUnaryOperationBinder> unaryOperationBinders = new ConcurrentDictionary<ExpressionType, SymplUnaryOperationBinder>();
        public SymplUnaryOperationBinder GetUnaryOperationBinder(ExpressionType op) => unaryOperationBinders.GetOrAdd(op, op => new SymplUnaryOperationBinder(op));
    }
}