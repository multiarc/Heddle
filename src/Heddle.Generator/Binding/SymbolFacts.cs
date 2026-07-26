using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The generator-side facts adapter for the shared expression rule tables: Roslyn symbols in, Roslyn-free
    /// <see cref="NumericKind"/>/<see cref="OperandKind"/> out. Roslyn types are allowed here and nowhere in
    /// <c>src/Heddle/Language/**</c>; the runtime's mirror of this file is the <c>Type</c>-keyed
    /// <c>NumericTable.FromClrType</c> plus <c>RuntimeOperandFacts</c>.
    /// </summary>
    internal static class SymbolFacts
    {
        /// <summary>Roslyn <see cref="SpecialType"/> → <see cref="NumericKind"/>. <c>Nullable&lt;T&gt;</c> is
        /// unwrapped by the caller before mapping; the nullability travels on <see cref="OperandKind.IsNullable"/>.</summary>
        public static NumericKind ToNumericKind(SpecialType special)
        {
            switch (special)
            {
                case SpecialType.System_SByte: return NumericKind.SByte;
                case SpecialType.System_Byte: return NumericKind.Byte;
                case SpecialType.System_Int16: return NumericKind.Int16;
                case SpecialType.System_UInt16: return NumericKind.UInt16;
                case SpecialType.System_Int32: return NumericKind.Int32;
                case SpecialType.System_UInt32: return NumericKind.UInt32;
                case SpecialType.System_Int64: return NumericKind.Int64;
                case SpecialType.System_UInt64: return NumericKind.UInt64;
                case SpecialType.System_Char: return NumericKind.Char;
                case SpecialType.System_Single: return NumericKind.Single;
                case SpecialType.System_Double: return NumericKind.Double;
                case SpecialType.System_Decimal: return NumericKind.Decimal;
                default: return NumericKind.None;
            }
        }

        /// <summary>Strips a <c>Nullable&lt;T&gt;</c> wrapper, reporting whether one was present.</summary>
        public static ITypeSymbol Unwrap(ITypeSymbol type, out bool isNullable)
        {
            isNullable = false;
            if (type is INamedTypeSymbol named && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T &&
                named.TypeArguments.Length == 1)
            {
                isNullable = true;
                return named.TypeArguments[0];
            }

            return type;
        }

        /// <summary>Classifies a resolved type symbol as the shared rules see it. An unresolved symbol, a
        /// <c>dynamic</c> receiver, an error type or a type parameter is <see cref="OperandKind.Unknown"/> —
        /// degrade-on-doubt.</summary>
        public static OperandKind Classify(ITypeSymbol type)
        {
            if (type == null || type.TypeKind == TypeKind.Dynamic || type.TypeKind == TypeKind.Error ||
                type.TypeKind == TypeKind.TypeParameter)
                return OperandKind.Unknown;

            var underlying = Unwrap(type, out bool isNullable);
            if (underlying == null)
                return OperandKind.Unknown;

            if (underlying.TypeKind == TypeKind.Enum)
                return OperandKind.Of(OperandCategory.Enum, isNullable);

            var kind = ToNumericKind(underlying.SpecialType);
            if (kind != NumericKind.None)
                return OperandKind.Numeric(kind, isNullable);

            switch (underlying.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return OperandKind.Of(OperandCategory.Bool, isNullable);
                case SpecialType.System_String:
                    return OperandKind.Of(OperandCategory.String);
                case SpecialType.System_Object:
                    // An object-typed operand carries no usable static facts — the consumer's compiler and the
                    // runtime take visibly different routes through it, so never emit.
                    return OperandKind.Unknown;
            }

            if (underlying.IsReferenceType)
                return OperandKind.Of(OperandCategory.Reference);
            if (underlying.IsValueType)
                return OperandKind.Of(OperandCategory.Other, isNullable);
            return OperandKind.Unknown;
        }
    }
}
