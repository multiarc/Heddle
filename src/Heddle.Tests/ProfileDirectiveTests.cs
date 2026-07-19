using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The <c>@profile()</c> compile-time directive (phase 2 D2, D4, D13): both flip directions, the
    /// HED2001 unknown/empty/whitespace error, the HED2002 after-output warning (position + Fix + still
    /// applied), body-scoped isolation, and clean block removal (no residue where the directive stood).
    /// </summary>
    public class ProfileDirectiveTests
    {
        public class VModel { public string V { get; set; } }
        public class XyModel { public string X { get; set; } public string Y { get; set; } }
        public class SubtreeModel { public bool Flag { get; set; } public string V { get; set; } }

        private static HeddleTemplate Compile(string template, ExType modelType, OutputProfile profile)
        {
            HeddleTemplate.Configure(typeof(ProfileDirectiveTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static HeddleCompileResult TryCompile(string template, OutputProfile profile)
        {
            HeddleTemplate.Configure(typeof(ProfileDirectiveTests).GetTypeInfo().Assembly);
            using var t = new HeddleTemplate();
            return t.TryCompilation(template, new TemplateOptions { OutputProfile = profile });
        }

        [Fact]
        public void FlipTextDefaultToHtml()
        {
            var t = Compile("@profile(){{html}}@(V)", typeof(VModel), OutputProfile.Text);
            Assert.Equal("&lt;i&gt;", t.Generate(new VModel { V = "<i>" }));
        }

        [Fact]
        public void FlipHtmlDefaultToText()
        {
            var t = Compile("@profile(){{text}}@(V)", typeof(VModel), OutputProfile.Html);
            Assert.Equal("<i>", t.Generate(new VModel { V = "<i>" }));
        }

        [Fact]
        public void MatchIsCaseInsensitive()
        {
            Assert.Equal("&lt;i&gt;",
                Compile("@profile(){{HTML}}@(V)", typeof(VModel), OutputProfile.Text).Generate(new VModel { V = "<i>" }));
            Assert.Equal("<i>",
                Compile("@profile(){{Text}}@(V)", typeof(VModel), OutputProfile.Html).Generate(new VModel { V = "<i>" }));
        }

        [Fact]
        public void UnknownValueProducesHed2001WithValidValuesList()
        {
            var result = TryCompile("@profile(){{pdf}}", OutputProfile.Text);
            Assert.False(result.Success);
            var error = Assert.Single(result.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.UnknownOutputProfile);
            Assert.Equal("Unknown output profile 'pdf'. Valid values: text, html.", error.Error);
            // Positioned at the directive block, which sits at the top of the template (line 1).
            Assert.Equal(1, error.LinePosition.Line);
            Assert.InRange(error.Position.StartIndex, 0, "@profile()".Length);
        }

        [Fact]
        public void EmptyValueProducesHed2001WithEmptyQuotes()
        {
            var result = TryCompile("@profile(){{}}", OutputProfile.Text);
            Assert.False(result.Success);
            var error = Assert.Single(result.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.UnknownOutputProfile);
            Assert.Equal("Unknown output profile ''. Valid values: text, html.", error.Error);
        }

        [Fact]
        public void WhitespaceValueProducesHed2001WithEmptyQuotes()
        {
            var result = TryCompile("@profile(){{   }}", OutputProfile.Text);
            Assert.False(result.Success);
            var error = Assert.Single(result.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.UnknownOutputProfile);
            Assert.Equal("Unknown output profile ''. Valid values: text, html.", error.Error);
        }

        [Fact]
        public void AfterOutputWarnsHed2002ButStillApplies()
        {
            var t = Compile("@(X)@profile(){{html}}@(Y)", typeof(XyModel), OutputProfile.Text);
            // Flip is still applied: X (before) raw, Y (after) encoded.
            Assert.Equal("<i>&lt;i&gt;", t.Generate(new XyModel { X = "<i>", Y = "<i>" }));

            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.ProfileDirectiveAfterOutput);
            Assert.Equal(
                "@profile() appears after output has already been compiled; earlier output keeps the previous profile.",
                warning.Error);
            Assert.Equal("Move @profile() to the top of the template.", warning.Fix);
            // Positioned at the directive block (after the leading @(X)).
            Assert.True(warning.Position.StartIndex > 0);
        }

        [Fact]
        public void TopOfFileFlipDoesNotWarn()
        {
            var t = Compile("@profile(){{html}}@(V)", typeof(VModel), OutputProfile.Text);
            Assert.DoesNotContain(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.ProfileDirectiveAfterOutput);
        }

        [Fact]
        public void BodyScopedFlipStaysInSubtree()
        {
            // The flip inside the @if body affects only that subtree; the trailing @(V) keeps the Text default.
            var t = Compile("@if(Flag){{@profile(){{html}}[@(V)]}}[@(V)]", typeof(SubtreeModel), OutputProfile.Text);
            Assert.Equal("[&lt;i&gt;][<i>]", t.Generate(new SubtreeModel { Flag = true, V = "<i>" }));
        }

        [Fact]
        public void DirectiveBlockLeavesNoResidue()
        {
            var t = Compile("A@profile(){{html}}B", typeof(object), OutputProfile.Text);
            Assert.Equal("AB", t.Generate(null));
        }
    }
}
