using System.Linq.Expressions;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplBinary : SymplExpression
    {
        public SymplExpression Left { get; }

        public SymplExpression Right { get; }

        public ExpressionType Operation { get; }

        public SymplBinary(SymplExpression left, SymplExpression right, ExpressionType operation, SourceSpan location) : base(location)
        {     
            Left = left;
            Right = right;
            Operation = operation;
        }
    }
}