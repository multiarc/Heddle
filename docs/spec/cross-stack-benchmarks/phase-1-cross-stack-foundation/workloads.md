# Workload definitions — the eight-workload set

Supplementary document of the [Phase 1 — cross-stack-foundation spec](README.md). It pins the
exact template shape, model data, and expected output characteristics of every workload in the
cross-stack set: the three retained anchors and the five new workloads this phase adds. The
[parity contract v2](parity-contract-v2.md) defines the gates these workloads must pass; the
[golden corpus](golden-corpus.md) defines how their oracle outputs are exported.

## The set at a glance

| # | Workload id | Suite | Dimension it owns | Rows | Normalized output (approx.) |
|---|---|---|---|---|---|
| 1 | `composed-page` | raw | Layout/section/component composition machinery | — | re-pinned at export (41,111 B stored at the E20 redesign export) |
| 2 | `trivial-substitution` | raw | Per-render fixed-overhead floor | — | ~338 chars |
| 3 | `large-loop` | raw | Bulk iteration and output writing | 5,000 | ~193 KB |
| 4 | `mixed-page` | raw | Realistic mixture: literals + substitutions + modest loop + conditionals | 36 | ~10–16 KB |
| 5 | `conditional-heavy` | raw | Branch evaluation and control-flow dispatch | 200 | ~15 KB |
| 6 | `fragment-heavy` | raw | Per-row fragment dispatch + nested per-call composition | 48 | re-pinned at export (6,057 B stored at the E20 redesign export) |
| 7 | `fortunes-encoded` | encoded | Escaping correctness and cost at micro scale | 12 | ~1.5 KB |
| 8 | `encoded-loop` | encoded | Escaping throughput at scale | 5,000 | ~1 MB |

Workload ids are the stable cross-phase identifiers: corpus file names, verifier file names, and
report row labels all use them. Approximate sizes are authoring targets; the exact byte length of
each oracle is recorded in the corpus [manifest](golden-corpus.md#manifest) at export and is the
authoritative number.

Every workload is constrained to the common-denominator feature set (scalar substitution, loops,
conditionals, partial/include, HTML escaping) — a workload using an engine-exclusive feature
cannot be parity-gated (plan constraint, carried).

## Anchors (workloads 1–3) — retained byte-unchanged

> **Amended (E20, 2026-08-08):** superseded **for `composed-page`** — that workload was
> redesigned into a genuine full-page layout workload (see
> [Workload 1 — composed-page](#workload-1--composed-page-raw--amended-e20) below); its
> templates, model, golden and verifier all changed and its manifest row was re-recorded at the
> regeneration export. The freeze below stands unchanged for `trivial-substitution` and
> `large-loop`.

The three existing workloads are **not modified in any way**: their templates
([home.heddle](../../../../benchmarks/dotnet/templates/controlled/heddle/home.heddle),
[layout.heddle](../../../../benchmarks/dotnet/templates/controlled/heddle/layout.heddle),
[trivial-substitution.heddle](../../../../benchmarks/dotnet/templates/controlled/heddle/trivial-substitution.heddle),
[large-loop.heddle](../../../../benchmarks/dotnet/templates/controlled/heddle/large-loop.heddle)), their
runner classes, their models, and their benchmark classes stay untouched, so historical numbers
remain comparable run-to-run. This spec adds a corpus export and verifier definition for each —
nothing else.

- **`composed-page`** — `home.heddle` extends `layout.heddle` via `@<<{{layout.heddle}}`; the
  output is the documented ordered fragment sequence (not a full HTML page). The fidelity note
  and `@<<` root cause in the
  [Runners README](../../../../benchmarks/dotnet/GoldenCorpus/README.md) are carried **verbatim**
  into the corpus documentation (decision [D5](README.md#d5--the-composed-page-anchor-keeps-its-fragment-sequence-shape-q14),
  per Q1.4). Confirmed size: 55,456 raw / 34,837 normalized chars for every gated engine (Heddle plus the four
twins on net8.0/net10.0; the Scriban twin is `#if !NET6_0`, so on net6.0 four engines are gated).
  *(Amended (E20): superseded — the output IS a full HTML page now; the fidelity note and root
  cause were deleted from that README and D5 is retired. Normative shape below.)*
- **`trivial-substitution`** — one flat `<article>` card, ten scalar substitutions
  (`Title, Sku, Price, Brand, Category, Availability, Url, ImageUrl, Summary, Rating`), rendered
  raw (`OutputProfile.Text`, `ExpressionMode.Native`). Model values are plain ASCII
  (`SubstitutionContent.SubstitutionModel`).
- **`large-loop`** — `@list(Items){{<tr><td>row-@(Value)</td><td>@(Value)</td></tr>}}` over 5,000 rows
  (`LoopContent.RowCount = 5000`, `Value = i`; the display name `row-{i}` is composed by the
  template as `row-` + the value substitution — E21), rendered raw.

## Shared authoring rules for the five new workloads

1. **Raw suite renders every engine's non-encoding path** (Heddle `OutputProfile.Text`; Fluid
   `Render(ctx)` with no encoder argument; Scriban/DotLiquid default `{{ value }}`; Handlebars
   triple-mustache `{{{ value }}}`) — same discipline as the anchors.
2. **Encoded suite renders every engine's escaping path** (Heddle `OutputProfile.Html` with
   `@attr` in attribute-value positions; Fluid `| escape`; Scriban `| html.escape`; DotLiquid
   `| escape`; Handlebars double-mustache `{{ value }}` under the custom
   `FiveEntityTextEncoder` — see [D3](README.md#d3--handlebarsnet-encoded-twins-use-a-custom-itextencoder)).
3. **Templates are authored so that any cross-engine output difference is whitespace-only** —
   i.e. reconciled by the normalization steps of the
   [contract](parity-contract-v2.md#normalization-pipeline). Literal text between tags may be
   laid out freely; the **non-whitespace** text *inside* an element (between `>` and the next
   non-tag character) must be byte-identical across engines. (Since the 2026-07-20 maintainer
   decision, step N3b removes every whitespace run — including inside element text — to nothing
   before comparison, so **all** whitespace differences inside text are reconciled, run-length and
   presence/absence alike; only the non-whitespace content of that text must match. Authors still
   keep text dense as the simplest way to stay well inside the gate.)
4. **Raw-suite model values contain only ASCII printable characters and no `& < > " '`**, so raw
   and would-be-escaped renderings coincide and no engine's incidental escaping can matter.
   Encoded-suite untrusted values follow the
   [untrusted-data alphabet](parity-contract-v2.md#untrusted-data-alphabet) instead.
5. **Per-engine model views are materialized once** (static fields), never per benchmark op —
   the established `Shared` / `SharedDotLiquid` / `SharedDictionary` pattern of
   `LoopContent` ([LoopContent.cs](../../../../benchmarks/dotnet/src/Models/LoopContent.cs)).
   Dictionary views use lowercase snake_case keys (`image_url` precedent in
   `SubstitutionContent`).
6. **Heddle compile options** for every new workload mirror the anchors:
   `FileNamePostfix = ".heddle"`, `RootPath = "TestTemplates"`,
   `ExpressionMode = ExpressionMode.Native`, `ProvideLanguageFeatures = false`;
   `OutputProfile = Text` for raw workloads, `OutputProfile = Html` for encoded ones.
7. **Scriban twins are `#if !NET6_0`** like the existing ones (Scriban 7.2.5 does not target
   net6.0).

Template texts below are normative: the implementer transcribes them into the named files.
Whitespace inside the fenced blocks is part of the template; all differences that the
normalization pipeline erases (line endings, inter-tag whitespace, every whitespace run removed to
nothing per N3b, leading/trailing trim) are tolerated between the spec text and the
checked-in file, nothing else is.

---

## Workload 1 — `composed-page` (raw) — Amended (E20, E22)

**Dimension owned:** layout/section/component composition machinery — now measured for real: a
full HTML page assembled from a layout with a **live body slot**, overridable section defaults,
named chrome-fragment definitions (the inert literal text), and a structured navigation
rendered through loops and **nested partials** (menu → column → section → link).

### Model — pure structured data (ledger E20; E22 removed the text half)

The model is `ComposedModel { NavModel Nav }` and NOTHING else — every fragment of literal page
text lives in the templates (E22: "the model-preparation tier carries DATA only"):

- **Inert chrome fragments** (E22; formerly the `AreaData` blob dictionary, now deleted): the
  alert banner, the two secondary menus, the pinned-empty alert-below slot, and the six fixed
  asset/script snippets are **named definitions in a definition-only template library**
  (`chrome-fragments.heddle`, imported by the layout via `@<<`) that the chrome calls at its
  composition points — pre-rendered literal HTML, but owned by the template tier, overridable
  per page with the ordinary `<name:name>` mechanism. The memcpy floor stays represented; no
  C# fixture serves text to any engine.
- **Structured navigation** (`NavData`): the two mega menus and the footer links,
  transcribed from the retired blobs into

```csharp
public sealed class NavModel   { List<MegaMenu> Menus; List<NavColumn> FooterColumns; }
public sealed class MegaMenu   { List<MenuTab> Tabs; }
public sealed class MenuTab    { string Label, Href, Css; bool HasDropdown; string DropdownCss; List<NavColumn> Columns; }
public sealed class NavColumn  { List<NavSection> Sections; }
public sealed class NavSection { string Title, Href; bool TitleLinked; List<NavLink> Links; }
public sealed class NavLink    { string Label, Href; }
```

`TitleLinked` and `HasDropdown` are **precomputed booleans** (no engine evaluates a string
test). `ComposedModel { NavModel Nav }` is the workload model; dictionary/Hash views nest under
a `nav` key with `menus` / `footer_columns` and snake_case members (`has_dropdown`,
`dropdown_css`, `title_linked`, `image_url`-style convention).

**Rule-4 sanitization (blocking pre-decision for every port):** every nav text value is ASCII
with none of `& < > " '` — `&`/`&amp;` became `and`, apostrophes/`™`/accents were dropped at
transcription — so raw and would-be-escaped renderings coincide and no default-escaping engine
can double-escape. `NavData`'s static constructor asserts the rule.

**Fixture:** `export-corpus` writes the nav model to
`GoldenCorpus/fixtures/composed-page/nav.json` (UTF-8, LF, two-space indent, snake_case keys,
declaration property order), hash-recorded in the manifest's `fixtures` section — the single
source of truth the five non-.NET ecosystems load from
([golden-corpus.md](golden-corpus.md#manifest)).

### Heddle templates — the layout-as-definition idiom (normative)

**No engine change.** The shape is the documented composition idiom
(docs/language-reference.md §"Composition without coupling"), proven at benchmark scale by
`src/Heddle.Tests/LayoutDefinitionCompositionTests.cs`:

- `chrome-fragments.heddle` (E22) is a **definition-only fragment library**: the alert banner
  (`alert_top`), the two secondary menus (`secondary_wholesale_menu`,
  `secondary_retail_menu`), the pinned-empty `alert_below`, and the fixed asset/script
  snippets (`assets_styles`, `assets_scripts`, `custom_styles`, `head_scripts`,
  `body_scripts`, `body_end_scripts`). Importing it renders nothing; any piece is overridable
  with `<name:name>`.
- `layout.heddle` is **definition-only**: `@<<{{chrome-fragments.heddle}}` (nested imports are
  the documented mechanism — Stage-1 test (f)), then one `@% … %@` block holding the section
  defaults (`meta`, `socialmeta`, `page_scripts`, `endpage_scripts`), the four nav fragment
  definitions below, and `<layout>{{ …the full ~150-line chrome… }} :: ComposedModel` with a
  bare `@out()` at the body-slot position. Importing the file renders nothing.
- `home.heddle` is `@<<{{layout.heddle}}` + `@layout(){{ …slider markup… }}` — the call body
  splices at `@out()`. Section overrides, when a page wants them, are the ordinary
  `<name:name>` mechanism after the import line. **Both tracks carry the same slider body**
  (the verifier's removed-segment calibration pin is the slider, so an empty body fails).

The nav fragment definitions (normative bytes for the controlled oracle; the chrome literal is
normative as committed in
[layout.heddle](../../../../benchmarks/dotnet/templates/controlled/heddle/layout.heddle)):

```heddle
<nav_link>
{{<li class="nav-link"><a href="@(Href)">@(Label)</a></li>}}
<nav_section>
{{<div class="nav-section">@if(TitleLinked){{<span class="nav-title"><a href="@(Href)">@(Title)</a></span>}}@else(){{<span class="nav-title">@(Title)</span>}}<ul>@list(Links){{@nav_link()}}</ul></div>}}
<nav_column>
{{<div class="nav-column">@list(Sections){{@nav_section()}}</div>}}
<mega_menu>
{{<div class="top-menu-wrapper"><ul class="top-menu">@list(Tabs){{<li class="@(Css)"><a href="@(Href)" class="drop">@(Label)</a>@if(HasDropdown){{<div class="@(DropdownCss)">@list(Columns){{@nav_column()}}</div>}}</li>}}</ul></div>}}
```

Call sites in the chrome: `@list(Nav.Menus){{@mega_menu()}}` where the two mega-menu blobs
were, `@list(Nav.FooterColumns){{@nav_column()}}` where the footer-links blob was, the
chrome-fragment definition calls (`@alert_top()`, `@secondary_wholesale_menu()`,
`@secondary_retail_menu()`, `@alert_below()`, `@assets_styles()`, `@custom_styles()`,
`@head_scripts()`, `@body_scripts()`, `@assets_scripts()`, `@body_end_scripts()`) at their
chrome positions (E22 — these replaced the deleted C# extensions; the templates carry **no
embedded C#** and call **no extensions**), `@out()` where `@body()` was. The workload runs
`ExpressionMode.Native` like every other workload and binds the typed `ComposedModel`.

**Chrome-authoring hazards (pinned by the Stage-1 tests, binding on every re-transcription):**
an adjacent `}}` pair inside the definition body is the subtemplate CLOSE token (minified CSS
`…}}` must be spaced or left unminified — the compile fails, it does not truncate), and a
literal `@` in chrome (emails, CSS at-rules) must be escaped `@@`.

### Native-layout mandate for twins and ports (normative)

Every engine composes with its **own documented layout mechanism** and a **live body slot** —
transcribing the chrome as one flat literal is a construct deviation, not a permissible
simplification:

| Engine | Layout mechanism (live body slot) | Nav rendering |
|---|---|---|
| Heddle | definition-only import + `@layout(){{ body }}` splicing at `@out()` | nested definitions (above) |
| Razor | `Layout` + one mid-page `@RenderBody()` | nested `@foreach` + partials; chrome fragments as literal HTML in the layout view (Razor-native — no workaround needed) |
| Fluid / DotLiquid | capture-then-include: `{% capture body_content %}…{% endcapture %}{% include 'layout' %}`, layout emits the slot | nested `{% for %}` + `nav-column`-family partials (dialect-suffixed) |
| Scriban | same capture-then-include shape in Scriban syntax | nested `{{ for }}` + partials |
| Handlebars | partial block `{{#> layout}}…{{/layout}}` + `{{> @partial-block}}` (probe against Handlebars.Net 2.1.6 first; documented fallback: registered body partial) | `{{#each}}` + partials |
| Rust (Askama / Tera) | `{% extends %}` + `{% block body %}` with a real child body | nav loops + nested `{% include %}` |
| JVM (JTE) | layout with a `gg.jte.Content bodySlot` parameter | `@for` + `@template` calls |
| JVM (Thymeleaf) | parameterized fragment `layout(~{:: body})` — re-run feasibility rungs first; exclusion-with-evidence is the accepted controlled-cell fallback | `th:each` + fragments |
| JS (eta) | native `layout()` + `it.body` | loops + partials |
| JS (handlebars) | partial-block layout | `{{#each}}` + partials |
| Python (Jinja2) | `{% extends %}` + filled block | loops + `{% include %}` |
| Python (Mako) | `<%inherit/>` + `${self.body()}` | loops + defs |
| Go (stdlib) | `{{block "body"}}` + `{{define "body"}}` override in the associated set | `{{range}}` + `{{template}}` |
| Go (templ) | `@layout(m) { children }` | nav components |

> **Added (E22):** the inert chrome fragments are **literal template text in every engine** —
> transcribed from the Heddle templates/golden and policed by the byte gate (controlled) and
> the verifier (idiomatic). No engine loads blob text from a data file or a C# fixture any
> more; the per-ecosystem blob fixture files (e.g. Rust's `data/composed-page/*.html`, the JVM
> `resources/composed-page/` blob files, and their hash-freshness tests) are **deleted** when
> each port lands. `fixtures/composed-page/nav.json` remains the only composed-page data
> fixture.

### Expected output characteristics

- One full page: doctype + IE-conditional html open, head (section defaults + asset/script
  fragments), header chrome with the alert and secondary-menu fragments, **both mega
  menus** from `Nav.Menus`, the spliced slider body, the four footer nav columns from
  `Nav.FooterColumns`, footer chrome, closing scripts, `</html>`.
- Exactly one `<li class="nav-link"><a href="…">…</a></li>` per `NavLink` and one
  `<div class="nav-column">` per column — the verifier counts both from the model.
- Size is re-pinned by the manifest at export (41,111 B stored at the E20 redesign export;
  raw render 47,455 chars).

---

## Workload 4 — `mixed-page` (raw)

**Dimension owned:** realistic mixture — literal HTML skeleton + scalar substitutions + a modest
loop + page-level and row-level conditionals in one template; output between the anchors'
extremes. This workload carries the realistic-full-page duty the composed-page anchor cannot
(Q1.4).

### Model — `MixedContent` (new file `benchmarks/dotnet/src/Models/MixedContent.cs`)

```csharp
public sealed class MixedModel
{
    public string PageTitle { get; set; }     // "Mercantile - Catalog"  (ASCII hyphen)
    public string StoreName { get; set; }     // "Mercantile"
    public string HeroHeading { get; set; }   // "Autumn hardware sale"
    public string HeroTagline { get; set; }   // "Hand-picked tools, fair prices, shipped tomorrow."
    public bool ShowBanner { get; set; }      // true
    public string BannerText { get; set; }    // "Free shipping on orders over 60."
    public bool ShowDebugPanel { get; set; }  // false
    public string FooterNote { get; set; }    // "Prices include VAT where applicable."
    public int Year { get; set; }             // 2026
    public string SupportEmail { get; set; }  // "support at mercantile.example"
    public List<MixedProduct> Products { get; set; }
}

public sealed class MixedProduct
{
    public string Name { get; set; }   // $"Product {i:D2}"
    public int SkuNumber { get; set; } // 1000 + i (templates compose the display SKU: MX-@(SkuNumber) — E21)
    public int Price { get; set; }     // 950 + i * 7
    public bool OnSale { get; set; }   // i % 3 == 0
    public int Batch { get; set; }     // i (templates compose the blurb sentence around @(Batch) — E21)
}
```

`Products` has **36** rows, `i` in `[1, 36]`. All values are ASCII with no `& < > " '` (rule 4);
`SupportEmail` is exactly `"support at mercantile.example"` (a plain phrase, so no engine or
idiomatic port is tempted to treat it as a linkable address).

### Heddle template — `benchmarks/dotnet/templates/controlled/heddle/mixed-page.heddle`

```heddle
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>@(PageTitle)</title>
<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
</head>
<body>
<header>
<h1>@(StoreName)</h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
@if(ShowBanner){{<div class="banner">@(BannerText)</div>}}
<section class="hero">
<h2>@(HeroHeading)</h2>
<p>@(HeroTagline)</p>
</section>
<section class="grid">
@list(Products){{<article class="card"><h3>@(Name)</h3><p class="sku">MX-@(SkuNumber)</p><p class="price">@(Price)</p>@if(OnSale){{<p class="sale">On sale</p>}}<p class="blurb">A dependable workshop staple from batch @(Batch), checked for daily use and backed by our lifetime guarantee.</p></article>}}
</section>
@if(ShowDebugPanel){{<pre class="debug">debug</pre>}}
</main>
<footer>
<p>@(FooterNote)</p>
<p>@(StoreName) @(Year) @(SupportEmail)</p>
</footer>
</body>
</html>
```

### Twin templates

Fluid and DotLiquid share one Liquid source (new file `Runners/MixedLiquidTemplates.cs`, the
`LiquidTemplates.cs` pattern). The body mirrors the Heddle template with:

- `{{ page_title }}`, `{{ store_name }}`, … for scalars;
- `{% if show_banner %}<div class="banner">{{ banner_text }}</div>{% endif %}`;
- `{% for p in products %}<article class="card"><h3>{{ p.name }}</h3><p class="sku">MX-{{ p.sku_number }}</p><p class="price">{{ p.price }}</p>{% if p.on_sale %}<p class="sale">On sale</p>{% endif %}<p class="blurb">A dependable workshop staple from batch {{ p.batch }}, checked for daily use and backed by our lifetime guarantee.</p></article>{% endfor %}`;
- `{% if show_debug_panel %}<pre class="debug">debug</pre>{% endif %}`.

Scriban mirrors with `{{ if show_banner }} … {{ end }}`, `{{ for p in products }} … {{ end }}`,
`{{ p.name }}` members. Handlebars mirrors with `{{{page_title}}}`, `{{#if show_banner}} …
{{/if}}`, `{{#each products}} … {{/each}}` and `{{{name}}}`-form members inside the loop
(triple-mustache throughout — rule 1). Chained `else if` is not needed here.

### Expected output characteristics

- One `<style>` block and full HTML skeleton in literal text; 8 distinct scalar page members
  (`StoreName` is rendered twice — in the header and the footer — for 9 substitution sites);
  36 cards; 12 of them (i = 3, 6, …, 36) carry the `On sale` paragraph; the debug panel never
  renders.
- Normalized size in the 10–16 KB band — between the anchors' extremes (exact bytes pinned by
  the manifest at export).

> **Added (E20):** the idiomatic mixed-page is **single-file** in every ecosystem. Layout
> composition is composed-page's dimension; an idiomatic port that split mixed-page into a
> layout + body was measuring the wrong thing on the wrong row, and divergent existing ports
> are brought in line with this rule.

---

## Workload 5 — `conditional-heavy` (raw)

**Dimension owned:** branch evaluation and control-flow dispatch — 200 rows, each performing one
four-way branch chain (1–3 condition evaluations, depending on which branch fires) plus two
independent toggles: **roughly 850 condition evaluations per render**, with a fixed
taken/skipped mixture and deliberately small branch bodies so dispatch, not writing, dominates.

### Model — `ConditionalContent` (new file `Runners/ConditionalContent.cs`)

```csharp
public sealed class ConditionalModel { public List<ConditionalRow> Rows { get; set; } }

public sealed class ConditionalRow
{
    public string Name { get; set; }   // $"unit-{i:D3}"
    public int Seq { get; set; }       // i (templates compose the note text: note @(Seq) — E21)
    public bool IsBronze { get; set; } // i % 4 == 0
    public bool IsSilver { get; set; } // i % 4 == 1
    public bool IsGold { get; set; }   // i % 4 == 2  (else branch fires when i % 4 == 3)
    public bool HasNote { get; set; }  // i % 2 == 0
    public bool IsActive { get; set; } // i % 5 != 0
}
```

200 rows, `i` in `[0, 199]`. Every engine branches on the same precomputed booleans — no engine
evaluates integer comparisons — so the workload measures branch *dispatch* on identical data, and
the four-way chain is expressible in vanilla Handlebars (`{{#if}}/{{else if}}` needs truthy
operands only; equality helpers are not in the common-denominator set). The dictionary views
expose `name, seq, is_bronze, is_silver, is_gold, has_note, is_active`.

### Heddle template — `TestTemplates/conditional-heavy.heddle`

```heddle
<ul class="matrix">@list(Rows){{<li>@if(IsBronze){{<span class="t0">bronze</span>}}@elif(IsSilver){{<span class="t1">silver</span>}}@elif(IsGold){{<span class="t2">gold</span>}}@else(){{<span class="t3">platinum</span>}}<em>@(Name)</em>@if(HasNote){{<small>note @(Seq)</small>}}@if(IsActive){{<b>active</b>}}</li>}}</ul>
```

(The tier chain is one Heddle branch set — opener, two continuations, terminal; the two toggles
are separate single-`@if` sets. There is no non-whitespace text between the branch blocks of the
tier set, so no `HED3001` gap warning fires.)

### Twin templates

- Liquid (Fluid + DotLiquid, shared source `Runners/ConditionalLiquidTemplates.cs`):
  `{% for r in rows %}<li>{% if r.is_bronze %}<span class="t0">bronze</span>{% elsif r.is_silver %}<span class="t1">silver</span>{% elsif r.is_gold %}<span class="t2">gold</span>{% else %}<span class="t3">platinum</span>{% endif %}<em>{{ r.name }}</em>{% if r.has_note %}<small>note {{ r.seq }}</small>{% endif %}{% if r.is_active %}<b>active</b>{% endif %}</li>{% endfor %}`
  wrapped in the same `<ul class="matrix">…</ul>`.
- Scriban: same structure with `{{ if r.is_bronze }} … {{ else if r.is_silver }} … {{ else }} …
  {{ end }}` (chained `else if` verified against Scriban 7.2.5 — probe E, spike record in the
  [README Assumed state](README.md#assumed-state)).
- Handlebars: `{{#each rows}}<li>{{#if is_bronze}}<span class="t0">bronze</span>{{else if is_silver}}<span class="t1">silver</span>{{else if is_gold}}<span class="t2">gold</span>{{else}}<span class="t3">platinum</span>{{/if}}<em>{{{name}}}</em>{{#if has_note}}<small>note {{{seq}}}</small>{{/if}}{{#if is_active}}<b>active</b>{{/if}}</li>{{/each}}`
  (chained `{{else if}}` verified against Handlebars.Net 2.1.6 — probe E).

### Expected output characteristics

- Per row: exactly one tier `<span>`, the `<em>` name, `<small>` on even rows (100 rows),
  `<b>active</b>` on 160 rows. Normalized size ≈ 15 KB.

---

## Workload 6 — `fragment-heavy` (raw) — Amended (E20)

**Dimension owned:** per-row fragment **dispatch** plus nested per-call composition — 48 rows of
four distinct fragment kinds (12 each), one four-way branch per row selecting which partial to
invoke, and one level of nesting (the card fragment renders two sub-partials against its
`Promo`). *(The pre-E20 single-tile shape — one partial invoked 48 times — is superseded; it
measured call overhead with no dispatch and no nesting.)*

### Model — `FragmentContent` (`benchmarks/dotnet/src/Models/FragmentContent.cs`)

```csharp
public sealed class FragmentModel { public List<FragmentRow> Items { get; set; } }

public sealed class FragmentRow
{
    public string Kind { get; set; }      // { "tile", "card", "media", "stat" }[i % 4] — informational
    public bool IsTile { get; set; }      // Kind == "tile"   (precomputed; engines dispatch on these,
    public bool IsCard { get; set; }      // Kind == "card"    never on the string — guaranteed
    public bool IsMedia { get; set; }     // Kind == "media"   common-denominator dispatch)
    public bool IsStat { get; set; }      // Kind == "stat"
    public string Name { get; set; }      // $"item-{i:D2}" (identity data — the one padded value)
    public int Value { get; set; }        // i * 11
    public string Badge { get; set; }     // { "new", "hot", "sale", "std" }[i % 4]
    public int Delta { get; set; }        // i % 7 - 3
    public FragmentPromo Promo { get; set; } // on EVERY row (no engine needs a null guard)
}

public sealed class FragmentPromo
{
    public string Label { get; set; }     // == Badge
    public int Price { get; set; }        // 9 + i (whole-currency units — an int, not a string)
}
```

48 rows, `i` in `[0, 47]`. Dictionary views expose `kind, is_tile, is_card, is_media, is_stat,
name, value, badge, delta` and a nested `promo` (`label`, `price`). All values are rule-4 clean.

**The model carries data, never display strings.** Derived text is composed by the TEMPLATES
(that is the work being measured): the media caption is the literal `Caption for ` + `name`, the
media image source is `/img/` + `name` + `.jpg`, and the display price is `price` + the literal
`.99` — plain literal-plus-substitution composition, portable to every engine with no arithmetic
or formatting helpers. Model-side pre-formatting of any of these is a port defect: it moves
rendering work out of the engine under test.

### Heddle template — `templates/{track}/heddle/fragment-heavy.heddle` (normative)

```heddle
@%
<tile>
{{<section class="tile"><h3>@(Name)</h3><p class="v">@(Value)</p><span class="badge">@(Badge)</span></section>}}
<badge>
{{<span class="promo-badge">@(Label)</span>}}
<price>
{{<p class="price">@(Price).99</p>}}
<card>
{{<article class="card"><h3>@(Name)</h3>@badge(Promo)@price(Promo)<p class="v">@(Value)</p></article>}}
<media_row>
{{<div class="media-row"><img src="/img/@(Name).jpg" alt="@(Name)" /><div class="media-body"><h4>@(Name)</h4><p>Caption for @(Name)</p></div></div>}}
<stat>
{{<div class="stat"><span class="stat-name">@(Name)</span><span class="stat-value">@(Value)</span><span class="stat-delta">@(Delta)</span></div>}}
%@
<div class="panel">@list(Items){{@if(IsTile){{@tile()}}@elif(IsCard){{@card()}}@elif(IsMedia){{@media_row()}}@else(){{@stat()}}}}</div>
```

`@badge(Promo)` passes the row's `Promo` member down
([language-reference — passing the current value into a call](../../../language-reference.md#passing-the-current-value-into-a-call))
— the one nesting level. There is no non-whitespace text between the branch blocks of the
dispatch chain, so no `HED3001` gap warning fires.

### Dispatch per family (normative)

Six partials per engine (`tile`, `card`, `badge`, `price`, `media-row`, `stat`; card invokes
badge + price against the row's promo), dispatched per row:

| Family | Dispatch construct |
|---|---|
| Heddle | `@if(IsTile){{…}}@elif(IsCard){{…}}@elif(IsMedia){{…}}@else(){{…}}` (above) |
| Fluid / DotLiquid (Liquid) | `{% case kind %}{% when 'tile' %}…{% endcase %}` or the boolean `{% if is_tile %}/{% elsif %}` chain, with the per-kind `{% include %}` inside each arm |
| Scriban | `{{ case kind }}{{ when 'tile' }}…{{ end }}` (or boolean `if/else if` chain) + `{{ include }}` |
| Handlebars | chained `{{#if is_tile}}{{> tile this}}{{else if is_card}}{{> card this}}…{{/if}}` (the probe-verified pattern from conditional-heavy) |
| Razor | C# `switch` on `Kind` or an if-chain, invoking `Html.PartialAsync` per kind |
| Rust (Askama / Tera) | `{% if item.is_tile %}…{% elif %}` chain; per-kind include/partial |
| JVM (JTE / Thymeleaf) | JTE `@if/@elseif` chain; Thymeleaf `th:switch` over the boolean cases (conditional-heavy precedent) |
| JS (eta / handlebars) | boolean chain + partials |
| Python (Jinja2 / Mako) | `{% if %}/{% elif %}` include chains; Mako `args=` pass-through |
| Go (stdlib / templ) | `{{if .IsTile}}{{template "tile" .}}{{else if}}` chain; templ `switch` |

Idiomatic tracks may use each engine's documented native switch form where one exists; the
controlled track uses the constructs above. Main templates wrap the loop in
`<div class="panel">…</div>`.

### Expected output characteristics

- Exactly 12 each of `<section class="tile">`, `<article class="card">`,
  `<div class="media-row">`, `<div class="stat">`, in model order; 12 `promo-badge` spans and
  12 `price` paragraphs (proof the nesting ran). Size re-pinned by the manifest at export
  (6,057 B stored at the E20 redesign export).

---

## Workload 7 — `fortunes-encoded` (encoded)

**Dimension owned:** escaping correctness and cost at micro scale — a small table whose data
includes the XSS payload and a Japanese UTF-8 string, rendered through each engine's escaping
path. Untrusted data sits in **HTML text context only** (the classic Fortunes shape);
attribute-context escaping is owned by workload 8.

### Model — `FortunesContent` (new file `Runners/FortunesContent.cs`)

```csharp
public sealed class FortuneModel { public List<FortuneRow> Rows { get; set; } }
public sealed class FortuneRow { public int Id { get; set; } public string Message { get; set; } }
```

Exactly 12 rows, in this order (Ids 1–12). The strings are pinned byte-for-byte; note row 1
deliberately writes `4.33e67` (no `+` — the
[untrusted-data alphabet](parity-contract-v2.md#untrusted-data-alphabet) excludes `+`, `` ` ``,
and `=` so that broader-set escapers in later phases, Go `html/template` above all, are not
structurally excluded by the data):

| Id | Message |
|---|---|
| 1 | `A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1` |
| 2 | `A computer program does what you tell it to do, not what you want it to do.` |
| 3 | `A computer scientist is someone who fixes things that aren't broken.` |
| 4 | `A list is only as strong as its weakest link. — Donald Knuth` |
| 5 | `After enough decimal places, nobody gives a damn.` |
| 6 | `Any program that runs right is obsolete.` |
| 7 | `Computers make very fast, very accurate mistakes.` |
| 8 | `Emacs is a nice operating system, but I prefer UNIX. — Tom Christiansen` |
| 9 | `Feature: A bug with seniority.` |
| 10 | `fortune: No such file or directory` |
| 11 | `<script>alert("This should not be displayed in a browser alert box.");</script>` |
| 12 | `フレームワークのベンチマーク` |

Row 11 is **the XSS payload string** (identical to TechEmpower's) and row 12 **the Japanese
string** (identical to TechEmpower's). The em dashes in rows 4 and 8 are U+2014 — BMP,
≥ U+0100, inside the untrusted-data alphabet, and pass through every configured escaper
unescaped (spike B: `WebUtility.HtmlEncode` and every twin escape filter leave BMP ≥ U+0100
alone). Apostrophes (rows 3) escape to the canonical `&#39;` on every configured path.

### Heddle template — `TestTemplates/fortunes-encoded.heddle`

Compiled with `OutputProfile = OutputProfile.Html` (text-context substitutions run
`HtmlEncodedRenderer` → `WebUtility.HtmlEncode`):

```heddle
<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>@list(Rows){{<tr><td>@(Id)</td><td>@(Message)</td></tr>}}</table></body></html>
```

(The skeleton mirrors TechEmpower's required Fortunes response markup.)

### Twin templates

| Engine | Row body |
|---|---|
| Fluid | `<tr><td>{{ r.id }}</td><td>{{ r.message \| escape }}</td></tr>` |
| DotLiquid | `<tr><td>{{ r.id }}</td><td>{{ r.message \| escape }}</td></tr>` |
| Scriban | `<tr><td>{{ r.id }}</td><td>{{ r.message \| html.escape }}</td></tr>` |
| Handlebars | `<tr><td>{{id}}</td><td>{{message}}</td></tr>` — double-mustache, under `FiveEntityTextEncoder` ([D3](README.md#d3--handlebarsnet-encoded-twins-use-a-custom-itextencoder)) |

`id` is an int and never contains escapable characters; escaping it (Handlebars double-mustache
does) is a no-op, so the un-filtered `{{ r.id }}` on the other twins cannot diverge.

### Expected output characteristics

- Exactly one `<table>` with a header row and 12 data rows in model order.
- The escaped XSS form appears exactly once:
  `&lt;script&gt;alert(&quot;This should not be displayed in a browser alert box.&quot;);&lt;/script&gt;`.
- The raw substring `<script>alert(` appears **zero** times (the Fortunes rule — asserted by the
  [idiomatic verifier](golden-corpus.md#idiomatic-verifier-definitions) and by the encoded-suite
  gate).
- `フレームワークのベンチマーク` appears intact as raw UTF-8.
- Normalized size ≈ 1.5 KB.

---

## Workload 8 — `encoded-loop` (encoded)

**Dimension owned:** escaping throughput at scale — a large-loop-shaped workload with escapable
content in **every** cell, escape cost dominating; exercises both text and attribute-value
contexts.

### Model — `EncodedLoopContent` (new file `Runners/EncodedLoopContent.cs`)

```csharp
public sealed class EncodedLoopModel { public List<EncodedLoopRow> Items { get; set; } }

public sealed class EncodedLoopRow
{
    public string Tag { get; set; }     // $"tag-{i}&'{i % 7}'"
    public string Name { get; set; }    // $"item <{i}> & \"co\""
    public string Comment { get; set; } // $"'q' & <angle> \"d\" こんにちは {i}"
}
```

5,000 rows, `i` in `[0, 4999]`. Every cell contains characters from the five-character set, and
every `Comment` carries the Japanese run `こんにちは` so the escaper also scans multi-byte UTF-8
it must pass through. All values satisfy the
[untrusted-data alphabet](parity-contract-v2.md#untrusted-data-alphabet).

### Heddle template — `TestTemplates/encoded-loop.heddle`

Compiled with `OutputProfile = OutputProfile.Html`. The attribute-value position uses `@attr`
(the contextual attribute encoder, `ContextEncoders.EscapeAttribute`), which also satisfies the
`HED2004` context-encoding lint; text positions use plain `@(…)`:

```heddle
<table>@list(Items){{<tr><td data-tag="@attr(Tag)">@(Name)</td><td>@(Comment)</td></tr>}}</table>
```

On the pinned alphabet, Heddle's two encoders emit identical bytes for identical input
(`WebUtility.HtmlEncode` and `EscapeAttribute` agree on the five-character set and both pass
BMP ≥ U+0100 through — spike B), so the mixed use of `@attr` and `@(…)` cannot introduce
intra-oracle inconsistency.

### Twin templates

| Engine | Row body |
|---|---|
| Fluid | `<tr><td data-tag="{{ item.tag \| escape }}">{{ item.name \| escape }}</td><td>{{ item.comment \| escape }}</td></tr>` |
| DotLiquid | same as Fluid (shared Liquid source `Runners/EncodedLoopLiquidTemplates.cs`) |
| Scriban | `<tr><td data-tag="{{ item.tag \| html.escape }}">{{ item.name \| html.escape }}</td><td>{{ item.comment \| html.escape }}</td></tr>` |
| Handlebars | `<tr><td data-tag="{{tag}}">{{name}}</td><td>{{comment}}</td></tr>` — double-mustache under `FiveEntityTextEncoder` |

### Expected output characteristics

- Exactly 5,000 `<tr>` rows; every row contains `&amp;`, `&lt;`, `&gt;`, `&quot;`, and `&#39;`
  at least once; `こんにちは` appears exactly 5,000 times intact.
- No raw `<` from data survives: the only `<`/`>` bytes in the output belong to the literal
  markup.
- Normalized size ≈ 1 MB (the corpus entry is the largest committed golden; the size is accepted
  — see [README — Performance considerations](README.md#performance-considerations)).
