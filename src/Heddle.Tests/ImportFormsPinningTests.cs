using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;
using Heddle.TestCorpus;

namespace Heddle.Tests
{
    /// <summary>
    /// Characterization pins for the <c>@&lt;&lt;{{path}}</c> composition import, written green against the
    /// current engine before the docs describe them. <c>@&lt;&lt;{{path}}</c> is the parse-time composition import:
    /// definitions merged, output chains re-based to the import position, default chains carried.
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

        [Fact]
        public void I01_ComposeImportMergesDefinitions()
        {
            var t = CompileInline("@<<{{ergo-import-library.heddle}}\n@lib_badge()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains("<b>lib</b>", t.Generate(null));
        }

        [Fact]
        public void I02_ComposeImportGolden()
        {
            var t = CompileFixture("ergo-import-composition");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            // Normalize line endings to compare composition semantics, not newline bytes (CRLF→LF on Windows).
            var actual = t.Generate(null).Replace("\r\n", "\n");
            File.WriteAllText(TestCorpusIndex.WrittenArtifactPath("test-ergo-import-composition.html"), actual);
            var expected = File.ReadAllText("TestTemplate/generated-ergo-import-composition.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
            Assert.DoesNotContain("STATIC-IN-LIB", actual); // Imported static text never transfers
        }

        [Fact]
        public void I09_ComposeImportOfMissingFileErrorsAtOrigin()
        {
            var t = CompileInline("@<<{{ergo-import-does-not-exist.heddle}}");
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.Position.StartIndex == 0 && e.Position.Length == 0);
        }
    }
}
