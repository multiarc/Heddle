using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Characterization pins for the <c>@&lt;&lt;{{path}}</c> composition import (phase 4 D11), written green
    /// against the current engine before the docs describe them. <c>@&lt;&lt;{{path}}</c> is the parse-time
    /// composition import: definitions merged, output chains re-based to the import position, default chains
    /// carried.
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
