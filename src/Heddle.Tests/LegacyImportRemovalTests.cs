using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// import-removal-spec (D4/D5): <c>@import()</c> has been removed. Any call site now produces a single
    /// positioned <c>HED4003</c> removal <b>error</b> (no longer a warning), for every call shape — top-level,
    /// chained/consuming, and nested inside an <c>@if</c>/<c>@for</c> body, an output block, or a definition body.
    /// The message names <c>@&lt;&lt;{{ path }}</c> and <c>@partial(){{ name }}</c> as the replacements and is never
    /// the generic "Cannot find extension" fallthrough (G2 — the <c>import</c> name stays registered as a
    /// tombstone). <c>@&lt;&lt;</c> composition never raises <c>HED4003</c>. Errors are read from the compile
    /// result's error list filtered by <see cref="HeddleDiagnosticIds.LegacyImportDirective"/>.
    /// </summary>
    public class LegacyImportRemovalTests
    {
        private const string ExpectedMessage =
            "'@import' has been removed. Use '@<<{{ path }}' to share definitions and layouts across files, " +
            "or '@partial(){{ name }}' to embed another template's rendered output inline. " +
            "See docs/language-reference.md#imports--.";

        private static List<HeddleCompileError> CompileErrors(string template)
        {
            HeddleTemplate.Configure(typeof(LegacyImportRemovalTests).GetTypeInfo().Assembly);
            var context = new CompileContext(
                new TemplateOptions { RootPath = Path.GetFullPath("TestTemplate") }, typeof(object));
            using (var t = new HeddleTemplate(template, context))
            {
                _ = t.CompileResult; // force the compile
                return t.CompileResult.Errors.ToList();
            }
        }

        private static List<HeddleCompileError> RemovalErrors(List<HeddleCompileError> errors) =>
            errors.Where(e => e.DiagnosticId == HeddleDiagnosticIds.LegacyImportDirective).ToList();

        // Asserts the single positioned HED4003 removal error for a call shape that carries exactly one @import:
        // exact D5 message, a positioned whole-call span starting at the @import call, and — G2 — that no error is
        // the generic "Cannot find extension" fallthrough.
        private static HeddleCompileError AssertSinglePositionedRemovalError(string template)
        {
            var errors = CompileErrors(template);
            var removal = RemovalErrors(errors);

            var error = Assert.Single(removal);
            Assert.Equal(ExpectedMessage, error.Error);
            Assert.True(error.Position.Length > 0, "HED4003 carries a whole-call span");

            // Positioned at the @import call (the whole-call span begins within the '@import(' opening).
            int at = template.IndexOf("@import", StringComparison.Ordinal);
            Assert.InRange(error.Position.StartIndex, at, at + "@import".Length);

            // G2: never the generic unknown-extension fallthrough.
            Assert.DoesNotContain(errors, e => e.Error != null && e.Error.Contains("Cannot find extension"));
            return error;
        }

        [Fact] // Top-level: @import(){{path}} → one positioned HED4003 error at the call.
        public void TopLevelImportEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@import(){{ergo-import-library.heddle}}");
        }

        [Fact] // Chained / consuming: @out():import(){{path}} — the consumed import call still errors exactly once.
        public void ChainedConsumingImportEmitsRemovalError()
        {
            var errors = CompileErrors("@out():import(){{ergo-import-library.heddle}}");
            var removal = RemovalErrors(errors);

            var error = Assert.Single(removal);
            Assert.Equal(ExpectedMessage, error.Error);
            Assert.True(error.Position.Length > 0, "HED4003 carries a whole-call span");
            Assert.DoesNotContain(errors, e => e.Error != null && e.Error.Contains("Cannot find extension"));
        }

        [Fact] // Nested in an @if body.
        public void ImportNestedInIfEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@if(true){{ @import(){{ergo-import-library.heddle}} }}");
        }

        [Fact] // Nested in a @for body.
        public void ImportNestedInForEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@for(3){{ @import(){{ergo-import-library.heddle}} }}");
        }

        [Fact] // Nested inside an @out output block (an output-producing subtemplate context).
        public void ImportNestedInOutputBlockEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@out(){{ @import(){{ergo-import-library.heddle}} }}");
        }

        [Fact] // Nested inside a @%…%@ definition body.
        public void ImportNestedInDefinitionBodyEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError(
                "@%<page>{{ @import(){{ergo-import-library.heddle}} }}%@@page()");
        }

        [Fact] // Two call sites → two positioned removal errors at distinct offsets.
        public void TwoImportCallsEmitTwoDistinctlyPositionedRemovalErrors()
        {
            var errors = CompileErrors(
                "@import(){{ergo-import-library.heddle}}\n@import(){{ergo-import-empty.heddle}}");
            var removal = RemovalErrors(errors);

            Assert.Equal(2, removal.Count);
            Assert.All(removal, e => Assert.Equal(ExpectedMessage, e.Error));
            Assert.All(removal, e => Assert.True(e.Position.Length > 0));
            // Distinct call sites → distinct positions.
            Assert.Equal(2, removal.Select(e => e.Position.StartIndex).Distinct().Count());
            Assert.DoesNotContain(errors, e => e.Error != null && e.Error.Contains("Cannot find extension"));
        }

        [Fact] // @<< composition import never raises HED4003 (G3 — the replacements are untouched).
        public void ComposeImportRaisesNoRemovalError()
        {
            var errors = CompileErrors("@<<{{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.DoesNotContain(errors, e => e.DiagnosticId == HeddleDiagnosticIds.LegacyImportDirective);
        }
    }
}
