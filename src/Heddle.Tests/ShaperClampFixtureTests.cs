using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The runtime half of the clamp-drift fixture. The runtime is the fixed point: its
    /// <c>WidenToWholeLine</c> has always clamped, so this template's bytes are unchanged. The fixture now
    /// precompiles and renders byte-identically instead of silently degrading.
    /// </summary>
    public class ShaperClampFixtureTests
    {
        private static HeddleTemplate CompileFixture(string name)
        {
            HeddleTemplate.Configure(typeof(ShaperClampFixtureTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions(name) { RootPath = "TestTemplate", FileNamePostfix = ".heddle" };
            return new HeddleTemplate(new CompileContext(options, ExType.Dynamic));
        }

        [Fact]
        public void OvershootFixtureShapesToTheSameWorkingDocumentTheGeneratorNowProduces()
        {
            var template = CompileFixture("shaper-clamp-overshoot");
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            var document = (RuntimeDocument) typeof(HeddleTemplate)
                .GetField("_runtimeDocument", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(template);

            // The same literal the generator's adapter test asserts for this shape.
            Assert.Equal("X\n", document.Document);
            Assert.Equal("X\n", template.Generate(null));
        }

        [Fact] // the fixture's LF line endings are pinned by .gitattributes (src/Heddle.Tests/TestTemplate/**)
        public void FixtureIsCheckedInWithLfEndings()
        {
            var text = File.ReadAllText(Path.Combine("TestTemplate", "shaper-clamp-overshoot.heddle"));
            Assert.DoesNotContain("\r", text);
        }
    }
}
