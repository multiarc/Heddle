# Phase 8 — Linux cross-check tooling (reproduce book)

Tooling for the [Phase 8 — linux-crosscheck spec](../../docs/spec/cross-stack-benchmarks/phase-8-linux-crosscheck/README.md)
(supplements: [environment-and-toolchains.md](../../docs/spec/cross-stack-benchmarks/phase-8-linux-crosscheck/environment-and-toolchains.md),
[harness-settings-and-validation.md](../../docs/spec/cross-stack-benchmarks/phase-8-linux-crosscheck/harness-settings-and-validation.md)).

This directory is the reproduce path the published Linux cross-check report points at (spec D18).
It was authored **before any bare-metal Ubuntu session exists**, under two standing rulings:

- **SR-1 — no benchmark results this pass.** No Windows protocol run for phases 1–6 is published
  yet, so `windows-source.json` is *not* transcribed here (only its schema and structural checks
  ship — see below), and no Linux number is produced by this pass.
- **SR-2 — Linux validation happens on WSL2 (Ubuntu 24.04 default instance), not bare metal.**
  The scripts that only make sense on bare metal (`tune.sh`, `untune.sh`, the GRUB isolation
  entry) are committed and **designed to fail cleanly under WSL** with an explicit
  `bare-metal-only` message — that failure is their correct WSL behavior, not a defect.
  `capture-environment.sh` runs everywhere and records every bare-metal-only item as
  `unavailable under WSL (deferred to bare metal)`.

## What runs where

| Script | Bare-metal Ubuntu Server 24.04 (measurement box) | WSL2 Ubuntu 24.04 (functional posture, SR-2) |
|---|---|---|
| `setup-toolchains.sh` | installs the pinned toolchains (spec D4/D5) | installs identically (Ubuntu 24.04 userland) — functional validation only |
| `capture-environment.sh` | full D3 record (M1–M5, K1–K8, F1–F7, toolchain table, repo state) | completes; bare-metal-only items recorded as unavailable-under-WSL |
| `tune.sh` / `untune.sh` | enter/exit the one D2 session state | **designed clean failure** (`bare-metal-only`) |
| `grub-isolation.cfg.example` | template for the isolation boot entry | not applicable (no GRUB) |
| `run-*.sh` | tuned-state pre-flight + ecosystem commands | `--smoke --no-tune-check` functional runs only (bypass recorded in output) |
| `validate.py` / `test_validate.py` | part 2 verdicts | fully OS-independent; runs anywhere (stdlib-only Python ≥ 3.10) |

## Deferred to bare metal (SR-2 list — deferred, not faked)

None of the following was executed or simulated in this pass; each is a bare-metal work item on
the Q1.6 protocol box (Ryzen 9 9950X) after the D1 sequencing gate opens:

1. **D1 install** — Ubuntu Server 24.04.x dual-boot (disjoint partition/disk, HWE kernel
   `linux-generic-hwe-24.04`, firmware untouched, Windows-preservation checks, quiesce list).
2. **GRUB isolation entry** — instantiating `grub-isolation.cfg.example` with the **actual**
   SMT sibling pair read from `/sys/devices/system/cpu/cpu4/topology/thread_siblings_list`
   on the installed system (never an assumed enumeration).
3. **pyperf system tune** — `tune.sh` steps 2–4 of D2 (machine-wide tune, `amd-pstate` boost
   off, `pyperf system show` capture).
4. **Firmware capture** — `dmidecode` items M1/M3 and the M4 firmware-invariance attestation.
5. **tune/untune post-condition asserts** — governor/boost/sample-rate/isolated-set read-backs
   passing (on WSL they fail by design), and `untune.sh` + `pyperf system show` restore proof.

Also deferred under SR-1: the transcription of `windows-source.json` (no published Windows
protocol reports exist to transcribe), the D12 gate sweep as a publication gate, all D13
measurement runs, and the report itself.

## Order of operations (bare-metal session)

1. `PRECONDITION` line in the run log: all Windows protocol runs published; shipped subset listed
   (D1 sequencing gate — hard stop until true).
2. D1 install + `grub-isolation.cfg.example` instantiated; Windows-preservation checks recorded.
3. `./setup-toolchains.sh` — pinned toolchains; emits the `version-deltas.md` draft.
4. Boot the "Ubuntu — benchmark isolation" GRUB entry.
5. `sudo ./tune.sh` — enters the D2 state, asserts post-conditions, records
   `pyperf system show` to `out/session-state.txt` (the pre-flight baseline).
6. `./capture-environment.sh` — full D3 closed checklist into `out/environment/`.
7. Gate sweep (D12): each ecosystem's own gate commands, red-first, triage on failure.
8. `./run-all.sh` — anchor-first order (dotnet → rust → jvm → js → python → go),
   stop-on-red-gate; each launcher asserts the tuned state before timing.
9. `python3 validate.py --windows-source windows-source.json --linux-results <part1 data>` —
   part 2 tables and verdicts.
10. `sudo ./untune.sh`; reboot to the default entry; assemble the `docs/benchmarks/<date>/`
    report; run the D17 checklist.

## WSL functional posture (what this pass validated, SR-2)

- `bash -n` on every shell script (syntax).
- `capture-environment.sh` dry-run completes under WSL, recording each unavailable item.
- `tune.sh` fails with its designed `bare-metal-only` message under WSL.
- `python3 test_validate.py` green (all D15/D16/D17.4 threshold-straddling fixtures).
- `setup-toolchains.sh --dry-run` prints its plan without installing (installs are the next
  work item).

## Files

| File | Role |
|---|---|
| `README.md` | this reproduce book |
| `grub-isolation.cfg.example` | isolation boot-entry template (D1.6) |
| `tune.sh` / `untune.sh` | D2 session-state enter/exit with post-condition asserts |
| `capture-environment.sh` | D3 closed environment checklist capture |
| `setup-toolchains.sh` | D4/D5 pinned toolchain installs + `version-deltas.md` draft |
| `run-dotnet.sh` … `run-go.sh`, `run-all.sh` | per-harness launchers (D6–D11), tuned-state pre-flight, `--smoke` |
| `windows-source.schema.json` | schema for the (SR-1-deferred) `windows-source.json` transcription; D17.4 structural rules |
| `validate.py` / `test_validate.py` | D15 d-forms, D16 thresholds, D17 structural checks + fixtures |
| `common.sh` | shared helpers (WSL detection, tuned-state pre-flight, output paths) |

Script outputs land under `benchmarks/linux-crosscheck/out/` (git-ignored via `out/.gitignore`;
publication copies artifacts into `docs/benchmarks/<date>/` at WI8, never the other way).

## Parameterizations recorded (SR-1/SR-2/SR-3)

- **.NET SDK version** (`setup-toolchains.sh`): the spec pins "the exact Windows-run SDK
  version", but under SR-1 no Windows protocol report is published yet. The script is
  parameterized (`DOTNET_SDK_VERSION`, default `10.0.302` — the current 10.x line observed on
  the Windows machine). **SR-1 note:** before the bare-metal run, this default MUST be replaced
  with the value from the published Windows protocol report's environment block.
- **Temurin fallback** (`--allow-jdk-fallback`, SR-3 mirror): if no Temurin 25 GA build is
  downloadable, the flag permits a Temurin/JDK 23 fallback; use is recorded as a
  `version-deltas.md` row with a caveat obligation (D4.3). Default: hard-fail on pin
  unavailability.
- **CPython fallback** (`--allow-python-fallback`, SR-3): primary path is deadsnakes
  `python3.14`; documented fallback is a source build of 3.14.6 (`--enable-optimizations
  --with-lto`, D5). Under SR-3 a deadsnakes `python3.13` fallback is additionally acceptable
  behind this flag; use is recorded as a delta row. Default: hard-fail.
- **Tuned-state pre-flight bypass** (`--no-tune-check`, SR-2): launchers implement the D2/WI5
  `pyperf system show` pre-flight; under WSL it cannot pass, so the bypass flag exists for
  functional (`--smoke`) runs only. Every bypass is printed and written into the launcher's
  output header — a bare-metal measurement run must never use it.
