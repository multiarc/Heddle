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
        public DayOfWeek? NE { get; set; }
        public Uri R { get; set; }
        public Guid O { get; set; }
        public Meas M { get; set; }
    }

    /// <summary>A user struct with SOME operators — '+', '&lt;', '&gt;' — and deliberately not their
    /// siblings, so the sweep proves Bound, Absent and Unknown witness rows against the real engine.</summary>
    public struct Meas
    {
        public int V { get; set; }

        public static Meas operator +(Meas a, Meas b) => new Meas { V = a.V + b.V };

        public static bool operator <(Meas a, Meas b) => a.V < b.V;

        public static bool operator >(Meas a, Meas b) => a.V > b.V;

        public override string ToString() => "m" + V;
    }

    /// <summary>
    /// The operator classification table. Structural guard: every ExprOperator/category pair has a verdict.
    /// Lockstep sweep: compiles and asserts Supported compiles, NotDefined errors, over all decidable pairs.
    /// RequiresRuntimeSemantics means "do not emit" (user-defined operator behavior).
    /// </summary>
    public class NativeOperatorRulesTests
    {
        private static readonly (string Expr, OperandKind Kind, Type Clr)[] Operands =
        {
            ("I32", OperandKind.Numeric(NumericKind.Int32, false), typeof(int)),
            ("U32", OperandKind.Numeric(NumericKind.UInt32, false), typeof(uint)),
            ("I64", OperandKind.Numeric(NumericKind.Int64, false), typeof(long)),
            ("U64", OperandKind.Numeric(NumericKind.UInt64, false), typeof(ulong)),
            ("Dbl", OperandKind.Numeric(NumericKind.Double, false), typeof(double)),
            ("Dec", OperandKind.Numeric(NumericKind.Decimal, false), typeof(decimal)),
            ("Ch", OperandKind.Numeric(NumericKind.Char, false), typeof(char)),
            ("B", OperandKind.Of(OperandCategory.Bool), typeof(bool)),
            ("S", OperandKind.Of(OperandCategory.String), typeof(string)),
            ("NI32", OperandKind.Numeric(NumericKind.Int32, true), typeof(int?)),
            ("NB", OperandKind.Of(OperandCategory.Bool, true), typeof(bool?)),
            ("E", OperandKind.Of(OperandCategory.Enum, false, "System.DayOfWeek"), typeof(DayOfWeek)),
            ("NE", OperandKind.Of(OperandCategory.Enum, true, "System.DayOfWeek"), typeof(DayOfWeek?)),
            // A reference and a user struct without user-defined operators, proving the verdicts for the
            // engine sites that never consult them.
            ("R", OperandKind.Of(OperandCategory.Reference, false, "System.Uri"), typeof(Uri)),
            ("O", OperandKind.Of(OperandCategory.Other, false, "System.Guid"), typeof(Guid)),
            ("M", OperandKind.Of(OperandCategory.Other, false, "Heddle.Tests.Meas"), typeof(Meas)),
            ("null", OperandKind.Null, null),
        };

        /// <summary>The test producer's <see cref="OperatorWitness"/>, from CLR reflection — the same sound
        /// claims the generator makes from symbols: Bound for an exact-signature operator declared on an
        /// operand type, Absent only when the name exists nowhere in either hierarchy.</summary>
        private static OperatorWitness Witness(ExprOperator op, Type left, Type right)
        {
            if (left == null || right == null)
                return OperatorWitness.Unknown;
            if (Nullable.GetUnderlyingType(left) != null || Nullable.GetUnderlyingType(right) != null)
                return OperatorWitness.Unknown;
            string name = OperatorMethodName(op);
            if (name == null)
                return OperatorWitness.Unknown;

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
            var candidates = left.GetMethods(flags).Concat(right.GetMethods(flags))
                .Where(m => m.Name == name && m.GetParameters().Length == 2)
                .ToList();
            if (candidates.Count == 0)
                return OperatorWitness.Absent;

            bool boolRequired = op != ExprOperator.Add && op != ExprOperator.Subtract &&
                                op != ExprOperator.Multiply && op != ExprOperator.Divide &&
                                op != ExprOperator.Modulo;
            foreach (var method in candidates)
            {
                var parameters = method.GetParameters();
                if ((method.DeclaringType == left || method.DeclaringType == right) &&
                    parameters[0].ParameterType == left && parameters[1].ParameterType == right &&
                    method.ReturnType != typeof(void) &&
                    (!boolRequired || method.ReturnType == typeof(bool)))
                    return OperatorWitness.Bound;
            }

            return OperatorWitness.Unknown;
        }

        private static string OperatorMethodName(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return "op_Addition";
                case ExprOperator.Subtract: return "op_Subtraction";
                case ExprOperator.Multiply: return "op_Multiply";
                case ExprOperator.Divide: return "op_Division";
                case ExprOperator.Modulo: return "op_Modulus";
                case ExprOperator.Equal: return "op_Equality";
                case ExprOperator.NotEqual: return "op_Inequality";
                case ExprOperator.LessThan: return "op_LessThan";
                case ExprOperator.LessThanOrEqual: return "op_LessThanOrEqual";
                case ExprOperator.GreaterThan: return "op_GreaterThan";
                case ExprOperator.GreaterThanOrEqual: return "op_GreaterThanOrEqual";
                default: return null;
            }
        }

        /// <summary>The test producer's <see cref="TypeRelation"/> — the engine's own unification facts,
        /// from CLR reflection.</summary>
        private static TypeRelation Relate(Type left, Type right)
        {
            if (left == null || right == null)
                return TypeRelation.Unknown;
            var leftU = Nullable.GetUnderlyingType(left) ?? left;
            var rightU = Nullable.GetUnderlyingType(right) ?? right;
            if (leftU == rightU)
                return TypeRelation.Identical;
            if (!leftU.IsValueType && !rightU.IsValueType)
            {
                if (rightU.IsAssignableFrom(leftU))
                    return TypeRelation.LeftWidensToRight;
                if (leftU.IsAssignableFrom(rightU))
                    return TypeRelation.RightWidensToLeft;
            }

            return TypeRelation.None;
        }

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
                // A non-nullable value side between BCL-shaped operands is the engine's HED1008 outright;
                // a user type beside it may carry an equality operator, so that pair stays runtime-owned.
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, i32, str));
                Assert.Equal(OperatorVerdict.NotDefined,
                    NativeOperatorRules.Classify(op, OperandKind.Of(OperandCategory.Enum), i32));
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics,
                    NativeOperatorRules.Classify(op, i32, reference));
                Assert.Equal(OperatorVerdict.RequiresRuntimeSemantics,
                    NativeOperatorRules.Classify(op, OperandKind.Of(OperandCategory.Enum),
                        OperandKind.Of(OperandCategory.Enum)));
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
            var sameEnum = OperandKind.Of(OperandCategory.Enum, false, "System.DayOfWeek");
            var otherEnum = OperandKind.Of(OperandCategory.Enum, false, "System.ConsoleColor");
            foreach (var op in new[] { ExprOperator.And, ExprOperator.Or, ExprOperator.ExclusiveOr })
            {
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, e, i32));
                // Identity decides the enum pair; without it the shape stays runtime-owned.
                Assert.Equal(OperatorVerdict.Supported, NativeOperatorRules.Classify(op, sameEnum, sameEnum));
                Assert.Equal(OperatorVerdict.NotDefined, NativeOperatorRules.Classify(op, sameEnum, otherEnum));
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
                    var verdict = NativeOperatorRules.Classify(op, left.Kind, right.Kind,
                        Relate(left.Clr, right.Clr), Witness(op, left.Clr, right.Clr));
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
            foreach (var trueArm in Operands)
            foreach (var falseArm in Operands)
            {
                var verdict = NativeOperatorRules.ClassifyTernary(condition.Kind, trueArm.Kind, falseArm.Kind,
                    Relate(trueArm.Clr, falseArm.Clr));
                if (verdict == OperatorVerdict.RequiresRuntimeSemantics)
                    continue;
                Check(failures, $"{condition.Expr} ? {trueArm.Expr} : {falseArm.Expr}", verdict);
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
