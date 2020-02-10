
using System;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using Sympl.Runtime;

namespace Sympl.Binders
{
    public class SymplCreateInstanceBinder : CreateInstanceBinder
    {
        public SymplCreateInstanceBinder(CallInfo callInfo) : base(callInfo)
        {
        }

        public override DynamicMetaObject FallbackCreateInstance(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject errorSuggestion)
        {
            // Defer if any object has no value so that we evaluate their Expressions and nest a
            // CallSite for the InvokeMember.
            if (!target.HasValue || Array.Exists(args, a => !a.HasValue))
            {
                var deferArgs = new DynamicMetaObject[args.Length + 1];
                deferArgs[0] = target;
                args.CopyTo(deferArgs.AsSpan(1));

                return Defer(deferArgs);
            }

            // Make sure target actually contains a Type.
            if (!typeof(Type).IsAssignableFrom(target.LimitType))
            {
                return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, args, BindingRestrictions.Empty,
                           typeof(InvalidOperationException),
                           $"Type object must be used when creating instance -- {args}");
            }

            var type = target.Value as Type;
            Debug.Assert(type != null);
            var constructors = type.GetConstructors();
            // Get constructors with right arg counts.

            var res = Array.FindAll(constructors, c => c.GetParameters().Length == args.Length && RuntimeHelpers.ParametersMatchArguments(c.GetParameters(), args));


            // We generate an instance restriction on the target since it is a Type and the
            // constructor is associate with the actual Type instance.
            var restrictions = RuntimeHelpers.GetTargetArgsRestrictions(target, args, true);
            if (res.Length == 0)
            {
                return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, args, restrictions,
                           typeof(MissingMemberException),
                           $"Can't bind create instance -- {args}");
            }

            var ctorArgs = RuntimeHelpers.ConvertArguments(args, res[0].GetParameters());
            return new DynamicMetaObject(
                // Creating an object, so don't need EnsureObjectResult.
                Expression.New(res[0], ctorArgs), restrictions);
        }
    }
}