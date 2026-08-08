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
            // Since the E20 full-page redesign the nav needles and counts are COMPUTED — walked
            // from the very NavData model every engine renders from. The chrome and fragment
            // anchors are literal needles (E22): all of that text now lives in the templates, so
            // there is no C# fixture left to compute them from; the byte gate and verify-corpus
            // freshness police the template text itself.
            var nav = NavData.Model();

            var totalLinks = 0;
            var totalColumns = 0;
            void CountColumn(NavColumn column)
            {
                totalColumns++;
                foreach (var s in column.Sections) totalLinks += s.Links.Count;
            }
            foreach (var menu in nav.Menus)
                foreach (var tab in menu.Tabs)
                    foreach (var column in tab.Columns)
                        CountColumn(column);
            foreach (var column in nav.FooterColumns) CountColumn(column);

            // A unique deep link, resolved from the model so the needle cannot drift from it.
            var privacy = nav.FooterColumns
                .SelectMany(c => c.Sections).SelectMany(s => s.Links)
                .Single(l => string.Equals(l.Href, "/content/privacy-policy", StringComparison.Ordinal));
            var privacyNeedle =
                $"<li class=\"nav-link\"><a href=\"{privacy.Href}\">{privacy.Label}</a></li>";

            // The layout's <meta> section default, as the template spells it.
            const string sectionMeta = "<title>Title</title>";

            // The slider fragment composed-page.heddle splices into the layout's @out() slot. Pinned as the
            // removed-row corruption so an idiomatic page with an EMPTY body fails the verifier.
            const string sliderSegment =
                "<img src=\"/files/homepage/homebtmbanners/gluten-hp.jpg\" width=\"984\" border=\"0\" />";

            // One anchor unique to each mega menu (the menus are near-identical; these hrefs are
            // the wholesale-only and retail-only rows). Swapped as the reordered corruption.
            const string wholesaleAnchor = "/product/coming-soon-paleo-pork";
            const string retailAnchor = "/products/paleo-friendly-pork";

            var markers = new[]
            {
                // Full-page shape, in document order: doctype, head, alert blob, header chrome,
                // secondary-menu blobs, the two mega menus, the spliced body, footer chrome,
                // closing script, closing tag.
                "<!DOCTYPE html>",
                sectionMeta,
                "xmas-shipping-alert-1.jpg",           // the alert_top fragment
                "id=\"search-area-form\"",
                "/content/wholesale-promotions",       // secondary_wholesale_menu fragment
                "/content/request-catalog\">Catalog</a>", // secondary_retail_menu fragment
                ">Shop All Products</a>",
                wholesaleAnchor,
                retailAnchor,
                "homebtmbanners/gluten-hp.jpg",
                "/Assets/images/seeourcatalog.jpg",
                "<script src=\"/bodyend.js\"></script>", // the body_end_scripts fragment
                "</html>",
            };

            return new Authored
            {
                Definition = Def("composed-page", "raw",
                    values: new[]
                    {
                        V(sectionMeta, 1),
                        // Every nav link renders exactly one of these (mega menus + footer).
                        V("<li class=\"nav-link\"><a href=\"", totalLinks),
                        // Every nav column (mega menus + the four footer columns).
                        V("<div class=\"nav-column\">", totalColumns),
                        // The first footer column's title — proves the footer rendered.
                        V("<span class=\"nav-title\">Need Help?</span>", 1),
                        V(privacyNeedle, 1),
                    },
                    markers: markers),
                // Delete the spliced slider body (an empty idiomatic body must FAIL); swap the
                // wholesale-only and retail-only mega anchors. Both trip the ordered-markers check.
                RemovedSegment = sliderSegment,
                RemovedKind = "marker",
                SwapA = wholesaleAnchor,
                SwapB = retailAnchor,
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

        private static Authored FragmentHeavy()
        {
            // The first row is a tile; its whole fragment is the removed-row corruption, computed
            // from the model so the pin cannot drift. Rows 0 and 24 are both tiles (i % 4 == 0),
            // row 47 a stat — the three name markers cover the dispatch cycle end to end.
            var row0 = FragmentContent.Model().Items[0];
            var firstTile = "<section class=\"tile\"><h3>" + row0.Name + "</h3><p class=\"v\">"
                + row0.Value.ToString(CultureInfo.InvariantCulture)
                + "</p><span class=\"badge\">" + row0.Badge + "</span></section>";

            return new Authored
            {
                Definition = Def("fragment-heavy", "raw",
                    values: new[]
                    {
                        // 12 of each fragment kind (48 rows, i % 4 dispatch)...
                        V("<section class=\"tile\">", 12),
                        V("<article class=\"card\">", 12),
                        V("<div class=\"media-row\">", 12),
                        V("<div class=\"stat\">", 12),
                        // ...and the nested badge/price partials once per card — proves the
                        // nesting level actually ran.
                        V("<span class=\"promo-badge\">", 12),
                        V("<p class=\"price\">", 12),
                        V("item-00", 1),
                        V("item-47", 1),
                    },
                    markers: new[] { "<div class=\"panel\">", "item-00", "item-24", "item-47", "</div>" }),
                RemovedSegment = firstTile,
                RemovedKind = "value",
                SwapA = "item-00",
                SwapB = "item-24",
            };
        }

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
