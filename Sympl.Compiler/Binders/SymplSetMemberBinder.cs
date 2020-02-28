using System;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Scripting.ComInterop;
using Sympl.Runtime;

namespace Sympl.Binders
{
    /// <summary>
    /// Used in general dotted expressions for setting members.
    /// </summary>
    public class SymplSetMemberBinder : SetMemberBinder
    {
        public SymplSetMemberBinder(String name) : base(name, true)
        {
        }

        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion)
        {
            // First try COM binding.
            if (ComBinder.TryBindSetMember(this, target, value, out var result))
                return result;

            // Defer if any object has no value so that we evaluate their Expressions and nest a
            // CallSite for the InvokeMember.
            if (!target.HasValue) return Defer(target);
            
            // Find our own binding.
            var members = target.LimitType.GetMember(Name, BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
            if (members.Length == 1)
            {
                var mem = members[0];
                Expression val;
                switch (mem.MemberType)
                {
                    // Should check for member domain type being Type and value being TypeModel, similar
                    // to ConvertArguments, and building an expression like GetRuntimeTypeMoFromModel.
                    case MemberTypes.Property:
                        val = Expression.Convert(value.Expression, ((PropertyInfo) mem).PropertyType);
                        break;
                    case MemberTypes.Field:
                        val = Expression.Convert(value.Expression, ((FieldInfo) mem).FieldType);
                        break;
                    default:
                        return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, null,
                                   BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType),
                                   typeof(InvalidOperationException),
                                   "Sympl only supports setting Properties and fields at this time.");
                }

                return new DynamicMetaObject(
                    // Assign returns the stored value, so we're good for Sympl.
                    RuntimeHelpers.EnsureObjectResult(Expression.Assign(
                        Expression.MakeMemberAccess(Expression.Convert(target.Expression, members[0].DeclaringType),
                            members[0]), val)),
                    // Don't need restriction test for name since this rule is only used where binder
                    // is used, which is only used in sites with this binder.Name.
                    BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));
            }
            else
            {
                return errorSuggestion ?? RuntimeHelpers.CreateThrow(target, null,
                           BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType),
                           typeof(MissingMemberException), "IDynObj member name conflict.");
            }
        }
    }
}