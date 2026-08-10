#!/usr/bin/env bash
# run-dotnet.sh — .NET protocol suite launcher (anchor rows first: every ratio
# column needs the Linux Heddle rows this suite produces).
# Same invocation as the Windows protocol run: Release, net10.0, the harness's own ShortRun
# default, MemoryDiagnoser via the suite attributes, no affinity, no custom job.
# BenchmarkDotNet's own priority behavior is left unmodified; its warning lines are part of the
# captured log.
#
# usage: ./run-dotnet.sh [--smoke] [--no-tune-check]

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "dotnet (cross-stack .NET leg)" 2>&1 | tee "$LCX_OUT_DIR/run-dotnet.log"

# cwd MUST be the harness directory: BenchmarkDotNet writes its artifacts relative to the current
# directory, and that is where report assembly copies them from. (Template and corpus paths resolve by walking
# up from the assembly location, so those work from anywhere; the artifacts do not.)
cd "$LCX_REPO_ROOT/benchmarks/dotnet"

# Gates first: the ecosystem's own commands, unmodified.
lcx_note "gate: cell registry" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
dotnet run -c Release --project . -- gate 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
lcx_note "gate: selftest" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
dotnet run -c Release --project . -- selftest 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
lcx_note "gate: verify-corpus" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
dotnet run -c Release --project . -- verify-corpus 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"

# The cross-stack sweep, then the three sidebars. Verbs rather than one bare `--filter '*'`: the
# sweep's row set is normative and must not silently acquire whatever else carries a [Benchmark].
LCX_DOTNET_VERBS=(bench-crossstack bench-techniques bench-cold bench-internal)

if [ "$LCX_SMOKE" = "1" ]; then
  # Functional pass only: BenchmarkDotNet Dry job (1 launch, 1 iteration, no validity).
  lcx_note "SMOKE: BenchmarkDotNet --job Dry across every suite (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
  for verb in "${LCX_DOTNET_VERBS[@]}"; do
    dotnet run -c Release --project . -- "$verb" --job Dry 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
  done
else
  # Measurement: every suite at the harness defaults, matching the Windows runs.
  for verb in "${LCX_DOTNET_VERBS[@]}"; do
    dotnet run -c Release --project . -- "$verb" 2>&1 | tee -a "$LCX_OUT_DIR/run-dotnet.log"
  done
fi
lcx_note "dotnet launcher done; artifacts under benchmarks/dotnet/BenchmarkDotNet.Artifacts (copied to the report dir at assembly)"
