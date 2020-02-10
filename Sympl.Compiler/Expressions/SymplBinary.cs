using System.Linq.Expressions;

namespace Sympl.Expressions
{
    public class SymplBinary : SymplExpression
    {
        public SymplExpression Left { get; }

        public SymplExpression Right { get; }

        public ExpressionType Operation { get; }

        public SymplBinary(SymplExpression left, SymplExpression right, ExpressionType operation)
        {     
            Left = left;
            Right = right;
            Operation = operation;
        }
    }
}