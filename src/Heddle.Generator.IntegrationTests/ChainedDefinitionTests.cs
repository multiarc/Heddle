using System;
using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Differential coverage for chaining a value INTO a definition call (<c>@box():producer()</c>). This suite
    /// used to pin the opposite fact: the generator refused every multi-item chain, so these two templates fell
    /// back to the dynamic tier and the assertion was <c>ExpectDegrade</c>. They precompile now — the chain is
    /// emitted as the engine's own <c>TemplateChain</c> shape — and the rendered bytes below are unchanged, which
    /// is what makes the flip a capability rather than a behaviour change.
    /// <para>The definition-specific fact is <c>MarkChainConsumer</c>'s: a bodiless definition invocation with a
    /// producer to its right takes its content from the chained channel, so the chained value is what its
    /// <c>@out()</c> emits. On this tier that is <c>PrecompiledInitSite.HasProducerToRight</c>, which
    /// <c>PrecompiledRuntime.InitDefinition</c> reads to set the carrier's <c>ReceivesChainedValue</c>.</para>
    /// </summary>
    public class ChainedDefinitionTests
    {
        private static string RenderDynamic(string content, string model)
        {
            var t = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), new ExType(typeof(string))));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Theory]
        // A value chained into a definition: box's @out() emits it.
        [InlineData("@% <box>{{[@out()]}} %@\n@box():string(this)", "Hi", "[Hi]\n")]
        // The @heading():emphasis() wrapper chain, valued through a string producer.
        [InlineData("@% <heading>{{<h2>@out()</h2>}} <emphasis>{{<em>@out()</em>}} %@\n@heading():emphasis():string(this)",
            "Hi", "<h2><em>Hi</em></h2>\n")]
        public void ChainedIntoDefinition_Precompiles_AndRendersIdentically(string body, string value,
            string expected)
        {
            const string key = "views/chained-def.heddle";
            var template = "@model(){{System.String}}@\\\n" + body + "\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            // Tier-pinned: fallback is byte-identical by design, so an unpinned byte comparison would prove
            // nothing about which tier produced them.
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), value);
            Assert.Equal(dyn, pre);

            // Chained value reaches definition @out() — the same bytes this suite pinned when it fell back.
            Assert.Equal(expected, pre);
            Assert.Equal(expected, RenderDynamic(template, value));
        }
    }
}
