using Heddle.Extensions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// C2-R7 escape-set exhaustive coverage for the three context encoders (<c>@attr</c>, <c>@js</c>, <c>@url</c>),
    /// exercised directly against <see cref="ContextEncoders"/>: every normative character individually, a
    /// passthrough string proving the original reference is returned unchanged (allocation-conscious, C2-R1), and the
    /// stringify contract (invariant culture). All non-printable/high code points are built numerically so no literal
    /// control byte lives in this source file.
    /// </summary>
    public class ContextEncodersTests
    {
        // ---- @attr (C2-R2): & < > " ' ----

        [Theory]
        [InlineData("&", "&amp;")]
        [InlineData("<", "&lt;")]
        [InlineData(">", "&gt;")]
        [InlineData("\"", "&quot;")]
        [InlineData("'", "&#39;")]
        [InlineData("a&b<c>d\"e'f", "a&amp;b&lt;c&gt;d&quot;e&#39;f")]
        public void Attr_EscapesExactSet(string input, string expected)
        {
            Assert.Equal(expected, ContextEncoders.EscapeAttribute(input));
        }

        [Fact]
        public void Attr_Passthrough_ReturnsOriginalReference()
        {
            var s = "plain text 123 / : ; = ? no-attr-specials";
            Assert.Same(s, ContextEncoders.EscapeAttribute(s));
        }

        [Fact]
        public void Attr_EmptyAndNull_ReturnOriginal()
        {
            Assert.Same("", ContextEncoders.EscapeAttribute(""));
            Assert.Null(ContextEncoders.EscapeAttribute(null));
        }

        // ---- @js (C2-R3) ----

        [Theory]
        [InlineData("\\", "\\\\")]
        [InlineData("\"", "\\\"")]
        [InlineData("'", "\\'")]
        [InlineData("`", "\\`")]
        [InlineData("\n", "\\n")]
        [InlineData("\r", "\\r")]
        [InlineData("\t", "\\u0009")]
        [InlineData("<", "\\u003C")]
        [InlineData("&", "\\u0026")]
        public void Js_EscapesExactSet(string input, string expected)
        {
            Assert.Equal(expected, ContextEncoders.EscapeJs(input));
        }

        [Theory]
        [InlineData(0x2028, "\\u2028")]  // JS line separator
        [InlineData(0x2029, "\\u2029")]  // JS paragraph separator
        [InlineData(0x0000, "\\u0000")]  // C0 controls (NUL .. US), each as \u00XX
        [InlineData(0x0001, "\\u0001")]
        [InlineData(0x0007, "\\u0007")]
        [InlineData(0x000B, "\\u000B")]
        [InlineData(0x001F, "\\u001F")]
        public void Js_EscapesCodePoint(int codePoint, string expected)
        {
            var input = ((char)codePoint).ToString();
            Assert.Equal(expected, ContextEncoders.EscapeJs(input));
        }

        [Fact]
        public void Js_AllC0Controls_ProduceExpectedEscapes()
        {
            for (int cp = 0x00; cp <= 0x1F; cp++)
            {
                var input = ((char)cp).ToString();
                string expected;
                if (cp == '\n') expected = "\\n";
                else if (cp == '\r') expected = "\\r";
                else expected = "\\u" + cp.ToString("X4");
                Assert.Equal(expected, ContextEncoders.EscapeJs(input));
            }
        }

        [Fact]
        public void Js_BlocksScriptClose_ButLeavesGtAndSlash()
        {
            // Only '<' is escaped (blocks </script>); '>' and '/' are left as-is.
            Assert.Equal("\\u003C/script>", ContextEncoders.EscapeJs("</script>"));
        }

        [Fact]
        public void Js_Passthrough_ReturnsOriginalReference()
        {
            var s = "Hello, World! 123 (safe) = ok";
            Assert.Same(s, ContextEncoders.EscapeJs(s));
        }

        [Fact]
        public void Js_EmptyAndNull_ReturnOriginal()
        {
            Assert.Same("", ContextEncoders.EscapeJs(""));
            Assert.Null(ContextEncoders.EscapeJs(null));
        }

        // ---- @url (C2-R4): Uri.EscapeDataString semantics ----

        [Theory]
        [InlineData(" ", "%20")]
        [InlineData("&", "%26")]
        [InlineData("/", "%2F")]
        [InlineData("?", "%3F")]
        [InlineData("=", "%3D")]
        [InlineData("a b&c", "a%20b%26c")]
        public void Url_PercentEncodesComponent(string input, string expected)
        {
            Assert.Equal(expected, ContextEncoders.EscapeUrl(input));
        }

        [Fact]
        public void Url_NonAscii_Utf8PercentEncoded()
        {
            // U+00E9 'e-acute' -> UTF-8 0xC3 0xA9 -> "%C3%A9" (uppercase hex).
            var input = ((char)0x00E9).ToString();
            Assert.Equal("%C3%A9", ContextEncoders.EscapeUrl(input));
        }

        [Fact]
        public void Url_Passthrough_AllUnreserved_ReturnsOriginalReference()
        {
            var s = "abcABC0123-._~";
            Assert.Same(s, ContextEncoders.EscapeUrl(s));
        }

        [Fact]
        public void Url_EmptyAndNull_ReturnOriginal()
        {
            Assert.Same("", ContextEncoders.EscapeUrl(""));
            Assert.Null(ContextEncoders.EscapeUrl(null));
        }

        // ---- Stringify (C2-R1): non-string input under invariant culture ----

        [Fact]
        public void Stringify_NonString_UsesInvariantCulture()
        {
            Assert.Equal("42", ContextEncoders.Stringify(42));
            Assert.Equal("1.5", ContextEncoders.Stringify(1.5d));   // invariant '.' not a locale ','
            Assert.Same("already", ContextEncoders.Stringify("already"));
        }
    }
}
