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
        private const string SubLibraryKey = "sub/lib.heddle";
        private const string SubLibrary = "@%\n<greet>\n{{[[sub]]}}\n%@\n";
        private const string TargetKey = "views/target.heddle";

        private readonly string _root;

        public ImportSpellingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "heddle-import-spelling-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(Path.Combine(_root, "sub"));
            File.WriteAllText(Path.Combine(_root, LibraryKey), Library);
            File.WriteAllText(Path.Combine(_root, "sub", "lib.heddle"), SubLibrary);
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
        // A `.` names the directory it is in, so GetFullPath drops it whether or not a `..` follows. Applying `..`
        // dropped these only as a side effect of the walk it took to do it, so a spelling carrying only `.`
        // never reached the walk at all and became a build error over a file the engine reads.
        [InlineData("./lib.heddle")]
        [InlineData(".//lib.heddle")]
        [InlineData("./lib.heddle/.")]
        [InlineData("lib.heddle/.")]
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

        private static readonly bool FileSystemIsCaseSensitive = ProbeCaseSensitivity();

        /// <summary>Writes a probe file and asks for it back under a different casing. Only the file
        /// NAME is re-cased: re-casing the directory too would answer a question about the temp
        /// path's own spelling instead.</summary>
        private static bool ProbeCaseSensitivity()
        {
            var dir = System.IO.Path.GetTempPath();
            var name = "heddle-case-probe-" + Guid.NewGuid().ToString("N");
            var path = System.IO.Path.Combine(dir, name);
            System.IO.File.WriteAllText(path, string.Empty);
            try
            {
                return !System.IO.File.Exists(System.IO.Path.Combine(dir, name.ToUpperInvariant()));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>Spellings that name no file the engine can read either. A <c>..</c> with nothing left to cancel
        /// against reaches above the root, and a key differs by case — both refuse on both tiers, and the build
        /// error is matched by an engine error rather than standing alone over a template that renders.</summary>
        [Theory]
        [InlineData("./../outside/lib.heddle")]
        [InlineData("LIB.heddle")]
        [InlineData("x/../../lib.heddle")]
        // A trailing separator survives GetFullPath, so the read lands on a directory and fails. `lib.heddle/.`,
        // three rows up, is the same file spelled one character differently and is read by both tiers.
        [InlineData("lib.heddle/")]
        // A `..` that cancels the whole spelling leaves the root itself, which is a directory on both tiers.
        [InlineData("sub/..")]
        public void AnImportSpellingTheEngineResolvesToNothingIsRefusedByBothTiers(string spelling)
        {
            // "LIB.heddle" is the case-differing row, and whether it names a readable file is a
            // property of the FILE SYSTEM, not of either tier: NTFS and APFS resolve it to
            // lib.heddle, ext4 does not. On a case-insensitive volume the engine reads the file and
            // compiles, so the row's own premise -- that this spelling reaches nothing -- is false
            // and it can prove nothing about refusal. Probed rather than assumed from the OS,
            // because Windows can mount case-sensitive directories and macOS ships either.
            if (!FileSystemIsCaseSensitive
                && string.Equals(spelling, "LIB.heddle", StringComparison.Ordinal))
            {
                Assert.Skip("the file system resolving this corpus is case-insensitive, so a " +
                            "case-differing spelling names a file that exists");
            }

            var engine = Engine(spelling);
            Assert.False(engine.CompileResult.Success);
            Assert.Contains(engine.CompileResult.Errors, e => e.DiagnosticId == "HED4009");

            var gen = DifferentialHarness.Generate(Corpus(spelling));
            var missing = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7011"));
            Assert.Equal(DiagnosticSeverity.Error, missing.Severity);
            Assert.Contains(spelling, missing.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, TargetKey);
        }

        /// <summary>
        /// A directory in a subdirectory, so that a spelling reaching it through <c>.</c> is a different file from
        /// the one at the root. Without it, a rule that dropped every segment it did not understand would pass the
        /// rows above by landing on the right file for the wrong reason.
        /// </summary>
        [Theory]
        [InlineData("sub/./lib.heddle")]
        [InlineData("sub/././lib.heddle")]
        [InlineData("./sub/./lib.heddle")]
        [InlineData("sub/lib.heddle/.")]
        public void AnImportSpellingWithInteriorDotsResolvesToTheFileTheEngineReads(string spelling)
        {
            var engine = Engine(spelling);
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());
            Assert.Equal("[[sub]]\n", engine.Generate(null));

            var corpus = new[] { (LibraryKey, Library), (SubLibraryKey, SubLibrary), (TargetKey, Target(spelling)) };
            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(corpus, TargetKey, Target(spelling), null,
                null, _root);
            Assert.Equal("[[sub]]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// A backslash, which is a separator on Windows and an ordinary file-name character everywhere else — so
        /// there is no single right outcome to assert, only that the two tiers reach the same one. The build tier
        /// used to replace every backslash with a slash unconditionally, which made <c>x\..\lib.heddle</c> resolve to
        /// <c>lib.heddle</c> on a host where the engine looks for a file whose name contains backslashes and finds
        /// none. What the assertion pins is the agreement; the rows above pin the outcomes that do not vary.
        /// </summary>
        /// <para><b>The corpus is what makes these rows reach anything.</b> Three of the four original spellings
        /// carry a <c>..</c> that key derivation refuses outright, and the fourth collapsed to a key no template in
        /// the corpus had — so the class was measured entirely against misses. The rows added below collapse to keys
        /// the corpus <em>does</em> hold, which is where a backslash silently bound the wrong file.</para>
        [Theory]
        [InlineData("x\\..\\lib.heddle")]
        [InlineData("x\\../lib.heddle")]
        [InlineData("x/..\\lib.heddle")]
        [InlineData("x\\lib.heddle")]
        // Collapses to `sub/lib.heddle`, which is in the corpus: off Windows the build tier bound the sub-directory
        // library while the engine looked for a file whose name is `sub\lib.heddle` and found none.
        [InlineData("sub\\lib.heddle")]
        // Collapses to `lib.heddle`, likewise present.
        [InlineData(".\\lib.heddle")]
        [InlineData("sub\\..\\lib.heddle")]
        public void ABackslashIsReadTheSameWayByBothTiers(string spelling)
        {
            var corpus = new[] { (LibraryKey, Library), (SubLibraryKey, SubLibrary), (TargetKey, Target(spelling)) };
            var engine = Engine(spelling);
            var gen = DifferentialHarness.Generate(corpus);

            if (engine.CompileResult.Success)
            {
                // Which file it is differs by platform; that the two tiers read the same one does not.
                var expected = engine.Generate(null);
                Assert.Empty(gen.Diagnostics.Where(d => d.Id == "HED7011"));
                var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(corpus, TargetKey,
                    Target(spelling), null, null, _root);
                Assert.Equal(expected, dyn);
                Assert.Equal(dyn, precompiled);
            }
            else
            {
                Assert.Contains(engine.CompileResult.Errors, e => e.DiagnosticId == "HED4009");
                Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7011"));
                DifferentialHarness.ExpectDegrade(gen, TargetKey);
            }
        }
    }
}
