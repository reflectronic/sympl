using System;
using System.Collections.Generic;
using Microsoft.Scripting;

namespace Sympl.Syntax
{
    class KeywordToken : IdOrKeywordToken
    {
        public KeywordTokenKind Kind { get; }

        public KeywordToken(KeywordTokenKind kind, SourceSpan location) : base(KeywordToString[kind], location)
        {
            Kind = kind;
        }

        public KeywordToken(SourceSpan location) : base(location)
        {
        }

        static readonly Dictionary<String, KeywordTokenKind> StringToKeyword = new Dictionary<String, KeywordTokenKind>(StringComparer.OrdinalIgnoreCase)
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
        static readonly Dictionary<KeywordTokenKind, String> KeywordToString = new Dictionary<KeywordTokenKind, String>()
        {
            { KeywordTokenKind.Import, "import" },
            { KeywordTokenKind.Defun, "defun" },
            { KeywordTokenKind.Lambda, "lambda" },
            { KeywordTokenKind.Defclass, "defclass" },
            { KeywordTokenKind.Defmethod, "defmethod" },
            { KeywordTokenKind.New, "new" },
            { KeywordTokenKind.Set, "set" },
            { KeywordTokenKind.LetStar, "let*" },
            { KeywordTokenKind.Block, "block" },
            { KeywordTokenKind.Loop, "loop" },
            { KeywordTokenKind.Break, "break" },
            { KeywordTokenKind.Continue, "continue" },
            { KeywordTokenKind.Return, "return" },
            { KeywordTokenKind.Cons, "cons" },
            { KeywordTokenKind.Eq, "eq" },
            { KeywordTokenKind.List, "list" },
            { KeywordTokenKind.Elt, "elt" },
            { KeywordTokenKind.Nil, "nil" },
            { KeywordTokenKind.True, "true" },
            { KeywordTokenKind.If, "if" },
            { KeywordTokenKind.False, "false" },
            { KeywordTokenKind.Add, "+" },
            { KeywordTokenKind.Subtract, "-" },
            { KeywordTokenKind.Multiply, "*" },
            { KeywordTokenKind.Divide, "/" },
            { KeywordTokenKind.Equal, "=" },
            { KeywordTokenKind.NotEqual, "!=" },
            { KeywordTokenKind.GreaterThan, ">" },
            { KeywordTokenKind.LessThan, "<" },
            { KeywordTokenKind.And, "and" },
            { KeywordTokenKind.Or, "or" },
            { KeywordTokenKind.Not, "not" }
        };

        internal static KeywordToken MakeKeywordToken(String name, SourceSpan location)
        {
            if (StringToKeyword.TryGetValue(name, out var kind))
            {
                return new KeywordToken(kind, location);
            }
            else
            {
                throw new ArgumentException("Given keyword name is not a keyword.", nameof(name));
            }
        }

        internal static Boolean IsKeywordName(String id) => StringToKeyword.ContainsKey(id);
    }
}