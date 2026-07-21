using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Heddle.Performance.Runners;

namespace Heddle.Performance;

/// <summary>
/// Phase 5 large-loop render head-to-head (spec D5/WI9), run with
/// <c>dotnet run -c Release -- --filter *LoopRenderBenchmarks*</c>. A single list iteration over
/// 5,000 rows, each emitting two scalar members — output dominated by one large loop. All five
/// engines render raw over models materialized once (no per-op model allocation) and are
/// parity-checked byte-identical in <see cref="Setup"/> before any timing. Host-free (no Razor/DI).
/// </summary>
[MemoryDiagnoser]
public class LoopRenderBenchmarks
{
    private LoopHeddleTest _heddleTest;
    private LoopFluidTest _fluidTest;
#if !NET6_0
    private LoopScribanTest _scribanTest;
#endif
    private LoopDotLiquidTest _dotLiquidTest;
    private LoopHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new LoopHeddleTest();
        _fluidTest = new LoopFluidTest();
#if !NET6_0
        _scribanTest = new LoopScribanTest();
#endif
        _dotLiquidTest = new LoopDotLiquidTest();
        _handlebarsTest = new LoopHandlebarsTest();

        // Every competitor twin must render output identical to Heddle (after the single documented
        // normalization — a functional no-op here, the sources are whitespace-free) before we time
        // anything, so a drifted twin fails loudly rather than benchmarking different work.
        ParityCheck.AssertLoop();
        GoldenCorpus.AssertFresh("large-loop");
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
