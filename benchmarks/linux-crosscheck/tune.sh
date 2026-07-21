#!/usr/bin/env bash
# tune.sh — enter the one D2 measurement-session state (Phase 8 spec D2 steps 1-4)
# and assert every post-condition (WI2: governor, boost=0, sample rate, isolated set).
#
# BARE-METAL ONLY. Under WSL2 (SR-2) this script fails cleanly by design — WSL
# exposes no cpufreq policy, no boost control, no isolcpus boot and no real tune
# surface; the failure below IS the designed WSL outcome, not a defect.
#
# Run as: sudo ./tune.sh   (pyperf system tune and the boost write need root)

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"

lcx_mkout

fail_bare_metal_only() {
  echo "tune.sh: BARE-METAL-ONLY OPERATION — $1" >&2
  echo "tune.sh: the D2 session state (pyperf system tune, amd-pstate boost-off, isolcpus pair)" >&2
  echo "tune.sh: exists only on the bare-metal Ubuntu Server 24.04 boot of the protocol box." >&2
  echo "tune.sh: Under WSL2 this failure is the designed outcome (SR-2); the tune/untune work" >&2
  echo "tune.sh: is deferred to bare metal, not faked here." >&2
  exit 1
}

# --- Step 0: refuse WSL up front (SR-2 designed failure) ----------------------
if lcx_is_wsl; then
  fail_bare_metal_only "WSL2 detected via /proc/version"
fi

# --- Step 1 precondition: isolation boot entry active -------------------------
# The SMT pair is READ from the topology, never assumed (D1.6).
if ! PAIR="$(lcx_smt_pair)"; then
  fail_bare_metal_only "cannot read /sys/devices/system/cpu/cpu${LCX_ISOLATION_CPU}/topology/thread_siblings_list (no per-CPU topology on this kernel)"
fi
lcx_note "SMT sibling pair of cpu${LCX_ISOLATION_CPU}'s physical core (read, not assumed): ${PAIR}"

ISOLATED_FILE="/sys/devices/system/cpu/isolated"
if [ ! -r "$ISOLATED_FILE" ]; then
  fail_bare_metal_only "cannot read $ISOLATED_FILE"
fi
ISOLATED="$(cat "$ISOLATED_FILE")"
if [ -z "$ISOLATED" ]; then
  echo "tune.sh: ASSERT FAILED (isolated set): /sys/devices/system/cpu/isolated is empty —" >&2
  echo "tune.sh: the 'Ubuntu — benchmark isolation' GRUB entry is not booted (bare-metal-only" >&2
  echo "tune.sh: precondition, D2 step 1; see grub-isolation.cfg.example)." >&2
  exit 1
fi
lcx_note "isolated set: ${ISOLATED} (expected: ${PAIR})"
if [ "$ISOLATED" != "$PAIR" ]; then
  echo "tune.sh: ASSERT FAILED (isolated set): isolated='${ISOLATED}' does not equal the read" >&2
  echo "tune.sh: sibling pair '${PAIR}' — re-check the instantiated GRUB entry (bare-metal-only item)." >&2
  exit 1
fi

# --- Step 2: machine-wide pyperf system tune ----------------------------------
lcx_note "running: python3 -m pyperf system tune (machine-wide, D2 step 2)"
python3 -m pyperf system tune

# --- Step 3: boost explicitly off via the amd-pstate cpufreq control ----------
BOOST_FILE="/sys/devices/system/cpu/cpufreq/boost"
if [ ! -e "$BOOST_FILE" ]; then
  fail_bare_metal_only "no $BOOST_FILE (amd-pstate boost control absent on this kernel/platform)"
fi
# Record prior state for untune.sh.
cat "$BOOST_FILE" > "$LCX_OUT_DIR/boost-prior.txt"
echo 0 > "$BOOST_FILE"

# --- Post-condition asserts (WI2: fail if any item does not read back) --------
assert_eq() { # label expected actual
  if [ "$2" != "$3" ]; then
    echo "tune.sh: ASSERT FAILED ($1): expected '$2', read back '$3'." >&2
    echo "tune.sh: a post-condition that cannot be satisfied here means this is not the tuned" >&2
    echo "tune.sh: bare-metal measurement boot (bare-metal-only operation; SR-2 defers it)." >&2
    exit 1
  fi
  lcx_note "post-condition OK ($1): $3"
}

# Governor: every policy must read 'performance'.
GOVS="$(cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor 2>/dev/null | sort -u || true)"
if [ -z "$GOVS" ]; then
  fail_bare_metal_only "no cpufreq scaling_governor files exposed"
fi
assert_eq "governor (all policies)" "performance" "$GOVS"

# Boost: must read back 0.
assert_eq "boost" "0" "$(cat "$BOOST_FILE")"

# perf sample rate: pyperf system tune sets 1 (K7).
SAMPLE_RATE="$(sysctl -n kernel.perf_event_max_sample_rate)"
assert_eq "perf_event_max_sample_rate" "1" "$SAMPLE_RATE"

# Isolated set unchanged post-tune.
assert_eq "isolated set" "$PAIR" "$(cat "$ISOLATED_FILE")"

# --- Step 4: capture the authoritative post-tune state ------------------------
lcx_note "capturing 'pyperf system show' -> $LCX_STATE_FILE (authoritative D2 state record)"
python3 -m pyperf system show | tee "$LCX_STATE_FILE"

lcx_note "D2 session state entered. Isolated pair: ${PAIR}. Run capture-environment.sh next."
lcx_note "Every run-*.sh will pre-flight against $LCX_STATE_FILE before timing."
