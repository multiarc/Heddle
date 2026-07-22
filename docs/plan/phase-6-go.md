# Phase 6 — go

## Header

- **Status:** in progress
- **Goal (one line):** The Go ecosystem comparison — Heddle's golden-corpus workloads ported to html/template and templ under both fairness tracks, measured with Go's testing benchmark harness plus benchstat, and published as a per-ecosystem date-stamped report.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) (golden oracle corpus, parity contract v2, metrics/publication protocol)
- **Changes an externally-visible contract:** no — this phase consumes the Phase 1 corpus and contract v2 unchanged; its outputs are Go-side implementations and a new immutable date-stamped report under `docs/benchmarks/<date>/`.

## Goal

Go is priority 5 of 5 — included for completeness by standing ruling — yet it carries a comparison
no other ecosystem phase can provide: **html/template is the only engine in the entire program —
the ten competitor engines and Heddle itself — that performs its context-aware escaping at
runtime.** The program fields three contextual encoders — Heddle and JTE (phase 3) decide escaping
context at compile time; html/template decides it per render — so the
encoding-ON suite's compile-time-vs-runtime contextual comparison exists
only through this phase. If phase 6 were cut, the encoded suite would keep a compile-time
contextual peer (JTE) but lose its only runtime contextual encoder, and with it that comparison;
that scientific value, not Go's priority rank, is the reason this phase earns its slot.

The phase delivers the Go port of the Phase 1 workload set (all eight workloads: six raw, two
encoded) against two engines chosen by standing ruling: **html/template** (credibility pick — the
stdlib baseline every Go developer knows, runtime reflection-driven, contextually auto-escaping) and
**templ** (performance/peer pick — AOT `go generate` codegen to compiled, typed Go; at ~10.4K stars
the community favorite among compiled Go engines, and Heddle's architectural analogue in this
ecosystem). templ also supplies the program's **third compiled/typed data point** after Askama
(phase 2) and JTE (phase 3), which is what lets the phase 7 consolidated report say whether the
compiled-engine result generalizes across toolchains rather than being a .NET artifact. Both
fairness tracks ship, per standing ruling, exactly as Phase 1 defines them: controlled
(byte-identical to the golden oracle after documented normalization, gated before any timing) and
idiomatic (machine-checkable functional-equivalence verifier). The user-visible outcome is one
date-stamped `docs/benchmarks/<date>/` report in the repo's established honest-reporting style.

## Non-goals / scope boundary

- **No cross-ecosystem ranking of non-Heddle engines in this phase's report.** The phase 6 report
  compares Go engines against each other, parity-gated against the Heddle golden corpus, and — per
  Q2.2 = option A — carries a clearly-labeled, wall-time-only Heddle reference row; per Q6.2, Heddle
  may be ranked against the Go engines wall-time-only, but the Go engines are never ranked against
  another ecosystem's engines. The *complete* consolidated cross-stack comparison remains phase 7's
  job.
- **hero is excluded from the cast.** Its maintenance status is uncertain (research spike 2), and
  the ruling's two-engine cast plus the optional quicktemplate stretch already covers the
  compiled-Go niche. The report documents this exclusion with its reason.
- **quicktemplate is a conditional stretch, not core scope** — see Design direction for the
  inclusion rule. Phase completion never depends on it.
- **No cross-runtime allocation comparison.** Go's `testing.B` reports allocs/op natively and the
  phase reports it — but per-ecosystem only, explicitly labeled non-comparable, per standing ruling
  and the Phase 1 metrics protocol. Go's GC differs fundamentally from the CLR's (non-generational,
  non-compacting), so the numbers do not mean the same thing; this phase references that rule, it
  does not re-argue it.
- **No changes to the golden corpus, contract v2, or the Heddle engine.** Upstream corpus changes
  are ordinary git-versioned changes (Phase 1, Q1.5 — no version-bump or re-run-all-gates
  ceremony); this phase only consumes.
- **No HTTP or database stack** — template rendering in isolation, as everywhere in the program.
- **No re-litigation of the engine cast** (standing ruling: two per ecosystem, already chosen).

## Design direction

**Interpreted-vs-precompiled framing, per the Go ecosystem's own precedent.** The only maintained
Go template benchmark, slinso/goTemplateBenchmark, organizes its seventeen-plus engines along one
axis: interpreted vs precompiled. The phase 6 report adopts that framing as its organizing
structure — html/template as the reflection-driven runtime baseline, templ (and quicktemplate, if
included) as the AOT-compiled tier — because it is the framing Go readers already trust, and it is
the same compiled-vs-interpreted story Heddle's own positioning tells. What this phase adds over
that precedent is what no Go suite has ever had: an output-equality gate (the surveyed suite asserts
no output equality at all), which is the program's credibility differentiator per Phase 1.

**The encoded suite is this phase's headline, and it reports contextual-vs-flat within Go — as an
architectural comparison, not an isolated feature cost.** Inside the Go ecosystem, the two encoded
workloads put html/template's runtime contextual encoder and templ's flat escaper on the same
corpus-gated output, within one runtime where allocations and wall time are directly comparable.
Because the context-confinement constraint keeps every encoder family's output equivalent in the
measured contexts, the delta between these two engines reflects their whole architectures (runtime
reflection vs AOT codegen) *including* their different escaping models — the report presents it
that way and never attributes the difference to context analysis alone; no cast configuration
toggles contextual vs flat escaping within one engine. The same confinement carries the symmetric
caveat Phase 1's protocol requires every encoded-suite report to state: the confined contexts are
exactly the ones where context awareness buys nothing extra, so the suite can show what a
contextual encoder costs but structurally cannot show what it prevents — html/template's (or
Heddle's) encoded-suite numbers are never presented as evidence that contextual encoding is pure
overhead. The cross-language pairing that motivates
the phase — Heddle's and JTE's compile-time contextual encoding vs html/template's runtime
contextual encoding — is assembled in phase 7 from wall-time-per-render numbers, the only
cross-comparable metric, under the same architectural-comparison framing. Phase 1's
context-confinement constraint (untrusted data only in HTML text and attribute-value positions) is
what is designed to make the byte gate passable for every encoder family at once — an assumption
Phase 1 flags for per-engine verification; the phase relies on it rather than restating it, and the
report carries a dedicated contextual-encoding section so the comparison is legible, not buried in
a table.

**Harness: Go testing benchmarks plus benchstat, per the Phase 1 protocol.** The metrics protocol
names Go's standard harness for this ecosystem; `testing.B` provides wall time and native allocs/op,
and benchstat provides the statistical treatment of repeated runs. Per-run settings, iteration
policy, and toolchain pinning are spec territory. Cold parse/compile cost follows the Phase 1 Q1.3
resolution: templ templates are compiled at build time via `go generate` and have no runtime parse
step at all — the same AOT asymmetry Phase 1 records for Heddle, Askama, and JTE — so any cold-cost
figures are per-ecosystem only, never a cross-language column.

**The stdlib engine's raw path is a real decision, recorded, not assumed.** Every raw workload runs
each engine's non-encoding path (parity contract v1/v2). For the Go stdlib, that path can be read
two ways — text/template (the same stdlib engine minus the escaping pass) or html/template with
per-value raw bypass — and the choice affects both what the benchmark measures and how the
credibility pick is labeled. Q6.1 settles it: text/template for the raw suites and html/template
for the encoded suite, presented as the one stdlib engine's two paths — mirroring every other cast
engine's non-encoding mode and avoiding measuring bypass-wrapper overhead as if it were templating;
the report labels the surface per suite either way.

**Report table baseline, fixed by the protocol.** Per Q6.2's program-wide presentation rule, the
per-ecosystem wall-time ratio column anchors to the labeled Heddle reference row (Q2.2 = option A),
uniform across phases 2–6 and fixed once in the Phase 1 publication protocol; the baseline for the
within-ecosystem non-comparable metrics (allocs/op, etc.) stays within Go — the credibility pick,
the stdlib engine — a spec-level detail. Non-Heddle engines are never ranked across ecosystems.

**quicktemplate: in as a conditional stretch, by rule.** The standing ruling made it "optional
third if cheap"; this phase operationalizes "cheap" as a rule rather than leaving it open:
quicktemplate is included **only if** templ's controlled-track ports have passed the byte gate for
all eight workloads (proving the compiled-Go port path works) **and** the incremental work is
limited to authoring quicktemplate template twins over the already-built Go harness and twin
scaffolding — zero new harness, gate, or contract work (the operational meaning of the ruling's
"if cheap"). Under any
schedule pressure it is cut first, before any core scope, and its absence never blocks phase
completion. The rationale for wanting it at all: its "up to 20x faster than html/template"
marketing claim is exactly the kind of claim a parity-gated suite exists to test — but the
credibility and peer roles are already filled by the two ruled-in engines.

**Porting risk is concentrated in templ's controlled track.** templ's whitespace-control
documentation is thin (research spike 2 flag) — the exact mechanics of authoring whitespace-free,
byte-exact output are an assumption the spec must verify before bulk porting begins. If templ
proves structurally unable to meet the byte gate despite best-effort authoring, the Phase 1 Q1.2
policy applies unchanged: it remains in the idiomatic track, its controlled-track cells are marked
excluded-with-documented-evidence, and no replacement engine enters without user sign-off.

## Dependencies & ordering

- **Depends on:** Phase 1, fully — the golden corpus (no workload may be ported before its corpus
  entry exists), parity contract v2 (including the Q1.1 escaped-entity reconciliation, which the Go
  engines' actual escape output must be validated against), the metrics/publication protocol, and
  the Phase 1 resolutions of Q1.3 (cold-cost reporting), Q1.6 (the single benchmark machine), and
  Q1.7 (the idiomatic evidence standard).
- **Independent of phases 2–5.** Phase 6 can start any time after Phase 1 lands, in any order
  relative to the other ecosystem phases; it is priority 5 and individually cuttable per the
  approved backbone — with the recorded caveat that cutting it forfeits the cast's only runtime
  contextual encoder, and with it the compile-time-vs-runtime contextual-encoding comparison.
- **Internal ordering:** stdlib engine ports first (cheapest authoring; exercises the Go-side
  parity tooling and the Q6.1 decision early) → templ controlled-track ports, riskiest workload
  first, so a whitespace-control dead end surfaces before bulk effort → idiomatic-track
  implementations under the Q1.7 standard → quicktemplate stretch only after its inclusion rule is
  satisfied → measurement runs on the protocol machine → report authored and published.
- **Unblocks:** phase 7's Go rows, its compiled-engine generalization argument (third AOT data
  point), and the runtime-contextual side of its contextual-encoding analysis.

## Back-compat / impact

- **Nothing existing breaks.** The .NET suite, the golden corpus, contract v2, and all published
  reports are untouched; this phase adds a Go implementation area to the repo and one new
  immutable date-stamped report directory.
- **New toolchain surface on the benchmark machine:** the Go toolchain and the templ generator
  (and quicktemplate's, if the stretch lands) join the recorded environment; the Phase 1 protocol's
  environment block carries their versions in every published run.
- **Corpus coupling:** once this phase's ports exist, a golden-corpus regeneration — an ordinary
  git-versioned change (Phase 1, Q1.5) — means any subsequent Go run re-runs the Go parity gates
  against the current corpus, like any other repo change.
- **Report readers:** the per-ecosystem report follows the established `docs/benchmarks/<date>/`
  style ([2026-07-18](../benchmarks/2026-07-18/index.md) is the model), so no new reading
  conventions are introduced beyond the interpreted-vs-precompiled section framing.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| templ cannot hit the byte gate — whitespace-control docs are thin and the authoring mechanics are unverified | Spec-time verification spike on the smallest workload before bulk porting; if structurally incapable, Phase 1 Q1.2 policy applies (idiomatic-only, documented exclusion, no silent replacement) | M |
| html/template's contextual encoder emits escaped-entity spellings outside the contract v2 (Q1.1) reconciliation, breaking the encoded-suite gate | Validate both Go engines' actual escape output against the Q1.1 rule at spec time, before any encoded port; prefer engine configuration over normalization where offered, per the Q1.1 resolution | M |
| The stdlib raw-path choice (Q6.1) is made implicitly and the credibility pick's raw numbers get mislabeled or contested | Q6.1 resolved before any raw-suite authoring; the report states explicitly which stdlib surface each suite measured | S |
| The reflection-driven stdlib trails templ so widely that the phase reads as a strawman comparison | The interpreted-vs-precompiled framing (slinso precedent) presents the gap as the ecosystem's own known structure, with each engine's role (stdlib baseline vs compiled peer) labeled; honest-reporting posture throughout | S |
| quicktemplate stretch absorbs effort that belongs to core scope | The conditional inclusion rule is binding: only after templ's controlled track is fully green, and only as template-twin authoring over the existing harness and scaffolding (zero new harness, gate, or contract work); cut first under pressure | S |
| Heddle's composition workloads (composed page, fragment-heavy) map awkwardly onto html/template's associated-template mechanism or templ components, threatening parity | Phase 1 proved every workload byte-portable across four dissimilar .NET engines before export; port the composition workloads early in each engine's sequence so redesign pressure, if any, surfaces while the Phase 1 escalation path is cheap | M |
| Go benchmark stability on the protocol machine (Phase 1 Q1.6 fixes the existing Windows box) undermines benchstat confidence | Per-harness stability settings are resolved in this ecosystem's spec per Q1.6; benchstat's repeated-run statistics are the detection mechanism, and the report publishes variance alongside means | M |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] Controlled-track implementations exist for all eight workloads on the stdlib engine (per the
      Q6.1 resolution) and on templ, and each passes the contract v2 byte gate against the golden
      corpus before any timing — or carries an excluded-with-documented-evidence marker per the
      Phase 1 Q1.2 policy.
- [ ] Both encoded workloads, on every measured Go engine, contain zero occurrences of the raw
      `<script>` XSS payload, contain its escaped form, and reproduce the Japanese UTF-8 string
      intact.
- [ ] Idiomatic-track implementations exist for all eight workloads on both engines, each passing
      the Phase 1 machine-checkable verifier, authored to the Phase 1 Q1.7 evidence standard.
- [ ] All measurements run under Go's testing benchmark harness with benchstat-consumable output
      from repeated runs; allocs/op is reported for every benchmark.
- [ ] The published report labels all allocation figures per-ecosystem-only and not
      cross-comparable, and no table or prose in it juxtaposes Go allocation/GC numbers with
      another runtime's; it carries a clearly-labeled, wall-time-only Heddle reference row (Q2.2 =
      option A) with its wall-time ratio column anchored to that Heddle row (Q6.2), while allocation
      baselines stay within-ecosystem.
- [ ] The report is published as a date-stamped `docs/benchmarks/<date>/` directory with an
      environment block recording the Go toolchain and templ generator versions, a
      reproduce-it-yourself command, and interpreted-vs-precompiled organization.
- [ ] The report contains a dedicated contextual-encoding section covering the encoded suite,
      identifying html/template as the cast's only runtime contextual encoder (Heddle and JTE are
      contextual at compile time), framing encoder deltas as whole-engine architectural
      differences, never as an isolated cost of context analysis, and stating the Phase 1
      confinement caveat: the confined encoded workloads cannot exhibit contextual encoding's
      differentiating benefit, so encoded-suite deltas must not be read as contextual encoding
      being pure overhead.
- [ ] Every workload where html/template or templ beats the other — and any suite where the stdlib
      baseline outperforms the compiled pick on any metric — is reported as prominently as the
      converse (honest-reporting posture), stated in the report's prose, not only visible in
      tables.
- [ ] The report documents the engine selection: both ruled-in picks, hero's exclusion with reason,
      and quicktemplate's status (included via the conditional rule, or not included and why).
- [ ] If quicktemplate is included, it meets every gate above; its inclusion occurred only after
      templ's controlled track was fully green.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A templ controlled-track output differs from the golden corpus by one byte surviving normalization | Parity gate fails loudly before any timing; no benchmark numbers are produced for that workload |
| An encoded workload's html/template output is searched for the raw `<script>` payload | Zero occurrences; the escaped form is present; the Japanese UTF-8 string is present intact |
| The idiomatic verifier is fed a templ output with one data row removed or one payload unescaped | Fail, with the failed check identified |
| The same benchmark is run twice and the outputs are fed to benchstat | benchstat consumes both runs and produces a comparison with variance, per the protocol |
| A reader searches the phase 6 report for a Go-vs-.NET allocation comparison | None exists; the report's own text marks allocation figures as per-ecosystem, not cross-comparable |
| quicktemplate work is proposed while any templ controlled-track workload is still red | The conditional inclusion rule blocks it; core scope proceeds without it |
| A reader asks the report which stdlib surface produced the raw-suite numbers | The report states it explicitly, per the Q6.1 resolution |
| templ proves unable to pass the byte gate on a workload after best-effort authoring | The Phase 1 Q1.2 policy is applied: idiomatic-track retention, documented controlled-track exclusion, no replacement engine without user sign-off |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). Q6.1 (text/template for the raw suites,
html/template for the encoded suite — the one stdlib engine's two paths) and Q6.2 (the program-wide
presentation rule: the wall-time ratio column anchors to the Heddle reference row; non-Heddle
engines are never ranked across ecosystems; non-comparable-metric baselines stay within-ecosystem)
are resolved (user, 2026-07-20) and folded above. The program-wide **Q2.2** resolved to option A —
overriding this phase's earlier working assumption of option B — so the report carries the labeled,
wall-time-only Heddle reference row. Phase 1's Q1.1, Q1.2, Q1.3, Q1.5, Q1.6, and Q1.7 are inherited
resolutions referenced above, not duplicated here.

## External grounding

| Claim | Source |
|---|---|
| Go per-ecosystem precedent organizes engines interpreted vs precompiled; active, 277★; asserts no output equality | [slinso/goTemplateBenchmark](https://github.com/slinso/goTemplateBenchmark) |
| html/template performs built-in context-aware auto-escaping at runtime — the only cast engine to perform context analysis at runtime (Heddle and JTE escape contextually at compile time); raw bypass via `template.HTML`; `{{- -}}` trim | Go html/template documentation; research spike 2 engine notes (grounding dossier, 2026-07-19); [jte.gg](https://jte.gg/) |
| templ: AOT `go generate` codegen to compiled, typed Go; escapes by default with raw-HTML bypass; ~10.4K★; whitespace-control docs thin (assumption to verify in spec) | [a-h/templ](https://github.com/a-h/templ); research spike 2 engine notes (grounding dossier, 2026-07-19) |
| quicktemplate: typed codegen, "up to 20x faster than html/template" claim, trim and raw-output syntax available | [valyala/quicktemplate](https://github.com/valyala/quicktemplate); research spike 2 engine notes (grounding dossier, 2026-07-19) |
| hero's maintenance status is uncertain, grounding its exclusion | Research spike 2 engine notes (grounding dossier, 2026-07-19) |
| Allocation/GC numbers are not comparable across runtimes with different allocator designs; wall time per render is the sole cross-comparable metric | Standing ruling 5 and the [Phase 1 metrics protocol](phase-1-cross-stack-foundation.md); BenchmarkDotNet documentation ([benchmarkdotnet.org](https://benchmarkdotnet.org/)) |
| Controlled-track methodology: semantically-equivalent, disclosed-idiom implementations so the engine, not the algorithm, is measured | Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016); [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet) |
| Encoded-workload shape (untrusted rows, `<script>` payload, Japanese UTF-8) and its context-confinement constraint | [Phase 1](phase-1-cross-stack-foundation.md) design direction; [TechEmpower/FrameworkBenchmarks](https://github.com/TechEmpower/FrameworkBenchmarks) Fortunes rules |
| Honest-reporting posture and report style this phase's publication must match | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Parity gate mechanics (byte-identical after documented normalization, asserted before timing) | [src/Heddle.Performance/Runners/README.md](../../src/Heddle.Performance/Runners/README.md); [Phase 1](phase-1-cross-stack-foundation.md) contract v2 |

Assumptions to verify in the spec (flagged, not grounded here): templ's and quicktemplate's exact
whitespace-control syntax; the concrete escaped-entity output of html/template's and templ's
escaping paths against the contract v2 (Q1.1) reconciliation rule; templ generator and Go toolchain
version pinning; Go benchmark stability settings on the protocol machine chosen under Phase 1 Q1.6.
