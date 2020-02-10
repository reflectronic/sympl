using System;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplLambda : SymplExpression
    {
        public SymplLambda(IdOrKeywordToken[] parms, SymplExpression[] body)
        {
            Params = parms;
            Body = body;
        }

        public IdOrKeywordToken[] Params { get; }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Lambda  ({Params}) ...>";
    }
}