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
N1–N4 (+N5 encoded); controlled templates below are single-line — line breaks inside the fenced
blocks that are marked *(wrapped for readability)* are **not** part of the template text.

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
   (`Product ${String(i).padStart(2, "0")}`, `MX-${1000 + i}`, `950 + i * 7`, `i % 3 === 0`,
   the pinned blurb sentence with `${i}`), and likewise for conditional-heavy (200 rows,
   `unit-${String(i).padStart(3, "0")}`, `i % 4` tier booleans, `i % 2 === 0`, `i % 5 !== 0`),
   fragment-heavy (48 rows, `tile-${String(i).padStart(2, "0")}`, `i * 11`,
   `["new","hot","sale","std"][i % 4]`), large-loop (5,000 rows, `row-${i}`, `i`), and
   encoded-loop (5,000 rows, `` `tag-${i}&'${i % 7}'` ``, `` `item <${i}> & "co"` ``,
   `` `'q' & <angle> "d" こんにちは ${i}` ``).
4. **Pinned literal sets are transcribed verbatim:** the 12 fortunes rows (ids 1–12, messages
   byte-for-byte from workloads.md, including the XSS payload row 11 and Japanese row 12); the
   trivial-substitution scalar values (from
   [SubstitutionContent.cs](../../../../src/Heddle.Performance/Runners/SubstitutionContent.cs)
   — `Heddle Handbook`, `HB-2001`, `4.8`, etc.); the mixed-page page scalars (workloads.md).
5. **Composed-page** transcribes the fragment literals from
   [TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs) and
   `AreaComponent.Areas` into:

   ```js
   export const model = deepFreeze({
     section: { meta, social, page_scripts, endpage_scripts },   // SectionMeta, SectionSocial, SectionPageScripts (""), SectionEndPageScripts ("")
     comp: { assets_styles, custom_styles, head_scripts, body_scripts,
             assets_scripts, body_end_scripts },                 // the six Comp* consts
     area_names: [...TwinContent.AreaOrder],                     // same strings, same order
     areas: { ... }                                              // AreaComponent.Areas entries, keys verbatim
   });
   ```

   The multi-KB area fragments are transcribed exactly (copy from the C# source, unescaping C#
   string syntax only). *(Verify at implementation: transcription exactness is proven by the
   byte gate — a single wrong byte fails `npm run gate` with a first-diff excerpt.)*
6. **Fortunes/encoded-loop values must satisfy the untrusted-data alphabet** by construction —
   they are the Phase 1 pinned values, which already do; the transcription adds or removes
   nothing.

## Registered helpers — the full disclosure surface

The complete registered-helper surface of the JS suite, both tracks, is **one helper**:

**`area` (composed-page only).** JS implementation (in `src/engines/handlebars.mjs`):

```js
hb.registerHelper("area", function (name) {
  return new Handlebars.SafeString(
    Object.prototype.hasOwnProperty.call(model.areas, name) ? model.areas[name] : ""
  );
});
```

**.NET twin equivalence.** This mirrors the Handlebars.Net twin's helper byte-for-byte in
behavior ([HandlebarsTest.cs](../../../../src/Heddle.Performance/Runners/HandlebarsTest.cs)):
the .NET helper does `output.WriteSafeString(TwinContent.Areas.TryGetValue(name, out var v) ? v : string.Empty)`
— a name→fragment lookup emitting the fragment unescaped, empty string on a miss. Same lookup
table (transcribed `AreaComponent.Areas`), same miss behavior, same safe-string (no-escape)
emission; `SafeString` is Handlebars-JS's documented equivalent of `WriteSafeString`. The
computation is semantically identical, which is what the Are-We-Fast-Yet disclosed-idiom rule
requires.

**Report disclosure text (verbatim — D5/D15 require it in the published report):**

> *Registered helpers (Handlebars, both tracks): the composed-page workload registers one
> helper, `area(name)`, which looks up a prebuilt HTML fragment by name and returns it as a
> `SafeString` — the same helper surface as the intra-.NET Handlebars.Net twin. No other
> workload registers any helper. In particular, conditional-heavy uses only the built-in
> `{{#if}}` / `{{else if}}` chain over precomputed boolean model fields; no comparison or
> equality helpers are registered anywhere in this suite.*

No Eta workload registers helper-like extensions; Eta's `include`/`layout` are built-in engine
functions, not user extensions, and are named as such in the report's methodology note.

## Handlebars — controlled track

Files: `benchmarks/js/src/templates/handlebars/controlled/<id>.hbs` (+ two partial files).
Compile mode: runtime `hb.compile` once at startup (README D7). Raw workloads: triple-mustache
throughout; encoded workloads: double-mustache, stock escaper, N5 in the gate (README D4).
Texts 1–3 are the intra-.NET twin constants verbatim; 4–8 are the workloads.md Handlebars
columns verbatim.

### Workload 1 — `composed-page`

Entry template (`composed-page.hbs`):

```handlebars
{{> layout }}
```

Registered partial `layout` (`layout.partial.hbs`) *(wrapped for readability — one line, no
newlines)*:

```handlebars
{{{section.meta}}}{{{section.social}}}{{{comp.assets_styles}}}{{{comp.custom_styles}}}{{{comp.head_scripts}}}{{{comp.body_scripts}}}{{#each area_names}}{{area this}}{{/each}}{{{comp.assets_scripts}}}{{{section.page_scripts}}}{{{section.endpage_scripts}}}{{{comp.body_end_scripts}}}
```

Environment: `Handlebars.create()`, `registerPartial("layout", …)`, the `area` helper above —
the JS mirror of `HandlebarsTest.CreateEnvironment()` (which uses `RegisterTemplate`,
Handlebars.Net's partial-registration API). Note `{{area this}}` receives the current
`area_names` element — in Handlebars-JS, `this` inside `#each` is the current item, matching
Handlebars.Net's behavior in the twin.

### Workload 2 — `trivial-substitution`

`trivial-substitution.hbs` *(one line)*:

```handlebars
<article><h1>{{{title}}}</h1><p class="sku">{{{sku}}}</p><p class="price">{{{price}}}</p><p class="brand">{{{brand}}}</p><p class="cat">{{{category}}}</p><p class="avail">{{{availability}}}</p><a class="link" href="{{{url}}}"><img src="{{{image_url}}}"></a><p class="sum">{{{summary}}}</p><p class="rating">{{{rating}}}</p></article>
```

### Workload 3 — `large-loop`

`large-loop.hbs` *(one line)*:

```handlebars
{{#each items}}<tr><td>{{{name}}}</td><td>{{value}}</td></tr>{{/each}}
```

(`{{value}}` double-mustache carried from the .NET twin verbatim: `value` is an integer whose
string form contains no character in `escapeExpression`'s table, so the escaper is a
byte-level no-op here — raw-suite discipline is preserved.)

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
{{#each products}}<article class="card"><h3>{{{name}}}</h3><p class="sku">{{{sku}}}</p><p class="price">{{{price}}}</p>{{#if on_sale}}<p class="sale">On sale</p>{{/if}}<p class="blurb">{{{blurb}}}</p></article>{{/each}}
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
<ul class="matrix">{{#each rows}}<li>{{#if is_bronze}}<span class="t0">bronze</span>{{else if is_silver}}<span class="t1">silver</span>{{else if is_gold}}<span class="t2">gold</span>{{else}}<span class="t3">platinum</span>{{/if}}<em>{{{name}}}</em>{{#if has_note}}<small>{{{note}}}</small>{{/if}}{{#if is_active}}<b>active</b>{{/if}}</li>{{/each}}</ul>
```

### Workload 6 — `fragment-heavy`

`fragment-heavy.hbs` *(one line)*:

```handlebars
<div class="panel">{{#each items}}{{> tile this}}{{/each}}</div>
```

Registered partial `tile` (`tile.partial.hbs`, one line):

```handlebars
<section class="tile"><h3>{{{name}}}</h3><p class="v">{{{value}}}</p><span class="badge">{{{badge}}}</span></section>
```

(`{{> tile this}}` — partial with explicit context, documented "partial contexts" syntax.)

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

Files: `benchmarks/js/src/templates/handlebars/idiomatic/<id>.hbs` (+ the two partials).
Texts: identical to the controlled texts above (README D9 — the controlled twins already are
the documented patterns), with `mixed-page.hbs` and `fortunes-encoded.hbs` kept in the same
multi-line/single-line form as controlled (no gratuitous reformatting — one less diff surface).
Execution mode is the differentiator: `Handlebars.precompile(src, { knownHelpersOnly: true,
knownHelpers: { area: true } })` (the `knownHelpers` entry only in the composed-page
environment; other templates precompile with `knownHelpersOnly: true` and no extra entries) +
`Handlebars.template(eval("(" + spec + ")"))` once at startup (README D7).

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

Files: `benchmarks/js/src/templates/eta/controlled/<id>.eta` (+ `tile.partial.eta`,
`layout.partial.eta`). Instance: `new Eta()` defaults; templates registered as `@<id>` via
`loadTemplate`; render call `eta.render("@<id>", model)` (README D8). Raw workloads use
`<%~ %>`; encoded use `<%= %>`. All controlled texts are **single physical lines** with no
trailing newline *(wrapped for readability below)*.

### Workload 1 — `composed-page`

`composed-page.eta`:

```eta
<%~ include("@layout", it) %>
```

`layout.partial.eta` (registered as `@layout`) *(one line)*:

```eta
<%~ it.section.meta %><%~ it.section.social %><%~ it.comp.assets_styles %><%~ it.comp.custom_styles %><%~ it.comp.head_scripts %><%~ it.comp.body_scripts %><% it.area_names.forEach(n => { %><%~ it.areas[n] !== undefined ? it.areas[n] : "" %><% }) %><%~ it.comp.assets_scripts %><%~ it.section.page_scripts %><%~ it.section.endpage_scripts %><%~ it.comp.body_end_scripts %>
```

(Equivalently authored: entry template delegates to a composition unit, fragments emitted raw
in the same order as the Heddle `@<<` layout and the Handlebars twin; the area lookup with
empty-string miss mirrors the `area` helper.)

### Workload 2 — `trivial-substitution`

*(one line)*:

```eta
<article><h1><%~ it.title %></h1><p class="sku"><%~ it.sku %></p><p class="price"><%~ it.price %></p><p class="brand"><%~ it.brand %></p><p class="cat"><%~ it.category %></p><p class="avail"><%~ it.availability %></p><a class="link" href="<%~ it.url %>"><img src="<%~ it.image_url %>"></a><p class="sum"><%~ it.summary %></p><p class="rating"><%~ it.rating %></p></article>
```

### Workload 3 — `large-loop`

*(one line)*:

```eta
<% it.items.forEach(r => { %><tr><td><%~ r.name %></td><td><%~ r.value %></td></tr><% }) %>
```

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
<% it.products.forEach(p => { %><article class="card"><h3><%~ p.name %></h3><p class="sku"><%~ p.sku %></p><p class="price"><%~ p.price %></p><% if (p.on_sale) { %><p class="sale">On sale</p><% } %><p class="blurb"><%~ p.blurb %></p></article><% }) %>
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
<ul class="matrix"><% it.rows.forEach(r => { %><li><% if (r.is_bronze) { %><span class="t0">bronze</span><% } else if (r.is_silver) { %><span class="t1">silver</span><% } else if (r.is_gold) { %><span class="t2">gold</span><% } else { %><span class="t3">platinum</span><% } %><em><%~ r.name %></em><% if (r.has_note) { %><small><%~ r.note %></small><% } %><% if (r.is_active) { %><b>active</b><% } %></li><% }) %></ul>
```

### Workload 6 — `fragment-heavy`

`fragment-heavy.eta` *(one line)*:

```eta
<div class="panel"><% it.items.forEach(item => { %><%~ include("@tile", item) %><% }) %></div>
```

`tile.partial.eta` (registered as `@tile`, one line):

```eta
<section class="tile"><h3><%~ it.name %></h3><p class="v"><%~ it.value %></p><span class="badge"><%~ it.badge %></span></section>
```

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

- **`composed-page`** — native layout: child template declares the layout and renders the area
  sequence as its body; the shell places the body between the head-side and tail-side
  fragments (same fragment order as the oracle):

  `composed-page.eta`:

  ```eta
  <% layout("@shell", it) %>
  <% it.area_names.forEach(n => { %><%~ it.areas[n] !== undefined ? it.areas[n] : "" %><% }) %>
  ```

  `shell.layout.eta` (registered as `@shell`):

  ```eta
  <%~ it.section.meta %><%~ it.section.social %>
  <%~ it.comp.assets_styles %><%~ it.comp.custom_styles %><%~ it.comp.head_scripts %><%~ it.comp.body_scripts %>
  <%~ it.body %>
  <%~ it.comp.assets_scripts %><%~ it.section.page_scripts %><%~ it.section.endpage_scripts %><%~ it.comp.body_end_scripts %>
  ```

- **`mixed-page`** — native layout: `page.layout.eta` (`@page`) holds the skeleton
  (doctype/head/style/header/footer) with `<%~ it.body %>` inside `<main>`; the child declares
  `<% layout("@page", it) %>` and renders banner/hero/grid/debug as its body. Fragment order
  preserves the verifier's markers (`<!DOCTYPE html>` → `<header>` → `class="hero"` →
  `class="grid"` → `<footer>` → `</html>`).
- **All other workloads** — multi-line, indented versions of the controlled logic (same
  constructs: `forEach`, `if`/`else if`, `include("@tile", item)` for fragment-heavy, `<%= %>`
  for the encoded suites). No structural difference beyond formatting; the verifier judges.

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
| composed-page | `{{> layout}}` + `area` helper, runtime compile | same text, precompiled (`knownHelpers: area`) | `include("@layout", it)` | `layout("@shell", it)` + body = areas |
| trivial-substitution | card, triple-stache | same, precompiled | `<%~ %>` card | multi-line `<%= %>` card |
| large-loop | `#each` row | same, precompiled | `forEach` row | multi-line `forEach` |
| mixed-page | skeleton + `#if`/`#each` | same, precompiled | skeleton + `if`/`forEach` | `layout("@page", it)` |
| conditional-heavy | chained `{{else if}}`, **no helpers** | same, precompiled | `if`/`else if` chain | multi-line same |
| fragment-heavy | `{{> tile this}}` ×48 | same, precompiled | `include("@tile", item)` ×48 | multi-line same |
| fortunes-encoded | double-stache (N5 in gate) | same, precompiled | `<%= %>` (canonical) | multi-line same |
| encoded-loop | double-stache (N5 in gate) | same, precompiled | `<%= %>` (canonical) | multi-line same |

Every controlled cell must pass the byte gate and every idiomatic cell the verifier before any
timing (README D10); the expected-divergence analysis per cell is: none beyond whitespace
(reconciled by N2/N3/N3b/N4 — since the 2026-07-20 N3b ruling any whitespace-only divergence
passes) and, for Handlebars encoded cells only, the `&#x27;` spelling (N5).
