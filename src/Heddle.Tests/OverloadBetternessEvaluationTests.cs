using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Xunit;
using Xunit.Abstractions;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 4 WI10 — the Q4.2(b) evaluation instrument. <b>Analysis only</b>: nothing here changes runtime overload
    /// behavior, and the shared <see cref="OverloadRank"/> core is the measurement tool, not the subject.
    /// <para>The question the user asked: would adopting C#'s native <i>betterness</i> schema in the runtime binder
    /// (plus extra validations preserving Heddle's documented deviations) make sense, with the generator then
    /// matching by construction? Answering it needs a number: how many calls that are <b>ambiguous today</b> would
    /// bind under betterness, and — the dangerous half — how many calls would bind to a <b>different</b> overload
    /// than they do now. This test computes both over the shipped built-in table and pins them, so the plan's
    /// quantified claim is executable rather than asserted.</para>
    /// <para>Betterness is modeled per the C# rule for the case that actually arises here: conversion to
    /// <c>P1</c> is better than conversion to <c>P2</c> when <c>P1</c> is an exact match, or when an implicit
    /// conversion <c>P1 → P2</c> exists and no implicit conversion <c>P2 → P1</c> does — i.e. "closest target
    /// wins", which is exactly the property Heddle's flat rank does not have.</para>
    /// </summary>
    public class OverloadBetternessEvaluationTests
    {
        private readonly ITestOutputHelper _output;

        public OverloadBetternessEvaluationTests(ITestOutputHelper output) => _output = output;

        private static readonly NumericKind[] ArgumentKinds =
        {
            NumericKind.SByte, NumericKind.Byte, NumericKind.Int16, NumericKind.UInt16, NumericKind.Int32,
            NumericKind.UInt32, NumericKind.Int64, NumericKind.UInt64, NumericKind.Char, NumericKind.Single,
            NumericKind.Double, NumericKind.Decimal,
        };

        private sealed class KindRankModel : IRankModel<NumericKind>
        {
            public static readonly KindRankModel Instance = new KindRankModel();
            public bool AreSame(NumericKind a, NumericKind b) => a == b;
            public bool IsObject(NumericKind type) => false;
            public bool IsValueType(NumericKind type) => true;

            public bool TryGetNullableUnderlying(NumericKind type, out NumericKind underlying)
            {
                underlying = NumericKind.None;
                return false;
            }

            public NumericKind KindOf(NumericKind type) => type;
            public bool IsReferenceAssignable(NumericKind from, NumericKind to) => false;
        }

        /// <summary>The numeric-parameter signatures of one built-in name, in table order. Names whose overloads are
        /// not all-numeric (format/str/substr/…) have no overload-selection question to answer.</summary>
        private static IEnumerable<(string Name, List<NumericKind[]> Signatures)> NumericOverloadGroups()
        {
            foreach (var group in DefaultFunctionTable.Rows.GroupBy(r => r.Name, StringComparer.Ordinal))
            {
                var signatures = new List<NumericKind[]>();
                bool allNumeric = true;
                foreach (var row in group)
                {
                    var kinds = row.ParameterTypeNames.Select(KindOfName).ToArray();
                    if (kinds.Any(k => k == NumericKind.None))
                    {
                        allNumeric = false;
                        break;
                    }

                    signatures.Add(kinds);
                }

                if (allNumeric && signatures.Count > 1)
                    yield return (group.Key, signatures);
            }
        }

        private static NumericKind KindOfName(string name)
        {
            switch (name)
            {
                case "System.Int32": return NumericKind.Int32;
                case "System.Int64": return NumericKind.Int64;
                case "System.Double": return NumericKind.Double;
                case "System.Decimal": return NumericKind.Decimal;
                case "System.Single": return NumericKind.Single;
                default: return NumericKind.None;
            }
        }

        /// <summary>C#'s better-conversion-target rule, restricted to the numeric lattice.</summary>
        private static bool BetterTarget(NumericKind argument, NumericKind p1, NumericKind p2)
        {
            if (p1 == p2)
                return false;
            if (argument == p1)
                return true;
            if (argument == p2)
                return false;
            bool oneToTwo = NumericTable.IsImplicit(p1, p2);
            bool twoToOne = NumericTable.IsImplicit(p2, p1);
            return oneToTwo && !twoToOne;
        }

        private static int BetternessWinner(List<NumericKind[]> signatures, NumericKind[] args)
        {
            var applicable = new List<int>();
            for (int i = 0; i < signatures.Count; i++)
            {
                if (signatures[i].Length != args.Length)
                    continue;
                bool ok = true;
                for (int a = 0; a < args.Length; a++)
                {
                    if (args[a] != signatures[i][a] && !NumericTable.IsImplicit(args[a], signatures[i][a]))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    applicable.Add(i);
            }

            if (applicable.Count == 0)
                return -1;

            int winner = -2;   // -2 = ambiguous
            foreach (var candidate in applicable)
            {
                bool betterThanAll = applicable.Where(o => o != candidate).All(other =>
                {
                    bool any = false;
                    for (int a = 0; a < args.Length; a++)
                    {
                        if (BetterTarget(args[a], signatures[other][a], signatures[candidate][a]))
                            return false;
                        if (BetterTarget(args[a], signatures[candidate][a], signatures[other][a]))
                            any = true;
                    }

                    return any;
                });

                if (betterThanAll)
                    return candidate;
            }

            return winner;
        }

        private static int FlatWinner(List<NumericKind[]> signatures, NumericKind[] args)
        {
            var candidates = signatures
                .Select(s => new RankCandidate<NumericKind>(s, false, NumericKind.None))
                .ToArray();
            var rankArgs = args.Select(RankArgument<NumericKind>.Of).ToArray();
            var binding = OverloadRank.Bind(KindRankModel.Instance, candidates, rankArgs);
            switch (binding.Outcome)
            {
                case BindOutcome.Bound: return binding.Index;
                case BindOutcome.Ambiguous: return -2;
                default: return -1;
            }
        }

        [Fact]
        public void QuantifiedBehavioralDelta_OverTheShippedBuiltInTable()
        {
            int total = 0, bothBind = 0, ambiguousToday = 0, wouldBindUnderBetterness = 0, differentWinner = 0;
            int inapplicableBoth = 0;
            var newlyBinding = new List<string>();
            var changedWinner = new List<string>();

            foreach (var (name, signatures) in NumericOverloadGroups())
            {
                int arity = signatures[0].Length;
                foreach (var args in Combinations(arity))
                {
                    total++;
                    int flat = FlatWinner(signatures, args);
                    int better = BetternessWinner(signatures, args);
                    if (flat == -1 && better == -1)
                    {
                        inapplicableBoth++;
                        continue;
                    }

                    if (flat == -2 && better >= 0)
                    {
                        wouldBindUnderBetterness++;
                        if (newlyBinding.Count < 12)
                            newlyBinding.Add($"{name}({Describe(args)}) → {Describe(signatures[better])}");
                        continue;
                    }

                    if (flat == -2)
                    {
                        ambiguousToday++;
                        continue;
                    }

                    if (flat >= 0 && better >= 0)
                    {
                        bothBind++;
                        if (flat != better)
                        {
                            differentWinner++;
                            changedWinner.Add(
                                $"{name}({Describe(args)}): flat → {Describe(signatures[flat])}, betterness → {Describe(signatures[better])}");
                        }
                    }
                }
            }

            _output.WriteLine($"argument combinations examined: {total}");
            _output.WriteLine($"  no applicable overload on either schema: {inapplicableBoth}");
            _output.WriteLine($"  bind identically today and under betterness: {bothBind - differentWinner}");
            _output.WriteLine($"  ambiguous today, would BIND under betterness: {wouldBindUnderBetterness}");
            _output.WriteLine($"  ambiguous under both: {ambiguousToday}");
            _output.WriteLine($"  bind today but to a DIFFERENT overload under betterness: {differentWinner}");
            foreach (var line in newlyBinding)
                _output.WriteLine("    newly binding: " + line);
            foreach (var line in changedWinner)
                _output.WriteLine("    changed winner: " + line);

            // The findings, pinned so the plan's quantified claims stay executable (a change to the built-in table
            // is a spec change and moves these numbers deliberately):
            //
            //  1. Betterness is a pure *widening* over the shipped built-in table — differentWinner is ZERO. Not one
            //     call that binds today would bind to a different overload, so no rendered byte changes for any
            //     template that compiles today. That is the single most important number in the evaluation.
            //  2. 82 of 480 argument combinations are ambiguity errors today and would compile under betterness —
            //     every one of them a sub-int or unsigned argument to abs/min/max/round (abs(byte), min(sbyte, uint),
            //     round(float, short) …), i.e. exactly the shapes a template author writes by accident.
            //  3. Betterness does **not** remove ambiguity, it only reduces it: 62 combinations stay ambiguous
            //     because `double` and `decimal` are mutually non-convertible, so neither target is "closer"
            //     (min(ulong, int) is the family). Any adoption still needs the ambiguity error and its diagnostic.
            Assert.Equal(480, total);
            Assert.Equal(0, differentWinner);
            Assert.Equal(82, wouldBindUnderBetterness);
            Assert.Equal(62, ambiguousToday);
            Assert.Equal(220, bothBind - differentWinner);
        }

        private static string Describe(NumericKind[] kinds) => string.Join(", ", kinds);

        private static IEnumerable<NumericKind[]> Combinations(int arity)
        {
            if (arity == 1)
            {
                foreach (var a in ArgumentKinds)
                    yield return new[] { a };
                yield break;
            }

            foreach (var a in ArgumentKinds)
            foreach (var b in ArgumentKinds)
            {
                if (arity == 2)
                {
                    yield return new[] { a, b };
                    continue;
                }

                foreach (var c in ArgumentKinds)
                    yield return new[] { a, b, c };
            }
        }
    }
}
