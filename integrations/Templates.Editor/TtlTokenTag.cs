using Microsoft.VisualStudio.Text.Tagging;
using Templates.Editor.Error;

namespace Templates.Editor {
    internal sealed class TtlTokenTag : Token, ITag {

        public TtlTokenTag(Token token, TtlTemplateErrorContainer errorContainer = null)
        {
            CapturedString = token.CapturedString;
            Type = token.Type;
            StartIndex = token.StartIndex;
            Length = token.Length;
            ErrorContainer = errorContainer;
        }

        //public State ParserState { get; set; }
        public TtlTemplateErrorContainer ErrorContainer { get; set; }
    }
}
