using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Characterization pins for the two distinct import forms (phase 4 D11), written green against the
    /// current engine before the docs describe them. <c>@&lt;&lt;{{path}}</c> is the parse-time composition
    /// import (definitions merged, output chains re-based to the import position, default chains carried);
    /// <c>@import(){{path}}</c> parses into the body's already-compiled isolated context and merges nothing.
    /// </summary>
    public class ImportFormsPinningTests
    {
        private static HeddleTemplate CompileInline(string template)
        {
            HeddleTemplate.Configure(typeof(ImportFormsPinningTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { RootPath = Path.GetFullPath("TestTemplate") };
            return new HeddleTemplate(template, new CompileContext(options, typeof(object)));
        }

        private static HeddleTemplate CompileFixture(string name)
        {
            HeddleTemplate.Configure(typeof(ImportFormsPinningTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions(name)
            {
                RootPath = "TestTemplate",
                FileNamePostfix = ".heddle"
            };
            return new HeddleTemplate(new CompileContext(options, typeof(object)));
        }

        [Fact] // I01 — @<< merges definitions: the by-name call resolves and renders
        public void I01_ComposeImportMergesDefinitions()
        {
            var t = CompileInline("@<<{{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains("<b>lib</b>", t.Generate(null));
        }

        [Fact] // I02 / I03 / I08 — @<< composition golden: re-based chain, carried default, dropped static text
        public void I02_ComposeImportGolden()
        {
            var t = CompileFixture("ergo-import-composition");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            // Normalize both sides: the rendered output embeds the imported .heddle fixtures' raw newlines,
            // which are CRLF on a Windows checkout unless pinned — this test compares composition semantics,
            // not newline bytes (the .heddle files are also pinned to LF in .gitattributes).
            var actual = t.Generate(null).Replace("\r\n", "\n");
            File.WriteAllText("TestTemplate/test-ergo-import-composition.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-ergo-import-composition.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
            Assert.DoesNotContain("STATIC-IN-LIB", actual); // I03: imported static text never transfers
        }

        [Fact] // I04 — @import() merges nothing: the subsequent by-name call is unresolved
        public void I04_InlineImportMergesNothing()
        {
            var t = CompileInline("@import(){{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.Error.Contains("lib_badge"));
        }

        [Fact] // I05 — @import() of a definition-bearing library from a non-zero offset fails the compile.
        // Spec correction (phase 4 ledger): D11/I05 assumed @import() renders the block away and merges
        // nothing (golden BEFORE\n\nAFTER\n). Verified: @import() re-parses the file into the body's context
        // at that context's offset, so any position-bearing content (here the library's definition) hits
        // ParseContext.GetBlockPosition with a negative index and throws — surfaced as "Error while compiling
        // import" at the call. This is the pre-existing offset-dependent limitation recorded in ledger row
        // C19. The offset-0 "parses-into-a-dead-context, merges nothing" behavior is pinned by I04 instead;
        // a failing compile carries no golden (the spec's own convention for I04/I06/I07/I09).
        public void I05_InlineImportOfDefinitionLibraryFailsFromOffset()
        {
            var t = CompileFixture("ergo-import-inline");
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.Error == "Error while compiling import");
        }

        [Fact] // I06 — @import() of a file with a syntax error fails the importing compile (shared error list)
        public void I06_InlineImportOfBrokenFileFailsCompile()
        {
            var t = CompileInline("@import(){{ergo-import-broken.heddle}}");
            Assert.False(t.CompileResult.Success);
        }

        [Fact] // I07 — @import() of a missing file: "Error while compiling import" at the call position
        public void I07_InlineImportOfMissingFileErrorsAtCall()
        {
            var t = CompileInline("@import(){{ergo-import-does-not-exist.heddle}}");
            Assert.False(t.CompileResult.Success);
            var error = t.CompileResult.Errors.FirstOrDefault(e => e.Error == "Error while compiling import");
            Assert.True(error != null, t.CompileResult.ToString());
            Assert.True(error.Position.Length > 0, "@import() missing-file error is positioned at the call");
        }

        [Fact] // I09 — @<< of a missing file: walk-time exception recorded at 0:0 (D11 corrected row)
        public void I09_ComposeImportOfMissingFileErrorsAtOrigin()
        {
            var t = CompileInline("@<<{{ergo-import-does-not-exist.heddle}}");
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.Position.StartIndex == 0 && e.Position.Length == 0);
        }
    }
}
