# Cross-stack benchmarks — reproduce book

One directory per ecosystem harness, plus two master runners (Windows and Linux) that run
every gate and every measurement with the parameters the phase specs make normative.

The specs are the source of truth; nothing in this file overrides them:

| Ecosystem | Harness | Normative spec |
|---|---|---|
| .NET (anchor) | `benchmarks/dotnet` (BenchmarkDotNet) | [phase 1 — metrics-protocol.md](../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md) |
| Rust | `benchmarks/rust` (Criterion) | [phase 2 — README.md](../docs/spec/cross-stack-benchmarks/phase-2-rust/README.md) (D9/WI10) |
| JVM | `benchmarks/jvm` (JMH) | [phase 3 — harness-and-jmh.md](../docs/spec/cross-stack-benchmarks/phase-3-jvm/harness-and-jmh.md) |
| JS | `benchmarks/js` (mitata) | [phase 4 — harness-and-run.md](../docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md) |
| Python | `benchmarks/python` (pyperf) | [phase 5 — harness.md](../docs/spec/cross-stack-benchmarks/phase-5-python/harness.md) |
| Go | `benchmarks/go` (testing + benchstat) | [phase 6 — harness-and-measurement.md](../docs/spec/cross-stack-benchmarks/phase-6-go/harness-and-measurement.md) |
| Linux cross-check | `benchmarks/linux-crosscheck` | [phase 8 — README.md](../docs/spec/cross-stack-benchmarks/phase-8-linux-crosscheck/README.md) |

## Prerequisites (toolchain pins)

| Toolchain | Pin | Notes |
|---|---|---|
| .NET SDK | **TBD** — pinned to the SDK of the Windows protocol run, which is pending re-test. The published 2026-07-25 Linux run used SDK 10.0.110 / runtime .NET 10.0.10 | suites target `net10.0`, `-c Release` |
| Rust | rustc/cargo 1.97.1 | `rust-toolchain.toml` in `benchmarks/rust` |
| JDK | Temurin 25 | `pom.xml` pins `maven.compiler.release=25`; on a JDK < 25 the runner passes `-Dmaven.compiler.release=23` |
| Node.js | v24.x (major pin, ledger E18; was the exact v24.18.0) | `package.json` `engines: 24.x`; on a non-24 node the runner uses `npm ci --engine-strict=false` and records the delta |
| CPython | 3.14.x (minor pin, ledger E19; was the exact 3.14.6) | harness venv at `benchmarks/python/.venv`; pyperf 2.10.0 + psutil 7.2.2 from `requirements.txt` |
| Go | go1.26.x (1.26.5 asserted by `run-benchmarks.ps1`) | templ CLI v0.3.1020 via `go tool templ` |

**Version deltas (SR-3 posture):** the Windows master runner *warns* on any delta from a pin
and continues; a *measurement* run on a delta'd toolchain must record the delta in the run
report's environment block (the Go harness is stricter: `run-benchmarks.ps1` hard-fails its
own version asserts). The Linux side records deltas in `version-deltas.md`
(`setup-toolchains.sh`).

## The commands

Run from the **repo root**:

```powershell
# Windows — FULL measurement (protocol machine, quiet, AC power, elevated shell for pyperf):
powershell -ExecutionPolicy Bypass -File benchmarks\run-all.ps1

# Windows — SMOKE (short functional pass, ~10–20 min; results carry NO measurement validity):
powershell -ExecutionPolicy Bypass -File benchmarks\run-all.ps1 -Smoke
```

```bash
# Linux (bare metal) — FULL measurement, one command (tunes, measures, untunes).
# Needs the "Ubuntu — benchmark isolation" boot entry (see preconditions below):
./benchmarks/run-all.sh --tune

# Linux (bare metal) — same, without that boot entry: performance governors + boost off
# are set here and restored on exit; the missing CPU isolation is a recorded delta:
./benchmarks/run-all.sh --tune-no-isolation

# Linux (bare metal) — FULL measurement on an already-tuned machine:
./benchmarks/run-all.sh

# Linux — SMOKE (short functional pass; no tuned state required, no measurement validity):
./benchmarks/run-all.sh --smoke
```

**Two Linux runners, different jobs.** `benchmarks/run-all.sh` is the twin of the Windows runner
(per-step logs, summary table, `--ecosystem` subsets, and `taskset` + `nice -n -20` standing in
for the Windows High priority classes). `benchmarks/linux-crosscheck/` is the Phase 8 cross-check
protocol — D2 no-priority posture, tuned-state pre-flight against a recorded session state,
`validate.py` part-2 tables. **Anything published as a Linux cross-check goes through
`linux-crosscheck/`**; see [its README](linux-crosscheck/README.md) for the full bare-metal order
of operations (install, GRUB isolation entry, environment capture):

```bash
# Phase 8 cross-check, from benchmarks/linux-crosscheck/ — FULL measurement:
sudo ./tune.sh && ./run-all.sh; ./untune.sh

# Phase 8 cross-check, Linux/WSL — functional pass (no tuned state required; bypass is recorded):
./run-all.sh --smoke --no-tune-check
```

### Windows runner options

| Option | Effect |
|---|---|
| `-Smoke` | short functional flags everywhere (see table below) |
| `-Ecosystem dotnet,rust,jvm,js,python,go` | subset (any combination; program order is preserved) |
| `-OutDir <path>` | artifact/log destination (default `benchmarks\out\windows-run-<timestamp>`, git-ignored; the path must not contain spaces) |
| `-Budget short\|baseline` | measurement budget **per engine**: `short` ~10 min each (default), `baseline` ~30 min each. A leg's total is its engine count times that, so the .NET leg is about 3x the others. Named `-Budget` because `$PROFILE` is a PowerShell automatic variable |
| `-JsPasses <n>` | JS repeat passes per render track (default: the budget's — 18 short / 54 baseline; minimum 5, D13's verdict floor) |

### Linux runner options

| Option | Effect |
|---|---|
| `--smoke` | short functional flags everywhere (same shapes as `-Smoke`); also skips the bare-metal pre-flight, recorded in the log |
| `--ecosystem dotnet,rust,jvm,js,python,go` | subset (any combination; program order is preserved) |
| `--out-dir <path>` | artifact/log destination (default `benchmarks/out/linux-run-<timestamp>`, git-ignored) |
| `--budget short\|baseline` | measurement budget per engine, exactly as `-Budget` |
| `--js-passes <n>` | JS repeat passes per render track, exactly as `-JsPasses` |
| `--tune` | run the committed `linux-crosscheck/tune.sh` under `sudo` before the run and `untune.sh` from an `EXIT` trap afterwards (so an aborted run still restores the machine). Those two scripts are invoked unmodified. Requires the isolation boot entry — `tune.sh` refuses without it |
| `--tune-no-isolation` | tune what needs no reboot: `performance` governors, `cpufreq/boost=0`, `kernel.perf_event_max_sample_rate=1`. Every prior value is captured to `logs/tune-prior-state.txt` first and restored from an `EXIT` trap. The absent isolated SMT pair is recorded, the pre-flight accepts it, timed runs are not pinned, and pyperf falls back to `--affinity=4`. Mutually exclusive with `--tune` |
| `--no-tune-check` | record a bypass of the bare-metal pre-flight (and of the WSL refusal) and measure anyway — the numbers are not publishable |
| `--no-priority` | launch the JS and Go timed runs plain, matching the Phase 8 D2 posture |

### Bare-metal preconditions (Linux, full measurement)

Without `--smoke` the runner refuses to measure under WSL and pre-flights the machine state,
aborting with instructions unless all of the following hold:

* the *Ubuntu — benchmark isolation* GRUB entry is booted, so `/sys/devices/system/cpu/isolated`
  equals the SMT sibling pair of the isolation core (read from the topology, never assumed).
  Instantiate it from [linux-crosscheck/grub-isolation.cfg.example](linux-crosscheck/grub-isolation.cfg.example)
  — read the pair with `cat /sys/devices/system/cpu/cpu4/topology/thread_siblings_list`, put
  `isolcpus=<PAIR> nohz_full=<PAIR> rcu_nocbs=<PAIR>` on a **dedicated** entry (never on
  `GRUB_CMDLINE_LINUX_DEFAULT`), `sudo update-grub`, reboot into it. Without this entry
  `--tune` refuses by design; use `--tune-no-isolation` to measure anyway with the delta recorded;
* every cpufreq policy reads `performance` and `/sys/devices/system/cpu/cpufreq/boost` reads `0`
  — this is what `--tune` (i.e. `linux-crosscheck/tune.sh`) or `--tune-no-isolation` establishes;
* quiet machine: no other builds, watchers, indexing editors or browsers for the duration;
* root or passwordless `sudo`, so `nice -n -20` and pyperf's priority elevation actually apply
  (missing privileges are recorded, not fatal — the run continues at default priority).

## What a run does

**Phase 1 — gates, all selected ecosystems, stop on first red** (same rule as
`linux-crosscheck/run-all.sh`: a red gate is triaged before anything later runs):

1. .NET: `gate` (every registered cell), `selftest` (the gate's own checks, including the
   six-technique differential), `verify-corpus` (corpus freshness + verifier calibration)
2. Rust: `cargo run --release --bin gate`
3. JVM: `mvnw -q clean verify` (gates wired into `verify`)
4. JS: `npm ci`, `npm run selftest`, `npm run gate`
5. Python: venv create/reuse + `pip install -r requirements.txt`, `python -m runner.selftest`, `python -m runner.gate_all`
6. Go: `go test ./suites` (TestMain gates before anything can time)

**Phase 2 — measurement**, in the anchor-first program order dotnet → rust → jvm → js →
python → go. The runner prints the machine-state rules (protocol box, quiet machine, power/tuned
state) before this phase; failures here are recorded and the run continues, with a nonzero
overall exit at the end.

| Ecosystem | Full mode (protocol shape) | Smoke mode |
|---|---|---|
| .NET | `bench-crossstack`, 8 suites, one `--filter *<Suite>*` run each, both fairness tracks, ShortRun + MemoryDiagnoser; then the `bench-techniques`, `bench-cold` and `bench-internal` sidebars | same, `--job Dry` |
| Rust | `cargo bench --bench controlled --bench idiomatic --bench cold -- --noplot`, then `alloc_report` (alloc-count feature), then `summarize` (non-fatal while the Phase 1 Heddle reference rows are pending) | `cargo bench … -- --test` |
| JVM | `java -jar target/benchmarks.jar -prof gc -rf json -rff <out>` — committed annotation regime (Fork 3, 1×2 s / 3×1 s), **~9 min**; `baseline` adds `-wi 2 -i 9` | `-f 1 -wi 1 -i 1 -w 1s -r 1s -foe true` |
| JS | `run.ps1` / `run.sh` per bench script (`--expose-gc --allow-natives-syntax`, priority posture per platform); the two render tracks run `-Repeat <passes>` and are aggregated by `bench/aggregate.mjs`, which emits the D13 `STABILITY:` verdict every run | the three scripts once via the launcher |
| Python | five pyperf Runner scripts, `--affinity=<4 on Windows, isolated pair on Linux> -o <out>/python/<name>.json`, elevated shell / root (warned if not), then the separate `mem_tracemalloc.py` pass | pyperf `--debug-single-value`; memory `--reps 5` |
| Go | `run-benchmarks.ps1` / `run-benchmarks.sh` (version asserts, templ freshness, vet, gates, prebuild, timed runs `-test.count=20 -test.benchtime=1s`, benchstat) | `-Count 1 -BenchTime 100ms` / `--count 1 --benchtime 100ms` |

### Measurement budget — `--budget short` (default) / `baseline`

Each harness sets its own run lengths, so before
[ledger E6](../docs/spec/records.md#cross-spec-amendments-ledger) "one cell" cost wildly
different amounts of wall clock per ecosystem. These are **measured** figures from the
withdrawn 2026-07-22 Windows run's `summary.txt`, not estimates. That run's *render figures* are
invalid and its report has been removed pending a Windows re-test (**TBD**), but its per-step
wall-clock durations are simply how long each harness took, which is what this comparison needs:

| Ecosystem | Cells | Was | s/cell | The problem |
|---|---:|---:|---:|---|
| JS | 32 | 31 s | **1.0** | 15–32× *below* every other leg |
| Rust | 33 | 8.3 min | 15 | in band |
| Python | 32 | 14.3 min | 17 | in band |
| Go | 32 | 13.8 min | 26 | in band |
| .NET | 41 | 21.5 min | 32 | in band |
| JVM | 32 | **4.47 h** | **503** | 16–34× above the band; **83% of the whole session** |

Neither extreme was chosen — both are what the harness happened to default to. E6 replaced that
with one selectable budget; [E13](../docs/spec/records.md#cross-spec-amendments-ledger) then changed
the unit it is measured in from **one ecosystem** to **one engine**:

```bash
./benchmarks/run-all.sh                    # --budget short    ~10 min per engine  (default)
./benchmarks/run-all.sh --budget baseline   # ~30 min per engine: ~3x the capture samples
```
```powershell
.\benchmarks\run-all.ps1 -Budget baseline   # same on Windows (-Budget, not -Profile: $PROFILE is reserved)
```

Every harness's **committed source/script default is the `short` shape**, so a bare invocation of
any single harness is already the budgeted regime; `baseline` layers CLI overrides on top. Nothing
changes about *what* is measured — only how many times.

**The unit is one engine (E13, resized program-wide by E14).** An *engine* is **16 cells** — eight
workloads × two fairness tracks — and every engine in the program gets **~10 min at `short` and
~30 min at `baseline`**, whichever ecosystem it lives in. Five legs carry two engines each; .NET
carries **six** (Heddle, Fluid, Scriban, DotLiquid, Handlebars.Net, Razor), so its leg is about three
times the others *by construction*. Budgeting the leg instead would have given each .NET engine a
third of the sampling every other ecosystem's engines get, and the cross-engine comparison this
program exists to make would rest on its worst-sampled rows.

| Ecosystem | Knob | `short` (committed default) | `baseline` override | short | baseline |
|---|---|---|---|---:|---:|
| .NET | BenchmarkDotNet job | ShortRun + **`LaunchCount 3`** | `--launchCount 10` | ~93 min† | ~284 min† |
| Rust | criterion | warm-up 5 s, measure 26 s, 100 samples | `--warm-up-time 10 --measurement-time 84` | ~20 min | ~60 min |
| JVM | JMH annotations | `@Fork(5)`, `@Warmup(1×2s)`, `@Measurement(5×1s)` | `-f 5 -wi 2 -i 17` | ~20 min | ~60 min |
| JS | aggregated passes | 38 passes per render track | 114 passes | ~20 min | ~61 min |
| Python | pyperf | render 20 processes × 6 values × 1 warmup; cold-compile `--processes 7` | `--values 20 --warmups 2`; cold-compile at default 20 | ~19 min | ~61 min |
| Go | `go test -count` | `28` | `--count 84` | ~19 min | ~58 min |

**Every figure in that table is projected**, and deliberately so: they are the E6-measured durations
scaled by the knob change, not fresh measurements. Only the .NET per-cell cost was measured directly
for this resize (below). Replace the whole column with real durations after the first protocol run.

Session totals at these settings: **~3.2 h short, ~9.7 h baseline** — against ~1 h and ~2.5 h when
the budget was per *ecosystem*, and 5.45 h before E6. That is the price of sampling seventeen
engines' worth of comparison rows equally instead of sampling six of them a third as well as the
rest.

† **.NET, and why its figures are the shape they are.** The leg is **151 cells**: 96 cross-stack
(8 workloads × 6 engines × 2 tracks) plus the techniques, cold and internal sidebars. Only the 96
cross-stack cells carry an engine's comparison rows, so those are what the per-engine budget sizes:
**~9.9 min short / ~30.1 min baseline** for each engine's 16 cells.

.NET spends its budget on **launches**, and only on launches. Two reasons. The practical one: the leg
is overhead-bound (one process per benchmark case, plus JIT and `[MemoryDiagnoser]`), so wall clock
is very nearly linear in `LaunchCount`, which makes the knob predictable — measured here on one
12-cell suite, **139 s at 1 launch, 316 s at 2, 704 s at 5**, about 10.8 s per cell per additional
launch. Raising the iteration counts instead runs through BenchmarkDotNet's pilot stage and does
not: a trial at `--launchCount 4 --warmupCount 5 --iterationCount 8` overshot its projection by more
than 2× and was abandoned.

The substantive reason: `LaunchCount 1` samples the **within-process term only** — one process, one
JIT, one heap layout — and never samples the cross-process term at all. E6 established that this is
the term worth paying for, keeping JMH's forks plural after measuring fork-to-fork RSD at 1.18%
median against a within-fork 0.18%, and re-spending the whole JS budget on independent processes for
exactly this reason. The .NET leg was the one that had never bought it.

The default job lives in the harness's configuration rather than in a `[SimpleJob]` attribute, for
the same reason `--job Dry` now means what it says: an attribute job cannot be *replaced* from the
command line, only added to, so a smoke pass used to run the full measurement as well as the dry
one.

`LaunchCount 3` puts an engine's 16 cells at ~9.9 min and `LaunchCount 10` at ~30.1 min. The leg
totals in the table are larger because they also carry the 55 Heddle-only sidebar cells, which ride
at the same job shape but belong to no engine's comparison share.

**JS is a different mechanism, and deliberately so.** mitata exposes no per-cell time budget:
`B.run()` builds its own options object and `run()` forwards only `throw`, so the 642 ms
`min_cpu_time` is unreachable without forking the library. JS therefore spends its budget on
**independent processes** — `run.sh --repeat N`, then `bench/aggregate.mjs` publishes the median
of the per-pass `avg` with the cross-pass `min … max` as dispersion. That buys the
*cross-process* term a single-process harness never samples, which is the same thing JMH gets
from forks and pyperf from its 20 workers. It also makes the Phase 4 D13 stability verdict
automatic instead of opt-in: every run writes `stability-summary-<track>.md` with a
`STABILITY:` line and exits non-zero on `failed`. Measured here: 18 passes of the controlled
track in 279 s → `STABILITY: verified`, cross-pass RSD median 1.81% / max 2.85%. The
withdrawn 2026-07-22 run shipped JS numbers with no verdict at all because the old opt-in switch was
simply never passed.

**Priority posture (Linux twins).** Windows sets a High process priority class for the JS and Go
timed runs (`start /high`, `PriorityClass = High`); `run-all.sh`, `js/run.sh` and
`go/run-benchmarks.sh` approximate that with `taskset -c <isolated SMT pair>` + `nice -n -20`.
The Phase 8 launchers under `linux-crosscheck/` deliberately do **not** (D2: the tune plus CPU
isolation *is* the isolation), so numbers from the twins are not drop-in comparable with the
published cross-check runs — record the delta in the run report's environment block, or pass
`--no-priority` to match D2.

## Where artifacts land

Everything from one Windows run lands under the run's `-OutDir`
(default `benchmarks\out\windows-run-<yyyyMMdd-HHmmss>`, git-ignored):

```
<OutDir>\
  logs\<eco>-<step>.log     every step's full output (also echoed live)
  logs\preamble.log         toolchain versions + pin-delta warnings
  summary.txt               the per-step exit-code table
  dotnet\                   copy of BenchmarkDotNet.Artifacts (results\*.md/csv/json + logs)
  rust\criterion\           copy of target\criterion (estimates.json per benchmark)
  jvm\jmh-result.json       JMH JSON (written there directly via -rff)
  js\                       copy of benchmarks\js\artifacts (*.txt captures + *.json)
  python\<name>.json        pyperf JSONs + memory.json (written there directly via -o)
  go\                       copy of benchmarks\go\results (bench + benchstat outputs)
```

A Linux run's outputs land under `--out-dir` in exactly the same shape
(default `benchmarks/out/linux-run-<yyyyMMdd-HHmmss>`, git-ignored), with `logs/tune.log` added
when `--tune` is used. The Phase 8 cross-check tooling writes elsewhere:
`benchmarks/linux-crosscheck/out/` (self-git-ignored).

## Running one harness alone (manual per-ecosystem commands)

All commands from the harness directory shown; gates first, always. Where the launcher differs
per platform, the Windows form is given first and the Linux twin second.

| Ecosystem | Directory | Gate | Measurement |
|---|---|---|---|
| .NET | `benchmarks/dotnet` | `dotnet run -c Release -- gate`, then `… -- selftest`, then `… -- verify-corpus` | `dotnet run -c Release -- bench-crossstack --filter *<Suite>*` (8 suites), then `bench-techniques`, `bench-cold`, `bench-internal` (`export-corpus` rewrites the goldens — never run it casually) |
| Rust | `benchmarks/rust` | `cargo run --release --bin gate` | `cargo bench --bench controlled --bench idiomatic --bench cold -- --noplot`; `cargo run --release --features alloc-count --bin alloc_report`; `cargo run --release --bin summarize` |
| JVM | `benchmarks/jvm` | `.\mvnw.cmd -q clean verify` / `./mvnw -q clean verify` (add `-Dmaven.compiler.release=23` on a JDK < 25) | `java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json` |
| JS | `benchmarks/js` | `npm ci` + `npm run selftest` + `npm run gate` | `./run.ps1 bench/controlled.mjs` / `./run.sh bench/controlled.mjs` (also `idiomatic.mjs`, `cold-compile.mjs`); stability: `-Repeat 5` / `--repeat 5` |
| Python | `benchmarks/python` | `python -m runner.selftest` + `python -m runner.gate_all` (venv python: `.venv\Scripts\python.exe` / `.venv/bin/python`) | `python bench_<name>.py --affinity=4 -o results/<name>.json` (×5, elevated shell / root); `python mem_tracemalloc.py -o results/memory.json` |
| Go | `benchmarks/go` | `go test ./suites` | `./run-benchmarks.ps1` / `./run-benchmarks.sh` |

## Publication

Assembling a published report is **manual**, per
[metrics-protocol.md](../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md):
copy the run's artifacts into `docs/benchmarks/<run-date>/` with the environment block,
the two-track tables, the allocation labels/caveats, and the honest-reporting rules — the
runners never write into `docs/`. The Linux cross-check report additionally goes through
`linux-crosscheck/validate.py` (part 2 tables/verdicts) and the WI8 assembly step described
in [linux-crosscheck/README.md](linux-crosscheck/README.md).

**The tables themselves are generated, not transcribed.** Once the artifacts are in place,
`report/consolidate.py` reads every harness's native output — BenchmarkDotNet CSV, Criterion
`estimates.json`, JMH JSON, mitata JSON, pyperf JSON, benchstat text — and writes
`consolidated-tables.md` into the run directory:

```bash
python benchmarks/report/consolidate.py docs/benchmarks/<run-date>
python benchmarks/report/consolidate.py --check docs/benchmarks/<run-date>
```

`--check` re-derives the tables and diffs them against the committed file, so a published
report stays byte-reproducible from its artifacts and a stale table is a failing command rather
than a silent error. The report's `index.md` is still hand-written — the narrative, the
environment block, the findings and the caveat register are judgement, not extraction — and it
links the generated tables rather than restating their numbers. Note this keeps the
runners-never-write-into-`docs/` invariant intact: `consolidate.py` is run by hand after a run,
never by `run-all.ps1`/`run-all.sh`.

Worked example: docs/benchmarks/2026-07-25 is the first
six-ecosystem report published this way.
