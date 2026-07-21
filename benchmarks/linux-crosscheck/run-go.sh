#!/usr/bin/env bash
# run-go.sh — Go testing/benchstat launcher (Phase 8 spec D11; Windows source: Phase 6).
# Replicates the Phase 6 run-benchmarks steps: version asserts, templ regeneration
# freshness, vet, gates, prebuild, timed invocation(s), benchstat. Prebuilt binary
# launched PLAIN — no nice, no taskset, no start /high counterpart (D2 rule; the
# tuned system is the isolation). GOMAXPROCS/GOGC runtime defaults, values recorded
# as found (isolcpus-reduced mask disclosed, not overridden). The D11 trigger
# (benchstat > ±5% -> taskset re-run on CCD0 non-isolated CPUs) is an operator step,
# recorded as one environment change.
#
# usage: ./run-go.sh [--smoke] [--no-tune-check]
#   --smoke: -count=1 -benchtime=100ms (functional only)

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "go (testing/benchstat, D11)" 2>&1 | tee "$LCX_OUT_DIR/run-go.log"

cd "$LCX_REPO_ROOT/benchmarks/go"

# Version assert (Phase 6 reproduce path; expected pin go1.26.5 linux/amd64).
GO_V="$(go version)"
lcx_note "go version: $GO_V" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
case "$GO_V" in
  *"go1.26.5 linux/amd64"*) : ;;
  *)
    if [ "$LCX_SMOKE" = "1" ]; then
      lcx_note "RECORDED: go version differs from the pin (go1.26.5 linux/amd64) — acceptable in --smoke only" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
    else
      lcx_die "go version assert failed: want go1.26.5 linux/amd64, got: $GO_V"
    fi ;;
esac

# templ regeneration freshness (Phase 6; testing-plan check).
lcx_note "templ freshness: git diff --exit-code -- '*_templ.go'" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
git diff --exit-code -- '*_templ.go' 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"

# Vet + gates (D12: TestMain runs all gates in `go test ./suites`).
lcx_note "go vet ./..." 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
go vet ./... 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
lcx_note "gate: go test ./suites" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
go test ./suites 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"

# Prebuild, then launch the binary plain (D11).
BENCH_BIN="$LCX_OUT_DIR/go-bench"
lcx_note "prebuild: go test -c -o $BENCH_BIN ./suites" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
go test -c -o "$BENCH_BIN" ./suites 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"

if [ "$LCX_SMOKE" = "1" ]; then
  COUNT=1; BENCHTIME=100ms
  lcx_note "SMOKE: -count=1 -benchtime=100ms (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
else
  COUNT=20; BENCHTIME=1s
fi

RESULTS="$LCX_OUT_DIR/go-results"
mkdir -p "$RESULTS"
lcx_note "bench: -run '^$' -bench '^BenchmarkRender$' -count=$COUNT -benchtime=$BENCHTIME (GOMAXPROCS recorded as found)" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
"$BENCH_BIN" -test.run '^$' -test.bench '^BenchmarkRender$' -test.count="$COUNT" -test.benchtime="$BENCHTIME" 2>&1 | tee "$RESULTS/bench.txt"

# Cold-parse sidebar runs separately per the Phase 6 procedure (operator step; same flags).

if command -v benchstat >/dev/null 2>&1; then
  benchstat "$RESULTS/bench.txt" 2>&1 | tee "$RESULTS/benchstat.txt"
else
  lcx_note "RECORDED: benchstat not on PATH — run 'benchstat $RESULTS/bench.txt' before publication (sec/op + ±% are the published pair)" 2>&1 | tee -a "$LCX_OUT_DIR/run-go.log"
fi
lcx_note "go launcher done; outputs under $RESULTS"
