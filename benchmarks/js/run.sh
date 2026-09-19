#!/usr/bin/env bash
# run.sh -- Linux twin of run.ps1: protocol launcher for the JS benchmark harness.
#
# Starts `node --expose-gc --allow-natives-syntax <script>` with stdout captured to
# artifacts/<name>.txt (in addition to the JSON the script writes itself), waits for exit and
# propagates the exit code. `--repeat N` runs the same invocation N times sequentially into
# artifacts/stability/run-<k>.txt/.json for the cross-pass stability verdict.
#
# Priority posture: run.ps1 sets a High process priority class; this twin approximates it with
# `taskset -c <isolated SMT pair>` + `nice -n -20` (see benchmarks/bench-common.sh). That is a
# deliberate delta from benchmarks/linux-crosscheck/run-js.sh, which launches node plain with no
# priority tweaks at all -- record the delta in the run report, or pass --no-priority.
#
# usage:
#   ./run.sh bench/controlled.mjs
#   ./run.sh bench/controlled.mjs --repeat 18
#   ./run.sh bench/controlled.mjs --no-priority

set -uo pipefail

HARNESS_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
. "$(cd -- "$HARNESS_ROOT/.." >/dev/null 2>&1 && pwd)/bench-common.sh"

usage() {
  echo "usage: run.sh <bench/script.mjs> [--repeat N] [--no-priority]"
  echo "  --repeat N       run N times into artifacts/stability/<name>/, then aggregate (1-100)"
  echo "  --no-priority    plain launch, no taskset/nice (matches the Linux crosscheck launcher)"
}

SCRIPT=""
REPEAT=1
while [ $# -gt 0 ]; do
  case "$1" in
    --repeat) REPEAT="${2:?--repeat requires N}"; shift ;;
    --no-priority) BENCH_NO_PRIORITY=1 ;;
    -h|--help) usage; exit 0 ;;
    -*) bench_die "unknown argument: $1" ;;
    *)
      [ -z "$SCRIPT" ] || bench_die "unexpected extra argument: $1"
      SCRIPT="$1" ;;
  esac
  shift
done

[ -n "$SCRIPT" ] || { usage >&2; bench_die "run.sh: a bench script path is required"; }
case "$REPEAT" in
  ''|*[!0-9]*) bench_die "run.sh: --repeat must be an integer" ;;
esac
if [ "$REPEAT" -lt 1 ] || [ "$REPEAT" -gt 100 ]; then
  bench_die "run.sh: --repeat must be in 1..100 (got $REPEAT)"
fi

SCRIPT_PATH="$HARNESS_ROOT/$SCRIPT"
[ -f "$SCRIPT_PATH" ] || bench_die "run.sh: script not found: $SCRIPT_PATH"

# The artifact base name mirrors the bench script name (bench/controlled.mjs -> controlled).
NAME="$(basename -- "$SCRIPT")"; NAME="${NAME%.*}"
ARTIFACTS_DIR="$HARNESS_ROOT/artifacts"
mkdir -p "$ARTIFACTS_DIR"

bench_priority_argv
[ -n "$BENCH_PRIORITY_RECORDED" ] && echo "run.sh: $BENCH_PRIORITY_RECORDED"
if [ "${#BENCH_PRIORITY_PREFIX[@]}" -gt 0 ]; then
  echo "run.sh: launch prefix: ${BENCH_PRIORITY_PREFIX[*]}"
fi

cd "$HARNESS_ROOT"

invoke_bench_run() { # stdout_path -> exit code of node
  local stdout_path="$1" rc
  ${BENCH_PRIORITY_PREFIX[@]+"${BENCH_PRIORITY_PREFIX[@]}"} \
    node --expose-gc --allow-natives-syntax "$SCRIPT_PATH" | tee "$stdout_path"
  rc=${PIPESTATUS[0]}
  return "$rc"
}

if [ "$REPEAT" -le 1 ]; then
  invoke_bench_run "$ARTIFACTS_DIR/$NAME.txt"
  code=$?
  echo "run.sh: $SCRIPT exited with code $code; capture: artifacts/$NAME.txt"
  exit "$code"
fi

# --repeat mode: N consecutive full runs into artifacts/stability/<name>/run-<k>.txt/.json.
#
# The directory is per script, and it is CLEARED first. Both matter: `bench/aggregate.mjs`
# medians every run-*.json it finds positionally, so a leftover pass from a different script --
# or from a longer previous sequence -- would silently corrupt the aggregate rather than fail.
STABILITY_DIR="$ARTIFACTS_DIR/stability/$NAME"
rm -rf "$STABILITY_DIR"
mkdir -p "$STABILITY_DIR"

k=1
while [ "$k" -le "$REPEAT" ]; do
  invoke_bench_run "$STABILITY_DIR/run-$k.txt"
  code=$?
  if [ "$code" -ne 0 ]; then
    echo "run.sh: repeat $k/$REPEAT failed with exit code $code - aborting the sequence." >&2
    exit "$code"
  fi
  # The bench script writes its JSON artifact as artifacts/<name>.json; snapshot it per run.
  if [ -f "$ARTIFACTS_DIR/$NAME.json" ]; then
    cp -f "$ARTIFACTS_DIR/$NAME.json" "$STABILITY_DIR/run-$k.json"
  else
    bench_warn "run.sh: expected JSON artifact not found after run $k: artifacts/$NAME.json"
  fi
  echo "run.sh: repeat $k/$REPEAT complete."
  k=$((k + 1))
done
echo "run.sh: $REPEAT consecutive runs captured under artifacts/stability/$NAME/."

# Collapse the passes into the published artifact + the stability verdict. This OVERWRITES
# artifacts/<name>.json, which currently holds only the final pass, with the cross-pass
# aggregate; a 'failed' verdict exits non-zero and takes the whole step down with it.
node "$HARNESS_ROOT/bench/aggregate.mjs" "$NAME"
exit $?
