namespace Sympl.Expressions
{
    public class SymplNew : SymplExpression
    {
        public SymplExpression Type { get; }

        public SymplExpression[] Arguments { get; }

        public SymplNew(SymplExpression type, SymplExpression[] arguments)
        {
            Type = type;
            Arguments = arguments;
        }
    }
}