using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Scripting.Runtime;
using Sympl.Analysis;
using Sympl.Binders;
using Sympl.Syntax;

namespace Sympl.Runtime
{
    public class SymplRuntime
    {
        readonly IList<Assembly> assemblies;
        readonly ExpandoObject globals = new ExpandoObject();
        readonly Scope dlrGlobals;

        readonly ConcurrentDictionary<String, Symbol> Symbols = new ConcurrentDictionary<String, Symbol>(StringComparer.OrdinalIgnoreCase);

        public SymplRuntime(IList<Assembly> assemblies, Scope dlrGlobals)
        {
            this.assemblies = assemblies;
            this.dlrGlobals = dlrGlobals;
            AddAssemblyNamesAndTypes();
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
        public void AddAssemblyNamesAndTypes()
        {
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetExportedTypes())
                {
                    var names = type.FullName!.Split('.');
                    var table = globals;
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
        }

        /// <summary>
        /// Executes the file in a new module scope and stores the scope on Globals, using either the
        /// provided name, globalVar, or the file's base name.
        /// </summary>
        /// <returns>The module scope.</returns>
        public IDynamicMetaObjectProvider ExecuteFile(String filename, String? globalVar = null)
        {
            var moduleEO = CreateScope();
            ExecuteFileInScope(filename, moduleEO);

            globalVar ??= Path.GetFileNameWithoutExtension(filename);
            DynamicObjectHelpers.SetMember(globals, globalVar!, moduleEO);

            return moduleEO;
        }

        /// <summary>
        /// Executes the file in the given module scope. This does NOT store the module scope on Globals.
        /// </summary>
        public void ExecuteFileInScope(String filename, IDynamicMetaObjectProvider moduleEO)
        {
            var f = new StreamReader(filename);
            // Simple way to convey script rundir for RuntimeHelpers.Import to load .js files.
            DynamicObjectHelpers.SetMember(moduleEO, "__file__", Path.GetFullPath(filename));
            try
            {
                var moduleFun = ParseFileToLambda(filename, f);
                var d = moduleFun.Compile();
                d(this, moduleEO);
            }
            finally
            {
                f.Close();
            }
        }

        internal Expression<Func<SymplRuntime, IDynamicMetaObjectProvider, Object>> ParseFileToLambda(String filename, TextReader reader)
        {
            var asts = new Parser().ParseFile(reader);
            var scope = new AnalysisScope(null, filename, this,
                Expression.Parameter(typeof(SymplRuntime), nameof(SymplRuntime)),
                Expression.Parameter(typeof(IDynamicMetaObjectProvider), "fileModule"));

            var body = new Expression[asts.Length + 1];
            for (var i = 0; i < asts.Length; i++)
            {
                body[i] = ExpressionTreeGenerator.AnalyzeExpression(asts[i], scope);
            }
            body[^1] = Expression.Constant(null);

            return Expression.Lambda<Func<SymplRuntime, IDynamicMetaObjectProvider, Object>>(Expression.Block(body),
                    scope.RuntimeExpr, scope.ModuleExpr);
        }

        /// <summary>
        /// Executes a single expression parsed from string in the provided module scope and returns
        /// the resulting value.
        /// </summary>
        public Object ExecuteExpression(String expression, IDynamicMetaObjectProvider moduleEO)
        {
            var moduleFun = ParseExprToLambda(new StringReader(expression));
            return moduleFun.Compile().Invoke(this, moduleEO);
        }

        internal Expression<Func<SymplRuntime, IDynamicMetaObjectProvider, Object>> ParseExprToLambda(TextReader reader)
        {
            var ast = new Parser().ParseSingleExpression(reader);
            var scope = new AnalysisScope(null, "__snippet__", this,
                Expression.Parameter(typeof(SymplRuntime), nameof(SymplRuntime)),
                Expression.Parameter(typeof(IDynamicMetaObjectProvider), "fileModule"));

            return Expression.Lambda<Func<SymplRuntime, IDynamicMetaObjectProvider, Object>>(Expression.Convert(ExpressionTreeGenerator.AnalyzeExpression(ast, scope), typeof(Object)),
                    scope.RuntimeExpr, scope.ModuleExpr);
        }

        public IDynamicMetaObjectProvider Globals => globals;

        public IDynamicMetaObjectProvider DlrGlobals => dlrGlobals;

        public static ExpandoObject CreateScope() => new ExpandoObject();

        /// <summary>
        /// Returns the Symbol interned in this runtime if it is already there. If not, this makes
        /// the Symbol and interns it.
        /// </summary>
        public Symbol MakeSymbol(String name) => Symbols.GetOrAdd(name, name => new Symbol(name));

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