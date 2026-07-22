using System;
using System.Collections.Generic;
using System.Text;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// D1-R3 parity gate. Every competitor twin must render output that is byte-identical to
    /// Heddle's after the documented normalization (<see cref="TwinContent.Normalize"/> —
    /// collapse whitespace runs between tags) followed by the contract-v2 N3b projection
    /// (every run of the six ASCII whitespace characters TAB/LF/VT/FF/CR/SPACE removed from both
    /// sides at comparison time, so the gate compares only non-whitespace bytes — spec D8/D9, a
    /// verdict-preserving loosening for the three anchors). <see cref="Assert"/> runs inside each
    /// render benchmark's GlobalSetup so a drifted twin fails loudly instead of benchmarking
    /// different work; <see cref="Report"/> is a fast, host-free harness
    /// (`dotnet run -c Release -- parity`).
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
            => AssertAgainst(new HeddleTest().Render(), Twins());

        /// <summary>Human-readable pass/fail report with the first divergence for any failing twin.</summary>
        public static (bool AllMatch, string Text) Report()
        {
            var sb = new StringBuilder();
            var heddleRaw = new HeddleTest().Render();
            var oracle = TwinContent.Normalize(heddleRaw);
            sb.AppendLine("D1-R3 parity check (normalized: collapse whitespace runs between tags)");
            sb.AppendLine($"  Heddle oracle: {heddleRaw.Length} raw chars, {oracle.Length} normalized chars");
            var oracleStripped = StripWhitespace(oracle);
            var all = true;
            foreach (var (name, render) in Twins())
            {
                var raw = render();
                var actual = TwinContent.Normalize(raw);
                var match = string.Equals(oracleStripped, StripWhitespace(actual), StringComparison.Ordinal);
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

        // ---- Cross-stack phase 1 workloads (mixed-page, conditional-heavy, fragment-heavy,
        // fortunes-encoded, encoded-loop). Same normalize-strip-and-compare discipline via the
        // shared helpers; Scriban rows exist only on net8.0+ (see csproj). ----

        public static IEnumerable<(string Name, Func<string> Render)> TwinsMixed()
        {
            yield return ("Fluid", () => new MixedFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new MixedScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new MixedDotLiquidTest().Render());
            yield return ("Handlebars", () => new MixedHandlebarsTest().Render());
        }

        public static IEnumerable<(string Name, Func<string> Render)> TwinsConditional()
        {
            yield return ("Fluid", () => new ConditionalFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new ConditionalScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new ConditionalDotLiquidTest().Render());
            yield return ("Handlebars", () => new ConditionalHandlebarsTest().Render());
        }

        public static IEnumerable<(string Name, Func<string> Render)> TwinsFragment()
        {
            yield return ("Fluid", () => new FragmentFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new FragmentScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new FragmentDotLiquidTest().Render());
            yield return ("Handlebars", () => new FragmentHandlebarsTest().Render());
        }

        public static IEnumerable<(string Name, Func<string> Render)> TwinsFortunes()
        {
            yield return ("Fluid", () => new FortunesFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new FortunesScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new FortunesDotLiquidTest().Render());
            yield return ("Handlebars", () => new FortunesHandlebarsTest().Render());
        }

        public static IEnumerable<(string Name, Func<string> Render)> TwinsEncodedLoop()
        {
            yield return ("Fluid", () => new EncodedLoopFluidTest().Render());
#if !NET6_0
            yield return ("Scriban", () => new EncodedLoopScribanTest().Render());
#endif
            yield return ("DotLiquid", () => new EncodedLoopDotLiquidTest().Render());
            yield return ("Handlebars", () => new EncodedLoopHandlebarsTest().Render());
        }

        /// <summary>Throws if any mixed-page twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertMixed()
            => AssertAgainst(new MixedHeddleTest().Render(), TwinsMixed());

        /// <summary>Throws if any conditional-heavy twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertConditional()
            => AssertAgainst(new ConditionalHeddleTest().Render(), TwinsConditional());

        /// <summary>Throws if any fragment-heavy twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertFragment()
            => AssertAgainst(new FragmentHeddleTest().Render(), TwinsFragment());

        /// <summary>Throws if any fortunes-encoded twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertFortunes()
            => AssertAgainst(new FortunesHeddleTest().Render(), TwinsFortunes());

        /// <summary>Throws if any encoded-loop twin diverges from Heddle. Called from GlobalSetup.</summary>
        public static void AssertEncodedLoop()
            => AssertAgainst(new EncodedLoopHeddleTest().Render(), TwinsEncodedLoop());

        /// <summary>Pass/fail report for the mixed-page workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportMixed()
            => ReportAgainst("mixed-page", new MixedHeddleTest().Render(), TwinsMixed());

        /// <summary>Pass/fail report for the conditional-heavy workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportConditional()
            => ReportAgainst("conditional-heavy", new ConditionalHeddleTest().Render(), TwinsConditional());

        /// <summary>Pass/fail report for the fragment-heavy workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportFragment()
            => ReportAgainst("fragment-heavy", new FragmentHeddleTest().Render(), TwinsFragment());

        /// <summary>Pass/fail report for the fortunes-encoded workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportFortunes()
            => ReportAgainst("fortunes-encoded", new FortunesHeddleTest().Render(), TwinsFortunes());

        /// <summary>Pass/fail report for the encoded-loop workload's twins.</summary>
        public static (bool AllMatch, string Text) ReportEncodedLoop()
            => ReportAgainst("encoded-loop", new EncodedLoopHeddleTest().Render(), TwinsEncodedLoop());

        private static void AssertAgainst(string heddleRaw, IEnumerable<(string Name, Func<string> Render)> twins)
        {
            var oracle = TwinContent.Normalize(heddleRaw);
            var oracleStripped = StripWhitespace(oracle);
            foreach (var (name, render) in twins)
            {
                var actual = TwinContent.Normalize(render());
                if (!string.Equals(oracleStripped, StripWhitespace(actual), StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Parity twin '{name}' diverged from Heddle. {Describe(oracle, actual)}");
            }
        }

        private static (bool AllMatch, string Text) ReportAgainst(
            string workload, string heddleRaw, IEnumerable<(string Name, Func<string> Render)> twins)
        {
            var sb = new StringBuilder();
            var oracle = TwinContent.Normalize(heddleRaw);
            var oracleStripped = StripWhitespace(oracle);
            sb.AppendLine($"Parity check — {workload} (normalized: collapse whitespace runs between tags)");
            sb.AppendLine($"  Heddle oracle: {heddleRaw.Length} raw chars, {oracle.Length} normalized chars");
            var all = true;
            foreach (var (name, render) in twins)
            {
                var raw = render();
                var actual = TwinContent.Normalize(raw);
                var match = string.Equals(oracleStripped, StripWhitespace(actual), StringComparison.Ordinal);
                all &= match;
                sb.AppendLine(match
                    ? $"  [PASS] {name,-11} {raw.Length} raw chars, {actual.Length} normalized chars == Heddle"
                    : $"  [FAIL] {name,-11} {Describe(oracle, actual)}");
            }
            sb.AppendLine(all ? "ALL TWINS MATCH." : "PARITY FAILED.");
            return (all, sb.ToString());
        }

        /// <summary>
        /// Contract-v2 step N3b: remove every run of the six ASCII whitespace characters
        /// (TAB, LF, VT, FF, CR, SPACE) entirely, so the ordinal compare sees only non-whitespace
        /// bytes. Applied to both sides at comparison time only — <see cref="Describe"/> excerpts
        /// keep the readable normalized form.
        /// </summary>
        private static string StripWhitespace(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (c != '\t' && c != '\n' && c != '\v' && c != '\f' && c != '\r' && c != ' ')
                    sb.Append(c);
            }
            return sb.ToString();
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
