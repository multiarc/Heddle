using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Cross-stack phase 1 mixed-page render head-to-head (spec D1/WI5), run with
/// <c>dotnet run -c Release -- --filter *MixedRenderBenchmarks*</c>. One realistic mid-size page:
/// full HTML skeleton, scalar substitutions, a 36-product loop, and page- and row-level
/// conditionals. All five engines render raw and are parity-checked against the Heddle oracle in
/// <see cref="Setup"/> before any timing. Host-free (no Razor/DI). Each benchmark method returns
/// the rendered string so BenchmarkDotNet's consumer sees it.
/// </summary>
// ShortRunJob (LaunchCount 1, WarmupCount 3, IterationCount 3) rather than BenchmarkDotNet's
// adaptive defaults: ledger E6's uniform ~10 min per-ecosystem measurement budget. At defaults
// these eight suites cost 21.5 min for 41 methods (~32 s each), the second-largest leg.
[MemoryDiagnoser, ShortRunJob]
public class MixedRenderBenchmarks
{
    private MixedHeddleTest _heddleTest;
    private MixedFluidTest _fluidTest;
    private MixedScribanTest _scribanTest;
    private MixedDotLiquidTest _dotLiquidTest;
    private MixedHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new MixedHeddleTest();
        _fluidTest = new MixedFluidTest();
        _scribanTest = new MixedScribanTest();
        _dotLiquidTest = new MixedDotLiquidTest();
        _handlebarsTest = new MixedHandlebarsTest();

        // Every competitor twin must render output identical to Heddle under the contract's
        // controlled gate (normalize, then the N3b whitespace strip on both sides) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertMixed();
        GoldenCorpus.AssertFresh("mixed-page");
    }

    // Heddle is the ratio baseline for the render suite.
    [Benchmark(Baseline = true)]
    public string RenderHeddle() => _heddleTest.Render();

    [Benchmark]
    public string RenderFluid() => _fluidTest.Render();

    [Benchmark]
    public string RenderScriban() => _scribanTest.Render();

    [Benchmark]
    public string RenderDotLiquid() => _dotLiquidTest.Render();

    [Benchmark]
    public string RenderHandlebars() => _handlebarsTest.Render();
}
