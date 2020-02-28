using System;
using System.Dynamic;
using System.Linq.Expressions;

namespace Sympl.Runtime
{
    ////////////////////////////////////
    // TypeModel and TypeModelMetaObject
    ////////////////////////////////////

    /// <summary>
    /// Wraps <see cref="Type"/> s.
    /// </summary>
    /// <devdoc>
    /// When Sympl code encounters a type leaf node in <see cref="CodeContext.Globals" /> and tries to invoke a member,
    /// wrapping the <see cref="ReflectedType" />s in <see cref="TypeModel" /> allows member access to get the type's members and
    /// not <see cref="Type" />'s members.
    ///
    /// TODO: Use DLR namespace/type trackers!
    /// </devdoc>
    public class TypeModel : IDynamicMetaObjectProvider
    {
        public TypeModel(Type type)
        {
            ReflectedType = type;
        }

        public Type ReflectedType { get; }

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => new TypeModelMetaObject(parameter, this);
    }
}