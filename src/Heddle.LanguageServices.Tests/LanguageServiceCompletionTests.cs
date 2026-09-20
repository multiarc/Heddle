using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Validates the context table and abstract-definition rule: driven editor-less against the corpus blog model.
    /// Model members come from the retained scope map; extensions/functions from the live registries; the abstract
    /// intersection excludes members missing at any call site.
    /// </summary>
    public class LanguageServiceCompletionTests
    {
        private const string Path = "doc.heddle";

        private static System.Collections.Generic.IReadOnlyList<string> Complete(string marked)
        {
            var (text, offset) = CorpusFixture.At(marked);
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            return CorpusFixture.Labels(service.GetCompletions(Path, offset));
        }

        [Fact] // C01 — typed member completion inside a narrowed @list body
        public void C01_ListBodyOffersElementMembersNotBlogMembers()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@list(Articles){{ @(§ }}");
            Assert.Contains("Title", labels);
            Assert.Contains("Summary", labels);
            Assert.Contains("Author", labels);
            Assert.Contains("Rating", labels);
            Assert.DoesNotContain("Articles", labels); // that is Blog's member, not Article's
        }

        /// <summary>A name the model type withholds — a <c>[Hidden]</c> override or <c>new</c> property, a field
        /// or a method hiding a property — is not
        /// offered from the base class that still declares it visibly, and binding it is the same positioned
        /// <c>HED0001</c> the compiler raises.</summary>
        [Fact]
        public void MembersHiddenByTheDerivedTypeAreNotOfferedFromItsBase()
        {
            var labels = Complete("@model(){{Corpus.Bank}}\n@list(Accounts){{ @(§ }}");
            Assert.Contains("Owner", labels);
            Assert.Contains("Number", labels);
            Assert.DoesNotContain("Secret", labels);
            Assert.DoesNotContain("Token", labels);
            Assert.DoesNotContain("Pin", labels);  // hidden by a field
            Assert.DoesNotContain("Code", labels); // hidden by a method

            using var service = CorpusFixture.NewTypedService();
            var analysis = service.Analyze(Path, "@model(){{Corpus.Bank}}\n@list(Accounts){{ @(Secret)|@(Token)|@(Pin)|@(Code)|@(Owner) }}", 1);
            Assert.Equal(4, analysis.Diagnostics.Count(d => d.Id == "HED0001"));
        }

        /// <summary>The document itself is a scope: its model is the <c>@model</c> type. Pins the omission where
        /// only call bodies were scopes, so an expression written at the top level of a document was offered
        /// functions and keywords but none of its model's members.</summary>
        [Theory]
        [InlineData("@model(){{Corpus.Blog}}\n@(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n@(§")]
        [InlineData("@model(){{Corpus.Blog}}\n<h1>@(Title)</h1> @(§) tail")]
        [InlineData("@model(){{Corpus.Blog}}\n@list(Articles){{ @(Title) }}\n@(§)")]
        [InlineData("@(§)\n@model(){{Corpus.Blog}}")]
        [InlineData("@model(){{Corpus.Blog}}\n@if(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n@(Title + §)")]
        // Braces that open no body — in a string, a comment, a raw block, plain text, after an escape — and a
        // body further down that is still being typed: none of them encloses this offset.
        [InlineData("@model(){{Corpus.Blog}}\n@(\"{{\") @(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n@* {{ *@ @(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n@{ var t = '{{'; }@ @(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n<p>use {{ to open</p> @(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n@@{{ @(§)")]
        [InlineData("@model(){{Corpus.Blog}}\n@(§)\n@list(Articles){{ @(Title)")]
        public void DocumentLevelExpressionOffersTheModelsMembers(string marked)
        {
            var labels = Complete(marked);
            Assert.Contains("Title", labels);
            Assert.Contains("Articles", labels);
            Assert.Contains("upper", labels);
            Assert.Contains("this", labels);
        }

        [Fact]
        public void DocumentLevelMemberAccessResolvesThePrefix()
        {
            var labels = Complete("@model(){{Corpus.Page}}\n@(Article.Author.§)");
            Assert.Contains("Name", labels);
            Assert.DoesNotContain("Title", labels);
            Assert.Contains("Title", Complete("@model(){{Corpus.Page}}\n@(Article.§)"));
        }

        [Fact]
        public void DocumentLevelHonoursTheHiddenMemberRule()
        {
            var labels = Complete("@model(){{Corpus.Account}}\n@(§)");
            Assert.Contains("Owner", labels);
            Assert.Contains("Number", labels);
            Assert.DoesNotContain("Secret", labels);
            Assert.DoesNotContain("Token", labels);
            Assert.DoesNotContain("Pin", labels);
            Assert.DoesNotContain("Code", labels);
        }

        /// <summary>A body is unclosed for as long as it is being typed, which is exactly when completion is asked
        /// for: the cursor in it is offered the element's members, not the document's and not nothing.</summary>
        [Theory]
        [InlineData("@model(){{Corpus.Blog}}\n@list(Articles){{ @(§")]
        [InlineData("@model(){{Corpus.Blog}}\n@list(Articles){{ <b>@(§)</b>")]
        [InlineData("@model(){{Corpus.Blog}}\n@list(Articles){{ @(\"{{\") @(§) }}")]
        public void ABodyStillBeingTypedOffersItsElementsMembers(string marked)
        {
            var labels = Complete(marked);
            Assert.Contains("Author", labels);
            Assert.DoesNotContain("Articles", labels);
        }

        [Fact]
        public void DocumentWithNoModelOffersFunctionsButGuessesNoMembers()
        {
            var labels = Complete("plain @(§)");
            Assert.Contains("upper", labels);
            Assert.DoesNotContain("Title", labels);
        }

        /// <summary>The fallback is for the document level only. A body whose type the compiler did not record —
        /// an abstract definition nobody calls — stays unknown: the document's model is not its model.</summary>
        [Fact]
        public void UncalledDefinitionBodyIsNotGivenTheDocumentsModel()
        {
            var labels = Complete("@%<panel>{{ @(§ }}%@\n@model(){{Corpus.Blog}}\n@(Title)");
            Assert.DoesNotContain("Title", labels);
            Assert.DoesNotContain("Articles", labels);
        }

        [Fact]
        public void DocumentLevelHoverDescribesTheModelsMember()
        {
            var (text, offset) = CorpusFixture.At("@model(){{Corpus.Blog}}\n@(Ti§tle)");
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            var hover = service.GetHover(Path, offset);
            Assert.NotNull(hover);
            Assert.Contains("Title", hover.Markdown);
            Assert.Contains("string", hover.Markdown);
        }

        [Fact] // C02 — root members via ::
        public void C02_RootReferenceOffersRootMembers()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@list(Articles){{ @(::§ }}");
            Assert.Contains("Articles", labels);
            Assert.Contains("Title", labels);
        }

        [Fact] // C03 — member after '.'
        public void C03_MemberAfterDotResolvesPrefixType()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@list(Articles){{ @(Author.§ }}");
            Assert.Contains("Name", labels);
            Assert.DoesNotContain("Title", labels); // Author has no Title
        }

        [Fact] // C05 — callable names at top level include definitions, extensions, functions
        public void C05_CallableNamesIncludeExtensionsAndFunctions()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@§");
            Assert.Contains("list", labels);
            Assert.Contains("if", labels);
            Assert.Contains("raw", labels);
            Assert.Contains("else", labels);
            Assert.Contains("upper", labels); // function
            Assert.Contains("range", labels);
            Assert.DoesNotContain("", labels); // the unnamed EmptyExtension alias is filtered
        }

        [Fact] // Functions carry a signature detail
        public void FunctionItemsCarrySignatureDetail()
        {
            var (text, offset) = CorpusFixture.At("@model(){{Corpus.Blog}}\n@(up§)");
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            var upper = service.GetCompletions(Path, offset).Items.FirstOrDefault(i => i.Label == "upper");
            Assert.NotNull(upper);
            Assert.Equal(CompletionItemKind.Function, upper.Kind);
            Assert.Contains("upper(", upper.Detail);
        }

        [Fact] // C04 — named-argument props with filtering of already-passed
        public void C04_NamedArgumentOffersUnpassedProps()
        {
            var marked = "@%<card(style: string = \"plain\", compact: bool = false)>{{X}} :: Corpus.Article%@\n" +
                         "@model(){{Corpus.Blog}}\n@card(style: \"wide\", §";
            var (text, offset) = CorpusFixture.At(marked);
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            var items = service.GetCompletions(Path, offset).Items;
            Assert.Contains(items, i => i.Label == "compact" && i.Kind == CompletionItemKind.Prop);
            Assert.DoesNotContain(items, i => i.Label == "style"); // already passed
        }

        [Fact] // C07/C08 — abstract-definition name intersection
        public void C07_AbstractDefinitionOffersOnlySharedMembers()
        {
            // <panel> is abstract; called with a Corpus.Article (has Rating) and a Corpus.Menu (no Rating). Both
            // share Title, so Title is offered and Rating is not.
            var marked =
                "@%<panel>{{ @(§ }}%@\n" +
                "@model(){{Corpus.Page}}\n" +
                "@panel(Article)\n" +
                "@panel(Menu)";
            var labels = Complete(marked);
            Assert.Contains("Title", labels);
            Assert.DoesNotContain("Rating", labels); // present only on Article — excluded by the intersection
            Assert.DoesNotContain("Items", labels);  // present only on Menu — excluded
        }

        [Fact]
        public void ScannedFunctionExportIsOffered()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@(§)");
            Assert.Contains("titlecase", labels);
        }

        [Fact]
        public void ScannedExtensionIsOffered()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@§");
            Assert.Contains("badge", labels);
        }
    }
}
