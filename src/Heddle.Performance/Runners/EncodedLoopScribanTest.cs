// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the encoded-loop workload. One <c>{{ for }}</c> over 5,000
    /// <see cref="ScriptObject"/> rows materialized once from the neutral rows, every untrusted
    /// member through <c>| html.escape</c> — Scriban's HTML escaper, which on the pinned
    /// untrusted-data alphabet matches Heddle's <c>@attr</c> and <c>@(…)</c> encoders
    /// byte-for-byte. Output is asserted against the Heddle oracle.
    /// </summary>
    public sealed class EncodedLoopScribanTest
    {
        internal const string LoopTemplate =
            "<table>{{ for item in items }}" +
            "<tr><td data-tag=\"{{ item.tag | html.escape }}\">{{ item.name | html.escape }}</td><td>{{ item.comment | html.escape }}</td></tr>" +
            "{{ end }}</table>";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _template;
        private long _length;

        public EncodedLoopScribanTest()
        {
            _template = Template.Parse(LoopTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var rows = new ScriptArray();
            foreach (var row in EncodedLoopContent.Model().Items)
            {
                var o = new ScriptObject();
                o["tag"] = row.Tag;
                o["name"] = row.Name;
                o["comment"] = row.Comment;
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
            var context = new TemplateContext { LoopLimit = EncodedLoopContent.RowCount + 1 };
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
