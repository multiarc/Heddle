using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Shared machinery for the eight cross-stack suites — one suite per protocol workload, one
    /// <c>[Benchmark]</c> per engine, both fairness tracks (ledger E8/E9/E10).
    ///
    /// <para><b>The benchmark methods are declared on the concrete suites, not here.</b> A suite is
    /// six one-line methods over this base; the repetition is deliberate. Inherited
    /// <c>[Benchmark]</c> methods are discoverable but make the run's shape depend on a reflection
    /// rule rather than on something a reader can see, and a measurement harness is the last place
    /// to spend that. What lives here is everything that is genuinely common: the track parameter,
    /// the gate that must pass before anything is timed, cell resolution, and host teardown.</para>
    ///
    /// <para><b>Nothing is timed until it is gated.</b> <see cref="Setup"/> renders every engine's
    /// cell for this workload and asserts it — byte gate on the controlled track, functional
    /// verifier on the idiomatic one, security floor on the encoded workloads. A drifted twin
    /// therefore fails the run rather than contributing a number for different work, which is the
    /// property the whole program rests on.</para>
    /// </summary>
    [MemoryDiagnoser]
    public abstract class CrossStackSuite
    {
        /// <summary>
        /// The engines in one row each, keyed by the short name the report tables use. The key is
        /// what <c>Render&lt;Key&gt;</c> is named after and what the consolidation tool maps to a
        /// display name; the value is the registry's display name for the same engine.
        /// </summary>
        internal static readonly (string Key, string Engine)[] Engines =
        {
            ("Heddle", HeddleEngine.Name),
            ("Fluid", FluidEngine.Name),
            ("Scriban", ScribanEngine.Name),
            ("DotLiquid", DotLiquidEngine.Name),
            ("Handlebars", HandlebarsEngine.Name),
            ("Razor", RazorEngine.Name),
        };

        /// <summary>
        /// Both fairness tracks, measured in one sweep. The controlled track is the byte-identical
        /// fair fight; the idiomatic track is each engine authored the way its own documentation
        /// teaches. Neither is the "real" number on its own — the pair is the evidence.
        /// </summary>
        [Params("controlled", "idiomatic")]
        public string Track { get; set; }

        /// <summary>The protocol workload this suite measures.</summary>
        protected abstract string Workload { get; }

        private readonly Dictionary<string, Func<string>> _cells =
            new Dictionary<string, Func<string>>(StringComparer.Ordinal);

        /// <summary>
        /// Gates every engine's cell for this workload and track, in the very process that will do
        /// the timing.
        ///
        /// <para><b>All six, not just the one being measured</b> — BenchmarkDotNet runs one child
        /// process per case, so this is six times more gate work than each case strictly needs.
        /// The redundancy buys a stronger property than "this row was gated": every row is gated in
        /// the same process, against the same loaded corpus, on the same run. Two costs are worth
        /// stating rather than discovering. It adds setup time, which is not measured; and it builds
        /// the Razor MVC host — with its runtime-compilation file watchers — inside every child,
        /// including ones that never render through Razor. Those watchers are idle and are not in
        /// the timed region, but they are live during it.</para>
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            foreach (var (key, engine) in Engines)
            {
                var cell = Registry.All.FirstOrDefault(
                    c => c.InCrossStack && c.Track == Track && c.Workload == Workload && c.Engine == engine)
                    ?? throw new GateFailure(
                        $"[FAIL] no cross-stack cell registered for {engine} / {Track} / {Workload}");

                var output = cell.Render();
                if (Track == "controlled") Controlled.AssertCell(cell.Engine, cell.Workload, output);
                else Verifier.AssertCell(cell.Engine, cell.Workload, output);

                _cells[key] = cell.Render;
            }
        }

        /// <summary>
        /// Razor holds an MVC host with runtime-compilation file watchers. Without disposal the
        /// BenchmarkDotNet child process can outlive the run on Windows.
        /// </summary>
        [GlobalCleanup]
        public void Cleanup() => RazorEngine.Shutdown();

        /// <summary>Renders one gated competitor cell. The rendered string is returned so
        /// BenchmarkDotNet consumes it and the work cannot be eliminated.</summary>
        protected string Render(string engineKey) => _cells[engineKey]();

        /// <summary>
        /// Heddle's row, and the one place this harness deliberately does not render to a string.
        /// The UTF-8 sink is the path comparable with the other five ecosystems — they all emit
        /// UTF-8 or Latin-1 — and materialising a UTF-16 string here would measure an allocator
        /// cliff (the CLR's 85,000-byte Large Object Heap threshold) that no other ecosystem pays.
        /// The returned checksum is computed from the bytes the engine actually wrote, so the sink
        /// cannot win by doing less; see <c>Gate/Materialisation.cs</c>.
        /// </summary>
        protected ulong RenderHeddleUtf8()
            => HeddleEngine.RenderToSink(Track, Workload, HeddleEngine.Sink.Utf8);
    }
}
