"""Per-track gate sweep -- ``python -m runner.gate_all --track controlled|idiomatic``.

Runs the appropriate gate for every selected cell (engine x workload): the controlled
byte gate (``gates.check_parity``) or the idiomatic verifier (``verify.check_verified``).
This is the CLI the WI3/WI4/WI5 done-when checks invoke; a full sweep must report
16/16 cells PASS per track (2 engines x 8 workloads).

Engine wiring lives in ``runner/engines.py`` (WI3). Until it lands (or for cells it does
not register), affected cells are reported UNREGISTERED -- cleanly, without a traceback --
and the sweep exits non-zero.

Filters: ``--track``, ``--engine``, ``--workload`` (each repeatable-free single values;
defaults sweep everything).
"""

from __future__ import annotations

import argparse
import sys

from . import gates, verify
from .data import MODELS

ENGINES = ["jinja2", "mako"]
TRACKS = ["controlled", "idiomatic"]


def _load_engines():
    """Imports ``runner.engines`` if present (it arrives in WI3)."""
    try:
        from . import engines  # type: ignore[attr-defined]
    except ImportError:
        return None
    return engines


def run_cell(engines_mod, engine: str, track: str, workload: str) -> tuple[str, str]:
    """Gates one cell; returns ``(status, detail)`` where status is
    ``PASS`` / ``FAIL`` / ``UNREGISTERED``."""
    if engines_mod is None:
        return "UNREGISTERED", "runner/engines.py not present (arrives in WI3)"
    try:
        template = engines_mod.load(engine, track, workload)
    except (KeyError, NotImplementedError, FileNotFoundError) as e:
        return "UNREGISTERED", f"cell not registered: {e}"
    output = engines_mod.render(template, MODELS[workload])
    if track == "controlled":
        failure = gates.check_parity(workload, output)
        if failure is not None:
            if failure.startswith("SECURITY: "):
                return "FAIL", f"SECURITY FAIL {engine}/{track}/{workload}: " + failure[10:]
            return "FAIL", f"GATE FAIL {engine}/{track}/{workload}: {failure}"
    else:
        failures = verify.check_verified(workload, output)
        if failures:
            detail = "; ".join(
                f"VERIFY FAIL {engine}/{workload} {f.kind}: {f.message}" for f in failures
            )
            return "FAIL", detail
    return "PASS", ""


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m runner.gate_all",
        description="16-cell gate sweep per track (Phase 5 WI2; consumed by WI3-WI5).",
    )
    parser.add_argument("--track", choices=TRACKS, help="sweep one track only")
    parser.add_argument("--engine", choices=ENGINES, help="sweep one engine only")
    parser.add_argument("--workload", choices=gates.WORKLOADS, help="sweep one workload only")
    args = parser.parse_args(argv)

    tracks = [args.track] if args.track else TRACKS
    engines_sel = [args.engine] if args.engine else ENGINES
    workloads = [args.workload] if args.workload else gates.WORKLOADS

    engines_mod = _load_engines()
    passed = failed = unregistered = 0
    for track in tracks:
        for engine in engines_sel:
            for workload in workloads:
                status, detail = run_cell(engines_mod, engine, track, workload)
                cell = f"{engine}/{track}/{workload}"
                if status == "PASS":
                    passed += 1
                    print(f"[PASS] {cell}")
                elif status == "UNREGISTERED":
                    unregistered += 1
                    print(f"[UNREGISTERED] {cell}: {detail}")
                else:
                    failed += 1
                    print(f"[FAIL] {cell}", file=sys.stderr)
                    print(f"  {detail}", file=sys.stderr)

    total = passed + failed + unregistered
    print(
        f"gate_all: {passed}/{total} cells PASS"
        + (f", {failed} FAIL" if failed else "")
        + (f", {unregistered} unregistered" if unregistered else "")
    )
    return 0 if failed == 0 and unregistered == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
