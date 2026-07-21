// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the conditional-heavy workload. One <c>{{ for }}</c> over 200
    /// <see cref="ScriptObject"/> rows materialized once, each row running the four-way
    /// <c>{{ if }}/{{ else if }}</c> tier chain (chained <c>else if</c> probe-E-verified against
    /// Scriban 7.2.5) plus two toggles on the same precomputed booleans as every other engine.
    /// Scriban does not HTML-encode output, matching the Heddle oracle's
    /// <c>OutputProfile.Text</c>. Output is asserted equal to Heddle under the parity normalizer.
    /// </summary>
    public sealed class ConditionalScribanTest
    {
        internal const string PageTemplate =
            "<ul class=\"matrix\">{{ for r in rows }}<li>" +
            "{{ if r.is_bronze }}<span class=\"t0\">bronze</span>" +
            "{{ else if r.is_silver }}<span class=\"t1\">silver</span>" +
            "{{ else if r.is_gold }}<span class=\"t2\">gold</span>" +
            "{{ else }}<span class=\"t3\">platinum</span>{{ end }}" +
            "<em>{{ r.name }}</em>" +
            "{{ if r.has_note }}<small>{{ r.note }}</small>{{ end }}" +
            "{{ if r.is_active }}<b>active</b>{{ end }}" +
            "</li>{{ end }}</ul>";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _page;
        private long _length;

        public ConditionalScribanTest()
        {
            _page = Template.Parse(PageTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var rows = new ScriptArray();
            foreach (var row in ConditionalContent.Model().Rows)
            {
                var o = new ScriptObject();
                o["name"] = row.Name;
                o["note"] = row.Note;
                o["is_bronze"] = row.IsBronze;
                o["is_silver"] = row.IsSilver;
                o["is_gold"] = row.IsGold;
                o["has_note"] = row.HasNote;
                o["is_active"] = row.IsActive;
                rows.Add(o);
            }

            var globals = new ScriptObject();
            globals["rows"] = rows;
            return globals;
        }

        private static TemplateContext CreateContext()
        {
            var context = new TemplateContext();
            context.PushGlobal(Globals);
            return context;
        }

        public string Render() => _page.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
#endif
