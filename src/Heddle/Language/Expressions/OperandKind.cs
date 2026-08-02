using System;

namespace Heddle.Language.Expressions
{
    /// <summary>Coarse static classification for operands; <see cref="Unknown"/> means "no static type available" and causes graceful degradation.</summary>
    internal enum OperandCategory
    {
        /// <summary>No static type available — always degrades (degrade-on-doubt).</summary>
        Unknown = 0,

        /// <summary>One of the twelve numeric primitives; <c>Kind</c> carries which (<c>char</c> included).</summary>
        Numeric,

        /// <summary><c>System.Boolean</c>.</summary>
        Bool,

        /// <summary><c>System.String</c>.</summary>
        String,

        /// <summary>Any enum type.</summary>
        Enum,

        /// <summary>The <c>null</c> literal — typed by the consuming operator.</summary>
        NullLiteral,

        /// <summary>A known reference type that is not <see cref="String"/>.</summary>
        Reference,

        /// <summary>A known non-primitive value type (user structs, <c>DateTime</c>, …).</summary>
        Other
    }

    /// <summary>
    /// How two operand types relate, as the engine's unifier sees them. The descriptor holds no conversion
    /// graph, so the caller supplies it — the generator from Roslyn conversion classification, tests from
    /// CLR reflection. User-defined conversions never count, because the engine's unifier never binds them.
    /// </summary>
    internal enum TypeRelation
    {
        /// <summary>At least one side could not be typed — claims nothing.</summary>
        Unknown = 0,

        /// <summary>The same type, nullability aside.</summary>
        Identical,

        /// <summary>Both are reference types and the left implicitly reference-converts to the right.</summary>
        LeftWidensToRight,

        /// <summary>Both are reference types and the right implicitly reference-converts to the left.</summary>
        RightWidensToLeft,

        /// <summary>Both types are known and none of the above holds.</summary>
        None
    }

    /// <summary>
    /// Whether the engine's user-defined-operator lookup binds for an operand pair. Caller-supplied like
    /// <see cref="TypeRelation"/> — the generator proves it from operator symbols, tests from reflection —
    /// and only the sound claims are made: Bound needs an operator declared on an operand type itself with
    /// exactly the operands' types; Absent needs no operator by that name anywhere in either base chain.
    /// </summary>
    internal enum OperatorWitness
    {
        /// <summary>No proof either way — runtime-owned.</summary>
        Unknown = 0,

        /// <summary>A user-defined operator the engine's factory binds exists for exactly this pair.</summary>
        Bound,

        /// <summary>No user-defined operator by that name exists — the factory refuses on every input.</summary>
        Absent
    }

    /// <summary>Operand classification: category, numeric kind (if applicable), and nullability. The sole shape used by operator rules, enabling one table to serve both generator and runtime.</summary>
    internal readonly struct OperandKind
    {
        private OperandKind(OperandCategory category, NumericKind kind, bool isNullable, string typeIdentity)
        {
            Category = category;
            Kind = kind;
            IsNullable = isNullable;
            TypeIdentity = typeIdentity;
        }

        public OperandCategory Category { get; }

        /// <summary>Meaningful only for <see cref="OperandCategory.Numeric"/>.</summary>
        public NumericKind Kind { get; }

        /// <summary>Whether a <c>Nullable&lt;T&gt;</c> wrapper is present (never true for reference categories).</summary>
        public bool IsNullable { get; }

        /// <summary>An opaque, ordinal-compared spelling of the operand's underlying type, or null when the
        /// producer has none. Only ever compared left-against-right within one producer, so the two producers'
        /// spellings (symbol display vs <c>Type.FullName</c>) never need to agree with each other.</summary>
        public string TypeIdentity { get; }

        public static readonly OperandKind Unknown =
            new OperandKind(OperandCategory.Unknown, NumericKind.None, false, null);

        public static readonly OperandKind Null =
            new OperandKind(OperandCategory.NullLiteral, NumericKind.None, false, null);

        public static OperandKind Numeric(NumericKind kind, bool isNullable) =>
            kind == NumericKind.None
                ? Unknown
                : new OperandKind(OperandCategory.Numeric, kind, isNullable, null);

        public static OperandKind Of(OperandCategory category, bool isNullable = false, string typeIdentity = null) =>
            new OperandKind(category, NumericKind.None, isNullable, typeIdentity);

        /// <summary>True for the reference categories and for a lifted value kind — the operands <c>??</c> and a
        /// <c>null</c> comparison accept on the left.</summary>
        public bool IsNullAssignable =>
            IsNullable || Category == OperandCategory.String || Category == OperandCategory.Reference ||
            Category == OperandCategory.NullLiteral;

        /// <summary>Both identities are known and equal — the operands are the same type, nullability aside.</summary>
        public static bool KnownSameType(in OperandKind left, in OperandKind right) =>
            left.TypeIdentity != null && right.TypeIdentity != null &&
            string.Equals(left.TypeIdentity, right.TypeIdentity, StringComparison.Ordinal);

        /// <summary>Both identities are known and different — an unknown identity is neither same nor
        /// different, so verdicts built on "different" must not fire on it.</summary>
        public static bool KnownDifferentType(in OperandKind left, in OperandKind right) =>
            left.TypeIdentity != null && right.TypeIdentity != null &&
            !string.Equals(left.TypeIdentity, right.TypeIdentity, StringComparison.Ordinal);
    }
}
