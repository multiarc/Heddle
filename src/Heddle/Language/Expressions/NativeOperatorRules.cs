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
    /// Decision table for native-tier operator semantics. Ensures generator and runtime reach the same verdict
    /// where exact, or both degrade where not. Exact for Numeric, Bool, String, Enum, NullLiteral; inexact for
    /// Reference, Other, Unknown (runtime-owned).
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
                        // Enums and user types concatenate through the same object-pair string.Concat the
                        // engine binds UNCONDITIONALLY — its EmitStringConcat never consults a user-defined
                        // operator or conversion, where verbatim C# would prefer one. The writer therefore
                        // spells the Concat call explicitly for these operands (see WriteBinary), which is
                        // the engine's exact BCL call and bypasses user operators the same way.
                        return OperatorVerdict.Supported;
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
            if (left.Category == OperandCategory.Unknown || right.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;

            // The engine gates a shift on integral operands BEFORE any factory runs — no user-operator
            // fallback exists — so a reference or user struct is HED1008 on every input, whatever it declares.
            if (MayCarryUserOperator(left) || MayCarryUserOperator(right))
                return OperatorVerdict.NotDefined;

            bool leftShiftable = left.Category == OperandCategory.Numeric && NumericTable.IsIntegral(left.Kind);
            bool rightIntegral = right.Category == OperandCategory.Numeric && NumericTable.IsIntegral(right.Kind);
            if (!leftShiftable || !rightIntegral)
                return OperatorVerdict.NotDefined;

            // Every integral count and either nullability is emittable. C# lifts a shift exactly as the
            // runtime's expression trees lift it (null in, null out), and a count outside C#'s int-only rule
            // is normalised by the writer with the truncating (int)/(int?) cast that reproduces the runtime's
            // Expression.Convert — see NativeExpressionWriter.ShiftCountSpelling. This used to degrade as
            // runtime-owned; OperatorGuardDifferentialTests' shift rows pin the byte parity that closed it.
            return OperatorVerdict.Supported;
        }

        private static OperatorVerdict ClassifyRelational(in OperandKind left, in OperandKind right)
        {
            // Unknown has no facts; a reference or user struct may carry a user-defined comparison operator
            // (with ANY partner type, an enum included) and the engine's relational tail binds it.
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            // Both sides BCL-shaped from here. Enums have no relational operator anywhere the engine looks —
            // LINQ's arithmetic comparisons exclude enums and an enum type cannot declare one — so every enum
            // pairing is the engine's HED1008, matched rather than degraded.
            if (left.Category == OperandCategory.Enum || right.Category == OperandCategory.Enum)
                return OperatorVerdict.NotDefined;

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
            if (left.Category == OperandCategory.Unknown || right.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;

            bool leftNull = left.Category == OperandCategory.NullLiteral;
            bool rightNull = right.Category == OperandCategory.NullLiteral;
            if (leftNull && rightNull)
                // The engine folds this to a constant (Expression.Constant(op)); the writer spells that
                // exact constant — 'true' for '==', 'false' for '!=' — so C#'s null-literal rules never run.
                return OperatorVerdict.Supported;
            if (leftNull || rightNull)
            {
                // Verbatim for every null-inhabitable other side, references included: C# binds the same
                // user operator (or reference test) for a null literal that the engine's typed-null
                // Expression.Equal binds.
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

            // Mixed/unrelated operands run the engine's equality TAIL — a user-defined operator where the
            // pair binds one, null-safe object.Equals for the rest. That tail is TOTAL where both sides are
            // statically null-assignable, so those pairs emit through the RuntimeOperators adapter, which
            // replays the same chain over the same static types (see EqualityViaAdapter). A pair with a
            // non-nullable value side can still reach the engine's HED1008 refusal and stays runtime-owned:
            // a refusal stated at template compile must not become a render-time throw.
            if (left.IsNullAssignable && right.IsNullAssignable)
                return OperatorVerdict.Supported;

            // From here at least one side is a non-nullable value type. An enum pair stays runtime-owned:
            // the SAME enum type binds LINQ's enum equality arm while a different type is HED1008, and the
            // descriptor carries no identity to tell them apart. A pair touching a reference or user struct
            // stays runtime-owned too — it may declare an equality operator taking the partner. Every other
            // BCL-shaped pair is a guaranteed HED1008: no user operator can exist between two primitives.
            if (left.Category == OperandCategory.Enum && right.Category == OperandCategory.Enum)
                return OperatorVerdict.RequiresRuntimeSemantics;
            if (MayCarryUserOperator(left) || MayCarryUserOperator(right))
                return OperatorVerdict.RequiresRuntimeSemantics;
            return OperatorVerdict.NotDefined;
        }

        /// <summary>Whether a <see cref="OperatorVerdict.Supported"/> equality emits through
        /// <c>Heddle.Precompiled.RuntimeOperators</c> rather than verbatim: the mixed/unrelated pairs whose
        /// semantics live in the engine's fallback chain. The verbatim shapes — a null comparison, promoted
        /// numerics, matched bools, strings — keep the C# operator.</summary>
        public static bool EqualityViaAdapter(in OperandKind left, in OperandKind right)
        {
            if (left.Category == OperandCategory.NullLiteral || right.Category == OperandCategory.NullLiteral)
                return false;
            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
                return false;
            if (left.Category == OperandCategory.Bool && right.Category == OperandCategory.Bool)
                return false;
            if (left.Category == OperandCategory.String && right.Category == OperandCategory.String)
                return false;
            if (left.Category == OperandCategory.Unknown || right.Category == OperandCategory.Unknown)
                return false;
            return left.IsNullAssignable && right.IsNullAssignable;
        }

        private static OperatorVerdict ClassifyBitwise(in OperandKind left, in OperandKind right)
        {
            if (left.Category == OperandCategory.Unknown || right.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;

            // The engine's bitwise visitor has exactly three arms — matched bools, one enum type,
            // integrals — and no user-operator fallback at all: a reference or user struct is HED1008 on
            // every input, whatever the partner and whatever it declares.
            if (MayCarryUserOperator(left) || MayCarryUserOperator(right))
                return OperatorVerdict.NotDefined;

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
            if (left.Category == OperandCategory.Unknown || right.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;
            // The engine demands exactly bool — no op_true, no implicit conversion is consulted — so every
            // non-bool operand (references and user structs included) is HED1005 on every input.
            if (left.Category != OperandCategory.Bool || right.Category != OperandCategory.Bool)
                return OperatorVerdict.NotDefined;
            // Nullable bool degrades rather than using C#'s lifted form.
            return left.IsNullable || right.IsNullable ? OperatorVerdict.NotDefined : OperatorVerdict.Supported;
        }

        private static OperatorVerdict ClassifyCoalesce(in OperandKind left, in OperandKind right)
        {
            if (left.Category == OperandCategory.NullLiteral)
                // The engine coalesces a typed object null against ANY right operand — a boxing or reference
                // conversion to object always exists — and the whole expression is just the right operand
                // boxed. The writer spells exactly that: ((object)(right)). The result type is object, which
                // CoalesceResult reports as Unknown so no parent overclaims it.
                return OperatorVerdict.Supported;
            if (!left.IsNullAssignable)
                return OperatorVerdict.NotDefined;   // '??' needs a reference or Nullable<T> left operand
            if (IsUndecidable(left) || IsUndecidable(right))
                return OperatorVerdict.RequiresRuntimeSemantics;

            if (right.Category == OperandCategory.NullLiteral)
                return OperatorVerdict.Supported;    // Coalesce(x, null-of-x's-type) on both sides

            // Enum operands keep the one genuinely runtime-owned case: a nullable enum coalescing its own
            // enum type works on the engine and a different enum throws, and the descriptor carries no
            // identity to tell the two apart.
            if (left.Category == OperandCategory.Enum || right.Category == OperandCategory.Enum)
                return OperatorVerdict.RequiresRuntimeSemantics;

            if (left.Category == OperandCategory.String)
                return right.Category == OperandCategory.String
                    ? OperatorVerdict.Supported
                    // The engine's Expression.Coalesce has no conversion between a string and a value
                    // operand and raises HED1007 — a deterministic refusal, matched rather than degraded.
                    : OperatorVerdict.NotDefined;

            if (left.Category == OperandCategory.Numeric && right.Category == OperandCategory.Numeric)
            {
                if (left.Kind == right.Kind)
                    return OperatorVerdict.Supported;
                // Differing kinds unify in the ENGINE's promotion, which the writer spells as casts on both
                // operands — verbatim C# would refuse some of these pairs (int? ?? uint is CS0019) while the
                // engine renders the promoted type. See NativeExpressionWriter's coalesce spelling.
                return NumericTable.TryPromote(left.Kind, right.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            if (left.Category == OperandCategory.Bool && right.Category == OperandCategory.Bool)
                return OperatorVerdict.Supported;

            // Every remaining mix — a numeric against a bool, either against a string — is a pair the
            // engine's Coalesce refuses with HED1007 on every input, so the verdict matches the refusal.
            return OperatorVerdict.NotDefined;
        }

        #endregion

        #region Unary and ternary

        /// <summary>Classifies <paramref name="op"/> in unary position.</summary>
        public static OperatorVerdict ClassifyUnary(ExprOperator op, in OperandKind operand)
        {
            if (operand.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;

            // The engine's unary visitor has no user-operator fallback: every arm gates on bool/numeric/enum
            // shape and refuses the rest with HED1009, so a reference or user struct never binds.
            if (MayCarryUserOperator(operand))
                return OperatorVerdict.NotDefined;

            if (operand.Category == OperandCategory.Enum)
            {
                // '~' is the one unary the engine defines on enums — complement over the underlying type,
                // converted back to the operand's own (nullable) enum type — and C#'s enum '~' is
                // byte-identical, lifted included. '!', '-' and '+' on an enum are HED1009 on every input.
                return op == ExprOperator.OnesComplement
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;
            }

            switch (op)
            {
                case ExprOperator.Not:
                    // C#'s lifted '!' on bool? is the engine's lifted Expression.Not — null in, null out.
                    return operand.Category == OperandCategory.Bool
                        ? OperatorVerdict.Supported
                        : OperatorVerdict.NotDefined;

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

        /// <summary>Classifies <c>?:</c>. Arm unification stopped being runtime-owned for the shapes whose
        /// unification is SPELLABLE: numeric pairs emit through an explicit cast to the engine's promoted type
        /// (so C#'s own conditional typing — which refuses <c>int</c> against <c>uint</c> outright — never gets
        /// a vote), a null arm takes the other arm's type on both tiers verbatim, and bool/string pairs lift in
        /// C# exactly as the engine converts them. What stays runtime-owned is what the descriptor genuinely
        /// cannot decide: reference/Other arms (assignability needs type identity) and enum arm pairs (same
        /// enum unifies trivially, different enums error, and no identity tells them apart).</summary>
        public static OperatorVerdict ClassifyTernary(in OperandKind condition, in OperandKind whenTrue,
            in OperandKind whenFalse)
        {
            if (condition.Category == OperandCategory.Unknown)
                return OperatorVerdict.RequiresRuntimeSemantics;
            if (condition.Category != OperandCategory.Bool || condition.IsNullable)
                return OperatorVerdict.NotDefined;

            bool tNull = whenTrue.Category == OperandCategory.NullLiteral;
            bool fNull = whenFalse.Category == OperandCategory.NullLiteral;
            if (tNull && fNull)
                return OperatorVerdict.NotDefined;   // no common type on either side
            if (tNull || fNull)
            {
                // The engine types the null constant as the other arm's type; C# infers the conditional's
                // type from the non-null arm. Verbatim, whatever the arm is — provided null can inhabit it.
                var other = tNull ? whenFalse : whenTrue;
                if (other.Category == OperandCategory.Unknown)
                    return OperatorVerdict.RequiresRuntimeSemantics;
                return other.IsNullAssignable ? OperatorVerdict.Supported : OperatorVerdict.NotDefined;
            }

            if (IsUndecidable(whenTrue) || IsUndecidable(whenFalse))
                return OperatorVerdict.RequiresRuntimeSemantics;

            if (whenTrue.Category == OperandCategory.Numeric && whenFalse.Category == OperandCategory.Numeric)
            {
                return NumericTable.TryPromote(whenTrue.Kind, whenFalse.Kind, out _)
                    ? OperatorVerdict.Supported
                    : OperatorVerdict.NotDefined;   // decimal against a real — the engine's HED1007
            }

            if (whenTrue.Category == OperandCategory.Bool && whenFalse.Category == OperandCategory.Bool)
                return OperatorVerdict.Supported;   // C# lifts mixed nullability exactly as the engine converts
            if (whenTrue.Category == OperandCategory.String && whenFalse.Category == OperandCategory.String)
                return OperatorVerdict.Supported;

            if (whenTrue.Category == OperandCategory.Enum && whenFalse.Category == OperandCategory.Enum)
                return OperatorVerdict.RequiresRuntimeSemantics;

            // Every remaining mix is a value pair the engine's unification refuses with HED1007.
            return OperatorVerdict.NotDefined;
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
                    // Lifted on either side lifts the result — the count too: a null count is a null result.
                    return OperandKind.Numeric(ShiftKind(left.Kind), lifted);

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
            if (left.Category == OperandCategory.NullLiteral)
                return OperandKind.Unknown;   // 'null ?? x' is the right operand boxed — the type is object
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
                    return OperandKind.Of(OperandCategory.Bool, operand.IsNullable);
                case ExprOperator.Negate:
                    // The runtime widens uint to long before negating, exactly as C# does.
                    return OperandKind.Numeric(
                        operand.Kind == NumericKind.UInt32 ? NumericKind.Int64 : NumericTable.UnaryPromote(operand.Kind),
                        operand.IsNullable);
                case ExprOperator.UnaryPlus:
                case ExprOperator.OnesComplement:
                    // '~' over an enum keeps the enum's own type (the engine converts the complement back).
                    if (operand.Category == OperandCategory.Enum)
                        return OperandKind.Of(OperandCategory.Enum, operand.IsNullable);
                    return OperandKind.Numeric(NumericTable.UnaryPromote(operand.Kind), operand.IsNullable);
                default:
                    return OperandKind.Unknown;
            }
        }

        /// <summary>The kind a <see cref="OperatorVerdict.Supported"/> ternary emission produces. A null arm
        /// yields the other arm's kind; equal numeric kinds keep their own kind (the engine's unifier returns
        /// equal types untouched, so a char pair stays char and renders as a character); differing numeric
        /// kinds take the engine's promotion, lifted when either arm is; a bool pair lifts likewise.</summary>
        public static OperandKind TernaryResult(in OperandKind whenTrue, in OperandKind whenFalse)
        {
            if (ClassifyTernary(OperandKind.Of(OperandCategory.Bool), whenTrue, whenFalse) !=
                OperatorVerdict.Supported)
                return OperandKind.Unknown;

            if (whenTrue.Category == OperandCategory.NullLiteral)
                return whenFalse;
            if (whenFalse.Category == OperandCategory.NullLiteral)
                return whenTrue;

            if (whenTrue.Category == OperandCategory.Numeric)
            {
                bool lifted = whenTrue.IsNullable || whenFalse.IsNullable;
                if (whenTrue.Kind == whenFalse.Kind)
                    return OperandKind.Numeric(whenTrue.Kind, lifted);
                NumericTable.TryPromote(whenTrue.Kind, whenFalse.Kind, out var promoted);
                return OperandKind.Numeric(promoted, lifted);
            }

            if (whenTrue.Category == OperandCategory.Bool)
                return OperandKind.Of(OperandCategory.Bool, whenTrue.IsNullable || whenFalse.IsNullable);
            return whenTrue;   // String
        }

        #endregion

        /// <summary>A reference or user-struct operand may declare a user-defined operator the descriptor cannot
        /// see — for the engine sites that BIND user operators (arithmetic, relational, equality tails), whether
        /// the engine succeeds or refuses depends on type identity the table does not hold. The engine sites that
        /// never consult user operators (shift, bitwise, logical, unary) refuse these operands on every input,
        /// so there the same categories flip to <see cref="OperatorVerdict.NotDefined"/> instead.</summary>
        private static bool MayCarryUserOperator(in OperandKind kind)
        {
            switch (kind.Category)
            {
                case OperandCategory.Reference:
                case OperandCategory.Other:
                    return true;
                default:
                    return false;
            }
        }

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
