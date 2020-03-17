using Templates.Performance.Runners;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.ObjectPool;
using Templates.Native;
using Templates.Performance.TestSuite;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Templates.Performance
{
    public class Program
    {
        private static readonly List<IRunner> Tests = new List<IRunner>();

        public static void SetUpTests(IServiceProvider serviceProvider) {
            Tests.Add(new RazorTest(serviceProvider));
        }
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(ConfigureDefaultServices)
                .ConfigureLogging(logging => logging.AddConsole(co => co.LogToStandardErrorThreshold = LogLevel.Warning)).Build();

            await host.StartAsync();

            SetUpTests(host.Services);
            //Run all tests
            foreach (var test in Tests) {
                test.Run();
            }
            await host.StopAsync();
            
            Console.WriteLine("Done all, press any key to exit...");
            Console.ReadKey();
        }

        private static void ConfigureDefaultServices(IServiceCollection services)
        {
            var appDirectory = Directory.GetCurrentDirectory() + "\\TestTemplates";

            services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
            {
                options.FileProviders.Clear();
                options.FileProviders.Add(new PhysicalFileProvider(appDirectory));
            });

            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

            var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<DiagnosticSource>(diagnosticSource);

            services.AddLogging();
            services.AddControllersWithViews();
            services.AddSingleton<RazorViewToStringRenderer>();
            AssemblyHelper.Configure(typeof(Program).GetTypeInfo().Assembly);
            AssemblyHelper.Configure(typeof(IHtmlContent).GetTypeInfo().Assembly);
        }

    }
}