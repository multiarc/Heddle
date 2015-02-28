using Antlr4.Runtime;

namespace Templates.Language {
    public partial class TtlLexer {
        public int CurrentMode { get; private set; }

        public override void Mode(int m) {
            CurrentMode = m;
            base.Mode(m);
        }

        public override void Reset() {
            CurrentMode = 0;
            base.Reset();
        }

        public override void Emit(IToken token) {
            switch (token.Type) {
            //@ext(...)sometext handling to interpret "sometext" as TEXT
            case OUT_ID:
                int i = -2;
                int la = InputStream.La(i);

                char c = (char)la;
                while (la != -1 && c != ':' && c != '@' && c != '(' && c != ')') {
                    i--;
                    c = (char)InputStream.La(i);
                }
                if (c == ')') {
                    Type = TEXT;
                    if (CurrentMode == CALL)
                        PopMode();
                    PopMode();

                    token = new CommonToken(token)
                    {
                        Type = TEXT
                    };
                }
                break;
            //@ext(...)(... handling to interpret "(..." as TEXT
            case OUT_PARAMSTART:
                i = -2;
                la = InputStream.La(i);
                c = (char)la;
                while (la != -1 && c != ':' && c != '@' && c != '(' && c != ')') {
                    i--;
                    c = (char)InputStream.La(i);
                }
                if (c == ')') {
                    Type = TEXT;
                    if (CurrentMode == CALL)
                        PopMode();
                    PopMode();

                    token = new CommonToken(token)
                    {
                        Type = TEXT
                    };
                }
                break;
            //@ext(...)WS*...sometext handling to interpret "\s...sometext" as TEXT
            case OUT_WS:
                i = -2;
                la = InputStream.La(i);

                char prev = (char)la;
                while (la != -1 && prev != ':' && prev != '@' && prev != '(' && prev != ')') {
                    i--;
                    prev = (char)InputStream.La(i);
                }
                i = 1;
                la = InputStream.La(i);
                c = (char)la;
                if (la != -1 && c != ':' && c != '[' && prev == ')') {
                    Type = TEXT;
                    PopMode();

                    token = new CommonToken(token)
                    {
                        Type = TEXT
                    };
                }
                else {
                    Type = WHITESPACE;
                    token = new CommonToken(token)
                    {
                        Type = WHITESPACE
                    };
                }
                break;
            }
            base.Emit(token);
        }
    }
}
