// The five-entity encoder the encoded workloads are gated on.
// This is engine CONFIGURATION, not a normalization carve-out: the parity contract
// prefers configuring an engine to its documented behaviour over widening the gate.
using System.Collections.Generic;
using System.IO;
using System.Text;
using HandlebarsDotNet;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Handlebars.Net text encoder for the encoded-suite twins: escapes
    /// exactly the five-character set — <c>&amp;</c>→<c>&amp;amp;</c>, <c>&lt;</c>→<c>&amp;lt;</c>,
    /// <c>&gt;</c>→<c>&amp;gt;</c>, <c>"</c>→<c>&amp;quot;</c>, <c>'</c>→<c>&amp;#39;</c> — and
    /// passes every other character (including all non-ASCII) through unchanged, so double-mustache
    /// output is byte-identical to Heddle's <c>WebUtility.HtmlEncode</c> family on the pinned
    /// untrusted-data alphabet. Handlebars.Net's default encoder leaves <c>'</c> unescaped and
    /// decimal-escapes non-ASCII, which is a genuine gate-breaking divergence; configuring the
    /// engine (the parity contract's preferred remedy) removes it without any normalization
    /// carve-out.
    /// </summary>
    public sealed class FiveEntityTextEncoder : ITextEncoder
    {
        public void Encode(StringBuilder text, TextWriter target)
        {
            for (var i = 0; i < text.Length; i++)
                EncodeChar(text[i], target);
        }

        public void Encode(string text, TextWriter target)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (var i = 0; i < text.Length; i++)
                EncodeChar(text[i], target);
        }

        public void Encode<T>(T text, TextWriter target) where T : IEnumerator<char>
        {
            while (text.MoveNext())
                EncodeChar(text.Current, target);
        }

        private static void EncodeChar(char c, TextWriter target)
        {
            switch (c)
            {
                case '&':
                    target.Write("&amp;");
                    break;
                case '<':
                    target.Write("&lt;");
                    break;
                case '>':
                    target.Write("&gt;");
                    break;
                case '"':
                    target.Write("&quot;");
                    break;
                case '\'':
                    target.Write("&#39;");
                    break;
                default:
                    target.Write(c);
                    break;
            }
        }
    }
}
