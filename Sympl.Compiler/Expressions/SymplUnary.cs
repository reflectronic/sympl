using System.Linq.Expressions;

namespace Sympl.Expressions
{
    public class SymplUnary : SymplExpression
    {
        public SymplExpression Operand { get; }

        public ExpressionType Operation { get; }

        public SymplUnary(SymplExpression expression, ExpressionType operation)
        {
            Operand = expression;
            Operation = operation;
        }
    }
}