using System;
using System.IO;
using System.Text;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Phase 8 WI2 — adapter-level tests for <see cref="Utf8ScopeRenderer"/> and <see cref="TextWriterScopeRenderer"/>,
    /// independent of the engine. Pins the D5 two-tier transcode (single-call ≤ 5 461 UTF-16 units, chunked above),
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
                "",                                             // empty (skipped)
                "</h1>\n  <p>Made by ",                         // 19 ASCII, tier 1
                Repeat("П", 1200),                          // 1 200 Cyrillic (2-byte), tier 1
                Ascii(3000) + Repeat("😀", 500),       // 3000 ASCII + 500 emoji pairs (4 B), tier 1 (4000 units)
                Ascii(5461),                                     // tier boundary (tier 1)
                Ascii(5462),                                     // one over the boundary (tier 2)
                Ascii(100_000),                                  // tier 2, ASCII
                Repeat("中", 100_000),                       // tier 2, CJK (3-byte)
                "Café — Привет! 😀🌍", // mixed scripts + emoji
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
            // Exactly-sizeHint spans, fresh each call — the GetSpan-contract stress. Byte output must be identical.
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
            // Chunked and single-call writes interleave in call order with no state crossing a Render boundary.
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
            // A dynamic value may carry a lone surrogate at run time (HED7005 only guards static pieces). Encoding.UTF8
            // replaces it with U+FFFD (EF BF BD), identically in the adapter and in the oracle — D5 ill-formed parity.
            var input = "a\uD83Db";   // lone high surrogate
            var w = new TestBufferWriter();
            new Utf8ScopeRenderer(w).Render(input);
            Assert.Equal(Encoding.UTF8.GetBytes(input), w.ToArray());
        }
    }
}
