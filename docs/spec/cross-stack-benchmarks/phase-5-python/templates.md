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
benchmarks/python/templates/
  jinja2/controlled/   composed-page.jinja  layout.jinja  trivial-substitution.jinja
                       large-loop.jinja  mixed-page.jinja  conditional-heavy.jinja
                       fragment-heavy.jinja  tile.jinja
                       fortunes-encoded.jinja  encoded-loop.jinja
  jinja2/idiomatic/    composed-page.jinja  layout.jinja  base.jinja
                       trivial-substitution.jinja  large-loop.jinja  mixed-page.jinja
                       conditional-heavy.jinja  fragment-heavy.jinja  tile.jinja
                       fortunes-encoded.html  encoded-loop.html
  mako/controlled/     composed-page.mako  layout.mako  … (same ids)  tile.mako
  mako/idiomatic/      … (same ids, all .mako)
```

All template files are committed LF (`.gitattributes` rule, [README WI1](README.md#wi1--harness-skeleton-environment-data-module)).
Controlled and idiomatic trees never share files. Context variable names (from
`runner/data.py`, [README D6](README.md#d6--model-data-is-regenerated-in-python-from-the-pinned-formulas-the-byte-gate-is-the-transcription-check)) —
identical for both engines and both tracks:

| Workload | Context |
|---|---|
| composed-page | `section` (dict: `meta, social, page_scripts, endpage_scripts`), `comp` (dict: `assets_styles, custom_styles, head_scripts, body_scripts, assets_scripts, body_end_scripts`), `areas` (dict keyed by area name), `area_names` (the seven-entry ordered list) — values transcribed from [TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs) and `AreaComponent.Areas` |
| trivial-substitution | top-level scalars `title, sku, price, brand, category, availability, url, image_url, summary, rating` — values from [SubstitutionContent.cs](../../../../src/Heddle.Performance/Runners/SubstitutionContent.cs) |
| large-loop | `items` — 5,000 dicts `{name: "row-"+i, value: i}` |
| mixed-page | `page_title, store_name, hero_heading, hero_tagline, show_banner, banner_text, show_debug_panel, footer_note, year, support_email, products` (36 product dicts `{name, sku, price, on_sale, blurb}`) |
| conditional-heavy | `rows` — 200 dicts `{name, note, is_bronze, is_silver, is_gold, has_note, is_active}` |
| fragment-heavy | `items` — 48 dicts `{name, value, badge}` |
| fortunes-encoded | `rows` — the 12 pinned dicts `{id, message}` |
| encoded-loop | `items` — 5,000 dicts `{tag, name, comment}` |

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

### Workload 1 — `composed-page`

Jinja2 `composed-page.jinja`:

```jinja
{% include "layout.jinja" %}
```

Jinja2 `layout.jinja` (one line):

```jinja
{{ section.meta }}{{ section.social }}{{ comp.assets_styles }}{{ comp.custom_styles }}{{ comp.head_scripts }}{{ comp.body_scripts }}{% for name in area_names %}{{ areas[name] }}{% endfor %}{{ comp.assets_scripts }}{{ section.page_scripts }}{{ section.endpage_scripts }}{{ comp.body_end_scripts }}
```

Mako `composed-page.mako`:

```mako
<%include file="layout.mako"/>\
```

Mako `layout.mako`:

```mako
${section["meta"]}${section["social"]}${comp["assets_styles"]}${comp["custom_styles"]}${comp["head_scripts"]}${comp["body_scripts"]}\
% for name in area_names:
${areas[name]}\
% endfor
${comp["assets_scripts"]}${section["page_scripts"]}${section["endpage_scripts"]}${comp["body_end_scripts"]}
```

This is the [LiquidTemplates.cs](../../../../src/Heddle.Performance/Runners/LiquidTemplates.cs)
twin shape one-for-one: layout as an include, sections/components as lookups, the ordered area
menus as a real loop.

### Workload 2 — `trivial-substitution`

Both engines: the exact
[SubstitutionLiquidTemplates.cs](../../../../src/Heddle.Performance/Runners/SubstitutionLiquidTemplates.cs)
card literal, one line, no whitespace between tags. Jinja2 `trivial-substitution.jinja` is that
Liquid text verbatim (the pure-substitution subset of Liquid is valid Jinja2):

```jinja
<article><h1>{{ title }}</h1><p class="sku">{{ sku }}</p><p class="price">{{ price }}</p><p class="brand">{{ brand }}</p><p class="cat">{{ category }}</p><p class="avail">{{ availability }}</p><a class="link" href="{{ url }}"><img src="{{ image_url }}"></a><p class="sum">{{ summary }}</p><p class="rating">{{ rating }}</p></article>
```

Mako `trivial-substitution.mako`: the same literal with each `{{ x }}` replaced by `${x}`.

### Workload 3 — `large-loop`

Jinja2 `large-loop.jinja` (the
[LoopLiquidTemplates.cs](../../../../src/Heddle.Performance/Runners/LoopLiquidTemplates.cs)
text, itself valid Jinja2):

```jinja
{% for item in items %}<tr><td>{{ item.name }}</td><td>{{ item.value }}</td></tr>{% endfor %}
```

Mako `large-loop.mako`:

```mako
% for item in items:
<tr><td>${item["name"]}</td><td>${item["value"]}</td></tr>\
% endfor
```

### Workload 4 — `mixed-page`

Jinja2 `mixed-page.jinja` mirrors the Heddle template of
[workloads.md](../phase-1-cross-stack-foundation/workloads.md#workload-4--mixed-page-raw) line
for line — identical literal skeleton (incl. the `<style>` line, byte-for-byte), with:

- scalars `{{ page_title }}`, `{{ store_name }}`, `{{ hero_heading }}`, `{{ hero_tagline }}`,
  `{{ banner_text }}`, `{{ footer_note }}`, `{{ year }}`, `{{ support_email }}`;
- `{% if show_banner %}<div class="banner">{{ banner_text }}</div>{% endif %}` on the banner line;
- the product loop as **one line**:
  `{% for p in products %}<article class="card"><h3>{{ p.name }}</h3><p class="sku">{{ p.sku }}</p><p class="price">{{ p.price }}</p>{% if p.on_sale %}<p class="sale">On sale</p>{% endif %}<p class="blurb">{{ p.blurb }}</p></article>{% endfor %}`;
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
<article class="card"><h3>${p["name"]}</h3><p class="sku">${p["sku"]}</p><p class="price">${p["price"]}</p>\
% if p["on_sale"]:
<p class="sale">On sale</p>\
% endif
<p class="blurb">${p["blurb"]}</p></article>\
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
<ul class="matrix">{% for r in rows %}<li>{% if r.is_bronze %}<span class="t0">bronze</span>{% elif r.is_silver %}<span class="t1">silver</span>{% elif r.is_gold %}<span class="t2">gold</span>{% else %}<span class="t3">platinum</span>{% endif %}<em>{{ r.name }}</em>{% if r.has_note %}<small>{{ r.note }}</small>{% endif %}{% if r.is_active %}<b>active</b>{% endif %}</li>{% endfor %}</ul>
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
<small>${r["note"]}</small>\
% endif
% if r["is_active"]:
<b>active</b>\
% endif
</li>\
% endfor
</ul>
```

### Workload 6 — `fragment-heavy`

Jinja2 `fragment-heavy.jinja` — include shares the active context, so the loop-local `item` is
visible in the partial (probe H, byte-exact):

```jinja
<div class="panel">{% for item in items %}{% include "tile.jinja" %}{% endfor %}</div>
```

Jinja2 `tile.jinja`:

```jinja
<section class="tile"><h3>{{ item.name }}</h3><p class="v">{{ item.value }}</p><span class="badge">{{ item.badge }}</span></section>
```

Mako — the `%for` loop variable is a generated-code local, **not** a context member, so the
include must pass it explicitly (probe H; a bare `<%include>` raises `NameError`):

`fragment-heavy.mako`:

```mako
<div class="panel">\
% for item in items:
<%include file="tile.mako" args="item=item"/>\
% endfor
</div>
```

`tile.mako`:

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
(`{# … #}` / `<%doc>…</%doc>`), per contract v2's idiomatic authoring standard:

| Workload | Jinja2 idiomatic construct | Jinja2 doc cited | Mako idiomatic construct | Mako doc cited |
|---|---|---|---|---|
| composed-page | `layout.jinja` renders the fragment sequence with a `{% for %}` and declares an empty `{% block content %}{% endblock %}`; `composed-page.jinja` is `{% extends "layout.jinja" %}` | [Template inheritance](https://jinja.palletsprojects.com/en/stable/templates/#template-inheritance) | `layout.mako` renders the sequence and calls `${self.body()}` (empty body); `composed-page.mako` opens with `<%inherit file="layout.mako"/>` | [Inheritance](https://docs.makotemplates.org/en/latest/inheritance.html) |
| trivial-substitution | plain template, multi-line card | [Variables](https://jinja.palletsprojects.com/en/stable/templates/#variables) | plain template, `${x}` expressions | [Expression substitution](https://docs.makotemplates.org/en/latest/syntax.html#expression-substitution) |
| large-loop | `{% for %}` over `items`, one row per source line | [For](https://jinja.palletsprojects.com/en/stable/templates/#for) | `% for` control lines | [Control structures](https://docs.makotemplates.org/en/latest/syntax.html#control-structures) |
| mixed-page | `base.jinja` skeleton with `{% block title %}` / `{% block content %}`; `mixed-page.jinja` extends it and fills the blocks; loop + `{% if %}` inside | [Template inheritance](https://jinja.palletsprojects.com/en/stable/templates/#template-inheritance), [If](https://jinja.palletsprojects.com/en/stable/templates/#if) | `base.mako` skeleton with `<%block name="title"/>` and `${self.body()}`; `mixed-page.mako` inherits and fills | [Inheritance — blocks](https://docs.makotemplates.org/en/latest/inheritance.html#using-blocks), [Defs and blocks](https://docs.makotemplates.org/en/latest/defs.html) |
| conditional-heavy | `{% if %}/{% elif %}/{% else %}` chain inside `{% for %}`, indented | [If](https://jinja.palletsprojects.com/en/stable/templates/#if) | `% if/% elif/% else` control lines | [Control structures](https://docs.makotemplates.org/en/latest/syntax.html#control-structures) |
| fragment-heavy | `tile.jinja` defines `{% macro tile(item) %}`; main imports via `{% from "tile.jinja" import tile %}` and calls `{{ tile(item) }}` | [Macros](https://jinja.palletsprojects.com/en/stable/templates/#macros), [Import](https://jinja.palletsprojects.com/en/stable/templates/#import) | `tile.mako` defines `<%def name="tile(item)">`; main imports via `<%namespace file="tile.mako" import="tile"/>` and calls `${tile(item)}` | [Defs](https://docs.makotemplates.org/en/latest/defs.html), [Namespaces](https://docs.makotemplates.org/en/latest/namespaces.html) |
| fortunes-encoded | `.html` template; escaping via the environment's `select_autoescape()` | [Autoescaping](https://jinja.palletsprojects.com/en/stable/api/#autoescaping) | `<%page expression_filter="h"/>` at the top of the template | [Filtering — expression_filter](https://docs.makotemplates.org/en/latest/filtering.html) |
| encoded-loop | `.html` template; same environment | same | same directive | same |

Both idiomatic mechanisms for fragment-heavy were executed (probe H: Jinja2 macro import; Mako
namespace-def import), and `<%inherit>` was executed (probe F). Idiomatic implementations must
still satisfy the security floor and the `forbidden`/`required` verifier entries on encoded
workloads — a missed escape fails the gate exactly as the plan's validation scenario demands.
