using Microsoft.VisualStudio.Text.Tagging;
using Heddle.Editor.Classification;
using Heddle.Editor.Error;

namespace Heddle.Editor {
    internal sealed class HeddleTokenTag : Token, ITag {

        public HeddleTokenTag(Token token)
        {
            CapturedString = token.CapturedString;
            Type = token.Type;
            StartIndex = token.StartIndex;
            Length = token.Length;
            Error = token.Error;
        }
    }
}
