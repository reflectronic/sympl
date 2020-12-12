using System.Dynamic;
using System.Linq.Expressions;
using Sympl.Runtime;

namespace Sympl.Binders
{
    public class SymplUnaryOperationBinder : UnaryOperationBinder
    {
        public SymplUnaryOperationBinder(ExpressionType operation) : base(operation)
        {
        }

        public override DynamicMetaObject FallbackUnaryOperation(DynamicMetaObject target, DynamicMetaObject? errorSuggestion) =>
            // Defer if any object has no value so that we evaluate their Expressions and nest a
            // CallSite for the InvokeMember.
            target.HasValue
                ? new DynamicMetaObject(
                    RuntimeHelpers.EnsureObjectResult(Expression.MakeUnary(Operation,
                        Expression.Convert(target.Expression, target.LimitType), target.LimitType)),
                    target.Restrictions.Merge(
                        BindingRestrictions.GetTypeRestriction(target.Expression, target.LimitType)))
                : Defer(target);
    }
}