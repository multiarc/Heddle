using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Templates.Collections;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Performance.Runners {
    public class TemplaterTest : IRunner {
        public TemplaterTest()
        {
            /*JIT*/
            var test = new TtlTemplate
                (new CompileContext
                     (new TemplateOptions("template") {
                         FileNamePostfix = ".ttl",
                         RootPath = @"TestTemplates",
                         AllowCSharp = true
                     }));
            var testString = test.Generate(DataFiller.FillData());
            File.WriteAllText("test.html", testString);
            test.Dispose();
            /*END JIT*/
        }

        public void Run()
        {
            var list = new SmartList<TestDataStructure>();
            Console.WriteLine("Enter tries count (template generate):");
            // = 1000;
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);
            for (int i = 0; i < n; i++)
                list.Add(DataFiller.FillData());
            var watcher = new Stopwatch();
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
            Console.WriteLine("Compile time: {0}", watcher.Elapsed);
            watcher.Reset();
            watcher.Start();
            long length = list.AsParallel().Sum(item => (long)target.Generate(item).Length);
            watcher.Stop();
            Console.WriteLine("Parrallel implementation:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            watcher.Reset();
            watcher.Start();
            length = list.Sum(item => (long)target.Generate(item).Length);
            watcher.Stop();
            target.Dispose();
            Console.WriteLine("Syncronous implementation:");
            Console.WriteLine("{0} run times: {1}", n, watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
        }
    }
}
