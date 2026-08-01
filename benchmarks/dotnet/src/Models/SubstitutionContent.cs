// The fixtures every engine renders this workload from (ledger E8).
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

namespace Heddle.Benchmarks.Dotnet.Models
{
    /// <summary>
    /// Shared, engine-neutral model for the trivial-substitution workload (phase 5 WI4): one flat
    /// card dominated by ten scalar member substitutions with minimal literal glue and no
    /// composition (no layout, components, or loop) — the shape where Heddle's lead is expected to
    /// narrow or invert. The values are plain ASCII (no character any engine encodes differently)
    /// and every per-engine view is materialized once (static), so no benchmark op re-allocates
    /// the model.
    /// </summary>
    public static class SubstitutionContent
    {
        public sealed class SubstitutionModel
        {
            public string Title { get; set; }
            public string Sku { get; set; }
            public int Price { get; set; }
            public string Brand { get; set; }
            public string Category { get; set; }
            public string Availability { get; set; }
            public string Url { get; set; }
            public string ImageUrl { get; set; }
            public string Summary { get; set; }
            public string Rating { get; set; }
        }

        private static readonly SubstitutionModel Shared = new SubstitutionModel
        {
            Title = "Heddle Handbook",
            Sku = "HB-2001",
            Price = 4200,
            Brand = "Heddle Press",
            Category = "Reference",
            Availability = "In stock",
            Url = "/catalog/handbook",
            ImageUrl = "/img/handbook.png",
            Summary = "A concise field guide to the engine.",
            Rating = "4.8",
        };

        private static readonly Dictionary<string, object> SharedMap = new Dictionary<string, object>
        {
            ["title"] = Shared.Title,
            ["sku"] = Shared.Sku,
            ["price"] = Shared.Price,
            ["brand"] = Shared.Brand,
            ["category"] = Shared.Category,
            ["availability"] = Shared.Availability,
            ["url"] = Shared.Url,
            ["image_url"] = Shared.ImageUrl,
            ["summary"] = Shared.Summary,
            ["rating"] = Shared.Rating,
        };

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static SubstitutionModel Model() => Shared;

        /// <summary>Lowercase-keyed view for the Liquid twins (Fluid, and the Scriban ScriptObject source).</summary>
        public static Dictionary<string, object> LiquidModel() => SharedMap;

        /// <summary>Lowercase-keyed view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedMap;
    }
}
