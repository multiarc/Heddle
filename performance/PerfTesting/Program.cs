using System;
using PerfTesting.Runners;
using Templates.Collections;

namespace PerfTesting {
    internal class Program
    {

        private static readonly SmartList<IRunner> tests = new SmartList<IRunner>();

        public static void SetUpTests()
        {
            //tests.Add(new HtmlEncodeTest());
            tests.Add(new TemplaterStrings());
            //tests.Add(new TemplaterTest());
        }

        private static void Main (string[] args)
        {
            SetUpTests();
            //Run all tests
            foreach (var test in tests)
            {
                test.Run();
            }
            Console.WriteLine("Done all, press any key to exit...");
            Console.ReadKey();
        }
    }
}