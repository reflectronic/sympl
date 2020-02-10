namespace Sympl.Expressions
{
    public class SymplEq : SymplExpression
    {
        public SymplEq(SymplExpression left, SymplExpression right)
        {
            Left = left;
            Right = right;
        }

        public SymplExpression Left { get; }

        public SymplExpression Right { get; }
    }
}