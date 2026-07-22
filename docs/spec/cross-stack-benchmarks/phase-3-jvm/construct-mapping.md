# Construct mapping — the 8 × 2 × 2 cell matrix, Java models, normative template texts

Supplementary document of the [Phase 3 — jvm spec](README.md). It pins every cell of the
workload × engine × track matrix: the Java model definitions (byte-compatible with the pinned
data of [Phase 1 workloads.md](../phase-1-cross-stack-foundation/workloads.md)), the construct
each engine uses per workload per track, and the normative template texts. Template texts are
normative the same way Phase 1's are: whitespace differences that the contract's N2/N3/N3b/N4
reconcile (line endings, inter-tag whitespace, every whitespace run removed to nothing per N3b,
edge trim) are tolerated between this document and the checked-in files; nothing non-whitespace is. Gate definitions live in the
[parity contract](../phase-1-cross-stack-foundation/parity-contract-v2.md); the Thymeleaf
controlled authoring pattern is normative in the
[feasibility doc](thymeleaf-controlled-feasibility.md).

## The matrix at a glance

Track gates: controlled = byte gate vs corpus entry (+ encoded security floor); idiomatic =
Phase 1 verifier. Engine modes: JTE raw = `ContentType.Plain` engine, JTE encoded =
`ContentType.Html` engine; Thymeleaf = `TemplateMode.HTML` throughout, raw output via
unescaped `[(...)]`/`th:utext`, encoded via escaped `[[...]]`/`th:text`.

| # | Workload | JTE controlled | JTE idiomatic | Thymeleaf controlled | Thymeleaf idiomatic |
|---|---|---|---|---|---|
| 1 | composed-page | `@template.controlled.layout(m)` + map lookups + `@for` over area names (Plain) | same constructs, multi-line (Plain) | `th:replace` layout fragment + `[(...)]` map lookups + `th:block th:each` — feasibility rung 8 | natural layout: `th:replace`, `th:utext` on elements |
| 2 | trivial-substitution | `${...}` × 10 (Plain) | same, multi-line | literal tags + `[(...)]` text; `th:attr` for `href`/`src` — rung 1 | `th:utext`/`th:href`-style attrs on elements |
| 3 | large-loop | `@for` + `${...}` (Plain) | same, multi-line | `th:block th:each` + `[(...)]` — rung 3 | `th:each` on `<tr>`, `th:utext` on `<td>` |
| 4 | mixed-page | `@if` + `@for` + nested `@if` (Plain) | same, multi-line | `th:block th:if`/`th:each` + `[(...)]` — rung 5 | `th:if`/`th:each` on elements, `th:utext` |
| 5 | conditional-heavy | `@if/@elseif/@else` chain + two `@if` (Plain) | same, multi-line | `th:block th:switch`/`th:case`/`*` + `th:block th:if` — rung 4 | `th:switch`/`th:case` on `<span>`s, `th:if` on elements |
| 6 | fragment-heavy | `@template.controlled.tile(item)` × 48 (Plain) | `@template.idiomatic.tile(item)` | `th:block th:replace="~{controlled/tile :: tile(${item})}"` — rung 6 | `th:replace` on a host element |
| 7 | fortunes-encoded | `${...}` under `FiveEntityHtmlOutput` (Html, [D4](README.md#d4--jte-controlled-encoded-suite-renders-through-a-custom-fiveentityhtmloutput)) | `${...}` under stock OWASP output (Html; verifier per [D6](README.md#d6--jte-idiomatic-encoded-cells-need-a-verifier-needle-amendment-erratum-not-local-patch) amendment) | `[[...]]` escaped inlining — rung 2 | `th:text` on `<td>` elements |
| 8 | encoded-loop | `${...}` text + attribute under `FiveEntityHtmlOutput` (Html) | stock OWASP output (Html; D6) | `[[...]]` text + `th:attr` attribute — rung 7 | `th:text` + `th:attr` on elements |

Every construct above is in the common-denominator feature set (scalar substitution, loops,
conditionals, partial/include, HTML escaping) — no engine-exclusive feature is exercised.

## Java models

One file `benchmarks/jvm/src/main/java/heddle/benchmarks/jvm/model/Models.java` hosting
static nested classes and static final instances materialized once (the `LoopContent.Shared`
discipline). All classes are JavaBean-style POJOs (private final fields + getters), because
Thymeleaf's standalone OGNL expression language resolves `${p.name}` through
`getName()`/`isName()` and JTE templates call the same getters explicitly. All string assembly
uses `String.format(Locale.ROOT, …)` or plain concatenation of ASCII/int values — no locale,
time, or randomness anywhere, so model bytes are deterministic and identical to the C#
generators' output.

| Class | Fields (getter-implied) | Pinned data (must match Phase 1 byte-for-byte) |
|---|---|---|
| `SubstitutionModel` | `title, sku, price (int), brand, category, availability, url, imageUrl, summary, rating` | `"Heddle Handbook"`, `"HB-2001"`, `4200`, `"Heddle Press"`, `"Reference"`, `"In stock"`, `"/catalog/handbook"`, `"/img/handbook.png"`, `"A concise field guide to the engine."`, `"4.8"` |
| `LoopRow` | `name, value (int)` | 5,000 rows, `i` in `[0,4999]`: `"row-" + i`, `i` |
| `MixedModel` | `pageTitle, storeName, heroHeading, heroTagline, showBanner (boolean), bannerText, showDebugPanel (boolean), footerNote, year (int), supportEmail, products (List<MixedProduct>)` | exactly the Phase 1 values: `"Mercantile - Catalog"`, `"Mercantile"`, `"Autumn hardware sale"`, `"Hand-picked tools, fair prices, shipped tomorrow."`, `true`, `"Free shipping on orders over 60."`, `false`, `"Prices include VAT where applicable."`, `2026`, `"support at mercantile.example"` |
| `MixedProduct` | `name, sku, price (int), onSale (boolean), blurb` | 36 rows, `i` in `[1,36]`: `String.format(Locale.ROOT, "Product %02d", i)`, `"MX-" + (1000 + i)`, `950 + i * 7`, `i % 3 == 0`, `"A dependable workshop staple from batch " + i + ", checked for daily use and backed by our lifetime guarantee."` |
| `ConditionalRow` | `name, note, bronze, silver, gold, hasNote, active (booleans)` | 200 rows, `i` in `[0,199]`: `String.format(Locale.ROOT, "unit-%03d", i)`, `"note " + i`, `i % 4 == 0`, `i % 4 == 1`, `i % 4 == 2`, `i % 2 == 0`, `i % 5 != 0` |
| `FragmentRow` | `name, value (int), badge` | 48 rows, `i` in `[0,47]`: `String.format(Locale.ROOT, "tile-%02d", i)`, `i * 11`, `new String[]{"new","hot","sale","std"}[i % 4]` |
| `FortuneRow` | `id (int), message` | the 12 pinned rows of [workloads.md workload 7](../phase-1-cross-stack-foundation/workloads.md#workload-7--fortunes-encoded-encoded), transcribed byte-for-byte (row 1 `4.33e67` — no `+`; rows 4/8 em dash U+2014; row 11 the exact XSS payload; row 12 `フレームワークのベンチマーク`); source files saved UTF-8 without BOM |
| `EncodedLoopRow` | `tag, name, comment` | 5,000 rows, `i` in `[0,4999]`: `"tag-" + i + "&'" + (i % 7) + "'"`, `"item <" + i + "> & \"co\""`, `"'q' & <angle> \"d\" こんにちは " + i` |
| `ComposedModel` | `sections (Map<String,String>), comps (Map<String,String>), areas (Map<String,String>), areaNames (List<String>)` | loaded once from the fragment resource files below; key sets and `areaNames` order mirror `TwinContent.Sections()`/`Components()`/`Areas`/`AreaOrder` exactly |

**Composed-page fragment resources.** The multi-KB section/component/area fragments are model
data in the intra-.NET twins (from
[`TwinContent.cs`](../../../../src/Heddle.Performance/Runners/TwinContent.cs) /
`AreaComponent.Areas`), never template literals. The JVM harness stores each fragment as one
UTF-8 (no BOM) resource file under `benchmarks/jvm/src/main/resources/composed-page/`
(`section.<key>.txt`, `comp.<key>.txt`, `area.<index>.<slug>.txt`, plus `area-order.txt`, one
name per line), transcribed byte-for-byte from the C# sources at implementation time.
Transcription errors cannot slip through: the controlled byte gate compares the composed
output against the corpus entry before any timing, so a single wrong byte fails loudly
(the same self-verification Phase 1 relies on). Both engines' composed-page models read these
same files — the fragments exist exactly once in the harness.

## JTE templates

Source trees (per [D3](README.md#d3--jte-controlled-track-whitespace-free-authoring-trimcontrolstructures--false-generate-goal-aot)):
`src/main/jte-plain/{controlled,idiomatic}/` (ContentType `Plain`, package
`heddle.jte.gen.plain`) and `src/main/jte-html/{controlled,idiomatic}/` (ContentType `Html`,
package `heddle.jte.gen.html`). Render names are path-based: `controlled/mixed-page.jte` etc.
Controlled templates are whitespace-free (control structures inline, single-line bodies);
idiomatic templates are multi-line with a header comment citing jte.gg doc pages (Q1.7/D7).
`M` below abbreviates `heddle.benchmarks.jvm.model.Models`.

### Controlled — raw suite (`jte-plain/controlled/`)

`trivial-substitution.jte`:

```jte
@param heddle.benchmarks.jvm.model.Models.SubstitutionModel m
<article><h1>${m.getTitle()}</h1><p class="sku">${m.getSku()}</p><p class="price">${m.getPrice()}</p><p class="brand">${m.getBrand()}</p><p class="cat">${m.getCategory()}</p><p class="avail">${m.getAvailability()}</p><a class="link" href="${m.getUrl()}"><img src="${m.getImageUrl()}"></a><p class="sum">${m.getSummary()}</p><p class="rating">${m.getRating()}</p></article>
```

`large-loop.jte`:

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.LoopRow> items
@for(var r : items)<tr><td>${r.getName()}</td><td>${r.getValue()}</td></tr>@endfor
```

`conditional-heavy.jte`:

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.ConditionalRow> rows
<ul class="matrix">@for(var r : rows)<li>@if(r.isBronze())<span class="t0">bronze</span>@elseif(r.isSilver())<span class="t1">silver</span>@elseif(r.isGold())<span class="t2">gold</span>@else<span class="t3">platinum</span>@endif<em>${r.getName()}</em>@if(r.isHasNote())<small>${r.getNote()}</small>@endif@if(r.isActive())<b>active</b>@endif</li>@endfor</ul>
```

`mixed-page.jte` — the full HTML skeleton of
[workloads.md workload 4](../phase-1-cross-stack-foundation/workloads.md#workload-4--mixed-page-raw)
transcribed literally (including the one-line `<style>` block), with these dynamic sites:

```jte
@param heddle.benchmarks.jvm.model.Models.MixedModel m
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>${m.getPageTitle()}</title>
<style>…the pinned CSS line, verbatim…</style>
</head>
<body>
<header>
<h1>${m.getStoreName()}</h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
@if(m.isShowBanner())<div class="banner">${m.getBannerText()}</div>@endif
<section class="hero">
<h2>${m.getHeroHeading()}</h2>
<p>${m.getHeroTagline()}</p>
</section>
<section class="grid">
@for(var p : m.getProducts())<article class="card"><h3>${p.getName()}</h3><p class="sku">${p.getSku()}</p><p class="price">${p.getPrice()}</p>@if(p.isOnSale())<p class="sale">On sale</p>@endif<p class="blurb">${p.getBlurb()}</p></article>@endfor
</section>
@if(m.isShowDebugPanel())<pre class="debug">debug</pre>@endif
</main>
<footer>
<p>${m.getFooterNote()}</p>
<p>${m.getStoreName()} ${m.getYear()} ${m.getSupportEmail()}</p>
</footer>
</body>
</html>
```

`tile.jte` (the partial) and `fragment-heavy.jte`:

```jte
@param heddle.benchmarks.jvm.model.Models.FragmentRow row
<section class="tile"><h3>${row.getName()}</h3><p class="v">${row.getValue()}</p><span class="badge">${row.getBadge()}</span></section>
```

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<div class="panel">@for(var item : items)@template.controlled.tile(item)@endfor</div>
```

`composed-page.jte` + `layout.jte` — mirror the intra-.NET twin construct set (layout call,
`sections`/`comps` map lookups, `areas` lookups driven by one `@for` over `areaNames`):
`composed-page.jte` is `@template.controlled.layout(m)`; `layout.jte` takes the
`ComposedModel` and emits the twin fragment sequence with
`${m.getSections().get("meta")}`-style lookups and
`@for(var name : m.getAreaNames())${m.getAreas().get(name)}@endfor` at the area sites, in the
exact order the golden dictates. The corpus entry is the byte target; the checked-in template
is complete when the gate passes.

### Controlled — encoded suite (`jte-html/controlled/`)

Rendered through `FiveEntityHtmlOutput` (D4).

`fortunes-encoded.jte`:

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.FortuneRow> rows
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>@for(var r : rows)<tr><td>${r.getId()}</td><td>${r.getMessage()}</td></tr>@endfor</table></body></html>
```

`encoded-loop.jte`:

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.EncodedLoopRow> items
<table>@for(var item : items)<tr><td data-tag="${item.getTag()}">${item.getName()}</td><td>${item.getComment()}</td></tr>@endfor</table>
```

### Idiomatic (`jte-plain/idiomatic/`, `jte-html/idiomatic/`)

Same constructs and the same model access, authored the way jte's documentation writes
templates — multi-line, indented, control structures on their own lines (output-layout freedom
is what the verifier-not-byte gate buys) — rendered through the **stock** engine
(`StringOutput`; the Html engine wraps it in `OwaspHtmlTemplateOutput` itself). Header comment
per file cites: `https://jte.gg/syntax/` (all), `https://jte.gg/html-rendering/` (encoded),
`https://jte.gg/pre-compiling/` (engine setup). The encoded idiomatic gates run against the
amended verifier needles now settled in
[Phase 1 golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md#idiomatic-verifier-definitions)
(rationale in [D6](README.md#d6--jte-idiomatic-encoded-cells-need-a-verifier-needle-amendment-erratum-not-local-patch)).

## Thymeleaf templates

Resource tree: `src/main/resources/thymeleaf/{controlled,idiomatic}/*.html`; template names
`controlled/mixed-page` etc. (resolver prefix `thymeleaf/`, suffix `.html`,
`TemplateMode.HTML` — [D2](README.md#d2--thymeleaf-engine-setup-standalone-315release-classloader-resolver-cached-templates-reused-context)).
Controlled templates follow the
[block-only pattern](thymeleaf-controlled-feasibility.md#the-mechanism-set-all-core-standard-dialect-all-cited)
strictly; `th:*` attributes on output tags appear **only** where an attribute value is
substituted (`th:attr` — trivial-substitution's `href`/`src`, encoded-loop's `data-tag`),
because inlining operates in tag bodies, not attribute values. Context variables are set once
per workload: `m` (page models), `rows`/`items` (lists), plus `sections`/`comps`/`areas`/`areaNames`
for composed-page.

### Controlled — raw suite

`trivial-substitution.html` (feasibility rung 1 — also exercises the two-attribute `<a>` tag,
divergence class B1/B2):

```html
<article><h1>[(${m.title})]</h1><p class="sku">[(${m.sku})]</p><p class="price">[(${m.price})]</p><p class="brand">[(${m.brand})]</p><p class="cat">[(${m.category})]</p><p class="avail">[(${m.availability})]</p><a class="link" th:attr="href=${m.url}"><img th:attr="src=${m.imageUrl}"></a><p class="sum">[(${m.summary})]</p><p class="rating">[(${m.rating})]</p></article>
```

(Raw-suite model values contain no `& < > " '` by the Phase 1 authoring rule, so `th:attr`'s
escaping is a byte-level no-op here; the raw *text* path stays the unescaped `[(...)]`.)

`large-loop.html` (rung 3):

```html
<th:block th:each="r : ${items}"><tr><td>[(${r.name})]</td><td>[(${r.value})]</td></tr></th:block>
```

`conditional-heavy.html` (rung 4):

```html
<ul class="matrix"><th:block th:each="r : ${rows}"><li><th:block th:switch="${true}"><th:block th:case="${r.bronze}"><span class="t0">bronze</span></th:block><th:block th:case="${r.silver}"><span class="t1">silver</span></th:block><th:block th:case="${r.gold}"><span class="t2">gold</span></th:block><th:block th:case="*"><span class="t3">platinum</span></th:block></th:block><em>[(${r.name})]</em><th:block th:if="${r.hasNote}"><small>[(${r.note})]</small></th:block><th:block th:if="${r.active}"><b>active</b></th:block></li></th:block></ul>
```

(`th:case` values evaluate in order, first true wins, `*` is the default — a genuine 1–4
evaluation chain; the tier booleans are mutually exclusive by construction.)

`mixed-page.html` (rung 5) — the literal skeleton of workload 4 with:
`[(${m.pageTitle})]` etc. at the nine scalar sites;
`<th:block th:if="${m.showBanner}"><div class="banner">[(${m.bannerText})]</div></th:block>`;
`<th:block th:each="p : ${m.products}"><article class="card"><h3>[(${p.name})]</h3><p class="sku">[(${p.sku})]</p><p class="price">[(${p.price})]</p><th:block th:if="${p.onSale}"><p class="sale">On sale</p></th:block><p class="blurb">[(${p.blurb})]</p></article></th:block>`;
`<th:block th:if="${m.showDebugPanel}"><pre class="debug">debug</pre></th:block>`.

`tile.html` + `fragment-heavy.html` (rung 6):

```html
<th:block th:fragment="tile(item)"><section class="tile"><h3>[(${item.name})]</h3><p class="v">[(${item.value})]</p><span class="badge">[(${item.badge})]</span></section></th:block>
```

```html
<div class="panel"><th:block th:each="item : ${items}"><th:block th:replace="~{controlled/tile :: tile(${item})}"/></th:block></div>
```

`composed-page.html` + `layout.html` (rung 8 — highest exposure, divergence class B4):
`composed-page.html` is `<th:block th:replace="~{controlled/layout :: layout}"/>`;
`layout.html` wraps the twin fragment sequence in `<th:block th:fragment="layout">…</th:block>`
with `[(${sections.meta})]`-style lookups and
`<th:block th:each="name : ${areaNames}">[(${areas[name]})]</th:block>` at the area sites, in
golden order.

### Controlled — encoded suite

`fortunes-encoded.html` (rung 2):

```html
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr><th:block th:each="r : ${rows}"><tr><td>[[${r.id}]]</td><td>[[${r.message}]]</td></tr></th:block></table></body></html>
```

`encoded-loop.html` (rung 7 — the sanctioned `th:attr` exception, divergence class B1):

```html
<table><th:block th:each="item : ${items}"><tr><td th:attr="data-tag=${item.tag}">[[${item.name}]]</td><td>[[${item.comment}]]</td></tr></th:block></table>
```

### Idiomatic

Natural templates, the way the tutorial writes them — multi-line indented HTML, `xmlns:th` on
the root element where the template is a full document, processors on the output elements
themselves, prototype body text inside `th:text`/`th:utext` elements. Representative shapes
(full files follow the same idiom; header comment per file cites the tutorial sections used —
§§3, 5, 6, 7, 8 and 12 of usingthymeleaf.html — per Q1.7/D7):

- substitution: `<td th:utext="${r.name}">row name</td>` (raw) /
  `<td th:text="${r.message}">message</td>` (encoded);
- iteration: `<tr th:each="r : ${rows}">…</tr>`;
- condition: `<p class="sale" th:if="${p.onSale}">On sale</p>`;
- tier chain: `th:switch`/`th:case` on the `<span>` elements inside a `<li th:each=…>`;
- partial: `<div th:replace="~{idiomatic/tile :: tile(${item})}"></div>`;
- attribute: `<td th:attr="data-tag=${item.tag}" th:text="${item.name}">item</td>`.

The idiomatic gate is the verifier alone; output layout, extra whitespace, and the `xmlns:th`
attribute on the root (which Thymeleaf leaves in output when authored — acceptable, since no
verifier needle or marker matches against it) are all free.

## Cross-checks this document is bound by

- Every controlled template's dynamic sites correspond one-to-one with the Heddle normative
  template's `@(…)`/`@if`/`@list`/call sites in
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md) — same substitution count,
  same branch structure, same partial-call count (the "semantically equivalent,
  equivalently-authored" controlled standard).
- Encoded templates put untrusted data only in HTML **text context** and **double-quoted
  attribute-value context** — exactly the two contexts the corpus confines untrusted data to;
  no `script`/event-handler context exists in any template (the `FiveEntityHtmlOutput`
  tripwire and the Thymeleaf mechanism set both enforce this).
- Fortunes-encoded expected output: escaped payload exactly once, raw `<script>alert(` zero
  times, `フレームワークのベンチマーク` intact — asserted by the security floor and the
  verifier, per contract.
