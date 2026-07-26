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
            Assert.Equal("[Hi]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this)", typeof(string), "Hi"));
        }

        [Fact]
        public void DocumentedHeadingEmphasisChain()
        {
            // Language-reference example with a string producer providing the value for the chain.
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
            // No producer and no caller body: @out() has nothing to emit.
            Assert.Equal("[]",
                Render("@% <box>{{[@out()]}} %@\n@box()", typeof(string), "Hi"));
        }

        [Fact]
        public void ForIndexDoesNotLeakIntoDefinitionOut()
        {
            // @for threads its index on the chained channel, but a lone @box() is not a chain consumer.
            Assert.Equal("[][][]",
                Render("@% <box>{{[@out()]}} %@\n@for(3){{@box()}}", typeof(object), null));
        }

        // Caller body on a chained definition call ALWAYS wins (static or dynamic).

        [Fact]
        public void StaticCallerBodyWinsOverChainedValue()
        {
            Assert.Equal("[BODY]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this){{BODY}}", typeof(string), "Hi"));
        }

        [Fact]
        public void StaticCallerBodyWinsOverChainedBooleanProducer()
        {
            Assert.Equal("[YES]",
                Render("@% <box>{{[@out()]}} %@\n@box():if(true){{YES}}", typeof(string), "Hi"));
        }

        [Fact]
        public void DynamicCallerBodyWinsOverChainedValue()
        {
            Assert.Equal("[HiDY]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this){{@()DY}}", typeof(string), "Hi"));
        }

        [Fact]
        public void ExplicitEmptyCallerBodyWinsAndRendersEmpty()
        {
            // A syntactically-present body (even {{}}) WINS, distinguishing it from no body.
            Assert.Equal("[]",
                Render("@% <box>{{[@out()]}} %@\n@box():string(this){{}}", typeof(string), "Hi"));
        }

        [Fact]
        public void NoBodyMemberProducerChainStillFlows()
        {
            Assert.Equal("[TITLE]",
                Render("@% <box>{{[@out()]}} %@\n@box():(Title)", typeof(TitleModel),
                    new TitleModel { Title = "TITLE" }));
        }

        // FINDING 2 (DEFERRED): slot-mode definitions drop chained values (no body to project into).

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
