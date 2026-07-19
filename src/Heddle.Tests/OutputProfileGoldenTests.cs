using System.IO;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generative goldens for the profile phase (WI8): the flagship fixture pinned both ways (roadmap
    /// criterion 1), the <c>@profile()</c> directive fixture, and the partial-lineage fixture pair where an
    /// Html parent's profile is inherited by the compiled child (X05). Line endings are normalized per the
    /// testing standards.
    /// </summary>
    public class OutputProfileGoldenTests
    {
        public class FlagshipModel
        {
            public string Title { get; set; }
            public string Body { get; set; }
            public string Trusted { get; set; }
        }

        public class DirModel { public string Value { get; set; } }
        public class PartialModel { public string UserInput { get; set; } }

        private static void AssertGolden(string name, string actual)
        {
            File.WriteAllText($"TestTemplate/test-{name}.html", actual);
            var expected = File.ReadAllText($"TestTemplate/generated-{name}.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        private static string RenderInline(string fixture, object model, OutputProfile profile)
        {
            HeddleTemplate.Configure(typeof(OutputProfileGoldenTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText($"TestTemplate/{fixture}.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, model.GetType()));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Fact]
        public void FlagshipText()
        {
            var model = new FlagshipModel { Title = "Hello & <World>", Body = "<script>alert(1)</script>", Trusted = "<em>ok</em>" };
            AssertGolden("profile-flagship-text", RenderInline("profile-flagship", model, OutputProfile.Text));
        }

        [Fact]
        public void FlagshipHtml()
        {
            var model = new FlagshipModel { Title = "Hello & <World>", Body = "<script>alert(1)</script>", Trusted = "<em>ok</em>" };
            AssertGolden("profile-flagship-html", RenderInline("profile-flagship", model, OutputProfile.Html));
        }

        [Fact]
        public void DirectiveFlip()
        {
            var model = new DirModel { Value = "<i>" };
            AssertGolden("profile-directive", RenderInline("profile-directive", model, OutputProfile.Text));
        }

        [Fact]
        public void PartialLineageUnderHtmlParent()
        {
            HeddleTemplate.Configure(typeof(OutputProfileGoldenTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("profile-partial-parent")
            {
                RootPath = "TestTemplate",
                FileNamePostfix = ".heddle",
                OutputProfile = OutputProfile.Html
            };
            var t = new HeddleTemplate(new CompileContext(options, typeof(PartialModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var actual = t.Generate(new PartialModel { UserInput = "<script>alert(1)</script>" });
            AssertGolden("profile-partial", actual);
        }
    }
}
