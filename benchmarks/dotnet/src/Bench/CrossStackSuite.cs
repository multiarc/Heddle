using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Shared machinery for the eight cross-stack suites — one suite per protocol workload, one
    /// <c>[Benchmark]</c> per engine, both fairness tracks.
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

        // Owned by the suite, reused across iterations, sized in Setup. See PrepareBenchSinks.
        private object _heddleModel;
        private BenchSinks.BenchBufferWriter _benchBuffer;
        private BenchSinks.BenchTextWriter _benchWriter;

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

            // Heddle's two streaming rows. Hoisting the model and pre-sizing the sinks here is what
            // keeps the timed region to the engine alone: the model allocation and the sink's buffer
            // are setup costs, not render costs, and the competitor rows pay neither.
            _heddleModel = HeddleEngine.ModelFor(Workload);
            PrepareBenchSinks(_cells["Heddle"]());
        }

        /// <summary>
        /// Sizes both bench sinks past their high-water mark and proves, in this process and before
        /// anything is timed, that each reproduces the gated string render exactly.
        ///
        /// <para>This is where the per-iteration checksum went. The gate writers fold FNV-1a over
        /// every unit — the TextWriter one through a per-char virtual call — which is correct for a
        /// gate and ruinous for a measurement: it taxed every timed iteration with harness work no
        /// competitor row paid, and because both backends paid the same constant it compressed the
        /// runtime-vs-precompiled ratio toward 1.0. Proving the property once per process gives the
        /// same guarantee for none of the cost.</para>
        /// </summary>
        private void PrepareBenchSinks(string oracle)
        {
            // Utf8ScopeRenderer.RenderSingle can reserve up to ~3x the char count on a GetSpan, so
            // size well past it: a resize inside the timed region would be timing Array.Resize.
            _benchBuffer = new BenchSinks.BenchBufferWriter(oracle.Length * 4 + 4096);
            _benchWriter = new BenchSinks.BenchTextWriter(oracle.Length + 1024);

            HeddleEngine.RenderToBuffer(Track, Workload, _benchBuffer, _heddleModel);
            if (!string.Equals(Encoding.UTF8.GetString(_benchBuffer.WrittenSpan), oracle, StringComparison.Ordinal))
                throw new GateFailure(
                    $"[FAIL] {Workload}/{Track}: the utf8 bench sink disagrees with the gated string render.");

            HeddleEngine.RenderToWriter(Track, Workload, _benchWriter, _heddleModel);
            if (!_benchWriter.WrittenSpan.SequenceEqual(oracle.AsSpan()))
                throw new GateFailure(
                    $"[FAIL] {Workload}/{Track}: the textwriter bench sink disagrees with the gated string render.");

            _benchBuffer.Reset();
            _benchWriter.Reset();
        }

        /// <summary>
        /// Razor holds an MVC host with runtime-compilation file watchers. Without disposal the
        /// BenchmarkDotNet child process can outlive the run on Windows.
        /// </summary>
        [GlobalCleanup]
        public void Cleanup() => RazorEngine.Shutdown();

        /// <summary>
        /// Renders one gated cell — including Heddle's. The rendered string is returned so
        /// BenchmarkDotNet consumes it and the work cannot be eliminated.
        ///
        /// <para><b>Every row, Heddle included, renders to a string.</b> Heddle's row used to render
        /// to the UTF-8 sink instead, reasoning that UTF-8 is what the other five ecosystems emit
        /// and that materialising a UTF-16 string would charge Heddle the CLR's 85,000-byte Large
        /// Object Heap cliff that Go, Rust and JS never pay. That rationale holds only ABOVE the
        /// threshold, and it was applied to all eight workloads. The five tier-1 workloads top out
        /// at 31,098 B as UTF-16 — roughly a third of the cliff — so those cells bought no
        /// protection and paid the sink's fixed ~65 KB buffer anyway: 2.40x on trivial-substitution,
        /// whose whole output is 338 B. It also exempted Heddle alone from a cost its five .NET
        /// competitors all pay, in the one table that compares them directly. Measuring the anchor
        /// differently from the field made every `vs Heddle` ratio in the program partly a
        /// measurement of output format.</para>
        ///
        /// <para>Heddle's other sinks are not hidden — <see cref="RenderHeddleUtf8Sink"/> and
        /// <see cref="RenderHeddleTextWriterSink"/> measure them as their own rows in this same
        /// sweep, so the UTF-8 advantage on large outputs is visible as a technique rather than
        /// baked silently into the anchor.</para>
        /// </summary>
        protected string Render(string engineKey) => _cells[engineKey]();

        /// <summary>
        /// Heddle's non-materialising sinks, measured as extra rows beside the anchor.
        ///
        /// <para>They stream into a pre-sized, reused sink (<see cref="BenchSinks"/>) and return the
        /// units written, so the timed region is the engine plus the cheapest honest consumer and
        /// nothing else. They are NOT like-for-like with the competitor rows, every one of which
        /// materialises a string, and the report keeps them out of the cross-stack ranking for that
        /// reason. They answer a different and equally real question: what does Heddle cost when the
        /// caller can stream.</para>
        /// </summary>
        protected int RenderHeddleUtf8Sink()
        {
            _benchBuffer.Reset();
            HeddleEngine.RenderToBuffer(Track, Workload, _benchBuffer, _heddleModel);
            return _benchBuffer.WrittenCount;
        }

        /// <inheritdoc cref="RenderHeddleUtf8Sink"/>
        protected int RenderHeddleTextWriterSink()
        {
            _benchWriter.Reset();
            HeddleEngine.RenderToWriter(Track, Workload, _benchWriter, _heddleModel);
            return _benchWriter.Length;
        }
    }
}
