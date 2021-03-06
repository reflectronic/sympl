using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplIf : SymplExpression
    {
        public SymplIf(SymplExpression test, SymplExpression consequent, SymplExpression? alternative, SourceSpan location) : base(location)
        {
            Test = test;
            Consequent = consequent;
            Alternative = alternative;
        }

        public SymplExpression Test { get; }

        public SymplExpression Consequent { get; }

        public SymplExpression? Alternative { get; }
    }
}