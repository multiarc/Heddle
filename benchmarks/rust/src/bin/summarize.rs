//! `summarize` binary — generates the report's two wall-time tables.
//!
//! Reads every `target/criterion/<group>/<engine>/new/estimates.json` written by the three
//! bench targets, takes `mean.point_estimate` plus the 95% CI bounds, joins the Heddle
//! reference values from the checked-in `heddle-reference.toml`, and prints GitHub-flavored
//! Markdown rows for `docs/benchmarks/<date>/index.md`. Generation (rather than hand
//! transcription) is what makes the tables mechanically faithful to the committed JSON.
//!
//! Two deliberate loud failures, both exit nonzero and print nothing pasteable:
//!   * `pending = true` in `heddle-reference.toml` — no published run supplies the Heddle
//!     reference rows yet, so no table may be emitted.
//!   * any expected cell missing its `estimates.json` — a partial table pasted into a report
//!     reads as a complete one.
//!
//! Usage: `cargo run --release --bin summarize`

use std::collections::BTreeMap;
use std::fs;
use std::path::PathBuf;
use std::process::ExitCode;

use heddle_bench_rust::corpus::WORKLOADS;

const TRACKS: [&str; 2] = ["controlled", "idiomatic"];
const ENGINES: [&str; 2] = ["askama", "tera"];

/// The Tera cold-parse sidebar — one aggregate number, per-ecosystem only.
const COLD_GROUP: &str = "cold";
const COLD_FUNCTION: &str = "tera-parse-all-templates";

// ---- heddle-reference.toml ---------------------------------------------------------------

/// The reference file's schema is fixed and tiny (`pending`, `source-run`, `[workloads]` of
/// `id = <ns>`), so it is parsed here rather than adding a TOML crate to the pinned
/// dependency set.
struct Reference {
    pending: bool,
    source_run: String,
    workloads: BTreeMap<String, f64>,
}

fn parse_reference(text: &str) -> Result<Reference, String> {
    let mut pending = true;
    let mut source_run = String::new();
    let mut workloads = BTreeMap::new();
    let mut in_workloads = false;

    for (lineno, raw) in text.lines().enumerate() {
        let line = raw.split('#').next().unwrap_or("").trim();
        if line.is_empty() {
            continue;
        }
        if line.starts_with('[') {
            in_workloads = line == "[workloads]";
            continue;
        }
        let (key, value) = line
            .split_once('=')
            .ok_or_else(|| format!("heddle-reference.toml:{}: not a key = value line", lineno + 1))?;
        let key = key.trim();
        let value = value.trim().trim_matches('"');

        if in_workloads {
            let ns: f64 = value.parse().map_err(|_| {
                format!("heddle-reference.toml:{}: '{key}' is not a number", lineno + 1)
            })?;
            workloads.insert(key.to_string(), ns);
        } else {
            match key {
                "pending" => pending = value == "true",
                "source-run" => source_run = value.to_string(),
                _ => {}
            }
        }
    }
    Ok(Reference {
        pending,
        source_run,
        workloads,
    })
}

// ---- criterion estimates ------------------------------------------------------------------

struct Estimate {
    mean_ns: f64,
    lower_ns: f64,
    upper_ns: f64,
}

fn criterion_dir() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("target")
        .join("criterion")
}

fn read_estimate(group: &str, function: &str) -> Option<Estimate> {
    let path = criterion_dir()
        .join(group)
        .join(function)
        .join("new")
        .join("estimates.json");
    let text = fs::read_to_string(path).ok()?;
    let value: serde_json::Value = serde_json::from_str(&text).ok()?;
    let mean = value.get("mean")?;
    Some(Estimate {
        mean_ns: mean.get("point_estimate")?.as_f64()?,
        lower_ns: mean
            .get("confidence_interval")?
            .get("lower_bound")?
            .as_f64()?,
        upper_ns: mean
            .get("confidence_interval")?
            .get("upper_bound")?
            .as_f64()?,
    })
}

/// Criterion-native display: the unit Criterion itself would print for that magnitude.
fn fmt_time(ns: f64) -> String {
    if ns < 1_000.0 {
        format!("{ns:.2} ns")
    } else if ns < 1_000_000.0 {
        format!("{:.2} us", ns / 1_000.0)
    } else {
        format!("{:.2} ms", ns / 1_000_000.0)
    }
}

fn fmt_native(e: &Estimate) -> String {
    format!(
        "{} [{}, {}]",
        fmt_time(e.mean_ns),
        fmt_time(e.lower_ns),
        fmt_time(e.upper_ns)
    )
}

fn engine_label(engine: &str) -> &'static str {
    match engine {
        "askama" => "Askama",
        "tera" => "Tera",
        _ => "?",
    }
}

// ---- tables --------------------------------------------------------------------------------

fn render_table(track: &str, reference: &Reference, missing: &mut Vec<String>) -> String {
    let mut out = String::new();
    out.push_str(&format!(
        "### Wall time - {track} track (Criterion mean, 95% CI)\n\n"
    ));
    out.push_str(&format!(
        "Heddle rows are the labeled excerpt from the published run of {} named by \
         `heddle-reference.toml`; they are not re-measured here. Whether \
         that run is on the protocol machine is stated in that file's header - as of 2026-07-25 \
         it is not, and the Windows protocol run is pending a re-test. Ratios are engine / \
         Heddle.\n\n",
        reference.source_run
    ));
    out.push_str("| Workload | Engine | Criterion mean (95% CI) | ns/render | Ratio vs Heddle |\n");
    out.push_str("|---|---|---|---|---|\n");

    for workload in WORKLOADS {
        let heddle_ns = reference.workloads.get(workload).copied();
        match heddle_ns {
            Some(ns) => out.push_str(&format!(
                "| {workload} | Heddle (reference - .NET 10, same machine, from {} run) | - | {ns:.2} | - |\n",
                reference.source_run
            )),
            None => {
                missing.push(format!("heddle-reference.toml: no value for '{workload}'"));
                out.push_str(&format!(
                    "| {workload} | Heddle (reference - .NET 10, same machine) | - | MISSING | - |\n"
                ));
            }
        }

        for engine in ENGINES {
            let group = format!("{track}-{workload}");
            match read_estimate(&group, engine) {
                Some(estimate) => {
                    let ratio = match heddle_ns {
                        Some(ns) if ns > 0.0 => format!("{:.2}x", estimate.mean_ns / ns),
                        _ => "-".to_string(),
                    };
                    out.push_str(&format!(
                        "| {workload} | {} | {} | {:.2} | {ratio} |\n",
                        engine_label(engine),
                        fmt_native(&estimate),
                        estimate.mean_ns
                    ));
                }
                None => {
                    missing.push(format!("target/criterion/{group}/{engine}/new/estimates.json"));
                    out.push_str(&format!(
                        "| {workload} | {} | MISSING | MISSING | - |\n",
                        engine_label(engine)
                    ));
                }
            }
        }
    }
    out.push('\n');
    out
}

fn main() -> ExitCode {
    let reference_path = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("heddle-reference.toml");
    let text = match fs::read_to_string(&reference_path) {
        Ok(text) => text,
        Err(err) => {
            eprintln!("summarize: cannot read {}: {err}", reference_path.display());
            return ExitCode::FAILURE;
        }
    };
    let reference = match parse_reference(&text) {
        Ok(reference) => reference,
        Err(message) => {
            eprintln!("summarize: {message}");
            return ExitCode::FAILURE;
        }
    };

    if reference.pending {
        eprintln!("summarize: no published Heddle reference run exists yet.");
        eprintln!(
            "summarize: {} carries pending = true, so the Heddle reference rows do not exist yet",
            reference_path.display()
        );
        eprintln!("summarize: and NO table is emitted - tables without the anchor row would be");
        eprintln!("summarize: unpublishable, and inventing Heddle numbers is forbidden.");
        eprintln!("summarize: when that run is published: set pending = false, fill source-run");
        eprintln!("summarize: and the eight [workloads] entries from its report, then re-run.");
        return ExitCode::FAILURE;
    }

    let mut missing = Vec::new();
    let mut document = String::new();
    for track in TRACKS {
        document.push_str(&render_table(track, &reference, &mut missing));
    }

    // Cold-parse sidebar: one aggregate Tera number, per-ecosystem only, non-comparable.
    match read_estimate(COLD_GROUP, COLD_FUNCTION) {
        Some(estimate) => {
            document.push_str("### Tera cold parse (all templates)\n\n");
            document.push_str(&format!(
                "{} - one aggregate parse of all 21 Tera template files. Per-ecosystem only, \
                 not comparable across ecosystems or engines: Askama (like Heddle) has no \
                 runtime parse step, it is compiled into the binary at build time.\n\n",
                fmt_native(&estimate)
            ));
        }
        None => eprintln!(
            "summarize: note - no cold-parse estimate at target/criterion/{COLD_GROUP}/{COLD_FUNCTION}/new/estimates.json \
             (run `cargo bench --bench cold`); the cold-parse sidebar is omitted."
        ),
    }

    if !missing.is_empty() {
        eprintln!("summarize: {} required input(s) missing:", missing.len());
        for item in &missing {
            eprintln!("summarize:   {item}");
        }
        eprintln!("summarize: run the full `cargo bench --bench controlled --bench idiomatic --bench cold`");
        eprintln!("summarize: (and fill heddle-reference.toml) before pasting any table.");
        print!("{document}");
        return ExitCode::FAILURE;
    }

    print!("{document}");
    ExitCode::SUCCESS
}
