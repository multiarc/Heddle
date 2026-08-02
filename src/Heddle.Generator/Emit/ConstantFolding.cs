using System;
using Heddle.Language.Expressions;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Whether C# would refuse to compile an expression that the dynamic engine accepts.
    /// <para>The two tiers disagree about when an arithmetic fault is discovered. The engine builds a
    /// <c>System.Linq.Expressions</c> tree and finds out at render time — <c>@(1/0)</c> compiles and throws when
    /// rendered — while C# decides at compile time, so writing the operator through verbatim turned a template the
    /// engine renders into a build error in the host's project, reported against the <c>.heddle</c> file with no
    /// Heddle diagnostic attached.</para>
    /// <para><b>An overflow is not one of those, and has not been since the emission became
    /// <c>unchecked</c>.</b> <c>unchecked(2147483647+1)</c> is a legal constant that folds to
    /// <c>-2147483648</c> — the very number the engine's unchecked <c>Expression.Add</c> renders — so refusing it
    /// took a working template off the precompiled tier for a build error that can no longer happen. What
    /// <c>unchecked</c> does <i>not</i> settle is still refused: a division by constant zero, a
    /// <c>decimal</c> overflow (never governed by the checked context), and the smallest signed value divided by
    /// <c>-1</c>, which C# folds silently to a value the engine raises on instead of producing.</para>
    /// <para>Rather than guess a value, an expression identified here is left unwritten, which degrades it to the
    /// dynamic tier — the tier whose behaviour is the contract.</para>
    /// <para><b>Both directions of error matter.</b> Missing a fault breaks the host's build; reporting one that is
    /// not there silently moves an expression — and with it the whole template — off the precompiled tier. An earlier
    /// version did both, because it folded every integer to <c>long</c> and so lost the operand's real type:
    /// <c>(2147483647+0)+(1+0)</c> looked like it fitted and did not, while <c>2147483647+1L</c> looked like it
    /// overflowed and does not. The fold therefore tracks the type C# would evaluate in, including its promotion
    /// rules, and reports "not constant" only where it genuinely cannot decide.</para>
    /// <para><b>One pairing is not a fault on either tier and still cannot be written through.</b> An <c>int</c>
    /// meeting a <c>uint</c> is evaluated in <c>uint</c> by C# — the implicit constant conversion — and in
    /// <c>long</c> by the engine. Both reach the same number while it fits in a <c>uint</c>, so the value alone is
    /// safe to print; the <i>type</i> is not, and it is what the next operator promotes from. That is why the flag
    /// travels on the folded result instead of being consumed where the pairing arose.</para>
    /// </summary>
    internal static class ConstantFolding
    {
        internal static bool CompilerWouldReject(ExprNode node)
        {
            return Fold(node).Rejected;
        }

        /// <summary>
        /// Whether the two tiers evaluate <paramref name="node"/> in different types even though they reach the same
        /// number. The value is safe to render — a <c>uint</c> and a <c>long</c> holding the same number print the
        /// same bytes — but it is not safe to hand to anything that keys on its type, because the next promotion
        /// starts from <c>uint</c> here and from <c>long</c> there. Callers that place the value in such a position
        /// ask this and degrade.
        /// </summary>
        internal static bool TiersEvaluateDifferently(ExprNode node)
        {
            return Fold(node).TiersDiffer;
        }

        /// <summary>Locates the innermost constant division or modulo whose divisor is zero — the site C#
        /// reports CS0020 against, and the one the forwarded HED1018 names. Returns false when a rejection
        /// (if any) has some other cause, so the generic degrade keeps covering those.</summary>
        internal static bool TryFindDivisionByConstantZero(ExprNode node, out BinaryNode site)
        {
            site = null;
            switch (node)
            {
                case UnaryNode unary:
                    return TryFindDivisionByConstantZero(unary.Operand, out site);
                case TernaryNode ternary:
                    return TryFindDivisionByConstantZero(ternary.Condition, out site) ||
                           TryFindDivisionByConstantZero(ternary.WhenTrue, out site) ||
                           TryFindDivisionByConstantZero(ternary.WhenFalse, out site);
                case BinaryNode binary:
                    if (TryFindDivisionByConstantZero(binary.Left, out site) ||
                        TryFindDivisionByConstantZero(binary.Right, out site))
                        return true;
                    if (binary.Operator != ExprOperator.Divide && binary.Operator != ExprOperator.Modulo)
                        return false;
                    var left = Fold(binary.Left);
                    var right = Fold(binary.Right);
                    if (!left.IsConstant || !right.IsConstant)
                        return false;
                    if (!Numeric.Unify(left.Value, right.Value, out var a, out var b))
                        return false;
                    if (!b.IsZero || !(a.IsIntegral || a.Kind == NumericKind.Decimal))
                        return false;
                    site = binary;
                    return true;
                default:
                    return false;
            }
        }

        private static Folded Fold(ExprNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return Numeric.From(literal.Value, out var value) ? Folded.Constant(value) : Folded.Unknown;
                case UnaryNode unary:
                    return FoldUnary(unary);
                case BinaryNode binary:
                    return FoldBinary(binary);
                case TernaryNode ternary:
                    return FoldTernary(ternary);
                default:
                    return Folded.Unknown;
            }
        }

        /// <summary>
        /// A conditional over a constant condition is a constant expression to C#, so <c>(true?1:1)/0</c> is a
        /// build error and the arm has to be followed. Both arms are folded whichever is taken: C# reports a fault
        /// in the arm it will discard just the same.
        /// </summary>
        private static Folded FoldTernary(TernaryNode node)
        {
            var condition = Fold(node.Condition);
            var whenTrue = Fold(node.WhenTrue);
            var whenFalse = Fold(node.WhenFalse);
            if (condition.Rejected || whenTrue.Rejected || whenFalse.Rejected)
                return Folded.Refused;
            if (!(node.Condition is LiteralNode literal) || !(literal.Value is bool taken))
                return Folded.Unknown;
            if (!whenTrue.IsConstant || !whenFalse.IsConstant)
                return Folded.Unknown;
            // The conditional's type is the common type of both arms, not the taken one's: `true ? 1 : 1L` is a
            // long, and folding it as an int would refuse arithmetic C# accepts.
            if (!Numeric.Unify(whenTrue.Value, whenFalse.Value, out var a, out var b))
                return Folded.Unknown;
            var csharp = taken ? a : b;
            if (!Numeric.UnifyAsEngine(whenTrue.EngineValue, whenFalse.EngineValue, out var ea, out var eb))
            {
                return whenTrue.TiersDiffer || whenFalse.TiersDiffer
                    ? Folded.Refused
                    : Folded.Constant(csharp);
            }

            return Agreed(csharp, taken ? ea : eb);
        }

        private static Folded FoldUnary(UnaryNode node)
        {
            var operand = Fold(node.Operand);
            if (!operand.IsConstant)
                return operand;

            if (!TryUnary(operand.Value, node.Operator, out var csharp, out var faulted))
                return faulted ? Folded.Refused : Folded.Unknown;
            if (!operand.TiersDiffer)
                return Folded.Constant(csharp);
            // `-` promotes both tiers' operands to the same type again, so the taint ends there; `~` does not, and
            // the complement of a uint 0 is 4294967295 where the complement of a long 0 is -1.
            if (!TryUnary(operand.EngineValue, node.Operator, out var engine, out _))
                return Folded.Refused;
            return Agreed(csharp, engine);
        }

        /// <summary>
        /// One tier's unary result. Returns false with <paramref name="faulted"/> set for an arithmetic fault the
        /// consumer's compiler reports, and false without it for an operand this fold cannot decide.
        /// </summary>
        private static bool TryUnary(Numeric value, ExprOperator op, out Numeric result, out bool faulted)
        {
            result = default;
            faulted = false;
            switch (op)
            {
                case ExprOperator.UnaryPlus:
                    result = value.Promoted();
                    return true;
                case ExprOperator.OnesComplement:
                    if (!value.IsIntegral)
                        return false;
                    return TryEvaluate(value.Promoted(), Numeric.Zero(value.Promoted().Kind),
                        ExprOperator.OnesComplement, out result, out faulted);
                case ExprOperator.Negate:
                    if (value.Kind == NumericKind.UInt)
                    {
                        result = Numeric.LongFrom(value);   // C# converts a negated uint to long
                        return true;
                    }

                    if (value.Kind == NumericKind.ULong)
                        return false;   // negating a ulong is a type error, not an overflow one
                    return TryEvaluate(value, Numeric.Zero(value.Kind), ExprOperator.Negate, out result, out faulted);
                default:
                    return false;
            }
        }

        private static Folded FoldBinary(BinaryNode node)
        {
            var left = Fold(node.Left);
            var right = Fold(node.Right);
            if (!left.IsConstant || !right.IsConstant)
                return left.Rejected || right.Rejected ? Folded.Refused : Folded.Unknown;

            // A shift is not a promoted pair: the left operand keeps its own type and the count is taken modulo the
            // operand's width, so unifying the two would give the result the wrong type.
            if (node.Operator == ExprOperator.LeftShift || node.Operator == ExprOperator.RightShift)
                return FoldShift(left, right, node.Operator);

            if (!Numeric.Unify(left.Value, right.Value, out var a, out var b))
                return Folded.Unknown;

            switch (node.Operator)
            {
                case ExprOperator.Divide:
                case ExprOperator.Modulo:
                    // Floating point is excluded on purpose: 1.0/0 is a legal constant expression yielding infinity,
                    // and both tiers agree on it. What matters is the type the operation is evaluated in, not the
                    // divisor's own — 1/0.0 is a double divide by a zero double, and legal.
                    if (b.IsZero && (a.IsIntegral || a.Kind == NumericKind.Decimal))
                        return Folded.Refused;
                    // The one integral division that overflows: the smallest value of the type over -1. Unlike the
                    // other operators, `unchecked` does not settle this one in the tiers' favour — C# folds it
                    // silently to the smallest value again, where the engine's Expression.Divide raises
                    // OverflowException at render. Emitting the fold would print a number the dynamic tier never
                    // produces.
                    if (Numeric.DivisionOverflows(a, b))
                        return Folded.Refused;
                    return Apply(left, right, a, b, node.Operator);
                case ExprOperator.Add:
                case ExprOperator.Subtract:
                case ExprOperator.Multiply:
                    return Apply(left, right, a, b, node.Operator);
                case ExprOperator.And:
                case ExprOperator.Or:
                case ExprOperator.ExclusiveOr:
                    // Bitwise on integers cannot fault, but its result feeds operators that can, and leaving it
                    // undecided is what let `(1&1)/0` reach the host's compiler as a division by constant zero.
                    return a.IsIntegral ? Apply(left, right, a, b, node.Operator) : Folded.Unknown;
                default:
                    return Folded.Unknown;
            }
        }

        /// <summary>
        /// Shifts never overflow or throw — the count is masked to the operand's width — so the fold always
        /// succeeds, and what it is for is giving the operators above it a value to decide on.
        /// </summary>
        private static Folded FoldShift(Folded value, Folded count, ExprOperator op)
        {
            if (!value.Value.IsIntegral || !count.Value.TryAsShiftCount(out var places))
                return Folded.Unknown;
            if (count.TiersDiffer)
                return Folded.Refused;   // the two tiers would mask a differently-typed count to different widths
            var csharp = Numeric.Shift(value.Value, places, op);
            if (!value.TiersDiffer)
                return Folded.Constant(csharp);
            return Agreed(csharp, Numeric.Shift(value.EngineValue, places, op));
        }

        /// <summary>
        /// Evaluates in each tier's own unified type. Integral arithmetic wraps, because the emitted expression is
        /// written inside an <c>unchecked</c> and C# folds a constant there by wrapping too — the same value the
        /// engine's <c>Expression.Add</c> produces at render. <c>decimal</c> is the exception and still refuses: its
        /// overflow is not governed by the checked context at all, so a constant that overflows one is a compile
        /// error in the consumer's build whatever it is wrapped in.
        /// <para>The engine's promotion is asked separately, over the operand <i>types</i> rather than their values,
        /// and the two results are compared. Where they disagree the expression belongs to the engine; where they
        /// agree on the number but not the type, the difference travels on with the folded value.</para>
        /// </summary>
        private static Folded Apply(Folded left, Folded right, Numeric a, Numeric b, ExprOperator op)
        {
            if (!TryEvaluate(a, b, op, out var csharp, out _))
                return Folded.Refused;
            if (!Numeric.UnifyAsEngine(left.EngineValue, right.EngineValue, out var ea, out var eb))
            {
                // A pairing the engine has no promotion for. Where an operand already carries a type difference the
                // expression belongs to the engine; where neither does, this is the long-standing reading of a
                // non-negative constant against a `ulong` and it stays as it was.
                return left.TiersDiffer || right.TiersDiffer ? Folded.Refused : Folded.Constant(csharp);
            }

            if (!TryEvaluate(ea, eb, op, out var engine, out _))
                return Folded.Refused;
            return Agreed(csharp, engine);
        }

        /// <summary>The folded constant when the two tiers reach the same number, and a refusal when they do not.
        /// The result keeps both types, because a number the tiers hold as <c>uint</c> and <c>long</c> prints the
        /// same and promotes differently.</summary>
        private static Folded Agreed(Numeric csharp, Numeric engine) =>
            Numeric.SameValue(csharp, engine) ? Folded.Constant(csharp, engine) : Folded.Refused;

        private static bool TryEvaluate(Numeric a, Numeric b, ExprOperator op, out Numeric result, out bool faulted)
        {
            result = default;
            faulted = false;
            try
            {
                result = Numeric.Evaluate(a, b, op);
                return true;
            }
            catch (OverflowException)
            {
                faulted = true;
                return false;
            }
            catch (DivideByZeroException)
            {
                faulted = true;
                return false;
            }
        }

        private enum NumericKind
        {
            Int,
            UInt,
            Long,
            ULong,
            Decimal,
            Double
        }

        /// <summary>
        /// A constant with the type C# would give it. Types narrower than <c>int</c> are promoted to <c>int</c> on
        /// use, which is why they are not represented separately; <c>float</c> joins <c>double</c> because neither
        /// can produce a compile-time fault worth degrading for.
        /// </summary>
        private readonly struct Numeric
        {
            private readonly long _signed;
            private readonly ulong _unsigned;
            private readonly decimal _decimal;
            private readonly double _double;

            private Numeric(NumericKind kind, long signed, ulong unsigned, decimal dec, double dbl)
            {
                Kind = kind;
                _signed = signed;
                _unsigned = unsigned;
                _decimal = dec;
                _double = dbl;
            }

            internal NumericKind Kind { get; }

            internal bool IsIntegral => Kind == NumericKind.Int || Kind == NumericKind.UInt ||
                                        Kind == NumericKind.Long || Kind == NumericKind.ULong;

            internal bool IsZero
            {
                get
                {
                    switch (Kind)
                    {
                        case NumericKind.UInt:
                        case NumericKind.ULong:
                            return _unsigned == 0;
                        case NumericKind.Decimal:
                            return _decimal == 0m;
                        case NumericKind.Double:
                            return _double == 0d;
                        default:
                            return _signed == 0;
                    }
                }
            }

            internal static Numeric Zero(NumericKind kind) => new Numeric(kind, 0, 0, 0m, 0d);

            /// <summary>Whether dividing <paramref name="a"/> by <paramref name="b"/> overflows — the smallest value
            /// of a signed integral type over <c>-1</c>, and nothing else. The two operands have already been
            /// unified, so an unsigned kind cannot meet a negative divisor at all.</summary>
            internal static bool DivisionOverflows(Numeric a, Numeric b)
            {
                if (b._signed != -1)
                    return false;
                return a.Kind == NumericKind.Int && a._signed == int.MinValue ||
                       a.Kind == NumericKind.Long && a._signed == long.MinValue;
            }

            private static Numeric Signed(NumericKind kind, long value) => new Numeric(kind, value, 0, 0m, 0d);
            private static Numeric Unsigned(NumericKind kind, ulong value) => new Numeric(kind, 0, value, 0m, 0d);

            /// <summary>The CLR types a template literal can carry. A narrower integer is not among them: the
            /// language has no <c>byte</c> or <c>short</c> suffix, so the smallest a written number ever arrives as
            /// is <c>int</c>. Rows for the narrower types would model a promotion that has nothing to promote.</summary>
            internal static bool From(object literal, out Numeric result)
            {
                switch (literal)
                {
                    case char v: result = Signed(NumericKind.Int, v); return true;
                    case int v: result = Signed(NumericKind.Int, v); return true;
                    case uint v: result = Unsigned(NumericKind.UInt, v); return true;
                    case long v: result = Signed(NumericKind.Long, v); return true;
                    case ulong v: result = Unsigned(NumericKind.ULong, v); return true;
                    case decimal v: result = new Numeric(NumericKind.Decimal, 0, 0, v, 0d); return true;
                    case float v: result = new Numeric(NumericKind.Double, 0, 0, 0m, v); return true;
                    case double v: result = new Numeric(NumericKind.Double, 0, 0, 0m, v); return true;
                    default: result = default; return false;
                }
            }

            /// <summary>The type a lone operand takes on use — narrower-than-int is already stored as int.</summary>
            internal Numeric Promoted() => this;

            /// <summary>The shift count, which C# requires to be an <c>int</c>. Anything else is a type error the
            /// compiler owns, so it is left undecided.</summary>
            internal bool TryAsShiftCount(out int places)
            {
                places = 0;
                switch (Kind)
                {
                    case NumericKind.Int:
                        places = (int)_signed;
                        return true;
                    case NumericKind.UInt:
                        if (_unsigned > int.MaxValue)
                            return false;
                        places = (int)_unsigned;
                        return true;
                    default:
                        return false;
                }
            }

            /// <summary>C#'s shift, in C#: the result keeps the operand's type, and the count is masked to that
            /// type's width — to five bits for a 32-bit operand and six for a 64-bit one — by the very operators
            /// written below. Masking it again here would compute the same number twice and read as though the
            /// language did not already do it.</summary>
            internal static Numeric Shift(Numeric value, int places, ExprOperator op)
            {
                bool left = op == ExprOperator.LeftShift;
                switch (value.Kind)
                {
                    case NumericKind.UInt:
                    {
                        var operand = (uint)value._unsigned;
                        return Unsigned(NumericKind.UInt, left ? operand << places : operand >> places);
                    }

                    case NumericKind.Long:
                        return Signed(NumericKind.Long, left ? value._signed << places : value._signed >> places);

                    case NumericKind.ULong:
                        return Unsigned(NumericKind.ULong,
                            left ? value._unsigned << places : value._unsigned >> places);

                    default:
                    {
                        var operand = (int)value._signed;
                        return Signed(NumericKind.Int, left ? operand << places : operand >> places);
                    }
                }
            }

            /// <summary>
            /// C#'s binary numeric promotion, reduced to the cases that can fault: if either side is
            /// <c>double</c> the result is <c>double</c>, else <c>decimal</c>, else <c>ulong</c>, else <c>long</c>,
            /// <c>uint</c>, else <c>int</c>. A pairing C# genuinely rejects — <c>ulong</c> with a negative signed
            /// constant — falls out of <see cref="Convert"/>'s checked cast, which overflows and reports the pair as
            /// undecidable. An explicit guard for it here was dead code: the cast already covered every case.
            /// </summary>
            internal static bool Unify(Numeric x, Numeric y, out Numeric a, out Numeric b)
            {
                a = default;
                b = default;
                var kind = Wider(x, y);
                return Convert(x, kind, out a) && Convert(y, kind, out b);
            }

            /// <summary>
            /// The same promotion asked the way the <b>engine</b> asks it. The engine builds a
            /// <c>System.Linq.Expressions</c> tree over the two operand <i>types</i>, so the implicit conversion C#
            /// grants a non-negative integer <i>constant</i> never applies: an <c>int</c> meeting a <c>uint</c> is
            /// evaluated in <c>long</c> there and in <c>uint</c> here. Returns false for a pairing the engine has no
            /// promotion for — a <c>ulong</c> with a signed operand — where C#'s constant conversion may still have
            /// one.
            /// </summary>
            internal static bool UnifyAsEngine(Numeric x, Numeric y, out Numeric a, out Numeric b)
            {
                a = default;
                b = default;
                return TryWiderAsEngine(x.Kind, y.Kind, out var kind) &&
                       Convert(x, kind, out a) && Convert(y, kind, out b);
            }

            private static bool TryWiderAsEngine(NumericKind x, NumericKind y, out NumericKind kind)
            {
                kind = NumericKind.Int;
                if (x == NumericKind.Double || y == NumericKind.Double)
                {
                    kind = NumericKind.Double;
                    return true;
                }

                if (x == NumericKind.Decimal || y == NumericKind.Decimal)
                {
                    kind = NumericKind.Decimal;
                    return true;
                }

                if (x == NumericKind.ULong || y == NumericKind.ULong)
                {
                    if (IsSignedKind(x) || IsSignedKind(y))
                        return false;
                    kind = NumericKind.ULong;
                    return true;
                }

                if (x == NumericKind.Long || y == NumericKind.Long)
                {
                    kind = NumericKind.Long;
                    return true;
                }

                if (x == NumericKind.UInt && IsSignedKind(y) || y == NumericKind.UInt && IsSignedKind(x))
                {
                    kind = NumericKind.Long;
                    return true;
                }

                if (x == NumericKind.UInt || y == NumericKind.UInt)
                    kind = NumericKind.UInt;
                return true;
            }

            private static bool IsSignedKind(NumericKind kind) =>
                kind == NumericKind.Int || kind == NumericKind.Long;

            /// <summary>Whether two constants hold the same number whatever type each is held in. <c>decimal</c>
            /// carries every <c>long</c> and <c>ulong</c> exactly, which is what makes it the comparison type for the
            /// integral kinds.</summary>
            internal static bool SameValue(Numeric x, Numeric y)
            {
                if (x.Kind == NumericKind.Double || y.Kind == NumericKind.Double)
                    return x.ToDouble().Equals(y.ToDouble());
                return x.ToDecimal() == y.ToDecimal();
            }

            internal static Numeric LongFrom(Numeric value)
            {
                return Signed(NumericKind.Long, -(long)value._unsigned);
            }

            /// <summary>
            /// Takes values, not just kinds, because <c>int</c> meeting <c>uint</c> depends on the value. The general
            /// promotion is <c>long</c>, but a non-negative <c>int</c> <b>constant</b> converts implicitly to
            /// <c>uint</c>, so <c>0 - 3000000000</c> is unsigned arithmetic and underflows — which is why C# rejects
            /// it, and why treating the pair as <c>long</c> here let it through to break the host's build.
            /// </summary>
            private static NumericKind Wider(Numeric x, Numeric y)
            {
                if (x.Kind == NumericKind.Double || y.Kind == NumericKind.Double) return NumericKind.Double;
                if (x.Kind == NumericKind.Decimal || y.Kind == NumericKind.Decimal) return NumericKind.Decimal;
                if (x.Kind == NumericKind.ULong || y.Kind == NumericKind.ULong) return NumericKind.ULong;
                if (x.Kind == NumericKind.Long || y.Kind == NumericKind.Long) return NumericKind.Long;
                if (x.Kind == NumericKind.UInt && y.Kind == NumericKind.UInt) return NumericKind.UInt;
                if (x.Kind == NumericKind.UInt) return y._signed >= 0 ? NumericKind.UInt : NumericKind.Long;
                if (y.Kind == NumericKind.UInt) return x._signed >= 0 ? NumericKind.UInt : NumericKind.Long;
                return NumericKind.Int;
            }

            private static bool Convert(Numeric value, NumericKind kind, out Numeric result)
            {
                result = default;
                try
                {
                    switch (kind)
                    {
                        case NumericKind.Double:
                            result = new Numeric(kind, 0, 0, 0m, value.ToDouble());
                            return true;
                        case NumericKind.Decimal:
                            result = new Numeric(kind, 0, 0, value.ToDecimal(), 0d);
                            return true;
                        case NumericKind.ULong:
                            result = Unsigned(kind, value.Kind == NumericKind.ULong || value.Kind == NumericKind.UInt
                                ? value._unsigned
                                : checked((ulong)value._signed));
                            return true;
                        case NumericKind.UInt:
                            // The signed field is where an Int keeps its value. Reading the unsigned one made every
                            // int operand promoted to uint arrive as zero, so (4294967295u + 1) folded as
                            // (4294967295 + 0) and was emitted, and (5 - 1u) folded as (0 - 1) and was refused.
                            result = Unsigned(kind, value.Kind == NumericKind.UInt || value.Kind == NumericKind.ULong
                                ? value._unsigned
                                : checked((uint)value._signed));
                            return true;
                        default:
                            result = Signed(kind, value.Kind == NumericKind.UInt ? (long)value._unsigned : value._signed);
                            return true;
                    }
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            private double ToDouble()
            {
                switch (Kind)
                {
                    case NumericKind.Double: return _double;
                    case NumericKind.Decimal: return (double)_decimal;
                    case NumericKind.UInt:
                    case NumericKind.ULong: return _unsigned;
                    default: return _signed;
                }
            }

            private decimal ToDecimal()
            {
                switch (Kind)
                {
                    case NumericKind.Decimal: return _decimal;
                    case NumericKind.Double: return (decimal)_double;
                    case NumericKind.UInt:
                    case NumericKind.ULong: return _unsigned;
                    default: return _signed;
                }
            }

            internal static Numeric Evaluate(Numeric a, Numeric b, ExprOperator op)
            {
                switch (a.Kind)
                {
                    case NumericKind.Double:
                        return new Numeric(a.Kind, 0, 0, 0m, Doubles(a._double, b._double, op));
                    case NumericKind.Decimal:
                        return new Numeric(a.Kind, 0, 0, Decimals(a._decimal, b._decimal, op), 0d);
                    case NumericKind.UInt:
                        return op == ExprOperator.OnesComplement
                            ? Unsigned(a.Kind, ~(uint)a._unsigned)
                            : Unsigned(a.Kind, unchecked((uint)Unsigneds(a._unsigned, b._unsigned, op)));
                    case NumericKind.ULong:
                        return Unsigned(a.Kind, Unsigneds(a._unsigned, b._unsigned, op));
                    case NumericKind.Int:
                        return Signed(a.Kind, unchecked((int)Signeds(a._signed, b._signed, op)));
                    default:
                        return Signed(a.Kind, Signeds(a._signed, b._signed, op));
                }
            }

            private static double Doubles(double a, double b, ExprOperator op)
            {
                switch (op)
                {
                    case ExprOperator.Add: return a + b;
                    case ExprOperator.Subtract: return a - b;
                    case ExprOperator.Multiply: return a * b;
                    case ExprOperator.Divide: return a / b;
                    case ExprOperator.Modulo: return a % b;
                    case ExprOperator.Negate: return -a;
                    default: return a;
                }
            }

            private static decimal Decimals(decimal a, decimal b, ExprOperator op)
            {
                switch (op)
                {
                    case ExprOperator.Add: return a + b;
                    case ExprOperator.Subtract: return a - b;
                    case ExprOperator.Multiply: return a * b;
                    case ExprOperator.Divide: return a / b;
                    case ExprOperator.Modulo: return a % b;
                    case ExprOperator.Negate: return -a;
                    default: return a;
                }
            }

            /// <summary>Wrapping, in the widest unsigned type: the narrowing back to the operand's own width is the
            /// caller's, and the low bits of a wider wrap are the ones a narrower wrap would have produced.</summary>
            private static ulong Unsigneds(ulong a, ulong b, ExprOperator op)
            {
                switch (op)
                {
                    case ExprOperator.Add: return unchecked(a + b);
                    case ExprOperator.Subtract: return unchecked(a - b);
                    case ExprOperator.Multiply: return unchecked(a * b);
                    case ExprOperator.Divide: return a / b;
                    case ExprOperator.Modulo: return a % b;
                    case ExprOperator.OnesComplement: return ~a;
                    case ExprOperator.And: return a & b;
                    case ExprOperator.Or: return a | b;
                    case ExprOperator.ExclusiveOr: return a ^ b;
                    // Negating an unsigned constant is a type error, not an overflow; leave it to the compiler.
                    default: return a;
                }
            }

            private static long Signeds(long a, long b, ExprOperator op)
            {
                switch (op)
                {
                    case ExprOperator.Add: return unchecked(a + b);
                    case ExprOperator.Subtract: return unchecked(a - b);
                    case ExprOperator.Multiply: return unchecked(a * b);
                    case ExprOperator.Divide: return a / b;
                    case ExprOperator.Modulo: return a % b;
                    case ExprOperator.Negate: return unchecked(-a);
                    case ExprOperator.OnesComplement: return ~a;
                    case ExprOperator.And: return a & b;
                    case ExprOperator.Or: return a | b;
                    case ExprOperator.ExclusiveOr: return a ^ b;
                    default: return a;
                }
            }
        }

        private readonly struct Folded
        {
            private Folded(bool isConstant, bool rejected, Numeric value, Numeric engineValue)
            {
                IsConstant = isConstant;
                Rejected = rejected;
                Value = value;
                EngineValue = engineValue;
            }

            internal bool IsConstant { get; }
            internal bool Rejected { get; }

            /// <summary>The constant with the type <b>C#</b> evaluates it in — the type the emitted text will have
            /// in the consumer's build.</summary>
            internal Numeric Value { get; }

            /// <summary>The same constant with the type the <b>engine</b> evaluates it in. Equal to
            /// <see cref="Value"/> except where the two promotions part company.</summary>
            internal Numeric EngineValue { get; }

            /// <summary>Whether the two tiers hold this constant in different types. The number is the same on both
            /// — a pair that disagrees on the number is refused where it arises — but the type is not, and it is
            /// what the next promotion starts from.</summary>
            internal bool TiersDiffer => IsConstant && Value.Kind != EngineValue.Kind;

            internal static Folded Unknown => new Folded(false, false, default, default);
            internal static Folded Refused => new Folded(false, true, default, default);
            internal static Folded Constant(Numeric value) => new Folded(true, false, value, value);

            internal static Folded Constant(Numeric value, Numeric engineValue) =>
                new Folded(true, false, value, engineValue);
        }
    }
}
