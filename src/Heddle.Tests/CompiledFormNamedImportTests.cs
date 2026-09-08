using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>A composition import by registered name resolves from the artifact rows at load:
    /// the main row's re-parse expands the nick row's recorded root text, never the disk. Mirrors
    /// the host's item map (keys and validated names serve; anything else reads off disk), so build
    /// and load expand alike. Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormNamedImportTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormNamedImportTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        [Fact]
        public void NamedImportExpandsFromTheArtifactRowAtLoad()
        {
            PrecompiledTemplates.ResetForTests();
            const string nickText = "@%<n_badge>{{NICK}}%@";
            const string mainText = "BEFORE\n@<<{{Nick}}\nAFTER\n@n_badge()\n";
            // Map keys are row-faithful: keys and registered names normalized, as the host
            // writes them; the import spelling normalizes at lookup time.
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "main.heddle", mainText },
                { "nick.heddle", nickText },
                { "Nick.heddle", nickText }
            };
            string rootPath = TestCorpusIndex.CorpusDir;
            var nick = Record("nick.heddle", "Nick.heddle", nickText, map, rootPath);
            var main = Record("main.heddle", null, mainText, map, rootPath);
            var merged = CompiledArtifactMerger.Merge(new[] { nick, main });
            CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(merged), "HeddleTestAsm_NamedImport");

            var requestOptions = new TemplateOptions("main")
            {
                RootPath = rootPath,
                FileNamePostfix = ".heddle",
                OutputProfile = OutputProfile.Text,
                EnableFileChangeCheck = false,
                ExpressionMode = ExpressionMode.Native
            };
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve("main.heddle", requestOptions, out entry) &&
                entry != null, "TryResolve refused the named-import row.");
            var strategy = entry.GetStrategy(requestOptions);
            Assert.NotNull(strategy);
            string expected = DynamicRender(mainText, map, rootPath);
            Assert.Equal(expected, CompiledFormHarness.RenderStrategy(strategy, null));
            Assert.Equal("BEFORE\nAFTER\nNICK\n", expected);
        }

        [Fact]
        public void UnmappedImportFallsBackToDiskAtLoad()
        {
            PrecompiledTemplates.ResetForTests();
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "heddle-import-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                const string nickText = "@%<d_badge>{{DISK}}%@";
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "DiskNick"), nickText);
                const string mainText = "BEFORE\n@<<{{DiskNick}}\nAFTER\n@d_badge()\n";
                var map = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "main.heddle", mainText }
                };
                var main = Record("main.heddle", null, mainText, map, dir, "Heddle.Generated.S");
                var merged = CompiledArtifactMerger.Merge(new[] { main });
                CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(merged),
                    "HeddleTestAsm_DiskImport");

                var requestOptions = new TemplateOptions("main")
                {
                    RootPath = dir,
                    FileNamePostfix = ".heddle",
                    OutputProfile = OutputProfile.Text,
                    EnableFileChangeCheck = false,
                    ExpressionMode = ExpressionMode.Native
                };
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve("main.heddle", requestOptions, out entry) &&
                    entry != null, "TryResolve refused the disk-import row.");
                var strategy = entry.GetStrategy(requestOptions);
                if (strategy == null)
                {
                    string fault = entry.TryGetRequestFault(requestOptions, out var reason, out var detail)
                        ? reason + ": " + detail
                        : "<no recorded fault>";
                    Assert.True(false, "Materialization faulted: " + fault + ".");
                }

                Assert.Equal("BEFORE\nAFTER\nDISK\n",
                    CompiledFormHarness.RenderStrategy(strategy, null));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        private static CompiledArtifact Record(string key, string registeredName, string text,
            Dictionary<string, string> map, string rootPath, string entryPoint = null)
        {
            var options = new TemplateOptions("named")
            {
                RootPath = rootPath,
                FileNamePostfix = ".heddle",
                OutputProfile = OutputProfile.Text,
                EnableFileChangeCheck = false,
                ExpressionMode = ExpressionMode.Native
            };
            var context = new CompileContext(options, ExType.Dynamic);
            context.DeferUnboundFunctions = true;
            context.RecordForm = true;
            context.ImportReader = ImportMap.ReaderFor(map, rootPath);
            context.ImportIdentifier = ImportMap.IdentifierFor(map, rootPath);
            var template = new HeddleTemplate(text, context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + key + ": " +
                CompiledFormHarness.Summarize(context) + ".");
            return context.FormRecord.ToArtifact("test", "test", key, ContentHash.HashText(text),
                registeredName, entryPoint, ExType.Dynamic, false, false, "Text", "Native",
                options.TrimDirectiveLines);
        }

        private static string DynamicRender(string text, Dictionary<string, string> map, string rootPath)
        {
            var options = new TemplateOptions("named")
            {
                RootPath = rootPath,
                OutputProfile = OutputProfile.Text,
                EnableFileChangeCheck = false,
                ExpressionMode = ExpressionMode.Native
            };
            var context = new CompileContext(options, ExType.Dynamic);
            context.ImportReader = ImportMap.ReaderFor(map, rootPath);
            context.ImportIdentifier = ImportMap.IdentifierFor(map, rootPath);
            var template = new HeddleTemplate(text, context);
            Assert.True(template.CompileResult.Success, "Dynamic reference failed to compile.");
            return template.Generate(null);
        }
    }
}
