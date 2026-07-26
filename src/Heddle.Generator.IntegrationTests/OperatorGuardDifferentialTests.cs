using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 4 WI2/WI5 (D3→D6) — the differential corpus for the "deviations from C#" set
    /// ([docs/native-expressions.md] deviations 1–7). Before the shared
    /// <c>NativeOperatorRules</c> table the generator emitted <c>(left op right)</c> with no operand typing at all,
    /// which broke in two opposite directions at once: mixed-type equality produced <b>CS0019 in the consumer's
    /// build</b> for a template the runtime accepts, while enum arithmetic and <c>enum &amp; 0</c> produced valid C#
    /// that <i>renders</i> where the runtime raises a positioned error.
    /// <para>Each entry asserts the shape that closes its half: the template degrades at build time (so no raw C#
    /// operator reaches the consumer's compiler — <c>DifferentialHarness.Generate</c> itself fails the test if the
    /// generated code does not compile), and the dynamic tier — the semantics of record — then supplies the single
    /// verdict both tiers share.</para>
    /// </summary>
    public class OperatorGuardDifferentialTests
    {
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";

        private static string Template(string expression) =>
            "@model(){{" + OrderType + "}}@\\\nvalue: @(" + expression + ")\n";

        /// <summary>Builds the template, asserts the emitter degraded it, and asserts the generated sources carry no
        /// raw C# operator for the guarded expression.</summary>
        private static void AssertDegrades(string key, string expression)
        {
            var content = Template(expression);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
            // A degrade emits no template source at all, so there is nothing left that could carry the operator.
            Assert.Empty(gen.TemplateSources);
        }

        /// <summary>The verdict-parity half: the guarded template degrades, and the dynamic tier rejects it with the
        /// stated positioned diagnostic — so both tiers agree the template is illegal instead of one rendering and
        /// the other erroring.</summary>
        private static void AssertBothTiersReject(string key, string expression, string diagnosticId)
        {
            AssertDegrades(key, expression);

            var template = new HeddleTemplate(Template(expression),
                new CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.False(template.CompileResult.Success);
            Assert.Contains(template.CompileResult.ErrorList, e => e.DiagnosticId == diagnosticId);
        }

        /// <summary>The render-parity half: the guarded template degrades and the dynamic tier renders it, so the
        /// rendered bytes are the runtime's by construction.</summary>
        private static void AssertDegradesAndRenders(string key, string expression, Order model, string expected)
        {
            AssertDegrades(key, expression);

            var template = new HeddleTemplate(Template(expression),
                new CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal(expected, template.Generate(model));
        }

        // --- Deviation 1: == / != on unrelated or mixed types -------------------------------------------------

        [Fact]
        public void MixedTypeEquality_CompilesTheConsumerProject_AndDegrades()
        {
            // The CS0019 class. `Name == Count` is string-vs-int: emitted verbatim it is a hard error in the
            // *consumer's* build — the worst failure mode a source generator has. Both tiers now reject it, with the
            // runtime's own positioned diagnostic.
            AssertBothTiersReject("guard/eq-mixed.heddle", "Name == Count",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        [Fact]
        public void UnrelatedReferenceEquality_DegradesAndRendersTheRuntimeVerdict()
        {
            // The other half of deviation 1: two unrelated *reference* types are CS0019 in C# but a total, null-safe
            // object.Equals in the native tier — so the runtime renders `False` where the generator used to break
            // the build.
            AssertDegradesAndRenders("guard/eq-unrelated.heddle", "Maker == Where",
                new Order { Maker = new Manufacturer(), Where = new Address() }, "value: False\n");
        }

        [Fact]
        public void NullComparisonOnAReference_StillPrecompiles()
        {
            // The guard must not over-degrade the common shapes: `x == null` on a reference operand is identical in
            // both tiers and stays on the precompiled tier.
            var key = "guard/eq-null.heddle";
            var content = Template("Name == null");
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Order),
                new Order { Name = null });
            Assert.Equal(dyn, precompiled);
        }

        // --- Deviation 4: enum arithmetic -----------------------------------------------------------------------

        [Fact]
        public void EnumArithmetic_DegradesInsteadOfRendering()
        {
            // `(Status + 1)` is legal C# and used to render; the native tier raises a positioned error. Opposite
            // verdicts — now both reject.
            AssertBothTiersReject("guard/enum-arith.heddle", "Status + 1",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        // --- Deviation 5: the `enum & 0` literal case ------------------------------------------------------------

        [Fact]
        public void EnumBitwiseWithZeroLiteral_DegradesInsteadOfRendering()
        {
            // C# carries a special case letting the literal 0 convert to any enum; the native tier deliberately does
            // not. Same story as deviation 4 — valid C# that renders vs a positioned runtime error.
            AssertBothTiersReject("guard/enum-and-zero.heddle", "Flags & 0",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        // --- Deviation 6: user-defined implicit conversions -------------------------------------------------------

        [Fact]
        public void UserImplicitConversion_IsNeverConsultedByTheConsumersCompiler()
        {
            // `Total + 1` binds in C# through Money's implicit conversion from int; the native tier never consults
            // user conversions during promotion, so it errors. The generator must not let the consumer's compiler
            // supply the conversion.
            AssertBothTiersReject("guard/user-conversion.heddle", "Total + 1",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        [Fact]
        public void UserDefinedOperator_DegradesAndRendersTheRuntimeResult()
        {
            // The runtime *does* honor user-defined operators, so `Total + Total` renders — through the dynamic
            // tier, at a precompilation-coverage cost with no output change (the guard cannot see whether a struct
            // carries an operator, and a wrong guess here is a silent numeric divergence).
            AssertDegradesAndRenders("guard/user-operator.heddle", "Total + Total",
                new Order { Total = new Money(2.5m) }, "value: 5.0\n");
        }

        // --- Deviation 7: bool? logicals ---------------------------------------------------------------------------

        [Fact]
        public void NullableBoolLogical_DegradesInsteadOfRendering()
        {
            AssertBothTiersReject("guard/nullable-logical.heddle", "Approved && Approved",
                HeddleDiagnosticIds.LogicalOperatorRequiresBool);
        }

        // --- Illegal promotions (HED1008 class) --------------------------------------------------------------------

        [Fact]
        public void IllegalNumericPromotion_DegradesInsteadOfRendering()
        {
            // decimal with double has no common type in either tier, but only one of them used to say so.
            AssertBothTiersReject("guard/illegal-promotion.heddle", "Total.Amount + 1.5",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        // --- The coverage floor: the shapes that must keep precompiling ---------------------------------------------

        [Theory]
        [InlineData("guard/keep-arith.heddle", "Count * 2 + 1")]
        [InlineData("guard/keep-compare.heddle", "Count > 0 && Count <= 10")]
        [InlineData("guard/keep-concat.heddle", "\"n=\" + Count")]
        [InlineData("guard/keep-coalesce.heddle", "Name ?? \"anonymous\"")]
        [InlineData("guard/keep-ternary.heddle", "Count > 0 ? \"some\" : \"none\"")]
        [InlineData("guard/keep-builtin.heddle", "len(Name) > 0")]
        [InlineData("guard/keep-unary.heddle", "-Count")]
        [InlineData("guard/keep-shift.heddle", "Count << 2")]
        [InlineData("guard/keep-bitwise.heddle", "Count & 3")]
        public void SupportedShapes_StayOnThePrecompiledTier(string key, string expression)
        {
            var content = Template(expression);
            var model = new Order { Count = 3, Name = "abc" };
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Order), model);
            Assert.Equal(dyn, precompiled);
        }
    }
}
