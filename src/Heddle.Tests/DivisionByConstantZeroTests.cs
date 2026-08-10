using System;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The engine half of the same-fact-same-id rule: an integral or <c>decimal</c> divide/modulo over CONSTANT operands with a
    /// zero divisor is a positioned <c>HED1018</c> compile error — rendering it could only ever throw, so the
    /// failure moves to where C# puts <c>CS0020</c>. The boundaries are as much the test as the rule: floating
    /// point folds to Infinity and keeps rendering, and a RUNTIME divisor that happens to be zero still throws
    /// at render, because both are exactly where C# draws its own lines. Nothing pinned the old render-time
    /// throw — this suite is the first to state the engine's divide-by-zero behaviour at all.
    /// </summary>
    public class DivisionByConstantZeroTests
    {
        public class Model
        {
            public int Count { get; set; }
        }

        private static HeddleTemplate Compile(string template) =>
            new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(Model)));

        [Theory]
        [InlineData("@(1/0)")]
        [InlineData("@(1%0)")]
        [InlineData("@(1/(1-1))")]
        [InlineData("@(1L/0)")]
        [InlineData("@(1.0m/0m)")]
        // A zero reached through operators the fold has to evaluate, not just a spelled literal.
        [InlineData("@(1/(1&0))")]
        [InlineData("@((2>>1)/0)")]
        [InlineData("@((true?1:1)/0)")]
        // A constant division INSIDE a non-constant expression still errors: the rule keys on the division's
        // own operands, not on the whole template expression.
        [InlineData("@(Count + 1/0)")]
        public void AConstantZeroDivisorIsAPositionedCompileError(string template)
        {
            var result = Compile(template).CompileResult;
            Assert.False(result.Success, "expected a compile error");
            var error = Assert.Single(result.Errors);
            Assert.Equal(HeddleDiagnosticIds.DivisionByConstantZero, error.DiagnosticId);
            Assert.True(error.Position.Length > 0, "diagnostic must carry a non-empty position");
        }

        /// <summary>The over-reach guard: <c>1.0/0</c> is a legal constant yielding Infinity in C# and in the
        /// engine alike, and it must keep compiling and rendering.</summary>
        [Theory]
        [InlineData("@(1.0/0)")]
        [InlineData("@(1/0.0)")]
        [InlineData("@(1.0%0)")]
        public void FloatingPointDivisionByZeroStillCompilesAndRenders(string template)
        {
            var t = Compile(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.False(string.IsNullOrEmpty(t.Generate(new Model())));
        }

        /// <summary>A runtime divisor that happens to be zero is unknowable at compile time; the template
        /// compiles and the render throws, exactly as C# compiles <c>x/0</c>'s runtime twin (verified against
        /// Roslyn: no CS0020 without a constant expression).</summary>
        [Fact]
        public void ARuntimeZeroDivisorStillCompilesAndThrowsAtRender()
        {
            var t = Compile("@(1/Count)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Throws<DivideByZeroException>(() => t.Generate(new Model { Count = 0 }));
            Assert.Equal("1", t.Generate(new Model { Count = 1 }));
        }
    }
}
