using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Shared, engine-neutral model for the conditional-heavy workload (cross-stack phase 1 WI1):
    /// 200 rows, each carrying precomputed booleans for one four-way tier chain plus two
    /// independent toggles — every engine branches on the same booleans, so the workload measures
    /// branch dispatch on identical data. Every per-engine view is materialized once (static).
    /// </summary>
    internal static class ConditionalContent
    {
        public sealed class ConditionalModel
        {
            public List<ConditionalRow> Rows { get; set; }
        }

        public sealed class ConditionalRow
        {
            public string Name { get; set; }
            public string Note { get; set; }
            public bool IsBronze { get; set; }
            public bool IsSilver { get; set; }
            public bool IsGold { get; set; }
            public bool HasNote { get; set; }
            public bool IsActive { get; set; }
        }

        public const int RowCount = 200;

        private static readonly ConditionalModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static ConditionalModel BuildModel()
        {
            var rows = new List<ConditionalRow>(RowCount);
            for (var i = 0; i < RowCount; i++)
                rows.Add(new ConditionalRow
                {
                    Name = $"unit-{i:D3}",
                    Note = $"note {i}",
                    IsBronze = i % 4 == 0,
                    IsSilver = i % 4 == 1,
                    IsGold = i % 4 == 2,
                    HasNote = i % 2 == 0,
                    IsActive = i % 5 != 0,
                });
            return new ConditionalModel { Rows = rows };
        }

        private static Hash BuildDotLiquid()
        {
            var rows = new List<Hash>(RowCount);
            foreach (var row in Shared.Rows)
                rows.Add(new Hash
                {
                    ["name"] = row.Name,
                    ["note"] = row.Note,
                    ["is_bronze"] = row.IsBronze,
                    ["is_silver"] = row.IsSilver,
                    ["is_gold"] = row.IsGold,
                    ["has_note"] = row.HasNote,
                    ["is_active"] = row.IsActive,
                });
            return new Hash { ["rows"] = rows };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var rows = new List<Dictionary<string, object>>(RowCount);
            foreach (var row in Shared.Rows)
                rows.Add(new Dictionary<string, object>
                {
                    ["name"] = row.Name,
                    ["note"] = row.Note,
                    ["is_bronze"] = row.IsBronze,
                    ["is_silver"] = row.IsSilver,
                    ["is_gold"] = row.IsGold,
                    ["has_note"] = row.HasNote,
                    ["is_active"] = row.IsActive,
                });
            return new Dictionary<string, object> { ["rows"] = rows };
        }

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static ConditionalModel Model() => Shared;

        /// <summary>DotLiquid view: a <see cref="Hash"/> whose <c>rows</c> is a list of row hashes.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Lowercase snake_case dictionary view for the Fluid/Scriban twins.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Lowercase snake_case dictionary view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
