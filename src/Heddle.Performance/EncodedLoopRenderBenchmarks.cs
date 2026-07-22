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
[MemoryDiagnoser]
public class EncodedLoopRenderBenchmarks
{
    private EncodedLoopHeddleTest _heddleTest;
    private EncodedLoopFluidTest _fluidTest;
#if !NET6_0
    private EncodedLoopScribanTest _scribanTest;
#endif
    private EncodedLoopDotLiquidTest _dotLiquidTest;
    private EncodedLoopHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new EncodedLoopHeddleTest();
        _fluidTest = new EncodedLoopFluidTest();
#if !NET6_0
        _scribanTest = new EncodedLoopScribanTest();
#endif
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

#if !NET6_0
    [Benchmark]
    public string RenderScriban() => _scribanTest.Render();
#endif

    [Benchmark]
    public string RenderDotLiquid() => _dotLiquidTest.Render();

    [Benchmark]
    public string RenderHandlebars() => _handlebarsTest.Render();
}
