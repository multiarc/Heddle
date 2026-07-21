using System.Threading.Tasks;
using HandlebarsDotNet;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Handlebars.Net twin of the mixed-page workload. One page template over the shared
    /// dictionary model, with <c>{{#if}}</c>/<c>{{#each}}</c> blocks over the same precomputed
    /// booleans and 36 products as every other engine. Triple mustaches keep every substitution
    /// raw, matching the Heddle oracle's <c>OutputProfile.Text</c>. Output is asserted equal to
    /// Heddle under the parity normalizer.
    /// </summary>
    public sealed class MixedHandlebarsTest
    {
        internal const string PageTemplate =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>{{{page_title}}}</title>" +
            "<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>" +
            "</head><body><header><h1>{{{store_name}}}</h1>" +
            "<nav><a href=\"/\">Home</a><a href=\"/catalog\">Catalog</a><a href=\"/deals\">Deals</a><a href=\"/about\">About</a><a href=\"/support\">Support</a><a href=\"/account\">Account</a></nav>" +
            "</header><main>" +
            "{{#if show_banner}}<div class=\"banner\">{{{banner_text}}}</div>{{/if}}" +
            "<section class=\"hero\"><h2>{{{hero_heading}}}</h2><p>{{{hero_tagline}}}</p></section>" +
            "<section class=\"grid\">" +
            "{{#each products}}<article class=\"card\"><h3>{{{name}}}</h3><p class=\"sku\">{{{sku}}}</p><p class=\"price\">{{{price}}}</p>{{#if on_sale}}<p class=\"sale\">On sale</p>{{/if}}<p class=\"blurb\">{{{blurb}}}</p></article>{{/each}}" +
            "</section>" +
            "{{#if show_debug_panel}}<pre class=\"debug\">debug</pre>{{/if}}" +
            "</main><footer><p>{{{footer_note}}}</p>" +
            "<p>{{{store_name}}} {{{year}}} {{{support_email}}}</p>" +
            "</footer></body></html>";

        private readonly HandlebarsTemplate<object, object> _page;
        private long _length;

        public MixedHandlebarsTest()
        {
            _page = Handlebars.Create().Compile(PageTemplate);
        }

        public string Render() => _page(MixedContent.HandlebarsModel());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
