using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace Templates.Language {
    public partial class TtlLexer {
        public override void Emit(IToken token) {
            if (_type == OUT_ID) {
                int i = -1;
                char c = (char)_input.La(i);
                while (c != ':' && c != '@' && c != '(' && c != ')') {
                    i--;
                    c = (char)_input.La(i);
                }
                if (c == ')') {
                    Type = TEXT;
                    if (_mode == CALL)
                        PopMode();
                    PopMode();
                    
                    token = new CommonToken(token)
                    {
                        Type = TEXT
                    };
                }
            }

            base.Emit(token);
        }
    }
}
