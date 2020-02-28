using System;
using System.Dynamic;
using Microsoft.Scripting.ComInterop;
using Sympl.Runtime;

namespace Sympl.Binders
{
    public class SymplGetIndexBinder : GetIndexBinder
    {
        public SymplGetIndexBinder(CallInfo callinfo) : base(callinfo)
        {
        }

        public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
        {
            // First try COM binding.
             if (ComBinder.TryBindGetIndex(this, target, args, out var result))
                return result;

            // Defer if any object has no value so that we evaulate their Expressions and nest a
            // CallSite for the InvokeMember.
            if (!target.HasValue || Array.Exists(args, a => !a.HasValue))
            {
                var deferArgs = new DynamicMetaObject[args.Length + 1];
                deferArgs[0] = target;
                args.CopyTo(deferArgs.AsSpan(1));

                return Defer(deferArgs);
            }

            // Give good error for Cons.
            if (target.LimitType == typeof(Cons))
            {
                if (args.Length != 1)
                    return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, args, BindingRestrictions.Empty,
                               typeof(InvalidOperationException),
                               $"Indexing list takes single index. Got {args.Length}");
            }

            // Find our own binding.
            // Conversions created in GetIndexExpression must be consistent with restrictions made in GetTargetArgsRestrictions.
            var indexingExpr = RuntimeHelpers.EnsureObjectResult(RuntimeHelpers.GetIndexingExpression(target, args));
            var restrictions = RuntimeHelpers.GetTargetArgsRestrictions(target, args, false);
            return new DynamicMetaObject(indexingExpr, restrictions);
        }
    }
}