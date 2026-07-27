using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Heddle.Native;
using Heddle.Performance.TestSuite;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The MVC host the Razor parity twin renders through (ledger entry E5).
    ///
    /// Extracted so there is exactly one definition: <see cref="TextRenderBenchmarks"/> needs it for
    /// the benchmark, and the `parity` verb needs it so the one-command proof covers all five twins
    /// rather than silently skipping the one engine that requires DI. The verb builds it lazily, so
    /// the other seven workloads' checks stay host-free and fast.
    /// </summary>
    internal static class RazorHost
    {
        /// <summary>Builds and starts the host. The caller owns disposal.</summary>
        public static IHost Build()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                // Host lifecycle hygiene only (no benchmark-semantics change): suppress the console
                // lifetime's "Application started / Press Ctrl+C" status banner so benchmark stdout
                // stays clean, and keep warnings+ on stderr.
                .UseConsoleLifetime(o => o.SuppressStatusMessages = true)
                .ConfigureLogging(logging =>
                    logging.AddConsole(co => co.LogToStandardErrorThreshold = LogLevel.Warning))
                .Build();
            host.Start();
            return host;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
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
            services.AddSingleton<IWebHostEnvironment>(new BenchmarkHostingEnvironment(fileProvider, appDirectory));
            AssemblyHelper.Configure(typeof(Program).GetTypeInfo().Assembly);
            AssemblyHelper.Configure(typeof(IHtmlContent).GetTypeInfo().Assembly);
        }

        private sealed class BenchmarkHostingEnvironment : IWebHostEnvironment
        {
            public BenchmarkHostingEnvironment(IFileProvider contentRootFileProvider, string webRootPath)
            {
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
}
