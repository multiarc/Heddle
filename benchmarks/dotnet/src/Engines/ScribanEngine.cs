using System;
using System.Collections.Generic;
using Heddle.Benchmarks.Dotnet.Models;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Scriban twin, all eight workloads.
    ///
    /// Scriban does not HTML-encode output, matching Heddle's <c>OutputProfile.Text</c>; the encoded
    /// workloads escape in-template with the <c>| html.escape</c> filter. Composed-page renders the
    /// capture-then-include layout idiom: the page captures its body into <c>body_content</c> and
    /// includes the layout, which emits the captured slot; the layout pulls the per-fragment chrome
    /// partials and the nested nav partials (mega-menu/nav-column/nav-section/nav-link) through an
    /// <see cref="ITemplateLoader"/>. Partials see the includer's loop variables because Scriban
    /// includes share the render context (the tile.scriban precedent). All literal chrome is
    /// template text (no text fixture is served from C#; the model passes only <c>nav</c>).
    /// </summary>
    public static class ScribanEngine
    {
        public const string Name = "Scriban 7.2.5";

        public static IEnumerable<Cell> Cells(string track)
        {
            string Src(string file) => Templates.Load(track, "scriban", file);

            // ---- composed-page: capture-then-include; the layout includes the chrome-fragment
            // partials (the chrome-fragment library, one partial per named definition in the Heddle
            // twin's shared/chrome-fragments.heddle) and the nested nav partials.
            var composedLoader = new NamedLoader(new Dictionary<string, string>
            {
                ["layout"] = Src("shared/layout.scriban"),
                ["alert-top"] = Src("shared/alert-top.scriban"),
                ["secondary-wholesale-menu"] = Src("shared/secondary-wholesale-menu.scriban"),
                ["secondary-retail-menu"] = Src("shared/secondary-retail-menu.scriban"),
                ["alert-below"] = Src("shared/alert-below.scriban"),
                ["assets-styles"] = Src("shared/assets-styles.scriban"),
                ["assets-scripts"] = Src("shared/assets-scripts.scriban"),
                ["custom-styles"] = Src("shared/custom-styles.scriban"),
                ["head-scripts"] = Src("shared/head-scripts.scriban"),
                ["body-scripts"] = Src("shared/body-scripts.scriban"),
                ["body-end-scripts"] = Src("shared/body-end-scripts.scriban"),
                ["mega-menu"] = Src("shared/mega-menu.scriban"),
                ["nav-column"] = Src("shared/nav-column.scriban"),
                ["nav-section"] = Src("shared/nav-section.scriban"),
                ["nav-link"] = Src("shared/nav-link.scriban"),
            });
            var composed = Template.Parse(Src("composed-page.scriban"));
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () =>
                {
                    // The model carries structured nav DATA only — all literal chrome and
                    // fragment text lives in the templates.
                    var globals = new ScriptObject();
                    globals["nav"] = ComposedContent.LiquidModel()["nav"];
                    var ctx = new TemplateContext { TemplateLoader = composedLoader };
                    ctx.PushGlobal(globals);
                    return composed.Render(ctx);
                },
            };

            // ---- fragment-heavy: six partials; the card partial nests badge + price
            // against the row's promo — the one nesting level.
            var fragmentLoader = new NamedLoader(new Dictionary<string, string>
            {
                ["tile"] = Src("shared/tile.scriban"),
                ["card"] = Src("shared/card.scriban"),
                ["badge"] = Src("shared/badge.scriban"),
                ["price"] = Src("shared/price.scriban"),
                ["media-row"] = Src("shared/media-row.scriban"),
                ["stat"] = Src("shared/stat.scriban"),
            });
            yield return Flat(track, "fragment-heavy", Src("fragment-heavy.scriban"),
                FragmentContent.LiquidModel(), fragmentLoader);

            yield return Flat(track, "trivial-substitution", Src("trivial-substitution.scriban"), SubstitutionContent.LiquidModel());
            yield return Flat(track, "large-loop", Src("large-loop.scriban"), LoopContent.LiquidModel(),
                loopLimit: LoopContent.RowCount + 1);
            yield return Flat(track, "mixed-page", Src("mixed-page.scriban"), MixedContent.LiquidModel());
            yield return Flat(track, "conditional-heavy", Src("conditional-heavy.scriban"), ConditionalContent.LiquidModel());
            yield return Flat(track, "fortunes-encoded", Src("fortunes-encoded.scriban"), FortunesContent.LiquidModel());
            yield return Flat(track, "encoded-loop", Src("encoded-loop.scriban"), EncodedLoopContent.LiquidModel(),
                loopLimit: EncodedLoopContent.RowCount + 1);
        }

        private static Cell Flat(string track, string workload, string source, Dictionary<string, object> model,
                                 ITemplateLoader loader = null, int loopLimit = 0)
        {
            var template = Template.Parse(source);
            return new Cell
            {
                Engine = Name, Track = track, Workload = workload, InCrossStack = true,
                Render = () =>
                {
                    var globals = new ScriptObject();
                    foreach (var kv in model) globals[kv.Key] = kv.Value;
                    // Scriban caps loops at 1000 iterations by default; large-loop and encoded-loop
                    // iterate 5,000 rows. Raising the cap to exactly the row count keeps the guard
                    // meaningful -- a template that looped further would still trip it.
                    var ctx = new TemplateContext();
                    if (loader != null) ctx.TemplateLoader = loader;
                    if (loopLimit > 0) ctx.LoopLimit = loopLimit;
                    ctx.PushGlobal(globals);
                    return template.Render(ctx);
                },
            };
        }

        /// <summary>Serves the registered partials by name; anything else is a harness defect.</summary>
        private sealed class NamedLoader : ITemplateLoader
        {
            private readonly Dictionary<string, string> _partials;
            public NamedLoader(Dictionary<string, string> partials) => _partials = partials;

            public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName) => templateName;

            public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
            {
                if (_partials.TryGetValue(templatePath, out var source)) return source;
                throw new System.IO.FileNotFoundException(
                    $"Scriban requested unregistered partial '{templatePath}' (registered: {string.Join(", ", _partials.Keys)})");
            }

            public System.Threading.Tasks.ValueTask<string> LoadAsync(
                TemplateContext context, SourceSpan callerSpan, string templatePath)
                => new System.Threading.Tasks.ValueTask<string>(Load(context, callerSpan, templatePath));
        }
    }
}
