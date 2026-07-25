using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Hosting;
using Heddle.Performance.Runners;
using Heddle.Performance.TestSuite;

namespace Heddle.Performance;

// ShortRunJob (LaunchCount 1, WarmupCount 3, IterationCount 3) rather than BenchmarkDotNet's
// adaptive defaults: ledger E6's uniform ~10 min per-ecosystem measurement budget. At defaults
// these eight suites cost 21.5 min for 41 methods (~32 s each), the second-largest leg.
[MemoryDiagnoser, ShortRunJob]
public class TextRenderBenchmarks
{
    private IHost _host;
    private HeddleTest _heddleTest;
    private RazorTest _razorTest;
    private FluidTest _fluidTest;
#if !NET6_0
    private ScribanTest _scribanTest;
#endif
    private DotLiquidTest _dotLiquidTest;
    private HandlebarsTest _handlebarsTest;

    [GlobalSetup]
    public Task Setup() {
        // The MVC host exists for the Razor twin. Since E5 it is required by the GATE as well as
        // the row: Razor is a parity twin now, and it is the one engine that renders through DI.
        _host = RazorHost.Build();

        _heddleTest = new HeddleTest();
        _razorTest = new RazorTest(_host.Services);
        _fluidTest = new FluidTest();
#if !NET6_0
        _scribanTest = new ScribanTest();
#endif
        _dotLiquidTest = new DotLiquidTest();
        _handlebarsTest = new HandlebarsTest();

        // D1-R3: every competitor twin must render output identical to Heddle (after the single
        // documented normalization) before we time anything, so a drifted twin fails loudly rather
        // than benchmarking different work. Passing the host's services includes the Razor twin,
        // which since E5 is asserted like the other four rather than exempt.
        ParityCheck.Assert(_host.Services);
        GoldenCorpus.AssertFresh("composed-page");
        return Task.CompletedTask;
    }

    [GlobalCleanup]
    public async Task Teardown() {
        // StopAsync stops hosted services but does NOT dispose the host: without Dispose the
        // ConsoleLifetime registrations and the PhysicalFileProvider/file-watcher machinery
        // (config reload + Razor runtime compilation) stay alive and can keep the BenchmarkDotNet
        // child process from exiting on Windows after the run finishes. Dispose releases them.
        await _host.StopAsync();
        _host.Dispose();
    }

    // D1-R5: Heddle is the ratio baseline for the render suite.
    [Benchmark(Baseline = true)]
    public async Task RenderHeddle() {
        await _heddleTest.Run();
    }

    [Benchmark]
    public async Task RenderRazor() {
        await _razorTest.Run();
    }

    [Benchmark]
    public async Task RenderFluid() {
        await _fluidTest.Run();
    }

#if !NET6_0
    [Benchmark]
    public async Task RenderScriban() {
        await _scribanTest.Run();
    }
#endif

    [Benchmark]
    public async Task RenderDotLiquid() {
        await _dotLiquidTest.Run();
    }

    [Benchmark]
    public async Task RenderHandlebars() {
        await _handlebarsTest.Run();
    }
    
}