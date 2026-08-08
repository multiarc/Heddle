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
    /// <para><b>The STRING sink is the cross-stack row</b> (<see cref="Cell.InCrossStack"/>), because
    /// it is like-for-like with the five .NET competitors, every one of which materialises a UTF-16
    /// string, and with the other five ecosystems, every one of which returns its own runtime's native
    /// materialised string. "Materialise your runtime's native string" is the invariant this program
    /// actually holds — Rust and Go merely happen to make that UTF-8, while the JVM, JS and Python all
    /// hand back Latin-1/UTF-16 compact strings. An earlier revision ranked .NET on the UTF-8 path
    /// arguing that UTF-16 charges Heddle a Large Object Heap cliff no other ecosystem pays; that
    /// reasoning holds only ABOVE the 85,000-byte threshold, and it was applied to all eight workloads
    /// including five whose output tops out at 31,098 bytes. It also exempted the anchor alone from a
    /// cost its five .NET rivals all pay, and left it the only cell in the sweep that never
    /// materialised its output — the exact failure ledger E4 added MATERIALISATION-CHECK to prevent.</para>
    ///
    /// <para>The LOH observation survives as a reason the <i>utf8 technique row</i> is interesting on
    /// composed-page (94,910 bytes as UTF-16 against 47,459 as UTF-8, re-measured at the E20
    /// full-page redesign), not as the anchor's rationale.
    /// All three sinks are gated; utf8 and textwriter ride along as non-ranked technique rows in the
    /// same sweep and are compared exhaustively by <c>bench-techniques</c>.</para>
    /// </summary>
    public static class HeddleEngine
    {
        /// <summary>The cross-stack row name — unqualified, because this is the engine's one ranked
        /// row. The sink-qualified labels below (<c>Heddle (utf8 sink)</c>, <c>Heddle (textwriter
        /// sink)</c>) name the non-ranked technique cells. An earlier revision left this as
        /// "Heddle (utf8)" after the anchor moved to the string sink, which both mislabelled the
        /// anchor and collided with the real utf8 cell's computed label.</summary>
        public const string Name = "Heddle";

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

        /// <summary>Per-workload compile settings: output profile, expression tier, model type.</summary>
        private static readonly (string Workload, OutputProfile Profile, ExpressionMode Mode, Type ModelType)[] Specs =
        {
            // composed-page runs the native expression tier like every other workload: the
            // extension arguments are native string literals ("styles", area names), and the
            // layout definition renders the structured nav through @list(Nav.Menus) /
            // @list(Nav.FooterColumns) — no embedded C# anywhere in the templates.
            ("composed-page", OutputProfile.Text, ExpressionMode.Native, typeof(ComposedModel)),
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
            "composed-page" => ComposedContent.Model(),
            "trivial-substitution" => SubstitutionContent.Model(),
            "large-loop" => LoopContent.Model(),
            "mixed-page" => MixedContent.Model(),
            "conditional-heavy" => ConditionalContent.Model(),
            "fragment-heavy" => FragmentContent.Model(),
            "fortunes-encoded" => FortunesContent.Model(),
            "encoded-loop" => EncodedLoopContent.Model(),
            _ => throw new ArgumentException($"unknown workload '{workload}'", nameof(workload)),
        };

        /// <summary>The runtime-compiled template for one workload, compiled once.</summary>
        public static HeddleTemplate Template(string track, string workload)
        {
            var key = track + "/" + workload;
            if (Compiled.TryGetValue(key, out var cached)) return cached;

            if (!_extensionsConfigured)
            {
                // Registers this assembly's [ExportExtensions] set (area_component and friends).
                // HeddleTemplate.Configure is the public entry point, and the public one is all
                // this harness will use: a benchmark that needs private access to the thing it
                // measures is measuring something no caller can reach. The engine grants this
                // assembly no InternalsVisibleTo, deliberately.
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
        /// Gate cells: every workload through every runtime sink. The STRING sink carries
        /// <see cref="Cell.InCrossStack"/>; the other two are gated but stay out of the sweep's
        /// competitor row.
        ///
        /// <para><b>Why string and not utf8.</b> The cross-stack cell is the one compared directly
        /// against Fluid, Scriban, DotLiquid, Handlebars.Net and Razor, and every one of those
        /// materialises a UTF-16 string. It is also the row every other ecosystem is ranked against,
        /// and each of those returns its own runtime's native materialised string — Rust and Go
        /// happen to make that UTF-8, while the JVM, JS and Python all hand back Latin-1/UTF-16
        /// compact strings. "Materialise your runtime's native string" is the invariant the program
        /// actually holds; "emit UTF-8" was never it. Rendering the anchor to a byte sink also made
        /// it the one cell in the sweep that never materialised its output at all, which is the
        /// exact failure ledger E4 added MATERIALISATION-CHECK to prevent after V8 returned a lazy
        /// rope.</para>
        ///
        /// <para>Gating all three still matters more than benchmarking all three: the gate runs
        /// inside <c>run-all</c>, so a sink that stops producing correct bytes fails the sweep.</para>
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
                        Engine = sink == Sink.String ? Name : $"Heddle ({SinkLabel(sink)} sink)",
                        Track = track,
                        Workload = workload,
                        InCrossStack = sink == Sink.String,
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
        /// The BENCH path: render into a CALLER-OWNED sink, doing nothing else.
        ///
        /// <para>The model is a parameter rather than resolved here, and the sink belongs to the
        /// caller rather than being allocated per call. Both matter for fairness. The previous shape
        /// called <see cref="ModelFor"/> on every invocation — allocating a fresh model inside the
        /// timed region that the precompiled suite, which hoists its model into
        /// <c>[GlobalSetup]</c>, never paid — and allocated a fresh 64 KB checksum buffer per render.
        /// Neither cost belongs to the engine.</para>
        ///
        /// <para>Nothing is returned: the caller reads the count off its own sink and hands that to
        /// BenchmarkDotNet, which is what keeps the work from being elided. The engine's writes
        /// cannot be optimised away regardless — they are stores through spans into escaping heap
        /// arrays across non-inlined virtual calls. Content is proven once per process by the gate
        /// and by each suite's setup assertion; see <c>Bench/BenchSinks.cs</c>.</para>
        /// </summary>
        public static void RenderToBuffer(string track, string workload, IBufferWriter<byte> buffer,
            object model, Backend backend = Backend.Runtime)
        {
            if (backend == Backend.Precompiled) { Precompiled.RenderToBuffer(workload, buffer, model); return; }
            Template(track, workload).Generate(model, buffer);
        }

        /// <inheritdoc cref="RenderToBuffer"/>
        public static void RenderToWriter(string track, string workload, TextWriter writer,
            object model, Backend backend = Backend.Runtime)
        {
            if (backend == Backend.Precompiled) { Precompiled.RenderToWriter(workload, writer, model); return; }
            Template(track, workload).Generate(model, writer);
        }

        /// <summary>The string technique: producing the string IS the work, so it is returned and the
        /// caller consumes its length.</summary>
        public static string RenderToString(string track, string workload, object model,
            Backend backend = Backend.Runtime)
            => backend == Backend.Precompiled
                ? Precompiled.RenderToString(workload, model)
                : Template(track, workload).Generate(model);

        public static string SinkLabel(Sink sink) => sink switch
        {
            Sink.String => "string",
            Sink.TextWriter => "textwriter",
            Sink.Utf8 => "utf8",
            _ => sink.ToString(),
        };
    }
}
