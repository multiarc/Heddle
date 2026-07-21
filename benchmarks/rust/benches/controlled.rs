//! Controlled-track Criterion bench (WI7). Custom main (`harness = false`): gates all 16
//! controlled cells **before** constructing `Criterion` (README D11 — a failed gate produces
//! no `target/criterion` output), then times each cell under the shared D9 config. Groups are
//! `controlled-<workload-id>`, functions `askama` / `tera`, so artifacts land at
//! `target/criterion/controlled-<id>/<engine>/new/estimates.json` (D9/D13).

use std::time::Duration;

use criterion::Criterion;
use heddle_bench_rust::{corpus, gates};

fn main() {
    // D11: parity before timing — panic with the Diagnostics message before any Criterion
    // construction.
    for cell in gates::CELLS.iter().filter(|c| c.track == "controlled") {
        gates::assert_controlled(cell);
    }

    // D9: defaults except measurement_time 5 s -> 10 s; the trailing `.configure_from_args()`
    // is mandatory and last, so `--test` / `--noplot` / name filters take effect while the
    // pinned settings survive when no CLI flag overrides them.
    let mut criterion = Criterion::default()
        .warm_up_time(Duration::from_secs(3))
        .measurement_time(Duration::from_secs(10))
        .sample_size(100)
        .confidence_level(0.95)
        .configure_from_args();

    for workload in corpus::WORKLOADS {
        let mut group = criterion.benchmark_group(format!("controlled-{workload}"));
        for cell in gates::CELLS
            .iter()
            .filter(|c| c.track == "controlled" && c.workload == workload)
        {
            // The rendered String is returned into Criterion's sink: drop cost is inside the
            // measurement for every engine equally (D9).
            group.bench_function(cell.engine, |b| b.iter(|| (cell.render)()));
        }
        group.finish();
    }

    criterion.final_summary();
}
