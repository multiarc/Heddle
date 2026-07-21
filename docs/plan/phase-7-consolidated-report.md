# Phase 7 — consolidated-report

## Header

- **Status:** in progress
- **Goal (one line):** The single consolidated cross-stack report — every shipped ecosystem's
  protocol-conformant results juxtaposed on wall time per render, dual-track, with the
  workload-shape strengths-and-weaknesses analysis — published under `docs/benchmarks/<date>/` in
  the repo's established honest style.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) (metrics
  and publication protocol, plus its intra-.NET eight-workload protocol run as the Heddle/.NET
  source rows) and **any shipped subset of phases 2–6** ([Rust](phase-2-rust.md),
  [JVM](phase-3-jvm.md), [JS](phase-4-js.md), [Python](phase-5-python.md), [Go](phase-6-go.md) —
  each individually cuttable; this phase consumes whatever subset exists).
- **Changes an externally-visible contract:** no — the deliverable is one additive, immutable
  date-stamped report directory; no corpus, contract, protocol, engine, or prior report is
  modified.

## Goal

This phase delivers what the whole program was built toward and what no per-ecosystem phase is
allowed to deliver: the **complete consolidated cross-language comparison**. Under the Phase 1
protocol, wall time per render is the only number that may be compared across runtimes. Q2.2
resolved to option A, so each per-ecosystem report (phases 2–6) already carries a labeled,
wall-time-only Heddle reference row drawn from the Phase 1 protocol run; this report is where those
same published numbers are assembled into the one *complete* cross-language juxtaposition — every
shipped engine's Heddle-anchored comparison, grouped per ecosystem, side by side — rather than the
per-ecosystem excerpts. It recomputes nothing: the
reference rows and this report cite the same source runs, so no figure can diverge between them.

The report is a narrative, not a leaderboard. It presents, for each of the eight Phase 1
workloads and each fairness track (controlled and idiomatic, never mixed in one table), the
same-machine wall-time-per-render results of every engine from every shipped ecosystem, sourced
exclusively from the already-published protocol-conformant runs — this phase performs **no new
measurements**. Around those tables it delivers the analysis the user asked for and that only the
consolidated view can support: **which workload shapes favor which engine architecture**. The
program's cast was chosen to make that question answerable — three AOT-compiled/typed peers
(Askama, JTE, templ) against Heddle test whether the compiled-engine result generalizes across
toolchains or is a .NET artifact; the seven engines with no ahead-of-time typed-codegen step —
runtime interpreters (Tera, Thymeleaf, html/template) and compilers to untyped host code or
bytecode (Handlebars, Eta, Jinja2, Mako) — map where runtime template processing narrows on
bulk-amortized shapes versus where per-render overhead floors dominate; and the encoded suite's
escaping-model split
(compile-time contextual: Heddle and JTE; runtime contextual: html/template; flat escapers: the
rest) gets its only cross-language reading here — framed as correlation between escaping model and
whole-engine wall time, never as an isolated cost of context analysis, since the confined-context
encoded workloads are exactly the ones where every encoder family emits equivalent output (the
Phase 1 constraint that makes the byte gate passable) — and, by the same constraint's disclosed
caveat, the ones where contextual encoding's differentiating benefit cannot appear, which the
analysis states wherever it presents encoded-suite results, per the Phase 1
protocol. The analysis must be as
articulate about Heddle's weaknesses as its strengths: every workload where Heddle trails or an
engine narrows to measurement distance is reported as prominently as any win, in the posture the
[2026-07-18 report](../benchmarks/2026-07-18/index.md) already models intra-.NET (an allocation
inversion and an ~8% time gap led that report's findings). Where Heddle loses, the report says
what that reveals about the design's cost — not just that it happened.

Because phases 2–6 are individually cuttable, the report is designed to ship over **any subset**:
it opens with an inclusion manifest enumerating every ecosystem present and every ecosystem
absent with the reason (phase cut, run not yet published, or contract-v2 cell exclusions), and
its architecture claims degrade explicitly with the evidence — fewer AOT data points means the
generalization question is stated as narrowed or open, never extrapolated.

## Non-goals / scope boundary

- **No new measurements.** This phase aggregates the published, protocol-conformant runs of
  Phase 1 (the intra-.NET eight-workload protocol run) and phases 2–6. If a source run is
  missing, stale, or defective, the remedy is a re-run under the owning phase's machinery; Phase
  7 never runs a benchmark.
- **No edits to any existing artifact.** Published reports stay immutable; the golden corpus,
  parity contract v2, the metrics protocol, and all per-ecosystem harnesses are untouched. The
  pre-protocol reports ([2026-07-11](../benchmarks/2026-07-11/index.md),
  [2026-07-18](../benchmarks/2026-07-18/index.md)) are cited as history and are **not**
  aggregated — only runs produced under the Phase 1 protocol are eligible rows.
- **No marketing-style ranking.** No overall-winner declaration, no single aggregate score or
  cross-workload geomean leaderboard, and no ranking table that is not accompanied by
  workload-shape context. This is the report-level enforcement of the repo's
  no-universal-superiority posture, stated here as a non-goal because the consolidated view is
  where the temptation to summarize into a headline is strongest. Per Q6.2 this also means **no
  global cross-ecosystem leaderboard of non-Heddle engines**: Heddle may be compared and ranked
  against any engine wall-time-only, but engine-vs-engine rankings among the competitors exist only
  within an ecosystem — the consolidated view groups its Heddle-anchored comparisons per ecosystem
  and never ranks, say, Askama against JTE against templ.
- **No cross-runtime allocation/GC comparison** — the standing metrics ruling, restated because
  this is the one document where a reader could physically place a .NET allocation column next
  to a JVM one. Allocation/GC data appears only as per-ecosystem sidebars, each explicitly
  labeled not cross-comparable.
- **No new metrics and no metric re-derivation.** Every figure is a figure some source run
  already published (under the Phase 2 Q2.1 statistic mapping); this phase computes nothing
  beyond ratios against its declared baseline.
- **No re-opening of resolved cross-phase decisions.** The per-ecosystem-vs-consolidated
  juxtaposition question (Q2.2 = option A), the ratio-baseline / presentation rule (Q6.2), the
  statistic mapping (Q2.1), and the memory-metric decisions (Q4.1, Q5.1) were resolved in their
  owning phases; this phase consumes those resolutions and does not revisit them.
- **PHP and Ruby remain program-level non-goals** (standing ruling; rationale recorded in
  [Phase 1](phase-1-cross-stack-foundation.md)) — the manifest lists them as out-of-scope, not
  as absences.

## Design direction

**One omnibus consolidated report, not per-ecosystem-only and not a matrix of pairwise pages.**
Three alternatives were weighed. (a) *Per-ecosystem-only* — skip Phase 7, let readers assemble
the picture themselves: rejected because the program's central questions (does the compiled
thesis generalize beyond .NET; how do engines with different escaping models compare across
runtimes; which shapes
favor which architecture) are only answerable with all ecosystems side by side, and leaving the
assembly to readers guarantees exactly the unlabeled cross-runtime comparisons the protocol
exists to prevent. (b) *Pairwise head-to-head pages* (Heddle-vs-Askama, Heddle-vs-JTE, …):
rejected because it fragments the shape analysis, multiplies publication surface, and structurally
biases toward Heddle-centric framing. (c) *One consolidated narrative* — chosen: it is the
single place the protocol's one cross-comparable number is exercised in full, it can hold the
good-and-bad analysis in one argument, and it matches the precedent that credible cross-language
methodology publishes one comparative account of many implementations (Are-We-Fast-Yet's single
cross-language results narrative; TechEmpower's single results round) rather than scattered
fragments.

**Dual-track presentation, tracks never mixed.** The controlled track (byte-identical,
equivalently-authored — "how fast is the engine") and the idiomatic track (functional-equivalence
— "how fast is the practitioner experience") answer different questions by standing ruling, so
the consolidated report presents them as parallel views with separate tables and a short
explanation of what each may and may not be used to claim. A controlled number never appears in
an idiomatic table or vice versa; divergence between an engine's two tracks is itself reported as
a finding (it measures how much the engine's idiomatic style costs against its ceiling).

**Provenance-first aggregation.** Every row cites its source: the date-stamped run directory,
commit, and environment block it came from. The report's own environment section is a *manifest
of source environments* rather than a single block, because its numbers were produced across
multiple dated runs on the one protocol machine (Phase 1 Q1.6). Cross-run drift is disclosed, not
hidden: where source runs are separated by toolchain or OS changes on the protocol machine, the
affected juxtaposition carries a caveat, and a materially-drifted source run is grounds to
request a re-run from the owning phase before publication — never grounds for silent adjustment.
Heddle is the cross-stack wall-time ratio baseline, consistent with every published intra-.NET
report and with the per-ecosystem Heddle reference rows the ecosystem reports carry (Q2.2 = option
A, Q6.2 — the wall-time ratio column anchors to that Heddle row program-wide); within-ecosystem
sidebar tables for the non-comparable metrics keep their within-ecosystem baseline convention
(Q6.2), so the sidebars remain consistent with the per-ecosystem reports they cite. Non-Heddle
engines are never ranked across ecosystems.

**The strengths-and-weaknesses analysis is the report's centerpiece, organized by workload
dimension.** Phase 1 built the workload set so each workload owns one variability dimension;
the consolidated analysis inverts that table — for each dimension (composition machinery,
per-render overhead floor, bulk iteration, mixed realistic pages, branching, per-call
composition cost, escaping at micro scale, escaping at bulk scale), it states which engine
architectures led, trailed, or converged, and why that is consistent with (or surprising given)
their designs: AOT-compiled/typed vs engines without ahead-of-time typed codegen; managed-GC vs
native; contextual vs
flat encoding; logic-less vs expression-bearing template languages. The intra-.NET evidence
already demonstrates the pattern this section exists to surface — a compiled engine winning
composition by 2.0x while an opcode-compiled logic-less engine allocates 0.46x on trivial
substitution and closes to ~8% on a pure loop — and the consolidated view tests whether such
patterns hold across runtimes. Claims are counted, not vibed: each architectural statement names
the workloads and engines behind it, and where the shipped subset leaves a claim resting on one
or two data points, the report says the evidence is thin instead of generalizing. The fair-fight
framing hierarchy established by phases 2–6 carries through: Rust/JVM/Go compiled peers are
fair-fight evidence, JS/Python results are reach-and-context evidence, each labeled as such.

**Degradation by manifest.** Shipping with a subset means: every included ecosystem gets its
rows and its allocation sidebar; every absent phase-2–6 ecosystem gets a manifest line with the
reason; every claim that depended on an absent ecosystem is explicitly downgraded (e.g. cutting
Go forfeits the cast's only runtime contextual encoder, so the compile-time-vs-runtime side of
the contextual-encoding analysis is replaced by a statement that it could not be performed and
why — the caveat Phase 6 itself records — while the compile-time-contextual-vs-flat reading
continues through JTE if Phase 3 shipped). Contract-v2 excluded cells (e.g. a Thymeleaf
controlled-track exclusion under Phase 1
Q1.2) are carried through as visible marked cells with their documented reasons, never dropped
rows.

**Reproducibility posture, inherited and restated.** Date-stamped directory, single recorded
machine, per-source-run reproduce-it-yourself commands, hardware- and date-specific framing, no
universal-superiority claims — the same posture as every published report, applied to the one
document most likely to be quoted out of context.

## Dependencies & ordering

- **Depends on Phase 1, fully:** the metrics/publication protocol (what may be compared, and
  how it is published), the machine ruling (Q1.6 — all aggregated runs must come from the one
  protocol machine), the cold-cost reporting ruling (Q1.3 — per its resolution, cold parse/compile
  never becomes a cross-language column here), and Phase 1's intra-.NET eight-workload protocol
  run, which supplies the Heddle baseline rows and the .NET twins' rows.
- **Depends on the shipped subset of phases 2–6:** each shipped phase contributes its
  ecosystem's rows and its per-ecosystem sidebar. No phase 2–6 is individually required; at
  least Phase 1's run must exist for the report to have a subject (see Q7.2 for the quorum
  question below).
- **Binding cross-phase resolutions consumed here (resolved in their owning phases, referenced
  not duplicated):** **Q2.1** (which harness statistic is "wall time per render" — blocks every
  comparison column in this report); **Q2.2 = option A** (every per-ecosystem report carries a
  labeled, wall-time-only Heddle reference row, so this report cites those shared source runs and
  recomputes nothing — nothing is double-published because the ecosystem rows are excerpts of the
  same numbers this report assembles in full); **Q4.1** and **Q4.2** (whether the JS sidebar has
  memory columns; the Handlebars/Handlebars.Net side-note is in scope and may be referenced);
  **Q5.1** (which Python memory metric the sidebar ingests); **Q6.2** (the program-wide
  presentation rule: the wall-time ratio column anchors to the Heddle reference row, non-Heddle
  engines are never ranked across ecosystems, and non-comparable-metric sidebars keep their
  within-ecosystem baseline).
- **Internal ordering:** inclusion manifest frozen (which runs are in, with provenance) →
  aggregation rules instantiated from the resolved cross-phase questions → dual-track
  comparison tables assembled with per-row source citations → workload-shape analysis authored
  → limitations section assembled from the source runs' own disclosed caveats → publication as
  a date-stamped directory.
- **Unblocks:** [Phase 8 — linux-crosscheck](phase-8-linux-crosscheck.md), the program's
  sequenced-last Linux cross-check, which consumes any shipped subset of phases 2–7 and — where
  this report shipped — takes its consolidated findings as validation targets. Phase 8 is not a
  hard dependency in either direction: it runs over the shipped subset whether or not Phase 7 is
  in it, and, being individually cuttable by design, it is no part of this phase's done-ness.
  Phase 7's publication completes the Windows measurement-and-reporting program for whatever
  subset shipped; Phase 8 remains the program's separate, optional final act.

## Back-compat / impact

- **Existing published reports:** untouched and newly contextualized. The consolidated report
  links the per-ecosystem and intra-.NET reports as its sources; it supersedes none of them and
  restates their per-ecosystem-only data (allocations, cold costs) only inside labeled
  non-comparable sidebars that cite back.
- **Pre-protocol history:** the 2026-07-11 and 2026-07-18 reports remain the intra-.NET
  historical record; the consolidated report cites them as motivation and precedent but
  aggregates only protocol-conformant runs, and says so.
- **No citation-update surface:** `docs/language-assessment.md` was removed from the repo (user
  cleanup) and no longer exists, so the link-only citation update Q7.1 originally contemplated has
  no target. Per Q7.1 as amended (2026-07-20), this phase's deliverable is the date-stamped
  directory alone, full stop; there is no assessment document to cite into, and any docs-site page
  or README comparison update remains out of scope for this phase.
- **New surface:** exactly one new `docs/benchmarks/<date>/` directory, and nothing else. Nothing
  existing breaks at any point; cutting this phase leaves the per-ecosystem reports standing alone,
  losing only the consolidated narrative.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Cross-run environmental drift on the protocol machine (source runs published weeks apart; toolchain/OS updates between them) quietly undermines "same machine" comparability | Provenance-first design: per-source-run environment manifest, drift disclosed next to the affected juxtaposition; materially-drifted runs trigger a re-run request to the owning phase before publication — never a silent adjustment, and never a Phase 7 measurement | M |
| The shipped subset guts a headline claim (e.g. Rust cut ⇒ no closest architectural peer; Go cut ⇒ no runtime contextual encoder) and the report overreaches anyway | Degradation-by-manifest is a design commitment: absent ecosystems enumerated with reasons, dependent claims explicitly downgraded to "narrowed" or "unanswered"; Q7.2 sets no minimum quorum — the report ships over any non-empty shipped subset, the manifest and downgrade machinery carrying the honesty burden | M |
| Summarization pressure turns the report into a marketing leaderboard (single scores, geomean rankings, out-of-context quotes) | Non-goal enforced as success criteria: no aggregate cross-workload score, every comparative table carries workload-shape context, no-universal-superiority statement in the prose; the 2026-07-18 posture is the model | M |
| Double-publication or inconsistency between this report and the per-ecosystem Heddle reference rows (Q2.2 = option A puts a Heddle row in every ecosystem report) | Aggregation cites the same published source runs the ecosystem reference rows cite, so figures cannot diverge; the ecosystem rows are labeled excerpts and this report the complete assembly — reference rows are cited, never recomputed | M |
| Harness-statistic mismatch (Criterion vs BenchmarkDotNet vs JMH vs mitata vs pyperf vs testing/benchstat) makes the wall-time columns subtly non-equivalent | Phase 2 Q2.1's mapping is a binding dependency; the report documents the mapping once, publishes each figure's dispersion alongside its point estimate, and inherits each source run's own stability disclosures | M |
| Architecture analysis over-generalizes from at most three AOT peers and one runtime contextual encoder | Counted-claims rule: every architectural statement names its supporting workloads and engines; thin evidence is labeled thin; reach-vs-fair-fight labeling from phases 2–6 carries through | M |
| Per-ecosystem caveats (JMH profile realism, pyperf-on-Windows stability, mitata deopt flags) get lost in aggregation, laundering caveated numbers into clean-looking tables | The limitations section is assembled *from the source runs' disclosed caveats* as a checklist item; each sidebar carries its ecosystem's caveat inline | S |
| Allocation sidebars visually invite the cross-runtime comparison the ruling forbids | Sidebars are per-ecosystem sections, never adjacent columns of one table; each carries the explicit not-cross-comparable label; a no-juxtaposition check is a success criterion | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] Exactly one new `docs/benchmarks/<date>/` directory contains the consolidated report; no
      existing file under `docs/benchmarks/` is modified.
- [ ] No edit exists outside the new `docs/benchmarks/<date>/` directory at all (Q7.1, as
      amended): no docs-site page, no README comparison, no citation update to any other document —
      the date-stamped directory is the phase's sole publication surface.
- [ ] Every numeric figure in the report is traceable: it carries (directly or via its table's
      provenance note) the source run directory and commit, and spot-checking any figure against
      its source artifact reproduces it exactly — zero numbers exist that no source run
      published.
- [ ] Only protocol-conformant runs are aggregated: every source run is a Phase 1–6 run produced
      under the Phase 1 protocol on the protocol machine; the 2026-07-11 and 2026-07-18 reports
      appear as cited history only.
- [ ] Cross-language comparison tables contain wall time per render (with dispersion, per the
      Q2.1 mapping) and nothing else, with the ratio column anchored to Heddle (Q6.2); no table or
      prose juxtaposes allocation/GC, memory, or cold parse/compile figures across runtimes, and no
      table ranks non-Heddle engines across ecosystems.
- [ ] Each included ecosystem has a per-ecosystem sidebar for its non-comparable metrics, each
      carrying an explicit not-cross-comparable label and a citation to its source report; the
      sidebars use the within-ecosystem baseline convention Phase 6 Q6.2 fixes.
- [ ] Controlled and idiomatic tracks are presented separately and labeled; no table mixes
      numbers from different tracks; the report states what each track's numbers may be used to
      claim.
- [ ] The inclusion manifest enumerates every phase-2–6 ecosystem as included (with source run)
      or absent (with reason), lists PHP/Ruby as program-level non-goals with a link to the
      recorded rationale, and surfaces every contract-v2 excluded cell with its documented
      reason.
- [ ] The workload-shape analysis section covers all eight workload dimensions and, for each,
      names which engine architectures led/trailed/converged with the supporting engines and
      workloads counted; any claim resting on an absent ecosystem is explicitly downgraded.
- [ ] Every workload where Heddle trails any engine on wall time — on either track — is stated
      in the report's prose (not only visible in tables) as prominently as any Heddle win, with
      an account of what the loss reveals; the report contains an explicit
      no-universal-superiority statement.
- [ ] The report contains no aggregate cross-workload ranking score (no geomean or points
      leaderboard); every summary table is accompanied by workload-shape context.
- [ ] The report includes a source-environment manifest (machine, OS/toolchain versions, run
      date, commit per source run), per-source-run reproduce-it-yourself commands, and a
      limitations section that carries forward each source run's own disclosed caveats
      (including the JVM isolated-microbenchmark note, the Phase 1 encoded-suite confinement
      caveat wherever encoded-suite results appear, and any harness stability postures).
- [ ] Fair-fight vs reach framing is preserved: compiled-peer results (Rust/JVM/Go, as shipped)
      and reach results (JS/Python, as shipped) are labeled per their phases' framing
      commitments.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A reader spot-checks any wall-time figure against the cited source run's artifacts | The figure matches the source exactly; the source directory and commit are identifiable from the report |
| Phase 5 (Python) is cut before running | The report still ships; the manifest lists Python as absent with the reason; no Python row, sidebar, or Python-dependent claim appears; nothing else changes |
| Phase 6 (Go) is cut | The manifest records the absence and the report's contextual-encoding section states that the compile-time-vs-runtime contextual comparison could not be performed and why, downgrading the analysis to what the shipped engines support (e.g. compile-time contextual vs flat via JTE, if Phase 3 shipped), rather than extrapolating |
| A reader searches the report for any table placing .NET allocation next to JVM/Go/JS/Python/Rust memory figures | None exists; per-ecosystem sidebars are separate, labeled not cross-comparable, and cite their source reports |
| A reader looks for where Heddle loses or narrows | A prose findings section names every such workload/engine/track, as prominently as the wins, in the 2026-07-18 posture |
| A draft includes an overall-winner score or cross-workload geomean column | Rejected against the success criteria before publication; comparisons remain per-workload with shape context |
| A controlled-track number is found in an idiomatic-track table (or vice versa) | Treated as a defect; tracks are separated before publication |
| An ecosystem report's labeled Heddle reference row (Q2.2 = option A) is compared with this report's figures for the same engine/workload | They match exactly — both cite the same published source run; the ecosystem row is a labeled excerpt and this report the complete assembly, so no contradiction between documents is possible |
| A source run's environment block shows a toolchain change relative to other source runs | The affected juxtaposition carries a disclosed drift caveat, or the owning phase re-runs before this report publishes; Phase 7 performs no measurement either way |
| A contract-v2 excluded cell exists in a source report (e.g. a Thymeleaf controlled-track exclusion) | The consolidated tables show the cell as excluded with its documented reason — never a silently missing row |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). Q7.1 (the date-stamped directory is the
phase's sole publication surface — originally resolved with a link-only citation update to
`docs/language-assessment.md`, amended 2026-07-20 to the directory alone after that file was removed
from the repo, leaving nothing to cite into) and Q7.2 (no minimum quorum — ship over any non-empty
shipped subset of phases 2–6) are resolved (user, 2026-07-20) and folded above. Binding resolutions consumed from other phases, referenced
above and not duplicated: Q1.3, Q1.6, Q2.1, Q2.2 (= option A), Q4.1, Q4.2, Q5.1, and Q6.2.

## External grounding

| Claim | Source |
|---|---|
| Wall time per render is the only cross-language-comparable number; allocation/GC is per-ecosystem-only and explicitly non-comparable; one machine; publication under `docs/benchmarks/<date>/` in the existing style | Standing ruling 5 (grounding dossier, 2026-07-19); [Phase 1 metrics & publication protocol](phase-1-cross-stack-foundation.md); BenchmarkDotNet documentation ([benchmarkdotnet.org](https://benchmarkdotnet.org/)) |
| Both fairness tracks ship, labeled, answering different questions (engine vs practitioner experience) | Standing ruling 6 (grounding dossier, 2026-07-19); Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016), [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet); TechEmpower posture per [TechEmpower/FrameworkBenchmarks](https://github.com/TechEmpower/FrameworkBenchmarks) |
| Phases 2–6 are independent, priority-ordered, individually cuttable; Phase 7 consolidates | Standing ruling 8, the approved phase backbone (grounding dossier, 2026-07-19) |
| Honest-reporting posture: losses reported as prominently as wins, no universal-superiority claims, hardware- and date-specific numbers, reproduce-it-yourself commands — the report style this phase must match | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Heddle is the ratio baseline in the repo's published parity benchmarks | [src/Heddle.Performance/Runners/README.md](../../src/Heddle.Performance/Runners/README.md); [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Workload-shape dependence is demonstrated fact, not hypothesis: composition 2.0x Heddle lead vs allocation inversion (0.46x) and ~8% loop-time gap intra-.NET | [docs/benchmarks/2026-07-11/index.md](../benchmarks/2026-07-11/index.md); [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Each workload owns one variability dimension — the inversion of that table is the shape analysis's structure | [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md), workload table |
| Three AOT-compiled/typed peers (Askama, JTE, templ) exist to test whether the compiled result generalizes; html/template is the cast's only runtime contextual encoder (JTE, like Heddle, is contextual at compile time), and cutting Go forfeits the compile-time-vs-runtime contextual comparison | [Phase 2](phase-2-rust.md); [Phase 3](phase-3-jvm.md); [Phase 6](phase-6-go.md); research spike 2 engine notes (grounding dossier, 2026-07-19) |
| JS/Python results are reach-and-context evidence, not fair-fight evidence — framing this report must preserve | [Phase 4](phase-4-js.md); [Phase 5](phase-5-python.md) |
| The juxtaposition-placement question (Q2.2) resolved to option A: every per-ecosystem report carries a labeled, wall-time-only Heddle reference row, and this report is the complete assembly of the same source numbers | [open-questions.md](open-questions.md) Q2.2; [Phase 2](phase-2-rust.md); [Phase 3](phase-3-jvm.md) |
| Per-ecosystem caveats this report's limitations section must carry: JVM isolated-microbenchmark JIT-profile risk; harness stability postures | Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the JVM" (ACM SAC 2026, [arXiv:2605.23570](https://arxiv.org/abs/2605.23570)); [Phase 3](phase-3-jvm.md); [Phase 5](phase-5-python.md); [Phase 4](phase-4-js.md) |
| Phase 1's protocol run over the full eight-workload set is the Heddle/.NET source-row provider | [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md), success criteria |

Assumptions to verify in the spec (flagged, not grounded here): the concrete list of published
source-run directories at authoring time (fixes the inclusion manifest); the recorded values of
the binding cross-phase resolutions (Q1.3, Q1.6, Q2.1, Q2.2, Q4.1, Q4.2, Q5.1, Q6.2) in
`open-questions.md` (all resolved 2026-07-20); whether any source run's environment drifted enough
to trigger the re-run-request path.
