using System;
using Microsoft.Scripting;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplLambda : SymplExpression
    {
        public SymplLambda(IdOrKeywordToken[] parms, SymplExpression[] body, SourceSpan location) : base(location)
        {
            Parameters = parms;
            Body = body;
        }

        public IdOrKeywordToken[] Parameters { get; }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Lambda  ({Parameters}) ...>";
    }
}