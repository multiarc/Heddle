//! Controlled-track Criterion bench. Custom main (`harness = false`): gates all 16
//! controlled cells **before** constructing `Criterion` (a failed gate produces
//! no `target/criterion` output), then times each cell under the shared pinned config.
//! Groups are `controlled-<workload-id>`, functions `askama` / `tera`, so artifacts land at
//! `target/criterion/controlled-<id>/<engine>/new/estimates.json`.

use std::time::Duration;

use criterion::Criterion;
use heddle_bench_rust::{corpus, gates};

fn main() {
    // Parity before timing — panic with the diagnostic message before any Criterion
    // construction.
    for cell in gates::CELLS.iter().filter(|c| c.track == "controlled") {
        gates::assert_controlled(cell);
    }

    // Pinned per-engine budget: Criterion defaults except warm_up_time 3 s
    // -> 5 s and measurement_time 5 s -> 26 s; the trailing `.configure_from_args()`
    // is mandatory and last, so `--test` / `--noplot` / name filters take effect while the
    // pinned settings survive when no CLI flag overrides them.
    let mut criterion = Criterion::default()
        .warm_up_time(Duration::from_secs(5))
        .measurement_time(Duration::from_secs(26))
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
            // measurement for every engine equally.
            group.bench_function(cell.engine, |b| b.iter(|| (cell.render)()));
        }
        group.finish();
    }

    criterion.final_summary();
}
