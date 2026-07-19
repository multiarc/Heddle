using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Phase 7 (WI5) — LSP region projection and the transitively-surfaced region diagnostics.
    /// <c>DefinitionInfo.Regions</c> projects the parse model; completion offers a callee's PUBLIC region names at
    /// a call-body <c>&lt;</c> override position; HED5019/HED5020 appear as editor diagnostics; a matched fill's
    /// retracted base-not-found error is gone from BOTH diagnostic sources (the D5 phase-split retract).
    /// </summary>
    public class RegionProjectionTests
    {
        private const string Path = "doc.heddle";

        private const string Feed =
            "@%<feed>{{" +
            "@%<:heading>{{<h2>d</h2>}}" +
            "<:item :: Corpus.Article>{{<li>@(Title)</li>}}" +
            "<divider>{{<hr>}}%@" +
            "@heading()@divider()@out()}}%@\n";

        [Fact]
        public void DefinitionInfoProjectsRegions()
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path, "@model(){{Corpus.Blog}}\n" + Feed + "@feed()", 1);
            var feed = analysis.Definitions.FirstOrDefault(d => d.Name == "feed");
            Assert.NotNull(feed);
            Assert.Equal(3, feed.Regions.Count);
            var heading = feed.Regions.First(r => r.Name == "heading");
            Assert.True(heading.IsPublic);
            var item = feed.Regions.First(r => r.Name == "item");
            Assert.True(item.IsPublic);
            Assert.Equal("Corpus.Article", item.TypeName);
            var divider = feed.Regions.First(r => r.Name == "divider");
            Assert.False(divider.IsPublic);
        }

        [Fact]
        public void CompletionOffersPublicRegionsAtOverridePosition()
        {
            var (text, offset) = CorpusFixture.At("@model(){{Corpus.Blog}}\n" + Feed + "@feed(){{@%<§%@}}");
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            var labels = CorpusFixture.Labels(service.GetCompletions(Path, offset));
            Assert.Contains("heading", labels);
            Assert.Contains("item", labels);
            Assert.DoesNotContain("divider", labels); // private — not offered
        }

        [Fact]
        public void HoverShowsRegionVisibilityAndType()
        {
            var (text, offset) = CorpusFixture.At("@model(){{Corpus.Blog}}\n" + Feed + "@feed(){{@%<it§em:item>{{x}}%@}}");
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            var hover = service.GetHover(Path, offset);
            Assert.NotNull(hover);
            Assert.Contains("<:item> :: Corpus.Article", hover.Markdown);
            Assert.Contains("public region", hover.Markdown);
        }

        [Fact]
        public void Hed5019SurfacesAsEditorDiagnostic()
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path,
                "@model(){{Corpus.Blog}}\n" + Feed + "@feed(){{@%<divider:divider>{{y}}%@}}", 1);
            Assert.Contains(analysis.Diagnostics, d => d.Id == "HED5019");
            // The retracted base-not-found error must be gone from the union (D5: removed from BOTH lists).
            Assert.DoesNotContain(analysis.Diagnostics, d => d.Message.Contains("couldn't be found"));
        }

        [Fact]
        public void Hed5020SurfacesAsEditorDiagnostic()
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path,
                "@model(){{Corpus.Blog}}\n@%<card>{{@%<:head>{{a}}<:head>{{b}}%@@head()}}%@@card()", 1);
            Assert.Contains(analysis.Diagnostics, d => d.Id == "HED5020");
        }

        [Fact]
        public void MatchedFillLeavesNoDiagnostics()
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path,
                "@model(){{Corpus.Blog}}\n" + Feed + "@feed(){{@%<heading:heading>{{<h2>x</h2>}}%@}}", 1);
            Assert.DoesNotContain(analysis.Diagnostics, d => d.Message.Contains("couldn't be found"));
        }
    }
}
