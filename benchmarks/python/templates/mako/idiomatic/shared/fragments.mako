## Idiomatic Mako fragment-heavy library (ledger E20): the six fragment kinds as
## <%def> blocks -- tile, badge, price, card (which nests badge + price against the
## row's promo, the one nesting level), media_row, and stat -- imported by
## fragment-heavy.mako via <%namespace>. Derived display text (the display price
## `price`.99, the image src /img/`name`.jpg, the caption "Caption for `name`") is
## composed here as literal-plus-substitution per ledger E21 (Phase 5 D5, Q1.7).
## Docs: https://docs.makotemplates.org/en/latest/defs.html
##       https://docs.makotemplates.org/en/latest/namespaces.html
<%def name="tile(item)">
<section class="tile">
    <h3>${item["name"]}</h3>
    <p class="v">${item["value"]}</p>
    <span class="badge">${item["badge"]}</span>
</section>
</%def>
<%def name="badge(promo)">
<span class="promo-badge">${promo["label"]}</span>
</%def>
<%def name="price(promo)">
<p class="price">${promo["price"]}.99</p>
</%def>
<%def name="card(item)">
<article class="card">
    <h3>${item["name"]}</h3>
    ${badge(item["promo"])}
    ${price(item["promo"])}
    <p class="v">${item["value"]}</p>
</article>
</%def>
<%def name="media_row(item)">
<div class="media-row">
    <img src="/img/${item["name"]}.jpg" alt="${item["name"]}" />
    <div class="media-body">
        <h4>${item["name"]}</h4>
        <p>Caption for ${item["name"]}</p>
    </div>
</div>
</%def>
<%def name="stat(item)">
<div class="stat">
    <span class="stat-name">${item["name"]}</span>
    <span class="stat-value">${item["value"]}</span>
    <span class="stat-delta">${item["delta"]}</span>
</div>
</%def>
