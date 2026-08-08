using System.Linq;
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
        /// An integral constant that overflows. Both tiers wrap it and produce the same number:
        /// <c>2147483647+1</c> is <c>-2147483648</c> on each. The emitted expression is written inside an
        /// <c>unchecked</c> — because the engine's <c>Expression.Add</c> is unchecked and a host's
        /// <c>&lt;CheckForOverflowUnderflow&gt;</c> must not decide what a template renders — and a constant folded
        /// there wraps rather than failing the build, so refusing these took working templates off the precompiled
        /// tier for a build error that can no longer happen.
        /// <para>The assertion is the rendered bytes and not merely that the template precompiled: what has to hold
        /// is that the two tiers wrap to the <em>same</em> number, which a "still precompiles" row cannot say.</para>
        /// </summary>
        [Theory]
        [InlineData("2147483647+1")]
        [InlineData("(2147483646+1)+1")]
        [InlineData("(2147483647+0)+(1+0)")]
        [InlineData("+2147483647+1")]
        [InlineData("2147483647*2")]
        [InlineData("2000000000+2000000000")]
        [InlineData("4294967295u+1u")]
        [InlineData("3000000000u*2u")]
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
        // overflows. Folding it as a zero shift instead would produce a different number here than at render.
        [InlineData("(1<<(0-2))*4")]
        public void AnIntegralConstantOverflowPrecompilesAndWrapsToTheEnginesNumber(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);

            var (precompiled, dyn) = DifferentialHarness.Render(Key, Template(expression), typeof(string), "hello");
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// An <c>int</c> meeting a <c>uint</c> where the unsigned arithmetic wraps, so the two tiers reach
        /// DIFFERENT numbers: C# computes in <c>uint</c> (the implicit constant conversion) while the engine's
        /// tree computes in <c>long</c>. The writer now spells the ENGINE's folded value as a typed literal —
        /// <c>0u-5</c> emits as <c>(-5L)</c> — so the shape precompiles and renders the engine's number.
        /// </summary>
        [Theory]
        [InlineData("0-3000000000")]
        [InlineData("4294967295u+1")]
        [InlineData("0u-5")]
        [InlineData("2*3000000000")]
        [InlineData("2147483648*2")]
        [InlineData("1u-2")]
        public void AnIntMeetingAUintThatWraps_NowPrecompilesToTheEnginesNumber(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);

            var (precompiled, dyn) = DifferentialHarness.Render(Key, Template(expression), typeof(string), "hello");
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The near neighbour that keeps the row above a rule rather than a refusal of every mixed pair:
        /// the same <c>int</c>/<c>uint</c> mixing with a result that fits in a <c>uint</c>, which both tiers reach and
        /// render identically — and still on the precompiled tier, which is the half the byte comparison alone
        /// cannot say.</summary>
        [Theory]
        [InlineData("4294967295u-1")]
        [InlineData("5-1u")]
        [InlineData("2u*3")]
        [InlineData("3000000000u+1")]
        public void AnIntMeetingAUintThatDoesNotWrapPrecompilesAndMatches(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);

            var (precompiled, dyn) = DifferentialHarness.Render(Key, Template(expression), typeof(string), "hello");

            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The mixed pair as the operand of a <b>second</b> operator, where the tiers' numbers part company:
        /// <c>(0-0u)-(1u)</c> is 4294967295 in C#'s <c>uint</c> and -1 in the engine's <c>long</c>. The
        /// engine-literal spelling closes these too — the whole constant emits as the engine's value.
        /// </summary>
        [Theory]
        [InlineData("(0-0u)-(1u)")]
        [InlineData("(0+0u)-(1u)")]
        [InlineData("(0*1u)-(1u)")]
        [InlineData("(1u-1)-(1u)")]
        [InlineData("(0&0u)-(1u)")]
        [InlineData("((0-0u)-(0u))-(1u)")]
        [InlineData("(0|0u)-(1u)")]
        [InlineData("(0^0u)-(1u)")]
        [InlineData("(true?0u:0)-(1u)")]
        // The complement is the unary operator that does not re-promote both tiers onto one type: `~(uint)0` is
        // 4294967295 where `~(long)0` is -1.
        [InlineData("~(0-0u)")]
        // A shift keeps its left operand's type, so it carries the difference straight into the number: shifting a
        // uint 4294967295 left by 31 gives 2147483648 and shifting the long by 31 gives 9223372034707292160.
        [InlineData("(4294967295u-0)<<31")]
        public void AMixedPairFeedingASecondOperator_NowPrecompilesToTheEnginesNumber(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);

            var (precompiled, dyn) = DifferentialHarness.Render(Key, Template(expression), typeof(string), "hello");
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The near neighbours of the row above, so that it reads as a rule about the mixed promotion rather than a
        /// refusal of every nested constant. None of these mixes an <c>int</c> with a <c>uint</c> anywhere, or the
        /// operator that consumes the mixed pair re-promotes both tiers onto <c>long</c> — and every one still
        /// precompiles and renders the engine's bytes.
        /// </summary>
        [Theory]
        [InlineData("(0u-0u)-(1u)")]
        [InlineData("(4294967295u-0u)<<31")]
        [InlineData("(0-0)-(1)")]
        [InlineData("(0L-0)-(1u)")]
        // A signed partner puts both tiers in `long`, which is why the mixed pair may still be written here.
        [InlineData("(5-1u)-(1)")]
        [InlineData("(5-1u)+Length")]
        [InlineData("-(0-0u)")]
        public void AMixedPairWhoseSecondOperatorReconvergesStillPrecompiles(string expression)
        {
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, Key);

            var (precompiled, dyn) = DifferentialHarness.Render(Key, Template(expression), typeof(string), "hello");
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The tainted constant reaching a <b>member</b>: the engine-literal spelling types the constant as the
        /// engine's <c>long</c>, so even the <c>uint</c> member rows — where verbatim C# would have stayed in
        /// <c>uint</c> and wrapped — now promote to the engine's type and match its bytes.
        /// </summary>
        [Theory]
        [InlineData("Ticks - (0-0u)")]
        [InlineData("Ticks + (5-1u)")]
        [InlineData("Count - (0-0u)")]
        [InlineData("Count + (5-1u)")]
        [InlineData("Label + (5-1u)")]
        public void AMixedPairMeetingAMemberFollowsThatMembersType(string expression)
        {
            const string key = "views/arith-member.heddle";
            var template = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.UnsignedMemberModel}}@(" +
                           expression + ")";
            var model = new Fixtures.UnsignedMemberModel { Ticks = 5, Count = 7, Label = "n" };

            var generated = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, key);
            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(Fixtures.UnsignedMemberModel), model);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The two overflows <c>unchecked</c> does <b>not</b> settle. A <c>decimal</c> overflow is
        /// <c>CS0463</c> whatever it is written inside, and the smallest signed value over <c>-1</c> is folded
        /// silently by C# while the engine raises <c>OverflowException</c> at render. Both now emit as a
        /// <c>RuntimeOperators</c> call the consumer's compiler cannot fold, so the template PRECOMPILES and
        /// the render throw is the engine's own — parity here means both tiers throwing alike.
        /// </summary>
        [Theory]
        [InlineData("79228162514264337593543950335m+1m")]
        [InlineData("-79228162514264337593543950335m-1m")]
        [InlineData("79228162514264337593543950335m*2m")]
        [InlineData("(0-2147483647-1)/(0-1)")]
        [InlineData("(0-2147483647-1)%(0-1)")]
        [InlineData("(0-9223372036854775807L-1L)/(0-1)")]
        [InlineData("(0-9223372036854775807L-1L)%(0-1)")]
        public void AThrowingConstantFold_NowPrecompilesAndThrowsAlikeAtRender(string expression)
        {
            var (precompiled, dyn) = DifferentialHarness.DeferredWithOptions(Key, Template(expression),
                typeof(string), "hello", new Heddle.Data.TemplateOptions());

            var precompiledThrow = Assert.ThrowsAny<System.ArithmeticException>(() => precompiled());
            var dynamicThrow = Assert.ThrowsAny<System.ArithmeticException>(() => dyn());
            Assert.Equal(dynamicThrow.GetType(), precompiledThrow.GetType());
        }

        /// <summary>
        /// Division by a constant zero is refused at compile time by BOTH tiers. It used to degrade
        /// instead — C# refuses <c>CS0020</c>, the engine's expression tree threw at render, and the degrade let
        /// "the tier whose behaviour is the contract" keep rendering — but that expression has no behaviour to
        /// keep: rendering it can only ever throw. The engine now raises <c>HED1018</c> from its expression
        /// compiler, and the generator, whose fold proves the same fact, FORWARDS the same id as a build error
        /// rather than minting a HED7xxx twin — one fact, one id, per the same-fact-same-id registry rule.
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
        public void ConstantDivisionByZeroIsRefusedByBothTiers(string expression)
        {
            // The engine half: a positioned HED1018 compile error, not a render-time throw.
            var engine = new Heddle.HeddleTemplate(Template(expression),
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.False(engine.CompileResult.Success);
            Assert.Contains(engine.CompileResult.Errors, e => e.DiagnosticId == "HED1018");

            // The build half: the SAME id, forwarded as an error — not a HED7xxx twin. The template still
            // degrades (no entry is emitted), so HED7031's notice may ride along; nothing else may.
            var generated = DifferentialHarness.Generate(new[] { (Key, Template(expression)) });
            var forwarded = Assert.Single(generated.Diagnostics.Where(d => d.Id == "HED1018"));
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, forwarded.Severity);
            Assert.Empty(generated.Diagnostics.Where(d => d.Id != "HED1018" && d.Id != "HED7031"));
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
        /// straight into a host build that then failed with <c>CS0220</c>. Since the emission became
        /// <c>unchecked</c> the fold wraps instead, so what has to hold is that it wraps to the engine's number.
        /// <para>Written without the <c>Length +</c> wrapper the other cases use: <c>int + ulong</c> is refused by
        /// the runtime operator rules before the fold is ever consulted, so the wrapper hid this entire column.</para>
        /// </summary>
        [Theory]
        [InlineData("18446744073709551615 + 'a'")]
        [InlineData("'a' + 18446744073709551615")]
        [InlineData("18446744073709551615 * 'a'")]
        public void AnUnsignedLongMeetingACharWrapsToTheEnginesNumber(string expression)
        {
            const string key = "views/ulongchar.heddle";
            var template = "@model(){{string}}@(" + expression + ")|@(Length)";
            var generated = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.Empty(generated.Diagnostics);
            DifferentialHarness.ExpectPrecompiled(generated, key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(string), "hello");
            Assert.Equal(dyn, precompiled);
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
