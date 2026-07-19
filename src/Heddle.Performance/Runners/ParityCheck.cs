using System;
using System.Collections.Generic;
using System.Text;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// D1-R3 parity gate. Every competitor twin must render output that is byte-identical to
    /// Heddle's after the single documented normalization (<see cref="TwinContent.Normalize"/> —
    /// collapse whitespace runs between tags). <see cref="Assert"/> runs inside each render
    /// benchmark's GlobalSetup so a drifted twin fails loudly instead of benchmarking different
    /// work; <see cref="Report"/> is a fast, host-free harness (`dotnet run -c Release -- parity`).
    /// </summary>
    internal static class ParityCheck
    {
        public static IEnumerable<(string Name, Func<string> Render)> Twins()
        {
            yield return ("Fluid", () => new FluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new ScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new DotLiquidTest().Render());
            yield return ("Handlebars", () => new HandlebarsTest().Render());
        }

        /// <summary>Throws if any twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void Assert()
        {
            var oracle = TwinContent.Normalize(new HeddleTest().Render());
            foreach (var (name, render) in Twins())
            {
                var actual = TwinContent.Normalize(render());
                if (!string.Equals(oracle, actual, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Parity twin '{name}' diverged from Heddle. {Describe(oracle, actual)}");
            }
        }

        /// <summary>Human-readable pass/fail report with the first divergence for any failing twin.</summary>
        public static (bool AllMatch, string Text) Report()
        {
            var sb = new StringBuilder();
            var heddleRaw = new HeddleTest().Render();
            var oracle = TwinContent.Normalize(heddleRaw);
            sb.AppendLine("D1-R3 parity check (normalized: collapse whitespace runs between tags)");
            sb.AppendLine($"  Heddle oracle: {heddleRaw.Length} raw chars, {oracle.Length} normalized chars");
            var all = true;
            foreach (var (name, render) in Twins())
            {
                var raw = render();
                var actual = TwinContent.Normalize(raw);
                var match = string.Equals(oracle, actual, StringComparison.Ordinal);
                all &= match;
                sb.AppendLine(match
                    ? $"  [PASS] {name,-11} {raw.Length} raw chars, {actual.Length} normalized chars == Heddle"
                    : $"  [FAIL] {name,-11} {Describe(oracle, actual)}");
            }
            sb.AppendLine(all ? "ALL TWINS MATCH." : "PARITY FAILED.");
            return (all, sb.ToString());
        }

        // ---- Phase 5 additive workloads (trivial-substitution, large-loop). The home members
        // above are untouched; these siblings reuse the same normalize-and-compare discipline. ----

        public static IEnumerable<(string Name, Func<string> Render)> TwinsSubstitution()
        {
            yield return ("Fluid", () => new SubstitutionFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new SubstitutionScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new SubstitutionDotLiquidTest().Render());
            yield return ("Handlebars", () => new SubstitutionHandlebarsTest().Render());
        }

        public static IEnumerable<(string Name, Func<string> Render)> TwinsLoop()
        {
            yield return ("Fluid", () => new LoopFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new LoopScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new LoopDotLiquidTest().Render());
            yield return ("Handlebars", () => new LoopHandlebarsTest().Render());
        }

        /// <summary>Throws if any trivial-substitution twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertSubstitution()
            => AssertAgainst(new SubstitutionHeddleTest().Render(), TwinsSubstitution());

        /// <summary>Throws if any large-loop twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertLoop()
            => AssertAgainst(new LoopHeddleTest().Render(), TwinsLoop());

        /// <summary>Pass/fail report for the trivial-substitution workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportSubstitution()
            => ReportAgainst("trivial-substitution", new SubstitutionHeddleTest().Render(), TwinsSubstitution());

        /// <summary>Pass/fail report for the large-loop workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportLoop()
            => ReportAgainst("large-loop", new LoopHeddleTest().Render(), TwinsLoop());

        private static void AssertAgainst(string heddleRaw, IEnumerable<(string Name, Func<string> Render)> twins)
        {
            var oracle = TwinContent.Normalize(heddleRaw);
            foreach (var (name, render) in twins)
            {
                var actual = TwinContent.Normalize(render());
                if (!string.Equals(oracle, actual, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Parity twin '{name}' diverged from Heddle. {Describe(oracle, actual)}");
            }
        }

        private static (bool AllMatch, string Text) ReportAgainst(
            string workload, string heddleRaw, IEnumerable<(string Name, Func<string> Render)> twins)
        {
            var sb = new StringBuilder();
            var oracle = TwinContent.Normalize(heddleRaw);
            sb.AppendLine($"Parity check — {workload} (normalized: collapse whitespace runs between tags)");
            sb.AppendLine($"  Heddle oracle: {heddleRaw.Length} raw chars, {oracle.Length} normalized chars");
            var all = true;
            foreach (var (name, render) in twins)
            {
                var raw = render();
                var actual = TwinContent.Normalize(raw);
                var match = string.Equals(oracle, actual, StringComparison.Ordinal);
                all &= match;
                sb.AppendLine(match
                    ? $"  [PASS] {name,-11} {raw.Length} raw chars, {actual.Length} normalized chars == Heddle"
                    : $"  [FAIL] {name,-11} {Describe(oracle, actual)}");
            }
            sb.AppendLine(all ? "ALL TWINS MATCH." : "PARITY FAILED.");
            return (all, sb.ToString());
        }

        private static string Describe(string expected, string actual)
        {
            if (expected.Length != actual.Length && (expected.Length == 0 || actual.Length == 0))
                return $"expected {expected.Length} chars, got {actual.Length}.";
            var n = Math.Min(expected.Length, actual.Length);
            var i = 0;
            while (i < n && expected[i] == actual[i]) i++;
            var from = Math.Max(0, i - 40);
            string Slice(string s) => s.Substring(from, Math.Min(120, s.Length - from)).Replace("\n", "\\n");
            return $"first diff at index {i} (of exp {expected.Length}/act {actual.Length}).\n" +
                   $"    expected: ...{Slice(expected)}...\n" +
                   $"    actual:   ...{Slice(actual)}...";
        }
    }
}
