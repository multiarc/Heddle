using System;
using System.Collections.Generic;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// Table-driven C# numeric-promotion knowledge shared by the native-expression operator compiler
    /// and the function-overload resolver. Works over reflected <see cref="Type"/>s at compile time.
    /// </summary>
    internal static class NumericPromotion
    {
        private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(char), typeof(float), typeof(double), typeof(decimal)
        };

        private static readonly HashSet<Type> IntegralTypes = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char)
        };

        // Signed integral types that make an accompanying ulong operand illegal (C# rule).
        private static readonly HashSet<Type> SignedIntegral = new HashSet<Type>
        {
            typeof(sbyte), typeof(short), typeof(int), typeof(long)
        };

        // C# implicit numeric conversions (§10.2.3), keyed by source type.
        private static readonly Dictionary<Type, HashSet<Type>> ImplicitNumeric = new Dictionary<Type, HashSet<Type>>
        {
            [typeof(sbyte)] = new HashSet<Type> { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(byte)] = new HashSet<Type> { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(short)] = new HashSet<Type> { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(ushort)] = new HashSet<Type> { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(int)] = new HashSet<Type> { typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(uint)] = new HashSet<Type> { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(long)] = new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) },
            [typeof(ulong)] = new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) },
            [typeof(char)] = new HashSet<Type> { typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(float)] = new HashSet<Type> { typeof(double) }
        };

        public static bool IsNumeric(Type type) => type != null && NumericTypes.Contains(type);

        public static bool IsIntegral(Type type) => type != null && IntegralTypes.Contains(type);

        /// <summary>C# implicit numeric conversion source → target (excludes identity).</summary>
        public static bool IsImplicitNumeric(Type from, Type to)
        {
            return from != null && to != null && ImplicitNumeric.TryGetValue(from, out var set) && set.Contains(to);
        }

        /// <summary>
        /// Binary numeric promotion. Returns the promoted type or <c>false</c> for the illegal mixes
        /// (decimal with float/double; ulong with a signed integral) which the caller reports as HED1008.
        /// </summary>
        public static bool TryPromote(Type left, Type right, out Type promoted)
        {
            promoted = null;
            if (!IsNumeric(left) || !IsNumeric(right))
                return false;

            if (left == typeof(decimal) || right == typeof(decimal))
            {
                if (left == typeof(float) || left == typeof(double) || right == typeof(float) || right == typeof(double))
                    return false;
                promoted = typeof(decimal);
                return true;
            }

            if (left == typeof(double) || right == typeof(double))
            {
                promoted = typeof(double);
                return true;
            }

            if (left == typeof(float) || right == typeof(float))
            {
                promoted = typeof(float);
                return true;
            }

            if (left == typeof(ulong) || right == typeof(ulong))
            {
                if (SignedIntegral.Contains(left) || SignedIntegral.Contains(right))
                    return false;
                promoted = typeof(ulong);
                return true;
            }

            if (left == typeof(long) || right == typeof(long))
            {
                promoted = typeof(long);
                return true;
            }

            if (left == typeof(uint) && SignedIntegral.Contains(right) ||
                right == typeof(uint) && SignedIntegral.Contains(left))
            {
                promoted = typeof(long);
                return true;
            }

            if (left == typeof(uint) || right == typeof(uint))
            {
                promoted = typeof(uint);
                return true;
            }

            promoted = typeof(int);
            return true;
        }

        /// <summary>Unary numeric promotion for <c>+</c>/<c>~</c> (and the operand widening of <c>-</c>).</summary>
        public static Type UnaryPromote(Type operand)
        {
            if (operand == typeof(sbyte) || operand == typeof(byte) || operand == typeof(short) ||
                operand == typeof(ushort) || operand == typeof(char))
                return typeof(int);
            return operand;
        }
    }
}
