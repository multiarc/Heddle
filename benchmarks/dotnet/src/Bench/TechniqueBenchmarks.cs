using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// <para><b>The streaming sinks are pre-sized and reused; the model is hoisted.</b> Both suites
    /// time exactly one engine call, into a sink that does nothing but advance an offset or perform
    /// one bulk copy — see <c>Bench/BenchSinks.cs</c>. Content is proven once per process, untimed,
    /// by the gate and by each suite's <c>[GlobalSetup]</c>, not re-proven on every iteration; the
    /// differential in <c>Gate/SelfTest.cs</c> shows all six paths agree byte for byte.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class TechniqueRuntimeBenchmarks
    {
        /// <summary>All eight workloads: the runtime backend covers the whole protocol set.</summary>
        public static IEnumerable<string> Workloads() => GoldenCorpus.Workloads.Select(w => w.Id);

        [ParamsSource(nameof(Workloads))]
        public string Workload { get; set; }

        private object _model;
        private BenchSinks.BenchBufferWriter _buffer;
        private BenchSinks.BenchTextWriter _writer;

        [GlobalSetup]
        public void Setup()
        {
            // Gate before timing, exactly as the cross-stack suites do: the string sink is asserted
            // against the corpus, and the technique differential proves the other two agree with it.
            var output = HeddleEngine.Render("controlled", Workload, HeddleEngine.Sink.String);
            Controlled.AssertCell(HeddleEngine.Name, Workload, output);

            // Hoisted so the timed region is the render alone. Previously ModelFor ran inside every
            // timed iteration here while the precompiled suite below hoisted it — an asymmetry that
            // handed the runtime side extra per-iteration work and understated its advantage.
            _model = HeddleEngine.ModelFor(Workload);
            _buffer = new BenchSinks.BenchBufferWriter(output.Length * 4 + 4096);
            _writer = new BenchSinks.BenchTextWriter(output.Length + 1024);
            TechniqueSetup.AssertSinks(output, _buffer, _writer,
                b => HeddleEngine.RenderToBuffer("controlled", Workload, b, _model),
                w => HeddleEngine.RenderToWriter("controlled", Workload, w, _model),
                Workload, "runtime");
        }

        [Benchmark(Baseline = true)]
        public int Utf8()
        {
            _buffer.Reset();
            HeddleEngine.RenderToBuffer("controlled", Workload, _buffer, _model);
            return _buffer.WrittenCount;
        }

        [Benchmark]
        public int TextWriter()
        {
            _writer.Reset();
            HeddleEngine.RenderToWriter("controlled", Workload, _writer, _model);
            return _writer.Length;
        }

        [Benchmark]
        public int String() => HeddleEngine.RenderToString("controlled", Workload, _model).Length;
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
        private BenchSinks.BenchBufferWriter _buffer;
        private BenchSinks.BenchTextWriter _writer;

        [GlobalSetup]
        public void Setup()
        {
            _model = HeddleEngine.ModelFor(Workload);
            var output = Engines.Precompiled.Render("controlled", Workload, HeddleEngine.Sink.String);
            Controlled.AssertCell("Heddle (precompiled/string)", Workload, output);

            _buffer = new BenchSinks.BenchBufferWriter(output.Length * 4 + 4096);
            _writer = new BenchSinks.BenchTextWriter(output.Length + 1024);
            TechniqueSetup.AssertSinks(output, _buffer, _writer,
                b => Engines.Precompiled.RenderToBuffer(Workload, b, _model),
                w => Engines.Precompiled.RenderToWriter(Workload, w, _model),
                Workload, "precompiled");
        }

        [Benchmark(Baseline = true)]
        public int Utf8()
        {
            _buffer.Reset();
            Engines.Precompiled.RenderToBuffer(Workload, _buffer, _model);
            return _buffer.WrittenCount;
        }

        [Benchmark]
        public int TextWriter()
        {
            _writer.Reset();
            Engines.Precompiled.RenderToWriter(Workload, _writer, _model);
            return _writer.Length;
        }

        [Benchmark]
        public int String() => Engines.Precompiled.RenderToString(Workload, _model).Length;
    }

    /// <summary>
    /// The once-per-process proof that the bench sinks see the whole output, shared by both technique
    /// suites. Runs in <c>[GlobalSetup]</c>, so it costs a timed iteration nothing.
    /// </summary>
    internal static class TechniqueSetup
    {
        public static void AssertSinks(string oracle,
            BenchSinks.BenchBufferWriter buffer, BenchSinks.BenchTextWriter writer,
            Action<BenchSinks.BenchBufferWriter> renderBuffer,
            Action<BenchSinks.BenchTextWriter> renderWriter,
            string workload, string backend)
        {
            renderBuffer(buffer);
            if (!string.Equals(Encoding.UTF8.GetString(buffer.WrittenSpan), oracle, StringComparison.Ordinal))
                throw new GateFailure(
                    $"[FAIL] {backend}/{workload}: the utf8 bench sink disagrees with the gated string render.");

            renderWriter(writer);
            if (!writer.WrittenSpan.SequenceEqual(oracle.AsSpan()))
                throw new GateFailure(
                    $"[FAIL] {backend}/{workload}: the textwriter bench sink disagrees with the gated string render.");

            buffer.Reset();
            writer.Reset();
        }
    }
}
