using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// How many times a template reads a property is observable — getters log, materialize, and query — so it is part
    /// of what the two tiers have to agree on, not an internal detail. The generated tier hops once per segment; the
    /// engine used to test and read through a fresh copy of everything above each hop, which doubled the reads at
    /// every segment and left the tiers exponentially apart on the same path.
    /// </summary>
    public class MemberHopEvaluationCountTests
    {
        private const string Key = "views/counting-chain.heddle";

        private static string Template(int hops)
        {
            var path = string.Empty;
            for (int i = 0; i < hops; i++)
                path += "Next.";
            return "@model(){{Heddle.Generator.IntegrationTests.Fixtures.CountingChain}}@\\\n@(" + path + "Value)\n";
        }

        [Theory]
        [InlineData(2)]
        [InlineData(6)]
        [InlineData(12)]
        public void BothTiersReadTheModelTheSameNumberOfTimes(int hops)
        {
            var model = CountingChain.Of(hops);
            var backends = DifferentialHarness.DeferredWithOptions(Key, Template(hops), typeof(CountingChain), model,
                new TemplateOptions());

            CountingChain.Reads = 0;
            var precompiled = backends.precompiled();
            var precompiledReads = CountingChain.Reads;

            CountingChain.Reads = 0;
            var dynamic = backends.dynamic();
            var dynamicReads = CountingChain.Reads;

            Assert.Equal(dynamic, precompiled);
            Assert.Equal(precompiledReads, dynamicReads);
            Assert.Equal(hops + 1, dynamicReads);
        }
    }
}
