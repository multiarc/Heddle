#!/usr/bin/env bash
# run-jvm.sh — JMH launcher (Phase 8 spec D8; Windows source: Phase 3 harness-and-jmh).
# Annotation regime lives in the sources (Fork 5, 5x10s / 5x10s, Threads 1, no jvmArgs)
# and carries verbatim; this launcher performs the identical invocation:
#   ./mvnw -q clean verify   (gates wired into verify — D12)
#   java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json | tee jmh-log.txt
# Temurin 25 linux-x64 per the toolchain table; the JMH '# VM version' line is the
# recorded runtime identity. The DCE plausibility pass runs on the results before
# publication (operator step, Phase 3 procedure).
#
# usage: ./run-jvm.sh [--smoke] [--no-tune-check]
#   --smoke: JMH command-line override -f 1 -wi 1 -i 1 -w 1s -r 1s (functional only)

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "jvm (JMH, D8)" 2>&1 | tee "$LCX_OUT_DIR/run-jvm.log"

cd "$LCX_REPO_ROOT/benchmarks/jvm"

# Build + gates (D12: GateCli calibrate/gate are wired into verify).
lcx_note "gate + build: ./mvnw -q clean verify" 2>&1 | tee -a "$LCX_OUT_DIR/run-jvm.log"
./mvnw -q clean verify 2>&1 | tee -a "$LCX_OUT_DIR/run-jvm.log"

RESULT_JSON="$LCX_OUT_DIR/jmh-result.json"
LOG_TXT="$LCX_OUT_DIR/jmh-log.txt"

if [ "$LCX_SMOKE" = "1" ]; then
  lcx_note "SMOKE: JMH -f 1 -wi 1 -i 1 -w 1s -r 1s (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-jvm.log"
  java -jar target/benchmarks.jar -f 1 -wi 1 -i 1 -w 1s -r 1s -prof gc -rf json -rff "$RESULT_JSON" | tee "$LOG_TXT"
else
  # Measurement (Phase 3 verbatim; annotation regime governs shape). ~4.5-5.5 h unattended.
  java -jar target/benchmarks.jar -prof gc -rf json -rff "$RESULT_JSON" | tee "$LOG_TXT"
fi
lcx_note "jvm launcher done; jmh-result.json + jmh-log.txt under $LCX_OUT_DIR"
