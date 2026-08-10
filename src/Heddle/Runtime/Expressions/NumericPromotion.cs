using System;
using Heddle.Language.Expressions;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// C# numeric-promotion lookups shared by the operator compiler and function resolver.
    /// Wraps <see cref="NumericTable"/> with <see cref="Type"/>-based APIs.
    /// </summary>
    internal static class NumericPromotion
    {
        public static bool IsNumeric(Type type) => NumericTable.IsNumeric(NumericTable.FromClrType(type));

        public static bool IsIntegral(Type type) => NumericTable.IsIntegral(NumericTable.FromClrType(type));

        /// <summary>C# implicit numeric conversion source → target (excludes identity).</summary>
        public static bool IsImplicitNumeric(Type from, Type to)
        {
            return NumericTable.IsImplicit(NumericTable.FromClrType(from), NumericTable.FromClrType(to));
        }

        /// <summary>Binary numeric promotion; returns the promoted type or <c>false</c> for illegal mixes
        /// (decimal with float/double, ulong with signed integral).</summary>
        public static bool TryPromote(Type left, Type right, out Type promoted)
        {
            if (!NumericTable.TryPromote(NumericTable.FromClrType(left), NumericTable.FromClrType(right),
                    out var kind))
            {
                promoted = null;
                return false;
            }

            promoted = NumericTable.ToClrType(kind);
            return true;
        }

        /// <summary>Unary numeric promotion for <c>+</c>/<c>~</c> (and the operand widening of <c>-</c>).</summary>
        public static Type UnaryPromote(Type operand)
        {
            var kind = NumericTable.FromClrType(operand);
            return kind == NumericKind.None ? operand : NumericTable.ToClrType(NumericTable.UnaryPromote(kind));
        }
    }
}
