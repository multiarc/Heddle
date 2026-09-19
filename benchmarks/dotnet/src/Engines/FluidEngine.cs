using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Fluid;
using Heddle.Benchmarks.Dotnet.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Fluid (Liquid) twin, all eight workloads.
    ///
    /// Template sources are dialect-suffixed (<c>*.fluid.liquid</c>) wherever this twin's authoring
    /// diverges from DotLiquid's ownership: the composed-page layout family, the fragment-heavy
    /// partial family, and the three workloads whose display strings are composed
    /// template-side. The remaining flat workloads
    /// (trivial-substitution and the two encoded ones) still share one un-suffixed Liquid source
    /// with <see cref="DotLiquidEngine"/>.
    ///
    /// composed-page follows the Liquid layout idiom pinned for this workload: the page captures its
    /// body into <c>body_content</c> and <c>{% include %}</c>s the layout, which emits the slot; ALL
    /// literal chrome is template text, the ten inert fragments living as per-fragment partials
    /// mirroring <c>shared/chrome-fragments.heddle</c>, and the nav rendering through nested loops +
    /// nested partials (mega-menu, nav-column, nav-section, nav-link) bound with the documented
    /// <c>{% include ... with ... %}</c> form. fragment-heavy dispatches per row over the
    /// precomputed booleans (<c>{% if %}/{% elsif %}</c> chain) into six partials, the card partial
    /// nesting badge + price against the row's promo.
    ///
    /// Fluid's default convenience encoder is the raw (non-encoding) encoder, which matches the
    /// Heddle oracle's <c>OutputProfile.Text</c>. The encoded workloads escape in-template with the
    /// <c>| escape</c> filter, so escaping is the engine's own documented mechanism rather than a
    /// host setting the harness imposed.
    /// </summary>
    public static class FluidEngine
    {
        public const string Name = "Fluid.Core 2.31.0";

        private static readonly FluidParser Parser = new FluidParser();

        public static IEnumerable<Cell> Cells(string track)
        {
            string Src(string file) => Templates.Load(track, "liquid", file);

            // A partial set served to {% include %} under its logical name: 'layout' resolves to
            // layout.liquid in the provider, whose bytes come from shared/layout.fluid.liquid on
            // disk — non-entry templates live under liquid/shared/, only the eight directly-runnable
            // workload entries sit at the folder's top level.
            TemplateOptions Options(params string[] partials)
            {
                var files = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var p in partials) files[p + ".liquid"] = Src("shared/" + p + ".fluid.liquid");
                return new TemplateOptions { FileProvider = new DictionaryFileProvider(files) };
            }

            // ---- composed-page: capture-then-include layout, per-fragment chrome partials,
            //      nested nav partials
            var composedOptions = Options(
                "layout",
                "alert-top", "secondary-wholesale-menu", "secondary-retail-menu", "alert-below",
                "assets-styles", "assets-scripts", "custom-styles", "head-scripts", "body-scripts",
                "body-end-scripts",
                "mega-menu", "nav-column", "nav-section", "nav-link");
            var composed = Parser.Parse(Src("composed-page.fluid.liquid"));
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () =>
                {
                    var ctx = new TemplateContext(composedOptions);
                    // The model carries structured nav DATA only — all literal chrome and
                    // fragment text lives in the templates.
                    ctx.SetValue("nav", ComposedContent.LiquidModel()["nav"]);
                    return composed.Render(ctx);
                },
            };

            // ---- fragment-heavy: six partials, boolean dispatch chain, card nests badge + price
            var fragmentOptions = Options("tile", "card", "badge", "price", "media-row", "stat");
            yield return Flat(track, "fragment-heavy", Src("fragment-heavy.fluid.liquid"),
                FragmentContent.LiquidModel(), fragmentOptions);

            // ---- the six flat workloads: one template, a dictionary model, no partials
            yield return Flat(track, "trivial-substitution", Src("trivial-substitution.liquid"), SubstitutionContent.LiquidModel());
            yield return Flat(track, "large-loop", Src("large-loop.fluid.liquid"), LoopContent.LiquidModel());
            yield return Flat(track, "mixed-page", Src("mixed-page.fluid.liquid"), MixedContent.LiquidModel());
            yield return Flat(track, "conditional-heavy", Src("conditional-heavy.fluid.liquid"), ConditionalContent.LiquidModel());
            yield return Flat(track, "fortunes-encoded", Src("fortunes-encoded.liquid"), FortunesContent.LiquidModel());
            yield return Flat(track, "encoded-loop", Src("encoded-loop.liquid"), EncodedLoopContent.LiquidModel());
        }

        private static Cell Flat(string track, string workload, string source, Dictionary<string, object> model,
                                 TemplateOptions options = null)
        {
            var template = Parser.Parse(source);
            return new Cell
            {
                Engine = Name, Track = track, Workload = workload, InCrossStack = true,
                Render = () =>
                {
                    // A fresh context per render is Fluid's documented usage. Hoisting it would
                    // measure a shape no caller writes, and would quietly move per-render work into
                    // setup -- the one flattering mistake a twin's author is most tempted to make.
                    var ctx = options == null ? new TemplateContext() : new TemplateContext(options);
                    foreach (var kv in model) ctx.SetValue(kv.Key, kv.Value);
                    return template.Render(ctx);
                },
            };
        }

        /// <summary>An <see cref="IFileProvider"/> serving a fixed set of in-memory templates by
        /// logical name — the include-resolution seam for the partial families.</summary>
        private sealed class DictionaryFileProvider : IFileProvider
        {
            private readonly Dictionary<string, IFileInfo> _files;

            public DictionaryFileProvider(Dictionary<string, string> files)
            {
                _files = new Dictionary<string, IFileInfo>(StringComparer.Ordinal);
                foreach (var kv in files) _files[kv.Key] = new StringFileInfo(kv.Value, kv.Key);
            }

            public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

            public IFileInfo GetFileInfo(string subpath)
            {
                var name = subpath.Replace('\\', '/').TrimStart('/');
                return _files.TryGetValue(name, out var file) ? file : new NotFoundFileInfo(name);
            }

            public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
        }

        private sealed class StringFileInfo : IFileInfo
        {
            private readonly byte[] _bytes;
            public StringFileInfo(string content, string name)
            {
                _bytes = new UTF8Encoding(false).GetBytes(content);
                Name = name;
            }
            public bool Exists => true;
            public long Length => _bytes.Length;
            public string PhysicalPath => null;
            public string Name { get; }
            public DateTimeOffset LastModified => DateTimeOffset.MinValue;
            public bool IsDirectory => false;
            public Stream CreateReadStream() => new MemoryStream(_bytes, writable: false);
        }
    }
}
