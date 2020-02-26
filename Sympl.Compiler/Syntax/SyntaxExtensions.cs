using System;
using Sympl.Syntax;

namespace Sympl.Syntax
{
    static class SyntaxExtensions
    {
        public static Boolean IsSyntaxToken(this Token token, SyntaxTokenKind kind) => token is SyntaxToken syntaxToken && syntaxToken.Kind == kind;
        public static Boolean IsKeywordToken(this Token token, KeywordTokenKind kind) => token is KeywordToken keywordToken && keywordToken.Kind == kind;
    }
}
