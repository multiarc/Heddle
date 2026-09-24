using System.Collections.Generic;
using HandlebarsDotNet;
using Heddle.Benchmarks.Dotnet.Models;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Handlebars.Net twin, all eight workloads.
    ///
    /// Raw workloads use triple mustaches (<c>{{{member}}}</c>) so fragments stay unencoded,
    /// matching Heddle's <c>OutputProfile.Text</c>; the encoded workloads use double mustaches and
    /// let Handlebars' own entity encoder do the escaping. Partials are registered with
    /// <c>RegisterTemplate</c> — the engine's documented mechanism.
    ///
    /// composed-page uses the engine's native layout mechanism, the PARTIAL BLOCK (probed against
    /// Handlebars.Net 2.1.6, supported): the page is <c>{{#&gt; layout}}…body…{{/layout}}</c> and
    /// the layout partial emits the live body slot with <c>{{&gt; @partial-block}}</c> — the exact
    /// analogue of Heddle's definition-only layout with <c>@out()</c>. The layout carries the full
    /// literal chrome; the inert chrome fragments and the section defaults are per-fragment
    /// partials mirroring <c>shared/chrome-fragments.heddle</c> (no C# tier text), and the nav
    /// renders through nested partials (<c>mega_menu → nav_column → nav_section → nav_link</c>).
    ///
    /// fragment-heavy dispatches per row over the precomputed booleans with the probe-verified
    /// chained <c>{{else if}}</c> and one nesting level: <c>card</c> renders <c>badge</c> and
    /// <c>price</c> against the row's <c>promo</c> (<c>{{&gt; badge promo}}</c>).
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

            // ---- composed-page: partial-block layout. The model carries structured nav
            // DATA only — every fragment of literal chrome text is a registered partial
            // (mirroring shared/chrome-fragments.heddle), and the layout partial holds the full chrome
            // with {{> @partial-block}} at the body slot.
            var composedEnv = Handlebars.Create();
            composedEnv.RegisterTemplate("layout", Src("shared/layout.hbs"));
            // Section defaults (Heddle shared/layout.heddle's @% block).
            composedEnv.RegisterTemplate("meta", Src("shared/meta.hbs"));
            composedEnv.RegisterTemplate("socialmeta", Src("shared/socialmeta.hbs"));
            composedEnv.RegisterTemplate("page_scripts", Src("shared/page-scripts.hbs"));
            composedEnv.RegisterTemplate("endpage_scripts", Src("shared/endpage-scripts.hbs"));
            // Chrome fragments (mirroring shared/chrome-fragments.heddle).
            composedEnv.RegisterTemplate("alert_top", Src("shared/alert-top.hbs"));
            composedEnv.RegisterTemplate("alert_below", Src("shared/alert-below.hbs"));
            composedEnv.RegisterTemplate("secondary_wholesale_menu", Src("shared/secondary-wholesale-menu.hbs"));
            composedEnv.RegisterTemplate("secondary_retail_menu", Src("shared/secondary-retail-menu.hbs"));
            composedEnv.RegisterTemplate("assets_styles", Src("shared/assets-styles.hbs"));
            composedEnv.RegisterTemplate("assets_scripts", Src("shared/assets-scripts.hbs"));
            composedEnv.RegisterTemplate("custom_styles", Src("shared/custom-styles.hbs"));
            composedEnv.RegisterTemplate("head_scripts", Src("shared/head-scripts.hbs"));
            composedEnv.RegisterTemplate("body_scripts", Src("shared/body-scripts.hbs"));
            composedEnv.RegisterTemplate("body_end_scripts", Src("shared/body-end-scripts.hbs"));
            // Structured nav, rendered through nested partials.
            composedEnv.RegisterTemplate("nav_link", Src("shared/nav-link.hbs"));
            composedEnv.RegisterTemplate("nav_section", Src("shared/nav-section.hbs"));
            composedEnv.RegisterTemplate("nav_column", Src("shared/nav-column.hbs"));
            composedEnv.RegisterTemplate("mega_menu", Src("shared/mega-menu.hbs"));
            var composed = composedEnv.Compile(Src("composed-page.hbs"));
            var composedModel = ComposedContent.HandlebarsModel();
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () => composed(composedModel),
            };

            // ---- fragment-heavy: six partials, per-row {{#if}}/{{else if}} dispatch over the
            // precomputed kind booleans; card nests badge + price against the row's promo.
            var fragmentEnv = Handlebars.Create();
            fragmentEnv.RegisterTemplate("tile", Src("shared/tile.hbs"));
            fragmentEnv.RegisterTemplate("card", Src("shared/card.hbs"));
            fragmentEnv.RegisterTemplate("badge", Src("shared/badge.hbs"));
            fragmentEnv.RegisterTemplate("price", Src("shared/price.hbs"));
            fragmentEnv.RegisterTemplate("media_row", Src("shared/media-row.hbs"));
            fragmentEnv.RegisterTemplate("stat", Src("shared/stat.hbs"));
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
            // oracle. Configuring the engine is the parity contract's preferred remedy -- it removes the
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
