using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI9 render parity: for the model-less corpus templates that precompile (definition default output,
    /// overrides/layering, import composition), renders the real <c>TestTemplate/**</c> file through both backends and
    /// asserts byte-identical output — the render-correctness gate the classification-only
    /// <see cref="CorpusDifferentialTests"/> does not itself provide. Imports resolve from the corpus directory on the
    /// dynamic side and from the whole-corpus <c>AdditionalFiles</c> set on the precompiled side.
    /// </summary>
    public class CorpusRenderParityTests
    {
        private static string CorpusDir()
        {
            var self = typeof(DifferentialHarness).Assembly.Location;
            var candidate = self.Replace("Heddle.Generator.IntegrationTests", "Heddle.Tests");
            if (!File.Exists(candidate))
                return null;
            var tfmDir = Path.GetDirectoryName(candidate);
            var projDir = Path.GetFullPath(Path.Combine(tfmDir, "..", "..", ".."));
            var corpus = Path.Combine(projDir, "TestTemplate");
            return Directory.Exists(corpus) ? corpus : null;
        }

        private static List<(string key, string content)> LoadCorpus(string dir)
        {
            var list = new List<(string, string)>();
            foreach (var path in Directory.EnumerateFiles(dir, "*.heddle", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                var rel = path.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                list.Add((rel, File.ReadAllText(path)));
            }

            return list;
        }

        [Theory]
        [InlineData("optimized-document.heddle")]
        [InlineData("ergo-double-render.heddle")]
        [InlineData("ergo-import-library.heddle")]
        [InlineData("ergo-import-composition.heddle")]
        [InlineData("branching-partial-parent.heddle")]
        [InlineData("profile-partial-parent.heddle")]
        [InlineData("profile-flagship.heddle")]
        [InlineData("profile-directive.heddle")]
        // Hidden-token offset regression: a single-file multi-line definition body with an inner comment must
        // render byte-identically across the precompiled and runtime backends (the enclosing-block trim fix lives
        // in both backends' DocumentShaper/HeddleCompiler). The cross-file override page layers an imported
        // definition and falls back to the dynamic path, so it carries no precompiled entry to compare — its
        // correctness is pinned by the runtime golden (Heddle.Tests MultilineOverrideOffsetRegressionTests).
        [InlineData("regr-def-inner-comment.heddle")]
        public void ModelLessCorpusTemplateRendersIdentically(string name)
        {
            var dir = CorpusDir();
            if (dir == null)
                return; // Heddle.Tests corpus for this TFM not built — the full-solution gate builds it.

            // Diagnostic-fixture templates carry deliberate front-end errors; exclude them so the rest of the corpus
            // generates cleanly (imports still resolve from the remaining set).
            var diagnosticFixtures = new HashSet<string>(StringComparer.Ordinal)
            {
                "ergo-import-broken.heddle", "import-origin-a.heddle", "import-origin-b.heddle",
                "import-origin-broken.heddle", "import-origin-c.heddle",
            };
            var corpus = LoadCorpus(dir).Where(t => !diagnosticFixtures.Contains(Path.GetFileName(t.key))).ToList();
            var target = corpus.FirstOrDefault(t => Path.GetFileName(t.key) == name);
            Assert.False(target.content == null, "Corpus template not found: " + name);

            var self = typeof(DifferentialHarness).Assembly.Location;
            var testsDll = self.Replace("Heddle.Generator.IntegrationTests", "Heddle.Tests");
            var extra = File.Exists(testsDll)
                ? new[] { MetadataReference.CreateFromFile(testsDll) }
                : Array.Empty<MetadataReference>();

            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(
                corpus, target.key, target.content, typeof(object), null, dir, extraReferences: extra);
            Assert.Equal(dyn, precompiled);
        }
    }
}
