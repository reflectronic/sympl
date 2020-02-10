namespace Sympl.Syntax
{
    /// <summary>
    /// Used for debugging. The parser does identity check on SyntaxToken members.
    /// </summary>
    enum SyntaxTokenKind
    {
        Paren,
        CloseParen,
        EOF,
        Quote,
        Dot,
    }
}