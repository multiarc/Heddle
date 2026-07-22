using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Phase 5 trivial-substitution render head-to-head (spec D5/WI9), run with
/// <c>dotnet run -c Release -- --filter *SubstitutionRenderBenchmarks*</c>. One flat card whose
/// output is dominated by ten scalar member substitutions — no layout, components, or loop; the
/// shape where Heddle's composition advantage is expected to narrow or invert. All five engines
/// render raw and are parity-checked byte-identical in <see cref="Setup"/> before any timing.
/// Host-free (no Razor/DI).
/// </summary>
[MemoryDiagnoser]
public class SubstitutionRenderBenchmarks
{
    private SubstitutionHeddleTest _heddleTest;
    private SubstitutionFluidTest _fluidTest;
#if !NET6_0
    private SubstitutionScribanTest _scribanTest;
#endif
    private SubstitutionDotLiquidTest _dotLiquidTest;
    private SubstitutionHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new SubstitutionHeddleTest();
        _fluidTest = new SubstitutionFluidTest();
#if !NET6_0
        _scribanTest = new SubstitutionScribanTest();
#endif
        _dotLiquidTest = new SubstitutionDotLiquidTest();
        _handlebarsTest = new SubstitutionHandlebarsTest();

        // Every competitor twin must render output identical to Heddle (after the single documented
        // normalization — a functional no-op here, the sources are whitespace-free) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertSubstitution();
        GoldenCorpus.AssertFresh("trivial-substitution");
    }

    // Heddle is the ratio baseline for the render suite.
    [Benchmark(Baseline = true)]
    public async Task RenderHeddle()
    {
        await _heddleTest.Run();
    }

    [Benchmark]
    public async Task RenderFluid()
    {
        await _fluidTest.Run();
    }

#if !NET6_0
    [Benchmark]
    public async Task RenderScriban()
    {
        await _scribanTest.Run();
    }
#endif

    [Benchmark]
    public async Task RenderDotLiquid()
    {
        await _dotLiquidTest.Run();
    }

    [Benchmark]
    public async Task RenderHandlebars()
    {
        await _handlebarsTest.Run();
    }
}
