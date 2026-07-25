using System;
using System.Collections.Generic;
using Heddle.Benchmarks.Dotnet.Models;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Scriban twin, all eight workloads (ported from the retired *ScribanTest.cs).
    ///
    /// Scriban does not HTML-encode output, matching Heddle's <c>OutputProfile.Text</c>; the encoded
    /// workloads escape in-template with the <c>| html.escape</c> filter. Composed-page pulls its
    /// layout through an <see cref="ITemplateLoader"/>, and the area lookup is an imported function
    /// because bracket indexing with a variable key is not a documented Scriban construct.
    /// </summary>
    public static class ScribanEngine
    {
        public const string Name = "Scriban 7.2.5";

        public static IEnumerable<Cell> Cells(string track)
        {
            string Src(string file) => Templates.Load(track, "scriban", file);

            // ---- composed-page: {{ include 'layout' }}
            var composedLoader = new NamedLoader(new Dictionary<string, string>
            {
                ["layout"] = Src("layout.scriban"),
            });
            var composed = Template.Parse(Src("composed-page.scriban"));
            yield return new Cell
            {
                Engine = Name, Track = track, Workload = "composed-page", InCrossStack = true,
                Render = () =>
                {
                    var globals = new ScriptObject();
                    globals.Import("area", new Func<string, string>(name =>
                        TwinContent.Areas.TryGetValue(name, out var v) ? v : string.Empty));
                    globals["section"] = ToScriptObject(TwinContent.Sections());
                    globals["comp"] = ToScriptObject(TwinContent.Components());
                    globals["area_names"] = TwinContent.AreaOrder;
                    var ctx = new TemplateContext { TemplateLoader = composedLoader };
                    ctx.PushGlobal(globals);
                    return composed.Render(ctx);
                },
            };

            // ---- fragment-heavy: tile partial through the same loader mechanism
            var fragmentLoader = new NamedLoader(new Dictionary<string, string>
            {
                ["tile"] = Src("tile.scriban"),
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

        private static ScriptObject ToScriptObject(IEnumerable<KeyValuePair<string, string>> src)
        {
            var o = new ScriptObject();
            foreach (var kv in src) o[kv.Key] = kv.Value;
            return o;
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
