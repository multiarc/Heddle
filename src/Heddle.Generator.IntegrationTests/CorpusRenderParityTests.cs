using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Render parity: for the model-less corpus templates that precompile (definition default output, overrides/
    /// layering, import composition), renders the real <c>TestTemplate/**</c> file through both backends and asserts
    /// byte-identical output — the render-correctness gate the classification-only <see cref="CorpusDifferentialTests"/>
    /// does not itself provide. Imports resolve from the corpus directory on the dynamic side and from the whole-corpus
    /// <c>AdditionalFiles</c> set on the precompiled side.
    /// </summary>
    public class CorpusRenderParityTests
    {
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
        // The clamp-drift fixture: an INDENTED @<< composition import on the document's last line, importing a
        // file with a zero-output directive. The imported chain keeps the import-site offset while the import's
        // own (widened) line is removed, leaving that offset past the end of the shortened working document.
        // Before the clamp fix the emitter threw IndexOutOfRangeException inside WidenToWholeLine and the
        // template silently lost precompilation; RenderInCorpus's ExpectPrecompiled is what makes that regression
        // a red build, and the byte assertion covers the rest.
        [InlineData("shaper-clamp-overshoot.heddle")]
        public void ModelLessCorpusTemplateRendersIdentically(string name)
        {
            // The corpus is in THIS project's own output directory (TestCorpus.props), so locating it is
            // AppContext.BaseDirectory and nothing else. The assembly-path rewrite + `../../..` climb that stood
            // here, and the hard assert that had to be bolted on top of it, are both gone.
            var dir = TestCorpusIndex.CorpusDir;
            // FrontEndError entries carry deliberate parse errors; excluded so the rest of the corpus generates
            // cleanly (imports still resolve from what remains). The set is read from the intent table, not
            // hand-copied into a third HashSet as it was here.
            var corpus = TestCorpusIndex.Load(includeFrontEndErrorFixtures: false);
            var target = corpus.FirstOrDefault(t => Path.GetFileName(t.key) == name);
            Assert.False(target.content == null, "Corpus template not found: " + name);

            var (precompiled, dyn) = DifferentialHarness.RenderInCorpus(
                corpus, target.key, target.content, typeof(object), null, dir,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            Assert.Equal(dyn, precompiled);
        }
    }
}
