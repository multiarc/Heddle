using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Heddle.Benchmarks.Dotnet.Gate;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Heddle's BUILD-TIME compiled backend: the same three sinks reached through a tree the
    /// source generator emitted at compile time rather than one the runtime built from the template
    /// text. Three of the six render techniques (ledger E10).
    ///
    /// <para><b>Coverage is discovered, never assumed.</b> The generator "simply leaves
    /// un-precompiled" any template its emitter does not yet cover — no source, no manifest entry,
    /// no diagnostic — and the engine then serves that key from the dynamic path. That is correct
    /// behaviour for an application and a trap for a benchmark: rendering through the fallback and
    /// labelling the number "precompiled" would report the runtime backend under the wrong name,
    /// which is the same class of defect as timing a rope you never flatten. So this module asks the
    /// manifest which keys are actually precompiled and registers cells for those only.</para>
    ///
    /// <para><b>Known coverage limit</b>, structural rather than incidental:</para>
    /// <list type="bullet">
    ///   <item>The two ENCODED workloads are excluded at the project level: HeddleOutputProfile is
    ///   compilation-wide and they need Html where the raw workloads need Text.</item>
    /// </list>
    ///
    /// <para>All six raw workloads are covered (6/8). The two historical <c>home.heddle</c>
    /// refusals are gone: the templates carry no embedded C# (extension arguments are native
    /// string literals), and the E20 layout-as-definition shape splices the body through the
    /// documented <c>@out()</c> slot instead of full-overriding a <c>body</c> region.</para>
    /// </summary>
    public static class Precompiled
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static Dictionary<string, PrecompiledTemplateInfo> _entries;

        /// <summary>Template key for a workload, matching the generator's filename-derived keys.</summary>
        private static string KeyFor(string workload)
            => (workload == "composed-page" ? "home" : workload) + ".heddle";

        /// <summary>The precompiled entries this assembly actually carries, keyed by template key.</summary>
        public static IReadOnlyDictionary<string, PrecompiledTemplateInfo> Entries()
        {
            if (_entries != null) return _entries;
            PrecompiledTemplates.Register(typeof(Precompiled).Assembly);
            _entries = PrecompiledTemplates.Entries.ToDictionary(e => e.Key, StringComparer.Ordinal);
            return _entries;
        }

        /// <summary>Workloads with a real precompiled entry, in protocol order.</summary>
        public static IEnumerable<string> CoveredWorkloads()
            => Corpus.GoldenCorpus.Workloads.Select(w => w.Id).Where(w => Entries().ContainsKey(KeyFor(w)));

        /// <summary>Workloads WITHOUT one, so the gate can state the gap instead of hiding it.</summary>
        public static IEnumerable<string> UncoveredWorkloads()
            => Corpus.GoldenCorpus.Workloads.Select(w => w.Id).Where(w => !Entries().ContainsKey(KeyFor(w)));

        private static IProcessStrategy Root(string workload)
        {
            if (!Entries().TryGetValue(KeyFor(workload), out var entry))
                throw new InvalidOperationException(
                    $"no precompiled entry for '{workload}' — this cell should not have been registered.");
            return entry.Strategy;
        }

        // No TemplateOptions are threaded through. The options-carrying PrecompiledRuntime
        // overloads are `internal` (reachable only from Heddle's friend assemblies), and this
        // harness deliberately is not one -- a benchmark that needs private access to the thing it
        // benchmarks is measuring something no user can reach. The public overloads are correct
        // here anyway: the generated tree was emitted under the compilation-wide
        // HeddleOutputProfile/HeddleExpressionMode, and the manifest records that fingerprint, so
        // the options are already baked into what is being rendered.

        /// <summary>Renders through one sink and returns the output as a string, for gating.</summary>
        public static string Render(string track, string workload, HeddleEngine.Sink sink)
        {
            var root = Root(workload);
            var model = HeddleEngine.ModelFor(workload);
            switch (sink)
            {
                case HeddleEngine.Sink.String:
                    return PrecompiledRuntime.GenerateString(root, model, null, null);

                case HeddleEngine.Sink.TextWriter:
                {
                    var writer = new Materialisation.ChecksumTextWriter();
                    PrecompiledRuntime.GenerateToWriter(root, model, null, null, writer);
                    return PrecompiledRuntime.GenerateString(root, model, null, null);
                }

                case HeddleEngine.Sink.Utf8:
                {
                    var buffer = new Materialisation.ChecksumBufferWriter();
                    PrecompiledRuntime.GenerateUtf8(root, model, null, null, buffer);
                    return Utf8NoBom.GetString(buffer.WrittenSpan);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(sink), sink, null);
            }
        }

        /// <summary>The bench path: render into a caller-owned sink. See
        /// <see cref="HeddleEngine.RenderToBuffer"/> for why the model and the sink are both the
        /// caller's. No <c>track</c> parameter: this backend is controlled-track only, and accepting
        /// one it then ignored invited idiomatic-track rows that were silently controlled numbers.</summary>
        public static void RenderToBuffer(string workload, IBufferWriter<byte> buffer, object model)
            => PrecompiledRuntime.GenerateUtf8(Root(workload), model, null, null, buffer);

        /// <inheritdoc cref="RenderToBuffer"/>
        public static void RenderToWriter(string workload, TextWriter writer, object model)
            => PrecompiledRuntime.GenerateToWriter(Root(workload), model, null, null, writer);

        /// <inheritdoc cref="RenderToBuffer"/>
        public static string RenderToString(string workload, object model)
            => PrecompiledRuntime.GenerateString(Root(workload), model, null, null);

        /// <summary>
        /// Gate cells for the covered workloads, controlled track only. Never in the cross-stack
        /// sweep: .NET contributes one Heddle row, and it is the runtime UTF-8 one.
        /// </summary>
        public static IEnumerable<Cell> Cells()
        {
            foreach (var workload in CoveredWorkloads())
            {
                foreach (var sink in new[] { HeddleEngine.Sink.Utf8, HeddleEngine.Sink.String, HeddleEngine.Sink.TextWriter })
                {
                    var capturedSink = sink;
                    var capturedWorkload = workload;
                    yield return new Cell
                    {
                        Engine = $"Heddle (precompiled/{HeddleEngine.SinkLabel(sink)})",
                        Track = "controlled",
                        Workload = workload,
                        InCrossStack = false,
                        Render = () => Render("controlled", capturedWorkload, capturedSink),
                    };
                }
            }
        }
    }
}
