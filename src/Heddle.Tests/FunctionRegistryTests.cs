using System;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <see cref="FunctionRegistry"/> semantics (D12): overload ranking (exact/widening/object), the
    /// params-expanded composite <c>format</c> (incl. HED1015), replace-on-exact-signature, freeze-then-throw,
    /// delegate closures, and <see cref="FunctionRegistry.Default"/> immutability.
    /// </summary>
    public class FunctionRegistryTests
    {
        public class IntModel { public int N { get; set; } }
        public class TwoIntModel { public int X { get; set; } public int Y { get; set; } }

        public static string DescribeInt(int x) => "int";
        public static string DescribeObject(object x) => "obj";
        public static int TwiceInt(int x) => x * 2;
        public static long Wide(long x) => x + 1;

        private static MethodInfo M(string name) =>
            typeof(FunctionRegistryTests).GetMethod(name, BindingFlags.Public | BindingFlags.Static);

        private static string Render(string template, FunctionRegistry registry, object model)
        {
            HeddleTemplate.Configure(typeof(FunctionRegistryTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { Functions = registry };
            var t = new HeddleTemplate(template, new CompileContext(options, model.GetType()));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Fact]
        public void NewRegistryContainsBuiltIns()
        {
            var registry = new FunctionRegistry();
            Assert.True(registry.Contains("upper"));
            Assert.True(FunctionRegistry.Default.Contains("upper"));
        }

        [Fact]
        public void ExactOverloadBeatsObjectOverload()
        {
            var registry = new FunctionRegistry();
            registry.Register("describe", M(nameof(DescribeObject)));
            registry.Register("describe", M(nameof(DescribeInt)));
            Assert.Equal("int", Render("@(describe(N))", registry, new IntModel { N = 5 }));
        }

        [Fact]
        public void WideningOverloadBindsWhenNoExactMatch()
        {
            var registry = new FunctionRegistry();
            registry.Register("wide", M(nameof(Wide)));
            Assert.Equal("6", Render("@(wide(N))", registry, new IntModel { N = 5 }));
        }

        [Fact]
        public void ReplaceOnExactSignature()
        {
            var registry = new FunctionRegistry();
            registry.Register("describe", M(nameof(DescribeInt)));
            registry.Register("describe", (Func<int, string>)(x => "replaced"));
            Assert.Equal("replaced", Render("@(describe(N))", registry, new IntModel { N = 1 }));
        }

        [Fact]
        public void DelegateClosureIsInvoked()
        {
            int factor = 3;
            var registry = new FunctionRegistry();
            registry.Register("scale", (Func<int, int>)(x => x * factor));
            Assert.Equal("12", Render("@(scale(N))", registry, new IntModel { N = 4 }));
        }

        [Fact]
        public void CompositeFormatRendersInvariant()
        {
            var registry = new FunctionRegistry();
            Assert.Equal("3 and 7", Render("@(format(\"{0} and {1}\", X, Y))", registry, new TwoIntModel { X = 3, Y = 7 }));
        }

        [Fact]
        public void CompositeFormatArgumentShortfallIsHed1015()
        {
            HeddleTemplate.Configure(typeof(FunctionRegistryTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate("@(format(\"{0}{1}\", X))",
                new CompileContext(new TemplateOptions(), typeof(TwoIntModel)));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.FormatArgumentCountMismatch);
        }

        [Fact]
        public void NoOverloadIsHed1012WithCandidateList()
        {
            HeddleTemplate.Configure(typeof(FunctionRegistryTests).GetTypeInfo().Assembly);
            // min has no (string, int) overload.
            var t = new HeddleTemplate("@(min(Name, 2))",
                new CompileContext(new TemplateOptions(), typeof(NameModel)));
            Assert.False(t.CompileResult.Success);
            var error = t.CompileResult.Errors.Single(e => e.DiagnosticId == HeddleDiagnosticIds.NoFunctionOverload);
            Assert.Contains("min(", error.Error);
        }

        public class NameModel { public string Name { get; set; } }

        [Fact]
        public void RegisterAfterFreezeThrows()
        {
            var registry = new FunctionRegistry();
            registry.Register("f", (Func<int, int>)(x => x));
            var t = new HeddleTemplate("@(f(N))",
                new CompileContext(new TemplateOptions { Functions = registry }, typeof(IntModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.True(registry.IsFrozen);
            Assert.Throws<InvalidOperationException>(() => registry.Register("g", (Func<int, int>)(x => x)));
        }

        [Fact]
        public void DefaultRegistryIsFrozenAndImmutable()
        {
            Assert.True(FunctionRegistry.Default.IsFrozen);
            Assert.Throws<InvalidOperationException>(() => FunctionRegistry.Default.Register("x", (Func<int>)(() => 1)));
        }

        [Fact]
        public void InstanceMethodRegistrationThrows()
        {
            var registry = new FunctionRegistry();
            var instanceMethod = typeof(FunctionRegistryTests).GetMethod(nameof(InstanceHelper),
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Throws<ArgumentException>(() => registry.Register("bad", instanceMethod));
        }

        public string InstanceHelper(int x) => x.ToString();
    }
}
