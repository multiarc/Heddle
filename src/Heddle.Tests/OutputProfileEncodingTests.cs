using System.Net;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>The phase 2 executable spec, written security-first (WI1): the profile x construct matrix
    /// (M01-M15), the XSS corpus (X01-X12), and the encoding-pin rows (E01-E14). Every row asserts exact
    /// output bytes; the E-rows pin the 1.x <c>WebUtility.HtmlEncode</c> baseline so the 2.0 encoder swap's
    /// golden churn is measured, not discovered.</para>
    /// <para>The default profile stays <see cref="OutputProfile.Text"/> in 1.x; <c>Html</c> behavior is
    /// selected explicitly per row via <see cref="TemplateOptions.OutputProfile"/> or a <c>@profile()</c>
    /// directive.</para>
    /// </summary>
    public class OutputProfileEncodingTests
    {
        private const string Xss = "<script>alert(1)</script>";

        public class VModel { public string V { get; set; } }
        public class PriceModel { public decimal Price { get; set; } public decimal Quantity { get; set; } }
        public class Inner { public string Name { get; set; } }
        public class ItemModel { public Inner Item { get; set; } }
        public class NullModel { public string NullProp { get; set; } }
        public class QModel { public string Q { get; set; } }
        public class FlagModel { public bool Flag { get; set; } public string UserInput { get; set; } }
        public class TagsModel { public string[] Tags { get; set; } }
        public class XyModel { public string X { get; set; } public string Y { get; set; } }
        public class UserInputModel { public string UserInput { get; set; } }
        public class TrustedModel { public string TrustedHtml { get; set; } }

        private static HeddleTemplate Compile(string template, ExType modelType, OutputProfile profile)
        {
            HeddleTemplate.Configure(typeof(OutputProfileEncodingTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { OutputProfile = profile };
            var t = new HeddleTemplate(template, new CompileContext(options, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static string Render(string template, object model, ExType modelType, OutputProfile profile)
        {
            return Compile(template, modelType, profile).Generate(model);
        }

        // ---------------- Profile x construct matrix (M01-M15) ----------------

        [Fact]
        public void M01_UnnamedMemberPath()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("<b>x</b>", Render("@(V)", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@(V)", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M02_Raw()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("<b>x</b>", Render("@raw(V)", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("<b>x</b>", Render("@raw(V)", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M03_Html()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@html(V)", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@html(V)", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M04_String()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@string(V)", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@string(V)", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M05_UnnamedBareModelString()
        {
            Assert.Equal("<b>x</b>", Render("@()", "<b>x</b>", typeof(string), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@()", "<b>x</b>", typeof(string), OutputProfile.Html));
        }

        [Fact]
        public void M06_NativeExpressionNonStringBoxing()
        {
            var m = new PriceModel { Price = 2m, Quantity = 3m };
            Assert.Equal("6", Render("@(Price * Quantity)", m, typeof(PriceModel), OutputProfile.Text));
            Assert.Equal("6", Render("@(Price * Quantity)", m, typeof(PriceModel), OutputProfile.Html));
        }

        [Fact]
        public void M07_FoldedConstant()
        {
            Assert.Equal("<i>a", Render("@(\"<i>\" + \"a\")", null, typeof(object), OutputProfile.Text));
            Assert.Equal("&lt;i&gt;a", Render("@(\"<i>\" + \"a\")", null, typeof(object), OutputProfile.Html));
        }

        [Fact]
        public void M08_StandaloneFunctionCarrier()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("<B>X</B>", Render("@upper(V)", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;B&gt;X&lt;/B&gt;", Render("@upper(V)", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M09_FunctionInsideExpression()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("<B>X</B>", Render("@(upper(V))", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;B&gt;X&lt;/B&gt;", Render("@(upper(V))", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M10_NullProperty()
        {
            var m = new NullModel { NullProp = null };
            Assert.Equal("", Render("@(NullProp)", m, typeof(NullModel), OutputProfile.Text));
            Assert.Equal("", Render("@(NullProp)", m, typeof(NullModel), OutputProfile.Html));
        }

        [Fact]
        public void M11_BodiedUnnamedIsRescopingContainer()
        {
            var m = new ItemModel { Item = new Inner { Name = "<i>" } };
            Assert.Equal("[<i>]", Render("@(Item){{[@(Name)]}}", m, typeof(ItemModel), OutputProfile.Text));
            Assert.Equal("[&lt;i&gt;]", Render("@(Item){{[@(Name)]}}", m, typeof(ItemModel), OutputProfile.Html));
        }

        [Fact]
        public void M12_OutSpliceStaysSingleEncoded()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@out():html(V)", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@out():html(V)", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M13_NestedHtmlDoubleEncodesUnderHtml()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@(html(V))", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&amp;lt;b&amp;gt;x&amp;lt;/b&amp;gt;", Render("@(html(V))", m, typeof(VModel), OutputProfile.Html));
        }

        [Fact]
        public void M14_AttributeContext()
        {
            var m = new QModel { Q = "a\"b" };
            Assert.Equal("<div data-v=\"a\"b\">", Render("<div data-v=\"@(Q)\">", m, typeof(QModel), OutputProfile.Text));
            Assert.Equal("<div data-v=\"a&quot;b\">", Render("<div data-v=\"@(Q)\">", m, typeof(QModel), OutputProfile.Html));
        }

        [Fact]
        public void M15_TopLevelChainedHtmlIsDiscarded()
        {
            var m = new VModel { V = "<b>x</b>" };
            Assert.Equal("<b>x</b>", Render("@(V):html()", m, typeof(VModel), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", Render("@(V):html()", m, typeof(VModel), OutputProfile.Html));
        }

        // ---------------- XSS corpus (X01-X12) ----------------

        [Fact]
        public void X01_FlagshipDirectiveFlip()
        {
            var m = new UserInputModel { UserInput = Xss };
            Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;",
                Render("@profile(){{html}}@(UserInput)", m, typeof(UserInputModel), OutputProfile.Text));
        }

        [Fact]
        public void X02_RawPassthroughUnderHtml()
        {
            var m = new TrustedModel { TrustedHtml = "<em>ok</em>" };
            Assert.Equal("<em>ok</em>", Render("@raw(TrustedHtml)", m, typeof(TrustedModel), OutputProfile.Html));
        }

        [Fact]
        public void X03_ProfileInheritsIntoIfBody()
        {
            Assert.Equal("<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>",
                Render("@if(Flag){{<p>@(UserInput)</p>}}", new FlagModel { Flag = true, UserInput = Xss },
                    typeof(FlagModel), OutputProfile.Html));
            Assert.Equal("",
                Render("@if(Flag){{<p>@(UserInput)</p>}}", new FlagModel { Flag = false, UserInput = Xss },
                    typeof(FlagModel), OutputProfile.Html));
        }

        [Fact]
        public void X04_ListEncodesEachElementOnce()
        {
            var m = new TagsModel { Tags = new[] { "<b>", "&" } };
            Assert.Equal("<span>&lt;b&gt;</span><span>&amp;</span>",
                Render("@list(Tags){{<span>@()</span>}}", m, typeof(TagsModel), OutputProfile.Html));
        }

        [Fact]
        public void X06_NestedHtmlDoubleEncodeUnderHtml()
        {
            var m = new UserInputModel { UserInput = "<b>x</b>" };
            Assert.Equal("&amp;lt;b&amp;gt;x&amp;lt;/b&amp;gt;",
                Render("@(html(UserInput))", m, typeof(UserInputModel), OutputProfile.Html));
        }

        [Fact]
        public void X07_UnknownProfileValue()
        {
            HeddleTemplate.Configure(typeof(OutputProfileEncodingTests).GetTypeInfo().Assembly);
            using var t = new HeddleTemplate();
            var result = t.TryCompilation("@profile(){{pdf}}", new TemplateOptions());
            Assert.False(result.Success);
            var error = Assert.Single(result.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.UnknownOutputProfile);
            Assert.Equal("Unknown output profile 'pdf'. Valid values: text, html.", error.Error);
        }

        [Fact]
        public void X09_StandaloneFunctionCarrierRedirects()
        {
            var m = new UserInputModel { UserInput = "<b>x</b>" };
            Assert.Equal("&lt;B&gt;X&lt;/B&gt;",
                Render("@upper(UserInput)", m, typeof(UserInputModel), OutputProfile.Html));
        }

        [Fact]
        public void X10_MidFileFlipLeavesEarlierOutputRaw()
        {
            var m = new XyModel { X = "<i>", Y = "<i>" };
            var t = Compile("@(X)@profile(){{html}}@(Y)", typeof(XyModel), OutputProfile.Text);
            Assert.Equal("<i>&lt;i&gt;", t.Generate(m));
            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.ProfileDirectiveAfterOutput);
            Assert.Equal("Move @profile() to the top of the template.", warning.Fix);
        }

        [Fact]
        public void X11_ControlRowsUnderTextAreRawByteExact()
        {
            Assert.Equal("<em>ok</em>",
                Render("@raw(TrustedHtml)", new TrustedModel { TrustedHtml = "<em>ok</em>" }, typeof(TrustedModel), OutputProfile.Text));
            Assert.Equal("<p><script>alert(1)</script></p>",
                Render("@if(Flag){{<p>@(UserInput)</p>}}", new FlagModel { Flag = true, UserInput = Xss }, typeof(FlagModel), OutputProfile.Text));
            Assert.Equal("<span><b></span><span>&</span>",
                Render("@list(Tags){{<span>@()</span>}}", new TagsModel { Tags = new[] { "<b>", "&" } }, typeof(TagsModel), OutputProfile.Text));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;",
                Render("@(html(UserInput))", new UserInputModel { UserInput = "<b>x</b>" }, typeof(UserInputModel), OutputProfile.Text));
            Assert.Equal("<B>X</B>",
                Render("@upper(UserInput)", new UserInputModel { UserInput = "<b>x</b>" }, typeof(UserInputModel), OutputProfile.Text));
        }

        [Fact]
        public void X12_DirectiveOverridesHtmlDefault()
        {
            var m = new UserInputModel { UserInput = "<b>x</b>" };
            Assert.Equal("<b>x</b>",
                Render("@profile(){{text}}@(UserInput)", m, typeof(UserInputModel), OutputProfile.Html));
        }

        // ---------------- Encoding-pin rows (E01-E14) ----------------

        // Each row renders @(V) under Html and asserts the exact 1.x WebUtility.HtmlEncode bytes.
        // Non-ASCII inputs use \u escapes so source encoding cannot perturb the pinned bytes; the
        // E12/E13 control characters are supplied programmatically so line-ending normalization
        // cannot touch them. See D11 for the 2.0 re-pin budget.
        [Theory]
        [InlineData("<", "&lt;")]                        // E01
        [InlineData(">", "&gt;")]                        // E02
        [InlineData("&", "&amp;")]                       // E03
        [InlineData("\"", "&quot;")]                     // E04
        [InlineData("'", "&#39;")]                       // E05
        [InlineData("+", "+")]                           // E06
        [InlineData("\u00A0", "&#160;")]                 // E07 NBSP
        [InlineData("\u00E9", "&#233;")]                 // E08 U+00E9 e-acute
        [InlineData("\u0100", "\u0100")]               // E09 U+0100 (raw)
        [InlineData("\u4E2D", "\u4E2D")]               // E10 U+4E2D CJK (raw)
        [InlineData("\uD83D\uDE00", "&#128512;")]      // E11 U+1F600 astral (surrogate pair)
        [InlineData("\r\n", "\r\n")]                     // E12 CR LF raw
        [InlineData("\t", "\t")]                         // E13 tab raw
        [InlineData("a=b/c", "a=b/c")]                   // E14 printable Basic Latin raw
        public void EncodingPin_MatchesWebUtilityBaseline(string input, string expected)
        {
            var m = new VModel { V = input };
            Assert.Equal(expected, Render("@(V)", m, typeof(VModel), OutputProfile.Html));
            // Cross-check the pinned bytes really are the WebUtility baseline the 2.0 swap re-pins.
            Assert.Equal(WebUtility.HtmlEncode(input), Render("@(V)", m, typeof(VModel), OutputProfile.Html));
        }
    }
}
