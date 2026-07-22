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

## Model construction (`src/models.rs`)

One module builds every model exactly once (`std::sync::OnceLock`), mirroring the .NET
`Shared`-instance discipline. All numeric formatting is `i32` `Display` (no locale, matching C#
invariant `int` formatting). Construction formulas, per workload:

| Workload | Rust construction (normative) |
|---|---|
| `composed-page` | `ComposedModel { section_meta, section_social, section_page_scripts: "", section_endpage_scripts: "", comp_assets_styles, comp_custom_styles, comp_head_scripts, comp_body_scripts, comp_assets_scripts, comp_body_end_scripts, area_names: Vec<String> (the 7 names in TwinContent.AreaOrder order), areas: HashMap<String, String> (all 7 entries, the empty-content area included as "") }`. Fragment strings come from `include_str!` of the data files below |
| `trivial-substitution` | `SubstitutionModel { title: "Heddle Handbook", sku: "HB-2001", price: 4200, brand: "Heddle Press", category: "Reference", availability: "In stock", url: "/catalog/handbook", image_url: "/img/handbook.png", summary: "A concise field guide to the engine.", rating: "4.8" }` |
| `large-loop` | 5,000 rows, `i` in `[0, 4999]`: `LoopRow { name: format!("row-{i}"), value: i }` |
| `mixed-page` | Page scalars exactly as Phase 1 pins them (`page_title: "Mercantile - Catalog"`, …, `show_banner: true`, `show_debug_panel: false`, `year: 2026`, `support_email: "support at mercantile.example"`); 36 products, `i` in `[1, 36]`: `MixedProduct { name: format!("Product {i:02}"), sku: format!("MX-{}", 1000 + i), price: 950 + i * 7, on_sale: i % 3 == 0, blurb: format!("A dependable workshop staple from batch {i}, checked for daily use and backed by our lifetime guarantee.") }` |
| `conditional-heavy` | 200 rows, `i` in `[0, 199]`: `ConditionalRow { name: format!("unit-{i:03}"), note: format!("note {i}"), is_bronze: i % 4 == 0, is_silver: i % 4 == 1, is_gold: i % 4 == 2, has_note: i % 2 == 0, is_active: i % 5 != 0 }` |
| `fragment-heavy` | 48 rows, `i` in `[0, 47]`: `FragmentRow { name: format!("tile-{i:02}"), value: i * 11, badge: ["new", "hot", "sale", "std"][i as usize % 4] }` |
| `fortunes-encoded` | Exactly the 12 pinned `(id, message)` rows of Phase 1 [workloads.md — workload 7](../phase-1-cross-stack-foundation/workloads.md#workload-7--fortunes-encoded-encoded), transcribed as Rust string literals byte-for-byte (row 4/8 em dashes are U+2014; row 11 is the XSS payload; row 12 the Japanese string) |
| `encoded-loop` | 5,000 rows, `i` in `[0, 4999]`: `EncodedLoopRow { tag: format!("tag-{i}&'{}'", i % 7), name: format!("item <{i}> & \"co\""), comment: format!("'q' & <angle> \"d\" こんにちは {i}") }` |

All model structs derive `serde::Serialize` (Tera contexts are built from them); Askama template
structs borrow them (`&'static` references to the `OnceLock` singletons). A unit test per model
pins the counts and distinctive values ([README testing plan](README.md#testing-plan)).

### Composed-page fragment data files (`data/composed-page/`)

The composed-page model data is Heddle's own fragment set, exactly as the .NET twins consume it
from [`TwinContent.cs`](../../../../src/Heddle.Performance/Runners/TwinContent.cs) and
[`AreaComponent.cs`](../../../../src/Heddle.Performance/TestSuite/Extensions/AreaComponent.cs)
(`AreaComponent.Areas` — the very dictionary Heddle renders from). The Rust port cannot
reference C# statics, so the fragments are committed as data files consumed via `include_str!`:

```
benchmarks/rust/data/composed-page/
  section-meta.html            ← TwinContent.SectionMeta
  section-social.html          ← TwinContent.SectionSocial
  comp-assets-styles.html      ← TwinContent.CompAssetsStyles
  comp-custom-styles.html      ← TwinContent.CompCustomStyles
  comp-head-scripts.html       ← TwinContent.CompHeadScripts
  comp-body-scripts.html       ← TwinContent.CompBodyScripts
  comp-assets-scripts.html     ← TwinContent.CompAssetsScripts
  comp-body-end-scripts.html   ← TwinContent.CompBodyEndScripts
  area-1.html … area-7.html    ← AreaComponent.Areas values, in TwinContent.AreaOrder order
                                 (area-6 is the empty "Alert Top Section Below Nav" entry: a 0-byte file)
```

The two empty sections (`SectionPageScripts`, `SectionEndPageScripts`) are empty-string consts in
`models.rs`, not files. Copy rule: the file content is the C# string literal's value, byte-for-byte
(the multi-line verbatim literals keep their internal indentation — it is inter-tag whitespace
and N3 erases it). Any copy error is caught by the controlled byte gate, which is exactly the
"twins cannot silently drift" property the .NET suite gets from sharing the statics. These files
are pinned `-text` in `.gitattributes` ([README D2](README.md#d2--harness-location-benchmarksrust-a-new-top-level-benchmarks-directory)).
*(Verify at implementation: the exact `AreaComponent.Areas` entry for "Alert Top Section Below
Nav" — TwinContent's `AreaOrder` comment records it as empty content; transcribe whatever the
dictionary actually holds.)*

---

## Workload 1 — `composed-page` (raw)

**Cells and gates.** Controlled Askama + controlled Tera: byte gate vs
`composed-page.golden.html` (N5 not applied — raw suite). Idiomatic Askama + idiomatic Tera:
`composed-page.verify.json`.

**Construct mapping.** Heddle composes via `@<<{{layout.heddle}}` (documented fragment-sequence
output, Q1.4/D5 in Phase 1); the .NET twins mirror it as *home = one include of layout; layout =
ordered concatenation of section/component substitutions + one real loop over the ordered area
names with a per-name lookup* ([`LiquidTemplates.cs`](../../../../src/Heddle.Performance/Runners/LiquidTemplates.cs)).
The Rust controlled ports keep exactly that construct set: `{% include %}` for the layout, scalar
substitutions for sections/components, `{% for %}` over `area_names`, and a per-name lookup —
Tera via documented bracket indexing (`areas[name]`), Askama via a documented `self` method call
(`self.area(name)`, since bracket indexing with a variable key is not a documented Askama
construct; [README D7](README.md#d7--controlled-track-construct-mapping-one-jinja-family-text-where-the-engines-agree)).

### Controlled — Askama

`templates/controlled/askama/composed-page.html`:

```jinja
{% include "controlled/askama/composed-page-layout.html" %}
```

`templates/controlled/askama/composed-page-layout.html` (one line):

```jinja
{{ section_meta }}{{ section_social }}{{ comp_assets_styles }}{{ comp_custom_styles }}{{ comp_head_scripts }}{{ comp_body_scripts }}{% for name in area_names %}{{ self.area(name) }}{% endfor %}{{ comp_assets_scripts }}{{ section_page_scripts }}{{ section_endpage_scripts }}{{ comp_body_end_scripts }}
```

Template struct (in `src/engines/askama_controlled.rs`):

```rust
#[derive(askama::Template)]
#[template(path = "controlled/askama/composed-page.html", escape = "none")]
pub struct ComposedControlled<'a> {
    pub section_meta: &'a str,
    pub section_social: &'a str,
    pub section_page_scripts: &'a str,
    pub section_endpage_scripts: &'a str,
    pub comp_assets_styles: &'a str,
    pub comp_custom_styles: &'a str,
    pub comp_head_scripts: &'a str,
    pub comp_body_scripts: &'a str,
    pub comp_assets_scripts: &'a str,
    pub comp_body_end_scripts: &'a str,
    pub area_names: &'a [String],
    pub areas: &'a std::collections::HashMap<String, String>,
}
impl ComposedControlled<'_> {
    fn area(&self, name: &str) -> &str { &self.areas[name] }
}
```

(Askama loop variables borrow, so `name` is `&String`; deref coercion turns it into the `&str`
argument. Included templates get full access to the including context — verified against the
0.16.0 book, "Included templates get full access to the context in which they're used".)

### Controlled — Tera

`templates/controlled/tera/composed-page.html` / `…/composed-page-layout.html` — same two texts
with the include target `"controlled/tera/composed-page-layout.html"` and the lookup spelled with
documented bracket notation:

```jinja
… {% for name in area_names %}{{ areas[name] }}{% endfor %} …
```

Registered in the **autoescape-off** controlled-raw instance. Context: `section_*`/`comp_*`
scalars, `area_names` array, `areas` map, inserted from the shared model.

### Idiomatic — both engines

The docs-taught form of "a page composed from a layout" is template inheritance: a base template
(`extends`/`block`) whose blocks default to the section content, with the child overriding
nothing — mirroring how `layout.heddle` supplies section defaults. Multi-line, indented; the
area loop is identical to the controlled form. Because this workload's model values *are*
trusted HTML fragments, the idiomatic templates use each engine's documented trusted-HTML
mechanism — the `safe` filter — on every fragment-valued expression (Askama and Tera both:
`{{ section_meta|safe }}`, `{{ areas[name]|safe }}` / `{{ self.area(name)|safe }}`), because the
idiomatic instances run with default escaping on ([README D3](README.md#d3--escaping-mode-per-template-per-track)).

`templates/idiomatic/askama/composed-page-base.html` (child `composed-page.html` is
`{% extends "idiomatic/askama/composed-page-base.html" %}` with no overrides; Tera mirror under
`idiomatic/tera/` with `areas[name]`):

```jinja
{% block meta %}{{ section_meta|safe }}{% endblock %}
{% block social %}{{ section_social|safe }}{% endblock %}
{{ comp_assets_styles|safe }}
{{ comp_custom_styles|safe }}
{{ comp_head_scripts|safe }}
{{ comp_body_scripts|safe }}
{% for name in area_names %}
  {{ self.area(name)|safe }}
{% endfor %}
{{ comp_assets_scripts|safe }}
{% block page_scripts %}{{ section_page_scripts|safe }}{% endblock %}
{% block endpage_scripts %}{{ section_endpage_scripts|safe }}{% endblock %}
{{ comp_body_end_scripts|safe }}
```

Doc citations (header comment in each file, per Q1.7/D16): Askama book *Template syntax —
Template inheritance*, *Filters — safe*; Tera docs *Inheritance*, *Auto-escaping* (safe filter).

**Idiomatic-gate note.** The verifier's ordered markers walk the fragment sequence
(SectionMeta → SectionSocial → styles → areas → body-end scripts); the inheritance form
preserves that order by construction. The `{% for %}`/`{% block %}` tag lines contribute only
inter-tag whitespace, which the verifier's N1–N4 normalization erases. One subtlety: the
`comp_custom_styles` fragment is `/* CSS Comment Test */` (`TwinContent.CompCustomStyles`), which
**begins and ends with `/`, not `<`/`>`**. Its neighbours are `CompAssetsStyles` (ends `… />`) and
`CompHeadScripts` (starts `<script …`), so the two adjacent newlines are `>\n/` and `/\n<` — neither
is a `>`-whitespace-`<` run, so N3 does **not** collapse them. Since the 2026-07-20 N3b maintainer
step, however, **N3b removes every whitespace run anywhere to nothing** from both sides at
comparison, so these newlines vanish under normalization (identically on the oracle and the
candidate) and any whitespace-only difference here — including a space-vs-nothing one — passes by
construction. This is harmless either way:
`comp_custom_styles` is neither a `values` nor an ordered-`markers` verifier needle for this
workload (golden-corpus.md), and no marker substring spans that boundary, so the inter-fragment
whitespace cannot fail the idiomatic verifier. (If strict byte-adjacency were ever required, the
fragment would be authored tight against a neighbour on one line; the gate does not require it.)

---

## Workload 2 — `trivial-substitution` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `trivial-substitution.golden.html`.
Idiomatic × 2: `trivial-substitution.verify.json`.

### Controlled — one text, both engines

`templates/controlled/askama/trivial-substitution.html` and
`templates/controlled/tera/trivial-substitution.html` (one line, mirroring
[`trivial-substitution.heddle`](../../../../src/Heddle.Performance/TestTemplates/trivial-substitution.heddle)):

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

### Controlled — one text, both engines

```jinja
{% for item in items %}<tr><td>{{ item.name }}</td><td>{{ item.value }}</td></tr>{% endfor %}
```

(Mirrors [`large-loop.heddle`](../../../../src/Heddle.Performance/TestTemplates/large-loop.heddle).
Askama: `items: &[LoopRow]`, `escape = "none"`. Tera: controlled-raw instance.)

### Idiomatic — both engines

The same loop, multi-line, with the row markup kept tight (`<td>{{ item.name }}</td>` — text
inside an element must stay adjacent to its tags; the verifier needle
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
{% for p in products %}<article class="card"><h3>{{ p.name }}</h3><p class="sku">{{ p.sku }}</p><p class="price">{{ p.price }}</p>{% if p.on_sale %}<p class="sale">On sale</p>{% endif %}<p class="blurb">{{ p.blurb }}</p></article>{% endfor %}
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

Docs-taught inheritance: `mixed-page-base.html` carries the skeleton (`<head>`, header, footer)
with `{% block content %}`; the child `mixed-page.html` extends it and fills the hero, banner
conditional, product grid (loop + `{% if p.on_sale %}`), and debug conditional, indented
naturally. Default escaping on (model values contain no escapables — byte-neutral; verified by
the verifier's exact-count needles, e.g. `<article class="card">` → 36, `<p class="sale">On
sale</p>` → 12). Doc citations: Askama book *Template inheritance*; Tera docs *Inheritance*,
*Control structures*.

---

## Workload 5 — `conditional-heavy` (raw)

**Cells and gates.** Controlled × 2: byte gate vs `conditional-heavy.golden.html`.
Idiomatic × 2: `conditional-heavy.verify.json`.

### Controlled — one text, both engines

Both engines accept `{% elif %}` (Askama 0.16 book documents `elif` and `else if` as
equivalents; Tera documents `elif`), so one text serves both — a four-way chain on precomputed
booleans plus two toggles, one line:

```jinja
<ul class="matrix">{% for r in rows %}<li>{% if r.is_bronze %}<span class="t0">bronze</span>{% elif r.is_silver %}<span class="t1">silver</span>{% elif r.is_gold %}<span class="t2">gold</span>{% else %}<span class="t3">platinum</span>{% endif %}<em>{{ r.name }}</em>{% if r.has_note %}<small>{{ r.note }}</small>{% endif %}{% if r.is_active %}<b>active</b>{% endif %}</li>{% endfor %}</ul>
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

**Construct mapping.** The Phase 1 twin construct is *one registered partial invoked once per
loop iteration, receiving the current row* — with scope-sharing includes on DotLiquid/Scriban
and explicit-argument partials on Fluid/Handlebars. Both Rust engines take the scope-sharing
path: `{% include %}` inside the loop, the tile referencing the loop variable — Askama's book
states includes see loop variables; Tera renders includes "using the current context".
*(Verify at implementation for Tera 2.0: the loop variable's visibility inside the include —
the byte gate proves it either way; fallback below.)*

### Controlled — one text pair, both engines

Main (`fragment-heavy.html`):

```jinja
<div class="panel">{% for item in items %}{% include "controlled/<engine>/fragment-heavy-tile.html" %}{% endfor %}</div>
```

Tile (`fragment-heavy-tile.html`):

```jinja
<section class="tile"><h3>{{ item.name }}</h3><p class="v">{{ item.value }}</p><span class="badge">{{ item.badge }}</span></section>
```

(`<engine>` = `askama` / `tera` in each copy's include path.)

**Primary path grounding.** Tera 2.0's `{% include %}` renders "using the current context"
(README assumed state, keats.github.io/tera include section); during a `{% for %}` iteration the
current context contains the loop variable `item`, so the tile's `{{ item.name }}` resolves. This
is the verified primary path — the marker above is a byte-gate-decided confirmation fact (per D4's
permitted-marker convention), not an open design choice: whichever mechanism is used, the composed
tile bytes are identical and the byte gate decides.

**Fallback** (only if a Tera 2.0 point-release regressed include's loop-variable visibility): the
documented partial-with-explicit-arguments mechanism is a Tera **component** — verified to exist in
2.0 (README assumed state; Tera's own docs state `{% include %}` cannot be passed a custom context,
so components are the sanctioned way to pass one). Define the tile once as
`{% component tile(item) %}<section class="tile">…</section>{% endcomponent %}` and invoke it inside
the loop (JSX-style `{{<tile item=item />}}` per the 2.0 component syntax), carrying `item`
explicitly. Inlining the tile body into the loop is *not* an option — it would remove the per-call
composition the workload owns. If the fallback is used it is recorded in the report's port notes;
the cell's gate and measured composition-per-call shape are unchanged either way. There is no
`render_component` symbol in Tera 2.0 — the construct is the `{% component %}`/`{% endcomponent %}`
tag pair.

### Idiomatic — both engines

Identical construct (include-per-iteration is the documented way both engines split "large or
repetitive blocks" into files), authored multi-line. Default escaping on. Doc citations: Askama
book *Template syntax — Include*; Tera docs *Include*.

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

All gate assets live at `src/Heddle.Performance/GoldenCorpus/` (Phase 1
[golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md#location-and-layout)),
read by the harness via the repo-relative path from `benchmarks/rust/`
(`../../src/Heddle.Performance/GoldenCorpus/`). Both gates run per cell **in the same process
before its timing loop** and again standalone via `cargo run --release --bin gate`
([README D11](README.md#d11--parity-before-timing-gates-run-inside-every-bench-binary-and-standalone)).
