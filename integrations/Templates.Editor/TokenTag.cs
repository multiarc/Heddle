using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;

namespace Templates.Editor {
    public class TokenTag : Token, ITag {
        public TokenTag(Token token, State syntaxState)
        {
            CapturedString = token.CapturedString;
            Type = token.Type;
            StartIndex = token.StartIndex;
            Length = token.Length;
        }

        public State ParserState { get; set; }
    }
}
