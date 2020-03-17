using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Performance.Runners
{
    public class TemplaterTest : IRunner
    {
        public TemplaterTest()
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
                var testString = test.Generate(DataFiller.FillData());
                File.WriteAllText("test.html", testString);
                test.Dispose();
            }
            /*END JIT*/
        }

        public void Run()
        {
            //var list = new List<TestDataStructure>();
            Console.WriteLine("Enter tries count (template generate):");
            // = 1000;
            string quantity = Console.ReadLine();
            int.TryParse(quantity, out var n);
            var data = DataFiller.FillData();
            //for (int i = 0; i < n; i++)
            //    list.Add(data);
            var watcher = new Stopwatch();
            watcher.Start();
            using var target = new TtlTemplate

            (new CompileContext
            (new TemplateOptions("template")
            {
                FileNamePostfix = ".ttl",
                RootPath = @"TestTemplates",
                AllowCSharp = true
            }));
            watcher.Stop();
            Console.WriteLine("Compile time: {0}, ticks:{1}", watcher.Elapsed, watcher.ElapsedTicks);
            if (!target.CompileResult.Success)
            {
                Console.Write(target.CompileResult.ToString());
                return;
            }
            watcher.Reset();
            long length = target.Generate(data).Length * (long)n;
            watcher.Start();
            Enumerable.Repeat(data, n).AsParallel().ForAll(item => target.Generate(item));
            watcher.Stop();
            Console.WriteLine("Parrallel implementation:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0:F} Mb/s, ticks:{1}", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char),
                watcher.ElapsedTicks);
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            watcher.Reset();
            long entireLength = 0;
            watcher.Start();
            for (var i = 0; i < n; i++)
            {
                entireLength += target.Generate(data).Length;
            }
            watcher.Stop();
            Console.WriteLine(entireLength);
            Console.WriteLine("Syncronous implementation:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0:F} Mb/s, ticks:{1}", length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char),
                watcher.ElapsedTicks);
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
        }
    }
}