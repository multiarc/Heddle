# Phase 7 — consolidated-report (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-7-consolidated-report.md](../../../plan/phase-7-consolidated-report.md)
  (standing rulings in the [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — every resolution there is binding on
  this spec; the ones this phase consumes directly: **Q7.1 as amended 2026-07-20** (the
  date-stamped directory is the phase's **sole** publication surface), **Q7.2** (no minimum
  quorum — any non-empty shipped subset of phases 2–6 ships), and the cross-phase resolutions
  Q1.3, Q1.6, Q2.1, Q2.2 = A, Q4.1, Q4.2, Q5.1, Q6.2)
- **Assumes merged:**
  [Phase 1 — cross-stack-foundation](../phase-1-cross-stack-foundation/README.md) in full,
  including its published protocol run under `docs/benchmarks/<date>/` (the source of every
  Heddle figure in this report), **plus whichever subset of phases 2–6 has shipped its
  published run at assembly time** — [Rust](../phase-2-rust/README.md),
  [JVM](../phase-3-jvm/README.md), [JS](../phase-4-js/README.md),
  [Python](../phase-5-python/README.md), [Go](../phase-6-go/README.md). No phase 2–6 is
  individually required (Q7.2). Everything consumed is **read-only**: published run
  directories, harness artifacts, corpus, contract, and protocol are never edited by this
  phase; a defect found in any of them is escalated to its owning phase (D14), never patched
  here.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, error surface, testing plan |
| [report-assembly.md](report-assembly.md) | The normative report format and assembly mechanics: directory contents, exact `index.md` section order, table shapes and provenance format, figure-extraction and spot-check procedures, `sources.json` schema, `consolidate.py` contract, classification tables, counted-claims and claim-downgrade rules, drift-review and re-run-request procedures, verbatim texts, and the publication checklist |

## Scope and goal

Assemble and publish the **single consolidated cross-stack report**: every shipped ecosystem's
protocol-conformant, already-published wall-time-per-render results juxtaposed per workload and
per fairness track (tracks never mixed), Heddle-anchored per Q6.2, grouped per ecosystem, with
the workload-shape strengths-and-weaknesses analysis over all eight Phase 1 dimensions, an
inclusion manifest covering every ecosystem present and absent, per-ecosystem non-comparable
sidebars, a source-environment manifest with per-source reproduce commands, a limitations
section assembled from the source runs' own disclosed caveats, and the repo's honest-reporting
posture — published as **exactly one new immutable `docs/benchmarks/<yyyy-MM-dd>/` directory
and nothing else** (Q7.1 as amended).

This phase performs **no measurements**, edits **no existing artifact**, derives **no new
metric** (the only arithmetic permitted anywhere is unit conversion to ns/render, and a
Heddle-anchored ratio only where a source table did not already publish it — D4), and publishes
**no ranking of non-Heddle engines across ecosystems** and **no aggregate cross-workload
score** (Q6.2; plan non-goals). The pre-protocol reports
([2026-07-11](../../../benchmarks/2026-07-11/index.md),
[2026-07-18](../../../benchmarks/2026-07-18/index.md)) are cited as history only and are never
aggregated.

## Assumed state

Re-verified against the current working tree and the phase 1–6 specs while authoring
(2026-07-21); nothing is trusted from the plan alone. The three assumptions the plan flagged
for this spec are each closed below (rows marked **[plan-flagged]**).

| Seam | Verified state |
|---|---|
| Published protocol runs **[plan-flagged: "the concrete list of published source-run directories"]** | **None exist yet.** `docs/benchmarks/` contains only the pre-protocol `2026-07-11/` and `2026-07-18/` directories *(listed 2026-07-21)*; phases 1–6 are specified but unimplemented. The inclusion manifest therefore **cannot be frozen at spec time** and is frozen instead by an executable procedure at assembly time (D1, WI1) — this closes the flagged assumption: the list is a WI1 output, not a spec constant |
| Cross-run environment drift **[plan-flagged: "whether any source run's environment drifted"]** | Unassessable before any source run exists; closed as a decided **procedure** with pinned materiality rules and a re-run-request mechanism (D11, WI2, [report-assembly.md — drift review](report-assembly.md#drift-review-and-re-run-requests)) |
| Recorded cross-phase resolutions **[plan-flagged: "the recorded values … in open-questions.md"]** | All verified present and resolved (user, 2026-07-20) in [open-questions.md](../../../plan/open-questions.md) *(read)*: Q1.3 (cold cost per-ecosystem only), Q1.6 (Windows/Ryzen 9 9950X protocol machine), Q2.1 (harness-default point estimate + dispersion), Q2.2 = **A** (Heddle reference row in every per-ecosystem report), Q4.1 (JS time-only unless verified — Phase 4 D3 closed it to **time-only**), Q4.2 (Handlebars/Handlebars.Net side-note, in the JS report), Q5.1 (tracemalloc allocated-bytes-per-render), Q6.2 (Heddle-anchored ratio; non-Heddle engines never ranked across ecosystems; within-ecosystem baselines for non-comparable metrics), Q7.1 as amended, Q7.2 |
| `docs/language-assessment.md` | **Absent** *(checked 2026-07-21)* — confirming Q7.1's amendment: there is no citation target; the date-stamped directory is the sole surface |
| Metrics & publication protocol | [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md) *(read)* — metric rules 1–4, the two required verbatim label texts (allocation non-comparability; encoded-suite confinement caveat), the Q2.1 statistic-mapping table (BenchmarkDotNet `Mean`/`Error`+`StdDev`; Criterion `mean` point estimate/95% CI; JMH avgt `Score`/`Error` 99.9% CI; mitata `avg`/percentile spread; pyperf `mean`/std dev; benchstat `sec/op`/`±%`), ns/render normalization in Heddle-row tables, presentation rules 1–5, machine record, publication format for **runs**, honest-reporting rules 1–6 |
| Controlled-gate normalization incl. N3b (**maintainer decision 2026-07-20, Phase 1 D8**) | [parity-contract-v2.md — normalization pipeline](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline) *(read)*: the closed list is N1, N2, N3, N4, N5 for the stored oracle, plus **N3b** (remove every whitespace run, anywhere, to **nothing** — applied identically to oracle and candidate at comparison, so the gate compares non-whitespace bytes). Consequence consumed here: any whitespace-only divergence — run-length or presence/absence — passes the controlled gate program-wide; exclusion cells can exist only for **non-whitespace** divergence. The report's gate description must state the amended pipeline (D3) |
| Heddle reference row appears in **both** track tables | **Settled program-wide**: [Phase 2 D13](../phase-2-rust/README.md#d13--report-one-docsbenchmarksdate-directory-in-the-protocol-shape) and [Phase 3 D12](../phase-3-jvm/README.md#d12--report-format-protocol-shape-heddle-reference-row-per-ecosystem-sidebar-sac-2026-note) independently record the identical reading (the row is track-agnostic labeled reference metadata; presentation rule 5 governs engine result rows), both verification cycles confirmed it as the only internally consistent reading under rule 2, and this spec consumes it as settled fact — the consolidated tables carry the Heddle row in controlled **and** idiomatic tables |
| Phase 1 report shape and artifacts | [Phase 1 README WI8](../phase-1-cross-stack-foundation/README.md#wi8--the-protocols-first-exercise-eight-workload-run-published): `index.md` + BenchmarkDotNet `-report-github.md`/`-report.csv`/`-report.html` triplet per suite; tables Mean/Error/StdDev + Ratio (Heddle baseline), `[MemoryDiagnoser]` allocation columns; **controlled track only** ([D15](../phase-1-cross-stack-foundation/README.md#d15--phase-1-ships-the-controlled-track-only-intra-net) — no intra-.NET idiomatic rows exist, so the consolidated idiomatic tables carry no .NET engine rows) |
| Phase 2 (Rust) report shape and artifacts | [Phase 2 D13](../phase-2-rust/README.md#d13--report-one-docsbenchmarksdate-directory-in-the-protocol-shape): two wall-time tables (Criterion mean + 95% CI, ns/render, Heddle ratio; rows Heddle/Askama/Tera), allocation sidebar (Tera baseline), Tera cold-parse figure, compilation-model disclosure; artifacts `criterion/<group>/<fn>/estimates.json` per cell, `criterion-console.txt`, `alloc-report.txt`, `gate-report.txt` |
| Phase 3 (JVM) report shape and artifacts | [Phase 3 D12](../phase-3-jvm/README.md#d12--report-format-protocol-shape-heddle-reference-row-per-ecosystem-sidebar-sac-2026-note): per-track tables (`Score` ns/op + `Error` 99.9% CI, Heddle ratio; role labels JTE performance/peer, Thymeleaf credibility), `gc.alloc.rate.norm` sidebar (Thymeleaf baseline), **no cold-cost figures** (Phase 3 D13), the verbatim SAC 2026 disclosed-limitations note; artifacts `jmh-result.json`, `jmh-log.txt`; possible Thymeleaf controlled-cell exclusions with evidence records |
| Phase 4 (JS) report shape and artifacts | [Phase 4 D15](../phase-4-js/README.md#d15--report-framing-heddle-row-ratio-anchor-side-note-placement-verified-download-figure) + D3/D12/D13: reach-not-fair-fight framing statement first in results; **time-only** (verbatim statement; no memory sidebar exists), cold-compile table (Handlebars baseline), Q4.2 side-note as its own `###` subsection outside ranking tables, `DEOPT-CHECK` posture, 5-run Windows stability procedure with published `stability-summary.md`; artifacts `js-controlled.txt/.json`, `js-idiomatic.txt/.json`, `js-cold-compile.txt/.json` |
| Phase 5 (Python) report shape and artifacts | [Phase 5 D13](../phase-5-python/README.md#d13--report-instantiation-q22--a-q62-honest-reporting-rules) + D9/D10/D11: mean ± std dev **and median** per row, µs/ms + ns/render + Heddle ratio, tracemalloc memory table (mean/median/std of `allocated`, `retained` secondary; Jinja2 baseline; verbatim method note), cold-compile section, stability-posture disclosure in the environment block, any pyperf instability warnings quoted next to affected rows; artifacts: five pyperf JSONs + `memory.json` |
| Phase 6 (Go) report shape and artifacts | [Phase 6 D11](../phase-6-go/README.md#d11--report-format-instantiation) + D1/D9/D10: benchstat `sec/op` + `±%`, per-suite stdlib surface labels (`text/template` raw / `html/template` encoded, Q6.1), allocs/op sidebar (stdlib baseline, per-suite surface), stdlib cold-parse sidebar, dedicated contextual-encoding section, quicktemplate conditional-stretch rows with dormancy framing if shipped; artifacts `bench-*.txt`, `benchstat-*.txt`; templ `composed-page` expected to pass under N3b |
| Within-ecosystem baselines for non-comparable metrics | [Phase 1 D13](../phase-1-cross-stack-foundation/README.md#d13--presentation-rules-q22--a-q62-with-the-allocation-baseline-pinned): the credibility pick — Tera, Thymeleaf, Handlebars, Jinja2, html/template (per-suite surface per Phase 6's recorded correction); intra-.NET keeps Heddle. The consolidated sidebars inherit these unchanged |
| Spec master index | [docs/spec/README.md](../../README.md) *(read 2026-07-21)* — carries **no** cross-stack-benchmarks initiative row yet; the row lands per the index-maintenance convention (WI6), the only edit outside this folder and it happens at **spec landing**, not at report publication |

## Design decisions

Every plan lean, pin, and spec-territory delegation for this phase, closed. Normative formats,
schemas, verbatim texts, and procedures live in [report-assembly.md](report-assembly.md); the
decisions below fix *what* and *why*.

### D1 — Go/no-go and the assembly-time manifest freeze (Q7.2)
- **Decision.** The report ships iff **both** hold at assembly time: (a) Phase 1's protocol run
  is published (there is no Heddle source otherwise), and (b) **at least one** of phases 2–6
  has a published protocol-conformant run (Q7.2's "non-empty subset"; with zero ecosystem runs
  there is nothing cross-stack to consolidate and the phase simply waits — it is never
  cancelled by subset size). No other quorum exists: a reach-only subset (JS and/or Python
  alone) ships as labeled reach evidence. The inclusion manifest is **frozen by procedure at
  assembly time** (WI1): enumerate every `docs/benchmarks/<date>/` directory; classify each as
  *pre-protocol history* (2026-07-11, 2026-07-18 — cited, never aggregated), *protocol run*
  (produced under the Phase 1 protocol by phases 1–6, identified by its environment block and
  owning-phase reproduce command), or *other report* (e.g. this phase's own prior edition, if
  any); for each ecosystem with ≥ 1 protocol run, aggregate **the newest published run**
  (older protocol runs are cited as history in the manifest, never mixed into the tables);
  record the freeze machine-readably in `sources.json` (schema in
  [report-assembly.md](report-assembly.md#sourcesjson-schema)).
- **Rationale.** Q7.2 resolution verbatim; the newest-run rule is the only selection rule that
  needs no judgement and matches the corrections-get-a-new-date immutability convention (a
  newer run *is* the owning phase's current word). Freezing by procedure closes the
  plan-flagged "concrete list" assumption honestly — at authoring time the list is empty
  (Assumed state).
- **Alternatives rejected.** A minimum compiled-peer quorum (Q7.2 option B — rejected by
  ruling); aggregating multiple runs per ecosystem side by side (invites cross-date
  comparison inside one ecosystem — a drift surface the manifest exists to control); freezing
  the manifest in this spec (impossible — no runs exist; would guarantee staleness).
- **Grounding.** [open-questions Q7.2](../../../plan/open-questions.md); Assumed state row 1;
  [metrics-protocol — publication format](../phase-1-cross-stack-foundation/metrics-protocol.md#publication-format)
  (immutability/corrections convention).

### D2 — Sole publication surface: one directory, four files, zero outside edits (Q7.1 as amended)
- **Decision.** The phase's entire published output is one new directory
  `docs/benchmarks/<yyyy-MM-dd>/` containing exactly four files:
  1. `index.md` — the consolidated report (D3);
  2. `sources.json` — the frozen machine-readable inclusion manifest (D1);
  3. `consolidate.py` — the assembly script (D13), committed **inside the directory** so the
     report is self-contained and reproducible without touching any other tree;
  4. `consolidated-tables.md` — the script's verbatim table output, the byte-reproducibility
     anchor (`--check`, D13).
  `<date>` is the assembly date; if that date's directory already exists (e.g. a source run
  published the same day), publication moves to the next calendar day — never a same-directory
  merge, never an edit to the existing directory. **At publication time no file outside the new
  directory is created or modified** — no docs-site page, no README comparison, no citation
  update, no `benchmarks/` tooling tree, satisfying the plan success criterion literally. The
  directory is immutable once published; any correction is a complete new dated directory that
  states what it corrects (protocol convention). The only outside edit this phase ever makes is
  the spec master-index row at **spec landing** (WI6) — sanctioned by the index-maintenance
  convention and prior to, and independent of, publication.
- **Rationale.** Q7.1 as amended is categorical ("the date-stamped directory alone, full
  stop"), and the plan's success criterion reads "no edit exists outside the new directory at
  all" — placing even the assembly tooling inside the directory is the only reading that
  cannot be argued to violate it, and it buys self-containment: a reader at the published
  commit reproduces the tables from the directory itself.
- **Alternatives rejected.** A `benchmarks/consolidate/` tooling tree (defensible as
  non-publication surface, but contradicts the success criterion's letter — not worth the
  argument for one small script); publishing the script nowhere and assembling by hand (see
  D13 — hand-copying hundreds of figures is the one step with no gate); editing this report's
  directory for corrections (barred by the protocol's immutability rule).
- **Grounding.** [open-questions Q7.1](../../../plan/open-questions.md) (amended resolution);
  plan §Success criteria items 1–2;
  [metrics-protocol — publication format](../phase-1-cross-stack-foundation/metrics-protocol.md#publication-format).

### D3 — Report document structure: a consolidated-report format, pinned section by section
- **Decision.** `index.md` follows the **consolidated-report format** pinned normatively in
  [report-assembly.md — index.md structure](report-assembly.md#indexmd--normative-structure):
  H1 `# Cross-stack consolidated report — <yyyy-MM-dd>`, then in order: intro (what this is,
  no-new-measurements statement, protocol links, gate description naming the amended
  N1–N5-including-N3b pipeline per the 2026-07-20 maintainer decision, Phase 1 D8);
  `## Inclusion manifest`; `## Source environments, drift, and reproduce commands`;
  `## How to read these tables` (dual-track meaning statements, statistic mapping, ratio
  anchor, ranking scope, counted-claims convention); `## Controlled track — wall time per
  render` and `## Idiomatic track — wall time per render` (D5); `## Workload-shape analysis`
  (eight subsections, D8); `## Findings — where Heddle wins, loses, and narrows` (D12);
  `## Per-ecosystem sidebars — not cross-comparable` (D7); `## Limitations` (D10); `## Files`.
  This deliberately **instantiates a sibling of, not the letter of, the protocol's
  run-publication format**: the protocol's `index.md` shape (single `## Environment` block,
  `## Results — what this run actually shows`) is defined for *measurement runs*, and this
  directory publishes no run — a single environment block would be a false record. The
  deviation is confined to structure; everything substantive is inherited unchanged: the
  directory convention, immutability, verbatim label texts, honest-reporting rules 1–6, track
  captions, Heddle row label format, and the encoded-suite caveat placement.
- **Rationale.** The plan requires exactly these content blocks (manifest, dual-track tables,
  sidebars, shape analysis, limitations, source-environment manifest, reproduce commands,
  no-universal-superiority statement); pinning their order and per-section required content is
  what makes WI4 executable without design decisions. The ordering puts everything a reader
  needs to *interpret* the tables (manifest, environments, how-to-read) before the tables, the
  analysis and findings after them, and the non-comparable material after the findings so the
  cross-comparable story is never visually adjacent to non-comparable numbers.
- **Alternatives rejected.** Forcing the run format verbatim (H1 "Benchmark run", one
  environment block — misdescribes a no-run publication); sidebars interleaved per-ecosystem
  into the track sections (puts allocation columns visually adjacent to cross-comparable
  tables — the exact juxtaposition risk the plan's risk table names); a multi-file report
  (one `index.md` is the established style; the consolidated narrative is one argument).
- **Grounding.** Plan §Design direction and §Success criteria;
  [metrics-protocol — publication format, honest-reporting rules](../phase-1-cross-stack-foundation/metrics-protocol.md#publication-format);
  [parity-contract-v2 — N3b](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline).

### D4 — Every figure is a verbatim excerpt of a published source table; the only arithmetic is unit conversion
- **Decision.** The **source of record for every number in this report is the published
  `index.md` of the owning source run** — the same published figures the per-ecosystem Heddle
  reference rows excerpt (Q2.2 = A), so no figure can diverge between this report and any
  per-ecosystem report by construction. The raw harness artifacts committed beside each source
  `index.md` (Phase 1 CSV triplets; Rust `estimates.json`; JVM `jmh-result.json`; JS mitata
  JSON; Python pyperf JSONs; Go benchstat text — per-phase extraction table in
  [report-assembly.md](report-assembly.md#figure-extraction-and-the-spot-check-layer)) are the
  **spot-check layer**, not a second source. Permitted transformations, closed list: (1) unit
  conversion to ns/render where a source table printed only a native unit (pure presentational
  arithmetic, mandated by the protocol's ns-normalization rule for Heddle-row tables); (2) a
  Heddle-anchored ratio computed as `engine ns ÷ Heddle reference ns` **only** where a source
  table did not already publish that ratio, flagged in the table's provenance note. Nothing
  else is ever computed — no re-derived statistics, no averages of averages, no geomeans. Each
  ecosystem's Heddle reference figures are taken from **the Phase 1 run that ecosystem's own
  published report cites** in its reference-row label; if the shipped subset cites more than
  one distinct Phase 1 run date, that is a drift finding handled by D11's rule R5 (disclosed
  in the manifest and in every affected shape-analysis subsection; per-ecosystem tables are
  unaffected because every ratio lives inside one ecosystem's table).
- **Rationale.** The plan's traceability criterion ("zero numbers exist that no source run
  published") is satisfiable only if the published tables are the sole source; parsing raw
  artifacts as the primary source would re-derive statistics six different ways and reopen the
  Q2.1 mapping this phase is barred from touching. The both-cite-the-same-run rule is the
  plan's own anti-divergence design, extended to the multi-Phase-1-run edge case.
- **Alternatives rejected.** Primary extraction from raw artifacts (six parsers, six chances
  to re-derive a statistic, and a published-table/consolidated-table divergence channel);
  re-measuring anything (barred — plan non-goal 1); recomputing ratios everywhere for
  uniformity (needless divergence risk against already-published ratio columns; copying wins).
- **Grounding.** Plan §Goal ("It recomputes nothing"), §Non-goals ("No new metrics and no
  metric re-derivation"), §Success criteria (traceability);
  [open-questions Q2.2](../../../plan/open-questions.md);
  [metrics-protocol — statistic mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21)
  (unit convention).

### D5 — Dual-track table shape: per workload, one sub-table per ecosystem; tracks never mixed
- **Decision.** Each track section contains **one `###` subsection per workload** (protocol
  order 1–8); each workload subsection contains **one table per included ecosystem** (order:
  .NET, Rust, JVM, JS, Python, Go — plan priority order with the .NET source first), never a
  single all-ecosystem table. Table rows: the Heddle reference row (label format
  `Heddle (reference — .NET 10, same machine, from <date> run)`, wall time only, dashes
  elsewhere) plus that ecosystem's engines only. Columns, closed set: harness-native point
  estimate with its harness-native dispersion (Q2.1 pair, printed as the source printed them),
  ns/render, ratio vs the Heddle row. **Nothing else** — no allocation, memory, or cold-cost
  column ever appears in a track table. Captions name the track and the evidence class:
  Rust/JVM/Go tables carry `— fair-fight evidence`, JS/Python tables carry
  `— reach/context evidence (not a fair fight)` (the phases' framing commitments, carried
  through). Go stdlib rows carry the per-suite surface label (`text/template` on raw
  workloads, `html/template` on encoded — Q6.1). The idiomatic section's .NET position states
  in one line that Phase 1 shipped controlled-only intra-.NET (Phase 1 D15), so idiomatic
  tables carry the Heddle reference row and ecosystem engines only. Excluded controlled cells
  print `excluded — documented evidence` linking the owning run's evidence record — never a
  dropped row, never a blank. Workload subsections containing encoded workloads (7–8) carry
  the verbatim confinement caveat immediately below their tables. Every table carries the
  pinned provenance note ([report-assembly.md — provenance format](report-assembly.md#table-shapes-captions-and-provenance)).
- **Rationale.** Per-workload grouping is what the shape analysis needs (the plan's "results
  of every engine from every shipped ecosystem, side by side" per workload); per-ecosystem
  sub-tables are what Q6.2 needs (no single table whose rows invite sorting non-Heddle engines
  across ecosystems). The Heddle row in every sub-table gives the one sanctioned cross-table
  reading — Heddle-anchored ratios — without ranking competitors against each other.
- **Alternatives rejected.** One combined table per workload with an ecosystem column
  (physically a cross-ecosystem leaderboard of non-Heddle engines the moment a reader sorts
  it — Q6.2's target); per-ecosystem top-level grouping with workloads inside (reproduces the
  per-ecosystem reports and buries the per-workload juxtaposition the plan demands); adding
  dispersion-normalized or significance columns (new derived metrics — barred).
- **Grounding.** [open-questions Q6.2, Q2.1](../../../plan/open-questions.md);
  [metrics-protocol — presentation rules 1–5, metric rule 4, label texts](../phase-1-cross-stack-foundation/metrics-protocol.md#presentation-rules-q22--a-q62);
  plan §Design direction (dual-track; degradation); Assumed state (both-tables Heddle row,
  settled).

### D6 — Ranking-scope enforcement (Q6.2) and the no-score rule, made checkable
- **Decision.** Enforced properties of the published report, each verified by the publication
  checklist ([report-assembly.md — checklist](report-assembly.md#publication-checklist)):
  (a) no table contains wall-time rows from two ecosystems' non-Heddle engines; (b) no prose
  sentence ranks or compares two non-Heddle engines from different ecosystems (Heddle-vs-X is
  permitted anywhere, wall-time-only); (c) no geomean, points total, medal count,
  cross-workload average, or any single aggregate score exists anywhere; (d) every summary
  statement in the findings section names its workload(s) — no workload-free "X is faster
  than Heddle" sentence; (e) no table or prose juxtaposes allocation/GC/memory/cold figures
  across runtimes (sidebars are per-ecosystem sections under their own H2, each labeled).
- **Rationale.** These are the plan's non-goals and success criteria converted into binary
  checks; the consolidated view is where the plan says the temptation is strongest, so the
  rules are enumerated rather than implied.
- **Alternatives rejected.** A "reader's digest" summary table of best-engine-per-workload
  (a medal table by another name — fails (c)/(d) in spirit); relegating the rules to prose
  guidance (unenforceable — the checklist makes them gates).
- **Grounding.** [open-questions Q6.2](../../../plan/open-questions.md); plan §Non-goals,
  §Success criteria, §Risks (summarization pressure).

### D7 — Per-ecosystem sidebars: verbatim excerpts of the source runs' non-comparable tables
- **Decision.** `## Per-ecosystem sidebars — not cross-comparable` contains one `###`
  subsection per included ecosystem (same order as D5), each opening with the protocol's
  verbatim allocation label (where allocation/memory data appears) and a citation line to its
  source report, then reproducing — values verbatim, never recomputed or re-baselined — the
  source run's non-comparable tables: **.NET** BenchmarkDotNet allocation columns (Heddle
  baseline — intra-.NET convention); **Rust** allocation-counter table (Tera baseline) + the
  Tera cold-parse figure + the compilation-model disclosure; **JVM** `gc.alloc.rate.norm`
  table (Thymeleaf baseline) — no cold section (Phase 3 D13 measured none); **JS** cold-compile
  table (Handlebars baseline) + the verbatim time-only statement (no memory data exists —
  Q4.1/Phase 4 D3); **Python** tracemalloc memory table with its verbatim method note (Jinja2
  baseline) + cold-compile table; **Go** allocs/op table (stdlib baseline, per-suite surface
  labels) + stdlib cold-parse figure (+ quicktemplate rows if its run shipped them). Sidebars
  never contain a Heddle row (Heddle appears in no ecosystem's non-comparable metrics — the
  reference row is wall-time-only by ruling) except the .NET sidebar, where Heddle is a
  measured in-process participant. Each sidebar ends with the source-report link as its
  "full detail" pointer.
- **Rationale.** Plan back-compat section verbatim ("restates their per-ecosystem-only data …
  only inside labeled non-comparable sidebars that cite back"); within-ecosystem baselines are
  Phase 1 D13's and are inherited, not re-decided; separate-H2 placement is the plan's own
  mitigation for the juxtaposition risk.
- **Alternatives rejected.** Summarizing sidebars to "representative rows" (an editorial
  selection this phase has no license to make — verbatim excerpts or links only); a unified
  "memory appendix" table with an ecosystem column (the forbidden juxtaposition in one
  artifact); omitting sidebars and linking only (the plan's success criterion requires each
  included ecosystem to *have* a sidebar here).
- **Grounding.** [metrics-protocol — metric rules 2–3 + allocation label](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules);
  [Phase 1 D13](../phase-1-cross-stack-foundation/README.md#d13--presentation-rules-q22--a-q62-with-the-allocation-baseline-pinned);
  phases 2–6 report decisions (Assumed state rows); plan §Success criteria (sidebar item).

### D8 — Workload-shape analysis: eight dimensions, pinned classifications, counted claims
- **Decision.** `## Workload-shape analysis` contains exactly eight `###` subsections, one per
  workload in protocol order, titled `### <dimension> — <workload-id>` using the dimensions of
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md#the-set-at-a-glance)
  (composition machinery / per-render overhead floor / bulk iteration / realistic mixture /
  branch dispatch / per-call composition / escaping at micro scale / escaping at bulk scale).
  Each subsection must state, per track, which engine **architecture classes** led, trailed,
  or converged and why that is consistent with or surprising given their designs — using the
  four classification tables pinned in
  [report-assembly.md — classifications](report-assembly.md#engine-classification-tables)
  (AOT-compiled/typed vs no-AOT-typed-codegen; escaping model compile-time-contextual /
  runtime-contextual / flat; logic-less vs expression-bearing; native vs managed-GC runtime).
  **Counted-claims rule, mechanical:** every architectural sentence carries the support tag
  `*(evidence: <engines>; <workload-ids>; <track>; n = <engine×workload data points>)*`;
  a claim with n ≤ 2 must open with "Thin evidence —" or be stated as narrowed/open; claims
  count only cross-stack cast engines plus Heddle (intra-.NET twins may be cited as intra-.NET
  context but never counted in n). Encoded-suite subsections (7–8) frame results strictly as
  **correlation between escaping model and whole-engine wall time**, never as an isolated cost
  of context analysis, and carry the confinement caveat adjacent (the caveat's own text states
  why the differentiating benefit of contextual encoding cannot appear here). Fair-fight vs
  reach labeling from D5 carries into the prose (a claim supported only by JS/Python evidence
  is labeled reach/context). Claims that would rest on an absent ecosystem are downgraded per
  D9's pre-written rules — never extrapolated. The Q4.2 Handlebars-vs-Handlebars.Net
  side-note may be **cited by link** (one sentence, wall-time framing, in the per-render-floor
  or reach-context prose) and is never re-tabulated here.
- **Rationale.** The plan makes this section the report's centerpiece and specifies the
  inversion of the workload table as its structure; pinning the classification tables and the
  support-tag format removes all authoring-time judgement about what counts as evidence, and
  makes the "counted, not vibed" criterion checkable by grep.
- **Alternatives rejected.** Organizing by architecture class instead of workload (loses the
  one-dimension-per-workload structure Phase 1 built and the plan names); free-form evidence
  citation (uncheckable); counting intra-.NET twins in n (they are one runtime's engines — the
  cross-language claims would inflate their support).
- **Grounding.** Plan §Design direction (centerpiece paragraph, verbatim requirements);
  [workloads.md — the set at a glance](../phase-1-cross-stack-foundation/workloads.md#the-set-at-a-glance);
  [open-questions Q4.2](../../../plan/open-questions.md);
  [metrics-protocol — encoded caveat text](../phase-1-cross-stack-foundation/metrics-protocol.md#required-label-texts-verbatim).

### D9 — Inclusion manifest and degradation machinery: absences, downgrades, excluded cells
- **Decision.** `## Inclusion manifest` opens the report body with one row per ecosystem
  (.NET, Rust, JVM, JS, Python, Go) in the pinned manifest format
  ([report-assembly.md — manifest format](report-assembly.md#inclusion-manifest-format)):
  status `included` (with run date, commit, engines, tracks, Heddle-reference run date) or
  `absent` (with reason, closed vocabulary: `phase cut` / `run not yet published`), plus a
  PHP/Ruby row with status `out of scope — program-level non-goal` linking the recorded
  rationale in [Phase 1's plan document](../../../plan/phase-1-cross-stack-foundation.md), and
  a note row recording that .NET participates controlled-track-only (Phase 1 D15). The
  manifest also lists every **contract-v2 excluded cell** across included runs (workload,
  engine, track, one-line reason, evidence link) — the same cells the D5 tables mark. The
  **claim-downgrade rules are pre-written per absent ecosystem** in
  [report-assembly.md — degradation rules](report-assembly.md#claim-downgrade-rules-per-absent-ecosystem)
  (Rust: closest-peer loss, native-runtime class emptied; JVM: second AOT peer and the only
  non-Heddle compile-time-contextual encoder lost; JS: only logic-less cross-stack engine
  lost, Q4.2 side-note unavailable; Python: reach evidence reduced; Go: the cast's only
  runtime contextual encoder lost — the compile-time-vs-runtime contextual comparison is
  replaced by a could-not-be-performed statement, with the compile-time-contextual-vs-flat
  reading continuing through JTE if the JVM shipped; compound rule: with zero AOT peers
  shipped, the compiled-thesis question is stated open) — WI4 applies them verbatim to
  whatever the frozen subset is.
- **Rationale.** Degradation-by-manifest is the plan's design commitment and its second risk
  row's mitigation; pre-writing the downgrade text per absence is what makes "the report
  overreaches anyway" structurally impossible rather than editorially avoided — the plan's Go
  and Python validation scenarios are satisfied by construction.
- **Alternatives rejected.** Deciding downgrades at authoring time against the actual subset
  (exactly the judgement call the spec must remove); listing absences only in prose (the
  manifest is the plan's named mechanism and Phase 8 consumes it); treating excluded cells as
  absences (they are present-with-evidence cells — the contract's category, kept distinct).
- **Grounding.** Plan §Design direction (degradation by manifest), §Validation scenarios
  (Python cut, Go cut, excluded cell);
  [parity-contract-v2 — exclusion policy](../phase-1-cross-stack-foundation/parity-contract-v2.md#exclusion-policy);
  [open-questions Q1.2, Q7.2](../../../plan/open-questions.md).

### D10 — Limitations: assembled from the source runs' own disclosed caveats, enumerated
- **Decision.** `## Limitations` carries forward, conditionally on inclusion, the following
  register (full texts and sourcing rules in
  [report-assembly.md — caveat register](report-assembly.md#limitations--the-caveat-register)):
  1. **Always:** the encoded-suite confinement caveat, verbatim (it also appears adjacent to
     every encoded table/subsection per protocol); the one-machine/dated-numbers statement;
     the cross-run drift disclosures from WI2; the statement that phases 2–6 gates reconcile
     whitespace via the program-wide N1–N5 pipeline including N3b and canonicalize entity
     spellings via N5 — i.e. what "byte-identical" precisely means here.
  2. **If JVM included:** the SAC 2026 disclosed-limitations note, verbatim as published in
     the JVM run (isolated JMH microbenchmark JIT-profile realism).
  3. **If JS included:** the time-only statement (mitata memory semantics undocumented), the
     deopt-check posture (`!`-marker investigation rule) and its run's outcome, and the
     Windows stability verification verdict (5-run RSD procedure) as published.
  4. **If Python included:** the pyperf Windows stability posture (no `system tune` on
     Windows; REALTIME priority + single-CPU affinity; dispersion-first reading) and any
     quoted pyperf instability warnings, as published.
  5. **If Rust included:** the Criterion Windows posture (no priority/affinity manipulation;
     10 s measurement; CI-half-width trigger disclosure if it fired), as published.
  6. **If Go included:** the benchstat variance posture (`-count=20`, `±%` reading, High
     priority, no affinity unless the ±5% fallback fired — then the pinning disclosure), as
     published; the quicktemplate dormancy disclosure if its rows shipped.
  **Sourcing rule:** for every caveat whose text the owning run published, this report copies
  the *published* text (the run is the authoritative instantiation of its spec); the register
  above is the completeness checklist, not a second source of wording. A caveat the register
  expects but the source run omitted is a source-report defect — escalated per D14, not
  silently repaired or invented here.
- **Rationale.** The plan's risk row ("caveats get lost in aggregation, laundering caveated
  numbers into clean-looking tables") is retired by making the register explicit and the
  publication checklist verify each conditional entry against the manifest; copying published
  text avoids a drift channel between spec-pinned and run-published wording.
- **Alternatives rejected.** Free assembly from memory at authoring time (the exact loss mode
  the risk names); restating caveats from the phase specs instead of the published runs (the
  runs may have recorded triggered variations — e.g. a fired affinity fallback — that the
  specs only anticipate).
- **Grounding.** Plan §Risks (caveat row), §Success criteria (limitations item); Phase 3 D12
  (SAC note), Phase 4 D3/D12/D13, Phase 5 D9, Phase 2 D9, Phase 6 D9/D10 (Assumed state rows);
  [metrics-protocol — label texts, honest-reporting rules](../phase-1-cross-stack-foundation/metrics-protocol.md#required-label-texts-verbatim).

### D11 — Source-environment manifest, per-source reproduce commands, drift rules, re-run requests
- **Decision.** `## Source environments, drift, and reproduce commands` contains: (a) one
  summary row per aggregated run — ecosystem, run date, directory link, commit, OS build,
  CPU, harness + version, runtime/toolchain line, recorded stability posture — transcribed
  from that run's environment block, with the full fenced block one link away (never
  re-typed); (b) each run's **reproduce command(s) copied verbatim** from its `index.md`
  intro; (c) the **drift table** produced by WI2's review, which compares the environment
  blocks pairwise under the pinned materiality rules
  ([report-assembly.md — drift review](report-assembly.md#drift-review-and-re-run-requests)):
  **R1** different CPU/physical machine → the run is non-conformant with Q1.6 and is
  **not aggregable** (manifest: absent, reason `run not protocol-conformant`); **R2** OS
  feature-update build difference (e.g. 26200 → 26300) or recorded firmware/BIOS/hardware
  configuration change → **material**: publication blocks until the owning phase re-runs
  **or** the maintainer waives in writing, the waiver and a caveat appearing next to every
  affected juxtaposition; **R3** OS servicing-build (UBR) difference within one feature
  build → not material, disclosed in the drift table; **R4** differing per-run stability
  postures (priority/affinity/power plan) → not material (each is that harness's recorded
  protocol posture), summarized in the table; **R5** two included runs citing different
  Phase 1 Heddle-reference run dates → material **for cross-ecosystem shape claims only**:
  disclosed in the manifest and in every affected shape subsection; per-ecosystem tables
  unaffected (D4). The **re-run-request procedure**: a triggered R1/R2/R5 finding appends an
  entry to `rerun-requests.md` in this spec folder (created only when first needed — date,
  evidence lines from the two environment blocks, owning phase, requested action,
  disposition); publication is blocked while any entry lacks a disposition
  (`re-run published <date>` or `waived by maintainer <date> + caveat text`). Phase 7 never
  measures anything either way.
- **Rationale.** Provenance-first aggregation is the plan's stated design; the pinned rules
  convert "materially drifted" from judgement into a table, and the request file keeps the
  remedy where the plan puts it — the owning phase's machinery, with the maintainer as the
  only waiver authority (most-reversible: a waiver is one recorded line; a silent adjustment
  is forbidden).
- **Alternatives rejected.** A single merged environment block (false record — the plan
  explicitly replaces it with a manifest of source environments); treating any environment
  difference as material (every pair of dated runs differs somewhere; the rules name what
  matters); Phase 7 re-running a drifted suite itself (barred — no measurements).
- **Grounding.** Plan §Design direction (provenance-first), §Risks row 1, §Validation
  scenarios (toolchain-change scenario);
  [open-questions Q1.6](../../../plan/open-questions.md);
  [metrics-protocol — machine and environment](../phase-1-cross-stack-foundation/metrics-protocol.md#machine-and-environment-q16).

### D12 — Findings section: Heddle-trails coverage, track-divergence findings, no-universal-superiority statement
- **Decision.** `## Findings — where Heddle wins, loses, and narrows` must, in prose with
  numbers: (a) name **every** workload × track × engine cell whose published ratio is < 1.00
  (an engine beating the Heddle reference) and every cell within measurement distance
  (ratio inside the cell's published dispersion of 1.00 — "narrows to measurement distance"),
  each with an account of what the result reveals about Heddle's design cost, as prominently
  as any win — the 2026-07-18 posture; the coverage list is emitted mechanically by
  `consolidate.py` (a `heddle-trails` checklist, an authoring aid — never published as a
  table) and the publication checklist verifies every listed cell is covered; (b) report
  notable controlled-vs-idiomatic divergence within an engine as findings (the cost of an
  engine's idiomatic style against its ceiling — plan design direction); (c) close with the
  **no-universal-superiority statement, verbatim** as pinned in
  [report-assembly.md — verbatim texts](report-assembly.md#verbatim-texts).
- **Rationale.** Honest-reporting rule 2 and two plan success criteria, made mechanical: the
  script-emitted coverage list removes the possibility of an unlisted loss; the verbatim
  statement satisfies the explicit-statement criterion checkably.
- **Alternatives rejected.** Table-only loss visibility (the criterion demands prose);
  defining "narrows" qualitatively (the dispersion-overlap definition uses only published
  numbers — no new statistics).
- **Grounding.** Plan §Success criteria (losses item, no-ranking item),
  [2026-07-18 report](../../../benchmarks/2026-07-18/index.md) (posture model);
  [metrics-protocol — honest-reporting rules](../phase-1-cross-stack-foundation/metrics-protocol.md#honest-reporting-rules).

### D13 — Assembly tooling: `consolidate.py`, stdlib-only, inside the published directory
- **Decision.** Assembly is **scripted, not manual**: `consolidate.py` — a single-file,
  CPython ≥ 3.12, **stdlib-only** script committed inside the published directory (D2) —
  reads `sources.json`, parses each included source run's published `index.md` wall-time and
  sidebar tables (the pinned per-phase table captions/labels are its lookup keys), regroups
  them into the D5/D7 consolidated tables, applies the two permitted transformations of D4
  where needed, and writes `consolidated-tables.md`, whose tables are pasted into `index.md`
  unmodified. Its `--check` mode regenerates the tables to a temporary buffer and fails
  non-zero unless (a) the regenerated output byte-equals the committed
  `consolidated-tables.md`, (b) every table block of `consolidated-tables.md` appears
  verbatim in the committed `index.md`, and (c) every run directory and commit `sources.json`
  cites exists. It also emits the D12 `heddle-trails` authoring checklist. Full CLI, parsing
  contract, and error surface:
  [report-assembly.md — consolidate.py contract](report-assembly.md#consolidatepy-contract).
  Python is chosen because the script must run on the protocol machine (CPython is already a
  phase 5 prerequisite there and freely installable if phase 5 was cut) **and** re-run under
  Phase 8's Ubuntu boot unchanged; stdlib-only keeps it dependency- and venv-free. If a source
  run's published table cannot be located by the pinned keys, the published run wins: the
  parser is adapted (published directories are immutable facts), and the adaptation is a
  reviewed change to this directory's script before publication.
- **Rationale.** Hand-transcribing several hundred figures across up to six runs is the one
  pipeline step with no gate — the exact argument Phase 2 used for `summarize.rs`, and Phase
  1's own deferred-item trigger for report tooling ("a second protocol run making the manual
  copy step demonstrably error-prone") has fired several times over by this phase.
  Script-generated tables also make the plan's spot-check criterion *total* for the
  published-table layer: every figure is mechanically identical to its source table, and the
  human spot check is reserved for the source-table-vs-raw-artifact layer (D4).
- **Alternatives rejected.** Manual assembly + checklist (no gate on the highest-volume copy
  step); a .NET console project (adds surface outside the directory — D2 — and to the
  solution); PowerShell (viable, but not portable to the Phase 8 Linux boot without a
  pwsh install; Python is already in the program's toolchain); parsing raw artifacts instead
  of published tables (rejected in D4).
- **Grounding.** [Phase 2 D13](../phase-2-rust/README.md#d13--report-one-docsbenchmarksdate-directory-in-the-protocol-shape)
  (summarize rationale); [Phase 1 deferred items](../phase-1-cross-stack-foundation/README.md#deferred-items)
  (report-tooling trigger); D2; plan §Success criteria (traceability, figure match).

### D14 — Defects found during assembly are escalated to the owning phase; nothing is patched locally
- **Decision.** Any defect this phase finds in a source artifact — a missing protocol-mandated
  element in a published `index.md` (e.g. an absent caveat, D10), a figure that fails the
  spot check against its raw artifact, a stale or defective run — is **escalated to the
  owning phase**: for a defective *published run*, the remedy is a re-run/correction under the
  owning phase's machinery publishing a new dated directory (this report then aggregates the
  new one per D1's newest-run rule); for spec-level text pressure, an entry in the cross-spec
  amendments ledger
  ([records.md — ledger](../../records.md#cross-spec-amendments-ledger), mechanism per
  [spec-conventions — amendments](../../common/spec-conventions.md#amendments-during-implementation)).
  Phase 7 never edits, re-measures, or compensates for a source artifact. This spec's own
  authoring pass surfaced **no** upstream errata: the one interpretive question it depends on
  (Heddle row in both track tables) is already settled program-wide (Assumed state), and the
  N3b maintainer ruling (2026-07-20, Phase 1 D8) is already folded into the Phase 1 contract text.
- **Rationale.** Plan non-goals 1–2 verbatim; the ledger is the repo's sanctioned amendment
  channel; recording "no errata filed" closes the question of what this spec owes the ledger.
- **Alternatives rejected.** Local compensation (silent adjustment — forbidden by the plan's
  drift rule and the immutability convention); holding publication for minor upstream wording
  defects the maintainer declines to fix (the maintainer waiver path in D11 generalizes:
  disclosed caveat, publication proceeds).
- **Grounding.** Plan §Non-goals, §Risks row 1;
  [spec-conventions — amendments](../../common/spec-conventions.md#amendments-during-implementation).

## Implementation plan

Ordered. WI1–WI5 execute at assembly time (after Phase 1's run and ≥ 1 ecosystem run are
published — D1); WI6 executes when this spec folder lands. All publication files live in the
one new `docs/benchmarks/<yyyy-MM-dd>/` directory (D2).

### WI1 — Freeze the inclusion manifest (`sources.json`)
- **Files.** New: `docs/benchmarks/<date>/sources.json`.
- **Change.** Execute D1's enumeration/classification procedure over `docs/benchmarks/`;
  record every ecosystem's status, newest protocol run (date, commit from its environment
  block, engines, tracks, Heddle-reference run date, artifact list, excluded cells with
  evidence links), every absence with its closed-vocabulary reason, and the PHP/Ruby
  out-of-scope entry — schema per
  [report-assembly.md — sources.json](report-assembly.md#sourcesjson-schema).
- **Done when.** `sources.json` validates against the schema (checked by
  `consolidate.py --check`); every cited directory and commit exists; every ecosystem
  (.NET, Rust, JVM, JS, Python, Go, plus the PHP/Ruby entry) has exactly one entry; the D1
  go/no-go precondition is recorded as satisfied.

### WI2 — Drift review
- **Files.** New (conditional): `docs/spec/cross-stack-benchmarks/phase-7-consolidated-report/rerun-requests.md`
  (only if an R1/R2/R5 finding triggers).
- **Change.** Compare all included runs' environment blocks pairwise under D11's rules
  R1–R5; produce the drift table for the report; file re-run requests for material findings
  and hold publication until each has a disposition.
- **Done when.** The drift table covers every pair of included runs with a per-rule verdict;
  zero open (disposition-less) entries exist in `rerun-requests.md`; every R2/R5 waiver has
  its caveat text written and placed.

### WI3 — `consolidate.py` and `consolidated-tables.md`
- **Files.** New: `docs/benchmarks/<date>/consolidate.py`,
  `docs/benchmarks/<date>/consolidated-tables.md`.
- **Change.** Implement the script per
  [report-assembly.md — consolidate.py contract](report-assembly.md#consolidatepy-contract)
  (source-table parsing keyed on the phases' pinned captions/labels, D5/D7 table emission,
  D4 transformations with per-cell flags, exclusion/absence handling, `heddle-trails`
  checklist, `--check` mode); run it; commit its output.
- **Done when.** `python consolidate.py` succeeds against the frozen `sources.json` and every
  included run; re-running is byte-stable; `--check` exits 0 against the committed output;
  deliberately editing one committed figure makes `--check` exit non-zero naming the cell
  (spot check, then revert); every excluded cell and absent ecosystem from `sources.json`
  appears in the output as specified (marked cell / omitted table with manifest reference).

### WI4 — Author `index.md`
- **Files.** New: `docs/benchmarks/<date>/index.md`.
- **Change.** Author the report in D3's exact section order: paste the generated tables
  unmodified; write the manifest (D9), environments/drift/reproduce section (D11),
  how-to-read section, workload-shape analysis applying D8's classification tables,
  counted-claims tags, and D9's downgrade rules for the actual absent set; findings per D12
  covering the full `heddle-trails` list; sidebars per D7; limitations per D10's register;
  files section listing the four directory files and linking every source run.
- **Done when.** Every section of
  [report-assembly.md — index.md structure](report-assembly.md#indexmd--normative-structure)
  is present in order with its required content; `consolidate.py --check` still exits 0
  (tables verbatim in `index.md`); every counted-claims tag parses under the pinned format;
  every absent-ecosystem downgrade rule for the actual subset is applied verbatim.

### WI5 — Verification pass and publication
- **Files.** None new (the four D2 files are final after this pass).
- **Change.** Execute the figure-match **spot-check procedure**
  ([report-assembly.md — spot check](report-assembly.md#figure-extraction-and-the-spot-check-layer)):
  for every included ecosystem × track, verify the deterministic cell set (first and last
  workload row per engine) from the published source `index.md` against the **raw harness
  artifact** per the extraction table; then walk the full
  [publication checklist](report-assembly.md#publication-checklist) (C1–C16, covering every
  plan success criterion). Publish by committing the directory.
- **Done when.** All spot-checked cells match exactly (any mismatch → D14 escalation, no
  publication); every checklist item is checked; the commit touches only
  `docs/benchmarks/<date>/` (plus, if triggered, this folder's `rerun-requests.md` in a prior
  commit); `git status` confirms no other path changed at the publication commit.

### WI6 — Spec master-index row (spec landing)
- **Files.** Changed: [docs/spec/README.md](../../README.md) (initiative row per the
  index-maintenance convention — the only edit outside this folder, sanctioned by the
  convention and executed when this spec folder lands, not at publication).
- **Done when.** The master index reaches this folder's two documents; the row's one-line
  summary names the phase's most consequential decisions (sole-surface directory, verbatim
  excerpts, scripted assembly).

## Public API / contract

No code API of any kind is created: `consolidate.py` is a private assembly tool shipped as a
reproducibility artifact inside the published directory. No .NET, Rust, JVM, JS, Python, or Go
source outside that directory is added or changed; no package is published. Thread-safety:
n/a (a single-threaded script run by hand).

Artifact-shaped contract:

| Artifact | Direction | Consumers | Normative definition |
|---|---|---|---|
| `docs/benchmarks/<date>/` consolidated report (4 files, D2) | produced | readers; [Phase 8 — linux-crosscheck](../../../plan/phase-8-linux-crosscheck.md) (validation targets) | [report-assembly.md](report-assembly.md) |
| Published phase 1–6 run directories | consumed, read-only | this phase | each owning phase's spec (Assumed state rows) |
| `sources.json` | produced | `consolidate.py`; Phase 8; readers | [report-assembly.md — schema](report-assembly.md#sourcesjson-schema) |
| `rerun-requests.md` (conditional) | produced | owning phases; maintainer | D11 |

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — this phase changes no compiler, grammar, or engine
surface; the [diagnostic registry](../../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
is untouched. The phase's own error surface is `consolidate.py`'s, all host-side, every failure
fatal (exit ≠ 0, nothing written or check failed):

| Surface | Exit | Message shape | Trigger |
|---|---|---|---|
| Manifest validation | 1 | `sources.json: <path>: <schema violation>` | missing/extra field, unknown status or reason vocabulary |
| Source resolution | 1 | `source run <date> (<ecosystem>): directory/commit not found` | cited directory absent or commit unknown to git |
| Table lookup | 1 | `source run <date> (<ecosystem>): no <track> wall-time table matching pinned caption keys` | published table not found by the pinned keys (→ parser adaptation per D13, or D14 escalation) |
| Cell extraction | 1 | `source run <date> (<ecosystem>): <workload>/<engine>: cell missing or unparsable: <detail>` | row/column absent, or a non-excluded cell empty |
| Exclusion consistency | 1 | `<workload>/<engine>/<track>: sources.json exclusion has no matching source-table marker (or vice versa)` | manifest and source report disagree about an excluded cell |
| `--check` table drift | 1 | `consolidated-tables.md is stale: first differing line <n>` / `table "<caption>" not found verbatim in index.md` | committed output ≠ regenerated, or index.md paste drifted |
| Heddle-reference mismatch | 1 | `<ecosystem>: Heddle reference run <date-a> ≠ sources.json heddleReferenceRun <date-b>` | source report's reference-row label disagrees with the frozen manifest (→ WI1 fix or D14) |

## Testing plan

**TDD verdict.** No shipped library code is touched, so no xUnit/pytest suite is added; the
executable specification is `consolidate.py --check` plus the publication checklist — the same
gates-are-the-spec verdict every phase of this program uses. `--check` is red by construction
until WI3/WI4 complete (no committed tables, then no matching `index.md`), and green is a
publication precondition.

**Named checks.**
- `python consolidate.py` — deterministic assembly; byte-stable re-run (WI3 done-when).
- `python consolidate.py --check` — the three-way consistency gate (regenerated ==
  `consolidated-tables.md` == tables embedded in `index.md`; sources resolve).
- Negative check: a one-character edit to any committed figure flips `--check` to a failure
  naming the cell (executed once in WI3, then reverted).
- Spot check (WI5): deterministic cell set verified against **raw harness artifacts** per the
  [extraction table](report-assembly.md#figure-extraction-and-the-spot-check-layer) — the
  layer `--check` cannot cover.
- [Publication checklist](report-assembly.md#publication-checklist) C1–C16 — one item per plan
  success criterion plus the D6 enforcement properties; every item binary.

**Regression gate (at the publication commit).**
1. `python consolidate.py --check` in the new directory — exit 0.
2. `git status` — the commit touches only `docs/benchmarks/<date>/`.
3. Every relative link in `index.md` resolves (source runs, evidence records, plan/spec
   documents).
4. Grep gates for D6: no `geomean`/aggregate-score construct; every wall-time table's rows are
   one ecosystem's engines + the Heddle row; allocation/memory/cold figures appear only under
   the sidebars H2 and each sidebar carries the verbatim label.
5. The counted-claims tags all parse (`*(evidence: …; n = <int>)*`) and every `n ≤ 2` claim
   carries the thin-evidence wording.

## Back-compat and migration

- **Purely additive.** One new immutable directory; no existing file under `docs/benchmarks/`
  or anywhere else changes at publication (D2; regression-gate step 2 proves it). No breaking
  window is engaged
  ([D2 — breaking windows](../../common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows)).
- **Existing published reports** are newly contextualized, never superseded: the consolidated
  report links the per-ecosystem and intra-.NET runs as sources; the pre-protocol 2026-07-11 /
  2026-07-18 reports are cited as history and never aggregated.
- **Per-ecosystem Heddle reference rows vs this report:** cannot diverge — both are verbatim
  excerpts of the same published Phase 1 figures (D4), which is the plan's own
  double-publication mitigation.
- **Cutting this phase** loses only the consolidated narrative; per-ecosystem reports stand
  alone unchanged. **Phase 8** consumes this report if it shipped and runs regardless.

## Performance considerations

None material: this phase executes no benchmark and touches no hot path. `consolidate.py`
parses a handful of Markdown/JSON files in well under a second; it runs on the author's
machine, never inside any measured process, and its runtime has no bearing on any published
number. The one performance-adjacent obligation is negative — the phase must not perturb
source artifacts — and is structural: everything consumed is read-only committed history.

## Standards compliance

- **DRY:** every figure, caveat text, and environment line exists once at its source (the
  published runs) and is excerpted or linked, never re-derived — the whole phase is a DRY
  exercise; the downgrade rules, classification tables, and verbatim texts exist once in
  [report-assembly.md](report-assembly.md) and are applied, not restated, in `index.md`.
- **YAGNI:** no generic multi-report framework, no HTML rendering, no artifact parsers beyond
  the published-table layer (raw artifacts are a manual spot-check layer), no automation of
  the prose sections; the script exists only because the copy step is the one ungated step.
- **Open/closed:** a future ecosystem or a re-shipped run changes `sources.json` and yields a
  **new** dated directory; the script and format absorb it with zero structural change.
- **Deliberate non-abstraction:** per-phase table-lookup keys are listed literally per
  ecosystem rather than generalized — a failing lookup must name its phase at a glance.

## Deferred items / non-goals

| Item | Trigger |
|---|---|
| Aggregating a later-shipped ecosystem (e.g. Python ships after this report publishes) | The owning phase's run publishing → a **new** consolidated directory is assembled (this one is immutable); same spec, new `sources.json` |
| Machine-parsing raw harness artifacts as the primary figure source | A recurring spot-check failure showing published source tables cannot be trusted (would first be a D14 escalation against the owning phases) |
| Any docs-site page, README comparison, or other citation surface | A user re-ruling amending Q7.1 (currently barred, full stop) |
| A cross-runtime cold-cost or memory appendix | A ratified Phase 1 protocol amendment (Q1.3 and the metrics ruling currently forbid it) |
| Linux consolidated report | Phase 8 owns all Linux publication; its numbers are never merged here (Q5.2/Q1.6) |
| Intra-.NET idiomatic rows in the idiomatic tables | Phase 1 D15's trigger (a decision to present .NET as an idiomatic-track ecosystem row) — until then the idiomatic tables carry the Heddle reference row and phases 2–6 engines only |

## External references

- Marr, Daloze & Mössenböck, *Cross-Language Compiler Benchmarking: Are We Fast Yet?* (DLS
  2016) — the single-consolidated-account precedent: <https://github.com/smarr/are-we-fast-yet>
- TechEmpower FrameworkBenchmarks — single results-round precedent:
  <https://github.com/TechEmpower/FrameworkBenchmarks>
- Schiavio, Bulej & Binder, *Misleading Microbenchmarks on the JVM* (ACM SAC 2026) — the JVM
  caveat carried in D10: <https://arxiv.org/abs/2605.23570>
- Harness documentation anchoring the Q2.1 statistic pairs this report reprints:
  BenchmarkDotNet <https://benchmarkdotnet.org/>; Criterion.rs
  <https://bheisler.github.io/criterion.rs/book/analysis.html>; JMH
  <https://github.com/openjdk/jmh>; mitata <https://github.com/evanwashere/mitata>; pyperf
  <https://pyperf.readthedocs.io/>; benchstat
  <https://pkg.go.dev/golang.org/x/perf/cmd/benchstat>
- Phase 1 spec documents (binding): [README](../phase-1-cross-stack-foundation/README.md),
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md),
  [parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  [golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md),
  [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md)
- Phase 2–6 specs (binding, read-only): [Rust](../phase-2-rust/README.md),
  [JVM](../phase-3-jvm/README.md), [JS](../phase-4-js/README.md),
  [Python](../phase-5-python/README.md), [Go](../phase-6-go/README.md)
- Published report style models: [2026-07-18](../../../benchmarks/2026-07-18/index.md),
  [2026-07-11](../../../benchmarks/2026-07-11/index.md)
- Repo conventions bound by this spec:
  [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [coding-standards](../../common/coding-standards.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md)
