#!/usr/bin/env bash
# run-all.sh — the full measurement matrix in order, stop-on-red-gate.
# Anchor-first: the intra-.NET suite produces the Linux Heddle anchor rows every
# ratio column needs; then the program's priority order Rust -> JVM -> JS -> Python -> Go.
# An ecosystem absent from the shipped subset is skipped by the operator and
# manifest-recorded — this script stops on the first failure
# (a red gate must be triaged before anything later runs).
#
# For the JS ecosystem the five-run stability procedure (5x controlled) runs
# BEFORE the timed suites, as the measurement order requires.
#
# usage: ./run-all.sh [--smoke] [--no-tune-check]

set -euo pipefail
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
. "$HERE/common.sh"
lcx_parse_launcher_args "$@"

PASS=()
[ "$LCX_SMOKE" = "1" ] && PASS+=(--smoke)
[ "$LCX_NO_TUNE_CHECK" = "1" ] && PASS+=(--no-tune-check)

echo "== run-all: measurement order, stop-on-red-gate =="

# 1. Intra-.NET (Linux Heddle anchor rows first).
"$HERE/run-dotnet.sh" ${PASS[@]+"${PASS[@]}"}

# 2. Rust
"$HERE/run-rust.sh" ${PASS[@]+"${PASS[@]}"}

# 3. JVM
"$HERE/run-jvm.sh" ${PASS[@]+"${PASS[@]}"}

# 4. JS — stability procedure first (publication-gating), then the timed suites.
if [ "$LCX_SMOKE" = "1" ]; then
  # Smoke: the launcher's single-pass smoke covers the functional check.
  "$HERE/run-js.sh" ${PASS[@]+"${PASS[@]}"}
else
  "$HERE/run-js.sh" --repeat 5 --suite controlled ${PASS[@]+"${PASS[@]}"}
  echo "== run-all: compute the five-run RSD verdict before continuing (all RSD <= 5% -> verified;"
  echo "==          (5%,10%] after one re-run cycle -> verified-with-disclosure; > 10% persisting"
  echo "==          -> failed, and JS publishes no Linux numbers, manifest-recorded)."
  "$HERE/run-js.sh" ${PASS[@]+"${PASS[@]}"}
fi

# 5. Python
"$HERE/run-python.sh" ${PASS[@]+"${PASS[@]}"}

# 6. Go
"$HERE/run-go.sh" ${PASS[@]+"${PASS[@]}"}

echo "== run-all: complete. Next: validate.py (part 2 tables), then untune.sh, then report assembly. =="
