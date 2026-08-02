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
    /// The operator classification table. Structural guard: every ExprOperator/category pair has a verdict.
    /// Lockstep sweep: compiles and asserts Supported compiles, NotDefined errors, over all decidable pairs.
    /// RequiresRuntimeSemantics means "do not emit" (user-defined operator behavior).
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

        [Fact]
        public void Deviation1_MixedEqualityEmitsThroughTheAdapterWhereTheEnginesChainIsTotal()
        {
            var i32 = OperandKind.Numeric(NumericKind.Int32, false);
            var ni32 = OperandKind.Numeric(NumericKind.Int32, true);
            var str = OperandKind.Of(OperandCategory.String);
            var reference = OperandKind.Of(OperandCategory.Reference);
            foreach (var op in new[] { ExprOperator.Equal, ExprOperator.NotEqual })
            {
                // A non-nullable value side can still reach the engine's HED1008, so it stays runtime-owned…
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics, NativeOperatorRules.Classify(op, i32, str));
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics,
                    NativeOperatorRules.Classify(op, OperandKind.Of(OperandCategory.Enum), i32));
                // …while a pair that is null-assignable on BOTH sides runs the engine's total fallback chain,
                // replayed by RuntimeOperators over the same static types.
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, reference, reference));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, str, reference));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, ni32, reference));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, i32, i32));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, str, OperandKind.Null));
            }

            // The adapter routing is scoped to the mixed pairs: the verbatim shapes keep the C# operator.
            Assert.True(NativeOperatorRules.EqualityViaAdapter(reference, reference));
            Assert.True(NativeOperatorRules.EqualityViaAdapter(str, ni32));
            Assert.False(NativeOperatorRules.EqualityViaAdapter(i32, i32));
            Assert.False(NativeOperatorRules.EqualityViaAdapter(str, str));
            Assert.False(NativeOperatorRules.EqualityViaAdapter(str, OperandKind.Null));
            Assert.False(NativeOperatorRules.EqualityViaAdapter(OperandKind.Unknown, reference));
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
        /// A mismatched-nullability bool pair under a bitwise operator is refused with the same positioned
        /// diagnostic every other illegal operand pair in the tier carries, and with the verdict the shared
        /// operator table already published for the shape — so the runtime and the table now answer alike.
        /// <para>Matched nullability on both sides stays legal, which is what keeps this a refusal of the
        /// mismatch rather than a refusal of nullable bool.</para>
        /// </summary>
        [Fact]
        public void NullableBoolBitwise_MismatchedNullability_IsAPositionedBinaryOperatorError()
        {
            var b = OperandKind.Of(OperandCategory.Bool);
            var nb = OperandKind.Of(OperandCategory.Bool, true);
            foreach (var op in new[] { ExprOperator.And, ExprOperator.Or, ExprOperator.ExclusiveOr })
            {
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, b, b));
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, nb, nb));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, b, nb));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, nb, b));
            }

            foreach (var expression in new[] { "B & NB", "NB & B", "B | NB", "NB | B", "B ^ NB", "NB ^ B" })
            {
                var result = Compile(expression);
                Assert.False(result.Success);

                // Positioned and identified, not an escaping throw and not the catch-all.
                var error = Assert.Single(result.ErrorList);
                Assert.Equal(HeddleDiagnosticIds.BinaryOperatorNotDefined, error.DiagnosticId);
                Assert.Null(error.Exception);
                Assert.DoesNotContain(result.ErrorList,
                    e => e.DiagnosticId == HeddleDiagnosticIds.CompilationFailed);
                Assert.Contains("bool", error.Error);
                Assert.Contains("bool?", error.Error);
                Assert.True(error.Position.Length > 0);
            }

            // The sibling mismatch that was already guarded, for contrast — same operand pair, same diagnostic.
            var equality = Compile("B == NB");
            Assert.False(equality.Success);
            Assert.Contains(equality.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.BinaryOperatorNotDefined);
        }

        /// <summary>The near-neighbours the refusal must not take with it: a matched pair on either nullability,
        /// under every bitwise operator, still compiles and evaluates.</summary>
        [Fact]
        public void BitwiseBoolPairsOfMatchingNullability_StillCompile()
        {
            foreach (var expression in new[] { "B & B", "B | B", "B ^ B", "NB & NB", "NB | NB", "NB ^ NB" })
            {
                var result = Compile(expression);
                Assert.True(result.Success, expression + " => " + string.Join("; ", result.ErrorList));
            }
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
