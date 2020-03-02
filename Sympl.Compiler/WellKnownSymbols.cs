using System.Reflection;
using Sympl.Runtime;

namespace Sympl
{
    static class WellKnownSymbols
    {
        public static readonly MethodInfo Import = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.Import))!;
        public static readonly MethodInfo Eq = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.Eq))!;
        public static readonly MethodInfo MakeCons = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.MakeCons))!;
        public static readonly MethodInfo _List = typeof(Cons).GetMethod(nameof(Cons.List))!;
        public static readonly PropertyInfo ReflectedType = typeof(TypeModel).GetProperty(nameof(TypeModel.ReflectedType))!;
    }
}
