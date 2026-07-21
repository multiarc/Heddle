namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid mixed-page twins — both consume the
    /// same dialect, so the page is authored once. The body mirrors <c>mixed-page.heddle</c>
    /// densely (no whitespace between tags, so the parity normalizer reconciles only the Heddle
    /// template's line breaks): full literal skeleton, scalar substitutions, one page-level and
    /// one row-level <c>{% if %}</c>, and a real <c>{% for %}</c> over the 36 products.
    /// </summary>
    internal static class MixedLiquidTemplates
    {
        public const string PageTemplate =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>{{ page_title }}</title>" +
            "<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>" +
            "</head><body><header><h1>{{ store_name }}</h1>" +
            "<nav><a href=\"/\">Home</a><a href=\"/catalog\">Catalog</a><a href=\"/deals\">Deals</a><a href=\"/about\">About</a><a href=\"/support\">Support</a><a href=\"/account\">Account</a></nav>" +
            "</header><main>" +
            "{% if show_banner %}<div class=\"banner\">{{ banner_text }}</div>{% endif %}" +
            "<section class=\"hero\"><h2>{{ hero_heading }}</h2><p>{{ hero_tagline }}</p></section>" +
            "<section class=\"grid\">" +
            "{% for p in products %}<article class=\"card\"><h3>{{ p.name }}</h3><p class=\"sku\">{{ p.sku }}</p><p class=\"price\">{{ p.price }}</p>{% if p.on_sale %}<p class=\"sale\">On sale</p>{% endif %}<p class=\"blurb\">{{ p.blurb }}</p></article>{% endfor %}" +
            "</section>" +
            "{% if show_debug_panel %}<pre class=\"debug\">debug</pre>{% endif %}" +
            "</main><footer><p>{{ footer_note }}</p>" +
            "<p>{{ store_name }} {{ year }} {{ support_email }}</p>" +
            "</footer></body></html>";
    }
}
