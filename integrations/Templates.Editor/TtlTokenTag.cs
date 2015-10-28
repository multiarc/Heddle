using Microsoft.VisualStudio.Text.Tagging;
using Templates.Editor.Classification;
using Templates.Editor.Error;

namespace Templates.Editor {
    internal sealed class TtlTokenTag : Token, ITag {

        public TtlTokenTag(Token token)
        {
            CapturedString = token.CapturedString;
            Type = token.Type;
            StartIndex = token.StartIndex;
            Length = token.Length;
            Error = token.Error;
        }
    }
}
