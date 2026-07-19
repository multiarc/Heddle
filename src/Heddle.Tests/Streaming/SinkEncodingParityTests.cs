using System;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Phase 8 WI4 — the runtime-backend cells of the D9 encoding-parity matrix (rows 1–3, 7–9): for each corpus
    /// fixture × {Text, Html} × {string, TextWriter, byte} the outputs are byte-identical (byte sink UTF-8-normalized).
    /// The encoding sites do not move (phase 2 seams), so parity is structural: encode (chars) happens before any byte
    /// transcode, single-encoded, and the encode proxy is not IUtf8ScopeRenderer, so pre-encoded bytes cannot bypass it.
    /// </summary>
    public class SinkEncodingParityTests
    {
        public class X { public string Body { get; set; } public string Trusted { get; set; } }

        private static X Xss() => new X
        {
            Body = "<script>alert('x&y')</script> \"q\" 'a' <b>&amp;</b>",
            Trusted = "<em>ok</em> — Привет 😀"
        };

        [Theory]
        [InlineData("<p>@(Body)</p> <div>@(Trusted)</div>")]
        [InlineData("@if(Body){{<x>@(Body)</x>}}@else(){{none}}")]
        public void TextProfile_ThreeSinksByteIdentical(string doc)
        {
            SinkTestHarness.AssertThreeSinkParity(doc, Xss(), typeof(X), OutputProfile.Text);
        }

        [Theory]
        [InlineData("<p>@(Body)</p> <div>@(Trusted)</div>")]
        [InlineData("@if(Body){{<x>@(Body)</x>}}@else(){{none}}")]
        public void HtmlProfile_ThreeSinksByteIdentical_SingleEncoded(string doc)
        {
            var s = SinkTestHarness.AssertThreeSinkParity(doc, Xss(), typeof(X), OutputProfile.Html);
            // Single-encoded: the dangerous '<' becomes &lt; exactly once, never &amp;lt; (double-encode).
            Assert.Contains("&lt;script&gt;", s);
            Assert.DoesNotContain("<script>", s);
            Assert.DoesNotContain("&amp;lt;", s);
        }

        [Fact]
        public void HtmlProfile_ByteSink_MatchesStringPathUtf8Exactly()
        {
            // Row 9: encode (chars) → transcode (D5). The proxy is not IUtf8ScopeRenderer, so no byte write bypasses it.
            var t = SinkTestHarness.Compile("<p>@(Body)</p>", typeof(X), OutputProfile.Html);
            var model = Xss();
            var s = t.Generate(model);
            var bw = new TestBufferWriter();
            t.Generate(model, bw);
            Assert.Equal(Encoding.UTF8.GetBytes(s), bw.ToArray());
        }
    }
}
