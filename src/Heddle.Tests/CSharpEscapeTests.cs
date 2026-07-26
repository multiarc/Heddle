using System.Text;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 4 WI7 (D9) — the unified C# escape set. Three tables became one, so the two things worth pinning are
    /// (a) the table's rows, and (b) that folding them together did not move a single existing byte: the string
    /// form must be identical to the pre-fold <c>PieceWriter.Escape</c> for every input that contains no lone
    /// surrogate — which is every input any golden carries.
    /// </summary>
    public class CSharpEscapeTests
    {
        #region Legacy body — verbatim pre-fold PieceWriter.Escape

        private static string LegacyPieceEscape(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\v': sb.Append("\\v"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int) c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        #endregion

        [Fact]
        public void StringForm_IsByteIdenticalToTheLegacyTable_ForEverySurrogateFreeCodeUnit()
        {
            // Exhaustive over the BMP minus the surrogate range — the only inputs whose handling the fold changed.
            for (int i = 0; i <= 0xFFFF; i++)
            {
                if (i >= 0xD800 && i <= 0xDFFF)
                    continue;
                var s = ((char) i).ToString();
                Assert.Equal(LegacyPieceEscape(s), CSharpEscape.StringLiteral(s));
            }
        }

        [Theory]
        [InlineData("", "\"\"")]
        [InlineData("plain", "\"plain\"")]
        [InlineData("a\"b", "\"a\\\"b\"")]
        [InlineData("a\\b", "\"a\\\\b\"")]
        [InlineData("a\nb\r\tc", "\"a\\nb\\r\\tc\"")]
        [InlineData("a\0\a\b\f\vz", "\"a\\0\\a\\b\\f\\vz\"")]
        [InlineData("\u0001", "\"\\u0001\"")]
        [InlineData("it's", "\"it's\"")]
        public void StringTableRows(string value, string expected) =>
            Assert.Equal(expected, CSharpEscape.StringLiteral(value));

        [Theory]
        [InlineData('a', "'a'")]
        [InlineData('\'', "'\\''")]
        [InlineData('"', "'\"'")]
        [InlineData('\\', "'\\\\'")]
        [InlineData('\n', "'\\n'")]
        [InlineData('\0', "'\\0'")]
        [InlineData('\a', "'\\a'")]
        [InlineData('\v', "'\\v'")]
        [InlineData('\u0001', "'\\u0001'")]
        public void CharTableRows(char value, string expected) =>
            Assert.Equal(expected, CSharpEscape.CharLiteral(value));

        [Fact]
        public void LoneSurrogates_AreEscaped_NotWrittenRaw()
        {
            // 04 F9: neither generator table guarded a lone surrogate in a *literal* — only the u8 twin was guarded
            // — so an unpaired code unit went raw into generated source.
            Assert.Equal("\"a\\ud800b\"", CSharpEscape.StringLiteral("a\uD800b"));
            Assert.Equal("\"\\udc00\"", CSharpEscape.StringLiteral("\uDC00"));
            Assert.Equal("'\\ud800'", CSharpEscape.CharLiteral('\uD800'));
            Assert.Equal("'\\udfff'", CSharpEscape.CharLiteral('\uDFFF'));
        }

        [Fact]
        public void WellFormedSurrogatePairs_StayVerbatim_SoTheU8TwinEncodesTheSameText()
        {
            const string emoji = "a\U0001F600b";
            Assert.Equal("\"" + emoji + "\"", CSharpEscape.StringLiteral(emoji));
            Assert.False(CSharpEscape.HasLoneSurrogate(emoji));
        }

        [Fact]
        public void LoneSurrogateDetection()
        {
            // Not a [Theory]: xUnit's data serializer mangles an unpaired surrogate in a test name.
            Assert.True(CSharpEscape.HasLoneSurrogate("\uD800"));
            Assert.True(CSharpEscape.HasLoneSurrogate("\uDC00"));
            Assert.True(CSharpEscape.HasLoneSurrogate("a\uD800"));
            Assert.True(CSharpEscape.HasLoneSurrogate("\uD800a"));
            Assert.False(CSharpEscape.HasLoneSurrogate("\uD83D\uDE00"));
            Assert.False(CSharpEscape.HasLoneSurrogate("plain"));
            Assert.False(CSharpEscape.HasLoneSurrogate(string.Empty));
        }

        [Fact]
        public void LiteralFormatterDelegatesToTheSameTable()
        {
            Assert.Equal(CSharpEscape.StringLiteral("a\tb"), LiteralFormatter.Format("a\tb"));
            Assert.Equal(CSharpEscape.CharLiteral('\uD800'), LiteralFormatter.Format('\uD800'));
        }
    }
}
