using System;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// The twelve numeric primitives the native tier promotes over, plus <see cref="None"/> for "not a numeric
    /// primitive". Keying conversion tables on this enum instead of <see cref="Type"/> or Roslyn's
    /// <c>SpecialType</c> makes the operator-legality and overload-rank rules expressible in one Roslyn-free file —
    /// fact sources stay per-side adapters while the lattice is shared.
    /// </summary>
    internal enum NumericKind
    {
        None = 0,
        SByte,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Char,
        Single,
        Double,
        Decimal
    }

    /// <summary>
    /// The C# implicit-numeric-conversion (§10.2.3) and binary/unary promotion tables, keyed on
    /// <see cref="NumericKind"/>. Transcribed from the runtime's <c>NumericPromotion</c> <see cref="Type"/>-keyed table
    /// and diffed against the generator's <c>SpecialType</c>-keyed twin in <c>TemplateEmitter</c>; they agreed
    /// entry-for-entry, and this file is what stops that agreement from rotting. Every function returns
    /// false/<see cref="NumericKind.None"/> for <see cref="NumericKind.None"/> rather than throwing, so callers can
    /// feed unclassified operands safely.
    /// <para>BCL types only (<see cref="FromClrType"/> takes a <see cref="Type"/>); no Roslyn, netstandard2.0-clean.</para>
    /// </summary>
    internal static class NumericTable
    {
        /// <summary>Number of enum members, including <see cref="NumericKind.None"/> — the lockstep sweep's bound.</summary>
        public const int KindCount = 13;

        // One bitmask per source kind: bit (int)target is set when source converts implicitly to target.
        private static readonly int[] ImplicitTargets = BuildImplicitTargets();

        private static int[] BuildImplicitTargets()
        {
            var table = new int[KindCount];
            table[(int) NumericKind.SByte] = Mask(NumericKind.Int16, NumericKind.Int32, NumericKind.Int64,
                NumericKind.Single, NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.Byte] = Mask(NumericKind.Int16, NumericKind.UInt16, NumericKind.Int32,
                NumericKind.UInt32, NumericKind.Int64, NumericKind.UInt64, NumericKind.Single, NumericKind.Double,
                NumericKind.Decimal);
            table[(int) NumericKind.Int16] = Mask(NumericKind.Int32, NumericKind.Int64, NumericKind.Single,
                NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.UInt16] = Mask(NumericKind.Int32, NumericKind.UInt32, NumericKind.Int64,
                NumericKind.UInt64, NumericKind.Single, NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.Int32] = Mask(NumericKind.Int64, NumericKind.Single, NumericKind.Double,
                NumericKind.Decimal);
            table[(int) NumericKind.UInt32] = Mask(NumericKind.Int64, NumericKind.UInt64, NumericKind.Single,
                NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.Int64] = Mask(NumericKind.Single, NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.UInt64] = Mask(NumericKind.Single, NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.Char] = Mask(NumericKind.UInt16, NumericKind.Int32, NumericKind.UInt32,
                NumericKind.Int64, NumericKind.UInt64, NumericKind.Single, NumericKind.Double, NumericKind.Decimal);
            table[(int) NumericKind.Single] = Mask(NumericKind.Double);
            return table;
        }

        private static int Mask(params NumericKind[] kinds)
        {
            int mask = 0;
            foreach (var kind in kinds)
                mask |= 1 << (int) kind;
            return mask;
        }

        /// <summary>The CLR type → kind adapter. <see cref="Type"/> is BCL, so this one lives in the shared file;
        /// the Roslyn <c>SpecialType</c> adapter stays on the generator side.</summary>
        public static NumericKind FromClrType(Type type)
        {
            if (type == null)
                return NumericKind.None;
            if (type == typeof(sbyte)) return NumericKind.SByte;
            if (type == typeof(byte)) return NumericKind.Byte;
            if (type == typeof(short)) return NumericKind.Int16;
            if (type == typeof(ushort)) return NumericKind.UInt16;
            if (type == typeof(int)) return NumericKind.Int32;
            if (type == typeof(uint)) return NumericKind.UInt32;
            if (type == typeof(long)) return NumericKind.Int64;
            if (type == typeof(ulong)) return NumericKind.UInt64;
            if (type == typeof(char)) return NumericKind.Char;
            if (type == typeof(float)) return NumericKind.Single;
            if (type == typeof(double)) return NumericKind.Double;
            if (type == typeof(decimal)) return NumericKind.Decimal;
            return NumericKind.None;
        }

        /// <summary>The CLR type for a kind, or null for <see cref="NumericKind.None"/> — the inverse of
        /// <see cref="FromClrType"/>, used by the runtime's delegating shims.</summary>
        public static Type ToClrType(NumericKind kind)
        {
            switch (kind)
            {
                case NumericKind.SByte: return typeof(sbyte);
                case NumericKind.Byte: return typeof(byte);
                case NumericKind.Int16: return typeof(short);
                case NumericKind.UInt16: return typeof(ushort);
                case NumericKind.Int32: return typeof(int);
                case NumericKind.UInt32: return typeof(uint);
                case NumericKind.Int64: return typeof(long);
                case NumericKind.UInt64: return typeof(ulong);
                case NumericKind.Char: return typeof(char);
                case NumericKind.Single: return typeof(float);
                case NumericKind.Double: return typeof(double);
                case NumericKind.Decimal: return typeof(decimal);
                default: return null;
            }
        }

        /// <summary>Whether the kind is one of the twelve numeric primitives (<c>char</c> included).</summary>
        public static bool IsNumeric(NumericKind kind) => kind != NumericKind.None;

        /// <summary>The integral set: the eight integer primitives plus <c>char</c>.</summary>
        public static bool IsIntegral(NumericKind kind)
        {
            switch (kind)
            {
                case NumericKind.SByte:
                case NumericKind.Byte:
                case NumericKind.Int16:
                case NumericKind.UInt16:
                case NumericKind.Int32:
                case NumericKind.UInt32:
                case NumericKind.Int64:
                case NumericKind.UInt64:
                case NumericKind.Char:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The signed-integral set — the operands that make an accompanying <c>ulong</c> illegal.</summary>
        public static bool IsSigned(NumericKind kind)
        {
            switch (kind)
            {
                case NumericKind.SByte:
                case NumericKind.Int16:
                case NumericKind.Int32:
                case NumericKind.Int64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>C# implicit numeric conversion source → target (excludes identity).</summary>
        public static bool IsImplicit(NumericKind from, NumericKind to)
        {
            if (from == NumericKind.None || to == NumericKind.None)
                return false;
            return (ImplicitTargets[(int) from] & (1 << (int) to)) != 0;
        }

        /// <summary>
        /// Binary numeric promotion, first matching rule wins. Returns false for the two illegal mixes the caller
        /// reports as HED1008 — <c>decimal</c> with <c>float</c>/<c>double</c>, and <c>ulong</c> with a signed
        /// integral — and for any non-numeric operand.
        /// </summary>
        public static bool TryPromote(NumericKind left, NumericKind right, out NumericKind promoted)
        {
            promoted = NumericKind.None;
            if (left == NumericKind.None || right == NumericKind.None)
                return false;

            if (left == NumericKind.Decimal || right == NumericKind.Decimal)
            {
                if (left == NumericKind.Single || left == NumericKind.Double ||
                    right == NumericKind.Single || right == NumericKind.Double)
                    return false;
                promoted = NumericKind.Decimal;
                return true;
            }

            if (left == NumericKind.Double || right == NumericKind.Double)
            {
                promoted = NumericKind.Double;
                return true;
            }

            if (left == NumericKind.Single || right == NumericKind.Single)
            {
                promoted = NumericKind.Single;
                return true;
            }

            if (left == NumericKind.UInt64 || right == NumericKind.UInt64)
            {
                if (IsSigned(left) || IsSigned(right))
                    return false;
                promoted = NumericKind.UInt64;
                return true;
            }

            if (left == NumericKind.Int64 || right == NumericKind.Int64)
            {
                promoted = NumericKind.Int64;
                return true;
            }

            if (left == NumericKind.UInt32 && IsSigned(right) || right == NumericKind.UInt32 && IsSigned(left))
            {
                promoted = NumericKind.Int64;
                return true;
            }

            if (left == NumericKind.UInt32 || right == NumericKind.UInt32)
            {
                promoted = NumericKind.UInt32;
                return true;
            }

            promoted = NumericKind.Int32;
            return true;
        }

        /// <summary>Unary numeric promotion for <c>+</c>/<c>~</c> (and the operand widening of <c>-</c>): the five
        /// sub-<c>int</c> kinds widen to <c>int</c>, every other kind maps to itself.</summary>
        public static NumericKind UnaryPromote(NumericKind operand)
        {
            switch (operand)
            {
                case NumericKind.SByte:
                case NumericKind.Byte:
                case NumericKind.Int16:
                case NumericKind.UInt16:
                case NumericKind.Char:
                    return NumericKind.Int32;
                default:
                    return operand;
            }
        }
    }
}
