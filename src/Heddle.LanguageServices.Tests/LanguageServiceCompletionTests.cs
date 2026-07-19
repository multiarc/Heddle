using System.Linq;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// The phase 6 D12 context table and the D13 abstract-definition rule, driven editor-less against the corpus
    /// blog model (success criterion 1 + 6). Model members come from the retained scope map; extensions/functions
    /// from the live registries; the abstract intersection excludes members missing at any call site.
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
            Assert.Contains("raw", labels);   // phase 2 alias
            Assert.Contains("else", labels);  // phase 3 alias
            Assert.Contains("upper", labels); // function
            Assert.Contains("range", labels); // phase 4 function
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

        [Fact] // Corpus D24 export: titlecase offered indistinguishably from built-ins
        public void ScannedFunctionExportIsOffered()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@(§)");
            Assert.Contains("titlecase", labels);
        }

        [Fact] // Corpus D23 export: badge offered among extension names
        public void ScannedExtensionIsOffered()
        {
            var labels = Complete("@model(){{Corpus.Blog}}\n@§");
            Assert.Contains("badge", labels);
        }
    }
}
