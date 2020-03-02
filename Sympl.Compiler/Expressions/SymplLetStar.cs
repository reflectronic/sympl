using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Scripting;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplLetStar : SymplExpression
    {
        public SymplLetStar(LetBinding[] bindings, SymplExpression[] body, SourceSpan location) : base(location)
        {
            Bindings = bindings;
            Body = body;
        }

        public LetBinding[] Bindings { get; }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Let* ({Bindings}){Body}>";


        /// <summary>
        /// Represents a binding defined in a <see cref="SymplLetStar" />.
        /// </summary>
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "It's fine")]
        public class LetBinding
        {
            public LetBinding(IdOrKeywordToken variable, SymplExpression value)
            {
                Variable = variable;
                Value = value;
            }

            public IdOrKeywordToken Variable { get; }

            public SymplExpression Value { get; }
        }
    }
}