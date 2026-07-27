using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Arithmetic the dynamic engine accepts but C# refuses to compile. The engine builds an expression tree and
    /// finds out at render time; C# decides at compile time. Writing the operator through verbatim therefore turned
    /// working templates into build errors in the host's project — a raw <c>CS0020</c>/<c>CS0220</c> reported against
    /// the <c>.heddle</c> file, with no Heddle diagnostic id, and a manifest still claiming the template had
    /// precompiled successfully.
    /// <para><b>Every template here carries a typed model, and that is load-bearing.</b> An earlier version of this
    /// class used <c>@model(){{dynamic}}</c> throughout — under which a constant-only expression degrades anyway, for
    /// reasons that have nothing to do with the fold. Deleting the entire fix left all 876 generator tests green.
    /// A typed model is what makes a degrade attributable to the thing being tested.</para>
    /// </summary>
    public class ConstantArithmeticDifferentialTests
    {
        private const string Key = "views/arith.heddle";

        private static string Template(string expression) => "@model(){{string}}@(Length + (" + expression + "))";

        /// <summary>
        /// C# rejects these outright; the engine renders them (<c>2147483647+1</c> gives <c>-2147483648</c>). The
        /// expression is declined at build time so the value comes from the tier that can produce one.
        /// </summary>
        [Theory]
        [InlineData("2147483647+1")]
        [InlineData("(2147483646+1)+1")]
        [InlineData("(2147483647+0)+(1+0)")]
        [InlineData("+2147483647+1")]
        [InlineData("-2147483648-1")]
        [InlineData("0-3000000000")]
        [InlineData("2147483647*2")]
        [InlineData("2000000000+2000000000")]
        [InlineData("4294967295u+1u")]
        [InlineData("3000000000u*2u")]
        [InlineData("79228162514264337593543950335m+1m")]
        public void ConstantOverflowDegradesInsteadOfBreakingTheBuild(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });

            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectDegrade(generated, Key);
        }

        /// <summary>
        /// Division by a constant zero is a build error in C# and a render-time <c>DivideByZeroException</c> in the
        /// engine. There is no value to fold to, so the only answer that matches is to let the engine raise it.
        /// </summary>
        [Theory]
        [InlineData("1/0")]
        [InlineData("1%0")]
        [InlineData("1/(1-1)")]
        [InlineData("(1+1)/0")]
        [InlineData("1.0m/0m")]
        [InlineData("1L/0")]
        public void ConstantDivisionByZeroDegradesInsteadOfBreakingTheBuild(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });

            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectDegrade(generated, Key);
        }

        /// <summary>
        /// The degrade has to be narrow, and this is the half that matters most in daily use: a fold that refuses
        /// what C# accepts moves the <b>whole template</b> off the precompiled tier, silently and with no diagnostic
        /// to notice. These are all legal C#, several of them only by the promotion rules — <c>2147483647+1L</c>
        /// evaluates in <c>long</c> and does not overflow — and every one must still precompile.
        /// </summary>
        [Theory]
        [InlineData("Length/2")]
        [InlineData("Length+1")]
        [InlineData("1/Length")]
        [InlineData("6/3")]
        [InlineData("2+3")]
        [InlineData("1.0/0")]
        [InlineData("1.0%0")]
        [InlineData("1/0.0")]
        [InlineData("2147483647+1L")]
        [InlineData("1L+2147483647")]
        [InlineData("2000000000L*3")]
        [InlineData("3000000000+1")]
        [InlineData("-2147483648")]
        [InlineData("~0")]
        [InlineData("+5")]
        public void LegalArithmeticStillPrecompiles(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });

            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);
        }
    }
}
