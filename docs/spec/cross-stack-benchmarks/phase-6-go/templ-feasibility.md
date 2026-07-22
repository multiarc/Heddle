# templ controlled-track feasibility — whitespace analysis and verification spike

Supplementary document of the [Phase 6 — go spec](README.md). It answers the plan's flagged
porting risk head-on: **can templ output reach the contract v2 byte gate, given that templ has no
whitespace-control syntax and unconditionally collapses HTML text whitespace at generation
time?** The answer is derived per workload against the Phase 1 pinned template shapes
([workloads.md](../phase-1-cross-stack-foundation/workloads.md)) and the normalization pipeline
([parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline)).
Since the 2026-07-20 maintainer ruling added the uniform **N3b** whitespace-run collapse, any
whitespace-only divergence passes the controlled gate program-wide, so templ's whitespace behavior
is no longer a controlled-track risk. Verification spike **S1** — the first work item of the
implementation plan — now confirms the analysis and pins templ's actual escaper bytes; its expected
outcome is a clean pass, with a single **non-whitespace** contingency (an escaper spelling outside
the N5 table) routing to the Q1.2 exclusion path.

## The engine facts this analysis rests on

Verified primary-source facts (re-checked while authoring this spec, 2026-07-20):

| # | Fact | Evidence |
|---|---|---|
| F1 | templ has **no whitespace-control syntax** (no trim markers, no preserve flag) and its generator **collapses every run of whitespace in HTML text nodes to a single space** at `templ generate` time. | [a-h/templ#90](https://github.com/a-h/templ/issues/90) (closed as enhancement; no `--preserve-whitespace` shipped as of v0.3.1020); spike C §(d); templ.guide has no whitespace page (404 re-checked) |
| F2 | The collapsing is a **generate-time transformation of template literal text only**. Runtime data — expression output and `templ.Raw` content — is emitted as-is (escaped or raw respectively), never whitespace-collapsed. | Mechanism per #90 (the *generator* rewrites text nodes); `templ.Raw` documented as "bypassing all HTML escaping and security mechanisms" ([templ.guide — rendering raw HTML](https://templ.guide/syntax-and-usage/rendering-raw-html/)) |
| F3 | templ collapses runs **to a single space**: it never inserts whitespace where the source has none, and a lone literal space (already a single space) is a fixed point of the transformation. What is **not documented** is the boundary behavior for *statement-adjacent* whitespace — whitespace whose neighbors are statements/component calls rather than text — i.e. whether it is emitted as a space or dropped; this is the one behavior S1 must observe. | #90 wording ("all whitespace … to a single space"); absence of any statement-whitespace documentation ([templ.guide — statements](https://templ.guide/syntax-and-usage/statements/), re-read: says nothing about whitespace) |
| F4 | `<style>` element contents are rendered **without any changes** (CSS curly braces included), so a literal `<style>` block is authorable. | [templ.guide — CSS style management](https://templ.guide/syntax-and-usage/css-style-management/): "`<style>` element contents are rendered to the output without any changes." (doc claim — S1 probe P3 executes it) |
| F5 | Text expressions `{ expr }` accept strings, numbers, and booleans; text output is escaped with the stdlib five-character decimal table (`&amp; &lt; &gt; &#34; &#39;` via `html.EscapeString`); attribute expression values are auto-quoted and HTML-attribute-encoded (docs show `&quot;`/`&#39;` spellings for the attribute path). | [templ.guide — expressions](https://templ.guide/syntax-and-usage/expressions/); templ `runtime.go` `EscapeString` → `html.EscapeString` (spike C item 10); [templ.guide — attributes](https://templ.guide/syntax-and-usage/attributes/). Exact attribute-path spellings are pinned by S1 probe P2; **every candidate spelling is inside the N5 table either way** |
| F6 | templ components are Go functions; arguments (structs, rows) pass with ordinary Go call syntax `@tile(item)`. | [templ.guide — template composition](https://templ.guide/syntax-and-usage/template-composition/) |

## Why the question is narrower than it looks

The byte gate compares the **non-whitespace** bytes of the normalized candidate and the stored
oracle (stored form N1 decode → N2 line endings → N3 collapse `>\s+<` to `><` → N4 trim → N5 entity
canonicalization, encoded suite only; then **N3b removes every whitespace run — to nothing — from
both sides at comparison**). Since the 2026-07-20 maintainer ruling added N3b, **any
whitespace-only divergence passes the controlled gate program-wide** — run-length or
presence-vs-absence alike ([contract v2 — normalization pipeline](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline);
[Phase 6 D13](README.md#d13--whitespace-only-divergence-resolved-program-wide-by-the-n3b-whitespace-run-collapse-maintainer-ruling-2026-07-20-option-a)).
So templ's whitespace collapsing — whether its effect is between tags (N3) or adjacent to non-tag
text (N3b) — is reconciled by the pipeline. The only divergence that can still fail the gate is a
**non-whitespace** one; for templ the sole such candidate is an escaper spelling outside the N5
table (which the untrusted-data alphabet is already designed to keep out of the corpus).

The Phase 1 workload shapes were authored under exactly this discipline
([workloads.md — shared authoring rule 3](../phase-1-cross-stack-foundation/workloads.md#shared-authoring-rules-for-the-five-new-workloads):
the **non-whitespace** text *inside* an element must be byte-identical across engines, while N3b
removes every whitespace run before comparison so intra-text whitespace differences are reconciled).
The pinned templates keep every text node **dense** — text is always directly flanked by tags, and
the only intra-text whitespace is single literal spaces that are part of the content. A templ twin
transcribed densely gives the collapser nothing to change inside text, and any whitespace it emits
anywhere else (between elements, or an inter-statement separator space adjacent to non-tag text) is
removed by N3b from both sides and, being whitespace-only, passes by construction under the
2026-07-20 ruling.

## Per-workload trace

Track: controlled. "Oracle shape" facts below are read from the pinned normative template texts
and models in [workloads.md](../phase-1-cross-stack-foundation/workloads.md) and — for
`composed-page` — from the live twin sources
([TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs),
[LiquidTemplates.cs](../../../../src/Heddle.Performance/Runners/LiquidTemplates.cs),
[AreaComponent.cs](../../../../src/Heddle.Performance/TestSuite/Extensions/AreaComponent.cs),
all read for this spec).

| Workload | Text-node exposure in the pinned shape | templ verdict |
|---|---|---|
| `trivial-substitution` | One dense line; every text node and expression directly flanked by tags; zero whitespace runs outside tags. | **Provably whitespace-safe** (nothing to collapse). S1 probe P1 executes it end-to-end as the byte-gate smoke test. |
| `large-loop` | Dense single-line row body `<tr><td>…</td><td>…</td></tr>`. | **Provably whitespace-safe.** |
| `mixed-page` | Skeleton has newlines only between sibling elements (inter-tag → N3); all headings/paragraphs dense; footer line `@(StoreName) @(Year) @(SupportEmail)` has **single literal spaces between expressions** — a one-space text node, which collapsing maps to itself (F1: runs collapse *to* a single space; a single space is a fixed point); `<style>` content is single-spaced CSS with `{}` braces. | **Whitespace-safe given F3 + F4.** Two executed confirmations needed: the style-content passthrough (F4 is doc-only — probe P3) and the space-between-expressions fixed point (probe P1 covers the same construct). |
| `conditional-heavy` | Every branch body is a complete element (`<span>…</span>` etc.); all text inside leaf elements, dense. templ requires the `if/else if/else` chain on its own lines — that whitespace sits between elements → single space → N3. | **Provably whitespace-safe given F3** (no insertion where none authored). Probe P4 confirms statement-adjacent behavior. |
| `fragment-heavy` | Tile partial body dense; loop calls a component per row; all inter-fragment whitespace is between `</section>` and `<section` → N3. | **Provably whitespace-safe given F3/F6.** |
| `fortunes-encoded` | Dense; every `{ }` expression flanked by `<td>`/`</td>`. Divergence risk is *spelling*, not whitespace: templ emits `&#34;` for `"` in text (F5) — reconciled by N5 (`&#34;` → `&quot;`), and `&#39;` is already canonical. Japanese passes through (stdlib escaper touches only the five characters). | **Whitespace-safe; N5 required in the Go gate** ([D4](README.md#d4--n5-entity-canonicalization-is-implemented-in-the-go-gate-runner)). Probe P2 pins the exact text- and attribute-path bytes. |
| `encoded-loop` | Dense; attribute expression `data-tag={ item.Tag }` plus two text expressions per row. Same spelling situation as fortunes; attribute path adds the F5 attribute-encoder question. | **Whitespace-safe; N5 required.** Probe P2 pins attribute bytes. |
| `composed-page` | The oracle is the ordered concatenation of fragment strings **with zero separator bytes** — verified: the .NET twins emit the fragments back-to-back (`LiquidTemplates.LayoutTemplate` concatenates `{{ section.meta }}{{ section.social }}{{ comp.assets_styles }}{{ comp.custom_styles }}…` with no whitespace between actions, and today's parity gate passes all four twins at 34,837 normalized chars), so the normalized oracle contains `…main.css" />/* CSS Comment Test */<script src="/head.js">…` at the `custom_styles` boundary. The fragments themselves are **runtime data** in every engine (read from `TwinContent`/`AreaComponent`, not transcribed into template text), so F2 keeps them byte-exact through `templ.Raw`. The one mechanic to observe: what bytes, if any, templ emits **between consecutive `@templ.Raw(...)`/statement nodes** — its source form requires the calls on separate lines. If templ emits a separator space there (`> /*` / `*/ <`), that is a **whitespace-only** divergence: N3b removes the space from both sides, so the candidate's `>␠/*`/`*/␠<` and the oracle's zero-separator `>/*`/`*/<` reduce to the **same** non-whitespace bytes and the cell passes by construction (2026-07-20 ruling); if templ emits nothing, the boundary is already byte-exact. Either way the cell passes on whitespace grounds — this is precisely the presence-vs-absence case that a "collapse to a single space" rule would have failed and that N3b's removal-to-nothing resolves. | **Expected pass.** S1 probe P4 records templ's actual inter-statement bytes as evidence; both outcomes pass the gate. |

**Summary of the analysis:** for all eight workloads templ's collapsing is whitespace-only and
reconciled by the pipeline — N3 for inter-element spaces, **N3b for every other whitespace run
(including the `composed-page` inter-statement boundary)**, N5 for spellings. The whitespace-only
divergence class passes by construction under the 2026-07-20 N3b ruling, so `composed-page` is no
longer the uncertain cell. The only outcome that could still fail a cell is a **non-whitespace**
one — an escaper spelling outside the N5 table (F5). S1 confirms the whitespace analysis and pins
the escaper bytes before any bulk porting; F3 (no-insertion) and F4 (style passthrough) are still
recorded as evidence but no longer gate the pass/fail outcome, because even an unexpected inserted
space would be whitespace-only and pass.

## S1 — the verification spike (first work item)

**Scope:** one throwaway-quality but committed Go/templ probe package under
`benchmarks/go/internal/spike/` (kept in-tree as evidence; excluded from benchmark runs), plus a
recorded result block in `benchmarks/go/README.md`. Prerequisite: Phase 1's corpus is exported
and committed (spec [assumed state](README.md#assumed-state)).

**Probes (all four required, executed at the pinned toolchain — Go 1.26.5, templ v0.3.1020):**

- **P1 — smallest-workload byte gate.** Author `trivial-substitution.templ` densely
  (per [port-mapping.md](port-mapping.md#workload-2--trivial-substitution)), `go tool templ generate`
  (the pinned CLI via `go tool`, per [D3](README.md#d3--toolchain-and-dependency-pins)),
  render, run the full N1–N4 pipeline, byte-compare against
  `src/Heddle.Performance/GoldenCorpus/trivial-substitution.golden.html`. Also author one line
  containing `{ a } { b } { c }` string expressions separated by single spaces and assert the
  single spaces survive verbatim (the mixed-page footer construct; F3 fixed point).
- **P2 — escaper bytes.** Render a one-row fortunes-shaped fragment and an encoded-loop-shaped
  row (`data-tag={ tag }` + text expressions) with data covering all of `& < > " '` plus
  `こんにちは`; record the exact emitted entity spellings for the text path and the attribute
  path; assert every spelling is in the [N5 table](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5)
  and that N5 output equals the canonical spellings; assert the Japanese string is byte-intact
  and `+`-free data round-trips unescaped.
- **P3 — style passthrough.** Author the exact mixed-page `<style>` line (CSS braces, single
  spaces) inside a templ component; assert the rendered style content is byte-identical to the
  authored content (F4 executed).
- **P4 — inter-statement whitespace (evidence only).** Author a component with two consecutive
  `@templ.Raw(...)` calls on separate lines whose payloads reproduce the composed-page
  CSS-comment neighborhood (`<link … />` + `/* CSS Comment Test */` + `<script …></script>`)
  and byte-inspect what appears between fragments; record the exact output bytes. This is now an
  **evidence-recording** probe, not a gate: whether templ emits a separator space or nothing, the
  divergence is whitespace-only and passes via N3b (2026-07-20 ruling). It is retained so the
  observed behavior is documented (and to confirm the divergence really is whitespace-only, not a
  reordering or added non-whitespace byte).

**Completion check:** `benchmarks/go/README.md` gains a "S1 results" section recording, per
probe, the command, the observed bytes (hex excerpt where relevant), and the verdict.

### Expected outcome — pass (the analysis holds)

P1 byte-equal (with any whitespace-only difference reconciled by N3/N3b); P2 spellings all inside
N5; P3 style content whitespace-only-or-identical; P4 shows a whitespace-only boundary (space or
nothing) that N3b reconciles. **Path:** proceed with the implementation plan
([README — implementation plan](README.md#implementation-plan)): templ controlled ports are
authored for all eight workloads, `composed-page` first (it was the historically-flagged cell),
each gated before timing. No further feasibility checkpoints exist — S1 was the only one.

### Contingency — a genuine non-whitespace divergence

The exclusion path triggers **only** if a probe shows a divergence that is **not** whitespace-only
and that the pipeline cannot reconcile — for templ the sole such candidate is **P2 showing an
escaper spelling outside the N5 table** (a byte the untrusted-data alphabet did not already keep
out). A whitespace-only divergence never reaches this path (it passes via N3b, 2026-07-20 ruling).
A P1 non-whitespace failure additionally triggers a one-time re-check of the probe implementation
itself before any exclusion is recorded. If a genuine non-whitespace divergence is confirmed, the
Phase 1 [exclusion policy](../phase-1-cross-stack-foundation/parity-contract-v2.md#exclusion-policy)
(Q1.2) is executed with this evidence-collection procedure, per affected workload:

1. **Best-effort record.** Enumerate the authoring variants attempted (dense single-line,
   adjacent-call, alternative element nesting — each with its `.templ` source committed under
   `benchmarks/go/internal/spike/attempts/`), so "best-effort authoring" is demonstrable, not
   asserted.
2. **Divergence record.** For the best variant: first-diff byte index, expected/actual lengths,
   ±40-char excerpt of both sides (the `ParityCheck.Describe` shape), and the classification. The
   classification must be `beyond whitespace` (non-whitespace) for this path to apply at all — a
   whitespace-only divergence passes via N3b and never reaches here. Record the specific
   non-whitespace bytes and the engine behavior responsible with its primary-source link (for the
   only expected case, a P2 escaper spelling outside the N5 table).
3. **Evidence file.** Write `benchmarks/go/EXCLUSIONS.md` with one section per excluded cell
   containing 1–2; the published report's controlled-track cell prints
   `excluded — documented evidence` linking to it
   (honest-reporting rule 6, [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md#honest-reporting-rules)).
4. **Track consequences.** templ stays fully in the idiomatic track for the affected workloads
   (idiomatic ports are unconditional scope); its controlled-track cells for unaffected
   workloads still ship. No replacement engine enters without user sign-off (standing ruling).
5. **quicktemplate consequence.** Any templ controlled-track exclusion means templ is not
   "fully green", so the [quicktemplate conditional stretch](README.md#d10--quicktemplate-stretch-operationalized-dormancy-disclosed)
   is automatically not triggered — no decision needed at that point; this spec decides it now.
6. **Whitespace-only divergence never reaches this path (decision [D13](README.md#d13--whitespace-only-divergence-resolved-program-wide-by-the-n3b-whitespace-run-collapse-maintainer-ruling-2026-07-20-option-a), CLOSED — option A).**
   The former whitespace-only escalation is resolved: the 2026-07-20 maintainer ruling added the
   uniform **N3b** whitespace-run collapse to contract v2, so **any** whitespace-only divergence
   passes the controlled gate program-wide and is never a candidate for exclusion. If a probe's
   divergence is classified whitespace-only in step 2, the cell **passes** — do not run the
   exclusion procedure. This procedure applies only to a confirmed non-whitespace divergence.

The expected outcome and the non-whitespace contingency are both fully specified; S1 confirms and
records evidence, it does not design.
