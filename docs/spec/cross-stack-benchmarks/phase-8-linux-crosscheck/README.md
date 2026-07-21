# Phase 8 — linux-crosscheck (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-8-linux-crosscheck.md](../../../plan/phase-8-linux-crosscheck.md)
  (standing rulings in the [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — Q5.2 is this phase's charter; Q1.6,
  Q2.1, Q2.2 = A, Q6.2, Q1.2, and Q7.2's subset semantics bind as recorded there)
- **Assumes merged:** [Phase 1 spec](../phase-1-cross-stack-foundation/README.md) fully (corpus,
  contract v2 incl. N3b, metrics protocol), plus **any shipped subset** of the
  [phase 2](../phase-2-rust/README.md)–[6](../phase-6-go/README.md) harnesses and their published
  Windows reports (Q7.2 semantics, mirrored); the Phase 7 consolidated report if it shipped
  (consumed as published findings only — this spec does not depend on Phase 7 spec internals).
  Sequenced last: nothing here starts until every Windows run the program will publish is
  published.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, gates |
| [environment-and-toolchains.md](environment-and-toolchains.md) | Dual-boot establishment rules, the one-machine-state rule, the closed environment-record checklist (firmware / mitigations / frequency policy / per-engine toolchain-libc-allocator identity), version pinning + delta procedure, CPython provenance |
| [harness-settings-and-validation.md](harness-settings-and-validation.md) | Per-harness Linux stability settings for all six harnesses, gate re-assertion + failure triage, measurement-run matrix, two-part report format, dimensionless dispersion form, material-divergence thresholds, cross-OS checkable criteria |

## Scope and goal

Re-run the full protocol suite — all eight workloads, both fairness tracks, every shipped
ecosystem including the intra-.NET suite — on the **same physical machine** (the Q1.6
Windows/Ryzen 9 9950X protocol box) booted bare-metal into **Ubuntu 24.04**, under the
per-harness Linux stability settings this spec pins (pyperf's system tune and CPU isolation among
them), and publish the results **separately** as one immutable, clearly-labeled
`docs/benchmarks/<date>/` Linux cross-check report whose second part states, per shipped
ecosystem and track, whether the published Windows findings (rankings, relative gaps, dispersion)
held — at the exact material-divergence thresholds this spec fixes. The Windows numbers remain
the program's numbers of record; no Linux number joins or is juxtaposed with them in absolute
units, anywhere.

Out of scope (plan non-goals, carried): any OS shootout or cross-OS absolute-time juxtaposition;
any edit to a phase 1–7 artifact or published report; new workloads/engines/tracks/metrics; a
change of protocol machine or a recurring dual-OS matrix; PHP and Ruby.

## Assumed state

Re-verified while authoring; ecosystem-harness rows describe artifacts the phase 1–6 specs pin
but which ship as those phases complete — each carries a *(verify at implementation)* obligation
to re-check against the shipped tree and the published Windows reports.

| Seam | Verified state |
|---|---|
| Published Windows reports | `docs/benchmarks/2026-07-11/` and `2026-07-18/` exist *(listed)*; the phase 1–6 protocol runs' directories are the validation targets and Heddle-anchor sources — their dates and environment blocks are read at implementation time, not assumed here *(verify at implementation)* |
| Phase 1 artifacts | Corpus + manifest under `src/Heddle.Performance/GoldenCorpus/` with verifier JSONs; contract v2 pipeline is the closed list N1, N2, N3, **N3b**, N4, N5 ([Phase 1 D8](../phase-1-cross-stack-foundation/README.md#d8--normalization-v2-v1s-three-steps--defined-encoding--portable-whitespace-definition--n3b-run-collapse) — N3b is the program-wide comparison-time whitespace strip that removes every whitespace run, anywhere, to **nothing** from both oracle and candidate, so the gate compares non-whitespace bytes only; a **maintainer decision of 2026-07-20** recorded in Phase 1 D8 and parity-contract-v2 §normalization-pipeline); gates run in the timed process before any timing |
| Ecosystem harnesses | `benchmarks/rust/`, `benchmarks/jvm/`, `benchmarks/js/`, `benchmarks/python/`, `benchmarks/go/` per their specs, each with its own gate commands and committed lockfiles/manifests; all six harnesses are natively cross-platform (BenchmarkDotNet, Criterion, JMH, mitata, pyperf, Go testing/benchstat) *(verify at implementation: shipped subset + exact paths)* |
| Windows harness-settings decisions being mirrored | Phase 1 D14/metrics-protocol (BenchmarkDotNet defaults, net10.0); [Phase 2 D9](../phase-2-rust/README.md#d9--criterion-configuration-for-the-windowsryzen-protocol-machine); [Phase 3 harness-and-jmh](../phase-3-jvm/harness-and-jmh.md#jmh-benchmark-shape-d9) (Fork 5, 5×10 s / 5×10 s, Threads 1, `-prof gc`); [Phase 4 harness-and-run](../phase-4-js/harness-and-run.md#mitata-run-shape) (Node flags, mitata defaults, High priority launcher, D13 stability procedure); [Phase 5 harness](../phase-5-python/harness.md#run-protocol) (pyperf defaults 20×3×1, `--affinity=4`, elevated REALTIME); [Phase 6 harness-and-measurement](../phase-6-go/harness-and-measurement.md#repeated-runs-stability-settings-benchstat) (`-count=20`, `-benchtime=1s`, prebuilt + `start /high`, no affinity + ±5% trigger) — all read first-hand |
| pyperf Linux facts | Primary-doc verified (pyperf.readthedocs.io, system + runner pages, fetched 2026-07-21): `pyperf system tune` on Linux sets governor→`performance`, raises `scaling_min_freq`, stops `irqbalance` and manages IRQ affinity, sets `perf_event_max_sample_rate`→1, and handles turbo via MSR / `intel_pstate` paths; isolation is **detected, not configured** — recommended kernel parameters `isolcpus=<list>`, `rcu_nocbs=<list>`, optionally `nohz_full=<list>`; the Runner pins workers to isolated CPUs by default when detected; `--affinity=CPU_LIST` forces pinning; `pyperf system show`/`reset` display/undo tuning |
| Ubuntu 24.04 facts | Primary-source verified (ubuntu.com/about/release-cycle, fetched 2026-07-21): 24.04 LTS released April 2024, standard security maintenance to May 2029; GA kernel line is 6.8 (predates the Zen 5 9950X, launched August 2024), hence the HWE-kernel decision in D1 |
| Phase 7 spec | [phase-7-consolidated-report/README.md](../phase-7-consolidated-report/README.md) and its supplementary [report-assembly.md](../phase-7-consolidated-report/report-assembly.md) both exist (appeared mid-authoring, cross-checked 2026-07-21). Cross-check result: compatible — its D1/D9 freeze the inclusion manifest at assembly time as `sources.json` (this spec's Q7.2 mirror starts from that file when Phase 7 shipped), its D13 pins `consolidate.py` stdlib-only citing portability to this phase's Linux boot, and it cites the same N3b ruling as the 2026-07-20 maintainer decision in Phase 1 D8. Coupling stays loose: this phase consumes Phase 7's *published findings* (and `sources.json`) only, never its spec internals *(verify at implementation: re-check against the shipped Phase 7 report)* |

## Design decisions

Every plan-flagged assumption is closed below (details in the two supplements). The plan's
"Open questions" section is empty by construction; the flagged spec details it lists at the end
of External grounding are each closed here: per-harness Linux settings (D6–D11), the
machine-level recording list and per-engine toolchain identity list (D3), pinning practicability
(D4), the dimensionless dispersion form (D15), and the material-divergence thresholds (D16).

### D1 — Dual-boot establishment: bare-metal Ubuntu Server 24.04, firmware untouched, sequencing gate
- **Decision.** Ubuntu **Server** 24.04 LTS (latest 24.04.x point release, minimal install, no
  desktop) installed dual-boot on a partition/disk disjoint from Windows, with the
  `linux-generic-hwe-24.04` kernel; **no UEFI/BIOS setting changes** from the Windows-runs
  configuration; Windows boot verified intact after install; nothing begins until every Windows
  run the program will publish is published. Specified-vs-recorded boundary and quiesce list in
  [environment-and-toolchains.md](environment-and-toolchains.md#dual-boot-establishment-d1).
- **Rationale.** Bare-metal dual-boot on the same box is the charter (Q5.2; plan design
  direction — VM/WSL2/CI all defeat the isolation conditions being exercised). Server flavor
  minimizes background noise sources with zero cost (no GUI needed). HWE kernel because the GA
  6.8 kernel predates the 9950X's Zen 5 and the frequency-policy record depends on mature
  `amd-pstate` support. Firmware invariance is what makes "hardware held constant" evidence.
- **Alternatives rejected.** Desktop flavor (adds compositor/indexing services for nothing);
  GA kernel (pre-Zen-5); a second machine, VM, WSL2, CI runners (all rejected in the plan with
  recorded reasons); changing BIOS settings "for Linux" (would contaminate the one-variable
  design).
- **Grounding.** Q5.2 resolution; plan §Design direction; ubuntu.com release-cycle (Assumed
  state row).

### D2 — One recorded machine state for every Linux number
- **Decision.** A single measurement-session state covers every timed run of every harness:
  isolation boot entry (`isolcpus`/`nohz_full`/`rcu_nocbs` on one SMT pair), machine-wide
  `pyperf system tune`, CPU boost explicitly off via the `amd-pstate` boost control (recorded as
  a known-divergent frequency-policy item vs the boost-on Windows runs), state captured via
  `pyperf system show`, `pyperf system reset` afterwards; **no per-harness priority elevation
  anywhere on Linux** — the Windows priority knobs were isolation substitutes, and their Linux
  counterpart is the tuned system itself. Interruption/re-entry rules in
  [environment-and-toolchains.md](environment-and-toolchains.md#one-machine-state-for-every-linux-number-d2).
- **Rationale.** The plan requires "one machine, one OS, one recorded environment for every
  Linux number"; a per-harness patchwork of states would make within-Linux ratios and the
  findings-validation section internally inconsistent. **Boost-off is not an independent,
  avoidable frequency-policy knob smuggled in beyond the charter — it is a constituent of
  `pyperf system tune`, the very Linux best-stability regime the phase exists to exercise.**
  pyperf's own `system tune` turns turbo/boost off as part of its stability posture (it handles
  turbo via MSR / `intel_pstate` paths — Assumed state, pyperf docs); pyperf is the phase's
  **named driver** (plan Goal), and Q5.2's charter deliberately changes "the operating system
  **and its isolation conditions**" — of which `pyperf system tune` is the canonical instance.
  D2 asserts the AMD `cpufreq/boost` control directly only because pyperf's turbo paths are
  Intel-oriented and AMD coverage cannot be assumed; the direct `echo 0` is the **AMD realization
  of pyperf's turbo-off intent**, not a second variable. Because the Linux Heddle anchor is
  measured under the same state, every within-OS ratio/rank/dispersion stays consistent, and
  absolute times are never cross-compared anyway (D17). The residual objection — that D16's
  attribution vocabulary (`held`/`narrowed`/`environment-sensitive`) cannot decompose a divergence
  into "OS-caused" vs "boost-caused" — is answered by design, not left open: the phase tests
  whether findings survive **the OS together with its isolation conditions as a disclosed bundle**,
  and forswears any per-sub-cause causal attribution (that would be the OS-shootout analysis the
  phase rejects); boost state is recorded (F3) and surfaced next to affected findings so the reader
  knows exactly what the bundle contains.
- **Alternatives rejected.** Tuning only for the pyperf runs (two machine states → two
  environment records → the "one recorded environment" rule broken); leaving boost on to "match
  Windows and reduce confounds" (rejected — it would *defeat* the phase's purpose of exercising
  Linux best-stability conditions, and it does not even yield a clean state: `pyperf system tune`
  believes turbo is handled via its Intel paths while AMD boost stays on, producing an
  **internally inconsistent tune** more ambiguous than a uniformly disclosed boost-off; the charter
  enrolls "isolation conditions", and pyperf's tune — turbo-off included — is precisely those);
  adding `nice`/`chrt` per harness (a knob with no Windows-decision counterpart — an unauditable
  variable).
- **Grounding.** pyperf system docs (verified, Assumed state); plan back-compat §one-machine
  discipline; plan risk row on hidden machine-level differences.

### D3 — The environment record is a closed checklist, captured by script
- **Decision.** `capture-environment.sh` collects exactly the closed lists of
  [environment-and-toolchains.md — D3](environment-and-toolchains.md#environment-record-checklist-d3--closed-list):
  machine/firmware identity (M1–M5), kernel/mitigations/isolation (K1–K8, mitigations at kernel
  defaults and recorded), frequency policy (F1–F7), and the per-engine
  **toolchain / runtime / libc / allocator identity table** (one row per ecosystem: version
  string, install channel, target triple, glibc, allocator note), plus repo/corpus commit state.
  The report's environment block summarizes it; the full capture ships in the published
  directory.
- **Rationale.** Closes the plan's two flagged recording assumptions (machine-level items; the
  platform-toolchain identity that necessarily accompanies the OS change) as one enumerated,
  script-captured list — disclosed structural properties, not eliminable confounds.
- **Alternatives rejected.** Free-form "record the environment" (exactly the undocumented
  decision this spec exists to eliminate); attempting Windows/Linux firmware *equalization*
  beyond invariance (nothing to equalize — the settings are shared by construction when
  untouched).
- **Grounding.** Plan risk rows (firmware/mitigation/frequency; toolchain drift front (b));
  metrics-protocol environment-block shape.

### D4 — Version pinning: identical platform-independent pins, recorded deltas elsewhere
- **Decision.** All package-manager pins must be byte-identical to each ecosystem spec's pins
  (enforced by the same committed lockfiles/manifests); platform-specific artifacts (Node, Go,
  Rust toolchain, .NET SDK, Temurin, CPython) install the same version *number* for linux-x64,
  with the platform build identity recorded in the D3 table; every unavoidable difference lands
  in a published `version-deltas.md` whose non-empty rows each create an inline caveat
  obligation on affected validation statements. No upstream upgrades are adopted. Full procedure
  in [environment-and-toolchains.md — D4](environment-and-toolchains.md#version-pinning-vs-each-windows-source-run-d4).
- **Rationale.** Bounds toolchain-drift front (a) (plan risk table) to near zero for libraries
  and to a recorded, caveat-carrying delta for platform artifacts; the Windows reports'
  environment blocks — not this spec — are the delta baseline, so the procedure stays correct
  even if a source run shipped with a later pin.
- **Alternatives rejected.** "Latest at run time" (unattributable drift); refusing to run on any
  delta (a single unavailable Temurin build would forfeit the whole JVM validation — worse than
  a disclosed delta).
- **Grounding.** Plan risk table row 1 + its mitigation (verbatim posture); Phase 7
  drift-disclosure posture (plan).

### D5 — CPython on Linux: deadsnakes 3.14, build provenance recorded
- **Decision.** deadsnakes PPA `python3.14` (Ubuntu 24.04's archive carries 3.12); exact micro +
  package version recorded, micro delta vs the Windows run's 3.14.6 recorded if present;
  fallback on PPA unavailability: source build of 3.14.6 with
  `--enable-optimizations --with-lto`; either way `CONFIG_ARGS` build provenance is captured in
  the D3 table. Details in
  [environment-and-toolchains.md — D5](environment-and-toolchains.md#cpython-on-linux-d5).
- **Rationale.** Interpreter build flags (PGO/LTO) move Python wall times materially; the
  Windows run used the python.org PGO installer, so provenance must be a disclosed
  platform-toolchain item, not an assumption. deadsnakes is the standard maintained 3.14-on-LTS
  channel; a most-reversible choice — switching to the source-build path is one recorded change.
- **Alternatives rejected.** Ubuntu archive python3 (wrong minor, 3.12); standalone-build
  distributions (third-party toolchain/build flags, less attributable than deadsnakes or a
  self-built `CONFIG_ARGS`-recorded binary).
- **Grounding.** Phase 5 D2 pins + run protocol; CPython build docs (External references).

### D6–D11 — Per-harness Linux stability settings
- **Decision.** One closed decision per harness, each mirroring the structure of its ecosystem
  spec's Windows-settings decision, all inside the D2 state — normative text in
  [harness-settings-and-validation.md](harness-settings-and-validation.md#per-harness-linux-stability-settings-d6d11):
  **D6 BenchmarkDotNet** — identical protocol-run invocation, harness defaults, BDN's own
  priority behavior recorded; **D7 Criterion** — Phase 2 D9 builder verbatim (3 s / 10 s / 100 /
  0.95 / `configure_from_args`, `--noplot`), no pinning, 5%-CI trigger re-targeted to `taskset`;
  **D8 JMH** — Phase 3 annotation regime and single `-prof gc` invocation verbatim, Temurin 25
  linux-x64, DCE plausibility pass carried; **D9 mitata** — same Node flags and mitata defaults,
  `run-js.sh` launcher (no priority class), the Phase 4 D13 five-run stability procedure
  **re-executed on Linux with the same 5%/10% RSD thresholds as a publication gate**;
  **D10 pyperf** — system tune + isolated-pair boot + explicit `--affinity=<isolated pair>` +
  Runner defaults (20×3×1) unchanged, memory pass unchanged, Linux stability-posture paragraph
  replacing the Windows one; **D11 Go** — `-count=20`, `-benchtime=1s`, prebuilt binary launched
  plain, `GOMAXPROCS` recorded as found, benchstat ±5% trigger re-targeted to `taskset`.
- **Rationale.** Q1.6's structure ("per-harness stability settings resolved in each ecosystem
  spec") is mirrored one-for-one: every Windows knob either carries verbatim (where
  OS-independent), is replaced by its documented Linux counterpart (pyperf), or is deliberately
  dropped under the D2 no-priority rule with the drop recorded. Triggers survive with Linux
  mechanisms so instability handling stays evidence-driven, not knob-driven.
- **Alternatives rejected.** One uniform setting set imposed across harnesses (the protocol
  explicitly refuses this); per-harness `nice`/`chrt` (D2); re-tuning harness statistics
  (Q2.1 bars it).
- **Grounding.** The six Windows-settings decisions (Assumed state row); pyperf runner/system
  docs (verified); benchstat count guidance.

### D12 — Gate re-assertion on Linux, with contract-evidence triage
- **Decision.** Before any timing, each shipped ecosystem's own gate commands run on Linux
  (table in
  [harness-settings-and-validation.md](harness-settings-and-validation.md#gate-re-assertion-on-linux-d12)),
  re-asserting N1–N5 **including N3b** (the program-wide comparison-time whitespace strip that
  removes every whitespace run to nothing from both sides — maintainer decision 2026-07-20,
  Phase 1 D8) in every runtime. Failure triage, in order: capture →
  checksum-vs-divergence classification → (whitespace-only cannot survive N3b) →
  `excluded — documented evidence (Linux)` under the Q1.2/Phase 1 D11 machinery with evidence in
  `portability-findings.md` → reported as a portability finding in part 2 → upstream defects go
  to the amendments ledger, never patched locally. Windows-only tooling assumptions are fixed
  only in this phase's own wrappers.
- **Rationale.** The plan's internal ordering and risk rows verbatim: gates before measurement;
  a gate catching a real cross-OS difference is the contract working; report immutability and
  artifact ownership stand.
- **Alternatives rejected.** Local template/gate patches (breaks read-only consumption);
  skipping the failing cell silently (barred by honest-reporting rule 6 and the manifest rule).
- **Grounding.** Q1.2 (as amended — N3b); Phase 1 D11; plan §Back-compat; amendments-ledger
  convention.

### D13 — Measurement runs: same shapes, anchor-first order, Q7.2-mirrored manifest
- **Decision.** The run matrix re-executes each ecosystem spec's measurement shapes unchanged
  (referenced, not restated), ordered: intra-.NET first (produces the Linux Heddle anchor rows),
  then Rust → JVM → JS → Python → Go; absences/blocks/exclusions recorded in an inclusion
  manifest mirroring Phase 7's subset semantics. Matrix in
  [harness-settings-and-validation.md](harness-settings-and-validation.md#measurement-runs-d13).
- **Rationale.** Full-suite breadth is the plan's recorded decision (a ranking cannot be
  validated by re-measuring one participant); anchor-first because every ratio column needs the
  Linux Heddle row; manifest semantics are inherited, not reinvented.
- **Alternatives rejected.** Python-only spot check (rejected in the plan); re-deriving new
  measurement shapes for Linux (would validate a different experiment).
- **Grounding.** Plan §Design direction; Q7.2 resolution; each ecosystem harness doc.

### D14 — Report part 1: protocol-format Linux publication
- **Decision.** One immutable `docs/benchmarks/<date>/` directory; `index.md` opens with a
  verbatim framing statement (cross-check, not an OS comparison; Windows numbers remain the
  numbers of record), then the protocol's exact section skeleton with the environment summary,
  inclusion manifest, and per-ecosystem tables in each Windows report's own shapes — Heddle
  reference rows sourced from the **Linux intra-.NET run in the same directory**, ratio columns
  anchored to them (Q2.2 = A and Q6.2 applied within the report), presentation rules 1–5 and
  honest-reporting rules 1–6 in full, all mandated verbatim label texts carried — the one
  deliberately-adapted exception being the Heddle reference-row label itself (it names the
  **Linux** intra-.NET source "from part 1 of this report" rather than a dated Windows run,
  as D17.4 requires; the adaptation is called out where the label is pinned in the supplement).
  Full format in
  [harness-settings-and-validation.md](harness-settings-and-validation.md#part-1--protocol-format-linux-publication-d14).
- **Rationale.** "Complete protocol-format publication of the Linux run" is the plan's part-one
  definition; anchoring to the Linux-side Heddle row is what keeps every ratio within one OS.
- **Alternatives rejected.** Anchoring ratios to the Windows Heddle numbers (a cross-OS
  juxtaposition in ratio clothing); publishing per-ecosystem Linux directories (fragments the
  single cross-check event and multiplies environment blocks).
- **Grounding.** Plan §Design direction (structural validation, separately published);
  metrics-protocol presentation rules; Q2.2/Q6.2.

### D15 — Dimensionless dispersion form: harness-native relative dispersion `d`
- **Decision.** Cross-OS dispersion is compared only as a per-cell relative dispersion `d`,
  defined per harness (BenchmarkDotNet `StdDev/Mean`; Criterion relative 95% CI half-width; JMH
  `Error/Score`; mitata `(p99 − avg)/avg` for both tracks; pyperf `std/mean`; benchstat `±%`/100),
  never compared across harnesses, and used in the D16 combination formulas (both the D16.1
  rank-resolution test and the D16.2 gap-movement bound). Table + rationale in
  [harness-settings-and-validation.md](harness-settings-and-validation.md#part-2--findings-validation-d15d16-close-the-plans-flagged-details).
- **Rationale.** Every cross-OS dispersion comparison is same-harness by construction, so the
  form needs fixing only per harness — and harness-native definitions extend Q2.1's
  each-harness-stays-native credibility to dispersion. This *is* the plan's "CV or relative CI
  width" choice, resolved as: CV where the harness reports mean+stddev, relative CI half-width
  where it reports a CI, the published `±%` for benchstat, and — for mitata — the relative
  upper-tail spread `(p99 − avg)/avg` of its own per-cell distribution, uniform across **both**
  the controlled and idiomatic tracks (every published mitata cell carries `avg` and `p99`).
- **Alternatives rejected.** Forcing CV everywhere (requires re-deriving stddevs from raw
  samples for CI harnesses — a second analysis, barred in spirit by Q2.1); using mitata's raw
  min…max spread (outlier-sensitive extremes); using the five-run stability RSD as mitata's `d`
  (it is published for the **controlled suite only**, so it yields no `d` for the JS idiomatic
  track on either OS — leaving WI7's mandatory per-track validation uncomputable for JS idiomatic
  — and is a between-run statistic where every other `d` is within-run; it stays the JS
  publication-gating stability verdict of D9, not the findings-validation dispersion).
- **Grounding.** Q2.1 resolution; metrics-protocol statistic mapping (mitata's per-cell
  `avg`/percentile spread published for both tracks); Phase 4 D13 stability artifacts (both OSes
  ship `stability-summary.md`, which keeps its D9 publication-gating role).

### D16 — Material-divergence thresholds and the combination formula
- **Decision.** Three closed definitions
  ([full text](harness-settings-and-validation.md#part-2--findings-validation-d15d16-close-the-plans-flagged-details)):
  (1) **rank change** is material iff the pair's order differs across OSes *and* is resolved
  beyond combined same-OS dispersion, evaluated in dimensionless **ratio** form
  (`|r_A − r_B| > (d_A + d_B)·min(r_A,r_B)`, `r_X` = wall-time ratio to that OS's Heddle anchor —
  the homogeneous reduction of the wall-time inequality, so no absolute Windows time is needed)
  on **both** OSes; (2) **gap movement** is material iff
  `|r_L/r_W − 1| > max(D, 0.05)` where
  `D = (d_engine,W + d_Heddle,W) + (d_engine,L + d_Heddle,L)` (linear sums — interval
  arithmetic, not quadrature, because the `d` statistics are heterogeneous in kind); the 5%
  floor reuses the program's own established stability line (Criterion/benchstat/mitata
  triggers); (3) **dispersion character change** iff `max(d_W,d_L)/min(d_W,d_L) ≥ 2` and
  `max ≥ 0.01`, with the pyperf-vs-Phase-5-posture paragraph mandatory. All values (`r_W`,
  `r_L`, movement, `D`, `d_W`, `d_L`, verdict) are published for **every** cell — "material"
  controls prominence, never visibility.
- **Rationale.** Makes the plan's "materially" checkable by a script: resolved-on-both-OSes
  prevents declaring flips inside noise; the linear-sum bound is the only combination the
  heterogeneous dispersion statistics can honestly support, and its conservatism cannot hide
  anything because every number is published regardless; the floor is grounded in thresholds
  the program already ratified rather than invented.
- **Alternatives rejected.** Quadrature (assumes independence and uniform coverage across CV /
  95% / 99.9% statistics); no floor (flags noise-scale movement as findings when dispersions
  are very tight, manufacturing false divergence headlines); a rank-change rule counting
  unresolved swaps (would report scheduler noise as environment sensitivity).
- **Grounding.** Plan §Design direction ("at WHAT level" paragraph) and success criteria; the
  phase 2/4/6 5% triggers (Assumed state row).

### D17 — No-cross-OS-absolute-time rules as checkable criteria
- **Decision.** Six mechanical publication-blocking checks
  ([full list](harness-settings-and-validation.md#d17--no-cross-os-absolute-time-rules-as-checkable-criteria)):
  no Windows-attributed absolute value anywhere in the Linux report (sweep + grep against the
  cited reports' transcribed headline numbers); no modification to any existing
  `docs/benchmarks/*` or `docs/plan/*` file; no time-unit token attached to a Windows figure in
  part 2; ratios anchored Linux-side in part 1 and quoted-as-published in part 2
  (`windows-source.json` stores ratios, never recomputes them from absolute times); framing
  statement first; all verbatim labels present.
- **Rationale.** Turns the plan's forbidden-juxtaposition success criteria into pass/fail
  checks an implementer and reviewer can run; the structural rule (store ratios, not times) makes
  the worst violation impossible rather than merely forbidden.
- **Alternatives rejected.** Prose-only prohibition (unenforceable); storing Windows absolute
  times in the tooling "for convenience" (invites exactly the recomputation the rule bars).
- **Grounding.** Plan §Non-goals + success criteria 8–9; validation scenarios rows 3–4.

### D18 — Committed scripts and artifacts
- **Decision.** All phase-owned tooling lives in a new **`benchmarks/linux-crosscheck/`**
  directory, committed at the published commit: `README.md` (run book), `setup-toolchains.sh`,
  `capture-environment.sh`, `tune.sh` / `untune.sh` (D2 enter/exit incl. boost + `system show`
  capture), `run-dotnet.sh`, `run-rust.sh`, `run-jvm.sh`, `run-js.sh`, `run-python.sh`,
  `run-go.sh`, `run-all.sh` (WI order, stop-on-red-gate), `grub-isolation.cfg.example` (the
  boot-entry template), `windows-source.json` (transcribed Windows **ranks, ratios (`r_W`), and
  dimensionless dispersions (`d`)** — never absolute times, D17.4 — with per-value source-file
  citations), and `validate.py` (+ `test_validate.py`) computing every
  part 2 table and verdict. The published `docs/benchmarks/<date>/` directory contains:
  `index.md`, every harness's raw artifacts (same types as its Windows report), `environment/`
  (full D3 capture + run log), `version-deltas.md`, `stability-summary.md` (+ five JS captures),
  and `portability-findings.md` (only if a gate failure occurred). The report's reproduce block
  points at `benchmarks/linux-crosscheck/README.md`.
- **Rationale.** The plan's new-surface commitment (Linux reproduce path) with exact paths; a
  dedicated directory keeps every fix out of phase 1–7 artifacts by construction.
- **Alternatives rejected.** Scattering wrappers into each ecosystem's harness directory
  (edits phase-owned trees); leaving `windows-source.json` untranscribed and parsing five
  heterogeneous Windows artifact formats live (fragile, and its failure mode — silent misreads —
  is worse than a spot-checked transcription).
- **Grounding.** Plan §Back-compat "New surface"; success criterion 10.

## Implementation plan

Ordered; the plan's internal ordering, made concrete. Every WI runs only after all Windows runs
are published (D1 gate).

### WI1 — Environment establishment
- **Files.** New: `benchmarks/linux-crosscheck/README.md`, `grub-isolation.cfg.example`,
  `tune.sh`, `untune.sh`.
- **Change.** Execute [environment-and-toolchains.md — D1](environment-and-toolchains.md#dual-boot-establishment-d1):
  install Ubuntu Server 24.04.x + HWE kernel per the rules, add the isolation boot entry
  (recording the actual sibling pair), quiesce list applied, Windows-preservation checks done.
- **Done when.** The isolation boot shows the expected `/proc/cmdline` and
  `/sys/devices/system/cpu/isolated`; Windows still boots; the run log records the D1
  "recorded" items and the precondition line.

### WI2 — Environment record + session-state tooling
- **Files.** New: `benchmarks/linux-crosscheck/capture-environment.sh`.
- **Change.** Implement the D3 closed checklist verbatim; `tune.sh` performs D2 steps 2–4 and
  fails if any post-condition (governor, boost=0, sample rate, isolated set) does not read
  back as expected.
- **Done when.** `tune.sh && capture-environment.sh` produces the full record with every
  M/K/F item present and boost reading 0; `untune.sh` restores and `pyperf system show`
  confirms.

### WI3 — Toolchains installed, deltas recorded
- **Files.** New: `benchmarks/linux-crosscheck/setup-toolchains.sh`; output
  `version-deltas.md` (draft, finalized at publication).
- **Change.** Install per the D4/D5 matrix (exact versions from the published Windows reports'
  environment blocks); fill the D3 per-engine identity table; produce the delta table.
- **Done when.** Every shipped ecosystem's toolchain version-asserts inside its own reproduce
  path (e.g. Go's version assertions, npm `engine-strict`, `rustup` pin, `python -VV`), and
  `version-deltas.md` exists with every row either `identical` or delta + caveat obligation.

### WI4 — Gate re-assertion sweep
- **Files.** Changed: none outside this phase; new (only on failure):
  `docs/benchmarks/<date>/portability-findings.md`.
- **Change.** Run the D12 table of gate commands per shipped ecosystem; apply the triage steps
  on any failure.
- **Done when.** Every shipped ecosystem's gate commands exit 0 on Linux, or each failing cell
  has a completed triage record (evidence, exclusion marker, manifest row, ledger proposal if
  applicable) — no undispositioned failure.

### WI5 — Linux launchers and validation tooling
- **Files.** New: `benchmarks/linux-crosscheck/run-{dotnet,rust,jvm,js,python,go,all}.sh`,
  `windows-source.json`, `validate.py`, `test_validate.py`.
- **Change.** Launchers per D6–D11 (each asserts the tuned state via a `pyperf system show`
  comparison before timing, then invokes the ecosystem's own commands); transcribe
  `windows-source.json` from the published Windows reports (ratios, `d` values, ranks — never
  absolute times, D17.4) with per-value citations; implement `validate.py` verdicts per
  D15/D16.
- **Done when.** `test_validate.py` passes (fixtures straddling each threshold: resolved and
  unresolved flips, movement just above/below `max(D, 0.05)`, dispersion ratio just
  above/below 2 and the 1% floor, and a **zero-dispersion cell** (`min(d)=0`) exercising the
  D16.3 divide-by-zero guard); a documented spot-check of ≥ 3 `windows-source.json` values
  per ecosystem against the cited report files is recorded in the run log; launchers refuse to
  run when the state assert fails.

### WI6 — Measurement runs
- **Files.** Run artifacts under `benchmarks/*/results` equivalents, copied to the publication
  directory in WI8.
- **Change.** Execute the D13 matrix in order (intra-.NET anchors first; JS stability procedure
  before JS timing), inside one D2 session state; apply the D6–D11 triggers as written when
  tripped.
- **Done when.** Every shipped ecosystem × track × workload cell has either a timed artifact
  with dispersion or a manifest-recorded exclusion; the JS stability verdict is `verified` or
  `verified-with-disclosure`; the JMH DCE plausibility pass and Go regeneration-freshness check
  passed.

### WI7 — Findings validation
- **Files.** Generated part 2 tables from `validate.py`; prose authored into `index.md`.
- **Change.** Compute every D16 verdict; author the per-ecosystem × per-track prose with
  Windows citations, inline version-delta caveats (D4), the mandatory pyperf/Phase 5 posture
  paragraph, and Phase 7 findings enumeration if it shipped.
- **Done when.** Every shipped ecosystem × track has its table + prose; every material item is
  named in prose with its claim disposition (`held`/`narrowed`/`environment-sensitive`); a
  no-material-divergence outcome, if that is the outcome, states the exact validation scope.

### WI8 — Publication
- **Files.** New: `docs/benchmarks/<yyyy-MM-dd>/` (index.md + all D18 artifacts); changed:
  `docs/spec/README.md` (master-index row for this initiative, per the index-maintenance
  convention).
- **Change.** Assemble the D14 report; run the D17 checklist and the testing-plan sign-off;
  `untune.sh` + default-boot restore afterwards.
- **Done when.** All six D17 checks pass and are recorded; `git diff --stat` against the
  pre-phase baseline shows only additions under `docs/benchmarks/<date>/`,
  `benchmarks/linux-crosscheck/`, and the index row; the directory is complete per D18.

## Public API / contract

No .NET, library, or engine API is added or changed anywhere. The phase's externally consumed
surface is artifact-shaped:

| Artifact | Consumers | Normative definition |
|---|---|---|
| `docs/benchmarks/<date>/` Linux cross-check report | readers; the program's credibility record | [harness-settings-and-validation.md — report](harness-settings-and-validation.md#the-report-d14d17) |
| `benchmarks/linux-crosscheck/` scripts + `windows-source.json` + `validate.py` | reproducers of the Linux run | D18; [environment-and-toolchains.md](environment-and-toolchains.md) |
| `portability-findings.md` (conditional) | contract v2 evidence trail; any future amendments-ledger entry | [D12 triage](harness-settings-and-validation.md#gate-re-assertion-on-linux-d12) |

Thread-safety: not applicable — all tooling is single-run shell/Python driving existing
harnesses.

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — no compiler, grammar, or engine surface changes; the
diagnostic registry is untouched.

| Surface | Type / exit | Trigger |
|---|---|---|
| `tune.sh` state assert | exit 1, names the failing item (governor / boost / sample rate / isolated set) | any D2 post-condition not reading back |
| `run-*.sh` pre-flight | exit 1, `state drift: <diff excerpt>` | `pyperf system show` differs from the recorded session state |
| Ecosystem gates on Linux | each harness's own existing failure surface (unchanged) | contract v2 / verifier failure — triaged per D12 |
| `setup-toolchains.sh` | exit 1 per component, `pin unavailable: <component> <version>` | a D4 must-be-identical pin cannot be installed |
| `validate.py` | exit 1 on malformed inputs; verdict output otherwise (verdicts are data, not errors) | missing artifact, citation-less `windows-source.json` entry, absolute-time field present (D17.4 structural check) |
| D17 publication checklist | publication blocked; failing check named in the run log | any of the six checks failing |

## Testing plan

**TDD verdict.** The gates and checklists are the executable spec, as throughout this program:
the D12 gate sweep runs red-first on Linux before any number exists; `validate.py` is built
against `test_validate.py`'s threshold-straddling fixtures before it sees real data; the D17
checklist gates publication. No xUnit additions; no `src/` code is touched (the whole existing
`Heddle.Tests` suite is unaffected and not re-run for this phase beyond the .NET harness's own
build).

**Named checks.**
- Gate sweep: the six ecosystems' own gate commands, exit-code-gated, on Linux (WI4).
- `test_validate.py`: rank-resolution fixtures in ratio form (resolved flip → material;
  unresolved flip → "not resolved" verdict), gap fixtures (movement `= max(D,0.05) ± ε`),
  dispersion fixtures (ratio 2 ± ε; 1% floor; a `min(d)=0` zero-dispersion cell exercising the
  D16.3 divide-by-zero guard), plus a D17.4 structural fixture (input containing an absolute-time
  field must be rejected).
- `windows-source.json` spot-check: ≥ 3 values per ecosystem against cited files, recorded.
- JS Linux stability procedure (Phase 4 D13 thresholds, publication-gating).
- JMH DCE plausibility pass; Go `git diff --exit-code -- '*_templ.go'` freshness; corpus SHA-256
  checks inside every gate runner.

**Regression gate (before the publication commit).**
1. WI4 all green (or fully triaged) — recorded.
2. `test_validate.py` green; spot-check recorded.
3. D17 six checks green — recorded in the run log.
4. `git diff` cleanliness check (D17.2): no existing file modified.
5. Plan success-criteria checklist (all ten boxes of the
   [plan](../../../plan/phase-8-linux-crosscheck.md#success-criteria)) ticked line by line in
   the run log.

## Back-compat and migration

- **Phase 1–7 artifacts and published reports: untouched**, proven by D17.2's git check. Gate
  failures produce evidence and ledger proposals, never local patches (D12).
- **The one-machine Windows protocol (Q1.6): reaffirmed** — the report states the Windows
  numbers remain the numbers of record; the Windows boot and its recorded environment survive
  (D1 preservation checks) for any future protocol-conformant run.
- **The physical machine** gains a dual-boot configuration only after all Windows publishing is
  complete (D1 sequencing gate); firmware settings are unchanged.
- **No breaking window engaged:** everything is additive (one report directory, one tooling
  directory, one index row).
- **Cuttability:** if the phase is cut after partial work, nothing published changes; the plan
  records the cut as forfeiting robustness evidence (plan §Risks, final row).

## Performance considerations

- No Heddle hot path, no engine code, no harness code is modified; the phase measures, it does
  not optimize.
- Session budget (informs scheduling, not a gate): JMH ≈ 4.5–5.5 h dominates; BenchmarkDotNet
  eight suites ≈ 1–2 h; Go ≈ 1.5 h; Rust ≈ 0.5 h (33 × 13 s + gates); pyperf five scripts +
  memory pass ≈ 1–2 h; JS stability (5×) + three runs ≈ 1 h — a full session is a one-to-two
  working-day machine reservation, consistent with the plan's downtime risk note.
- `isolcpus` removes the isolated pair from every non-pinned harness's affinity mask (recorded,
  disclosed); render loops are single-threaded throughout, so the reduced mask affects
  background runtime threads only — any effect is part of the deliberately-changed variable and
  is visible in the published dispersion.
- Boost-off lengthens absolute wall times for every Linux number equally; no absolute time is
  cross-compared (D17), so no comparison is distorted.

## Standards compliance

- **DRY:** the Windows harness decisions are referenced and mirrored, never restated as new
  normative text (the six D6–D11 records carry only the deltas); measurement shapes are
  referenced from the owning specs (D13); the environment checklist lives once
  (supplement D3) and the report summarizes it.
- **YAGNI:** no dual-OS automation framework, no recurring-matrix tooling, no generic
  cross-OS statistics library — one script set for one deliberate event; `validate.py` computes
  exactly the three D16 verdicts and nothing speculative.
- **Most-reversible where evidence is short:** deadsnakes-vs-source (D5), the HWE-kernel choice
  (D1), and the `taskset` re-run triggers (D7/D11) are all single-recorded-change reversals
  with named triggers.
- **Right seam:** all fixes live in `benchmarks/linux-crosscheck/` (D18), keeping phase 1–7
  ownership boundaries structural rather than disciplinary.

## Deferred items

| Item | Trigger |
|---|---|
| A second Linux cross-check (any future re-run under newer Ubuntu/toolchains) | A user ruling; the plan fixes this phase as a single deliberate event, not a recurring matrix |
| Automating the Windows-source transcription (parsing all five harness artifact formats) | A second cross-check making the one-time hand transcription + spot-check demonstrably error-prone |
| Priority/affinity experiments beyond the recorded triggers (e.g. `chrt`, cgroup shielding) | A D6–D11 trigger firing **and** the `taskset` re-run still exceeding its threshold — then proposed as a recorded environment change, never ad hoc |
| Windows-boot re-validation runs after the Linux phase | Any future protocol-conformant Windows run (Q1.6 machinery, unchanged by this phase) |
| Consuming the Phase 7 *spec* (beyond its published findings) | The Phase 7 spec shipping with manifest wording that diverges from this spec's Q7.2 mirror — reconcile via the amendments ledger |

## External references

- pyperf — system tuning operations, isolation detection, `--affinity`, Runner defaults
  (primary, fetched 2026-07-21): <https://pyperf.readthedocs.io/en/latest/system.html>,
  <https://pyperf.readthedocs.io/en/latest/runner.html>
- Ubuntu 24.04 LTS release/support facts (primary, fetched 2026-07-21):
  <https://ubuntu.com/about/release-cycle>; kernel/HWE packaging:
  <https://ubuntu.com/kernel/lifecycle>
- Linux kernel boot parameters (`isolcpus`, `nohz_full`, `rcu_nocbs`):
  <https://www.kernel.org/doc/html/latest/admin-guide/kernel-parameters.html>
- amd-pstate driver and the cpufreq `boost` control:
  <https://www.kernel.org/doc/html/latest/admin-guide/pm/amd-pstate.html>
- benchstat repeated-run guidance: <https://pkg.go.dev/golang.org/x/perf/cmd/benchstat>
- BenchmarkDotNet (cross-platform behavior, priority handling): <https://benchmarkdotnet.org/>
- Criterion.rs configuration: <https://bheisler.github.io/criterion.rs/book/>
- JMH: <https://github.com/openjdk/jmh>; mitata: <https://github.com/evanwashere/mitata>
- CPython build/optimization flags (`--enable-optimizations`, `--with-lto`):
  <https://docs.python.org/3/using/configure.html>; deadsnakes PPA:
  <https://launchpad.net/~deadsnakes/+archive/ubuntu/ppa>
- Program documents bound by this spec:
  [metrics-protocol](../phase-1-cross-stack-foundation/metrics-protocol.md),
  [parity-contract-v2](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  [golden-corpus](../phase-1-cross-stack-foundation/golden-corpus.md), the phase 2–6 harness
  documents (Assumed state row), and the repo conventions:
  [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md)
