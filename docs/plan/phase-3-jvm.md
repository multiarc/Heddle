# Phase 3 — jvm

## Header

- **Status:** in progress
- **Goal (one line):** A published dual-track JVM benchmark report — JTE and Thymeleaf rendered against the phase 1 golden corpus under JMH — pairing Heddle's closest compiled JVM peer with the JVM's most-adopted production engine.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md)
- **Changes an externally-visible contract:** no — this phase consumes the golden corpus, parity contract v2, and the metrics/publication protocol read-only; its output is a date-stamped report artifact, not a contract.

## Goal

This phase extends the parity-gated comparison to the JVM, the second ecosystem in the standing
priority order — and second for two reasons that reinforce each other. First, JTE gives the survey
its second compiled/typed fair-fight peer (after Askama in phase 2): templates precompiled to typed
Java classes via javac, typed `@param` models, reflection-free rendering, compile-time context-aware
escaping — architecturally the closest JVM analogue of Heddle's own compiled positioning, so a
JTE-vs-Heddle result speaks directly to whether Heddle's design thesis holds outside .NET. Second,
Thymeleaf carries the JVM's largest enterprise server-side templating install base
— the Spring-ecosystem default — so its inclusion is what makes the JVM report legible to the
practitioners the project most wants to reach. One engine answers "is Heddle fast among compiled
peers?"; the other answers "how does the compiled approach compare to what the enterprise actually
runs?".

The phase delivers a JVM harness that renders all eight phase 1 workloads with both engines in both
fairness tracks (controlled byte-identical and idiomatic functional-equivalence, per the gate
definitions phase 1 established), a measurement run under JMH on the protocol machine, and a
date-stamped per-ecosystem report under `docs/benchmarks/<date>/` in the repo's established
honest-reporting style — with JVM allocation/GC figures reported per-ecosystem only, per the
standing metrics ruling.

## Non-goals / scope boundary

- **No other JVM engines.** FreeMarker, Pebble, Velocity, Mustache.java, Rocker and the rest of the
  mbosecke-suite roster are out; the cast is fixed at two engines per ecosystem by standing ruling.
  Why Thymeleaf and JTE beat the alternatives is recorded under Design direction as rationale, not
  as a decision to reopen.
- **No Kotlin-specific engines** (and no JTE `.kte` Kotlin dialect) — the JVM entry is canonical
  Java; a Kotlin variant would add a compiler dimension without adding an engine dimension.
- **No Spring MVC integration benchmarking.** Thymeleaf is measured standalone through its own
  rendering API, not through Spring view resolution — routing a render through the framework stack
  would reintroduce exactly the full-stack confound phase 1 excluded when it declined TechEmpower's
  measurement model.
- **No alternative-runtime variants** (GraalVM native-image, alternative JVM vendors as extra
  columns). The protocol names one standard harness and one machine per ecosystem; the JVM entry is
  a single pinned JDK in steady state. The spec pins which one.
- **No corpus or contract edits from this phase.** A parity failure on the JVM is evidence — it
  feeds phase 1's Q1.2 machinery — never a reason to patch the golden corpus or bend
  contract v2 locally.
- **No cross-runtime allocation comparison** — inherited from the standing metrics ruling; restated
  because the JVM's GC-centric tooling makes this the ecosystem where readers will be most tempted.

## Design direction

**Engine pair: peer + credibility, alternatives rejected on role fit.** JTE is the performance/peer
pick: it is the only widely-adopted JVM engine that mirrors Heddle's whole architecture —
ahead-of-time compilation to typed classes, no reflection at render time, compile-time context-aware
escaping — making the controlled-track comparison a genuine like-for-like. Thymeleaf is the
credibility pick: the Spring ecosystem's default engine and the largest production install base in
JVM server-side templating. Alternatives weighed and rejected: FreeMarker and Pebble both have
benchmark-precedent standing (the mbosecke/template-benchmark lineage is built around that roster)
and would be technically easier controlled-track ports than Thymeleaf — but neither carries
Thymeleaf's practitioner credibility, and either would fill the same interpreted-runtime slot
Thymeleaf already fills while shrinking the audience that recognizes the comparison. The pair was
fixed by standing ruling; this paragraph records why the ruling is also the right call, not a
re-litigation.

**Both tracks, with Thymeleaf's controlled-track risk surfaced first.** Both fairness tracks ship,
per the standing ruling, under the gate definitions phase 1 owns (controlled = byte-identical to the
golden corpus after documented normalization, asserted before any timing; idiomatic = the
machine-checkable functional-equivalence verifier). Thymeleaf is the dossier's flagged riskiest
controlled-track port in the entire cast: its DOM-based natural-templating model and weak
template-level whitespace trimming are the concern. Q1.2's resolution shrinks that exposure: the
controlled gate normalizes whitespace, so Thymeleaf output that diverges from the oracle *only* in
whitespace still passes — the byte-gate risk is now specifically *beyond-whitespace* divergence.
Where that residual divergence proves unreachable even with best-effort authoring, Q1.2 defines what
happens (the engine stays idiomatic-track-only, its controlled cell marked
excluded-with-documented-evidence, no replacement engine without user sign-off) — this phase does
not duplicate that resolution, but it is the phase with first-line exposure to it, so the internal
ordering retires that risk first: Thymeleaf controlled-track feasibility is attempted before any
other JVM work, so a Q1.2 outcome is known while it is still cheap. Note the controlled track's
Are-We-Fast-Yet discipline permits disclosed non-idiomatic authoring — the controlled Thymeleaf
templates may use whatever documented Thymeleaf mechanisms best target the byte gate, with the
idiomatic track carrying the natural-templating idiom.

**JMH, with the misleading-microbenchmark risk named and mitigated at plan level.** JMH is the
protocol's named JVM harness. The load-bearing methodology constraint for this phase is
Schiavio/Bulej/Binder (ACM SAC 2026): isolated JMH microbenchmarks can induce tiered-JIT
compilation profiles unrepresentative of real applications — warmup iteration counts alone do not
buy profile realism. The plan-level mitigation direction is twofold: (a) the phase measures and
reports **steady-state** per-render wall time, the regime where the workloads' hot render paths are
compiled and the comparison is at least internally consistent across engines; (b) the published
report carries a **disclosed-limitations note** stating the isolated-profile caveat with citation,
rather than implying the numbers predict in-application behavior. The same JMH discipline problem
has a second face — dead-code elimination and constant folding silently deleting the work being
measured — which is carried as a named risk below with the mitigation direction of following
Oracle's published JMH result-consumption guidance. Concrete JMH configuration (forks, iteration
counts, Blackhole usage, JDK pin) is spec territory and deliberately not stated here. The
mbosecke/template-benchmark lineage (including the casid fork that adds JTE and the agentgt
escaping-on fork) serves as harness precedent only: it asserts no output equality, which is
precisely the gap this project's byte gate fills.

**Reporting stays in the house style.** One date-stamped `docs/benchmarks/<date>/` report in the
established format ([2026-07-18](../benchmarks/2026-07-18/index.md) is the model): environment
block, reproduce-it-yourself command, no universal-superiority claims, and every result that
complicates Heddle's story reported as prominently as the ones that flatter it. Wall time per
render is the only number eligible for cross-language comparison; JVM allocation/GC figures
appear only within this report and carry the explicit not-cross-comparable label. Per the Phase 1
publication protocol (Q2.2 = option A, Q6.2), the report carries a clearly-labeled, wall-time-only
Heddle reference row sourced from the Phase 1 protocol run — overriding this phase's earlier
defer-to-Phase-7 lean — with its wall-time ratio column anchored to that Heddle row; the *complete*
cross-language juxtaposition still lives only in Phase 7. The two engines
are labeled by role (peer vs credibility) so a fast-compiled-engine-beats-runtime-DOM-engine result
reads as the expected consequence of different designs, not a strawman takedown. The dossier flags
JTE's whitespace-control documentation as thin — that is an assumption phase 1 already routed to
spec-time verification, and this phase's controlled-track authoring depends on its resolution.

## Dependencies & ordering

- **Depends on:** phase 1, fully — no JVM port may start against a workload whose golden-corpus
  entry does not exist (phase 1's own rule). Specifically load-bearing here: the corpus entries for
  all eight workloads, contract v2's normalization list and exclusion policy (phase 1 Q1.1 and Q1.2
  resolutions), the metrics protocol's machine decision (phase 1 Q1.6), and the idiomatic-track
  evidence standard (phase 1 Q1.7).
- **Independent of phases 2, 4, 5, 6** — the ecosystem phases share only the phase 1 contract, are
  priority-ordered not dependency-ordered, and this phase is individually cuttable without
  affecting the others.
- **Internal ordering:** Thymeleaf controlled-track feasibility first (retires the phase's largest
  risk and resolves this phase's Q1.2 exposure early) → JTE controlled track → idiomatic track for
  both engines → encoded suite (requires phase 1 Q1.1's entity-spelling reconciliation to be settled)
  → measurement run on the protocol machine → published report.
- **Unblocks:** the JVM rows of the phase 7 consolidated report; nothing else consumes this phase's
  output.

## Back-compat / impact

- **Existing benchmark artifacts:** untouched. Published .NET reports remain immutable; the
  intra-.NET suite is not modified by this phase.
- **Golden corpus and contract v2:** consumed read-only. Any JVM finding that pressures the
  contract (an unreconcilable entity spelling, a byte-gate impossibility) is escalated as evidence
  through phase 1's Q1.2 escalation machinery, never absorbed as a local workaround.
- **Heddle engine:** unchanged — this phase measures it via the corpus, not by running it.
- **New surface:** a JVM harness area in the repo and one new date-stamped report directory. Both
  are additive; nothing existing breaks at any point.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Thymeleaf cannot pass the controlled byte gate despite best-effort authoring (the dossier's flagged riskiest port) | Q1.2 shrinks the exposure — whitespace-only divergence is reconciled by the gate's whitespace normalization, so only beyond-whitespace divergence can fail; feasibility is attempted first in the internal ordering, and on a genuine beyond-whitespace failure Q1.2's policy applies (idiomatic-track-only with documented exclusion evidence, no replacement engine without user sign-off), with the controlled track proceeding on JTE alone | M |
| Isolated JMH microbenchmarks yield unrepresentative tiered-JIT profiles (SAC 2026 finding), overstating what steady-state numbers mean | Steady-state measurement posture, internally consistent across both engines; report carries a disclosed-limitations note with citation; concrete profile-hygiene settings pinned in the spec | M |
| Dead-code elimination / constant folding deletes the measured work, producing impossibly fast numbers | JMH result-consumption discipline per Oracle guidance, pinned in the spec; plausibility check that per-render time scales with workload output size across the eight workloads | M |
| JTE's thin whitespace-control documentation blocks whitespace-free controlled-track authoring | Assumption already flagged by phase 1 for spec-time verification; corpus templates are authored whitespace-free by design, which minimizes dependence on trim syntax in the first place | S |
| JVM escapers' entity spellings fall outside the reconciliation phase 1 Q1.1 established, breaking the encoded suite's gate | Encoded ports start only after Q1.1's resolution is validated against both JVM engines' actual escaping output; a mismatch is contract evidence, not a local normalization hack | M |
| Report reads as a strawman — compiled engines trouncing a runtime DOM engine | Role labeling (peer vs credibility), dual-track structure giving Thymeleaf its idiomatic-practice showing, and the house posture of reporting every result that cuts against Heddle or JTE as prominently as wins | S |
| JVM warmup/tiering variance on the protocol machine destabilizes run-to-run numbers | JMH's statistical machinery plus the protocol's recorded environment; per-harness stability settings are a spec item phase 1's protocol already assigns to each ecosystem spec | M |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] The JVM harness renders all eight corpus workloads with both JTE and Thymeleaf in both
      tracks, or a controlled-track cell carries a documented exclusion under contract v2's policy
      (the phase 1 Q1.2 path) — no silent gaps in the engine × workload × track matrix.
- [ ] Every controlled-track measurement is preceded by an automatic byte-parity assertion against
      the workload's golden-corpus entry (normalization per contract v2); a failed assertion
      produces no benchmark numbers for that cell.
- [ ] Every idiomatic-track implementation passes contract v2's per-workload functional-equivalence
      verifier before any timing.
- [ ] Both engines' encoded-suite outputs contain zero occurrences of the raw XSS payload, and
      contain the escaped forms and the intact UTF-8 string.
- [ ] The measurement run executes under JMH on the protocol machine and publishes as a
      date-stamped `docs/benchmarks/<date>/` directory with environment block and reproduce
      command, in the existing report style.
- [ ] The published report contains a disclosed-limitations note naming the isolated-microbenchmark
      JIT-profile risk, with citation.
- [ ] Allocation/GC figures appear only in per-ecosystem context, explicitly marked not
      cross-comparable; no published table juxtaposes JVM and non-JVM allocation numbers.
- [ ] The report carries a clearly-labeled, wall-time-only Heddle reference row (per Q2.2 = option
      A), its wall-time ratio column anchored to that Heddle row (Q6.2); allocation baselines stay
      within-ecosystem and the complete cross-language juxtaposition is left to Phase 7.
- [ ] The report labels each engine's role (performance/peer vs credibility) and reports every
      workload where Thymeleaf beats JTE — or where any JVM result complicates Heddle's positioning
      — as prominently as favorable results, stated in the report's prose, not only visible in
      tables.
- [ ] The phase spec records verified JTE whitespace-control behavior (closing the flagged
      assumption) before controlled-track authoring begins.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| A JTE controlled-track render differs from the golden corpus by one byte that survives normalization | Parity assertion fails loudly before timing; no numbers are produced for that cell |
| Best-effort controlled-track Thymeleaf authoring diverges from the oracle beyond whitespace and cannot reach the byte gate | Whitespace-only divergence would have passed (the gate normalizes whitespace); for the residual beyond-whitespace failure, Q1.2's policy applies: the cell is marked excluded with documented evidence, Thymeleaf remains in the idiomatic track, and no replacement engine appears without user sign-off |
| Either engine's encoded-suite output is searched for the raw `<script>` payload | Zero occurrences; escaped form present; Japanese UTF-8 string present intact |
| The idiomatic verifier is fed a JVM output with one row removed or one payload unescaped | Fail, with the failed check identified |
| A reader compares the report's JVM allocation figures with a .NET report's | The report's own text marks allocation/GC as not cross-comparable; no published table invites the comparison |
| The published report is checked for the JMH methodology caveat | A disclosed-limitations note is present, naming the isolated-profile risk and citing the SAC 2026 paper |
| A benchmark result shows a render time implausibly below the cost of writing the workload's output | Treated as a DCE red flag: the result is quarantined and the harness's result-consumption discipline is audited before any publication |

## Open questions

None — resolved; see [open-questions.md](open-questions.md). The program-wide Q2.2 resolved to
option A (user, 2026-07-20), overriding this phase's earlier defer-to-Phase-7 lean: the JVM report
carries a clearly-labeled, wall-time-only Heddle reference row, folded into the reporting design
above. Phase 1's Q1.1, Q1.2, Q1.3, Q1.6, and Q1.7 are load-bearing resolutions of this phase,
referenced above, not duplicated here.

## External grounding

| Claim | Source |
|---|---|
| JTE precompiles templates to typed Java classes via javac, typed `@param` models, compile-time context-aware escaping; philosophically closest JVM peer to Heddle | [jte.gg](https://jte.gg/), per research spike 2 engine notes (grounding dossier, 2026-07-19) |
| Thymeleaf is runtime DOM-based natural templating (`th:text`/`th:utext`) with weak template-level whitespace trim; flagged riskiest controlled-track port in the cast | Research spike 2 engine notes (grounding dossier, 2026-07-19) |
| Engine cast for the JVM fixed at Thymeleaf (credibility) + JTE (performance/peer) | Standing ruling 2 (grounding dossier, 2026-07-19) |
| Isolated JVM microbenchmarks induce unrepresentative tiered-JIT profiles; discipline beyond warmup counts is required | Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the JVM" (ACM SAC 2026, [arXiv:2605.23570](https://arxiv.org/abs/2605.23570)) |
| JMH requires Blackhole/dead-code-elimination discipline for valid results | Oracle JMH guidance, per the grounding dossier's harness facts (2026-07-19) |
| Java per-ecosystem benchmark precedent: JMH, engines out-of-the-box, no output assertion; casid fork adds JTE; agentgt fork adds UTF-8/escaping-on | [mbosecke/template-benchmark](https://github.com/mbosecke/template-benchmark) and fork lineage |
| Controlled-track methodology: semantically-equivalent, disclosed-idiom implementations so the engine, not the algorithm, is measured | Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?" (DLS 2016); [smarr/are-we-fast-yet](https://github.com/smarr/are-we-fast-yet) |
| Allocation/GC numbers are not comparable across runtimes with different allocator designs | BenchmarkDotNet documentation ([benchmarkdotnet.org](https://benchmarkdotnet.org/)); standing ruling 5 |
| Parity gate, golden corpus, dual-track gate definitions, exclusion policy, metrics/publication protocol | [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) |
| Honest-reporting posture model (losses reported as prominently as wins) | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md) |

Assumptions to verify in the spec (flagged, not grounded here): exact JTE whitespace-control syntax
(carried from phase 1's flagged list); Thymeleaf's concrete escaped-entity output spellings (feeds
validation of phase 1 Q1.1's resolution); the pinned JDK version/vendor for the protocol machine; the
"Spring default / largest JVM install base" adoption characterization of Thymeleaf, which is a
standing-ruling premise here and should be backed by a citable adoption source before the published
report repeats it.
