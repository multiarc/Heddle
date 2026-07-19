// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the large-loop workload. One <c>{{ for }}</c> over 5,000
    /// <see cref="ScriptObject"/> rows materialized once from the neutral rows (mirroring the home
    /// twin's <c>ToScriptObject</c>). Scriban does not HTML-encode output, matching the Heddle
    /// oracle's <c>OutputProfile.Text</c>. Output is asserted byte-identical to Heddle.
    /// </summary>
    public sealed class LoopScribanTest
    {
        internal const string LoopTemplate =
            "{{ for item in items }}<tr><td>{{ item.name }}</td><td>{{ item.value }}</td></tr>{{ end }}";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _loop;
        private long _length;

        public LoopScribanTest()
        {
            _loop = Template.Parse(LoopTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var rows = new ScriptArray();
            foreach (var row in LoopContent.Model().Items)
            {
                var o = new ScriptObject();
                o["name"] = row.Name;
                o["value"] = row.Value;
                rows.Add(o);
            }

            var globals = new ScriptObject();
            globals["items"] = rows;
            return globals;
        }

        private static TemplateContext CreateContext()
        {
            // Scriban guards loops with a default 1000-iteration limit; lift it so the 5,000-row
            // workload renders (a twin-configuration necessity, not a normalization change).
            var context = new TemplateContext { LoopLimit = LoopContent.RowCount + 1 };
            context.PushGlobal(Globals);
            return context;
        }

        public string Render() => _loop.Render(CreateContext());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
#endif
