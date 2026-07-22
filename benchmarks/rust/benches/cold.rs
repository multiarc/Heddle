//! Tera cold-parse bench (WI7, README D12). Custom main (`harness = false`): calls
//! `assert_all()` first (cold numbers are also numbers — D11), then times exactly one thing:
//! constructing fresh `Tera` instances and registering the same template sets the three
//! runtime instances hold (all 21 Tera template files across both tracks — the full parse
//! set). Reported as "Tera cold parse (all templates)" in the per-ecosystem report section,
//! labeled non-comparable; Askama (like Heddle) has no runtime parse step and is disclosed as
//! build-time in the report. Artifact path: `target/criterion/cold/tera-parse-all-templates/`.

use std::time::Duration;

use criterion::Criterion;
use heddle_bench_rust::engines::{tera_controlled, tera_idiomatic};
use heddle_bench_rust::gates;

fn main() {
    // D11: parity before timing — all 32 cells, before any Criterion construction.
    gates::assert_all();

    // D9 config; the trailing `.configure_from_args()` is mandatory and last.
    let mut criterion = Criterion::default()
        .warm_up_time(Duration::from_secs(3))
        .measurement_time(Duration::from_secs(10))
        .sample_size(100)
        .confidence_level(0.95)
        .configure_from_args();

    let mut group = criterion.benchmark_group("cold");
    group.bench_function("tera-parse-all-templates", |b| {
        b.iter(|| {
            // The same three instances the runtime holds: controlled-raw (8 files),
            // controlled-encoded (2), idiomatic (11) — 21 template files parsed fresh.
            (
                tera_controlled::build_fresh_raw(),
                tera_controlled::build_fresh_encoded(),
                tera_idiomatic::build_fresh(),
            )
        })
    });
    group.finish();

    criterion.final_summary();
}
