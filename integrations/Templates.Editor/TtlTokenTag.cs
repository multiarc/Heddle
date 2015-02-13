using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;
using Templates.Editor.Error;

namespace Templates.Editor {
    internal sealed class TtlTokenTag : Token, ITag {
        public TtlTokenTag(Token token, State syntaxState, TtlTemplateErrorContainer errorContainer = null)
        {
            CapturedString = token.CapturedString;
            Type = token.Type;
            StartIndex = token.StartIndex;
            Length = token.Length;
            ParserState = syntaxState;
            ErrorContainer = errorContainer;
        }

        public State ParserState { get; set; }
        public TtlTemplateErrorContainer ErrorContainer { get; set; }
    }
}
