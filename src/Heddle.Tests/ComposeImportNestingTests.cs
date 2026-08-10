using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Composition imports nested in subtemplates (<c>@if</c>/<c>@for</c>/<c>@%</c> bodies) threw
    /// <see cref="System.ArgumentException"/> due to coordinate-space mismatches. Now collects a positioned
    /// <c>HED4004</c> diagnostic and skips the import, since composition is only well-defined at document scope.
    /// </summary>
    public class ComposeImportNestingTests
    {
        private static HeddleTemplate CompileInline(string template)
        {
            HeddleTemplate.Configure(typeof(ComposeImportNestingTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { RootPath = Path.GetFullPath("TestTemplate") };
            return new HeddleTemplate(template, new CompileContext(options, typeof(object)));
        }

        private static void AssertPositionedDiagnostic(string template)
        {
            var t = CompileInline(template);

            Assert.False(t.CompileResult.Success, "nested @<< must not compile");

            // No raw ArgumentException; collects HED4004 instead.
            Assert.DoesNotContain(t.CompileResult.Errors, e => e.Exception != null);
            Assert.DoesNotContain(t.CompileResult.Errors,
                e => e.Error == "Value does not fall within the expected range.");

            var diag = t.CompileResult.Errors.FirstOrDefault(
                e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportNotTopLevel);
            Assert.True(diag != null, "expected HED4004; got: " + t.CompileResult.ToString());

            Assert.True(diag.Position.Length > 0, "diagnostic must be positioned at the directive");
            Assert.Equal("@<<", template.Substring(diag.Position.StartIndex, 3));
        }

        [Fact]
        public void ComposeImport_InsideIf_YieldsPositionedDiagnostic()
        {
            AssertPositionedDiagnostic("@if(true){{ @<<{{ergo-import-library.heddle}} X }}");
        }

        [Fact]
        public void ComposeImport_InsideFor_YieldsPositionedDiagnostic()
        {
            AssertPositionedDiagnostic("@for(3){{ @<<{{ergo-import-library.heddle}} X }}");
        }

        [Fact]
        public void ComposeImport_InsideDefinitionBody_YieldsPositionedDiagnostic()
        {
            AssertPositionedDiagnostic("@%\n<page>\n{{ @<<{{ergo-import-library.heddle}} X }}\n%@\n@page()");
        }

        [Fact]
        public void ComposeImport_TopLevel_StillComposes()
        {
            var t = CompileInline("@<<{{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains("<b>lib</b>", t.Generate(null));
        }

        [Fact]
        public void ComposeImport_ChainedTopLevel_StillComposes()
        {
            var t = CompileInline("@<<{{ergo-import-composition.heddle}}\n@lib_badge()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains("<b>lib</b>", t.Generate(null));
        }
    }
}
