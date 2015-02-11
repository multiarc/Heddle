using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates;
using Templates.Collections;
using Templates.Data;
using Templates.Runtime;
using Templates.Strings;
using Templates.Strings.Core;

namespace PerfTesting.Runners {
    public class TemplaterStrings :IRunner {
        public void Run()
        {
            Console.WriteLine("Prepearing string tests");
            var watcher = new Stopwatch();
            watcher.Reset();
            var target = new TtlTemplate
                (new DocumentContext
                     (new TemplateOptions {
                         TemplateName = "template",
                         FileNamePostfix = ".ttl",
                         RootPath = @"g:\Work\Templater\performance\PerfTesting\TestTemplates"
                     }));
            var testFastList = new SmartList<ExString>();
            var list = new SmartList<TestDataStructure>();
            Console.WriteLine("Enter tries count (strings test):");
            string quantity = Console.ReadLine();
            int n;
            int.TryParse(quantity, out n);
            for (int i = 0; i < n; i++)
                list.Add(DataFiller.FillData());
            foreach (TestDataStructure item in list)
            {
                testFastList.Add(target.GenerateString(item));
            }

            SmartList<string> testStringList = testFastList.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToSmartArray();
            SmartList<StringBuilder> testBuilderList = testStringList.Select(i => new StringBuilder(i)).ToSmartArray();

            Console.WriteLine("Starting Replace Test");

            ExString toFindF = "a";
            ExString toReplaceF = "<th>";
            toReplaceF.Replace("th", "b"); //JIT
            watcher.Start();

            var length = testFastList.Sum(fastString => fastString.Replace(toFindF, toReplaceF).Length);
            watcher.Stop();
            Console.WriteLine("ExString.Replace Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            watcher.Reset();

            const string toFindS = "a";
            const string toReplaceS = "<th>";
            toReplaceS.Replace("th", "b"); //JIT
            length = 0;
            watcher.Start();
            foreach (string str in testStringList)
                length += str.Replace(toFindS, toReplaceS).Length;
            watcher.Stop();
            Console.WriteLine("String.Replace Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof(char));
            watcher.Reset();

            length = 0;
            watcher.Start();
            foreach (StringBuilder str in testBuilderList)
                length += str.Replace(toFindS, toReplaceS).Length;
            watcher.Stop();
            Console.WriteLine("StringBuilder.Replace Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            watcher.Reset();

            Console.WriteLine("Starting Concat Test");
            ExString resultF = new ExString("xxxx") + new ExString("ffff"); //JIT
            resultF = ExString.Empty;
            watcher.Start();
            for (int i = 0; i < testFastList.Length; i++) {
                resultF += testFastList[i];
                testFastList[i] = null;
            }
            watcher.Stop();
            Console.WriteLine("ExString.Concat Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (resultF.Length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            watcher.Reset();
            resultF = null;

            watcher.Start();
            string resultS = testStringList.Aggregate(string.Empty, (current, t) => current + t);
            watcher.Stop();
            Console.WriteLine("String.Concat Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (resultS.Length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            watcher.Reset();

            Console.WriteLine("Prepearing append test");

            for (int i = 0; i < testBuilderList.Length; i++) {
                testFastList[i] = testBuilderList[i].ToString();
                testStringList[i] = testBuilderList[i].ToString();
                testBuilderList[i] = null;
            }
            Console.WriteLine("Starting append test");

            var testBuilder = new StringBuilder();
            var testFastBuilder = new ExStringBuilder();

            testFastBuilder.Append(new ExString("xxx")); //JIT
            testFastBuilder.ToExString(); //JIT

            testFastBuilder.Clear();
            watcher.Start();
            foreach (ExString t in testFastList) {
                testFastBuilder.Append(t);
            }
            length = testFastBuilder.ToExString().Length;
            watcher.Stop();

            Console.WriteLine("ExStringBuilder.Append Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", testFastBuilder.ToExString().Length / 1048576.0 * sizeof(char));
            //testFastBuilder = null;
            watcher.Reset();
            watcher.Start();
            testBuilder.Append("xxx"); //JIT
            testBuilder.ToString(); //JIT

            testBuilder.Clear();
            foreach (string t in testStringList) {
                testBuilder.Append(t);
            }
            length = testBuilder.ToString().Length;
            watcher.Stop();

            Console.WriteLine("StringBuilder.Append Run time: {0}", watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof(char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", testBuilder.Length / 1048576.0 * sizeof(char));

            testBuilder = null;

            Console.WriteLine("Done");
        }
    }
}
