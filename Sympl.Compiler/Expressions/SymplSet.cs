using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplSet : SymplExpression
    {
        public SymplSet(SymplExpression lhs, SymplExpression value, SourceSpan location) : base(location)
        {
            Source = lhs;
            Value = value;
        }

        public SymplExpression Source { get; }

        public SymplExpression Value { get; }

        public override String ToString() => $"<Set {Source}={Value}>";
    }
}