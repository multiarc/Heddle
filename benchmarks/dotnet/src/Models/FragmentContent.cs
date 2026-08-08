// The fixtures every engine renders this workload from (ledger E20; supersedes the E8 single-tile
// shape).
//
// Load-bearing values: the golden corpus is Heddle's render OF THESE, so changing one changes the
// oracle every ecosystem is gated against. Change them only with a corpus re-export in the same
// commit.
//
// The types are PUBLIC, and that is the point rather than an accident: Razor's views are compiled
// at runtime into a separate assembly, so an `internal` model would be unreachable to exactly one
// engine and would force a bespoke projection for it -- a second copy of the data, free to drift.
// One public fixture, read by all six, makes drift impossible instead of merely unlikely.
using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Benchmarks.Dotnet.Models
{
    /// <summary>
    /// Shared, engine-neutral model for the fragment-heavy workload (cross-stack phase 1 WI1,
    /// redesigned under ledger E20): 48 rows of FOUR distinct fragment kinds
    /// (<c>tile</c>/<c>card</c>/<c>media</c>/<c>stat</c>, 12 each), dispatched per row over the
    /// precomputed <c>IsTile</c>/<c>IsCard</c>/<c>IsMedia</c>/<c>IsStat</c> booleans (guaranteed
    /// common-denominator dispatch — no engine compares strings), with one level of nesting: the
    /// <c>card</c> fragment renders <c>badge</c> and <c>price</c> sub-partials against the row's
    /// <see cref="FragmentPromo"/>. Per-row dispatch plus nested per-call composition is the
    /// dimension measured. Every per-engine view is materialized once (static).
    /// </summary>
    public static class FragmentContent
    {
        public sealed class FragmentModel
        {
            public List<FragmentRow> Items { get; set; }
        }

        public sealed class FragmentRow
        {
            /// <summary>"tile" | "card" | "media" | "stat" — informational; engines dispatch on
            /// the booleans below, never on this string.</summary>
            public string Kind { get; set; }
            public bool IsTile { get; set; }
            public bool IsCard { get; set; }
            public bool IsMedia { get; set; }
            public bool IsStat { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public string Badge { get; set; }
            /// <summary>Media rows render it; blank elsewhere.</summary>
            public string Caption { get; set; }
            /// <summary>Media rows render it; blank elsewhere.</summary>
            public string ImageUrl { get; set; }
            /// <summary>Stat rows render it.</summary>
            public int Delta { get; set; }
            /// <summary>The nesting level: the card fragment renders badge + price from it.
            /// Present on every row so no engine needs a null guard.</summary>
            public FragmentPromo Promo { get; set; }
        }

        public sealed class FragmentPromo
        {
            public string Label { get; set; }
            public string Price { get; set; }
        }

        public const int RowCount = 48;

        private static readonly string[] Kinds = { "tile", "card", "media", "stat" };
        private static readonly string[] Badges = { "new", "hot", "sale", "std" };

        private static readonly FragmentModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static FragmentModel BuildModel()
        {
            var items = new List<FragmentRow>(RowCount);
            for (var i = 0; i < RowCount; i++)
            {
                var kind = Kinds[i % 4];
                items.Add(new FragmentRow
                {
                    Kind = kind,
                    IsTile = kind == "tile",
                    IsCard = kind == "card",
                    IsMedia = kind == "media",
                    IsStat = kind == "stat",
                    Name = $"item-{i:D2}",
                    Value = i * 11,
                    Badge = Badges[i % 4],
                    Caption = kind == "media" ? $"Caption for item-{i:D2}" : "",
                    ImageUrl = kind == "media" ? $"/img/item-{i:D2}.jpg" : "",
                    Delta = i % 7 - 3,
                    Promo = new FragmentPromo
                    {
                        Label = Badges[i % 4],
                        Price = $"{9 + i}.99",
                    },
                });
            }
            return new FragmentModel { Items = items };
        }

        private static Hash BuildDotLiquid()
        {
            var items = new List<Hash>(RowCount);
            foreach (var row in Shared.Items)
                items.Add(new Hash
                {
                    ["kind"] = row.Kind,
                    ["is_tile"] = row.IsTile,
                    ["is_card"] = row.IsCard,
                    ["is_media"] = row.IsMedia,
                    ["is_stat"] = row.IsStat,
                    ["name"] = row.Name,
                    ["value"] = row.Value,
                    ["badge"] = row.Badge,
                    ["caption"] = row.Caption,
                    ["image_url"] = row.ImageUrl,
                    ["delta"] = row.Delta,
                    ["promo"] = new Hash
                    {
                        ["label"] = row.Promo.Label,
                        ["price"] = row.Promo.Price,
                    },
                });
            return new Hash { ["items"] = items };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var items = new List<Dictionary<string, object>>(RowCount);
            foreach (var row in Shared.Items)
                items.Add(new Dictionary<string, object>
                {
                    ["kind"] = row.Kind,
                    ["is_tile"] = row.IsTile,
                    ["is_card"] = row.IsCard,
                    ["is_media"] = row.IsMedia,
                    ["is_stat"] = row.IsStat,
                    ["name"] = row.Name,
                    ["value"] = row.Value,
                    ["badge"] = row.Badge,
                    ["caption"] = row.Caption,
                    ["image_url"] = row.ImageUrl,
                    ["delta"] = row.Delta,
                    ["promo"] = new Dictionary<string, object>
                    {
                        ["label"] = row.Promo.Label,
                        ["price"] = row.Promo.Price,
                    },
                });
            return new Dictionary<string, object> { ["items"] = items };
        }

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static FragmentModel Model() => Shared;

        /// <summary>DotLiquid view: a <see cref="Hash"/> whose <c>items</c> is a list of row hashes.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Lowercase snake_case dictionary view for the Fluid/Scriban twins.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Lowercase snake_case dictionary view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
