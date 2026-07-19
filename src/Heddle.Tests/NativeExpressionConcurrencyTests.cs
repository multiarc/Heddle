using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Concurrency isolation: parallel renders of one compiled template with opposite conditions, and parallel
    /// compiles sharing one frozen <see cref="FunctionRegistry"/> and one options object. Plus the standalone
    /// name-resolution rules (extension-wins shadowing warning HED1016; empty-argument function call).
    /// </summary>
    public class NativeExpressionConcurrencyTests
    {
        public class Model { public int X { get; set; } }
        public class TwoModel { public int A { get; set; } public int B { get; set; } }
        public class CountModel { public int Count { get; set; } }

        public static int Now() => 42;

        [Fact]
        public void ParallelRendersOfOppositeConditionsAreIsolated()
        {
            HeddleTemplate.Configure(typeof(NativeExpressionConcurrencyTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { Functions = new FunctionRegistry() };
            var t = new HeddleTemplate("@if(X > 0){{positive}}@if(X < 0){{negative}}",
                new CompileContext(options, typeof(Model)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            Parallel.For(0, 2000, i =>
            {
                bool positive = i % 2 == 0;
                var result = t.Generate(new Model { X = positive ? 1 : -1 });
                Assert.Equal(positive ? "positive" : "negative", result);
            });
        }

        [Fact]
        public void ParallelCompilesShareOneFrozenRegistry()
        {
            HeddleTemplate.Configure(typeof(NativeExpressionConcurrencyTests).GetTypeInfo().Assembly);
            var registry = new FunctionRegistry();
            var options = new TemplateOptions { Functions = registry };

            // Freeze the registry through one compile, then hammer it concurrently.
            var warmup = new HeddleTemplate("@(max(A, B))", new CompileContext(options, typeof(TwoModel)));
            Assert.True(warmup.CompileResult.Success, warmup.CompileResult.ToString());
            Assert.True(registry.IsFrozen);

            Parallel.For(0, 500, i =>
            {
                var t = new HeddleTemplate("@(max(A, B))", new CompileContext(options, typeof(TwoModel)));
                Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
                Assert.Equal("7", t.Generate(new TwoModel { A = 3, B = 7 }));
            });
        }

        [Fact]
        public void ExtensionWinsOverShadowingFunctionWithWarning()
        {
            HeddleTemplate.Configure(typeof(NativeExpressionConcurrencyTests).GetTypeInfo().Assembly);
            var registry = new FunctionRegistry();
            registry.Register("if", (Func<bool, string>)(b => "function"));
            var options = new TemplateOptions { Functions = registry };

            var t = new HeddleTemplate("@if(Count > 0){{extension}}", new CompileContext(options, typeof(CountModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.FunctionShadowedByExtension);
            Assert.Equal("extension", t.Generate(new CountModel { Count = 1 }));
        }

        [Fact]
        public void StandaloneFunctionWithEmptyArguments()
        {
            HeddleTemplate.Configure(typeof(NativeExpressionConcurrencyTests).GetTypeInfo().Assembly);
            var registry = new FunctionRegistry();
            registry.Register("now", (Func<int>)Now);
            var options = new TemplateOptions { Functions = registry };

            var t = new HeddleTemplate("@now()", new CompileContext(options, typeof(Model)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("42", t.Generate(new Model()));
        }
    }
}
