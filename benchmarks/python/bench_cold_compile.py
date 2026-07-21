"""pyperf cold parse/compile bench -- both engines, controlled sources (Phase 5 WI5; D10).

Measures, per workload x engine, the cold template-construction cost on the
controlled-track source text preloaded into a string:

- Jinja2: ``Environment.from_string(source)`` -- parse + compile to Python bytecode,
  no bytecode cache configured;
- Mako: ``Template(text=source)`` -- parse + compile to a Python module, in-memory,
  no ``module_directory``.

No parity gate applies to the timed operation (nothing is rendered); instead the
script asserts once per worker -- at module top level, before any registration -- that
compiling + rendering each controlled source still passes the byte gate, so the cold
pass can never time a non-conformant source (harness.md). Results are per-ecosystem
and non-comparable (Q1.3); benchmark names are ``<engine>/cold-compile/<workload-id>``.

Protocol invocation (elevated shell, from ``benchmarks/python/`` -- harness.md):

    python bench_cold_compile.py --affinity=4 -o results\\cold-compile.json

Spec: docs/spec/cross-stack-benchmarks/phase-5-python/README.md (D10);
      docs/spec/cross-stack-benchmarks/phase-5-python/harness.md (bench script shape).
"""

import jinja2
import mako.template
import pyperf

from runner import data, engines, gates

ENGINES = ["jinja2", "mako"]
WORKLOADS = gates.WORKLOADS

# Controlled-track source texts preloaded into strings before any timing (D10).
sources = {
    (engine, w): (
        engines.TEMPLATES_DIR
        / engine
        / "controlled"
        / engines.template_filename(engine, "controlled", w)
    ).read_text(encoding="utf-8")
    for engine in ENGINES
    for w in WORKLOADS
}

# Hard gate, every worker: compiling + rendering each controlled source (through the
# WI3 engine wiring, which compiles from the same files) must still pass the byte
# gate -- the cold pass can never time a non-conformant source (D7/D10).
for engine in ENGINES:
    for w in WORKLOADS:
        out = engines.render(engines.load(engine, "controlled", w), data.MODELS[w])
        gates.assert_parity(w, out, engine, "cold-compile")
        del out

# One long-lived plain Environment; ``from_string`` compiles fresh every call (no
# loader, hence no template cache; no bytecode cache configured -- D10).
_JINJA2_ENV = jinja2.Environment()


def jinja2_compile(source):
    return _JINJA2_ENV.from_string(source)


def mako_compile(source):
    return mako.template.Template(text=source)


runner = pyperf.Runner()
for w in WORKLOADS:
    runner.bench_func(f"jinja2/cold-compile/{w}", jinja2_compile, sources[("jinja2", w)])
for w in WORKLOADS:
    runner.bench_func(f"mako/cold-compile/{w}", mako_compile, sources[("mako", w)])
