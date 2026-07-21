"""Memory pass -- tracemalloc single-render boundaries, one fresh child per cell
(Phase 5 WI6; README D11; harness.md "Memory pass"). Never pyperf; no timing number is
ever taken in this process, and no memory number in a timing process.

Per engine x track x workload cell the parent spawns one fresh child process (same
interpreter) which: builds the engine object, template, and model; performs one
untimed warm-up render gated exactly as the bench scripts' warm-up gate is (D7:
``assert_parity`` on the controlled track, ``assert_verified`` on the idiomatic
track -- before any tracing starts); then, with tracemalloc active, runs R measured
repetitions with single-render boundaries:

    gc.collect() -> base = traced current -> reset_peak() -> render()
    -> allocated_i = peak - base; retained_i = cur - base -> del out

Output (``-o``, default ``results/memory.json``): per cell
``{"allocated": {"mean","median","std"}, "retained": {"mean","median","std"},
"repetitions": R}`` -- statistics via the ``statistics`` stdlib module, ``std`` =
sample standard deviation. Any cell failure (including a warm-up gate failure) exits 1
and writes no partial memory.json.

Protocol invocation (from ``benchmarks/python/`` -- harness.md):

    python mem_tracemalloc.py -o results\\memory.json

``--reps`` (default 100 -- the D11 R), ``--engine``/``--track``/``--workload`` filters
exist for smoke validation only; the protocol run uses the defaults (all 32 cells,
100 repetitions).
"""

from __future__ import annotations

import argparse
import gc
import json
import statistics
import subprocess
import sys
import tracemalloc
from pathlib import Path

HERE = Path(__file__).resolve().parent
ENGINES = ["jinja2", "mako"]
TRACKS = ["controlled", "idiomatic"]
DEFAULT_REPS = 100  # D11: R = 100 measured repetitions


def _stats(values: list[int]) -> dict:
    return {
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "std": statistics.stdev(values) if len(values) > 1 else 0.0,
    }


def run_cell(engine: str, track: str, workload: str, reps: int) -> dict:
    """Executes one cell's D11 measurement inside this (child) process."""
    from runner import data, engines, gates, verify

    template = engines.load(engine, track, workload)
    ctx = data.MODELS[workload]

    # One untimed warm-up render (populates engine caches), gated exactly as the
    # bench scripts' warm-up gate is (D7) -- BEFORE any tracing starts, so a
    # non-conformant template can never enter the traced measurement loop.
    out = engines.render(template, ctx)
    if track == "controlled":
        gates.assert_parity(workload, out, engine, track)
    else:
        verify.assert_verified(workload, out, engine)
    del out

    gc.collect()
    tracemalloc.start()
    allocated = [0] * reps
    retained = [0] * reps
    for i in range(reps):
        gc.collect()
        base = tracemalloc.get_traced_memory()[0]
        tracemalloc.reset_peak()
        out = engines.render(template, ctx)
        cur, peak = tracemalloc.get_traced_memory()
        allocated[i] = peak - base
        retained[i] = cur - base
        del out
    tracemalloc.stop()

    return {
        "allocated": _stats(allocated),
        "retained": _stats(retained),
        "repetitions": reps,
    }


def child_main(cell: str, reps: int) -> int:
    engine, track, workload = cell.split("/")
    try:
        result = run_cell(engine, track, workload, reps)
    except SystemExit:
        # Gate/verifier failure: its own error-surface message is already on stderr.
        raise
    except Exception as e:  # noqa: BLE001 -- the D11 error surface wants the exception
        print(f"MEM FAIL {cell}: {type(e).__name__}: {e}", file=sys.stderr)
        return 1
    json.dump(result, sys.stdout)
    return 0


def parent_main(args: argparse.Namespace) -> int:
    tracks = [args.track] if args.track else TRACKS
    engines_sel = [args.engine] if args.engine else ENGINES
    from runner import gates  # workload id list only; no corpus read in the parent

    workloads = [args.workload] if args.workload else gates.WORKLOADS

    results: dict[str, dict] = {}
    for track in tracks:
        for engine in engines_sel:
            for workload in workloads:
                cell = f"{engine}/{track}/{workload}"
                # One fresh child process per cell (D11) -- same venv interpreter.
                proc = subprocess.run(
                    [sys.executable, str(HERE / "mem_tracemalloc.py"),
                     "--cell", cell, "--reps", str(args.reps)],
                    capture_output=True, text=True, cwd=str(HERE),
                )
                if proc.stderr:
                    sys.stderr.write(proc.stderr)
                if proc.returncode != 0:
                    print(
                        f"MEM FAIL {cell}: child process exited {proc.returncode}"
                        " -- no memory.json written",
                        file=sys.stderr,
                    )
                    return 1
                try:
                    results[cell] = json.loads(proc.stdout)
                except json.JSONDecodeError as e:
                    print(f"MEM FAIL {cell}: unreadable child output: {e}", file=sys.stderr)
                    return 1
                print(
                    f"[OK] {cell}: allocated mean {results[cell]['allocated']['mean']:.0f} B,"
                    f" retained mean {results[cell]['retained']['mean']:.0f} B"
                    f" ({args.reps} reps)"
                )

    out_path = Path(args.output)
    if not out_path.is_absolute():
        out_path = HERE / out_path
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(f"mem_tracemalloc: {len(results)} cells -> {out_path}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python mem_tracemalloc.py",
        description="Separate tracemalloc memory pass (Phase 5 D11) -- never pyperf.",
    )
    parser.add_argument("-o", "--output", default="results/memory.json",
                        help="output JSON path (default: results/memory.json)")
    parser.add_argument("--reps", type=int, default=DEFAULT_REPS,
                        help=f"measured repetitions per cell (default {DEFAULT_REPS} -- D11;"
                             " lower values are for smoke validation only)")
    parser.add_argument("--engine", choices=ENGINES, help="smoke filter: one engine only")
    parser.add_argument("--track", choices=TRACKS, help="smoke filter: one track only")
    parser.add_argument("--workload", help="smoke filter: one workload only")
    parser.add_argument("--cell", help=argparse.SUPPRESS)  # internal: child mode
    args = parser.parse_args(argv)

    if args.cell:
        return child_main(args.cell, args.reps)
    return parent_main(args)


if __name__ == "__main__":
    sys.exit(main())
