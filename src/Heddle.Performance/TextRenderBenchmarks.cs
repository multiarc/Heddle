using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Heddle.Native;
using Heddle.Performance.Runners;
using Heddle.Performance.TestSuite;

namespace Heddle.Performance;

[MemoryDiagnoser]
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
    public async Task Setup() {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureDefaultServices)
            .ConfigureLogging(logging => logging.AddConsole(co => co.LogToStandardErrorThreshold = LogLevel.Warning))
            .Build();

        await _host.StartAsync();
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
        // than benchmarking different work.
        ParityCheck.Assert();
        GoldenCorpus.AssertFresh("composed-page");
    }

    [GlobalCleanup]
    public async Task Teardown() {
        await _host.StopAsync();
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
    
    private static void ConfigureDefaultServices(IServiceCollection services) {
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

        var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
        services.AddSingleton<DiagnosticSource>(diagnosticSource);

        services.AddLogging();
        services.AddControllersWithViews();
        services.AddRazorPages().AddRazorRuntimeCompilation().AddApplicationPart(typeof(Program).Assembly);
        services.AddSingleton<RazorViewToStringRenderer>();
        services.AddSingleton<DiagnosticSource>(diagnosticSource);
        services.AddSingleton<DiagnosticListener>(diagnosticSource);
        var appDirectory = Directory.GetCurrentDirectory();
        var fileProvider = new PhysicalFileProvider(appDirectory);
        services.AddSingleton<IWebHostEnvironment>(new HostingEnvironment(fileProvider, appDirectory));
        AssemblyHelper.Configure(typeof(Program).GetTypeInfo().Assembly);
        AssemblyHelper.Configure(typeof(IHtmlContent).GetTypeInfo().Assembly);
    }

    internal class HostingEnvironment : IWebHostEnvironment
    {
        public HostingEnvironment(IFileProvider contentRootFileProvider, string webRootPath) {
            ContentRootFileProvider = contentRootFileProvider;
            WebRootPath = webRootPath;
            ContentRootPath = webRootPath;
            WebRootFileProvider = contentRootFileProvider;
        }

        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "Heddle.Performance";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}