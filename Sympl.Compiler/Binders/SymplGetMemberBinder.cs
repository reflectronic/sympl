
using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Scripting.ComInterop;
using Sympl.Runtime;

namespace Sympl.Binders
{
    /// <summary>
    /// Used in general dotted expressions for fetching members.
    /// </summary>
    public class SymplGetMemberBinder : GetMemberBinder
    {
        public SymplGetMemberBinder(String name) : base(name, true)
        {
        }

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
            // First try COM binding.
            if (ComBinder.TryBindGetMember(this, target, out var result, true))
                return result;

            // Defer if any object has no value so that we evaluate their Expressions and nest a
            // CallSite for the InvokeMember.
            if (!target.HasValue) return Defer(target);
            
            // Find our own binding.
            var members = target.LimitType.GetMember(Name, BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);

            return members.Length == 1
                ? new DynamicMetaObject(
                    RuntimeHelpers.EnsureObjectResult(Expression.MakeMemberAccess(
                        Expression.Convert(target.Expression, members[0].DeclaringType), members[0])),
                    // Don't need restriction test for name since this rule is only used where binder
                    // is used, which is only used in sites with this binder.Name.
                    BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType))
                : errorSuggestion ?? RuntimeHelpers.CreateThrow(target, null,
                      BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType),
                      typeof(MissingMemberException), $"cannot bind member, {Name}, on object {target.Value}");
        }
    }
}