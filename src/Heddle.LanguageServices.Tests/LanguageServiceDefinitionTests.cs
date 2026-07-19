using System;
using System.IO;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>Phase 6 D16/D26 go-to-definition: definition calls → the surviving header span; import sites →
    /// the resolved file (success criterion 3).</summary>
    public class LanguageServiceDefinitionTests
    {
        private const string Path = "doc.heddle";
        private static string CorpusDir => System.IO.Path.Combine(AppContext.BaseDirectory, "Corpus");

        [Fact]
        public void DefinitionCallTargetsHeaderSpan()
        {
            var template = "@%<card>{{X}} :: Corpus.Article%@\n@card()";
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, template, 1);
            int callOffset = template.LastIndexOf("card") + 1;
            var target = service.GetDefinition(Path, callOffset);
            Assert.NotNull(target);
            Assert.Equal(Path, target.SourcePath);
            // The header span begins at the '<card>' declaration, before the call.
            Assert.True(target.Offset < callOffset);
        }

        [Fact]
        public void ImportSiteTargetsResolvedFile()
        {
            var template = "@<<{{layout.heddle}}";
            using var service = CorpusFixture.NewTypedService(CorpusDir);
            service.Analyze(Path, template, 1);
            var target = service.GetDefinition(Path, 5); // inside the @<< site
            Assert.NotNull(target);
            Assert.Contains("layout.heddle", target.SourcePath);
            Assert.Equal(0, target.Offset);
        }

        [Fact]
        public void UnresolvableReturnsNull()
        {
            var template = "@model(){{Corpus.Blog}}\nplain text here";
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, template, 1);
            Assert.Null(service.GetDefinition(Path, template.IndexOf("plain") + 1));
        }
    }
}
