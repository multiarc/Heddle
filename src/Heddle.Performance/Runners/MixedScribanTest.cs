// Scriban 7.2.5 (the only release with the 2026 GHSA fixes) no longer supports net6.0, so the
// Scriban twin exists only on net8.0+ legs; see Heddle.Performance.csproj.
#if !NET6_0
using System.Threading.Tasks;
using Scriban;
using Scriban.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Scriban twin of the mixed-page workload. One page template over
    /// <see cref="ScriptObject"/> globals materialized once from the shared model (mirroring the
    /// large-loop twin), with <c>{{ if }}</c>/<c>{{ for }}</c> blocks over the same precomputed
    /// booleans and 36 products as every other engine. Scriban does not HTML-encode output,
    /// matching the Heddle oracle's <c>OutputProfile.Text</c>. Output is asserted equal to Heddle
    /// under the parity normalizer.
    /// </summary>
    public sealed class MixedScribanTest
    {
        internal const string PageTemplate =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>{{ page_title }}</title>" +
            "<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>" +
            "</head><body><header><h1>{{ store_name }}</h1>" +
            "<nav><a href=\"/\">Home</a><a href=\"/catalog\">Catalog</a><a href=\"/deals\">Deals</a><a href=\"/about\">About</a><a href=\"/support\">Support</a><a href=\"/account\">Account</a></nav>" +
            "</header><main>" +
            "{{ if show_banner }}<div class=\"banner\">{{ banner_text }}</div>{{ end }}" +
            "<section class=\"hero\"><h2>{{ hero_heading }}</h2><p>{{ hero_tagline }}</p></section>" +
            "<section class=\"grid\">" +
            "{{ for p in products }}<article class=\"card\"><h3>{{ p.name }}</h3><p class=\"sku\">{{ p.sku }}</p><p class=\"price\">{{ p.price }}</p>{{ if p.on_sale }}<p class=\"sale\">On sale</p>{{ end }}<p class=\"blurb\">{{ p.blurb }}</p></article>{{ end }}" +
            "</section>" +
            "{{ if show_debug_panel }}<pre class=\"debug\">debug</pre>{{ end }}" +
            "</main><footer><p>{{ footer_note }}</p>" +
            "<p>{{ store_name }} {{ year }} {{ support_email }}</p>" +
            "</footer></body></html>";

        private static readonly ScriptObject Globals = BuildGlobals();

        private readonly Template _page;
        private long _length;

        public MixedScribanTest()
        {
            _page = Template.Parse(PageTemplate);
        }

        private static ScriptObject BuildGlobals()
        {
            var model = MixedContent.Model();

            var products = new ScriptArray();
            foreach (var p in model.Products)
            {
                var o = new ScriptObject();
                o["name"] = p.Name;
                o["sku"] = p.Sku;
                o["price"] = p.Price;
                o["on_sale"] = p.OnSale;
                o["blurb"] = p.Blurb;
                products.Add(o);
            }

            var globals = new ScriptObject();
            globals["page_title"] = model.PageTitle;
            globals["store_name"] = model.StoreName;
            globals["hero_heading"] = model.HeroHeading;
            globals["hero_tagline"] = model.HeroTagline;
            globals["show_banner"] = model.ShowBanner;
            globals["banner_text"] = model.BannerText;
            globals["show_debug_panel"] = model.ShowDebugPanel;
            globals["footer_note"] = model.FooterNote;
            globals["year"] = model.Year;
            globals["support_email"] = model.SupportEmail;
            globals["products"] = products;
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
