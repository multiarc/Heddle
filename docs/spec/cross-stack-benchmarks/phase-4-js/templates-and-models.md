# JS templates and models — normative texts

Supplementary document of the [Phase 4 — js spec](README.md). It pins the JS model
transcriptions and the exact template text of every workload×engine×track cell, the registered
helper surface with its report disclosure text, and the doc citations the idiomatic files carry.
Workload semantics and the .NET twin texts these start from are pinned in Phase 1's
[workloads.md](../phase-1-cross-stack-foundation/workloads.md); the gates that judge these texts
are in [parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md).

Authoring rules carried from Phase 1 (workloads.md §Shared authoring rules): raw suites render
each engine's non-encoding path (Handlebars triple-mustache; Eta `<%~ %>`); encoded suites
render each engine's escaping path (Handlebars double-mustache, stock; Eta `<%= %>` under
default `autoEscape`); templates are authored so any cross-engine difference is erased by
N1–N4 (+N5 encoded); the controlled floor templates below are single-line — line breaks inside
fenced blocks marked *(wrapped for readability)* are **not** part of the template text — while
the layout shells, the secondary-menu chrome partials and `mixed-page` are multi-line (every
such line break is N3b-erased, so the byte gate is indifferent).

## Models — transcription rules

One ES module per workload under `benchmarks/js/src/models/`, exporting a single frozen model
object (deep-frozen at module load). Rules:

1. **Keys are the Phase 1 dictionary-view keys** — lowercase snake_case (`page_title`,
   `image_url`, `is_bronze`, …) — so one model object serves Handlebars (`{{page_title}}`),
   Eta (`it.page_title`), and both tracks.
2. **Type mapping:** .NET `string` → JS string, transcribed byte-for-byte (source files are
   UTF-8; the Japanese and U+2014 characters are pasted literally); .NET `int` → JS number
   (every pinned numeric value in the set is an integer; `String(n)` on an integer is
   culture-free and byte-identical to .NET invariant `int` formatting — no float ever renders;
   the sole scientific-notation figure `4.33e67` is inside a pinned string); .NET `bool` → JS
   boolean.
3. **Generation formulas are transcribed, not their outputs** — e.g. mixed-page products are
   built by a loop `for (let i = 1; i <= 36; i++)` applying the exact workloads.md formulas
   (`Product ${String(i).padStart(2, "0")}`, `sku_number: 1000 + i`, `950 + i * 7`,
   `i % 3 === 0`, `batch: i`), and likewise for conditional-heavy (200 rows,
   `unit-${String(i).padStart(3, "0")}`, `seq: i`, `i % 4` tier booleans, `i % 2 === 0`,
   `i % 5 !== 0`), fragment-heavy (48 rows, `kind: ["tile","card","media","stat"][i % 4]` with
   the four precomputed `is_tile`/`is_card`/`is_media`/`is_stat` dispatch booleans — E20,
   `item-${String(i).padStart(2, "0")}`, `i * 11`, `["new","hot","sale","std"][i % 4]`,
   `delta: (i % 7) - 3`, and a nested `promo: { label: badge, price: 9 + i }` on every row),
   large-loop (5,000 rows, `{ value: i }` and nothing else), and encoded-loop (5,000 rows,
   `` `tag-${i}&'${i % 7}'` ``, `` `item <${i}> & "co"` ``,
   `` `'q' & <angle> "d" こんにちは ${i}` ``).

   **The models carry DATA only — no derived display strings (ledger E21).** `sku_number`,
   `batch`, `seq`, `delta` and `promo.price` are JS **numbers**; the display forms
   (`MX-` + sku_number, the blurb sentence around batch, `note ` + seq, `row-` + value, the
   media caption/image src, the `.99` display price) are composed by the TEMPLATES as
   literal-plus-substitution — that composition is the work being measured, and pre-formatting
   it model-side is a port defect. Large-loop rows carry ONLY `value` (no `name`);
   fragment-heavy rows carry no `caption`/`image_url`/price string. The zero-padded identity
   names (`item-{i:D2}`, `unit-{i:D3}`, `Product {i:D2}`) stay model-side by design — row
   identity, not display text.
4. **Pinned literal sets are transcribed verbatim:** the 12 fortunes rows (ids 1–12, messages
   byte-for-byte from workloads.md, including the XSS payload row 11 and Japanese row 12); the
   trivial-substitution scalar values (from
   [SubstitutionContent.cs](../../../../benchmarks/dotnet/src/Models/SubstitutionContent.cs)
   — `Heddle Handbook`, `HB-2001`, `4.8`, etc.); the mixed-page page scalars (workloads.md).
5. **Composed-page is pure structured data — `{ nav }` and NOTHING else (ledger E20, E22).**
   The model module loads the nav model once at module init from the Phase 1 corpus fixture
   `benchmarks/dotnet/GoldenCorpus/fixtures/composed-page/nav.json` — the single source of
   truth every non-.NET ecosystem loads from — resolving the corpus directory the same way the
   gate's corpus loader does (`corpusDir` from `src/gate/corpus.mjs`), verifying the bytes
   against the manifest's `fixtures` section (SHA-256 + byte length, the same
   corrupted-checkout guard as the golden loader) before `JSON.parse`, then deep-freezing:

   ```js
   const nav = JSON.parse(…verified nav.json bytes…);   // { menus, footer_columns }, snake_case
   export const model = deepFreeze({ nav });
   ```

   `nav.menus[].tabs[]` carry `label, href, css, has_dropdown, dropdown_css, columns`;
   columns → `sections[]` (`title, href, title_linked, links[]`) → `links[]`
   (`label, href`); `nav.footer_columns` are the same column shape. `has_dropdown` and
   `title_linked` are precomputed booleans. The pre-E20 blob transcription (`section`, `comp`,
   `area_names`, `areas` — ~1,100 lines of literal HTML) is **deleted, not relocated**: every
   literal chrome fragment lives in the template tier (E22 — templates hold ALL text; the
   model-preparation tier carries DATA only), so the JS templates transcribe the chrome as
   literal template text policed by the byte gate (controlled) and verifier (idiomatic).
6. **Fortunes/encoded-loop values must satisfy the untrusted-data alphabet** by construction —
   they are the Phase 1 pinned values, which already do; the transcription adds or removes
   nothing.

## Registered helpers — the full disclosure surface — Amended (E22 port landing, 2026-08-08)

The complete registered-helper surface of the JS suite, both tracks, is **ZERO helpers**.

The `area` helper this section previously specified is **deleted**: E22 removed the C# text
tier it read from, so composed-page is pure template composition — the chrome fragments are
registered **partials** of literal template text and the nav renders through nested partials.
No workload registers any helper in either track; the only helpers any template invokes are
the built-ins (`#if`, `#each`).

**Report disclosure text (verbatim — D5/D15 require it in the published report):**

> *Registered helpers (Handlebars, both tracks): none. The suite registers zero helpers; the
> composed-page workload composes through a partial-block layout and registered partials of
> literal template text, and conditional-heavy uses only the built-in `{{#if}}` /
> `{{else if}}` chain over precomputed boolean model fields — no comparison or equality
> helpers are registered anywhere in this suite.*

The old disclosure of a `knownHelpers: { area: true }` divergence in the composed-page
idiomatic precompile options is **obsolete and superseded**: with no helper registered,
`knownHelpersOnly: true` needs no `knownHelpers` extension anywhere, and **all 16 idiomatic
precompiles use identical options** (`{ knownHelpersOnly: true }`).

No Eta workload registers helper-like extensions; Eta's `include`/`layout` are built-in engine
functions, not user extensions, and are named as such in the report's methodology note.

## Handlebars — controlled track

Files: `benchmarks/js/src/templates/handlebars/controlled/<id>.hbs` + the partial files
(`<name>.partial.hbs` — the cold-compile discovery convention, below). Compile mode: runtime
`hb.compile` once at startup (README D7). Raw workloads: triple-mustache throughout; encoded
workloads: double-mustache, stock escaper, N5 in the gate (README D4). Each workload gets its
own `Handlebars.create()` environment (partial registration is per-environment).

**Cold-compile support-file convention (bench/cold-compile.mjs):** any file named
`<name>.partial.<ext>` or `<name>.layout.<ext>` under an engine's `controlled/` directory is
auto-discovered and registered (Handlebars `registerPartial(name, …)`; Eta
`loadTemplate("@" + name, …)`) before the workload's cold compile — the support set needs no
per-workload list in the cold harness.

### Workload 1 — `composed-page` — Amended (E20/E22 landed form)

Partial-block layout — **probe-verified on handlebars 4.7.9** (and Handlebars.Net 2.1.6 on
the .NET side) in both the runtime-compile and precompile/`template` paths. Entry template
(`composed-page.hbs`):

```handlebars
{{#> layout}}
…the slider markup, transcribed from home.heddle…
{{/layout}}
```

Registered partial `layout` (`layout.partial.hbs`, ~125 lines): the **full literal chrome**
with `{{> @partial-block}}` at the body-slot position, the section defaults inlined as
literal text (`<title>Title</title>`, the socialmeta tags — Handlebars has no
overridable-section mechanism; the empty `page_scripts`/`endpage_scripts` defaults are simply
absent), the ten chrome-fragment partial calls (`{{> alert_top}}`,
`{{> secondary_wholesale_menu}}`, `{{> secondary_retail_menu}}`, `{{> alert_below}}`,
`{{> assets_styles}}`, `{{> custom_styles}}`, `{{> head_scripts}}`, `{{> body_scripts}}`,
`{{> assets_scripts}}`, `{{> body_end_scripts}}` — one literal-text `.partial.hbs` per
fragment, mirroring `chrome-fragments.heddle`), and the nav rendered through nested partials:
`{{#each nav.menus}}{{> mega_menu}}{{/each}}` at the mega-menu site and
`{{#each nav.footer_columns}}{{> nav_column}}{{/each}}` in the footer, with
`mega_menu → nav_column → nav_section → nav_link` each `{{#each}}`-ing into the next
(implicit current context — inside `#each` the partial sees the current item).

**15 registered partials** for this workload: `layout` + the ten chrome fragments + the four
nav partials. All partial names are snake_case, one file per partial.

### Workload 2 — `trivial-substitution`

`trivial-substitution.hbs` *(one line)*:

```handlebars
<article><h1>{{{title}}}</h1><p class="sku">{{{sku}}}</p><p class="price">{{{price}}}</p><p class="brand">{{{brand}}}</p><p class="cat">{{{category}}}</p><p class="avail">{{{availability}}}</p><a class="link" href="{{{url}}}"><img src="{{{image_url}}}"></a><p class="sum">{{{summary}}}</p><p class="rating">{{{rating}}}</p></article>
```

### Workload 3 — `large-loop`

`large-loop.hbs` *(one line)*:

```handlebars
{{#each items}}<tr><td>row-{{{value}}}</td><td>{{{value}}}</td></tr>{{/each}}
```

(E21 landed form — the display name is composed in the template as the literal `row-` + the
value substitution; the model row carries only `value`.)

### Workload 4 — `mixed-page`

`mixed-page.hbs` — the Heddle skeleton of workloads.md with the Handlebars substitutions its
twin section prescribes (`{{{…}}}` scalars, `{{#if}}`, `{{#each}}`). Literal HTML (doctype,
`<style>` block, header/nav/footer) is byte-identical to the Heddle template text:

```handlebars
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>{{{page_title}}}</title>
<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
</head>
<body>
<header>
<h1>{{{store_name}}}</h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
{{#if show_banner}}<div class="banner">{{{banner_text}}}</div>{{/if}}
<section class="hero">
<h2>{{{hero_heading}}}</h2>
<p>{{{hero_tagline}}}</p>
</section>
<section class="grid">
{{#each products}}<article class="card"><h3>{{{name}}}</h3><p class="sku">MX-{{{sku_number}}}</p><p class="price">{{{price}}}</p>{{#if on_sale}}<p class="sale">On sale</p>{{/if}}<p class="blurb">A dependable workshop staple from batch {{{batch}}}, checked for daily use and backed by our lifetime guarantee.</p></article>{{/each}}
</section>
{{#if show_debug_panel}}<pre class="debug">debug</pre>{{/if}}
</main>
<footer>
<p>{{{footer_note}}}</p>
<p>{{{store_name}}} {{{year}}} {{{support_email}}}</p>
</footer>
</body>
</html>
```

(Line structure mirrors the Heddle template; all line-break differences are erased by N2–N4.
Text inside elements — e.g. `{{{store_name}}} {{{year}}} {{{support_email}}}` with its single
spaces — is byte-significant and matches the Heddle template exactly.)

### Workload 5 — `conditional-heavy`

`conditional-heavy.hbs` *(one line)* — workloads.md twin text verbatim; **no helpers**
(README D5):

```handlebars
<ul class="matrix">{{#each rows}}<li>{{#if is_bronze}}<span class="t0">bronze</span>{{else if is_silver}}<span class="t1">silver</span>{{else if is_gold}}<span class="t2">gold</span>{{else}}<span class="t3">platinum</span>{{/if}}<em>{{{name}}}</em>{{#if has_note}}<small>note {{{seq}}}</small>{{/if}}{{#if is_active}}<b>active</b>{{/if}}</li>{{/each}}</ul>
```

### Workload 6 — `fragment-heavy` — Amended (E20 landed form)

`fragment-heavy.hbs` *(one line)* — the chained boolean dispatch (the probe-verified
`{{else if}}` pattern from conditional-heavy) over the six registered partials:

```handlebars
<div class="panel">{{#each items}}{{#if is_tile}}{{> tile this}}{{else if is_card}}{{> card this}}{{else if is_media}}{{> media_row this}}{{else}}{{> stat this}}{{/if}}{{/each}}</div>
```

The six partials (`tile`, `card`, `badge`, `price`, `media_row`, `stat` — snake_case names,
one `.partial.hbs` file each; `{{> tile this}}` is the documented partial-with-explicit-context
syntax); the card nests badge + price against the row's promo:

```handlebars
tile.partial.hbs:      <section class="tile"><h3>{{{name}}}</h3><p class="v">{{{value}}}</p><span class="badge">{{{badge}}}</span></section>
badge.partial.hbs:     <span class="promo-badge">{{{label}}}</span>
price.partial.hbs:     <p class="price">{{{price}}}.99</p>
card.partial.hbs:      <article class="card"><h3>{{{name}}}</h3>{{> badge promo}}{{> price promo}}<p class="v">{{{value}}}</p></article>
media_row.partial.hbs: <div class="media-row"><img src="/img/{{{name}}}.jpg" alt="{{{name}}}" /><div class="media-body"><h4>{{{name}}}</h4><p>Caption for {{{name}}}</p></div></div>
stat.partial.hbs:      <div class="stat"><span class="stat-name">{{{name}}}</span><span class="stat-value">{{{value}}}</span><span class="stat-delta">{{{delta}}}</span></div>
```

(the media caption, image source and `.99` display price are template-composed — E21.)

### Workload 7 — `fortunes-encoded`

`fortunes-encoded.hbs` *(one line)* — double-mustache, stock escaper:

```handlebars
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>{{#each rows}}<tr><td>{{id}}</td><td>{{message}}</td></tr>{{/each}}</table></body></html>
```

Escaper walkthrough on the pinned data: rows 3/11 apostrophes → `&#x27;` (N5 → `&#39;`);
row 11 `<`/`>`/`"` → `&lt;`/`&gt;`/`&quot;` (already canonical); row 12 Japanese passes
through untouched; no pinned message contains `` ` `` or `=` (alphabet), so the beyond-five
table entries never fire. Post-N5 output is byte-identical to the oracle.

### Workload 8 — `encoded-loop`

`encoded-loop.hbs` *(one line)*:

```handlebars
<table>{{#each items}}<tr><td data-tag="{{tag}}">{{name}}</td><td>{{comment}}</td></tr>{{/each}}</table>
```

Same walkthrough: data `&`/`<`/`>`/`"` escape to the canonical named forms; every `'` →
`&#x27;` → N5 → `&#39;`; `こんにちは` passes through; no `` ` ``/`=`/`+` exists in the data.

## Handlebars — idiomatic track

Files: `benchmarks/js/src/templates/handlebars/idiomatic/<id>.hbs` (+ the same partial set).
Texts: identical to the controlled texts above (README D9 — the controlled twins already are
the documented patterns), with `mixed-page.hbs` and `fortunes-encoded.hbs` kept in the same
multi-line/single-line form as controlled (no gratuitous reformatting — one less diff surface).
Execution mode is the differentiator: `Handlebars.precompile(src, { knownHelpersOnly: true })`
— **identical options for all 16 idiomatic precompiles**; with zero registered helpers
(above), no `knownHelpers` extension exists anywhere — +
`Handlebars.template(eval("(" + spec + ")"))` once at startup (README D7). Partials are
precompiled and registered through the same path.

Each idiomatic file carries the Q1.7 citation header:

```handlebars
{{!-- idiomatic Handlebars implementation (Phase 4).
     Patterns followed: https://handlebarsjs.com/guide/#simple-expressions,
     https://handlebarsjs.com/guide/builtin-helpers.html (#if, #each),
     https://handlebarsjs.com/guide/partials.html (partials, partial contexts),
     https://handlebarsjs.com/guide/installation/precompilation.html (precompiled execution). --}}
```

(The comment is stripped by compilation and emits no output byte; the idiomatic gate is the
whitespace-insensitive verifier regardless.)

## Eta — controlled track

Files: `benchmarks/js/src/templates/eta/controlled/` — the eight `<id>.eta` workload
templates + `shell.layout.eta` + **20 `<name>.partial.eta`** files (the ten chrome fragments,
the four nav partials, and the six fragment-heavy partials). Instance: `new Eta()` defaults;
templates registered as `@<id>`/`@<name>` via `loadTemplate`; render call
`eta.render("@<id>", model)` (README D8). Raw workloads use `<%~ %>`; encoded use `<%= %>`.
The floor workload texts are single physical lines with no trailing newline *(wrapped for
readability below)*; the shell, the two secondary-menu chrome partials and `mixed-page.eta`
are **multi-line** — every such line break is N3b-erased, so the byte gate is indifferent.

### Workload 1 — `composed-page` — Amended (E20/E22 landed form)

Eta's **native `layout()` mechanism, in BOTH tracks**. `composed-page.eta`:

```eta
<% layout("@shell", it) %>…the slider markup, transcribed from home.heddle…
```

`shell.layout.eta` (registered as `@shell`, ~125 lines): the full literal chrome with
`<%~ it.body %>` at the body-slot position, the section defaults inlined as literal text
(`<title>Title</title>`, the socialmeta tags — eta has no overridable-section mechanism; the
empty `page_scripts`/`endpage_scripts` defaults are simply omitted), the ten chrome-fragment
partials included at their chrome positions (`<%~ include("@alert_top", it) %>`,
`…@secondary_wholesale_menu`, `…@secondary_retail_menu`, `…@alert_below`,
`…@assets_styles`, `…@custom_styles`, `…@head_scripts`, `…@body_scripts`,
`…@assets_scripts`, `…@body_end_scripts` — one literal-text `.partial.eta` per fragment,
mirroring `chrome-fragments.heddle`), and the nav rendered through nested partials:
`<% it.nav.menus.forEach(m => { %><%~ include("@mega_menu", m) %><% }) %>` at the mega-menu
site and `<% it.nav.footer_columns.forEach(c => { %><%~ include("@nav_column", c) %><% }) %>`
in the footer, with `mega_menu → nav_column → nav_section → nav_link` each passing the child
node down through `include`.

### Workload 2 — `trivial-substitution`

*(one line)*:

```eta
<article><h1><%~ it.title %></h1><p class="sku"><%~ it.sku %></p><p class="price"><%~ it.price %></p><p class="brand"><%~ it.brand %></p><p class="cat"><%~ it.category %></p><p class="avail"><%~ it.availability %></p><a class="link" href="<%~ it.url %>"><img src="<%~ it.image_url %>"></a><p class="sum"><%~ it.summary %></p><p class="rating"><%~ it.rating %></p></article>
```

### Workload 3 — `large-loop`

*(one line)*:

```eta
<% it.items.forEach(r => { %><tr><td>row-<%~ r.value %></td><td><%~ r.value %></td></tr><% }) %>
```

(E21 landed form — `row-` is template-composed.)

### Workload 4 — `mixed-page`

Same literal skeleton as the Handlebars text (byte-identical literal HTML), with Eta
constructs *(line structure mirrors the Handlebars/Heddle text; substitution lines shown)*:

```eta
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title><%~ it.page_title %></title>
<style>…same single-line style block, byte-identical…</style>
</head>
<body>
<header>
<h1><%~ it.store_name %></h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
<% if (it.show_banner) { %><div class="banner"><%~ it.banner_text %></div><% } %>
<section class="hero">
<h2><%~ it.hero_heading %></h2>
<p><%~ it.hero_tagline %></p>
</section>
<section class="grid">
<% it.products.forEach(p => { %><article class="card"><h3><%~ p.name %></h3><p class="sku">MX-<%~ p.sku_number %></p><p class="price"><%~ p.price %></p><% if (p.on_sale) { %><p class="sale">On sale</p><% } %><p class="blurb">A dependable workshop staple from batch <%~ p.batch %>, checked for daily use and backed by our lifetime guarantee.</p></article><% }) %>
</section>
<% if (it.show_debug_panel) { %><pre class="debug">debug</pre><% } %>
</main>
<footer>
<p><%~ it.footer_note %></p>
<p><%~ it.store_name %> <%~ it.year %> <%~ it.support_email %></p>
</footer>
</body>
</html>
```

(The `<style>` ellipsis above is presentational in this spec only — the checked-in file
contains the full style block byte-identical to the Heddle/Handlebars texts. Eta's default
`autoTrim` `[false, 'nl']` trims one newline after a tag-closing `%>` at line end; every such
newline here sits between `>` and `<` and is erased by N3 anyway, so the setting cannot affect
the byte gate.)

### Workload 5 — `conditional-heavy`

*(one line)*:

```eta
<ul class="matrix"><% it.rows.forEach(r => { %><li><% if (r.is_bronze) { %><span class="t0">bronze</span><% } else if (r.is_silver) { %><span class="t1">silver</span><% } else if (r.is_gold) { %><span class="t2">gold</span><% } else { %><span class="t3">platinum</span><% } %><em><%~ r.name %></em><% if (r.has_note) { %><small>note <%~ r.seq %></small><% } %><% if (r.is_active) { %><b>active</b><% } %></li><% }) %></ul>
```

### Workload 6 — `fragment-heavy` — Amended (E20 landed form)

`fragment-heavy.eta` *(one line)* — the boolean `if/else if` dispatch chain over the six
partials:

```eta
<div class="panel"><% it.items.forEach(item => { %><% if (item.is_tile) { %><%~ include("@tile", item) %><% } else if (item.is_card) { %><%~ include("@card", item) %><% } else if (item.is_media) { %><%~ include("@media_row", item) %><% } else { %><%~ include("@stat", item) %><% } %><% }) %></div>
```

The six partials (`@tile`, `@card`, `@badge`, `@price`, `@media_row`, `@stat`, one
`.partial.eta` file each); the card nests badge + price against the row's promo
(`include("@badge", it.promo)` / `include("@price", it.promo)`); the tile:

```eta
<section class="tile"><h3><%~ it.name %></h3><p class="v"><%~ it.value %></p><span class="badge"><%~ it.badge %></span></section>
```

(the media caption `Caption for <%~ it.name %>`, image source `/img/<%~ it.name %>.jpg` and
display price `<%~ it.price %>.99` are template-composed — E21.)

### Workload 7 — `fortunes-encoded`

*(one line; `<%= %>` = escaped path)*:

```eta
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr><% it.rows.forEach(r => { %><tr><td><%= r.id %></td><td><%= r.message %></td></tr><% }) %></table></body></html>
```

Eta's `XMLEscape` emits the canonical five spellings, so the output is byte-identical to the
oracle before N5 (N5 is applied uniformly to encoded candidates and is an identity here).

### Workload 8 — `encoded-loop`

*(one line)*:

```eta
<table><% it.items.forEach(r => { %><tr><td data-tag="<%= r.tag %>"><%= r.name %></td><td><%= r.comment %></td></tr><% }) %></table>
```

## Eta — idiomatic track

Files: `benchmarks/js/src/templates/eta/idiomatic/…`. Docs-styled, multi-line, layout system
exercised where the docs prescribe it (README D9). Gate: the functional-equivalence verifier
(whitespace-insensitive). Raw workloads may use `<%= %>` here (docs default): raw model values
contain no `& < > " '` by Phase 1 rule 4, so escaping is a byte no-op — except the
composed-page fragments and area lookups, which are trusted HTML and use `<%~ %>` (the
documented raw-output tag for trusted markup).

- **`composed-page`** — the same native layout shape as controlled (the layout mechanism is
  identical in both tracks — it is eta's one documented layout system): the child declares
  `<% layout("@shell", it) %>` with the slider markup as its body, and the idiomatic
  `shell.layout.eta` carries the same full chrome, chrome-fragment includes and nav partial
  chain, authored docs-style multi-line.
- **`mixed-page`** — **single-file** (workloads.md idiomatic mixed-page rule, added E20:
  layout composition is composed-page's dimension) — the full skeleton lives inline; the
  former `page.layout.eta` split is deleted. Fragment order preserves the verifier's markers
  (`<!DOCTYPE html>` → `<header>` → `class="hero"` → `class="grid"` → `<footer>` →
  `</html>`).
- **All other workloads** — multi-line, indented versions of the controlled logic (same
  constructs: `forEach`, `if`/`else if`, the fragment-heavy dispatch chain over
  `include("@<kind>", item)`, `<%= %>` for the encoded suites). No structural difference
  beyond formatting; the verifier judges.

Each idiomatic file carries the Q1.7 citation header:

```eta
<% /* idiomatic Eta implementation (Phase 4).
      Patterns followed: https://eta.js.org/docs/4.x.x/syntax/cheatsheet,
      https://eta.js.org/docs/4.x.x/syntax/layouts-and-blocks (layout, it.body),
      https://eta.js.org/docs/4.x.x/api/overview (loadTemplate, render). */ %>
```

## Track/engine cell summary

| Workload | HB controlled | HB idiomatic | Eta controlled | Eta idiomatic |
|---|---|---|---|---|
| composed-page | `{{#> layout}}` partial block + 15 partials, runtime compile | same text, precompiled | `layout("@shell", it)` + partials | same native layout, docs-style |
| trivial-substitution | card, triple-stache | same, precompiled | `<%~ %>` card | multi-line `<%= %>` card |
| large-loop | `#each` + `row-{{{value}}}` | same, precompiled | `forEach` + `row-` composition | multi-line `forEach` |
| mixed-page | skeleton + `#if`/`#each` | same, precompiled | skeleton + `if`/`forEach` | single-file (E20 rule) |
| conditional-heavy | chained `{{else if}}`, **no helpers** | same, precompiled | `if`/`else if` chain | multi-line same |
| fragment-heavy | boolean chain over 6 partials | same, precompiled | boolean chain over 6 partials | multi-line same |
| fortunes-encoded | double-stache (N5 in gate) | same, precompiled | `<%= %>` (canonical) | multi-line same |
| encoded-loop | double-stache (N5 in gate) | same, precompiled | `<%= %>` (canonical) | multi-line same |

Every controlled cell must pass the byte gate and every idiomatic cell the verifier before any
timing (README D10); the expected-divergence analysis per cell is: none beyond whitespace
(reconciled by N2/N3/N3b/N4 — since the 2026-07-20 N3b ruling any whitespace-only divergence
passes) and, for Handlebars encoded cells only, the `&#x27;` spelling (N5).
