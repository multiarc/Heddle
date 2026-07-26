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
    /// The legacy <c>@import()</c> raises positioned <c>HED4003</c> error in all contexts. The message names
    /// <c>@&lt;&lt;{{ path }}</c> and <c>@partial(){{ name }}</c> as replacements; <c>import</c> stays registered
    /// as a tombstone to avoid the generic fallthrough.
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

        private static HeddleCompileError AssertSinglePositionedRemovalError(string template)
        {
            var errors = CompileErrors(template);
            var removal = RemovalErrors(errors);

            var error = Assert.Single(removal);
            Assert.Equal(ExpectedMessage, error.Error);
            Assert.True(error.Position.Length > 0, "HED4003 carries a whole-call span");

            int at = template.IndexOf("@import", StringComparison.Ordinal);
            Assert.InRange(error.Position.StartIndex, at, at + "@import".Length);

            Assert.DoesNotContain(errors, e => e.Error != null && e.Error.Contains("Cannot find extension"));
            return error;
        }

        [Fact]
        public void TopLevelImportEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@import(){{ergo-import-library.heddle}}");
        }

        [Fact]
        public void ChainedConsumingImportEmitsRemovalError()
        {
            var errors = CompileErrors("@out():import(){{ergo-import-library.heddle}}");
            var removal = RemovalErrors(errors);

            var error = Assert.Single(removal);
            Assert.Equal(ExpectedMessage, error.Error);
            Assert.True(error.Position.Length > 0, "HED4003 carries a whole-call span");
            Assert.DoesNotContain(errors, e => e.Error != null && e.Error.Contains("Cannot find extension"));
        }

        [Fact]
        public void ImportNestedInIfEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@if(true){{ @import(){{ergo-import-library.heddle}} }}");
        }

        [Fact]
        public void ImportNestedInForEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@for(3){{ @import(){{ergo-import-library.heddle}} }}");
        }

        [Fact]
        public void ImportNestedInOutputBlockEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError("@out(){{ @import(){{ergo-import-library.heddle}} }}");
        }

        [Fact]
        public void ImportNestedInDefinitionBodyEmitsPositionedRemovalError()
        {
            AssertSinglePositionedRemovalError(
                "@%<page>{{ @import(){{ergo-import-library.heddle}} }}%@@page()");
        }

        [Fact]
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

        [Fact]
        public void ComposeImportRaisesNoRemovalError()
        {
            var errors = CompileErrors("@<<{{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.DoesNotContain(errors, e => e.DiagnosticId == HeddleDiagnosticIds.LegacyImportDirective);
        }
    }
}
