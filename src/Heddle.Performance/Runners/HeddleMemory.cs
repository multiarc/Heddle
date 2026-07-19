using System;
using System.IO;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners {
    public class HeddleMemory {

        public void Run()
        {
            Console.WriteLine("Memory Leaks Test");
            var reader = File.OpenText(@"test.html");
            string document = reader.ReadToEnd();
            reader.Dispose();
            var test = new HeddleTemplate
                    (new CompileContext
                         (new TemplateOptions("home") {
                             FileNamePostfix = ".heddle",
                             RootPath = @"TestTemplates",
                             ExpressionMode = ExpressionMode.FullCSharp
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
