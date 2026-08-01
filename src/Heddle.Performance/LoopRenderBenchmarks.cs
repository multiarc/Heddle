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
// ShortRunJob (LaunchCount 1, WarmupCount 3, IterationCount 3) rather than BenchmarkDotNet's
// adaptive defaults: ledger E6's uniform ~10 min per-ecosystem measurement budget. At defaults
// these eight suites cost 21.5 min for 41 methods (~32 s each), the second-largest leg.
[MemoryDiagnoser, ShortRunJob]
public class LoopRenderBenchmarks
{
    private LoopHeddleTest _heddleTest;
    private LoopFluidTest _fluidTest;
    private LoopScribanTest _scribanTest;
    private LoopDotLiquidTest _dotLiquidTest;
    private LoopHandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public void Setup()
    {
        _heddleTest = new LoopHeddleTest();
        _fluidTest = new LoopFluidTest();
        _scribanTest = new LoopScribanTest();
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

    [Benchmark]
    public async Task RenderScriban()
    {
        await _scribanTest.Run();
    }

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
