#!/usr/bin/env bash
# run-dotnet.sh — intra-.NET protocol suite launcher (Phase 8 spec D6; anchor rows first, D13.1).
# Same invocation as the Phase 1 protocol run: Release, net10.0, BenchmarkDotNet defaults,
# MemoryDiagnoser via the suite attributes, no affinity, no custom job. BenchmarkDotNet's own
# priority behavior is left unmodified; its warning lines are part of the captured log (D6).
#
# usage: ./run-dotnet.sh [--smoke] [--no-tune-check]

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "dotnet (intra-.NET suite, D6)" 2>&1 | tee "$LCX_OUT_DIR/run-dotnet.log"

# cwd MUST be the project directory: the harness resolves TestTemplates/ (and the
# corpus paths) against the current directory, exactly as the Windows protocol runs
# did (WI4 finding: launching from the repo root fails with
# "File not found [TestTemplates/home.heddle]").
cd "$LCX_REPO_ROOT/src/Heddle.Performance"

# Gates first (D12): parity + verify-corpus, the ecosystem's own commands, unmodified.
lcx_note "gate: parity" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
dotnet run -c Release --project . -f net10.0 -- parity 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
lcx_note "gate: verify-corpus" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
dotnet run -c Release --project . -f net10.0 -- verify-corpus 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"

if [ "$LCX_SMOKE" = "1" ]; then
  # Functional pass only: BenchmarkDotNet Dry job (1 launch, 1 iteration, no validity).
  lcx_note "SMOKE: BenchmarkDotNet --job Dry across all suites (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
  dotnet run -c Release --project . -f net10.0 -- --filter '*' --job Dry 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
else
  # Measurement: all eight suites, harness defaults (Phase 1 metrics-protocol shape).
  dotnet run -c Release --project . -f net10.0 -- --filter '*' 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
fi
lcx_note "dotnet launcher done; artifacts under src/Heddle.Performance/BenchmarkDotNet.Artifacts (copied to the report dir at WI8)"
