using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Cross-stack phase 1 conditional-heavy render head-to-head (spec D1/WI5), run with
/// <c>dotnet run -c Release -- --filter *ConditionalRenderBenchmarks*</c>. 200 rows, each
/// dispatched through one four-way branch chain on precomputed booleans plus two toggles — the
/// shape that measures branch dispatch on identical data everywhere. All five engines render raw
/// and are parity-checked against the Heddle oracle in <see cref="Setup"/> before any timing.
/// Host-free (no Razor/DI). Each benchmark method returns the rendered string so
/// BenchmarkDotNet's consumer sees it.
/// </summary>
[MemoryDiagnoser]
public class ConditionalRenderBenchmarks
{
    private ConditionalHeddleTest _heddleTest;
    private ConditionalFluidTest _fluidTest;
#if !NET6_0
    private ConditionalScribanTest _scribanTest;
#endif
    private ConditionalDotLiquidTest _dotLiquidTest;
    private ConditionalHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new ConditionalHeddleTest();
        _fluidTest = new ConditionalFluidTest();
#if !NET6_0
        _scribanTest = new ConditionalScribanTest();
#endif
        _dotLiquidTest = new ConditionalDotLiquidTest();
        _handlebarsTest = new ConditionalHandlebarsTest();

        // Every competitor twin must render output identical to Heddle under the contract's
        // controlled gate (normalize, then the N3b whitespace strip on both sides) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertConditional();
        GoldenCorpus.AssertFresh("conditional-heavy");
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
