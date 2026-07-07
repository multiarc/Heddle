using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>A top-level model for the D25 imported-definition-body fixture (a simple short name the template
    /// type parser resolves without the nested-type <c>+</c> separator).</summary>
    public class ImportOriginModel { public string Title { get; set; } }

    /// <summary>
    /// White-box (IVT) coverage of the phase 6 D25 <c>ImportOrigin</c> marker: entries produced by an imported
    /// file's parse or call-site compile carry the marker (path + site); nested A→B→C imports end site-anchored
    /// in A's coordinates with C's path (shared-instance re-anchor); the flag-off compile stamps nothing; and the
    /// D2 scope-map <c>Record</c> is skipped for import-marked compiles (foreign offsets stay out of the map).
    /// </summary>
    public class ImportOriginTests
    {
        

        private static CompileContext CompileInline(string template, System.Type modelType,
            bool provideLanguageFeatures = true)
        {
            HeddleTemplate.Configure(typeof(ImportOriginTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                RootPath = Path.GetFullPath("TestTemplate") + Path.DirectorySeparatorChar,
                ProvideLanguageFeatures = provideLanguageFeatures
            };
            var context = new CompileContext(options, modelType);
            var _ = new HeddleTemplate(template, context);
            return context;
        }

        private static CompileContext CompileFixture(string name, bool provideLanguageFeatures = true)
        {
            HeddleTemplate.Configure(typeof(ImportOriginTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions(name)
            {
                RootPath = "TestTemplate" + Path.DirectorySeparatorChar,
                FileNamePostfix = ".heddle",
                ProvideLanguageFeatures = provideLanguageFeatures
            };
            var context = new CompileContext(options, typeof(object));
            var _ = new HeddleTemplate(context);
            return context;
        }

        [Fact]
        public void ImportedSyntaxErrorCarriesOriginPathAndSite()
        {
            var context = CompileInline("@<<{{import-origin-broken.heddle}}", typeof(object));
            var marked = context.CompileErrors.FirstOrDefault(e => e.ImportOrigin != null);
            Assert.NotNull(marked);
            Assert.Contains("import-origin-broken.heddle", marked.ImportOrigin.Path);
            // Site anchors at the @<< block in the importing document (starts at offset 0 here).
            Assert.Equal(0, marked.ImportOrigin.Site.StartIndex);
        }

        [Fact]
        public void ImportedDefinitionBodyCompileErrorIsStamped()
        {
            // @lib_card() compiles the imported (pinned) definition body at this call site → HED0001 on the body's
            // NoSuchMember read; the funnel bracket (stamp site 4) marks it with the library origin.
            var context = CompileInline(
                "@<<{{import-origin-badmember-lib.heddle}}\n@lib_card()", typeof(ImportOriginModel));
            var marked = context.CompileErrors.FirstOrDefault(
                e => e.ImportOrigin != null && e.Error.Contains("NoSuchMember"));
            Assert.NotNull(marked);
            Assert.Contains("import-origin-badmember-lib.heddle", marked.ImportOrigin.Path);
        }

        [Fact]
        public void NestedImportsEndSiteAnchoredInTopDocumentWithDeepestPath()
        {
            // A imports B imports C(broken). The C error is stamped during B's parse and re-anchored by A's import
            // block: Path keeps the deepest file (C), Site bubbles to A's coordinates.
            var context = CompileFixture("import-origin-a");
            var marked = context.CompileErrors.FirstOrDefault(e => e.ImportOrigin != null);
            Assert.NotNull(marked);
            Assert.Contains("import-origin-c.heddle", marked.ImportOrigin.Path);
            Assert.Equal(0, marked.ImportOrigin.Site.StartIndex); // the @<< in A begins the document
        }

        [Fact]
        public void FlagOffStampsNoOrigin()
        {
            var context = CompileInline("@<<{{import-origin-broken.heddle}}", typeof(object),
                provideLanguageFeatures: false);
            Assert.All(context.CompileErrors, e => Assert.Null(e.ImportOrigin));
        }

        [Fact]
        public void ImportMarkedBodyDoesNotPolluteScopeMapWithForeignOffsets()
        {
            var context = CompileInline(
                "@<<{{import-origin-badmember-lib.heddle}}\n@lib_card()", typeof(ImportOriginModel));
            Assert.NotNull(context.ScopeMap);
            // The importing document is short; the imported definition body's foreign span (its @(NoSuchMember)
            // offset in the library file) must not appear as a recorded entry.
            int docLength = context.ScopeMap.Entries.Count == 0 ? 0 : context.ScopeMap.Entries.Max(e => e.Offset + e.Length);
            Assert.True(docLength <= "@<<{{import-origin-badmember-lib.heddle}}\n@lib_card()".Length + 1,
                "No recorded span should extend past the analyzed document length.");
        }
    }
}
