using System;
using System.IO;
using Templates;
using Templates.Data;
using Templates.Runtime;

namespace PerfTesting.Runners {
    public class TemplaterMemory : IRunner {
        public void Run()
        {
            Console.WriteLine("Memory Leaks Test");
            var reader = File.OpenText(@"D:\Tmp\template.html");
            string document = reader.ReadToEnd();
            reader.Close();
            var test = new TtlTemplate
                    (new CompileContext
                         (new TemplateOptions {
                             TemplateName = "template",
                             FileNamePostfix = ".ttl",
                             RootPath = @"g:\Work\Templater\performance\PerfTesting\TestTemplates",
                             AllowCSharp = true
                         }));
            var testItem = DataFiller.FillData();
            int i = 0;
            while (i < 1000000) {
                test.Generate(testItem);
                var writer = File.CreateText(@"D:\Tmp\template.html");
                writer.Write(document);
                writer.Close();
                i++;
            }
            test.Dispose();
        }
    }
}
