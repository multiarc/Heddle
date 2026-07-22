# Phase 1 — cross-stack-foundation

## Header

- **Status:** in progress
- **Goal (one line):** A parity-proven, expanded workload set with a Heddle golden-output corpus, a cross-language parity contract (v2), and a metrics/publication protocol that every ecosystem phase (2–6) measures against.
- **Depends on:** nothing
- **Changes an externally-visible contract:** yes — introduces parity contract v2 and the golden oracle corpus as the contract every later phase consumes; the existing intra-.NET parity contract ([Runners README](../../src/Heddle.Performance/Runners/README.md)) remains valid for the existing suites and is extended, not broken.

## Goal

This phase turns Heddle's existing intra-.NET benchmark credibility work into a foundation that five
language ecosystems can build on independently. Today the repo has three parity-checked workloads
(composed page, trivial substitution, large loop) held byte-identical across Heddle and four .NET
twins ([2026-07-11](../benchmarks/2026-07-11/index.md), [2026-07-18](../benchmarks/2026-07-18/index.md)),
but the workload set is too narrow to defend cross-stack claims, has no encoding-ON coverage, and its
parity contract is written for one runtime.

Phase 1 delivers four things, in dependency order:

1. **An expanded workload set** — the three existing workloads retained unchanged as the cross-stack
   anchor set, plus three new raw workloads that add the variability dimensions the current set lacks,
   plus a separate two-workload **encoding-ON suite** (Fortunes-shaped: UTF-8 + XSS-escaped untrusted
   rows) that measures each engine's escaping path. Every new workload is implemented and
   parity-proven in the existing intra-.NET suite (Heddle + Fluid, Scriban, DotLiquid,
   Handlebars.Net) **before** any cross-language port exists, so each workload arrives in phases 2–6
   with a demonstrated-passable parity harness.
2. **The golden oracle corpus** — Heddle-rendered golden outputs, one per workload, exported with
   recorded byte length, content hash, and generating commit, as the single cross-language parity
   reference.
3. **Parity contract v2** — the normalization rules extended from the intra-.NET contract to
   cross-language use, the dual-track gate definitions (controlled = byte-identical; idiomatic =
   machine-checkable functional equivalence), and the exclusion policy for structurally-incapable
   engines.
4. **The metrics & publication protocol** — wall time per render as the only cross-language-comparable
   number, allocations/GC per-ecosystem-only and explicitly marked non-comparable, per-ecosystem
   standard harnesses, one machine, date-stamped publication under `docs/benchmarks/<date>/` in the
   repo's established honest-reporting style (losses reported as prominently as wins — the
   [2026-07-18 report](../benchmarks/2026-07-18/index.md) is the model).

Why this phase exists at all: the research survey found **no existing benchmark that is both
cross-language and template-focused**, and no surveyed project enforces byte-identical cross-engine
output. The byte gate is therefore the project's credibility differentiator — and it is far cheaper
to prove each workload can pass that gate inside one runtime with an existing harness than to debug
parity failures across five ecosystems simultaneously.

## Non-goals / scope boundary

- **Cross-language ports are not in this phase.** Phases 2–6 (Rust, JVM, JS/Node, Python, Go — in
  that priority order) port the workloads; phase 1 only makes them portable and hands over the corpus
  and contract. The seam is explicit: phase 1 ends where a per-ecosystem harness begins.
- **PHP and Ruby are non-goals of the whole plan** (standing ruling). Rationale, recorded here at
  plan level (the plan index links here rather than restating it): the five chosen ecosystems already
  cover the audience Heddle's positioning targets, each additional ecosystem costs a full phase of
  engine research, twin authoring, parity debugging, and harness work, and the user priority-ranked
  the ecosystems with PHP and Ruby below the cut line. Additionally, the flagship engines of both
  (Blade, ERB/Haml ecosystems) skew either framework-coupled or whitespace-significant, which would
  concentrate exclusions rather than comparisons. This is a scope decision, not a quality judgment on
  those ecosystems.
- **No HTTP or database stack.** The suite measures template rendering in isolation. TechEmpower's
  Fortunes workload contributes its *shape* (untrusted rows, UTF-8, mandatory escaping) to the
  encoded suite; its throughput-under-load, full-stack measurement model is explicitly not adopted —
  a template-only TechEmpower variant was discussed upstream and never built, which is the gap this
  project fills.
- **No changes to the Heddle engine.** The composed-page workload's documented fragment-sequence
  shape (the `@<<` entry-point behavior recorded in the
  [Runners README](../../src/Heddle.Performance/Runners/README.md)) is carried as-is; changing
  composition semantics is an engine concern outside the benchmark workstream.
- **No new .NET competitor engines** and no re-litigation of the engine cast (standing ruling: two
  engines per ecosystem, already chosen).
- **No retroactive edits to published reports.** The 2026-07-11 and 2026-07-18 runs are date-stamped
  historical artifacts and stay untouched.

## Design direction

**Foundation-first, oracle-out.** New workloads land in the intra-.NET suite first because that suite
already has the only working byte-parity harness (the parity assertion the Runners README documents,
which gates timing) and four
independent twin engines to validate portability against. A workload that four dissimilar .NET
engines (two interpreted Liquid dialects, one Scriban, one Handlebars) can render byte-identically is
strong evidence for the seven cross-language engines that, like the twins, have no ahead-of-time
typed-codegen step — whether they interpret their parsed templates at runtime (Tera, Thymeleaf,
html/template) or compile them to untyped host code or bytecode (Handlebars, Eta, Jinja2, Mako) —
and a necessary-but-not-sufficient screen for the three ahead-of-time typed-codegen engines
(Askama, JTE, templ), whose composition-mapping and whitespace-control porting risk the owning
phases (2, 3, 6) carry in their own risk tables; a workload that fails even this screen gets redesigned
before it costs five ecosystems any effort. Once a workload passes intra-.NET parity, Heddle's output
becomes the exported golden oracle — the corpus, not the .NET twins, is what phases 2–6 compare
against.

**Dual-track fairness, per the standing ruling.** The controlled track follows the
Are-We-Fast-Yet discipline (Marr, Daloze & Mössenböck, DLS 2016): semantically-equivalent,
equivalently-authored, whitespace-free templates with a byte-identical output gate, so the *engine*
is measured, not the template author. The idiomatic track follows the TechEmpower posture:
per-engine idiomatic implementations under a functional-equivalence gate, so the *practitioner
experience* is measured. The two answer different questions and both ship, labeled, in every
ecosystem. Alternatives weighed and rejected: adopting an existing per-ecosystem suite's workloads
(mbosecke/template-benchmark, askama-rs/template-benchmark, slinso/goTemplateBenchmark) — none
asserts output equality and none shares workloads across languages, so they serve as harness
precedents only; running only one track — the two postures each have a known blind spot the other
covers, and the user ruled both in.

**Workload set: bounded to eight, every workload owning one dimension.** The 2026-07-18 run proved
results are workload-shape-dependent (Handlebars.Net allocates 0.46x Heddle's bytes on trivial
substitution and sits within ~8% on large-loop time), so credibility requires shape coverage — but
the full matrix is workloads × 2 tracks × ~11 engines × 6 runtimes, so every added workload must earn
its cost. The set:

| # | Workload | Suite | Dimension it owns (no other workload covers it) |
|---|---|---|---|
| 1 | Composed page (anchor, unchanged) | raw | Layout/section/component composition machinery; ~35 KB normalized output |
| 2 | Trivial substitution (anchor, unchanged) | raw | Per-render fixed-overhead floor; ~338 B, ten scalar substitutions |
| 3 | Large loop (anchor, unchanged) | raw | Bulk iteration and output writing; 5,000 rows, ~193 KB |
| 4 | Mid-size mixed page (new) | raw | Realistic mixture — literal HTML + substitutions + a modest loop + conditionals in one template, output between the anchors' extremes (tens of KB). Precedent: the minijinja-vs-jinja2 study's realistic ~65 KB e-commerce template |
| 5 | Conditional-heavy (new) | raw | Branch evaluation and control-flow dispatch — many if/else decisions per render with a mix of taken/skipped branches; no current workload exercises branching at all |
| 6 | Fragment-heavy (new) | raw | Per-call composition overhead — dozens of small partial/component invocations with arguments, isolating call cost from the composed page's single ordered fragment sequence |
| 7 | Fortunes-shaped (new) | encoded | Escaping correctness and cost at micro scale — a small table (~12 rows) whose data includes a `<script>` XSS payload and a Japanese UTF-8 string, rendered through each engine's escaping path |
| 8 | Encoded bulk loop (new) | encoded | Escaping throughput at scale — a large-loop-shaped workload with escapable content in every cell, where escape cost dominates. Precedent: the Rust suite's "big table" workload and the escaping-on fork of the Java suite |

Dimensions considered and deliberately folded out to keep the set bounded: deep-vs-flat composition
nesting (stresses the same machinery as workloads 1 and 6 while adding portability risk from
engine include-depth limits) and throughput-under-concurrency (a runtime/server property, not an
engine property — out of scope with the HTTP stack). All workloads are constrained to the
common-denominator feature set every chosen engine has (scalar substitution, loops, conditionals,
partial/include, HTML escaping) — this is a design constraint, not an accident, because a workload
using an engine-exclusive feature cannot be parity-gated.

**The encoded suite is a separate suite, not a flag on the raw one.** Raw and encoded measure
different code paths (the 2026-07-18 report is explicit that the raw suites "measure templating, not
encoding"), and mixing them would let an engine's escaping cost contaminate its templating numbers.
Encoded workloads confine untrusted data to plain HTML text and attribute-value contexts, because
placing data in contexts where only contextual encoders react differently would make the byte gate
structurally impossible for flat escapers. The confinement rests on the expectation that flat HTML
escapers (most engines) and contextual encoders (Heddle, JTE, Go html/template) escape the same
character set in those two contexts — an authoring assumption, not an established fact: a contextual
encoder may escape a broader character set even there, so contract v2 validates each engine's actual
escaper output against the oracle before any encoded port is gated (entity-spelling variance is
Q1.1's subject; any wider character-set divergence is contract evidence handled through the same
machinery). The confinement cuts both ways, and the program discloses it rather than letting a
hostile reader discover it: by excluding the contexts where only contextual encoders react
differently, the suite also excludes exactly the scenarios where context awareness delivers its
differentiating benefit — a contextual encoder (Heddle, JTE, html/template) still runs its
context machinery on these workloads while the design suppresses what that machinery buys. Encoded-suite
numbers therefore measure escaping cost in confined contexts, never the value of contextual
encoding; the metrics/publication protocol requires every report that presents encoded-suite
results to state this caveat alongside them, so a contextual encoder's encoded-suite deficit is
never readable as "context awareness is pure overhead." The encoded suite verifies, as
part of its gate, that the raw XSS payload never appears unescaped in any engine's output (the
Fortunes rule).

**Parity contract v2 direction.** V1's normalization (line endings to `\n`, inter-tag whitespace
collapse, trim) carries forward unchanged for the raw suites. V2 extends it with what cross-language
use demands: a defined output encoding (UTF-8, no BOM) so "byte-identical" means the same thing on
every runtime, and a documented reconciliation for the encoded suite's known cross-engine variance in
escaped-entity spelling (engines legitimately differ on e.g. `&#39;` vs `&#x27;` for the same
character — Q1.1 resolves this with a v2 normalization step that canonicalizes semantically-identical
entity spellings, with engine configuration preferred to normalization wherever an engine offers it).
The **controlled gate** stays what v1 made it: byte-identical to
the golden oracle after the documented normalization, asserted before any timing, in every ecosystem
— a hard gate, per the standing ruling. The **idiomatic gate** is defined measurably: an
implementation passes if a per-workload machine-checkable verifier confirms (a) every model data
value appears in the output exactly the expected number of times, (b) a per-workload ordered list of
structural markers appears in order, and (c) for encoded workloads, zero occurrences of any raw
untrusted payload and presence of its escaped form. "Looks about right" is never the bar; the
verifier is. The **exclusion policy** encodes the standing ruling: engines that structurally cannot
target byte-exact output (whitespace-significant syntaxes — Pug, Slim, Haml; non-file DSLs — maud)
are excluded with a documented reason and never graded on a curve. A separate, softer case is an
otherwise-file-based engine (Thymeleaf is the flagged risk) that cannot reach byte-exactness despite
best-effort authoring: because the controlled gate normalizes line endings, inter-tag whitespace, and
trim (v1's normalization, carried forward — not arbitrary intra-text whitespace), an engine whose
output diverges from the oracle *only* within those normalized dimensions still passes — so this
fallback bites only on divergence *beyond* what normalization covers, and then (Q1.2) the engine stays in the idiomatic track, its
controlled-track cell is marked excluded-with-documented-evidence, and no replacement engine enters
without user sign-off.

**Metrics & publication protocol direction.** Wall time per render is the only number that may be
compared across languages. Allocation/GC figures are reported inside each ecosystem only and carry
an explicit not-cross-comparable label, because allocator designs differ across runtimes and the
numbers do not mean the same thing (the grounding for the standing ruling); cold parse/compile cost
is likewise per-ecosystem only and never a cross-language column (Q1.3), since AOT compilation
(Heddle, Askama, JTE, templ) and runtime parse/compile are different operations. The protocol fixes
the cross-stack presentation rules once, so every ecosystem report and the consolidated report share
them: (a) each per-ecosystem report (phases 2–6) carries a clearly-labeled, wall-time-only Heddle
reference row sourced from this phase's protocol run, so the only cross-comparable number is visible
in the ecosystem report itself while Phase 7 remains the single *complete* cross-stack comparison
(Q2.2 = option A; allocations are never juxtaposed across runtimes); (b) the per-ecosystem wall-time
ratio column anchors to that Heddle reference row, uniform across phases 2–6, while baselines for the
non-cross-comparable metrics stay within-ecosystem, the exact choice a spec detail (Q6.2); (c) Heddle
may be compared and ranked against any engine, wall-time-only, anywhere, but non-Heddle engines are
never ranked across ecosystems — engine-vs-engine comparison exists only within an ecosystem (Q6.2).
Each ecosystem uses its standard harness (BenchmarkDotNet, Criterion.rs, JMH, mitata, pyperf, Go's
testing/benchstat — the per-phase specs pin versions and settings), and the report format documents
once which central-tendency point estimate each harness contributes as "wall time per render", with
its dispersion published alongside (Q2.1). All cross-compared runs for phases 2–7 execute on one
recorded machine — the existing Windows/Ryzen 9 9950X box, for continuity with the published .NET
numbers, with OS/toolchain versions recorded and per-harness stability settings resolved in each
ecosystem spec (Q1.6); a separate, later Linux cross-check on the same physical machine (Ubuntu
24.04) is a distinct new phase, `phase-8-linux-crosscheck.md`, separately published and never merged
or cross-compared with the Windows numbers, so the one-machine rule for phases 2–7 stays intact.
Every run publishes as a date-stamped `docs/benchmarks/<date>/` directory with an environment block,
reproduce-it-yourself command, and the existing honest-reporting posture: no universal-superiority
claims, and every workload where Heddle loses reported as prominently as where it wins.

## Dependencies & ordering

- **Depends on:** nothing. Phase 1 builds directly on the repo's existing intra-.NET suite and
  parity harness.
- **Internal ordering:** new workloads implemented and parity-proven intra-.NET → encoding-ON twins
  validated intra-.NET → golden corpus exported → parity contract v2 finalized against what the
  corpus actually required → metrics/publication protocol documented → one intra-.NET benchmark run
  over the full eight-workload set published as the protocol's own first exercise.
- **Unblocks:** phases 2–6 (each consumes the golden corpus, contract v2, and the protocol; they are
  independent of each other, priority-ordered, and individually cuttable) and phase 7 (the
  consolidated report aggregates only runs produced under this protocol).
- Nothing in phases 2–6 may start against a workload whose corpus entry does not yet exist —
  otherwise ecosystems port moving targets.

## Back-compat / impact

- **Published reports:** unaffected. `docs/benchmarks/2026-07-11/` and `docs/benchmarks/2026-07-18/`
  are immutable date-stamped artifacts; new runs get new dates.
- **Existing intra-.NET suite:** gains five new workloads (three raw, two encoded); the three
  existing workloads and their templates stay unchanged, so historical numbers remain comparable
  run-to-run.
- **Parity contract:** v1 (the Runners README) remains the accurate description of the existing
  intra-.NET raw suites. V2 is a superset for cross-stack use; where v2 adds rules (output encoding,
  encoded-suite reconciliation), existing raw workloads are unaffected because their outputs never
  exercised those rules.
- **.NET twins:** each twin gains encoding-ON variants for the encoded suite (the current twins all
  deliberately run their non-encoding paths). The Razor twin remains outside the parity gate, as
  today, and is not part of the cross-stack contract.
- **New external contract:** the golden corpus + contract v2 become load-bearing for five later
  phases. The corpus is git-versioned like any other committed artifact; regenerating or expanding
  it is an ordinary versioned change — no version-bump ceremony and no mandatory re-run of every
  shipped ecosystem's gates (Q1.5) — and shipped phases pick up a changed corpus the same way they
  pick up any other repo change.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Escaped-entity spelling variance across engines breaks the byte gate for the encoded suite | Apply Q1.1's resolved rule — a documented v2 normalization that canonicalizes semantically-identical entity spellings, with engine configuration preferred where an engine offers it; validate it against all four .NET twins' escaping paths before the corpus is exported | M |
| A new workload cannot be expressed byte-identically even by the four .NET twins | Intra-.NET parity is the feasibility gate by design; a failing workload is redesigned or dropped before any ecosystem sees it | M |
| Workload-set growth explodes the phase 2–6 matrix | Hard cap at eight workloads; every workload must own a dimension no other covers (table above); anchors are mandatory, additions need that justification | M |
| Golden corpus drifts if the Heddle engine changes during the v2.0 window | Corpus entries record generating commit + hash so any change is auditable in git; the user has ruled this drift acceptable and declined a pinning / re-run-all-gates ceremony (Q1.5) — the corpus is git-versioned and regeneration is an ordinary versioned change | M |
| Composed-page anchor's fragment-sequence shape (documented `@<<` behavior) invites "not a real page" criticism cross-stack | Fidelity note carried verbatim into the corpus documentation; the mid-size mixed page carries realistic-full-page duty (Q1.4: keep the anchor as-is, changing it requires an out-of-scope engine change) | S |
| Common-denominator feature constraint biases the suite toward simple templates | Dimensions chosen to stress distinct engine subsystems (composition, branching, call overhead, bulk writing, escaping) rather than feature breadth; the constraint and its rationale are published with the contract | S |
| Encoding-ON parity is impossible for some contexts because contextual encoders (Heddle, JTE, html/template) and flat escapers legitimately diverge | Encoded workloads confine untrusted data to HTML text/attribute contexts where the encoder families are expected to emit identical output — an assumption validated against the four .NET twins' escapers before corpus export and against each ecosystem's engines at spec time (the Q1.1 machinery); this authoring constraint is part of contract v2 | M |
| Single-machine protocol choice later proves wrong for a cross-language toolchain (e.g. harness assumes Linux CPU isolation) | Machine fixed by Q1.6: the Windows/Ryzen box for phases 2–7, OS/toolchain recorded, all cross-compared runs from it; the pyperf-on-Windows isolation exposure is disclosed per phase (Phase 5) and answered by a separate later Linux cross-check on the same box (Phase 8, `phase-8-linux-crosscheck.md`), never merged with the Windows numbers | M |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] The intra-.NET suite contains exactly eight workloads: the three anchors byte-unchanged, three
      new raw workloads, two encoding-ON workloads.
- [ ] Every raw workload passes the byte-parity assertion (Heddle vs all four twins, after documented
      normalization) before any timing, and the assertion runs automatically ahead of every benchmark
      run.
- [ ] Every encoded workload passes the same gate on the twins' encoding-ON paths, its output
      contains the escaped forms of the XSS payload and the UTF-8 string, and the raw `<script>`
      payload appears zero times in any engine's output.
- [ ] The golden corpus contains one Heddle-rendered golden output per workload, each with recorded
      byte length, content hash, and generating commit, committed to the repo.
- [ ] Regenerating any golden output at its recorded commit reproduces the committed bytes exactly.
- [ ] Parity contract v2 is published and enumerates: every normalization step (closed list), the
      controlled-track gate, the idiomatic-track verifier definition (data-value coverage + ordered
      structural markers + escaping checks), and the exclusion policy with the four known structural
      exclusions documented.
- [ ] The idiomatic verifier for each workload rejects a deliberately corrupted output (a removed
      row, a reordered section, an unescaped payload) and accepts the golden output.
- [ ] The metrics/publication protocol is published and states: wall time per render as the sole
      cross-language-comparable metric, the per-ecosystem-only non-comparable status of
      allocation/GC numbers, the named standard harness per ecosystem, the single recorded
      machine, and the encoded-suite confinement caveat — the confined workloads cannot exhibit
      contextual encoding's differentiating benefit — which every report presenting encoded-suite
      results must state alongside those results.
- [ ] One intra-.NET benchmark run covering all eight workloads is published under a new
      `docs/benchmarks/<date>/` directory in the existing style: environment block, reproduce
      command, and explicit reporting of every workload where a twin beats Heddle on any metric.
- [ ] Each of the five new workloads' documentation names the single variability dimension it owns.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A twin's output for any workload differs from the Heddle oracle by one byte that survives normalization | Parity assertion fails loudly before any timing runs; no benchmark numbers are produced for that suite |
| The golden output for a workload is regenerated at its recorded generating commit | Bytes, length, and hash match the committed corpus entry exactly |
| An encoded workload's rendered output is searched for the raw `<script>` XSS payload | Zero occurrences; the escaped form is present; the Japanese UTF-8 string is present intact |
| The idiomatic verifier is fed the golden output for its workload | Pass |
| The idiomatic verifier is fed the golden output with one data row removed, or one section reordered, or one payload unescaped | Fail, with the failed check identified |
| A hypothetical whitespace-significant engine (Pug-shaped) is proposed for the controlled track | The exclusion policy applies: documented exclusion with reason, no curve-graded inclusion |
| An intra-.NET benchmark run over the eight workloads completes | A date-stamped `docs/benchmarks/<date>/` directory exists with environment block, reproduce command, per-suite reports, and prose that reports any Heddle losses as prominently as wins |
| A reader compares allocation numbers between two ecosystems in any published report | The report's own text explicitly marks allocation/GC as not cross-comparable, so no published table juxtaposes them across runtimes |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). Q1.1–Q1.7 (and the program-wide Q2.2
and Q6.2 presentation rules folded into the metrics/publication protocol above) are all resolved
(user, 2026-07-20) and integrated into the sections above.

## External grounding

| Claim | Source |
|---|---|
| No surveyed benchmark is both cross-language and template-focused; byte-identical cross-engine output enforcement is unprecedented among surveyed projects | Research spike 1 survey (grounding dossier, 2026-07-19), across the sources below |
| Fortunes workload shape: ~12 rows, mandatory XSS-escaping of a `<script>` payload, Japanese UTF-8 string; functional rules checked but output never diffed; template-only variant discussed, never built (issue #10345) | [TechEmpower/FrameworkBenchmarks](https://github.com/TechEmpower/FrameworkBenchmarks) |
| Controlled-track methodology: semantically-equivalent, disclosed-idiom implementations so the engine, not the algorithm, is measured | Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016); [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet) |
| Isolated JVM microbenchmarks induce unrepresentative tiered-JIT profiles; harness discipline beyond warmup counts is required | Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the JVM" (ACM SAC 2026, [arXiv:2605.23570](https://arxiv.org/abs/2605.23570)) |
| Allocation/GC numbers are not comparable across runtimes with different allocator designs | BenchmarkDotNet documentation ([benchmarkdotnet.org](https://benchmarkdotnet.org/)); encoded as standing ruling 5 |
| Java per-ecosystem precedent (JMH, engines out-of-the-box, no output assertion; escaping-on fork exists; JTE fork exists) | [mbosecke/template-benchmark](https://github.com/mbosecke/template-benchmark) and fork lineage |
| Rust per-ecosystem precedent (Criterion, "big table" and "teams" workloads) | [askama-rs/template-benchmark](https://github.com/askama-rs/template-benchmark) |
| Go per-ecosystem precedent (interpreted vs precompiled organization) | [slinso/goTemplateBenchmark](https://github.com/slinso/goTemplateBenchmark) |
| Python realistic-template precedent (~65 KB e-commerce template, transparent scripts, Oct 2025) | simonw/research, minijinja-vs-jinja2 study |
| Existing parity contract v1: byte-identical after documented normalization (line endings, inter-tag whitespace collapse, trim), asserted before timing; templates authored whitespace-free; raw path on every engine | [src/Heddle.Performance/Runners/README.md](../../src/Heddle.Performance/Runners/README.md) |
| Results are workload-shape-dependent: Handlebars.Net 0.46x alloc on trivial substitution, within ~8% on large-loop time; honest-reporting posture modeled | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |
| Composed-page anchor: ~35 K normalized chars, four twins parity-checked, 2.0x time lead; fragment-sequence fidelity note and `@<<` root cause documented | [docs/benchmarks/2026-07-11/index.md](../benchmarks/2026-07-11/index.md); [Runners README](../../src/Heddle.Performance/Runners/README.md) |
| Contextual vs flat escaping split among cast engines (Heddle and JTE contextual at compile time; Go html/template contextual at runtime; others flat HTML) — grounds the encoded-suite context-confinement constraint | Research spike 2 engine notes (grounding dossier, 2026-07-19); Go html/template documentation; [jte.gg](https://jte.gg/) |

Assumptions to verify in the spec (flagged, not grounded here): exact whitespace-control syntax for
Askama, JTE, templ, and quicktemplate; the Handlebars npm download figure; the concrete
escaped-entity output of each .NET twin's encoding path (feeds Q1.1's resolved normalization rule); and the
context-confinement assumption itself — that every cast engine's escaper (flat or contextual) emits
an identical escaped character set in plain-HTML-text and attribute-value contexts, beyond the Q1.1
entity-spelling variance — checked per engine against actual escaper output.
