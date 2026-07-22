"""pyperf render bench -- Jinja2 x controlled track (Phase 5 WI5; README D7/D8).

The controlled byte gate runs at module top level, BEFORE any ``bench_func``
registration; pyperf re-executes this module in every worker process, so the gate
provably runs in the same process that produces every timed value (D7). A gate
failure raises ``SystemExit(1)`` before registration, so pyperf emits nothing for a
failed suite.

Protocol invocation (elevated shell, from ``benchmarks/python/`` -- harness.md):

    python bench_jinja2_controlled.py --affinity=4 -o results\\jinja2-controlled.json

Spec: docs/spec/cross-stack-benchmarks/phase-5-python/harness.md (bench script shape).
"""

import pyperf

from runner import data, engines, gates

ENGINE = "jinja2"
TRACK = "controlled"
WORKLOADS = gates.WORKLOADS

templates = {w: engines.load(ENGINE, TRACK, w) for w in WORKLOADS}
contexts = {w: data.MODELS[w] for w in WORKLOADS}

# Hard gate: every cell, every worker, before any registration (D7).
for w in WORKLOADS:
    out = engines.render(templates[w], contexts[w])
    gates.assert_parity(w, out, ENGINE, TRACK)
    del out

runner = pyperf.Runner()
for w in WORKLOADS:
    # One render = one complete output string from the cached template; the callable
    # takes positional args only (bench_func rejects kwargs -- executed evidence).
    runner.bench_func(f"{ENGINE}/{TRACK}/{w}", engines.render, templates[w], contexts[w])
