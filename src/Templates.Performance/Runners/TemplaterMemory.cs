using System;
using System.IO;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Performance.Runners {
    public class TemplaterMemory {

        public void Run()
        {
            Console.WriteLine("Memory Leaks Test");
            var reader = File.OpenText(@"test.html");
            string document = reader.ReadToEnd();
            reader.Dispose();
            var test = new TtlTemplate
                    (new CompileContext
                         (new TemplateOptions("home") {
                             FileNamePostfix = ".ttl",
                             RootPath = @"TestTemplates",
                             AllowCSharp = true
                         }));
            if (!test.CompileResult.Success) {
                Console.Write(test.CompileResult.ToString());
                return;
            }
            int i = 0;
            while (i < 1000000) {
                test.Generate(null);
                var writer = File.CreateText(@"test.html");
                writer.Write(document);
                writer.Dispose();
                i++;
            }
            test.Dispose();
        }
    }
}
