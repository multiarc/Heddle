#!/usr/bin/env bash
# bench-common.sh -- shared helpers for the Linux twins of the PowerShell runners
# (run-all.sh, go/run-benchmarks.sh, js/run.sh).
#
# Deliberately standalone: benchmarks/linux-crosscheck/common.sh implements the Phase 8
# cross-check protocol (tuned-state pre-flight against a recorded session state, WSL
# functional posture, D2 no-priority rule) and is NOT sourced or modified from here.
#
# Sourced, not executed.

# --- Layout ------------------------------------------------------------------------------
BENCH_COMMON_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
BENCH_ROOT="$BENCH_COMMON_DIR"
BENCH_REPO_ROOT="$(cd -- "$BENCH_ROOT/.." >/dev/null 2>&1 && pwd)"

# The physical core whose SMT sibling pair is the isolation target (Phase 8 D1.6 value).
BENCH_ISOLATION_CPU="${BENCH_ISOLATION_CPU:-4}"

# Set to 1 to suppress the nice/taskset launch prefix everywhere (D2 posture).
BENCH_NO_PRIORITY="${BENCH_NO_PRIORITY:-0}"

bench_note() { echo "== $*"; }
bench_warn() { echo "WARN: $*" >&2; }
bench_die() { echo "ERROR: $*" >&2; exit 1; }

# --- WSL detection -----------------------------------------------------------------------
# WSL2 kernels self-identify in /proc/version and /proc/sys/kernel/osrelease.
bench_is_wsl() {
  grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null || \
    grep -qiE 'microsoft|wsl' /proc/sys/kernel/osrelease 2>/dev/null
}

# --- Topology ----------------------------------------------------------------------------
# The SMT sibling pair is READ from the topology, never assumed. Fails (nonzero) when the
# topology file is absent -- which it is under WSL2.
bench_smt_pair() {
  local f="/sys/devices/system/cpu/cpu${BENCH_ISOLATION_CPU}/topology/thread_siblings_list"
  [ -r "$f" ] || return 1
  cat "$f"
}

bench_isolated_set() {
  cat /sys/devices/system/cpu/isolated 2>/dev/null || true
}

# --- Tuned-state pre-flight ---------------------------------------------------------------
# Asserts the bare-metal measurement state: every cpufreq policy on 'performance', boost
# off, and the isolated set equal to the read SMT pair. Returns nonzero with remediation
# instructions instead of dying, so callers decide whether the failure is fatal.
#
# bench_tuned_preflight [allow_no_isolation]
#   allow_no_isolation=1 -- a missing/empty isolated set is RECORDED instead of fatal
#   (the --tune-no-isolation mode: tuned frequencies, no isolcpus boot entry).
bench_tuned_preflight() {
  local allow_no_isolation="${1:-0}"
  local ok=0 pair isolated govs boost

  if bench_is_wsl; then
    echo "PRE-FLIGHT: WSL2 detected -- no cpufreq policies, no boost control, no isolcpus boot." >&2
    ok=1
  fi

  if ! pair="$(bench_smt_pair)"; then
    echo "PRE-FLIGHT: cannot read cpu${BENCH_ISOLATION_CPU}'s thread_siblings_list (no per-CPU topology)." >&2
    [ "$allow_no_isolation" = "1" ] || ok=1
    pair=""
  fi

  isolated="$(bench_isolated_set)"
  if [ -z "$isolated" ]; then
    if [ "$allow_no_isolation" = "1" ]; then
      echo "RECORDED: /sys/devices/system/cpu/isolated is empty -- measuring WITHOUT CPU isolation."
      echo "RECORDED: no isolcpus/nohz_full/rcu_nocbs boot entry, so the timed runs are not pinned"
      echo "RECORDED: to an isolated SMT pair. This is a measurement-quality delta against the"
      echo "RECORDED: published protocol and must be disclosed in the run report."
    else
      echo "PRE-FLIGHT: /sys/devices/system/cpu/isolated is empty -- the isolation GRUB entry is not booted." >&2
      ok=1
    fi
  elif [ -n "$pair" ] && [ "$isolated" != "$pair" ]; then
    echo "PRE-FLIGHT: isolated set '$isolated' does not equal the read sibling pair '$pair'." >&2
    ok=1
  fi

  govs="$(cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor 2>/dev/null | sort -u || true)"
  if [ "$govs" != "performance" ]; then
    echo "PRE-FLIGHT: cpufreq governors read '${govs:-<none>}', expected 'performance'." >&2
    ok=1
  fi

  boost="$(cat /sys/devices/system/cpu/cpufreq/boost 2>/dev/null || true)"
  if [ "$boost" != "0" ]; then
    echo "PRE-FLIGHT: cpufreq boost reads '${boost:-<absent>}', expected '0'." >&2
    ok=1
  fi

  if [ "$ok" -ne 0 ]; then
    echo "PRE-FLIGHT: this machine is NOT in the bare-metal measurement state." >&2
    echo "PRE-FLIGHT: enter it with the committed Phase 8 tooling (unmodified):" >&2
    echo "PRE-FLIGHT:   sudo $BENCH_ROOT/linux-crosscheck/tune.sh    (or pass --tune to run-all.sh)" >&2
    echo "PRE-FLIGHT: boot the 'Ubuntu -- benchmark isolation' entry first" >&2
    echo "PRE-FLIGHT: (benchmarks/linux-crosscheck/grub-isolation.cfg.example)." >&2
    echo "PRE-FLIGHT: without that boot entry, --tune-no-isolation tunes frequencies only" >&2
    echo "PRE-FLIGHT: (recorded delta); --smoke skips this check; --no-tune-check records a bypass." >&2
    return 1
  fi

  bench_note "pre-flight OK: governors=performance, boost=0, isolated=${isolated:-<none, recorded>}"
  return 0
}

# --- Frequency tuning without the isolation boot entry --------------------------------------
# Self-contained counterpart of linux-crosscheck/tune.sh for machines that are NOT booted
# into the isolcpus entry: it sets what can be set without a reboot (performance governors,
# boost off, perf sample rate) and nothing else. linux-crosscheck/ is not involved.
#
# Every prior value is captured first so bench_untune_no_isolation restores the machine
# exactly as found -- including on an aborted run (callers wire it to an EXIT trap).

bench_sudo() {
  if [ "$(id -u)" = "0" ]; then "$@"; else sudo "$@"; fi
}

bench_write_sysfs() { # value path
  printf '%s\n' "$1" | bench_sudo tee "$2" >/dev/null
}

BENCH_BOOST_FILE="/sys/devices/system/cpu/cpufreq/boost"

# bench_capture_prior_state <file>
bench_capture_prior_state() {
  local out="$1" f
  : > "$out"
  for f in /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor; do
    [ -r "$f" ] || continue
    printf 'gov|%s|%s\n' "$f" "$(cat "$f")" >> "$out"
  done
  if [ -r "$BENCH_BOOST_FILE" ]; then
    printf 'boost|%s|%s\n' "$BENCH_BOOST_FILE" "$(cat "$BENCH_BOOST_FILE")" >> "$out"
  fi
  if command -v sysctl >/dev/null 2>&1; then
    printf 'sysctl|kernel.perf_event_max_sample_rate|%s\n' \
      "$(sysctl -n kernel.perf_event_max_sample_rate 2>/dev/null || echo '')" >> "$out"
  fi
}

# bench_tune_no_isolation <prior-state-file>
# Returns nonzero (after reporting) if a post-condition does not read back.
bench_tune_no_isolation() {
  local prior="$1" f govs boost

  if bench_is_wsl; then
    echo "ERROR: --tune-no-isolation is bare-metal only (WSL2 exposes no cpufreq policies)." >&2
    return 1
  fi
  if [ "$(id -u)" != "0" ] && ! sudo -n true 2>/dev/null; then
    echo "NOTE: --tune-no-isolation needs root; sudo will prompt." >&2
  fi

  bench_capture_prior_state "$prior"
  bench_note "prior machine state captured -> $prior"

  local any=0
  for f in /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor; do
    [ -w "$f" ] || [ -r "$f" ] || continue
    bench_write_sysfs performance "$f" || true
    any=1
  done
  [ "$any" = "1" ] || { echo "ERROR: no cpufreq scaling_governor files exposed -- nothing to tune." >&2; return 1; }

  if [ -e "$BENCH_BOOST_FILE" ]; then
    bench_write_sysfs 0 "$BENCH_BOOST_FILE" || true
  else
    bench_note "RECORDED: no $BENCH_BOOST_FILE -- boost control absent on this kernel/platform"
  fi

  if command -v sysctl >/dev/null 2>&1; then
    bench_sudo sysctl -q -w kernel.perf_event_max_sample_rate=1 2>/dev/null || \
      bench_note "RECORDED: could not set kernel.perf_event_max_sample_rate=1"
  fi

  # Post-conditions (the ones this mode claims; isolation is explicitly NOT claimed).
  govs="$(cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor 2>/dev/null | sort -u || true)"
  if [ "$govs" != "performance" ]; then
    echo "ERROR: governor post-condition failed: read '${govs:-<none>}', expected 'performance'." >&2
    return 1
  fi
  bench_note "post-condition OK (governor, all policies): performance"
  if [ -e "$BENCH_BOOST_FILE" ]; then
    boost="$(cat "$BENCH_BOOST_FILE")"
    if [ "$boost" != "0" ]; then
      echo "ERROR: boost post-condition failed: read '$boost', expected '0'." >&2
      return 1
    fi
    bench_note "post-condition OK (boost): 0"
  fi
  bench_note "tuned WITHOUT CPU isolation (no isolcpus boot entry) -- recorded delta"
  return 0
}

# bench_untune_no_isolation <prior-state-file>
bench_untune_no_isolation() {
  local prior="$1" kind path value
  if [ ! -f "$prior" ]; then
    bench_warn "no prior-state file at $prior -- machine left tuned; restore manually"
    return 1
  fi
  while IFS='|' read -r kind path value; do
    [ -n "${kind:-}" ] || continue
    case "$kind" in
      gov|boost) [ -n "$value" ] && bench_write_sysfs "$value" "$path" || true ;;
      sysctl)    [ -n "$value" ] && bench_sudo sysctl -q -w "$path=$value" 2>/dev/null || true ;;
    esac
  done < "$prior"
  bench_note "machine state restored from $prior"
  bench_note "governors now: $(cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor 2>/dev/null | sort -u | tr '\n' ' ')"
  [ -e "$BENCH_BOOST_FILE" ] && bench_note "boost now: $(cat "$BENCH_BOOST_FILE")"
  return 0
}

# --- Launch priority (Windows-parity posture) ---------------------------------------------
# The Windows launchers set a High process priority class (js/run.ps1, go/run-benchmarks.ps1's
# `start /high`). The Linux twins approximate that with `nice -n -20` plus `taskset` onto the
# isolated SMT pair.
#
# NOTE: benchmarks/linux-crosscheck/run-{go,js}.sh deliberately do NOT do this (Phase 8 D2:
# the tune + isolation IS the isolation). Runs made with these twins therefore carry a
# posture delta that must be recorded in the run report's environment block.
#
# Populates the global array BENCH_PRIORITY_PREFIX (possibly empty) and the string
# BENCH_PRIORITY_RECORDED. Never fails: when part of the prefix cannot be applied it records
# the fact so it lands in the launcher log, and continues with what it can apply.
BENCH_PRIORITY_PREFIX=()
BENCH_PRIORITY_RECORDED=""

bench_priority_argv() {
  local pair
  BENCH_PRIORITY_PREFIX=()
  BENCH_PRIORITY_RECORDED=""

  if [ "$BENCH_NO_PRIORITY" = "1" ]; then
    BENCH_PRIORITY_RECORDED="RECORDED: --no-priority -- plain launch (Phase 8 D2 posture)"
    return 0
  fi

  if pair="$(bench_smt_pair)" && [ -n "$(bench_isolated_set)" ]; then
    if command -v taskset >/dev/null 2>&1; then
      BENCH_PRIORITY_PREFIX+=(taskset -c "$pair")
    else
      BENCH_PRIORITY_RECORDED="RECORDED: taskset not on PATH -- no CPU pinning applied"
    fi
  else
    BENCH_PRIORITY_RECORDED="RECORDED: no isolated SMT pair -- no CPU pinning applied"
  fi

  if [ "$(id -u)" = "0" ]; then
    BENCH_PRIORITY_PREFIX+=(nice -n -20)
  elif sudo -n true 2>/dev/null; then
    BENCH_PRIORITY_PREFIX+=(sudo -n nice -n -20)
  else
    BENCH_PRIORITY_RECORDED="${BENCH_PRIORITY_RECORDED:+$BENCH_PRIORITY_RECORDED; }RECORDED: no root and no passwordless sudo -- 'nice -n -20' not applied, default priority used"
  fi
  return 0
}
