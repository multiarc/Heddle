#!/usr/bin/env bash
# common.sh — shared helpers for the Phase 8 linux-crosscheck tooling (spec D18).
# Sourced by every script in this directory; not executable on its own.

set -u

# Directory this file lives in (repo path: benchmarks/linux-crosscheck).
LCX_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
LCX_REPO_ROOT="$(cd -- "$LCX_DIR/../.." >/dev/null 2>&1 && pwd)"
LCX_OUT_DIR="${LCX_OUT_DIR:-$LCX_DIR/out}"
LCX_STATE_FILE="$LCX_OUT_DIR/session-state.txt"

lcx_mkout() {
  mkdir -p "$LCX_OUT_DIR"
  # Keep script outputs out of git (D17.2: no stray files outside the dated report dir).
  if [ ! -f "$LCX_OUT_DIR/.gitignore" ]; then
    printf '*\n' > "$LCX_OUT_DIR/.gitignore"
  fi
}

# --- WSL detection (SR-2) ----------------------------------------------------
# WSL2 kernels self-identify in /proc/version and /proc/sys/kernel/osrelease
# ("microsoft-standard-WSL2"). Bare-metal Ubuntu does not.
lcx_is_wsl() {
  grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null || \
    grep -qiE 'microsoft|wsl' /proc/sys/kernel/osrelease 2>/dev/null
}

lcx_die() {
  echo "ERROR: $*" >&2
  exit 1
}

lcx_note() {
  echo "== $*"
}

# --- SMT sibling pair (never assumed; D1.6 / spec 'verify at implementation') --
# Reads the thread-sibling list of the configured physical core (default: CPU 4's
# core, per environment-and-toolchains.md D1.6). Fails if the topology file is
# absent — which it is under WSL2 (no real per-CPU topology is exposed).
LCX_ISOLATION_CPU="${LCX_ISOLATION_CPU:-4}"

lcx_smt_pair() {
  local f="/sys/devices/system/cpu/cpu${LCX_ISOLATION_CPU}/topology/thread_siblings_list"
  if [ ! -r "$f" ]; then
    return 1
  fi
  cat "$f"
}

# --- Tuned-state pre-flight (WI5: launchers assert the D2 state before timing) --
# Compares `pyperf system show` against the state recorded by tune.sh.
# Callers pass "$1" = 1 to bypass (--no-tune-check, SR-2); the bypass is RECORDED
# on stdout so it lands in every captured launcher log.
lcx_preflight_tuned_state() {
  local bypass="${1:-0}"
  if [ "$bypass" = "1" ]; then
    echo "TUNE-CHECK: BYPASSED via --no-tune-check (recorded)."
    echo "TUNE-CHECK: this is acceptable only for functional/--smoke runs (SR-2 WSL posture);"
    echo "TUNE-CHECK: a bare-metal measurement run MUST NOT bypass the pre-flight."
    return 0
  fi
  if [ ! -f "$LCX_STATE_FILE" ]; then
    lcx_die "state drift: no recorded session state at $LCX_STATE_FILE — run tune.sh first (or pass --no-tune-check for a functional run; the bypass is recorded)"
  fi
  local current
  if ! current="$(python3 -m pyperf system show 2>&1)"; then
    lcx_die "state drift: 'pyperf system show' failed: $(printf '%s' "$current" | head -n 3)"
  fi
  local diff_out
  if ! diff_out="$(printf '%s\n' "$current" | diff -u "$LCX_STATE_FILE" - 2>&1)"; then
    echo "state drift: <diff excerpt>" >&2
    printf '%s\n' "$diff_out" | head -n 20 >&2
    exit 1
  fi
  echo "TUNE-CHECK: pyperf system show matches recorded session state ($LCX_STATE_FILE)."
}

# --- Launcher arg parsing (shared shape: [--smoke] [--no-tune-check]) ----------
LCX_SMOKE=0
LCX_NO_TUNE_CHECK=0

lcx_parse_launcher_args() {
  while [ $# -gt 0 ]; do
    case "$1" in
      --smoke) LCX_SMOKE=1 ;;
      --no-tune-check) LCX_NO_TUNE_CHECK=1 ;;
      -h|--help)
        echo "usage: $(basename -- "$0") [--smoke] [--no-tune-check]"
        echo "  --smoke          short functional pass (no measurement validity)"
        echo "  --no-tune-check  bypass the D2 tuned-state pre-flight (bypass is recorded; WSL/SR-2 only)"
        exit 0 ;;
      *) lcx_die "unknown argument: $1" ;;
    esac
    shift
  done
}

lcx_launcher_header() {
  local name="$1"
  echo "== linux-crosscheck launcher: $name =="
  echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if lcx_is_wsl; then echo "environment: WSL2 (SR-2 functional posture)"; else echo "environment: non-WSL Linux"; fi
  if [ "$LCX_SMOKE" = "1" ]; then
    echo "mode: SMOKE (short functional flags — results carry NO measurement validity)"
  else
    echo "mode: MEASUREMENT (spec D6-D11 shapes)"
  fi
  lcx_preflight_tuned_state "$LCX_NO_TUNE_CHECK"
}
