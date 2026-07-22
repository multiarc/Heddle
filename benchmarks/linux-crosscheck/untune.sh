#!/usr/bin/env bash
# untune.sh — exit the D2 measurement-session state (Phase 8 spec D2 step 6):
# pyperf system reset, boost restored, session state file retired.
#
# BARE-METAL ONLY. Under WSL2 (SR-2) this fails cleanly by design — there is no
# tuned state to undo; that failure is the designed outcome, not a defect.
#
# Run as: sudo ./untune.sh

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"

fail_bare_metal_only() {
  echo "untune.sh: BARE-METAL-ONLY OPERATION — $1" >&2
  echo "untune.sh: there is no D2 tuned state to undo except on the bare-metal measurement" >&2
  echo "untune.sh: boot; under WSL2 this failure is the designed outcome (SR-2, deferred)." >&2
  exit 1
}

if lcx_is_wsl; then
  fail_bare_metal_only "WSL2 detected via /proc/version"
fi

# --- pyperf system reset (documented: governor, frequency, irqbalance, sample rate restored)
lcx_note "running: python3 -m pyperf system reset"
python3 -m pyperf system reset

# --- Restore boost to its pre-tune value (recorded by tune.sh) ----------------
BOOST_FILE="/sys/devices/system/cpu/cpufreq/boost"
if [ ! -e "$BOOST_FILE" ]; then
  fail_bare_metal_only "no $BOOST_FILE (amd-pstate boost control absent)"
fi
PRIOR="1"
if [ -f "$LCX_OUT_DIR/boost-prior.txt" ]; then
  PRIOR="$(cat "$LCX_OUT_DIR/boost-prior.txt")"
else
  lcx_note "no recorded prior boost state ($LCX_OUT_DIR/boost-prior.txt) — restoring default 1"
fi
echo "$PRIOR" > "$BOOST_FILE"
lcx_note "boost restored to: $(cat "$BOOST_FILE")"

# --- Confirm via pyperf system show, then retire the session-state baseline ---
lcx_note "post-reset 'pyperf system show' (confirmation record):"
python3 -m pyperf system show || true

if [ -f "$LCX_STATE_FILE" ]; then
  mv "$LCX_STATE_FILE" "$LCX_STATE_FILE.retired.$(date -u +%Y%m%dT%H%M%SZ)"
  lcx_note "session state file retired — run-*.sh pre-flights will now refuse to run."
fi

lcx_note "D2 state exited. Reboot to the DEFAULT GRUB entry before any non-measurement use"
lcx_note "(the isolation entry must not remain the running boot outside a session)."
