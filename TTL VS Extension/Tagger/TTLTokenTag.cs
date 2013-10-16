using Microsoft.VisualStudio.Text.Tagging;
using Templates.Core.Data;

namespace TTL.Tagger {
    internal sealed class TTLTokenTag : ITag
    {
        public TTLTokenTag(TokenType type)
        {
            Type = type;
        }

        public TokenType Type
        {
            get;
            private set;
        }
    }
}