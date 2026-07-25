#!/usr/bin/env bash
# run-benchmarks.sh -- Linux twin of run-benchmarks.ps1: Phase 6 (Go) reproduce-it-yourself
# entry point.
#
# Performs, in order (harness-and-measurement.md, D9):
#   1. Toolchain version assertions (go1.26.5; templ v0.3.1020 via `go tool`, per D3).
#   2. `go tool templ generate` + `git diff --exit-code -- *_templ.go` (regeneration
#      freshness -- committed generated code must match the pinned generator).
#   3. `go vet ./...`
#   4. `go test ./...` (gates + unit tests; TestMain gates before anything can time).
#   5. Prebuild: `go test -c -o bench ./suites`.
#   6. Two timed invocations: BenchmarkRender, then the BenchmarkColdParse sidebar.
#      Defaults: -test.count=20, -test.benchtime=1s.
#   7. benchstat over each output.
#
# Priority posture: run-benchmarks.ps1 launches through `cmd /c start /high`; this twin
# approximates that with `taskset -c <isolated SMT pair>` + `nice -n -20` (see
# benchmarks/bench-common.sh). That is a deliberate delta from
# benchmarks/linux-crosscheck/run-go.sh, which launches the prebuilt binary plain under the
# Phase 8 D2 no-priority rule -- record the delta in the run report, or pass --no-priority.

set -uo pipefail

HARNESS_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
. "$(cd -- "$HARNESS_ROOT/.." >/dev/null 2>&1 && pwd)/bench-common.sh"

usage() {
  echo "usage: run-benchmarks.sh [--count N] [--benchtime D] [--version-check-only] [--no-priority]"
  echo "  --count N              -test.count for both timed runs (default 20)"
  echo "  --benchtime D          -test.benchtime for both timed runs (default 1s)"
  echo "  --version-check-only   run the toolchain asserts and exit"
  echo "  --no-priority          plain launch, no taskset/nice (Phase 8 D2 posture)"
}

COUNT=20
BENCHTIME="1s"
VERSION_CHECK_ONLY=0
while [ $# -gt 0 ]; do
  case "$1" in
    --count) COUNT="${2:?--count requires N}"; shift ;;
    --benchtime) BENCHTIME="${2:?--benchtime requires a duration}"; shift ;;
    --version-check-only) VERSION_CHECK_ONLY=1 ;;
    --no-priority) BENCH_NO_PRIORITY=1 ;;
    -h|--help) usage; exit 0 ;;
    *) bench_die "unknown argument: $1" ;;
  esac
  shift
done
case "$COUNT" in
  ''|*[!0-9]*) bench_die "--count must be an integer" ;;
esac

cd "$HARNESS_ROOT"

assert_last_exit() { # exit_code step
  if [ "$1" -ne 0 ]; then
    echo "FAIL: $2 (exit code $1)" >&2
    exit 1
  fi
}

# --- 1. Toolchain version assertions (D3) -------------------------------------------------
GO_VERSION="$(go version)"; assert_last_exit $? "go version"
case "$GO_VERSION" in
  *go1.26.5*) : ;;
  *) echo "FAIL: pinned toolchain is go1.26.5; found: $GO_VERSION" >&2; exit 1 ;;
esac
# `go tool` may print "go: downloading ..." lines first; the version is the last line.
TEMPL_VERSION="$(go tool templ version)"; assert_last_exit $? "go tool templ version"
TEMPL_VERSION="$(printf '%s\n' "$TEMPL_VERSION" | tail -n 1 | tr -d '[:space:]')"
if [ "$TEMPL_VERSION" != "v0.3.1020" ]; then
  echo "FAIL: pinned templ CLI is v0.3.1020; found: $TEMPL_VERSION" >&2
  exit 1
fi
echo "OK: $GO_VERSION; templ $TEMPL_VERSION"
if [ "$VERSION_CHECK_ONLY" = "1" ]; then
  echo "Version asserts passed; exiting (--version-check-only)."
  exit 0
fi

# --- 2. Regeneration freshness -------------------------------------------------------------
go tool templ generate; assert_last_exit $? "go tool templ generate"
if ! git diff --exit-code -- '*_templ.go'; then
  echo "FAIL: committed *_templ.go does not match the pinned generator's output." >&2
  exit 1
fi

# --- 3. Vet --------------------------------------------------------------------------------
go vet ./...; assert_last_exit $? "go vet"

# --- 4. Gates + unit tests -----------------------------------------------------------------
go test ./...; assert_last_exit $? "go test (gates + unit tests)"

# --- 5. Prebuild ---------------------------------------------------------------------------
go test -c -o bench ./suites; assert_last_exit $? "go test -c (prebuild)"

# --- 6. Timed invocations -------------------------------------------------------------------
mkdir -p results
STAMP="$(date +%Y%m%d)"
RENDER_OUT="results/bench-render-$STAMP.txt"
COLD_OUT="results/bench-coldparse-$STAMP.txt"

bench_priority_argv
[ -n "$BENCH_PRIORITY_RECORDED" ] && echo "run-benchmarks.sh: $BENCH_PRIORITY_RECORDED"
if [ "${#BENCH_PRIORITY_PREFIX[@]}" -gt 0 ]; then
  echo "run-benchmarks.sh: launch prefix: ${BENCH_PRIORITY_PREFIX[*]}"
fi

timed_run() { # bench_regex out_path
  ${BENCH_PRIORITY_PREFIX[@]+"${BENCH_PRIORITY_PREFIX[@]}"} \
    ./bench -test.run '^$' -test.bench "$1" \
    -test.benchtime "$BENCHTIME" -test.count "$COUNT" > "$2" 2>&1
}

timed_run '^BenchmarkRender$' "$RENDER_OUT"
assert_last_exit $? "BenchmarkRender timed run"
echo "BenchmarkRender run written to $RENDER_OUT"

timed_run '^BenchmarkColdParse$' "$COLD_OUT"
assert_last_exit $? "BenchmarkColdParse timed run"
echo "BenchmarkColdParse run written to $COLD_OUT"
echo "Note: both stdlib cold rows are 'cold parse only' (html/template escape-compilation is lazy)."
echo "Note: templ sidebar cells are 'AOT - no runtime parse (compiled by go generate)'."

# --- 7. benchstat ---------------------------------------------------------------------------
BENCHSTAT_RENDER="results/benchstat-render-$STAMP.txt"
BENCHSTAT_COLD="results/benchstat-coldparse-$STAMP.txt"
go tool benchstat "$RENDER_OUT" > "$BENCHSTAT_RENDER"
assert_last_exit $? "benchstat (render)"
go tool benchstat "$COLD_OUT" > "$BENCHSTAT_COLD"
assert_last_exit $? "benchstat (coldparse)"

echo "Done. benchstat summaries: $BENCHSTAT_RENDER, $BENCHSTAT_COLD"
