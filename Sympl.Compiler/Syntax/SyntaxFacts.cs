using System;
using Microsoft.Scripting.Utils;
using Sympl.Expressions;

namespace Sympl.Syntax
{
    public static class SyntaxFacts
    {
        public static String GetString(SyntaxTokenKind kind) => kind switch
        {
            SyntaxTokenKind.Invalid => throw Assert.Unreachable,
            SyntaxTokenKind.OpenParenthesis => "(",
            SyntaxTokenKind.CloseParenthesis => ")",
            SyntaxTokenKind.Eof => "<eof>",
            SyntaxTokenKind.Quote => "'",
            SyntaxTokenKind.Dot => ".",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static String GetString(KeywordTokenKind kind) => KeywordToken.KeywordNames[kind];

        public static String GetString(Type type)
        {
            if (type == typeof(SymplExpression)) return "Expression";
            if (type == typeof(LiteralToken)) return "Literal";
            return type.Name;
        }
    }
}
