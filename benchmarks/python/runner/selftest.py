"""Verifier/gate conformance calibration -- ``python -m runner.selftest`` (WI2, D12).

Proves this Python implementation is conformant before it gates anything:

1. N-step unit fixtures derived from the contract (BOM survival, the closed six-character
   whitespace class, N3b space-vs-nothing, the full N5 recognized-spelling table incl.
   leading-zero/hex-case variants, the no-rescan rule, non-five-character references
   untouched).
2. Model pins -- the D6 spot counts/values against ``runner.data``.
3. Every committed golden loads (SHA-256-verified against the manifest) and is accepted
   by its verifier; the controlled byte gate accepts each golden as its own candidate.
4. The Phase 1 calibration corruptions are rejected with the correct check kind:
   two per raw workload, three per encoded workload (2x6 + 3x2 = 18).

Exit 0 iff everything passes.
"""

from __future__ import annotations

import sys

from . import gates, verify
from .data import MODELS

_passed = 0
_failed = 0


def _check(ok: bool, label: str) -> None:
    global _passed, _failed
    if ok:
        _passed += 1
    else:
        _failed += 1
        print(f"[FAIL] {label}", file=sys.stderr)


# ---- 1. N-step fixtures -----------------------------------------------------------------------

SIX = ["\t", "\n", "\x0b", "\x0c", "\r", " "]


def run_normalization_fixtures() -> None:
    # N1: strict decode; a BOM is not stripped and fails the comparison.
    _check(gates.n1_decode(b"\xef\xbb\xbfx") == "﻿x", "N1: BOM survives decode")
    bom_candidate = gates.n3b_strip(gates.normalize("﻿<a></a>", "raw"))
    plain = gates.n3b_strip(gates.normalize("<a></a>", "raw"))
    _check(bom_candidate != plain, "N1: BOM survives normalization and fails compare")
    try:
        gates.n1_decode(b"\xff\xfe")
        _check(False, "N1: invalid UTF-8 must fail")
    except (UnicodeDecodeError, ValueError):
        _check(True, "N1: invalid UTF-8 must fail")

    # N2: line endings.
    _check(gates.n2_line_endings("a\r\nb\rc\nd") == "a\nb\nc\nd", "N2: CRLF/CR -> LF")

    # N3: inter-tag collapse -- each of the six characters, and only the six.
    for ws in SIX:
        _check(
            gates.n3_inter_tag(f"<a>{ws}{ws}<b>") == "<a><b>",
            f"N3: collapses U+{ord(ws):04X} between tags",
        )
    _check(
        gates.n3_inter_tag("<a> <b>") == "<a> <b>",
        "N3: U+00A0 is NOT whitespace (closed set, never \\s)",
    )
    _check(
        gates.n3_inter_tag("<a>　<b>") == "<a>　<b>",
        "N3: U+3000 is NOT whitespace (closed set, never \\s)",
    )
    _check(gates.n3_inter_tag("<p>a  b</p>") == "<p>a  b</p>", "N3: element text untouched")
    _check(gates.n3_inter_tag("/> \n/* c */") == "/> \n/* c */", "N3: >-ws-non-< untouched")

    # N3b: every whitespace run removed to NOTHING (not to a space), anywhere.
    _check(gates.n3b_strip("a \t\n\x0b\x0c\r b") == "ab", "N3b: six-char runs -> nothing")
    _check(gates.n3b_strip("> /") == gates.n3b_strip(">/"), "N3b: presence-vs-absence equal")
    _check(gates.n3b_strip("a b") == "ab", "N3b: space removed, not collapsed to space")
    _check(gates.n3b_strip("a b") == "a b", "N3b: U+00A0 survives (closed set)")

    # N4: trim of exactly the six-character set (never str.strip()'s Unicode default).
    _check(gates.n4_trim(" \t\n\x0b\x0c\r x \t\n\x0b\x0c\r ") == "x", "N4: six-char trim")
    _check(gates.n4_trim(" x　") == " x　", "N4: Unicode whitespace survives")

    # N5: the full closed recognized-spelling table -> canonical.
    table = {
        "&amp;": "&amp;", "&#38;": "&amp;", "&#038;": "&amp;", "&#x26;": "&amp;",
        "&#X26;": "&amp;", "&#x026;": "&amp;",
        "&lt;": "&lt;", "&#60;": "&lt;", "&#060;": "&lt;", "&#x3c;": "&lt;",
        "&#x3C;": "&lt;", "&#X3C;": "&lt;", "&#x03c;": "&lt;",
        "&gt;": "&gt;", "&#62;": "&gt;", "&#062;": "&gt;", "&#x3e;": "&gt;",
        "&#x3E;": "&gt;", "&#X3E;": "&gt;",
        "&quot;": "&quot;", "&#34;": "&quot;", "&#034;": "&quot;", "&#0034;": "&quot;",
        "&#x22;": "&quot;", "&#X22;": "&quot;",
        "&apos;": "&#39;", "&#39;": "&#39;", "&#039;": "&#39;", "&#x27;": "&#39;",
        "&#X27;": "&#39;",
    }
    for spelling, canonical in table.items():
        _check(
            gates.n5_canonicalize(spelling) == canonical,
            f"N5: {spelling} -> {canonical}",
        )
    # The one rewrite this cast actually fires (MarkupSafe's quote spelling).
    _check(
        gates.n5_canonicalize('<td data-x="&#34;q&#34;">') == '<td data-x="&quot;q&quot;">',
        "N5: MarkupSafe &#34; -> &quot; in context",
    )
    # Single pass, no rescan: data that escaped to `&amp;#39;` stays put.
    _check(gates.n5_canonicalize("&amp;#39;") == "&amp;#39;", "N5: no rescan of replacements")
    # Non-five-character references and unrecognized entities pass through untouched.
    for untouched in ["&#43;", "&#x60;", "&#X3D;", "&copy;", "&AMP;", "&#3;", "& ", "&#"]:
        _check(
            gates.n5_canonicalize(untouched) == untouched,
            f"N5: {untouched!r} untouched",
        )
    # Full hex-family vector (the Askama-family spellings).
    _check(
        gates.n5_canonicalize("&#60;script&#62;alert(&#34;x&#34;);") ==
        "&lt;script&gt;alert(&quot;x&quot;);",
        "N5: full decimal-family vector",
    )


# ---- 2. Model pins (D6 spot counts and values) ------------------------------------------------


def run_model_pins() -> None:
    _check(sorted(MODELS) == sorted(gates.WORKLOADS), "data: eight workload ids")
    _check(len(MODELS["mixed-page"]["products"]) == 36, "data: 36 products")
    _check(
        sum(1 for p in MODELS["mixed-page"]["products"] if p["on_sale"]) == 12,
        "data: 12 products on sale",
    )
    _check(len(MODELS["conditional-heavy"]["rows"]) == 200, "data: 200 conditional rows")
    _check(
        sum(1 for r in MODELS["conditional-heavy"]["rows"] if r["is_active"]) == 160,
        "data: 160 active rows",
    )
    _check(len(MODELS["fragment-heavy"]["items"]) == 48, "data: 48 tiles")
    _check(len(MODELS["fortunes-encoded"]["rows"]) == 12, "data: 12 fortunes")
    _check(len(MODELS["large-loop"]["items"]) == 5000, "data: 5000 loop rows")
    _check(len(MODELS["encoded-loop"]["items"]) == 5000, "data: 5000 encoded rows")
    _check(
        MODELS["fortunes-encoded"]["rows"][10]["message"] ==
        '<script>alert("This should not be displayed in a browser alert box.");</script>',
        "data: row-11 XSS payload byte-exact",
    )
    _check(
        MODELS["fortunes-encoded"]["rows"][11]["message"] == "フレームワークのベンチマーク",
        "data: row-12 Japanese string",
    )
    _check(MODELS["encoded-loop"]["items"][0]["tag"] == "tag-0&'0'", "data: encoded tag-0")
    _check(
        MODELS["encoded-loop"]["items"][0]["comment"] == "'q' & <angle> \"d\" こんにちは 0",
        "data: encoded comment 0",
    )
    _check(MODELS["trivial-substitution"]["rating"] == "4.8", "data: rating is the string 4.8")
    _check(
        MODELS["composed-page"]["section"]["meta"] == "<title>Title</title>",
        "data: section meta",
    )
    _check(
        MODELS["composed-page"]["areas"]["Alert Top Section Below Nav"] == "",
        "data: area-6 is the empty entry",
    )


# ---- 3./4. Corpus acceptance + corruption calibration -----------------------------------------

# 'removed row': delete the first occurrence of the workload's pinned segment.
# 'reordered section': swap the first occurrences of A and B (A strictly before B).
# 'unescaped payload' (encoded only): replace the first escaped form with its raw form.
# Pins mirror the Phase 1 verify-corpus rules (golden-corpus.md "Verification";
# IdiomaticChecks corruption pins).

_ENCODED_LOOP_ROW_0 = (
    '<tr><td data-tag="tag-0&amp;&#39;0&#39;">item &lt;0&gt; &amp; &quot;co&quot;</td>'
    "<td>&#39;q&#39; &amp; &lt;angle&gt; &quot;d&quot; こんにちは 0</td></tr>"
)

#: (workload, removed_segment, removed_kind, swap_a, swap_b, (escaped, raw) or None)
CALIBRATION_PINS = [
    (
        "composed-page",
        '<meta property="og:image" content="/files/catalog/img.jpg">',
        "marker",
        "<title>Title</title>",
        '<meta property="og:image" content="/files/catalog/img.jpg">',
        None,
    ),
    ("trivial-substitution", "HB-2001", "value", 'class="sku"', 'class="rating"', None),
    (
        "large-loop",
        "<tr><td>row-0</td><td>0</td></tr>",
        "value",
        "<tr><td>row-0</td><td>0</td></tr>",
        "<tr><td>row-2500</td><td>2500</td></tr>",
        None,
    ),
    ("mixed-page", '<article class="card">', "value", "<header>", 'class="hero"', None),
    ("conditional-heavy", "unit-000", "value", "unit-000", "unit-100", None),
    ("fragment-heavy", "tile-00", "value", "tile-00", "tile-24", None),
    (
        "fortunes-encoded",
        "<tr><td>1</td><td>A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1</td></tr>",
        "value",
        "<tr><th>id</th><th>message</th></tr>",
        "フレームワークのベンチマーク",
        ("&lt;script&gt;alert(", "<script>alert("),
    ),
    (
        "encoded-loop",
        _ENCODED_LOOP_ROW_0,
        "value",
        "tag-0&amp;&#39;0&#39;",
        "item &lt;2500&gt;",
        # Phase 1 pin: this workload carries no script payload; its escaped->raw
        # corruption is the comment's angle text (forbidden: `<angle>`).
        ("&lt;angle&gt;", "<angle>"),
    ),
]


def _remove_first(golden: str, segment: str) -> str:
    at = golden.find(segment)
    assert at >= 0, f"removed-segment pin not found: {segment[:48]}"
    return golden[:at] + golden[at + len(segment):]


def _swap_first(golden: str, a: str, b: str) -> str:
    ia = golden.find(a)
    ib = golden.find(b)
    assert ia >= 0 and ib >= ia + len(a), "swap pins must exist, ordered, non-overlapping"
    return golden[:ia] + b + golden[ia + len(a):ib] + a + golden[ib + len(b):]


def _unescape_first(golden: str, escaped: str, raw: str) -> str:
    assert escaped in golden, f"unescape pin not found: {escaped}"
    return golden.replace(escaped, raw, 1)


def _check_rejected(workload: str, corrupted: str, kind: str, label: str) -> None:
    failures = verify.check_verified(workload, corrupted)
    if not failures:
        _check(False, f"{workload} calibration: corruption '{label}' was NOT rejected")
    elif not any(f.kind == kind for f in failures):
        _check(
            False,
            f"{workload} calibration: '{label}' expected a {kind} failure,"
            f" got {[f.kind for f in failures]}",
        )
    else:
        _check(True, f"{workload} calibration: {label} rejected as {kind}")


def run_calibration() -> None:
    corruptions = 0
    for workload in gates.WORKLOADS:
        golden = gates.load_golden(workload)  # SHA-256-verified against the manifest
        # The verifier accepts its golden.
        failures = verify.check_verified(workload, golden)
        _check(
            not failures,
            f"{workload}: golden must be accepted, got {[(f.kind, f.message) for f in failures]}",
        )
        # The controlled byte gate accepts the golden as its own candidate.
        _check(
            gates.check_parity(workload, golden) is None,
            f"{workload}: byte gate accepts the golden as candidate",
        )

    for workload, removed, removed_kind, swap_a, swap_b, unescape in CALIBRATION_PINS:
        golden = gates.load_golden(workload)
        _check_rejected(
            workload, _remove_first(golden, removed), removed_kind, "removed-row"
        )
        corruptions += 1
        _check_rejected(
            workload, _swap_first(golden, swap_a, swap_b), "marker", "reordered-section"
        )
        corruptions += 1
        if unescape is not None:
            escaped, raw = unescape
            _check_rejected(
                workload,
                _unescape_first(golden, escaped, raw),
                "forbidden",
                "unescaped-payload",
            )
            corruptions += 1
    _check(corruptions == 18, f"calibration: 18 corruptions exercised (got {corruptions})")


def main() -> int:
    run_normalization_fixtures()
    run_model_pins()
    run_calibration()
    total = _passed + _failed
    status = "OK" if _failed == 0 else "FAILED"
    print(f"selftest {status}: {_passed}/{total} checks passed"
          f" (8 goldens accepted, 18 corruptions rejected)" if _failed == 0
          else f"selftest {status}: {_failed}/{total} checks failed")
    return 0 if _failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
