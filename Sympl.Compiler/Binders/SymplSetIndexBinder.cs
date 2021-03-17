using System;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using Microsoft.Scripting.ComInterop;
using Sympl.Runtime;

namespace Sympl.Binders
{
    public class SymplSetIndexBinder : SetIndexBinder
    {
        public SymplSetIndexBinder(CallInfo callinfo) : base(callinfo)
        {
        }

        public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target, DynamicMetaObject[] indexes, DynamicMetaObject value, DynamicMetaObject? errorSuggestion)
        {

            // First try COM binding.
            if (ComBinder.TryBindSetIndex(this, target, indexes, value, out var result))
                return result;

            // Defer if any object has no value so that we evaulate their Expressions and nest a
            // CallSite for the InvokeMember.
            if (!target.HasValue || !value.HasValue || Array.Exists(indexes, a => !a.HasValue))
            {
                var deferArgs = new DynamicMetaObject[indexes.Length + 2];
                indexes.CopyTo(deferArgs.AsSpan(1));
                deferArgs[0] = target;
                deferArgs[^1] = value;
                return Defer(deferArgs);
            }

            // Find our own binding.
            var valueExpr = value.Expression;
            //we convert a value of TypeModel to Type.
            if (value.LimitType == typeof(TypeModel))
            {
                valueExpr = RuntimeHelpers.GetRuntimeTypeMoFromModel(value).Expression;
            }

            Debug.Assert(target.HasValue && target.LimitType != typeof(Array));
            Expression setIndexExpr;
            if (target.LimitType == typeof(Cons))
            {
                if (indexes.Length != 1)
                {
                    return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, indexes, BindingRestrictions.Empty, typeof(InvalidOperationException), $"Indexing list takes single index.  Got {indexes}");
                }

                // Call RuntimeHelper.SetConsElt
                var args = new Expression[]
                {
                    // The first argument is the list
                    Expression.Convert(target.Expression, target.LimitType),
                    // The second argument is the index.
                    Expression.Convert(indexes[0].Expression, indexes[0].LimitType),
                    // The last argument is the value
                    Expression.Convert(valueExpr, typeof(Object))
                };

                // Sympl helper returns value stored.
                setIndexExpr = Expression.Call(typeof(RuntimeHelpers), nameof(RuntimeHelpers.SetConsElt), null, args);
            }
            else
            {
                var indexingExpr = RuntimeHelpers.GetIndexingExpression(target, indexes);
                // Assign returns the stored value, so we're good for Sympl.
                setIndexExpr = Expression.Assign(indexingExpr, valueExpr);
            }

            var restrictions = RuntimeHelpers.GetTargetArgsRestrictions(target, indexes, false);
            return new DynamicMetaObject(RuntimeHelpers.EnsureObjectResult(setIndexExpr), restrictions);
        }
    }
}