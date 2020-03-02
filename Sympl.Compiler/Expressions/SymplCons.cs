using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplCons : SymplExpression
    {
        public SymplCons(SymplExpression left, SymplExpression right, SourceSpan location) : base(location)
        {
            Left = left;
            Right = right;
        }

        public SymplExpression Left { get; }

        public SymplExpression Right { get; }
    }
}