using System;
using Heddle.Language.Expressions;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Whether C# would refuse to compile an expression that the dynamic engine accepts.
    /// <para>The two tiers disagree about when an arithmetic fault is discovered. The engine builds a
    /// <c>System.Linq.Expressions</c> tree and finds out at render time — <c>@(1/0)</c> compiles and throws when
    /// rendered, and <c>@(2147483647+1)</c> compiles and renders <c>-2147483648</c>. C# decides both at compile time
    /// and rejects them outright, so writing the operator through verbatim turned a template the engine renders into
    /// a build error in the host's project, reported against the <c>.heddle</c> file with no Heddle diagnostic
    /// attached.</para>
    /// <para>Rather than guess a value, an expression identified here is left unwritten, which degrades it to the
    /// dynamic tier — the tier whose behaviour is the contract.</para>
    /// <para><b>Both directions of error matter.</b> Missing a fault breaks the host's build; reporting one that is
    /// not there silently moves an expression — and with it the whole template — off the precompiled tier. An earlier
    /// version did both, because it folded every integer to <c>long</c> and so lost the operand's real type:
    /// <c>(2147483647+0)+(1+0)</c> looked like it fitted and did not, while <c>2147483647+1L</c> looked like it
    /// overflowed and does not. The fold therefore tracks the type C# would evaluate in, including its promotion
    /// rules, and reports "not constant" only where it genuinely cannot decide.</para>
    /// </summary>
    internal static class ConstantFolding
    {
        internal static bool CompilerWouldReject(ExprNode node)
        {
            return Fold(node).Rejected;
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
                default:
                    return Folded.Unknown;
            }
        }

        private static Folded FoldUnary(UnaryNode node)
        {
            var operand = Fold(node.Operand);
            if (!operand.IsConstant)
                return operand;

            var value = operand.Value;
            switch (node.Operator)
            {
                case ExprOperator.UnaryPlus:
                    return Folded.Constant(value.Promoted());
                case ExprOperator.OnesComplement:
                    return value.IsIntegral
                        ? Apply(value.Promoted(), Numeric.Zero(value.Promoted().Kind), ExprOperator.OnesComplement)
                        : Folded.Unknown;
                case ExprOperator.Negate:
                    if (value.Kind == NumericKind.UInt)
                        return Folded.Constant(Numeric.LongFrom(value));   // C# converts a negated uint to long
                    if (value.Kind == NumericKind.ULong)
                        return Folded.Unknown;   // negating a ulong is a type error, not an overflow one
                    return Apply(value, Numeric.Zero(value.Kind), ExprOperator.Negate);
                default:
                    return Folded.Unknown;
            }
        }

        private static Folded FoldBinary(BinaryNode node)
        {
            var left = Fold(node.Left);
            var right = Fold(node.Right);
            if (!left.IsConstant || !right.IsConstant)
                return left.Rejected || right.Rejected ? Folded.Refused : Folded.Unknown;

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
                    return Apply(a, b, node.Operator);
                case ExprOperator.Add:
                case ExprOperator.Subtract:
                case ExprOperator.Multiply:
                    return Apply(a, b, node.Operator);
                default:
                    return Folded.Unknown;
            }
        }

        /// <summary>Evaluates in the unified type, checked, so an overflow C# would report becomes a refusal here.</summary>
        private static Folded Apply(Numeric a, Numeric b, ExprOperator op)
        {
            try
            {
                return Folded.Constant(Numeric.Evaluate(a, b, op));
            }
            catch (OverflowException)
            {
                return Folded.Refused;
            }
            catch (DivideByZeroException)
            {
                return Folded.Refused;
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

            private static Numeric Signed(NumericKind kind, long value) => new Numeric(kind, value, 0, 0m, 0d);
            private static Numeric Unsigned(NumericKind kind, ulong value) => new Numeric(kind, 0, value, 0m, 0d);

            internal static bool From(object literal, out Numeric result)
            {
                switch (literal)
                {
                    case sbyte v: result = Signed(NumericKind.Int, v); return true;
                    case byte v: result = Signed(NumericKind.Int, v); return true;
                    case short v: result = Signed(NumericKind.Int, v); return true;
                    case ushort v: result = Signed(NumericKind.Int, v); return true;
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

            /// <summary>
            /// C#'s binary numeric promotion, reduced to the cases that can fault: if either side is
            /// <c>double</c> the result is <c>double</c>, else <c>decimal</c>, else <c>ulong</c>, else <c>long</c>,
            /// else <c>uint</c>, else <c>int</c>. The one combination C# rejects outright — <c>ulong</c> with a
            /// signed type — is reported as undecidable rather than as a fault, since it is a type error the emitter
            /// has no business diagnosing here.
            /// </summary>
            internal static bool Unify(Numeric x, Numeric y, out Numeric a, out Numeric b)
            {
                a = default;
                b = default;
                var kind = Wider(x, y);
                // Only a NEGATIVE signed operand makes a ulong pairing a type error; a non-negative integer
                // constant converts implicitly, which is how `ulong + 'a'` is legal C#. Refusing to decide for
                // every signed operand let that pairing through to break the host's build.
                if (kind == NumericKind.ULong && (IsNegative(x) || IsNegative(y)))
                {
                    return false;
                }

                return Convert(x, kind, out a) && Convert(y, kind, out b);
            }

            /// <summary>
            /// Takes values, not just kinds, because <c>int</c> meeting <c>uint</c> depends on the value. The general
            /// promotion is <c>long</c>, but a non-negative <c>int</c> <b>constant</b> converts implicitly to
            /// <c>uint</c>, so <c>0 - 3000000000</c> is unsigned arithmetic and underflows — which is why C# rejects
            /// it, and why treating the pair as <c>long</c> here let it through to break the host's build.
            /// </summary>
            private static bool IsNegative(Numeric value)
            {
                return value.Kind != NumericKind.UInt && value.Kind != NumericKind.ULong && value._signed < 0;
            }

            internal static Numeric LongFrom(Numeric value)
            {
                return Signed(NumericKind.Long, -(long)value._unsigned);
            }

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
                        // Complement narrows deliberately rather than checked: ~5u is 4294967290u, and computing it
                        // in ulong then narrowing under check made every complement look like an overflow.
                        return op == ExprOperator.OnesComplement
                            ? Unsigned(a.Kind, ~(uint)a._unsigned)
                            : Unsigned(a.Kind, checked((uint)Unsigneds(a._unsigned, b._unsigned, op)));
                    case NumericKind.ULong:
                        return Unsigned(a.Kind, Unsigneds(a._unsigned, b._unsigned, op));
                    case NumericKind.Int:
                        return Signed(a.Kind, checked((int)Signeds(a._signed, b._signed, op)));
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

            private static ulong Unsigneds(ulong a, ulong b, ExprOperator op)
            {
                switch (op)
                {
                    case ExprOperator.Add: return checked(a + b);
                    case ExprOperator.Subtract: return checked(a - b);
                    case ExprOperator.Multiply: return checked(a * b);
                    case ExprOperator.Divide: return a / b;
                    case ExprOperator.Modulo: return a % b;
                    case ExprOperator.OnesComplement: return ~a;
                    // Negating an unsigned constant is a type error, not an overflow; leave it to the compiler.
                    default: return a;
                }
            }

            private static long Signeds(long a, long b, ExprOperator op)
            {
                switch (op)
                {
                    case ExprOperator.Add: return checked(a + b);
                    case ExprOperator.Subtract: return checked(a - b);
                    case ExprOperator.Multiply: return checked(a * b);
                    case ExprOperator.Divide: return a / b;
                    case ExprOperator.Modulo: return a % b;
                    case ExprOperator.Negate: return checked(-a);
                    case ExprOperator.OnesComplement: return ~a;
                    default: return a;
                }
            }
        }

        private readonly struct Folded
        {
            private Folded(bool isConstant, bool rejected, Numeric value)
            {
                IsConstant = isConstant;
                Rejected = rejected;
                Value = value;
            }

            internal bool IsConstant { get; }
            internal bool Rejected { get; }
            internal Numeric Value { get; }

            internal static Folded Unknown => new Folded(false, false, default);
            internal static Folded Refused => new Folded(false, true, default);
            internal static Folded Constant(Numeric value) => new Folded(true, false, value);
        }
    }
}
