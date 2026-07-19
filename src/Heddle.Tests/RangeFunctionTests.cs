using System;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The <c>range</c> step-validation split (phase 4 D2/D3): a statically-visible non-positive literal step
    /// is a positioned HED4001 compile error (literal and sign-prefixed literal); a model-driven non-positive
    /// step throws <see cref="TemplateProcessingException"/> at render with the same message; a host-replaced
    /// <c>range</c> governs its own step rules and never triggers the built-in static check (R04).
    /// </summary>
    public class RangeFunctionTests
    {
        public class StepModel { public int Step { get; set; } }

        public static Heddle.Models.Range HostRange(int start, int last, int step) =>
            new Heddle.Models.Range(start, last, 1);

        private const string StepZeroMessage =
            "Function 'range' requires a positive step, but 0 was supplied — a zero or negative step never terminates the loop.";

        private static HeddleCompileResult Compile(string template, ExType modelType, TemplateOptions options = null)
        {
            HeddleTemplate.Configure(typeof(RangeFunctionTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template, new CompileContext(options ?? new TemplateOptions(), modelType));
            return t.CompileResult;
        }

        [Fact] // R01
        public void R01_LiteralZeroStepIsPositionedHed4001()
        {
            var result = Compile("@for(range(1, 5, 0)){{x}}", typeof(object));
            Assert.False(result.Success);
            var error = result.Errors.FirstOrDefault(e => e.DiagnosticId == HeddleDiagnosticIds.RangeStepNotPositive);
            Assert.True(error != null, result.ToString());
            Assert.Equal(StepZeroMessage, error.Error);
            Assert.True(error.Position.Length > 0, "HED4001 must point at the step argument");
        }

        [Fact] // R02
        public void R02_SignPrefixedNegativeLiteralStepIsHed4001()
        {
            var result = Compile("@for(range(5, 1, -1)){{x}}", typeof(object));
            Assert.False(result.Success);
            var error = result.Errors.FirstOrDefault(e => e.DiagnosticId == HeddleDiagnosticIds.RangeStepNotPositive);
            Assert.True(error != null, result.ToString());
            Assert.Equal(
                "Function 'range' requires a positive step, but -1 was supplied — a zero or negative step never terminates the loop.",
                error.Error);
            Assert.True(error.Position.Length > 0);
        }

        [Fact] // R03
        public void R03_ModelDrivenZeroStepThrowsAtRenderWithHed4001Text()
        {
            HeddleTemplate.Configure(typeof(RangeFunctionTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate("@for(range(1, 5, Step)){{x}}",
                new CompileContext(new TemplateOptions(), typeof(StepModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var ex = Assert.Throws<TemplateProcessingException>(() => t.Generate(new StepModel { Step = 0 }));
            Assert.Equal(StepZeroMessage, ex.Message);
        }

        [Fact] // R04
        public void R04_HostReplacedRangeExemptFromBuiltInStaticCheck()
        {
            var registry = new FunctionRegistry();
            registry.Register("range", typeof(RangeFunctionTests).GetMethod(nameof(HostRange)));
            var options = new TemplateOptions { Functions = registry };

            var result = Compile("@for(range(2, 4, 0)){{x}}", typeof(object), options);
            Assert.True(result.Success, result.ToString());
            Assert.DoesNotContain(result.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.RangeStepNotPositive);
        }

        [Fact] // range() in the default registry — two- and three-arg overloads produce Heddle.Models.Range
        public void RangeJoinsDefaultRegistryWithBothOverloads()
        {
            Assert.True(FunctionRegistry.Default.Contains("range"));
            var overloads = typeof(FunctionRegistry)
                .GetMethod("GetOverloads", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(FunctionRegistry.Default, new object[] { "range" }) as System.Collections.IEnumerable;
            Assert.Equal(2, overloads.Cast<object>().Count());
        }
    }
}
