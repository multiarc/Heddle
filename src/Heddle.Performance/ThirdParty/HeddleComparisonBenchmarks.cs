using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Fluid.Benchmarks;

namespace Heddle.Performance.ThirdParty
{
    /// <summary>
    /// The head-to-head that adds Heddle to the upstream product-render comparison. It replicates the
    /// upstream <c>ComparisonBenchmarks</c> configuration verbatim — <see cref="MemoryDiagnoserAttribute"/>,
    /// <c>GroupBenchmarksBy(ByCategory)</c>, <c>ShortRunJob</c>, Fluid as the <c>Baseline</c> — over the
    /// same <c>Parse</c> and <c>Render</c> categories, and drives each engine through the shared
    /// <see cref="BaseBenchmarks"/> methods (same model, same output oracle).
    ///
    /// <para>Two deliberate differences from upstream <c>ComparisonBenchmarks</c>, both documented in
    /// <c>ThirdParty/README.md</c>: (1) it adds the <c>Heddle_*</c> rows; (2) it omits Liquid.NET, which
    /// upstream already leaves un-benchmarked ("Ignored since Liquid.NET is much slower and not in active
    /// development") — so the abandoned, vulnerability-flagged package is not pulled into this repo. The
    /// <c>ParseBig</c> category is dropped because Heddle (like Handlebars upstream) carries no big-template
    /// twin. Nothing else about the methodology changes.</para>
    /// </summary>
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class HeddleComparisonBenchmarks
    {
        private readonly FluidBenchmarks _fluidBenchmarks = new FluidBenchmarks();
        private readonly ScribanBenchmarks _scribanBenchmarks = new ScribanBenchmarks();
        private readonly DotLiquidBenchmarks _dotLiquidBenchmarks = new DotLiquidBenchmarks();
        private readonly HandlebarsBenchmarks _handlebarsBenchmarks = new HandlebarsBenchmarks();
        private readonly HeddleBenchmarks _heddleBenchmarks = new HeddleBenchmarks();

        [Benchmark(Baseline = true), BenchmarkCategory("Parse")]
        public object Fluid_Parse() => _fluidBenchmarks.Parse();

        [Benchmark, BenchmarkCategory("Parse")]
        public object Scriban_Parse() => _scribanBenchmarks.Parse();

        [Benchmark, BenchmarkCategory("Parse")]
        public object DotLiquid_Parse() => _dotLiquidBenchmarks.Parse();

        [Benchmark, BenchmarkCategory("Parse")]
        public object Handlebars_Parse() => _handlebarsBenchmarks.Parse();

        [Benchmark, BenchmarkCategory("Parse")]
        public object Heddle_Parse() => _heddleBenchmarks.Parse();

        [Benchmark(Baseline = true), BenchmarkCategory("Render")]
        public string Fluid_Render() => _fluidBenchmarks.Render();

        [Benchmark, BenchmarkCategory("Render")]
        public string Scriban_Render() => _scribanBenchmarks.Render();

        [Benchmark, BenchmarkCategory("Render")]
        public string DotLiquid_Render() => _dotLiquidBenchmarks.Render();

        [Benchmark, BenchmarkCategory("Render")]
        public string Handlebars_Render() => _handlebarsBenchmarks.Render();

        [Benchmark, BenchmarkCategory("Render")]
        public string Heddle_Render() => _heddleBenchmarks.Render();
    }
}
