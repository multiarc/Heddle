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
    /// Shared, engine-neutral model for the large-loop workload: a single iteration
    /// over <see cref="RowCount"/> rows, each emitting two scalar members — output dominated by one
    /// large loop. The row payload is built once (static); each per-engine container is likewise
    /// materialized once, because the competitor engines resolve <c>item.name</c>/<c>item.value</c>
    /// on their native member-accessible containers (DotLiquid needs a <see cref="Hash"/>; Fluid's
    /// default member-access strategy denies un-registered POCO members), not on a plain CLR POCO.
    /// </summary>
    public static class LoopContent
    {
        public const int RowCount = 5000;

        public sealed class LoopRow
        {
            /// <summary>The one datum per row. The templates compose the display name
            /// (<c>row-@(Value)</c>) — the model never pre-formats it.</summary>
            public int Value { get; set; }
        }

        public sealed class LoopModel
        {
            public List<LoopRow> Items { get; set; }
        }

        private static readonly LoopModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static LoopModel BuildModel()
        {
            var items = new List<LoopRow>(RowCount);
            for (var i = 0; i < RowCount; i++)
                items.Add(new LoopRow { Value = i });
            return new LoopModel { Items = items };
        }

        private static Hash BuildDotLiquid()
        {
            var rows = new List<Hash>(RowCount);
            foreach (var row in Shared.Items)
                rows.Add(new Hash { ["value"] = row.Value });
            return new Hash { ["items"] = rows };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var rows = new List<Dictionary<string, object>>(RowCount);
            foreach (var row in Shared.Items)
                rows.Add(new Dictionary<string, object> { ["value"] = row.Value });
            return new Dictionary<string, object> { ["items"] = rows };
        }

        /// <summary>The Heddle-typed model — <c>@list(Items)</c> iterates the <see cref="LoopRow"/> list.</summary>
        public static LoopModel Model() => Shared;

        /// <summary>DotLiquid view: a <see cref="Hash"/> whose <c>items</c> is a list of row hashes.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Fluid view: dictionary rows resolved via <c>item.name</c>.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Handlebars view: dictionary rows resolved via <c>{{name}}</c> inside <c>{{#each}}</c>.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
