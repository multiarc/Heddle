// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System;
using System.Threading.Tasks;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the composed home page. The layout is pulled in with
    /// <c>{{ include 'layout' }}</c> via a template loader, reusable sections are members, component
    /// calls are an imported <c>area</c> function plus <c>comp.*</c> members, and the ordered area
    /// menus are rendered with a real <c>{{ for }}</c> loop. Output is asserted byte-identical to
    /// Heddle. See Runners/README.md.
    /// </summary>
    public sealed class ScribanTest
    {
        private readonly Template _home;
        private long _length;

        public ScribanTest()
        {
            _home = Template.Parse(HomeTemplate);
        }

        internal const string LayoutTemplate =
            "{{ section.meta }}{{ section.social }}" +
            "{{ comp.assets_styles }}{{ comp.custom_styles }}{{ comp.head_scripts }}{{ comp.body_scripts }}" +
            "{{ for name in area_names }}{{ area name }}{{ end }}" +
            "{{ comp.assets_scripts }}{{ section.page_scripts }}{{ section.endpage_scripts }}{{ comp.body_end_scripts }}";

        internal const string HomeTemplate = "{{ include 'layout' }}";

        private static ScriptObject ToScriptObject(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> src)
        {
            var o = new ScriptObject();
            foreach (var kv in src) o[kv.Key] = kv.Value;
            return o;
        }

        private static TemplateContext CreateContext()
        {
            var globals = new ScriptObject();
            globals.Import("area", new Func<string, string>(name =>
                TwinContent.Areas.TryGetValue(name, out var v) ? v : string.Empty));
            globals["section"] = ToScriptObject(TwinContent.Sections());
            globals["comp"] = ToScriptObject(TwinContent.Components());
            globals["area_names"] = TwinContent.AreaOrder;

            var context = new TemplateContext { TemplateLoader = LayoutLoader.Instance };
            context.PushGlobal(globals);
            return context;
        }

        // Scriban does not HTML-encode output, matching Heddle.
        public string Render() => _home.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        /// <summary>Cold parse of both templates (D1-R4 parse cost).</summary>
        public static void ColdParse()
        {
            Template.Parse(LayoutTemplate);
            Template.Parse(HomeTemplate);
        }

        private sealed class LayoutLoader : ITemplateLoader
        {
            public static readonly LayoutLoader Instance = new LayoutLoader();

            public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName) => templateName;

            public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath) => LayoutTemplate;

            public System.Threading.Tasks.ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
                => new System.Threading.Tasks.ValueTask<string>(LayoutTemplate);
        }
    }
}
#endif
