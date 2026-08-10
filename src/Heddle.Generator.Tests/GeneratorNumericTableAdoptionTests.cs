extern alias gen;
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Xunit;
using NumericKind = gen::Heddle.Language.Expressions.NumericKind;
using NumericTable = gen::Heddle.Language.Expressions.NumericTable;
using SymbolFacts = gen::Heddle.Generator.Binding.SymbolFacts;
using TemplateEmitter = gen::Heddle.Generator.Emit.TemplateEmitter;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The emitter's widening table must match the shared <see cref="NumericTable"/>; drifting silently would
    /// change property defaults and generated casts. Exhaustively tests <see cref="TemplateEmitter.IsImplicitNumericWidening"/>
    /// against the shared table and a legacy reference.
    /// </summary>
    public class GeneratorNumericTableAdoptionTests
    {
        #region Legacy table (pre-consolidation)

        private static bool LegacyIsImplicitNumericWidening(SpecialType from, SpecialType to)
        {
            if (from == to)
                return false;
            switch (from)
            {
                case SpecialType.System_SByte:
                    return to == SpecialType.System_Int16 || to == SpecialType.System_Int32 || to == SpecialType.System_Int64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Byte:
                    return to == SpecialType.System_Int16 || to == SpecialType.System_UInt16 || to == SpecialType.System_Int32 ||
                           to == SpecialType.System_UInt32 || to == SpecialType.System_Int64 || to == SpecialType.System_UInt64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Int16:
                    return to == SpecialType.System_Int32 || to == SpecialType.System_Int64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_UInt16:
                    return to == SpecialType.System_Int32 || to == SpecialType.System_UInt32 || to == SpecialType.System_Int64 ||
                           to == SpecialType.System_UInt64 || to == SpecialType.System_Single || to == SpecialType.System_Double ||
                           to == SpecialType.System_Decimal;
                case SpecialType.System_Int32:
                    return to == SpecialType.System_Int64 || to == SpecialType.System_Single ||
                           to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_UInt32:
                    return to == SpecialType.System_Int64 || to == SpecialType.System_UInt64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Char:
                    return to == SpecialType.System_UInt16 || to == SpecialType.System_Int32 || to == SpecialType.System_UInt32 ||
                           to == SpecialType.System_Int64 || to == SpecialType.System_UInt64 || to == SpecialType.System_Single ||
                           to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Single:
                    return to == SpecialType.System_Double;
                default:
                    return false;
            }
        }

        #endregion

        private static IEnumerable<SpecialType> AllSpecialTypes()
        {
            foreach (SpecialType value in Enum.GetValues(typeof(SpecialType)))
                yield return value;
        }

        [Fact]
        public void TheEmittersWideningRule_MatchesTheDeletedTable_Exhaustively()
        {
            foreach (var from in AllSpecialTypes())
            foreach (var to in AllSpecialTypes())
            {
                Assert.Equal(LegacyIsImplicitNumericWidening(from, to),
                    TemplateEmitter.IsImplicitNumericWidening(from, to));
            }
        }

        /// <summary>The emitter's rule is the shared table's rule (through the Roslyn facts adapter) for all pairs.</summary>
        [Fact]
        public void TheEmittersWideningRule_IsTheSharedTableThroughTheAdapter()
        {
            foreach (var from in AllSpecialTypes())
            foreach (var to in AllSpecialTypes())
            {
                Assert.Equal(
                    NumericTable.IsImplicit(SymbolFacts.ToNumericKind(from), SymbolFacts.ToNumericKind(to)),
                    TemplateEmitter.IsImplicitNumericWidening(from, to));
            }
        }

        /// <summary>The <c>SpecialType</c> adapter: twelve numeric primitives map across; others (including
        /// <c>bool</c>, <c>string</c>, <c>object</c>) are <see cref="NumericKind.None"/>. A wrong mapping silently breaks property defaults.</summary>
        [Fact]
        public void TheSpecialTypeAdapterCoversTheTwelvePrimitives_AndNothingElse()
        {
            var expected = new Dictionary<SpecialType, NumericKind>
            {
                [SpecialType.System_SByte] = NumericKind.SByte,
                [SpecialType.System_Byte] = NumericKind.Byte,
                [SpecialType.System_Int16] = NumericKind.Int16,
                [SpecialType.System_UInt16] = NumericKind.UInt16,
                [SpecialType.System_Int32] = NumericKind.Int32,
                [SpecialType.System_UInt32] = NumericKind.UInt32,
                [SpecialType.System_Int64] = NumericKind.Int64,
                [SpecialType.System_UInt64] = NumericKind.UInt64,
                [SpecialType.System_Char] = NumericKind.Char,
                [SpecialType.System_Single] = NumericKind.Single,
                [SpecialType.System_Double] = NumericKind.Double,
                [SpecialType.System_Decimal] = NumericKind.Decimal,
            };

            foreach (var special in AllSpecialTypes())
            {
                var want = expected.TryGetValue(special, out var kind) ? kind : NumericKind.None;
                Assert.Equal(want, SymbolFacts.ToNumericKind(special));
            }
        }
    }
}
