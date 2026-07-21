"""Controlled-track gate runner -- parity contract v2 (N1-N5), manifest SHA-256
verification, byte gate with first-diff excerpt, and the encoded-suite security floor.

Implements the contract's closed list exactly, nothing else (Phase 5 README D7;
harness.md "Gate runner mechanics"; Phase 1 parity-contract-v2.md). *Whitespace* is
everywhere the closed six-character set ``{TAB, LF, VT, FF, CR, SPACE}`` spelled as the
explicit class ``[\\t\\n\\x0b\\x0c\\r ]`` -- never ``\\s`` (Unicode-wide in Python) and
never ``str.strip()``'s default (also Unicode-wide).
"""

from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

# ---- corpus access (harness.md: repo root = parents[3] of this file) --------------------------

REPO_ROOT = Path(__file__).resolve().parents[3]
CORPUS_DIR = REPO_ROOT / "src" / "Heddle.Performance" / "GoldenCorpus"

#: The eight workload ids in workload-number order (Phase 1 workloads.md).
WORKLOADS = [
    "composed-page",
    "trivial-substitution",
    "large-loop",
    "mixed-page",
    "conditional-heavy",
    "fragment-heavy",
    "fortunes-encoded",
    "encoded-loop",
]

_manifest_cache: dict[str, dict] | None = None
_golden_cache: dict[str, str] = {}


def manifest_entries() -> dict[str, dict]:
    """Loads ``manifest.json`` once and returns its entries keyed by workload id."""
    global _manifest_cache
    if _manifest_cache is None:
        path = CORPUS_DIR / "manifest.json"
        try:
            raw = path.read_bytes()
        except OSError as e:
            print(f"CORPUS FAIL manifest: cannot read {path}: {e}", file=sys.stderr)
            raise SystemExit(1) from None
        manifest = json.loads(raw.decode("utf-8"))
        _manifest_cache = {entry["workload"]: entry for entry in manifest["entries"]}
    return _manifest_cache


def suite_of(workload: str) -> str:
    """``"raw"`` or ``"encoded"`` per the manifest entry."""
    return manifest_entries()[workload]["suite"]


def load_golden(workload: str) -> str:
    """Loads ``<id>.golden.html``, verifying its SHA-256 (and byteLength) against
    ``manifest.json`` before use -- a mismatch means the checkout is stale or corrupt
    and aborts (D7)."""
    if workload in _golden_cache:
        return _golden_cache[workload]
    entry = manifest_entries().get(workload)
    if entry is None:
        print(
            f"CORPUS FAIL {workload}: no manifest.json entry for this workload",
            file=sys.stderr,
        )
        raise SystemExit(1)
    path = CORPUS_DIR / entry["file"]
    try:
        raw = path.read_bytes()
    except OSError as e:
        print(f"CORPUS FAIL {workload}: cannot read {path}: {e}", file=sys.stderr)
        raise SystemExit(1) from None
    digest = "sha256:" + hashlib.sha256(raw).hexdigest()
    if digest != entry["hash"] or len(raw) != entry["byteLength"]:
        print(
            f"CORPUS FAIL {workload}: sha256 mismatch vs manifest.json"
            " — checkout is stale or corrupt",
            file=sys.stderr,
        )
        raise SystemExit(1)
    # N1: strict UTF-8; the stored oracle is UTF-8 without BOM.
    _golden_cache[workload] = raw.decode("utf-8")
    return _golden_cache[workload]


# ---- normalization pipeline (contract v2, the closed list) ------------------------------------

#: The contract's closed six-character whitespace set (as a str.strip argument).
WHITESPACE = "\t\n\x0b\x0c\r "

_N3_RE = re.compile(r">[\t\n\x0b\x0c\r ]+<")
_N3B_RE = re.compile(r"[\t\n\x0b\x0c\r ]+")

# N5 -- the full closed recognized-spelling alternation (harness.md, verbatim): named
# entities case-sensitive; numeric references with any number of leading zeros,
# case-insensitive hex digits and `x`.
_N5 = re.compile(
    r"&(?:"
    r"amp|#0*38|#[xX]0*26|"
    r"lt|#0*60|#[xX]0*3[cC]|"
    r"gt|#0*62|#[xX]0*3[eE]|"
    r"quot|#0*34|#[xX]0*22|"
    r"apos|#0*39|#[xX]0*27"
    r");"
)

_N5_CANONICAL = {0x26: "&amp;", 0x3C: "&lt;", 0x3E: "&gt;", 0x22: "&quot;", 0x27: "&#39;"}
_N5_NAMED = {"amp": 0x26, "lt": 0x3C, "gt": 0x3E, "quot": 0x22, "apos": 0x27}


def _n5_replacement(match: re.Match) -> str:
    body = match.group(0)[1:-1]  # between '&' and ';'
    if body.startswith("#"):
        code = int(body[2:], 16) if body[1] in "xX" else int(body[1:], 10)
    else:
        code = _N5_NAMED[body]
    return _N5_CANONICAL[code]


def n1_decode(data: bytes) -> str:
    """N1 -- strict UTF-8 decode; a BOM is NOT stripped (it survives to the comparison
    and fails it). Invalid UTF-8 is a gate failure (ValueError propagates to the caller's
    failure surface)."""
    return data.decode("utf-8")  # deliberately not "utf-8-sig"


def n2_line_endings(s: str) -> str:
    """N2 -- every CRLF becomes LF, then every remaining CR becomes LF."""
    return s.replace("\r\n", "\n").replace("\r", "\n")


def n3_inter_tag(s: str) -> str:
    """N3 -- collapse every whitespace run between ``>`` and ``<`` to nothing. A single
    pass is sufficient: the replacement ``><`` contains no whitespace, so no new match
    can form."""
    return _N3_RE.sub("><", s)


def n3b_strip(s: str) -> str:
    """N3b -- remove every whitespace run, anywhere, to nothing (the 2026-07-20
    maintainer comparison step; applied to BOTH sides at comparison, never stored)."""
    return _N3B_RE.sub("", s)


def n4_trim(s: str) -> str:
    """N4 -- trim leading/trailing whitespace of exactly the six-character set
    (never ``str.strip()``'s Unicode-wide default -- a leading U+00A0 must survive)."""
    return s.strip(WHITESPACE)


def n5_canonicalize(s: str) -> str:
    """N5 (encoded suites only) -- one left-to-right scan, non-overlapping, output never
    rescanned; only the five markup-significant characters' spellings are canonicalized
    (``&#43;`` and friends pass through untouched)."""
    return _N5.sub(_n5_replacement, s)


def normalize(s: str, suite: str) -> str:
    """The stored-form pipeline: N2 -> N3 -> N4 (+ N5 when ``suite == "encoded"``).
    N1 is inherent for ``str`` input; use :func:`n1_decode` for bytes."""
    s = n4_trim(n3_inter_tag(n2_line_endings(s)))
    if suite == "encoded":
        s = n5_canonicalize(s)
    return s


# ---- byte comparison (first-diff index + excerpt, the ParityCheck.Describe shape) -------------


def describe_diff(expected: bytes, actual: bytes) -> str:
    """First differing byte index, expected/actual lengths, and a +/-40-char excerpt
    window from each side, ``\\n``-escaped."""
    limit = min(len(expected), len(actual))
    index = next(
        (i for i in range(limit) if expected[i] != actual[i]),
        limit if len(expected) != len(actual) else -1,
    )
    if index < 0:
        return "no difference"

    def excerpt(data: bytes) -> str:
        window = data[max(0, index - 40): index + 40]
        return window.decode("utf-8", errors="replace").replace("\n", "\\n")

    return (
        f"first diff at byte {index} (expected {len(expected)} bytes, got {len(actual)})\n"
        f"  expected: ...{excerpt(expected)}...\n"
        f"  actual:   ...{excerpt(actual)}..."
    )


# ---- security floor (encoded suites; contract v2 controlled gate item 5) ----------------------

RAW_PAYLOAD = "<script>alert("
ESCAPED_PAYLOAD = "&lt;script&gt;alert("

#: Expected escaped-payload counts in normalized encoded output: the payload string
#: appears only in the fortunes model.
EXPECTED_ESCAPED_COUNTS = {"fortunes-encoded": 1, "encoded-loop": 0}


def check_security_floor(workload: str, raw_output: str, normalized: str) -> str | None:
    """Raw ``<script>alert(`` must occur 0 times in the UN-normalized output; the escaped
    form must occur the expected count in the normalized output. Returns a failure
    message or ``None``."""
    raw_hits = raw_output.count(RAW_PAYLOAD)
    if raw_hits != 0:
        return f'raw "{RAW_PAYLOAD}" found {raw_hits} times (expected 0)'
    expected = EXPECTED_ESCAPED_COUNTS[workload]
    escaped_hits = normalized.count(ESCAPED_PAYLOAD)
    if escaped_hits != expected:
        return f'escaped "{ESCAPED_PAYLOAD}" found {escaped_hits} times (expected {expected})'
    return None


# ---- the controlled gate ----------------------------------------------------------------------


def check_parity(workload: str, raw_output: str) -> str | None:
    """Runs the full controlled gate for one cell; returns a failure message or ``None``.

    Pipeline: normalize the candidate (N1-N5), then apply N3b to BOTH the normalized
    candidate and the loaded oracle, UTF-8-encode, and compare the non-whitespace bytes.
    Encoded suites additionally pass the security floor.
    """
    suite = suite_of(workload)
    golden = load_golden(workload)
    normalized = normalize(raw_output, suite)
    actual = n3b_strip(normalized).encode("utf-8")
    expected = n3b_strip(golden).encode("utf-8")
    if actual != expected:
        return describe_diff(expected, actual)
    if suite == "encoded":
        failure = check_security_floor(workload, raw_output, normalized)
        if failure is not None:
            return "SECURITY: " + failure
    return None


def assert_parity(workload: str, raw_output: str, engine: str = "?", track: str = "controlled") -> None:
    """Hard gate: prints the README error-surface message and exits 1 on any failure.
    Bench scripts call this at module top level, before any ``bench_func`` registration,
    so pyperf emits nothing for a failed suite (D7)."""
    failure = check_parity(workload, raw_output)
    if failure is None:
        return
    cell = f"{engine}/{track}/{workload}"
    if failure.startswith("SECURITY: "):
        print(f"SECURITY FAIL {cell}: {failure[len('SECURITY: '):]}", file=sys.stderr)
    else:
        print(f"GATE FAIL {cell}: {failure}", file=sys.stderr)
    raise SystemExit(1)
