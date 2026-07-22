"""Idiomatic-track verifier -- the Python implementation of parity-contract-v2
"Idiomatic-track gate", driven by the Phase 1 ``<id>.verify.json`` definitions (D12).

Matching semantics: the candidate is normalized (N1-N4, +N5 for encoded suites), then
the N3b whitespace strip is applied to the output AND to every needle string before
matching; ``values`` are exact non-overlapping counts, ``markers`` are strictly ordered,
``forbidden`` must be absent from both the raw and the normalized output, ``required``
is a minimum count. Whitespace is everywhere the closed six-character set (gates.py).
"""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass

from . import gates

_verify_cache: dict[str, dict] = {}


def load_verify(workload: str) -> dict:
    """Loads and parses ``<id>.verify.json``."""
    if workload not in _verify_cache:
        path = gates.CORPUS_DIR / f"{workload}.verify.json"
        try:
            raw = path.read_bytes()
        except OSError as e:
            print(f"CORPUS FAIL {workload}: cannot read {path}: {e}", file=sys.stderr)
            raise SystemExit(1) from None
        _verify_cache[workload] = json.loads(raw.decode("utf-8"))
    return _verify_cache[workload]


@dataclass(frozen=True)
class Failure:
    """One failed check: its kind (``value``/``marker``/``forbidden``/``required``)
    plus the Diagnostics-table message tail."""

    kind: str
    message: str


def _excerpt(s: str) -> str:
    truncated = s if len(s) <= 48 else s[:48] + "…"
    return truncated.replace("\n", "\\n")


def count_occurrences(haystack: str, needle: str) -> int:
    """Non-overlapping occurrence count (``str.count`` is non-overlapping); an empty
    needle counts 0."""
    if not needle:
        return 0
    return haystack.count(needle)


def verify(definition: dict, raw_output: str) -> list[Failure]:
    """Runs the verifier against a candidate's raw output. Empty list = accepted."""
    stripped = gates.n3b_strip(gates.normalize(raw_output, definition["suite"]))
    stripped_raw = gates.n3b_strip(raw_output)
    failures: list[Failure] = []

    for check in definition["values"]:
        needle = gates.n3b_strip(check["text"])
        found = count_occurrences(stripped, needle)
        if found != check["count"]:
            failures.append(Failure(
                "value",
                f'expected {check["count"]} of "{_excerpt(check["text"])}", found {found}',
            ))

    pos = 0
    for marker in definition["markers"]:
        needle = gates.n3b_strip(marker)
        at = stripped.find(needle, pos)
        if at < 0:
            # Keep scanning subsequent markers from the current position so every
            # out-of-order/missing marker is reported.
            failures.append(Failure(
                "marker", f'marker "{_excerpt(marker)}" not found after index {pos}'
            ))
        else:
            pos = at + len(needle)

    for forbidden in definition["forbidden"]:
        needle = gates.n3b_strip(forbidden)
        found = max(
            count_occurrences(stripped_raw, needle),
            count_occurrences(stripped, needle),
        )
        if found != 0:
            failures.append(Failure(
                "forbidden", f'expected 0 of "{_excerpt(forbidden)}", found {found}'
            ))

    for check in definition["required"]:
        needle = gates.n3b_strip(check["text"])
        found = count_occurrences(stripped, needle)
        if found < check["minCount"]:
            failures.append(Failure(
                "required",
                f'expected {check["minCount"]} of "{_excerpt(check["text"])}", found {found}',
            ))

    return failures


def check_verified(workload: str, raw_output: str) -> list[Failure]:
    """Loads the workload's verifier definition and runs it."""
    return verify(load_verify(workload), raw_output)


def assert_verified(workload: str, raw_output: str, engine: str = "?") -> None:
    """Hard gate: prints ``VERIFY FAIL`` lines and exits 1 on any failed check (D7/D12)."""
    failures = check_verified(workload, raw_output)
    if not failures:
        return
    for failure in failures:
        print(
            f"VERIFY FAIL {engine}/{workload} {failure.kind}: {failure.message}",
            file=sys.stderr,
        )
    raise SystemExit(1)
