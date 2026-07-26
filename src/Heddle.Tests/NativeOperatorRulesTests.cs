using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language.Expressions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The lockstep sweep's model: one readable property per operand kind the shared table
    /// distinguishes.</summary>
    public sealed class OperandModel
    {
        public int I32 { get; set; }
        public uint U32 { get; set; }
        public long I64 { get; set; }
        public ulong U64 { get; set; }
        public double Dbl { get; set; }
        public decimal Dec { get; set; }
        public char Ch { get; set; }
        public bool B { get; set; }
        public string S { get; set; }
        public int? NI32 { get; set; }
        public bool? NB { get; set; }
        public DayOfWeek E { get; set; }
    }

    /// <summary>
    /// The operator classification table.
    /// <para>Two guards. The <b>structural</b> one enumerates <see cref="ExprOperator"/> and demands a verdict for
    /// every member over the category cross-product, so a new operator cannot ship without a row. The
    /// <b>lockstep sweep</b> then compiles the real thing: for every operator over every operand pair in the
    /// decidable categories, it asserts that <see cref="OperatorVerdict.Supported"/> really does compile in the
    /// native tier and <see cref="OperatorVerdict.NotDefined"/> really does raise a positioned error — the two
    /// verdicts the generator acts on. <see cref="OperatorVerdict.RequiresRuntimeSemantics"/> means only "do not
    /// emit" and is deliberately unconstrained: it is the answer for operands whose behavior depends on user-defined
    /// operators the descriptor does not carry.</para>
    /// </summary>
    public class NativeOperatorRulesTests
    {
        private static readonly (string Expr, OperandKind Kind)[] Operands =
        {
            ("I32", OperandKind.Numeric(NumericKind.Int32, false)),
            ("U32", OperandKind.Numeric(NumericKind.UInt32, false)),
            ("I64", OperandKind.Numeric(NumericKind.Int64, false)),
            ("U64", OperandKind.Numeric(NumericKind.UInt64, false)),
            ("Dbl", OperandKind.Numeric(NumericKind.Double, false)),
            ("Dec", OperandKind.Numeric(NumericKind.Decimal, false)),
            ("Ch", OperandKind.Numeric(NumericKind.Char, false)),
            ("B", OperandKind.Of(OperandCategory.Bool)),
            ("S", OperandKind.Of(OperandCategory.String)),
            ("NI32", OperandKind.Numeric(NumericKind.Int32, true)),
            ("NB", OperandKind.Of(OperandCategory.Bool, true)),
            ("E", OperandKind.Of(OperandCategory.Enum)),
            ("null", OperandKind.Null),
        };

        private static readonly ExprOperator[] BinaryOperators = Enum.GetValues(typeof(ExprOperator))
            .Cast<ExprOperator>().Where(OperatorLexeme.IsBinary).ToArray();

        private static readonly ExprOperator[] UnaryOperators = Enum.GetValues(typeof(ExprOperator))
            .Cast<ExprOperator>().Where(OperatorLexeme.IsUnary).ToArray();

        private static IEnumerable<OperandKind> AllCategories()
        {
            foreach (var operand in Operands)
                yield return operand.Kind;
            yield return OperandKind.Unknown;
            yield return OperandKind.Of(OperandCategory.Reference);
            yield return OperandKind.Of(OperandCategory.Other);
        }

        [Fact]
        public void EveryOperatorHasAVerdictForEveryOperandPair()
        {
            var verdicts = Enum.GetValues(typeof(OperatorVerdict)).Cast<OperatorVerdict>().ToArray();
            foreach (ExprOperator op in Enum.GetValues(typeof(ExprOperator)))
            foreach (var left in AllCategories())
            foreach (var right in AllCategories())
            {
                Assert.Contains(NativeOperatorRules.Classify(op, left, right), verdicts);
                Assert.Contains(NativeOperatorRules.ClassifyUnary(op, left), verdicts);
                Assert.Contains(NativeOperatorRules.ClassifyTernary(left, right, right), verdicts);
            }
        }

        [Fact]
        public void UnknownOperandAlwaysDegrades()
        {
            foreach (ExprOperator op in Enum.GetValues(typeof(ExprOperator)))
            foreach (var other in AllCategories())
            {
                Assert.NotEqual(OperatorVerdict.Supported,
                    NativeOperatorRules.Classify(op, OperandKind.Unknown, other));
                Assert.NotEqual(OperatorVerdict.Supported,
                    NativeOperatorRules.Classify(op, other, OperandKind.Unknown));
                Assert.NotEqual(OperatorVerdict.Supported,
                    NativeOperatorRules.ClassifyUnary(op, OperandKind.Unknown));
            }
        }

        [Fact]
        public void AUnaryOnlyOperatorIsNeverEmittableInBinaryPosition_AndViceVersa()
        {
            var numeric = OperandKind.Numeric(NumericKind.Int32, false);
            foreach (var op in UnaryOperators)
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, numeric, numeric));
            foreach (var op in BinaryOperators)
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.ClassifyUnary(op, numeric));
        }

        // ---- The seven documented deviations, as data ------------------------------------------------------

        [Fact]
        public void Deviation1_MixedAndUnrelatedEqualityIsNeverEmitted()
        {
            var i32 = OperandKind.Numeric(NumericKind.Int32, false);
            var str = OperandKind.Of(OperandCategory.String);
            var reference = OperandKind.Of(OperandCategory.Reference);
            foreach (var op in new[] { ExprOperator.Equal, ExprOperator.NotEqual })
            {
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics, NativeOperatorRules.Classify(op, i32, str));
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics,
                    NativeOperatorRules.Classify(op, reference, reference));
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics,
                    NativeOperatorRules.Classify(op, OperandKind.Of(OperandCategory.Enum), i32));
                // …while the shapes that really are identical stay emittable.
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, i32, i32));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, str, OperandKind.Null));
            }
        }

        [Fact]
        public void Deviation4_EnumArithmeticIsNotDefined()
        {
            var e = OperandKind.Of(OperandCategory.Enum);
            var i32 = OperandKind.Numeric(NumericKind.Int32, false);
            foreach (var op in new[] { ExprOperator.Add, ExprOperator.Subtract, ExprOperator.Multiply,
                         ExprOperator.Divide, ExprOperator.Modulo })
            {
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, e, i32));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, i32, e));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, e, e));
            }
        }

        [Fact]
        public void Deviation5_TheEnumAndZeroLiteralCaseIsNotCarriedOver()
        {
            var e = OperandKind.Of(OperandCategory.Enum);
            var i32 = OperandKind.Numeric(NumericKind.Int32, false);
            foreach (var op in new[] { ExprOperator.And, ExprOperator.Or, ExprOperator.ExclusiveOr })
            {
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, e, i32));
                // Same-enum bitwise is legal in the native tier but stays runtime-owned until corpus-proven.
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics, NativeOperatorRules.Classify(op, e, e));
            }
        }

        [Fact]
        public void Deviation6_UserOperatorBearingOperandsAreNeverEmitted()
        {
            var other = OperandKind.Of(OperandCategory.Other);
            var i32 = OperandKind.Numeric(NumericKind.Int32, false);
            foreach (var op in BinaryOperators)
            {
                Assert.NotEqual(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, other, i32));
                Assert.NotEqual(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, other, other));
            }
        }

        [Fact]
        public void Deviation7_NullableBoolLogicalsAreNotDefined()
        {
            var b = OperandKind.Of(OperandCategory.Bool);
            var nb = OperandKind.Of(OperandCategory.Bool, true);
            foreach (var op in new[] { ExprOperator.AndAlso, ExprOperator.OrElse })
            {
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, b, b));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, nb, b));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, b, nb));
            }
        }

        /// <summary>
        /// The one runtime defect this phase found and deliberately left in place, pinned as a defect
        /// (phase-4 audit, 2026-07-26). `bool &amp; bool?` reaches <c>Expression.And</c> with no guard — the
        /// bitwise path's bool arm tests the <b>underlying</b> types, so a mismatched lifted pair walks straight
        /// into the factory — and the throw is contained by the compile-item catch, surfacing as an
        /// <c>Error while compiling</c> with an exception attached instead of the positioned HED1008 the sibling
        /// mismatch (<c>bool == bool?</c>) gets.
        /// <para>What this pin buys: the containment is now a test, not prose. If the blast radius ever widens —
        /// the throw escaping compilation, or reaching render — this reddens. Fixing the shape (a real HED1008)
        /// also reddens it, which is the intended prompt to move the row deliberately rather than by accident.
        /// The table already answers <see cref="OperatorVerdict.NotDefined"/> for the pair, so no generated code
        /// can reach it; before phase 4 the generator emitted C#'s lifted `&amp;` and rendered.</para>
        /// </summary>
        [Fact]
        public void NullableBoolBitwise_IsAContainedRuntimeDefect_NotAPositionedError()
        {
            var b = OperandKind.Of(OperandCategory.Bool);
            var nb = OperandKind.Of(OperandCategory.Bool, true);
            foreach (var op in new[] { ExprOperator.And, ExprOperator.Or, ExprOperator.ExclusiveOr })
            {
                // Matched pairs stay emittable; only the mismatch is refused.
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, b, b));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, nb, nb));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, b, nb));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, nb, b));
            }

            var result = Compile("B & NB");
            Assert.False(result.Success);
            // Contained: the compile-item catch turns it into a collected error, never an escaping throw and never
            // a render. Documented as a diagnostic-shape defect out of phase 4's scope.
            Assert.Contains(result.ErrorList, e => e.Exception != null);
            Assert.DoesNotContain(result.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.BinaryOperatorNotDefined);

            // The sibling mismatch the runtime *does* guard, for contrast — same operand pair, real diagnostic.
            var equality = Compile("B == NB");
            Assert.False(equality.Success);
            Assert.Contains(equality.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        [Fact]
        public void IllegalPromotionsAreNotDefined()
        {
            var dec = OperandKind.Numeric(NumericKind.Decimal, false);
            var dbl = OperandKind.Numeric(NumericKind.Double, false);
            var u64 = OperandKind.Numeric(NumericKind.UInt64, false);
            var i32 = OperandKind.Numeric(NumericKind.Int32, false);
            Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(ExprOperator.Add, dec, dbl));
            Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(ExprOperator.Add, u64, i32));
            Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(ExprOperator.LessThan, dec, dbl));
        }

        [Fact]
        public void BinaryVerdictsPredictTheCompilersOutcome()
        {
            var failures = new List<string>();
            foreach (var op in BinaryOperators)
            {
                var lexeme = OperatorLexeme.ForBinary(op);
                foreach (var left in Operands)
                foreach (var right in Operands)
                {
                    var verdict = NativeOperatorRules.Classify(op, left.Kind, right.Kind);
                    if (verdict == OperatorVerdict.RequiresRuntimeSemantics)
                        continue;   // "do not emit" — claims nothing about the runtime's own outcome
                    Check(failures, $"{left.Expr} {lexeme} {right.Expr}", verdict);
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        [Fact]
        public void UnaryAndTernaryVerdictsPredictTheCompilersOutcome()
        {
            var failures = new List<string>();
            foreach (var op in UnaryOperators)
            {
                var lexeme = OperatorLexeme.ForUnary(op);
                foreach (var operand in Operands)
                {
                    var verdict = NativeOperatorRules.ClassifyUnary(op, operand.Kind);
                    if (verdict == OperatorVerdict.RequiresRuntimeSemantics)
                        continue;
                    Check(failures, lexeme + operand.Expr, verdict);
                }
            }

            foreach (var condition in Operands)
            foreach (var arm in Operands)
            {
                var verdict = NativeOperatorRules.ClassifyTernary(condition.Kind, arm.Kind, arm.Kind);
                if (verdict == OperatorVerdict.RequiresRuntimeSemantics)
                    continue;
                Check(failures, $"{condition.Expr} ? {arm.Expr} : {arm.Expr}", verdict);
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        private static void Check(List<string> failures, string expression, OperatorVerdict verdict)
        {
            var result = Compile(expression);
            bool compiled = result.Success;
            if (verdict == OperatorVerdict.Supported && !compiled)
                failures.Add($"Supported but the runtime rejected it: @({expression}) — {result}");
            if (verdict == OperatorVerdict.NotDefined && compiled)
                failures.Add($"NotDefined but the runtime compiled it: @({expression})");
        }

        private static HeddleCompileResult Compile(string expression)
        {
            var template = new HeddleTemplate("@(" + expression + ")",
                new CompileContext(new TemplateOptions(), typeof(OperandModel)));
            return template.CompileResult;
        }
    }
}
