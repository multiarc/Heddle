# Python benchmark harness (Phase 5)

The Python leg of the cross-stack template-engine survey: **Jinja2 3.1.6** and
**Mako 1.3.12** rendering the eight Phase 1 workloads on both fairness tracks, measured
with **pyperf 2.10.0**, plus a separate tracemalloc memory pass.

**This directory is an implementation of a spec — the spec is normative:**

- [Phase 5 spec](../../docs/spec/cross-stack-benchmarks/phase-5-python/README.md)
  (decisions D1–D13, work items, error surface)
- [templates.md](../../docs/spec/cross-stack-benchmarks/phase-5-python/templates.md)
  (normative template texts, both engines, both tracks)
- [harness.md](../../docs/spec/cross-stack-benchmarks/phase-5-python/harness.md)
  (directory layout, gate mechanics, run protocol, report instantiation)

Gates run against the Phase 1 golden corpus at
[`src/Heddle.Performance/GoldenCorpus/`](../../src/Heddle.Performance/GoldenCorpus/)
(read-only; every consumed file is SHA-256-verified against `manifest.json`).

## Environment

The measurement run is pinned to **CPython 3.14.6, 64-bit, python.org Windows installer**
(spec D2). Package pins live in [`requirements.txt`](requirements.txt) (`==` only):
Jinja2 3.1.6, Mako 1.3.12, MarkupSafe 3.0.3, pyperf 2.10.0, psutil 7.2.2.

> Development note: the harness was authored and self-tested under CPython 3.13.0 (the
> interpreter available on the authoring machine — an accepted delta); the dedicated
> protocol run must use 3.14.6 per D2.

## Setup

```powershell
cd benchmarks\python

py -3.14 -m venv .venv          # protocol machine; the venv is git-ignored
.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Conformance (all must exit 0 before any measurement)

```powershell
python -m runner.selftest                     # N-step fixtures + verifier calibration
python -m runner.gate_all --track controlled  # 16-cell byte-gate sweep (WI3)
python -m runner.gate_all --track idiomatic   # 16-cell verifier sweep (WI4)
```

## Measurement (elevated PowerShell, machine idle, AC power — harness.md run protocol)

```powershell
python bench_jinja2_controlled.py --affinity=4 -o results\jinja2-controlled.json
python bench_jinja2_idiomatic.py  --affinity=4 -o results\jinja2-idiomatic.json
python bench_mako_controlled.py   --affinity=4 -o results\mako-controlled.json
python bench_mako_idiomatic.py    --affinity=4 -o results\mako-idiomatic.json
python bench_cold_compile.py      --affinity=4 -o results\cold-compile.json

python mem_tracemalloc.py -o results\memory.json   # separate pass, never combined with timing

python -m runner.report_table results
```

`results/` is git-ignored; published copies go to `docs/benchmarks/<date>/`.
