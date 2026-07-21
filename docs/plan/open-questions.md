# Open questions

> **Plan DoR: the Open section is empty.** Every question is driven to a resolution and folded into
> the affected phase document(s) before the plan is ready.

Question IDs are phase-scoped: `Q<phase>.<n>`. Phase 1's original Q1–Q7 are now Q1.1–Q1.7, Phase
2's original Q8/Q9 are now Q2.1/Q2.2, Phase 3's original Q3.1 is merged into Q2.2 (one entry, two
affected phases). Leans are preserved verbatim from the phase documents that recorded them.

## Open

None — all questions resolved (user, 2026-07-20). The plan is at DoR.

## Resolved

### Q1.1 — How does the encoded suite's byte gate reconcile legitimate cross-engine variance in escaped-entity spelling (e.g. `&#39;` vs `&#x27;`)?  [status: RESOLVED]
- **Context.** Raised by Phase 1's parity contract v2: engines legitimately differ on the entity
  spelling of the same escaped character, so a strictly byte-identical gate on the encoded suite
  would fail correct output. Affects Phase 1 (contract authoring) and every encoded-suite port in
  phases 2–6, each of which applies the resolved rule to its engines' actual escaper output.
- **Blocks.** Contract v2's normalization list; the encoded-suite parity gate; every encoded port
  in phases 2–6.
- **Options.** A) A documented v2 normalization step that canonicalizes semantically-identical HTML
  entity spellings, keeping everything else byte-strict — engines stay stock, the normalizer gains
  one closed rule. B) Engine configuration to emit oracle-matching spellings, where an engine
  offers it — output stays literally byte-identical, but depends on each engine's configuration
  surface.
- **Recommended default (lean).** (Phase 1, verbatim) "add a documented v2 normalization step that
  canonicalizes semantically-identical HTML entity spellings, keeping everything else byte-strict;
  prefer engine configuration to normalization wherever an engine offers it".
- **Resolution.** Accepted as recommended (user, 2026-07-20): add a documented v2 normalization
  step that canonicalizes semantically-identical HTML entity spellings, keeping everything else
  byte-strict; prefer engine configuration to normalization wherever an engine offers it.

### Q1.2 — What happens when a cast engine (Thymeleaf is the flagged risk) cannot pass the controlled byte gate despite best-effort authoring?  [status: RESOLVED]
- **Context.** Raised by Phase 1's exclusion policy; Thymeleaf (Phase 3) is the dossier-flagged
  riskiest controlled-track port in the cast. Phase 3 sequences Thymeleaf controlled-track
  feasibility first specifically to retire this exposure early; phases 4 and 6 also invoke this
  question's policy as their fallback for hard cells (Handlebars conditional-heavy, templ
  whitespace control).
- **Blocks.** Contract v2's exclusion-policy wording; Phase 3 scope.
- **Options.** A) The engine stays in the idiomatic track with its controlled-track cell marked
  excluded-with-documented-evidence — preserves the hard gate, costs a controlled-track cell.
  B) Introduce a replacement engine — fills the cell but reopens the fixed cast, so it requires
  user sign-off by the standing ruling.
- **Recommended default (lean).** (Phase 1, verbatim) "the engine stays in the idiomatic track, its
  controlled-track cell is marked excluded-with-documented-evidence, and no replacement engine is
  introduced without user sign-off".
- **Resolution.** Resolved with modification (user, 2026-07-20): the expected divergence for hard
  controlled-track engines (Thymeleaf is the flagged case) is whitespace-only, and the controlled
  gate's documented whitespace-collapse normalization already reconciles whitespace-only
  divergence — an engine whose output differs from the oracle **only** in whitespace still passes
  the controlled byte gate. The exclusion path — the engine stays in the idiomatic track, its
  controlled-track cell is marked excluded-with-documented-evidence, and no replacement engine is
  introduced without user sign-off — applies only to divergence **beyond** whitespace.

### Q1.3 — Does cold parse/compile cost get any cross-stack reporting, or is it per-ecosystem only?  [status: RESOLVED]
- **Context.** Raised by Phase 1's metrics protocol: AOT-compiled engines (Heddle, Askama, JTE,
  templ) have no runtime parse step while the rest of the cast parses or compiles its templates at
  runtime, so a cross-language cold-cost column would compare unlike operations. Phases 2, 4, 5, and 6 explicitly defer to this question's
  resolution and add nothing to it.
- **Blocks.** The metrics protocol; Phase 7 report columns.
- **Options.** A) Per-ecosystem only, labeled non-comparable — consistent with the allocation/GC
  rule, avoids the unlike-things comparison. B) A cross-language cold-cost column — more data, but
  juxtaposes build-time compilation with runtime parsing as if they were the same operation.
- **Recommended default (lean).** (Phase 1, verbatim) "per-ecosystem only, labeled non-comparable,
  because AOT compilation (Heddle, Askama, JTE, templ) and runtime parse/compile are different
  operations and a cross-language column would compare unlike things".
- **Resolution.** Accepted as recommended (user, 2026-07-20): per-ecosystem only, labeled
  non-comparable, because AOT compilation (Heddle, Askama, JTE, templ) and runtime parse/compile
  are different operations and a cross-language column would compare unlike things.

### Q1.4 — Does the composed-page anchor keep its documented fragment-sequence shape as the cross-language reference, or is a full-page composition workload authored instead?  [status: RESOLVED]
- **Context.** Raised by Phase 1: the composed-page anchor's fragment-sequence shape (the
  documented `@<<` entry-point behavior) invites "not a real page" criticism cross-stack, but
  changing it would require an engine change, which is out of the plan's scope.
- **Blocks.** The composed-page corpus entry; phases 2–6 port templates for workload 1.
- **Options.** A) Keep the anchor as-is with the fidelity note carried into the corpus docs —
  preserves continuity with published .NET numbers; the mid-size mixed page carries
  realistic-full-page duty. B) Author a new full-page composition workload — answers the criticism
  directly but requires an engine change ruled out of scope.
- **Recommended default (lean).** (Phase 1, verbatim) "keep it as-is with the fidelity note carried
  into the corpus docs (changing it requires an engine change, ruled out of scope) and let the
  mid-size mixed page carry realistic-full-page duty".
- **Resolution.** Accepted as recommended (user, 2026-07-20): keep the anchor as-is with the
  fidelity note carried into the corpus docs (changing it requires an engine change, ruled out of
  scope) and let the mid-size mixed page carry realistic-full-page duty.

### Q1.5 — What is the golden-corpus regeneration policy once phases 2–6 have consumed it?  [status: RESOLVED]
- **Context.** Raised by Phase 1: once Phase 2 starts, corpus changes are breaking changes for
  every shipped ecosystem. The corpus records generating commit + hash per entry; the risk is
  silent drift if the Heddle engine changes during the v2.0 window. Phases 2–6 all reference this
  policy as their upstream-change contract.
- **Blocks.** The corpus stability guarantees phases 2–6 rely on.
- **Options.** A) Corpus pinned to its generating commit; regeneration is a deliberate, documented,
  version-bumping event that re-runs every parity gate in every already-shipped ecosystem — strong
  stability, some ceremony per engine change. B) Regenerate freely as the engine evolves — no
  ceremony, but shipped ecosystems silently port moving targets (the drift risk Phase 1's risk
  table names).
- **Recommended default (lean).** (Phase 1, verbatim) "the corpus is pinned to its generating
  commit; regeneration is a deliberate, documented event that bumps a corpus version and re-runs
  every parity gate in every already-shipped ecosystem before the new corpus is adopted".
- **Resolution.** Recommended default **rejected** (user, 2026-07-20): "Do not pin anything, we do
  not care that much, this is unimportant. It is versioned already, there is no action needed and
  if it expands it will be versioned the same way." No regeneration-policy machinery: no corpus
  version bumps and no mandatory re-run-all-shipped-gates ceremony. The golden corpus lives under
  ordinary git versioning, which suffices; corpus expansion or regeneration is an ordinary
  versioned change, handled the same way. The drift risk is explicitly accepted by user ruling.

### Q1.6 — Which single machine hosts all cross-compared runs — the existing Windows/Ryzen 9 9950X box used by the published reports, or a Linux box?  [status: RESOLVED]
- **Context.** Raised by Phase 1's one-machine protocol rule. Some cross-language harnesses (e.g.
  pyperf) assume Linux-style CPU isolation for best stability; the published .NET numbers come from
  the Windows/Ryzen box. Phase 5 is the most exposed ecosystem phase (see Q5.2, conditional on this
  question); phases 2, 4, and 6 each resolve per-harness Windows stability settings in their specs
  under this question's lean.
- **Blocks.** The machine line of the metrics protocol; every measurement run in phases 2–6 and the
  Phase 7 report.
- **Options.** A) The existing Windows/Ryzen box — continuity with the published .NET numbers;
  per-harness stability settings resolved per ecosystem spec. B) A Linux box — better matches some
  harnesses' isolation assumptions, but severs continuity with every published Heddle number.
- **Recommended default (lean).** (Phase 1, verbatim) "the existing Windows/Ryzen box, for
  continuity with the published .NET numbers, with OS/toolchain versions recorded and per-harness
  stability settings resolved in each ecosystem spec".
- **Resolution.** Accepted as recommended (user, 2026-07-20): the existing Windows/Ryzen box, for
  continuity with the published .NET numbers, with OS/toolchain versions recorded and per-harness
  stability settings resolved in each ecosystem spec. (A separate later Linux cross-check on the
  same physical machine is a distinct new phase — see Q5.2 and `phase-8-linux-crosscheck.md` — and
  does not alter the one-machine rule for phases 2–7.)

### Q1.7 — What evidence standard defines "idiomatic" for the idiomatic track?  [status: RESOLVED]
- **Context.** Raised by Phase 1's dual-track fairness design: the idiomatic track measures the
  practitioner experience, which requires a defensible definition of "how a practitioner would
  write it". Phases 2–6 all author their idiomatic implementations to this standard (phases 4 and
  5 cite it explicitly as the per-implementation documentation-citation discipline).
- **Blocks.** Contract v2's idiomatic-track definition; authoring guidance for phases 2–6.
- **Options.** A) Implementations authored in-repo following each engine's official documentation
  patterns, with the doc pages cited per implementation — controlled provenance, uniform quality.
  B) Import implementations from third-party benchmark repos — cheaper, but of varying quality and
  provenance.
- **Recommended default (lean).** (Phase 1, verbatim) "idiomatic implementations are authored
  in-repo following each engine's official documentation patterns, with the doc pages cited per
  implementation, rather than imported from third-party benchmark repos of varying quality".
- **Resolution.** Accepted as recommended (user, 2026-07-20): idiomatic implementations are
  authored in-repo following each engine's official documentation patterns, with the doc pages
  cited per implementation, rather than imported from third-party benchmark repos of varying
  quality.

### Q2.1 — Which statistic from each harness is "wall time per render" for cross-stack purposes, given Criterion's regression-based estimates vs BenchmarkDotNet's mean-based summaries?  [status: RESOLVED]
- **Context.** Raised by Phase 2 (first non-.NET harness): Criterion.rs is statistics-driven
  (linear-regression estimates, 95% CIs) where BenchmarkDotNet reports mean-based summaries, so the
  one cross-comparable number needs a documented statistic mapping. Though raised in Phase 2, the
  resolution lands in the Phase 1 metrics protocol's report format and binds every ecosystem's
  harness (JMH, mitata, pyperf, benchstat included).
- **Blocks.** The Rust report's tables; Phase 7's comparison columns; and by extension every
  ecosystem report's number selection.
- **Options.** A) Each harness's default central-tendency point estimate, with dispersion published
  alongside and the mapping documented once — keeps each harness native and credible. B) Force one
  harness's statistical methodology onto the other ecosystems — uniform statistics, but sacrifices
  the "standard harness per ecosystem" credibility the protocol is built on (explicitly not
  attempted, per Phase 2's design direction).
- **Recommended default (lean).** (Phase 2, verbatim) "each harness's default central-tendency
  point estimate, with dispersion (CI / stddev) published alongside and the mapping documented once
  in the metrics protocol's report format".
- **Resolution.** Accepted as recommended (user, 2026-07-20): each harness's default
  central-tendency point estimate, with dispersion (CI / stddev) published alongside and the
  mapping documented once in the metrics protocol's report format.

### Q2.2 — Does a per-ecosystem report juxtapose Heddle's same-machine .NET wall times, or is all cross-runtime juxtaposition deferred to the Phase 7 consolidated report?  [status: RESOLVED]
*(Merged entry: Phase 2's original Q9 and Phase 3's original Q3.1 ask the same question. Affected
phases: 2 and 3 directly (they recorded the opposite leans below); phases 4–6, whose report layouts
follow the program-wide resolution — see the stance note below; Phase 7's aggregation rules.)*
- **Context.** Every ecosystem phase publishes a per-ecosystem report, and wall time per render is
  the only cross-language-comparable number — so each report must decide whether a labeled Heddle
  reference row appears in it, or whether cross-runtime juxtaposition lives only in Phase 7. Phase
  2 raises it for the Rust report (where Heddle-vs-Askama is the program's headline result); Phase
  3 raises the identical question for the JVM report. Stance note (what the resolution had to
  reconcile program-wide): Phase 6's non-goals adopted option B as a working assumption, explicitly
  subordinate to this resolution; Phases 4 and 5 framed their reports conditionally on this
  resolution (their honest-reporting criteria about Heddle-trailing results apply to whichever
  report carries the juxtaposition). No phase pre-decided the question; the two recorded leans
  below were opposite, so a program-wide resolution was required and, once made, fixes the report
  layout of all of phases 2–6.
- **Blocks.** The Rust report's structure; the JVM report layout; by symmetry the report layout of
  phases 4–6; Phase 7's aggregation rules (avoiding double-published comparisons).
- **Options.** A) Include a clearly-labeled, wall-time-only Heddle reference row sourced from the
  Phase 1 protocol run — surfaces the headline comparison early; allocations never juxtaposed
  either way. B) Defer all cross-runtime juxtaposition to Phase 7 — keeps one canonical place for
  the only cross-comparable number; per-ecosystem reports rank only their own engines.
- **Recommended default (lean).** The two phase documents recorded **opposite leans**, so no single
  default could be preserved; a program-wide resolution was required.
  - Phase 2 (verbatim, for A): "include a clearly-labeled, wall-time-only Heddle reference row
    sourced from the Phase 1 protocol run, because the Heddle-vs-Askama match-up is the program's
    headline result and burying it until Phase 7 delays the credibility payoff; allocations are
    never juxtaposed either way".
  - Phase 3 (verbatim, for B): "defer to phase 7; the per-ecosystem report ranks JTE and Thymeleaf
    against the corpus workloads and states that cross-language wall-time comparison appears only
    in the consolidated report, keeping one canonical place for the only cross-comparable number".
- **Related (distinct, not merged).** [Q6.2](#q62--which-engine-is-the-ratio-baseline-in-the-per-ecosystem-report-tables-given-heddle-is-not-an-in-process-participant--status-resolved)
  (which engine anchors the within-ecosystem ratio column — interacts because option A adds a
  Heddle row); [Q4.2](#q42--is-the-js-handlebars-vs-net-handlebarsnet-cross-runtime-comparison-in-scope-for-the-js-report--status-resolved)
  (a same-engine cross-runtime side-note, permitted wall-time-only — a special case whose framing
  depends on this resolution).
- **Resolution.** Resolved — **option A** (user, 2026-07-20): every per-ecosystem report
  (phases 2–6) carries a clearly-labeled, wall-time-only Heddle reference row sourced from the
  Phase 1 protocol run; allocations are never juxtaposed. Phase 7 remains the canonical, **complete**
  cross-stack comparison — the per-ecosystem Heddle row is a labeled excerpt of the same published
  protocol numbers, not a second analysis.

### Q4.1 — Does the JS suite report per-ecosystem heap/GC metrics via mitata, or wall time only?  [status: RESOLVED]
- **Context.** Raised by Phase 4: the grounding dossier establishes mitata as the JS harness but is
  silent on its memory-measurement credibility (stability across runs, documented semantics), so
  the JS report's column set cannot be fixed yet. Any memory figures published are
  per-ecosystem-only and marked non-comparable, per the standing metrics ruling.
- **Blocks.** The JS report's column set; the Phase 7 per-ecosystem memory appendix for JS.
- **Options.** A) Time-only, stated explicitly in the report — thinner report, but costs no
  cross-stack claim since memory is per-ecosystem-only anyway. B) Publish heap/GC columns if the
  spec verifies mitata's memory instrumentation is credible — richer per-ecosystem data, contingent
  on verification.
- **Recommended default (lean).** (Phase 4, verbatim) "time-only, stated explicitly in the report,
  unless the spec verifies mitata's memory instrumentation is credible (stable across runs,
  documented semantics) — the dossier grounds mitata as the harness but is silent on its memory
  measurement; if verified, figures are per-ecosystem-only per the standing metrics ruling".
- **Resolution.** Accepted as recommended (user, 2026-07-20): time-only, stated explicitly in the
  report, unless the spec verifies mitata's memory instrumentation is credible (stable across runs,
  documented semantics) — if verified, figures are per-ecosystem-only per the standing metrics
  ruling.

### Q4.2 — Is the JS Handlebars vs .NET Handlebars.Net cross-runtime comparison in scope for the JS report?  [status: RESOLVED]
- **Context.** Raised by Phase 4: the same template language, byte-identical templates, same
  workloads, same machine, two runtimes (V8 vs .NET) — a comparison no surveyed benchmark has
  published, and nearly free because both twins already exist and pass parity against the same
  corpus. Wall time is cross-comparable under the protocol, so the comparison is permitted; whether
  the JS report carries it is the question.
- **Blocks.** The JS report's structure; whether Phase 7 may reference the side-note.
- **Options.** A) Yes, as a small explicitly-labeled side-note — wall-time only, outside the
  engine-ranking tables, framed as port/runtime context — novel data at near-zero cost. B) No —
  avoids any risk of the side-note being misread as a .NET-vs-Node runtime shootout, at the cost of
  an unpublished unique result.
- **Recommended default (lean).** (Phase 4, verbatim) "yes, as a small explicitly-labeled side-note
  — wall-time only (the sole cross-comparable metric), placed outside the engine-ranking tables,
  framed as port/runtime context and never as part of the ecosystem verdict — because the data is
  nearly free (both twins already exist and pass parity against the same corpus) and no surveyed
  benchmark has published such a comparison".
- **Related (distinct, not merged).** [Q2.2](#q22--does-a-per-ecosystem-report-juxtapose-heddles-same-machine-net-wall-times-or-is-all-cross-runtime-juxtaposition-deferred-to-the-phase-7-consolidated-report--status-resolved)
  — the general per-ecosystem-juxtaposition question; this side-note is a same-engine special case
  whose placement follows Q2.2's resolution.
- **Resolution.** Accepted as recommended (user, 2026-07-20): yes, as a small explicitly-labeled
  side-note — wall-time only, placed outside the engine-ranking tables, framed as port/runtime
  context and never part of the ecosystem verdict. Placement is settled by Q2.2 = option A: the JS
  report carries the cross-runtime juxtaposition, so the side-note lives there.

### Q5.1 — Which memory metric does the Python per-ecosystem report publish (tracemalloc allocated-bytes-per-render vs pyperf's peak-memory tracking)?  [status: RESOLVED]
- **Context.** Raised by Phase 5: "memory" is ambiguous for CPython (refcounting + pymalloc, unlike
  tracing-GC runtimes), and an unconsidered choice produces a per-ecosystem metric readers or Phase
  7 over-interpret. Whatever is chosen carries the protocol's explicit not-cross-comparable label
  and a method note.
- **Blocks.** The Python report's memory columns; the spec's harness instrumentation; Phase 7's
  ingestion of the per-ecosystem table.
- **Options.** A) tracemalloc allocated-bytes-per-render — closest in kind to the other ecosystems'
  per-render allocation columns. B) pyperf's peak-memory tracking — native to the harness, but a
  peak figure, not a per-render allocation figure.
- **Recommended default (lean).** (Phase 5, verbatim) "tracemalloc allocated-bytes-per-render as
  the headline figure, because it is closest in *kind* to the other ecosystems' per-render
  allocation columns (while still labeled not cross-comparable), with the measurement method
  disclosed in the report; exact instrumentation settled in the spec".
- **Resolution.** Accepted as recommended (user, 2026-07-20): tracemalloc
  allocated-bytes-per-render as the headline figure, closest in *kind* to the other ecosystems'
  per-render allocation columns (still labeled not cross-comparable), with the measurement method
  disclosed in the report; exact instrumentation settled in the spec.

### Q5.2 — If Phase 1's Q1.6 keeps the Windows box, does the Python phase accept pyperf's reduced isolation guarantees on Windows as-is, or additionally publish a non-cross-comparable Linux stability cross-check run?  [status: RESOLVED]
- **Context.** Raised by Phase 5, conditional on [Q1.6](#q16--which-single-machine-hosts-all-cross-compared-runs--the-existing-windowsryzen-9-9950x-box-used-by-the-published-reports-or-a-linux-box--status-resolved):
  pyperf's best-stability guidance assumes Linux-style CPU isolation, making Phase 5 the ecosystem
  phase most exposed to the machine decision. Phase 5 references Q1.6 but does not resolve it.
- **Blocks.** Phase 5's measurement-run environment section and report wording.
- **Options.** A) Accept the protocol machine as-is; disclose the stability posture and dispersion
  statistics in the report — honest about variance, keeps the one-machine protocol intact. B) Add
  a Linux stability cross-check run — more confidence in the numbers' tightness, but invites
  exactly the cross-machine comparison the one-machine protocol exists to prevent.
- **Recommended default (lean).** (Phase 5, verbatim) "accept the protocol machine as-is and
  disclose the stability posture and dispersion statistics in the report, with no second machine —
  a secondary run invites exactly the cross-machine comparison the one-machine protocol exists to
  prevent".
- **Resolution.** Resolved — **neither lean** (user, 2026-07-20): "We will check the same machine
  but on Linux (Ubuntu 24.04) later, separately, plan for this." Windows remains the protocol
  machine for phases 2–7 (per Q1.6), and Phase 5 accepts it with the stability/dispersion
  disclosure of its option A. Additionally, a separate, later Linux cross-check — the **same
  physical machine** running Ubuntu 24.04 — is added to the program as a new **Phase 8**
  (`phase-8-linux-crosscheck.md`, authored separately), separately published and **never** merged
  or cross-compared with the Windows numbers inside the phases 2–7 reports.

### Q6.1 — Which stdlib surface carries the raw (encoding-off) Go suites: text/template, or html/template with per-value raw bypass?  [status: RESOLVED]
- **Context.** Raised by Phase 6: every raw workload runs each engine's non-encoding path, and for
  the Go stdlib that path can be read two ways — text/template (the same stdlib engine minus the
  escaping pass) or html/template with per-value raw bypass. The choice affects what the benchmark
  measures and how the credibility pick is labeled; Phase 6 records it as a decision, not a silent
  assumption.
- **Blocks.** Raw-suite twin authoring for the stdlib pick; controlled-track parity work; report
  row labeling.
- **Options.** A) text/template for the raw suites and html/template for the encoded suite,
  presented as one stdlib engine's two paths — mirrors every other cast engine's non-encoding mode
  and avoids measuring bypass-wrapper overhead as if it were templating. B) html/template with
  per-value raw bypass throughout — one engine surface for all suites, but its raw numbers include
  bypass-wrapper overhead.
- **Recommended default (lean).** (Phase 6, verbatim) "text/template for the raw suites and
  html/template for the encoded suite, presented in the report as the one stdlib engine's two paths
  — this mirrors how every other cast engine's non-encoding mode is used (parity contract v1/v2)
  and avoids measuring bypass-wrapper overhead as if it were templating; the report labels the
  surface per suite either way".
- **Resolution.** Accepted as recommended (user, 2026-07-20): text/template for the raw suites and
  html/template for the encoded suite, presented in the report as the one stdlib engine's two
  paths, mirroring every other cast engine's non-encoding mode and avoiding measuring
  bypass-wrapper overhead as if it were templating; the report labels the surface per suite either
  way.

### Q6.2 — Which engine is the ratio baseline in the per-ecosystem report tables, given Heddle is not an in-process participant?  [status: RESOLVED]
- **Context.** Raised by Phase 6 but generalizing to all of phases 2–6: the intra-.NET reports use
  Heddle as the ratio baseline, but Heddle does not run in-process in any other ecosystem, so each
  per-ecosystem report needs a baseline convention.
- **Blocks.** The Phase 6 report's table format; cross-phase report consistency that Phase 7
  inherits.
- **Options.** A) The credibility pick (for Go, the stdlib engine) as within-ecosystem baseline,
  fixed once in the Phase 1 publication protocol as the convention for all of phases 2–6 —
  consistent tables across every report. B) Decide per phase — flexible, but Phase 7 inherits
  inconsistent ratio columns.
- **Recommended default (lean).** (Phase 6, verbatim) "the credibility pick (the stdlib engine) as
  within-ecosystem baseline, and preferably fixed once in the Phase 1 publication protocol as the
  convention for all of phases 2–6 rather than decided per-phase".
- **Related (distinct, not merged).** [Q2.2](#q22--does-a-per-ecosystem-report-juxtapose-heddles-same-machine-net-wall-times-or-is-all-cross-runtime-juxtaposition-deferred-to-the-phase-7-consolidated-report--status-resolved)
  — whether a Heddle reference row appears at all is Q2.2; which engine anchors the ratio column is
  this question. Q2.2 resolved to include a Heddle row, so its relationship to the ratio baseline
  is settled here.
- **Resolution.** Resolved with a **program-wide presentation rule** (user, 2026-07-20): (i) Heddle
  may be compared and ranked against any engine, wall-time-only, anywhere; (ii) non-Heddle engines
  are **never** compared across ecosystems — engine-vs-engine ranking exists only within an
  ecosystem, program-wide (so Phase 7 groups Heddle-anchored comparisons per ecosystem and
  publishes no global cross-ecosystem leaderboard of non-Heddle engines); (iii) with Q2.2 = option
  A, the per-ecosystem **wall-time ratio column anchors to the labeled Heddle reference row**,
  uniform across phases 2–6 and fixed once in the Phase 1 publication protocol; baselines for the
  non-cross-comparable metrics (allocations, etc.) stay within-ecosystem, the exact choice a spec
  detail.

### Q7.1 — Does the consolidated report also feed `docs/language-assessment.md` and the published docs site, or is the date-stamped benchmark directory the phase's sole publication surface?  [status: RESOLVED]
- **Context.** Raised by Phase 7: `docs/language-assessment.md` already sourced its performance
  figures exclusively from committed `docs/benchmarks/<date>/` artifacts with direct links, so a
  low-cost citation opportunity existed once the consolidated report ships; whether that update
  (and any docs-site surface) is in scope for this phase, versus the date-stamped report
  directory alone, needed a decision. **Status note (2026-07-20):** `docs/language-assessment.md`
  has since been removed from the repo (user cleanup), so the citation opportunity no longer
  exists — see the amended resolution below.
- **Blocks.** This phase's deliverable list and back-compat/impact surface.
- **Options.** A) The date-stamped directory is the sole deliverable; a link-only citation update
  to the language assessment's performance/industry-comparison sections is in scope (the
  assessment's prose stays untouched) — cheap and consistent with that document's existing
  sourcing contract. B) Broaden scope to a docs-site page, assessment-prose rewrite, or README
  comparison update — richer surface, but expands this phase's back-compat impact beyond one
  additive report directory.
- **Recommended default (lean).** (Phase 7, verbatim) "the date-stamped directory is the
  deliverable; a link-only citation update to the language assessment's
  performance/industry-comparison sections is in scope, because that document's own contract
  already sources numbers exclusively from committed `docs/benchmarks/<date>/` artifacts, so a
  link is cheap and consistent — while any docs-site page, assessment-prose rewrite, or README
  comparison update is out of scope for this phase".
- **Resolution.** Originally accepted as recommended (user, 2026-07-20): the date-stamped directory
  is the deliverable; a link-only citation update to the language assessment's
  performance/industry-comparison sections is in scope (the assessment's prose stays untouched);
  any docs-site page, assessment-prose rewrite, or README comparison update is out of scope for
  this phase. **Amended same day (2026-07-20), factual-necessity amendment:** the user removed
  `docs/language-assessment.md` from the repo, so the link-only citation-update clause is now moot —
  there is nothing to cite into. The resolution is simply that **the date-stamped directory is the
  phase's sole publication surface**, full stop, with no docs-site or other surface added (option
  A's other half, which still holds). This is not a re-opening of the A-vs-B product choice: the
  user's original preference for a cheap citation update stands, it is merely inapplicable now.
  Stays RESOLVED.

### Q7.2 — Is there a minimum ecosystem quorum below which the consolidated report does not ship (e.g. at least one compiled-peer ecosystem among Rust/JVM/Go)?  [status: RESOLVED]
- **Context.** Raised by Phase 7: because phases 2–6 are each individually cuttable, the
  consolidated report may need to ship over any non-empty subset, down to a reach-only subset
  (JS/Python alone) with no compiled peer at all; whether a minimum quorum gates publication is a
  go/no-go rule this phase needs before shipping.
- **Blocks.** The cut-decision consequences of phases 2–6 and this phase's go/no-go rule.
- **Options.** A) Ship with any non-empty shipped subset of phases 2–6, with the manifest and
  claim-downgrade machinery carrying the honesty burden — a reach-only report is still
  publishable as labeled reach evidence. B) Require a minimum quorum (e.g. at least one
  compiled-peer ecosystem among Rust/JVM/Go) before publication — guards against an
  under-evidenced report, but risks withholding shipped, honestly-labeled data.
- **Recommended default (lean).** (Phase 7, verbatim) "ship with any non-empty shipped subset of
  phases 2–6, with the manifest and claim-downgrade machinery carrying the honesty burden — a
  reach-only report (JS/Python alone) is still publishable as labeled reach evidence, and
  withholding shipped data would be its own credibility failure".
- **Resolution.** Accepted as recommended (user, 2026-07-20): ship with any non-empty shipped
  subset of phases 2–6, with the manifest and claim-downgrade machinery carrying the honesty
  burden — a reach-only report (JS/Python alone) is still publishable as labeled reach evidence,
  and withholding shipped data would be its own credibility failure.
