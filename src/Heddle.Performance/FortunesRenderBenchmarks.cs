using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Cross-stack phase 1 fortunes-encoded render head-to-head (spec D1/WI5), run with
/// <c>dotnet run -c Release -- --filter *FortunesRenderBenchmarks*</c>. The TechEmpower-shaped
/// Fortunes table: 12 pinned rows including the XSS payload and the Japanese string, rendered
/// with HTML encoding ON in every engine (Heddle <c>OutputProfile.Html</c>; Handlebars.Net under
/// the <see cref="FiveEntityTextEncoder"/> — spec D3). All five engines are parity-checked
/// against the Heddle oracle in <see cref="Setup"/> before any timing. Host-free (no Razor/DI).
/// Each benchmark method returns the rendered string so BenchmarkDotNet's consumer sees it.
/// </summary>
[MemoryDiagnoser]
public class FortunesRenderBenchmarks
{
    private FortunesHeddleTest _heddleTest;
    private FortunesFluidTest _fluidTest;
#if !NET6_0
    private FortunesScribanTest _scribanTest;
#endif
    private FortunesDotLiquidTest _dotLiquidTest;
    private FortunesHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new FortunesHeddleTest();
        _fluidTest = new FortunesFluidTest();
#if !NET6_0
        _scribanTest = new FortunesScribanTest();
#endif
        _dotLiquidTest = new FortunesDotLiquidTest();
        _handlebarsTest = new FortunesHandlebarsTest();

        // Every competitor twin must render output identical to Heddle under the contract's
        // controlled gate (normalize, then the N3b whitespace strip on both sides) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertFortunes();
        GoldenCorpus.AssertFresh("fortunes-encoded");
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
