using System.Linq.Expressions;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplUnary : SymplExpression
    {
        public SymplExpression Operand { get; }

        public ExpressionType Operation { get; }

        public SymplUnary(SymplExpression expression, ExpressionType operation, SourceSpan location) : base(location)
        {
            Operand = expression;
            Operation = operation;
        }
    }
}