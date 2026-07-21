#!/usr/bin/env bash
# run-js.sh — mitata launcher (Phase 8 spec D9; Windows source: Phase 4 D10-D13, run.ps1).
# Replicates run.ps1's capture and -Repeat behavior WITHOUT the High priority class
# (D2 no-priority rule): starts `node --expose-gc --allow-natives-syntax <script>`,
# tees stdout to an artifacts file, propagates the exit code, supports --repeat N
# for the Phase 4 D13 five-run stability procedure (re-executed on Linux, publication-
# gating: all RSD <= 5% -> verified; (5%,10%] after one re-run cycle -> verified-with-
# disclosure; > 10% persisting -> failed, no JS Linux numbers). run.ps1 is NOT modified.
#
# usage: ./run-js.sh [--smoke] [--no-tune-check] [--repeat N] [--suite controlled|idiomatic|cold|all]
#   --smoke: single mitata pass of the controlled suite only (mitata defaults; the
#            "single pass" is the smoke shape — mitata has no shorter sampling knob
#            that would keep defaults untouched)
#   stability procedure: ./run-js.sh --repeat 5 --suite controlled

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"

REPEAT=1
SUITE="all"
ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --repeat) REPEAT="${2:?--repeat requires N}"; shift ;;
    --suite) SUITE="${2:?--suite requires a name}"; shift ;;
    -h|--help)
      echo "usage: run-js.sh [--smoke] [--no-tune-check] [--repeat N] [--suite controlled|idiomatic|cold|all]"
      echo "  --smoke          single mitata pass, controlled suite (functional only)"
      echo "  --no-tune-check  bypass the D2 tuned-state pre-flight (bypass is recorded; WSL/SR-2 only)"
      echo "  --repeat N       run the suite N times (Phase 4 D13 stability: --repeat 5 --suite controlled)"
      echo "  --suite NAME     controlled | idiomatic | cold | all (default all)"
      exit 0 ;;
    *) ARGS+=("$1") ;;
  esac
  shift
done
lcx_parse_launcher_args ${ARGS[@]+"${ARGS[@]}"}
lcx_mkout
ART="$LCX_OUT_DIR/js-artifacts"
mkdir -p "$ART"
lcx_launcher_header "js (mitata, D9)" 2>&1 | tee "$LCX_OUT_DIR/run-js.log"

cd "$LCX_REPO_ROOT/benchmarks/js"

# Gates first (D12): the ecosystem's own commands, unmodified.
lcx_note "gate: npm run selftest" 2>&1 | tee -a "$LCX_OUT_DIR/run-js.log"
npm run selftest 2>&1 | tee -a "$LCX_OUT_DIR/run-js.log"
lcx_note "gate: npm run gate" 2>&1 | tee -a "$LCX_OUT_DIR/run-js.log"
npm run gate 2>&1 | tee -a "$LCX_OUT_DIR/run-js.log"

# suite name -> script path (Phase 4 bench scripts; same Node flags, mitata defaults).
suite_script() {
  case "$1" in
    controlled) echo "bench/controlled.mjs" ;;
    idiomatic)  echo "bench/idiomatic.mjs" ;;
    cold)       echo "bench/cold-compile.mjs" ;;
    *) lcx_die "unknown suite: $1" ;;
  esac
}

run_suite() { # name
  local name="$1" script rc
  script="$(suite_script "$name")"
  local i=1
  while [ "$i" -le "$REPEAT" ]; do
    local out="$ART/${name}.txt"
    if [ "$REPEAT" -gt 1 ]; then out="$ART/${name}-run${i}.txt"; fi
    lcx_note "node --expose-gc --allow-natives-syntax $script -> $out (run $i/$REPEAT)"
    set +e
    node --expose-gc --allow-natives-syntax "$script" | tee "$out"
    rc=${PIPESTATUS[0]}
    set -e
    if [ "$rc" -ne 0 ]; then
      echo "run-js.sh: $script exited $rc (exit code propagated — run.ps1 parity)" >&2
      exit "$rc"
    fi
    i=$((i + 1))
  done
}

if [ "$LCX_SMOKE" = "1" ]; then
  lcx_note "SMOKE: single mitata pass, controlled suite only (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-js.log"
  REPEAT=1
  run_suite controlled
elif [ "$SUITE" = "all" ]; then
  # Measurement order (D13.4): stability procedure is run FIRST by the operator
  # (./run-js.sh --repeat 5 --suite controlled), then the three timed suites.
  run_suite controlled
  run_suite idiomatic
  run_suite cold
else
  run_suite "$SUITE"
fi
lcx_note "js launcher done; captures under $ART"
