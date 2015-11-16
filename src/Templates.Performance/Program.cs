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
#if !DOTNET5_4 && !DNX451
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
#if !DOTNET5_4
            Console.ReadKey();
#else
            Console.ReadLine();
#endif
        }
#endif

    }
}
