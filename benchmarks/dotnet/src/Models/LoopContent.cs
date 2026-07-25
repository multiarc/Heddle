// Ported verbatim from the retired src/Heddle.Performance/Runners/LoopContent.cs (ledger E8).
// Values are byte-identical to the originals: this is a relocation and a visibility change, not a
// re-authoring. The types are PUBLIC here, which is the point -- Razor's runtime-compiled views
// live in a separate assembly and could not reach the old `internal` models, which is why the old
// harness needed a bespoke RazorTwinModel projection for the one workload Razor covered. Public
// models let every engine, Razor included, read the same fixtures and make drift impossible.
using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Benchmarks.Dotnet.Models
{
    /// <summary>
    /// Shared, engine-neutral model for the large-loop workload (phase 5 WI4): a single iteration
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
            public string Name { get; set; }
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
                items.Add(new LoopRow { Name = "row-" + i, Value = i });
            return new LoopModel { Items = items };
        }

        private static Hash BuildDotLiquid()
        {
            var rows = new List<Hash>(RowCount);
            foreach (var row in Shared.Items)
                rows.Add(new Hash { ["name"] = row.Name, ["value"] = row.Value });
            return new Hash { ["items"] = rows };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var rows = new List<Dictionary<string, object>>(RowCount);
            foreach (var row in Shared.Items)
                rows.Add(new Dictionary<string, object> { ["name"] = row.Name, ["value"] = row.Value });
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
