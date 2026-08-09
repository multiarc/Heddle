using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// An import-only library file is also a <c>&lt;HeddleTemplate&gt;</c> item, so the generator compiles it on its
    /// own. A fragment that is only well-formed inside an importer fails that standalone pass, and the front end's
    /// error used to be forwarded as an error against the consumer's build — <b>an error no run of the application
    /// can produce</b>, because in production the engine never compiles that file standalone: it only ever reaches
    /// the compiler already expanded into the document that imports it.
    /// <para>The shapes below are the measurement the fix rests on — which fragments actually fail the standalone
    /// pass, taken from the parse-time seam that makes an importer matter: the imported document is parsed with the
    /// importer's context as its parent, so it inherits every definition registered above the <c>@&lt;&lt;</c> line,
    /// and standalone that inherited set is empty.</para>
    /// <para>The template is still dropped from precompilation; only the diagnostic is withheld, and only where
    /// something in the compilation actually imports it.</para>
    /// </summary>
    public class ImportOnlyFragmentStandalonePassTests : IDisposable
    {
        private readonly string _root;

        public ImportOnlyFragmentStandalonePassTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "heddle-import-only-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
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

        /// <summary>The dynamic tier reads imports off disk, so the corpus has to exist there too.</summary>
        private IReadOnlyList<(string key, string content)> OnDisk(IReadOnlyList<(string key, string content)> corpus)
        {
            foreach (var (key, content) in corpus)
                File.WriteAllText(Path.Combine(_root, key), content);
            return corpus;
        }

        private const string ShellKey = "shell.heddle";

        // The importer supplies the base definition and the region the fragment reaches for.
        private const string Shell =
            "@%\n<greet>\n{{[[base]]}}\n%@\n";

        private const string FragmentKey = "fragment.heddle";

        /// <summary>Shape 1: a partial override whose base definition belongs to the importer.</summary>
        private const string OverrideFragment = "@%\n<greet:greet>\n{{[[override]]}}\n%@\n";

        /// <summary>Shape 3: a bodied call to a definition the importer supplies.</summary>
        private const string BodiedCallFragment = "@greet(){{[[body]]}}\n";

        private static IReadOnlyList<(string key, string content)> Corpus(string fragment, string importerBody) =>
            new[]
            {
                (ShellKey, Shell),
                (FragmentKey, fragment),
                ("importer.heddle", importerBody),
            };

        public static IEnumerable<object[]> Fragments() => new[]
        {
            new object[] { OverrideFragment },
            new object[] { BodiedCallFragment },
        };

        /// <summary>The fix: when something imports the fragment, its standalone-pass error is not a build error.</summary>
        [Theory]
        [MemberData(nameof(Fragments))]
        public void AnImportedFragmentsStandaloneErrorIsNotABuildError(string fragment)
        {
            var importer = "@<<{{" + ShellKey + "}}\n@<<{{" + FragmentKey + "}}\n@greet()\n";
            var gen = DifferentialHarness.Generate(Corpus(fragment, importer));

            Assert.DoesNotContain(gen.Diagnostics,
                d => d.Severity == DiagnosticSeverity.Error && d.Id == "HED7012");
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        /// <summary>The near-neighbour that must keep reporting: the same fragment with nothing importing it is a
        /// template a consumer really can compile on its own, so its error is real and stays an error. Without this
        /// row the fix above could be satisfied by suppressing the channel outright.</summary>
        [Theory]
        [MemberData(nameof(Fragments))]
        public void TheSameFragmentNobodyImportsStillReportsItsError(string fragment)
        {
            // The importer imports the shell only, so the fragment is nobody's import target.
            var importer = "@<<{{" + ShellKey + "}}\n@greet()\n";
            var gen = DifferentialHarness.Generate(Corpus(fragment, importer));

            Assert.Contains(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        /// <summary>The importer itself must still build cleanly. It used to degrade for a reason of its own — the
        /// override the fragment carries — and now precompiles, because layering an imported definition is layering
        /// like any other: the import is expanded into this document before the parse that builds the layers. What
        /// this row is about either way is that no error reaches the consumer's build.</summary>
        [Fact]
        public void TheImporterStillBuildsWithoutAnError()
        {
            const string importerKey = "importer.heddle";
            var importer = "@<<{{" + ShellKey + "}}\n@<<{{" + FragmentKey + "}}\n@greet()\n";
            var corpus = new[]
            {
                (ShellKey, Shell),
                (FragmentKey, OverrideFragment),
                (importerKey, importer),
            };

            var gen = DifferentialHarness.Generate(OnDisk(corpus));
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, importerKey);
        }

        /// <summary>A library that <b>does</b> stand alone keeps its own precompiled entry: the fix withholds an
        /// error, it does not take every import target off the tier.</summary>
        [Fact]
        public void AnImportedLibraryThatCompilesStandaloneKeepsItsEntry()
        {
            const string libraryKey = "lib.heddle";
            const string library = "@%\n<greet>\n{{[[hello]]}}\n%@\n";
            const string importerKey = "uses.heddle";
            const string importer = "@<<{{" + libraryKey + "}}\n@greet()\n";

            var corpus = OnDisk(new[] { (libraryKey, library), (importerKey, importer) });
            var gen = DifferentialHarness.Generate(corpus);
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(
                corpus, importerKey, importer, null, null, _root);
            Assert.Equal("[[hello]]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
