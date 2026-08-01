using System.Linq;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <c>round</c>, <c>floor</c> and <c>ceil</c> carry the same four numeric tiers <c>abs</c>, <c>min</c> and
    /// <c>max</c> carry. Without the integral tiers an <c>int</c> argument is ambiguous rather than wrong:
    /// <c>int→double</c> and <c>int→decimal</c> are both widening and neither is closer, which is the same
    /// reason <c>Math.Floor(3)</c> is <c>CS0121</c> in C#. The fix is the overload set, not the ranker.
    /// </summary>
    public class IntegralRoundingBuiltInTests
    {
        public sealed class NumbersModel
        {
            public int I { get; set; }
            public long L { get; set; }
            public double D { get; set; }
            public decimal M { get; set; }
        }

        private static HeddleCompileResult Compile(string expression)
        {
            var template = new HeddleTemplate("@(" + expression + ")",
                new CompileContext(new TemplateOptions(), typeof(NumbersModel)));
            return template.CompileResult;
        }

        private static string Render(string expression, NumbersModel model)
        {
            var template = new HeddleTemplate("@(" + expression + ")",
                new CompileContext(new TemplateOptions(), typeof(NumbersModel)));
            return template.Generate(model);
        }

        [Theory]
        // literals
        [InlineData("floor(3)")]
        [InlineData("ceil(3)")]
        [InlineData("round(3)")]
        [InlineData("round(3, 2)")]
        // an int-typed member, which is the shape a template actually carries
        [InlineData("floor(I)")]
        [InlineData("ceil(I)")]
        [InlineData("round(I)")]
        [InlineData("round(I, 2)")]
        // the long tier
        [InlineData("floor(L)")]
        [InlineData("ceil(L)")]
        [InlineData("round(L)")]
        [InlineData("round(L, 2)")]
        public void AnIntegralArgumentBinds(string expression)
        {
            var result = Compile(expression);
            Assert.True(result.Success,
                expression + " => " + string.Join("; ", result.ErrorList.Select(e => e.ToString())));
            Assert.DoesNotContain(result.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);
        }

        [Theory]
        [InlineData("floor(3)", "3")]
        [InlineData("ceil(3)", "3")]
        [InlineData("round(3)", "3")]
        [InlineData("round(3, 2)", "3")]
        [InlineData("floor(-3)", "-3")]
        [InlineData("ceil(-3)", "-3")]
        public void RoundingAnIntegerIsTheIdentity(string expression, string expected)
        {
            Assert.Equal(expected, Render(expression, new NumbersModel()));
        }

        /// <summary>The near-neighbours the widening must not disturb: the double and decimal tiers still bind,
        /// still to their own overload, and still produce the rounding they always did.</summary>
        [Theory]
        [InlineData("floor(3.7)", "3")]
        [InlineData("ceil(3.2)", "4")]
        [InlineData("round(3.5)", "4")]
        [InlineData("floor(D)", "2")]
        [InlineData("ceil(D)", "3")]
        [InlineData("floor(M)", "2")]
        [InlineData("ceil(M)", "3")]
        public void TheFractionalTiersAreUnmoved(string expression, string expected)
        {
            // Every expected value here is integral, so no decimal separator reaches the assertion and the
            // suite's ambient culture cannot decide the outcome.
            Assert.Equal(expected, Render(expression, new NumbersModel { D = 2.7, M = 2.7m }));
        }

        /// <summary>The shipped counter-example must stay ambiguous: widening these three names must not be read
        /// as a change to the ranker, which still refuses a genuine tie.</summary>
        [Fact]
        public void AGenuineTieIsStillAmbiguous()
        {
            var result = Compile("min(1, 2u)");
            Assert.False(result.Success);
            Assert.Contains(result.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);
        }

        /// <summary>The build tier reads the same rows, so an overload added to one and not the other is a
        /// silent tier divergence. This is the structural half; the byte-for-byte half is the generator suite's.</summary>
        [Theory]
        [InlineData("round")]
        [InlineData("floor")]
        [InlineData("ceil")]
        public void TheSharedTableCarriesFourNumericTiers(string name)
        {
            var singleArg = DefaultFunctionTable.Rows
                .Where(r => r.Name == name && r.ParameterTypeNames.Length == 1)
                .Select(r => r.ParameterTypeNames[0])
                .OrderBy(t => t, System.StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[] { "System.Decimal", "System.Double", "System.Int32", "System.Int64" },
                singleArg);
        }

    }
}
