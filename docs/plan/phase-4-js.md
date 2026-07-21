# Phase 4 — js

## Header

- **Status:** in progress
- **Goal (one line):** A JS/Node ecosystem comparison — Handlebars and Eta versus the Heddle golden corpus, both fairness tracks, measured under mitata and published per the phase-1 protocol with explicit reach-not-fair-fight framing.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md)
- **Changes an externally-visible contract:** no — consumes the golden oracle corpus, parity contract v2, and the metrics/publication protocol from phase 1 unchanged; its output is a date-stamped report under `docs/benchmarks/<date>/`.

## Goal

This phase ports the eight-workload set (phase 1) to the JS/Node ecosystem and measures two engines
against the Heddle golden corpus: **Handlebars** as the credibility pick — the dominant standalone
template engine by npm reach, at roughly 14–22M weekly downloads (approximate; sources disagree on
the exact figure, which the spec must re-verify before the report cites it) — and **Eta** as the
performance pick — a modern precompiled EJS successor with native layouts/partials and explicit
whitespace-control documentation. Both fairness tracks ship, per the standing ruling: the controlled
track under the byte-identical gate against the golden corpus, and the idiomatic track under
contract v2's functional-equivalence verifier. The deliverable is a per-ecosystem benchmark report
in the repo's established style ([2026-07-18](../benchmarks/2026-07-18/index.md) is the model).

**Why JS is priority 3, and the framing burden that comes with it.** JS/Node is the largest
practitioner population any phase reaches — npm download volume dwarfs every other ecosystem's
distribution channel — which is exactly why it ranks above Python and Go. But every viable JS
competitor is dynamically typed and none is an AOT-compiled typed-model engine: there is no
architectural peer to Heddle here the way Askama (phase 2) and JTE (phase 3) are. A Heddle win on
wall time is therefore the *expected* outcome, and presenting it as proof of engineering superiority
would be a strawman. This phase's distinctive honest-reporting duty is to say so in the published
report itself: JS results are **reach and context evidence** — how Heddle compares against what the
largest practitioner audience actually uses — not fair-fight evidence, which the Rust and JVM phases
carry. The 2026-07-18 intra-.NET run is the standing caution that expected wins are not guaranteed
wins: Handlebars.Net — a port of this very phase's credibility pick — allocated 0.46x Heddle's bytes
on trivial substitution and came within ~8% on large-loop time. Any workload where a JS engine beats
Heddle on any metric is reported as prominently as the wins, per the protocol — and, under Q2.2 (option A), this phase's own report is where the
cross-runtime juxtaposition lands, so the callout duty sits here (the *complete* consolidated
comparison still lives in phase 7).

## Non-goals / scope boundary

- **Other JS template engines.** Two engines per ecosystem is a standing ruling. Specifically:
  - **Pug** is excluded by the structural-exclusion policy (standing ruling; contract v2):
    whitespace-significant indentation syntax cannot target arbitrary byte-exact output. Documented
    exclusion, not curve-graded inclusion.
  - **EJS** — considered and rejected: Eta is its actively-maintained, precompiled successor with
    native layout support; benchmarking both would measure the same lineage twice.
  - **Nunjucks** — considered and rejected: a Jinja2-style engine whose comparison duty is already
    carried by Jinja2 itself in phase 5, with no performance-pick or reach argument strong enough to
    displace either chosen engine.
- **SSR component frameworks are out of scope.** React (and JSX SSR generally) and marko are
  component rendering frameworks, not standalone template engines; comparing them would compare
  framework runtimes, not templating. The only JS templating benchmark precedent found,
  marko-js/templating-benchmarks, is archived (2026-02) and serves as evidence of the vacancy, not
  as a suite to adopt.
- **Node only.** The ruling names the ecosystem "JS/Node". Alternative runtimes (Bun, Deno) are out
  of scope; adding runtimes would multiply the matrix while measuring runtime differences, not
  engine differences. The spec pins one Node version.
- **No HTTP/DB stack, no throughput-under-load** — phase-1 scope boundary, inherited.
- **No changes to the Heddle engine, the golden corpus, or contract v2.** This phase is a consumer.
  A workload that cannot pass the gate here follows contract v2's exclusion/fallback machinery; it
  does not renegotiate the contract.
- **No new cross-comparable metrics.** Wall time per render is the only cross-language-comparable
  number; V8 heap/GC figures versus .NET allocation figures are non-comparable by the standing
  ruling and the phase-1 protocol — referenced here, not restated.

## Design direction

**Harness: mitata, the 2025-26 JS benchmarking best practice.** mitata provides automatic warmup and
— decisively for a JIT runtime — **deoptimization detection**, so a measured render loop that V8
deopts mid-run is flagged rather than silently averaged into the numbers. This directly addresses
the JIT-profile-realism concern the methodology anchors raise for managed runtimes (the SAC 2026
JVM finding has a V8 analogue: an isolated microbenchmark can induce an unrepresentative
optimization profile). Alternative weighed and rejected: **tinybench** — a fine lightweight harness,
but it offers no deopt visibility, and legacy benchmark.js is unmaintained; for a published
cross-stack report the harness that can *see* JIT pathology beats the one that cannot. The spec pins
Node and mitata versions; all runs execute on the single protocol machine (phase-1 Q1.6), and the
environment block records Node, V8, mitata, and engine versions.

**Metrics: wall time per render, with heap/GC conditional.** Wall time per render is the phase's
primary and only cross-comparable metric. Whether the JS report also carries per-ecosystem heap/GC
columns is settled by Q4.1: time-only and stated as such in the report, *unless* the spec verifies
mitata's memory instrumentation is credible (stable across runs, documented semantics) — the
grounding dossier establishes mitata as the harness but not its memory-measurement credibility.
Either way, any memory figures published are per-ecosystem-only and explicitly marked
non-comparable, per the protocol.

**Both tracks, per phase 1's definitions.** Controlled: equivalently-authored, whitespace-free
templates, byte-identical to the golden corpus after contract v2 normalization, asserted before any
timing. Both engines have the mechanics for this — Handlebars has `{{{ }}}` raw output and `~` trim
markers; Eta has an autoEscape flag and explicitly documented whitespace control. Idiomatic:
each engine's official-documentation-pattern implementation under the contract v2 verifier — for
Handlebars that means idiomatic helper/partial usage; for Eta, its native layout system. Compile
happens once outside the timed region in both tracks, consistent with the existing suites; whether
Handlebars uses runtime `compile` or ahead-of-time `precompile` in each track is a spec decision
(the idiomatic track should use whichever its docs present as production-canonical), and cold
parse/compile cost reporting follows phase-1 Q1.3's resolution.

**The Handlebars logic-less constraint is this phase's hardest port.** Handlebars is deliberately
logic-less: control flow exists only through helpers, and the built-in conditional helper is
truthiness-only — the conditional-heavy workload (phase-1 workload 5), with its many mixed
taken/skipped branches, will likely require registered comparison helpers to express at all. The
direction: registered helpers are acceptable in the controlled track as **disclosed idioms** — the
Are-We-Fast-Yet discipline phase 1 adopted explicitly permits documented per-language idioms so long
as the computation is semantically equivalent — with every registered helper disclosed in the
report. If even disclosed helpers cannot hit the byte gate for a workload, the fallback is contract
v2's excluded-cell mechanism (the phase-1 Q1.2 machinery): the workload stays in Handlebars' idiomatic
track, its controlled cell is marked excluded with evidence, and no replacement engine is
introduced. A twin-authoring economy also falls out of the cast: Handlebars shares its template
language with Handlebars.Net, whose parity-passing templates already exist in the intra-.NET suite —
the JS controlled-track templates start from those, not from scratch.

**The Handlebars/Handlebars.Net twin dynamic is a novel side-story — settled by Q4.2.** The same
template language, byte-identical templates, same workloads, same machine, two runtimes (V8 vs
.NET) — no surveyed benchmark has published that comparison. Wall time is cross-comparable under the
protocol, so the comparison is *permitted*, and Q4.2 rules it in: a small, explicitly-labeled
side-note, wall-time only, placed outside the engine-ranking tables, framed as port/runtime context
and never part of the ecosystem verdict. With Q2.2 = option A the JS report carries the cross-runtime
juxtaposition, so the side-note lives here.

## Dependencies & ordering

- **Depends on: phase 1, fully.** No workload may be ported before its golden-corpus entry exists
  (phase-1 rule); the controlled gate needs contract v2's normalization list (including the
  encoded-suite entity-spelling resolution, phase-1 Q1.1) and the machine ruling (phase-1 Q1.6).
- **Independent of phases 2, 3, 5, 6** — priority-ordered but individually cuttable, per the
  phase backbone. Nothing here blocks or is blocked by another ecosystem phase.
- **Internal ordering:** controlled-track twins authored and byte-gate-passing for both engines →
  idiomatic implementations passing the contract v2 verifier → measurement run under mitata →
  per-ecosystem report published.
- **Unblocks:** the JS rows of the phase-7 consolidated report (which aggregates only
  protocol-conformant runs).

## Back-compat / impact

- **Existing suites and reports: untouched.** The intra-.NET suite, its Handlebars.Net twin, and
  the published 2026-07-11 / 2026-07-18 reports are unaffected; this phase adds a new per-ecosystem
  harness area alongside them.
- **Golden corpus and contract v2: consumed read-only.** A corpus regeneration during this phase's
  lifetime is an ordinary git-versioned change (phase-1 Q1.5 — no version-bump or re-run-all-gates
  ceremony); any subsequent JS run re-runs this phase's parity gates against the current corpus.
- **Publication:** one or more new date-stamped `docs/benchmarks/<date>/` directories in the
  established style. No retroactive edits to prior reports.
- **New repo surface:** a Node sub-project with pinned dependencies (exact layout is spec
  territory). It introduces npm dependencies into the repo for the first time; versions are pinned
  and recorded so a future run can reproduce the environment.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Expected-win framing invites strawman criticism (all JS competitors dynamically typed; no architectural peer) | The report itself states the reach-not-fair-fight framing explicitly; architectural-peer duty is carried by phases 2–3; losses reported as prominently as wins — the Handlebars.Net precedent (0.46x alloc, ~8% loop time) shows the outcome is not foreordained | M |
| Handlebars' logic-less design cannot express the conditional-heavy workload for the controlled track | Disclosed registered helpers under the Are-We-Fast-Yet disclosed-idiom rule; fallback to contract v2's excluded-cell mechanism (idiomatic track only) with documented evidence; templates seeded from the parity-passing Handlebars.Net twins | M |
| V8 JIT deopt or unrepresentative optimization profile skews measured numbers | mitata's deopt detection is the harness-selection reason; any flagged deopt in measured code is investigated or documented before the run is published; Node version pinned by the spec | M |
| mitata memory instrumentation proves not credible, leaving the JS report thinner than other ecosystems' | Q4.1's resolution: publish time-only and say so explicitly (heap/GC only if the spec verifies mitata's memory instrumentation) — the protocol already makes memory per-ecosystem-only, so a missing memory column costs no cross-stack claim | S |
| Cross-runtime Handlebars vs Handlebars.Net side-note misread as a .NET-vs-Node runtime shootout | Q4.2's resolution: labeled side-note, wall-time only, outside the engine-ranking tables, with framing text; placed in this report per Q2.2 = option A | S |
| npm dependency/engine version churn between authoring and measurement run | Exact engine, Node, and mitata versions pinned and recorded in the environment block; report is date-stamped per protocol | S |
| Node-on-Windows measurement stability (protocol machine per phase-1 Q1.6 is the Windows/Ryzen box) | mitata's Windows behavior is carried as an assumption to verify, not asserted (see this phase's assumptions-to-verify list): its measurement stability on the Windows protocol machine is spec-verified before any published run; per-harness stability settings are resolved in the spec as phase-1 Q1.6 already requires; environment block records OS and Node build | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] Both engines (Handlebars, Eta) have controlled-track implementations for all eight workloads
      that pass the byte-identical gate against the golden corpus before any timing, or carry a
      documented contract-v2 exclusion with evidence for specific cells.
- [ ] Both engines have idiomatic-track implementations for all eight workloads that pass the
      contract v2 functional-equivalence verifier, each citing the official documentation pattern
      it follows (phase-1 Q1.7 discipline).
- [ ] Every registered Handlebars helper used by a controlled-track template is disclosed in the
      published report.
- [ ] Encoded-suite outputs contain zero occurrences of the raw `<script>` XSS payload, contain
      the escaped form, and carry the Japanese UTF-8 string intact, for both engines.
- [ ] The measurement run uses mitata on the single protocol machine, and the published environment
      block records Node, V8, mitata, and exact engine versions plus a reproduce command.
- [ ] No published number comes from a run in which mitata flagged a deoptimization in measured
      code that was not investigated and documented.
- [ ] The published report contains an explicit framing statement: JS results are reach/context
      evidence, the competitors are dynamically typed with no architectural peer to Heddle, and
      fair-fight evidence lives in the Rust/JVM phases.
- [ ] Any workload where one JS engine beats the other on any reported metric is called out in the
      report prose, not only visible in tables; and because this report carries the labeled Heddle
      reference row (Q2.2 = option A), any workload where Handlebars or Eta beats Heddle on any
      reported metric is called out the same way.
- [ ] The report carries a clearly-labeled, wall-time-only Heddle reference row (Q2.2 = option A)
      with its wall-time ratio column anchored to that Heddle row (Q6.2); allocation/memory
      baselines stay within-ecosystem, and the Handlebars-vs-Handlebars.Net cross-runtime side-note
      (Q4.2) is present, wall-time only and outside the engine-ranking tables.
- [ ] Memory/GC figures, if published (per Q4.1), appear per-ecosystem only and the report text
      marks them non-comparable across runtimes; if not published, the report states the JS suite
      is time-only and why.
- [ ] The Handlebars weekly-download figure, wherever cited, is the spec-verified number, not the
      approximate range in this plan.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A Handlebars or Eta controlled-track output differs from the golden corpus by one byte that survives contract v2 normalization | Parity assertion fails loudly before any timing; no numbers are produced for that suite |
| The conditional-heavy workload cannot hit the byte gate for Handlebars even with disclosed helpers | The controlled cell is marked excluded with documented evidence; the workload still ships in Handlebars' idiomatic track; Eta and all other cells are unaffected |
| An encoded-suite rendered output is searched for the raw `<script>` payload | Zero occurrences; escaped form present; UTF-8 string intact |
| mitata flags a deoptimization inside a measured render loop | The run is not published until the deopt is eliminated or its presence is documented in the report |
| A reader looks for how the report frames a Heddle wall-time win over both JS engines | The framing statement (reach/context, not fair-fight; typed-vs-dynamic asymmetry named) is present in the report prose |
| A reader attempts to compare a JS heap figure with a .NET allocation figure from the published reports | No published table juxtaposes them; the report text marks memory figures per-ecosystem-only and non-comparable |
| Pug (or another whitespace-significant engine) is proposed for inclusion | Contract v2 exclusion policy applies: documented exclusion, no curve-graded inclusion |
| The idiomatic verifier is fed an Eta output with one data row removed or one payload unescaped | Fail, with the failed check identified |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). Q4.1 (time-only unless mitata memory is
spec-verified) and Q4.2 (the Handlebars-vs-Handlebars.Net side-note is in scope) are resolved
(user, 2026-07-20) and folded above. The program-wide **Q2.2** resolved to option A, so this
report carries the labeled, wall-time-only Heddle reference row and the Q4.2 side-note lands here.
Phase 1's Q1.1, Q1.3, Q1.6, and Q1.7 are inherited resolutions referenced above, not duplicated
here.

## External grounding

| Claim | Source |
|---|---|
| Handlebars: precompilable, logic-less with helpers, `{{{ }}}` raw output, `~` trim; ~14–22M weekly npm downloads (figure varies by source — re-verify in spec); 18.4K★ | Research spike 2 engine notes (grounding dossier, 2026-07-19); [handlebarsjs.com](https://handlebarsjs.com/) |
| Eta: modern precompiled EJS successor, native layouts/partials, autoEscape flag, explicit whitespace-control docs | Research spike 2 engine notes (grounding dossier, 2026-07-19) |
| JS 2025-26 benchmarking best practice is mitata (deopt detection) over legacy benchmark.js; tinybench is the lightweight alternative | Research spike 1 methodology anchors (grounding dossier, 2026-07-19) |
| The only JS templating benchmark precedent is archived and dead (2026-02) | [marko-js/templating-benchmarks](https://github.com/marko-js/templating-benchmarks) |
| Controlled-track disclosed-idiom rule (registered helpers as documented per-language idioms) | Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016); [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet); adopted in [phase 1](phase-1-cross-stack-foundation.md) |
| Isolated microbenchmarks can induce unrepresentative JIT profiles on managed runtimes — the concern mitata's deopt detection addresses for V8 | Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the JVM" (ACM SAC 2026, [arXiv:2605.23570](https://arxiv.org/abs/2605.23570)), by analogy to V8's tiered JIT |
| Allocation/GC numbers are not comparable across runtimes; wall time per render is the sole cross-comparable metric | Standing ruling 5, grounded in BenchmarkDotNet documentation ([benchmarkdotnet.org](https://benchmarkdotnet.org/)); protocol in [phase 1](phase-1-cross-stack-foundation.md) |
| Handlebars.Net (the .NET port of this phase's credibility pick) was not uniformly beaten intra-.NET: 0.46x allocation on trivial substitution, within ~8% on large-loop time | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Pug is structurally excluded (whitespace-significant syntax cannot target byte-exact output) | Standing ruling 4; exclusion policy in [phase 1](phase-1-cross-stack-foundation.md) |
| Golden corpus, parity contract v2 (controlled byte gate + idiomatic verifier), and metrics/publication protocol this phase consumes | [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) |

Assumptions to verify in the spec (flagged, not grounded here): the exact Handlebars weekly-download
figure; mitata's memory-instrumentation capability and stability (Q4.1); mitata's measurement
stability on the Windows protocol machine (its cross-platform support is assumed, not
dossier-grounded); Handlebars `precompile`-vs-runtime-`compile` choice per track; Eta's
whitespace-control behavior against the concrete corpus templates.
