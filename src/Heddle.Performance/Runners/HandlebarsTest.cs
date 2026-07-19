using System.Collections.Generic;
using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the composed home page. The layout is a registered partial
    /// (<c>{{&gt; layout }}</c>), reusable sections are model values, component calls are an
    /// <c>area</c> helper plus <c>comp.*</c> lookups, and the ordered area menus are rendered with a
    /// real <c>{{#each}}</c> loop. Output is asserted byte-identical to Heddle. See Runners/README.md.
    /// </summary>
    public sealed class HandlebarsTest
    {
        private readonly HandlebarsTemplate<object, object> _template;
        private readonly object _model;
        private long _length;

        public HandlebarsTest()
        {
            var hb = CreateEnvironment();
            _template = hb.Compile(HomeTemplate);
            _model = BuildModel();
        }

        // Layout as a partial + an `area` helper; the home entry template just includes the layout,
        // mirroring the direction of Heddle's `@<<{{layout.heddle}}`.
        private static IHandlebars CreateEnvironment()
        {
            var hb = Handlebars.Create();
            hb.RegisterHelper("area", (output, context, arguments) =>
            {
                var name = arguments[0]?.ToString() ?? string.Empty;
                output.WriteSafeString(TwinContent.Areas.TryGetValue(name, out var v) ? v : string.Empty);
            });
            hb.RegisterTemplate("layout", LayoutTemplate);
            return hb;
        }

        private static object BuildModel() => new Dictionary<string, object>
        {
            ["section"] = TwinContent.Sections(),
            ["comp"] = TwinContent.Components(),
            ["area_names"] = TwinContent.AreaOrder,
        };

        // Triple mustaches keep the HTML fragments raw (double mustaches would entity-encode).
        internal const string LayoutTemplate =
            "{{{section.meta}}}{{{section.social}}}" +
            "{{{comp.assets_styles}}}{{{comp.custom_styles}}}{{{comp.head_scripts}}}{{{comp.body_scripts}}}" +
            "{{#each area_names}}{{area this}}{{/each}}" +
            "{{{comp.assets_scripts}}}{{{section.page_scripts}}}{{{section.endpage_scripts}}}{{{comp.body_end_scripts}}}";

        internal const string HomeTemplate = "{{> layout }}";

        public string Render() => _template(_model);

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        /// <summary>Cold parse/compile of both templates (D1-R4 parse cost).</summary>
        public static void ColdParse()
        {
            var hb = CreateEnvironment();
            hb.Compile(HomeTemplate);
        }
    }
}
