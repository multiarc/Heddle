//! Idiomatic-track Criterion bench. Custom main (`harness = false`): gates all 16
//! idiomatic cells (verifier semantics) **before** constructing `Criterion`, then
//! times each cell under the shared pinned config. Groups are `idiomatic-<workload-id>`,
//! functions `askama` / `tera`.

use std::time::Duration;

use criterion::Criterion;
use heddle_bench_rust::{corpus, gates};

fn main() {
    // Parity before timing — panic with the diagnostic message before any Criterion
    // construction.
    for cell in gates::CELLS.iter().filter(|c| c.track == "idiomatic") {
        gates::assert_idiomatic(cell);
    }

    // The shared pinned config; the trailing `.configure_from_args()` is mandatory and last.
    let mut criterion = Criterion::default()
        .warm_up_time(Duration::from_secs(5))
        .measurement_time(Duration::from_secs(26))
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
