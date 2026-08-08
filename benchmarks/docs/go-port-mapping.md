# Go port mapping — models, templates, and composition per workload

> Operational documentation of the completed cross-stack benchmark program — the contract the harnesses implement. Collapsed decision record: [cross-cutting-decisions.md § Program record — cross-stack benchmarks](../../docs/spec/common/cross-cutting-decisions.md#program-record--cross-stack-benchmarks-closed).

Port documentation for the Go leg of the program (phase 6). It pins the Go-side port of every
workload: the model transcription, the controlled-track template text per engine surface
(text/template for the raw suites, html/template for the encoded suite — Q6.1; templ for both),
the composition mapping, and the idiomatic-track authoring requirements. The normative source of
each workload's shape and data is
[workloads.md](workloads.md); this document only maps, it never
redefines. The templ whitespace discipline every controlled `.templ` file follows is derived in
[go-templ-feasibility.md](go-templ-feasibility.md).

## Engine surfaces and the Q6.1 labeling rule

| Suite | Stdlib surface (credibility pick) | Compiled pick |
|---|---|---|
| raw (workloads 1–6) | `text/template` — the stdlib engine minus the escaping pass | templ (`templ.Raw` for HTML-fragment *data*; text expressions elsewhere — raw model values contain no escapables by authoring rule 4, so templ's always-on escaper is a byte-level no-op on them) |
| encoded (workloads 7–8) | `html/template` — the runtime contextual encoder | templ (its default escaping path) |

Q6.1 (resolved) fixes the stdlib split and requires the report to label the measured surface per
suite; the program extends the same split to **both tracks** (controlled and idiomatic) so the two
tracks measure the same engine surface and stay comparable within the ecosystem
(decision D1).
Benchmark ids use `stdlib-text` / `stdlib-html` / `templ` (and `quicktemplate` if the stretch
triggers); report rows spell the full surface name.

## Model transcription (shared by all engines and tracks)

One package `benchmarks/go/internal/model` transcribes the pinned models — exact strings and
generation formulas — from [workloads.md](workloads.md) into Go
structs materialized once in package `init`/`var` blocks (the Go analogue of the .NET `Shared`
discipline). Rules:

- Field names keep the C# spelling (`PageTitle`, `OnSale`, …) — Go templates address them as
  `{{.PageTitle}}`; templ as `m.PageTitle`. Exported fields only (reflection needs them).
- Numeric formatting is `strconv.Itoa`/`%d` only (all pinned numbers are ints; no floats exist
  in any model), so output is locale-independent by construction.
- **Data, not display (amendment E21):** the model tier carries no derived
  display strings — templates compose them as literal-plus-substitution. Zero-padded
  *identity* names (`item-{i:D2}`, `unit-{i:D3}`, `Product {i:D2}`) and the encoded-suite
  payloads stay model-side by design (`fmt.Sprintf` for these is sanctioned).
- Formula fidelity examples (normative, per E20/E21): mixed-page
  `Name = fmt.Sprintf("Product %02d", i)`, `SkuNumber = 1000 + i` (an int — the template
  composes `MX-{{.SkuNumber}}`), `Price = 950 + i*7`, `OnSale = i%3 == 0`, `Batch = i` (the
  blurb sentence lives in the template), `i` in `[1, 36]`; conditional-heavy
  `Seq = i` (the template composes `note {{.Seq}}`), `IsBronze = i%4 == 0` …
  `IsActive = i%5 != 0`, `i` in `[0, 199]`; fragment-heavy (E20 four-kind redesign)
  `Kind = []string{"tile", "card", "media", "stat"}[i%4]` with precomputed
  `IsTile/IsCard/IsMedia/IsStat` booleans, `Name = fmt.Sprintf("item-%02d", i)`,
  `Value = i*11`, `Badge = []string{"new", "hot", "sale", "std"}[i%4]`, `Delta = i%7 - 3`,
  `Promo = FragmentPromo{Label: Badge, Price: 9 + i}` on every row; large-loop rows carry
  ONLY `Value = i` (the display name `row-{i}` is template-composed — E21); encoded-loop
  `Tag = fmt.Sprintf("tag-%d&'%d'", i, i%7)`, `Name = fmt.Sprintf("item <%d> & \"co\"", i)`,
  `Comment = fmt.Sprintf("'q' & <angle> \"d\" こんにちは %d", i)`, `i` in `[0, 4999]`.
- The 12 fortunes rows are string literals copied byte-for-byte from the pinned table
  (row 11 = the XSS payload, row 12 = the Japanese string, row 1 = `4.33e67` with no `+`).
- **composed-page model (amended — E20 structured nav, E22 no text blobs):** the model is
  `ComposedModel { Nav NavModel }` and nothing else — the former embedded blob fragments
  (`internal/model/data/composed-page/`, their go:embed loading, `AreaOrder`, the
  `Section`/`Comp`/`Areas` maps, and the blob-vs-corpus assembly test) are **deleted**;
  every fragment of literal chrome text is template-tier property, transcribed by the
  per-engine template files and policed by the byte gate. The structured navigation is
  loaded **once, at first use** (`sync.Once`, `model.Composed()`), from the corpus fixture
  `GoldenCorpus/fixtures/composed-page/nav.json` — resolved through the same corpus-dir
  resolution the gate uses (`corpus.Dir()`; `go:embed` cannot reach `../../dotnet`, so a
  runtime file read is the mechanism, and a missing/malformed fixture panics with the
  export-corpus regeneration hint). The Go structs mirror the pinned shape with snake_case
  `encoding/json` tags matching the fixture keys:
  `NavModel { Menus []MegaMenu "menus"; FooterColumns []NavColumn "footer_columns" }` →
  `MegaMenu { Tabs }` → `MenuTab { Label, Href, Css, HasDropdown "has_dropdown",
  DropdownCss "dropdown_css", Columns }` → `NavColumn { Sections }` →
  `NavSection { Title, Href, TitleLinked "title_linked", Links }` → `NavLink { Label, Href }`
  (`HasDropdown`/`TitleLinked` are precomputed booleans — no engine evaluates a string
  test). The model tests re-assert workloads.md rule-4 sanitization on every nav text value
  (printable ASCII, none of `& < > " '`). Fixture drift cannot ship: the byte gate compares
  the rendered page against the corpus entry, so any drift fails loudly before timing.

**Untrusted-data alphabet compliance (verified while authoring the port):** every pinned
encoded-suite value in workloads.md was checked character-by-character against the
[alphabet](parity-contract-v2.md#untrusted-data-alphabet):
no `+`, no `=`, no `` ` ``, no `&#` substring, no Latin-1-supplement or astral characters appear
in any fortunes message or encoded-loop cell (the fortunes row-1 value is `4.33e67` precisely so
`html/template`'s `+` → `&#43;` rule — golang/go#42506 — never fires). The Go gate additionally
asserts this at startup (a cheap scan of the materialized encoded models), so a future corpus
edit that violates the alphabet surfaces here as a named gate error, not as a byte diff
(decision D5 — alphabet compliance is re-asserted by the Go gate).

## Controlled track — stdlib surfaces

Template sources are Go raw string literals (backquoted) in
`benchmarks/go/internal/stdlibtpl/controlled/templates.go`, authored **densely**: byte-for-byte the pinned
Heddle/twin shape with `{{…}}` actions in place of `@(…)` substitutions, no whitespace beyond
what the pinned shape carries. text/template and html/template share the same source text per
workload wherever both consume it (they never do — raw and encoded workloads are disjoint — but
the encoded sources follow the same authoring rules). Every template is parsed once in `init`;
renders execute `Template.Execute` into a reused `bytes.Buffer` (reset per call), returning
`buf.String()` — the cached-template render path the metrics protocol defines.

Normative action mapping (transcribing, per workload, the pinned template texts):

| Heddle construct | text/template / html/template |
|---|---|
| `@(Member)` | `{{.Member}}` (inside a loop: the range variable is `.`, so `{{.Name}}` addresses the row field) |
| `@list(Items){{…}}` | `{{range .Items}}…{{end}}` |
| `@if(X){{…}}` | `{{if .X}}…{{end}}` |
| `@if/@elif/@elif/@else` four-way chain | `{{if .IsBronze}}…{{else if .IsSilver}}…{{else if .IsGold}}…{{else}}…{{end}}` |
| partial with current row (`@tile()` under `@list`) | associated template: `{{define "tile"}}…{{end}}` + `{{range .Items}}{{template "tile" .}}{{end}}` |
| composed-page section/component/area machinery | see [composed-page mapping](#workload-1--composed-page--amended-e20-e22-landed-form-2026-08-08) |

Workload-by-workload notes (raw suite = text/template):

### Workload 1 — composed-page — Amended (E20, E22; landed form 2026-08-08)

A genuine full-page layout with a live body slot, built as a **three-parse associated set**
(`composedChromeSrc` → `composedLayoutSrc` → `composedHomeSrc`, parsed into one template set
in that order — `templates.go`):

- Model (E20/E22): `type ComposedModel struct { Nav NavModel }` — nothing else; loaded once
  from `GoldenCorpus/fixtures/composed-page/nav.json` via `model.Composed()` (see
  [Model transcription](#model-transcription-shared-by-all-engines-and-tracks) above). All
  chrome text is template-tier property; no map of pre-rendered fragments exists anywhere
  in the Go tier.
- **Chrome parse** (`composedChromeSrc`) — a **definition-only chrome library**: one
  `{{define}}` per inert fragment (`alert_top`, `secondary_wholesale_menu`,
  `secondary_retail_menu`, the empty `alert_below`, `assets_styles`, `assets_scripts`,
  `custom_styles`, `head_scripts`, `body_scripts`, `body_end_scripts`), literal text
  mirroring `chrome-fragments.heddle`; parsing it renders nothing.
- **Layout parse** (`composedLayoutSrc`) — the section-default definitions (`meta`,
  `socialmeta`, and the **empty** `page_scripts`/`endpage_scripts`), the four **nested nav
  defines** (`mega_menu` → `nav_column` → `nav_section` → `nav_link`, invoking each other
  with `{{template "…" .}}` and dispatching on the precomputed `.TitleLinked`/`.HasDropdown`
  booleans), and `{{define "layout"}}` holding the full literal chrome with
  `{{template "…"}}` calls at the chrome/section positions,
  `{{range .Nav.Menus}}{{template "mega_menu" .}}{{end}}` /
  `{{range .Nav.FooterColumns}}{{template "nav_column" .}}{{end}}` at the nav sites, and
  **`{{block "body" .}}{{end}}`** at the body-slot position — an empty block default.
- **Home parse** (`composedHomeSrc`) — `{{define "body"}}…slider markup…{{end}}{{template "layout" .}}`:
  a **later non-empty `{{define "body"}}` in the associated set overrides the block's empty
  default** (the stdlib's documented block-override mechanism), then invokes the layout.
- text/template performs no escaping; every nav value is rule-4 clean and all chrome is
  literal template text, so the raw path is byte-faithful by construction.

### Workload 2 — trivial-substitution

Dense one-line `<article>` card, ten `{{.Member}}` substitutions in pinned order, including the
two attribute positions (`href="{{.Url}}"`, `src="{{.ImageUrl}}"`).

### Workload 3 — large-loop — Amended (E21; landed form)

The row carries ONLY `Value = i` (`Name` is deleted from `LoopRow`); the template composes
the display name as the literal `row-` plus the value substitution:

`{{range .Items}}<tr><td>row-{{.Value}}</td><td>{{.Value}}</td></tr>{{end}}` — `Value` is an
int; text/template renders ints via `fmt` (`%v`), identical bytes to `strconv.Itoa`.

### Workload 4 — mixed-page — Amended (E21; landed form)

`MixedProduct` carries `SkuNumber = 1000 + i` and `Batch = i` (ints) in place of the deleted
`Sku`/`Blurb` strings; the template composes the display SKU `MX-{{.SkuNumber}}` and the
blurb sentence around `{{.Batch}}` (normative texts in workloads.md workload 4).

Transcribe the pinned skeleton line-for-line (line breaks between sibling elements are
N2/N3-erased; the `<style>` line and all text-bearing elements stay dense); page conditionals
`{{if .ShowBanner}}…{{end}}` / `{{if .ShowDebugPanel}}…{{end}}`; product loop with the row-level
`{{if .OnSale}}<p class="sale">On sale</p>{{end}}`; footer
`<p>{{.StoreName}} {{.Year}} {{.SupportEmail}}</p>` keeps its single literal spaces.

### Workload 5 — conditional-heavy — Amended (E21; landed form)

`ConditionalRow` carries `Seq = i` (an int) in place of the deleted `Note` string; the
template composes the note text as the literal `note ` plus the substitution.

The pinned single-line `<ul class="matrix">` body with the four-way chain per row (mapping table
above) and the two toggles `{{if .HasNote}}<small>note {{.Seq}}</small>{{end}}{{if .IsActive}}<b>active</b>{{end}}`.

### Workload 6 — fragment-heavy — Amended (E20; landed form)

48 rows of four dispatched fragment kinds (12 each), one four-way dispatch per row on the
precomputed `IsTile/IsCard/IsMedia/IsStat` booleans (never on the `Kind` string), one
nesting level (card renders badge + price against `Promo`). The model carries data only —
the media caption (`Caption for ` + name), image source (`/img/` + name + `.jpg`) and
display price (price + `.99`) are composed by the templates (E21).

Six `{{define}}`s in one associated set (`tile`, `badge`, `price`, `card` — which nests
`{{template "badge" .Promo}}{{template "price" .Promo}}` — `media_row`, `stat`) + the main
body with the boolean dispatch chain:

`<div class="panel">{{range .Items}}{{if .IsTile}}{{template "tile" .}}{{else if .IsCard}}{{template "card" .}}{{else if .IsMedia}}{{template "media_row" .}}{{else}}{{template "stat" .}}{{end}}{{end}}</div>`

— the associated-template mechanism is the stdlib's partial-with-current-row construct,
mirroring the probe-E-verified twin constructs.

### Workload 7 — fortunes-encoded (html/template)

The pinned one-line skeleton with `{{.Id}}`/`{{.Message}}` in the row body. html/template's
contextual escaper fires on the text-context substitutions; on the pinned alphabet it escapes
exactly `& < > " '` (its `+` rule never fires — no `+` in the data), with spellings
`&amp; &lt; &gt; &#34; &#39;` — `&#34;` is reconciled by N5 (D4: N5 entity canonicalization
is implemented in the Go gate runner).
The Japanese row passes through byte-intact (the replacement tables touch only listed ASCII).

### Workload 8 — encoded-loop (html/template)

`<table>{{range .Items}}<tr><td data-tag="{{.Tag}}">{{.Name}}</td><td>{{.Comment}}</td></tr>{{end}}</table>`
— `{{.Tag}}` sits in quoted-attribute context (html/template's `htmlReplacementTable`, same
five-character effect on this alphabet), the other two in text context. Same N5 reconciliation.

## Controlled track — templ

`.templ` sources live in `benchmarks/go/internal/templeng/controlled/`, one file per workload
(`composed_page.templ`, …), generated `*_templ.go` committed
([D3](go-harness-and-measurement.md#layout-and-toolchain-rules-d2d3-condensed)). Authoring rules (all derived in
[go-templ-feasibility.md](go-templ-feasibility.md)):

1. **Dense text discipline** — element content and expressions exactly as pinned, no whitespace
   runs adjacent to text; newlines allowed only between sibling elements/statements.
2. Text substitutions are `{ m.Member }` (strings, ints — templ renders both natively, F5);
   attribute substitutions are `attr={ m.Member }` (templ auto-quotes).
3. Control flow is templ's Go-statement syntax: `if m.ShowBanner { … }`,
   `if r.IsBronze { … } else if r.IsSilver { … } else if r.IsGold { … } else { … }`,
   `for _, r := range m.Rows { … }`.
4. The fragment partials are templ components (`templ tile(r model.FragmentRow)` etc.) called
   `@tile(item)` in the dispatch chain (F6) — templ's partial-with-argument construct; the
   card component nests `@badge(r.Promo)` + `@price(r.Promo)`.
5. **composed-page (E20/E22 landed form — children composition):** the chrome lives in
   `templ layout(m model.ComposedModel)` with **`{ children... }`** at the body-slot position
   (the `@out()` analogue), and the page component invokes **`@layout(m) { …slider markup… }`**
   — the caller body splices at the slot (templ.guide template-composition, children). The
   inert chrome fragments are **literal template text in per-fragment components** mirroring
   `chrome-fragments.heddle` (no fragment text is model data); the structured nav renders
   through nested components (`megaMenu → navColumn → navSection → navLink`) over
   `model.ComposedModel`. One component-boundary note: `custom_styles` renders the whole
   `<style>/* CSS Comment Test */</style>` element — a component call cannot appear **inside**
   a `<style>` element (its content is CSS raw text, F4), so the fragment owns its `<style>`
   wrapper; the emitted bytes are identical.
6. mixed-page's `<style>` block is authored as a literal `<style>` element (F4 — P3 confirmed
   byte-identical passthrough; the `@templ.Raw(styleCSS)` fallback was not needed).
7. Raw workloads: model values are escape-free by authoring rule 4, so templ's always-on
   escaping of `{ }` expressions emits identical bytes to the raw path — no bypass needed.
   **E21 display composition is native text interpolation**: `row-{ r.Value }`,
   `MX-{ p.SkuNumber }`, `note { r.Seq }`, `{ p.Price }.99`, and dynamic attribute values by
   Go string concatenation (`src={ "/img/" + r.Name + ".jpg" }`).
8. **Void-element accommodation (F7 — the doctype-accommodation mechanism):** templ's
   generator normalizes self-closing void elements to the slashless form (`<img/>`/`<img />` →
   `<img>`), a NON-whitespace divergence from the oracle. The oracle's nine composed-page
   ` />` sites and fragment-heavy's media `<img … />` therefore emit their exact pinned bytes
   via `@templ.Raw` over pinned literals (literal-plus-substitution where the value is
   dynamic — the media row's ``@templ.Raw(`<img src="/img/` + r.Name + `.jpg" alt="` + r.Name + `" />`)``
   — every spliced value rule-4 clean). Same class as the lowercased `<!DOCTYPE html>` accommodation; each site is
   an accommodation over pinned template literal text, disclosed here, never over model data.
   **Idiomatic templ authors void elements natively** (slashless — the verifier does not
   check the slash).
8. Render path: `component.Render(ctx, buf)` into a reused `bytes.Buffer` with a background
   `context.Context` created once; per the metrics protocol, construction of the component value
   (a cheap closure) is inside the measured render — it is templ's per-render entry point,
   exactly how a caller invokes a cached templ template (there is no separate parse to exclude).

## Idiomatic track — both engines, all eight workloads

Gate: the Phase 1 verifier (`<id>.verify.json` semantics, N1–N4 + N5 for encoded) implemented in
`benchmarks/go/internal/corpus` — see
[go-harness-and-measurement.md](go-harness-and-measurement.md#idiomatic-verifier).
Authoring standard (Q1.7/D16, binding): in-repo, following official documentation patterns, with
the doc URLs cited in a header comment per implementation file. Concretely:

- **stdlib idiomatic** (`internal/stdlibtpl/idiomatic/idiomatic.go` + per-workload template consts):
  naturally formatted multi-line templates with indentation, `{{- -}}` trim markers where the Go
  docs use them, template composition via `{{define}}`/`{{template}}`/`ParseFS` idioms. Cited
  pages: <https://pkg.go.dev/text/template>, <https://pkg.go.dev/html/template>.
- **templ idiomatic** (`internal/templeng/idiomatic/*.templ`): naturally formatted (indented,
  one element per line — the format `templ fmt` produces; run `go tool templ fmt` (the pinned CLI
  via `go tool`, per [D3](go-harness-and-measurement.md#layout-and-toolchain-rules-d2d3-condensed), no global install) on
  these files, and only these, as part of authoring), idiomatic component decomposition (e.g. a `page` layout
  component wrapping content components for mixed-page). Cited pages:
  <https://templ.guide/syntax-and-usage/expressions/>, …/statements/, …/template-composition/,
  …/rendering-raw-html/ (composed-page fragments).
- The idiomatic sources may diverge freely in whitespace and structure; they must pass the
  verifier, and the encoded ones must satisfy the security-floor checks (`forbidden` /
  `required`).

## quicktemplate twins (conditional stretch only)

Authored **only** if the D10 rule (quicktemplate stretch, operationalized with dormancy
disclosed) triggers — it did not trigger, so no quicktemplate code exists; the pre-specified
shape is retained for the record. Shape (pre-specified so triggering it adds zero design work): `.qtpl` files under
`benchmarks/go/internal/qtpl/`, `{% stripspace %}` around each template (spike E confirmed
`stripspace`/`collapsespace`/`{%- -%}` exist), `{%s v %}` escaped output for encoded workloads
(quicktemplate's `%s` HTML-escapes by default) and `{%s= v %}` raw output for raw workloads and
composed-page fragments, `{% for %}`/`{% if %}`/function-component calls mirroring the templ
structure; generator `qtc` pinned via the same go.mod `tool` mechanism at quicktemplate v1.8.0.
Its entity spellings are pinned at authoring time by a P2-style probe and must fall inside N5;
a spelling beyond N5 is a **non-whitespace** divergence and excludes the affected encoded cells
per the same non-whitespace exclusion procedure
([go-templ-feasibility.md](go-templ-feasibility.md#s1--the-verification-spike-first-work-item)) —
never new harness, gate, or contract work, per the binding plan rule. (Whitespace-only divergence
passes via N3b — 2026-07-20 ruling — and is never excluded.)
