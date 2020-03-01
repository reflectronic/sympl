using System;
using Microsoft.Scripting;
using Sympl.Analysis;
using Sympl.Runtime;

namespace Sympl.Syntax
{
    /// <summary>
    /// Represents an identifier.
    /// </summary>
    /// <remarks>
    /// A subtype, <see cref="KeywordToken"/>, represents keywords. The parser handles when keywords
    /// can be used like identifiers, for example, as .NET members when importing and renaming,
    /// literal keyword constants (nil, true, false), etc. These are used also when parsing list
    /// literals before they get converted to runtime <see cref="Symbol"/> types by
    /// <see cref="ExpressionTreeGenerator"/>.
    /// </remarks>
    public class IdOrKeywordToken : Token
    {
        public IdOrKeywordToken(String id, SourceSpan location) : base(location, false)
        {
            Name = id;
        }

        public IdOrKeywordToken(SourceSpan location) : base(location, true)
        {
            Name = "";
        }

        public String Name { get; }
    }
}