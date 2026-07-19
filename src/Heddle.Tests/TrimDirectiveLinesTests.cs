using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Tests.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The directive-line trimming torture corpus (phase 4 D6–D8). Every row is rendered with the option OFF
    /// (byte-identical current output — the compatibility guarantee) and ON (the whole-line rule). Covers
    /// LF/CRLF/CR, indentation, trailing spaces, shared lines, <c>@\</c> idempotence, comment remnants,
    /// multi-line definitions, <c>@&lt;&lt;</c> lines, <c>@profile</c> lines, <c>@param</c> non-eligibility,
    /// EOF, blank-line preservation, body-nested directives, and branch-adjacent composition.
    /// </summary>
    public class TrimDirectiveLinesTests
    {
        public class VModel { public string V { get; set; } }
        public class NameModel { public string Name { get; set; } }
        public class FModel { public bool F { get; set; } }

        private static string Render(string template, ExType modelType, object model, bool trim,
            OutputProfile profile = OutputProfile.Text)
        {
            HeddleTemplate.Configure(typeof(TrimDirectiveLinesTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                TrimDirectiveLines = trim,
                OutputProfile = profile,
                RootPath = Path.GetFullPath("TestTemplate")
            };
            var t = new HeddleTemplate(template, new CompileContext(options, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Theory]
        [InlineData("@using(){{System.Linq}}\nX", "X", "\nX")]                                   // T01 LF
        [InlineData("@using(){{System.Linq}}\r\nX", "X", "\r\nX")]                               // T02 CRLF pair
        [InlineData("  @model(){{dynamic}}\nX", "X", "  \nX")]                                   // T03 leading indentation
        [InlineData("@model(){{dynamic}}  \t\nX", "X", "  \t\nX")]                               // T04 trailing spaces/tabs
        [InlineData("A @using(){{System.Linq}}\nX", "A \nX", "A \nX")]                           // T05 not whole-line
        [InlineData("@using(){{System.Linq}}@model(){{dynamic}}\nX", "X", "\nX")]                // T06 shared line composes
        [InlineData("@using(){{System.Linq}}@\\\nX", "X", "X")]                                  // T07 @\ idempotent
        [InlineData("@using(){{System.Linq}}\n@* note *@\n@model(){{dynamic}}\nX", "X", "\n\n\nX")] // T08 comment remnant
        [InlineData("@model(){{dynamic}} @* note *@\nX", "X", " \nX")]                           // T09 same-line comment
        [InlineData("@%<d>{{y}}%@\nX", "X", "\nX")]                                              // T10 single-line def
        [InlineData("@%\n<d>{{y}}\n%@\nX", "X", "\nX")]                                          // T11 multi-line def
        [InlineData("X\n@using(){{System.Linq}}", "X\n", "X\n")]                                 // T15 EOF terminator (previous line intact)
        [InlineData("@using(){{System.Linq}}\n\nX", "\nX", "\n\nX")]                             // T16 authored blank line preserved
        [InlineData("@using(){{System.Linq}}\rX", "X", "\rX")]                                   // T19 bare CR
        public void TrimTortureRow(string template, string expectedOn, string expectedOff)
        {
            Assert.Equal(expectedOff, Render(template, typeof(object), null, trim: false));
            Assert.Equal(expectedOn, Render(template, typeof(object), null, trim: true));
        }

        [Fact] // T12 — parse-time import line (output-free target, see the phase 4 ledger correction)
        public void T12_ImportLineTrims()
        {
            const string template = "@<<{{ergo-import-empty.heddle}}\nX";
            Assert.Equal("\nX", Render(template, typeof(object), null, trim: false));
            Assert.Equal("X", Render(template, typeof(object), null, trim: true));
        }

        [Fact] // T13 — phase 2 body-form @profile() directive trims; @(V) encodes under Html
        public void T13_ProfileBodyFormTrims()
        {
            const string template = "@profile(){{html}}\n@(V)";
            var model = new VModel { V = "<x>" };
            Assert.Equal("\n&lt;x&gt;", Render(template, typeof(VModel), model, trim: false));
            Assert.Equal("&lt;x&gt;", Render(template, typeof(VModel), model, trim: true));
        }

        [Fact] // T14 — @param is not eligible (its block stays, renders nothing, line intact)
        public void T14_ParamLineNotEligible()
        {
            const string template = "@param(Name)\nX";
            var model = new NameModel { Name = "n" };
            Assert.Equal("\nX", Render(template, typeof(NameModel), model, trim: false));
            Assert.Equal("\nX", Render(template, typeof(NameModel), model, trim: true));
        }

        [Fact] // T17 — branch-adjacent directive: the set ends at the directive, gap text renders, no HED3xxx
        public void T17_BranchAdjacentDirectiveComposes()
        {
            const string template = "@if(F){{a}}\n@using(){{System.Linq}}\n@else(){{b}}";
            var model = new FModel { F = false };
            AssertNoBranchDiagnostics(template, typeof(FModel));
            Assert.Equal("\n\nb", Render(template, typeof(FModel), model, trim: false));
            Assert.Equal("\nb", Render(template, typeof(FModel), model, trim: true));
        }

        [Fact] // T18 — trimming applies inside subtemplate bodies via the inherited option
        public void T18_BodyNestedDirectiveTrims()
        {
            const string template = "@if(F){{\n  @using(){{System.Linq}}\n  body}}";
            var model = new FModel { F = true };
            Assert.Equal("\n  \n  body", Render(template, typeof(FModel), model, trim: false));
            Assert.Equal("\n  body", Render(template, typeof(FModel), model, trim: true));
        }

        [Fact] // ergo-trim-preamble golden pair (roadmap criterion 3)
        public void ErgoTrimPreambleGoldenPair()
        {
            HeddleTemplate.Configure(typeof(TrimDirectiveLinesTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/ergo-trim-preamble.heddle").Replace("\r\n", "\n");

            var off = Render(document, typeof(TestDataStructure), new TestDataStructure(), trim: false);
            File.WriteAllText("TestTemplate/test-ergo-trim-off.html", off);
            Assert.Equal(File.ReadAllText("TestTemplate/generated-ergo-trim-off.html").Replace("\r\n", "\n"), off);

            var on = Render(document, typeof(TestDataStructure), new TestDataStructure(), trim: true);
            File.WriteAllText("TestTemplate/test-ergo-trim-on.html", on);
            Assert.Equal(File.ReadAllText("TestTemplate/generated-ergo-trim-on.html").Replace("\r\n", "\n"), on);
        }

        private static void AssertNoBranchDiagnostics(string template, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(TrimDirectiveLinesTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { TrimDirectiveLines = true }, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.BranchTextStripped);
            Assert.DoesNotContain(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
        }
    }
}
