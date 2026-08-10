using System;
using System.IO;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The front-end <see cref="ParserSettings"/>/<c>ImportReader</c> seam. Asserts the
    /// build-time entry (<see cref="DocumentParser.Parse(string, ParserSettings, out string)"/> with an in-memory
    /// <c>ImportReader</c>) produces a <see cref="ParseContext"/> byte-identical to the runtime file-IO path, so the
    /// same shared front-end source can compile into the generator without disk access.
    /// </summary>
    public class ParserSettingsSeamTests
    {
        private const string ImportBody =
            "@%<greeting>{{Hello, @(Name)!}} :: dynamic%@";

        private const string MainTemplate =
            "@<<{{lib.heddle}}@\\\n@greeting(User)\n";

        [Fact]
        public void ImportReaderPathMatchesFileIoPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-seam-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "lib.heddle"), ImportBody);

                var fileContext = DocumentParser.Parse(MainTemplate,
                    new CompileContext(new TemplateOptions { RootPath = root }), out var fileClean);

                var settings = new ParserSettings
                {
                    RootPath = "<none>",
                    ImportReader = path =>
                    {
                        Assert.Equal("lib.heddle", path);
                        return ImportBody;
                    }
                };
                var seamContext = DocumentParser.Parse(MainTemplate, settings, out var seamClean);

                Assert.Equal(fileClean, seamClean);
                Assert.Equal(fileContext.Errors.Count, seamContext.Errors.Count);
                Assert.Equal(
                    fileContext.DefinitionsBlock.Definitions.Keys.OrderBy(k => k, StringComparer.Ordinal),
                    seamContext.DefinitionsBlock.Definitions.Keys.OrderBy(k => k, StringComparer.Ordinal));
                Assert.Equal(fileContext.OutputChains.Count, seamContext.OutputChains.Count);
                Assert.Contains("greeting", seamContext.DefinitionsBlock.Definitions.Keys);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* best-effort cleanup */ }
            }
        }

        [Fact]
        public void SeamErrorsLandOnParseContextNotCompileContext()
        {
            // A parse-time semantic error (duplicate definition) now flows to ParseContext.Errors and is copied
            // into CompileContext.CompileErrors by the adapter — the single copy point for the front-end seam.
            const string duplicate = "@%<a>{{x}} :: dynamic%@@%<a>{{y}} :: dynamic%@";
            var compileContext = new CompileContext(new TemplateOptions());
            var context = DocumentParser.Parse(duplicate, compileContext, out _);

            Assert.NotEmpty(context.Errors);
            Assert.Equal(context.Errors.Count, compileContext.CompileErrors.Count);
            Assert.Same(context.Errors[0], compileContext.CompileErrors[0]);
        }

        [Fact]
        public void NullImportReaderFallsBackToFileIo()
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-seam-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "lib.heddle"), ImportBody);
                var settings = new ParserSettings { RootPath = root, ImportReader = null };
                var context = DocumentParser.Parse(MainTemplate, settings, out _);
                Assert.Empty(context.Errors);
                Assert.Contains("greeting", context.DefinitionsBlock.Definitions.Keys);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
