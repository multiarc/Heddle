using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Regression pins for the hidden-token offset defect in subtemplate bodies. A comment (any hidden token)
    /// sitting inside a multi-line definition/override body used to corrupt the render:
    /// <list type="bullet">
    /// <item>Under a cross-file <c>@&lt;&lt;</c> import the importing document's skipped-token positions were carried
    /// into the imported parse (wrong coordinate space) and a position that happened to land inside an imported
    /// definition body's span was injected into it, shifting every output chain by one — stray <c>)</c> after calls,
    /// <c>&lt;main&gt;</c> losing its <c>&gt;</c>.</item>
    /// <item>Under <c>TrimDirectiveLines</c> the whole comment-remnant line inside a definition block was removed
    /// without shrinking the enclosing block's length, so the block removal over-ran into the following static text
    /// (silent truncation, or an out-of-range crash).</item>
    /// </list>
    /// Both are exercised here through the runtime backend; <see cref="Heddle.Generator.IntegrationTests"/>
    /// re-renders the same cross-file fixture through the precompiled backend for the byte-identical differential.
    /// </summary>
    public class MultilineOverrideOffsetRegressionTests
    {
        private static string RenderFixture(string name)
        {
            HeddleTemplate.Configure(typeof(MultilineOverrideOffsetRegressionTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions(name)
            {
                RootPath = "TestTemplate",
                FileNamePostfix = ".heddle"
            };
            var template = new HeddleTemplate(new CompileContext(options, typeof(object)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(null).Replace("\r\n", "\n");
        }

        private static string Golden(string name) =>
            File.ReadAllText("TestTemplate/generated-" + name + ".html").Replace("\r\n", "\n");

        [Fact] // Bug A + Bug B: cross-file @<< import + multi-line indented override body carrying an inner comment.
        public void CrossFileMultilineOverrideWithInnerCommentRendersCleanly()
        {
            var actual = RenderFixture("regr-import-multiline-override");
            // The imported shell body must not drift: the override's hero header, the caller's <main> body, and the
            // shell's own default footer all render whole (no stray ')' , no lost '>').
            Assert.Contains("<header class=\"hero\">HERO-HEADER</header>", actual);
            Assert.Contains("<main> <article>PAGE-BODY</article> </main>", actual);
            Assert.Contains("<footer>DEFAULT-FOOTER</footer>", actual);
            Assert.DoesNotContain("</main>)", actual);
            Assert.DoesNotContain("<main <article", actual);
            Assert.Equal(Golden("regr-import-multiline-override"), actual);
        }

        [Fact] // Bug B: a comment inside a multi-line definition body must not eat the surrounding static text.
        public void MultilineDefinitionBodyWithInnerCommentKeepsSurroundingText()
        {
            var actual = RenderFixture("regr-def-inner-comment");
            Assert.StartsWith("PREFIX-TEXT", actual);
            Assert.Contains("MIDDLE-", actual);
            Assert.Contains("-SUFFIX", actual);
            Assert.Contains("<span>WIDGET</span>", actual);
            Assert.Equal(Golden("regr-def-inner-comment"), actual);
        }
    }
}
