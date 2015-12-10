using Templates.Performance.Runners;
using System;
using Templates.Collections;

namespace Templates.Performance
{
    public class Program
    {
        private static readonly SmartList<IRunner> Tests = new SmartList<IRunner>();

        public static void SetUpTests() {
            Tests.Add(new TemplaterTest());
            Tests.Add(new TemplaterStrings());
        }
        public void Main(string[] args)
        {
            SetUpTests();
            //Run all tests
            foreach (var test in Tests) {
                test.Run();
            }
            Console.WriteLine("Done all, press any key to exit...");
#if !DNXCORE50
            Console.ReadKey();
#else
            Console.ReadLine();
#endif
        }

    }
}