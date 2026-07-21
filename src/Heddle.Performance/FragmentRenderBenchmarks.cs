using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Cross-stack phase 1 fragment-heavy render head-to-head (spec D1/WI5), run with
/// <c>dotnet run -c Release -- --filter *FragmentRenderBenchmarks*</c>. 48 invocations of one
/// <c>tile</c> partial receiving the current row — the shape that measures partial/include
/// dispatch cost. All five engines render raw and are parity-checked against the Heddle oracle in
/// <see cref="Setup"/> before any timing. Host-free (no Razor/DI). Each benchmark method returns
/// the rendered string so BenchmarkDotNet's consumer sees it.
/// </summary>
[MemoryDiagnoser]
public class FragmentRenderBenchmarks
{
    private FragmentHeddleTest _heddleTest;
    private FragmentFluidTest _fluidTest;
#if !NET6_0
    private FragmentScribanTest _scribanTest;
#endif
    private FragmentDotLiquidTest _dotLiquidTest;
    private FragmentHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new FragmentHeddleTest();
        _fluidTest = new FragmentFluidTest();
#if !NET6_0
        _scribanTest = new FragmentScribanTest();
#endif
        _dotLiquidTest = new FragmentDotLiquidTest();
        _handlebarsTest = new FragmentHandlebarsTest();

        // Every competitor twin must render output identical to Heddle under the contract's
        // controlled gate (normalize, then the N3b whitespace strip on both sides) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertFragment();
        GoldenCorpus.AssertFresh("fragment-heavy");
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
