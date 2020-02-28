using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.IO;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;
using Sympl.Runtime;

namespace Sympl.Hosting
{
    /// <summary>
    /// Represents Sympl compiled code for the language implementation support the DLR Hosting APIs require.
    /// </summary>
    /// <remarks>
    /// This class represents Sympl compiled code for the language implementation support the DLR
    /// Hosting APIs require. The DLR Hosting APIs call on this class to run code in a new
    /// <see cref="ScriptCode"/> (represented as <see cref="Scope"/> at the language implementation level or a provided ScriptScope).
    /// </remarks>
    public sealed class SymplCode : ScriptCode
    {
        readonly Expression<Func<CodeContext, IDynamicMetaObjectProvider, Object>> lambda;

        readonly SymplContext symplContext;
        Func<CodeContext, IDynamicMetaObjectProvider, Object>? compiledLambda;

        public SymplCode(SymplContext symplContext,
            Expression<Func<CodeContext, IDynamicMetaObjectProvider, Object>> lambda, SourceUnit sourceUnit) : base(
            sourceUnit)
        {
            this.lambda = lambda;
            this.symplContext = symplContext;
        }

        public override Object Run() => Run(new Scope());

        public override Object Run(Scope scope)
        {
            compiledLambda ??= lambda.Compile();

            if (SourceUnit.Kind == SourceCodeKind.File)
            {
                // Simple way to convey script rundir for RuntimeHelpers.Import to load .sympl
                // files relative to the current script file.
                DynamicObjectHelpers.SetMember(scope, "__file__", Path.GetFullPath(SourceUnit.Path));
            }

            return compiledLambda(symplContext.Context, scope);
        }
    }
}