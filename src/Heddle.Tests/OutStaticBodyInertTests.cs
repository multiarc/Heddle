using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Regression: <c>@out()</c> with a static-only body (no <c>@</c> content) now emits only the chained value,
    /// not chained+body double-rendered. A dynamic body (with <c>@</c> content) remains a transform and is unaffected.
    /// </summary>
    public class OutStaticBodyInertTests
    {
        public class TitleModel { public string Title { get; set; } }

        private static HeddleTemplate Compile(string document, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(OutStaticBodyInertTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(new TemplateOptions(), modelType));
        }

        private static string Render(string document, ExType modelType, object model, object chained = null)
        {
            var t = Compile(document, modelType);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model, chained);
        }

        [Fact]
        public void ForIndexWithStaticBody_EmitsIndexOnly_NotIndexPlusBody()
        {
            Assert.Equal("[0][1][2]",
                Render("@for(3){{[@out(){{D}}]}}", typeof(object), null));
        }

        [Fact]
        public void TopLevelChainedWithStaticBody_EmitsChainedOnly()
        {
            Assert.Equal("[CH]",
                Render("[@out(){{BODY}}]", typeof(string), null, "CH"));
        }

        [Fact]
        public void NoChainedWithStaticBody_EmitsNothing()
        {
            Assert.Equal("[]",
                Render("[@out(){{BODY}}]", typeof(string), null));
        }

        [Fact]
        public void RenderAndProcessAgree_ForStaticBodyOut_StringChained()
        {
            var processPath = Render("@% <box>{{[@out()]}} %@\n@box(){{@out(){{BODY}}}}", typeof(string), "Hi");
            var renderPath = Render("[@out(){{BODY}}]", typeof(string), null);
            Assert.Equal("[]", processPath);
            Assert.Equal("[]", renderPath);
            Assert.Equal(renderPath, processPath);
        }

        [Fact]
        public void ForIndexWithDynamicBody_RendersTheTransform()
        {
            Assert.Equal("[<0>][<1>][<2>]",
                Render("@for(3){{[@out(){{<@(this)>}}]}}", typeof(object), null));
        }

        [Fact]
        public void TopLevelChainedWithDynamicBody_RendersTheTransform()
        {
            Assert.Equal("[<CH>]",
                Render("[@out(){{<@(this)>}}]", typeof(string), null, "CH"));
        }

        [Fact]
        public void BodilessOut_EmitsChainedValue_Unchanged()
        {
            Assert.Equal("[0][1][2]",
                Render("@for(3){{[@out()]}}", typeof(object), null));
        }

        [Fact]
        public void BodilessOut_WithNoChained_EmitsNothing_Unchanged()
        {
            Assert.Equal("[]",
                Render("[@out()]", typeof(string), null));
        }
    }
}
