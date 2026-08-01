using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Heddle.Benchmarks.Dotnet.Gate;
using Heddle.Benchmarks.Dotnet.Models;

namespace Heddle.Benchmarks.Dotnet.Corpus
{
    /// <summary>
    /// The authoring side of the idiomatic verifier: the eight per-workload definitions, in C#,
    /// from which <c>export-corpus</c> writes the committed <c>&lt;id&gt;.verify.json</c> files that
    /// all six ecosystems then read (normative tables in
    /// docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/golden-corpus.md
    /// §Idiomatic verifier definitions, including the 2026-07-20 amendment weakening the
    /// text-context quote-entity needles).
    ///
    /// <para><b>Authoring lives here; matching lives in <see cref="Verifier"/>.</b> That split is
    /// deliberate. The gate must read the same committed JSON every other ecosystem reads, or the
    /// .NET leg would be checking a definition nobody else can see; but the JSON has to be produced
    /// from something, and needles computed from the model fixtures cannot drift the way transcribed
    /// literals can. So the needles below are derived from the very members the engines render from
    /// wherever the spec allows it, and <see cref="Pin"/> asserts that each hand-chosen anchor really
    /// does occur in the fragment it stands for.</para>
    ///
    /// <para>Each definition also carries its <b>calibration pins</b> — the corruptions
    /// <c>verify-corpus</c> synthesizes from the golden and requires the verifier to reject, with the
    /// right check kind. Those pins are never exported: a consumer needs the definition, not the
    /// recipe for breaking it.</para>
    /// </summary>
    public static class VerifierDefinitions
    {
        /// <summary>A definition plus the calibration pins that prove it discriminates.</summary>
        public sealed class Authored
        {
            public Verifier.Definition Definition { get; init; }

            /// <summary>'removed row': the segment whose first occurrence is deleted from the golden.</summary>
            public string RemovedSegment { get; init; }

            /// <summary>The check kind the removed-row corruption must trip: <c>value</c> or <c>marker</c>.</summary>
            public string RemovedKind { get; init; }

            /// <summary>'reordered section': the two segments whose first occurrences are swapped (A before B).</summary>
            public string SwapA { get; init; }
            public string SwapB { get; init; }

            /// <summary>'unescaped payload' (encoded suite only): the escaped form and its raw form.</summary>
            public string UnescapeEscaped { get; init; }
            public string UnescapeRaw { get; init; }
        }

        /// <summary>All eight, in protocol order.</summary>
        public static IReadOnlyList<Authored> All() => new[]
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

        public static Authored For(string workload)
            => All().FirstOrDefault(a => string.Equals(a.Definition.Workload, workload, StringComparison.Ordinal))
               ?? throw new ArgumentException($"unknown workload '{workload}'", nameof(workload));

        // ---- the eight definitions -------------------------------------------------------------

        private static Authored ComposedPage()
        {
            // Needles computed from the members the engines render from — TwinContent and AreaData —
            // rather than transcribed: SectionMeta whole, the first 60 characters of the normalized
            // "Footer Links" area, and one distinctive >= 20-character anchor per ordered fragment.
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
                // and is therefore skipped rather than pinned to nothing).
                PinArea("Alert Top Section Above Nav", "xmas-shipping-alert-1.jpg"),
                PinArea("Secondary Wholesale Menu", "/content/wholesale-promotions"),
                PinArea("Secondary Retail Menu", "/content/request-catalog\">Catalog</a>"),
                PinArea("Wholesale Top Mega Menu", "/product/coming-soon-paleo-pork"),
                PinArea("Retail Top Mega Menu", "/products/paleo-friendly-pork"),
                PinArea("Footer Links", "<div class=\"footer-links-column\">"),
                Pin(TwinContent.Normalize(TwinContent.CompBodyEndScripts),
                    "<script src=\"/bodyend.js\"></script>"),
            };

            return new Authored
            {
                Definition = Def("composed-page", "raw",
                    values: new[] { V(sectionMeta, 1), V(footer60, 1) },
                    markers: markers),
                // Delete the SectionSocial fragment; swap the leading SectionMeta and SectionSocial
                // fragments. Both trip the ordered-markers check.
                RemovedSegment = markers[1],
                RemovedKind = "marker",
                SwapA = markers[0],
                SwapB = markers[1],
            };
        }

        private static Authored TrivialSubstitution() => new Authored
        {
            Definition = Def("trivial-substitution", "raw",
                values: new[]
                {
                    V("Heddle Handbook", 1),
                    V("HB-2001", 1),
                    V("A concise field guide to the engine.", 1),
                    V("4.8", 1),
                },
                markers: new[] { "<article>", "<h1>", "class=\"sku\"", "class=\"rating\"", "</article>" }),
            RemovedSegment = "HB-2001",
            RemovedKind = "value",
            SwapA = "class=\"sku\"",
            SwapB = "class=\"rating\"",
        };

        private static Authored LargeLoop() => new Authored
        {
            Definition = Def("large-loop", "raw",
                values: new[]
                {
                    V("<tr><td>row-0</td><td>0</td></tr>", 1),
                    V("<tr><td>row-4999</td><td>4999</td></tr>", 1),
                    V("<tr><td>row-", 5000),
                },
                markers: new[] { "row-0", "row-2500", "row-4999" }),
            RemovedSegment = "<tr><td>row-0</td><td>0</td></tr>",
            RemovedKind = "value",
            SwapA = "<tr><td>row-0</td><td>0</td></tr>",
            SwapB = "<tr><td>row-2500</td><td>2500</td></tr>",
        };

        private static Authored MixedPage() => new Authored
        {
            Definition = Def("mixed-page", "raw",
                values: new[]
                {
                    V("Mercantile - Catalog", 1),
                    V("Autumn hardware sale", 1),
                    V("Product 01", 1),
                    V("Product 36", 1),
                    V("MX-1036", 1),
                    V("<article class=\"card\">", 36),
                    V("<p class=\"sale\">On sale</p>", 12),
                    V("Free shipping on orders over 60.", 1),
                },
                markers: new[]
                {
                    "<!DOCTYPE html>", "<header>", "class=\"hero\"", "class=\"grid\"", "<footer>", "</html>",
                }),
            RemovedSegment = "<article class=\"card\">",
            RemovedKind = "value",
            SwapA = "<header>",
            SwapB = "class=\"hero\"",
        };

        private static Authored ConditionalHeavy() => new Authored
        {
            Definition = Def("conditional-heavy", "raw",
                values: new[]
                {
                    V("unit-000", 1),
                    V("unit-199", 1),
                    V("<li>", 200),
                    V("<span class=\"t0\">bronze</span>", 50),
                    V("<span class=\"t3\">platinum</span>", 50),
                    V("<small>", 100),
                    V("<b>active</b>", 160),
                },
                markers: new[] { "<ul class=\"matrix\">", "unit-000", "unit-100", "unit-199", "</ul>" }),
            RemovedSegment = "unit-000",
            RemovedKind = "value",
            SwapA = "unit-000",
            SwapB = "unit-100",
        };

        private static Authored FragmentHeavy() => new Authored
        {
            Definition = Def("fragment-heavy", "raw",
                values: new[]
                {
                    V("<section class=\"tile\">", 48),
                    V("tile-00", 1),
                    V("tile-47", 1),
                    V("<span class=\"badge\">new</span>", 12),
                },
                markers: new[] { "<div class=\"panel\">", "tile-00", "tile-24", "tile-47", "</div>" }),
            RemovedSegment = "tile-00",
            RemovedKind = "value",
            SwapA = "tile-00",
            SwapB = "tile-24",
        };

        private static Authored FortunesEncoded()
        {
            // One value per pinned message, in the escaped form, computed from the model rather than
            // transcribed. Two rows carry the 2026-07-20 amendment: row 3's text-context apostrophe
            // and row 11's XSS payload are weakened to quote-agnostic substrings, because engines
            // legitimately differ on whether a text-context quote is entity-escaped at all.
            var rows = FortunesContent.Model().Rows;
            var values = new List<Verifier.ValueCheck>();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i == 2)
                {
                    // Row 3 — stops before the raw apostrophe in "aren't".
                    values.Add(V("A computer scientist is someone who fixes things that aren", 1));
                }
                else if (i == 10)
                {
                    // Row 11 — the two quote-agnostic halves of the payload.
                    values.Add(V("&lt;script&gt;alert(", 1));
                    values.Add(V(");&lt;/script&gt;", 1));
                }
                else
                {
                    values.Add(V(WebUtility.HtmlEncode(rows[i].Message), 1));
                }
            }
            values.Add(V("<tr><td>", 12));

            var firstRow = string.Format(CultureInfo.InvariantCulture,
                "<tr><td>{0}</td><td>{1}</td></tr>", rows[0].Id, WebUtility.HtmlEncode(rows[0].Message));

            return new Authored
            {
                Definition = Def("fortunes-encoded", "encoded",
                    values: values.ToArray(),
                    markers: new[]
                    {
                        "<!DOCTYPE html>", "<table>", "<tr><th>id</th><th>message</th></tr>",
                        "フレームワークのベンチマーク", "</table>",
                    },
                    forbidden: new[] { "<script>alert(" },
                    required: new[] { R("&lt;script&gt;alert(", 1), R(");&lt;/script&gt;", 1) }),
                RemovedSegment = firstRow,
                RemovedKind = "value",
                SwapA = "<tr><th>id</th><th>message</th></tr>",
                SwapB = "フレームワークのベンチマーク",
                UnescapeEscaped = "&lt;script&gt;alert(",
                UnescapeRaw = "<script>alert(",
            };
        }

        private static Authored EncodedLoop()
        {
            // Row 0's escaped cells, computed with the canonical escaper so the pins cannot drift
            // from the model. Attribute and text spellings coincide on this data.
            var row0 = EncodedLoopContent.Model().Items[0];
            var tag0 = WebUtility.HtmlEncode(row0.Tag);                 // tag-0&amp;&#39;0&#39;
            var firstRow = "<tr><td data-tag=\"" + tag0 + "\">" + WebUtility.HtmlEncode(row0.Name)
                           + "</td><td>" + WebUtility.HtmlEncode(row0.Comment) + "</td></tr>";

            return new Authored
            {
                Definition = Def("encoded-loop", "encoded",
                    values: new[]
                    {
                        // Attribute-context needle — deliberately NOT weakened by the amendment.
                        V(tag0, 1),
                        // Text-context needle — quote entity weakened.
                        V("item &lt;4999&gt; &amp;", 1),
                        V("<tr><td data-tag=\"", 5000),
                        V("こんにちは", 5000),
                    },
                    markers: new[] { tag0, "item &lt;2500&gt;", "こんにちは 4999" },
                    forbidden: new[] { "<script>alert(", "<angle>" },
                    required: new[] { R("&lt;angle&gt;", 5000) }),
                RemovedSegment = firstRow,
                RemovedKind = "value",
                SwapA = tag0,
                SwapB = "item &lt;2500&gt;",
                // This workload carries no script payload, so its escaped->raw corruption is the
                // comment's angle text, which must always be escaped (see forbidden, above).
                UnescapeEscaped = "&lt;angle&gt;",
                UnescapeRaw = "<angle>",
            };
        }

        // ---- export ----------------------------------------------------------------------------

        /// <summary>
        /// The exported cross-language form: UTF-8, LF, two-space indent, arrays one entry per line
        /// for values and markers and inline for the usually-empty forbidden/required lists. The
        /// layout is part of the committed artifact — a re-export that reflowed it would show every
        /// sidecar as changed and bury the one needle that actually moved.
        /// </summary>
        public static string ToJson(Verifier.Definition def)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"workload\": ").Append(JsonString(def.Workload)).Append(",\n");
            sb.Append("  \"suite\": ").Append(JsonString(def.Suite)).Append(",\n");
            sb.Append("  \"values\": [\n");
            for (var i = 0; i < def.Values.Count; i++)
                sb.Append("    { \"text\": ").Append(JsonString(def.Values[i].Text))
                  .Append(", \"count\": ").Append(def.Values[i].Count.ToString(CultureInfo.InvariantCulture))
                  .Append(" }").Append(i < def.Values.Count - 1 ? "," : "").Append('\n');
            sb.Append("  ],\n");
            sb.Append("  \"markers\": [\n");
            for (var i = 0; i < def.Markers.Count; i++)
                sb.Append("    ").Append(JsonString(def.Markers[i]))
                  .Append(i < def.Markers.Count - 1 ? "," : "").Append('\n');
            sb.Append("  ],\n");
            sb.Append("  \"forbidden\": [");
            sb.Append(string.Join(", ", def.Forbidden.Select(JsonString)));
            sb.Append("],\n");
            sb.Append("  \"required\": [");
            for (var i = 0; i < def.Required.Count; i++)
                sb.Append(i > 0 ? ", " : "")
                  .Append("{ \"text\": ").Append(JsonString(def.Required[i].Text))
                  .Append(", \"minCount\": ").Append(def.Required[i].MinCount.ToString(CultureInfo.InvariantCulture))
                  .Append(" }");
            sb.Append("]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        // ---- construction helpers --------------------------------------------------------------

        private static Verifier.Definition Def(
            string workload, string suite,
            Verifier.ValueCheck[] values = null,
            string[] markers = null,
            string[] forbidden = null,
            Verifier.RequiredCheck[] required = null)
            => new Verifier.Definition
            {
                Workload = workload,
                Suite = suite,
                Values = new List<Verifier.ValueCheck>(values ?? Array.Empty<Verifier.ValueCheck>()),
                Markers = new List<string>(markers ?? Array.Empty<string>()),
                Forbidden = new List<string>(forbidden ?? Array.Empty<string>()),
                Required = new List<Verifier.RequiredCheck>(required ?? Array.Empty<Verifier.RequiredCheck>()),
            };

        private static Verifier.ValueCheck V(string text, int count)
            => new Verifier.ValueCheck { Text = text, Count = count };

        private static Verifier.RequiredCheck R(string text, int minCount)
            => new Verifier.RequiredCheck { Text = text, MinCount = minCount };

        /// <summary>
        /// Asserts the chosen anchor really is a substring of the member it stands for, and is long
        /// enough to be distinctive. A marker that silently stopped matching its own fragment would
        /// make the verifier weaker without failing anything.
        /// </summary>
        private static string Pin(string normalizedFragment, string anchor)
        {
            if (anchor.Length < 20)
                throw new CorpusException($"marker anchor is shorter than 20 characters: \"{anchor}\"");
            if (normalizedFragment.IndexOf(anchor, StringComparison.Ordinal) < 0)
                throw new CorpusException($"marker anchor does not occur in its source fragment: \"{anchor}\"");
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
