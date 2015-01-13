using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Strings.Core;
using Westwind.RazorHosting;

namespace RazorTemplateTest {
    class Program {
        private static readonly object Lock = new object();

        private static long _lengthGenerated;
        private static int _countGenerated;
        private static int _n;
        private static Stopwatch _watcher;

        static void Main(string[] args)
        {
            var hostContainer = new RazorFolderHostContainer();
            //var engine = new RazorEngine<RazorTemplateBase>
            //{
            //    Configuration = {CompileToMemory = false},
            //    HostContainer = hostContainer,
            //    TemplatePerRequestConfigurationData = new RazorFolderHostTemplateConfiguration()
            //    {
                    
            //    }
            //};
            hostContainer.TemplatePath = @"g:\Work\Templater\RazorTemplateTest\TestTemplates\";
            var template = File.ReadAllText(@"G:\Work\Templater\RazorTemplateTest\TestTemplates\template.cshtml");
            var list = new SmartList<TestDataStructure>();
            Console.WriteLine("Enter tries count:");
            string quantity = Console.ReadLine();
            int.TryParse(quantity, out _n);
            for (int i = 0; i < _n; i++)
                list.Add(DataFiller.FillData());
            _watcher = new Stopwatch();
            hostContainer.AddAssemblyFromType(typeof(TestDataStructure));
            hostContainer.Start();
            _watcher.Start();
            //var assemblyId = engine.CompileTemplate(template);
            var test = hostContainer.RenderTemplate("template.cshtml", list.First());
            File.WriteAllText("test.html", test);
            _watcher.Stop();
            Console.WriteLine("Compile time: {0}", _watcher.Elapsed);
            _watcher.Reset();
            _lengthGenerated = 0;
            _watcher.Start();
            long length = list.Sum(item => (long)hostContainer.RenderTemplate("template.cshtml", item).Length);
            _watcher.Stop();
            hostContainer.Stop();
            Console.WriteLine("{0} run times: {1}", _n, _watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / _watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            Console.ReadKey();
        }
    }
}
