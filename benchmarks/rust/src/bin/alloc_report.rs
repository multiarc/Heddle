//! `alloc_report` binary — the separate, non-timed allocation pass (README D10, WI8).
//!
//! Built only with `--features alloc-count` (`required-features` in Cargo.toml), so the timed
//! Criterion binaries never link the counting global allocator. Per D10, for each of the 32
//! registered cells this binary renders once as an uncounted warm-up and then reports
//! `count_total` / `bytes_total` for a single counted render. Gates run first (D11 — no
//! number without a green gate, allocation numbers included).
//!
//! Usage: `cargo run --release --features alloc-count --bin alloc_report`
//! Report artifact: `alloc-report.txt` (D13 `## Files`).

use heddle_bench_rust::gates;

fn main() {
    // D11: parity before any number. Panics with the Diagnostics message on a red cell.
    gates::assert_all();

    println!("# alloc-report — allocation-counter 0.8.1, one counted render per cell (D10)");
    println!("# Per-ecosystem figures only; Tera is the baseline. Allocation counts are NOT");
    println!("# comparable across ecosystems (metrics-protocol metric rule 2).");
    println!();
    println!(
        "{:<11} {:<7} {:<21} {:>12} {:>14}",
        "track", "engine", "workload", "count_total", "bytes_total"
    );
    println!("{}", "-".repeat(11 + 1 + 7 + 1 + 21 + 1 + 12 + 1 + 14));

    for cell in gates::CELLS {
        // Warm-up render, deliberately outside the measured closure (lazy one-time work such
        // as Tera's instance construction must not land in the per-render figure).
        let warm = (cell.render)();
        std::hint::black_box(&warm);
        drop(warm);

        let info = allocation_counter::measure(|| {
            let output = (cell.render)();
            std::hint::black_box(&output);
        });

        println!(
            "{:<11} {:<7} {:<21} {:>12} {:>14}",
            cell.track, cell.engine, cell.workload, info.count_total, info.bytes_total
        );
    }
}
