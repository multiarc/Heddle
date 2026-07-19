using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 2 (post-2.0) WI1/D2 — the <c>@@</c> → literal-<c>@</c> escape. Each test pins one row of the D2
    /// tokenization table: greedy left-to-right pairing, the comment-adjacency guard (<c>@@*</c> stays
    /// directive-<c>@</c> + comment), the odd-run tail error, and the <c>SUB_BLOCK</c> mirror. The golden
    /// fixture proves the escape composes in a document and the render is stable.
    /// </summary>
    public class AtEscapeTests
    {
        private class TitleModel
        {
            public string Title { get; set; }
        }

        private class ArticlesModel
        {
            public string[] Articles { get; set; }
        }

        private static HeddleTemplate Compile(string template, Type modelType)
        {
            HeddleTemplate.Configure(typeof(AtEscapeTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));
        }

        private static string Render(string template, Type modelType, object model)
        {
            var t = Compile(template, modelType);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        private static string Render(string template)
        {
            return Render(template, typeof(object), null);
        }

        [Fact]
        public void AtAtBetweenText()
        {
            Assert.Equal("a@b", Render("a@@b"));
        }

        [Fact]
        public void TwoPairs()
        {
            Assert.Equal("@@", Render("@@@@"));
        }

        [Fact]
        public void EscapeThenDirective()
        {
            Assert.Equal("@T", Render("@@@(Title)", typeof(TitleModel), new TitleModel { Title = "T" }));
        }

        [Fact]
        public void EscapeThenParenText()
        {
            Assert.Equal("@(x)", Render("@@(x)"));
        }

        [Fact]
        public void EscapePercent()
        {
            Assert.Equal("@%", Render("@@%"));
        }

        [Fact]
        public void EmailAddress()
        {
            Assert.Equal("Reach us at @example.com", Render("Reach us at @@example.com"));
        }

        [Fact]
        public void AtAtAtEof()
        {
            // Guard at EOF: InputStream.LA(1) is EOF (-1) != '*', so a trailing '@@' escapes.
            Assert.Equal("a@", Render("a@@"));
        }

        [Fact]
        public void OddRunTailErrors()
        {
            // Odd run: the leading '@@' pairs greedily to a literal '@'; the leftover lone '@' opens a
            // directive with no call — the same HED0003 error class as today (D2/D-BC1).
            var t = Compile("@@@", typeof(object));
            Assert.False(t.CompileResult.Success);
            var error = Assert.Single(t.CompileResult.ErrorList);
            Assert.Equal(HeddleDiagnosticIds.SyntaxError, error.DiagnosticId);
        }

        [Fact]
        public void CommentAdjacentAtAtUnchanged()
        {
            // The comment-adjacency guard: '@@*' is NOT an escape — the first '@' opens a directive, the
            // second begins a comment '@*c*@', and the following call reduces exactly as today
            // (template.heddle:57's shape). The render is identical to the comment-free equivalent:
            // no literal '@' and no literal '*c*' appear.
            const string guarded = "@%\n<badge>\n{{P}}\n%@\n@@*c*@badge()";
            const string baseline = "@%\n<badge>\n{{P}}\n%@\n@badge()";
            var rendered = Render(guarded);
            Assert.Equal(Render(baseline), rendered);
            Assert.Equal("P", rendered);
            Assert.DoesNotContain("@", rendered);
            Assert.DoesNotContain("*c*", rendered);
        }

        [Fact]
        public void EscapeInSubtemplateBody()
        {
            // The SUB_BLOCK mirror: the escape fires inside a subtemplate body; the surrounding spaces lex
            // as SUB_TEXT, so each item renders ' @ '.
            Assert.Equal(" @  @ ", Render("@list(Articles){{ @@ }}", typeof(ArticlesModel),
                new ArticlesModel { Articles = new[] { "x", "y" } }));
        }

        [Fact]
        public void AtEscapeGolden()
        {
            // The text-position escape forms composed in one document, rendered under the generic
            // typeof(object) harness against a committed golden.
            HeddleTemplate.Configure(typeof(AtEscapeTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/at-escape.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var actual = t.Generate(null);
            File.WriteAllText("TestTemplate/test-at-escape.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-at-escape.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AtEscapeCommentAdjacentGolden()
        {
            // WI1 guard regression: the '@@*comment*@call(…)' shape at top level AND inside a SUB_BLOCK
            // body (mirroring template.heddle:57) stays byte-identical — the guard suppresses the escape.
            HeddleTemplate.Configure(typeof(AtEscapeTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/at-escape-comment-adjacent.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var actual = t.Generate(null);
            File.WriteAllText("TestTemplate/test-at-escape-comment-adjacent.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-at-escape-comment-adjacent.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }
    }
}
