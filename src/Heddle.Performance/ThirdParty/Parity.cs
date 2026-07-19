using System;
using System.Text;
using System.Text.RegularExpressions;
using Fluid.Benchmarks;

namespace Heddle.Performance.ThirdParty
{
    /// <summary>
    /// D3-R3 parity gate, mirroring D1's discipline: before any numbers are published, the Heddle
    /// entry's rendered output is asserted equivalent to the reference engine's output for the
    /// <em>unmodified</em> upstream scenario, and every engine's own <see cref="BaseBenchmarks.CheckBenchmark"/>
    /// output oracle is exercised (each engine's constructor runs it, so a drifted entry throws).
    ///
    /// <para>The reference oracle is Fluid rendering the vendored <c>product.liquid</c> (the scenario's
    /// own engine on the scenario's own template). Heddle renders <c>product.heddle</c>. Both outputs are
    /// compared after a single documented normalization (<see cref="Normalize"/>): unify line endings and
    /// collapse every run of whitespace to one space, then trim — the standard whitespace-insensitive HTML
    /// comparison, so only a genuine token/text difference (a wrong name, price, or truncation) can survive.</para>
    /// </summary>
    internal static class Parity
    {
        private static readonly Regex WhitespaceRuns = new Regex(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string html)
        {
            if (html == null) return string.Empty;
            html = html.Replace("\r\n", "\n").Replace("\r", "\n");
            html = WhitespaceRuns.Replace(html, " ");
            return html.Trim();
        }

        /// <summary>Human-readable pass/fail report; returns true when Heddle matches the Fluid reference.</summary>
        public static (bool Pass, string Text) Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine("D3-R3 parity check — Heddle entry vs Fluid reference on the upstream product scenario");
            sb.AppendLine("  (normalized: unify newlines, collapse whitespace runs to one space, trim)");

            // Constructing each entry runs its CheckBenchmark() — the upstream output oracle — and throws on drift.
            var fluidRaw = new FluidBenchmarks().Render();
            var scribanRaw = new ScribanBenchmarks().Render();
            var dotLiquidRaw = new DotLiquidBenchmarks().Render();
            var handlebarsRaw = new HandlebarsBenchmarks().Render();
            var heddleRaw = new HeddleBenchmarks().Render();

            sb.AppendLine("  [PASS] CheckBenchmark output oracle: Fluid, Scriban, DotLiquid, Handlebars, Heddle");

            var reference = Normalize(fluidRaw);
            var heddle = Normalize(heddleRaw);
            var pass = string.Equals(reference, heddle, StringComparison.Ordinal);

            sb.AppendLine($"  Fluid  reference: {fluidRaw.Length} raw chars, {reference.Length} normalized chars");
            sb.AppendLine($"  Heddle entry:     {heddleRaw.Length} raw chars, {heddle.Length} normalized chars");
            sb.AppendLine(pass
                ? "  [PASS] Heddle == Fluid reference (normalized)"
                : "  [FAIL] " + Describe(reference, heddle));

            // Sanity: the two other Liquid engines and Handlebars also match the reference (self-consistency).
            foreach (var (name, raw) in new[] { ("Scriban", scribanRaw), ("DotLiquid", dotLiquidRaw), ("Handlebars", handlebarsRaw) })
            {
                var actual = Normalize(raw);
                var match = string.Equals(reference, actual, StringComparison.Ordinal);
                pass &= match;
                sb.AppendLine(match
                    ? $"  [PASS] {name,-10} == Fluid reference (normalized)"
                    : $"  [FAIL] {name,-10} {Describe(reference, actual)}");
            }

            sb.AppendLine(pass ? "PARITY OK." : "PARITY FAILED.");
            return (pass, sb.ToString());
        }

        private static string Describe(string expected, string actual)
        {
            if (expected.Length == 0 || actual.Length == 0)
                return $"expected {expected.Length} chars, got {actual.Length}.";
            var n = Math.Min(expected.Length, actual.Length);
            var i = 0;
            while (i < n && expected[i] == actual[i]) i++;
            var from = Math.Max(0, i - 40);
            string Slice(string s) => s.Substring(from, Math.Min(120, s.Length - from));
            return $"first diff at index {i} (of exp {expected.Length}/act {actual.Length}).\n" +
                   $"    expected: ...{Slice(expected)}...\n" +
                   $"    actual:   ...{Slice(actual)}...";
        }
    }
}
