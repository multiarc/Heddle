using System;
using System.Diagnostics;
using System.Net;
using Templates.Data;
using Templates.Runtime;
using Templates.Strings.Web;

namespace Templates.Performance.Runners {
    public class HtmlEncodeTest : IRunner {

        public void Run()
        {
            var watcher = new ExecutionStopwatch();
            watcher.Reset();
            Console.WriteLine("Starting HTML Encode tests");
            Console.WriteLine("Enter tries count (html encode cycles):");
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);
            long length = 0;
            watcher.Start();
            for (int i = 0; i < n; i++)
            {
                length += WebUtility.HtmlEncode("<123456>").Length;
            }
            watcher.Stop();
            Console.WriteLine("System.Net.WebUtility.HtmlEncode(small string) Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));

            watcher.Reset();
            length = 0;
            watcher.Start();
            for (int i = 0; i < n; i++) {
                length += HtmlEncode.EncodeAlt("<123456>").Length;
            }
            watcher.Stop();
            Console.WriteLine("Templates.Strings.Web.HtmlEncode.Encode(small string) Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            Console.WriteLine("=======================================================");
            Console.WriteLine("Prepearing bigger string tests");
            var test = new TtlTemplate
                    (new CompileContext
                         (new TemplateOptions("template") {
                             FileNamePostfix = ".ttl",
                             RootPath = @"TestTemplates",
                             AllowCSharp = true
                         }));
            if (!test.CompileResult.Success) {
                Console.Write(test.CompileResult.ToString());
                return;
            }
            var testItem = DataFiller.FillData();
            var testBigString = test.Generate(testItem);
            test.Dispose();
            watcher.Reset();
            length = 0;
            watcher.Start();
            for (int i = 0; i < n; i++) {
                length += WebUtility.HtmlEncode(testBigString).Length;
            }
            watcher.Stop();
            Console.WriteLine("System.Net.WebUtility.HtmlEncode(small string) Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));

            watcher.Reset();
            length = 0;
            watcher.Start();
            for (int i = 0; i < n; i++) {
                length += HtmlEncode.EncodeAlt(testBigString).Length;
            }
            watcher.Stop();
            Console.WriteLine("Templates.Strings.Web.HtmlEncode.Encode(small string) Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
        }
    }
}
