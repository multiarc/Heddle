#!/usr/bin/env bash
# run-python.sh — pyperf launcher (Phase 8 spec D10; Windows source: Phase 5 D8/harness.md).
# The named driver of the phase. Runner settings unchanged from Windows (pyperf defaults:
# 20 processes x 3 values x 1 warmup, loops auto-calibrated), same five bench scripts,
# same JSON outputs; --affinity=<isolated pair> passed EXPLICITLY (the pair is read from
# the topology at run time, never assumed — affinity-target delta vs Windows --affinity=4
# is disclosed in the environment notes, D10). No elevated shell, no priority: the Linux
# counterpart of REALTIME_PRIORITY_CLASS is the tune + isolation itself (D2).
# Memory pass (mem_tracemalloc.py) runs identically, separate from timing (Phase 5 D11).
#
# usage: ./run-python.sh [--smoke] [--no-tune-check]
#   --smoke: pyperf quick flags (--fast: fewer processes/values — functional only);
#            under WSL with --no-tune-check, --affinity is omitted (no isolated pair
#            exists) and the omission is recorded in the log.

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "python (pyperf, D10)" 2>&1 | tee "$LCX_OUT_DIR/run-python.log"

cd "$LCX_REPO_ROOT/benchmarks/python"

PYBIN="${LCX_PYTHON:-}"
if [ -z "$PYBIN" ]; then
  for c in python3.14 python3.13 python3; do
    if command -v "$c" >/dev/null 2>&1; then PYBIN="$c"; break; fi
  done
fi
[ -n "$PYBIN" ] || lcx_die "no python3 found (run setup-toolchains.sh first)"
lcx_note "interpreter: $($PYBIN -VV 2>&1 | head -n 1)" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"

# Gates first (D12): selftest + both gate tracks, the ecosystem's own commands.
lcx_note "gate: selftest + gate_all (controlled, idiomatic)" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
"$PYBIN" -m runner.selftest 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
"$PYBIN" -m runner.gate_all --track controlled 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
"$PYBIN" -m runner.gate_all --track idiomatic 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"

# Affinity: the isolated SMT pair, read (never assumed) from the topology (D10).
AFFINITY_ARGS=()
if PAIR="$(lcx_smt_pair)" && [ -n "$(cat /sys/devices/system/cpu/isolated 2>/dev/null)" ]; then
  AFFINITY_ARGS=(--affinity="$PAIR")
  lcx_note "pyperf affinity: --affinity=$PAIR (isolated pair, recorded flag)" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
else
  if [ "$LCX_NO_TUNE_CHECK" = "1" ]; then
    lcx_note "RECORDED: no isolated pair available (WSL/SR-2) — --affinity omitted; functional run only" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
  else
    lcx_die "no isolated SMT pair detected and tune-check not bypassed — boot the isolation entry and run tune.sh (or pass --no-tune-check for a functional run)"
  fi
fi

SMOKE_ARGS=()
if [ "$LCX_SMOKE" = "1" ]; then
  SMOKE_ARGS=(--fast)
  lcx_note "SMOKE: pyperf --fast (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
fi

RESULTS="$LCX_OUT_DIR/python-results"
mkdir -p "$RESULTS"

# The five bench scripts (Phase 5 run protocol), same JSON outputs + pyperf stats dumps.
for script in bench_jinja2_controlled bench_jinja2_idiomatic bench_mako_controlled bench_mako_idiomatic bench_cold_compile; do
  lcx_note "$script" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
  "$PYBIN" "$script.py" ${AFFINITY_ARGS[@]+"${AFFINITY_ARGS[@]}"} ${SMOKE_ARGS[@]+"${SMOKE_ARGS[@]}"} -o "$RESULTS/$script.json" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
  "$PYBIN" -m pyperf stats "$RESULTS/$script.json" > "$RESULTS/$script.stats.txt" 2>&1 || true
done

# Memory pass — separate, never combined with timing (Phase 5 D11).
lcx_note "memory pass: mem_tracemalloc.py (separate pass)" 2>&1 | tee -a "$LCX_OUT_DIR/run-python.log"
"$PYBIN" mem_tracemalloc.py 2>&1 | tee "$RESULTS/mem_tracemalloc.txt"

lcx_note "python launcher done; JSON + stats under $RESULTS"
