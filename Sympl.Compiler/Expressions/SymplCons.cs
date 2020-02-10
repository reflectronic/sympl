namespace Sympl.Expressions
{
    public class SymplCons : SymplExpression
    {
        public SymplCons(SymplExpression left, SymplExpression right)
        {
            Left = left;
            Right = right;
        }

        public SymplExpression Left { get; }

        public SymplExpression Right { get; }
    }
}