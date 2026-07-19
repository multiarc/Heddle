using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Regression coverage for chaining a value INTO a definition call (<c>@box():producer()</c>). The definition's
    /// body <c>@out()</c> must emit the chained producer's output — the documented <c>@heading():emphasis()</c>
    /// pattern (language-reference.md "Chaining with ':'"). Previously the chained value was silently dropped:
    /// <see cref="Heddle.Core.DefinitionBaseExtension"/> threaded only its (empty) caller body onto the chained
    /// channel, ignoring the value the chain had already placed on <c>Scope.ChainedData</c>. The fix routes the
    /// chained value only for a definition that is a genuine chain consumer (a producer to its right) with no caller
    /// body — so ambient chained data (e.g. the <c>@for</c> loop index) never leaks into a lone <c>@box()</c>.
    /// </summary>
    public class ChainedDefinitionTests
    {
        public class TitleModel { public string Title { get; set; } }

        private static HeddleTemplate Compile(string document, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(ChainedDefinitionTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(new TemplateOptions(), modelType));
        }

        private static string Render(string document, ExType modelType, object model)
        {
            var t = Compile(document, modelType);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Fact]
        public void ChainedValueReachesDefinitionOut()
        {
            // @box():producer() — box's @out() must emit the producer's output, not an empty string.
            Assert.Equal("[Hi]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this)", typeof(string), "Hi"));
        }

        [Fact]
        public void DocumentedHeadingEmphasisChain()
        {
            // The language-reference example: @heading():emphasis() renders <h2><em>…</em></h2>. Extended with a
            // string producer so a real value flows the whole chain (three definitions deep).
            Assert.Equal("<h2><em>Hi</em></h2>",
                Render("@% <heading>{{<h2>@out()</h2>}} <emphasis>{{<em>@out()</em>}} %@\n" +
                       "@heading():emphasis():string(this)", typeof(string), "Hi"));
        }

        [Fact]
        public void CallerBodyStillWins()
        {
            // The body form is unchanged: @box(){{HELLO}} threads the caller body onto the chained channel.
            Assert.Equal("[HELLO]",
                Render("@% <box>{{[@out()]}} %@\n@box(){{HELLO}}", typeof(string), "Hi"));
        }

        [Fact]
        public void LoneDefinitionCallStaysEmpty()
        {
            // No producer and no caller body: @out() has nothing to emit — the chained channel carries no caller
            // content here, so the result stays empty (unchanged behavior).
            Assert.Equal("[]",
                Render("@% <box>{{[@out()]}} %@\n@box()", typeof(string), "Hi"));
        }

        [Fact]
        public void ForIndexDoesNotLeakIntoDefinitionOut()
        {
            // @for threads its index on the chained channel. A lone @box() inside the loop body is NOT a chain
            // consumer, so the index must not leak into its @out(); the result stays empty per iteration.
            Assert.Equal("[][][]",
                Render("@% <box>{{[@out()]}} %@\n@for(3){{@box()}}", typeof(object), null));
        }

        // -------------------------------------------------------------------------------------------------------
        // FINDING 1 regressions: a caller body on a chained definition call ALWAYS wins over the chained value.
        // The gate is the compile-time fact "the call site carries a {{...}} body" (OutputItem.ParameterTemplate
        // != null), not the runtime subtemplate flag — so a *pure static* body (which leaves the runtime
        // InnerExist == false) wins too, regardless of whether the body contains an @-construct.
        // -------------------------------------------------------------------------------------------------------

        [Fact]
        public void StaticCallerBodyWinsOverChainedValue()
        {
            // Pure-static body on a chained call. Previously dropped (emitted the chained "Hi"); must render the body.
            Assert.Equal("[BODY]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this){{BODY}}", typeof(string), "Hi"));
        }

        [Fact]
        public void StaticCallerBodyWinsOverChainedBooleanProducer()
        {
            // A static body chained after an @if(true) producer. Previously the body was lost (rendered []).
            Assert.Equal("[YES]",
                Render("@% <box>{{[@out()]}} %@\n@box():if(true){{YES}}", typeof(string), "Hi"));
        }

        [Fact]
        public void DynamicCallerBodyWinsOverChainedValue()
        {
            // A body containing an @-construct still wins (unchanged) — precedence no longer depends on @-content.
            Assert.Equal("[HiDY]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this){{@()DY}}", typeof(string), "Hi"));
        }

        [Fact]
        public void ExplicitEmptyCallerBodyWinsAndRendersEmpty()
        {
            // Ruling: a syntactically-present body — even the empty {{}} — is a body and WINS, so @out() emits it
            // (the empty string), NOT the chained value. Distinguishes a present-empty body ("") from no body (null).
            Assert.Equal("[]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this){{}}", typeof(string), "Hi"));
        }

        [Fact]
        public void NoBodyMemberProducerChainStillFlows()
        {
            // The no-body chain still flows: @box():(Title) emits the member producer's value into box's @out().
            Assert.Equal("[TITLE]",
                Render("@% <box>{{[@out()]}} %@\n@box():(Title)", typeof(TitleModel),
                    new TitleModel { Title = "TITLE" }));
        }

        // -------------------------------------------------------------------------------------------------------
        // FINDING 2 (DEFERRED): chaining a value INTO a SLOT-mode definition drops the value. A slot @out(expr)
        // projects the caller's {{...}} *template body* (with expr as its model); a chained producer supplies a
        // rendered *value*, not template content — there is no body to project and no clean channel to route the
        // value through, so the non-slot fix does not extend here. This pins the current (documented) behavior:
        // a bodiless chained call into a slot definition renders the wrapper with an empty projection.
        // -------------------------------------------------------------------------------------------------------

        [Fact]
        public void ChainedValueIntoSlotDefinitionIsDropped_Deferred()
        {
            Assert.Equal("<s></s>",
                Render("@% <sbox(out:: string)>{{<s>@out(this)</s>}} %@\n@sbox():string(this)", typeof(string), "Hi"));
        }

        [Fact]
        public void SlotDefinitionWithCallerBodyStillProjects()
        {
            // Contrast: the normal slot path (a caller body) is unaffected by the chained-definition fix.
            Assert.Equal("<s>BODY</s>",
                Render("@% <sbox(out:: string)>{{<s>@out(this)</s>}} %@\n@sbox(){{BODY}}", typeof(string), "Hi"));
        }
    }
}
