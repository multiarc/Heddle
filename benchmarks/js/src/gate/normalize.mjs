// Contract v2 normalization pipeline (Phase 4 WI3; normative shape in
// docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md §Gate implementation, contract
// in docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md).
//
// Whitespace everywhere below is the contract's explicit six-character closed set
// { TAB, LF, VT, FF, CR, SPACE } — never a language `\s` and never String.prototype.trim()
// (both are Unicode-wide and would strip characters the contract does not).

/** N3 — collapse every whitespace run sitting between `>` and `<` to nothing (single pass;
 * equivalent to repeat-until-fixpoint because the replacement `><` contains no whitespace). */
const INTER_TAG_RUN = />[\t\n\v\f\r ]+</g;

/** N4 — leading/trailing runs of the six-character class only. */
const EDGE_RUN = /^[\t\n\v\f\r ]+|[\t\n\v\f\r ]+$/g;

/** N3b — every whitespace run, anywhere, removed entirely (comparison-time projection). */
const ANY_RUN = /[\t\n\v\f\r ]+/g;

/**
 * N5 — one single left-to-right scan over every recognized spelling of the five
 * markup-significant characters (named entities case-sensitive; numeric references with any
 * number of leading zeros; hex digits and the `x` case-insensitive). Replacement output is
 * never rescanned (String.replace with a /g regex advances past each replacement), so data
 * that escaped to `&amp;#39;` is not double-canonicalized. Any other entity (e.g. `&#8482;`,
 * `&eacute;`, `&#x60;`) is untouched.
 */
const N5_SCAN = /&(?:amp|lt|gt|quot|apos);|&#0*(?:38|60|62|34|39);|&#[xX]0*(?:26|3[cC]|3[eE]|22|27);/g;

const CANONICAL_BY_CODEPOINT = {
  38: "&amp;",
  60: "&lt;",
  62: "&gt;",
  34: "&quot;",
  39: "&#39;",
};

const NAMED_TO_CODEPOINT = {
  "&amp;": 38,
  "&lt;": 60,
  "&gt;": 62,
  "&quot;": 34,
  "&apos;": 39,
};

function canonicalSpelling(match) {
  let codepoint;
  if (match[1] === "#") {
    codepoint =
      match[2] === "x" || match[2] === "X"
        ? parseInt(match.slice(3, -1), 16)
        : parseInt(match.slice(2, -1), 10);
  } else {
    codepoint = NAMED_TO_CODEPOINT[match];
  }
  return CANONICAL_BY_CODEPOINT[codepoint];
}

/** N5 entity canonicalization (encoded suites only). Exported for the security floor. */
export function canonicalizeEntities(text) {
  return text.replace(N5_SCAN, canonicalSpelling);
}

/**
 * N1 well-formedness: the candidate string must contain no lone surrogates (it could not have
 * been produced by decoding valid UTF-8). A leading U+FEFF (BOM) is deliberately NOT stripped
 * anywhere in this pipeline — it survives to the byte comparison and fails it.
 */
export function assertWellFormed(text, context) {
  if (!text.isWellFormed()) {
    throw new Error(
      `[FAIL] ${context}: invalid UTF-8 — candidate output contains a lone surrogate (N1)`,
    );
  }
}

/**
 * The stored-form pipeline N1–N5: well-formedness (N1), line endings (N2), inter-tag collapse
 * (N3), edge trim (N4), and — for `suite === "encoded"` only — entity canonicalization (N5).
 * N3b is NOT applied here; it is the comparison-time projection (stripWhitespace) applied
 * symmetrically to both sides by the gates.
 */
export function normalize(text, suite, context = "candidate") {
  assertWellFormed(text, context);
  let s = text.replaceAll("\r\n", "\n").replaceAll("\r", "\n"); // N2
  s = s.replace(INTER_TAG_RUN, "><"); // N3
  s = s.replace(EDGE_RUN, ""); // N4
  if (suite === "encoded") s = canonicalizeEntities(s); // N5
  return s;
}

/** N3b — remove every whitespace run (six-character class) entirely, anywhere in the text. */
export function stripWhitespace(text) {
  return text.replace(ANY_RUN, "");
}

/** Non-overlapping occurrence count (ordinal), mirroring IdiomaticChecks.CountOccurrences. */
export function countOccurrences(haystack, needle) {
  if (needle.length === 0) return 0;
  let count = 0;
  let index = 0;
  for (;;) {
    index = haystack.indexOf(needle, index);
    if (index < 0) return count;
    count++;
    index += needle.length;
  }
}
