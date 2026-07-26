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
    /// Phase 4 D5/WI4, completed by the phase-4 audit (2026-07-26). The plan's success criterion is "exactly one"
    /// numeric-kind table, and its non-goals deferred the emitter's <c>SpecialType</c>-keyed copy of the §10.2.3
    /// widening table to phase 1 while claiming "the lockstep test covers both existing copies". That claim was
    /// false: <c>NumericTableLockstepTests</c> compares the shared table to the <b>runtime's</b> pre-extraction
    /// body only, and <b>no</b> test referenced the emitter's copy at all — it could have drifted a row in silence,
    /// changing which prop defaults precompile and what cast the emitter writes.
    /// <para><c>TemplateEmitter.IsImplicitNumericWidening</c> is now a two-line adapter over
    /// <see cref="NumericTable.IsImplicit"/>. <b>Legacy*</b> below is the deleted table, transcribed verbatim, and
    /// the sweep is exhaustive over every <see cref="SpecialType"/> member — so the fold is characterized rather
    /// than argued, and re-introducing a private copy that differs anywhere fails here.</para>
    /// </summary>
    public class GeneratorNumericTableAdoptionTests
    {
        #region Legacy body — verbatim pre-fold TemplateEmitter.IsImplicitNumericWidening

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

        /// <summary>The fold itself: the emitter's answer <b>is</b> the shared table's answer through the Roslyn
        /// facts adapter, for every pair — not merely equal to it today.</summary>
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

        /// <summary>The adapter the fold rides on, pinned in its own right: twelve numeric primitives map across,
        /// everything else (including <c>bool</c>, <c>string</c>, <c>object</c>, <c>IntPtr</c>) is
        /// <see cref="NumericKind.None"/>. A wrong row here would silently un-widen a prop default.</summary>
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
