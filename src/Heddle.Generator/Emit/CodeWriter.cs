using System.Text;

namespace Heddle.Generator.Emit
{
    /// <summary>A minimal indentation-aware source builder for the emitter (phase 7 WI4). Deterministic — no
    /// culture-sensitive formatting; every emitter uses this one writer so generated output is byte-stable.</summary>
    internal sealed class CodeWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        public void Indent() => _indent++;

        public void Outdent()
        {
            if (_indent > 0)
                _indent--;
        }

        /// <summary>Writes a line at the current indentation. Empty input writes a bare newline (no indent).</summary>
        public void Line(string text = "")
        {
            if (text.Length != 0)
                _sb.Append(' ', _indent * 4);
            _sb.Append(text);
            _sb.Append('\n');
        }

        /// <summary>Writes a line verbatim — no indentation prefix (for <c>#line</c> and <c>#pragma</c> directives,
        /// which must sit in column 0).</summary>
        public void Raw(string text)
        {
            _sb.Append(text);
            _sb.Append('\n');
        }

        public override string ToString() => _sb.ToString();
    }
}
