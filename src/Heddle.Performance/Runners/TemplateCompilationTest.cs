using System;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    public class TemplateCompilationTest
    {
        public TemplateCompilationTest() {
            /*JIT*/
            var test = new HeddleTemplate
            (new CompileContext
            (new TemplateOptions("home") {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplates",
                ExpressionMode = ExpressionMode.FullCSharp
            }));
            if (!test.CompileResult.Success) {
                Console.Write(test.CompileResult.ToString());
            }
            else {
                test.Generate(null);
                test.Dispose();
            }
            /*END JIT*/
        }

        public void Run() {
            Console.WriteLine("Enter tries count (template compile):");
            // = 1000;
            string quantity = Console.ReadLine();
            int.TryParse(quantity, out var n);
            ulong ticksElapsed = 0;
            TimeSpan timeElapsed = TimeSpan.Zero;
            for (int i = 0; i < n; i++) {
                var target = new HeddleTemplate
                (new CompileContext
                (new TemplateOptions("template") {
                    FileNamePostfix = ".heddle",
                    RootPath = @"TestTemplates",
                    ExpressionMode = ExpressionMode.FullCSharp
                }));
                target.Dispose();
            }

            Console.WriteLine("Compile time average: {0}, ticks:{1}", TimeSpan.FromTicks(timeElapsed.Ticks / n),
                ticksElapsed / (ulong) n);
        }
    }
}