using System.IO;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// C2-R7 golden corpus fixture exercising all three context-encoding extensions in their real contexts under the
    /// Html profile: <c>@url</c> in a query component, <c>@attr</c> in an attribute value and element text, and
    /// <c>@js</c> in a <c>&lt;script&gt;</c> string literal. Pins that none is double-encoded / entity-corrupted.
    /// </summary>
    public class ContextEncodingGoldenTests
    {
        public class CtxModel
        {
            public string Query { get; set; }
            public string Title { get; set; }
            public string Label { get; set; }
        }

        private static void AssertGolden(string name, string actual)
        {
            File.WriteAllText($"TestTemplate/test-{name}.html", actual);
            var expected = File.ReadAllText($"TestTemplate/generated-{name}.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        [Fact]
        public void CtxEncodingFlagship()
        {
            HeddleTemplate.Configure(typeof(ContextEncodingGoldenTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/ctx-encoding.heddle").Replace("\r\n", "\n");
            var model = new CtxModel
            {
                Query = "c# tips & tricks",
                Title = "Ben & \"Jerry's\" <shop>",
                Label = "</script><x>&\"'",
            };
            var t = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Html }, typeof(CtxModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            AssertGolden("ctx-encoding", t.Generate(model));
        }
    }
}
