using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Heddle.Benchmarks.Dotnet.Corpus;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Heddle's render techniques against each other: three sinks — <c>string</c>,
    /// <see cref="System.IO.TextWriter"/>, UTF-8 <see cref="System.Buffers.IBufferWriter{T}"/> —
    /// across the two compilation backends, runtime and precompiled (ledger E10).
    ///
    /// <para><b>Not in the cross-stack sweep, by design.</b> One engine contributes one row to the
    /// comparison — the rule every other ecosystem follows — and that row is the runtime UTF-8 sink.
    /// These suites answer a different question, aimed at a Heddle user rather than at a
    /// cross-engine ranking: which of the six paths should I write, and what does the choice cost.
    /// Mixing them into the sweep would let one engine occupy six rows of a table every other
    /// ecosystem gets one row in.</para>
    ///
    /// <para><b>Every technique is measured through a checksum, never a materialised string.</b> The
    /// streaming sinks genuinely never allocate the full output; returning one would destroy the
    /// property being measured. The checksum is folded from the characters and bytes the engine
    /// actually produced, so a sink cannot win by eliding work — see <c>Gate/Materialisation.cs</c>,
    /// and the differential in <c>Gate/SelfTest.cs</c> that proves all six agree byte for byte.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class TechniqueRuntimeBenchmarks
    {
        /// <summary>All eight workloads: the runtime backend covers the whole protocol set.</summary>
        public static IEnumerable<string> Workloads() => GoldenCorpus.Workloads.Select(w => w.Id);

        [ParamsSource(nameof(Workloads))]
        public string Workload { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Gate before timing, exactly as the cross-stack suites do: the string sink is asserted
            // against the corpus, and the technique differential proves the other two agree with it.
            var output = HeddleEngine.Render("controlled", Workload, HeddleEngine.Sink.String);
            Controlled.AssertCell(HeddleEngine.Name, Workload, output);
        }

        [Benchmark(Baseline = true)]
        public ulong Utf8() => HeddleEngine.RenderToSink("controlled", Workload, HeddleEngine.Sink.Utf8);

        [Benchmark]
        public ulong TextWriter() => HeddleEngine.RenderToSink("controlled", Workload, HeddleEngine.Sink.TextWriter);

        [Benchmark]
        public ulong String() => HeddleEngine.RenderToSink("controlled", Workload, HeddleEngine.Sink.String);
    }

    /// <summary>
    /// The same three sinks reached through the build-time compiled backend.
    ///
    /// <para><b>Coverage is discovered from the manifest, never assumed</b>, which is why this is a
    /// separate suite rather than three more methods on the runtime one. The generator leaves a
    /// template its emitter does not cover un-precompiled and the engine then serves that key from
    /// the dynamic path — so a suite that assumed coverage would time the runtime backend under the
    /// precompiled name. The parameter set below is the covered set, and it is visibly shorter than
    /// the runtime suite's. That gap is the disclosure; see
    /// <c>Engines/PrecompiledBackend.cs</c> for what each uncovered workload is refused for.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class TechniquePrecompiledBenchmarks
    {
        /// <summary>Only the workloads this assembly actually carries a precompiled entry for.</summary>
        public static IEnumerable<string> Workloads() => Engines.Precompiled.CoveredWorkloads();

        [ParamsSource(nameof(Workloads))]
        public string Workload { get; set; }

        private object _model;

        [GlobalSetup]
        public void Setup()
        {
            _model = HeddleEngine.ModelFor(Workload);
            var output = Engines.Precompiled.Render("controlled", Workload, HeddleEngine.Sink.String);
            Controlled.AssertCell("Heddle (precompiled/string)", Workload, output);
        }

        [Benchmark(Baseline = true)]
        public ulong Utf8() => Engines.Precompiled.RenderToSink("controlled", Workload, HeddleEngine.Sink.Utf8, _model);

        [Benchmark]
        public ulong TextWriter() => Engines.Precompiled.RenderToSink("controlled", Workload, HeddleEngine.Sink.TextWriter, _model);

        [Benchmark]
        public ulong String() => Engines.Precompiled.RenderToSink("controlled", Workload, HeddleEngine.Sink.String, _model);
    }
}
