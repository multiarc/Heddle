using System;
using System.IO;
using System.Text;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Adapter-level tests for <see cref="Utf8ScopeRenderer"/> and <see cref="TextWriterScopeRenderer"/>,
    /// independent of the engine. Pins the two-tier transcode (single-call ≤ 5 461 UTF-16 units, chunked above),
    /// the tier boundary rows, the <c>RenderUtf8</c> straight copy, and GetSpan-contract compliance via the stingy
    /// writer. The oracle is always <c>Encoding.UTF8.GetBytes(input)</c>. Runs on every test TFM incl. net48.
    /// </summary>
    public class SinkAdapterTests
    {
        private static string Ascii(int n) => new string('a', n);
        private static string Repeat(string s, int n) { var sb = new StringBuilder(s.Length * n); for (int i = 0; i < n; i++) sb.Append(s); return sb.ToString(); }

        public static TheoryData<string> TierCorpus()
        {
            var data = new TheoryData<string>
            {
                "",
                "</h1>\n  <p>Made by ",
                Repeat("П", 1200),
                Ascii(3000) + Repeat("😀", 500),
                Ascii(5461),
                Ascii(5462),
                Ascii(100_000),
                Repeat("中", 100_000),
                "Café — Привет! 😀🌍",
            };
            return data;
        }

        [Theory]
        [MemberData(nameof(TierCorpus))]
        public void Utf8_GrowableWriter_ByteExact(string input)
        {
            var w = new TestBufferWriter();
            new Utf8ScopeRenderer(w).Render(input);
            Assert.Equal(Encoding.UTF8.GetBytes(input), w.ToArray());
        }

        [Theory]
        [MemberData(nameof(TierCorpus))]
        public void Utf8_StingyWriter_ByteExact(string input)
        {
            // Tests GetSpan contract with exact-sized spans.
            var w = new StingyBufferWriter();
            new Utf8ScopeRenderer(w).Render(input);
            Assert.Equal(Encoding.UTF8.GetBytes(input), w.ToArray());
        }

        [Theory]
        [MemberData(nameof(TierCorpus))]
        public void Utf8_RenderString_MatchesRenderSpan(string input)
        {
            var a = new TestBufferWriter();
            new Utf8ScopeRenderer(a).Render(input);
            var b = new TestBufferWriter();
            new Utf8ScopeRenderer(b).Render(input.AsSpan());
            Assert.Equal(a.ToArray(), b.ToArray());
        }

        [Fact]
        public void Utf8_RenderUtf8_StraightCopy()
        {
            var bytes = Encoding.UTF8.GetBytes("pre-encoded — bytes 😀");
            var w = new TestBufferWriter();
            new Utf8ScopeRenderer(w).RenderUtf8(bytes);
            Assert.Equal(bytes, w.ToArray());
        }

        [Fact]
        public void Utf8_RenderUtf8_StingyWriter_SegmentedCopy()
        {
            var bytes = Encoding.UTF8.GetBytes(Ascii(50_000));
            var w = new StingyBufferWriter();
            new Utf8ScopeRenderer(w).RenderUtf8(bytes);
            Assert.Equal(bytes, w.ToArray());
        }

        [Fact]
        public void Utf8_MultipleRendersConcatenate()
        {
            // Multiple renders must concatenate without state crossing render boundaries.
            var w = new TestBufferWriter();
            var r = new Utf8ScopeRenderer(w);
            r.Render(Ascii(6000));              // tier 2
            r.Render("😀");           // tier 1, emoji
            r.Render(Repeat("中", 10));     // tier 1, CJK
            Assert.Equal(Encoding.UTF8.GetBytes(Ascii(6000) + "😀" + Repeat("中", 10)), w.ToArray());
        }

        [Fact]
        public void Utf8_NullWriter_Throws() =>
            Assert.Throws<ArgumentNullException>(() => new Utf8ScopeRenderer(null));

        [Fact]
        public void Utf8_EmptyAndNull_NoBytes()
        {
            var w = new TestBufferWriter();
            var r = new Utf8ScopeRenderer(w);
            r.Render((string) null);
            r.Render(string.Empty);
            r.Render(ReadOnlySpan<char>.Empty);
            r.RenderUtf8(ReadOnlySpan<byte>.Empty);
            Assert.Equal(0, w.WrittenCount);
        }

        [Theory]
        [MemberData(nameof(TierCorpus))]
        public void TextWriter_ByteExact(string input)
        {
            var sw = new StringWriter();
            var r = new TextWriterScopeRenderer(sw);
            r.Render(input);
            Assert.Equal(input, sw.ToString());

            var sw2 = new StringWriter();
            new TextWriterScopeRenderer(sw2).Render(input.AsSpan());
            Assert.Equal(input, sw2.ToString());
        }

        [Fact]
        public void TextWriter_NullWriter_Throws() =>
            Assert.Throws<ArgumentNullException>(() => new TextWriterScopeRenderer(null));

        [Fact]
        public void Utf8_LoneSurrogate_ReplacedWithFffd()
        {
            // HED7005 guards only static surrogates; dynamic ones are replaced with U+FFFD by Encoding.UTF8.
            var input = "a\uD83Db";   // lone high surrogate
            var w = new TestBufferWriter();
            new Utf8ScopeRenderer(w).Render(input);
            Assert.Equal(Encoding.UTF8.GetBytes(input), w.ToArray());
        }
    }
}
