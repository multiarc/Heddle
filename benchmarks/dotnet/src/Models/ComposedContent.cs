// The composed-page model every engine renders from (ledger E20).
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
    /// The composed-page model: the structured navigation (<see cref="NavData"/>). Top-level so
    /// the Heddle layout definition can name it by short name (<c>:: ComposedModel</c>) through
    /// the configured assembly namespaces.
    /// </summary>
    public sealed class ComposedModel
    {
        public NavModel Nav { get; set; }
    }

    /// <summary>
    /// Shared, engine-neutral model for the composed-page workload (cross-stack phase 1 WI1,
    /// redesigned under ledger E20): a genuine full page. The four inert area blobs stay in
    /// <see cref="AreaData"/> and render through each engine's component/lookup mechanism; the
    /// structured navigation here renders through loops and nested partials. Every per-engine view
    /// is materialized once (static).
    /// </summary>
    public static class ComposedContent
    {
        private static readonly ComposedModel Shared = new ComposedModel { Nav = NavData.Model() };

        private static readonly Hash SharedDotLiquid = new Hash { ["nav"] = NavData.DotLiquidModel() };

        private static readonly Dictionary<string, object> SharedDictionary = new Dictionary<string, object>
        {
            ["nav"] = NavData.DictionaryModel(),
        };

        /// <summary>The Heddle-typed model (the oracle's compile context binds to it).</summary>
        public static ComposedModel Model() => Shared;

        /// <summary>DotLiquid view: <c>nav</c> holding nested <see cref="Hash"/>es.</summary>
        public static Hash DotLiquidModel() => SharedDotLiquid;

        /// <summary>Lowercase snake_case dictionary view for the Fluid/Scriban twins.</summary>
        public static Dictionary<string, object> LiquidModel() => SharedDictionary;

        /// <summary>Lowercase snake_case dictionary view for the Handlebars twin.</summary>
        public static Dictionary<string, object> HandlebarsModel() => SharedDictionary;
    }
}
