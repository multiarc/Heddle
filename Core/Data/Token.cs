namespace Templates.Core.Data {
    /// <summary>
    /// Lexer's token
    /// </summary>
    internal class Token {
        public string CapturedString
        {
            get;
            set;
        }

        public TokenType Type
        {
            get;
            set;
        }

        public int StartIndex
        {
            get;
            set;
        }

        public int Length
        {
            get;
            set;
        }
    }
}