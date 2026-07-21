//! Parity-contract-v2 normalization pipeline (N1–N5), hand-rolled per README D5 — no regex
//! dependency. *Whitespace* is exactly the six-character set
//! `{ TAB, LF, VT, FF, CR, SPACE }` (the contract's closed, portable definition — never a
//! language `\s`). N1 (UTF-8 decode) is inherent: engine output is a Rust `String`, and the
//! corpus loader fails on invalid UTF-8 (`corpus.rs`).
//!
//! Contract: `docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md`.

/// The contract's six-character whitespace set — TAB, LF, VT, FF, CR, SPACE.
#[inline]
pub fn is_contract_whitespace(byte: u8) -> bool {
    matches!(byte, b'\t' | b'\n' | 0x0B | 0x0C | b'\r' | b' ')
}

/// N2 — line endings: every `\r\n` becomes `\n`, then every remaining `\r` becomes `\n`.
pub fn n2_line_endings(s: &str) -> String {
    s.replace("\r\n", "\n").replace('\r', "\n")
}

/// N3 — inter-tag whitespace: collapse every run of one-or-more whitespace characters that
/// sits between `>` and `<` to nothing. Single left-to-right scan; equivalent to the
/// contract's repeated-regex definition because the replacement `><` contains no whitespace.
pub fn n3_inter_tag(s: &str) -> String {
    let bytes = s.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        let b = bytes[i];
        out.push(b);
        i += 1;
        if b == b'>' {
            let mut j = i;
            while j < bytes.len() && is_contract_whitespace(bytes[j]) {
                j += 1;
            }
            if j > i && j < bytes.len() && bytes[j] == b'<' {
                i = j; // erase the run; the `<` is copied on the next iteration
            }
        }
    }
    // Only ASCII whitespace bytes were removed, so the result is valid UTF-8.
    String::from_utf8(out).expect("N3 removes only ASCII bytes")
}

/// N3b — comparison-time whitespace-run collapse: remove **every** run of one-or-more
/// whitespace characters, anywhere in the text, to **nothing** (not to a space). Applied
/// symmetrically to both sides of every comparison and to every verifier needle; never baked
/// into the stored oracle (2026-07-20 maintainer step).
pub fn n3b_strip(s: &str) -> String {
    // Removing every whitespace character removes every run entirely.
    let bytes: Vec<u8> = s.bytes().filter(|&b| !is_contract_whitespace(b)).collect();
    String::from_utf8(bytes).expect("N3b removes only ASCII bytes")
}

/// N4 — trim: remove leading and trailing whitespace of exactly the six-character set
/// (**not** Rust's Unicode `str::trim` — a leading U+00A0 must survive).
pub fn n4_trim(s: &str) -> &str {
    s.trim_matches(|c: char| c.is_ascii() && is_contract_whitespace(c as u8))
}

/// N5 — entity canonicalization (encoded suite only): map every recognized spelling of the
/// five markup-significant characters to the canonical spelling, in a single left-to-right
/// scan with non-overlapping matches whose replacements are never rescanned. Numeric
/// references match with any number of leading zeros and case-insensitive hex digits/`x`;
/// named entities match case-sensitively. The spelling of any other character (e.g. `&#43;`)
/// is not canonicalized.
pub fn n5_canonicalize(s: &str) -> String {
    let bytes = s.as_bytes();
    let mut out: Vec<u8> = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'&'
            && let Some((len, replacement)) = match_entity(&bytes[i..])
        {
            match replacement {
                // Recognized spelling of one of the five characters -> canonical.
                Some(canonical) => out.extend_from_slice(canonical.as_bytes()),
                // Recognized numeric reference outside the five-character set: copied
                // untouched (N5 is scoped to exactly the five characters).
                None => out.extend_from_slice(&bytes[i..i + len]),
            }
            i += len;
            continue;
        }
        out.push(bytes[i]);
        i += 1;
    }
    String::from_utf8(out).expect("N5 replaces ASCII entities with ASCII entities")
}

/// Canonical spelling for one of the five markup-significant characters, if `code` is one.
fn canonical_for(code: u32) -> Option<&'static str> {
    match code {
        0x26 => Some("&amp;"),
        0x3C => Some("&lt;"),
        0x3E => Some("&gt;"),
        0x22 => Some("&quot;"),
        0x27 => Some("&#39;"),
        _ => None,
    }
}

/// Attempts to match an entity at the start of `rest` (which begins with `&`). Returns the
/// matched length and the canonical replacement (`None` = recognized numeric reference left
/// untouched); returns `None` overall when no entity is recognized at this position.
fn match_entity(rest: &[u8]) -> Option<(usize, Option<&'static str>)> {
    // Named spellings, matched case-sensitively (the contract's recognized set only).
    const NAMED: [(&[u8], &str); 5] = [
        (b"&amp;", "&amp;"),
        (b"&lt;", "&lt;"),
        (b"&gt;", "&gt;"),
        (b"&quot;", "&quot;"),
        (b"&apos;", "&#39;"),
    ];
    for (spelling, canonical) in NAMED {
        if rest.starts_with(spelling) {
            return Some((spelling.len(), Some(canonical)));
        }
    }

    // Numeric references: `&#digits;` or `&#x/Xhexdigits;`.
    if rest.len() < 4 || rest[1] != b'#' {
        return None;
    }
    let (radix, digits_start) = if rest[2] == b'x' || rest[2] == b'X' {
        (16u32, 3usize)
    } else {
        (10u32, 2usize)
    };
    let mut code: u32 = 0;
    let mut j = digits_start;
    while j < rest.len() {
        let d = (rest[j] as char).to_digit(radix);
        match d {
            Some(d) => {
                code = code.saturating_mul(radix).saturating_add(d);
                j += 1;
            }
            None => break,
        }
    }
    if j == digits_start || j >= rest.len() || rest[j] != b';' {
        return None; // no digits, or unterminated — not a numeric reference
    }
    Some((j + 1, canonical_for(code)))
}

/// The stored-form pipeline for a raw-suite candidate: N2 → N3 → N4 (N1 is inherent).
pub fn normalize_raw(s: &str) -> String {
    n4_trim(&n3_inter_tag(&n2_line_endings(s))).to_string()
}

/// The stored-form pipeline for an encoded-suite candidate: N2 → N3 → N4 → N5.
pub fn normalize_encoded(s: &str) -> String {
    n5_canonicalize(&normalize_raw(s))
}

/// Normalizes per suite (`"encoded"` applies N5; anything else is the raw pipeline).
pub fn normalize_for_suite(s: &str, suite: &str) -> String {
    if suite == "encoded" {
        normalize_encoded(s)
    } else {
        normalize_raw(s)
    }
}

// ---- tests (spec Testing plan — contract-derived vectors) ------------------------------------

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn n2_crlf_and_cr_vectors() {
        assert_eq!(n2_line_endings("a\r\nb"), "a\nb");
        assert_eq!(n2_line_endings("a\rb"), "a\nb");
        assert_eq!(n2_line_endings("a\r\n\rb\r"), "a\n\nb\n");
        assert_eq!(n2_line_endings("a\nb"), "a\nb");
    }

    #[test]
    fn n3_collapses_each_of_the_six_whitespace_chars_between_tags() {
        for ws in ['\t', '\n', '\u{0B}', '\u{0C}', '\r', ' '] {
            let input = format!("<a>{ws}<b>");
            assert_eq!(n3_inter_tag(&input), "<a><b>", "char U+{:04X}", ws as u32);
        }
    }

    #[test]
    fn n3_collapses_multi_char_runs_between_tags() {
        assert_eq!(n3_inter_tag("<a> \t\n\u{0B}\u{0C}\r <b>"), "<a><b>");
        assert_eq!(
            n3_inter_tag("<ul>\n  <li>x</li>\n  </ul>"),
            "<ul><li>x</li></ul>"
        );
    }

    #[test]
    fn n3_leaves_non_inter_tag_whitespace_alone() {
        // Run not between `>` and `<`: N3 territory ends where element text begins.
        assert_eq!(n3_inter_tag("<p>a  b</p>"), "<p>a  b</p>");
        // `>`-whitespace-non-`<` (the `>\n/` boundary from composed-page): untouched by N3.
        assert_eq!(n3_inter_tag("/> \n/* css */"), "/> \n/* css */");
        assert_eq!(n3_inter_tag("a > b < c"), "a > b < c");
    }

    #[test]
    fn n3b_removes_every_whitespace_run_to_nothing() {
        assert_eq!(n3b_strip("a b"), "ab");
        assert_eq!(n3b_strip("a \t\n\u{0B}\u{0C}\r b"), "ab");
        // Run inside element text and at a `>\n/` non-tag boundary.
        assert_eq!(n3b_strip("<p>a  b</p>"), "<p>ab</p>");
        assert_eq!(n3b_strip("/>\n/* css */"), "/>/*css*/");
    }

    #[test]
    fn n3b_presence_vs_absence_compares_equal() {
        // `>␠/` vs `>/` — one side has a run, the other has none; they must compare equal.
        assert_eq!(n3b_strip("> /"), n3b_strip(">/"));
    }

    #[test]
    fn n4_trims_only_the_six_char_set() {
        assert_eq!(n4_trim(" \t\n\u{0B}\u{0C}\r x \t\n\u{0B}\u{0C}\r "), "x");
        // A leading U+00A0 (not in the closed set) survives.
        assert_eq!(n4_trim("\u{00A0}x"), "\u{00A0}x");
        assert_eq!(n4_trim(" \u{00A0}x "), "\u{00A0}x");
    }

    #[test]
    fn n5_contract_table_vectors() {
        assert_eq!(n5_canonicalize("&#x27;"), "&#39;");
        assert_eq!(n5_canonicalize("&#034;"), "&quot;");
        assert_eq!(n5_canonicalize("&#X3C;"), "&lt;");
        assert_eq!(n5_canonicalize("&apos;"), "&#39;");
        assert_eq!(n5_canonicalize("&#38;"), "&amp;");
        assert_eq!(n5_canonicalize("&#60;"), "&lt;");
        assert_eq!(n5_canonicalize("&#62;"), "&gt;");
        assert_eq!(n5_canonicalize("&#34;"), "&quot;");
        // Canonical spellings are identity.
        assert_eq!(
            n5_canonicalize("&amp;&lt;&gt;&quot;&#39;"),
            "&amp;&lt;&gt;&quot;&#39;"
        );
    }

    #[test]
    fn n5_no_rescan_of_replacements() {
        // Data that escaped to `&amp;#39;` (a literal `&#39;` in source data) is not
        // double-canonicalized.
        assert_eq!(n5_canonicalize("&amp;#39;"), "&amp;#39;");
    }

    #[test]
    fn n5_leaves_non_five_char_references_untouched() {
        assert_eq!(n5_canonicalize("&#43;"), "&#43;");
        assert_eq!(n5_canonicalize("a &#x60; b"), "a &#x60; b");
        // Unrecognized named entity and bare ampersand: untouched.
        assert_eq!(n5_canonicalize("&copy; & &#"), "&copy; & &#");
    }

    #[test]
    fn n5_full_askama_family_vector() {
        assert_eq!(
            n5_canonicalize("&#60;script&#62;alert(&#34;x&#34;);"),
            "&lt;script&gt;alert(&quot;x&quot;);"
        );
    }
}
