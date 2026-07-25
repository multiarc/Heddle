using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Heddle;
using Heddle.Benchmarks.Dotnet.Models;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Heddle, all eight workloads across every render technique it exposes (ledger E8/E10).
    ///
    /// <para><b>The techniques.</b> <see cref="HeddleTemplate"/> offers three sinks —
    /// <c>Generate(model)</c> returning a <see cref="string"/>, <c>Generate(model, TextWriter)</c>,
    /// and <c>Generate(model, IBufferWriter&lt;byte&gt;)</c> writing UTF-8 directly. All are
    /// implemented and gated here.</para>
    ///
    /// <para><b>Only UTF-8 is wired into the cross-stack sweep</b> (<see cref="Cell.InCrossStack"/>).
    /// One engine contributes one row — the rule every other ecosystem follows — and UTF-8 is the
    /// right row because it is the only .NET path directly comparable with the other five
    /// ecosystems, which all emit UTF-8 or Latin-1. It also matters concretely: .NET strings are
    /// UTF-16, so the string path materialises composed-page at 108,802 bytes, over the CLR's
    /// 85,000-byte Large Object Heap threshold, where copy throughput collapses ~4.5x and every
    /// render drives a full Gen2 collection. The same output as UTF-8 is 54,411 bytes and never
    /// crosses that line. Ranking .NET on the UTF-16 path was measuring an allocator cliff no other
    /// ecosystem pays.</para>
    ///
    /// <para>The other techniques stay gated and are compared against each other by
    /// <c>bench-techniques</c>, outside <c>run-all</c>.</para>
    /// </summary>
    public static class HeddleEngine
    {
        /// <summary>The cross-stack row name. The technique suffix is deliberate: the tables must
        /// say which path produced the number, because the six differ by more than noise.</summary>
        public const string Name = "Heddle (utf8)";

        /// <summary>How a rendered result is produced. Sink and backend are independent axes.</summary>
        public enum Sink
        {
            /// <summary><c>Generate(model)</c> -> string. UTF-16, allocates the full output.</summary>
            String,
            /// <summary><c>Generate(model, TextWriter)</c>. Streams chars; no full-output string.</summary>
            TextWriter,
            /// <summary><c>Generate(model, IBufferWriter&lt;byte&gt;)</c>. Streams UTF-8 bytes.</summary>
            Utf8,
        }

        /// <summary>Which compilation backend produced the template.</summary>
        public enum Backend
        {
            /// <summary>Compiled at runtime by <see cref="HeddleTemplate"/>.</summary>
            Runtime,
            /// <summary>Compiled at build time by Heddle.Generator (lands in W7).</summary>
            Precompiled,
        }

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Per-workload compile settings, ported from the retired *HeddleTest.cs.</summary>
        private static readonly (string Workload, OutputProfile Profile, ExpressionMode Mode, Type ModelType)[] Specs =
        {
            // composed-page's model is an empty object and its expressions are extension calls, so
            // it is the one workload on the FullCSharp tier with an untyped model.
            ("composed-page", OutputProfile.Text, ExpressionMode.FullCSharp, null),
            ("trivial-substitution", OutputProfile.Text, ExpressionMode.Native, typeof(SubstitutionContent.SubstitutionModel)),
            ("large-loop", OutputProfile.Text, ExpressionMode.Native, typeof(LoopContent.LoopModel)),
            ("mixed-page", OutputProfile.Text, ExpressionMode.Native, typeof(MixedContent.MixedModel)),
            ("conditional-heavy", OutputProfile.Text, ExpressionMode.Native, typeof(ConditionalContent.ConditionalModel)),
            ("fragment-heavy", OutputProfile.Text, ExpressionMode.Native, typeof(FragmentContent.FragmentModel)),
            ("fortunes-encoded", OutputProfile.Html, ExpressionMode.Native, typeof(FortunesContent.FortuneModel)),
            ("encoded-loop", OutputProfile.Html, ExpressionMode.Native, typeof(EncodedLoopContent.EncodedLoopModel)),
        };

        // Keyed by "<track>/<workload>": the two tracks compile different sources.
        private static readonly Dictionary<string, HeddleTemplate> Compiled =
            new Dictionary<string, HeddleTemplate>(StringComparer.Ordinal);

        private static bool _extensionsConfigured;

        /// <summary>The model instance each workload renders from.</summary>
        public static object ModelFor(string workload) => workload switch
        {
            // composed-page renders entirely from extensions and layout section defaults, so its
            // model is genuinely empty -- the same `new object()` the retired harness passed.
            "composed-page" => EmptyModel,
            "trivial-substitution" => SubstitutionContent.Model(),
            "large-loop" => LoopContent.Model(),
            "mixed-page" => MixedContent.Model(),
            "conditional-heavy" => ConditionalContent.Model(),
            "fragment-heavy" => FragmentContent.Model(),
            "fortunes-encoded" => FortunesContent.Model(),
            "encoded-loop" => EncodedLoopContent.Model(),
            _ => throw new ArgumentException($"unknown workload '{workload}'", nameof(workload)),
        };

        private static readonly object EmptyModel = new object();

        /// <summary>The runtime-compiled template for one workload, compiled once.</summary>
        public static HeddleTemplate Template(string track, string workload)
        {
            var key = track + "/" + workload;
            if (Compiled.TryGetValue(key, out var cached)) return cached;

            if (!_extensionsConfigured)
            {
                // Registers this assembly's [ExportExtensions] set (area_component and friends).
                // HeddleTemplate.Configure is the public entry point; the retired harness reached
                // the internal AssemblyHelper directly, which it could only do because it was
                // strong-named and named in Heddle's InternalsVisibleTo list. This harness is not,
                // and should not need to be, a friend assembly of the engine it measures.
                HeddleTemplate.Configure(typeof(HeddleEngine).Assembly);
                _extensionsConfigured = true;
            }

            var spec = Array.Find(Specs, s => s.Workload == workload);
            if (spec.Workload == null) throw new ArgumentException($"unknown workload '{workload}'", nameof(workload));

            var options = new TemplateOptions(spec.Workload == "composed-page" ? "home" : spec.Workload)
            {
                FileNamePostfix = ".heddle",
                RootPath = Path.Combine(Templates.Root(), track, "heddle"),
                OutputProfile = spec.Profile,
                ExpressionMode = spec.Mode,
                ProvideLanguageFeatures = false,
            };

            var context = spec.ModelType == null
                ? new CompileContext(options)
                : new CompileContext(options, spec.ModelType);

            var template = new HeddleTemplate(context);
            Compiled[key] = template;
            return template;
        }

        /// <summary>Renders one workload through one sink, returning the output as a string.</summary>
        public static string Render(string track, string workload, Sink sink)
        {
            var template = Template(track, workload);
            var model = ModelFor(workload);

            switch (sink)
            {
                case Sink.String:
                    return template.Generate(model);

                case Sink.TextWriter:
                {
                    // A StringWriter would make this the string path with extra steps. The checksum
                    // writer reads every character instead, which is what the technique claims to do
                    // and what a counting writer would not prove.
                    var writer = new Gate.Materialisation.ChecksumTextWriter();
                    template.Generate(model, writer);
                    LastTextWriterHash = writer.Hash;
                    LastTextWriterCount = writer.Count;
                    // Re-rendered to a string only so the GATE has something to compare; the bench
                    // path calls RenderToSink, which never materialises a string at all.
                    return template.Generate(model);
                }

                case Sink.Utf8:
                {
                    var buffer = new Gate.Materialisation.ChecksumBufferWriter();
                    template.Generate(model, buffer);
                    LastUtf8Hash = buffer.Hash;
                    LastUtf8ByteCount = buffer.WrittenCount;
                    return Utf8NoBom.GetString(buffer.WrittenSpan);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(sink), sink, null);
            }
        }

        /// <summary>
        /// Gate cells: every workload through every runtime sink. Only the UTF-8 sink carries
        /// <see cref="Cell.InCrossStack"/>; the rest are gated but stay out of the sweep.
        ///
        /// Gating all three matters more than benchmarking all three: the gate runs inside
        /// <c>run-all</c>, so a sink that stops producing correct bytes fails the sweep even though
        /// nothing measures it there.
        /// </summary>
        public static IEnumerable<Cell> Cells(string track)
        {
            foreach (var (workload, _, _, _) in Specs)
            {
                foreach (var sink in new[] { Sink.Utf8, Sink.String, Sink.TextWriter })
                {
                    var capturedSink = sink;
                    var capturedTrack = track;
                    var capturedWorkload = workload;
                    yield return new Cell
                    {
                        Engine = sink == Sink.Utf8 ? Name : $"Heddle ({SinkLabel(sink)})",
                        Track = track,
                        Workload = workload,
                        InCrossStack = sink == Sink.Utf8,
                        Render = () => Render(capturedTrack, capturedWorkload, capturedSink),
                    };
                }
            }
        }

        /// <summary>Last observed sink telemetry, used by the gate's materialisation assertions.</summary>
        public static ulong LastTextWriterHash { get; private set; }
        public static long LastTextWriterCount { get; private set; }
        public static ulong LastUtf8Hash { get; private set; }
        public static int LastUtf8ByteCount { get; private set; }

        /// <summary>
        /// The BENCH path: renders through a sink and returns a checksum, never a string.
        ///
        /// This is what <c>bench-techniques</c> times. The string sink returns its output's length
        /// folded into the same hash space so all three are comparable, but the streaming sinks
        /// genuinely never allocate the full output -- which is the entire point of measuring them,
        /// and would be destroyed by materialising a string to return.
        /// </summary>
        public static ulong RenderToSink(string track, string workload, Sink sink, Backend backend = Backend.Runtime)
        {
            var model = ModelFor(workload);

            if (backend == Backend.Precompiled)
                return Precompiled.RenderToSink(track, workload, sink, model);

            var template = Template(track, workload);
            switch (sink)
            {
                case Sink.String:
                    return Gate.Materialisation.HashOf(template.Generate(model));
                case Sink.TextWriter:
                {
                    var writer = new Gate.Materialisation.ChecksumTextWriter();
                    template.Generate(model, writer);
                    return writer.Hash;
                }
                case Sink.Utf8:
                {
                    var buffer = new Gate.Materialisation.ChecksumBufferWriter();
                    template.Generate(model, buffer);
                    return buffer.Hash;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(sink), sink, null);
            }
        }

        public static string SinkLabel(Sink sink) => sink switch
        {
            Sink.String => "string",
            Sink.TextWriter => "textwriter",
            Sink.Utf8 => "utf8",
            _ => sink.ToString(),
        };
    }
}
