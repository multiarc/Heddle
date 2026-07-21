using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the fortunes-encoded workload. One <c>{{#each}}</c> over the shared
    /// 12 dictionary rows using <b>double-mustache</b> under an environment configured with
    /// <see cref="FiveEntityTextEncoder"/> (D3) — the encoder escapes exactly the five-character
    /// set to Heddle's spellings and passes everything else (including the Japanese string)
    /// through, so the escaped output matches the Heddle oracle byte-for-byte. Output is asserted
    /// against the Heddle oracle.
    /// </summary>
    public sealed class FortunesHandlebarsTest
    {
        internal const string FortunesTemplate =
            "<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>" +
            "{{#each rows}}<tr><td>{{id}}</td><td>{{message}}</td></tr>{{/each}}" +
            "</table></body></html>";

        private readonly HandlebarsTemplate<object, object> _template;
        private long _length;

        public FortunesHandlebarsTest()
        {
            var environment = Handlebars.Create(new HandlebarsConfiguration
            {
                TextEncoder = new FiveEntityTextEncoder()
            });
            _template = environment.Compile(FortunesTemplate);
        }

        public string Render() => _template(FortunesContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
