using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the encoded-loop workload. One <c>{{#each}}</c> over the shared
    /// 5,000 dictionary rows using <b>double-mustache</b> under an environment configured with
    /// <see cref="FiveEntityTextEncoder"/> (D3) — the encoder escapes exactly the five-character
    /// set to Heddle's spellings and passes everything else (including the per-row Japanese run)
    /// through, so the escaped output matches the Heddle oracle byte-for-byte. Output is asserted
    /// against the Heddle oracle.
    /// </summary>
    public sealed class EncodedLoopHandlebarsTest
    {
        internal const string LoopTemplate =
            "<table>{{#each items}}" +
            "<tr><td data-tag=\"{{tag}}\">{{name}}</td><td>{{comment}}</td></tr>" +
            "{{/each}}</table>";

        private readonly HandlebarsTemplate<object, object> _template;
        private long _length;

        public EncodedLoopHandlebarsTest()
        {
            var environment = Handlebars.Create(new HandlebarsConfiguration
            {
                TextEncoder = new FiveEntityTextEncoder()
            });
            _template = environment.Compile(LoopTemplate);
        }

        public string Render() => _template(EncodedLoopContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
