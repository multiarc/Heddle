//! Idiomatic-track Criterion bench (WI7). Custom main (`harness = false`): gates all 16
//! idiomatic cells (verifier semantics) **before** constructing `Criterion` (README D11), then
//! times each cell under the shared D9 config. Groups are `idiomatic-<workload-id>`, functions
//! `askama` / `tera` (D9/D13).

use std::time::Duration;

use criterion::Criterion;
use heddle_bench_rust::{corpus, gates};

fn main() {
    // D11: parity before timing — panic with the Diagnostics message before any Criterion
    // construction.
    for cell in gates::CELLS.iter().filter(|c| c.track == "idiomatic") {
        gates::assert_idiomatic(cell);
    }

    // D9 config; the trailing `.configure_from_args()` is mandatory and last.
    let mut criterion = Criterion::default()
        .warm_up_time(Duration::from_secs(3))
        .measurement_time(Duration::from_secs(10))
        .sample_size(100)
        .confidence_level(0.95)
        .configure_from_args();

    for workload in corpus::WORKLOADS {
        let mut group = criterion.benchmark_group(format!("idiomatic-{workload}"));
        for cell in gates::CELLS
            .iter()
            .filter(|c| c.track == "idiomatic" && c.workload == workload)
        {
            group.bench_function(cell.engine, |b| b.iter(|| (cell.render)()));
        }
        group.finish();
    }

    criterion.final_summary();
}
