using System;
using Microsoft.Scripting;

namespace Sympl.Expressions
{
    public class SymplQuote : SymplExpression
    {
        /// <summary>
        /// <paramref name="expression"/> must be <see cref="SymplList"/>, <see cref="SymplIdentifier"/>, or <see cref="SymplLiteral"/>
        /// </summary>
        public SymplQuote(Object expression, SourceSpan location) : base(location)
        {
            Expression = expression;
        }

        public Object Expression { get; }

        public override String ToString() => $"<QuoteExpr {Expression}>";
    }
}