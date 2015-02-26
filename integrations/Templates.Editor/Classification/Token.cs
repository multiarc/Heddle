using Templates.Editor.Classification;

namespace Templates.Editor {
    internal class Token {
        public string CapturedString {
            get;
            internal set;
        }
        public int Length {
            get;
            internal set;
        }
        public int StartIndex {
            get;
            internal set;
        }
        public TokenType Type {
            get;
            internal set;
        }
    }
}