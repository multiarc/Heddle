namespace Heddle.Language.Expressions
{
    /// <summary>What the shared operator table says about one operator applied to one pair of operand kinds.</summary>
    internal enum OperatorVerdict
    {
        /// <summary>Verbatim C# emission is provably byte-equivalent to the runtime result — the generator may
        /// emit.</summary>
        Supported,

        /// <summary>Legal in the native tier, but its semantics are runtime-owned (one of the documented deviations,
        /// or an operand whose facts the table cannot decide). The generator degrades; the dynamic tier evaluates it
        /// with the runtime's own compiler.</summary>
        RequiresRuntimeSemantics,

        /// <summary>The native tier rejects it with a positioned error. The generator degrades, and the dynamic tier
        /// raises that same error — so the two tiers reach the same verdict rather than opposite ones.</summary>
        NotDefined
    }

    /// <summary>
    /// A decision table encoding how the native tier handles the seven documented deviations from C# semantics.
    /// Before this file the runtime implemented them by hand while the generator emitted <c>(left op right)</c>
    /// unconditionally, consulting no operand types — so mixed-type equality produced <b>CS0019 in the consumer's
    /// build</b> for a template the runtime accepts, while enum arithmetic produced valid C# that renders where the
    /// runtime raises a positioned error. Opposite verdicts, silently.
    /// <para>The generator emits only on <see cref="OperatorVerdict.Supported"/>; both other verdicts degrade to the
    /// dynamic tier. The runtime builds <c>Expression</c> trees and gains a lockstep sweep asserting that
    /// <see cref="OperatorVerdict.Supported"/> really compiles and <see cref="OperatorVerdict.NotDefined"/> really
    /// raises a positioned error.</para>
    /// <para><b>Exactness boundary.</b> The verdicts are exact over the decidable categories — <c>Numeric</c>,
    /// <c>Bool</c>, <c>String</c>, <c>Enum</c>, <c>NullLiteral</c>. For <c>Reference</c>/<c>Other</c> the outcome
    /// depends on user-defined operators the descriptor deliberately does not carry, and for <c>Unknown</c> there
    /// are no facts at all; both answer <see cref="OperatorVerdict.RequiresRuntimeSemantics"/>, which means "do not
    /// emit" and claims nothing about whether the runtime compiles it.</para>
    /// </summary>
    internal static class NativeOperatorRules
    {
        #region Binary

        /// <summary>Classifies <paramref name="op"/> in binary position over the two operand kinds.</summary>
        public static OperatorVerdict Classify(ExprOperator op, in OperandKind left, in OperandKind right)
        {
            if (left.Category == OperandCategory.Unknown || right.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;

            switch (op)
            {
                case ExprOperator.Add:
                case ExprOperator.Subtract:
                case ExprOperator.Multiply:
                case ExprOperator.Divide:
                case ExprOperator.Modulo:
                    return ClassifyArithmetic(op, left, right);
                case ExprOperator.LeftShift:
                case ExprOperator.RightShift:
                    return ClassifyShift(left, right);
                case ExprOperator.LessThan:
                case ExprOperator.LessThanOrEqual:
                case ExprOperator.GreaterThan:
                case ExprOperator.GreaterThanOrEqual:
                    return ClassifyRelational(left, right);
                case ExprOperator.Equal:
                case ExprOperator.NotEqual:
                    return ClassifyEquality(left, right);
                case ExprOperator.And:
                case ExprOperator.ExclusiveOr:
                case ExprOperator.Or:
                    return ClassifyBitwise(left, right);
                case ExprOperator.AndAlso:
                case ExprOperator.OrElse:
                    return ClassifyLogical(left, right);
                case ExprOperator.Coalesce:
                    return ClassifyCoalesce(left, right);
                default:
                    // A unary-only operator in binary position: never emitted.
                    return OperatorVerdict.NotDefined;
            }
        }

        private static OperatorVerdict ClassifyArithmetic(ExprOperator op, in OperandKind left, in OperandKind right)
        {
            if (op == ExprOperator.Add &&
                (left.Category == OperandCategory.String || right.Category == OperandCategory.String))
            {
                var other = left.Category == OperandCategory.String ? right : left;
                switch (other.Category)
                {
                    case OperandCategory.String:
                    case OperandCategory.Numeric:
                    case OperandCategory.Bool:
                    case OperandCategory.NullLiteral:
                        return OperatorVerdict.Supported;   // string.Concat on both sides, same text
                    default:
                        // Formatting the non-string side is runtime-owned (Dev 6 reaches enums and user types).
                        return OperatorVerdict.RequiresRuntimeSemantics;
                }
            }

            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            // Enum arithmetic is not supported by the native tier — the runtime raises a positioned error
            // while generated C# would happily render.
            if (left.Category == OperandCategory.Enum || right.Category == OperandCategory.Enum)
                return OperatorVerdict.NotDefined;

            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
            {
                return NumericTable.TryPromote(left.Kind, right.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;   // the two illegal mixes — HED1008 class
            }

            return OperatorVerdict.NotDefined;
        }

        private static OperatorVerdict ClassifyShift(in OperandKind left, in OperandKind right)
        {
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            bool leftShiftable = left.Category == OperandCategory.Numeric && NumericTable.IsIntegral(left.Kind);
            bool rightIntegral = right.Category == OperandCategory.Numeric && NumericTable.IsIntegral(right.Kind);
            if (!leftShiftable || !rightIntegral)
                return OperatorVerdict.NotDefined;

            // C# accepts only an int-typed (or implicitly-int) shift count and no lifted form; the runtime converts
            // any integral count to int and lifts freely, so everything outside that intersection degrades.
            bool countIsInt = right.Kind == NumericKind.Int32 || NumericTable.IsImplicit(right.Kind, NumericKind.Int32);
            if (left.IsNullable || right.IsNullable || !countIsInt)
                return OperatorVerdict.RequiresRuntimeSemantics;

            return OperatorVerdict.Supported;
        }

        private static OperatorVerdict ClassifyRelational(in OperandKind left, in OperandKind right)
        {
            if (IsUndecidable(left) || IsUndecidable(right) ||
                left.Category == OperandCategory.Enum || right.Category == OperandCategory.Enum)
                return OperatorVerdict.RequiresRuntimeSemantics;

            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
            {
                return NumericTable.TryPromote(left.Kind, right.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            return OperatorVerdict.NotDefined;   // null, bool, string and cross-category all error in the runtime
        }

        private static OperatorVerdict ClassifyEquality(in OperandKind left, in OperandKind right)
        {
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            bool leftNull = left.Category == OperandCategory.NullLiteral;
            bool rightNull = right.Category == OperandCategory.NullLiteral;
            if (leftNull && rightNull)
                return OperatorVerdict.RequiresRuntimeSemantics;   // Runtime folds this to a constant; C# would report CS0019
            if (leftNull || rightNull)
            {
                var other = leftNull ? right : left;
                return other.IsNullAssignable ? OperatorVerdict.Supported : OperatorVerdict.NotDefined;
            }

            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
            {
                return NumericTable.TryPromote(left.Kind, right.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            if (left.Category == OperandCategory.Bool && right.Category == OperandCategory.Bool)
            {
                // bool vs bool? has no lifted equality in the native tier — mismatched nullability degrades.
                return left.IsNullable == right.IsNullable
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            if (left.Category == OperandCategory.String && right.Category == OperandCategory.String)
                return OperatorVerdict.Supported;

            // Mixed/unrelated operands use null-safe object.Equals in the runtime, but emitted C# is either CS0019
            // or a reference comparison. Same-enum equality stays conservative without corpus proof of byte-equivalence.
            return OperatorVerdict.RequiresRuntimeSemantics;
        }

        private static OperatorVerdict ClassifyBitwise(in OperandKind left, in OperandKind right)
        {
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            if (left.Category == OperandCategory.Bool && right.Category == OperandCategory.Bool)
            {
                // bool vs bool? mismatched nullability degrades — the runtime would surface it as an unpositioned error.
                return left.IsNullable == right.IsNullable
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            bool leftEnum = left.Category == OperandCategory.Enum;
            bool rightEnum = right.Category == OperandCategory.Enum;
            if (leftEnum && rightEnum)
                return OperatorVerdict.RequiresRuntimeSemantics;   // Same-enum bitwise is runtime-owned.
            // Mixed enum forms — including the `enum & 0` special case C# carries — are not emittable.
            if (leftEnum || rightEnum)
                return OperatorVerdict.NotDefined;

            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
            {
                if (!NumericTable.IsIntegral(left.Kind) || !NumericTable.IsIntegral(right.Kind))
                    return OperatorVerdict.NotDefined;
                return NumericTable.TryPromote(left.Kind, right.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            return OperatorVerdict.NotDefined;
        }

        private static OperatorVerdict ClassifyLogical(in OperandKind left, in OperandKind right)
        {
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;
            if (left.Category != OperandCategory.Bool || right.Category != OperandCategory.Bool)
                return OperatorVerdict.NotDefined;
            // Nullable bool degrades rather than using C#'s lifted form.
            return left.IsNullable || right.IsNullable ? OperatorVerdict.NotDefined : OperatorVerdict.Supported;
        }

        private static OperatorVerdict ClassifyCoalesce(in OperandKind left, in OperandKind right)
        {
            if (left.Category == OperandCategory.NullLiteral)
                return OperatorVerdict.RequiresRuntimeSemantics;
            if (!left.IsNullAssignable)
                return OperatorVerdict.NotDefined;   // '??' needs a reference or Nullable<T> left operand
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            if (right.Category == OperandCategory.NullLiteral)
                return OperatorVerdict.Supported;    // Coalesce(x, null-of-x's-type) on both sides

            if (left.Category == OperandCategory.String)
                return right.Category == OperandCategory.String
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.RequiresRuntimeSemantics;

            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
            {
                if (left.Kind == right.Kind)
                    return OperatorVerdict.Supported;
                return NumericTable.TryPromote(left.Kind, right.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            if (left.Category == OperandCategory.Bool && right.Category == OperandCategory.Bool)
                return OperatorVerdict.Supported;

            return OperatorVerdict.RequiresRuntimeSemantics;   // coalesce unification is runtime-owned
        }

        #endregion

        #region Unary and ternary

        /// <summary>Classifies <paramref name="op"/> in unary position.</summary>
        public static OperatorVerdict ClassifyUnary(ExprOperator op, in OperandKind operand)
        {
            if (IsUndecidable(operand) || operand.Category == OperandCategory.Enum)
                return OperatorVerdict.RequiresRuntimeSemantics;

            switch (op)
            {
                case ExprOperator.Not:
                    if (operand.Category != OperandCategory.Bool)
                        return OperatorVerdict.NotDefined;
                    return operand.IsNullable ? OperatorVerdict.RequiresRuntimeSemantics : OperatorVerdict.Supported;

                case ExprOperator.Negate:
                    if (operand.Category != OperandCategory.Numeric)
                        return OperatorVerdict.NotDefined;
                    // No negation of ulong on either side.
                    return operand.Kind == NumericKind.UInt64 ? OperatorVerdict.NotDefined : OperatorVerdict.Supported;

                case ExprOperator.UnaryPlus:
                    return operand.Category == OperandCategory.Numeric
                        ? OperatorVerdict.Supported
                        : OperatorVerdict.NotDefined;

                case ExprOperator.OnesComplement:
                    if (operand.Category != OperandCategory.Numeric)
                        return OperatorVerdict.NotDefined;
                    return NumericTable.IsIntegral(operand.Kind)
                        ? OperatorVerdict.Supported
                        : OperatorVerdict.NotDefined;

                default:
                    // A binary-only operator in unary position: never emitted.
                    return OperatorVerdict.NotDefined;
            }
        }

        /// <summary>Classifies <c>?:</c>. Only the trivial-unification case is emittable — arm unification is
        /// runtime-owned, and the descriptor deliberately carries no reference-type identity, so two operands that
        /// merely share a category are never treated as the same type.</summary>
        public static OperatorVerdict ClassifyTernary(in OperandKind condition, in OperandKind whenTrue,
            in OperandKind whenFalse)
        {
            if (condition.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;
            if (condition.Category != OperandCategory.Bool || condition.IsNullable)
                return OperatorVerdict.NotDefined;
            if (IsUndecidable(whenTrue) || IsUndecidable(whenFalse))
                return OperatorVerdict.RequiresRuntimeSemantics;
            if (whenTrue.Category == OperandCategory.NullLiteral && whenFalse.Category == OperandCategory.NullLiteral)
                return OperatorVerdict.NotDefined;   // no common type on either side

            bool identical = whenTrue.Category == whenFalse.Category && whenTrue.Kind == whenFalse.Kind &&
                             whenTrue.IsNullable == whenFalse.IsNullable;
            if (!identical)
                return OperatorVerdict.RequiresRuntimeSemantics;

            switch (whenTrue.Category)
            {
                case OperandCategory.Numeric:
                case OperandCategory.Bool:
                case OperandCategory.String:
                    return OperatorVerdict.Supported;
                default:
                    return OperatorVerdict.RequiresRuntimeSemantics;
            }
        }

        #endregion

        #region Result kinds (the estimator's promotion arithmetic)

        /// <summary>The kind of the value a <see cref="OperatorVerdict.Supported"/> binary emission produces, or
        /// <see cref="OperandKind.Unknown"/> when the operator would not be emitted at all — so a sub-expression the
        /// guard refuses can never contribute a kind that makes its parent emittable.</summary>
        public static OperandKind BinaryResult(ExprOperator op, in OperandKind left, in OperandKind right)
        {
            if (Classify(op, left, right) != OperatorVerdict.Supported)
                return OperandKind.Unknown;

            bool lifted = left.IsNullable || right.IsNullable;
            switch (op)
            {
                case ExprOperator.Add:
                    if (left.Category == OperandCategory.String || right.Category == OperandCategory.String)
                        return OperandKind.Of(OperandCategory.String);
                    goto case ExprOperator.Subtract;
                case ExprOperator.Subtract:
                case ExprOperator.Multiply:
                case ExprOperator.Divide:
                case ExprOperator.Modulo:
                    NumericTable.TryPromote(left.Kind, right.Kind, out var arithmetic);
                    return OperandKind.Numeric(arithmetic, lifted);

                case ExprOperator.LeftShift:
                case ExprOperator.RightShift:
                    return OperandKind.Numeric(ShiftKind(left.Kind), false);

                case ExprOperator.LessThan:
                case ExprOperator.LessThanOrEqual:
                case ExprOperator.GreaterThan:
                case ExprOperator.GreaterThanOrEqual:
                case ExprOperator.Equal:
                case ExprOperator.NotEqual:
                case ExprOperator.AndAlso:
                case ExprOperator.OrElse:
                    return OperandKind.Of(OperandCategory.Bool);

                case ExprOperator.And:
                case ExprOperator.ExclusiveOr:
                case ExprOperator.Or:
                    if (left.Category == OperandCategory.Bool)
                        return OperandKind.Of(OperandCategory.Bool, lifted);
                    NumericTable.TryPromote(left.Kind, right.Kind, out var bitwise);
                    return OperandKind.Numeric(bitwise, lifted);

                case ExprOperator.Coalesce:
                    return CoalesceResult(left, right);

                default:
                    return OperandKind.Unknown;
            }
        }

        private static OperandKind CoalesceResult(in OperandKind left, in OperandKind right)
        {
            if (right.Category == OperandCategory.NullLiteral)
                return left;
            if (left.Category == OperandCategory.String)
                return OperandKind.Of(OperandCategory.String);
            if (left.Category == OperandCategory.Bool)
                return OperandKind.Of(OperandCategory.Bool, right.IsNullable);
            if (left.Category != OperandCategory.Numeric)
                return OperandKind.Unknown;
            if (left.Kind == right.Kind)
                return OperandKind.Numeric(left.Kind, right.IsNullable);
            NumericTable.TryPromote(left.Kind, right.Kind, out var promoted);
            return OperandKind.Numeric(promoted, right.IsNullable);
        }

        /// <summary>The shift result type: the left operand's own kind for the three wide integrals, <c>int</c>
        /// otherwise — the runtime's <c>shiftType</c> rule, which is also C#'s.</summary>
        private static NumericKind ShiftKind(NumericKind left)
        {
            switch (left)
            {
                case NumericKind.Int64:
                case NumericKind.UInt64:
                case NumericKind.UInt32:
                    return left;
                default:
                    return NumericKind.Int32;
            }
        }

        /// <summary>The kind a <see cref="OperatorVerdict.Supported"/> unary emission produces.</summary>
        public static OperandKind UnaryResult(ExprOperator op, in OperandKind operand)
        {
            if (ClassifyUnary(op, operand) != OperatorVerdict.Supported)
                return OperandKind.Unknown;

            switch (op)
            {
                case ExprOperator.Not:
                    return OperandKind.Of(OperandCategory.Bool);
                case ExprOperator.Negate:
                    // The runtime widens uint to long before negating, exactly as C# does.
                    return OperandKind.Numeric(
                        operand.Kind == NumericKind.UInt32 ? NumericKind.Int64 : NumericTable.UnaryPromote(operand.Kind),
                        operand.IsNullable);
                case ExprOperator.UnaryPlus:
                case ExprOperator.OnesComplement:
                    return OperandKind.Numeric(NumericTable.UnaryPromote(operand.Kind), operand.IsNullable);
                default:
                    return OperandKind.Unknown;
            }
        }

        /// <summary>The kind a <see cref="OperatorVerdict.Supported"/> ternary emission produces — the arms are
        /// identical by construction there.</summary>
        public static OperandKind TernaryResult(in OperandKind whenTrue, in OperandKind whenFalse)
        {
            return ClassifyTernary(OperandKind.Of(OperandCategory.Bool), whenTrue, whenFalse) ==
                   OperatorVerdict.Supported
                ? whenTrue
                : OperandKind.Unknown;
        }

        #endregion

        /// <summary>The categories whose operator behavior the table cannot decide from the descriptor alone: a
        /// user-defined operator or conversion on a reference/struct type changes the answer, and Unknown has no
        /// facts at all.</summary>
        private static bool IsUndecidable(in OperandKind kind)
        {
            switch (kind.Category)
            {
                case OperandCategory.Unknown:
                case OperandCategory.Reference:
                case OperandCategory.Other:
                    return true;
                default:
                    return false;
            }
        }
    }
}
