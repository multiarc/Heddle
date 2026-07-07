using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The double-encode warning HED2003 (phase 2 D9): a bodiless unnamed <c>@(...)</c> under effective
    /// <c>Html</c> whose parenthesized chain's final producer carries <c>[EncodeOutput]</c> is flagged at the
    /// producer call. Negatives — named <c>@html()</c>, <c>@out():html()</c>, the inert top-level
    /// <c>@(V):html()</c>, and the <c>Text</c>-profile nested chain — never warn.
    /// </summary>
    public class DoubleEncodeWarningTests
    {
        public class VModel { public string V { get; set; } }

        private const string Hed2003Message =
            "'html()' output feeds the unnamed @(...) output, which already HTML-encodes under the Html profile — the value is encoded twice.";

        private static HeddleTemplate Compile(string template, OutputProfile profile)
        {
            HeddleTemplate.Configure(typeof(DoubleEncodeWarningTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, typeof(VModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static bool HasWarning(HeddleTemplate t) =>
            t.Context.CompileWarnings.Any(w => w.DiagnosticId == HeddleDiagnosticIds.RedundantEncodingExtension);

        [Fact]
        public void NestedHtmlUnderHtmlWarnsAtProducerAndDoubleEncodes()
        {
            var t = Compile("@(html(V))", OutputProfile.Html);
            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.RedundantEncodingExtension);
            Assert.Equal(Hed2003Message, warning.Error);
            Assert.Equal("Remove 'html()', or output the trusted value through @raw(...).", warning.Fix);
            // Positioned at the nested html producer (inside the outer @( ... )), not at the outer carrier.
            Assert.True(warning.Position.StartIndex > 1);
            Assert.Equal("&amp;lt;b&amp;gt;x&amp;lt;/b&amp;gt;", t.Generate(new VModel { V = "<b>x</b>" }));
        }

        [Fact]
        public void NestedHtmlUnderTextDoesNotWarn()
        {
            var t = Compile("@(html(V))", OutputProfile.Text);
            Assert.False(HasWarning(t));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", t.Generate(new VModel { V = "<b>x</b>" }));
        }

        [Fact]
        public void NamedHtmlDoesNotWarn()
        {
            var t = Compile("@html(V)", OutputProfile.Html);
            Assert.False(HasWarning(t));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", t.Generate(new VModel { V = "<b>x</b>" }));
        }

        [Fact]
        public void OutChainDoesNotWarn()
        {
            var t = Compile("@out():html(V)", OutputProfile.Html);
            Assert.False(HasWarning(t));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", t.Generate(new VModel { V = "<b>x</b>" }));
        }

        [Fact]
        public void TopLevelChainedHtmlDoesNotWarn()
        {
            // M15: the chained html() result lands in the ignored ChainedData of the bodiless sink and is
            // discarded; the sink encodes its own ModelData once. Inert, so no warning.
            var t = Compile("@(V):html()", OutputProfile.Html);
            Assert.False(HasWarning(t));
            Assert.Equal("&lt;b&gt;x&lt;/b&gt;", t.Generate(new VModel { V = "<b>x</b>" }));
        }
    }
}
