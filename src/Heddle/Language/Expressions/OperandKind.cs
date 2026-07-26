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

    /// <summary>Operand classification: category, numeric kind (if applicable), and nullability. The sole shape used by operator rules, enabling one table to serve both generator and runtime.</summary>
    internal readonly struct OperandKind
    {
        private OperandKind(OperandCategory category, NumericKind kind, bool isNullable)
        {
            Category = category;
            Kind = kind;
            IsNullable = isNullable;
        }

        public OperandCategory Category { get; }

        /// <summary>Meaningful only for <see cref="OperandCategory.Numeric"/>.</summary>
        public NumericKind Kind { get; }

        /// <summary>Whether a <c>Nullable&lt;T&gt;</c> wrapper is present (never true for reference categories).</summary>
        public bool IsNullable { get; }

        public static readonly OperandKind Unknown = new OperandKind(OperandCategory.Unknown, NumericKind.None, false);

        public static readonly OperandKind Null = new OperandKind(OperandCategory.NullLiteral, NumericKind.None, false);

        public static OperandKind Numeric(NumericKind kind, bool isNullable) =>
            kind == NumericKind.None ? Unknown : new OperandKind(OperandCategory.Numeric, kind, isNullable);

        public static OperandKind Of(OperandCategory category, bool isNullable = false) =>
            new OperandKind(category, NumericKind.None, isNullable);

        /// <summary>True for the reference categories and for a lifted value kind — the operands <c>??</c> and a
        /// <c>null</c> comparison accept on the left.</summary>
        public bool IsNullAssignable =>
            IsNullable || Category == OperandCategory.String || Category == OperandCategory.Reference ||
            Category == OperandCategory.NullLiteral;
    }
}
