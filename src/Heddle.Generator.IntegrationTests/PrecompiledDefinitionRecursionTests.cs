using System;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The recursion counter belongs to the definition carrier, and a generated definition binds the same carrier the
    /// engine does — so what the counter does on the way out of a failed render is one behaviour with two entry
    /// points into it, not two behaviours. A generated template is held by a static field for the life of the
    /// process, which makes an increment lost to a throw permanent in exactly the way it is on the dynamic tier.
    /// </summary>
    public class PrecompiledDefinitionRecursionTests
    {
        private const string Key = "views/precompiled-recursion.heddle";

        /// <summary>A definition called for its value from inside another definition's caller content: the half of
        /// the carrier that produces a value rather than rendering one.</summary>
        private const string Template =
            "@%<card>{{@(Value)}} :: Heddle.Generator.IntegrationTests.Fixtures.ExplodingModel%@\\\n" +
            "@%<frame>{{[@out()]}} :: Heddle.Generator.IntegrationTests.Fixtures.ExplodingModel%@\\\n" +
            "@frame(){{@card()}}";

        [Fact]
        public void AFailedValueProducingRenderDoesNotSpendThePrecompiledRecursionBudget()
        {
            var gen = DifferentialHarness.Generate(new[] { (Key, Template) });
            DifferentialHarness.ExpectPrecompiled(gen, Key);

            // Taken before the failures rather than written down here: the assertion is that the failures changed
            // nothing, not what the output happens to look like.
            var healthy = DifferentialHarness.RenderGenerated(gen, Key, new ExplodingModel { Explode = false });

            var budget = new TemplateOptions().MaxRecursionCount;
            for (var i = 0; i < budget * 2; i++)
                Assert.ThrowsAny<Exception>(() =>
                    DifferentialHarness.RenderGenerated(gen, Key, new ExplodingModel { Explode = true }));

            Assert.Equal(healthy,
                DifferentialHarness.RenderGenerated(gen, Key, new ExplodingModel { Explode = false }));
        }
    }
}
