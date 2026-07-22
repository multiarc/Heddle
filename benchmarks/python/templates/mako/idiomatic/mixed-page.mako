<%doc>
Idiomatic Mako mixed-page: inherits the base skeleton, fills its title block,
and supplies the page content as the template body; control flow as % lines
(Phase 5 D5).
Docs: https://docs.makotemplates.org/en/latest/inheritance.html#using-blocks
      https://docs.makotemplates.org/en/latest/defs.html
</%doc>
<%inherit file="base.mako"/>
<%block name="title">${page_title}</%block>
% if show_banner:
<div class="banner">${banner_text}</div>
% endif
<section class="hero">
<h2>${hero_heading}</h2>
<p>${hero_tagline}</p>
</section>
<section class="grid">
% for p in products:
<article class="card">
<h3>${p["name"]}</h3>
<p class="sku">${p["sku"]}</p>
<p class="price">${p["price"]}</p>
% if p["on_sale"]:
<p class="sale">On sale</p>
% endif
<p class="blurb">${p["blurb"]}</p>
</article>
% endfor
</section>
% if show_debug_panel:
<pre class="debug">debug</pre>
% endif
