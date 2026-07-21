using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the conditional-heavy workload. One <c>{{#each}}</c> over the 200
    /// shared dictionary rows, each row running the four-way <c>{{#if}}/{{else if}}</c> tier
    /// chain (chained <c>{{else if}}</c> probe-E-verified against Handlebars.Net 2.1.6) plus two
    /// toggles on the same precomputed booleans as every other engine — truthy operands only, so
    /// the chain stays inside vanilla Handlebars. Triple mustaches keep the substitutions raw,
    /// matching the Heddle oracle's <c>OutputProfile.Text</c>. Output is asserted equal to Heddle
    /// under the parity normalizer.
    /// </summary>
    public sealed class ConditionalHandlebarsTest
    {
        internal const string PageTemplate =
            "<ul class=\"matrix\">{{#each rows}}<li>" +
            "{{#if is_bronze}}<span class=\"t0\">bronze</span>" +
            "{{else if is_silver}}<span class=\"t1\">silver</span>" +
            "{{else if is_gold}}<span class=\"t2\">gold</span>" +
            "{{else}}<span class=\"t3\">platinum</span>{{/if}}" +
            "<em>{{{name}}}</em>" +
            "{{#if has_note}}<small>{{{note}}}</small>{{/if}}" +
            "{{#if is_active}}<b>active</b>{{/if}}" +
            "</li>{{/each}}</ul>";

        private readonly HandlebarsTemplate<object, object> _page;
        private long _length;

        public ConditionalHandlebarsTest()
        {
            _page = Handlebars.Create().Compile(PageTemplate);
        }

        public string Render() => _page(ConditionalContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
