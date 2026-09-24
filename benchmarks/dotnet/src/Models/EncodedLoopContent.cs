// The fixtures every engine renders this workload from.
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
    /// Shared, engine-neutral model for the encoded-loop workload:
    /// 5,000 rows with characters from the five-character escapable set in every cell and the
    /// Japanese run <c>こんにちは</c> in every comment, so the escaper both rewrites entities and
    /// scans multi-byte UTF-8 it must pass through. Every per-engine view is materialized once
    /// (static).
    /// </summary>
    public static class EncodedLoopContent
    {
        public sealed class EncodedLoopModel
        {
            public List<EncodedLoopRow> Items { get; set; }
        }

        public sealed class EncodedLoopRow
        {
            public string Tag { get; set; }
            public string Name { get; set; }
            public string Comment { get; set; }
        }

        public const int RowCount = 5000;

        private static readonly EncodedLoopModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static EncodedLoopModel BuildModel()
        {
            var items = new List<EncodedLoopRow>(RowCount);
            for (var i = 0; i < RowCount; i++)
                items.Add(new EncodedLoopRow
                {
                    Tag = $"tag-{i}&'{i % 7}'",
                    Name = $"item <{i}> & \"co\"",
                    Comment = $"'q' & <angle> \"d\" こんにちは {i}",
                });
            return new EncodedLoopModel { Items = items };
        }

        private static Hash BuildDotLiquid()
        {
            var items = new List<Hash>(RowCount);
            foreach (var row in Shared.Items)
                items.Add(new Hash
                {
                    ["tag"] = row.Tag,
                    ["name"] = row.Name,
                    ["comment"] = row.Comment,
                });
            return new Hash { ["items"] = items };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var items = new List<Dictionary<string, object>>(RowCount);
            foreach (var row in Shared.Items)
                items.Add(new Dictionary<string, object>
                {
                    ["tag"] = row.Tag,
                    ["name"] = row.Name,
                    ["comment"] = row.Comment,
                });
            return new Dictionary<string, object> { ["items"] = items };
        }

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static EncodedLoopModel Model() => Shared;

        /// <summary>DotLiquid view: a <see cref="Hash"/> whose <c>items</c> is a list of row hashes.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Lowercase snake_case dictionary view for the Fluid/Scriban twins.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Lowercase snake_case dictionary view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
