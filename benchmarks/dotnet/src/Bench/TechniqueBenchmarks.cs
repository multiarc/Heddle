using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Heddle.Benchmarks.Dotnet.Corpus;
using Heddle;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Benchmarks.Dotnet.Gate;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Heddle's render techniques against each other: three sinks — <c>string</c>,
    /// <see cref="System.IO.TextWriter"/>, UTF-8 <see cref="System.Buffers.IBufferWriter{T}"/> —
    /// across the two compilation backends, runtime and precompiled.
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
    /// The same three sinks reached through the build-time compiled backend: the compiled-form
    /// tier <c>Heddle.Build</c> embeds in this assembly, rendered through the public typed route
    /// (<c>BindTyped</c> once per workload, then <c>HeddleTemplate.Generate</c>).
    ///
    /// <para><b>Coverage is discovered from the registry, never assumed</b>, which is why this is a
    /// separate suite rather than three more methods on the runtime one. A workload without a
    /// registered entry renders from the dynamic path — so a suite that assumed coverage would
    /// time the runtime backend under the precompiled name. The parameter set below is the
    /// covered set; see <c>Engines/PrecompiledBackend.cs</c>.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class TechniquePrecompiledBenchmarks
    {
        /// <summary>Only the workloads this assembly actually carries a precompiled entry for.</summary>
        public static IEnumerable<string> Workloads() => Engines.Precompiled.CoveredWorkloads();

        [ParamsSource(nameof(Workloads))]
        public string Workload { get; set; }

        private object _model;
        private HeddleTemplate _bound;
        private BenchSinks.BenchBufferWriter _buffer;
        private BenchSinks.BenchTextWriter _writer;

        [GlobalSetup]
        public void Setup()
        {
            // Pinned on: the shared backend cache materializes once per workload per process, so this
            // suite states its arm up front rather than inheriting whatever a sibling suite bound first.
            AppContext.SetSwitch(TechniqueSetup.UseGeneratedSitesSwitch, true);
            _model = HeddleEngine.ModelFor(Workload);
            var output = Engines.Precompiled.Render("controlled", Workload, HeddleEngine.Sink.String);
            Controlled.AssertCell("Heddle (precompiled/string)", Workload, output);
            // The timed call is one Generate on the bound template, the same shape as the data-only
            // suite: routing every render through the backend per-workload lookup cost 24 B per render
            // that belonged to the harness, not the engine, and read as a difference between the arms.
            _bound = Engines.Precompiled.BoundFor(Workload);

            _buffer = new BenchSinks.BenchBufferWriter(output.Length * 4 + 4096);
            _writer = new BenchSinks.BenchTextWriter(output.Length + 1024);
            TechniqueSetup.AssertSinks(output, _buffer, _writer,
                b => _bound.Generate(_model, b),
                w => _bound.Generate(_model, w),
                Workload, "precompiled");
        }

        [Benchmark(Baseline = true)]
        public int Utf8()
        {
            _buffer.Reset();
            _bound.Generate(_model, _buffer);
            return _buffer.WrittenCount;
        }

        [Benchmark]
        public int TextWriter()
        {
            _writer.Reset();
            _bound.Generate(_model, _writer);
            return _writer.Length;
        }

        [Benchmark]
        public int String() => _bound.Generate(_model).Length;
    }

    /// <summary>
    /// The same three sinks with the site table off (P3-R8): the data path the "without" arm of the
    /// parity suite proves complete. The published table places each row beside
    /// <c>TechniqueRuntimeBenchmarks</c> on the same machine and job: mean within BenchmarkDotNet's
    /// reported error, allocated bytes equal.
    ///
    /// <para><b>Own materialization cache, deliberately.</b> <see cref="Engines.Precompiled"/> memoizes
    /// one bound entry per workload per process at whatever switch position bound first — sharing it
    /// would let whichever suite ran first pick the other suite's arm. This suite binds through the
    /// same public typed route (<c>BindTyped</c> once per workload, then
    /// <c>HeddleTemplate.Generate</c>) into its own cache, with the switch pinned off, so both arms
    /// stay honest in one process in either order.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class TechniquePrecompiledDataOnlyBenchmarks
    {
        /// <summary>Only the workloads this assembly actually carries a precompiled entry for.</summary>
        public static IEnumerable<string> Workloads() => Engines.Precompiled.CoveredWorkloads();

        [ParamsSource(nameof(Workloads))]
        public string Workload { get; set; }

        private object _model;
        private HeddleTemplate _bound;
        private BenchSinks.BenchBufferWriter _buffer;
        private BenchSinks.BenchTextWriter _writer;

        private static readonly Dictionary<string, HeddleTemplate> BoundByWorkload =
            new Dictionary<string, HeddleTemplate>(StringComparer.Ordinal);

        private static HeddleTemplate BoundDataOnly(string workload)
        {
            lock (BoundByWorkload)
            {
                if (BoundByWorkload.TryGetValue(workload, out var cached))
                    return cached;
                AppContext.SetSwitch(TechniqueSetup.UseGeneratedSitesSwitch, false);
                var assembly = typeof(Engines.Precompiled).Assembly;
                Heddle.Precompiled.PrecompiledTemplates.Register(assembly);
                Heddle.Precompiled.PrecompiledTemplateInfo entry = null;
                foreach (var candidate in Heddle.Precompiled.PrecompiledTemplates.Entries)
                    if (string.Equals(candidate.Key, workload + ".heddle", StringComparison.Ordinal))
                        entry = candidate;
                if (entry == null)
                    throw new InvalidOperationException(
                        $"no precompiled entry for '{workload}' — this cell should not have been registered.");
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions =
                    new Heddle.Data.TemplateOptions("benchmarks-data-only")
                    {
                        OutputProfile = entry.OptionsFingerprint.Profile,
                        ExpressionMode = entry.OptionsFingerprint.ExpressionMode,
                    };
                var bound = Heddle.Precompiled.PrecompiledTemplates.BindTyped(
                    assembly, workload + ".heddle", entry.ModelType);
                BoundByWorkload[workload] = bound;
                return bound;
            }
        }

        [GlobalSetup]
        public void Setup()
        {
            _model = HeddleEngine.ModelFor(Workload);
            _bound = BoundDataOnly(Workload);
            var output = _bound.Generate(_model);
            Controlled.AssertCell("Heddle (precompiled-data-only/string)", Workload, output);

            _buffer = new BenchSinks.BenchBufferWriter(output.Length * 4 + 4096);
            _writer = new BenchSinks.BenchTextWriter(output.Length + 1024);
            TechniqueSetup.AssertSinks(output, _buffer, _writer,
                b => _bound.Generate(_model, b),
                w => _bound.Generate(_model, w),
                Workload, "precompiled-data-only");
        }

        [Benchmark(Baseline = true)]
        public int Utf8()
        {
            _buffer.Reset();
            _bound.Generate(_model, _buffer);
            return _buffer.WrittenCount;
        }

        [Benchmark]
        public int TextWriter()
        {
            _writer.Reset();
            _bound.Generate(_model, _writer);
            return _writer.Length;
        }

        [Benchmark]
        public int String() => _bound.Generate(_model).Length;
    }

    /// <summary>
    /// The once-per-process proof that the bench sinks see the whole output, shared by both technique
    /// suites. Runs in <c>[GlobalSetup]</c>, so it costs a timed iteration nothing.
    /// </summary>
    internal static class TechniqueSetup
    {
        /// <summary>The public switch both precompiled technique suites pin before binding: the loader
        /// prefers a generated site unless this is <c>false</c> (default <c>true</c>).</summary>
        internal const string UseGeneratedSitesSwitch = "Heddle.Precompiled.UseGeneratedSites";

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
