using Templates.Performance.Runners;
using System;
using Templates.Collections;

namespace Templates.Performance
{
    public class Program
    {
        private static readonly SmartList<IRunner> Tests = new SmartList<IRunner>();

        public static void SetUpTests() {
            //tests.Add(new HtmlEncodeTest());
            //tests.Add(new TemplaterStrings());
            //Tests.Add(new TemplaterTest());
            Tests.Add(new TemplaterStrings());
        }
#if !DNXCORE50 && !DNX451
        public static void Main(string[] args) {

        }
#else
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
#endif

    }
}
