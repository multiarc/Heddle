using Templates.Performance.Runners;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

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
            var config = new ConfigurationBuilder()
                .Build();

            var builder = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseConfiguration(config)
                .UseStartup(typeof(Startup));

            var host = builder.Build();
            host.Start();

            SetUpTests(host.Services);
            //Run all tests
            foreach (var test in Tests) {
                test.Run();
            }
            Console.WriteLine("Done all, press any key to exit...");
#if !DNXCORE50
            Console.ReadKey();
#else
            Console.ReadLine();
#endif
        }

    }
}