using System;
using System.Collections.Generic;
using Heddle.Language.Expressions;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The characterization pin for the <see cref="NumericTable"/> extraction, and the transition
    /// scaffolding that keeps <c>NumericPromotion</c>'s public behavior identical while it delegates.
    /// <para><b>Legacy*</b> below is the pre-extraction body, transcribed verbatim from
    /// <c>NumericPromotion.cs</c> before the tables moved (captured first, moved second). The sweeps are exhaustive
    /// over all 13 × 13 kind pairs — nothing is sampled — so a single wrong row anywhere in the shared table fails
    /// here. The test retires with the delegation, once the shared table has been adopted elsewhere and no private
    /// copy is left to compare against.</para>
    /// </summary>
    public class NumericTableLockstepTests
    {
        #region Legacy bodies — verbatim pre-extraction NumericPromotion

        private static readonly HashSet<Type> LegacyNumericTypes = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(char), typeof(float), typeof(double), typeof(decimal)
        };

        private static readonly HashSet<Type> LegacyIntegralTypes = new HashSet<Type>
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char)
        };

        private static readonly HashSet<Type> LegacySignedIntegral = new HashSet<Type>
        {
            typeof(sbyte), typeof(short), typeof(int), typeof(long)
        };

        private static readonly Dictionary<Type, HashSet<Type>> LegacyImplicitNumeric = new Dictionary<Type, HashSet<Type>>
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

        private static bool LegacyIsNumeric(Type type) => type != null && LegacyNumericTypes.Contains(type);

        private static bool LegacyIsIntegral(Type type) => type != null && LegacyIntegralTypes.Contains(type);

        private static bool LegacyIsImplicitNumeric(Type from, Type to) =>
            from != null && to != null && LegacyImplicitNumeric.TryGetValue(from, out var set) && set.Contains(to);

        private static bool LegacyTryPromote(Type left, Type right, out Type promoted)
        {
            promoted = null;
            if (!LegacyIsNumeric(left) || !LegacyIsNumeric(right))
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
                if (LegacySignedIntegral.Contains(left) || LegacySignedIntegral.Contains(right))
                    return false;
                promoted = typeof(ulong);
                return true;
            }

            if (left == typeof(long) || right == typeof(long))
            {
                promoted = typeof(long);
                return true;
            }

            if (left == typeof(uint) && LegacySignedIntegral.Contains(right) ||
                right == typeof(uint) && LegacySignedIntegral.Contains(left))
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

        private static Type LegacyUnaryPromote(Type operand)
        {
            if (operand == typeof(sbyte) || operand == typeof(byte) || operand == typeof(short) ||
                operand == typeof(ushort) || operand == typeof(char))
                return typeof(int);
            return operand;
        }

        #endregion

        private static IEnumerable<(NumericKind kind, Type type)> AllKinds()
        {
            yield return (NumericKind.None, typeof(string));
            for (int i = 1; i < NumericTable.KindCount; i++)
            {
                var kind = (NumericKind) i;
                yield return (kind, NumericTable.ToClrType(kind));
            }
        }

        [Fact]
        public void KindEnum_CoversEveryNumericPrimitive_AndNothingElse()
        {
            Assert.Equal(NumericTable.KindCount, Enum.GetValues(typeof(NumericKind)).Length);
            foreach (var (kind, type) in AllKinds())
            {
                if (kind == NumericKind.None)
                    continue;
                Assert.Equal(kind, NumericTable.FromClrType(type));
            }

            Assert.Equal(NumericKind.None, NumericTable.FromClrType(null));
            Assert.Equal(NumericKind.None, NumericTable.FromClrType(typeof(string)));
            Assert.Equal(NumericKind.None, NumericTable.FromClrType(typeof(bool)));
            Assert.Equal(NumericKind.None, NumericTable.FromClrType(typeof(DateTime)));
            Assert.Equal(NumericKind.None, NumericTable.FromClrType(typeof(int?)));
        }

        [Fact]
        public void ImplicitConversionTable_MatchesTheLegacyBody_Exhaustively()
        {
            foreach (var (fromKind, fromType) in AllKinds())
            foreach (var (toKind, toType) in AllKinds())
            {
                Assert.Equal(LegacyIsImplicitNumeric(fromType, toType), NumericTable.IsImplicit(fromKind, toKind));
                Assert.Equal(LegacyIsImplicitNumeric(fromType, toType), NumericPromotion.IsImplicitNumeric(fromType, toType));
            }
        }

        [Fact]
        public void BinaryPromotion_MatchesTheLegacyBody_Exhaustively()
        {
            foreach (var (leftKind, leftType) in AllKinds())
            foreach (var (rightKind, rightType) in AllKinds())
            {
                bool legacy = LegacyTryPromote(leftType, rightType, out var legacyPromoted);
                bool shared = NumericTable.TryPromote(leftKind, rightKind, out var sharedKind);
                Assert.Equal(legacy, shared);
                Assert.Equal(legacyPromoted, NumericTable.ToClrType(sharedKind));

                bool facade = NumericPromotion.TryPromote(leftType, rightType, out var facadePromoted);
                Assert.Equal(legacy, facade);
                Assert.Equal(legacyPromoted, facadePromoted);
            }
        }

        [Fact]
        public void UnaryPromotion_And_Predicates_MatchTheLegacyBody_Exhaustively()
        {
            foreach (var (kind, type) in AllKinds())
            {
                Assert.Equal(LegacyIsNumeric(type), NumericTable.IsNumeric(kind));
                Assert.Equal(LegacyIsNumeric(type), NumericPromotion.IsNumeric(type));
                Assert.Equal(LegacyIsIntegral(type), NumericTable.IsIntegral(kind));
                Assert.Equal(LegacyIsIntegral(type), NumericPromotion.IsIntegral(type));

                if (kind == NumericKind.None)
                    continue;
                Assert.Equal(NumericTable.FromClrType(LegacyUnaryPromote(type)), NumericTable.UnaryPromote(kind));
                Assert.Equal(LegacyUnaryPromote(type), NumericPromotion.UnaryPromote(type));
            }

            // The façade keeps the legacy pass-through for a non-numeric operand rather than nulling it.
            Assert.Equal(typeof(string), NumericPromotion.UnaryPromote(typeof(string)));
            Assert.Null(NumericPromotion.UnaryPromote(null));
        }

        [Fact]
        public void SignedIntegralSet_MatchesTheLegacyBody()
        {
            foreach (var (kind, type) in AllKinds())
                Assert.Equal(LegacySignedIntegral.Contains(type), NumericTable.IsSigned(kind));
        }
    }
}
