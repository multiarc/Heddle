using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Heddle;
using Heddle.Benchmarks.Dotnet.Gate;
using Heddle.Benchmarks.Dotnet.Models;
using Heddle.Data;
using Heddle.Precompiled;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Heddle's BUILD-TIME compiled backend: the same three sinks reached through a precompiled
    /// entry rather than one the runtime built from the template text. Three of the six render
    /// techniques.
    ///
    /// <para><b>Coverage is discovered, never assumed.</b> A workload without a registered entry
    /// is served from the dynamic path — correct behaviour for an application and a trap for a
    /// benchmark: rendering through the fallback and labelling the number "precompiled" would
    /// report the runtime backend under the wrong name, which is the same class of defect as
    /// timing a rope you never flatten. So this module asks the registry which keys actually
    /// have entries and registers cells for those only.</para>
    ///
    /// <para><b>Phase 2 tier:</b> the controlled-track entry templates precompile through
    /// <c>Heddle.Build</c> (see the project file) into this assembly's artifact. Rendering goes
    /// through the PUBLIC typed route — <see cref="PrecompiledTemplates.DefaultOptions"/> set to
    /// the cell's options, <see cref="PrecompiledTemplates.BindTyped"/> once per workload, then
    /// <see cref="HeddleTemplate.Generate"/> on the three sinks — the same objects a generated
    /// wrapper uses. This harness deliberately is not one of the engine's friend assemblies: a
    /// benchmark that needs private access to the thing it benchmarks is measuring something no
    /// user can reach.</para>
    /// </summary>
    public static class Precompiled
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static Dictionary<string, PrecompiledTemplateInfo> _entries;
        private static readonly Dictionary<string, HeddleTemplate> _bound =
            new Dictionary<string, HeddleTemplate>(StringComparer.Ordinal);

        /// <summary>Template key for a workload — every entry template is named for its
        /// workload, composed-page included. The project file pins these bare-name keys through
        /// the <c>Key</c> item metadata, so they match the generator-era keys.</summary>
        private static string KeyFor(string workload)
            => workload + ".heddle";

        /// <summary>Per-workload compile settings: output profile, expression tier, model type.
        /// Mirrors the runtime backend's table (HeddleEngine.Specs) so both backends compile and
        /// validate under the same options.</summary>
        private static readonly (string Workload, OutputProfile Profile, ExpressionMode Mode, Type ModelType)[] Specs =
        {
            ("composed-page", OutputProfile.Text, ExpressionMode.Native, typeof(ComposedModel)),
            ("trivial-substitution", OutputProfile.Text, ExpressionMode.Native, typeof(SubstitutionContent.SubstitutionModel)),
            ("large-loop", OutputProfile.Text, ExpressionMode.Native, typeof(LoopContent.LoopModel)),
            ("mixed-page", OutputProfile.Text, ExpressionMode.Native, typeof(MixedContent.MixedModel)),
            ("conditional-heavy", OutputProfile.Text, ExpressionMode.Native, typeof(ConditionalContent.ConditionalModel)),
            ("fragment-heavy", OutputProfile.Text, ExpressionMode.Native, typeof(FragmentContent.FragmentModel)),
            ("fortunes-encoded", OutputProfile.Html, ExpressionMode.Native, typeof(FortunesContent.FortuneModel)),
            ("encoded-loop", OutputProfile.Html, ExpressionMode.Native, typeof(EncodedLoopContent.EncodedLoopModel)),
        };

        /// <summary>The precompiled entries this ONE assembly carries, keyed by template key.</summary>
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

        /// <summary>The bound typed entry for one workload, bound once. Sets
        /// <see cref="PrecompiledTemplates.DefaultOptions"/> to the cell's options before binding,
        /// exactly as a generated wrapper's host does.</summary>
        /// <summary>The bound template for a workload, for a caller that renders it many times: the
        /// technique suite holds it across its iterations so a timed render is one <c>Generate</c> call,
        /// not a lookup plus one.</summary>
        internal static HeddleTemplate BoundFor(string workload) => Bound(workload);

        private static HeddleTemplate Bound(string workload)
        {
            if (_bound.TryGetValue(workload, out var cached)) return cached;
            var spec = Array.Find(Specs, s => s.Workload == workload);
            if (spec.Workload == null) throw new ArgumentException($"unknown workload '{workload}'", nameof(workload));
            if (!Entries().ContainsKey(KeyFor(workload)))
                throw new InvalidOperationException(
                    $"no precompiled entry for '{workload}' — this cell should not have been registered.");
            PrecompiledTemplates.DefaultOptions = new TemplateOptions("benchmarks-controlled")
            {
                OutputProfile = spec.Profile,
                ExpressionMode = spec.Mode,
            };
            var bound = PrecompiledTemplates.BindTyped(typeof(Precompiled).Assembly, KeyFor(workload), spec.ModelType);
            _bound[workload] = bound;
            return bound;
        }

        /// <summary>Renders through one sink and returns the output as a string, for gating.</summary>
        public static string Render(string track, string workload, HeddleEngine.Sink sink)
        {
            var bound = Bound(workload);
            var model = HeddleEngine.ModelFor(workload);
            switch (sink)
            {
                case HeddleEngine.Sink.String:
                    return bound.Generate(model);

                case HeddleEngine.Sink.TextWriter:
                {
                    var writer = new Materialisation.ChecksumTextWriter();
                    bound.Generate(model, writer);
                    return bound.Generate(model);
                }

                case HeddleEngine.Sink.Utf8:
                {
                    var buffer = new Materialisation.ChecksumBufferWriter();
                    bound.Generate(model, buffer);
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
            => Bound(workload).Generate(model, buffer);

        /// <inheritdoc cref="RenderToBuffer"/>
        public static void RenderToWriter(string workload, TextWriter writer, object model)
            => Bound(workload).Generate(model, writer);

        /// <inheritdoc cref="RenderToBuffer"/>
        public static string RenderToString(string workload, object model)
            => Bound(workload).Generate(model);

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
