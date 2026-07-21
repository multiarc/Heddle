// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the fortunes-encoded workload. One <c>{{ for }}</c> over 12
    /// <see cref="ScriptObject"/> rows materialized once from the neutral rows, every message
    /// through <c>| html.escape</c> — Scriban's HTML escaper, which on the pinned untrusted-data
    /// alphabet matches Heddle's <c>OutputProfile.Html</c> encoder byte-for-byte. Output is
    /// asserted against the Heddle oracle.
    /// </summary>
    public sealed class FortunesScribanTest
    {
        internal const string FortunesTemplate =
            "<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>" +
            "{{ for r in rows }}<tr><td>{{ r.id }}</td><td>{{ r.message | html.escape }}</td></tr>{{ end }}" +
            "</table></body></html>";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _template;
        private long _length;

        public FortunesScribanTest()
        {
            _template = Template.Parse(FortunesTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var rows = new ScriptArray();
            foreach (var row in FortunesContent.Model().Rows)
            {
                var o = new ScriptObject();
                o["id"] = row.Id;
                o["message"] = row.Message;
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

        public string Render() => _template.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
#endif
