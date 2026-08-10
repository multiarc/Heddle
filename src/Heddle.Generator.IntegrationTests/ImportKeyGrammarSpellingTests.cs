using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The three spellings where the build tier's <b>template-key</b> grammar renamed the file an
    /// <c>@&lt;&lt;</c> import names.
    /// <para>An import is not a template key. The documented contract for <c>@&lt;&lt;</c> is that the spelling is
    /// taken verbatim and resolved with <c>Path.Combine(RootPath, spelling)</c> — "an absolute path wins" — which is
    /// what the engine does. The key grammar strips a leading <c>~/</c>, strips a leading <c>/</c>, and appends
    /// <c>.heddle</c> to an extension-less final segment; each of those three names a different file. So
    /// <c>/lib.heddle</c> is an absolute path to the engine and the root-relative <c>lib.heddle</c> to the key
    /// grammar, and the build tier used to precompile a template the engine refuses.
    /// <para><c>~/</c> and an extension-less name are real idioms — but for <c>@partial</c> and for registry keys,
    /// which resolve through <c>TemplateOptions.FileNamePostfix</c> and <c>TemplateKey</c> respectively, not through
    /// the import reader. Nothing in the user documentation offers either to <c>@&lt;&lt;</c>.</para></para>
    /// </summary>
    public class ImportKeyGrammarSpellingTests : IDisposable
    {
        private const string LibraryKey = "lib.heddle";
        private const string Library = "@%\n<greet>\n{{[[hello]]}}\n%@\n";
        private const string TargetKey = "views/target.heddle";

        private readonly string _root;

        public ImportKeyGrammarSpellingTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "heddle-import-keygrammar-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>Each of the three renames, refused on both tiers. The engine's refusal is asserted in the same
        /// test so the build error is never left standing alone over a template that renders.</summary>
        [Theory]
        // A leading separator: `Path.Combine` treats it as an absolute path, the key grammar as root-relative.
        [InlineData("/lib.heddle")]
        // The host "from the root" idiom: a literal `~` directory to `Path.Combine`, stripped by the key grammar.
        [InlineData("~/lib.heddle")]
        // No extension: the file `lib` to `Path.Combine`, `lib.heddle` to the key grammar.
        [InlineData("lib")]
        public void ASpellingTheKeyGrammarWouldRenameIsRefusedByBothTiers(string spelling)
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

        /// <summary>The near-neighbours the refusal must not take with it: spellings one character away that the
        /// engine <b>does</b> read must still precompile and still render the engine's bytes. Without these the
        /// guard above could be satisfied by refusing everything.</summary>
        [Theory]
        [InlineData("lib.heddle")]
        [InlineData("./lib.heddle")]
        [InlineData("x/../lib.heddle")]
        [InlineData("lib.heddle/.")]
        public void TheSpellingsTheEngineReadsStillPrecompile(string spelling)
        {
            var engine = Engine(spelling);
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());
            Assert.Equal("[[hello]]\n", engine.Generate(null));

            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(Corpus(spelling), TargetKey,
                Target(spelling), null, null, _root);
            Assert.Equal("[[hello]]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>An extension-less name still reaches a template through <c>@partial</c>, which is the idiom the
        /// documentation actually teaches for one — so the refusal above is about the import reader, not about
        /// extension-less names as such.</summary>
        [Fact]
        public void AnExtensionlessNameStillNamesAPartial()
        {
            const string childKey = "child.heddle";
            const string child = "[[child]]";
            const string parentKey = "parent.heddle";
            const string parent = "@partial(){{child}}";

            // The dynamic tier resolves a partial off disk, so the corpus alone is not enough.
            File.WriteAllText(Path.Combine(_root, childKey), child);
            File.WriteAllText(Path.Combine(_root, parentKey), parent);

            var corpus = new[] { (childKey, child), (parentKey, parent) };
            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(corpus, parentKey, parent, null, null, _root);
            Assert.Equal("[[child]]", dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
