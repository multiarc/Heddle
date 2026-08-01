using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The editor resolves model types through the same reflection resolver the engine compiles with, and it hands
    /// that resolver the same collected <c>@using</c> bodies — so a body that binds a name rather than opening a
    /// namespace types a document here for the same reason it types one at compile time. Pinned because a resolver
    /// the editor reached through a carrier of its own would be a third answer to a question two tiers already have
    /// to agree on.
    /// </summary>
    public class AliasTypeProjectionTests
    {
        private const string Path = "doc.heddle";

        [Theory]
        [InlineData("namespace-alias", "C = Corpus", "C.Article")]
        [InlineData("type-alias", "A = Corpus.Article", "A")]
        [InlineData("global-qualified", "Corpus", "global::Corpus.Article")]
        public void ADocumentModelTypedThroughADirectiveIsPinned(string name, string body, string spelling)
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path,
                "@using(){{" + body + "}}\n@model(){{" + spelling + "}}\n@(Title)", 1);

            Assert.NotNull(analysis.Scopes.RootType);
            Assert.Equal("Corpus.Article", analysis.Scopes.RootType.Type.FullName);
            Assert.DoesNotContain(analysis.Diagnostics,
                d => d.Severity == HeddleDiagnosticSeverity.Error);
        }

        /// <summary>The control: the same spelling with no directive to bind its head types nothing, so the rows
        /// above are evidence about the directive rather than about the spelling landing somewhere by luck.</summary>
        [Fact]
        public void ADocumentModelTypedThroughAnUndeclaredAliasIsNotPinned()
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path, "@model(){{C.Article}}\n@(Title)", 1);

            Assert.True(analysis.Scopes.RootType == null ||
                        analysis.Scopes.RootType.Type == typeof(object),
                        analysis.Scopes.RootType?.Type.FullName);
        }

        /// <summary>
        /// A definition's type sits in the <c>::</c> position, whose name the grammar reads as an identifier — so an
        /// alias head reaches it and the <c>global::</c> qualifier cannot, the same way no tier's grammar takes a
        /// nullable suffix there. Recorded as a property of the position rather than of the resolver: the qualifier
        /// resolves perfectly well when a spelling can carry it, which is the <c>@model</c> directive's free text.
        /// </summary>
        [Theory]
        [InlineData("namespace-alias", "C = Corpus", "C.Article")]
        [InlineData("type-alias", "A = Corpus.Article", "A")]
        public void ADefinitionTypedThroughADirectiveIsPinned(string name, string body, string spelling)
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path,
                "@using(){{" + body + "}}\n@%<card>{{@(Title)}} :: " + spelling + "%@\n@card()", 1);

            var card = analysis.Definitions.FirstOrDefault(d => d.Name == "card");
            Assert.NotNull(card);
            Assert.True(card.IsPinned, name + ": " + card.ModelTypeName);
            Assert.Equal("Corpus.Article", card.ModelType.Type.FullName);
        }

        [Fact]
        public void ADefinitionTypedThroughAnUndeclaredAliasIsNotPinned()
        {
            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path, "@%<card>{{@(Title)}} :: C.Article%@\n@card()", 1);

            var card = analysis.Definitions.FirstOrDefault(d => d.Name == "card");
            Assert.NotNull(card);
            Assert.False(card.IsPinned);
        }
    }
}
