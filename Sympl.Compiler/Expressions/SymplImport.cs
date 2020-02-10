using System;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplImport : SymplExpression
    {
        public SymplImport(IdOrKeywordToken[] nsOrModule, IdOrKeywordToken[] members, IdOrKeywordToken[] asNames)
        {
            NamespaceExpr = nsOrModule;
            MemberNames = members;
            Renames = asNames;
        }

        public IdOrKeywordToken[] NamespaceExpr { get; }

        public IdOrKeywordToken[] MemberNames { get; }

        public IdOrKeywordToken[] Renames { get; }

        public override String ToString() => "<ImportExpr>";
    }
}