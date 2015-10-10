using Antlr4.Runtime;

namespace Templates.Language
{
    public partial class TtlLexer
    {
        public int PreviousMode { get; protected set; }

        public override void Mode(int m)
        {
            PreviousMode = CurrentMode;
            base.Mode(m);
        }

        public override void Reset()
        {
            PreviousMode = CurrentMode;
            base.Reset();
        }

        public override void Emit(IToken token)
        {
            int i, la, nextLa;
            char c, prev, nextC;
            switch (token.Type)
            {
                //@ext(...)sometext handling to interpret "sometext" as TEXT
                case ID:
                    if (CurrentMode == OUT_MODE || CurrentMode == OUT_SUB || CurrentMode == DEF_OUT)
                    {
                        i = -1 - (token.StopIndex - token.StartIndex + 1);
                        la = InputStream.La(i);

                        c = (char) la;

                        while (la != -1 && c != ':' && c != '@' && c != '(' && c != ')')
                        {
                            i--;
                            la = InputStream.La(i);
                            c = (char) la;
                        }
                        if (c == ')')
                        {
                            Type = TEXT;
                            PopMode();

                            token = new CommonToken(token)
                            {
                                Type = TEXT
                            };
                        }
                    }
                    break;
                //@ext(...)(... handling to interpret "(..." as TEXT
                case OUT_PARAMSTART:
                    if (PreviousMode != DEF_OUT && CurrentMode != CALL)
                    {
                        i = -1 - (token.StopIndex - token.StartIndex + 1);
                        la = InputStream.La(i);
                        c = (char) la;
                        while (la != -1 && c != ':' && c != '@' && c != '(' && c != ')')
                        {
                            i--;
                            la = InputStream.La(i);
                            c = (char) la;
                        }
                        if (c == ')')
                        {
                            Type = TEXT;
                            PopMode();

                            token = new CommonToken(token)
                            {
                                Type = TEXT
                            };
                        }
                    }
                    break;
                //@ext(...)WS*...sometext handling to interpret "\s...sometext" as TEXT
                case OUT_WS:
                    i = -1 - (token.StopIndex - token.StartIndex + 1);
                    la = InputStream.La(i);

                    prev = (char) la;
                    while (la != -1 && prev != ':' && prev != '@' && prev != '(' && prev != ')')
                    {
                        i--;
                        la = InputStream.La(i);
                        prev = (char) la;
                    }
                    i = 1;
                    la = InputStream.La(i);
                    c = (char) la;
                    nextLa = InputStream.La(i + 1);
                    nextC = (char) nextLa;
                    if (la == -1 || c != ':' && (c != '{' || nextLa != -1 && nextC != '{') && prev == ')')
                    {
                        Type = TEXT;
                        PopMode();
                        token = new CommonToken(token)
                        {
                            Type = TEXT
                        };
                    }
                    else
                    {
                        token = NextToken();
                    }
                    break;
                case COMMENT:
                    if (CurrentMode == OUT_SUB || CurrentMode == OUT_MODE)
                    {
                        i = -1 - (token.StopIndex - token.StartIndex + 1);
                        la = InputStream.La(i);

                        prev = (char) la;
                        while (la != -1 && prev != ':' && prev != '@' && prev != '(' && prev != ')')
                        {
                            i--;
                            la = InputStream.La(i);
                            prev = (char) la;
                        }
                        i = 1;
                        la = InputStream.La(i);
                        c = (char) la;
                        nextLa = InputStream.La(i + 1);
                        nextC = (char) nextLa;
                        if (la == -1 || c != ':' && (c != '{' || nextLa == -1 || nextC != '{') && prev == ')')
                        {
                            Type = COMMENT;
                            PopMode();
                            token = new CommonToken(token)
                            {
                                Type = COMMENT
                            };
                        }
                        else
                        {
                            token = NextToken();
                        }
                    }
                    break;
                case CSHARP_END:
                    if (CurrentMode == CALL)
                    {
                        PopMode();
                        token = new CommonToken(token)
                        {
                            Type = OUT_PARAMEND
                        };
                    }
                    else
                    {
                        token = new CommonToken(token)
                        {
                            Type = CSHARP_TOKEN
                        };
                    }
                    break;
            }
            base.Emit(token);
        }
    }
}