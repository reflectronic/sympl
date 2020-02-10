using System;
using System.Dynamic;

namespace Sympl.Runtime
{
    /// <summary>
    /// This class is needed to canonicalize InvokeMemberBinders in Sympl. See the comment above the
    /// GetXXXBinder methods at the end of the <see cref="SymplRuntime" /> class.
    /// </summary>
    public class InvokeMemberBinderKey
    {
        public InvokeMemberBinderKey(String name, CallInfo info)
        {
            Name = name;
            Info = info;
        }

        public String Name { get; }

        public CallInfo Info { get; }

        public override Boolean Equals(Object? obj) =>
            // TODO: Consider implications of this
            // Don't lower the name. Sympl is case-preserving in the metadata in case some
            // DynamicMetaObject ignores ignoreCase. This makes some interop cases work, but the cost
            // is that if a Sympl program spells ".foo" and ".Foo" at different sites, they won't
            // share rules.
            obj is InvokeMemberBinderKey key && key.Name.Equals(Name, StringComparison.OrdinalIgnoreCase) && key.Info.Equals(Info);

        public override Int32 GetHashCode() => HashCode.Combine(Name, Info);
    }
}