using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Regression coverage for the built-in <c>@out()</c> (<see cref="Heddle.Extensions.OutExtension"/>, the non-slot
    /// mode) when it carries its own body. A non-slot <c>@out</c> is a parameterless value emitter: it emits the
    /// chained value, exactly like <c>@()</c>/<c>@raw</c>/<c>@html</c> emit the model value. Per the value-emitter
    /// convention a body counts only when it holds dynamic (<c>@</c>) content (<c>InnerExist</c>); a static-only body
    /// (<c>InnerExist == false</c>, its text parked in <c>_innerResult</c>) is <b>inert</b>. So a static-body
    /// <c>@out()</c> emits ONLY the chained value, exactly as <c>ProcessData</c> returns <c>scope.ChainedData</c>.
    /// A static body is NOT a null-default the way <c>@string</c>/<c>@attr</c> bodies are (those gate on
    /// <c>model == null</c>, not <c>InnerExist</c>) — <c>@out</c> has no value parameter and no null-default concept.
    ///
    /// Previously <c>RenderData</c> was missing a <c>return</c> after rendering the chained value, so it fell through
    /// to <c>RenderInnerResult</c> and ALSO emitted the inert static body: the chained value AND the body double-
    /// rendered (<c>&lt;chained&gt;&lt;body&gt;</c>) on the render path — the lone place a value emitter did that.
    ///
    /// A dynamic (<c>@</c>-containing) body is a real transform of the chained value and is unaffected — it takes the
    /// <c>InnerExist</c> branch in both paths. Distinct from the definition <c>@out()</c> chain flow
    /// (<see cref="Heddle.Core.DefinitionBaseExtension"/>) and from slot mode, both unchanged here.
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

        // ---- The double-render: chained value present + an inert static body ---------------------------------------

        [Fact]
        public void ForIndexWithStaticBody_EmitsIndexOnly_NotIndexPlusBody()
        {
            // @for threads the loop index on the chained channel; @out()'s static body must NOT also render.
            // Previously rendered "[0D][1D][2D]" (index AND body); now the index alone.
            Assert.Equal("[0][1][2]",
                Render("@for(3){{[@out(){{D}}]}}", typeof(object), null));
        }

        [Fact]
        public void TopLevelChainedWithStaticBody_EmitsChainedOnly()
        {
            // A chained value supplied at the top level; the inert static @out() body must not double-render.
            // Previously "[CHBODY]"; now "[CH]".
            Assert.Equal("[CH]",
                Render("[@out(){{BODY}}]", typeof(string), null, "CH"));
        }

        [Fact]
        public void NoChainedWithStaticBody_EmitsNothing()
        {
            // No chained value + an inert static body: @out emits the (absent) chained value only, body is inert.
            // Previously "[BODY]"; now "[]" (matching ProcessData, which returns the null chained value).
            Assert.Equal("[]",
                Render("[@out(){{BODY}}]", typeof(string), null));
        }

        // ---- Render/Process parity for the SAME bodied @out() (the STRING case) ------------------------------------

        [Fact]
        public void RenderAndProcessAgree_ForStaticBodyOut_StringChained()
        {
            // A definition's caller body is captured via GetInnerResult (ProcessData) — DefinitionBaseExtension. So the
            // caller-body @out(){{BODY}} runs through ProcessData and yields "[]". The render-path form must agree.
            // Parity holds for the string case; a non-string chained value is a separate axis (the process/concat path
            // drops non-strings via `as string ?? ""` — a pre-existing issue orthogonal to the inert-body rule).
            var processPath = Render("@% <box>{{[@out()]}} %@\n@box(){{@out(){{BODY}}}}", typeof(string), "Hi");
            var renderPath = Render("[@out(){{BODY}}]", typeof(string), null);
            Assert.Equal("[]", processPath);
            Assert.Equal("[]", renderPath);
            Assert.Equal(renderPath, processPath);
        }

        // ---- The dynamic-body transform is unaffected (InnerExist branch in both paths) ---------------------------

        [Fact]
        public void ForIndexWithDynamicBody_RendersTheTransform()
        {
            // A body containing an @-construct is a compiled transform of the chained value (its model), NOT an inert
            // body to be suppressed. It renders the body with the index as its model: unchanged by the fix.
            Assert.Equal("[<0>][<1>][<2>]",
                Render("@for(3){{[@out(){{<@(this)>}}]}}", typeof(object), null));
        }

        [Fact]
        public void TopLevelChainedWithDynamicBody_RendersTheTransform()
        {
            Assert.Equal("[<CH>]",
                Render("[@out(){{<@(this)>}}]", typeof(string), null, "CH"));
        }

        // ---- The bodiless @out() (the common case) is unchanged ---------------------------------------------------

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
