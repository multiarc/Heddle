# Phase 8 — linux-crosscheck

## Header

- **Status:** in progress
- **Goal (one line):** The same physical machine booted into Ubuntu 24.04 — the full protocol suite
  re-run across every shipped ecosystem and separately published as a clearly-labeled Linux
  cross-check that validates whether the program's findings (rankings, relative gaps, dispersion)
  hold under Linux CPU-isolation conditions.
- **Depends on:** [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md) (fully) and
  **any shipped subset of phases 2–7** (mirroring [Phase 7](phase-7-consolidated-report.md)'s subset
  semantics); sequenced last in the program.
- **Changes an externally-visible contract:** no — the deliverable is one additive, immutable
  date-stamped report directory. The one-machine (Windows) protocol for phases 2–7 is untouched:
  this phase *is* the separate later cross-check that Q5.2's resolution added to the program, not a
  change to any phase 2–7 rule, report, or artifact.

## Goal

This phase re-runs the benchmark protocol on the **same physical machine** — the Windows/Ryzen 9
9950X box that Q1.6 fixed as the protocol machine — booted into **Ubuntu 24.04**, and publishes the
results separately as a Linux cross-check. It exists by direct user ruling (Q5.2, resolved
2026-07-20): "We will check the same machine but on Linux (Ubuntu 24.04) later, separately, plan
for this."

The WHY is validation, not new results. Every cross-compared number in phases 2–7 comes from
Windows, and the program discloses rather than hides the consequence: some harnesses' best-stability
guidance assumes Linux-style CPU isolation — pyperf ([Phase 5](phase-5-python.md)) is the named,
most-exposed case — so a skeptical reader can ask whether the published rankings, relative gaps, and
dispersion figures are artifacts of the Windows measurement environment. This phase answers that
question with data instead of argument: the same workloads, same golden corpus, same parity gates,
same engines, same physical hardware — with the machine held constant and only the operating system
and its isolation conditions deliberately changed. Booting a different OS necessarily also carries a
different native toolchain, runtime, and libc for every engine (a Windows binary does not run on
Linux); that is not a hidden second variable but an unavoidable consequence of the OS change, and it
is bounded and disclosed rather than assumed away (see the toolchain-drift risk and the environment
manifest below), so the deliberately-changed variable either reproduces the program's findings or
shows where they move. pyperf is the named driver, but the
validation value spans every harness in the cast: each of them runs under different scheduler,
timer, and isolation conditions on Linux, and a finding that survives both environments is simply
stronger than one measured once.

Two framing commitments define the phase. First, the **Windows numbers remain the program's numbers
of record** (Q1.6's continuity rationale); this report is a cross-check of their robustness, labeled
as such, and its numbers are never merged into or cross-compared with the Windows numbers inside the
phase 2–7 reports. Second, the honest-reporting duty carries over with its polarity intact: where
the Linux results **differ materially** from the Windows findings — a ranking flips, a relative gap
moves beyond the runs' disclosed dispersion, dispersion itself changes character — the cross-check
report says so prominently in prose. Surfacing divergence is this phase's entire value; a
cross-check that only ever confirms would not need to be run.

## Non-goals / scope boundary

- **No Windows-vs-Linux OS shootout.** Differences between the Linux and Windows runs are reported
  as stability/portability findings *about the benchmark program* — how robust its findings are to
  the measurement environment — never as a comparison of the operating systems, and never as
  guidance on which OS is "faster." The report states this framing explicitly.
- **No absolute-time juxtaposition across OSes, anywhere.** The cross-OS reading in this report is
  confined to order statistics and dimensionless quantities: rankings, ratio-to-Heddle relative
  gaps, and dispersion expressed dimensionlessly (coefficient of variation, or relative CI width as
  a percentage of the point estimate — never raw absolute-time standard deviation or confidence-interval
  width compared or combined across OSes, which would be the same juxtaposition this rule forbids in
  another unit). No table or prose places a Linux wall time next to a Windows wall
  time — that juxtaposition is exactly the OS shootout this phase forswears, and the phase 2–7
  reports are equally forbidden from carrying Linux numbers (the one-machine-per-report protocol
  stands).
- **No edits to any phase 2–7 artifact.** Published reports are immutable date-stamped history; the
  golden corpus, parity contract v2, and the metrics/publication protocol are consumed read-only. If
  the Linux run exposes a defect in a shipped artifact, the remedy belongs to the owning phase's
  machinery, not to this report.
- **No new workloads, no new engines, no new tracks, no new metrics.** The suite is exactly the
  Phase 1 eight-workload, dual-track protocol suite as shipped by phases 1–6; the cast is the
  standing-ruling cast. A cross-check that changed the subject would validate nothing.
- **No change of protocol machine and no recurring dual-OS matrix.** Windows remains the protocol
  environment for any future protocol-conformant run; this phase is a single deliberate cross-check
  event, not a commitment to maintain every future run on two operating systems.
- **PHP and Ruby remain program-level non-goals** (standing ruling; rationale recorded in
  [Phase 1](phase-1-cross-stack-foundation.md)); an ecosystem absent from this cross-check because
  its phase was cut is a manifest line, not a scope change.

## Design direction

**Same physical machine, dual-booted bare-metal — not a second box, not CI runners, not a VM.** The
cross-check's evidentiary value rests on changing exactly one variable. A second (Linux) machine
changes two — hardware and OS — so any divergence would be unattributable to either, and it invites
precisely the cross-machine comparison the one-machine protocol exists to prevent (the recorded
reason Q5.2's original option B was never taken as a side-run; the user's resolution keeps the
machine constant and moves only the OS). CI runners are worse on every axis: shared, virtualized,
noisy-neighbor hardware with no isolation control is antithetical to a *stability* cross-check, and
pyperf-style system tuning is impossible there. Virtualization on the same box (a VM, or WSL2)
inserts a hypervisor between the benchmark and the silicon, so Linux's CPU-isolation guidance — the
very thing this phase exists to exercise — would not actually be in effect. Bare-metal Ubuntu 24.04
(the LTS release the user specified, Q5.2) on the recorded Ryzen box is the only configuration where
"same machine, Linux isolation conditions" is literally true. Per-harness Linux stability settings (pyperf system tune
and CPU isolation among them) are resolved in this phase's spec, mirroring how each ecosystem spec
resolved its Windows settings under Q1.6.

**Full-suite breadth, not a Python-only spot check.** pyperf's Linux-native guidance is the named
driver, and a minimal reading of Q5.2 would re-run only Phase 5. Rejected: the program's findings
are *rankings and relative gaps across the whole cast*, and a ranking cannot be validated by
re-measuring one participant. The cross-check therefore re-runs the full protocol suite — all eight
workloads, both fairness tracks, every shipped ecosystem **including the intra-.NET suite** (which
supplies the Linux-side Heddle protocol run that anchors the Linux ratio columns, and is itself a
data point: .NET is cross-platform, and Heddle's own Windows-vs-Linux ranking stability is part of
the finding set). Every parity gate — controlled byte gate and idiomatic verifier — re-runs on Linux
before any timing, which doubles as a live test of contract v2's claim that byte-identical means the
same thing on every runtime (defined UTF-8 output encoding, line-ending normalization).

**Structural validation, not number-vs-number diffing.** The report has two parts. Part one is a
complete protocol-format publication of the Linux run — the same tables, environment block,
reproduce commands, and presentation rules every protocol report uses, with the ratio column
anchored to the Linux-side Heddle reference row (Q2.2 = option A and Q6.2 apply *within* this report
exactly as they do within the ecosystem reports; non-Heddle engines are never ranked across
ecosystems; the encoded-suite confinement caveat is stated wherever encoded results appear). Part
two is the **findings-validation section**, the phase's purpose: for each shipped ecosystem and each
track, it states whether the Windows report's engine rankings hold, whether the relative gaps
(ratio-to-Heddle) moved materially, and how dispersion compares — citing each Windows source run
rather than reproducing its tables. "Materially" means, at WHAT level: a rank change, or gap
movement beyond the two runs' combined disclosed dispersion, with that dispersion combination
computed and compared dimensionlessly (coefficient of variation or relative CI width), never as
raw absolute-time standard deviations juxtaposed across OSes; the exact thresholds are a spec
detail. This confinement to order, ratios, and dimensionless dispersion is what makes the cross-OS
reading a statement about the *benchmark's findings* instead of about the operating systems.

**Separately published, Windows canonical.** The Linux run gets its own date-stamped
`docs/benchmarks/<date>/` directory, titled and framed as the Linux cross-check throughout.
Alternatives weighed and rejected: appending Linux columns or errata to the phase 2–7 reports
(violates report immutability and the one-machine-per-report protocol, and manufactures the
cross-OS juxtaposition); publishing a merged dual-OS mega-report (same objection, at larger scale);
leaving the Linux run unpublished as an internal sanity check (forfeits the credibility value — a
validation nobody can read validates nothing, and withholding run data would contradict the repo's
honest-reporting posture).

**Sequenced last, by design rather than accident.** Two reasons. First, a cross-check validates
*final published findings*; running it before phases 2–7 finish would validate moving targets and
have to be redone. Second, the protocol machine's Windows environment is itself a recorded,
load-bearing artifact for phases 2–7 — repartitioning and dual-booting the box mid-program is a
gratuitous risk to that environment, so the machine leaves its Windows protocol configuration only
after every Windows run this program needs is complete and published. Like the ecosystem phases,
this phase is individually cuttable: cutting it forfeits only the robustness evidence, and the
phase 2–7 reports already disclose the Windows-measurement posture honestly without it (Phase 5's
stability disclosure stands on its own).

## Dependencies & ordering

- **Depends on Phase 1, fully:** the golden corpus, parity contract v2, the metrics/publication
  protocol and its presentation rules (Q2.1 statistic mapping, Q2.2 = option A, Q6.2), and Phase 1's
  intra-.NET protocol suite, which this phase re-runs on Linux to produce the Linux-side Heddle
  anchor rows.
- **Depends on the shipped subset of phases 2–7,** mirroring Phase 7's semantics: each shipped
  ecosystem phase contributes the harness, ports, and published Windows report this phase re-runs
  and validates against; no single phase 2–6 is individually required. Phase 7 is not a hard
  dependency either: if it shipped, its consolidated cross-stack findings join the validation
  targets; if not, the per-ecosystem reports' findings are validated directly.
- **Sequenced last.** This phase starts only after every Windows run the program will publish is
  published — see the design direction's two sequencing reasons (validate final findings; do not
  disturb the recorded Windows protocol environment mid-program).
- **Internal ordering:** Ubuntu 24.04 environment established on the protocol box and recorded →
  toolchains installed with versions pinned as close as practicable to each source run's recorded
  versions, deltas recorded → all parity gates (controlled + idiomatic) re-asserted on Linux per
  shipped ecosystem → per-harness Linux stability settings applied per spec → full measurement runs
  → findings-validation section authored against the cited Windows reports → publication as a
  date-stamped directory.
- **Unblocks:** nothing — this phase is the program's final, optional act. Cutting it changes no
  other phase's deliverable.

## Back-compat / impact

- **Published reports (2026-07-11, 2026-07-18, and every phase 1–7 protocol report):** untouched.
  This phase adds one new date-stamped directory and edits nothing existing.
- **The one-machine protocol for phases 2–7:** intact and explicitly reaffirmed — the cross-check's
  own text states that its numbers do not join the Windows record. Within itself, the Linux run
  honors the same discipline: one machine, one OS, one recorded environment for every Linux number.
- **Golden corpus, contract v2, protocol:** consumed read-only. A parity-gate failure on Linux is
  handled as contract evidence through the existing exclusion/documentation machinery (and is
  itself a portability finding for the report), never a local patch.
- **The physical machine:** gains a dual-boot Ubuntu 24.04 configuration after phases 2–7 complete.
  The Windows environment remains available and is not the cross-check's casualty; any future
  protocol-conformant run still uses it (Q1.6 stands).
- **New surface:** the Linux benchmark environment setup and run scripts become reproduction
  artifacts committed at the published commit — the same
  reproduce-it-yourself obligation every ecosystem phase carries, now with a Linux reproduce path.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| Toolchain drift confounds the validation on two fronts: (a) newer compilers/runtimes/engine versions accumulating between the Windows source runs and the later Linux run, and (b) the platform-native toolchain, target triple, standard library, and allocator every engine necessarily uses on Linux differing from its Windows counterpart even at matched version numbers — a moved gap could be a version effect, a platform-toolchain effect, or an OS effect | Pin Linux toolchain/engine versions as close as practicable to each source run's recorded environment block, bounding (a); record each engine's Linux toolchain/runtime/libc/allocator identity in the environment manifest as a structural, disclosed property of the OS change rather than an eliminable confound, addressing (b); record every unavoidable delta; any validation statement touching a materially-drifted or platform-divergent component carries the delta as an inline caveat (the Phase 7 drift-disclosure posture, applied cross-OS) | M |
| The report gets read — or quoted — as a Windows-vs-Linux OS shootout despite the framing | Structural-validation confinement is a design commitment: no absolute-time cross-OS juxtaposition anywhere, framing statement in the report's opening, findings phrased as benchmark-robustness statements; enforced by success criteria | M |
| Dual-booting the protocol box (repartitioning, bootloader, firmware settings) disturbs the recorded Windows environment before phases 2–7 finish | Sequencing rule: the Linux environment is not established until every Windows run the program will publish is published; the Windows environment's recorded configuration is preserved for any future re-run | M |
| Hidden machine-level differences beyond the OS and its necessary toolchain (firmware/BIOS settings, CPU mitigations, power/frequency policy differing between the two boots) contaminate the "hardware held constant, only the OS deliberately changed" framing | The environment block records the machine-level configuration for the Linux boot (firmware, CPU-mitigation, frequency policy) alongside the OS/toolchain/runtime/libc versions; known-divergent settings are disclosed next to the affected findings rather than assumed equal; exact recording list is a spec detail | M |
| A parity gate fails on Linux (locale, filesystem, or encoding behavior differing from Windows) | The gate fails loudly before any timing, as designed; the failure is triaged as contract-v2 evidence and reported as a portability finding — a gate that catches a real cross-OS output difference is the contract working, not the phase failing | S |
| Windows-only assumptions in benchmark tooling/scripts surface late and stall the run | Surfaced by the internal ordering (gates re-asserted before any measurement); fixes live in this phase's own run tooling, never in phase 1–7 artifacts; effort is bounded because every harness in the cast is natively cross-platform | S |
| The cross-check finds broad material divergence, and reporting it appears to undermine the program's published Windows findings | This is the phase working as ruled, not a failure mode: the honest-reporting duty makes divergence the headline, and the validation section states what each divergence means for the affected claim (robust / narrowed / environment-sensitive); suppressing or softening it would be the actual credibility failure | M |
| Machine downtime and dual-boot logistics make the phase expensive at low marginal value if the Windows findings already look robust | The phase is individually cuttable by design and sequenced last, so cutting it costs no other phase anything; the cut is recorded as forfeiting robustness evidence, mirroring the ecosystem-phase cut semantics | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] Exactly one new `docs/benchmarks/<date>/` directory contains the Linux cross-check report,
      clearly titled and framed as such; no existing file under `docs/benchmarks/` or `docs/plan/`
      is modified.
- [ ] The report's environment block records the same physical machine as the Windows protocol runs
      (hardware identity stated), Ubuntu 24.04, all toolchain/engine versions, the applied
      per-harness Linux stability settings (including pyperf's), and every version delta against
      each Windows source run's recorded environment.
- [ ] Every shipped ecosystem — including the intra-.NET suite — is re-run on both fairness tracks
      across all eight workloads, or its absence/exclusion is recorded in an inclusion manifest with
      the reason (phase cut, or contract-v2 cell exclusion carried through), mirroring Phase 7's
      manifest semantics.
- [ ] Every controlled byte gate and idiomatic verifier passes on Linux before any timing, or the
      failing cell is documented under contract v2's exclusion machinery and reported as a
      portability finding — never silently dropped.
- [ ] The Linux tables follow the protocol's presentation rules: wall time per render with
      dispersion per the Q2.1 mapping, ratio column anchored to the Linux-side Heddle reference row
      (Q2.2 = option A, Q6.2), no cross-runtime allocation/GC juxtaposition, no cross-ecosystem
      ranking of non-Heddle engines, encoded-suite confinement caveat stated where encoded results
      appear.
- [ ] The findings-validation section covers every shipped ecosystem and both tracks, and for each
      states — with the Windows source run cited — whether rankings held, whether relative gaps
      moved materially (rank change, or movement beyond combined disclosed dispersion expressed
      dimensionlessly as coefficient of variation or relative CI width), and how dispersion
      compares in that same dimensionless form; the pyperf/Python dispersion comparison against
      Phase 5's disclosed Windows stability posture is present explicitly.
- [ ] Every material divergence is stated prominently in the report's prose (not only visible in
      tables), with what it means for the affected published claim; if none is found, the report
      says so and states the validation's scope rather than claiming universal confirmation.
- [ ] No table or prose in the report — or in any phase 2–7 report — juxtaposes a Linux absolute
      wall time with a Windows absolute wall time, and no table or prose combines or compares a
      Linux and a Windows dispersion figure in absolute-time units; the cross-OS reading is
      confined to rankings, ratios, and dimensionless dispersion (coefficient of variation or
      relative CI width), and the report's opening states the
      stability/portability-not-OS-comparison framing.
- [ ] The report states that the Windows runs remain the program's numbers of record and that this
      cross-check's numbers join no phase 2–7 comparison.
- [ ] The Linux run's setup and run scripts are committed at the published commit, and the report
      carries a reproduce-it-yourself command for the Linux environment.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| On Linux, two engines swap ranks on a workload relative to the Windows report | The findings-validation section names the flip prominently in prose, cites both runs, and states what it means for the affected claim (environment-sensitive, not robust) |
| The Linux run reproduces every shipped ranking within disclosed dispersion | The report states the confirmation with its scope (which ecosystems, which tracks, at which version deltas) and claims robustness for exactly that scope — no universal claim |
| A reader searches any phase 2–7 report directory for Linux numbers | None exist; the cross-check lives only in its own date-stamped directory |
| A reader searches the cross-check report for a table of Windows vs Linux wall times | None exists; cross-OS content is confined to rankings, ratio-to-Heddle gaps, and dimensionless dispersion (coefficient of variation / relative CI width), each citing its Windows source run |
| The pyperf run under Linux CPU isolation shows a materially lower coefficient of variation than Phase 5's Windows run disclosed | Reported as a stability finding, in that dimensionless form, that contextualizes (and validates the honesty of) Phase 5's disclosed posture; Phase 5's published report is not edited |
| A controlled-track twin's Linux output differs from the golden oracle by one byte surviving normalization | The parity gate fails loudly before timing; the cell is triaged under contract v2 and the outcome reported as a portability finding |
| An ecosystem phase was cut before this phase runs | The inclusion manifest lists it as absent with the reason; validation statements depending on it are downgraded, mirroring Phase 7's degradation-by-manifest |
| The Linux run requires an engine version newer than the Windows source run's recorded version | The delta appears in the environment manifest, and every validation statement touching that engine carries the version-delta caveat inline |
| Someone proposes appending a Linux-results column to a phase 2–7 report | Rejected against the non-goals and success criteria; the one-machine-per-report protocol and report immutability stand |

## Open questions

None. This phase was created by a resolved ruling (Q5.2, user, 2026-07-20: same machine, Ubuntu
24.04, later, separately planned) and makes its remaining calls within the standing rulings,
recorded above as decided rationale: full-suite breadth including the intra-.NET suite (rankings
cannot be validated by re-measuring one participant), bare-metal dual-boot on the Q1.6 box (only
configuration where the machine is held constant, the deliberately-changed variable is the OS and
its isolation conditions, and Linux isolation guidance actually applies), structural validation
confined to rankings/ratios/dimensionless dispersion with no cross-OS absolute-time juxtaposition
(keeps the cross-check about the benchmark, not the OSes), Windows numbers remaining
canonical (Q1.6 continuity), sequencing last (validate final findings; protect the Windows protocol
environment), and individual cuttability (mirrors the ecosystem-phase semantics). Inherited
resolutions referenced above, not duplicated: **Q1.6** (Windows protocol machine for phases 2–7),
**Q5.2** (this phase's charter), **Q2.1/Q2.2/Q6.2** (statistic mapping and presentation rules,
applied within the Linux report), **Q1.2** (exclusion machinery for any Linux gate failure), and
**Q7.2**'s subset semantics (mirrored by this phase's manifest).

## External grounding

| Claim | Source |
|---|---|
| This phase's charter: a separate, later Linux (Ubuntu 24.04) cross-check on the same physical machine, separately published, never merged or cross-compared with the Windows numbers inside the phase 2–7 reports | [open-questions.md](open-questions.md) Q5.2 resolution (user, 2026-07-20) |
| The protocol machine for phases 2–7 is the existing Windows/Ryzen 9 9950X box, for continuity with published .NET numbers — the same box this phase re-boots | [open-questions.md](open-questions.md) Q1.6 resolution; [Phase 1 metrics & publication protocol](phase-1-cross-stack-foundation.md) |
| pyperf's best-stability guidance assumes Linux-style CPU isolation and system tuning — the named driver of the cross-check | Research spike 1 harness facts (grounding dossier, 2026-07-19); pyperf documentation ([pyperf.readthedocs.io](https://pyperf.readthedocs.io/)); [Phase 5 — python](phase-5-python.md) |
| Phase 5 accepts the Windows machine with disclosed stability posture and dispersion statistics — the disclosure this phase's pyperf comparison validates against | [Phase 5 — python](phase-5-python.md); [open-questions.md](open-questions.md) Q5.2 |
| Wall time per render is the sole cross-language-comparable number; presentation rules (Heddle-anchored ratio column, no cross-ecosystem ranking of non-Heddle engines, encoded-suite caveat) — applied within the Linux report | Standing ruling 5 (grounding dossier, 2026-07-19); [Phase 1 metrics & publication protocol](phase-1-cross-stack-foundation.md); [open-questions.md](open-questions.md) Q2.1, Q2.2, Q6.2 |
| Contract v2 defines a cross-runtime byte-identical gate (UTF-8, no BOM; documented normalization) asserted before timing — the gate this phase re-asserts on Linux | [Phase 1 — cross-stack-foundation](phase-1-cross-stack-foundation.md), parity contract v2 direction |
| Subset semantics and degradation-by-manifest for shipping over any shipped subset of phases 2–6 — mirrored by this phase | [Phase 7 — consolidated-report](phase-7-consolidated-report.md); [open-questions.md](open-questions.md) Q7.2 |
| Honest-reporting posture: divergences and losses reported as prominently as confirmations and wins; no universal claims; hardware- and date-specific numbers; reproduce-it-yourself commands | [docs/benchmarks/2026-07-18/index.md](../benchmarks/2026-07-18/index.md); [Phase 1 metrics & publication protocol](phase-1-cross-stack-foundation.md) |
| Every harness in the cast is natively cross-platform (BenchmarkDotNet among them runs on Linux), bounding the porting effort of the Linux re-run | BenchmarkDotNet documentation ([benchmarkdotnet.org](https://benchmarkdotnet.org/)); research spike 1 harness facts (grounding dossier, 2026-07-19) |

Assumptions to verify in the spec (flagged, not grounded here): the concrete per-harness Linux
stability settings (pyperf system tune / CPU isolation flags and each other harness's Linux
counterpart); the machine-level configuration items the Linux environment block must record
(firmware, CPU-mitigation, and frequency-policy settings) to support the "hardware held constant"
claim; the per-engine toolchain/runtime/libc/allocator identity the environment block must record to
disclose the platform-toolchain change that necessarily accompanies the OS change; the practicable
degree of toolchain/engine version pinning against each Windows source run's recorded environment;
the concrete dimensionless form (coefficient of variation, or relative CI width as a percentage of
the point estimate) the findings-validation section uses for cross-OS dispersion comparison; and the
exact material-divergence thresholds for the findings-validation section.
