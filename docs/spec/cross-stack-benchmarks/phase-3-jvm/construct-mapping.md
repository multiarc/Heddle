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
| 1 | composed-page | `@template.controlled.shared.layout(m, bodySlot = @`…`)` — layout takes `gg.jte.Content bodySlot`, renders `${bodySlot}` at the slot; nav via `@for` + megamenu/navcolumn/navsection/navlink sub-templates; chrome as per-fragment sub-templates (Plain; E20/E22) | same constructs, `@template.idiomatic.shared.*`, multi-line (Plain) | parameterized fragment `layout(~{:: slider-body})` + `th:replace="${bodySlot}"` slot + chrome-fragment library + nested nav fragments — feasibility rung 8 re-run 2026-08-08, byte gate green (B10/B2b recorded) | natural layout: parameterized fragment + `th:each`/`th:replace` on elements |
| 2 | trivial-substitution | `${...}` × 10 (Plain) | same, multi-line | literal tags + `[(...)]` text; `th:attr` for `href`/`src` — rung 1 | `th:utext`/`th:href`-style attrs on elements |
| 3 | large-loop | `@for` + `${...}` (Plain) | same, multi-line | `th:block th:each` + `[(...)]` — rung 3 | `th:each` on `<tr>`, `th:utext` on `<td>` |
| 4 | mixed-page | `@if` + `@for` + nested `@if` (Plain) | same, multi-line | `th:block th:if`/`th:each` + `[(...)]` — rung 5 | `th:if`/`th:each` on elements, `th:utext` |
| 5 | conditional-heavy | `@if/@elseif/@else` chain + two `@if` (Plain) | same, multi-line | `th:block th:switch`/`th:case`/`*` + `th:block th:if` — rung 4 | `th:switch`/`th:case` on `<span>`s, `th:if` on elements |
| 6 | fragment-heavy | `@if(item.isTile())…@elseif…@else` boolean chain dispatching `@template.controlled.shared.{tile,card,media_row→mediarow,stat}(item)` over six sub-templates; card nests badge + price against `getPromo()` (Plain; E20) | same chain, `@template.idiomatic.shared.*`, multi-line | `th:switch="${true}"` over the boolean `th:case`s, each arm `th:replace`-ing its kind fragment — rung 6 (E20) | `th:switch` on `${item.kind}` with string `th:case`s (the documented native switch — workloads.md idiomatic allowance) |
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
| `LoopRow` | `value (int)` | 5,000 rows, `i` in `[0,4999]`: `i` — the row carries ONLY the ordinal; the display name `row-<i>` is composed by the templates as `row-` + the value substitution ([E21](../../records.md#cross-spec-amendments-ledger)) |
| `MixedModel` | `pageTitle, storeName, heroHeading, heroTagline, showBanner (boolean), bannerText, showDebugPanel (boolean), footerNote, year (int), supportEmail, products (List<MixedProduct>)` | exactly the Phase 1 values: `"Mercantile - Catalog"`, `"Mercantile"`, `"Autumn hardware sale"`, `"Hand-picked tools, fair prices, shipped tomorrow."`, `true`, `"Free shipping on orders over 60."`, `false`, `"Prices include VAT where applicable."`, `2026`, `"support at mercantile.example"` |
| `MixedProduct` | `name, skuNumber (int), price (int), onSale (boolean), batch (int)` | 36 rows, `i` in `[1,36]`: `String.format(Locale.ROOT, "Product %02d", i)`, `1000 + i`, `950 + i * 7`, `i % 3 == 0`, `i` — the templates compose the display SKU (`MX-` + `skuNumber`) and the blurb sentence around `batch` ([E21](../../records.md#cross-spec-amendments-ledger)) |
| `ConditionalRow` | `name, seq (int), bronze, silver, gold, hasNote, active (booleans)` | 200 rows, `i` in `[0,199]`: `String.format(Locale.ROOT, "unit-%03d", i)`, `i`, `i % 4 == 0`, `i % 4 == 1`, `i % 4 == 2`, `i % 2 == 0`, `i % 5 != 0` — the templates compose the note text (`note ` + `seq`) ([E21](../../records.md#cross-spec-amendments-ledger)); the zero-padded identity name stays model-side by design |
| `FragmentRow` | `kind, tile/card/media/stat (booleans, getters `isTile()` …), name, value (int), badge, delta (int), promo (FragmentPromo)` | 48 rows, `i` in `[0,47]` ([E20](../../records.md#cross-spec-amendments-ledger) redesign): `kind = {"tile","card","media","stat"}[i % 4]` (informational — engines dispatch on the precomputed booleans, never the string), `String.format(Locale.ROOT, "item-%02d", i)`, `i * 11`, `{"new","hot","sale","std"}[i % 4]`, `i % 7 - 3`, promo on EVERY row (no null guard). Derived display text (media caption `Caption for ` + name, image src `/img/` + name + `.jpg`, display price + `.99`) is composed by the TEMPLATES (E21) |
| `FragmentPromo` | `label, price (int)` | `label == badge`, `price = 9 + i` — whole-currency int; the templates compose the display price |
| `FortuneRow` | `id (int), message` | the 12 pinned rows of [workloads.md workload 7](../phase-1-cross-stack-foundation/workloads.md#workload-7--fortunes-encoded-encoded), transcribed byte-for-byte (row 1 `4.33e67` — no `+`; rows 4/8 em dash U+2014; row 11 the exact XSS payload; row 12 `フレームワークのベンチマーク`); source files saved UTF-8 without BOM |
| `EncodedLoopRow` | `tag, name, comment` | 5,000 rows, `i` in `[0,4999]`: `"tag-" + i + "&'" + (i % 7) + "'"`, `"item <" + i + "> & \"co\""`, `"'q' & <angle> \"d\" こんにちは " + i` |
| `ComposedModel` | `nav (NavModel)` — and NOTHING else ([E20](../../records.md#cross-spec-amendments-ledger) structured nav; [E22](../../records.md#cross-spec-amendments-ledger) removed the text half) | loaded once at static init (lazy holder) from `GoldenCorpus/fixtures/composed-page/nav.json` via the gate's corpus-dir resolution (`Corpus.resolveRoot()`, honoring `-Dheddle.corpus`) |
| `NavModel` → `MegaMenu` → `MenuTab` → `NavColumn` → `NavSection` → `NavLink` | `NavModel { menus, footerColumns }`; `MegaMenu { tabs }`; `MenuTab { label, href, css, hasDropdown, dropdownCss, columns }`; `NavColumn { sections }`; `NavSection { title, href, titleLinked, links }`; `NavLink { label, href }` | parsed from the fixture's snake_case keys (`has_dropdown`, `dropdown_css`, `title_linked`, `footer_columns`); `hasDropdown`/`titleLinked` are precomputed booleans (no engine evaluates a string or collection test); every text value is asserted against workloads.md rule 4 at load, the way `NavData`'s static constructor asserts it |

**Composed-page model source (E20/E22).** The pre-E20 blob resource files under
`src/main/resources/composed-page/` and their loading code are deleted: the model tier carries
DATA only — every fragment of literal page text (chrome, alert banners, secondary menus,
asset/script snippets) lives in the templates, and the ONLY composed-page data fixture is
`GoldenCorpus/fixtures/composed-page/nav.json` (UTF-8, LF, snake_case — the single source of
truth every non-.NET ecosystem loads from, hash-recorded in the manifest's `fixtures` section).
The harness parses it with the already-pinned Jackson dependency (the same mapper the corpus
manifest reader uses — no new dependency). Transcription errors cannot slip through: the
controlled byte gate compares the composed output against the corpus entry before any timing,
so a single wrong byte fails loudly (the same self-verification Phase 1 relies on). Both
engines render from this one loaded model; JTE takes the typed `ComposedModel` param, the
Thymeleaf context passes only the nav view (`nav` variable — E22: no text fixture is served to
any engine).

## JTE templates

Source trees (per [D3](README.md#d3--jte-controlled-track-whitespace-free-authoring-trimcontrolstructures--false-generate-goal-aot)):
`src/main/jte-plain/{controlled,idiomatic}/` (ContentType `Plain`, package
`heddle.jte.gen.plain`) and `src/main/jte-html/{controlled,idiomatic}/` (ContentType `Html`,
package `heddle.jte.gen.html`). Render names are path-based: `controlled/mixed-page.jte` etc.
Controlled templates are whitespace-free (control structures inline, single-line bodies);
idiomatic templates are multi-line with a header comment citing jte.gg doc pages (Q1.7/D7).
`M` below abbreviates `heddle.benchmarks.jvm.model.Models`.

### Controlled — raw suite (`jte-plain/controlled/`)

Entry templates sit at the top of the track folder; every sub-template (layout, chrome,
nav, fragment kinds) lives in the `shared` sub-package (`jte-plain/controlled/shared/`),
called as `@template.controlled.shared.<name>(…)`.

`trivial-substitution.jte`:

```jte
@param heddle.benchmarks.jvm.model.Models.SubstitutionModel m
<article><h1>${m.getTitle()}</h1><p class="sku">${m.getSku()}</p><p class="price">${m.getPrice()}</p><p class="brand">${m.getBrand()}</p><p class="cat">${m.getCategory()}</p><p class="avail">${m.getAvailability()}</p><a class="link" href="${m.getUrl()}"><img src="${m.getImageUrl()}"></a><p class="sum">${m.getSummary()}</p><p class="rating">${m.getRating()}</p></article>
```

`large-loop.jte`:

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.LoopRow> items
@for(var r : items)<tr><td>row-${r.getValue()}</td><td>${r.getValue()}</td></tr>@endfor
```

(the display name is composed in the template as `row-` + the value substitution — E21)

`conditional-heavy.jte`:

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.ConditionalRow> rows
<ul class="matrix">@for(var r : rows)<li>@if(r.isBronze())<span class="t0">bronze</span>@elseif(r.isSilver())<span class="t1">silver</span>@elseif(r.isGold())<span class="t2">gold</span>@else<span class="t3">platinum</span>@endif<em>${r.getName()}</em>@if(r.isHasNote())<small>note ${r.getSeq()}</small>@endif@if(r.isActive())<b>active</b>@endif</li>@endfor</ul>
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
@for(var p : m.getProducts())<article class="card"><h3>${p.getName()}</h3><p class="sku">MX-${p.getSkuNumber()}</p><p class="price">${p.getPrice()}</p>@if(p.isOnSale())<p class="sale">On sale</p>@endif<p class="blurb">A dependable workshop staple from batch ${p.getBatch()}, checked for daily use and backed by our lifetime guarantee.</p></article>@endfor
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

`fragment-heavy.jte` + the six sub-templates (E20 landed form — `tile.jte`, `card.jte`,
`badge.jte`, `price.jte`, `mediarow.jte`, `stat.jte`; card nests badge + price against the
row's promo):

```jte
@param java.util.List<heddle.benchmarks.jvm.model.Models.FragmentRow> items
<div class="panel">@for(var item : items)@if(item.isTile())@template.controlled.shared.tile(item)@elseif(item.isCard())@template.controlled.shared.card(item)@elseif(item.isMedia())@template.controlled.shared.mediarow(item)@else@template.controlled.shared.stat(item)@endif@endfor</div>
```

```jte
tile.jte:     <section class="tile"><h3>${row.getName()}</h3><p class="v">${row.getValue()}</p><span class="badge">${row.getBadge()}</span></section>
badge.jte:    <span class="promo-badge">${p.getLabel()}</span>
price.jte:    <p class="price">${p.getPrice()}.99</p>
card.jte:     <article class="card"><h3>${row.getName()}</h3>@template.controlled.shared.badge(row.getPromo())@template.controlled.shared.price(row.getPromo())<p class="v">${row.getValue()}</p></article>
mediarow.jte: <div class="media-row"><img src="/img/${row.getName()}.jpg" alt="${row.getName()}" /><div class="media-body"><h4>${row.getName()}</h4><p>Caption for ${row.getName()}</p></div></div>
stat.jte:     <div class="stat"><span class="stat-name">${row.getName()}</span><span class="stat-value">${row.getValue()}</span><span class="stat-delta">${row.getDelta()}</span></div>
```

(each file opens with its own `@param`; the media caption, image source and `.99` display
price are template-composed — E21.)

`composed-page.jte` + `layout.jte` (E20/E22 landed form — native layout with a live body
slot): `composed-page.jte` passes the slider markup as a **content block** —
`@template.controlled.shared.layout(m, bodySlot = @`…slider markup…`)` — and `layout.jte` declares
`@param heddle.benchmarks.jvm.model.Models.ComposedModel m` + `@param gg.jte.Content
bodySlot`, carries the full literal chrome (section defaults inlined — JTE has no
overridable-block mechanism, so `<title>Title</title>`/socialmeta are literal text and the
empty `page_scripts`/`endpage_scripts` defaults are simply absent), calls the per-fragment
chrome sub-templates (`@template.controlled.shared.assetsstyles()`, `…alerttop()`,
`…secondarywholesalemenu()`, `…secondaryretailmenu()`, `…alertbelow()`, `…customstyles()`,
`…headscripts()`, `…bodyscripts()`, `…assetsscripts()`, `…bodyendscripts()`) at their chrome
positions, renders the nav through `@for` + the nested `megamenu`/`navcolumn`/`navsection`/
`navlink` sub-templates, and emits `${bodySlot}` at the body-slot position. **Sub-template
names are flattened** (`megamenu.jte`, not `mega-menu.jte`): `@template.<path>` segments must
be valid Java identifiers, so hyphenated names are not addressable. The corpus entry is the
byte target; the checked-in template is complete when the gate passes.

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

Same entries-at-top/`shared` sub-package split as the controlled tree
(`@template.idiomatic.shared.<name>(…)` for sub-templates).
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
per workload: `m` (page models), `rows`/`items` (lists), plus `nav` (the `NavModel` view —
E20/E22) for composed-page. The classloader resolver additionally pins
`resolver.setCharacterEncoding("UTF-8")` (`ThymeleafEngines.java`) — without it the resolver
reads templates in the platform charset, and the multi-byte chrome/fixture text would corrupt
on a non-UTF-8 default locale.

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
<th:block th:each="r : ${items}"><tr><td>row-[(${r.value})]</td><td>[(${r.value})]</td></tr></th:block>
```

`conditional-heavy.html` (rung 4):

```html
<ul class="matrix"><th:block th:each="r : ${rows}"><li><th:block th:switch="${true}"><th:block th:case="${r.bronze}"><span class="t0">bronze</span></th:block><th:block th:case="${r.silver}"><span class="t1">silver</span></th:block><th:block th:case="${r.gold}"><span class="t2">gold</span></th:block><th:block th:case="*"><span class="t3">platinum</span></th:block></th:block><em>[(${r.name})]</em><th:block th:if="${r.hasNote}"><small>note [(${r.seq})]</small></th:block><th:block th:if="${r.active}"><b>active</b></th:block></li></th:block></ul>
```

(`th:case` values evaluate in order, first true wins, `*` is the default — a genuine 1–4
evaluation chain; the tier booleans are mutually exclusive by construction.)

`mixed-page.html` (rung 5) — the literal skeleton of workload 4 with:
`[(${m.pageTitle})]` etc. at the nine scalar sites;
`<th:block th:if="${m.showBanner}"><div class="banner">[(${m.bannerText})]</div></th:block>`;
`<th:block th:each="p : ${m.products}"><article class="card"><h3>[(${p.name})]</h3><p class="sku">MX-[(${p.skuNumber})]</p><p class="price">[(${p.price})]</p><th:block th:if="${p.onSale}"><p class="sale">On sale</p></th:block><p class="blurb">A dependable workshop staple from batch [(${p.batch})], checked for daily use and backed by our lifetime guarantee.</p></article></th:block>`;
`<th:block th:if="${m.showDebugPanel}"><pre class="debug">debug</pre></th:block>`.

`fragment-heavy.html` + the six kind fragments (rung 6; E20 landed form — `tile.html`,
`card.html`, `badge.html`, `price.html`, `media-row.html`, `stat.html`, each a
`th:fragment(item/promo)` file; card nests badge + price against `${item.promo}`):

```html
<th:block th:fragment="tile(item)"><section class="tile"><h3>[(${item.name})]</h3><p class="v">[(${item.value})]</p><span class="badge">[(${item.badge})]</span></section></th:block>
```

```html
<div class="panel"><th:block th:each="item : ${items}"><th:block th:switch="${true}"><th:block th:case="${item.tile}"><th:block th:replace="~{controlled/shared/tile :: tile(${item})}"/></th:block><th:block th:case="${item.card}"><th:block th:replace="~{controlled/shared/card :: card(${item})}"/></th:block><th:block th:case="${item.media}"><th:block th:replace="~{controlled/shared/media-row :: media_row(${item})}"/></th:block><th:block th:case="*"><th:block th:replace="~{controlled/shared/stat :: stat(${item})}"/></th:block></th:block></th:block></div>
```

(the boolean `th:switch="${true}"` chain — the conditional-heavy rung-4 pattern — is the
controlled dispatch; the idiomatic track switches on `${item.kind}` with string cases.)

`composed-page.html` + `layout.html` + `chrome-fragments.html` (rung 8 re-run 2026-08-08 —
E20/E22 landed form, byte gate green): `composed-page.html` is
`<th:block th:replace="~{controlled/shared/layout :: layout(~{:: slider-body})}">` wrapping a
`<th:block th:fragment="slider-body">…slider markup…</th:block>`; `layout.html` carries the
section-default fragments (`meta_section` — renamed from `meta` per divergence class **B10**,
a fragment selector also matches literal `<meta>` elements by tag name — `socialmeta`, empty
`page_scripts`/`endpage_scripts`), the four nested nav fragments
(`mega_menu(menu)` → `nav_column(column)` → `nav_section(section)` → `nav_link(link)`,
dispatching on the precomputed `${tab.hasDropdown}`/`${section.titleLinked}` booleans, with
`th:attr` for the generated attributes — placement per class **B2b**), and the
`layout(bodySlot)` fragment holding the full literal chrome with
`<th:block th:replace="${bodySlot}"/>` at the body-slot position;
`chrome-fragments.html` is the E22 chrome library (`alert_top`, `secondary_wholesale_menu`,
`secondary_retail_menu`, `alert_below`, `assets_styles`, `custom_styles`, `head_scripts`,
`body_scripts`, `assets_scripts`, `body_end_scripts` as literal-text fragments) inserted by
`th:replace` at the chrome positions.

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
- partial: `<div th:replace="~{idiomatic/shared/tile :: tile(${item})}"></div>`;
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
