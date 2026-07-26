using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Test model for imported-definition-body fixtures.</summary>
    public class ImportOriginModel { public string Title { get; set; } }

    /// <summary>
    /// The <c>ImportOrigin</c> marker: imported files carry path and site; nested imports end site-anchored
    /// at the top-level import block with the deepest file's path; flag-off stamps nothing; import-marked
    /// compiles skip the scope-map record.
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
            Assert.Equal(0, marked.ImportOrigin.Site.StartIndex);
        }

        [Fact]
        public void ImportedDefinitionBodyCompileErrorIsStamped()
        {
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
            // Imported definition body's offsets must not appear in the scope map (foreign offsets stay out).
            int docLength = context.ScopeMap.Entries.Count == 0 ? 0 : context.ScopeMap.Entries.Max(e => e.Offset + e.Length);
            Assert.True(docLength <= "@<<{{import-origin-badmember-lib.heddle}}\n@lib_card()".Length + 1,
                "No recorded span should extend past the analyzed document length.");
        }
    }
}
