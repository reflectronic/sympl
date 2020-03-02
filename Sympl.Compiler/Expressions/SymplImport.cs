using System;
using Microsoft.Scripting;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplImport : SymplExpression
    {
        public SymplImport(IdOrKeywordToken[] nsOrModule, IdOrKeywordToken[] members, IdOrKeywordToken[] asNames, SourceSpan location) : base(location)
        {
            Namespaces = nsOrModule;
            MemberNames = members;
            Renames = asNames;
        }

        public IdOrKeywordToken[] Namespaces { get; }

        public IdOrKeywordToken[] MemberNames { get; }

        public IdOrKeywordToken[] Renames { get; }

        public override String ToString() => "<ImportExpr>";
    }
}