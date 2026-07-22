using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Single C# source of truth for the per-workload idiomatic-verifier definitions
    /// (spec D10; normative tables in
    /// docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/golden-corpus.md
    /// §Idiomatic verifier definitions, including the 2026-07-20 amendment weakening the
    /// text-context quote-entity needles). <c>export-corpus</c> writes each definition as
    /// <c>GoldenCorpus/&lt;id&gt;.verify.json</c> for cross-language consumption;
    /// <c>verify-corpus</c> runs the calibration (accept golden, reject the synthesized
    /// corruptions) against the committed corpus.
    ///
    /// Matching semantics (parity-contract-v2 §idiomatic-track gate): the candidate is
    /// normalized (N1–N4, N5 identity on the intra-.NET escapers), then the N3b whitespace
    /// strip is applied to the output AND to every needle before matching; <c>values</c> are
    /// exact non-overlapping counts, <c>markers</c> are strictly ordered, <c>forbidden</c>
    /// must be absent from both raw and normalized output, <c>required</c> is a minimum count.
    /// </summary>
    internal static class IdiomaticChecks
    {
        internal sealed class ValueCheck
        {
            public ValueCheck(string text, int count) { Text = text; Count = count; }
            public string Text { get; }
            public int Count { get; }
        }

        internal sealed class RequiredCheck
        {
            public RequiredCheck(string text, int minCount) { Text = text; MinCount = minCount; }
            public string Text { get; }
            public int MinCount { get; }
        }

        /// <summary>
        /// One workload's verifier definition plus its calibration corruption pins. Only the
        /// verifier fields (workload/suite/values/markers/forbidden/required) are exported to
        /// the .verify.json; the corruption pins drive <c>verify-corpus</c> calibration only.
        /// </summary>
        internal sealed class Definition
        {
            public string Workload;
            public string Suite;
            public ValueCheck[] Values = Array.Empty<ValueCheck>();
            public string[] Markers = Array.Empty<string>();
            public string[] Forbidden = Array.Empty<string>();
            public RequiredCheck[] Required = Array.Empty<RequiredCheck>();

            // ---- calibration pins (golden-corpus.md §Verification), not exported ----------

            /// <summary>'removed row': segment whose first occurrence is deleted from the golden.</summary>
            public string RemovedSegment;
            /// <summary>Check kind the removed-row corruption must trip (value|marker).</summary>
            public string RemovedKind;
            /// <summary>'reordered section': the two segments whose first occurrences are swapped (A before B).</summary>
            public string SwapA;
            public string SwapB;
            /// <summary>'unescaped payload' (encoded only): escaped form replaced by its raw form.</summary>
            public string UnescapeEscaped;
            public string UnescapeRaw;
        }

        /// <summary>All eight definitions, ordered by workload number (1–8).</summary>
        public static Definition[] All() => new[]
        {
            ComposedPage(),
            TrivialSubstitution(),
            LargeLoop(),
            MixedPage(),
            ConditionalHeavy(),
            FragmentHeavy(),
            FortunesEncoded(),
            EncodedLoop(),
        };

        public static Definition For(string workloadId)
        {
            foreach (var def in All())
                if (string.Equals(def.Workload, workloadId, StringComparison.Ordinal))
                    return def;
            throw new ArgumentException($"Unknown workload id '{workloadId}'.", nameof(workloadId));
        }

        // ---- per-workload definitions (normative tables transcribed / computed) -------------

        private static Definition ComposedPage()
        {
            // Needles are computed from the very members Heddle renders from (TwinContent /
            // AreaComponent.Areas), per the spec: SectionMeta whole, the first 60 chars of the
            // normalized "Footer Links" area, and one distinctive >= 20-char anchor per ordered
            // fragment — resolved literals land in the exported JSON.
            var sectionMeta = TwinContent.Normalize(TwinContent.SectionMeta);
            var footer60 = TwinContent.Normalize(TwinContent.Areas["Footer Links"]).Substring(0, 60);

            var markers = new[]
            {
                Pin(sectionMeta, sectionMeta),
                Pin(TwinContent.Normalize(TwinContent.SectionSocial),
                    "<meta property=\"og:image\" content=\"/files/catalog/img.jpg\">"),
                Pin(TwinContent.Normalize(TwinContent.CompAssetsStyles),
                    "<link rel=\"stylesheet\" href=\"/main.css\" />"),
                // Each non-empty AreaOrder area, in order ("Alert Top Section Below Nav" is empty
                // and skipped).
                PinArea("Alert Top Section Above Nav", "xmas-shipping-alert-1.jpg"),
                PinArea("Secondary Wholesale Menu", "/content/wholesale-promotions"),
                PinArea("Secondary Retail Menu", "/content/request-catalog\">Catalog</a>"),
                PinArea("Wholesale Top Mega Menu", "/product/coming-soon-paleo-pork"),
                PinArea("Retail Top Mega Menu", "/products/paleo-friendly-pork"),
                PinArea("Footer Links", "<div class=\"footer-links-column\">"),
                Pin(TwinContent.Normalize(TwinContent.CompBodyEndScripts),
                    "<script src=\"/bodyend.js\"></script>"),
            };

            return new Definition
            {
                Workload = "composed-page",
                Suite = "raw",
                Values = new[]
                {
                    new ValueCheck(sectionMeta, 1),
                    new ValueCheck(footer60, 1),
                },
                Markers = markers,
                // Corruptions (golden-corpus.md §Verification special pins): delete the
                // SectionSocial marker fragment; swap the leading SectionMeta and SectionSocial
                // marker fragments. Both trip the ordered-markers check.
                RemovedSegment = markers[1],
                RemovedKind = "marker",
                SwapA = markers[0],
                SwapB = markers[1],
            };
        }

        private static Definition TrivialSubstitution() => new Definition
        {
            Workload = "trivial-substitution",
            Suite = "raw",
            Values = new[]
            {
                new ValueCheck("Heddle Handbook", 1),
                new ValueCheck("HB-2001", 1),
                new ValueCheck("A concise field guide to the engine.", 1),
                new ValueCheck("4.8", 1),
            },
            Markers = new[] { "<article>", "<h1>", "class=\"sku\"", "class=\"rating\"", "</article>" },
            // Special pins: delete the HB-2001 sku value (values check trips 1 -> 0); swap the
            // class="sku" and class="rating" marker segments (markers-in-order check trips).
            RemovedSegment = "HB-2001",
            RemovedKind = "value",
            SwapA = "class=\"sku\"",
            SwapB = "class=\"rating\"",
        };

        private static Definition LargeLoop() => new Definition
        {
            Workload = "large-loop",
            Suite = "raw",
            Values = new[]
            {
                new ValueCheck("<tr><td>row-0</td><td>0</td></tr>", 1),
                new ValueCheck("<tr><td>row-4999</td><td>4999</td></tr>", 1),
                new ValueCheck("<tr><td>row-", 5000),
            },
            Markers = new[] { "row-0", "row-2500", "row-4999" },
            // Delete the full first <tr>…</tr> (row-0 value drops to 0); swap the row-0 and
            // row-2500 rows (markers-in-order trips).
            RemovedSegment = "<tr><td>row-0</td><td>0</td></tr>",
            RemovedKind = "value",
            SwapA = "<tr><td>row-0</td><td>0</td></tr>",
            SwapB = "<tr><td>row-2500</td><td>2500</td></tr>",
        };

        private static Definition MixedPage() => new Definition
        {
            Workload = "mixed-page",
            Suite = "raw",
            Values = new[]
            {
                new ValueCheck("Mercantile - Catalog", 1),
                new ValueCheck("Autumn hardware sale", 1),
                new ValueCheck("Product 01", 1),
                new ValueCheck("Product 36", 1),
                new ValueCheck("MX-1036", 1),
                new ValueCheck("<article class=\"card\">", 36),
                new ValueCheck("<p class=\"sale\">On sale</p>", 12),
                new ValueCheck("Free shipping on orders over 60.", 1),
            },
            Markers = new[]
            {
                "<!DOCTYPE html>", "<header>", "class=\"hero\"", "class=\"grid\"", "<footer>", "</html>",
            },
            RemovedSegment = "<article class=\"card\">",
            RemovedKind = "value",
            SwapA = "<header>",
            SwapB = "class=\"hero\"",
        };

        private static Definition ConditionalHeavy() => new Definition
        {
            Workload = "conditional-heavy",
            Suite = "raw",
            Values = new[]
            {
                new ValueCheck("unit-000", 1),
                new ValueCheck("unit-199", 1),
                new ValueCheck("<li>", 200),
                new ValueCheck("<span class=\"t0\">bronze</span>", 50),
                new ValueCheck("<span class=\"t3\">platinum</span>", 50),
                new ValueCheck("<small>", 100),
                new ValueCheck("<b>active</b>", 160),
            },
            Markers = new[] { "<ul class=\"matrix\">", "unit-000", "unit-100", "unit-199", "</ul>" },
            RemovedSegment = "unit-000",
            RemovedKind = "value",
            SwapA = "unit-000",
            SwapB = "unit-100",
        };

        private static Definition FragmentHeavy() => new Definition
        {
            Workload = "fragment-heavy",
            Suite = "raw",
            Values = new[]
            {
                new ValueCheck("<section class=\"tile\">", 48),
                new ValueCheck("tile-00", 1),
                new ValueCheck("tile-47", 1),
                new ValueCheck("<span class=\"badge\">new</span>", 12),
            },
            Markers = new[] { "<div class=\"panel\">", "tile-00", "tile-24", "tile-47", "</div>" },
            RemovedSegment = "tile-00",
            RemovedKind = "value",
            SwapA = "tile-00",
            SwapB = "tile-24",
        };

        private static Definition FortunesEncoded()
        {
            // Values: the escaped form of each of the 12 pinned messages -> 1 (ten are identical
            // to their raw form), except row 3 and row 11, whose text-context quote entities are
            // weakened to quote-agnostic substrings (2026-07-20 amendment); plus <tr><td> -> 12.
            var rows = FortunesContent.Model().Rows;
            var values = new List<ValueCheck>();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i == 2)
                {
                    // Row 3 — weakened: stops before the raw apostrophe in "aren't".
                    values.Add(new ValueCheck("A computer scientist is someone who fixes things that aren", 1));
                }
                else if (i == 10)
                {
                    // Row 11 (XSS payload) — weakened to the two quote-agnostic halves.
                    values.Add(new ValueCheck("&lt;script&gt;alert(", 1));
                    values.Add(new ValueCheck(");&lt;/script&gt;", 1));
                }
                else
                {
                    values.Add(new ValueCheck(WebUtility.HtmlEncode(rows[i].Message), 1));
                }
            }
            values.Add(new ValueCheck("<tr><td>", 12));

            var firstRow = string.Format(CultureInfo.InvariantCulture,
                "<tr><td>{0}</td><td>{1}</td></tr>", rows[0].Id, WebUtility.HtmlEncode(rows[0].Message));

            return new Definition
            {
                Workload = "fortunes-encoded",
                Suite = "encoded",
                Values = values.ToArray(),
                Markers = new[]
                {
                    "<!DOCTYPE html>", "<table>", "<tr><th>id</th><th>message</th></tr>",
                    "フレームワークのベンチマーク", "</table>",
                },
                Forbidden = new[] { "<script>alert(" },
                Required = new[]
                {
                    new RequiredCheck("&lt;script&gt;alert(", 1),
                    new RequiredCheck(");&lt;/script&gt;", 1),
                },
                RemovedSegment = firstRow,
                RemovedKind = "value",
                SwapA = "<tr><th>id</th><th>message</th></tr>",
                SwapB = "フレームワークのベンチマーク",
                UnescapeEscaped = "&lt;script&gt;alert(",
                UnescapeRaw = "<script>alert(",
            };
        }

        private static Definition EncodedLoop()
        {
            // Row 0's escaped cells, computed with the canonical escaper so the pins cannot
            // drift from the model (attribute and text spellings coincide on this data).
            var row0 = EncodedLoopContent.Model().Items[0];
            var tag0 = WebUtility.HtmlEncode(row0.Tag);                 // tag-0&amp;&#39;0&#39;
            var firstRow = "<tr><td data-tag=\"" + tag0 + "\">" + WebUtility.HtmlEncode(row0.Name)
                           + "</td><td>" + WebUtility.HtmlEncode(row0.Comment) + "</td></tr>";

            return new Definition
            {
                Workload = "encoded-loop",
                Suite = "encoded",
                Values = new[]
                {
                    // Attribute-context needle — deliberately NOT weakened (amendment).
                    new ValueCheck(tag0, 1),
                    // Text-context needle — quote entity weakened (amendment).
                    new ValueCheck("item &lt;4999&gt; &amp;", 1),
                    new ValueCheck("<tr><td data-tag=\"", 5000),
                    new ValueCheck("こんにちは", 5000),
                },
                Markers = new[] { tag0, "item &lt;2500&gt;", "こんにちは 4999" },
                Forbidden = new[] { "<script>alert(", "<angle>" },
                Required = new[] { new RequiredCheck("&lt;angle&gt;", 5000) },
                RemovedSegment = firstRow,
                RemovedKind = "value",
                SwapA = tag0,
                SwapB = "item &lt;2500&gt;",
                // The workload carries no script payload; its pinned escaped->raw corruption is
                // the comment's angle text, which must always be escaped (forbidden: <angle>).
                UnescapeEscaped = "&lt;angle&gt;",
                UnescapeRaw = "<angle>",
            };
        }

        // ---- verifier ------------------------------------------------------------------------

        /// <summary>
        /// Runs the verifier against a candidate output. Returns an empty list when accepted;
        /// otherwise one message per failed check, each starting with
        /// "verifier &lt;value|marker|forbidden|required&gt;: ".
        /// </summary>
        public static List<string> Verify(Definition def, string rawOutput)
        {
            var failures = new List<string>();
            var normalized = TwinContent.Normalize(rawOutput);
            var stripped = Strip(normalized);
            var strippedRaw = Strip(rawOutput);

            foreach (var v in def.Values)
            {
                var found = CountOccurrences(stripped, Strip(v.Text));
                if (found != v.Count)
                    failures.Add($"verifier value: expected {v.Count} of \"{Excerpt(v.Text)}\", found {found}");
            }

            var pos = 0;
            foreach (var marker in def.Markers)
            {
                var needle = Strip(marker);
                var at = stripped.IndexOf(needle, pos, StringComparison.Ordinal);
                if (at < 0)
                {
                    failures.Add($"verifier marker: expected 1 of \"{Excerpt(marker)}\" in order after index {pos}, found 0");
                    // Keep scanning subsequent markers from the current position so every
                    // out-of-order/missing marker is reported.
                    continue;
                }
                pos = at + needle.Length;
            }

            foreach (var f in def.Forbidden)
            {
                var needle = Strip(f);
                var found = Math.Max(CountOccurrences(strippedRaw, needle), CountOccurrences(stripped, needle));
                if (found != 0)
                    failures.Add($"verifier forbidden: expected 0 of \"{Excerpt(f)}\", found {found}");
            }

            foreach (var r in def.Required)
            {
                var found = CountOccurrences(stripped, Strip(r.Text));
                if (found < r.MinCount)
                    failures.Add($"verifier required: expected {r.MinCount} of \"{Excerpt(r.Text)}\", found {found}");
            }

            return failures;
        }

        /// <summary>The exported cross-language form of a definition (UTF-8, LF, 2-space indent).</summary>
        public static string ToJson(Definition def)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"workload\": ").Append(JsonString(def.Workload)).Append(",\n");
            sb.Append("  \"suite\": ").Append(JsonString(def.Suite)).Append(",\n");
            sb.Append("  \"values\": [\n");
            for (var i = 0; i < def.Values.Length; i++)
                sb.Append("    { \"text\": ").Append(JsonString(def.Values[i].Text))
                  .Append(", \"count\": ").Append(def.Values[i].Count.ToString(CultureInfo.InvariantCulture))
                  .Append(" }").Append(i < def.Values.Length - 1 ? "," : "").Append('\n');
            sb.Append("  ],\n");
            sb.Append("  \"markers\": [\n");
            for (var i = 0; i < def.Markers.Length; i++)
                sb.Append("    ").Append(JsonString(def.Markers[i]))
                  .Append(i < def.Markers.Length - 1 ? "," : "").Append('\n');
            sb.Append("  ],\n");
            sb.Append("  \"forbidden\": [");
            sb.Append(string.Join(", ", def.Forbidden.Select(JsonString)));
            sb.Append("],\n");
            sb.Append("  \"required\": [");
            for (var i = 0; i < def.Required.Length; i++)
                sb.Append(i > 0 ? ", " : "")
                  .Append("{ \"text\": ").Append(JsonString(def.Required[i].Text))
                  .Append(", \"minCount\": ").Append(def.Required[i].MinCount.ToString(CultureInfo.InvariantCulture))
                  .Append(" }");
            sb.Append("]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        // ---- helpers -------------------------------------------------------------------------

        /// <summary>N3b: remove every run of the six ASCII whitespace characters entirely.</summary>
        internal static string Strip(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (c != '\t' && c != '\n' && c != '\v' && c != '\f' && c != '\r' && c != ' ')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        internal static int CountOccurrences(string haystack, string needle)
        {
            if (needle.Length == 0) return 0;
            var count = 0;
            var index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private static string Excerpt(string s)
            => (s.Length <= 48 ? s : s.Substring(0, 48) + "…").Replace("\n", "\\n");

        /// <summary>Asserts the chosen anchor really is a substring of the member it stands for.</summary>
        private static string Pin(string normalizedFragment, string anchor)
        {
            if (anchor.Length < 20)
                throw new InvalidOperationException($"Marker anchor too short (<20 chars): \"{anchor}\"");
            if (normalizedFragment.IndexOf(anchor, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException($"Marker anchor not found in its source fragment: \"{Excerpt(anchor)}\"");
            return anchor;
        }

        private static string PinArea(string areaName, string anchor)
            => Pin(TwinContent.Normalize(TwinContent.Areas[areaName]), anchor);

        private static string JsonString(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
