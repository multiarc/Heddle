# Linux environment establishment, environment record, and toolchain pinning

Supplementary document of the [Phase 8 — linux-crosscheck spec](README.md). It pins the dual-boot
establishment rules (what is specified vs what is recorded), the closed environment-record
checklist the plan flagged (firmware / CPU-mitigation / frequency-policy items and the per-engine
toolchain/runtime/libc/allocator identity list), the one-machine-state rule for the Linux session,
and the version-pinning-and-delta procedure against each Windows source run. Decisions of record:
README [D1](README.md#d1--dual-boot-establishment-bare-metal-ubuntu-server-2404-firmware-untouched-sequencing-gate)–[D5](README.md#d5--cpython-on-linux-deadsnakes-314-build-provenance-recorded).

## Dual-boot establishment (D1)

### Sequencing gate (hard precondition)

No step in this document executes until **every Windows run the program will publish is
published** — concretely: the shipped subset of phases 1–7 each has its `docs/benchmarks/<date>/`
directory merged, and the maintainer has confirmed no further Windows protocol run is planned.
The check is recorded as the first line of the run log:
`PRECONDITION: all Windows protocol runs published as of <date>; shipped subset = <list>`.

### What is specified (rules, binding)

1. **Distribution and image.** Ubuntu **Server** 24.04 LTS, the **latest 24.04.x point release**
   available at establishment time, minimal server install, no desktop environment. Rationale in
   README D1: a desktop session adds compositor/indexing/update services that are noise sources,
   and nothing in the run needs a GUI; the divergence from the Windows boot (which has a GUI) is
   a property of the deliberately-changed variable — Linux isolation conditions — and is
   disclosed in the report's environment notes.
2. **Kernel.** Install `linux-generic-hwe-24.04` and boot the HWE kernel. The 24.04 GA kernel
   (6.8, April 2024) predates the Ryzen 9 9950X (Zen 5, August 2024); the HWE line is Canonical's
   supported LTS path for newer silicon and carries the mature `amd-pstate` support the
   frequency-policy record depends on. The exact `uname -r` is recorded, never assumed.
3. **Placement.** Ubuntu installs to a partition/disk that does **not** overlap any partition the
   Windows environment uses. Preferred: a separate physical disk with its own ESP; acceptable: a
   new partition on free space with GRUB on the shared ESP. Forbidden: shrinking or rewriting the
   Windows system partition, converting disks, or letting the installer "erase disk".
4. **Firmware invariance (the "hardware held constant" guarantee).** No UEFI/BIOS setting is
   changed from the configuration the Windows protocol runs used — SMT stays enabled, memory
   profile (EXPO/XMP) unchanged, no curve-optimizer/PBO change, no Secure Boot toggle beyond what
   the installer strictly requires (if Secure Boot state must change to boot Ubuntu, the change
   and its Windows-boot verification are recorded as a known-divergent firmware item and restored
   consideration is noted; the default expectation is no change). The environment record captures
   the firmware identity (below) so the claim is evidence, not assertion.
5. **Windows preservation check.** After the Ubuntu install and again after the GRUB isolation
   entry is added, Windows is booted once and confirmed to start normally; the check is recorded
   in the run log. The Windows environment remains available for any future protocol-conformant
   run (Q1.6 stands; plan back-compat section).
6. **Boot-parameter configuration.** One additional GRUB entry ("Ubuntu — benchmark isolation")
   carries the isolation parameters of README D2:
   `isolcpus=<pair> nohz_full=<pair> rcu_nocbs=<pair>`, where `<pair>` is the SMT
   thread-sibling pair of one physical core on CCD0 — chosen as CPU 4's physical core, i.e. the
   contents of `/sys/devices/system/cpu/cpu4/topology/thread_siblings_list` *(verify at
   implementation: read the sibling list on the installed system and record the actual pair; do
   not assume an enumeration scheme)*. The kernel parameter names and the isolated-CPU detection
   they enable are pyperf's documented recommendation
   ([pyperf system docs](https://pyperf.readthedocs.io/en/latest/system.html)). The full
   `/proc/cmdline` of the measurement boot is recorded verbatim.
7. **Quiesce list.** Before any measurement: `unattended-upgrades`, `apt-daily*` timers,
   `fwupd-refresh.timer`, and `motd-news.timer` are stopped/masked for the session;
   `irqbalance` is left to `pyperf system tune` (which stops it — documented operation). The
   resulting `systemctl list-units --type=service --state=running` output is part of the
   environment record; no further service curation is specified — what runs is recorded, not
   hand-tuned beyond this list.

### What is recorded (facts, not rules)

Partition layout chosen, ESP arrangement, installer point-release and ISO checksum, install date,
the exact GRUB entry text, and both Windows-preservation check outcomes. These are captured in
the run log committed with the report (see README D18 for paths). The spec deliberately does not
prescribe partition sizes or disk selection — they carry no measurement consequence and pinning
them would fabricate precision.

## One machine state for every Linux number (D2)

The plan's back-compat rule — "one machine, one OS, one recorded environment for every Linux
number" — is implemented as a single **measurement session state**, entered once and held for
every timed run of every harness:

1. Boot the "benchmark isolation" GRUB entry (isolated pair active).
2. `sudo python3 -m pyperf system tune` — machine-wide (no `--affinity` restriction), applying
   pyperf's documented operations: scaling governor → `performance`, `scaling_min_freq` raised,
   `irqbalance` stopped + IRQ affinity managed, `perf_event_max_sample_rate` → 1, turbo handling
   via the driver paths pyperf implements
   ([pyperf system docs](https://pyperf.readthedocs.io/en/latest/system.html)).
3. **Boost explicitly off, machine-wide:** `echo 0 | sudo tee /sys/devices/system/cpu/cpufreq/boost`
   (the `amd-pstate` boost control; pyperf's turbo paths are MSR/`intel_pstate`-oriented, so the
   AMD control is asserted directly rather than assumed covered). The resulting state is read
   back and recorded. This is a **known-divergent frequency-policy setting vs the Windows runs**
   (Windows ran with boost enabled under the High-performance plan) and is disclosed as such in
   the environment record and next to affected findings — it is part of the deliberately-changed
   variable (Linux best-stability conditions), not a hidden confound; and because every Linux
   number, including the Linux Heddle anchor, is taken under the same state, all within-Linux
   ratios, ranks, and dispersions remain internally consistent.
4. `pyperf system show` output captured — the authoritative post-tune state record.
5. All harness runs execute in this state (order in README WI6). **No per-harness priority
   elevation is applied on Linux**: the Windows priority knobs (BenchmarkDotNet's default
   elevation, `start /high` for Go, run.ps1's High class for JS, psutil's
   `REALTIME_PRIORITY_CLASS` for pyperf workers) were each ecosystem's substitute for missing OS
   isolation; on Linux the deliberate mechanism is the tuned, isolated system itself, and adding
   `nice`/`chrt` per harness would introduce a knob no ecosystem spec's Windows decision
   structure contains. Where a harness elevates itself by default (BenchmarkDotNet attempts it on
   every OS), its own logged behavior is recorded verbatim, not suppressed.
6. After the last run: `sudo python3 -m pyperf system reset` and reboot to the default entry
   (documented reset semantics: governor, frequency, irqbalance, sample rate restored).

If the session must be interrupted (reboot, crash), steps 1–4 are re-executed and the fresh
`pyperf system show` is diffed against the recorded one; any difference aborts resumption until
reconciled. Numbers taken before and after a verified-identical re-entry may be combined; numbers
taken under a differing state are discarded and re-run.

## Environment-record checklist (D3 — closed list)

`capture-environment.sh` (committed; README D18) collects **exactly** the following, and the
report's environment block summarizes it. This closes the plan's flagged "exact recording list is
a spec detail".

### Machine and firmware

| # | Item | Source command |
|---|---|---|
| M1 | System/board/BIOS identity: vendor, product, BIOS version + date | `sudo dmidecode -t system -t baseboard -t bios` |
| M2 | CPU identity + topology: model, cores/threads, sockets, CCD/SMT layout | `lscpu` and `lscpu -e` |
| M3 | Memory: module count, size, configured speed | `sudo dmidecode -t memory` (speed lines) |
| M4 | Firmware-invariance statement: "no UEFI setting changed since the Windows protocol runs" + the D1 exceptions if any | run log entry (human attestation, dated) |
| M5 | SMT state (must read enabled) | `cat /sys/devices/system/cpu/smt/control` |

### Kernel, mitigations, isolation

| # | Item | Source command |
|---|---|---|
| K1 | OS release + kernel | `cat /etc/os-release`, `uname -a` |
| K2 | Kernel cmdline, verbatim (contains the isolation parameters) | `cat /proc/cmdline` |
| K3 | CPU-mitigation state, every file | `grep -r . /sys/devices/system/cpu/vulnerabilities/` |
| K4 | Isolated CPUs + their thread-sibling proof | `cat /sys/devices/system/cpu/isolated`, `cat /sys/devices/system/cpu/cpu<N>/topology/thread_siblings_list` |
| K5 | ASLR state | `cat /proc/sys/kernel/randomize_va_space` |
| K6 | Transparent hugepages mode | `cat /sys/kernel/mm/transparent_hugepage/enabled` |
| K7 | perf event sample rate (post-tune, expect 1) | `sysctl kernel.perf_event_max_sample_rate` |
| K8 | Running services list (post-quiesce) | `systemctl list-units --type=service --state=running` |

Mitigations are left at kernel defaults — **not** `mitigations=off` — because the Windows boots
also run with default mitigations; K3 makes the actual posture disclosed rather than assumed
equal across OSes (the plan's risk-table mitigation, verbatim intent).

### Frequency policy

| # | Item | Source command |
|---|---|---|
| F1 | Scaling driver and operation mode | `cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_driver`, `cat /sys/devices/system/cpu/amd_pstate/status` |
| F2 | Governor (all policies) | `cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor \| sort -u` |
| F3 | Boost state (expect 0 — D2 step 3) | `cat /sys/devices/system/cpu/cpufreq/boost` |
| F4 | Min/max scaling frequencies | `cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_{min,max}_freq` |
| F5 | Energy-performance preference | `cat /sys/devices/system/cpu/cpu0/cpufreq/energy_performance_preference` (if the driver exposes it) |
| F6 | The full `pyperf system show` output | `python3 -m pyperf system show` |
| F7 | Power source (AC) | `cat /sys/class/power_supply/*/online` where present; desktop box — recorded as AC by construction |

### Per-engine toolchain / runtime / libc / allocator identity (the platform-toolchain disclosure)

One table row per ecosystem, capturing the structural toolchain change that necessarily
accompanies the OS change (plan risk row (b)). Closed column set: **runtime/compiler version
string · install channel · target triple/arch · libc · allocator note**.

| Ecosystem | Version string source | Install channel (specified) | libc | Allocator note (recorded text) |
|---|---|---|---|---|
| .NET | `dotnet --info` (SDK + host + RID) | `dotnet-install.sh --version <exact Windows-run SDK version>` | glibc (`ldd --version`) | .NET GC (Server GC off — BenchmarkDotNet defaults, as on Windows); RID `linux-x64` |
| Rust | `rustc -vV` (release + host triple + LLVM) | `rustup toolchain install 1.97.1` (exact pin — same as phase 2 D1) | glibc | System allocator (`std`), same as Windows build; host `x86_64-unknown-linux-gnu` |
| JVM | JMH `# VM version` line + `java -version` | Temurin 25 Linux x64 tar.gz, same GA build as the Windows run if still published, else latest Temurin 25 GA (delta recorded) | glibc | JVM heap; GC as reported by the JMH VM line (defaults — no `jvmArgs`, phase 3 D9 rule carried) |
| JS/Node | `node --version` + `process.versions.v8` | nodejs.org official `linux-x64` tarball, `24.18.0` (same pin as phase 4) | glibc | V8 heap; npm `ci` against the committed lockfile |
| Python | `python -VV` + `sysconfig.get_config_var("CONFIG_ARGS")` | deadsnakes PPA `python3.14` (D5; exact micro + package version recorded; delta vs 3.14.6 recorded if the PPA has moved) | glibc | pymalloc (default); build provenance (PGO/LTO flags) recorded from `CONFIG_ARGS` — the Windows run used the python.org PGO installer, so provenance is a disclosed platform-toolchain item, not assumed equal |
| Go | `go version` (expect `go1.26.5 linux/amd64`) | go.dev/dl official `linux-amd64` tarball | glibc (cgo-linked stdlib parts) | Go runtime allocator + Green Tea GC (1.26 default), `GOGC=100`; `GOMAXPROCS` recorded as the runtime reports it (expected < 32 under `isolcpus` — the isolated pair leaves the default affinity mask; disclosed, not corrected) |

`ldd --version` (glibc version, expected 2.39 line for 24.04 — recorded, not assumed) is captured
once and referenced by every row. The engine *library* pins (crates, Maven artifacts, npm and pip
packages, go.mod) are platform-independent and are re-verified identical to each ecosystem spec's
pins by the lockfile/manifest checks in each harness's own reproduce path — they appear in the
delta table only if an install proves impossible at the pinned version.

### Repo state

Commit hash of the measurement checkout (`git rev-parse HEAD`, clean tree required — same
discipline as `export-corpus`), plus the corpus `generatingCommit` values consumed (from
`manifest.json`), proving the Linux run gated against the same corpus bytes the Windows runs did.

## Version pinning vs each Windows source run (D4)

**Policy: identical where the pin is platform-independent; recorded delta where the platform
forces a difference.** Concretely:

1. **Must be identical (failure to install at the pinned version blocks the ecosystem's run
   until resolved or the delta is maintainer-acknowledged):** all package-manager pins — Askama
   0.16.0 / Tera 2.0.0 / Criterion 0.8.2, jte 3.2.4 / Thymeleaf 3.1.5.RELEASE / JMH 1.37,
   handlebars 4.7.9 / eta 4.6.0 / mitata 1.0.34, Jinja2 3.1.6 / Mako 1.3.12 / MarkupSafe 3.0.3 /
   pyperf 2.10.0 / psutil 7.2.2, templ v0.3.1020, BenchmarkDotNet 0.15.8 — each enforced by the
   same committed lockfile/manifest its ecosystem spec pinned (`Cargo.lock`, `pom.xml`,
   `package-lock.json` + `npm ci`, `requirements.txt` `==` pins, `go.mod`, `csproj`). These
   values are restated here from the phase 1–6 specs *(verify at implementation: re-read each
   shipped ecosystem's published Windows report environment block and use its recorded values as
   the source of truth — the report, not this table, is what the delta is measured against)*.
2. **Pin-as-practicable (platform-specific artifacts):** Node 24.18.0 linux-x64, Go 1.26.5
   linux-amd64, Rust 1.97.1 `x86_64-unknown-linux-gnu`, .NET SDK at the exact Windows-run
   version (linux-x64), Temurin 25 same GA build where downloadable, CPython 3.14.x via
   deadsnakes (D5). Same version *number*, necessarily different platform build — the build
   identity goes in the toolchain table above, and only a version-*number* difference counts as
   a delta.
3. **Delta recording procedure.** `version-deltas.md` (published in the report directory) is a
   table: `component | Windows source run value (with report link) | Linux value | delta? |
   caveat obligation`. Every `delta? = yes` row creates an **inline caveat obligation**: every
   findings-validation statement in the report's part 2 that touches that component's ecosystem
   carries the delta inline (the plan's Phase 7 drift-disclosure posture applied cross-OS). Zero
   deltas is the target; the table is published even when empty ("no version deltas against any
   source run").
4. **No upgrades for their own sake.** Between the Windows source runs and this phase, upstream
   releases will have occurred; this phase does **not** adopt them. A pin is moved only when the
   pinned artifact is uninstallable on Ubuntu 24.04 (then: minimal move, delta recorded).

## CPython on Linux (D5)

Primary path: **deadsnakes PPA `python3.14`** on Ubuntu 24.04 (24.04's own archive ships Python
3.12 as `python3`; 3.14 is not in the release archive). The installed micro version and the
deadsnakes package version string are recorded; if the PPA's 3.14 micro differs from the Windows
run's 3.14.6, that is a recorded delta with a caveat obligation (D4.3). Fallback (trigger: the
PPA does not provide any 3.14 for noble at run time): build CPython from the 3.14.6 source
tarball with `./configure --enable-optimizations --with-lto` (matching the optimization posture
of the python.org Windows installer builds as closely as the platform allows), and record
`CONFIG_ARGS` + compiler version in the toolchain table. In both paths the venv/requirements
procedure of the phase 5 spec is unchanged.

The build-provenance disclosure exists because interpreter build flags (PGO/LTO) move Python
wall times materially; the record makes the Linux Python numbers attributable to a named build,
and any Python findings-validation statement cites the provenance line when dispersion or gaps
move.
