using System;
using System.Collections.Generic;
using DotLiquid;
using DotLiquid.FileSystems;
using Heddle.Benchmarks.Dotnet.Models;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// DotLiquid (Liquid) twin, all eight workloads.
    ///
    /// Shares <c>templates/{track}/liquid/</c> with <see cref="FluidEngine"/>; every template that
    /// leans on DotLiquid's scope-shared <c>{% include %}</c> (or otherwise diverges from the Fluid
    /// dialect) is <c>*.dotliquid.liquid</c>-suffixed. DotLiquid does not HTML-encode output,
    /// matching Heddle's <c>OutputProfile.Text</c>; the encoded workloads escape in-template with
    /// the <c>| escape</c> filter.
    ///
    /// composed-page is the capture-then-include layout idiom: the page captures the
    /// slider body into <c>body_content</c> and includes <c>layout</c>, which emits the slot plus
    /// the literal chrome, calling the chrome-fragment partials and the nested nav partial chain
    /// (mega-menu -> nav-column -> nav-section -> nav-link) from <c>{% for %}</c> loops.
    /// fragment-heavy dispatches one of four fragment partials per row; the card partial
    /// nests badge + price against the row's promo. All partials rely on DotLiquid's include
    /// sharing the enclosing scope (loop variables visible inside the partial — the historical
    /// tile precedent).
    ///
    /// DotLiquid resolves <c>{% include %}</c> through a PROCESS-WIDE static
    /// <c>Template.FileSystem</c>. Several workloads need includes with different targets, so the
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
            // includes through a process-wide static. Every include target of the redesigned
            // workloads is registered here: the layout, its chrome-fragment partials and the nested
            // nav partial chain (composed-page), and the six fragment partials
            // (fragment-heavy). Non-entry templates live under liquid/shared/; the registry
            // key stays the bare logical name, so {% include 'name' %} in the templates is
            // untouched by the on-disk layout.
            var partials = new Dictionary<string, string>();
            foreach (var name in PartialNames)
                partials[track + "/" + name] = Src("shared/" + name + ".dotliquid.liquid");
            InstallFileSystem(partials);

            // ---- composed-page: {% capture body_content %}…{% endcapture %}{% include 'layout' %}
            var composed = Template.Parse(Src("composed-page.dotliquid.liquid"));
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () => composed.Render(ComposedModel(track)),
            };

            // trivial-substitution is the one workload without a prebuilt Hash view: its values are flat
            // scalars, so the shared lowercase-keyed dictionary converts directly.
            yield return Flat(track, "trivial-substitution", Src("trivial-substitution.liquid"),
                ToHash(SubstitutionContent.LiquidModel()));
            yield return Flat(track, "large-loop", Src("large-loop.dotliquid.liquid"), LoopContent.DotLiquidModel());
            yield return Flat(track, "mixed-page", Src("mixed-page.dotliquid.liquid"), MixedContent.DotLiquidModel());
            yield return Flat(track, "conditional-heavy", Src("conditional-heavy.dotliquid.liquid"), ConditionalContent.DotLiquidModel());
            yield return Flat(track, "fragment-heavy", Src("fragment-heavy.dotliquid.liquid"), FragmentContent.DotLiquidModel());
            yield return Flat(track, "fortunes-encoded", Src("fortunes-encoded.liquid"), FortunesContent.DotLiquidModel());
            yield return Flat(track, "encoded-loop", Src("encoded-loop.liquid"), EncodedLoopContent.DotLiquidModel());
        }

        /// <summary>
        /// Every include target, one <c>*.dotliquid.liquid</c> file each: the composed-page layout,
        /// its ten chrome-fragment partials (the inert literal chrome), the four nested
        /// nav partials, and the six fragment-heavy partials.
        /// </summary>
        private static readonly string[] PartialNames =
        {
            // composed-page layout + chrome fragments
            "layout",
            "alert-top", "secondary-wholesale-menu", "secondary-retail-menu", "alert-below",
            "assets-styles", "assets-scripts", "custom-styles",
            "head-scripts", "body-scripts", "body-end-scripts",
            // composed-page nested nav chain
            "mega-menu", "nav-column", "nav-section", "nav-link",
            // fragment-heavy fragment kinds (card nests badge + price)
            "tile", "card", "badge", "price", "media-row", "stat",
        };

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
            // The model carries structured nav DATA only — all literal chrome and fragment
            // text lives in the templates.
            var root = new Hash();
            foreach (var kv in Models.ComposedContent.DotLiquidModel()) root[kv.Key] = kv.Value;
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
