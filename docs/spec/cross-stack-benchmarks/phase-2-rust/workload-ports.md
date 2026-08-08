# Workload ports — Askama and Tera, 32 cells

Supplementary document of the [Phase 2 — rust spec](README.md). It pins the normative template
texts, Rust model construction, and per-cell authoring notes for all eight Phase 1 workloads ×
two engines (Askama, Tera) × two tracks (controlled, idiomatic) — 32 cells, zero expected
exclusions. Model **data** is normative in Phase 1's
[workloads.md](../phase-1-cross-stack-foundation/workloads.md) (exact strings and generation
formulas); this document pins the Rust construction that reproduces it byte-for-byte and the
template texts that consume it. Gate definitions are Phase 1's
[parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md); which gate
guards which cell is restated per workload below.

Conventions used throughout:

- **Controlled texts are normative the same way Phase 1's twin texts are**: whitespace erased by
  the contract's N2–N4 (line endings, inter-tag runs, edge trim) is tolerated between this spec
  and the checked-in file; nothing else is. Text inside an element (between a `>` and the next
  `<`) must be transcribed byte-exactly.
- **Idiomatic texts are normative in structure** (constructs, inheritance shape, filter usage,
  doc citations); their indentation/line layout is free because the idiomatic gate is the
  verifier, which normalizes first.
- Where one template text serves both engines it is shown once and marked **(both engines)** —
  Askama and Tera share the Jinja-family `{% for %}` / `{% if %}` / `{% elif %}` / `{{ expr }}`
  surface for every construct these workloads need ([README D7](README.md#d7--controlled-track-construct-mapping-one-jinja-family-text-where-the-engines-agree)).
  The checked-in files are still two physical copies (one under each engine's template
  directory) because Askama consumes them at compile time and Tera at runtime.
- Rust model field names are the snake_case forms of the Phase 1 model members (`page_title`,
  `on_sale`, `image_url` — matching the dictionary-view keys the .NET twins already use), so the
  template texts read identically to the Phase 1 Liquid/Handlebars twin texts.
- File paths are relative to `benchmarks/rust/` ([README D2](README.md#d2--harness-location-benchmarksrust-a-new-top-level-benchmarks-directory)).
  Askama controlled templates carry `#[template(path = "controlled/askama/<file>", escape = "none")]`
  for raw workloads and no `escape` override for encoded ones; Tera controlled raw templates are
  registered in the autoescape-off instance, encoded ones in the default instance
  ([README D3](README.md#d3--escaping-mode-per-template-per-track)).

## Model construction (`src/models.rs`) — Amended (E20, E21, E22)

One module builds every model exactly once (`std::sync::OnceLock`), mirroring the .NET
`Shared`-instance discipline. All numeric formatting is integer `Display` (no locale, matching
C# invariant `int` formatting). Per ledger [E21/E22](../../records.md#cross-spec-amendments-ledger)
the model tier carries **DATA only** — no derived display strings (the templates compose them as
literal-plus-substitution) and no literal page text (chrome/blob text is template text).
Construction formulas, per workload:

| Workload | Rust construction (normative) |
|---|---|
| `composed-page` | **(E20 + E22)** `ComposedModel { nav: NavModel }` and NOTHING else — the structured navigation, nested under the `nav` key exactly as the Phase 1 dictionary views nest it. Struct chain (all snake_case fields, matching the fixture keys byte-for-byte): `NavModel { menus: Vec<MegaMenu>, footer_columns: Vec<NavColumn> }` → `MegaMenu { tabs: Vec<MenuTab> }` → `MenuTab { label, href, css, has_dropdown: bool, dropdown_css, columns: Vec<NavColumn> }` → `NavColumn { sections: Vec<NavSection> }` → `NavSection { title, href, title_linked: bool, links: Vec<NavLink> }` → `NavLink { label, href }`. Loaded ONCE at init (`OnceLock`) by `corpus::load_nav()` deserializing the corpus fixture `GoldenCorpus/fixtures/composed-page/nav.json` (`serde(deny_unknown_fields)` so fixture drift fails loudly; the manifest's `fixtures` section carries the fixture's golden-grade hash + byteLength, asserted by a corpus unit test). Every other former model member is GONE (E22): the `data/composed-page/*.html` blob files, their `include_str!` loading, the `section_*`/`comp_*` fragment fields and the `area_names`/`areas` map are deleted — the inert chrome is literal template text, policed by the byte gate/verifier, not model data |
| `trivial-substitution` | `SubstitutionModel { title: "Heddle Handbook", sku: "HB-2001", price: 4200, brand: "Heddle Press", category: "Reference", availability: "In stock", url: "/catalog/handbook", image_url: "/img/handbook.png", summary: "A concise field guide to the engine.", rating: "4.8" }` |
| `large-loop` | **(E21)** 5,000 rows, `i` in `[0, 4999]`: `LoopRow { value: i }` — value ONLY; the display name `row-{i}` is composed by the templates as the literal `row-` + the value substitution |
| `mixed-page` | Page scalars exactly as Phase 1 pins them (`page_title: "Mercantile - Catalog"`, …, `show_banner: true`, `show_debug_panel: false`, `year: 2026`, `support_email: "support at mercantile.example"`); 36 products, `i` in `[1, 36]`: **(E21)** `MixedProduct { name: format!("Product {i:02}"), sku_number: 1000 + i, price: 950 + i * 7, on_sale: i % 3 == 0, batch: i }` — ints replace the former `sku`/`blurb` strings; the templates compose the display SKU (`MX-` + sku_number) and the blurb sentence (around the batch substitution) |
| `conditional-heavy` | 200 rows, `i` in `[0, 199]`: **(E21)** `ConditionalRow { name: format!("unit-{i:03}"), seq: i, is_bronze: i % 4 == 0, is_silver: i % 4 == 1, is_gold: i % 4 == 2, has_note: i % 2 == 0, is_active: i % 5 != 0 }` — the int `seq` replaces the `note` string; the templates compose `note ` + the seq substitution |
| `fragment-heavy` | **(E20 + E21)** 48 rows, `i` in `[0, 47]`: `FragmentRow { kind: ["tile", "card", "media", "stat"][i % 4], is_tile, is_card, is_media, is_stat (precomputed booleans — engines dispatch on these, never on the string), name: format!("item-{i:02}"), value: i * 11, badge: ["new", "hot", "sale", "std"][i % 4], delta: (i % 7) as i64 - 3, promo: FragmentPromo { label: badge, price: 9 + i } }` — `promo` on EVERY row (no null guard), `price` an int. NO caption/image-url/price-string fields: the media caption (`Caption for ` + name), image source (`/img/` + name + `.jpg`) and display price (price + `.99`) are template-composed (E21 — pre-formatting them model-side is a port defect) |
| `fortunes-encoded` | Exactly the 12 pinned `(id, message)` rows of Phase 1 [workloads.md — workload 7](../phase-1-cross-stack-foundation/workloads.md#workload-7--fortunes-encoded-encoded), transcribed as Rust string literals byte-for-byte (row 4/8 em dashes are U+2014; row 11 is the XSS payload; row 12 the Japanese string) |
| `encoded-loop` | 5,000 rows, `i` in `[0, 4999]`: `EncodedLoopRow { tag: format!("tag-{i}&'{}'", i % 7), name: format!("item <{i}> & \"co\""), comment: format!("'q' & <angle> \"d\" こんにちは {i}") }` |

All model structs derive `serde::Serialize` (Tera contexts are built from them); the nav structs
additionally derive `Deserialize` (they are the fixture's parse target). Askama template structs
borrow them (`&'static` references to the `OnceLock` singletons). A unit test per model pins the
counts and distinctive values ([README testing plan](README.md#testing-plan)); the composed-page
tests additionally pin the nav totals the verifier derives from the same model (2 menus × 6
tabs, 4 footer columns, 36 total columns, 245 total links, the unique privacy deep link) and
assert workloads.md rule-4 cleanliness over every nav text value.

### Composed-page fragment data files — Deleted (E22)

*Superseded (E20/E22, 2026-08-08).* This section previously pinned `data/composed-page/` — 15
committed blob files (`section-*.html`, `comp-*.html`, `area-1…7.html`) consumed via
`include_str!` as the composed-page model's fragment strings. Per
[E22](../../records.md#cross-spec-amendments-ledger) the per-ecosystem blob fixture files and
their freshness tests are **deleted when each port lands**: the inert chrome is literal template
text in every engine, transcribed from the Heddle templates/golden and policed by the byte gate
(controlled) and the verifier (idiomatic). `fixtures/composed-page/nav.json` is the ONLY
composed-page data fixture, and the model row above is its single Rust consumer.

---

## Workload 1 — `composed-page` (raw)

**Cells and gates.** Controlled Askama + controlled Tera: byte gate vs
`composed-page.golden.html` (N5 not applied — raw suite). Idiomatic Askama + idiomatic Tera:
`composed-page.verify.json`.

> **Rewritten to the landed forms (E20/E22 port landing, 2026-08-08).** The pre-E20
> fragment-sequence construct mapping (layout as ordered concatenation of `section_*`/`comp_*`
> scalars + an `areas[name]` loop) is superseded; the sections below describe the templates as
> committed.

**Construct mapping.** Heddle composes via a definition-only layout import with a live
`@out()` slot; both Rust engines use their native inheritance mechanism per the workloads.md
native-layout mandate: `{% extends %}` + a **live `{% block body %}`** the page fills with the
slider markup.

### Controlled — both engines (one shape, two copies)

`templates/controlled/<engine>/composed-page.html`:

```jinja
{% extends "controlled/<engine>/shared/composed-page-layout.html" %}
{% block body %}…the slider markup, transcribed from composed-page.heddle…{% endblock %}
```

`templates/controlled/<engine>/shared/composed-page-layout.html` (~128 lines): the **full literal
chrome** transcribed from `layout.heddle`/the golden, carrying

- the four **overridable section-default blocks** — `{% block meta %}<title>Title</title>{% endblock %}`,
  `{% block socialmeta %}…{% endblock %}`, and the empty `{% block page_scripts %}{% endblock %}` /
  `{% block endpage_scripts %}{% endblock %}` — the engines' overridable-default mechanism for
  the layout.heddle section defaults;
- ten `{% include "controlled/<engine>/chrome/<fragment>.html" %}` sites for the inert chrome
  fragment files (`alert-top`, `alert-below`, `secondary-wholesale-menu`,
  `secondary-retail-menu`, `assets-styles`, `assets-scripts`, `custom-styles`, `head-scripts`,
  `body-scripts`, `body-end-scripts` — the E22 chrome-fragments library, one literal file per
  fragment);
- the nav include chain: `{% for menu in nav.menus %}{% include ".../nav/mega-menu.html" %}{% endfor %}`
  at the mega-menu site and `{% for column in nav.footer_columns %}{% include ".../nav/<column file>" %}{% endfor %}`
  in the footer — the **nav-column include is shared** between the dropdown chain and the
  footer, exactly as the Heddle `nav_column` definition is;
- `{% block body %}{% endblock %}` at the body-slot position.

The nav sub-partials nest mega-menu → column → section → link
(Askama file names `nav/mega-menu.html`, `nav/nav-column.html`, `nav/nav-section.html`,
`nav/nav-link.html`; Tera `nav/mega-menu.html`, `nav/column.html`, `nav/section.html`,
`nav/link.html`). Each reads its node through the enclosing loop variable — **Tera includes
take no arguments**; they render "using the current context", so `nav/section.html` reads
`section.title` etc. from the loop variable of the including file (the Tera 2.0 component
form — `{% component %}`/`{% endcomponent %}` with explicit arguments — remains the unused
documented fallback). Askama includes likewise see the including context ("Included templates
get full access to the context in which they're used", 0.16 book).

**No `|safe` appears anywhere in either track** — rule-4 sanitization keeps every nav value
escape-free, and all chrome is literal template text (literal text is never escaped), so the
raw and would-be-escaped renderings coincide. Askama structs: `ComposedControlled<'a> { nav:
&'a NavModel }` with `escape = "none"` (controlled raw); Tera controlled templates are
registered in the autoescape-off instance with a `nav`-keyed context.

### Idiomatic — both engines

Same shape as the docs teach it: the child extends the layout/base
(`idiomatic/askama/shared/composed-page-layout.html` / `idiomatic/tera/shared/composed-page-base.html`) and
fills `{% block body %}`; the layout carries the same section-default blocks, chrome includes
and nav include chain, authored multi-line. Doc citations (header comment in each file, per
Q1.7/D16): Askama book *Template syntax — Template inheritance*, *Include*, *For*, *If*; Tera
docs *Inheritance*, *Include*, *Control structures*. The verifier's ordered markers walk the
page landmarks (doctype → header chrome → mega menus → slider → footer columns → `</html>`);
the inheritance form preserves that order by construction, and all tag-line whitespace is
erased by N1–N4/N3b before checking.

---

## Workload 2 — `trivial-substitution` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `trivial-substitution.golden.html`.
Idiomatic × 2: `trivial-substitution.verify.json`.

### Controlled — one text, both engines

`templates/controlled/askama/trivial-substitution.html` and
`templates/controlled/tera/trivial-substitution.html` (one line, mirroring
[`trivial-substitution.heddle`](../../../../benchmarks/dotnet/templates/controlled/heddle/trivial-substitution.heddle)):

```jinja
<article><h1>{{ title }}</h1><p class="sku">{{ sku }}</p><p class="price">{{ price }}</p><p class="brand">{{ brand }}</p><p class="cat">{{ category }}</p><p class="avail">{{ availability }}</p><a class="link" href="{{ url }}"><img src="{{ image_url }}"></a><p class="sum">{{ summary }}</p><p class="rating">{{ rating }}</p></article>
```

Askama struct fields: the ten model fields (`price: i32`, rest `&str`); `escape = "none"`. Tera:
controlled-raw instance, context from `SubstitutionModel`.

### Idiomatic — both engines

Same substitutions authored multi-line/indented as the engines' getting-started pages teach
(one struct/context per template, default escaping left on — the pinned model values contain no
`& < > " '`, so escaping is byte-neutral; [README D3](README.md#d3--escaping-mode-per-template-per-track)).
Doc citations: Askama book *Getting started* / *Creating templates*; Tera docs *Getting started* /
*Variables*.

---

## Workload 3 — `large-loop` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `large-loop.golden.html`. Idiomatic × 2:
`large-loop.verify.json`.

### Controlled — one text, both engines (E21 landed form)

```jinja
{% for item in items %}<tr><td>row-{{ item.value }}</td><td>{{ item.value }}</td></tr>{% endfor %}
```

(Mirrors [`large-loop.heddle`](../../../../benchmarks/dotnet/templates/controlled/heddle/large-loop.heddle);
the display name is composed in the template as the literal `row-` + the value substitution —
E21. Askama: `items: &[LoopRow]`, `escape = "none"`. Tera: controlled-raw instance.)

### Idiomatic — both engines

The same loop, multi-line, with the row markup kept tight (`<td>row-{{ item.value }}</td>` —
text inside an element must stay adjacent to its tags; the verifier needle
`<tr><td>row-0</td><td>0</td></tr>` runs against normalized output, so line breaks between rows
are fine). Default escaping on. Doc citations: Askama book *Template syntax — For*; Tera docs
*Control structures — For loops*.

---

## Workload 4 — `mixed-page` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `mixed-page.golden.html`. Idiomatic × 2:
`mixed-page.verify.json`.

### Controlled — one text, both engines

Line-for-line mirror of the Heddle template
([workloads.md — workload 4](../phase-1-cross-stack-foundation/workloads.md#workload-4--mixed-page-raw));
line breaks appear only between tags (N3 territory), every substitution stays tight against its
surrounding markup. The `<style>` literal is transcribed byte-exactly from the Heddle template.

```jinja
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>{{ page_title }}</title>
<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
</head>
<body>
<header>
<h1>{{ store_name }}</h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
{% if show_banner %}<div class="banner">{{ banner_text }}</div>{% endif %}
<section class="hero">
<h2>{{ hero_heading }}</h2>
<p>{{ hero_tagline }}</p>
</section>
<section class="grid">
{% for p in products %}<article class="card"><h3>{{ p.name }}</h3><p class="sku">MX-{{ p.sku_number }}</p><p class="price">{{ p.price }}</p>{% if p.on_sale %}<p class="sale">On sale</p>{% endif %}<p class="blurb">A dependable workshop staple from batch {{ p.batch }}, checked for daily use and backed by our lifetime guarantee.</p></article>{% endfor %}
</section>
{% if show_debug_panel %}<pre class="debug">debug</pre>{% endif %}
</main>
<footer>
<p>{{ footer_note }}</p>
<p>{{ store_name }} {{ year }} {{ support_email }}</p>
</footer>
</body>
</html>
```

**Authoring note (whitespace).** The `<style>` element's content starts with `b` (not `<`), but
the whole element sits on one line with its tags, so no whitespace enters element text. The two
never/sometimes-empty `{% if %}` lines leave only blank lines between `>` and `<` — N3 erases
them. The `<title>` line's content is tight against both tags. No trim markers are needed
([README D6](README.md#d6--no-whitespace-control-markers-on-either-engine-in-either-track)).

### Idiomatic — both engines

**Single-file** (workloads.md idiomatic mixed-page rule, added E20: layout composition is
composed-page's dimension) — the full skeleton lives inline in `mixed-page.html`, authored
multi-line with the hero, banner conditional, product grid (loop + `{% if p.on_sale %}`), and
debug conditional indented naturally; the former `mixed-page-base.html` inheritance split is
deleted. Default escaping on (model values contain no escapables — byte-neutral; verified by
the verifier's exact-count needles, e.g. `<article class="card">` → 36, `<p class="sale">On
sale</p>` → 12). Doc citations: Askama book *Template syntax* (For, If); Tera docs *Control
structures*.

---

## Workload 5 — `conditional-heavy` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `conditional-heavy.golden.html`.
Idiomatic × 2: `conditional-heavy.verify.json`.

### Controlled — one text, both engines

Both engines accept `{% elif %}` (Askama 0.16 book documents `elif` and `else if` as
equivalents; Tera documents `elif`), so one text serves both — a four-way chain on precomputed
booleans plus two toggles, one line:

```jinja
<ul class="matrix">{% for r in rows %}<li>{% if r.is_bronze %}<span class="t0">bronze</span>{% elif r.is_silver %}<span class="t1">silver</span>{% elif r.is_gold %}<span class="t2">gold</span>{% else %}<span class="t3">platinum</span>{% endif %}<em>{{ r.name }}</em>{% if r.has_note %}<small>note {{ r.seq }}</small>{% endif %}{% if r.is_active %}<b>active</b>{% endif %}</li>{% endfor %}</ul>
```

### Idiomatic — both engines

Same branch structure, multi-line with each branch on its own line. Every branch body starts
with `<span` and the preceding `<li>` ends with `>`, so the layout's newlines are inter-tag and
normalize away in the verifier. Default escaping on. Doc citations: Askama book *Template
syntax — If*; Tera docs *Control structures — If*.

---

## Workload 6 — `fragment-heavy` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `fragment-heavy.golden.html`. Idiomatic × 2:
`fragment-heavy.verify.json`.

**Construct mapping (E20 landed form).** Four dispatched fragment kinds over six per-kind
files, one boolean four-way branch per row, one nesting level (card renders badge + price
against the row's promo). Both Rust engines take the scope-sharing include path:
`{% include %}` inside the loop and inside the card, the sub-partials referencing the loop
variable — Askama's book states includes see the including context; Tera renders includes
"using the current context", so `fragment-heavy-badge.html`'s `{{ item.promo.label }}`
resolves through the shared include context (the Tera 2.0 `{% component %}` explicit-argument
form remains the unused documented fallback).

### Controlled — one text set, both engines

Main (`fragment-heavy.html`) — the boolean `{% if %}/{% elif %}` dispatch chain:

```jinja
<div class="panel">{% for item in items %}{% if item.is_tile %}{% include "controlled/<engine>/shared/fragment-heavy-tile.html" %}{% elif item.is_card %}{% include "controlled/<engine>/shared/fragment-heavy-card.html" %}{% elif item.is_media %}{% include "controlled/<engine>/shared/fragment-heavy-media-row.html" %}{% else %}{% include "controlled/<engine>/shared/fragment-heavy-stat.html" %}{% endif %}{% endfor %}</div>
```

The six per-kind files (`<engine>` = `askama` / `tera` in each copy's include path):

```jinja
fragment-heavy-tile.html:      <section class="tile"><h3>{{ item.name }}</h3><p class="v">{{ item.value }}</p><span class="badge">{{ item.badge }}</span></section>
fragment-heavy-badge.html:     <span class="promo-badge">{{ item.promo.label }}</span>
fragment-heavy-price.html:     <p class="price">{{ item.promo.price }}.99</p>
fragment-heavy-card.html:      <article class="card"><h3>{{ item.name }}</h3>{% include ".../fragment-heavy-badge.html" %}{% include ".../fragment-heavy-price.html" %}<p class="v">{{ item.value }}</p></article>
fragment-heavy-media-row.html: <div class="media-row"><img src="/img/{{ item.name }}.jpg" alt="{{ item.name }}" /><div class="media-body"><h4>{{ item.name }}</h4><p>Caption for {{ item.name }}</p></div></div>
fragment-heavy-stat.html:      <div class="stat"><span class="stat-name">{{ item.name }}</span><span class="stat-value">{{ item.value }}</span><span class="stat-delta">{{ item.delta }}</span></div>
```

(The card **nests** badge + price — the one nesting level; the media caption, image source and
display price are composed in the templates as literal-plus-substitution, per E21. The
sub-partials read `item.promo.*` through the shared include context in both engines.)

### Idiomatic — both engines

Identical construct set (include-per-iteration is the documented way both engines split "large
or repetitive blocks" into files), authored multi-line. Default escaping on. Doc citations:
Askama book *Template syntax — Include*, *If*; Tera docs *Include*, *Control structures*.

---

## Workload 7 — `fortunes-encoded` (encoded)

**Cells and gates.** Controlled × 2: byte gate vs `fortunes-encoded.golden.html` **with N5
applied to the candidate** (encoded suite) plus the security floor (raw `<script>alert(` = 0
occurrences in un-normalized output; `&lt;script&gt;alert(` present after N5). Idiomatic × 2:
`fortunes-encoded.verify.json` (which carries the forbidden/required entries).

### Controlled — one text, both engines

One line, mirroring the Heddle template; substitutions are plain (no filters) because the
escaping path is the engine default — Askama's `Html` escaper via the `.html` extension (no
`escape` override on this struct), Tera's autoescape via the default-configured controlled-encoded
instance:

```jinja
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>{% for r in rows %}<tr><td>{{ r.id }}</td><td>{{ r.message }}</td></tr>{% endfor %}</table></body></html>
```

**Spelling note.** Tera 2.0.0's default `escape_html` emits the canonical five spellings
(`&amp; &lt; &gt; &quot; &#39;`) — N5 is an identity transform on its output. Askama 0.16.0
emits all-decimal NCRs (`&#38; &#60; &#62; &#34; &#39;`) — N5 canonicalizes four of the five
(the apostrophe is already canonical). Both verified against the pinned sources
([README D4](README.md#d4--askamas-spellings-are-reconciled-by-n5-in-the-gate-runner-not-by-a-custom-escaper)).
`r.id` is `i32` — its rendered digits contain nothing escapable, so escaping it is a no-op on
both engines.

### Idiomatic — both engines

Same table authored multi-line with the row markup tight (`<td>{{ r.id }}</td>`), default
escaping on — this **is** the idiomatic posture for untrusted data on both engines (no `safe`,
no raw). Doc citations: Askama book *Filters — escape*; Tera docs *Auto-escaping*.

---

## Workload 8 — `encoded-loop` (encoded)

**Cells and gates.** Controlled × 2: byte gate vs `encoded-loop.golden.html` with N5 + security
floor (`<script>alert(` never occurs in any output — the data contains none; the floor check
runs regardless — and the verifier's `<angle>` forbidden / `&lt;angle&gt;` ≥ 5000 rules guard
the idiomatic cells). Idiomatic × 2: `encoded-loop.verify.json`.

### Controlled — one text, both engines

One line; both text and attribute-value positions are plain substitutions under the default
escaper — Askama and Tera escape identically in both contexts (flat five-character escapers, so
the attribute-position output matches Heddle's `@attr` on the pinned alphabet, exactly as Phase
1's oracle analysis establishes):

```jinja
<table>{% for item in items %}<tr><td data-tag="{{ item.tag }}">{{ item.name }}</td><td>{{ item.comment }}</td></tr>{% endfor %}</table>
```

### Idiomatic — both engines

Same loop, multi-line, rows tight; default escaping on. Doc citations as workload 7.

---

## Cell → gate → asset summary

| Workload | Controlled gate asset | Idiomatic gate asset | N5 applied | Security floor |
|---|---|---|---|---|
| composed-page | `composed-page.golden.html` | `composed-page.verify.json` | no | no |
| trivial-substitution | `trivial-substitution.golden.html` | `trivial-substitution.verify.json` | no | no |
| large-loop | `large-loop.golden.html` | `large-loop.verify.json` | no | no |
| mixed-page | `mixed-page.golden.html` | `mixed-page.verify.json` | no | no |
| conditional-heavy | `conditional-heavy.golden.html` | `conditional-heavy.verify.json` | no | no |
| fragment-heavy | `fragment-heavy.golden.html` | `fragment-heavy.verify.json` | no | no |
| fortunes-encoded | `fortunes-encoded.golden.html` | `fortunes-encoded.verify.json` | yes | yes |
| encoded-loop | `encoded-loop.golden.html` | `encoded-loop.verify.json` | yes | yes |

All gate assets live at `benchmarks/dotnet/GoldenCorpus/` (Phase 1
[golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md#location-and-layout)),
read by the harness via the repo-relative path from `benchmarks/rust/`
(`../../benchmarks/dotnet/GoldenCorpus/`). Both gates run per cell **in the same process
before its timing loop** and again standalone via `cargo run --release --bin gate`
([README D11](README.md#d11--parity-before-timing-gates-run-inside-every-bench-binary-and-standalone)).
