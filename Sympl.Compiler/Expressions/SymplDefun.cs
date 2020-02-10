using System;
using Sympl.Syntax;

namespace Sympl.Expressions
{
    public class SymplDefun : SymplExpression
    {
        public SymplDefun(String name, IdOrKeywordToken[] parms, SymplExpression[] body)
        {
            Name = name;
            Params = parms;
            Body = body;
        }

        public String Name { get; }

        public IdOrKeywordToken[] Params { get; }

        public SymplExpression[] Body { get; }

        public override String ToString() => $"<Defun {Name} ({Body}) ...>";
    }
}