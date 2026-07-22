# Per-harness Linux settings, gate re-assertion, measurement runs, report, and findings validation

Supplementary document of the [Phase 8 — linux-crosscheck spec](README.md). It pins the Linux
stability settings for all six harnesses (each mirroring the structure of its ecosystem spec's
Windows-settings decision), the gate re-assertion procedure and its failure triage, the
measurement-run shapes, the two-part report format, and — closing the plan's two flagged spec
details — the dimensionless dispersion form and the exact material-divergence thresholds.
Decisions of record: README
[D6](README.md#d6d11--per-harness-linux-stability-settings)–[D17](README.md#d17--no-cross-os-absolute-time-rules-as-checkable-criteria).

Everything below runs inside the one recorded machine state of
[environment-and-toolchains.md — D2](environment-and-toolchains.md#one-machine-state-for-every-linux-number-d2):
tuned via `pyperf system tune`, boost off, the isolated SMT pair active, no per-harness priority
elevation.

## Per-harness Linux stability settings (D6–D11)

Each subsection names its Windows source decision, states the Linux setting set as a closed
decision, and carries the same instability trigger its ecosystem spec recorded (re-targeted to
Linux mechanisms). The statistic selection is the Phase 1
[mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21)
and is not varied by anything here.

### D6 — BenchmarkDotNet (intra-.NET suite; Windows source: Phase 1 D14 / metrics-protocol first exercise)

- Same invocation as the Phase 1 protocol run: `-c Release`, `net10.0`, BenchmarkDotNet 0.15.8,
  `[MemoryDiagnoser]` on, all eight suites, harness defaults — no affinity, no custom job.
- Priority: BenchmarkDotNet's **own default behavior**, unmodified; on Linux it attempts to raise
  process priority and logs a warning when it cannot — that log line is recorded verbatim in the
  environment notes (the D2 no-added-knobs rule; the harness's native posture is the setting).
- Any BenchmarkDotNet environment-validation warnings printed on Linux are quoted in the report.
- Trigger (mirrors the Windows posture of publishing dispersion rather than tuning): any suite
  whose `StdDev/Mean` exceeds 5% is re-run once; if it persists, the number is published with
  its dispersion and the instability named in prose — never suppressed.

### D7 — Criterion.rs (Rust; Windows source: Phase 2 D9)

- Identical builder, verbatim: `Criterion::default().warm_up_time(3 s).measurement_time(10 s)
  .sample_size(100).confidence_level(0.95).configure_from_args()`, invoked
  `cargo bench -- --noplot`; same bench naming, same `estimates.json` extraction
  (`mean.point_estimate`, 95% CI bounds). Nothing about the Criterion configuration is
  OS-conditional, so the Windows decision carries byte-identical.
- No pinning, no priority — mirroring D9's "no process-priority manipulation and no core
  pinning" stance; the tuned system replaces the quiet-Windows-box assumption.
- Trigger (D9's, re-targeted): any cell's 95% CI half-width > 5% of its mean → re-run the full
  Rust suite pinned to CCD0's non-isolated CPUs via `taskset -c <CCD0 list minus isolated pair>`
  as **one recorded environment change for the whole re-run**, never per-cell; the report then
  shows the pinned numbers and notes the unpinned dispersion.

### D8 — JMH (JVM; Windows source: Phase 3 harness-and-jmh measurement procedure)

- Identical annotation regime — `Mode.AverageTime`, ns, `@Fork(5)`, `@Warmup(5 × 10 s)`,
  `@Measurement(5 × 10 s)`, `@Threads(1)`, no `jvmArgs`, no `@CompilerControl` — and the
  identical invocation: `./mvnw -q clean verify` (gates wired into `verify`) then
  `java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json | tee jmh-log.txt`.
- Temurin 25 Linux x64 per the toolchain table; the JMH `# VM version` line is the recorded
  runtime identity. Expected duration unchanged (≈ 4.5–5.5 h); run unattended in the session
  state.
- The Phase 3 DCE plausibility pass (ns/op ≥ oracle byteLength / 10; size-ordering consistency)
  is executed on the Linux results identically before publication.
- Trigger: JMH's own per-benchmark `Error` (99.9% CI) exceeding 5% of `Score` on any cell → one
  re-run of that class; persistent → published with dispersion and named in prose (JMH's fork
  count already averages across processes; no further knob is added).

### D9 — mitata (JS; Windows source: Phase 4 D10–D13, run.ps1, stability procedure)

- Same Node flags (`--expose-gc --allow-natives-syntax`), same scripts, mitata defaults — no
  custom warmup/sampling — and the same DEOPT-CHECK in-process trailer discipline.
- Launcher: `run-js.sh` in this phase's tooling (README D18) replicates `run.ps1`'s capture and
  `-Repeat` behavior **without** the High priority class (D2 rule): starts
  `node --expose-gc --allow-natives-syntax <script>`, tees stdout to
  `artifacts/<name>.txt`, propagates exit code, `--repeat N` for the stability procedure. The
  phase 4 `run.ps1` is not modified (no edits outside this phase's tooling).
- **Linux stability verification (required, gates publication):** the Phase 4 D13 procedure is
  re-executed on Linux — five consecutive controlled-suite runs, per-cell RSD = stddev/mean of
  the five `avg` values, thresholds verbatim: all ≤ 5% → `verified`; any in (5%, 10%] after one
  investigate-and-re-run cycle → `verified-with-disclosure` (RSD table published); any > 10%
  persisting → `failed` → the JS ecosystem publishes no Linux numbers and the manifest records
  the reason. The five captures + `stability-summary.md` ship with the report. This five-run RSD
  is the JS **stability verdict** only; the JS findings-validation dispersion `d` is mitata's
  per-cell `(p99 − avg)/avg` (D15), which — unlike the controlled-only RSD — is defined for the
  idiomatic track too.

### D10 — pyperf (Python; Windows source: Phase 5 D8 / harness.md run protocol)

The named driver of the phase — the one harness whose Linux-native guidance now actually
applies:

- **System tune:** already applied session-wide (environment doc D2): governor `performance`,
  min frequency raised, `irqbalance` stopped + IRQ affinity managed,
  `perf_event_max_sample_rate=1`, turbo/boost off — pyperf's documented operations, state
  recorded via `pyperf system show`
  ([pyperf system docs](https://pyperf.readthedocs.io/en/latest/system.html)).
- **CPU isolation:** the kernel boots with `isolcpus=<pair> nohz_full=<pair> rcu_nocbs=<pair>`
  (pyperf's documented recommendation). pyperf's Runner pins workers to isolated CPUs
  automatically when it detects them (documented default); the run passes
  `--affinity=<isolated pair>` explicitly anyway so the pinning is a recorded flag, not an
  inference ([pyperf runner docs](https://pyperf.readthedocs.io/en/latest/runner.html)).
  **Affinity-target delta, disclosed:** the Windows source run pinned pyperf to a **single**
  logical CPU (`--affinity=4`, Phase 5); this run pins the **isolated SMT pair** (both
  thread-siblings of one physical core, with that pair removed from every other task by
  `isolcpus`), so the single-threaded worker effectively owns a whole physical core with an idle
  sibling instead of sharing a core with background threads. This is a consequence of the Linux
  isolation regime (pyperf's Runner pins to the *detected isolated set*, which is the pair), not
  an independent knob — but because it changes measurement conditions in a way that touches
  **variance**, it is recorded as a known per-harness affinity delta in the environment notes and
  is surfaced next to the mandatory pyperf/Phase-5 dispersion comparison (D16.3), exactly as the
  boost-off divergence is, so a dispersion change is not misread as a pure OS effect.
- **Runner settings otherwise unchanged from Windows:** pyperf defaults — 20 processes × 3
  values × 1 warmup, loops auto-calibrated (≥ 100 ms) — same five bench scripts, same JSON
  outputs, same `pyperf stats` dumps committed. No elevated shell, no priority request:
  `REALTIME_PRIORITY_CLASS` was the Windows-only accommodation; its Linux counterpart *is* the
  tune + isolation.
- **Memory pass:** `mem_tracemalloc.py` runs identically (separate pass, never combined with
  timing — Phase 5 D11); tracemalloc semantics are OS-independent.
- The report's environment notes replace the Phase 5 Windows stability-posture paragraph with
  its Linux counterpart: this run applies pyperf's documented best-stability configuration
  (system tune + isolated CPUs + affinity); dispersion is still published with every number.

### D11 — Go testing/benchstat (Go; Windows source: Phase 6 stability-settings table)

- Same flags: `-count=20` (benchstat guidance), `-benchtime=1s` stated explicitly,
  `-run '^$' -bench '^BenchmarkRender$'` (cold-parse sidebar separate), prebuilt binary
  (`go test -c -o bench ./suites`) launched directly — no `nice`, no `taskset` (D2 rule; the
  Windows `start /high` line's Linux counterpart is the tuned system).
- `GOMAXPROCS`/`GOGC` runtime defaults, actual values recorded (`GOMAXPROCS` is expected to
  reflect the isolcpus-reduced affinity mask — recorded and disclosed, not overridden).
- benchstat over the run outputs exactly as on Windows; `sec/op` + `±%` are the published pair.
- Trigger (Phase 6's, re-targeted): benchstat variation > ±5% on any suite → that suite re-run
  pinned to CCD0's non-isolated CPUs via `taskset -c <list>`, pinning recorded, pinned numbers
  shown with unpinned dispersion noted.

## Gate re-assertion on Linux (D12)

Before any timing, per shipped ecosystem, the ecosystem's **own** gate commands run on Linux —
the same programs, unmodified:

| Ecosystem | Commands (from the ecosystem's harness directory) | Pass condition |
|---|---|---|
| .NET | `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- parity` then `-- verify-corpus` | eight `[PASS]` blocks; exit 0 twice |
| Rust | `cargo run --release --bin gate` | 32 `[PASS]` cells, exit 0 |
| JVM | `GateCli calibrate` then `GateCli gate` (via `./mvnw -q clean verify`) | exit 0; excluded cells print their recorded marker |
| JS | `npm run selftest` then `npm run gate` | exit 0 twice |
| Python | `python -m runner.selftest`, `python -m runner.gate_all --track controlled`, `--track idiomatic` | exit 0 × 3 |
| Go | `go test ./suites` (TestMain runs all gates) | exit 0 |

This re-asserts the full contract-v2 pipeline — N1 (UTF-8, BOM survives), N2, N3, **N3b (the
comparison-time whitespace strip that removes every whitespace run, anywhere, to nothing from both
the oracle and the candidate, so the gate compares non-whitespace bytes only; program-wide per the
maintainer decision of 2026-07-20 recorded in [Phase 1 D8](../phase-1-cross-stack-foundation/README.md#d8--normalization-v2-v1s-three-steps--defined-encoding--portable-whitespace-definition--n3b-run-collapse)
and parity-contract-v2 §normalization-pipeline)**, N4,
N5 (encoded), byte compare, security floor, idiomatic verifier — in every runtime, on Linux,
which is the live test of contract v2's byte-identical-means-the-same-everywhere claim the plan
names. Windows-only assumptions in gate tooling surfaced here (path separators, shell wrappers)
are fixed **in this phase's own tooling** (wrapper scripts under `benchmarks/linux-crosscheck/`),
never by editing a phase 1–7 artifact; if a fix is impossible without touching an owned artifact,
that is an amendments-ledger proposal to the owning phase, and the ecosystem waits or is
manifest-recorded as blocked.

**Failure triage (exact steps, in order):**

1. **Capture** the full failure surface (first-diff excerpt / verifier kind + needle) into
   `docs/benchmarks/<date>/portability-findings.md` (created on first failure).
2. **Classify checkout corruption first:** the gate runners verify corpus SHA-256 against the
   manifest; a checksum failure is an environment/checkout defect — fix and re-run, no finding.
3. **Whitespace-only divergence cannot reach this point** (N3b reconciles it by construction —
   Q1.2 as amended); a surviving diff is therefore non-whitespace divergence or an encoding
   difference. Diagnose and attribute: locale/filesystem/encoding behavior, engine
   platform-specific output, or a defect in a shipped artifact.
4. **Contract-evidence treatment (Q1.2 / Phase 1 D11 machinery):** the failing cell is marked
   `excluded — documented evidence (Linux)` with the evidence in `portability-findings.md`; the
   engine's cell is excluded from Linux timing for that track/workload; no local patch to
   templates, gates, corpus, or engines. The exclusion is carried into the inclusion manifest
   and the affected findings-validation statements are downgraded accordingly.
5. **Portability finding:** every step-4 exclusion is additionally reported in the report's
   part 2 prose as a portability finding — a gate that catches a real cross-OS output difference
   is the contract working (plan risk table, verbatim posture).
6. **Upstream defect path:** if the diagnosis shows a defect in a shipped phase 1–7 artifact
   (e.g. a normalization-pipeline bug that Windows never exercised), the remedy is an
   [amendments-ledger](../../common/cross-cutting-decisions.md#cross-spec-amendments-ledger)
   entry proposed to the owning phase with the Linux evidence attached; this report only
   records the finding.

## Measurement runs (D13)

Same shapes as each ecosystem spec — referenced, not restated. The full matrix, run inside the
D2 session state, in this order (Heddle anchors first because every ratio column needs them):

1. **Intra-.NET protocol suite** (Phase 1 metrics-protocol first-exercise shape): all eight
   suites, net10.0, Release, MemoryDiagnoser — produces the **Linux-side Heddle anchor rows**
   and the intra-.NET twin data (itself a finding source: .NET's own Windows-vs-Linux ranking
   stability).
2. **Rust** — phase 2 WI7/WI8 shapes: Criterion benches both tracks, `alloc_report` pass, cold
   pass.
3. **JVM** — phase 3 procedure: single JMH invocation, `-prof gc`, DCE plausibility pass.
4. **JS** — phase 4 shapes: stability procedure first (D9 above), then controlled, idiomatic,
   cold-compile runs via `run-js.sh`.
5. **Python** — phase 5 run protocol: selftest/gates, five pyperf scripts with
   `--affinity=<isolated pair>`, memory pass, `pyperf stats` dumps.
6. **Go** — phase 6 procedure: `run-benchmarks` steps replicated by `run-go.sh` (version
   asserts, templ regeneration freshness, vet, gates, prebuild, two timed invocations,
   benchstat).

Ecosystem order 2–6 is the program's priority order; an ecosystem absent from the shipped subset
is skipped and manifest-recorded. Every run's raw artifacts are the same artifact types its
ecosystem's Windows report committed, published under the one Linux date directory.

**Inclusion manifest (Q7.2 semantics, mirrored):** part 1 of the report opens with a manifest
table — one row per ecosystem (including intra-.NET): `included` / `absent (phase cut)` /
`blocked (reason + link)`, plus per-cell exclusions carried from Windows reports and any new
Linux exclusions from D12. If Phase 7 shipped, its frozen `sources.json` (Phase 7 spec D1/D9)
is the authoritative record of the shipped subset and Windows excluded cells, and this manifest
is seeded from it; otherwise the per-ecosystem published reports are enumerated directly. Validation statements over absent ecosystems are omitted; over
excluded cells, downgraded and marked.

## The report (D14–D17)

One new immutable directory `docs/benchmarks/<yyyy-MM-dd>/`. `index.md` follows the protocol's
publication format with two parts inside the standard section skeleton.

### Part 1 — protocol-format Linux publication (D14)

- **H1 + intro:** titled as the Linux cross-check, e.g.
  `# Benchmark run — <date> (Linux cross-check, Ubuntu 24.04)`. The intro carries, verbatim,
  the **framing statement** (opening paragraph, required):

  > *"This report is a Linux cross-check of the Heddle cross-stack benchmark program: the same
  > physical machine as every published Windows run, booted into Ubuntu 24.04 under Linux
  > CPU-isolation conditions. Its purpose is to test whether the program's published findings —
  > rankings, relative gaps, and dispersion — are robust to the measurement environment. It is
  > not a Windows-vs-Linux comparison, and no number here says anything about which operating
  > system is faster. The Windows runs remain the program's numbers of record; the numbers in
  > this report join no phase 1–7 comparison."*

  plus the reproduce block (environment setup + per-ecosystem run commands — README D18 paths).
- **`## Environment`** — the environment-and-toolchains D3 summary block: hardware identity
  (same box as the Windows runs, stated), Ubuntu 24.04.x + kernel, cmdline, mitigations summary,
  frequency policy (incl. the boost-off divergence disclosure), isolation set, per-engine
  toolchain identity table, all harness versions and the applied per-harness settings (D6–D11),
  and a link to `version-deltas.md`.
- **Inclusion manifest** (above).
- **`## The workloads`** — protocol shape; encoded confinement caveat verbatim beside encoded
  entries.
- **`## Results`** — per ecosystem, in run order, the same table shapes as its Windows report:
  both track tables (captions name the track), each with the labeled row
  `Heddle (reference — .NET 10, same machine/OS, from part 1 of this report)` sourced from the
  **Linux** intra-.NET run in this same directory (Q2.2 = A and Q6.2 applied *within* this
  report: the ratio column anchors to the Linux-side Heddle row; a Windows Heddle row is
  forbidden here — D17). All presentation rules 1–5 apply: harness-native unit + ns/render in
  Heddle-row tables; allocation/GC and cold-cost sidebars per-ecosystem only with the verbatim
  labels; non-Heddle engines never ranked across ecosystems; every ecosystem-specific verbatim
  text its Windows report format mandates (JS not-fair-fight framing, raw-track fairness
  statements, method notes) carried identically. Honest-reporting rules 1–6 apply in full.
- **`## Files`** — every committed artifact, per ecosystem, plus `environment/`,
  `version-deltas.md`, and (if present) `portability-findings.md`.

### Part 2 — findings validation (D15/D16 close the plan's flagged details)

A dedicated `## Findings validation — do the Windows findings hold under Linux?` section, after
Results. For **each shipped ecosystem × track** it publishes one validation table and prose,
citing the Windows source run by link + date, restating from it **only** ranks, ratio values,
and dimensionless dispersion values (never absolute times — D17).

**D15 — the dimensionless dispersion form (decision):** a per-cell **relative dispersion `d`**,
defined harness-natively — because every cross-OS dispersion comparison in this report is
same-harness (Windows Criterion vs Linux Criterion, etc.), the form must only be fixed *per
harness*, and harness-native definitions keep Q2.1's each-harness-stays-native credibility rather
than forcing one statistic onto all six. The closed mapping:

| Harness | `d` = |
|---|---|
| BenchmarkDotNet | `StdDev / Mean` (CV) |
| Criterion.rs | `(CI_upper − CI_lower) / (2 × mean)` (relative 95% CI half-width) |
| JMH | `Error / Score` (relative 99.9% CI half-width) |
| mitata | `(p99 − avg) / avg` — the relative upper-tail spread of mitata's own per-cell distribution, computed from the percentiles mitata emits (`avg`, `p99`) in **both** tracks' published tables. If a mitata build emits `p999` but not `p99`, the highest emitted percentile strictly below `max` is used and the substitution recorded. Uniform across the controlled **and** idiomatic tracks and available on both OSes, since every published mitata cell (both tracks) carries this spread. |
| pyperf | `std dev / mean` (CV) |
| benchstat | the reported `±%` / 100 |

Rejected alternatives: one uniform CV everywhere (would require re-deriving standard deviations
from raw samples for CI-reporting harnesses — a second analysis the program forbids for the
point estimates and should equally not perform for dispersion); and — for mitata specifically —
the five-run stability RSD as `d`. The RSD was rejected because it is published **only for the
controlled suite** (Phase 4's five-run stability procedure is a controlled-only harness stand-in;
the idiomatic and cold tracks share the same harness but ship no per-cell RSD), so it yields **no
`d` for the JS idiomatic track** on either OS — leaving the mandatory per-track validation table
(WI7) uncomputable for JS idiomatic, and no Windows idiomatic RSD exists to compare against even
if this phase re-ran the five-run procedure on both Linux tracks (the shipped Windows report is
immutable). It is additionally a **between-run** statistic while every other `d` here is a
**within-run** one — a deeper kind-mismatch than the coverage heterogeneity below. The chosen
`(p99 − avg)/avg` is within-run (matching the five other harnesses in kind), computable per cell
on both tracks and both OSes, and — because it uses the `p99` quantile rather than the absolute
`max` — is not the outlier-sensitive raw min…max spread the program rightly avoids; its upper-tail
bias makes it conservative (wider `d`), consistent with the posture below. The five-run stability
RSD keeps its Phase 4 role as the JS **publication-gating stability verdict** (D9); it is no longer
overloaded as the findings-validation dispersion. Note the `d` values are heterogeneous in coverage
(CV vs 95% vs 99.9% CI vs relative p99-spread) — they are therefore **never compared across
harnesses**, only (a) across OSes within one harness and (b) summed into the D16 combination
formulas — **both** the rank-resolution test (D16.1) and the gap-movement bound (D16.2), each of
which sums an ecosystem engine's native `d` with the Heddle anchor's BDN `d` — where heterogeneity
biases the threshold conservative (wider), which errs toward calling fewer things material, and the
everything-published rule (below) keeps that bias harmless.

**D16 — material-divergence thresholds (decision).** Three checkable definitions; `W` = Windows
source run, `L` = this run.

1. *Material rank change.* For each ecosystem × track × workload, the wall-time order of
   {ecosystem engines + Heddle anchor} is compared between OSes. The resolution test is stated in
   **ratio terms** so it is evaluable from the ratio-only inputs the data contract provides (D17.4;
   the Windows side has no absolute times). Let `r_X` be an engine's wall-time ratio to that OS's
   Heddle anchor (`r_Heddle = 1`): on Windows `r_X` is the published ratio transcribed into
   `windows-source.json`; on Linux `r_X` is part 1's ratio column. A pair (A, B) **flips
   materially** iff the order differs between OSes **and** on *each* OS the pair was resolved:
   `|r_A − r_B| > (d_A + d_B) × min(r_A, r_B)` on that OS's own ratios. (This is the dimensionless
   form of the wall-time inequality `|t_A − t_B| > (d_A + d_B)·min(t_A, t_B)`: that inequality is
   homogeneous of degree 1, so dividing through by the common per-OS Heddle anchor leaves it
   unchanged — no absolute Windows time is needed to evaluate it.) A flip where either OS leaves the
   pair unresolved is reported as "order not resolved within disclosed dispersion" (stated, but not
   a material divergence).
2. *Material gap movement.* For each engine cell, `r = engine wall time / Heddle anchor wall
   time` on the same OS (Windows `r_W` from the published report's ratio column; Linux `r_L`
   from part 1). Combined disclosed dispersion of the pair of ratios:
   `D = (d_engine,W + d_Heddle,W) + (d_engine,L + d_Heddle,L)` — first-order linear sums, both
   for engine-vs-anchor within an OS and across the two OSes. Linear sum, not quadrature,
   because the `d` statistics are heterogeneous in kind and cannot support the
   independence/equal-coverage assumptions quadrature encodes; the sum is the standard
   interval-arithmetic bound. The movement is **material** iff
   `|r_L / r_W − 1| > max(D, 0.05)`. The 0.05 floor exists so that ultra-tight dispersions do
   not flag noise-scale movement as a finding, and its value is not invented here: 5% is the
   program's own established stability line (Criterion re-run trigger, benchstat re-run
   trigger, mitata RSD `verified` threshold — phases 2/6/4).
3. *Material dispersion change.* Per cell, dispersion **changes character** iff
   `max(d_W, d_L) / min(d_W, d_L) ≥ 2` **and** `max(d_W, d_L) ≥ 0.01` (a doubling below 1%
   relative dispersion is not a character change). **Zero-dispersion guard:** when
   `min(d_W, d_L) = 0` the ratio is undefined; the cell is then a character change iff
   `max(d_W, d_L) ≥ 0.01` (an appear-from-nothing dispersion above the 1% floor is material; a
   zero-vs-sub-1% pair is not), and `validate.py` never divides by zero. The
   **pyperf/Python comparison against Phase 5's disclosed Windows stability posture is mandatory
   and explicit** — one paragraph stating the per-cell `d` movement and what it says about the
   honesty of Phase 5's disclosed posture, in this dimensionless form only, and noting that the
   Linux pyperf worker is pinned to the isolated SMT **pair** (both siblings of one physical core,
   the sibling isolated and idle) whereas the Windows run pinned a **single** logical CPU
   (`--affinity=4`) — an affinity-target delta disclosed alongside the comparison (D10) so a
   variance change is not read as a pure OS effect.

**Everything is published; "material" only controls prominence.** The validation tables print
`r_W`, `r_L`, movement %, `D`, `d_W`, `d_L`, and the verdict for **every** cell, so
borderline movements are visible regardless of the threshold; every *material* item is
additionally named in the section's prose with what it means for the affected published claim
(`held` / `narrowed` / `environment-sensitive`), as prominently as any confirmation. If nothing
is material, the section says so and states the validation's exact scope (ecosystems, tracks,
version deltas) — no universal-robustness claim.

**Phase 7 coupling (loose by design):** if the consolidated report shipped, its findings list is
enumerated and each finding marked against the same three definitions with citations; if it did
not ship, the per-ecosystem Windows reports' findings are validated directly. Nothing in this
section depends on Phase 7 spec internals — only on its published findings text.

**Tooling:** `validate.py` (README D18) computes every table from (a) this run's artifacts and
(b) `windows-source.json` — a hand-transcribed extraction of the Windows source runs'
**ranks, ratio-to-Heddle values (`r_W`, quoted from each report's own ratio column), and
dimensionless dispersions (`d`)** — **never absolute times, point estimates, or any wall-time
figure** (D17.4 makes this a structural, tool-enforced check). This is not a convenience
restriction: every part-2 verdict is computable from these three inputs alone —
the rank-resolution test evaluates in ratio form (D16.1), gap movement reads `r_W` directly and
sums `d` values (D16.2), and dispersion character reads `d` values (D16.3); no Windows
point estimate is ever required, so storing one would only re-open the absolute-time seam D17.4
exists to weld shut. Each transcribed value is annotated with its source file within the
published Windows directory. The transcription is spot-checked as a listed completion check;
the script's threshold logic is unit-tested with synthetic fixtures on both sides of every
threshold.

### D17 — No-cross-OS-absolute-time rules, as checkable criteria

Publication is blocked until all of the following pass (they are the plan's success criteria
made mechanical; the checklist result is recorded in the run log):

1. **No Windows absolute time in this report:** no table in the Linux directory contains a
   wall-time, allocation, or dispersion value in absolute units attributed to a Windows run;
   Windows source runs appear only as links/dates and as restated ranks, ratio values, and `d`
   values. Check: manual sweep of every table + a grep of `index.md` for each Windows report's
   headline numbers (transcribed into the checklist from the cited reports) finding zero hits.
2. **No Linux number outside this directory:** `git status`/diff shows no modification to any
   existing `docs/benchmarks/*` or `docs/plan/*` file; the phase adds exactly one new dated
   directory (plus this spec's own tooling under `benchmarks/linux-crosscheck/`).
3. **No absolute-unit dispersion crossing:** every cross-OS dispersion statement uses `d`
   (dimensionless); check: part 2 contains no time-unit token (`ns`, `µs`, `ms`, `s/op`)
   attached to a Windows-attributed figure.
4. **Anchor provenance:** every ratio in part 1 anchors to the Linux Heddle row; every `r_W`
   in part 2 is quoted from the Windows report's own ratio column, never recomputed from
   absolute times by this phase's tooling (structural: `windows-source.json` stores only
   ranks, ratios as published, and dimensionless dispersions `d` — never a point estimate or any
   absolute-time field; `validate.py` rejects an input carrying one).
5. **Framing present:** the verbatim framing statement is the report's first body paragraph;
   the numbers-of-record sentence appears in it.
6. **Label completeness:** allocation label, encoded confinement caveat, cold-cost label, and
   every ecosystem-mandated verbatim text present at their protocol positions.
