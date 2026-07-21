# Go port mapping — models, templates, and composition per workload

Supplementary document of the [Phase 6 — go spec](README.md). It pins the Go-side port of every
Phase 1 workload: the model transcription, the controlled-track template text per engine surface
(text/template for the raw suites, html/template for the encoded suite — Q6.1; templ for both),
the composition mapping, and the idiomatic-track authoring requirements. The normative source of
each workload's shape and data is
[workloads.md](../phase-1-cross-stack-foundation/workloads.md); this document only maps, it never
redefines. The templ whitespace discipline every controlled `.templ` file follows is derived in
[templ-feasibility.md](templ-feasibility.md).

## Engine surfaces and the Q6.1 labeling rule

| Suite | Stdlib surface (credibility pick) | Compiled pick |
|---|---|---|
| raw (workloads 1–6) | `text/template` — the stdlib engine minus the escaping pass | templ (`templ.Raw` for HTML-fragment *data*; text expressions elsewhere — raw model values contain no escapables by authoring rule 4, so templ's always-on escaper is a byte-level no-op on them) |
| encoded (workloads 7–8) | `html/template` — the runtime contextual encoder | templ (its default escaping path) |

Q6.1 (resolved) fixes the stdlib split and requires the report to label the measured surface per
suite; this spec extends the same split to **both tracks** (controlled and idiomatic) so the two
tracks measure the same engine surface and stay comparable within the ecosystem
(decision [D1](README.md#d1--q61-bound-to-code-texttemplate-raw--htmltemplate-encoded-both-tracks)).
Benchmark ids use `stdlib-text` / `stdlib-html` / `templ` (and `quicktemplate` if the stretch
triggers); report rows spell the full surface name.

## Model transcription (shared by all engines and tracks)

One package `benchmarks/go/internal/model` transcribes the pinned models — exact strings and
generation formulas — from [workloads.md](../phase-1-cross-stack-foundation/workloads.md) into Go
structs materialized once in package `init`/`var` blocks (the Go analogue of the .NET `Shared`
discipline). Rules:

- Field names keep the C# spelling (`PageTitle`, `OnSale`, …) — Go templates address them as
  `{{.PageTitle}}`; templ as `m.PageTitle`. Exported fields only (reflection needs them).
- Numeric formatting is `strconv.Itoa`/`%d` only (all pinned numbers are ints; no floats exist
  in any model), so output is locale-independent by construction.
- Formula fidelity examples (normative): mixed-page `Name = fmt.Sprintf("Product %02d", i)`,
  `Sku = fmt.Sprintf("MX-%d", 1000+i)`, `Price = 950 + i*7`, `OnSale = i%3 == 0`, `i` in
  `[1, 36]`; conditional-heavy `IsBronze = i%4 == 0` … `IsActive = i%5 != 0`, `i` in `[0, 199]`;
  fragment-heavy `Badge = []string{"new", "hot", "sale", "std"}[i%4]`; large-loop
  `Name = "row-" + strconv.Itoa(i)`, 5,000 rows; encoded-loop
  `Tag = fmt.Sprintf("tag-%d&'%d'", i, i%7)`, `Name = fmt.Sprintf("item <%d> & \"co\"", i)`,
  `Comment = fmt.Sprintf("'q' & <angle> \"d\" こんにちは %d", i)`, `i` in `[0, 4999]`.
- The 12 fortunes rows are string literals copied byte-for-byte from the pinned table
  (row 11 = the XSS payload, row 12 = the Japanese string, row 1 = `4.33e67` with no `+`).
- **composed-page fragments** are transcribed as Go string constants in
  `benchmarks/go/internal/model/composed.go`, copied verbatim from
  [TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs) (the six component
  constants, the four section values, `AreaOrder`) and
  [AreaComponent.cs](../../../../src/Heddle.Performance/TestSuite/Extensions/AreaComponent.cs)
  (the seven area fragments, including their verbatim-string indentation — the `@"…"` literals'
  whitespace is part of the bytes). Transcription errors cannot ship: the byte gate compares the
  assembled output against the corpus entry, so any drift fails loudly before timing. *(Verify at
  implementation: copy from the C# source files at the corpus's recorded `generatingCommit`.)*

**Untrusted-data alphabet compliance (verified while authoring this spec):** every pinned
encoded-suite value in workloads.md was checked character-by-character against the
[alphabet](../phase-1-cross-stack-foundation/parity-contract-v2.md#untrusted-data-alphabet):
no `+`, no `=`, no `` ` ``, no `&#` substring, no Latin-1-supplement or astral characters appear
in any fortunes message or encoded-loop cell (the fortunes row-1 value is `4.33e67` precisely so
`html/template`'s `+` → `&#43;` rule — golang/go#42506 — never fires). The Go gate additionally
asserts this at startup (a cheap scan of the materialized encoded models), so a future corpus
edit that violates the alphabet surfaces in this phase as a named gate error, not as a byte diff
(decision [D5](README.md#d5--alphabet-compliance-is-re-asserted-by-the-go-gate)).

## Controlled track — stdlib surfaces

Template sources are Go raw string literals (backquoted) in
`benchmarks/go/internal/stdlibtpl/templates.go`, authored **densely**: byte-for-byte the pinned
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
| composed-page section/component/area machinery | see [composed-page mapping](#workload-1--composed-page) |

Workload-by-workload notes (raw suite = text/template):

### Workload 1 — composed-page

Mirrors the .NET twin structure exactly (one layout template + data-driven fragments +
a real loop), transcribed from `LiquidTemplates.LayoutTemplate` (read for this spec — the twins
emit the fragments **with zero separator bytes**, and today's parity pass proves the oracle has
none either):

- Model: `type ComposedModel struct { Section map[string]string; Comp map[string]string; AreaNames []string; Areas map[string]string }`
  filled from the transcribed constants (keys mirror the twin keys: `meta`, `social`,
  `page_scripts`, `endpage_scripts`; `assets_styles` … `body_end_scripts`).
- Home template: `{{template "layout" .}}` (the include mapping of the twin table).
- Layout template (one line, no whitespace between actions):
  `{{define "layout"}}{{index .Section "meta"}}{{index .Section "social"}}{{index .Comp "assets_styles"}}{{index .Comp "custom_styles"}}{{index .Comp "head_scripts"}}{{index .Comp "body_scripts"}}{{range .AreaNames}}{{index $.Areas .}}{{end}}{{index .Comp "assets_scripts"}}{{index .Section "page_scripts"}}{{index .Section "endpage_scripts"}}{{index .Comp "body_end_scripts"}}{{end}}`
- text/template performs no escaping, so the HTML fragments flow through raw — the same
  non-encoding discipline as Fluid's no-encoder path and Handlebars triple-mustache.

### Workload 2 — trivial-substitution

Dense one-line `<article>` card, ten `{{.Member}}` substitutions in pinned order, including the
two attribute positions (`href="{{.Url}}"`, `src="{{.ImageUrl}}"`).

### Workload 3 — large-loop

`{{range .Items}}<tr><td>{{.Name}}</td><td>{{.Value}}</td></tr>{{end}}` — `Value` is an int;
text/template renders ints via `fmt` (`%v`), identical bytes to `strconv.Itoa`.

### Workload 4 — mixed-page

Transcribe the pinned skeleton line-for-line (line breaks between sibling elements are
N2/N3-erased; the `<style>` line and all text-bearing elements stay dense); page conditionals
`{{if .ShowBanner}}…{{end}}` / `{{if .ShowDebugPanel}}…{{end}}`; product loop with the row-level
`{{if .OnSale}}<p class="sale">On sale</p>{{end}}`; footer
`<p>{{.StoreName}} {{.Year}} {{.SupportEmail}}</p>` keeps its single literal spaces.

### Workload 5 — conditional-heavy

The pinned single-line `<ul class="matrix">` body with the four-way chain per row (mapping table
above) and the two toggles `{{if .HasNote}}<small>{{.Note}}</small>{{end}}{{if .IsActive}}<b>active</b>{{end}}`.

### Workload 6 — fragment-heavy

`{{define "tile"}}<section class="tile"><h3>{{.Name}}</h3><p class="v">{{.Value}}</p><span class="badge">{{.Badge}}</span></section>{{end}}`
+ main `<div class="panel">{{range .Items}}{{template "tile" .}}{{end}}</div>` — the
associated-template mechanism is the stdlib's partial-with-current-row construct, mirroring the
probe-E-verified twin constructs.

### Workload 7 — fortunes-encoded (html/template)

The pinned one-line skeleton with `{{.Id}}`/`{{.Message}}` in the row body. html/template's
contextual escaper fires on the text-context substitutions; on the pinned alphabet it escapes
exactly `& < > " '` (its `+` rule never fires — no `+` in the data), with spellings
`&amp; &lt; &gt; &#34; &#39;` — `&#34;` is reconciled by N5 ([D4](README.md#d4--n5-entity-canonicalization-is-implemented-in-the-go-gate-runner)).
The Japanese row passes through byte-intact (the replacement tables touch only listed ASCII).

### Workload 8 — encoded-loop (html/template)

`<table>{{range .Items}}<tr><td data-tag="{{.Tag}}">{{.Name}}</td><td>{{.Comment}}</td></tr>{{end}}</table>`
— `{{.Tag}}` sits in quoted-attribute context (html/template's `htmlReplacementTable`, same
five-character effect on this alphabet), the other two in text context. Same N5 reconciliation.

## Controlled track — templ

`.templ` sources live in `benchmarks/go/internal/templeng/`, one file per workload
(`composed_page.templ`, …), generated `*_templ.go` committed
([D3](README.md#d3--toolchain-and-dependency-pins)). Authoring rules (all derived in
[templ-feasibility.md](templ-feasibility.md)):

1. **Dense text discipline** — element content and expressions exactly as pinned, no whitespace
   runs adjacent to text; newlines allowed only between sibling elements/statements.
2. Text substitutions are `{ m.Member }` (strings, ints — templ renders both natively, F5);
   attribute substitutions are `attr={ m.Member }` (templ auto-quotes).
3. Control flow is templ's Go-statement syntax: `if m.ShowBanner { … }`,
   `if r.IsBronze { … } else if r.IsSilver { … } else if r.IsGold { … } else { … }`,
   `for _, r := range m.Rows { … }`.
4. The tile partial is a templ component `templ tile(r model.FragmentRow)` called `@tile(item)`
   in the loop (F6) — templ's partial-with-argument construct.
5. composed-page: one `templ composedPage(m model.ComposedModel)` component issuing
   `@templ.Raw(...)` per section/component fragment in twin order and
   `for _, name := range m.AreaNames { @templ.Raw(m.Areas[name]) }` — the fragments are runtime
   data, byte-exact through `templ.Raw` (F2); the inter-statement whitespace question is decided
   by S1 probe P4 before this file is authored, and the authoring form (separate lines vs
   adjacent) is whichever form P4 proved byte-clean.
6. mixed-page's `<style>` block is authored as a literal `<style>` element (F4); if S1 probe P3
   contradicts F4, the fallback authoring is `@templ.Raw(styleCSS)` around a Go constant holding
   the pinned CSS text, disclosed in the report's methodology note (the CSS is template literal
   text in every other engine; the accommodation is recorded, not silent). Both outcomes are
   decided now; P3 selects. (A whitespace-only difference in the style content passes via N3b; only
   a non-whitespace change to the CSS would need the fallback.)
7. Raw workloads: model values are escape-free by authoring rule 4, so templ's always-on
   escaping of `{ }` expressions emits identical bytes to the raw path — no bypass needed except
   for composed-page's HTML fragments, where `@templ.Raw` **is** the engine's documented raw
   mechanism (the analogue of Handlebars triple-mustache in the .NET twins).
8. Render path: `component.Render(ctx, buf)` into a reused `bytes.Buffer` with a background
   `context.Context` created once; per the metrics protocol, construction of the component value
   (a cheap closure) is inside the measured render — it is templ's per-render entry point,
   exactly how a caller invokes a cached templ template (there is no separate parse to exclude).

## Idiomatic track — both engines, all eight workloads

Gate: the Phase 1 verifier (`<id>.verify.json` semantics, N1–N4 + N5 for encoded) implemented in
`benchmarks/go/internal/corpus` — see
[harness-and-measurement.md](harness-and-measurement.md#idiomatic-verifier).
Authoring standard (Q1.7/D16, binding): in-repo, following official documentation patterns, with
the doc URLs cited in a header comment per implementation file. Concretely:

- **stdlib idiomatic** (`internal/stdlibtpl/idiomatic.go` + per-workload template consts):
  naturally formatted multi-line templates with indentation, `{{- -}}` trim markers where the Go
  docs use them, template composition via `{{define}}`/`{{template}}`/`ParseFS` idioms. Cited
  pages: <https://pkg.go.dev/text/template>, <https://pkg.go.dev/html/template>.
- **templ idiomatic** (`internal/templeng/idiomatic/*.templ`): naturally formatted (indented,
  one element per line — the format `templ fmt` produces; run `go tool templ fmt` (the pinned CLI
  via `go tool`, per [D3](README.md#d3--toolchain-and-dependency-pins), no global install) on
  these files, and only these, as part of authoring), idiomatic component decomposition (e.g. a `page` layout
  component wrapping content components for mixed-page). Cited pages:
  <https://templ.guide/syntax-and-usage/expressions/>, …/statements/, …/template-composition/,
  …/rendering-raw-html/ (composed-page fragments).
- The idiomatic sources may diverge freely in whitespace and structure; they must pass the
  verifier, and the encoded ones must satisfy the security-floor checks (`forbidden` /
  `required`).

## quicktemplate twins (conditional stretch only)

Authored **only** if the [D10 rule](README.md#d10--quicktemplate-stretch-operationalized-dormancy-disclosed)
triggers. Shape (pre-specified so triggering it adds zero design work): `.qtpl` files under
`benchmarks/go/internal/qtpl/`, `{% stripspace %}` around each template (spike E confirmed
`stripspace`/`collapsespace`/`{%- -%}` exist), `{%s v %}` escaped output for encoded workloads
(quicktemplate's `%s` HTML-escapes by default) and `{%s= v %}` raw output for raw workloads and
composed-page fragments, `{% for %}`/`{% if %}`/function-component calls mirroring the templ
structure; generator `qtc` pinned via the same go.mod `tool` mechanism at quicktemplate v1.8.0.
Its entity spellings are pinned at authoring time by a P2-style probe and must fall inside N5;
a spelling beyond N5 is a **non-whitespace** divergence and excludes the affected encoded cells
per the same non-whitespace exclusion procedure
([templ-feasibility.md](templ-feasibility.md#s1--the-verification-spike-first-work-item)) —
never new harness, gate, or contract work, per the binding plan rule. (Whitespace-only divergence
passes via N3b — 2026-07-20 ruling — and is never excluded.)
