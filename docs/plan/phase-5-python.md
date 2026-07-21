# Phase 5 — python

## Header

- **Status:** in progress
- **Goal (one line):** The Python ecosystem comparison — Jinja2 and Mako ported to the eight-workload set on both fairness tracks under the pyperf harness, published as a per-ecosystem report with Python-only memory metrics.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md)
- **Changes an externally-visible contract:** no — this phase consumes phase 1's golden corpus, parity contract v2, and metrics/publication protocol without modifying them; its output is an immutable date-stamped report under `docs/benchmarks/<date>/`.

## Goal

This phase delivers the Python leg of the cross-stack survey: both cast engines — **Jinja2**
(credibility pick) and **Mako** (performance pick) — rendering all eight phase 1 workloads on both
fairness tracks, gated against the golden oracle corpus, measured with pyperf on the protocol
machine, and published as a per-ecosystem report in the repo's established style.

Why Python holds priority 4. Jinja2 is, by download volume (~610M monthly PyPI downloads), the
world's default template engine — beating or losing to Jinja2 is the single most *relatable* result
the survey can produce for the broadest audience, and Jinja's syntax family (Tera, minijinja,
Nunjucks are all clones) makes the Jinja2 numbers a reference point that contextualizes other
ecosystems' results too. But like the JS phase, the Python competitors run on an interpreted,
dynamically-typed runtime: the comparison earns *reach*, not fair-fight evidence, and the phase
carries the same honest-framing burden. CPython-interpreted rendering will very likely lose to
Heddle's AOT-compiled .NET rendering by a large factor. The phase's value is not that headline —
it is quantifying the gap *credibly* (parity-gated output, disclosed methodology, published
scripts) and showing where Python **narrows**: Mako's templates-compiled-to-Python-modules path,
and bulk-output workloads (the large loop, the encoded bulk loop) where per-render dispatch
overhead amortizes across output volume. (Under Q2.2 = option A, the Heddle juxtaposition appears in
this phase's report as a labeled, wall-time-only reference row; the *complete* cross-stack
comparison remains phase 7's.) The freshest precedent for a credible cross-runtime
template comparison is the simonw minijinja-vs-jinja2 study (Oct 2025); its transparency
conventions — published scripts, a realistic template, mean/median/std reporting — are the model
this phase's report mirrors.

## Non-goals / scope boundary

- **No other Python engines.** The cast is a standing ruling (two per ecosystem, already chosen);
  no optional third engine was ruled for Python (only Go carries an optional quicktemplate).
  Specifically excluded, with rationale recorded here so the report can link rather than re-argue:
  - **Django templates** — decided out, not an open question. The cast ruling fixes the two
    engines, and Django's template engine is framework-coupled: its idiomatic home is inside a
    Django project, so a standalone benchmark would measure a configuration few practitioners
    deploy. Adding any engine to the cast requires user sign-off (same posture as phase 1's Q1.2).
  - **Chameleon, Genshi, `string.Template`, and other historical engines** — outside the
    two-engine cast; the survey found no maintained Python template-benchmark suite whose engine
    roster would argue otherwise.
  - **Native-extension engines (e.g. the minijinja Python bindings)** — these would measure a Rust
    engine behind Python bindings, muddying the ecosystem story; the Rust phase (phase 2) covers
    that engine family in its own runtime.
- **GIL and multi-threaded rendering are out of scope.** The whole suite is single-threaded render
  benchmarks by design: phase 1 explicitly folded out throughput-under-concurrency as a
  runtime/server property, not an engine property. The GIL, free-threaded CPython builds, and any
  parallel-render scenario are therefore non-goals here, stated as a decision, not deferred.
- **CPython only.** Alternative runtimes (PyPy, GraalPy) would multiply the matrix and answer a
  runtime question, not a template-engine question; the audience-relatability rationale for
  priority 4 is a CPython audience. The exact CPython version is pinned in this phase's spec per
  the phase 1 protocol (specs pin versions and settings).
- **No consolidated cross-ecosystem tables in this phase's report.** Per the metrics protocol,
  wall time per render is the only cross-language-comparable number and the *consolidated*
  comparison is phase 7's job. This report ranks Jinja2 vs Mako and, per Q2.2 = option A, carries a
  clearly-labeled, wall-time-only Heddle reference row; it publishes no other cross-ecosystem
  tables. Memory metrics appear in this report **only** — Python-only, explicitly labeled not
  cross-comparable.
- **No changes to phase 1 artifacts.** The corpus, contract v2, and protocol are consumed
  read-only; a workload that proves unportable to Python is escalated under contract v2's
  exclusion policy, never patched locally.
- **No HTTP/DB stack, no changes to the Heddle engine, no retroactive edits to published
  reports** — all inherited from phase 1's scope boundary; not restated here.

## Design direction

**Cast rationale: relatability plus the nearest architectural neighbor.** Jinja2 is the
credibility pick — the default engine of the largest language community the survey touches, and
the origin of the syntax family half the survey's engines imitate. Mako is the performance pick —
it compiles templates to Python modules, the closest thing CPython offers to Heddle's
ahead-of-time posture, and it applies no auto-escaping by default, making it the easiest
encoding-off port in the entire survey (the raw suites need no bypass configuration at all).
Jinja2's standalone default is also autoescape-off, so *both* raw-track ports run each engine's
untouched default output path — a fairness property no other ecosystem in the survey gets for
free on both engines.

**Both tracks, per the standing ruling.** The controlled track holds both engines byte-identical
to the golden oracle after contract v2 normalization, asserted before any timing — the
Are-We-Fast-Yet discipline phase 1 encodes. Byte-gate risk is assessed **low** for this ecosystem:
Jinja's whitespace-control lineage (`trim_blocks`, `lstrip_blocks`, `{%- -%}`) is the most mature
in the survey, and Mako's embedded-Python control flow gives the template author direct control
over what is written (its exact whitespace semantics are a spec-verification item, carried in the
assumptions list below). The idiomatic track follows phase 1's Q1.7 resolution: implementations authored
in-repo from each engine's official documentation patterns (Jinja2 `Environment` usage, Mako
`TemplateLookup` with `<%inherit>` layouts), doc pages cited per implementation, gated by the
phase 1 machine-checkable functional verifier.

**Encoded suite: two flat escapers, one shared gate.** For the encoded workloads Jinja2 runs with
autoescape on (its MarkupSafe-backed flat HTML escaper) and Mako runs its escaping-filter path.
Both are flat escapers, so phase 1's context-confinement constraint (untrusted data only in HTML
text and attribute-value positions) keeps the byte gate structurally feasible against Heddle's
contextual encoder; escaped-entity spelling differences are reconciled by whatever phase 1's Q1.1
resolution prescribes — this phase adds no local rules.

**Harness: pyperf, with eyes open about the machine.** pyperf is the protocol-named standard
Python harness (process-spawning, multi-run, statistics-driven). Its best-stability guidance
assumes Linux-style CPU isolation and system tuning, and Q1.6 fixes the protocol machine as the
existing Windows/Ryzen box — making this the most machine-exposed of phases 2–6. Q5.2 resolves that
exposure: this phase accepts the Windows protocol machine as-is, resolves pyperf's concrete
stability settings for it in the spec, and discloses the harness's stability posture and dispersion
statistics in the report's environment block rather than presenting numbers as unconditionally
tight. Separately and later, the same physical machine is re-run on Ubuntu 24.04 as a distinct new
phase (`phase-8-linux-crosscheck.md`); those Linux numbers are published on their own and never
merged or cross-compared with this phase's Windows numbers, so the one-machine protocol for phases
2–7 stays intact.

**Framing duty: quantify, don't dunk.** This report carries the Heddle-vs-Python juxtaposition
(Q2.2 = option A), and its framing opens by stating what the comparison is: an AOT-compiled .NET
engine against interpreted CPython
engines — a cross-runtime gap measurement, not a like-for-like engine craftsmanship contest. The
expected large-factor Heddle wins are reported *and*, with equal prominence, every place Python
narrows the gap or inverts a metric (Mako vs Jinja2 internally, per-workload shape effects,
bulk-output amortization) — the same posture the
[2026-07-18 report](../benchmarks/2026-07-18/index.md) models intra-.NET; the report carries the
Mako-vs-Jinja2 and shape-effect findings alongside the Heddle reference row that Q2.2 places here. Alternatives weighed and rejected: leading with a single headline speedup
factor (invites the universal-superiority claim the repo's reporting style forbids), and skipping
Python entirely as "unfair" (forfeits the survey's most relatable data point; honesty in framing
is the mitigation, not omission).

## Dependencies & ordering

- **Depends on phase 1, fully:** the golden oracle corpus (no workload may be ported before its
  corpus entry exists), parity contract v2 (normalization rules, dual-track gates, exclusion
  policy), the resolution of Q1.1 (escaped-entity reconciliation — blocks this phase's encoded
  suite) and Q1.6 (machine — blocks this phase's measurement runs, and drives its pyperf stability
  posture), and the metrics/publication protocol.
- **Independent of phases 2, 3, 4, and 6.** Priority 4 orders scheduling preference, not a
  technical dependency; this phase is individually cuttable per the program backbone.
- **Internal ordering:** controlled-track ports parity-gated workload-by-workload against the
  corpus → idiomatic implementations authored and verifier-gated → encoded suite gated (after Q1.1
  resolution) → pyperf measurement run on the protocol machine → per-ecosystem report published.
- **Unblocks:** the Python rows of phase 7's consolidated report (which ingests only runs
  produced under the phase 1 protocol).

## Back-compat / impact

- **Heddle engine and the intra-.NET suite:** untouched. This phase adds Python harness and
  template assets in the repo location the phase 1 protocol/spec designates; no .NET code changes.
- **Published reports:** unaffected; this phase adds a new date-stamped `docs/benchmarks/<date>/`
  directory and never edits existing ones.
- **Golden corpus and contract v2:** consumed read-only. If the corpus is regenerated — an ordinary
  git-versioned change (phase 1's Q1.5 resolution: no version-bump or re-run-all-gates ceremony) —
  any new Python run re-runs this phase's parity gates against the current corpus, like any other
  repo change.
- **New surface:** the Python benchmark implementation becomes a maintained reproduction artifact
  — the report's reproduce-it-yourself command must keep working at the published commit, which
  is the same obligation every ecosystem phase carries.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| pyperf's best-stability tuning assumes Linux-style CPU isolation; the protocol machine (Q1.6) is the Windows box, so Python numbers may show wider variance than other ecosystems' | Q5.2: accept the Windows machine as-is, resolve concrete pyperf settings for it in the spec, and disclose the stability posture and dispersion statistics (mean/median/std, pyperf-style) in the report's environment block instead of presenting false precision; a separate later Ubuntu 24.04 cross-check on the same box (Phase 8, `phase-8-linux-crosscheck.md`) is published on its own, never merged with these Windows numbers | M |
| The expected large-factor Heddle win reads as dunking and damages the survey's credibility with exactly the audience Jinja2 was chosen to reach | Framing duty is a design commitment: cross-runtime-gap statement up front, where-Python-narrows findings reported with equal prominence, both tracks labeled, scripts published (simonw-study transparency conventions) | M |
| Mako's whitespace semantics (unverified in the research dossier) complicate the controlled-track byte gate | Spec-verification item before porting begins; Mako's embedded-Python control flow provides direct output control as the fallback authoring style; contract v2's exclusion policy is the documented last resort, never a curve | S |
| Escaped-entity spelling of MarkupSafe/Mako filters differs from the oracle's encoder in the encoded suite | Inherited from phase 1 Q1.1; this phase ports the encoded suite only after Q1.1's reconciliation rule is in contract v2, and validates both engines' escaper output against it before timing | S |
| Ambiguity in what "memory" means for CPython (refcounting + pymalloc, unlike tracing-GC runtimes) produces a per-ecosystem metric that phase 7 or readers over-interpret | Q5.1 fixes the metric: tracemalloc allocated-bytes-per-render as the headline figure (closest in kind to other ecosystems' per-render allocation columns), still carrying the protocol's explicit not-cross-comparable label and a method note in the report | M |
| CPython minor-version performance drift (interpreter optimizations land every release) dates the numbers quickly | Protocol already handles this: version pinned in the spec, recorded in the environment block, date-stamped directory, reproduce command — numbers are explicitly hardware- and date-specific | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] Both Jinja2 and Mako pass the controlled-track byte gate (identical to the golden oracle
      after contract v2 normalization, asserted before any timing) on all eight workloads, or any
      failing engine×workload cell is excluded under contract v2's documented-exclusion policy —
      never silently dropped and never curve-graded.
- [ ] Both engines' raw-track ports run their default (non-escaping) output paths with no
      escape-bypass configuration, and the report says so.
- [ ] Both engines' encoded-suite outputs contain zero occurrences of the raw `<script>` payload,
      contain its escaped form and the intact Japanese UTF-8 string, and pass the encoded byte
      gate under phase 1's Q1.1 reconciliation rule.
- [ ] Both engines have idiomatic-track implementations that pass the phase 1 functional verifier
      for every workload, with the official documentation page(s) each implementation follows
      cited per implementation (phase 1 Q1.7 resolution).
- [ ] A pyperf measurement run covering both tracks and both engines executes on the phase 1
      protocol machine (the Windows box per Q1.6/Q5.2), and the published report includes an
      environment block (OS, CPU, CPython version, engine versions, pyperf version, commit), a
      reproduce-it-yourself command, and dispersion statistics (mean/median/std) per benchmark.
- [ ] The report notes the planned separate Ubuntu 24.04 cross-check on the same physical machine
      (Phase 8, `phase-8-linux-crosscheck.md`) and states that those Linux numbers, when published,
      are not merged or cross-compared with this phase's Windows numbers.
- [ ] The report is published as a date-stamped `docs/benchmarks/<date>/` directory in the
      existing style; wall time per render is the only metric eligible for cross-language
      comparison, and the report carries a clearly-labeled, wall-time-only Heddle reference row
      (Q2.2 = option A) with its wall-time ratio column anchored to that Heddle row (Q6.2).
- [ ] Memory figures appear only in this per-ecosystem report, use the metric Q5.1 resolves
      (tracemalloc allocated-bytes-per-render), and carry the explicit not-cross-comparable label;
      no table juxtaposes them with another runtime's numbers.
- [ ] The report's prose states the cross-runtime framing up front and includes a
      where-Python-narrows finding set (at minimum: the Mako-vs-Jinja2 internal comparison and
      per-workload shape effects), stated in prose, not only visible in tables; and because this
      report carries the Heddle reference row (Q2.2 = option A), every Python narrowing or inversion
      is reported as prominently as any Heddle win.
- [ ] The exclusion rationale for Django templates, legacy engines, and native-extension engines
      is linked from the report (to this phase document or the contract), not re-argued ad hoc.
- [ ] All benchmark scripts and templates needed to reproduce the run are committed at the
      published commit.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A Jinja2 controlled-track output differs from the golden oracle by one byte that survives contract v2 normalization | The parity assertion fails loudly before any timing; no Python numbers are produced for that suite |
| A Mako encoded-suite template is misauthored so the escaping filter is skipped on one field | The encoded gate fails: the raw payload is detected in output; the run aborts before timing |
| The idiomatic verifier is fed a Python engine's output with one data row removed or one payload unescaped | Fail, with the failed check identified (phase 1 verifier contract) |
| A reader searches the published report for any cross-runtime memory comparison | None exists; the Python memory table carries the not-cross-comparable label and a metric-method note |
| The pyperf run on the protocol machine shows high dispersion for a benchmark | The report publishes the dispersion statistics and the environment block's stability-posture note; unstable results are disclosed, not suppressed or silently re-run until favorable |
| The golden corpus is regenerated — an ordinary git-versioned change (phase 1's Q1.5 resolution) — after this phase has shipped | Any subsequent Python run re-runs this phase's parity gates against the current corpus, like any other repo change; no separate version-bump or re-run-all-gates ceremony is triggered |
| The published report's reproduce command is run at the published commit | The run completes and regenerates results of the same shape (same benchmarks, same gates passing); numbers may differ within disclosed hardware/date variance |
| A reader asks why minijinja's Python bindings or Django templates are absent | The report links to the recorded exclusion rationale; no ad-hoc justification is invented |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). Q5.1 (tracemalloc
allocated-bytes-per-render as the headline memory figure) and Q5.2 (accept the Windows protocol
machine as-is, with a separate later Ubuntu 24.04 cross-check on the same box added as Phase 8,
`phase-8-linux-crosscheck.md`) are resolved (user, 2026-07-20) and folded above. The program-wide
**Q2.2** resolved to option A, so this report carries the labeled, wall-time-only Heddle reference
row. Inherited resolutions referenced above, not duplicated: phase 1's **Q1.1** (escaped-entity
reconciliation), **Q1.6** (Windows benchmark machine), **Q1.3** (cold parse/compile reporting —
Mako's module compilation and Jinja2's bytecode compilation are reported per-ecosystem only),
**Q1.5** (corpus is git-versioned; regeneration is an ordinary versioned change), and **Q1.7** (the
idiomatic evidence standard).

## External grounding

| Claim | Source |
|---|---|
| Jinja2: ~610M monthly PyPI downloads — the world's default template engine; compiles templates to Python bytecode; autoescape off by default standalone; `trim_blocks`/`lstrip_blocks`/`{%- -%}` whitespace control | Research spike 2 engine notes (grounding dossier, 2026-07-19); Jinja documentation ([jinja.palletsprojects.com](https://jinja.palletsprojects.com/)); [PyPI: Jinja2](https://pypi.org/project/Jinja2/) |
| Mako: compiles templates to Python modules; no auto-escaping by default (easiest encoding-off port in the survey); embedded Python control flow; `<%inherit>` layouts | Research spike 2 engine notes (grounding dossier, 2026-07-19); Mako documentation ([makotemplates.org](https://www.makotemplates.org/)) |
| pyperf is the standard Python benchmark harness; its best-stability guidance assumes Linux-style CPU isolation/system tuning | Research spike 1 harness facts (grounding dossier, 2026-07-19); phase 1 Q1.6 (resolved: Windows box); pyperf documentation ([pyperf.readthedocs.io](https://pyperf.readthedocs.io/)) |
| No maintained Python template-benchmark suite exists; the closest recent precedent is the minijinja-vs-jinja2 study (Oct 2025): realistic ~65 KB e-commerce template, published transparent scripts, mean/median/std reporting | Research spike 1 survey (grounding dossier, 2026-07-19); simonw/research, minijinja-vs-jinja2 study |
| Controlled-track methodology: semantically-equivalent, equivalently-authored implementations so the engine, not the algorithm, is measured | Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016); [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet) |
| Allocation/GC numbers are not comparable across runtimes with different allocator designs; wall time per render is the sole cross-comparable metric | Standing ruling 5 (grounding dossier); [phase 1 metrics & publication protocol](phase-1-cross-stack-foundation.md) |
| Honest-reporting posture: losses and narrowings reported as prominently as wins; no universal-superiority claims; numbers hardware- and date-specific | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Parity gate, golden corpus, dual-track definitions, exclusion policy, workload set (eight workloads incl. two encoded) | [phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md); parity contract v1 in [src/Heddle.Performance/Runners/README.md](../../src/Heddle.Performance/Runners/README.md) |

Assumptions to verify in the spec (flagged, not grounded here): Mako's exact whitespace-control
semantics for controlled-track authoring; the concrete escaped-entity output of MarkupSafe and
Mako's escaping filter (feeds phase 1 Q1.1's reconciliation rule); the maintenance status of any
excluded legacy engine, should the report choose to name it beyond the cast-ruling rationale.
