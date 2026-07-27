using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Arithmetic the dynamic engine accepts but C# refuses to compile. The engine builds an expression tree and
    /// finds out at render time; C# decides at compile time. Writing the operator through verbatim therefore turned
    /// working templates into build errors in the host's project — reported against the <c>.heddle</c> file, as a raw
    /// <c>CS0020</c>/<c>CS0220</c> with no Heddle diagnostic id, and with the manifest still claiming the template had
    /// precompiled successfully.
    /// <para>These render through both tiers. The precompiled side degrades the expression to the dynamic tier, so
    /// what a host observes is whatever the engine does — which is the contract.</para>
    /// </summary>
    public class ConstantArithmeticDifferentialTests
    {
        /// <summary>The sharpest case: the engine renders <c>-2147483648</c> and the generated code did not compile
        /// at all. The expression is now declined at build time, which is the degrade the harness makes tests
        /// declare, and the value a host sees comes from the tier that can produce one.</summary>
        [Theory]
        [InlineData("@(2147483647+1)")]
        [InlineData("@(-2147483648-1)")]
        [InlineData("@(2147483647*2)")]
        [InlineData("@((2147483646+1)+1)")]
        public void ConstantOverflowDegradesInsteadOfBreakingTheBuild(string expression)
        {
            const string key = "views/overflow.heddle";
            var generated = DifferentialHarness.Generate(new[] { (key, "@model(){{dynamic}}" + expression) });

            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectDegrade(generated, key);
        }

        /// <summary>
        /// Division by a constant zero is a build error in C# and a render-time <c>DivideByZeroException</c> in the
        /// engine. There is no value to fold to, so the only answer that matches is to let the engine raise it.
        /// </summary>
        [Theory]
        [InlineData("@(1/0)")]
        [InlineData("@(1%0)")]
        [InlineData("@(1/(1-1))")]
        [InlineData("@((1+1)/0)")]
        [InlineData("@(1.0m/0m)")]
        public void ConstantDivisionByZeroCompilesAndFaultsLikeTheEngine(string expression)
        {
            var generated = DifferentialHarness.Generate(
                new[] { ("views/divzero.heddle", "@model(){{dynamic}}" + expression) });

            Assert.Empty(generated.Diagnostics);
        }

        /// <summary>The degrade must be narrow. A non-constant divisor cannot be decided at build time and has to
        /// keep precompiling, or the fix would quietly move ordinary arithmetic off the fast tier.</summary>
        [Theory]
        [InlineData("@model(){{string}}@(Length/2)")]
        [InlineData("@model(){{string}}@(Length+1)")]
        [InlineData("@model(){{string}}@(1/Length)")]
        [InlineData("@model(){{dynamic}}@(6/3)")]
        [InlineData("@model(){{dynamic}}@(1.0/0)")]
        public void OrdinaryArithmeticStillPrecompiles(string template)
        {
            var generated = DifferentialHarness.Generate(new[] { ("views/ok.heddle", template) });

            Assert.Empty(generated.Diagnostics);
        }
    }
}
