//! `alloc_report` binary — the separate, non-timed allocation pass (README D10, WI8).
//!
//! Built only with `--features alloc-count` (`required-features` in Cargo.toml), so the timed
//! Criterion binaries never link the counting global allocator. Per D10, for each of the 32
//! registered cells this binary renders once as an uncounted warm-up and then reports
//! `count_total` / `bytes_total` for a single counted render. Gates run first (D11 — no
//! number without a green gate, allocation numbers included).
//!
//! Usage: `cargo run --release --features alloc-count --bin alloc_report [-- --out <path>]`
//!
//! Report artifact: `alloc-report.txt` (D13 `## Files`), written next to the crate by default.
//! Until 2026-07-25 this binary only printed to stdout despite documenting that artifact, so the
//! master runners — which copy `target/criterion` and nothing else — had nothing to collect and
//! every published report carried a "Rust allocation report absent" note. It now writes the file
//! *and* keeps stdout, so the step log still shows the table.

use std::io::Write;
use std::path::PathBuf;

use heddle_bench_rust::gates;

fn out_path() -> PathBuf {
    // `--out <path>` overrides; default is alongside the crate so `cargo run` from any cwd lands
    // in a predictable place the runners can copy from.
    let mut args = std::env::args().skip(1);
    while let Some(arg) = args.next() {
        if arg == "--out" {
            match args.next() {
                Some(p) => return PathBuf::from(p),
                None => {
                    eprintln!("alloc_report: --out requires a path");
                    std::process::exit(2);
                }
            }
        }
    }
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("alloc-report.txt")
}

fn main() {
    // D11: parity before any number. Panics with the Diagnostics message on a red cell.
    gates::assert_all();

    let mut out = String::new();
    out.push_str("# alloc-report — allocation-counter 0.8.1, one counted render per cell (D10)\n");
    out.push_str("# Per-ecosystem figures only; Tera is the baseline. Allocation counts are NOT\n");
    out.push_str("# comparable across ecosystems (metrics-protocol metric rule 2).\n\n");
    out.push_str(&format!(
        "{:<11} {:<7} {:<21} {:>12} {:>14}\n",
        "track", "engine", "workload", "count_total", "bytes_total"
    ));
    out.push_str(&"-".repeat(11 + 1 + 7 + 1 + 21 + 1 + 12 + 1 + 14));
    out.push('\n');

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

        out.push_str(&format!(
            "{:<11} {:<7} {:<21} {:>12} {:>14}\n",
            cell.track, cell.engine, cell.workload, info.count_total, info.bytes_total
        ));
    }

    // stdout keeps the step log readable; the file is what the runners copy into the run dir.
    print!("{out}");
    let _ = std::io::stdout().flush();

    let path = out_path();
    match std::fs::write(&path, out.as_bytes()) {
        Ok(()) => println!("\nalloc-report written to {}", path.display()),
        Err(err) => {
            eprintln!("alloc_report: could not write {}: {err}", path.display());
            std::process::exit(1);
        }
    }
}
