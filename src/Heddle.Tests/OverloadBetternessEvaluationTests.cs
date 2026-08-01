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
    /// Analysis only: pins quantified claims about adopting C#'s betterness schema (how many ambiguous calls would bind,
    /// how many would bind to a different overload). Betterness: P1 is better than P2 when P1 is an exact match or when a
    /// P1→P2 conversion exists with no P2→P1 — the "closest target wins" property Heddle's flat rank lacks.
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

            // Findings pinned for executable quantification (spec change moves these deliberately):
            // 1. Pure widening: differentWinner = ZERO (no existing call binds differently)
            // 2. 100 combinations: ambiguous today but bind under betterness (sub-int/unsigned patterns)
            // 3. 38 stay ambiguous (double/decimal non-convertible); adoption still needs ambiguity error
            // The rounding built-ins gaining their integral tiers moved (3) down by 24 and (2) up by 18: an
            // integral argument to floor/ceil/round now has an exact candidate, so it neither ties on the flat
            // rank nor needs betterness to settle it. `total` counts argument combinations per name+arity group
            // and so is unmoved by adding overloads to a group that already existed.
            Assert.Equal(480, total);
            Assert.Equal(0, differentWinner);
            Assert.Equal(100, wouldBindUnderBetterness);
            Assert.Equal(38, ambiguousToday);
            Assert.Equal(226, bothBind - differentWinner);
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
