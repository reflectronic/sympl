using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Scripting.ComInterop;
using Sympl.Runtime;

namespace Sympl.Binders
{
    /// <summary>
    /// Used in general dotted expressions in function calls for invoking members.
    /// </summary>
    public class SymplInvokeMemberBinder : InvokeMemberBinder
    {
        public SymplInvokeMemberBinder(String name, CallInfo callinfo) : base(name, true, callinfo)
        {
        }

        public override DynamicMetaObject FallbackInvokeMember(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject? errorSuggestion)
        {
            // First try COM binding.
            if (ComBinder.TryBindInvokeMember(this, target, args, out var result))
                return result;

            // Defer if any object has no value so that we evaluate their Expressions and nest a
            // CallSite for the InvokeMember.
            if (!target.HasValue || Array.Exists(args, a => !a.HasValue))
            {
                var deferArgs = new DynamicMetaObject[args.Length + 1];
                deferArgs[0] = target;
                args.CopyTo(deferArgs.AsSpan(1));
                return Defer(deferArgs);
            }

            // Find our own binding. Could consider allowing invoking static members from an instance.
            var members = target.LimitType.GetMember(Name, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
            if (members.Length == 1 && (members[0] is PropertyInfo || members[0] is FieldInfo))
            {
                // NEED TO TEST, should check for delegate value too
                // var mem = members[0];
                throw new NotImplementedException();
                //return new DynamicMetaObject(
                //    Expression.Dynamic(
                //        new SymplInvokeBinder(new CallInfo(args.Length)),
                //        typeof(object),
                //        args.Select(a => a.Expression).AddFirst(
                //               Expression.MakeMemberAccess(this.Expression, mem)));

                // Don't test for eventinfos since we do nothing with them now.
            }
            else
            {
                // Get MethodInfos with param types that work for args. This works except for value
                // args that need to pass to reftype params. We could detect that to be smarter and
                // then explicitly StrongBox the args.
                var res = Array.FindAll(members, meth => meth is MethodInfo m && m.GetParameters().Length == args.Length && RuntimeHelpers.ParametersMatchArguments(m.GetParameters(), args));

                // False below means generate a type restriction on the MO. We are looking at the
                // members targetMO's Type.
                var restrictions = RuntimeHelpers.GetTargetArgsRestrictions(target, args, false);
                if (res.Length == 0)
                    return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, args, restrictions,
                               typeof(MissingMemberException), $"Can't bind member invoke -- {args}");

                // restrictions and conversion must be done consistently.
                var callArgs = RuntimeHelpers.ConvertArguments(args, ((MethodInfo) res[0]).GetParameters());
                return new DynamicMetaObject(RuntimeHelpers.EnsureObjectResult(Expression.Call(Expression.Convert(target.Expression, target.LimitType), (MethodInfo) res[0] , callArgs)), restrictions);
                // Could hve tried just letting expression.Call factory do the work, but if there is more
                // than one applicable method using just assignablefrom, expression.Call throws. It does
                // not pick a "most applicable" method or any method.
            }
        }

        public override DynamicMetaObject FallbackInvoke(DynamicMetaObject target, DynamicMetaObject[] args, DynamicMetaObject? errorSuggestion)
        {
            var argExpressions = new Expression[args.Length + 1];
            for (var i = 0; i < args.Length; i++)
            {
                argExpressions[i + 1] = args[i].Expression;
            }

            argExpressions[0] = target.Expression;
            // Just "defer" since we have code in SymplInvokeBinder that knows what to do, and
            // typically this fallback is from a language like Python that passes a DynamicMetaObject
            // with HasValue == false.
            return new DynamicMetaObject(Expression.Dynamic(
                    // This call site doesn't share any L2 caching since we don't call
                    // GetInvokeBinder from Sympl. We aren't plumbed to get the runtime instance here.
                    new SymplInvokeBinder(new CallInfo(args.Length)), typeof(Object), // ret type
                    argExpressions),
                // No new restrictions since SymplInvokeBinder will handle it.
                target.Restrictions.Merge(BindingRestrictions.Combine(args)));
        }
    }
}