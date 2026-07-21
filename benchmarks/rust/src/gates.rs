//! Parity gates (WI6) — the cell registry and the `assert_controlled` / `assert_idiomatic` /
//! `assert_all` surface per README D11. The controlled gate per cell: load the golden →
//! render → normalize (N1–N4, +N5 for encoded) → N3b-strip both sides → byte compare;
//! encoded cells additionally assert the security floor (raw `<script>alert(` = 0 in
//! un-normalized output; post-N5 `&lt;script&gt;alert(` count per the verifier's `required`
//! entry). The idiomatic gate runs the `verifier.rs` port over `<id>.verify.json`. Failure
//! messages follow README §Diagnostics.

use crate::corpus;
use crate::engines::{askama_controlled, askama_idiomatic, tera_controlled, tera_idiomatic};
use crate::normalize::{n3b_strip, normalize_for_suite};
use crate::verifier;

// ---- cell registry ---------------------------------------------------------------------------

/// One gated cell: workload id × engine × track → render fn + gate kind (the track selects
/// the gate; the suite selects N5 + the security floor).
#[derive(Clone, Copy)]
pub struct Cell {
    pub track: &'static str,    // "controlled" | "idiomatic"
    pub engine: &'static str,   // "askama" | "tera"
    pub workload: &'static str, // Phase 1 workload id
    pub suite: &'static str,    // "raw" | "encoded"
    pub render: fn() -> String,
}

const fn cell(
    track: &'static str,
    engine: &'static str,
    workload: &'static str,
    suite: &'static str,
    render: fn() -> String,
) -> Cell {
    Cell {
        track,
        engine,
        workload,
        suite,
        render,
    }
}

/// The registry — all 32 cells: the 16 controlled cells (WI4) and the 16 idiomatic cells
/// (WI5). Rows are ordered workload-major (Phase 1 workload order), engine-minor
/// (askama, tera).
pub const CELLS: &[Cell] = &[
    // ---- controlled track (16 cells, WI4) ----------------------------------------------------
    cell(
        "controlled",
        "askama",
        "composed-page",
        "raw",
        askama_controlled::render_composed_page,
    ),
    cell(
        "controlled",
        "tera",
        "composed-page",
        "raw",
        tera_controlled::render_composed_page,
    ),
    cell(
        "controlled",
        "askama",
        "trivial-substitution",
        "raw",
        askama_controlled::render_trivial_substitution,
    ),
    cell(
        "controlled",
        "tera",
        "trivial-substitution",
        "raw",
        tera_controlled::render_trivial_substitution,
    ),
    cell(
        "controlled",
        "askama",
        "large-loop",
        "raw",
        askama_controlled::render_large_loop,
    ),
    cell(
        "controlled",
        "tera",
        "large-loop",
        "raw",
        tera_controlled::render_large_loop,
    ),
    cell(
        "controlled",
        "askama",
        "mixed-page",
        "raw",
        askama_controlled::render_mixed_page,
    ),
    cell(
        "controlled",
        "tera",
        "mixed-page",
        "raw",
        tera_controlled::render_mixed_page,
    ),
    cell(
        "controlled",
        "askama",
        "conditional-heavy",
        "raw",
        askama_controlled::render_conditional_heavy,
    ),
    cell(
        "controlled",
        "tera",
        "conditional-heavy",
        "raw",
        tera_controlled::render_conditional_heavy,
    ),
    cell(
        "controlled",
        "askama",
        "fragment-heavy",
        "raw",
        askama_controlled::render_fragment_heavy,
    ),
    cell(
        "controlled",
        "tera",
        "fragment-heavy",
        "raw",
        tera_controlled::render_fragment_heavy,
    ),
    cell(
        "controlled",
        "askama",
        "fortunes-encoded",
        "encoded",
        askama_controlled::render_fortunes_encoded,
    ),
    cell(
        "controlled",
        "tera",
        "fortunes-encoded",
        "encoded",
        tera_controlled::render_fortunes_encoded,
    ),
    cell(
        "controlled",
        "askama",
        "encoded-loop",
        "encoded",
        askama_controlled::render_encoded_loop,
    ),
    cell(
        "controlled",
        "tera",
        "encoded-loop",
        "encoded",
        tera_controlled::render_encoded_loop,
    ),
    // ---- idiomatic track (16 cells, WI5) -----------------------------------------------------
    cell(
        "idiomatic",
        "askama",
        "composed-page",
        "raw",
        askama_idiomatic::render_composed_page,
    ),
    cell(
        "idiomatic",
        "tera",
        "composed-page",
        "raw",
        tera_idiomatic::render_composed_page,
    ),
    cell(
        "idiomatic",
        "askama",
        "trivial-substitution",
        "raw",
        askama_idiomatic::render_trivial_substitution,
    ),
    cell(
        "idiomatic",
        "tera",
        "trivial-substitution",
        "raw",
        tera_idiomatic::render_trivial_substitution,
    ),
    cell(
        "idiomatic",
        "askama",
        "large-loop",
        "raw",
        askama_idiomatic::render_large_loop,
    ),
    cell(
        "idiomatic",
        "tera",
        "large-loop",
        "raw",
        tera_idiomatic::render_large_loop,
    ),
    cell(
        "idiomatic",
        "askama",
        "mixed-page",
        "raw",
        askama_idiomatic::render_mixed_page,
    ),
    cell(
        "idiomatic",
        "tera",
        "mixed-page",
        "raw",
        tera_idiomatic::render_mixed_page,
    ),
    cell(
        "idiomatic",
        "askama",
        "conditional-heavy",
        "raw",
        askama_idiomatic::render_conditional_heavy,
    ),
    cell(
        "idiomatic",
        "tera",
        "conditional-heavy",
        "raw",
        tera_idiomatic::render_conditional_heavy,
    ),
    cell(
        "idiomatic",
        "askama",
        "fragment-heavy",
        "raw",
        askama_idiomatic::render_fragment_heavy,
    ),
    cell(
        "idiomatic",
        "tera",
        "fragment-heavy",
        "raw",
        tera_idiomatic::render_fragment_heavy,
    ),
    cell(
        "idiomatic",
        "askama",
        "fortunes-encoded",
        "encoded",
        askama_idiomatic::render_fortunes_encoded,
    ),
    cell(
        "idiomatic",
        "tera",
        "fortunes-encoded",
        "encoded",
        tera_idiomatic::render_fortunes_encoded,
    ),
    cell(
        "idiomatic",
        "askama",
        "encoded-loop",
        "encoded",
        askama_idiomatic::render_encoded_loop,
    ),
    cell(
        "idiomatic",
        "tera",
        "encoded-loop",
        "encoded",
        tera_idiomatic::render_encoded_loop,
    ),
];

// ---- gate checks (Result-returning; `assert_*` panic wrappers below) -------------------------

/// The security-floor payload needles (README D11; parity-contract-v2 §Controlled gate).
const RAW_PAYLOAD: &str = "<script>alert(";
const ESCAPED_PAYLOAD: &str = "&lt;script&gt;alert(";

/// Controlled byte gate + (encoded) N5 + security floor. `Ok(())` = the cell passes.
pub fn check_controlled(cell: &Cell) -> Result<(), String> {
    let golden = corpus::load_golden(cell.workload)?;
    let output = (cell.render)();

    if cell.suite == "encoded" {
        check_security_floor(cell, &output)?;
    }

    let normalized = normalize_for_suite(&output, cell.suite);
    let expected = n3b_strip(&golden);
    let actual = n3b_strip(&normalized);
    if expected == actual {
        return Ok(());
    }
    let diff_at = first_diff(expected.as_bytes(), actual.as_bytes());
    Err(format!(
        "[FAIL] controlled {}/{}: byte mismatch (expected {} bytes, actual {}); first diff at {}\n  \
         expected: {}\n  actual:   {}",
        cell.engine,
        cell.workload,
        expected.len(),
        actual.len(),
        diff_at,
        window(&expected, diff_at),
        window(&actual, diff_at),
    ))
}

/// Encoded security floor: raw payload absent from **un-normalized** output; post-N5 escaped
/// payload count equals the verifier's `required` entry (when one exists — fortunes-encoded;
/// encoded-loop carries none and its data contains no payload, so ≥ 0 holds by data).
fn check_security_floor(cell: &Cell, output: &str) -> Result<(), String> {
    if let Some(at) = output.find(RAW_PAYLOAD) {
        return Err(format!(
            "[FAIL] security {}/{}: raw payload found at index {at}",
            cell.engine, cell.workload
        ));
    }
    let def = corpus::load_verify(cell.workload)?;
    if let Some(required) = def.required.iter().find(|r| r.text == ESCAPED_PAYLOAD) {
        let canonical = normalize_for_suite(output, cell.suite);
        let found = verifier::count_occurrences(&canonical, ESCAPED_PAYLOAD);
        if found != required.min_count {
            return Err(format!(
                "[FAIL] security {}/{}: escaped payload count expected {}, found {found}",
                cell.engine, cell.workload, required.min_count
            ));
        }
    }
    Ok(())
}

/// Idiomatic verifier gate (WI5's cells will use it; wired now).
pub fn check_idiomatic(cell: &Cell) -> Result<(), String> {
    let def = corpus::load_verify(cell.workload)?;
    let output = (cell.render)();
    let failures = verifier::verify(&def, &output);
    if failures.is_empty() {
        return Ok(());
    }
    Err(failures
        .iter()
        .map(|f| {
            format!(
                "[FAIL] idiomatic {}/{} verifier {}: {}",
                cell.engine,
                cell.workload,
                f.kind.label(),
                f.message
            )
        })
        .collect::<Vec<_>>()
        .join("\n"))
}

/// Dispatches on the cell's track.
pub fn check_cell(cell: &Cell) -> Result<(), String> {
    match cell.track {
        "controlled" => check_controlled(cell),
        "idiomatic" => check_idiomatic(cell),
        other => Err(format!(
            "[FAIL] {other} {}/{}: unknown track",
            cell.engine, cell.workload
        )),
    }
}

/// Panics with the Diagnostics message on failure — the bench-time gate surface (D11).
pub fn assert_controlled(cell: &Cell) {
    if let Err(message) = check_controlled(cell) {
        panic!("{message}");
    }
}

/// Panics with the Diagnostics message on failure — the bench-time gate surface (D11).
pub fn assert_idiomatic(cell: &Cell) {
    if let Err(message) = check_idiomatic(cell) {
        panic!("{message}");
    }
}

/// Gates every registered cell; panics on the first failure (D11 — no number without a green
/// gate). The standalone `gate` binary iterates `CELLS`/`check_cell` itself to print one line
/// per cell instead of stopping at the first.
pub fn assert_all() {
    for cell in CELLS {
        if let Err(message) = check_cell(cell) {
            panic!("{message}");
        }
    }
}

// ---- diagnostics helpers (README §Diagnostics message shape) ---------------------------------

/// Index of the first differing byte (= common length when one side is a prefix).
fn first_diff(expected: &[u8], actual: &[u8]) -> usize {
    expected
        .iter()
        .zip(actual.iter())
        .position(|(e, a)| e != a)
        .unwrap_or_else(|| expected.len().min(actual.len()))
}

/// A 120-char window around `at` (±40 chars context), `\n`-escaped, clamped to char
/// boundaries.
fn window(s: &str, at: usize) -> String {
    let mut start = at.saturating_sub(40).min(s.len());
    while !s.is_char_boundary(start) {
        start -= 1;
    }
    let mut end = (start + 120).min(s.len());
    while !s.is_char_boundary(end) {
        end += 1;
    }
    s[start..end].replace('\n', "\\n")
}

// ---- tests -----------------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn registry_holds_the_16_controlled_cells() {
        let controlled: Vec<_> = CELLS.iter().filter(|c| c.track == "controlled").collect();
        assert_eq!(controlled.len(), 16);
        for workload in corpus::WORKLOADS {
            for engine in ["askama", "tera"] {
                assert!(
                    controlled
                        .iter()
                        .any(|c| c.workload == workload && c.engine == engine),
                    "missing controlled cell {engine}/{workload}"
                );
            }
        }
        let encoded = controlled.iter().filter(|c| c.suite == "encoded").count();
        assert_eq!(encoded, 4, "exactly the four encoded controlled cells");
    }

    #[test]
    fn all_registered_cells_pass() {
        for cell in CELLS {
            check_cell(cell).unwrap_or_else(|e| panic!("{e}"));
        }
    }

    #[test]
    fn first_diff_and_window_shapes() {
        assert_eq!(first_diff(b"abcd", b"abXd"), 2);
        assert_eq!(first_diff(b"abc", b"abcd"), 3);
        assert_eq!(first_diff(b"", b""), 0);
        assert_eq!(window("short\ntext", 3), "short\\ntext");
        let long = "x".repeat(500);
        assert_eq!(window(&long, 250).len(), 120);
        // Multi-byte safety: a window edge inside a UTF-8 sequence is clamped outward.
        let jp = "こんにちは".repeat(100);
        let w = window(&jp, 250);
        assert!(!w.is_empty());
    }
}
