using System;
using Heddle.Language.Expressions;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// Table-driven C# numeric-promotion knowledge shared by the native-expression operator compiler
    /// and the function-overload resolver. Works over reflected <see cref="Type"/>s at compile time.
    /// <para>The tables themselves live once, in <see cref="NumericTable"/> under
    /// <c>Language/Expressions/</c>, where the generator's linked build sees the identical data. This type keeps its
    /// public shape and is a thin <see cref="Type"/>-keyed façade over that lattice; the exhaustive 13×13 lockstep
    /// sweep in the test suite pins the delegation against the pre-extraction bodies.</para>
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

        /// <summary>
        /// Binary numeric promotion. Returns the promoted type or <c>false</c> for the illegal mixes
        /// (decimal with float/double; ulong with a signed integral) which the caller reports as HED1008.
        /// </summary>
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
