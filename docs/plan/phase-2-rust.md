# Phase 2 — rust

## Header

- **Status:** in progress
- **Goal (one line):** A published Rust-ecosystem comparison — Askama and Tera rendering the full Phase 1 workload set on both fairness tracks under Criterion — that makes "Heddle is fast" falsifiable against its closest architectural peer.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) (golden oracle corpus, parity contract v2, metrics/publication protocol — all consumed, none modified)
- **Changes an externally-visible contract:** no — this phase is the first external consumer of parity contract v2 and the golden corpus; its only published artifact is an additive date-stamped report under `docs/benchmarks/<date>/`. Any contract ambiguity it uncovers is fed back as a documented contract erratum, never patched locally.

## Goal

This phase delivers the Rust ecosystem comparison: **Askama** (performance/peer pick) and **Tera**
(credibility pick) each ported to all eight Phase 1 workloads on both fairness tracks, measured
under Criterion, and published as a per-ecosystem report in the repo's established honest style.

Rust is priority 1 by standing ruling, and the reason is the fair-fight argument. Every engine
Heddle has beaten so far — Fluid, Scriban, DotLiquid, Handlebars.Net
([2026-07-11](../benchmarks/2026-07-11/index.md), [2026-07-18](../benchmarks/2026-07-18/index.md))
— processes its templates at runtime with no ahead-of-time typed-codegen step. An AOT-compiled
typed engine outrunning runtime-processing engines is the expected
result, and a skeptical reader can dismiss the whole body of evidence as a strawman. Askama is the
one engine in the entire five-ecosystem survey with Heddle's own architecture — compile-time code
generation from templates, typed models, zero runtime parsing, native-code output — sitting in the
ecosystem with the presumed-highest performance ceiling. The Heddle-vs-Askama match-up is therefore
the single most credibility-bearing result of the program: if Heddle's wall times stand near
Askama's, the claim "a compiled template engine on .NET competes with the best compiled engines
anywhere" is demonstrated; if they don't, the honest-reporting protocol publishes that just as
prominently, and the claim dies in public rather than surviving unfalsifiable. Either outcome is
the payoff. Tera supplies the complementary credibility anchor: the highest-all-time-download Rust
engine, runtime-interpreted and Jinja2-style, giving the Rust report the same
runtime-engine-vs-compiled-engine spread the .NET suite already has.

A second, structural reason Rust goes first: it is the hardest first consumer of Phase 1's
contract. If the golden corpus, the byte gate, and the dual-track definitions survive contact with
a compile-time derive-macro engine and a runtime Jinja dialect in a non-GC native runtime, the
remaining ecosystems (JVM, JS, Python, Go) inherit a shaken-out contract instead of discovering
its gaps in parallel.

## Non-goals / scope boundary

- **No other Rust engines.** minijinja, sailfish, ructe, horrorshow, markup, handlebars-rust and
  the rest of the 17-engine community roster are out. The standing ruling fixes two engines per
  ecosystem (credibility + peer), and the Rust pair is already cast. minijinja is the one
  alternative weighed seriously — see Design direction for why it lost the slot rather than being
  silently ignored. maud is excluded by structure under the contract's exclusion policy (macro
  DSL, not file-based templating), with the documented reason, not graded on a curve.
- **No SSR/component frameworks.** leptos, yew, dioxus and similar render HTML but are component
  frameworks, not template engines. The active Rust community suite
  (askama-rs/template-benchmark) casts a wide net whose roster blurs the engine-vs-framework
  line; this phase explicitly does not follow that precedent — the comparison set is template
  engines only, so the report measures templating and nothing else.
- **No HTTP or database stack, no throughput-under-load** — Phase 1's scope boundary carries
  through unchanged.
- **No modifications to the golden corpus or parity contract v2.** This phase consumes them. A
  corpus defect discovered here is fixed upstream as an ordinary git-versioned corpus change (Q1.5),
  not a local fix; a contract ambiguity becomes a documented erratum.
- **No engine patches or forks.** Askama and Tera are measured as released on crates.io, at
  versions pinned in the spec — the same "engines as published" posture the intra-.NET suite
  uses for its twins.
- **No cross-language verdicts.** This report publishes Rust-track results under the Phase 1
  protocol and — per Q2.2 (option A) — carries a clearly-labeled, wall-time-only Heddle reference
  row so the headline Heddle-vs-Askama comparison is visible here; the *complete* consolidated
  cross-stack comparison remains Phase 7's job. The reference row is a labeled excerpt of the Phase
  1 protocol numbers, not a verdict.
- **No cold-compile methodology decisions.** Whether cold parse/compile cost gets any cross-stack
  reporting is fixed by Phase 1's Q1.3 resolution (per-ecosystem only, labeled non-comparable);
  this phase implements it and does not re-open it.

## Design direction

**Engine pair: peer + incumbent, mirroring the .NET suite's spread.** Askama takes the peer slot
on architecture: compile-time codegen via derive macro, typed struct models, escaping decided at
compile time — the closest analogue to Heddle's AOT compilation and typed models in the survey —
with real ecosystem weight (~30M crates.io downloads, active askama-rs org, and the community's
benchmark suite maintained under its umbrella). Tera takes the credibility slot on install base:
the highest all-time downloads of any Rust template engine, runtime-interpreted, Jinja2-family —
the engine a practitioner most likely already knows. Together they bracket the ecosystem the same
way the .NET suite brackets its own (compiled Heddle vs runtime-processing twins), which keeps the Rust
report legible next to the existing ones.

**minijinja, considered and rejected.** minijinja is the third engine with genuine momentum (the
survey notes recent momentum shifting toward Askama *and* minijinja, and the strongest recent
Python-adjacent benchmarking precedent is a minijinja study). It loses the slot on category
overlap: minijinja is, like Tera, a runtime-interpreted Jinja2-dialect engine, so including it
would spend the ecosystem's second slot duplicating Tera's dimension instead of adding one, while
Tera holds the larger historical install base that the credibility slot exists to capture. This
is a documented trade-off under the fixed two-engine ruling, not an oversight; no open question
remains here.

**Both tracks, as ruled.** The controlled track ports the eight workloads as
equivalently-authored, whitespace-free Askama and Tera templates, gated **byte-identical (after
contract v2 normalization) against the Phase 1 golden corpus** before any timing — the
Are-We-Fast-Yet discipline, applied for the first time across a language boundary. The idiomatic
track authors each workload the way the engine's own documentation teaches (Askama's inheritance
and typed structs; Tera's Jinja-style blocks and context), gated by contract v2's
machine-checkable functional-equivalence verifier. The encoded suite runs each engine's escaping
path (Askama's default compile-time escaping; Tera's autoescape) and enforces the Fortunes rule
from the contract. Raw-suite ports use each engine's documented escaping-disable mechanism
(Askama `escape = "none"`, Tera's autoescape-off configuration), exactly as the .NET twins run
their non-encoding paths today.

**Harness: Criterion, comparability by protocol.** Criterion.rs is the Rust standard and the
harness the Phase 1 protocol names for this ecosystem. It is statistics-driven — automatic
warmup, linear-regression-based estimates, 95% confidence intervals — where BenchmarkDotNet
stages pilot/warmup phases and reports mean-based summaries with outlier handling. Both are
rigorous; their statistical models differ. Comparability of the one cross-language number (wall
time per render) is preserved the way the Phase 1 protocol demands: same machine, same workloads,
same gate-before-timing ordering, and the published report stating exactly which point estimate
each harness contributed alongside its dispersion — the statistic mapping is fixed by Q2.1: each
harness's default central-tendency point estimate, with dispersion (CI / stddev) published
alongside and the mapping documented once in the Phase 1 metrics protocol's report format. What is
*not* attempted: forcing one harness's methodology onto the other's ecosystem, which would
sacrifice the "standard harness per ecosystem" credibility the protocol is built on.

**Cold-start fairness by disclosure, not adjustment.** Askama (like Heddle) has no runtime parse
step — its cost sits at build time — while Tera parses templates at startup. Steady-state render
time, the protocol's comparable number, is fair to both because every engine is measured doing
the same job on a warm engine; the report discloses each engine's compilation model so readers
do not misread steady-state numbers as hiding or containing parse cost. Whether cold
parse/compile cost is additionally reported is governed entirely by Phase 1 Q1.3's resolution
(per-ecosystem only, labeled non-comparable); this phase implements that resolution
and adds nothing to it.

**Allocations: per-ecosystem, mechanism pinned in spec.** Rust-side allocation figures are
reported inside the Rust report only, explicitly labeled not-cross-comparable, per standing
ruling. Criterion has no built-in allocation diagnoser (unlike BenchmarkDotNet's
`[MemoryDiagnoser]`), so the spec pins the instrumentation mechanism; the plan constrains only
the reporting rule, not the tooling.

**Report in the existing style.** One date-stamped `docs/benchmarks/<date>/` directory in the
mold of the [2026-07-18 report](../benchmarks/2026-07-18/index.md): environment block (rustc and
crate versions, machine, commit), reproduce-it-yourself command, per-suite artifacts, and prose
that reports every workload where Heddle's numbers trail an engine's as prominently as where they
lead — for this phase in particular, that posture is the product. Per the Phase 1 publication
protocol (Q2.2 = option A, Q6.2), the report carries a clearly-labeled, wall-time-only Heddle
reference row sourced from the Phase 1 protocol run, and its wall-time ratio column anchors to that
Heddle row; allocation baselines stay within-ecosystem.

## Dependencies & ordering

- **Depends on:** Phase 1 complete — specifically the golden corpus (no workload may be ported
  before its corpus entry exists), parity contract v2 (both gate definitions and the exclusion
  policy), and the metrics/publication protocol (harness naming, single machine per Phase 1 Q1.6,
  wall-time-only comparability rule). Phase 1 resolutions Q1.1 (encoded-suite entity-spelling
  reconciliation), Q1.3 (cold-compile reporting), Q1.6 (machine) and Q1.7 (idiomatic evidence
  standard) bind here.
- **Internal ordering:** controlled-track ports gated green against the corpus → idiomatic-track
  ports gated green by the verifier → encoded suite gated (including the Fortunes rule) →
  Criterion measurement runs → per-ecosystem report published. Parity before timing is a hard
  ordering, per contract.
- **Unblocks:** Phase 7's Rust rows. Phases 3–6 do not depend on this phase (they are
  independent and individually cuttable by the standing ruling), but they inherit every contract
  erratum this phase surfaces — a second-order reason it runs first.

## Back-compat / impact

- **Published reports:** unaffected; the Rust report is a new date-stamped directory. Existing
  intra-.NET suites and their numbers are untouched.
- **Golden corpus & contract v2:** consumed read-only. This phase is their first external
  consumer, which makes it the de-facto acceptance test of Phase 1's deliverables: if the
  contract's normalization rules or verifier definitions prove ambiguous under Rust, the
  ambiguity is recorded against Phase 1's artifacts (erratum or, for corpus defects, a Q1.5
  git-versioned corpus change) so phases 3–6 never re-discover it.
- **Heddle engine and .NET suite:** no changes. No Heddle code, template, or twin is modified by
  this phase.
- **Repository surface:** adds a Rust benchmark workspace (location and layout are spec
  decisions) that is not part of the .NET build and cannot affect existing CI or packaging.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Askama's whitespace-control syntax is flagged **unverified** in the research dossier; the controlled track's byte gate depends on precise trim behavior | Carried as an assumption-to-verify in the spec (as Phase 1 already flags): the spec verifies trim semantics against Askama's documentation and actual output before any template is authored; whitespace-free authoring (the v1 discipline) minimizes exposure by leaving the normalizer little to reconcile | M |
| Askama's raw path (`escape = "none"`) or extension-inferred escaping interacts badly with the raw suite's requirements | Escaping mode is pinned per template in the spec and proven by the byte gate itself — a wrongly-escaped output cannot pass parity against the raw-suite oracle | S |
| Tera's flat HTML-only escaper spells entities differently from the oracle on the encoded suite | Governed by Phase 1 Q1.1's resolution (canonicalizing normalization or engine configuration); this phase applies the resolved rule and reports to Phase 1 if Tera's output falls outside it | M |
| Criterion and BenchmarkDotNet produce statistically different summary numbers for "wall time per render", undermining the one cross-comparable metric | Q2.1's resolution pins the statistic mapping (each harness's default central-tendency point estimate, dispersion alongside, documented once in the protocol's report format); both harnesses' dispersion is published alongside point estimates; same machine and workloads per protocol | M |
| Compile-time engines' absence of runtime parse cost is read as an unfair advantage (for Askama *and* Heddle) over Tera | Disclosure in the report of each engine's compilation model; steady-state render is the comparable number by protocol; cold-cost reporting follows Phase 1 Q1.3, not ad-hoc judgment | S |
| A workload shape (composed page's fragment sequence, fragment-heavy's dozens of calls) maps awkwardly onto Askama's inheritance/include model despite the common-denominator constraint | Phase 1 deliberately constrained workloads to features every cast engine has and parity-proved each across four dissimilar .NET engines; the spec maps each workload feature to a documented Askama construct before porting; a genuine impossibility invokes the contract's exclusion policy with evidence — never a quiet workload tweak | M |
| Askama beats Heddle on some or all workloads | Not mitigated — published, per the honest-reporting protocol. This outcome is within expectations for a native-code peer and is precisely what makes the comparison credible; suppressing or softening it would cost more than losing does | S |
| Rust toolchain behavior on the protocol machine (Phase 1 Q1.6: the existing Windows/Ryzen box) differs from the Linux environments most Rust benchmarking lore assumes | Criterion's Windows behavior is carried as an assumption to verify, not asserted (see this phase's assumptions-to-verify list): its measurement stability on the Windows protocol machine is spec-verified before any published run; the spec records rustc/toolchain versions in the environment block and resolves any Windows-specific harness settings, as the protocol already requires per ecosystem | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] All eight Phase 1 workloads have Askama and Tera implementations on **both** tracks — 32
      implementations total — or, for any missing cell, a documented exclusion under contract
      v2's policy with evidence (expected count of exclusions: zero, since both engines cover
      the common-denominator feature set).
- [ ] Every controlled-track implementation passes the byte-identical gate against the golden
      corpus (after contract v2 normalization) in an assertion that runs automatically before
      any timing; no benchmark number exists for an implementation whose gate has not passed.
- [ ] Every idiomatic-track implementation passes its workload's contract v2 functional-
      equivalence verifier before timing.
- [ ] Both encoded workloads, on both engines and both tracks, contain zero occurrences of the
      raw `<script>` payload, contain its escaped form, and preserve the Japanese UTF-8 string
      intact.
- [ ] Measurement runs use Criterion at a spec-pinned version, on the Phase 1 protocol machine,
      and the published tables state the harness point estimate used and its dispersion (per
      Q2.1's resolution).
- [ ] Rust-side allocation figures, if present, appear only in the per-ecosystem report and
      carry the explicit not-cross-comparable label; no table juxtaposes them with any other
      runtime's allocation numbers.
- [ ] Exactly one new `docs/benchmarks/<date>/` directory is published containing the Rust
      report: environment block (machine, OS, rustc, Askama/Tera/Criterion versions, commit),
      reproduce-it-yourself command, per-suite artifacts, both tracks labeled, and each
      engine's compilation model (build-time vs runtime parse) disclosed.
- [ ] Every workload where Heddle's protocol numbers trail Askama or Tera on wall time is
      reported in the prose as prominently as any workload where they lead (this report carries the
      labeled Heddle reference row per Q2.2's resolution — option A).
- [ ] The report makes no cross-language claim beyond what the protocol allows (wall time per
      render), and defers consolidated verdicts to Phase 7.
- [ ] Any contract-v2 ambiguity or corpus defect discovered during porting is recorded against
      Phase 1's artifacts (erratum or a Q1.5 git-versioned corpus change) — zero silent local workarounds.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| An Askama controlled-track output differs from the golden corpus by one byte that survives normalization | Parity assertion fails loudly before any timing; no Criterion numbers are produced for that cell |
| A Tera encoded-suite output is searched for the raw `<script>` payload | Zero occurrences; escaped form present; Japanese UTF-8 string intact — else the gate fails and timing is blocked |
| An idiomatic-track output with one row removed, one section reordered, or one payload unescaped is fed to the contract v2 verifier | Fail, with the failed check identified |
| The full Rust measurement run completes | A date-stamped `docs/benchmarks/<date>/` directory exists with environment block, reproduce command, both tracks labeled, and per-suite artifacts |
| Askama posts a better wall time than Heddle's protocol number on any workload | The report's prose states it plainly and as prominently as any Heddle win; no reframing |
| A reader tries to compare the Rust report's allocation figures with the .NET report's | The Rust report's own text marks allocations per-ecosystem-only and not cross-comparable |
| A workload genuinely cannot be expressed byte-identically in a cast engine despite best-effort authoring | The contract's exclusion policy applies: documented evidence, cell marked excluded, engine stays in the idiomatic track; escalated per Phase 1 Q1.2's pattern, never silently dropped |
| A contract v2 normalization rule proves ambiguous when applied to Rust output | An erratum is recorded against the Phase 1 contract artifact; phases 3–6 inherit the clarified wording |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). Q2.1 (harness statistic mapping) and
the program-wide Q2.2 (option A — this report carries a labeled, wall-time-only Heddle reference
row) are resolved (user, 2026-07-20) and integrated above.

## External grounding

| Claim | Source |
|---|---|
| Askama: compile-time codegen via derive macro, typed struct models, extension-inferred escaping with `escape = "none"`, `{%- -%}` trim; Heddle's closest architectural peer in the survey; ~30M crates.io downloads; active askama-rs org | Research spike 2 engine notes (grounding dossier, 2026-07-19); [askama.rs](https://askama.rs) |
| Tera: runtime-interpreted Jinja2-like; autoescape disable via `autoescape_on(vec![])` (flat HTML-only escaper); full Jinja-style whitespace control; highest all-time downloads among Rust template engines; momentum shifting toward Askama/minijinja | Research spike 2 engine notes (grounding dossier, 2026-07-19) |
| Rust per-ecosystem benchmark precedent: rosetta-rs suite archived 2023, succeeded by the active askama-rs suite (Criterion, 17 engines, "big table" + "teams" workloads); roster breadth blurs the engine-vs-framework boundary this phase avoids | [askama-rs/template-benchmark](https://github.com/askama-rs/template-benchmark); research spike 1 survey (grounding dossier, 2026-07-19) |
| Criterion.rs is statistics-driven: automatic warmup, linear-regression estimates, 95% confidence intervals | Research spike 1 harness facts (grounding dossier, 2026-07-19); Criterion.rs documentation |
| BenchmarkDotNet stages warmup/pilot phases automatically and reports mean-based summaries; allocation numbers are not cross-runtime comparable | [benchmarkdotnet.org](https://benchmarkdotnet.org/); standing ruling 5 |
| Controlled-track methodology (semantically-equivalent, equivalently-authored implementations so the engine is measured) | Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016); [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet) |
| Idiomatic-track posture (per-implementation idiomatic authoring, functional gate) | TechEmpower posture per research spike 1 (grounding dossier, 2026-07-19); [TechEmpower/FrameworkBenchmarks](https://github.com/TechEmpower/FrameworkBenchmarks) |
| maud excluded by structure (macro DSL, not file-based templating) | Research spike 2 exclusions (grounding dossier, 2026-07-19); standing ruling 4 |
| Existing .NET comparisons cover only competitors with no ahead-of-time typed-codegen step (Fluid, Scriban, DotLiquid, Handlebars.Net — all process templates at runtime) — the basis of the strawman objection Rust answers | [2026-07-11 report](../benchmarks/2026-07-11/index.md); [2026-07-18 report](../benchmarks/2026-07-18/index.md); [Runners README](../../src/Heddle.Performance/Runners/README.md) |
| Honest-reporting posture: losses reported as prominently as wins; workload-shape-dependence already demonstrated | [2026-07-18 report](../benchmarks/2026-07-18/index.md) |
| Golden corpus, parity contract v2 (both gates, exclusion policy), metrics/publication protocol, and the Phase 1 resolutions Q1.1/Q1.2/Q1.3/Q1.5/Q1.6/Q1.7 referenced here | [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) |

Assumptions to verify in the spec (flagged, not grounded here — inherited from the dossier's
unverified-items list): Askama's exact whitespace-control syntax and trim semantics; the precise
escaped-entity output of Askama's and Tera's escapers (feeds Phase 1 Q1.1's resolved rule); the
Rust allocation-instrumentation mechanism compatible with Criterion; Criterion's measurement
stability on the Windows protocol machine (its cross-platform behavior is assumed, not
dossier-grounded).
