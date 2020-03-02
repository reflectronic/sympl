using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplCall : SymplExpression
    {
        public SymplCall(SymplExpression fun, SymplExpression[] args, SourceSpan location) : base(location)
        {
            Function = fun;
            Arguments = args;
        }

        public SymplExpression Function { get; }

        public SymplExpression[] Arguments { get; }

        public override String ToString() => $"<Funcall ( {Function} {Arguments} )>";
    }
}