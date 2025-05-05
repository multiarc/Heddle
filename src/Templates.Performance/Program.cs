using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;

namespace Templates.Performance
{
    public class Program
    {
        public static void Main(string[] args) {
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