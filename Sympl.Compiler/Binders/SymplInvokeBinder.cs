
using System;
using System.Dynamic;
using System.Linq.Expressions;
using Microsoft.Scripting.ComInterop;
using Sympl.Runtime;

namespace Sympl.Binders
{
    public class SymplInvokeBinder : InvokeBinder
    {
        public SymplInvokeBinder(CallInfo callinfo) : base(callinfo)
        {
        }

        public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
        {
            // First try COM binding.
            if (ComBinder.TryBindInvoke(this, target, args, out var result))
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

            // Find our own binding.
            if (!target.LimitType.IsSubclassOf(typeof(Delegate)))
                return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, args,
                           BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType),
                           typeof(InvalidOperationException),
                           $"Wrong number of arguments for function -- {target.LimitType} got {args}");
            
            var parameters = target.LimitType.GetMethod("Invoke")!.GetParameters();
            if (parameters.Length != args.Length)
                return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, args,
                           BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType),
                           typeof(InvalidOperationException),
                           $"Wrong number of arguments for function -- {target.LimitType} got {args}");
                
                
            // Don't need to check if argument types match parameters. If they don't, users
            // get an argument conversion error.
            var callArgs = RuntimeHelpers.ConvertArguments(args, parameters);
            var expression = Expression.Invoke(Expression.Convert(target.Expression, target.LimitType), callArgs);
            return new DynamicMetaObject(RuntimeHelpers.EnsureObjectResult(expression), BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

        }
    }
}