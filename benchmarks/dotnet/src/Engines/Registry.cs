using System;
using System.Collections.Generic;
using System.Linq;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// One gated cell: an engine rendering one workload on one track. Mirrors
    /// <c>benchmarks/rust/src/gates.rs</c>'s <c>Cell</c> and
    /// <c>benchmarks/go/suites/gate_test.go</c>'s cell table — the harness-wide registry that the
    /// gate walks and the benches index into, so a cell cannot be benchmarked without being gated.
    /// </summary>
    public sealed class Cell
    {
        /// <summary>Display name used in gate output, bench ids and the report tables.</summary>
        public string Engine { get; init; }

        /// <summary><c>controlled</c> | <c>idiomatic</c>.</summary>
        public string Track { get; init; }

        /// <summary>One of the eight protocol workload ids.</summary>
        public string Workload { get; init; }

        /// <summary>Renders the cell, returning the output as a string for gating.</summary>
        public Func<string> Render { get; init; }

        /// <summary>
        /// True when this cell is the engine's RANKED row in the cross-stack sweep. All six Heddle
        /// techniques are gated; the STRING sink carries this flag, because it is like-for-like with
        /// the five .NET competitors and with every other ecosystem's materialised native string.
        /// The utf8 and textwriter sinks are still measured in the same sweep, as non-ranked
        /// technique rows the report quarantines from every ranking and margin, and are compared
        /// exhaustively against the precompiled backend by <c>bench-techniques</c>.
        /// </summary>
        public bool InCrossStack { get; init; }

        public override string ToString() => $"{Engine}/{Track}/{Workload}";
    }

    /// <summary>The harness-wide cell registry, assembled from every engine module.</summary>
    public static class Registry
    {
        private static List<Cell> _all;

        /// <summary>Every gated cell, engine-major then workload in protocol order.</summary>
        public static IReadOnlyList<Cell> All => _all ??= Build();

        public static IEnumerable<Cell> Controlled => All.Where(c => c.Track == "controlled");

        public static IEnumerable<Cell> Idiomatic => All.Where(c => c.Track == "idiomatic");

        /// <summary>The cells the cross-stack sweep measures.</summary>
        public static IEnumerable<Cell> CrossStack => All.Where(c => c.InCrossStack);

        private static List<Cell> Build()
        {
            var cells = new List<Cell>();
            // Both tracks, every engine. .NET shipped controlled-only for a long stretch,
            // which is why it was absent from every idiomatic table.
            foreach (var track in new[] { "controlled", "idiomatic" })
            {
                cells.AddRange(HeddleEngine.Cells(track));
                cells.AddRange(FluidEngine.Cells(track));
                cells.AddRange(ScribanEngine.Cells(track));
                cells.AddRange(DotLiquidEngine.Cells(track));
                cells.AddRange(HandlebarsEngine.Cells(track));
                cells.AddRange(RazorEngine.Cells(track));
            }
            // The build-time compiled backend, controlled track only and only for the workloads the
            // generator actually covers -- see PrecompiledBackend for why coverage is discovered.
            cells.AddRange(Precompiled.Cells());
            return cells;
        }
    }
}
