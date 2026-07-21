//! Read-only access to the Phase 1 golden corpus at
//! `src/Heddle.Performance/GoldenCorpus/`, resolved repo-relative from
//! `CARGO_MANIFEST_DIR` (README D2 — Phase 1 D6's revisit trigger is not fired; a relative
//! read is convenient). Formats: `golden-corpus.md` (Phase 1).

use std::path::{Path, PathBuf};

use serde::Deserialize;

use crate::verifier::VerifyDef;

/// `benchmarks/rust/../../src/Heddle.Performance/GoldenCorpus/`.
pub fn corpus_dir() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("../../src/Heddle.Performance/GoldenCorpus")
}

/// Loads `<id>.golden.html` — the normalized oracle (stored N1–N5 form, UTF-8 no BOM, no
/// trailing newline). Error strings carry the Diagnostics table's corpus-failure shapes.
pub fn load_golden(workload: &str) -> Result<String, String> {
    let path = corpus_dir().join(format!("{workload}.golden.html"));
    let bytes = std::fs::read(&path).map_err(|e| {
        format!(
            "[FAIL] corpus {workload}: cannot read {}: {e} (regenerate via the Phase 1 \
             export-corpus tool: dotnet run -c Release --project src/Heddle.Performance \
             -f net10.0 -- export-corpus)",
            path.display()
        )
    })?;
    String::from_utf8(bytes)
        .map_err(|_| format!("[FAIL] corpus {workload}: golden is not valid UTF-8"))
}

/// Loads and parses `<id>.verify.json` — the idiomatic-verifier definition.
pub fn load_verify(workload: &str) -> Result<VerifyDef, String> {
    let path = corpus_dir().join(format!("{workload}.verify.json"));
    let bytes = std::fs::read(&path).map_err(|e| {
        format!(
            "[FAIL] corpus {workload}: cannot read {}: {e} (regenerate via the Phase 1 \
             export-corpus tool: dotnet run -c Release --project src/Heddle.Performance \
             -f net10.0 -- export-corpus)",
            path.display()
        )
    })?;
    serde_json::from_slice(&bytes)
        .map_err(|e| format!("[FAIL] corpus {workload}: cannot parse verify.json: {e}"))
}

// ---- manifest --------------------------------------------------------------------------------

#[derive(Deserialize)]
pub struct Manifest {
    #[serde(rename = "$schema")]
    pub schema: String,
    pub generator: String,
    pub entries: Vec<ManifestEntry>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ManifestEntry {
    pub workload: String,
    pub suite: String,
    pub file: String,
    pub byte_length: u64,
    pub hash: String,
    pub generating_commit: String,
    pub generated_utc: String,
}

/// Loads and parses `manifest.json`.
pub fn load_manifest() -> Result<Manifest, String> {
    let path = corpus_dir().join("manifest.json");
    let bytes = std::fs::read(&path).map_err(|e| {
        format!(
            "[FAIL] corpus manifest: cannot read {}: {e}",
            path.display()
        )
    })?;
    serde_json::from_slice(&bytes)
        .map_err(|e| format!("[FAIL] corpus manifest: cannot parse manifest.json: {e}"))
}

/// The eight workload ids in workload-number order (Phase 1 workloads.md).
pub const WORKLOADS: [&str; 8] = [
    "composed-page",
    "trivial-substitution",
    "large-loop",
    "mixed-page",
    "conditional-heavy",
    "fragment-heavy",
    "fortunes-encoded",
    "encoded-loop",
];

// ---- tests -----------------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn all_eight_goldens_load_as_utf8() {
        for id in WORKLOADS {
            let golden = load_golden(id).unwrap_or_else(|e| panic!("{e}"));
            assert!(!golden.is_empty(), "{id} golden must be non-empty");
        }
    }

    #[test]
    fn all_eight_verify_definitions_load() {
        for id in WORKLOADS {
            let def = load_verify(id).unwrap_or_else(|e| panic!("{e}"));
            assert_eq!(def.workload, id);
            assert!(
                def.suite == "raw" || def.suite == "encoded",
                "{id}: unexpected suite {}",
                def.suite
            );
            assert!(!def.values.is_empty(), "{id}: values must be non-empty");
            assert!(!def.markers.is_empty(), "{id}: markers must be non-empty");
        }
    }

    #[test]
    fn manifest_matches_committed_files() {
        let manifest = load_manifest().unwrap_or_else(|e| panic!("{e}"));
        assert_eq!(manifest.entries.len(), 8);
        let ordered: Vec<&str> = manifest
            .entries
            .iter()
            .map(|e| e.workload.as_str())
            .collect();
        assert_eq!(ordered, WORKLOADS);
        for entry in &manifest.entries {
            let bytes = std::fs::read(corpus_dir().join(&entry.file))
                .unwrap_or_else(|e| panic!("{}: {e}", entry.file));
            assert_eq!(
                bytes.len() as u64,
                entry.byte_length,
                "{}: byteLength mismatch",
                entry.workload
            );
            assert!(
                entry.hash.starts_with("sha256:"),
                "{}: hash shape",
                entry.workload
            );
        }
    }
}
