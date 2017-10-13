using Templates.Performance.Runners;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.ObjectPool;
using Templates.Native;
using Templates.Performance.TestSuite;
using System.Reflection;

namespace Templates.Performance
{
    public class Program
    {
        private static readonly List<IRunner> Tests = new List<IRunner>();

        public static void SetUpTests(IServiceProvider serviceProvider) {
            Tests.Add(new RazorTest(serviceProvider));
        }
        public static void Main(string[] args)
        {
            // Initialize the necessary services
            var services = new ServiceCollection();
            ConfigureDefaultServices(services);
            var provider = services.BuildServiceProvider();

            SetUpTests(provider);
            //Run all tests
            foreach (var test in Tests) {
                test.Run();
            }
            Console.WriteLine("Done all, press any key to exit...");
            Console.ReadKey();
        }

        private static void ConfigureDefaultServices(IServiceCollection services)
        {
            var appDirectory = Directory.GetCurrentDirectory() + "\\TestTemplates";

            var environment = new HostingEnvironment
            {
                WebRootFileProvider = new PhysicalFileProvider(appDirectory),
                ApplicationName = "Templates.Performance"
            };
            services.AddSingleton<IHostingEnvironment>(environment);

            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.FileProviders.Clear();
                options.FileProviders.Add(new PhysicalFileProvider(appDirectory));
                options.CompilationCallback = ctx => ctx.Compilation =
                    ctx.Compilation.AddReferences(AssemblyHelper.GetApplicationReferences());
            });

            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

            var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<DiagnosticSource>(diagnosticSource);

            services.AddLogging();
            services.AddMvc();
            services.AddSingleton<RazorViewToStringRenderer>();
            AssemblyHelper.Configure(typeof(Program).GetTypeInfo().Assembly);
            AssemblyHelper.Configure(typeof(IHtmlContent).GetTypeInfo().Assembly);
        }

    }
}