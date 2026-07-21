# Cross-stack benchmarks — reproduce book

One directory per ecosystem harness, plus two master runners (Windows and Linux) that run
every gate and every measurement with the parameters the phase specs make normative.

The specs are the source of truth; nothing in this file overrides them:

| Ecosystem | Harness | Normative spec |
|---|---|---|
| .NET (anchor) | `src/Heddle.Performance` (BenchmarkDotNet) | [phase 1 — metrics-protocol.md](../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md) |
| Rust | `benchmarks/rust` (Criterion) | [phase 2 — README.md](../docs/spec/cross-stack-benchmarks/phase-2-rust/README.md) (D9/WI10) |
| JVM | `benchmarks/jvm` (JMH) | [phase 3 — harness-and-jmh.md](../docs/spec/cross-stack-benchmarks/phase-3-jvm/harness-and-jmh.md) |
| JS | `benchmarks/js` (mitata) | [phase 4 — harness-and-run.md](../docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md) |
| Python | `benchmarks/python` (pyperf) | [phase 5 — harness.md](../docs/spec/cross-stack-benchmarks/phase-5-python/harness.md) |
| Go | `benchmarks/go` (testing + benchstat) | [phase 6 — harness-and-measurement.md](../docs/spec/cross-stack-benchmarks/phase-6-go/harness-and-measurement.md) |
| Linux cross-check | `benchmarks/linux-crosscheck` | [phase 8 — README.md](../docs/spec/cross-stack-benchmarks/phase-8-linux-crosscheck/README.md) |

## Prerequisites (toolchain pins)

| Toolchain | Pin | Notes |
|---|---|---|
| .NET SDK | the exact SDK of the Windows protocol run (observed line: 10.0.302) | suites target `net10.0`, `-c Release` |
| Rust | rustc/cargo 1.97.1 | `rust-toolchain.toml` in `benchmarks/rust` |
| JDK | Temurin 25 | `pom.xml` pins `maven.compiler.release=25`; on a JDK < 25 the runner passes `-Dmaven.compiler.release=23` |
| Node.js | v24.18.0 | `package.json` `engines`; on any other node the runner uses `npm ci --engine-strict=false` |
| CPython | 3.14.6 | harness venv at `benchmarks/python/.venv`; pyperf 2.10.0 + psutil 7.2.2 from `requirements.txt` |
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

Linux, from `benchmarks/linux-crosscheck/` (see [its README](linux-crosscheck/README.md) for
the full bare-metal order of operations — install, GRUB isolation entry, environment capture):

```bash
# Linux — FULL measurement (bare-metal protocol box only):
sudo ./tune.sh && ./run-all.sh; ./untune.sh

# Linux/WSL — functional pass (no tuned state required; bypass is recorded):
./run-all.sh --smoke --no-tune-check
```

### Windows runner options

| Option | Effect |
|---|---|
| `-Smoke` | short functional flags everywhere (see table below) |
| `-Ecosystem dotnet,rust,jvm,js,python,go` | subset (any combination; program order is preserved) |
| `-OutDir <path>` | artifact/log destination (default `benchmarks\out\windows-run-<timestamp>`, git-ignored; the path must not contain spaces) |
| `-JsStabilityRepeat` | opt in to the JS five-run stability procedure (Phase 4 D13) before the timed JS suites — it is publication-gating but a separate step, so it never auto-runs |

## What a run does

**Phase 1 — gates, all selected ecosystems, stop on first red** (same rule as
`linux-crosscheck/run-all.sh`: a red gate is triaged before anything later runs):

1. .NET: `parity` + `verify-corpus` (golden-corpus freshness + verifier calibration)
2. Rust: `cargo run --release --bin gate`
3. JVM: `mvnw -q clean verify` (gates wired into `verify`)
4. JS: `npm ci`, `npm run selftest`, `npm run gate`
5. Python: venv create/reuse + `pip install -r requirements.txt`, `python -m runner.selftest`, `python -m runner.gate_all`
6. Go: `go test ./suites` (TestMain gates before anything can time)

**Phase 2 — measurement**, in the anchor-first program order dotnet → rust → jvm → js →
python → go. The runner prints the machine-state rules (protocol box, quiet machine, AC/High
Performance power) before this phase; failures here are recorded and the run continues, with
a nonzero overall exit at the end.

| Ecosystem | Full mode (protocol shape) | Smoke mode |
|---|---|---|
| .NET | 8 suites, one `--filter *<Suite>*` run each, BDN defaults + MemoryDiagnoser | same, `--job Dry` |
| Rust | `cargo bench --bench controlled --bench idiomatic --bench cold -- --noplot`, then `alloc_report` (alloc-count feature), then `summarize` (non-fatal while the Phase 1 Heddle reference rows are pending) | `cargo bench … -- --test` |
| JVM | `java -jar target/benchmarks.jar -prof gc -rf json -rff <out>` — committed annotation regime (Fork 5, 5×10 s / 5×10 s), **~4.5–5.5 h** | `-f 1 -wi 1 -i 1 -w 1s -r 1s -foe true` |
| JS | `run.ps1` per bench script (High priority, `--expose-gc --allow-natives-syntax`); stability 5-repeat only with `-JsStabilityRepeat` | the three scripts once via `run.ps1` |
| Python | five pyperf Runner scripts, `--affinity=4 -o <out>\python\<name>.json`, elevated shell (warned if not), then the separate `mem_tracemalloc.py` pass | pyperf `--debug-single-value`; memory `--reps 5` |
| Go | `run-benchmarks.ps1` (version asserts, templ freshness, vet, gates, prebuild, High-priority timed runs `-test.count=20 -test.benchtime=1s`, benchstat) | `-Count 1 -BenchTime 100ms` |

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

Linux run outputs land under `benchmarks/linux-crosscheck/out/` (self-git-ignored).

## Running one harness alone (manual per-ecosystem commands)

All commands from the harness directory shown; gates first, always.

| Ecosystem | Directory | Gate | Measurement |
|---|---|---|---|
| .NET | `src/Heddle.Performance` | `dotnet run -c Release -f net10.0 -- parity` then `… -- verify-corpus` | `dotnet run -c Release -f net10.0 -- --filter *<Suite>*` (8 suites; `export-corpus` rewrites goldens — never run it casually) |
| Rust | `benchmarks/rust` | `cargo run --release --bin gate` | `cargo bench --bench controlled --bench idiomatic --bench cold -- --noplot`; `cargo run --release --features alloc-count --bin alloc_report`; `cargo run --release --bin summarize` |
| JVM | `benchmarks/jvm` | `.\mvnw.cmd -q clean verify` (add `-Dmaven.compiler.release=23` on a JDK < 25) | `java -jar target\benchmarks.jar -prof gc -rf json -rff jmh-result.json` |
| JS | `benchmarks/js` | `npm ci` + `npm run selftest` + `npm run gate` | `./run.ps1 bench/controlled.mjs` (also `idiomatic.mjs`, `cold-compile.mjs`); stability: `./run.ps1 bench/controlled.mjs -Repeat 5` |
| Python | `benchmarks/python` | `python -m runner.selftest` + `python -m runner.gate_all` (venv python) | `python bench_<name>.py --affinity=4 -o results\<name>.json` (×5, elevated shell); `python mem_tracemalloc.py -o results\memory.json` |
| Go | `benchmarks/go` | `go test ./suites` | `./run-benchmarks.ps1` |

## Publication

Assembling a published report is **manual**, per
[metrics-protocol.md](../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md):
copy the run's artifacts into `docs/benchmarks/<run-date>/` with the environment block,
the two-track tables, the allocation labels/caveats, and the honest-reporting rules — the
runners never write into `docs/`. The Linux cross-check report additionally goes through
`linux-crosscheck/validate.py` (part 2 tables/verdicts) and the WI8 assembly step described
in [linux-crosscheck/README.md](linux-crosscheck/README.md).
