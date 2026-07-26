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

        // --- The unary and ternary halves of the guard ------------------------------------------------------------
        //
        // Added by the phase-4 audit (2026-07-26) to close a surviving mutant: deleting the `Supported` guards from
        // `WriteUnary` *and* `WriteTernary` together reddened **zero** tests in all three suites, even though D3/D6
        // and the success criteria both say "binaries/unaries/ternaries". The binary half had eight named entries;
        // the other two arities rode along on the shared estimator with no differential coverage at all, so the
        // unconditional-emit defect they were fixed for could have been reintroduced silently. One entry per verdict
        // class per arity, mirroring the binary rows above.

        [Fact]
        public void UnaryNegateOnAnEnum_DegradesInsteadOfEmitting()
        {
            // C# accepts `-enumValue` (it yields the underlying type) and would render; the native tier rejects any
            // non-numeric negate operand. Opposite verdicts before the guard — the unary twin of deviation 4.
            AssertBothTiersReject("guard/unary-enum.heddle", "-Status", HeddleDiagnosticIds.UnaryOperatorNotDefined);
        }

        [Fact]
        public void UnaryNegateOnAUserStruct_DegradesInsteadOfEmitting()
        {
            // Money declares an implicit conversion from int, so the consumer's compiler would happily negate it
            // through that conversion; the native tier never consults user conversions (deviation 6) and errors.
            AssertBothTiersReject("guard/unary-struct.heddle", "-Total", HeddleDiagnosticIds.UnaryOperatorNotDefined);
        }

        [Fact]
        public void UnaryNotOnANullableBool_DegradesAndRendersTheRuntimeResult()
        {
            // The other verdict class: the runtime *does* lift `!` over bool?, so this renders — but only the
            // runtime's own lifting is trusted to do it, so the expression degrades rather than being emitted.
            AssertDegradesAndRenders("guard/unary-lifted-not.heddle", "!Approved",
                new Order { Approved = true }, "value: False\n");
        }

        [Fact]
        public void StringConcatWithAUserConvertibleStruct_DegradesAndRendersTheRuntimeResult()
        {
            // Another surviving mutant closed by the phase-4 audit: promoting the string-`+` row's
            // Enum/Reference/Other partners to `Supported` reddened zero tests, because every fixture partner's
            // ToString and C#'s chosen overload happened to agree. `Label` breaks that tie on purpose — it declares
            // an implicit conversion to string that differs from its ToString — so the row's claim ("formatting the
            // non-string side is runtime-owned") is now observable: C# would emit Concat(string, string) through the
            // user conversion and render "converted:x", the runtime goes through Concat(object, object) and renders
            // "tostring:x". Deviation 6, in the concat position.
            AssertDegradesAndRenders("guard/concat-user-conversion.heddle", "\"n=\" + Tag",
                new Order { Tag = new Label("x") }, "value: n=tostring:x\n");
        }

        // --- The shift row: a wide count is CS0019 in the consumer's build --------------------------------------
        //
        // Also a surviving mutant closed by the phase-4 audit: deleting the shift row's `RequiresRuntimeSemantics`
        // arm entirely — emitting lifted shifts and wide shift counts verbatim — reddened zero tests. C# defines
        // `<<` only for an int-typed (or implicitly-int) count, while the runtime narrows *any* integral count to
        // int, so `Count << Big` is a hard consumer-build error precompiled and a rendering expression at run time:
        // the CS0019 class this phase exists to close, on a row no fixture reached.

        [Fact]
        public void WideShiftCount_DegradesAndRendersTheRuntimeResult()
        {
            AssertDegradesAndRenders("guard/shift-wide-count.heddle", "Count << Big",
                new Order { Count = 3, Big = 2 }, "value: 12\n");
        }

        [Fact]
        public void LiftedShift_DegradesAndRendersTheRuntimeResult()
        {
            // The lifted half of the same row. C# and the runtime happen to agree on the text here, but the
            // agreement is unproven (the runtime narrows the lifted count to int? on its own path), so the row
            // degrades on purpose — and this entry is what makes "degrades and renders identically" a fact.
            AssertDegradesAndRenders("guard/shift-lifted.heddle", "Maybe << 2",
                new Order { Maybe = 3 }, "value: 12\n");
        }

        [Fact]
        public void TernaryWithANonBoolCondition_DegradesInsteadOfEmitting()
        {
            AssertBothTiersReject("guard/ternary-cond.heddle", "Count ? \"a\" : \"b\"",
                HeddleDiagnosticIds.TernaryConditionNotBool);
        }

        [Fact]
        public void TernaryWithUnrelatedArms_DegradesInsteadOfEmitting()
        {
            // C# would reject this too (CS0173 — in the *consumer's* build, the worst failure mode), and the runtime
            // rejects it with its own positioned diagnostic. The guard turns a build break into a matched verdict.
            AssertBothTiersReject("guard/ternary-arms.heddle", "Count > 0 ? Name : Count",
                HeddleDiagnosticIds.TernaryArmsNoCommonType);
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
