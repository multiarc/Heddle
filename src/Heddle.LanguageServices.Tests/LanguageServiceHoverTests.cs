using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>Phase 6 D15 hover: member CLR types, definition signatures, function overloads (success criterion 4).</summary>
    public class LanguageServiceHoverTests
    {
        private const string Path = "doc.heddle";

        private static HoverResult Hover(string template, string token)
        {
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, template, 1);
            int offset = template.IndexOf(token) + 1;
            return service.GetHover(Path, offset);
        }

        [Fact]
        public void MemberHoverShowsClrType()
        {
            var hover = Hover("@model(){{Corpus.Blog}}\n@list(Articles){{@(Title)}}", "Title");
            Assert.NotNull(hover);
            Assert.Contains("Title", hover.Markdown);
            Assert.Contains("string", hover.Markdown);
        }

        [Fact]
        public void DefinitionHoverShowsSignature()
        {
            var template = "@%<card(style: string = \"plain\")>{{X}} :: Corpus.Article%@\n@card(style: \"x\")";
            var hover = Hover(template, "card(style: \"x\")"); // the call
            Assert.NotNull(hover);
            Assert.Contains("card", hover.Markdown);
            Assert.Contains("Corpus.Article", hover.Markdown);
        }

        [Fact]
        public void FunctionHoverShowsOverloads()
        {
            var hover = Hover("@model(){{Corpus.Blog}}\n@(upper(Title))", "upper");
            Assert.NotNull(hover);
            Assert.Contains("upper", hover.Markdown);
        }

        [Fact]
        public void ScannedExportedFunctionHoverIsRegistryDriven()
        {
            var hover = Hover("@model(){{Corpus.Blog}}\n@(titlecase(Title))", "titlecase");
            Assert.NotNull(hover);
            Assert.Contains("titlecase", hover.Markdown);
        }
    }
}
