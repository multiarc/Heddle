// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the trivial-substitution card. Ten scalar <c>{{ member }}</c> substitutions
    /// over a <see cref="ScriptObject"/> built once from the shared model — no layout, include, or
    /// loop. Scriban does not HTML-encode output, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted byte-identical to Heddle.
    /// </summary>
    public sealed class SubstitutionScribanTest
    {
        internal const string CardTemplate =
            "<article><h1>{{ title }}</h1><p class=\"sku\">{{ sku }}</p><p class=\"price\">{{ price }}</p>" +
            "<p class=\"brand\">{{ brand }}</p><p class=\"cat\">{{ category }}</p><p class=\"avail\">{{ availability }}</p>" +
            "<a class=\"link\" href=\"{{ url }}\"><img src=\"{{ image_url }}\"></a>" +
            "<p class=\"sum\">{{ summary }}</p><p class=\"rating\">{{ rating }}</p></article>";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _card;
        private long _length;

        public SubstitutionScribanTest()
        {
            _card = Template.Parse(CardTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var globals = new ScriptObject();
            foreach (var kv in SubstitutionContent.LiquidModel())
                globals[kv.Key] = kv.Value;
            return globals;
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            context.PushGlobal(Globals);
            return context;
        }

        public string Render() => _card.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
#endif
