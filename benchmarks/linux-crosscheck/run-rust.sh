#!/usr/bin/env bash
# run-rust.sh — Criterion launcher (Phase 8 spec D7; Windows source: Phase 2 D9).
# The Criterion builder config (3 s warm-up / 10 s measurement / 100 samples / 0.95 CI /
# configure_from_args) lives in the bench sources and carries byte-identical to Linux;
# this launcher only invokes `cargo bench -- --noplot`. No pinning, no priority (D2).
# The D7 instability trigger (95% CI half-width > 5% of mean -> full-suite taskset re-run)
# is applied by the operator as one recorded environment change, never per-cell.
#
# usage: ./run-rust.sh [--smoke] [--no-tune-check]
#   --smoke overrides the builder via configure_from_args:
#           --warm-up-time 1 --measurement-time 1 --sample-size 10 (functional only)

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"
lcx_parse_launcher_args "$@"
lcx_mkout
lcx_launcher_header "rust (Criterion, D7)" 2>&1 | tee "$LCX_OUT_DIR/run-rust.log"

cd "$LCX_REPO_ROOT/benchmarks/rust"

# Gate first (D12): the ecosystem's own gate binary, unmodified.
lcx_note "gate: cargo run --release --bin gate" 2>&1 | tee -a "$LCX_OUT_DIR/run-rust.log"
cargo run --release --bin gate 2>&1 | tee -a "$LCX_OUT_DIR/run-rust.log"

# Explicit --bench target list: the crate's lib target does not set `bench = false`,
# so a bare `cargo bench -- --noplot` (and even `--benches`, which also selects the
# lib) runs the libtest bench harness over src/lib.rs unittests, which rejects
# Criterion's flags ("error: Unrecognized option: 'noplot'") — found in the WSL
# functional pass (WI5). Phase 2 sources are untouched; the flags only select the
# three Criterion bench targets (harness = false) from Cargo.toml.
BENCH_TARGETS=(--bench controlled --bench idiomatic --bench cold)
if [ "$LCX_SMOKE" = "1" ]; then
  lcx_note "SMOKE: short Criterion pass via configure_from_args (no measurement validity)" 2>&1 | tee -a "$LCX_OUT_DIR/run-rust.log"
  cargo bench "${BENCH_TARGETS[@]}" -- --noplot --warm-up-time 1 --measurement-time 1 --sample-size 10 2>&1 | tee -a "$LCX_OUT_DIR/run-rust.log"
else
  # Measurement (Phase 2 D9 verbatim): builder defaults from source, --noplot only.
  cargo bench "${BENCH_TARGETS[@]}" -- --noplot 2>&1 | tee -a "$LCX_OUT_DIR/run-rust.log"
fi
lcx_note "rust launcher done; estimates.json under target/criterion (copied to the report dir at WI8)"
