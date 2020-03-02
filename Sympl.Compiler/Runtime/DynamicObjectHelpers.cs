using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Sympl.Runtime
{
    /// <summary>
    /// Provide access to <see cref="IDynamicMetaObjectProvider"/> members given names as data at runtime.
    /// </summary>
    /// <devdoc>
    /// When the names are known at compile time (o.foo), then they get baked into specific sites
    /// with specific binders that encapsulate the name. We need this in python because hasattr et al
    /// are case-sensitive.
    /// </devdoc>
    static class DynamicObjectHelpers
    {
        internal static Object Sentinel { get; } = new Object();

        internal static Boolean HasMember(IDynamicMetaObjectProvider o, String name) => GetMember(o, name) != Sentinel;

        static readonly Dictionary<String, CallSite<Func<CallSite, Object, Object>>> GetSites =
            new Dictionary<String, CallSite<Func<CallSite, Object, Object>>>();

        internal static Object GetMember(IDynamicMetaObjectProvider o, String name)
        {
            if (GetSites.TryGetValue(name, out var site)) 
                return site.Target(site, o);
            
            site = CallSite<Func<CallSite, Object, Object>>.Create(new DynamicObjectHelpersGetMemberBinder(name));
            GetSites[name] = site;

            return site.Target(site, o);
        }

        class DynamicObjectHelpersGetMemberBinder : GetMemberBinder
        {
            internal DynamicObjectHelpersGetMemberBinder(String name) : base(name, true)
            {
            }

            public override DynamicMetaObject
                FallbackGetMember(DynamicMetaObject target, DynamicMetaObject errorSuggestion) =>
                errorSuggestion ?? new DynamicMetaObject(Expression.Constant(Sentinel),
                    target.Restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)));
        }

        static readonly Dictionary<String, CallSite<Action<CallSite, Object, Object>>> SetSites =
            new Dictionary<String, CallSite<Action<CallSite, Object, Object>>>();

        internal static void SetMember(IDynamicMetaObjectProvider o, String name, Object value)
        {
            if (!SetSites.TryGetValue(name, out var site))
            {
                site = CallSite<Action<CallSite, Object, Object>>.Create(new DynamicObjectHelpersSetMemberBinder(name));
                SetSites[name] = site;
            }

            site.Target(site, o, value);
        }

        class DynamicObjectHelpersSetMemberBinder : SetMemberBinder
        {
            internal DynamicObjectHelpersSetMemberBinder(String name) : base(name, true)
            {
            }

            public override DynamicMetaObject
                FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion) =>
                errorSuggestion ?? RuntimeHelpers.CreateThrow(target, null, BindingRestrictions.Empty,
                    typeof(MissingMemberException),
                    "If IDynObj doesn't support setting members, DOHelpers can't do it for the IDO.");
        }
    }
}