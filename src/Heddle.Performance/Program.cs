using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Heddle.Performance.Runners;

namespace Heddle.Performance
{
    public class Program
    {
        public static void Main(string[] args) {
            // Fast, host-free D1 parity harness: `dotnet run -c Release -- parity`. Confirms every
            // competitor twin renders output identical to Heddle after the documented normalization
            // without spinning up the full BenchmarkDotNet run (which the render GlobalSetup also asserts).
            if (args != null && args.Length > 0 && string.Equals(args[0], "parity", StringComparison.OrdinalIgnoreCase))
            {
                var (allMatch, text) = ParityCheck.Report();
                Console.Write(text);
                Environment.Exit(allMatch ? 0 : 1);
                return;
            }

            // Switcher so either suite runs, e.g. `dotnet run -c Release -- --filter *TemplateParseBenchmarks*`.
            // With no args it prompts; the render head-to-head stays the default when a single type is selected.
            if (args != null && args.Length > 0)
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
                return;
            }

            BenchmarkRunner.Run<TextRenderBenchmarks>();
            // var runner = new TextRenderBenchmarks();
            // await runner.Setup();
            // while (true) {
            //     await runner.RenderTemplateEngine();
            // }
            //
            // await runner.Teardown();
        }
    }
}