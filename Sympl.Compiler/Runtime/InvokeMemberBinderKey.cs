using System;
using System.Dynamic;

namespace Sympl.Runtime
{
    /// <summary>
    /// This class is needed to canonicalize InvokeMemberBinders in Sympl. See the comment above the
    /// GetXXXBinder methods at the end of the <see cref="Hosting.SymplContext" /> class.
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
            obj is InvokeMemberBinderKey key && key.Name.Equals(Name, StringComparison.OrdinalIgnoreCase) && key.Info.Equals(Info);

        public override Int32 GetHashCode() => HashCode.Combine(Name, Info);
    }
}