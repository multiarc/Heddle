using System.IO;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Resolver cache identity under output profiles (phase 2 D8, X08): one resolver serves the same file
    /// under both profiles from two profile-suffixed cache entries (no collision), repeat requests hit the
    /// cache, the additive three-parameter constructor supplies a default profile, per-call context options
    /// take precedence over that default, and <c>RemoveFromCache</c> still evicts by instance.
    /// </summary>
    public class TemplateResolverProfileTests
    {
        public class FlagshipModel
        {
            public string Title { get; set; }
            public string Body { get; set; }
            public string Trusted { get; set; }
        }

        private static readonly string Root = Path.Combine("TestTemplate", "anchor.heddle");

        // The None-with-context path reads the file from the passed context's options, so a typed context
        // used with GetTemplate must carry the file-loading options (root path + extension) as a host would.
        private static CompileContext Context(OutputProfile profile) =>
            new CompileContext(new TemplateOptions("profile-flagship")
            {
                RootPath = "TestTemplate",
                FileNamePostfix = ".heddle",
                OutputProfile = profile
            }, typeof(FlagshipModel));

        private static FlagshipModel Model() => new FlagshipModel
        {
            Title = "T",
            Body = "<script>alert(1)</script>",
            Trusted = "<em>ok</em>"
        };

        [Fact]
        public void SameFileUnderBothProfilesYieldsTwoEntriesAndDifferentOutput()
        {
            HeddleTemplate.Configure(typeof(TemplateResolverProfileTests).GetTypeInfo().Assembly);
            var resolver = new TemplateResolver(Root, false);

            var textTemplate = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Text), TemplatePathType.None);
            var htmlTemplate = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Html), TemplatePathType.None);

            Assert.NotNull(textTemplate);
            Assert.NotNull(htmlTemplate);
            Assert.NotSame(textTemplate, htmlTemplate); // distinct cache entries, no Dictionary.Add collision

            var model = Model();
            Assert.Contains("<script>alert(1)</script>", textTemplate.Generate(model));
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", htmlTemplate.Generate(model));
        }

        [Fact]
        public void RepeatRequestsHitTheCachePerProfile()
        {
            HeddleTemplate.Configure(typeof(TemplateResolverProfileTests).GetTypeInfo().Assembly);
            var resolver = new TemplateResolver(Root, false);

            var text1 = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Text), TemplatePathType.None);
            var html1 = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Html), TemplatePathType.None);
            var text2 = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Text), TemplatePathType.None);
            var html2 = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Html), TemplatePathType.None);

            Assert.Same(text1, text2);
            Assert.Same(html1, html2);
        }

        [Fact]
        public void ThreeParameterConstructorAppliesDefaultProfileWhenNoContext()
        {
            HeddleTemplate.Configure(typeof(TemplateResolverProfileTests).GetTypeInfo().Assembly);

            var htmlResolver = new TemplateResolver(Root, false, OutputProfile.Html);
            var htmlTemplate = htmlResolver.GetTemplate("profile-resolver-default.heddle", "", out _, null, TemplatePathType.None);
            Assert.Equal("&lt;b&gt;", htmlTemplate.Generate("<b>"));

            var textResolver = new TemplateResolver(Root, false, OutputProfile.Text);
            var textTemplate = textResolver.GetTemplate("profile-resolver-default.heddle", "", out _, null, TemplatePathType.None);
            Assert.Equal("<b>", textTemplate.Generate("<b>"));
        }

        [Fact]
        public void PerCallContextProfileOverridesResolverDefault()
        {
            HeddleTemplate.Configure(typeof(TemplateResolverProfileTests).GetTypeInfo().Assembly);

            // Resolver default is Html, but the per-call context asks for Text — the context wins.
            var resolver = new TemplateResolver(Root, false, OutputProfile.Html);
            var template = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Text), TemplatePathType.None);
            Assert.Contains("<script>alert(1)</script>", template.Generate(Model()));
        }

        [Fact]
        public void RemoveFromCacheEvictsByInstance()
        {
            HeddleTemplate.Configure(typeof(TemplateResolverProfileTests).GetTypeInfo().Assembly);
            var resolver = new TemplateResolver(Root, false);

            var first = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Text), TemplatePathType.None);
            resolver.RemoveFromCache(first);
            var second = resolver.GetTemplate("profile-flagship.heddle", "", out _, Context(OutputProfile.Text), TemplatePathType.None);

            Assert.NotSame(first, second); // re-created because the cached instance was evicted
        }
    }
}
