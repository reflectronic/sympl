using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Sympl.Syntax
{
    class KeywordToken : IdOrKeywordToken
    {
        KeywordToken(String id) : base(id)
        {
        }

        public static readonly KeywordToken Import = new KeywordToken("Import");
        public static readonly KeywordToken Defun = new KeywordToken("Defun");
        public static readonly KeywordToken Lambda = new KeywordToken("Lambda");
        public static readonly KeywordToken Defclass = new KeywordToken("Defclass");
        public static readonly KeywordToken Defmethod = new KeywordToken("Defmethod");
        public static readonly KeywordToken New = new KeywordToken("New");
        public static readonly KeywordToken Set = new KeywordToken("Set");
        public static readonly KeywordToken LetStar = new KeywordToken("LetStar");
        public static readonly KeywordToken Block = new KeywordToken("Block");
        public static readonly KeywordToken Loop = new KeywordToken("Loop");
        public static readonly KeywordToken Break = new KeywordToken("Break");
        public static readonly KeywordToken Continue = new KeywordToken("Continue");
        public static readonly KeywordToken Return = new KeywordToken("Return");
        public static readonly KeywordToken List = new KeywordToken("List");
        public static readonly KeywordToken Cons = new KeywordToken("Cons");
        public static readonly KeywordToken Eq = new KeywordToken("Eq");
        public static readonly KeywordToken Elt = new KeywordToken("Elt");
        public static readonly KeywordToken Nil = new KeywordToken("Nil");
        public static readonly KeywordToken True = new KeywordToken("True");
        public static readonly KeywordToken If = new KeywordToken("If");
        public static readonly KeywordToken False = new KeywordToken("False");
        public static readonly KeywordToken Add = new KeywordToken("+");
        public static readonly KeywordToken Subtract = new KeywordToken("-");
        public static readonly KeywordToken Multiply = new KeywordToken("*");
        public static readonly KeywordToken Divide = new KeywordToken("/");
        public static readonly KeywordToken Equal = new KeywordToken("=");
        public static readonly KeywordToken NotEqual = new KeywordToken("!=");
        public static readonly KeywordToken GreaterThan = new KeywordToken(">");
        public static readonly KeywordToken LessThan = new KeywordToken("<");
        public static readonly KeywordToken And = new KeywordToken("And");
        public static readonly KeywordToken Or = new KeywordToken("Or");
        public static readonly KeywordToken Not = new KeywordToken("Not");

        static readonly Dictionary<String, KeywordToken> Keywords = new Dictionary<String, KeywordToken>(StringComparer.OrdinalIgnoreCase)
        {
            { "import", Import },
            { "defun", Defun },
            { "lambda", Lambda },
            { "defclass", Defclass },
            { "defmethod", Defmethod },
            { "new", New },
            { "set", Set },
            { "let*", LetStar },
            { "block", Block },
            { "loop", Loop },
            { "break", Break },
            { "continue", Continue },
            { "return", Return },
            { "cons", Cons },
            { "eq", Eq },
            { "list", List },
            { "elt", Elt },
            { "nil", Nil },
            { "true", True },
            { "if", If },
            { "false", False },
            { "+", Add },
            { "-", Subtract },
            { "*", Multiply },
            { "/", Divide },
            { "=", Equal },
            { "!=", NotEqual },
            { ">", GreaterThan },
            { "<", LessThan },
            { "and", And },
            { "or", Or },
            { "not", Not }
        };

        public override Boolean IsKeywordToken => true;

        internal static KeywordToken GetKeywordToken(String name) => Keywords[name];

        internal static Boolean IsKeywordName(String id) => Keywords.ContainsKey(id);
    }
}