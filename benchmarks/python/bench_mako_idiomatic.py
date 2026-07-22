"""pyperf render bench -- Mako x idiomatic track (Phase 5 WI5; README D7/D8).

The idiomatic functional verifier runs at module top level, BEFORE any ``bench_func``
registration; pyperf re-executes this module in every worker process, so the gate
provably runs in the same process that produces every timed value (D7). A verifier
failure raises ``SystemExit(1)`` before registration, so pyperf emits nothing for a
failed suite.

Protocol invocation (elevated shell, from ``benchmarks/python/`` -- harness.md):

    python bench_mako_idiomatic.py --affinity=4 -o results\\mako-idiomatic.json

Spec: docs/spec/cross-stack-benchmarks/phase-5-python/harness.md (bench script shape).
"""

import pyperf

from runner import data, engines, gates, verify

ENGINE = "mako"
TRACK = "idiomatic"
WORKLOADS = gates.WORKLOADS

templates = {w: engines.load(ENGINE, TRACK, w) for w in WORKLOADS}
contexts = {w: data.MODELS[w] for w in WORKLOADS}

# Hard gate: every cell, every worker, before any registration (D7).
for w in WORKLOADS:
    out = engines.render(templates[w], contexts[w])
    verify.assert_verified(w, out, ENGINE)
    del out

runner = pyperf.Runner()
for w in WORKLOADS:
    # One render = one complete output string from the cached template; the callable
    # takes positional args only (bench_func rejects kwargs -- executed evidence).
    runner.bench_func(f"{ENGINE}/{TRACK}/{w}", engines.render, templates[w], contexts[w])
