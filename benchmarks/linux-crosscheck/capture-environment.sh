#!/usr/bin/env bash
# capture-environment.sh — the D3 CLOSED environment-record checklist
# (Phase 8 spec D3; environment-and-toolchains.md "Environment-record checklist").
#
# Collects exactly M1-M5, K1-K8, F1-F7, the per-engine toolchain/libc/allocator
# identity table, and the repo/corpus state. Runs on bare metal AND under WSL2:
# under WSL (SR-2) every bare-metal-only item is RECORDED as
# "unavailable under WSL (deferred to bare metal)" instead of crashing — the
# checklist stays closed; nothing is silently skipped.
#
# usage: ./capture-environment.sh [--out DIR]

set -uo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"

OUT=""
while [ $# -gt 0 ]; do
  case "$1" in
    --out) OUT="${2:?--out requires a directory}"; shift ;;
    -h|--help) echo "usage: capture-environment.sh [--out DIR]"; exit 0 ;;
    *) lcx_die "unknown argument: $1" ;;
  esac
  shift
done

if [ -z "$OUT" ]; then
  lcx_mkout
  OUT="$LCX_OUT_DIR/environment"
fi
mkdir -p "$OUT"
RECORD="$OUT/environment-record.md"

IS_WSL=0
if lcx_is_wsl; then IS_WSL=1; fi
UNAVAILABLE_WSL="unavailable under WSL (deferred to bare metal)"

{
  echo "# Environment record (D3 closed checklist)"
  echo
  echo "- Captured: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if [ "$IS_WSL" = "1" ]; then
    echo "- Host class: **WSL2** (SR-2 functional posture — bare-metal-only items below are"
    echo "  recorded as deferred, not faked; this record is NOT a measurement-session record)"
  else
    echo "- Host class: non-WSL Linux"
  fi
  echo
} > "$RECORD"

# capture <id> <title> <slug> <cmd...>
# Runs cmd, stores raw output in $OUT/<slug>.txt, and appends a section to the record.
# Any failure (missing file, missing tool, permission) records the unavailable note.
capture() {
  local id="$1" title="$2" slug="$3"
  shift 3
  local raw="$OUT/$slug.txt"
  local status=0
  local output shown
  output="$("$@" 2>&1)" || status=$?
  shown="${*/#sudo_n/sudo -n}"
  {
    echo "## $id — $title"
    echo
    echo '```'
    echo "\$ $shown"
    echo '```'
    echo
  } >> "$RECORD"
  if [ $status -ne 0 ] || [ -z "$output" ]; then
    local note="unavailable ($* exited $status)"
    if [ "$IS_WSL" = "1" ]; then note="$UNAVAILABLE_WSL"; fi
    printf '%s\n' "$note" > "$raw"
    {
      echo "> $note"
      echo
    } >> "$RECORD"
    lcx_note "$id: $note"
  else
    printf '%s\n' "$output" > "$raw"
    {
      echo '```'
      printf '%s\n' "$output" | head -n 60
      if [ "$(printf '%s\n' "$output" | wc -l)" -gt 60 ]; then
        echo "... (full output: environment/$slug.txt)"
      fi
      echo '```'
      echo
    } >> "$RECORD"
    lcx_note "$id: captured -> $slug.txt"
  fi
}

# sudo wrapper that never prompts (non-interactive capture); failure => unavailable note.
sudo_n() { sudo -n "$@"; }

echo "### Machine and firmware (M1–M5)" >> "$RECORD"; echo >> "$RECORD"
capture M1 "System/board/BIOS identity" "m1-dmidecode-system"   sudo_n dmidecode -t system -t baseboard -t bios
capture M2 "CPU identity + topology"    "m2-lscpu"              lscpu
capture M2e "CPU topology (extended)"   "m2-lscpu-e"            lscpu -e
capture M3 "Memory modules + speed"     "m3-dmidecode-memory"   sudo_n dmidecode -t memory
# M4 is a dated human attestation, never script-fabricated.
{
  echo "## M4 — Firmware-invariance statement"
  echo
  if [ "$IS_WSL" = "1" ]; then
    echo "> $UNAVAILABLE_WSL — M4 is a run-log human attestation made on the bare-metal box"
    echo "> (\"no UEFI setting changed since the Windows protocol runs\" + D1 exceptions, dated)."
  else
    echo "> PENDING HUMAN ATTESTATION: record in the run log, dated:"
    echo "> \"no UEFI setting changed since the Windows protocol runs\" (+ D1 exceptions if any)."
  fi
  echo
} >> "$RECORD"
capture M5 "SMT state (must read enabled on bare metal)" "m5-smt" cat /sys/devices/system/cpu/smt/control

echo "### Kernel, mitigations, isolation (K1–K8)" >> "$RECORD"; echo >> "$RECORD"
capture K1a "OS release" "k1-os-release" cat /etc/os-release
capture K1b "Kernel"     "k1-uname"      uname -a
capture K2  "Kernel cmdline (verbatim; isolation parameters on the measurement boot)" "k2-cmdline" cat /proc/cmdline
capture K3  "CPU-mitigation state (every file, kernel defaults)" "k3-mitigations" grep -r . /sys/devices/system/cpu/vulnerabilities/
capture K4a "Isolated CPUs" "k4-isolated" cat /sys/devices/system/cpu/isolated
capture K4b "Thread-sibling proof (cpu${LCX_ISOLATION_CPU})" "k4-siblings" cat "/sys/devices/system/cpu/cpu${LCX_ISOLATION_CPU}/topology/thread_siblings_list"
capture K5  "ASLR state" "k5-aslr" cat /proc/sys/kernel/randomize_va_space
capture K6  "Transparent hugepages mode" "k6-thp" cat /sys/kernel/mm/transparent_hugepage/enabled
capture K7  "perf event sample rate (post-tune, expect 1)" "k7-perf-sample-rate" sysctl kernel.perf_event_max_sample_rate
capture K8  "Running services (post-quiesce)" "k8-services" systemctl list-units --type=service --state=running

echo "### Frequency policy (F1–F7)" >> "$RECORD"; echo >> "$RECORD"
capture F1a "Scaling driver" "f1-scaling-driver" cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_driver
capture F1b "amd-pstate status" "f1-amd-pstate" cat /sys/devices/system/cpu/amd_pstate/status
capture F2  "Governor (all policies, unique)" "f2-governors" bash -c 'cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor | sort -u'
capture F3  "Boost state (expect 0 in-session — D2 step 3)" "f3-boost" cat /sys/devices/system/cpu/cpufreq/boost
capture F4  "Min/max scaling frequencies" "f4-scaling-freq" bash -c 'cat /sys/devices/system/cpu/cpu0/cpufreq/scaling_min_freq /sys/devices/system/cpu/cpu0/cpufreq/scaling_max_freq'
capture F5  "Energy-performance preference (if exposed)" "f5-epp" cat /sys/devices/system/cpu/cpu0/cpufreq/energy_performance_preference
capture F6  "pyperf system show (full)" "f6-pyperf-show" python3 -m pyperf system show
capture F7  "Power source (AC; desktop box AC by construction)" "f7-power" bash -c 'cat /sys/class/power_supply/*/online'

# --- Per-engine toolchain / runtime / libc / allocator identity table ---------
lcx_note "building per-engine toolchain identity table"
TOOLCHAINS="$OUT/toolchain-identity.md"

# tool_version <cmd...> -> first line of output, or a not-installed note.
tool_version() {
  local out
  if out="$("$@" 2>&1)"; then
    printf '%s' "$out" | head -n 1
  else
    printf 'not installed (setup-toolchains.sh not yet run, or unavailable on this host)'
  fi
}

GLIBC="$(tool_version ldd --version)"
DOTNET_V="$(tool_version dotnet --version)"
RUSTC_V="$(tool_version rustc -vV)"
JAVA_V="$(java -version 2>&1 | head -n 1 || echo 'not installed (setup-toolchains.sh not yet run, or unavailable on this host)')"
NODE_V="$(tool_version node --version)"
PY_BIN=""
for c in python3.14 python3.13 python3; do
  if command -v "$c" >/dev/null 2>&1; then PY_BIN="$c"; break; fi
done
if [ -n "$PY_BIN" ]; then
  PY_V="$("$PY_BIN" -VV 2>&1 | head -n 1)"
  PY_CFG="$("$PY_BIN" -c 'import sysconfig; print(sysconfig.get_config_var("CONFIG_ARGS"))' 2>/dev/null || echo 'CONFIG_ARGS unavailable')"
else
  PY_V='not installed (setup-toolchains.sh not yet run)'
  PY_CFG='n/a'
fi
GO_V="$(tool_version go version)"

{
  echo "# Per-engine toolchain / runtime / libc / allocator identity (D3)"
  echo
  echo "glibc (captured once, referenced by every row): \`$GLIBC\`"
  echo
  echo "| Ecosystem | Version string | Install channel | Target triple/arch | libc | Allocator note |"
  echo "|---|---|---|---|---|---|"
  echo "| .NET | \`$DOTNET_V\` | dotnet-install.sh --version (D4.2; SR-1-parameterized) | linux-x64 (RID) | glibc | .NET GC (Server GC off — BenchmarkDotNet defaults, as on Windows) |"
  echo "| Rust | \`$RUSTC_V\` | rustup toolchain install 1.97.1 | x86_64-unknown-linux-gnu | glibc | System allocator (std), same as Windows build |"
  echo "| JVM | \`$JAVA_V\` | Temurin 25 linux-x64 tar.gz (JMH '# VM version' line is the runtime identity of record) | linux-x64 | glibc | JVM heap; GC as reported by the JMH VM line (defaults, no jvmArgs) |"
  echo "| JS/Node | \`$NODE_V\` | nodejs.org official linux-x64 tarball 24.18.0 | linux-x64 | glibc | V8 heap; npm ci against the committed lockfile |"
  echo "| Python | \`$PY_V\` | deadsnakes PPA python3.14 (D5; fallback recorded if used) | x86_64-linux-gnu | glibc | pymalloc (default); CONFIG_ARGS: \`$PY_CFG\` |"
  echo "| Go | \`$GO_V\` | go.dev/dl official linux-amd64 tarball 1.26.5 | linux/amd64 | glibc (cgo-linked stdlib parts) | Go runtime allocator + Green Tea GC (1.26 default), GOGC=100; GOMAXPROCS recorded as the runtime reports it |"
  echo
  echo "process.versions.v8: \`$(node -e 'console.log(process.versions.v8)' 2>/dev/null || echo 'n/a — node not installed')\`"
} > "$TOOLCHAINS"
lcx_note "toolchain table -> $TOOLCHAINS"

# --- Repo state ---------------------------------------------------------------
lcx_note "capturing repo state"
REPO="$OUT/repo-state.md"
{
  echo "# Repo state (D3)"
  echo
  cd "$LCX_REPO_ROOT" || exit 1
  echo "- HEAD: \`$(git rev-parse HEAD 2>/dev/null || echo 'unavailable')\`"
  if [ -z "$(git status --porcelain 2>/dev/null)" ]; then
    echo "- Working tree: clean"
  else
    echo "- Working tree: **DIRTY** (a measurement session requires a clean tree — same discipline as export-corpus)"
  fi
  echo "- Corpus generatingCommit values (from src/Heddle.Performance/GoldenCorpus/manifest.json):"
  if [ -f "src/Heddle.Performance/GoldenCorpus/manifest.json" ]; then
    grep -o '"generatingCommit"[^,}]*' src/Heddle.Performance/GoldenCorpus/manifest.json | sort -u | sed 's/^/  - `/;s/$/`/'
  else
    echo "  - manifest.json not found"
  fi
} > "$REPO"

{
  echo "## Companion files"
  echo
  echo "- Per-engine toolchain identity: \`toolchain-identity.md\`"
  echo "- Repo state: \`repo-state.md\`"
  echo "- Raw command outputs: \`*.txt\` alongside this record"
} >> "$RECORD"

lcx_note "D3 environment record complete -> $RECORD"
if [ "$IS_WSL" = "1" ]; then
  lcx_note "WSL run: bare-metal-only items were recorded as deferred (SR-2), not captured."
fi
exit 0
