// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the fragment-heavy workload. The <c>tile</c> partial is pulled in 48 times
    /// with a plain <c>{{ include 'tile' }}</c> via a template loader (the
    /// <see cref="ScribanTest"/> mechanism), which shares the enclosing scope so the partial
    /// reads the loop's <c>item.*</c> members (probe-E-verified construct; positional include
    /// arguments via <c>$0</c> do NOT bind against dictionary rows and must not be used). Scriban
    /// does not HTML-encode output, matching the Heddle oracle's <c>OutputProfile.Text</c>.
    /// Output is asserted equal to Heddle under the parity normalizer.
    /// </summary>
    public sealed class FragmentScribanTest
    {
        internal const string PageTemplate =
            "<div class=\"panel\">{{ for item in items }}{{ include 'tile' }}{{ end }}</div>";

        internal const string TileTemplate =
            "<section class=\"tile\"><h3>{{ item.name }}</h3><p class=\"v\">{{ item.value }}</p><span class=\"badge\">{{ item.badge }}</span></section>";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _page;
        private long _length;

        public FragmentScribanTest()
        {
            _page = Template.Parse(PageTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var items = new ScriptArray();
            foreach (var row in FragmentContent.Model().Items)
            {
                var o = new ScriptObject();
                o["name"] = row.Name;
                o["value"] = row.Value;
                o["badge"] = row.Badge;
                items.Add(o);
            }

            var globals = new ScriptObject();
            globals["items"] = items;
            return globals;
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext { TemplateLoader = TileLoader.Instance };
            context.PushGlobal(Globals);
            return context;
        }

        public string Render() => _page.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }

        private sealed class TileLoader : ITemplateLoader
        {
            public static readonly TileLoader Instance = new TileLoader();

            public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName) => templateName;

            public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath) => TileTemplate;

            public System.Threading.Tasks.ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
                => new System.Threading.Tasks.ValueTask<string>(TileTemplate);
        }
    }
}
#endif
