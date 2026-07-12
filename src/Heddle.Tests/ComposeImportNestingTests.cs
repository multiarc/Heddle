using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Regression for the pre-existing engine bug where a <c>@&lt;&lt;{{ path }}</c> composition import nested
    /// inside a subtemplate (an <c>@if</c>/<c>@for</c> body or a definition body) threw a raw
    /// <see cref="System.ArgumentException"/> ("Value does not fall within the expected range.") out of the
    /// compile path. The imported file is parsed in its own 0-based coordinate space, but the enclosing
    /// sub-context carries a non-zero offset, so <c>ParseContext.GetBlockPosition</c> computed a negative index and
    /// <c>BlockPosition</c>'s ctor threw. Composition is only well-defined at document scope, so the engine now
    /// collects a positioned <c>HED4004</c> diagnostic (never throws) and skips the import.
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

            // The raw offset defect (ArgumentException surfaced through the last-ditch exception handler) is gone:
            // no compile error carries a raw exception, and none is the un-positioned 0:0 fallback.
            Assert.DoesNotContain(t.CompileResult.Errors, e => e.Exception != null);
            Assert.DoesNotContain(t.CompileResult.Errors,
                e => e.Error == "Value does not fall within the expected range.");

            var diag = t.CompileResult.Errors.FirstOrDefault(
                e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportNotTopLevel);
            Assert.True(diag != null, "expected HED4004; got: " + t.CompileResult.ToString());

            // Positioned at the @<< directive itself, not at 0:0.
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

        [Fact] // The top-level form is unaffected: it still composes and renders the imported definition.
        public void ComposeImport_TopLevel_StillComposes()
        {
            var t = CompileInline("@<<{{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains("<b>lib</b>", t.Generate(null));
        }

        [Fact] // Chained composition is unaffected: root @<< a file that itself top-level @<< another file. The
        // re-parse of the composed file inherits offset 0 (fresh 0-based), so the precise offset guard leaves it
        // alone — the whole @<< chain composes and the deepest definition still renders.
        public void ComposeImport_ChainedTopLevel_StillComposes()
        {
            // ergo-import-composition.heddle top-level composes ergo-import-library.heddle and calls @lib_badge().
            var t = CompileInline("@<<{{ergo-import-composition.heddle}}\n@lib_badge()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains("<b>lib</b>", t.Generate(null));
        }
    }
}
