using System;
using Microsoft.Scripting;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    /// <summary>
    /// Represents identifiers, but the IdToken can be a keyword sometimes.
    /// </summary>
    /// <devdoc>
    /// For example, in quoted lists, import expressions, and as members of objects in dotted expressions.
    /// Need to check for <see cref="IdOrKeywordToken.IsKeywordToken"/> when it matters.
    /// </devdoc>
    public class SymplIdentifier : SymplExpression
    {
        public IdOrKeywordToken IdToken { get; }

        public SymplIdentifier(IdOrKeywordToken id) : base(id.Location)
        {   
            IdToken = id;
        }

        public override String ToString() => $"<IdExpr {IdToken.Name}>";
    }
}