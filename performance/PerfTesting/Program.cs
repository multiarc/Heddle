using System;
using PerfTesting.Runners;
using Templates.Collections;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using System.Collections.Generic;

namespace PerfTesting {
    public class GetValueClass {
        private static CallSite<Func<CallSite, object, object>> _callSite;

        public object GetValueActual(object obj) {
            if (_callSite == null) {
                CSharpArgumentInfo[] csharpArgumentInfoArray = new CSharpArgumentInfo[1];
                csharpArgumentInfoArray[0] = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null);
                _callSite = CallSite<Func<CallSite, object, object>>.Create(Binder.GetMember(CSharpBinderFlags.None, "Value", typeof(GetValueClass), csharpArgumentInfoArray));
            }
            return _callSite.Target(_callSite, obj);
        }
    }

    internal class Program {

        private static readonly SmartList<IRunner> tests = new SmartList<IRunner>();

        public static void SetUpTests() {
            //tests.Add(new HtmlEncodeTest());
            //tests.Add(new TemplaterStrings());
            tests.Add(new TemplaterTest());
        }

        private static void Main(string[] args) {
            SetUpTests();
            //Run all tests
            foreach (var test in tests) {
                test.Run();
            }
            Console.WriteLine("Done all, press any key to exit...");
            Console.ReadKey();
        }
    }
}