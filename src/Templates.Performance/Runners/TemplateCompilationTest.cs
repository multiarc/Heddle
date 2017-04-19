using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Templates.Collections;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Performance.Runners
{
    public class TemplateCompilationTest : IRunner
    {
        public TemplateCompilationTest(IServiceProvider serviceProvider)
        {
            /*JIT*/
            var test = new TtlTemplate
                (new CompileContext
                     (new TemplateOptions("template")
                     {
                         FileNamePostfix = ".ttl",
                         RootPath = @"TestTemplates",
                         AllowCSharp = true
                     }));
            if (!test.CompileResult.Success)
            {
                Console.Write(test.CompileResult.ToString());
            }
            else
            {
                test.Generate(DataFiller.FillData());
                test.Dispose();
            }
            /*END JIT*/
        }

        public void Run()
        {
            Console.WriteLine("Enter tries count (template compile):");
            // = 1000;
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);
            var watcher = new ExecutionStopwatch();
            ulong ticksElapsed = 0;
            TimeSpan timeElapsed = TimeSpan.Zero;
            for (int i = 0; i < n; i++)
            {
                watcher.Start();
                var target = new TtlTemplate
                    (new CompileContext
                        (new TemplateOptions("template")
                        {
                            FileNamePostfix = ".ttl",
                            RootPath = @"TestTemplates",
                            AllowCSharp = true
                        }));
                watcher.Stop();
                ticksElapsed += watcher.CpuTicks;
                timeElapsed += watcher.Elapsed;
                target.Dispose();
                watcher.Reset();
            }
            Console.WriteLine("Compile time average: {0}, ticks:{1}", TimeSpan.FromTicks(timeElapsed.Ticks/n), ticksElapsed/(ulong) n);
        }
    }
}
