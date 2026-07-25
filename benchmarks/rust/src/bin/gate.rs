//! `gate` binary — the standalone parity gate (README D11.3, WI6).
//!
//! Runs the same 32-cell registry the bench binaries gate against, but iterates
//! `gates::CELLS` / `gates::check_cell` itself so that one `[PASS]`/`[FAIL]` line is printed
//! per cell instead of stopping at the first failure (`gates::assert_all` panics on cell one).
//! Exits 0 iff all cells pass — the reproduce command's first step, the CI hook, and the
//! source of the report's `gate-report.txt` artifact (D13).
//!
//! Usage: `cargo run --release --bin gate`

use std::process::ExitCode;

use heddle_bench_rust::gates;

fn main() -> ExitCode {
    let total = gates::CELLS.len();
    let mut failed = 0usize;

    for cell in gates::CELLS {
        match gates::check_cell(cell) {
            Ok(()) => println!(
                "[PASS] {} {}/{}",
                cell.track, cell.engine, cell.workload
            ),
            Err(message) => {
                failed += 1;
                // The Result already carries the D-diagnostics `[FAIL] ...` message shape.
                println!("{message}");
            }
        }
    }

    println!();
    println!("gate: {}/{} cells passed", total - failed, total);

    if failed == 0 {
        ExitCode::SUCCESS
    } else {
        eprintln!("gate: {failed} cell(s) FAILED — no number may be published from this tree");
        ExitCode::FAILURE
    }
}
