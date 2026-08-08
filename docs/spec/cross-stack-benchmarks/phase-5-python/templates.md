# Python template authoring — normative texts, both engines, both tracks

Supplementary document of the [Phase 5 — python spec](README.md). It pins the exact template
texts (controlled track) and constructs-plus-citations (idiomatic track) for Jinja2 3.1.6 and
Mako 1.3.12 across the eight Phase 1 workloads. Workload models and oracle characteristics are
defined once in Phase 1's [workloads.md](../phase-1-cross-stack-foundation/workloads.md) and are
not restated; the gates are defined in the
[parity contract v2](../phase-1-cross-stack-foundation/parity-contract-v2.md). Authoring-style
rules are decision [D3](README.md#d3--controlled-track-authoring-rules-pinned-per-engine);
engine escaping configuration is [D4](README.md#d4--raw-suites-run-both-engines-untouched-non-escaping-defaults-encoded-suite-pins-one-engine-level-escaping-configuration-each)/[D5](README.md#d5--idiomatic-track-constructs-and-doc-citations-pinned-per-engine-q17).

Template texts below are normative: the implementer transcribes them into the named files.
Whitespace inside fenced blocks is part of the template. In Mako blocks a line-final `\` is the
Mako continuation character (it consumes the newline — verified, probe F); it must be the last
byte on its line.

## File layout and context names

```
benchmarks/python/templates/                        (landed E20/E22 port layout, 2026-08-08)
  jinja2/controlled/   the eight <id>.jinja workload templates + layout.jinja
                       + the ten chrome-fragment includes (alert-top, alert-below,
                         secondary-wholesale-menu, secondary-retail-menu, assets-styles,
                         assets-scripts, custom-styles, head-scripts, body-scripts,
                         body-end-scripts — one literal .jinja file each;
                         alert-below.jinja is the ZERO-BYTE pinned-empty fragment)
                       + the nav include chain (mega-menu, nav-column, nav-section,
                         nav-link .jinja)
                       + the six fragment-heavy partials (tile, card, badge, price,
                         media-row, stat .jinja)
  jinja2/idiomatic/    same eight ids + layout.jinja + the chrome includes
                       + nav.jinja (the nav MACRO library) + the six fragment macro files;
                       fortunes-encoded.html  encoded-loop.html (the .html-named encoded
                       pair — select_autoescape); NO base.jinja (idiomatic mixed-page is
                       single-file, E20)
  mako/controlled/     the eight <id>.mako + layout.mako + chrome-*.mako per-fragment
                       includes + nav-mega-menu/nav-column/nav-section/nav-link.mako
                       + the six fragment partials (tile, card, badge, price, media-row,
                         stat .mako)
  mako/idiomatic/      the eight <id>.mako + layout.mako + the <%def> libraries
                       chrome.mako / nav.mako / fragments.mako; NO base.mako
```

All template files are committed LF (`.gitattributes` rule, [README WI1](README.md#wi1--harness-skeleton-environment-data-module)).
Controlled and idiomatic trees never share files. Context variable names (from
`runner/data.py`, [README D6](README.md#d6--model-data-is-regenerated-in-python-from-the-pinned-formulas-the-byte-gate-is-the-transcription-check)) —
identical for both engines and both tracks:

| Workload | Context |
|---|---|
| composed-page | `nav` and NOTHING else (ledger [E20/E22](../../records.md#cross-spec-amendments-ledger)) — the structured navigation model, loaded by `runner/data.py` **once at import** from the corpus fixture `GoldenCorpus/fixtures/composed-page/nav.json` (strict UTF-8, `json.load`) via the gate's corpus-dir resolution (`gates.CORPUS_DIR`). Schema (snake_case): `{menus: [{tabs: [{label, href, css, has_dropdown, dropdown_css, columns: [{sections: [{title, href, title_linked, links: [{label, href}]}]}]}]}], footer_columns: [<column shape>]}`. The former `section`/`comp`/`areas`/`area_names` blob dicts are DELETED (E22: no text blobs in the model tier — ALL chrome/fragment text lives in the templates); `TwinContent.cs`/`AreaData.cs`, which they transcribed, no longer exist |
| trivial-substitution | top-level scalars `title, sku, price, brand, category, availability, url, image_url, summary, rating` — values from [SubstitutionContent.cs](../../../../benchmarks/dotnet/src/Models/SubstitutionContent.cs) |
| large-loop | `items` — 5,000 dicts `{value: i}` (E21: the display name is template-composed as `row-` + the value substitution; the model carries no `name`) |
| mixed-page | `page_title, store_name, hero_heading, hero_tagline, show_banner, banner_text, show_debug_panel, footer_note, year, support_email, products` (36 product dicts `{name, sku_number: 1000+i, price, on_sale, batch: i}` — E21: `sku_number` and `batch` are ints; templates compose the display SKU `MX-`+`sku_number` and the blurb sentence around `batch`; the former `sku`/`blurb` strings are deleted) |
| conditional-heavy | `rows` — 200 dicts `{name, seq: i, is_bronze, is_silver, is_gold, has_note, is_active}` (E21: `seq` is an int replacing the former `note` string; templates compose `note `+`seq`) |
| fragment-heavy | `items` — 48 dicts `{kind, is_tile, is_card, is_media, is_stat, name: "item-"+i (i:02d), value: i*11, badge, delta: i%7-3, promo: {label, price: 9+i}}` (E20: `kind` = `{tile,card,media,stat}[i%4]`, dispatch on the precomputed booleans; E21: `delta` and `promo.price` are ints — templates compose the media caption `Caption for `+`name`, image src `/img/`+`name`+`.jpg`, and display price `price`+`.99`; no caption/image-url/price-string field exists model-side) |
| fortunes-encoded | `rows` — the 12 pinned dicts `{id, message}` |
| encoded-loop | `items` — 5,000 dicts `{tag, name, comment}` |

Per E21/E22 (the data-not-display rule, [records.md](../../records.md#cross-spec-amendments-ledger)),
the model tier carries DATA only: no derived display string and no literal page text is served
from `runner/data.py` — pre-formatting either model-side moves rendering work out of the engine
under test and is a port defect. Zero-padded identity names (`item-{i:02d}`, `unit-{i:03d}`,
`Product {i:02d}`) and the encoded-suite payloads stay model-side by design. `nav.json` is the
one composed-page data fixture (hash-recorded in the manifest's `fixtures` section and
freshness-verified by `verify-corpus`); no per-ecosystem blob fixture files exist.

Jinja2 accesses row members with dotted attribute syntax (`p.name` — Jinja2's documented
attribute-then-item lookup resolves it against dicts; verified in probes F–H). Mako uses
subscript syntax (`p["name"]`). No context or row key collides with a dict method name
(`keys`/`values`/`items` never appear as *row* keys; `items` appears only as a top-level
context name, which Jinja2 resolves by context lookup, not attribute lookup — probe H).

## Controlled track

Engine wiring (in `runner/engines.py`): raw workloads render through
`jinja2.Environment(loader=FileSystemLoader(<controlled dir>), autoescape=False,
trim_blocks=False, lstrip_blocks=False, keep_trailing_newline=False)` and
`mako.lookup.TemplateLookup(directories=[<controlled dir>])`; encoded workloads through a
second `Environment(...)` identical but `autoescape=True`, and a second
`TemplateLookup(directories=[...], default_filters=["h"])`. Nothing else differs between raw
and encoded engine objects.

### Workload 1 — `composed-page` — Amended (E20/E22 landed form)

Jinja2 `composed-page.jinja` — genuine inheritance with a **live block**:

```jinja
{% extends "layout.jinja" %}
{% block content %}…the slider markup, transcribed from home.heddle…{% endblock %}
```

Jinja2 `layout.jinja` (~128 lines): the full literal chrome, with the overridable
section-default blocks (`{% block meta %}<title>Title</title>{% endblock %}`,
`{% block socialmeta %}…{% endblock %}`), ten `{% include "<fragment>.jinja" %}` sites for
the chrome fragments (`alert-below.jinja` is the zero-byte pinned-empty fragment), the nav
include chain — `{% for menu in nav.menus %}{% include "mega-menu.jinja" %}{% endfor %}` at
the mega-menu site and `{% for column in nav.footer_columns %}{% include "nav-column.jinja" %}{% endfor %}`
in the footer, with `mega-menu → nav-column → nav-section → nav-link` each including the next
(includes share the active context, so the loop variable is visible in the partial — the
probe-H mechanism) — and `{% block content %}{% endblock %}` at the body-slot position.

Mako `composed-page.mako` — native inheritance:

```mako
<%inherit file="layout.mako"/>\
…the slider markup…
```

Mako `layout.mako` (~130 lines): the full literal chrome with `${self.body()}` at the
body-slot position (the inheriting page's content splices there), the section defaults inline
literal text (Mako blocks are not used on the controlled track), ten
`<%include file="chrome-<fragment>.mako"/>` sites for the per-fragment chrome includes, and
the nav include chain with **explicit args** (the `%for` loop variable is a generated-code
local, not a context member — probe H):
`<%include file="nav-mega-menu.mako" args="menu=menu"/>` /
`<%include file="nav-column.mako" args="column=column"/>`, with
`nav-mega-menu → nav-column → nav-section → nav-link` each passing its node down via
`args=`, and each nav partial opening with the matching `<%page args="…"/>`.

### Workload 2 — `trivial-substitution`

Both engines: the exact
[SubstitutionLiquidTemplates.cs](../../../../benchmarks/dotnet/templates/controlled/liquid/trivial-substitution.liquid)
card literal, one line, no whitespace between tags. Jinja2 `trivial-substitution.jinja` is that
Liquid text verbatim (the pure-substitution subset of Liquid is valid Jinja2):

```jinja
<article><h1>{{ title }}</h1><p class="sku">{{ sku }}</p><p class="price">{{ price }}</p><p class="brand">{{ brand }}</p><p class="cat">{{ category }}</p><p class="avail">{{ availability }}</p><a class="link" href="{{ url }}"><img src="{{ image_url }}"></a><p class="sum">{{ summary }}</p><p class="rating">{{ rating }}</p></article>
```

Mako `trivial-substitution.mako`: the same literal with each `{{ x }}` replaced by `${x}`.

### Workload 3 — `large-loop`

Jinja2 `large-loop.jinja` (the
[LoopLiquidTemplates.cs](../../../../benchmarks/dotnet/templates/controlled/liquid/large-loop.liquid)
text, itself valid Jinja2):

```jinja
{% for item in items %}<tr><td>row-{{ item.value }}</td><td>{{ item.value }}</td></tr>{% endfor %}
```

Mako `large-loop.mako`:

```mako
% for item in items:
<tr><td>row-${item["value"]}</td><td>${item["value"]}</td></tr>\
% endfor
```

(E21 landed forms — the display name is composed in the template as `row-` + the value
substitution.)

### Workload 4 — `mixed-page`

Jinja2 `mixed-page.jinja` mirrors the Heddle template of
[workloads.md](../phase-1-cross-stack-foundation/workloads.md#workload-4--mixed-page-raw) line
for line — identical literal skeleton (incl. the `<style>` line, byte-for-byte), with:

- scalars `{{ page_title }}`, `{{ store_name }}`, `{{ hero_heading }}`, `{{ hero_tagline }}`,
  `{{ banner_text }}`, `{{ footer_note }}`, `{{ year }}`, `{{ support_email }}`;
- `{% if show_banner %}<div class="banner">{{ banner_text }}</div>{% endif %}` on the banner line;
- the product loop as **one line** (E21 landed form):
  `{% for p in products %}<article class="card"><h3>{{ p.name }}</h3><p class="sku">MX-{{ p.sku_number }}</p><p class="price">{{ p.price }}</p>{% if p.on_sale %}<p class="sale">On sale</p>{% endif %}<p class="blurb">A dependable workshop staple from batch {{ p.batch }}, checked for daily use and backed by our lifetime guarantee.</p></article>{% endfor %}`;
- `{% if show_debug_panel %}<pre class="debug">debug</pre>{% endif %}` on the debug line;
- footer line `<p>{{ store_name }} {{ year }} {{ support_email }}</p>`.

Mako `mixed-page.mako`: same skeleton; scalars as `${page_title}` etc.; control flow as `%`
lines with the loop body's element text held together by `\`:

```mako
% if show_banner:
<div class="banner">${banner_text}</div>
% endif
```

```mako
% for p in products:
<article class="card"><h3>${p["name"]}</h3><p class="sku">MX-${p["sku_number"]}</p><p class="price">${p["price"]}</p>\
% if p["on_sale"]:
<p class="sale">On sale</p>\
% endif
<p class="blurb">A dependable workshop staple from batch ${p["batch"]}, checked for daily use and backed by our lifetime guarantee.</p></article>\
% endfor
```

(the two fragments replace the Heddle `@if(ShowBanner){{…}}` line and the `@list(Products){{…}}`
line respectively; the debug-panel `% if` mirrors the banner's shape). Every surviving newline
in either engine's output sits between `>` and `<` — N3 territory (probe G verified the branch
pattern byte-level).

`mixed-page` is the one controlled workload without a single fenced verbatim text per engine
(both the literal `<style>`-bearing skeleton and the two element-text substitutions above are
long enough that the full page is described rather than reproduced whole). This is not an open
design decision: every line's rendering is fixed by (a) transcribing the Heddle skeleton in
[workloads.md](../phase-1-cross-stack-foundation/workloads.md#workload-4--mixed-page-raw)
byte-for-byte outside tag/expression positions, (b) substituting each scalar with its engine's
bare token (`{{ x }}` / `${x}`), and (c) applying D3's Mako rule mechanically — a trailing `\`
wherever the next line break would otherwise land inside element text, and nowhere else. Probe G
executed this derivation for both engines and the result is byte-identical to the corpus oracle
after normalization; there is exactly one correct rendering per line under the rule, so no
implementer judgment call remains.

### Workload 5 — `conditional-heavy`

Jinja2 `conditional-heavy.jinja` (one line, the Liquid twin with Jinja2 keywords — `elif` for
`elsif`):

```jinja
<ul class="matrix">{% for r in rows %}<li>{% if r.is_bronze %}<span class="t0">bronze</span>{% elif r.is_silver %}<span class="t1">silver</span>{% elif r.is_gold %}<span class="t2">gold</span>{% else %}<span class="t3">platinum</span>{% endif %}<em>{{ r.name }}</em>{% if r.has_note %}<small>note {{ r.seq }}</small>{% endif %}{% if r.is_active %}<b>active</b>{% endif %}</li>{% endfor %}</ul>
```

Mako `conditional-heavy.mako` (probe G's exact verified text, generalized to the model):

```mako
<ul class="matrix">\
% for r in rows:
<li>\
% if r["is_bronze"]:
<span class="t0">bronze</span>\
% elif r["is_silver"]:
<span class="t1">silver</span>\
% elif r["is_gold"]:
<span class="t2">gold</span>\
% else:
<span class="t3">platinum</span>\
% endif
<em>${r["name"]}</em>\
% if r["has_note"]:
<small>note ${r["seq"]}</small>\
% endif
% if r["is_active"]:
<b>active</b>\
% endif
</li>\
% endfor
</ul>
```

### Workload 6 — `fragment-heavy` — Amended (E20 landed form)

Four dispatched kinds over six per-kind files (`tile`, `card`, `badge`, `price`, `media-row`,
`stat`); the card nests badge + price against the row's promo; the boolean `{% if %}/{% elif %}`
chain is the controlled dispatch.

Jinja2 `fragment-heavy.jinja` — include shares the active context, so the loop-local `item`
is visible in the partials (probe H, byte-exact):

```jinja
<div class="panel">{% for item in items %}{% if item.is_tile %}{% include "tile.jinja" %}{% elif item.is_card %}{% include "card.jinja" %}{% elif item.is_media %}{% include "media-row.jinja" %}{% else %}{% include "stat.jinja" %}{% endif %}{% endfor %}</div>
```

The Jinja2 partials (the card's badge/price includes read `item.promo.*` through the shared
context; the media caption, image source and `.99` display price are template-composed — E21):

```jinja
tile.jinja:      <section class="tile"><h3>{{ item.name }}</h3><p class="v">{{ item.value }}</p><span class="badge">{{ item.badge }}</span></section>
badge.jinja:     <span class="promo-badge">{{ item.promo.label }}</span>
price.jinja:     <p class="price">{{ item.promo.price }}.99</p>
card.jinja:      <article class="card"><h3>{{ item.name }}</h3>{% include "badge.jinja" %}{% include "price.jinja" %}<p class="v">{{ item.value }}</p></article>
media-row.jinja: <div class="media-row"><img src="/img/{{ item.name }}.jpg" alt="{{ item.name }}" /><div class="media-body"><h4>{{ item.name }}</h4><p>Caption for {{ item.name }}</p></div></div>
stat.jinja:      <div class="stat"><span class="stat-name">{{ item.name }}</span><span class="stat-value">{{ item.value }}</span><span class="stat-delta">{{ item.delta }}</span></div>
```

Mako — the `%for` loop variable is a generated-code local, **not** a context member, so every
include passes its argument explicitly (probe H; a bare `<%include>` raises `NameError`) and
each partial opens with `<%page args="…"/>`:

`fragment-heavy.mako`:

```mako
<div class="panel">\
% for item in items:
% if item["is_tile"]:
<%include file="tile.mako" args="item=item"/>\
% elif item["is_card"]:
<%include file="card.mako" args="item=item"/>\
% elif item["is_media"]:
<%include file="media-row.mako" args="item=item"/>\
% else:
<%include file="stat.mako" args="item=item"/>\
% endif
% endfor
</div>
```

`tile.mako` (the card mirrors it, nesting
`<%include file="badge.mako" args="promo=item['promo']"/>` +
`<%include file="price.mako" args="promo=item['promo']"/>`):

```mako
<%page args="item"/><section class="tile"><h3>${item["name"]}</h3><p class="v">${item["value"]}</p><span class="badge">${item["badge"]}</span></section>\
```

### Workload 7 — `fortunes-encoded`

Jinja2 `fortunes-encoded.jinja` (rendered by the `autoescape=True` environment; no filter
syntax appears — D4):

```jinja
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>{% for r in rows %}<tr><td>{{ r.id }}</td><td>{{ r.message }}</td></tr>{% endfor %}</table></body></html>
```

Mako `fortunes-encoded.mako` (rendered by the `default_filters=["h"]` lookup; `${r["id"]}` is an
int — `markupsafe.escape(int)` is a verified no-op, probe G):

```mako
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>\
% for r in rows:
<tr><td>${r["id"]}</td><td>${r["message"]}</td></tr>\
% endfor
</table></body></html>
```

### Workload 8 — `encoded-loop`

Jinja2 `encoded-loop.jinja` (autoescape environment; the attribute-value position is escaped by
the same flat escaper — probe G validated the attribute cell against the canonical oracle
shape):

```jinja
<table>{% for item in items %}<tr><td data-tag="{{ item.tag }}">{{ item.name }}</td><td>{{ item.comment }}</td></tr>{% endfor %}</table>
```

Mako `encoded-loop.mako` (probe G's verified shape):

```mako
<table>\
% for item in items:
<tr><td data-tag="${item["tag"]}">${item["name"]}</td><td>${item["comment"]}</td></tr>\
% endfor
</table>
```

### Controlled-track invariants (checked by the gate, restated for the author)

1. No raw template contains any escape or bypass syntax; no encoded template contains
   per-expression escape filters (escaping is engine-level only — D4).
2. No Jinja2 template contains `{%-`, `-%}`, `{%+`, or `+%}`; no Mako template contains a
   `<% %>` code block.
3. The **non-whitespace** text inside an element (between a `>` and the next `<`) is
   byte-identical to the Heddle template's text at the same position; whitespace-run differences
   are reconciled by N3/N3b (since the 2026-07-20 N3b ruling, any whitespace-only divergence
   passes), and encoded-suite spelling differences by N5 — the contract tolerates nothing
   **non-whitespace** beyond the N5 spellings. Authoring still keeps line breaks between `>` and
   `<` or consumed by `\` (Mako) as the simplest dense form.

## Idiomatic track

Engine wiring (per D5): one `jinja2.Environment(loader=FileSystemLoader(<idiomatic dir>),
autoescape=select_autoescape())` — the API docs' recommended autoescape configuration, which
enables escaping for the `.html`-named encoded templates and leaves the `.jinja`-named raw
templates unescaped; one `mako.lookup.TemplateLookup(directories=[<idiomatic dir>])` with
library-default filters — the two encoded Mako templates carry
`<%page expression_filter="h"/>`, the filtering docs' template-declared escaping pattern.
Whitespace is authored naturally (indentation, one construct per line); the idiomatic gate
normalizes (N1–N4, +N5 encoded) before checking.

Per-workload constructs and the official doc pages each implementation follows (Q1.7). Each
cited URL is **also** transcribed into the template file's header comment
(Jinja2 `{# … #}`; Mako **leading `##` comment lines** — amended at the port landing: the
landed files use `##` line comments, not `<%doc>` blocks), per contract v2's idiomatic
authoring standard:

| Workload | Jinja2 idiomatic construct | Jinja2 doc cited | Mako idiomatic construct | Mako doc cited |
|---|---|---|---|---|
| composed-page | `layout.jinja` carries the full literal chrome, section-default blocks, per-fragment chrome includes, the nav rendered through `mega_menu`/`nav_column` macros imported from `nav.jinja` (the macro library — the four nested nav fragments as macros taking their model node explicitly), and a live `{% block content %}`; `composed-page.jinja` extends it and fills the block with the slider | [Template inheritance](https://jinja.palletsprojects.com/en/stable/templates/#template-inheritance), [Macros](https://jinja.palletsprojects.com/en/stable/templates/#macros), [Import](https://jinja.palletsprojects.com/en/stable/templates/#import) | `layout.mako` carries the full chrome with `${self.body()}` as the live body slot, the chrome fragments as `<%def>`s imported from `chrome.mako`, the nav through the `nav.mako` `<%def>` library inside `% for` loops; `composed-page.mako` opens with `<%inherit file="layout.mako"/>` and its body is the slider | [Inheritance](https://docs.makotemplates.org/en/latest/inheritance.html), [Defs](https://docs.makotemplates.org/en/latest/defs.html), [Namespaces](https://docs.makotemplates.org/en/latest/namespaces.html) |
| trivial-substitution | plain template, multi-line card | [Variables](https://jinja.palletsprojects.com/en/stable/templates/#variables) | plain template, `${x}` expressions | [Expression substitution](https://docs.makotemplates.org/en/latest/syntax.html#expression-substitution) |
| large-loop | `{% for %}` over `items`, one row per source line | [For](https://jinja.palletsprojects.com/en/stable/templates/#for) | `% for` control lines | [Control structures](https://docs.makotemplates.org/en/latest/syntax.html#control-structures) |
| mixed-page | **single-file** (workloads.md idiomatic mixed-page rule, added E20: layout composition is composed-page's dimension — `base.jinja` is deleted); full skeleton inline, loop + `{% if %}` indented naturally | [For](https://jinja.palletsprojects.com/en/stable/templates/#for), [If](https://jinja.palletsprojects.com/en/stable/templates/#if) | **single-file** (`base.mako` deleted, same rule); `%` control lines inline | [Control structures](https://docs.makotemplates.org/en/latest/syntax.html#control-structures) |
| conditional-heavy | `{% if %}/{% elif %}/{% else %}` chain inside `{% for %}`, indented | [If](https://jinja.palletsprojects.com/en/stable/templates/#if) | `% if/% elif/% else` control lines | [Control structures](https://docs.makotemplates.org/en/latest/syntax.html#control-structures) |
| fragment-heavy | each of the four kinds is a macro in its own partial file (`{% macro tile(item) %}` …); main imports via `{% from "tile.jinja" import tile %}` etc. and dispatches with the `{% if %}/{% elif %}` chain, calling `{{ tile(item) }}`; the card macro nests the badge/price sub-macros against `item.promo` (E20) | [Macros](https://jinja.palletsprojects.com/en/stable/templates/#macros), [Import](https://jinja.palletsprojects.com/en/stable/templates/#import), [If](https://jinja.palletsprojects.com/en/stable/templates/#if) | the per-kind `<%def>`s live in `fragments.mako`; main imports via `<%namespace file="fragments.mako" import="tile, card, media_row, stat"/>` and dispatches with `% if/% elif` control lines calling `${tile(item)}` etc.; card nests badge/price defs (E20) | [Defs](https://docs.makotemplates.org/en/latest/defs.html), [Namespaces](https://docs.makotemplates.org/en/latest/namespaces.html), [Control structures](https://docs.makotemplates.org/en/latest/syntax.html#control-structures) |
| fortunes-encoded | `.html` template; escaping via the environment's `select_autoescape()` | [Autoescaping](https://jinja.palletsprojects.com/en/stable/api/#autoescaping) | `<%page expression_filter="h"/>` at the top of the template | [Filtering — expression_filter](https://docs.makotemplates.org/en/latest/filtering.html) |
| encoded-loop | `.html` template; same environment | same | same directive | same |

Both idiomatic mechanisms for fragment-heavy were executed (probe H: Jinja2 macro import; Mako
namespace-def import), and `<%inherit>` was executed (probe F). Idiomatic implementations must
still satisfy the security floor and the `forbidden`/`required` verifier entries on encoded
workloads — a missed escape fails the gate exactly as the plan's validation scenario demands.
