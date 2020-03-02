using System;
using Microsoft.Scripting;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplDefun : SymplExpression
    {
        public SymplDefun(String name, IdOrKeywordToken[] parms, SymplExpression[] body, SourceSpan location) : base(location)
        {
            Name = name;
            Parameters = parms;
            Body = body;
        }

        public String Name { get; }

        public IdOrKeywordToken[] Parameters { get; }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Defun {Name} ({Body}) ...>";
    }
}