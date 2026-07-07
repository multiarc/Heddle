using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace Heddle.Performance
{
    public class Program
    {
        public static void Main(string[] args) {
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