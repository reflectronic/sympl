using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Sympl.Runtime
{
    /// <summary>
    /// A collection of functions that perform operations at runtime on Sympl code, such as
    /// performing an import or eq.
    /// </summary>
    public static class RuntimeHelpers
    {
        /// <summary>
        /// Imports a namespace.
        /// </summary>
        /// <devdoc>
        /// If <paramref name="names"/> is empty, then the name set in <paramref name="module"/> is
        /// the last name in <paramref name="what"/>. If <paramref name="renames"/> is not empty, it
        /// must have the same cardinality as <paramref name="names"/>.
        /// </devdoc>
        /// <param name="what">
        /// A list of names that either identifies (a possibly dotted sequence of) names to fetch
        /// from Globals or a file name to load.
        /// </param>
        /// <param name="names">
        /// A list of names to fetch from the final object that <paramref name="what"/> indicates and
        /// then set each name in <paramref name="module"/>.
        /// </param>
        /// <param name="renames">
        /// a list of names to add to <paramref name="module"/> instead of <paramref name="names"/>.
        /// </param>
        public static Object? Import(SymplRuntime runtime, IDynamicMetaObjectProvider module, String[] what, String[] names, String[] renames)
        {
            Object value;
            // Get object or file scope.
            if (what.Length == 1)
            {
                var name = what[0];
                if (DynamicObjectHelpers.HasMember(runtime.Globals, name))
                {
                    value = DynamicObjectHelpers.GetMember(runtime.Globals, name);
                    // Since runtime.Globals has Sympl's reflection of namespaces and types, we pick
                    // those up first above and don't risk hitting a NamespaceTracker for assemblies
                    // added when we initialized Sympl. The next check will correctly look up
                    // case-INsensitively for globals the host adds to ScriptRuntime.Globals.
                }
                else if (DynamicObjectHelpers.HasMember(runtime.DlrGlobals, name))
                {
                    value = DynamicObjectHelpers.GetMember(runtime.DlrGlobals, name);
                }
                else
                {
                    var f = (String) DynamicObjectHelpers.GetMember(module, "__file__");
                    f = Path.Combine(Path.GetDirectoryName(f)!, $"{name}.sympl");
                    if (File.Exists(f))
                    {
                        value = runtime.ExecuteFile(f);
                    }
                    else
                    {
                        throw new ArgumentException($"Import: can't find name in globals or as file to load -- {name} {f}");
                    }
                }
            }
            else
            {
                // What has more than one name, must be Globals access.
                value = what.Aggregate((Object) runtime.Globals, (current, name) => DynamicObjectHelpers.GetMember((IDynamicMetaObjectProvider) current, name));
                // For more correctness and generality, shouldn't assume all globals are dynamic
                // objects, or that a look up like foo.bar.baz cascades through all dynamic objects.
                // Would need to manually create a CallSite here with Sympl's GetMemberBinder, and
                // think about a caching strategy per name.
            }

            // Assign variables in module.
            if (names.Length == 0)
            {
                DynamicObjectHelpers.SetMember(module, what[^1], value);
            }
            else
            {
                if (renames.Length == 0) renames = names;
                for (var i = 0; i < names.Length; i++)
                {
                    var name = names[i];
                    var rename = renames[i];
                    DynamicObjectHelpers.SetMember(module, rename, DynamicObjectHelpers.GetMember((IDynamicMetaObjectProvider) value, name));
                }
            }

            return null;
        }

        /// <devdoc>Uses of the 'eq' keyword form in Sympl compiles to a call to this helper function.</devdoc>
        public static Boolean Eq(Object x, Object y)
        {
            if (x is null)
                return y is null;
            if (y is null)
                return false;
            
            var xtype = x.GetType();
            var ytype = y.GetType();

            return xtype.IsPrimitive && xtype != typeof(String) && ytype.IsPrimitive && ytype != typeof(String)
                ? x.Equals(y)
                : ReferenceEquals(x, y);
        }

        /// <devdoc>
        /// Uses of the 'cons' keyword form in Sympl compiles to a call to this helper function.
        /// </devdoc>
        public static Cons MakeCons(Object x, Object y) => new Cons(x, y);

        /// <summary>
        /// Gets the i-th element in the Cons list.
        /// </summary>
        public static Object? GetConsElt(Cons lst, Int32 i) => NthCdr(lst, i).First;

        /// <summary>
        /// Sets the i-th element in the Cons list with the specified value.
        /// </summary>
        public static Object SetConsElt(Cons lst, Int32 i, Object value)
        {
            lst = NthCdr(lst, i);
            lst.First = value;
            return value;
        }

        static Cons NthCdr(Cons? lst, Int32 i)
        {
            while (i > 0 && lst is { })
            {
                lst = lst.Rest as Cons;
                i--;
            }

            if (i == 0 && lst is { })
            {
                return lst;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(i));
            }
        }

        //////////////////////////////////////////////////
        // Array Utilities (slicing) and some LINQ helpers
        //////////////////////////////////////////////////

        public static T[] RemoveFirstElt<T>(IList<T> list)
        {
            // Make array ...
            if (list.Count == 0)
            {
                return Array.Empty<T>();
            }

            var res = new T[list.Count];
            list.CopyTo(res, 0);
            // Shift result
            return ShiftLeft(res, 1);
        }

        public static T[] RemoveFirstElt<T>(T[] array) => ShiftLeft(array, 1);

        static T[] ShiftLeft<T>(T[] array, Int32 count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var result = new T[array.Length - count];
            array.AsSpan(count..).CopyTo(result);
            return result;
        }

        public static T[] RemoveLast<T>(T[] array)
        {
            Array.Resize(ref array, array.Length - 1);
            return array;
        }

        ///////////////////////////////////////
        // Utilities used by binders at runtime
        ///////////////////////////////////////

        /// <summary>
        /// Returns whether the args are assignable to the parameters.
        /// </summary>
        /// <devdoc>
        /// We specially check for our TypeModel that wraps .NET's RuntimeType, and elsewhere we
        /// detect the same situation to convert the TypeModel for calls.
        ///
        /// Consider checking p.IsByRef and returning false since that's not CLS.
        ///
        /// Could check for a.HasValue and a.Value is None and ((paramtype is class or interface) or
        /// (paramtype is generic and <see cref="T?"/> )) to support passing nil anywhere.
        /// </devdoc>
        public static Boolean ParametersMatchArguments(ParameterInfo[] parameters, DynamicMetaObject[] args)
        {
            // We only call this after filtering members by this constraint.
            Debug.Assert(args.Length == parameters.Length, "Internal: args are not same len as params?!");
            for (var i = 0; i < args.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                // We consider arg of TypeModel and param of Type to be compatible.
                if (paramType == typeof(Type) && args[i].LimitType == typeof(TypeModel))
                    continue;

                if (!paramType
                    // Could check for HasValue and Value==null AND (paramtype is class or interface)
                    // or (is generic and nullable<T>) ... to bind nullables and null.
                    .IsAssignableFrom(args[i].LimitType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a DynamicMetaObject with an expression that fishes the .NET RuntimeType object
        /// from the TypeModel MO.
        /// </summary>
        public static DynamicMetaObject GetRuntimeTypeMoFromModel(DynamicMetaObject typeModel)
        {
            Debug.Assert(typeModel.LimitType == typeof(TypeModel), "Internal: MO is not a TypeModel?!");
            // Get tm.ReflectedType
            return new DynamicMetaObject(
                Expression.Property(Expression.Convert(typeModel.Expression, typeof(TypeModel)), WellKnownSymbols.ReflectedType),
                typeModel.Restrictions.Merge(
                    BindingRestrictions.GetTypeRestriction(typeModel.Expression, typeof(TypeModel))) //,
                // Must supply a value to prevent binder FallbackXXX methods
                // from infinitely looping if they do not check this MO for
                // HasValue == false and call Defer.  After Sympl added Defer
                // checks, we could verify, say, FallbackInvokeMember by no
                // longer passing a value here.
                //((TypeModel)typeModelMO.Value).ReflectedType
            );
        }

        /// <summary>
        /// Returns list of Convert expressions converting args to param types.
        /// </summary>
        /// <devdoc>
        /// If an arg is a <see cref="TypeModel"/>, then we treat it special to perform the binding.
        /// We need to map from our runtime model to .NET's <see cref="Type"/> object to match.
        ///
        /// To call this function, args and pinfos must be the same length, and param types must be
        /// assignable from args.
        ///
        /// NOTE, if using this function, then need to use <see
        /// cref="GetTargetArgsRestrictions(DynamicMetaObject, DynamicMetaObject[], Boolean)"/> and
        /// make sure you're performing the same conversions as restrictions.
        /// </devdoc>
        public static Expression[] ConvertArguments(DynamicMetaObject[] args, ParameterInfo[] ps)
        {
            Debug.Assert(args.Length == ps.Length, "Internal: args are not same len as params?!");
            var callArgs = new Expression[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var argExpr = args[i].Expression;
                if (args[i].LimitType == typeof(TypeModel) && ps[i].ParameterType == typeof(Type))
                {
                    // Get arg.ReflectedType
                    argExpr = GetRuntimeTypeMoFromModel(args[i]).Expression;
                }

                argExpr = Expression.Convert(argExpr, ps[i].ParameterType);
                callArgs[i] = argExpr;
            }

            return callArgs;
        }

        /// <summary>
        /// Generates the restrictions needed for the MO resulting from binding an operation.
        /// </summary>
        /// <devdoc>
        /// This combines all existing restrictions and adds some for arg conversions.
        ///
        /// NOTE, this function should only be used when the caller is converting arguments to the
        /// same types as these restrictions.
        /// </devdoc>
        /// <param name="instanceRestrictionOnTarget">
        /// Indicates whether to restrict the target to an instance (for operations on type objects)
        /// or to a type (for operations on an instance of that type).
        /// </param>
        public static BindingRestrictions GetTargetArgsRestrictions(DynamicMetaObject target, DynamicMetaObject[] args, Boolean instanceRestrictionOnTarget)
        {
            // Important to add existing restriction first because the DynamicMetaObjects (and
            // possibly values) we're looking at depend on the pre-existing restrictions holding true.
            var restrictions = target.Restrictions.Merge(BindingRestrictions.Combine(args));

            restrictions = instanceRestrictionOnTarget
                ? restrictions.Merge(BindingRestrictions.GetInstanceRestriction(target.Expression, target.Value))
                : restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType));

            return args.Select(t => t.HasValue && t.Value is null
                    ? BindingRestrictions.GetInstanceRestriction(t.Expression, null)
                    : BindingRestrictions.GetTypeRestriction(t.Expression, t.LimitType))
                .Aggregate(restrictions, (current, r) => current.Merge(r));
        }

        /// <summary>
        /// Return the expression for getting target[indexes].
        /// </summary>
        /// <devdoc>
        /// Note, callers must ensure consistent restrictions are added for the conversions on args
        /// and target.
        /// </devdoc>
        public static Expression GetIndexingExpression(DynamicMetaObject target, DynamicMetaObject[] indexes)
        {
            Debug.Assert(target.HasValue && target.LimitType != typeof(Array));

            var indexExpressions = Array.ConvertAll(indexes, i => Expression.Convert(i.Expression, i.LimitType));

            // CONS
            if (target.LimitType == typeof(Cons))
            {
                // Call RuntimeHelper.GetConsElt
                var args = new Expression[indexExpressions.Length + 1];

                args[0] = Expression.Convert(target.Expression, target.LimitType);
                indexExpressions.CopyTo(args.AsSpan(1));
                return Expression.Call(typeof(RuntimeHelpers), nameof(GetConsElt), null, args);
                // ARRAY
            }

            if (target.LimitType.IsArray)
            {
                return Expression.ArrayAccess(Expression.Convert(target.Expression, target.LimitType), indexExpressions);
                // INDEXER
            }

            var props = target.LimitType.GetProperties();

            var res = Array.FindAll(props, p => p.GetIndexParameters().Length == indexes.Length && ParametersMatchArguments(p.GetIndexParameters(), indexes));

            return res.Length == 0
                ? Expression.Throw(Expression.New(
                    typeof(MissingMemberException).GetConstructor(new[] {typeof(String)}) ??
                    throw new InvalidOperationException(),
                    Expression.Constant("Can't bind because there is no matching indexer.")))
                : (Expression) Expression.MakeIndex(Expression.Convert(target.Expression, target.LimitType), res[0],
                    indexExpressions);
        }

        /// <devdoc>
        /// A convenience function for when binders cannot bind. They need to return a
        /// <see cref="DynamicMetaObject"/>
        /// with appropriate restrictions that throws. Binders never just
        /// throw due to the protocol since a binder or MO down the line may provide an implementation.
        ///
        /// It returns a <see cref="DynamicMetaObject"/> whose expression throws the exception, and ensures
        /// the expression's type is object to satisfy the <see
        /// cref="System.Runtime.CompilerServices.CallSite{T}"/> return type constraint.
        ///
        /// A couple of calls to <see cref="CreateThrow(DynamicMetaObject, DynamicMetaObject[], BindingRestrictions, Type, Object[])"/>
        /// already have the args and target restrictions merged in, but <see cref="BindingRestrictions.Merge(BindingRestrictions)"/>
        /// doesn't add duplicates.
        /// </devdoc>
        public static DynamicMetaObject CreateThrow(DynamicMetaObject target, DynamicMetaObject[]? args, BindingRestrictions moreTests, Type exception, params Object[]? exceptionArgs)
        {
            Expression[]? argExpressions = null;
            var argTypes = Type.EmptyTypes;
            if (exceptionArgs is { })
            {
                argExpressions = new Expression[exceptionArgs.Length];
                argTypes = new Type[exceptionArgs.Length];

                for (var i = 0; i < exceptionArgs.Length; i++)
                {
                    Expression e = Expression.Constant(exceptionArgs[i]);
                    argExpressions[i] = e;
                    argTypes[i] = e.Type;
                }
            }

            var constructor = exception.GetConstructor(argTypes);
            if (constructor is null)
            {
                throw new ArgumentException("Type doesn't have constructor with a given signature");
            }

            return new DynamicMetaObject(Expression.Throw(Expression.New(constructor, argExpressions),
                // Force expression to be type object so that DLR CallSite code things only type
                // object flows out of the CallSite.
                typeof(Object)), target.Restrictions.Merge(BindingRestrictions.Combine(args)).Merge(moreTests));
        }

        /// <summary>
        /// Wraps expression if necessary so that any binder or <see cref="DynamicMetaObject"/> result
        /// expression returns <see cref="Object"/>. This is required by <see
        /// cref="System.Runtime.CompilerServices.CallSite{T}"/> s.
        /// </summary>
        public static Expression EnsureObjectResult(Expression expression) => !expression.Type.IsValueType
                ? expression
                : expression.Type == typeof(void)
                ? Expression.Block(expression, Expression.Default(typeof(Object)))
                : (Expression) Expression.Convert(expression, typeof(Object));
    }
}