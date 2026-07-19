using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The six phase-5 fixture/golden pairs (props-card, props-defaults, props-abstract-panel, props-inherit,
    /// slot-picker, slot-compose), rendered under <c>AllowCSharp = false</c> against committed goldens. These
    /// are the file-driven twins of the inline binding/inheritance/slot tests and the phase-9 gallery source.
    /// </summary>
    public class PropsGoldenTests
    {
        private static PropRoot Model() => new PropRoot
        {
            Article = new PropArticle { Title = "T", Summary = "S" },
            Site = new PropSite { DefaultCardStyle = "rooted" },
            Menu = new PropMenu
            {
                Options = new List<PropMenuOption>
                {
                    new PropMenuOption { Id = 1, Label = "A" },
                    new PropMenuOption { Id = 2, Label = "B" }
                }
            }
        };

        private static string RenderFixture(string name, object model)
        {
            HeddleTemplate.Configure(typeof(PropsGoldenTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText($"TestTemplate/{name}.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.Native }, model.GetType()));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        private static void AssertGolden(string name, string actual)
        {
            File.WriteAllText($"TestTemplate/test-{name}.html", actual);
            var expected = File.ReadAllText($"TestTemplate/generated-{name}.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PropsCard() => AssertGolden("props-card", RenderFixture("props-card", Model()));

        [Fact]
        public void PropsDefaults() => AssertGolden("props-defaults", RenderFixture("props-defaults", Model()));

        [Fact]
        public void PropsAbstractPanel() =>
            AssertGolden("props-abstract-panel", RenderFixture("props-abstract-panel", Model()));

        [Fact]
        public void PropsInherit() => AssertGolden("props-inherit", RenderFixture("props-inherit", Model()));

        [Fact]
        public void SlotPicker() => AssertGolden("slot-picker", RenderFixture("slot-picker", Model()));

        [Fact]
        public void SlotCompose() => AssertGolden("slot-compose", RenderFixture("slot-compose", Model()));
    }
}
