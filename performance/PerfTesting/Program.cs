using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Templates;
using Templates.Data;
using Templates.Runtime;
using Templates.Strings.Core;

namespace PerfTesting {
    internal class Program {
        private static readonly object Lock = new object();

        private static long _lengthGenerated;
        private static int _countGenerated;
        private static int _n;
        private static Stopwatch _watcher;

        private static void ResultsReturned (string document, Exception e)
        {
            if (e == null) {
                _lengthGenerated += document.Length;
                lock (Lock) {
                    _countGenerated++;
                    if (_countGenerated == _n) {
                        _watcher.Stop();
                        Console.WriteLine("{0} run times: {1}", _n, _watcher.Elapsed);
                        Console.WriteLine
                            ("Out Speed: {0} Mb/s", (_lengthGenerated / _watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
                        Console.WriteLine("Total Size: {0} Mb", _lengthGenerated / 1048576.0 * sizeof (char));
                    }
                }
            } else
                throw new Exception("Async Operation Completed with errors", e);
        }

        private static void Main (string[] args)
        {
            var list = new SmartList<TestDataStructure>();
            Console.WriteLine("Enter tries count:");
            // = 1000;
            string quantity = Console.ReadLine();
            int.TryParse(quantity, out _n);
            for (int i = 0; i < _n; i++)
                list.Add(DataFiller.FillData());
            _watcher = new Stopwatch();
            /*JIT*/
            var test = new TtlTemplate
                (new CompileContext
                     (new TemplateOptions
                     {
                         TemplateName = "template",
                         FileNamePostfix = ".ttl",
                         RootPath = @"G:\Work\Templater\PerfTesting\TestTemplates"
                     }));
            var testString = test.GenerateString(list.FirstOrDefault());
            File.WriteAllText("test.html", testString);
            test.Dispose();
            /*END JIT*/
            _watcher.Start();
            var target = new TtlTemplate
                (new CompileContext
                     (new TemplateOptions
                     {
                         TemplateName = "template",
                         FileNamePostfix = ".ttl",
                         RootPath = @"G:\Work\Templater\PerfTesting\TestTemplates"
                     }));
            _watcher.Stop();
            Console.WriteLine("Compile time: {0}", _watcher.Elapsed);
            _watcher.Reset();
            _lengthGenerated = 0;
            _watcher.Start();
            long length = list.Sum(item => (long) target.GenerateString(item).Length);
            _watcher.Stop();
            test.Dispose();
            Console.WriteLine("{0} run times: {1}", _n, _watcher.Elapsed);
            Console.WriteLine("Out Speed: {0} Mb/s", (length / _watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof (char));
            //Console.WriteLine("Memory Leaks Test");
            //var reader = File.OpenText(@"D:\Tmp\template.html");
            //string document = reader.ReadToEnd();
            //reader.Close();
            //test = new Template
            //        (new CompileContext
            //             (new TemplateOptions
            //             {
            //                 TemplateName = "template",
            //                 FileNamePostfix = ".html",
            //                 RootPath = @"D:\Tmp"
            //             }));
            //var testItem = list.First();
            //while (true)
            //{
            //    test.GenerateString(testItem);
            //    var writer = File.CreateText(@"D:\Tmp\template.html");
            //    writer.Write(document);
            //    writer.Close();
            //}
            //Console.WriteLine("Prepearing string tests");
            //watcher.Reset();

            //var testFastList = new SmartArrayCollection<FastString>();

            //foreach (TestDataStructure item in list)
            //    testFastList.Add(target.GenerateString(item));

            //SmartArrayCollection<string> testStringList = testFastList.Select(i => i.ToString()).ToSmartArray();
            //SmartArrayCollection<StringBuilder> testBuilderList = testStringList.Select(i => new StringBuilder(i)).ToSmartArray();

            //Console.WriteLine("Starting Replace Test");

            //FastString toFindF = "a";
            //FastString toReplaceF = "<th>";
            //toReplaceF.Replace("th", "b"); //JIT
            //length = 0;
            //watcher.Start();

            //foreach (FastString fastString in testFastList)
            //    length += fastString.Replace(toFindF, toReplaceF).Length;
            //watcher.Stop();
            //Console.WriteLine("FastString.Replace Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof (char));
            //watcher.Reset();

            //const string toFindS = "a";
            //const string toReplaceS = "<th>";
            //toReplaceS.Replace("th", "b"); //JIT
            //length = 0;
            //watcher.Start();
            //foreach (string str in testStringList)
            //    length += str.Replace(toFindS, toReplaceS).Length;
            //watcher.Stop();
            //Console.WriteLine("String.Replace Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //Console.WriteLine("Total Size: {0} Mb", length / 1048576.0 * sizeof (char));
            //watcher.Reset();

            //length = 0;
            //watcher.Start();
            //foreach (StringBuilder str in testBuilderList)
            //    length += str.Replace(toFindS, toReplaceS).Length;
            //watcher.Stop();
            //Console.WriteLine("StringBuilder.Replace Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //watcher.Reset();

            //Console.WriteLine("Starting Concat Test");
            //FastString resultF = new FastString("xxxx") + new FastString("ffff"); //JIT
            //resultF = FastString.EmptyFast;
            //watcher.Start();
            //for (int i = 0; i < testFastList.Length; i++) {
            //    resultF += testFastList[i];
            //    testFastList[i] = null;
            //}
            //watcher.Stop();
            //Console.WriteLine("FastString.Concat Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (resultF.Length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //watcher.Reset();
            //resultF = null;

            //watcher.Start();
            //string resultS = testStringList.Aggregate(string.Empty, (current, t) => current + t);
            //watcher.Stop();
            //Console.WriteLine("String.Concat Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (resultS.Length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //watcher.Reset();

            //Console.WriteLine("Prepearing append test");

            //for (int i = 0; i < testBuilderList.Length; i++) {
            //    testFastList[i] = testBuilderList[i].ToString();
            //    testStringList[i] = testBuilderList[i].ToString();
            //    testBuilderList[i] = null;
            //}

            //testBuilderList = null;

            //Console.WriteLine("Starting append test");

            //var testBuilder = new StringBuilder();
            //var testFastBuilder = new FastStringBuilder();

            //testFastBuilder.Append(new FastString("xxx")); //JIT
            //testFastBuilder.ToFastString(); //JIT

            //testFastBuilder.Clear();
            //watcher.Start();
            //foreach (FastString t in testFastList) {
            //    testFastBuilder.Append(t);
            //}
            //length = testFastBuilder.ToFastString().Length;
            //watcher.Stop();

            //Console.WriteLine("FastStringBuilder.Append Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //Console.WriteLine("Total Size: {0} Mb", testFastBuilder.ToFastString().Length / 1048576.0 * sizeof (char));
            ////testFastBuilder = null;
            //watcher.Reset();
            //watcher.Start();
            //testBuilder.Append("xxx"); //JIT
            //testBuilder.ToString(); //JIT

            //testBuilder.Clear();
            //foreach (string t in testStringList) {
            //    testBuilder.Append(t);
            //}
            //length = testBuilder.ToString().Length;
            //watcher.Stop();

            //Console.WriteLine("StringBuilder.Append Run time: {0}", watcher.Elapsed);
            //Console.WriteLine("Out Speed: {0} Mb/s", (length / watcher.Elapsed.TotalSeconds / 1048576.0 * sizeof (char)).ToString("F"));
            //Console.WriteLine("Total Size: {0} Mb", testBuilder.Length / 1048576.0 * sizeof (char));

            //testBuilder = null;

            //Console.WriteLine("Done");
            Console.ReadKey();
        }

        //FastString toFindF = ".00</td>";
        //FastString toReplaceF = "<th>";

        //var reader = File.OpenText("xxx - error.txt");
        //FastString work = reader.ReadToEnd();
        //int len = 0;
        //while (true)
        //{
        //    FastString result = work.Replace(toFindF, toReplaceF);
        //    unchecked
        //    {
        //        len += result.Length;
        //    }
        //    work = (FastString)work.Clone();
        //}
    }
}