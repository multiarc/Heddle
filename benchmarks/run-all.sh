#!/usr/bin/env bash
# run-all.sh -- Linux master runner for the cross-stack benchmark program (bare metal).
#
# The twin of run-all.ps1: one command runs every ecosystem's gates and then its measurement
# (or smoke) pass with the parameters the phase specs make normative:
#   .NET    docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md
#   Rust    docs/spec/cross-stack-benchmarks/phase-2-rust/README.md (D9/WI10)
#   JVM     docs/spec/cross-stack-benchmarks/phase-3-jvm/harness-and-jmh.md
#   JS      docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md (+ js/run.sh)
#   Python  docs/spec/cross-stack-benchmarks/phase-5-python/harness.md
#   Go      docs/spec/cross-stack-benchmarks/phase-6-go/harness-and-measurement.md
#           (+ go/run-benchmarks.sh)
# Same phase order as run-all.ps1: gates first, stop on the first red; then
# dotnet -> rust -> jvm -> js -> python -> go.
#
# This is NOT benchmarks/linux-crosscheck/run-all.sh. That one implements the Phase 8
# cross-check protocol (D2 no-priority posture, tuned-state pre-flight against a recorded
# session state, validate.py part-2 tables) and is left untouched; use it for anything
# published as a Linux cross-check. This script is the Windows-parity runner: per-step logs,
# a summary table, ecosystem subsets, and the nice/taskset approximation of the Windows High
# priority classes. The posture delta must be recorded in the run report's environment block.
#
# usage (from anywhere):
#   ./benchmarks/run-all.sh                     # full measurement (tuned bare metal required)
#   ./benchmarks/run-all.sh --tune              # tune first, untune on exit (needs sudo)
#   ./benchmarks/run-all.sh --smoke             # short functional pass
#   ./benchmarks/run-all.sh --ecosystem rust,go # subset
#   ./benchmarks/run-all.sh --out-dir /data/bench-out
#   ./benchmarks/run-all.sh --js-stability-repeat
#   ./benchmarks/run-all.sh --no-tune-check     # record the bypass and measure anyway
#   ./benchmarks/run-all.sh --no-priority       # plain launches (Phase 8 D2 posture)

set -uo pipefail

BENCH_ROOT_SELF="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
. "$BENCH_ROOT_SELF/bench-common.sh"

usage() {
  cat <<'EOF'
usage: run-all.sh [options]
  --smoke                  short functional flags everywhere (no measurement validity)
  --ecosystem LIST         comma-separated subset of dotnet,rust,jvm,js,python,go
  --out-dir PATH           artifact/log destination (default benchmarks/out/linux-run-<stamp>)
  --js-stability-repeat    run the JS five-run stability procedure before the timed suites
  --tune                   sudo linux-crosscheck/tune.sh first, untune.sh on exit
                           (requires the isolcpus boot entry -- the full protocol state)
  --tune-no-isolation      no reboot needed: set performance governors + boost off + perf
                           sample rate here, restore them on exit; the missing CPU isolation
                           is recorded as a measurement-quality delta
  --no-tune-check          record a bypass of the bare-metal pre-flight and continue
  --no-priority            no taskset/nice on the JS and Go timed runs (Phase 8 D2 posture)
  -h, --help               this help
EOF
}

# --- Arguments -----------------------------------------------------------------------------
SMOKE=0
OUT_DIR=""
JS_STABILITY_REPEAT=0
DO_TUNE=0
DO_TUNE_NO_ISOLATION=0
NO_TUNE_CHECK=0
ECO_ARG=""
while [ $# -gt 0 ]; do
  case "$1" in
    --smoke) SMOKE=1 ;;
    --ecosystem) ECO_ARG="${2:?--ecosystem requires a list}"; shift ;;
    --out-dir) OUT_DIR="${2:?--out-dir requires a path}"; shift ;;
    --js-stability-repeat) JS_STABILITY_REPEAT=1 ;;
    --tune) DO_TUNE=1 ;;
    --tune-no-isolation) DO_TUNE_NO_ISOLATION=1 ;;
    --no-tune-check) NO_TUNE_CHECK=1 ;;
    --no-priority) BENCH_NO_PRIORITY=1 ;;
    -h|--help) usage; exit 0 ;;
    *) usage >&2; bench_die "unknown argument: $1" ;;
  esac
  shift
done
if [ "$DO_TUNE" = "1" ] && [ "$DO_TUNE_NO_ISOLATION" = "1" ]; then
  bench_die "--tune and --tune-no-isolation are mutually exclusive"
fi

# Fixed program order (anchor-first, then the program's priority order -- same as Windows).
ALL_ORDER=(dotnet rust jvm js python go)
SELECTED=()
if [ -z "$ECO_ARG" ]; then
  SELECTED=("${ALL_ORDER[@]}")
else
  IFS=',' read -r -a REQUESTED <<< "$ECO_ARG"
  for r in "${REQUESTED[@]}"; do
    case " ${ALL_ORDER[*]} " in
      *" $r "*) : ;;
      *) bench_die "unknown ecosystem '$r' (valid: ${ALL_ORDER[*]})" ;;
    esac
  done
  for e in "${ALL_ORDER[@]}"; do
    for r in "${REQUESTED[@]}"; do
      if [ "$e" = "$r" ]; then SELECTED+=("$e"); break; fi
    done
  done
fi
[ "${#SELECTED[@]}" -gt 0 ] || bench_die "--ecosystem selected nothing"

# --- Layout ---------------------------------------------------------------------------------
REPO_ROOT="$BENCH_REPO_ROOT"
if [ -z "$OUT_DIR" ]; then
  OUT_DIR="$BENCH_ROOT_SELF/out/linux-run-$(date +%Y%m%d-%H%M%S)"
fi
case "$OUT_DIR" in
  /*) : ;;
  *) OUT_DIR="$(pwd)/$OUT_DIR" ;;
esac
LOG_DIR="$OUT_DIR/logs"
mkdir -p "$LOG_DIR" || bench_die "cannot create $LOG_DIR"

PERF_DIR="$REPO_ROOT/src/Heddle.Performance"
RUST_DIR="$BENCH_ROOT_SELF/rust"
JVM_DIR="$BENCH_ROOT_SELF/jvm"
JS_DIR="$BENCH_ROOT_SELF/js"
PY_DIR="$BENCH_ROOT_SELF/python"
GO_DIR="$BENCH_ROOT_SELF/go"
LCX_DIR="$BENCH_ROOT_SELF/linux-crosscheck"
VENV_PY="$PY_DIR/.venv/bin/python"

# The eight protocol suites (phase 1 metrics-protocol: 'The protocol's first exercise').
DOTNET_SUITES=(
  TextRenderBenchmarks
  SubstitutionRenderBenchmarks
  LoopRenderBenchmarks
  MixedRenderBenchmarks
  ConditionalRenderBenchmarks
  FragmentRenderBenchmarks
  FortunesRenderBenchmarks
  EncodedLoopRenderBenchmarks
)

# --- Step machinery ---------------------------------------------------------------------------
RESULTS=()
FAILED=0

if [ -t 1 ]; then
  C_RESET=$'\033[0m'; C_CYAN=$'\033[36m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_RED=$'\033[31m'
else
  C_RESET=""; C_CYAN=""; C_GREEN=""; C_YELLOW=""; C_RED=""
fi

# run_step <eco> <phase> <name> <workdir> [--non-fatal] -- <cmd> [args...]
run_step() {
  local eco="$1" phase="$2" name="$3" workdir="$4"; shift 4
  local nonfatal=0
  if [ "${1:-}" = "--non-fatal" ]; then nonfatal=1; shift; fi
  [ "${1:-}" = "--" ] && shift

  local logname logpath start code secs status
  logname="$(printf '%s-%s' "$eco" "$name" | tr -c 'A-Za-z0-9._-' '_')"
  logpath="$LOG_DIR/$logname.log"

  echo ""
  echo "${C_CYAN}== [$eco/$phase] $name${C_RESET}"
  echo "   dir: $workdir"
  echo "   cmd: $*"
  echo "   log: $logpath"

  {
    echo "# step: $eco/$name"
    echo "# dir:  $workdir"
    echo "# cmd:  $*"
    echo "# date: $(date '+%Y-%m-%d %H:%M:%S')"
    echo ""
  } > "$logpath"

  start=$SECONDS
  ( cd "$workdir" && "$@" ) 2>&1 | tee -a "$logpath"
  code=${PIPESTATUS[0]}
  secs=$(( SECONDS - start ))
  { echo ""; echo "# exit: $code"; } >> "$logpath"

  status="OK"
  if [ "$code" -ne 0 ]; then
    if [ "$nonfatal" = "1" ]; then status="WARN"; else status="FAIL"; FAILED=1; fi
  fi
  RESULTS+=("$eco|$phase|$name|$code|$secs|$status")

  case "$status" in
    FAIL) echo "${C_RED}== FAIL [$eco/$name] exit code $code${C_RESET}" ;;
    WARN) echo "${C_YELLOW}== WARN (non-fatal) [$eco/$name] exit code $code${C_RESET}" ;;
    *)    echo "${C_GREEN}== OK [$eco/$name] ${secs} s${C_RESET}" ;;
  esac
  return "$code"
}

copy_artifacts() { # eco name source dest
  local eco="$1" name="$2" src="$3" dest="$4"
  if [ ! -d "$src" ]; then
    echo "   (no artifacts at $src -- skipping copy)"
    return 0
  fi
  if mkdir -p "$dest" && cp -a "$src/." "$dest/"; then
    echo "   artifacts: $src -> $dest"
    RESULTS+=("$eco|copy|$name|0|0|OK")
  else
    bench_warn "artifact copy failed: $src -> $dest"
    RESULTS+=("$eco|copy|$name|1|0|WARN")
  fi
}

tool_version() { # cmd [args...]
  local out
  command -v "$1" >/dev/null 2>&1 || { echo "NOT FOUND"; return 0; }
  out="$("$@" 2>&1)"
  [ -n "$out" ] || { echo "NOT FOUND"; return 0; }
  printf '%s\n' "$out" | head -n 1
}

PREAMBLE_LOG="$LOG_DIR/preamble.log"
note() { echo "$*"; echo "$*" >> "$PREAMBLE_LOG"; }
: > "$PREAMBLE_LOG"

MODE="MEASUREMENT (spec protocol shapes)"
if [ "$SMOKE" = "1" ]; then
  MODE="SMOKE (short functional flags -- results carry NO measurement validity)"
fi

# --- Summary ----------------------------------------------------------------------------------
write_summary() {
  local summary_path="$OUT_DIR/summary.txt" row
  local table
  table="$(
    printf '%-10s %-8s %-36s %-9s %-8s %s\n' Ecosystem Phase Step ExitCode Seconds Status
    printf '%-10s %-8s %-36s %-9s %-8s %s\n' '---------' '-------' '-----------------------------------' '--------' '-------' '------'
    for row in ${RESULTS[@]+"${RESULTS[@]}"}; do
      IFS='|' read -r c1 c2 c3 c4 c5 c6 <<< "$row"
      printf '%-10s %-8s %-36s %-9s %-8s %s\n' "$c1" "$c2" "$c3" "$c4" "$c5" "$c6"
    done
  )"
  echo ""
  echo "################################################################################"
  echo "## SUMMARY"
  echo "################################################################################"
  printf '%s\n' "$table"
  {
    echo "mode:       $MODE"
    echo "ecosystems: ${SELECTED[*]}"
    echo "finished:   $(date '+%Y-%m-%d %H:%M:%S')"
    echo ""
    printf '%s\n' "$table"
  } > "$summary_path"
  echo ""
  echo "summary: $summary_path"
  echo "logs:    $LOG_DIR"
}

# --- Preamble: toolchain versions, pin deltas (SR-3 style: warn, never fail) -------------------
note '=============================================================================='
note ' Heddle cross-stack benchmarks -- Linux master runner (bare metal)'
note "   date:       $(date '+%Y-%m-%d %H:%M:%S')"
note "   mode:       $MODE"
note "   ecosystems: ${SELECTED[*]}"
note "   out dir:    $OUT_DIR"
note '=============================================================================='
note ''
note '-- Toolchain versions (spec pins in parentheses; deltas WARN, never fail: SR-3) --'

V_DOTNET="$(tool_version dotnet --version)"
note "   dotnet SDK : $V_DOTNET   (protocol records the exact SDK; 10.0.302 was the observed line)"

V_CARGO="$(tool_version cargo --version)"
V_RUSTC="$(tool_version rustc --version)"
note "   cargo      : $V_CARGO"
note "   rustc      : $V_RUSTC   (pin: 1.97.1)"
case "$V_RUSTC" in
  *1.97.1*) : ;;
  *) note "   WARN: rustc differs from the pinned 1.97.1 (record as a version delta)." ;;
esac

V_JAVA="$(tool_version java -version)"
note "   java       : $V_JAVA   (pin: Temurin 25)"
JDK_MAJOR=0
if [[ "$V_JAVA" =~ version\ \"([0-9]+) ]]; then JDK_MAJOR="${BASH_REMATCH[1]}"; fi
if [ "$JDK_MAJOR" != "25" ]; then
  note "   WARN: installed JDK major is $JDK_MAJOR, pin is Temurin 25 (record as a version delta)."
fi
V_MVN="$(tool_version mvn --version)"
note "   maven      : $V_MVN   (harness builds use its own mvnw wrapper)"

V_NODE="$(tool_version node --version)"
V_NPM="$(tool_version npm --version)"
note "   node       : $V_NODE   (pin: v24.18.0)"
note "   npm        : $V_NPM"
NODE_IS_PINNED=0
[ "$V_NODE" = "v24.18.0" ] && NODE_IS_PINNED=1
if [ "$NODE_IS_PINNED" = "0" ]; then
  note "   WARN: node differs from the pinned v24.18.0 (npm ci will run with --engine-strict=false; record as a version delta)."
fi

if [ -x "$VENV_PY" ]; then
  V_PY="$(tool_version "$VENV_PY" --version)"
else
  V_PY="$(tool_version python3 --version)"
fi
note "   python     : $V_PY   (pin: CPython 3.14.6; harness venv at benchmarks/python/.venv)"
case "$V_PY" in
  *3.14.6*) : ;;
  *) note "   WARN: python differs from the pinned CPython 3.14.6 (record as a version delta)." ;;
esac

V_GO="$(tool_version go version)"
note "   go         : $V_GO   (pin: go1.26.x; run-benchmarks.sh hard-asserts go1.26.5)"
case "$V_GO" in
  *go1.26.*) : ;;
  *) note "   WARN: go differs from the pinned 1.26.x line (record as a version delta)." ;;
esac
# `go tool` may emit "go: downloading ..." first; the version is the last line.
V_TEMPL="$( cd "$GO_DIR" && go tool templ version 2>&1 | tail -n 1 )"
[ -n "$V_TEMPL" ] || V_TEMPL="NOT FOUND"
note "   templ      : $V_TEMPL   (pin: v0.3.1020, via go tool)"

# Goldens-rewrite hazard: a stray dotnet-hosted watcher (Heddle demo/docs tooling) touching
# the working tree during export/verify would dirty the corpus manifest. Best-effort check.
# Running the whole matrix as root leaves every build output (jvm/target, rust/target,
# go/results, BenchmarkDotNet.Artifacts, benchmarks/out/...) owned by root, after which a
# later run as a normal user fails -- typically at `mvnw clean`, which cannot delete the
# root-owned jar. Pick one identity and stay with it.
if [ "$(id -u)" = "0" ]; then
  note ''
  note '   WARN: running as root. Every build output this run creates will be root-owned,'
  note '   so a later run as a normal user will fail (mvnw clean, cargo, npm ci).'
  note '   Either keep using root for benchmark runs, or run as your normal user and let'
  note '   the script sudo only where it must (tuning, nice) -- do not alternate.'
fi

DOTNET_PIDS="$(pgrep -x dotnet 2>/dev/null | tr '\n' ' ')"
if [ -n "${DOTNET_PIDS// /}" ]; then
  note ''
  note "   WARN: dotnet process(es) are running (PIDs: ${DOTNET_PIDS% })."
  note '   If any is a Heddle file-watcher (demo/docs tooling), stop it before measuring:'
  note '   a watcher rewriting goldens mid-run is a corpus-freshness hazard (verify-corpus would go red).'
fi
note ''

# --- Bare-metal state: pre-flight, optional tune/untune ---------------------------------------
UNTUNE_ON_EXIT=0
UNTUNE_NO_ISOLATION_ON_EXIT=0
PRIOR_STATE_FILE="$LOG_DIR/tune-prior-state.txt"
on_exit() {
  if [ "$UNTUNE_ON_EXIT" = "1" ]; then
    echo ""
    echo "== restoring the machine: sudo $LCX_DIR/untune.sh"
    sudo "$LCX_DIR/untune.sh" || bench_warn "untune.sh failed -- restore the machine manually"
  fi
  if [ "$UNTUNE_NO_ISOLATION_ON_EXIT" = "1" ]; then
    echo ""
    echo "== restoring the machine (governors, boost, perf sample rate)"
    bench_untune_no_isolation "$PRIOR_STATE_FILE" || \
      bench_warn "restore failed -- see $PRIOR_STATE_FILE and restore manually"
  fi
}
trap on_exit EXIT

if [ "$SMOKE" = "1" ]; then
  note 'RECORDED: --smoke -- bare-metal pre-flight skipped; results carry no measurement validity.'
  if [ "$DO_TUNE" = "1" ] || [ "$DO_TUNE_NO_ISOLATION" = "1" ]; then
    note 'NOTE: tuning with --smoke still tunes and restores (useful for rehearsing the flow).'
  fi
fi

if [ "$SMOKE" != "1" ] && bench_is_wsl; then
  if [ "$NO_TUNE_CHECK" = "1" ]; then
    note 'RECORDED: WSL2 detected and --no-tune-check passed -- measuring anyway; the numbers'
    note 'RECORDED: are NOT publishable (no cpufreq control, no isolcpus boot under WSL).'
  else
    bench_die "WSL2 detected: a measurement run needs the bare-metal boot of the protocol box.
  For a WSL functional pass use the Phase 8 tooling instead:
      benchmarks/linux-crosscheck/run-all.sh --smoke --no-tune-check
  or run this script with --smoke (or --no-tune-check to record a bypass)."
  fi
fi

if [ "$DO_TUNE" = "1" ]; then
  [ -x "$LCX_DIR/tune.sh" ] || bench_die "not found or not executable: $LCX_DIR/tune.sh"
  note "== tuning the machine: sudo $LCX_DIR/tune.sh (committed Phase 8 tooling, unmodified)"
  sudo "$LCX_DIR/tune.sh" 2>&1 | tee "$LOG_DIR/tune.log"
  if [ "${PIPESTATUS[0]}" -eq 0 ]; then
    UNTUNE_ON_EXIT=1
  else
    bench_die "tune.sh failed -- see $LOG_DIR/tune.log (the machine was left as found).
  Without the 'Ubuntu -- benchmark isolation' boot entry, use --tune-no-isolation:
  it sets performance governors + boost off here (no reboot) and records the missing
  CPU isolation as a measurement-quality delta."
  fi
elif [ "$DO_TUNE_NO_ISOLATION" = "1" ]; then
  note '== tuning the machine WITHOUT CPU isolation (governors, boost, perf sample rate)'
  bench_tune_no_isolation "$PRIOR_STATE_FILE" 2>&1 | tee "$LOG_DIR/tune.log"
  if [ "${PIPESTATUS[0]}" -eq 0 ]; then
    UNTUNE_NO_ISOLATION_ON_EXIT=1
  else
    bench_untune_no_isolation "$PRIOR_STATE_FILE" >/dev/null 2>&1 || true
    bench_die "--tune-no-isolation failed -- see $LOG_DIR/tune.log (prior state restored)"
  fi
  bench_tuned_preflight 1 2>&1 | tee -a "$PREAMBLE_LOG"
  [ "${PIPESTATUS[0]}" -eq 0 ] || exit 1
elif [ "$SMOKE" != "1" ]; then
  if [ "$NO_TUNE_CHECK" = "1" ]; then
    note 'RECORDED: --no-tune-check -- the bare-metal pre-flight was BYPASSED. A publishable'
    note 'RECORDED: measurement run must not bypass it (governors, boost, isolated set).'
  else
    bench_tuned_preflight 2>&1 | tee -a "$PREAMBLE_LOG"
    [ "${PIPESTATUS[0]}" -eq 0 ] || exit 1
  fi
fi

# --- Phase 1: GATES (all selected ecosystems, stop on first red) -------------------------------
echo '################################################################################'
echo '## PHASE 1: GATES (stop on first red -- a red gate must be triaged before'
echo '##          anything later runs; same rule as linux-crosscheck/run-all.sh)'
echo '################################################################################'

MVN_ARGS=()
if [ "$JDK_MAJOR" != "0" ] && [ "$JDK_MAJOR" -lt 25 ] 2>/dev/null; then
  MVN_ARGS=(-Dmaven.compiler.release=23)
  echo "${C_YELLOW}NOTE: JDK $JDK_MAJOR < 25 -- passing ${MVN_ARGS[*]} to the JVM build (pom pins release=25).${C_RESET}"
fi

NPM_CI_ARGS=()
[ "$NODE_IS_PINNED" = "0" ] && NPM_CI_ARGS=(--engine-strict=false)

for eco in "${SELECTED[@]}"; do
  case "$eco" in
    dotnet)
      run_step dotnet gate gate-parity "$PERF_DIR" -- \
        dotnet run -c Release -f net10.0 -- parity
      [ "$FAILED" = "1" ] && break
      run_step dotnet gate gate-verify-corpus "$PERF_DIR" -- \
        dotnet run -c Release -f net10.0 -- verify-corpus
      ;;
    rust)
      run_step rust gate gate "$RUST_DIR" -- cargo run --release --bin gate
      ;;
    jvm)
      # The committed mvnw wrapper often lacks the executable bit in a Windows-authored
      # checkout; it is a POSIX sh script, so fall back to `sh ./mvnw` rather than
      # changing the tracked file mode.
      MVNW=(./mvnw)
      [ -x "$JVM_DIR/mvnw" ] || MVNW=(sh ./mvnw)
      run_step jvm gate gate-build-verify "$JVM_DIR" -- \
        "${MVNW[@]}" -q clean verify ${MVN_ARGS[@]+"${MVN_ARGS[@]}"}
      ;;
    js)
      run_step js gate npm-ci "$JS_DIR" -- npm ci ${NPM_CI_ARGS[@]+"${NPM_CI_ARGS[@]}"}
      [ "$FAILED" = "1" ] && break
      run_step js gate selftest "$JS_DIR" -- npm run selftest
      [ "$FAILED" = "1" ] && break
      run_step js gate gate "$JS_DIR" -- npm run gate
      ;;
    python)
      if [ ! -x "$VENV_PY" ]; then
        run_step python gate venv-create "$PY_DIR" -- python3 -m venv .venv
        [ "$FAILED" = "1" ] && break
      fi
      run_step python gate pip-install "$PY_DIR" -- "$VENV_PY" -m pip install -r requirements.txt
      [ "$FAILED" = "1" ] && break
      run_step python gate selftest "$PY_DIR" -- "$VENV_PY" -m runner.selftest
      [ "$FAILED" = "1" ] && break
      run_step python gate gate-all "$PY_DIR" -- "$VENV_PY" -m runner.gate_all
      ;;
    go)
      run_step go gate gate "$GO_DIR" -- go test ./suites
      ;;
  esac
  [ "$FAILED" = "1" ] && break
done

if [ "$FAILED" = "1" ]; then
  echo ""
  echo "${C_RED}A GATE IS RED. Stopping before any measurement (triage the gate first).${C_RESET}"
  write_summary
  exit 1
fi

echo ""
echo "${C_GREEN}All selected gates are green.${C_RESET}"

# --- Phase 2: MEASUREMENT (or smoke) -----------------------------------------------------------
cat <<EOF

${C_YELLOW}################################################################################
## MACHINE-STATE RULES (phase specs; read before trusting any number)
##  * Protocol machine only: the bare-metal boot of the published protocol box
##    (metrics-protocol.md, Q1.6). Numbers from any other machine are not
##    comparable and must not be merged into reports.
##  * Tuned session state: performance governors, boost off, the isolated SMT
##    pair booted (linux-crosscheck/tune.sh; --tune does it for you).
##  * Quiet machine: no editors with background indexing, no other builds or
##    watchers, no browsers, for the duration.
##  * Per-harness stability settings: this runner launches the JS and Go timed
##    runs through taskset + nice -n -20 (the Windows High-priority-class
##    counterpart) and passes pyperf --affinity=<isolated pair>. That differs
##    from linux-crosscheck/run-*.sh, which launch plain under the D2 rule --
##    record the posture delta in the run report's environment block.
EOF
if [ "$UNTUNE_NO_ISOLATION_ON_EXIT" = "1" ]; then
  echo "##  RECORDED: --tune-no-isolation -- performance governors and boost=0 are set,"
  echo "##  but this boot has NO isolated SMT pair: the timed runs are not pinned and"
  echo "##  pyperf falls back to --affinity=4. Disclose this delta in the run report."
fi
if [ "$SMOKE" = "1" ]; then
  echo "##  SMOKE MODE: the above is informational -- smoke results carry no validity."
fi
echo "################################################################################${C_RESET}"
echo ""
echo '################################################################################'
if [ "$SMOKE" = "1" ]; then
  echo '## PHASE 2: SMOKE (functional pass; failures are recorded, run continues)'
else
  echo '## PHASE 2: MEASUREMENT (failures are recorded, run continues)'
fi
echo '################################################################################'

MEASURE_PHASE="measure"
[ "$SMOKE" = "1" ] && MEASURE_PHASE="smoke"

# Priority-posture pass-through for the two per-harness launchers.
PRIORITY_ARGS=()
[ "$BENCH_NO_PRIORITY" = "1" ] && PRIORITY_ARGS=(--no-priority)

for eco in "${SELECTED[@]}"; do
  case "$eco" in
    dotnet)
      # Phase 1 protocol shape: Release, net10.0, BenchmarkDotNet defaults,
      # MemoryDiagnoser via suite attributes; one --filter run per protocol suite.
      for suite in "${DOTNET_SUITES[@]}"; do
        BDN_ARGS=(dotnet run -c Release -f net10.0 -- --filter "*$suite*")
        [ "$SMOKE" = "1" ] && BDN_ARGS+=(--job Dry)
        run_step dotnet "$MEASURE_PHASE" "suite-$suite" "$PERF_DIR" -- "${BDN_ARGS[@]}"
      done
      copy_artifacts dotnet copy-bdn-artifacts "$PERF_DIR/BenchmarkDotNet.Artifacts" "$OUT_DIR/dotnet"
      ;;
    rust)
      # Phase 2 D9 via the WI5 finding (as in linux-crosscheck/run-rust.sh): the lib target
      # does not set bench = false, so the three Criterion bench targets are selected
      # explicitly; sources are untouched.
      if [ "$SMOKE" = "1" ]; then
        run_step rust "$MEASURE_PHASE" criterion-test-pass "$RUST_DIR" -- \
          cargo bench --bench controlled --bench idiomatic --bench cold -- --test
      else
        run_step rust "$MEASURE_PHASE" criterion-bench "$RUST_DIR" -- \
          cargo bench --bench controlled --bench idiomatic --bench cold -- --noplot
        run_step rust "$MEASURE_PHASE" alloc-report "$RUST_DIR" -- \
          cargo run --release --features alloc-count --bin alloc_report
        # summarize reads heddle-reference.toml; while the Phase 1 Windows reference rows
        # are pending it fails loudly by design -- captured, non-fatal.
        run_step rust "$MEASURE_PHASE" summarize "$RUST_DIR" --non-fatal -- \
          cargo run --release --bin summarize
      fi
      copy_artifacts rust copy-criterion "$RUST_DIR/target/criterion" "$OUT_DIR/rust/criterion"
      ;;
    jvm)
      mkdir -p "$OUT_DIR/jvm"
      RFF="$OUT_DIR/jvm/jmh-result.json"
      # JMH takes a machine-wide lock at ${TMPDIR:-/tmp}/jmh.lock, created by whoever runs
      # first. When this runner is used both as root and as a normal user, a leftover lock
      # owned by the other user kills every later run instantly: fs.protected_regular (=2 on
      # Ubuntu) stops even root from opening a world-writable file it does not own in a
      # sticky /tmp. Clear a stale lock -- but never while a JMH process is actually running,
      # since that is the lock doing its job (two concurrent JMH runs invalidate both).
      JMH_LOCK="${TMPDIR:-/tmp}/jmh.lock"
      if [ -e "$JMH_LOCK" ]; then
        if pgrep -f 'benchmarks\.jar' >/dev/null 2>&1; then
          bench_warn "a JMH run appears to be in progress; leaving $JMH_LOCK alone (this run will refuse to start)"
        elif rm -f "$JMH_LOCK" 2>/dev/null; then
          echo "   cleared stale JMH lock: $JMH_LOCK"
        else
          bench_warn "stale JMH lock $JMH_LOCK owned by $(stat -c %U "$JMH_LOCK" 2>/dev/null || echo '?') and not removable here -- remove it (sudo rm -f $JMH_LOCK) or the JMH step will fail instantly"
        fi
      fi
      if [ "$SMOKE" = "1" ]; then
        run_step jvm "$MEASURE_PHASE" jmh-smoke "$JVM_DIR" -- \
          java -jar target/benchmarks.jar -f 1 -wi 1 -i 1 -w 1s -r 1s -foe true -prof gc -rf json -rff "$RFF"
      else
        echo "${C_YELLOW}NOTE: full JMH run uses the committed annotation regime (Fork 5, 5x10s/5x10s).${C_RESET}"
        echo "${C_YELLOW}      Expect roughly 4.5-5.5 hours unattended (Phase 3 procedure).${C_RESET}"
        run_step jvm "$MEASURE_PHASE" jmh-full "$JVM_DIR" -- \
          java -jar target/benchmarks.jar -prof gc -rf json -rff "$RFF"
      fi
      ;;
    js)
      # Phase 4 shapes via the committed launcher js/run.sh (taskset + nice, node
      # --expose-gc --allow-natives-syntax, stdout captured to artifacts/).
      if [ "$SMOKE" != "1" ]; then
        if [ "$JS_STABILITY_REPEAT" = "1" ]; then
          run_step js "$MEASURE_PHASE" stability-repeat5 "$JS_DIR" -- \
            ./run.sh bench/controlled.mjs --repeat 5 ${PRIORITY_ARGS[@]+"${PRIORITY_ARGS[@]}"}
          echo "${C_YELLOW}NOTE: compute the five-run RSD verdict (Phase 4 D13 thresholds) from${C_RESET}"
          echo "${C_YELLOW}      artifacts/stability/run-*.json before publishing JS numbers.${C_RESET}"
        else
          echo "${C_YELLOW}NOTE: the JS five-run stability procedure (Phase 4 D13) is a separate,${C_RESET}"
          echo "${C_YELLOW}      publication-gating step. Re-run with --js-stability-repeat to execute it.${C_RESET}"
        fi
      fi
      for js_script in controlled idiomatic cold-compile; do
        run_step js "$MEASURE_PHASE" "bench-$js_script" "$JS_DIR" -- \
          ./run.sh "bench/$js_script.mjs" ${PRIORITY_ARGS[@]+"${PRIORITY_ARGS[@]}"}
      done
      copy_artifacts js copy-artifacts "$JS_DIR/artifacts" "$OUT_DIR/js"
      ;;
    python)
      # Phase 5 D8: five pyperf Runner scripts, library defaults, explicit --affinity, JSON
      # outputs. The Windows counterpart of the elevated shell is root/passwordless sudo
      # (pyperf raises the process priority where it can).
      if [ "$(id -u)" != "0" ] && ! sudo -n true 2>/dev/null; then
        echo "${C_YELLOW}WARN: not root and no passwordless sudo -- pyperf's priority elevation${C_RESET}"
        echo "${C_YELLOW}      is skipped silently. A measurement run should provide it (Phase 5 D8).${C_RESET}"
      fi
      PY_AFFINITY=""
      if PY_PAIR="$(bench_smt_pair)" && [ -n "$(bench_isolated_set)" ]; then
        PY_AFFINITY="$PY_PAIR"
        echo "   pyperf affinity: --affinity=$PY_AFFINITY (isolated pair, read from topology)"
      else
        PY_AFFINITY="4"
        echo "${C_YELLOW}   RECORDED: no isolated SMT pair -- falling back to --affinity=4 (the Windows value).${C_RESET}"
      fi
      mkdir -p "$OUT_DIR/python"
      PY_SMOKE_ARGS=()
      [ "$SMOKE" = "1" ] && PY_SMOKE_ARGS=(--debug-single-value)
      for s in bench_jinja2_controlled bench_jinja2_idiomatic bench_mako_controlled bench_mako_idiomatic bench_cold_compile; do
        run_step python "$MEASURE_PHASE" "$s" "$PY_DIR" -- \
          "$VENV_PY" "$s.py" "--affinity=$PY_AFFINITY" ${PY_SMOKE_ARGS[@]+"${PY_SMOKE_ARGS[@]}"} \
          -o "$OUT_DIR/python/$s.json"
      done
      # Memory pass -- tracemalloc, separate from timing (Phase 5 D11).
      MEM_ARGS=()
      [ "$SMOKE" = "1" ] && MEM_ARGS=(--reps 5)
      run_step python "$MEASURE_PHASE" mem-tracemalloc "$PY_DIR" -- \
        "$VENV_PY" mem_tracemalloc.py ${MEM_ARGS[@]+"${MEM_ARGS[@]}"} -o "$OUT_DIR/python/memory.json"
      ;;
    go)
      # Phase 6 reproduce path: the committed run-benchmarks.sh (version asserts, templ
      # freshness, vet, gates, prebuild, timed runs, benchstat).
      GO_ARGS=(./run-benchmarks.sh)
      [ "$SMOKE" = "1" ] && GO_ARGS+=(--count 1 --benchtime 100ms)
      GO_ARGS+=(${PRIORITY_ARGS[@]+"${PRIORITY_ARGS[@]}"})
      run_step go "$MEASURE_PHASE" run-benchmarks "$GO_DIR" -- "${GO_ARGS[@]}"
      copy_artifacts go copy-results "$GO_DIR/results" "$OUT_DIR/go"
      ;;
  esac
done

write_summary
if [ "$FAILED" = "1" ]; then
  echo "${C_RED}RESULT: FAILED (one or more steps exited nonzero -- see summary above).${C_RESET}"
  exit 1
fi
echo "${C_GREEN}RESULT: OK (all steps green; WARN rows, if any, are recorded non-fatal steps).${C_RESET}"
exit 0
