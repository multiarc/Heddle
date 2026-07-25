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
    /// Fluid (Liquid) twin, all eight workloads (ported from the retired *FluidTest.cs).
    ///
    /// Shares its Liquid sources with <see cref="DotLiquidEngine"/> — both consume the same dialect,
    /// so the templates are authored once under <c>templates/controlled/liquid/</c>. The one
    /// exception is fragment-heavy, where the two dialects genuinely diverge on partial invocation;
    /// those carry an engine suffix rather than being silently forked.
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

            // ---- composed-page: layout via {% include 'layout' %} through an in-memory provider
            var composedOptions = new TemplateOptions
            {
                FileProvider = new SingleFileProvider(Src("layout.liquid"), "layout.liquid"),
            };
            var composed = Parser.Parse(Src("composed-page.liquid"));
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () =>
                {
                    var ctx = new TemplateContext(composedOptions);
                    ctx.SetValue("section", ToObjectDict(TwinContent.Sections()));
                    ctx.SetValue("comp", ToObjectDict(TwinContent.Components()));
                    ctx.SetValue("areas", ToObjectDict(TwinContent.Areas));
                    ctx.SetValue("area_names", TwinContent.AreaOrder);
                    return composed.Render(ctx);
                },
            };

            // ---- fragment-heavy: tile partial, Fluid's {% render %} form
            var fragmentOptions = new TemplateOptions
            {
                FileProvider = new SingleFileProvider(Src("tile.fluid.liquid"), "tile.liquid"),
            };
            yield return Flat(track, "fragment-heavy", Src("fragment-heavy.fluid.liquid"),
                FragmentContent.LiquidModel(), fragmentOptions);

            // ---- the six flat workloads: one template, a dictionary model, no partials
            yield return Flat(track, "trivial-substitution", Src("trivial-substitution.liquid"), SubstitutionContent.LiquidModel());
            yield return Flat(track, "large-loop", Src("large-loop.liquid"), LoopContent.LiquidModel());
            yield return Flat(track, "mixed-page", Src("mixed-page.liquid"), MixedContent.LiquidModel());
            yield return Flat(track, "conditional-heavy", Src("conditional-heavy.liquid"), ConditionalContent.LiquidModel());
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
                    // A fresh context per render is Fluid's documented usage and is what the retired
                    // harness measured; hoisting it would measure a shape no caller writes.
                    var ctx = options == null ? new TemplateContext() : new TemplateContext(options);
                    foreach (var kv in model) ctx.SetValue(kv.Key, kv.Value);
                    return template.Render(ctx);
                },
            };
        }

        private static Dictionary<string, object> ToObjectDict(IEnumerable<KeyValuePair<string, string>> src)
        {
            var d = new Dictionary<string, object>();
            foreach (var kv in src) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>An <see cref="IFileProvider"/> serving exactly one in-memory template.</summary>
        private sealed class SingleFileProvider : IFileProvider
        {
            private readonly IFileInfo _file;
            public SingleFileProvider(string content, string name) => _file = new StringFileInfo(content, name);
            public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
            public IFileInfo GetFileInfo(string subpath) => _file; // only one include target exists
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
