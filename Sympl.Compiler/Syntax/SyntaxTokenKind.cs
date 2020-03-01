namespace Sympl.Syntax
{
    /// <summary>
    /// Used for debugging. The parser does identity check on SyntaxToken members.
    /// </summary>
    enum SyntaxTokenKind
    {
        Invalid,
        OpenParenthesis,
        CloseParenthesis,
        Eof,
        Quote,
        Dot,
    }
}