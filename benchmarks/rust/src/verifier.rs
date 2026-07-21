//! Idiomatic-track verifier — the Rust port of parity-contract-v2 §Idiomatic-track gate,
//! consuming the exported `<id>.verify.json` definitions. Matching semantics: the candidate
//! is normalized (N1–N4, +N5 for encoded suites), then the N3b whitespace strip is applied
//! to the output AND to every needle before matching; `values` are exact non-overlapping
//! counts, `markers` are strictly ordered, `forbidden` must be absent from both raw and
//! normalized output, `required` is a minimum count.

use serde::Deserialize;

use crate::normalize::{n3b_strip, normalize_for_suite};

// ---- serde model of `<id>.verify.json` (golden-corpus.md §Idiomatic verifier definitions) ----

#[derive(Deserialize)]
pub struct VerifyDef {
    pub workload: String,
    pub suite: String,
    pub values: Vec<ValueCheck>,
    pub markers: Vec<String>,
    pub forbidden: Vec<String>,
    pub required: Vec<RequiredCheck>,
}

#[derive(Deserialize)]
pub struct ValueCheck {
    pub text: String,
    pub count: usize,
}

#[derive(Deserialize)]
pub struct RequiredCheck {
    pub text: String,
    #[serde(rename = "minCount")]
    pub min_count: usize,
}

// ---- failure surface (README §Diagnostics) ---------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FailureKind {
    Value,
    Marker,
    Forbidden,
    Required,
}

impl FailureKind {
    pub fn label(self) -> &'static str {
        match self {
            FailureKind::Value => "value",
            FailureKind::Marker => "marker",
            FailureKind::Forbidden => "forbidden",
            FailureKind::Required => "required",
        }
    }
}

/// One failed check: its kind plus the Diagnostics-table message tail
/// (`expected <n> of "<needle>", found <m>` / `marker "<needle>" not found after index <i>`).
#[derive(Debug)]
pub struct Failure {
    pub kind: FailureKind,
    pub message: String,
}

// ---- verifier --------------------------------------------------------------------------------

/// Runs the verifier against a candidate's raw output. Empty result = accepted.
pub fn verify(def: &VerifyDef, raw_output: &str) -> Vec<Failure> {
    let normalized = normalize_for_suite(raw_output, &def.suite);
    let stripped = n3b_strip(&normalized);
    let stripped_raw = n3b_strip(raw_output);
    let mut failures = Vec::new();

    for v in &def.values {
        let found = count_occurrences(&stripped, &n3b_strip(&v.text));
        if found != v.count {
            failures.push(Failure {
                kind: FailureKind::Value,
                message: format!(
                    "expected {} of \"{}\", found {found}",
                    v.count,
                    excerpt(&v.text)
                ),
            });
        }
    }

    let mut pos = 0;
    for marker in &def.markers {
        let needle = n3b_strip(marker);
        match find_from(&stripped, &needle, pos) {
            Some(at) => pos = at + needle.len(),
            None => {
                // Keep scanning subsequent markers from the current position so every
                // out-of-order/missing marker is reported.
                failures.push(Failure {
                    kind: FailureKind::Marker,
                    message: format!("marker \"{}\" not found after index {pos}", excerpt(marker)),
                });
            }
        }
    }

    for f in &def.forbidden {
        let needle = n3b_strip(f);
        let found =
            count_occurrences(&stripped_raw, &needle).max(count_occurrences(&stripped, &needle));
        if found != 0 {
            failures.push(Failure {
                kind: FailureKind::Forbidden,
                message: format!("expected 0 of \"{}\", found {found}", excerpt(f)),
            });
        }
    }

    for r in &def.required {
        let found = count_occurrences(&stripped, &n3b_strip(&r.text));
        if found < r.min_count {
            failures.push(Failure {
                kind: FailureKind::Required,
                message: format!(
                    "expected {} of \"{}\", found {found}",
                    r.min_count,
                    excerpt(&r.text)
                ),
            });
        }
    }

    failures
}

/// Non-overlapping occurrence count (byte-wise; needles and haystack are both stripped).
pub fn count_occurrences(haystack: &str, needle: &str) -> usize {
    if needle.is_empty() {
        return 0;
    }
    let mut count = 0;
    let mut index = 0;
    while let Some(at) = find_from(haystack, needle, index) {
        count += 1;
        index = at + needle.len();
    }
    count
}

fn find_from(haystack: &str, needle: &str, from: usize) -> Option<usize> {
    if from > haystack.len() {
        return None;
    }
    haystack
        .as_bytes()
        .windows(needle.len())
        .skip(from)
        .position(|w| w == needle.as_bytes())
        .map(|p| p + from)
}

fn excerpt(s: &str) -> String {
    let truncated: String = if s.chars().count() <= 48 {
        s.to_string()
    } else {
        let mut t: String = s.chars().take(48).collect();
        t.push('\u{2026}');
        t
    };
    truncated.replace('\n', "\\n")
}

// ---- tests -----------------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;
    use crate::corpus::{WORKLOADS, load_golden, load_verify};

    // ---- corruption synthesis (golden-corpus.md §Verification rules) ------------------------

    /// 'removed row': delete the first occurrence of the workload's pinned segment.
    fn remove_first(golden: &str, segment: &str) -> String {
        let at = golden
            .find(segment)
            .unwrap_or_else(|| panic!("removed-segment pin not found: {}", excerpt(segment)));
        format!("{}{}", &golden[..at], &golden[at + segment.len()..])
    }

    /// 'reordered section': swap the first occurrences of A and B (A strictly before B).
    fn swap_first(golden: &str, a: &str, b: &str) -> String {
        let ia = golden
            .find(a)
            .unwrap_or_else(|| panic!("swap pin A not found: {}", excerpt(a)));
        let ib = golden
            .find(b)
            .unwrap_or_else(|| panic!("swap pin B not found: {}", excerpt(b)));
        assert!(
            ib >= ia + a.len(),
            "swap pins must be ordered and non-overlapping"
        );
        format!(
            "{}{}{}{}{}",
            &golden[..ia],
            b,
            &golden[ia + a.len()..ib],
            a,
            &golden[ib + b.len()..]
        )
    }

    /// 'unescaped payload' (encoded only): replace the first escaped form with its raw form.
    fn unescape_first(golden: &str, escaped: &str, raw: &str) -> String {
        assert!(
            golden.contains(escaped),
            "unescape pin not found: {}",
            excerpt(escaped)
        );
        golden.replacen(escaped, raw, 1)
    }

    /// Per-workload calibration pins — the same rules the Phase 1 `verify-corpus` command
    /// uses (`IdiomaticChecks.cs` corruption pins; golden-corpus.md §Verification).
    struct Pins {
        workload: &'static str,
        removed_segment: &'static str,
        removed_kind: FailureKind,
        swap_a: &'static str,
        swap_b: &'static str,
        /// (escaped, raw) — encoded workloads only.
        unescape: Option<(&'static str, &'static str)>,
    }

    const ENCODED_LOOP_ROW_0: &str = "<tr><td data-tag=\"tag-0&amp;&#39;0&#39;\">item &lt;0&gt; &amp; &quot;co&quot;</td><td>&#39;q&#39; &amp; &lt;angle&gt; &quot;d&quot; こんにちは 0</td></tr>";

    fn pins() -> [Pins; 8] {
        [
            Pins {
                workload: "composed-page",
                removed_segment: "<meta property=\"og:image\" content=\"/files/catalog/img.jpg\">",
                removed_kind: FailureKind::Marker,
                swap_a: "<title>Title</title>",
                swap_b: "<meta property=\"og:image\" content=\"/files/catalog/img.jpg\">",
                unescape: None,
            },
            Pins {
                workload: "trivial-substitution",
                removed_segment: "HB-2001",
                removed_kind: FailureKind::Value,
                swap_a: "class=\"sku\"",
                swap_b: "class=\"rating\"",
                unescape: None,
            },
            Pins {
                workload: "large-loop",
                removed_segment: "<tr><td>row-0</td><td>0</td></tr>",
                removed_kind: FailureKind::Value,
                swap_a: "<tr><td>row-0</td><td>0</td></tr>",
                swap_b: "<tr><td>row-2500</td><td>2500</td></tr>",
                unescape: None,
            },
            Pins {
                workload: "mixed-page",
                removed_segment: "<article class=\"card\">",
                removed_kind: FailureKind::Value,
                swap_a: "<header>",
                swap_b: "class=\"hero\"",
                unescape: None,
            },
            Pins {
                workload: "conditional-heavy",
                removed_segment: "unit-000",
                removed_kind: FailureKind::Value,
                swap_a: "unit-000",
                swap_b: "unit-100",
                unescape: None,
            },
            Pins {
                workload: "fragment-heavy",
                removed_segment: "tile-00",
                removed_kind: FailureKind::Value,
                swap_a: "tile-00",
                swap_b: "tile-24",
                unescape: None,
            },
            Pins {
                workload: "fortunes-encoded",
                removed_segment: "<tr><td>1</td><td>A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1</td></tr>",
                removed_kind: FailureKind::Value,
                swap_a: "<tr><th>id</th><th>message</th></tr>",
                swap_b: "フレームワークのベンチマーク",
                unescape: Some(("&lt;script&gt;alert(", "<script>alert(")),
            },
            Pins {
                workload: "encoded-loop",
                removed_segment: ENCODED_LOOP_ROW_0,
                removed_kind: FailureKind::Value,
                swap_a: "tag-0&amp;&#39;0&#39;",
                swap_b: "item &lt;2500&gt;",
                // Phase 1 pin: the workload carries no script payload; its escaped->raw
                // corruption is the comment's angle text (forbidden: `<angle>`).
                unescape: Some(("&lt;angle&gt;", "<angle>")),
            },
        ]
    }

    fn assert_rejected_with(def: &VerifyDef, corrupted: &str, kind: FailureKind, label: &str) {
        let failures = verify(def, corrupted);
        assert!(
            !failures.is_empty(),
            "{}/{label}: corruption must be rejected",
            def.workload
        );
        assert!(
            failures.iter().any(|f| f.kind == kind),
            "{}/{label}: expected a {} failure, got: {:?}",
            def.workload,
            kind.label(),
            failures
        );
    }

    #[test]
    fn verifier_accepts_all_eight_committed_goldens() {
        for id in WORKLOADS {
            let def = load_verify(id).unwrap_or_else(|e| panic!("{e}"));
            let golden = load_golden(id).unwrap_or_else(|e| panic!("{e}"));
            let failures = verify(&def, &golden);
            assert!(
                failures.is_empty(),
                "{id}: golden must be accepted, got: {failures:?}"
            );
        }
    }

    #[test]
    fn verifier_rejects_removed_row_corruptions() {
        for p in pins() {
            let def = load_verify(p.workload).unwrap_or_else(|e| panic!("{e}"));
            let golden = load_golden(p.workload).unwrap_or_else(|e| panic!("{e}"));
            let corrupted = remove_first(&golden, p.removed_segment);
            assert_rejected_with(&def, &corrupted, p.removed_kind, "removed-row");
        }
    }

    #[test]
    fn verifier_rejects_reordered_section_corruptions() {
        for p in pins() {
            let def = load_verify(p.workload).unwrap_or_else(|e| panic!("{e}"));
            let golden = load_golden(p.workload).unwrap_or_else(|e| panic!("{e}"));
            let corrupted = swap_first(&golden, p.swap_a, p.swap_b);
            assert_rejected_with(&def, &corrupted, FailureKind::Marker, "reordered-section");
        }
    }

    #[test]
    fn verifier_rejects_unescaped_payload_corruptions() {
        for p in pins() {
            let Some((escaped, raw)) = p.unescape else {
                continue; // raw workloads carry no escaped payload (two corruptions only)
            };
            let def = load_verify(p.workload).unwrap_or_else(|e| panic!("{e}"));
            let golden = load_golden(p.workload).unwrap_or_else(|e| panic!("{e}"));
            let corrupted = unescape_first(&golden, escaped, raw);
            assert_rejected_with(
                &def,
                &corrupted,
                FailureKind::Forbidden,
                "unescaped-payload",
            );
        }
    }

    #[test]
    fn count_occurrences_is_non_overlapping() {
        assert_eq!(count_occurrences("aaaa", "aa"), 2);
        assert_eq!(count_occurrences("abcabc", "abc"), 2);
        assert_eq!(count_occurrences("abc", ""), 0);
        assert_eq!(count_occurrences("abc", "zz"), 0);
    }
}
