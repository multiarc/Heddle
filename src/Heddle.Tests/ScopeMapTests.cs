using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// White-box (IVT) coverage of the phase 6 D2 scope map: spans + effective types recorded for nested bodies
    /// (<c>@list</c> narrowing, <c>@if</c> step-back — the body records the <i>caller's</i> model, not
    /// <see cref="bool"/>), one entry per compiled call site for abstract definitions, and the <c>null</c> map
    /// when <see cref="TemplateOptions.ProvideLanguageFeatures"/> is off (the null-cost guarantee's source).
    /// </summary>
    public class ScopeMapTests
    {
        public class Article { public string Title { get; set; } public string Summary { get; set; } }
        public class Menu { public string Title { get; set; } }
        public class Blog { public List<Article> Articles { get; set; } public string Title { get; set; } }
        public class Page { public Article Article { get; set; } public Menu Menu { get; set; } }

        private static CompileContext Compile(string template, System.Type modelType,
            bool provideLanguageFeatures = true)
        {
            HeddleTemplate.Configure(typeof(ScopeMapTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ProvideLanguageFeatures = provideLanguageFeatures };
            var context = new CompileContext(options, modelType);
            var template2 = new HeddleTemplate(template, context);
            Assert.True(template2.CompileResult.Success, template2.CompileResult.ToString());
            return context;
        }

        [Fact]
        public void FlagOffLeavesScopeMapNull()
        {
            var context = Compile("@list(Articles){{@(Title)}}", typeof(Blog), provideLanguageFeatures: false);
            Assert.Null(context.ScopeMap);
        }

        [Fact]
        public void FlagOnRecordsRootAndNarrowedListBody()
        {
            var context = Compile("@list(Articles){{@(Title)}}", typeof(Blog));
            Assert.NotNull(context.ScopeMap);
            Assert.Equal(typeof(Blog), context.ScopeMap.RootType.Type);
            // The @list body narrows the model to the element type.
            Assert.Contains(context.ScopeMap.Entries, e => e.ModelType != null && e.ModelType.Type == typeof(Article));
        }

        [Fact]
        public void IfBodyRecordsCallerModelNotBool()
        {
            var context = Compile("@if(Title != null){{@(Title)}}", typeof(Blog));
            Assert.NotNull(context.ScopeMap);
            // Step-back: the @if body threads the caller's model (Blog), never bool.
            Assert.DoesNotContain(context.ScopeMap.Entries,
                e => e.ModelType != null && e.ModelType.Type == typeof(bool));
            Assert.Contains(context.ScopeMap.Entries, e => e.ModelType != null && e.ModelType.Type == typeof(Blog));
        }

        [Fact]
        public void AbstractDefinitionBodyRecordsOneEntryPerCallSite()
        {
            // <panel> is abstract (no :: type): the compiler substitutes each caller's type, so the body compiles
            // once per call site — Article at one, Menu at the other.
            var template = "@%<panel>{{@(Title)}}%@@panel(Article)@panel(Menu)";
            var context = Compile(template, typeof(Page));
            Assert.NotNull(context.ScopeMap);
            var recorded = context.ScopeMap.Entries.Where(e => e.ModelType != null)
                .Select(e => e.ModelType.Type).ToList();
            Assert.Contains(typeof(Article), recorded);
            Assert.Contains(typeof(Menu), recorded);
        }
    }
}
