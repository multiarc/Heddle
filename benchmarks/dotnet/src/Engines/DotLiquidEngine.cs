using System;
using System.Collections.Generic;
using DotLiquid;
using DotLiquid.FileSystems;
using Heddle.Benchmarks.Dotnet.Models;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// DotLiquid (Liquid) twin, all eight workloads (ported from the retired *DotLiquidTest.cs).
    ///
    /// Shares <c>templates/controlled/liquid/</c> with <see cref="FluidEngine"/>. DotLiquid does not
    /// HTML-encode output, matching Heddle's <c>OutputProfile.Text</c>; the encoded workloads escape
    /// in-template with the <c>| escape</c> filter.
    ///
    /// DotLiquid resolves <c>{% include %}</c> through a PROCESS-WIDE static
    /// <c>Template.FileSystem</c>. Two workloads need includes with different targets, so the
    /// provider dispatches on the requested name instead of returning a single template the way the
    /// Fluid provider can. A static like this is exactly the kind of shared state that makes cells
    /// order-dependent, so it is installed once and dispatches by key rather than being reassigned
    /// per cell.
    /// </summary>
    public static class DotLiquidEngine
    {
        public const string Name = "DotLiquid 2.3.197";

        private static readonly object Gate = new object();
        private static bool _fileSystemInstalled;

        public static IEnumerable<Cell> Cells(string track)
        {
            string Src(string file) => Templates.Load(track, "liquid", file);

            // Installed per track: the partial sources differ between them, and DotLiquid resolves
            // includes through a process-wide static.
            InstallFileSystem(new Dictionary<string, string>
            {
                [track + "/layout"] = Src("layout.liquid"),
                [track + "/tile"] = Src("tile.dotliquid.liquid"),
            });

            // ---- composed-page: {% include 'layout' %}
            var composed = Template.Parse(Src("composed-page.liquid"));
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () => composed.Render(ComposedModel(track)),
            };

            // trivial-substitution is the one workload without a prebuilt Hash view: its values are flat
            // scalars, so the shared lowercase-keyed dictionary converts directly.
            yield return Flat(track, "trivial-substitution", Src("trivial-substitution.liquid"),
                ToHash(SubstitutionContent.LiquidModel()));
            yield return Flat(track, "large-loop", Src("large-loop.liquid"), LoopContent.DotLiquidModel());
            yield return Flat(track, "mixed-page", Src("mixed-page.liquid"), MixedContent.DotLiquidModel());
            yield return Flat(track, "conditional-heavy", Src("conditional-heavy.liquid"), ConditionalContent.DotLiquidModel());
            yield return Flat(track, "fragment-heavy", Src("fragment-heavy.dotliquid.liquid"), FragmentContent.DotLiquidModel());
            yield return Flat(track, "fortunes-encoded", Src("fortunes-encoded.liquid"), FortunesContent.DotLiquidModel());
            yield return Flat(track, "encoded-loop", Src("encoded-loop.liquid"), EncodedLoopContent.DotLiquidModel());
        }

        /// <summary>The model key carrying the current track for include resolution.</summary>
        private const string TrackKey = "__track";

        private static Cell Flat(string track, string workload, string source, Hash model)
        {
            var template = Template.Parse(source);
            var scoped = new Hash();
            foreach (var kv in model) scoped[kv.Key] = kv.Value;
            scoped[TrackKey] = track;
            return new Cell
            {
                Engine = Name, Track = track, Workload = workload, InCrossStack = true,
                Render = () => template.Render(scoped),
            };
        }

        private static Hash ToHash(Dictionary<string, object> src)
        {
            var hash = new Hash();
            foreach (var kv in src) hash[kv.Key] = kv.Value;
            return hash;
        }

        private static Hash ComposedModel(string track)
        {
            var section = new Hash();
            foreach (var kv in TwinContent.Sections()) section[kv.Key] = kv.Value;
            var comp = new Hash();
            foreach (var kv in TwinContent.Components()) comp[kv.Key] = kv.Value;
            var areas = new Hash();
            foreach (var kv in TwinContent.Areas) areas[kv.Key] = kv.Value;

            var root = new Hash();
            root["section"] = section;
            root["comp"] = comp;
            root["areas"] = areas;
            root["area_names"] = new List<string>(TwinContent.AreaOrder);
            root[TrackKey] = track;
            return root;
        }

        private static readonly Dictionary<string, string> AllPartials =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// DotLiquid resolves includes through a PROCESS-WIDE static, so the two tracks cannot each
        /// own one. Both tracks' partials are merged into a single registry keyed by track, and the
        /// templates reference them by the track-qualified name -- otherwise registering the
        /// idiomatic track would silently redirect the controlled track's includes.
        /// </summary>
        private static void InstallFileSystem(Dictionary<string, string> partials)
        {
            lock (Gate)
            {
                foreach (var kv in partials) AllPartials[kv.Key] = kv.Value;
                if (_fileSystemInstalled) return;
                Template.FileSystem = new NamedFileSystem(AllPartials);
                _fileSystemInstalled = true;
            }
        }

        /// <summary>Serves the registered partials by name; anything else is a harness defect.</summary>
        private sealed class NamedFileSystem : IFileSystem
        {
            private readonly Dictionary<string, string> _partials;
            public NamedFileSystem(Dictionary<string, string> partials) => _partials = partials;

            public string ReadTemplateFile(Context context, string templateName)
            {
                // DotLiquid passes the name as a Liquid literal, quotes included.
                var name = (context[templateName] as string ?? templateName).Trim('\'', '"');

                // Which track is rendering is carried on the model, because DotLiquid resolves
                // includes at RENDER time through a process-wide static and the file system has no
                // other way to know. Without this the idiomatic track's registration would silently
                // redirect the controlled track's includes, and the byte gate would fail somewhere
                // far from the cause.
                var track = context[TrackKey] as string;
                if (track != null && _partials.TryGetValue(track + "/" + name, out var scoped)) return scoped;
                if (_partials.TryGetValue(name, out var source)) return source;

                throw new System.IO.FileNotFoundException(
                    $"DotLiquid requested unregistered partial '{name}' on track '{track ?? "(none)"}' " +
                    $"(registered: {string.Join(", ", _partials.Keys)})");
            }
        }
    }
}
