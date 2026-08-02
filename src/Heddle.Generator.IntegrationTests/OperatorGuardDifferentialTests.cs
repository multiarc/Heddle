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

        /// <summary>Unrelated reference equality emits through RuntimeOperators, which replays the engine's
        /// own fallback chain (a user operator where the pair binds one, null-safe object.Equals otherwise)
        /// over the same static types — so the shape that used to degrade now precompiles and matches byte
        /// for byte, the null lanes included (object.Equals(null, null) is TRUE, and both tiers say so).</summary>
        [Fact]
        public void UnrelatedReferenceEquality_NowPrecompilesAndMatchesTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/eq-unrelated.heddle", "Maker == Where",
                    new Order { Maker = new Manufacturer(), Where = new Address() }),
                ("guard/eq-unrelated-nulls.heddle", "Maker == Where", new Order { Maker = null, Where = null }),
                ("guard/eq-unrelated-half.heddle", "Maker == Where",
                    new Order { Maker = new Manufacturer(), Where = null }),
                ("guard/neq-unrelated.heddle", "Maker != Where",
                    new Order { Maker = new Manufacturer(), Where = new Address() }),
                // A string against a nullable numeric: mixed KINDS, both null-assignable, same chain.
                ("guard/eq-mixed-kinds.heddle", "Name == Maybe", new Order { Name = "5", Maybe = 5 }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
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

        /// <summary>A user-defined operator the witness proves emits through the RuntimeOperators adapter,
        /// which replays the engine's Expression factory over the operands' static types; an operator the
        /// witness proves ABSENT is the engine's HED1008, matched at build time.</summary>
        [Fact]
        public void UserDefinedOperator_NowPrecompilesThroughTheAdapter()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/user-operator.heddle", "Total + Total", new Order { Total = new Money(2.5m) }),
                ("guard/user-relational.heddle", "Total < Total", new Order { Total = new Money(2.5m) }),
                ("guard/user-relational-gt.heddle", "Total > Total", new Order { Total = new Money(2.5m) }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }

            AssertBothTiersReject("guard/user-operator-absent.heddle", "Total - Total",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
            AssertBothTiersReject("guard/user-relational-absent.heddle", "Total <= Total",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
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

        /// <summary>Shapes the shared table emits verbatim (or as one spelled constant/cast) because C# and
        /// the engine are provably byte-identical there: <c>null == null</c>, lifted <c>!</c> on <c>bool?</c>,
        /// <c>~</c> over an enum at either nullability, and <c>null ?? x</c> (the right operand boxed).</summary>
        [Fact]
        public void VerbatimClosures_NowPrecompileAndMatchTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/null-eq-null.heddle", "null == null", new Order()),
                ("guard/null-neq-null.heddle", "null != null", new Order()),
                ("guard/unary-lifted-not.heddle", "!Approved", new Order { Approved = true }),
                ("guard/unary-lifted-not-null.heddle", "!Approved", new Order { Approved = null }),
                ("guard/complement-enum.heddle", "~Flags", new Order { Flags = OrderFlags.Rush }),
                ("guard/complement-lifted-enum.heddle", "~FlagsMaybe", new Order { FlagsMaybe = OrderFlags.Gift }),
                ("guard/complement-lifted-enum-null.heddle", "~FlagsMaybe", new Order { FlagsMaybe = null }),
                ("guard/null-coalesce-value.heddle", "null ?? Count", new Order { Count = 7 }),
                ("guard/null-coalesce-ref.heddle", "null ?? Name", new Order { Name = "x" }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
        }

        /// <summary>Shapes the shared table emits once operand type IDENTITY (and, for reference pairs, the
        /// assignability relation) is known: same-enum bitwise/equality/coalesce/ternary, identical
        /// reference and user-struct arms, and widening reference pairs, which pin the narrower operand
        /// with a cast to the wider type.</summary>
        [Fact]
        public void TypeIdentityClosures_NowPrecompileAndMatchTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/enum-bitwise.heddle", "Flags & Flags",
                    new Order { Flags = OrderFlags.Rush | OrderFlags.Gift }),
                ("guard/enum-bitwise-lifted.heddle", "Flags & FlagsMaybe",
                    new Order { Flags = OrderFlags.Rush, FlagsMaybe = OrderFlags.Rush }),
                ("guard/enum-bitwise-lifted-null.heddle", "Flags | FlagsMaybe",
                    new Order { Flags = OrderFlags.Rush, FlagsMaybe = null }),
                ("guard/enum-equality.heddle", "Status == Status", new Order { Status = OrderStatus.Open }),
                ("guard/enum-inequality.heddle", "Status != Status", new Order { Status = OrderStatus.Open }),
                ("guard/enum-ternary.heddle", "Count > 0 ? Status : Status",
                    new Order { Count = 1, Status = OrderStatus.Closed }),
                ("guard/enum-coalesce.heddle", "FlagsMaybe ?? Flags",
                    new Order { FlagsMaybe = null, Flags = OrderFlags.Gift }),
                ("guard/enum-coalesce-value.heddle", "FlagsMaybe ?? Flags",
                    new Order { FlagsMaybe = OrderFlags.Rush, Flags = OrderFlags.Gift }),
                ("guard/struct-ternary-identical.heddle", "Count > 0 ? Total : Total",
                    new Order { Count = 1, Total = new Money(2.5m) }),
                ("guard/ref-ternary-identical.heddle", "Count > 0 ? Maker : Maker",
                    new Order { Count = 0, Maker = new Manufacturer() }),
                ("guard/ref-ternary-widening.heddle", "Count > 0 ? Rig : Ride",
                    new Order { Count = 1, Rig = new Truck(), Ride = new Vehicle() }),
                ("guard/ref-coalesce-identical.heddle", "Maker ?? Maker", new Order { Maker = null }),
                ("guard/ref-coalesce-widening.heddle", "Rig ?? Ride",
                    new Order { Rig = null, Ride = new Vehicle() }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
        }

        /// <summary>The identity-decided refusals: shapes the engine rejects on every input, now matched
        /// (degrade + the engine's positioned id) instead of silently runtime-owned.</summary>
        [Fact]
        public void CrossTypeShapes_AreRefusedByBothTiers()
        {
            AssertBothTiersReject("guard/enum-eq-cross.heddle", "Status == Flags",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
            AssertBothTiersReject("guard/enum-bitwise-cross.heddle", "Flags & Status",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
            AssertBothTiersReject("guard/enum-relational.heddle", "Status < Status",
                HeddleDiagnosticIds.BinaryOperatorNotDefined);
            AssertBothTiersReject("guard/enum-ternary-cross.heddle", "Count > 0 ? Status : Flags",
                HeddleDiagnosticIds.TernaryArmsNoCommonType);
            AssertBothTiersReject("guard/ref-ternary-unrelated.heddle", "Count > 0 ? Maker : Where",
                HeddleDiagnosticIds.TernaryArmsNoCommonType);
        }

        /// <summary>String concatenation with an enum or user-typed operand now emits the engine's exact
        /// BCL call — string.Concat(object, object) — which bypasses user-defined '+' operators and implicit
        /// conversions the way the engine's EmitStringConcat always has. The Label row is the one that
        /// mattered: verbatim C# would take its implicit conversion to string and print a different text
        /// than the engine's ToString path.</summary>
        [Fact]
        public void StringConcatWithEnumAndUserTypes_NowPrecompilesAndMatchesTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/concat-user-conversion.heddle", "\"n=\" + Tag", new Order { Tag = new Label("x") }),
                ("guard/concat-enum.heddle", "\"s=\" + Status", new Order { Status = OrderStatus.Open }),
                ("guard/concat-ref.heddle", "\"m=\" + Maker", new Order { Maker = new Manufacturer() }),
                ("guard/concat-ref-null.heddle", "\"m=\" + Maker", new Order { Maker = null }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
        }

        /// <summary>Shift shapes the table used to hold as runtime-owned, now emitted: a wide count is
        /// normalised with the truncating <c>(int)</c>/<c>(int?)</c> cast the runtime applies through
        /// <c>Expression.Convert</c>, and a lifted shift lifts identically in C#. Byte parity across both
        /// tiers — including the truncation, mask and null lanes — is what moved the verdict.</summary>
        [Fact]
        public void WideShiftCount_NowPrecompilesAndMatchesTheRuntime()
        {
            var content = Template("Count << Big");
            var models = new[]
            {
                new Order { Count = 3, Big = 2 },
                // The mask lane: a count of 33 keeps its low five bits on both tiers, so this is << 1.
                new Order { Count = 3, Big = 33 },
                // The truncation lane: -1L truncates to -1, which masks to 31.
                new Order { Count = 3, Big = -1 },
            };
            for (var i = 0; i < models.Length; i++)
            {
                var (precompiled, dyn) = DifferentialHarness.Render(
                    "guard/shift-wide-count-" + i + ".heddle", content, typeof(Order), models[i]);
                Assert.Equal(dyn, precompiled);
            }
        }

        [Fact]
        public void LiftedShift_NowPrecompilesAndMatchesTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/shift-lifted-value.heddle", "Maybe << 2", new Order { Maybe = 3 }),
                ("guard/shift-lifted-null.heddle", "Maybe << 2", new Order { Maybe = null }),
                ("guard/shift-lifted-count.heddle", "Count << Maybe", new Order { Count = 3, Maybe = 2 }),
                ("guard/shift-lifted-count-null.heddle", "Count << Maybe", new Order { Count = 3, Maybe = null }),
                // The lifted-cast lane: a nullable WIDE count takes the (int?) spelling.
                ("guard/shift-lifted-wide.heddle", "Count << BigMaybe", new Order { Count = 3, BigMaybe = 2L }),
                ("guard/shift-lifted-wide-null.heddle", "Count << BigMaybe", new Order { Count = 3, BigMaybe = null }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
        }

        /// <summary>Coalesce over differing numeric kinds unifies in the engine's promotion, now written down
        /// as casts. The int? ?? uint row is the one that mattered: the table held it Supported and the writer
        /// emitted it VERBATIM — which is CS0019 in the consumer's build while the engine renders long — so
        /// before this spelling the pair was a latent consumer-build break, not a degrade.</summary>
        [Fact]
        public void NumericCoalescePromotion_NowPrecompilesAndMatchesTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/coalesce-promote.heddle", "Maybe ?? Big", new Order { Maybe = 5, Big = 9 }),
                ("guard/coalesce-promote-null.heddle", "Maybe ?? Big", new Order { Maybe = null, Big = 9 }),
                ("guard/coalesce-cs0019.heddle", "Maybe ?? Unsigned", new Order { Maybe = 5, Unsigned = 7 }),
                ("guard/coalesce-cs0019-null.heddle", "Maybe ?? Unsigned",
                    new Order { Maybe = null, Unsigned = 7 }),
                ("guard/coalesce-lifted-right.heddle", "Maybe ?? BigMaybe",
                    new Order { Maybe = null, BigMaybe = 4L }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
        }

        /// <summary>The cross-category coalesce mixes are refused by the engine's Coalesce on every input
        /// (HED1007), so the verdict now matches the refusal instead of degrading around it.</summary>
        [Fact]
        public void StringCoalesceWithAValueOperand_IsRefusedByBothTiers()
        {
            AssertBothTiersReject("guard/coalesce-mixed.heddle", "Name ?? Count",
                HeddleDiagnosticIds.TernaryArmsNoCommonType);
        }

        /// <summary>Ternary arm shapes the table used to hold as runtime-owned, now emitted: differing numeric
        /// kinds unify through the engine's promotion spelled as a cast on both arms — including int against
        /// uint, which verbatim C# refuses as CS0173 while the engine renders long — a null arm takes the
        /// other arm's type verbatim, and a mixed-nullability pair lifts identically on both tiers.</summary>
        [Fact]
        public void TernaryArmUnification_NowPrecompilesAndMatchesTheRuntime()
        {
            foreach (var (key, expression, model) in new[]
            {
                ("guard/ternary-promote.heddle", "Count > 0 ? Count : Big", new Order { Count = 3, Big = 9 }),
                ("guard/ternary-cs0173.heddle", "Count > 0 ? Count : Unsigned",
                    new Order { Count = 3, Unsigned = 7 }),
                ("guard/ternary-lifted.heddle", "Count > 0 ? Maybe : Count", new Order { Count = 3, Maybe = 5 }),
                ("guard/ternary-lifted-null.heddle", "Count > 0 ? Maybe : Count",
                    new Order { Count = 3, Maybe = null }),
                ("guard/ternary-null-arm.heddle", "Count > 0 ? null : Name", new Order { Count = 3, Name = "x" }),
                ("guard/ternary-null-arm-num.heddle", "Count > 0 ? null : Maybe",
                    new Order { Count = 3, Maybe = 5 }),
            })
            {
                var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order), model);
                Assert.Equal(dyn, precompiled);
            }
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
