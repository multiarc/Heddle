using System.Collections.Generic;
using DotLiquid;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Shared, engine-neutral model for the fortunes-encoded workload (cross-stack phase 1 WI1):
    /// the 12 pinned fortune rows — including the TechEmpower XSS payload (row 11) and the
    /// Japanese UTF-8 string (row 12) — rendered through each engine's escaping path in HTML text
    /// context. The strings are pinned byte-for-byte by the workloads spec; every per-engine view
    /// is materialized once (static).
    /// </summary>
    internal static class FortunesContent
    {
        public sealed class FortuneModel
        {
            public List<FortuneRow> Rows { get; set; }
        }

        public sealed class FortuneRow
        {
            public int Id { get; set; }
            public string Message { get; set; }
        }

        private static readonly string[] Messages =
        {
            "A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1",
            "A computer program does what you tell it to do, not what you want it to do.",
            "A computer scientist is someone who fixes things that aren't broken.",
            "A list is only as strong as its weakest link. — Donald Knuth",
            "After enough decimal places, nobody gives a damn.",
            "Any program that runs right is obsolete.",
            "Computers make very fast, very accurate mistakes.",
            "Emacs is a nice operating system, but I prefer UNIX. — Tom Christiansen",
            "Feature: A bug with seniority.",
            "fortune: No such file or directory",
            "<script>alert(\"This should not be displayed in a browser alert box.\");</script>",
            "フレームワークのベンチマーク",
        };

        private static readonly FortuneModel Shared = BuildModel();
        private static readonly Hash SharedDotLiquid = BuildDotLiquid();
        private static readonly Dictionary<string, object> SharedDictionary = BuildDictionary();

        private static FortuneModel BuildModel()
        {
            var rows = new List<FortuneRow>(Messages.Length);
            for (var i = 0; i < Messages.Length; i++)
                rows.Add(new FortuneRow { Id = i + 1, Message = Messages[i] });
            return new FortuneModel { Rows = rows };
        }

        private static Hash BuildDotLiquid()
        {
            var rows = new List<Hash>(Messages.Length);
            foreach (var row in Shared.Rows)
                rows.Add(new Hash { ["id"] = row.Id, ["message"] = row.Message });
            return new Hash { ["rows"] = rows };
        }

        private static Dictionary<string, object> BuildDictionary()
        {
            var rows = new List<Dictionary<string, object>>(Messages.Length);
            foreach (var row in Shared.Rows)
                rows.Add(new Dictionary<string, object> { ["id"] = row.Id, ["message"] = row.Message });
            return new Dictionary<string, object> { ["rows"] = rows };
        }

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static FortuneModel Model() => Shared;

        /// <summary>DotLiquid view: a <see cref="Hash"/> whose <c>rows</c> is a list of row hashes.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Lowercase snake_case dictionary view for the Fluid/Scriban twins.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Lowercase snake_case dictionary view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
