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
    /// <para><b>Mixed int/uint pairs are here because their absence hid a defect.</b> The class had no case pairing an
    /// unsuffixed uint-range literal with a small int, and behind that gap the fold read an <c>int</c>'s value out of
    /// the wrong field — so every int promoted to <c>uint</c> arrived as zero. Fuzzing against the real compiler
    /// found 2,081 expressions that broke the host build and 1,636 that were needlessly degraded.</para>
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
        [InlineData("0-3000000000")]
        [InlineData("2147483647*2")]
        [InlineData("2000000000+2000000000")]
        [InlineData("4294967295u+1u")]
        [InlineData("4294967295u+1")]
        [InlineData("0u-5")]
        [InlineData("2*3000000000")]
        [InlineData("2147483648*2")]
        [InlineData("1u-2")]
        [InlineData("3000000000u*2u")]
        [InlineData("79228162514264337593543950335m+1m")]
        [InlineData("-79228162514264337593543950335m-1m")]
        // Shifts, bitwise operators and a constant conditional are constant expressions to C# as much as `+` is,
        // and each of these overflows through one of them.
        [InlineData("(1<<1)+2147483647")]
        [InlineData("(1>>0)+2147483647")]
        [InlineData("(3&1)+2147483647")]
        [InlineData("(2|1)+2147483647")]
        [InlineData("(3^2)+2147483647")]
        [InlineData("(true?1:0)+2147483647")]
        [InlineData("(false?0:1)+2147483647")]
        [InlineData("(1<<30)*4")]
        // A negative shift count is masked to the operand width, so this is a large positive number that then
        // overflows. Folding it as a zero shift instead reports no fault and emits a build error.
        [InlineData("(1<<(0-2))*4")]
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
        [InlineData("(-1u)/0")]
        [InlineData("(-1u)%0")]
        [InlineData("1/(-0u)")]
        // A zero reached through an operator the fold does not evaluate is still a constant zero to C#.
        [InlineData("(1&1)/0")]
        [InlineData("(1|1)/0")]
        [InlineData("(1^0)/0")]
        [InlineData("(1<<1)/0")]
        [InlineData("(2>>1)/0")]
        [InlineData("(true?1:1)/0")]
        [InlineData("1/(1&0)")]
        [InlineData("1/(0|0)")]
        [InlineData("1/(1^1)")]
        [InlineData("1/(0<<3)")]
        [InlineData("1/(1>>1)")]
        [InlineData("1/(true?0:1)")]
        [InlineData("1%(1&0)")]
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
        [InlineData("3000000000/2")]
        [InlineData("5-1u")]
        [InlineData("~5u")]
        [InlineData("4294967295u-1")]
        [InlineData("2u*3u")]
        [InlineData("-2147483648")]
        [InlineData("-2147483648-1")]
        [InlineData("(-2147483648u)-1")]
        [InlineData("-(2147483648)-1")]
        [InlineData("-1u")]
        [InlineData("~0")]
        [InlineData("+5")]
        // The other half of deciding shifts, bitwise operators and constant conditionals: none of these faults, and
        // a fold that refused them would move every template containing one off the precompiled tier.
        [InlineData("(1<<4)+1")]
        [InlineData("(1<<31)+0")]
        [InlineData("(1<<32)+0")]
        [InlineData("(-1>>1)+0")]
        [InlineData("(1L<<62)+1")]
        [InlineData("(2147483647&1)+1")]
        [InlineData("(2147483647|0)-1")]
        [InlineData("(2147483647^2147483647)+1")]
        [InlineData("(true?1:2147483647)+1")]
        [InlineData("(false?2147483647:1)+1")]
        [InlineData("(1<<2)/2")]
        [InlineData("Length<<2")]
        [InlineData("1<<Length")]
        [InlineData("Length&1")]
        public void LegalArithmeticStillPrecompiles(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });

            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);
        }

        /// <summary>
        /// A <c>ulong</c> meeting a <c>char</c>. C# converts a non-negative integer constant to <c>ulong</c>
        /// implicitly, so this is legal C# and folds — and refusing to decide for every signed operand emitted it
        /// straight into a host build that then failed with <c>CS0220</c>.
        /// <para>Written without the <c>Length +</c> wrapper the other cases use: <c>int + ulong</c> is refused by
        /// the runtime operator rules before the fold is ever consulted, so the wrapper hid this entire column.</para>
        /// </summary>
        [Theory]
        [InlineData("18446744073709551615 + 'a'")]
        [InlineData("'a' + 18446744073709551615")]
        [InlineData("18446744073709551615 * 'a'")]
        public void AUnsignedLongMeetingACharDegradesInsteadOfBreakingTheBuild(string expression)
        {
            const string key = "views/ulongchar.heddle";
            var generated = DifferentialHarness.Generate(
                new[] { (key, "@model(){{string}}@(" + expression + ")|@(Length)") });

            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectDegrade(generated, key);
        }

        /// <summary>
        /// Deciding whether a shift is legal is only half of it — the other half is the number it produces, and no
        /// test anywhere compared one. C# masks a shift count to the operand's width (five bits for 32-bit, six for
        /// 64-bit), which makes a negative count a large positive one and a count past the width wrap around; the
        /// engine's expression tree does the same, and the two have to agree digit for digit.
        /// </summary>
        [Theory]
        [InlineData("Length<<2")]
        [InlineData("Length>>1")]
        [InlineData("1<<Length")]
        [InlineData("Length<<33")]
        [InlineData("Length<<(0-1)")]
        [InlineData("Length>>(0-3)")]
        [InlineData("(0-Length)>>1")]
        [InlineData("(0-1)>>Length")]
        [InlineData("1L<<Length")]
        [InlineData("1L<<(Length+60)")]
        public void AShiftProducesTheSameNumberOnBothTiers(string expression)
        {
            const string key = "views/shift-parity.heddle";
            var (precompiled, dyn) = DifferentialHarness.Render(
                key, "@model(){{string}}@(" + expression + ")", typeof(string), "hello");

            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// A property whose type cannot be made nullable. Writing the null-safe hop as <c>x?.M ?? default(T)</c>
        /// widens the member to <c>T?</c>, and a ref struct has no such form — so the generated code failed the
        /// consumer's build with <c>CS8978</c>, no Heddle diagnostic, and no degrade. The generator must never emit
        /// code the host's compiler rejects; where the widening is impossible the receiver is bound to a local and
        /// tested there.
        /// </summary>
        [Fact]
        public void ARefStructPropertyEmitsCodeThatCompiles()
        {
            const string key = "views/refstruct.heddle";
            var generated = DifferentialHarness.Generate(new[]
            {
                (key, "@model(){{Heddle.Generator.IntegrationTests.Fixtures.RefStructModel}}@(Buf.Length)")
            });

            Assert.Empty(generated.Diagnostics);
        }
    }
}
