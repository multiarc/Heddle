using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Shared, engine-neutral model for the fragment-heavy workload (cross-stack phase 1 WI1):
    /// 48 small rows, each rendered by one invocation of a <c>tile</c> partial receiving the
    /// current row — per-call composition overhead is the dimension measured. Every per-engine
    /// view is materialized once (static).
    /// </summary>
    internal static class FragmentContent
    {
        public sealed class FragmentModel
        {
            public List<FragmentRow> Items { get; set; }
        }

        public sealed class FragmentRow
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public string Badge { get; set; }
        }

        public const int RowCount = 48;

        private static readonly string[] Badges = { "new", "hot", "sale", "std" };

        private static readonly FragmentModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static FragmentModel BuildModel()
        {
            var items = new List<FragmentRow>(RowCount);
            for (var i = 0; i < RowCount; i++)
                items.Add(new FragmentRow
                {
                    Name = $"tile-{i:D2}",
                    Value = i * 11,
                    Badge = Badges[i % 4],
                });
            return new FragmentModel { Items = items };
        }

        private static Hash BuildDotLiquid()
        {
            var items = new List<Hash>(RowCount);
            foreach (var row in Shared.Items)
                items.Add(new Hash
                {
                    ["name"] = row.Name,
                    ["value"] = row.Value,
                    ["badge"] = row.Badge,
                });
            return new Hash { ["items"] = items };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var items = new List<Dictionary<string, object>>(RowCount);
            foreach (var row in Shared.Items)
                items.Add(new Dictionary<string, object>
                {
                    ["name"] = row.Name,
                    ["value"] = row.Value,
                    ["badge"] = row.Badge,
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
