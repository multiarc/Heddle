using Templates.Language;

namespace Templates.Editor.Classification {
    internal class Token
    {
        public string CapturedString { get; set; }

        public int Length { get; set; }
        public int StartIndex { get; set; }
        public TtlTokenType Type { get; set; }
        public string Error { get; set; }
    }
}