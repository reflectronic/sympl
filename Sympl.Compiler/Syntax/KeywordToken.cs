using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    public class KeywordToken : IdOrKeywordToken
    {
        public KeywordTokenKind Kind { get; }

        public KeywordToken(KeywordTokenKind kind, SourceSpan location) : base(KeywordNames[kind], location)
        {
            Kind = kind;
        }

        public KeywordToken(SourceSpan location) : base(location)
        {
        }

        public static readonly Dictionary<String, KeywordTokenKind> KeywordTypes = new Dictionary<String, KeywordTokenKind>(StringComparer.OrdinalIgnoreCase)
        {
            { "import", KeywordTokenKind.Import },
            { "defun", KeywordTokenKind.Defun },
            { "lambda", KeywordTokenKind.Lambda },
            { "defclass", KeywordTokenKind.Defclass },
            { "defmethod", KeywordTokenKind.Defmethod },
            { "new", KeywordTokenKind.New },
            { "set", KeywordTokenKind.Set },
            { "let*", KeywordTokenKind.LetStar },
            { "block", KeywordTokenKind.Block },
            { "loop", KeywordTokenKind.Loop },
            { "break", KeywordTokenKind.Break },
            { "continue", KeywordTokenKind.Continue },
            { "return", KeywordTokenKind.Return },
            { "cons", KeywordTokenKind.Cons },
            { "eq", KeywordTokenKind.Eq },
            { "list", KeywordTokenKind.List },
            { "elt", KeywordTokenKind.Elt },
            { "nil", KeywordTokenKind.Nil },
            { "true", KeywordTokenKind.True },
            { "if", KeywordTokenKind.If },
            { "false", KeywordTokenKind.False },
            { "+", KeywordTokenKind.Add },
            { "-", KeywordTokenKind.Subtract },
            { "*", KeywordTokenKind.Multiply },
            { "/", KeywordTokenKind.Divide },
            { "=", KeywordTokenKind.Equal },
            { "!=", KeywordTokenKind.NotEqual },
            { ">", KeywordTokenKind.GreaterThan },
            { "<", KeywordTokenKind.LessThan },
            { "and", KeywordTokenKind.And },
            { "or", KeywordTokenKind.Or },
            { "not", KeywordTokenKind.Not }
        };

        public static readonly Dictionary<KeywordTokenKind, String> KeywordNames = KeywordTypes.ToDictionary(p => p.Value, p => p.Key);

        internal static KeywordToken MakeKeywordToken(String name, SourceSpan location)
        {
            if (KeywordTypes.TryGetValue(name, out var kind))
            {
                return new KeywordToken(kind, location);
            }
            else
            {
                throw new ArgumentException("Given keyword name is not a keyword.", nameof(name));
            }
        }
    }
}