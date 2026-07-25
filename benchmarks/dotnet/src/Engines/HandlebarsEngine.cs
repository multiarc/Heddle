using System.Collections.Generic;
using HandlebarsDotNet;
using Heddle.Benchmarks.Dotnet.Models;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Handlebars.Net twin, all eight workloads (ported from the retired *HandlebarsTest.cs).
    ///
    /// Raw workloads use triple mustaches (<c>{{{member}}}</c>) so fragments stay unencoded,
    /// matching Heddle's <c>OutputProfile.Text</c>; the encoded workloads use double mustaches and
    /// let Handlebars' own entity encoder do the escaping. Partials are registered with
    /// <c>RegisterTemplate</c> — the engine's documented mechanism — and the composed-page layout is
    /// itself a partial, mirroring the direction of Heddle's <c>@&lt;&lt;{{layout.heddle}}</c>.
    ///
    /// Each cell gets its OWN <c>Handlebars.Create()</c> environment: partial registration is
    /// per-environment, and sharing one would let a partial registered for one workload resolve in
    /// another, which is the sort of cross-cell leakage that makes a gate pass for the wrong reason.
    /// </summary>
    public static class HandlebarsEngine
    {
        public const string Name = "Handlebars.Net 2.1.6";

        public static IEnumerable<Cell> Cells(string track)
        {
            string Src(string file) => Templates.Load(track, "handlebars", file);

            // ---- composed-page: layout partial + an `area` helper
            var composedEnv = Handlebars.Create();
            composedEnv.RegisterHelper("area", (output, context, arguments) =>
            {
                var name = arguments[0]?.ToString() ?? string.Empty;
                output.WriteSafeString(TwinContent.Areas.TryGetValue(name, out var v) ? v : string.Empty);
            });
            composedEnv.RegisterTemplate("layout", Src("layout.hbs"));
            var composed = composedEnv.Compile(Src("composed-page.hbs"));
            var composedModel = new Dictionary<string, object>
            {
                ["section"] = TwinContent.Sections(),
                ["comp"] = TwinContent.Components(),
                ["area_names"] = TwinContent.AreaOrder,
            };
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () => composed(composedModel),
            };

            // ---- fragment-heavy: tile partial invoked 48 times with {{> tile this}}
            var fragmentEnv = Handlebars.Create();
            fragmentEnv.RegisterTemplate("tile", Src("tile.hbs"));
            var fragment = fragmentEnv.Compile(Src("fragment-heavy.hbs"));
            var fragmentModel = FragmentContent.HandlebarsModel();
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "fragment-heavy", InCrossStack = true,
                Render = () => fragment(fragmentModel),
            };

            yield return Flat(track, "trivial-substitution", Src("trivial-substitution.hbs"), SubstitutionContent.HandlebarsModel());
            yield return Flat(track, "large-loop", Src("large-loop.hbs"), LoopContent.HandlebarsModel());
            yield return Flat(track, "mixed-page", Src("mixed-page.hbs"), MixedContent.HandlebarsModel());
            yield return Flat(track, "conditional-heavy", Src("conditional-heavy.hbs"), ConditionalContent.HandlebarsModel());
            // The two encoded workloads configure the five-entity encoder. Handlebars.Net's default
            // leaves ' unescaped and decimal-escapes non-ASCII, both genuine divergences from the
            // oracle. Configuring the engine is contract v2 D2's preferred remedy -- it removes the
            // divergence without widening the gate for everyone else.
            yield return Flat(track, "fortunes-encoded", Src("fortunes-encoded.hbs"), FortunesContent.HandlebarsModel(), fiveEntity: true);
            yield return Flat(track, "encoded-loop", Src("encoded-loop.hbs"), EncodedLoopContent.HandlebarsModel(), fiveEntity: true);
        }

        private static Cell Flat(string track, string workload, string source, Dictionary<string, object> model,
                                 bool fiveEntity = false)
        {
            var config = new HandlebarsConfiguration();
            if (fiveEntity) config.TextEncoder = new FiveEntityTextEncoder();
            var template = Handlebars.Create(config).Compile(source);
            return new Cell
            {
                Engine = Name, Track = track, Workload = workload, InCrossStack = true,
                Render = () => template(model),
            };
        }
    }
}
