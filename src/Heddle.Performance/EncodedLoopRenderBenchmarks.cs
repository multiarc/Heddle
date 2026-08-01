using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Cross-stack phase 1 encoded-loop render head-to-head (spec D1/WI5), run with
/// <c>dotnet run -c Release -- --filter *EncodedLoopRenderBenchmarks*</c>. 5,000 rows with
/// escapable characters in every cell, HTML encoding ON in both text and attribute contexts
/// (Heddle <c>OutputProfile.Html</c>; Handlebars.Net under the
/// <see cref="FiveEntityTextEncoder"/> — spec D3) — the workload that measures the escaping path
/// at volume. All five engines are parity-checked against the Heddle oracle in
/// <see cref="Setup"/> before any timing. Host-free (no Razor/DI). Each benchmark method returns
/// the rendered string so BenchmarkDotNet's consumer sees it.
/// </summary>
// ShortRunJob (LaunchCount 1, WarmupCount 3, IterationCount 3) rather than BenchmarkDotNet's
// adaptive defaults: ledger E6's uniform ~10 min per-ecosystem measurement budget. At defaults
// these eight suites cost 21.5 min for 41 methods (~32 s each), the second-largest leg.
[MemoryDiagnoser, ShortRunJob]
public class EncodedLoopRenderBenchmarks
{
    private EncodedLoopHeddleTest _heddleTest;
    private EncodedLoopFluidTest _fluidTest;
    private EncodedLoopScribanTest _scribanTest;
    private EncodedLoopDotLiquidTest _dotLiquidTest;
    private EncodedLoopHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new EncodedLoopHeddleTest();
        _fluidTest = new EncodedLoopFluidTest();
        _scribanTest = new EncodedLoopScribanTest();
        _dotLiquidTest = new EncodedLoopDotLiquidTest();
        _handlebarsTest = new EncodedLoopHandlebarsTest();

        // Every competitor twin must render output identical to Heddle under the contract's
        // controlled gate (normalize, then the N3b whitespace strip on both sides) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertEncodedLoop();
        GoldenCorpus.AssertFresh("encoded-loop");
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
