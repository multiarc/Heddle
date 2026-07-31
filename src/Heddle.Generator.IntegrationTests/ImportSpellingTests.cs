using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The two tiers do not identify an <c>@&lt;&lt;</c> import the same way. The engine resolves it against the
    /// resolver root through <c>Path.GetFullPath</c>, which cancels a <c>..</c> against the segment before it; the
    /// generator has no file system and matches the spelling against the keys of the <c>&lt;HeddleTemplate&gt;</c>
    /// items, where a <c>..</c> is not a legal key segment at all. So one file the engine read and rendered was an
    /// import nobody had included, and a working template failed the consumer's build.
    /// <para>The rows that must keep refusing are the point: a <c>..</c> reaching <i>above</i> the root, and a
    /// spelling differing only in case, both name nothing on either tier. Applying <c>..</c> is not the same as
    /// accepting any spelling that happens to end in the right file name.</para>
    /// </summary>
    public class ImportSpellingTests : IDisposable
    {
        private const string LibraryKey = "lib.heddle";
        private const string Library = "@%\n<greet>\n{{[[hello]]}}\n%@\n";
        private const string TargetKey = "views/target.heddle";

        private readonly string _root;

        public ImportSpellingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "heddle-import-spelling-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, LibraryKey), Library);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        private static string Target(string spelling) => "@<<{{" + spelling + "}}\n@greet()\n";

        private IReadOnlyList<(string key, string content)> Corpus(string spelling) =>
            new[] { (LibraryKey, Library), (TargetKey, Target(spelling)) };

        private HeddleTemplate Engine(string spelling) =>
            new HeddleTemplate(Target(spelling),
                new CompileContext(
                    new TemplateOptions { RootPath = _root + Path.DirectorySeparatorChar, FileNamePostfix = ".heddle" },
                    ExType.Dynamic));

        /// <summary>Every spelling the engine resolves to the same file, including the three that only resolve once
        /// <c>..</c> has been applied. All four must precompile and render the engine's bytes; the first is the
        /// control that says the harness is measuring the right thing.</summary>
        [Theory]
        [InlineData("lib.heddle")]
        [InlineData("x/../lib.heddle")]
        [InlineData("sub/../lib.heddle")]
        [InlineData("sub/./../lib.heddle")]
        public void AnImportSpellingTheEngineResolvesToAnIncludedFilePrecompiles(string spelling)
        {
            var engine = Engine(spelling);
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());
            Assert.Equal("[[hello]]\n", engine.Generate(null));

            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(Corpus(spelling), TargetKey,
                Target(spelling), null, null, _root);
            Assert.Equal("[[hello]]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>Spellings that name no file the engine can read either. A <c>..</c> with nothing left to cancel
        /// against reaches above the root, and a key differs by case — both refuse on both tiers, and the build
        /// error is matched by an engine error rather than standing alone over a template that renders.</summary>
        [Theory]
        [InlineData("./../outside/lib.heddle")]
        [InlineData("LIB.heddle")]
        [InlineData("x/../../lib.heddle")]
        public void AnImportSpellingTheEngineResolvesToNothingIsRefusedByBothTiers(string spelling)
        {
            var engine = Engine(spelling);
            Assert.False(engine.CompileResult.Success);
            Assert.Contains(engine.CompileResult.Errors, e => e.DiagnosticId == "HED4009");

            var gen = DifferentialHarness.Generate(Corpus(spelling));
            var missing = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7011"));
            Assert.Equal(DiagnosticSeverity.Error, missing.Severity);
            Assert.Contains(spelling, missing.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, TargetKey);
        }
    }
}
