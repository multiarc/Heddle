#!/usr/bin/env bash
# run-jvm.sh — JMH launcher.
# Annotation regime lives in the sources (Fork 3, 1x2s / 3x1s, Threads 1, no jvmArgs —
# the short budget; the 'baseline' budget adds -wi 2 -i 9)
# and carries verbatim; this launcher performs the identical invocation:
#   ./mvnw -q clean verify   (gates wired into verify)
#   java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json | tee jmh-log.txt
# Temurin 25 linux-x64 per the toolchain table; the JMH '# VM version' line is the
# recorded runtime identity. The DCE plausibility pass runs on the results before
# publication (operator step).
#
# usage: ./run-jvm.sh [--smoke] [--no-tune-check]
#   --smoke: JMH command-line override -f 1 -wi 1 -i 1 -w 1s -r 1s (functional only)

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "jvm (JMH)" 2>&1 | tee "$LCX_OUT_DIR/run-jvm.log"

cd "$LCX_REPO_ROOT/benchmarks/jvm"

# Build + gates (GateCli calibrate/gate are wired into verify).
lcx_note "gate + build: ./mvnw -q clean verify" 2>&1 | tee -a "$LCX_OUT_DIR/run-jvm.log"
./mvnw -q clean verify 2>&1 | tee -a "$LCX_OUT_DIR/run-jvm.log"

RESULT_JSON="$LCX_OUT_DIR/jmh-result.json"
LOG_TXT="$LCX_OUT_DIR/jmh-log.txt"

if [ "$LCX_SMOKE" = "1" ]; then
  lcx_note "SMOKE: JMH -f 1 -wi 1 -i 1 -w 1s -r 1s (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-jvm.log"
  java -jar target/benchmarks.jar -f 1 -wi 1 -i 1 -w 1s -r 1s -prof gc -rf json -rff "$RESULT_JSON" | tee "$LOG_TXT"
else
  # Measurement: the sources' annotation regime governs the shape, identical to the
  # Windows invocation. ~9 min at the short budget.
  java -jar target/benchmarks.jar -prof gc -rf json -rff "$RESULT_JSON" | tee "$LOG_TXT"
fi
lcx_note "jvm launcher done; jmh-result.json + jmh-log.txt under $LCX_OUT_DIR"
