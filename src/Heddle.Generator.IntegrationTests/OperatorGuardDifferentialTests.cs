using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The differential corpus for cases where the generator and runtime differ on operator validity. Before the
    /// shared <c>NativeOperatorRules</c> table the generator emitted <c>(left op right)</c> with no operand typing
    /// at all, which broke in two opposite directions: mixed-type equality produced <b>CS0019 in the consumer's
    /// build</b> for a template the runtime accepts, while enum arithmetic and <c>enum &amp; 0</c> produced valid C#
    /// that <i>renders</i> where the runtime raises a positioned error.
    /// <para>Each entry asserts the shape that closes its half: the template degrades at build time (so no raw C#
    /// operator reaches the consumer's compiler), and the dynamic tier supplies the single verdict both tiers share.</para>
    /// </summary>
    public class OperatorGuardDifferentialTests
    {
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";

        private static string Template(string expression) =>
            "@model(){{" + OrderType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static void AssertDegrades(string key, string expression)
        {
            var content = Template(expression);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.Empty(gen.TemplateSources);
        }

        /// <summary>Asserts both tiers reject the guarded expression with the same diagnostic.</summary>
        private static void AssertBothTiersReject(string key, string expression, string diagnosticId)
        {
            AssertDegrades(key, expression);

            var template = new HeddleTemplate(Template(expression),
                new CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.False(template.CompileResult.Success);
            Assert.Contains(template.CompileResult.ErrorList, e => e.DiagnosticId == diagnosticId);
        }

        /// <summary>Asserts the guarded expression degrades but the dynamic tier renders it.</summary>
        private static void AssertDegradesAndRenders(string key, string expression, Order model, string expected)
        {
            AssertDegrades(key, expression);

            var template = new HeddleTemplate(Template(expression),
                new CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal(expected, template.Generate(model));
        }


        [Fact]
        public void MixedTypeEquality_CompilesTheConsumerProject_AndDegrades()
        {
            AssertBothTiersReject("guard/eq-mixed.heddle", "Name == Count",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        [Fact]
        public void UnrelatedReferenceEquality_DegradesAndRendersTheRuntimeVerdict()
        {
            AssertDegradesAndRenders("guard/eq-unrelated.heddle", "Maker == Where",
                new Order { Maker = new Manufacturer(), Where = new Address() }, "value: False\n");
        }

        [Fact]
        public void NullComparisonOnAReference_StillPrecompiles()
        {
            var key = "guard/eq-null.heddle";
            var content = Template("Name == null");
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Order),
                new Order { Name = null });
            Assert.Equal(dyn, precompiled);
        }


        [Fact]
        public void EnumArithmetic_DegradesInsteadOfRendering()
        {
            AssertBothTiersReject("guard/enum-arith.heddle", "Status + 1",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }


        [Fact]
        public void EnumBitwiseWithZeroLiteral_DegradesInsteadOfRendering()
        {
            AssertBothTiersReject("guard/enum-and-zero.heddle", "Flags & 0",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }


        [Fact]
        public void UserImplicitConversion_IsNeverConsultedByTheConsumersCompiler()
        {
            AssertBothTiersReject("guard/user-conversion.heddle", "Total + 1",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        [Fact]
        public void UserDefinedOperator_DegradesAndRendersTheRuntimeResult()
        {
            AssertDegradesAndRenders("guard/user-operator.heddle", "Total + Total",
                new Order { Total = new Money(2.5m) }, "value: 5.0\n");
        }


        [Fact]
        public void NullableBoolLogical_DegradesInsteadOfRendering()
        {
            AssertBothTiersReject("guard/nullable-logical.heddle", "Approved && Approved",
                HeddleDiagnosticIds.LogicalOperatorRequiresBool);
        }


        [Fact]
        public void IllegalNumericPromotion_DegradesInsteadOfRendering()
        {
            AssertBothTiersReject("guard/illegal-promotion.heddle", "Total.Amount + 1.5",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        [Fact]
        public void UnaryNegateOnAnEnum_DegradesInsteadOfEmitting()
        {
            AssertBothTiersReject("guard/unary-enum.heddle", "-Status", HeddleDiagnosticIds.UnaryOperatorNotDefined);
        }

        [Fact]
        public void UnaryNegateOnAUserStruct_DegradesInsteadOfEmitting()
        {
            AssertBothTiersReject("guard/unary-struct.heddle", "-Total", HeddleDiagnosticIds.UnaryOperatorNotDefined);
        }

        [Fact]
        public void UnaryNotOnANullableBool_DegradesAndRendersTheRuntimeResult()
        {
            AssertDegradesAndRenders("guard/unary-lifted-not.heddle", "!Approved",
                new Order { Approved = true }, "value: False\n");
        }

        [Fact]
        public void StringConcatWithAUserConvertibleStruct_DegradesAndRendersTheRuntimeResult()
        {
            AssertDegradesAndRenders("guard/concat-user-conversion.heddle", "\"n=\" + Tag",
                new Order { Tag = new Label("x") }, "value: n=tostring:x\n");
        }

        [Fact]
        public void WideShiftCount_DegradesAndRendersTheRuntimeResult()
        {
            AssertDegradesAndRenders("guard/shift-wide-count.heddle", "Count << Big",
                new Order { Count = 3, Big = 2 }, "value: 12\n");
        }

        [Fact]
        public void LiftedShift_DegradesAndRendersTheRuntimeResult()
        {
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
            AssertBothTiersReject("guard/ternary-arms.heddle", "Count > 0 ? Name : Count",
                HeddleDiagnosticIds.TernaryArmsNoCommonType);
        }


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
