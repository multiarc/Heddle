# Python harness, measurement passes, and report instantiation

Supplementary document of the [Phase 5 — python spec](README.md). It pins the harness layout,
the gate-runner mechanics, the exact measurement CLI (timing, memory, cold compile), and the
Python instantiation of the Phase 1
[metrics & publication protocol](../phase-1-cross-stack-foundation/metrics-protocol.md).
Template texts are in [templates.md](templates.md); decisions D1–D13 in the
[README](README.md#design-decisions).

## Directory layout

```
benchmarks/python/
  README.md                    ← pointer to this spec + the setup/run commands below
  requirements.txt             ← the five D2 pins, == only
  .venv/                       ← git-ignored
  results/                     ← git-ignored raw outputs; published copies go to docs/benchmarks/<date>/
  templates/                   ← per templates.md (jinja2|mako × controlled|idiomatic)
  runner/
    __init__.py
    data.py                    ← the eight models (D6), module-level MODELS dict
    gates.py                   ← N1–N5, manifest SHA-256, byte gate, security floor (D7)
    verify.py                  ← idiomatic verifier over <id>.verify.json (D12)
    engines.py                 ← the four engine objects + template loading per track
    gate_all.py                ← 16-cell gate sweep per track (CLI: --track controlled|idiomatic)
    selftest.py                ← verifier calibration against corpus + corruptions (D12)
    report_table.py            ← markdown tables from results/*.json (mean/std/median, ns/render, ratio)
  bench_jinja2_controlled.py   ← pyperf Runner scripts (D8)
  bench_jinja2_idiomatic.py
  bench_mako_controlled.py
  bench_mako_idiomatic.py
  bench_cold_compile.py        ← D10
  mem_tracemalloc.py           ← D11
```

`requirements.txt` (exact content):

```
Jinja2==3.1.6
Mako==1.3.12
MarkupSafe==3.0.3
pyperf==2.10.0
psutil==7.2.2
```

Corpus access: `runner/gates.py` resolves the repo root as
`Path(__file__).resolve().parents[3]` and reads
`src/Heddle.Performance/GoldenCorpus/manifest.json`, `<id>.golden.html`, `<id>.verify.json`
from it. Every consumed corpus file's SHA-256 is verified against the manifest before use.

## Gate runner mechanics

`runner/gates.py` implements contract v2 exactly; the closed list, nothing else:

1. **N1** — candidate `str` is encoded/decoded strict UTF-8; the comparison is on UTF-8 bytes
   without BOM (a BOM in output survives to the comparison and fails it).
2. **N2** — `s.replace("\r\n", "\n").replace("\r", "\n")`.
3. **N3** — `re.sub(r">[\t\n\x0b\x0c\r ]+<", "><", s)` — the contract's closed six-character
   whitespace set spelled literally (never `\s`, which is Unicode-wide in Python). A single
   pass is sufficient (replacement `><` contains no whitespace, so no new match can form).
4. **N3b** — `re.sub(r"[\t\n\x0b\x0c\r ]+", "", s)` — remove every whitespace run anywhere to
   **nothing** (not to a space; the 2026-07-20 maintainer step; same closed six-character set).
   Applied at comparison to **both** the candidate's normalized output and the loaded oracle, so the
   gate compares non-whitespace bytes. Consequence: any whitespace-only divergence — run-length or
   presence-vs-absence — passes the gate. (N3/N4 shape the stored oracle; N3b subsumes them at
   comparison.)
5. **N4** — `s.strip("\t\n\x0b\x0c\r ")` — same closed set.
6. **N5** (encoded suites only) — one left-to-right scan, non-overlapping, output never
   rescanned, implemented as a single compiled alternation over the recognized spellings of the
   five characters (named entities case-sensitive; numeric references matched with any number
   of leading zeros, case-insensitive hex digits and `x`), replacing with the canonical
   `&amp; &lt; &gt; &quot; &#39;` per the
   [contract table](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5).
   Regex, verbatim:

   ```python
   _N5 = re.compile(
       r"&(?:"
       r"amp|#0*38|#[xX]0*26|"
       r"lt|#0*60|#[xX]0*3[cC]|"
       r"gt|#0*62|#[xX]0*3[eE]|"
       r"quot|#0*34|#[xX]0*22|"
       r"apos|#0*39|#[xX]0*27"
       r");")
   ```

   with a replacement function mapping each match to its canonical spelling. On this cast the
   only rewrite that actually fires is MarkupSafe's `&#34;` → `&quot;` (D4), but the full table
   is implemented for contract conformance.
6. **Compare** — apply N3b (whitespace removal) to both the normalized candidate and the loaded
   corpus text, then compare their UTF-8 bytes; on mismatch, report first-diff byte index,
   expected/actual lengths, and a ±40-char excerpt window (`\n`-escaped) — the `ParityCheck.Describe`
   shape.
7. **Security floor** (encoded suites) — assert `"<script>alert("` occurs 0 times in the
   **un-normalized** candidate and `"&lt;script&gt;alert("` occurs the expected count in the
   normalized candidate (expected counts: fortunes-encoded 1, encoded-loop 0 — the payload
   string appears only in the fortunes model).

Failure anywhere ⇒ `SystemExit(1)` with the [README error-surface](README.md#diagnostics--error-surface)
message; in a bench script this happens before any `bench_func` registration, so pyperf emits
nothing.

## Bench script shape (normative skeleton)

Every render bench script follows this exact shape (engine/track substituted); pyperf
re-executes the module in each worker, so the gates run in every process that produces a timed
value (D7):

```python
import pyperf
from runner import data, engines, gates

TRACK = "controlled"; ENGINE = "jinja2"
WORKLOADS = [...]  # the eight ids

templates = {w: engines.load(ENGINE, TRACK, w) for w in WORKLOADS}
contexts  = {w: data.MODELS[w] for w in WORKLOADS}

for w in WORKLOADS:                      # hard gate, every worker, before registration
    out = engines.render(templates[w], contexts[w])
    if TRACK == "controlled":
        gates.assert_parity(w, out)      # N1–N5 + byte compare + security floor
    else:
        gates.assert_verified(w, out)    # idiomatic verifier

runner = pyperf.Runner()
for w in WORKLOADS:
    runner.bench_func(f"{ENGINE}/{TRACK}/{w}", engines.render, templates[w], contexts[w])
```

`engines.render(template, ctx)` is one positional-args call (`bench_func` rejects kwargs —
executed evidence): Jinja2 `template.render(ctx)` (a single mapping argument), Mako
`template.render(**ctx)` wrapped so the registered callable takes `(template, ctx)`
positionally. One call = one complete output string from the cached template — the protocol's
metric-rule-1 definition; models and templates are module-level (built once per worker, outside
timed regions).

`bench_cold_compile.py` registers, per engine × workload,
`(<engine>/cold-compile/<id>)` over `Environment().from_string(source)` /
`Template(text=source)` with the controlled source text preloaded into a string (D10). No
parity gate applies (nothing is rendered); the script asserts once per worker that compiling +
rendering each source still passes the byte gate, so the cold pass can never time a
non-conformant source.

## Run protocol

All commands run from **`benchmarks/python/`** (the `-m runner.*` module commands resolve
against that directory), in an **elevated** PowerShell (D8: psutil's
`REALTIME_PRIORITY_CLASS` request is silently downgraded without elevation), venv activated.
Machine idle (no browsers/IDE builds), AC power, per the existing reports' practice.

```powershell
cd benchmarks\python

# one-time setup
py -3.14 -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# conformance before any measurement (all must exit 0)
python -m runner.selftest
python -m runner.gate_all --track controlled
python -m runner.gate_all --track idiomatic

# timing passes (pyperf defaults: 20 processes x 3 values x 1 warmup, loops auto >=100 ms)
python bench_jinja2_controlled.py --affinity=4 -o results\jinja2-controlled.json
python bench_jinja2_idiomatic.py  --affinity=4 -o results\jinja2-idiomatic.json
python bench_mako_controlled.py   --affinity=4 -o results\mako-controlled.json
python bench_mako_idiomatic.py    --affinity=4 -o results\mako-idiomatic.json
python bench_cold_compile.py      --affinity=4 -o results\cold-compile.json

# memory pass (separate; never combined with timing — D11)
python mem_tracemalloc.py -o results\memory.json

# tables for the report
python -m runner.report_table results
```

*(Verify at implementation: `--affinity=4` per the D8 CPU-topology note — confirm the SMT-pair
enumeration with Sysinternals coreinfo before the protocol run and record the confirmed CPU id
in the report's environment block.)*

Number extraction: `report_table.py` loads the pyperf JSONs via `pyperf.BenchmarkSuite.load`
and emits, per benchmark: **mean** and **std dev** (the Q2.1 statistic pair for pyperf),
**median**, the ns/render conversion, and the ratio vs the Heddle reference value (supplied to
the tool from the published Phase 1 run as a static table — an excerpt, never a
re-measurement). `pyperf stats results\<file>.json` output is committed alongside the JSONs in
the published directory as the human-readable dispersion record.

## Memory pass

`mem_tracemalloc.py` (D11): for each of the 32 cells (2 engines × 2 tracks × 8 workloads),
spawn one fresh child process (same venv interpreter) that executes:

```python
build engine + template + model          # identical wiring to the bench scripts
out = render()                           # one warm-up render, untimed, before tracing
if TRACK == "controlled":
    gates.assert_parity(w, out)          # same gate the bench scripts run — D7
else:
    gates.assert_verified(w, out)
del out
gc.collect()
tracemalloc.start()
for i in range(100):
    gc.collect()
    base = tracemalloc.get_traced_memory()[0]
    tracemalloc.reset_peak()
    out = render()
    cur, peak = tracemalloc.get_traced_memory()
    allocated[i] = peak - base
    retained[i]  = cur - base
    del out
tracemalloc.stop()
```

Output `results/memory.json`: per cell
`{"allocated": {"mean","median","std"}, "retained": {"mean","median","std"}, "repetitions": 100}`
(statistics via the `statistics` stdlib module; `std` = sample standard deviation). The
headline column is `allocated.mean`, reported as **"allocated bytes per render (peak live)"**
(the `(peak live)` qualifier names the mechanism — a per-render tracemalloc high-water mark,
not a cumulative allocation counter — see the method note below). No timing number is ever
taken in this process, and no memory number in a timing process. Each cell's warm-up render is
gated identically to the corresponding bench script's warm-up gate (D7) before tracing starts,
so no memory number is ever produced from a non-conformant template.

**Memory method note (verbatim in the report, adjacent to the memory table):**

> *"Memory method: allocated bytes per render = the peak of tracemalloc-traced Python
> allocations during a single render, minus the traced baseline immediately before it; mean
> over 100 single-render repetitions after one warm-up render, measured in a separate
> non-timing process. tracemalloc traces Python-level allocations only — this figure is a
> per-render allocation high-water mark, not allocator throughput and not process RSS."*

## Report format instantiation

The report is a new `docs/benchmarks/<yyyy-MM-dd>/` directory in the protocol's exact
`index.md` shape. Python-specific content, in protocol order:

1. **Intro** — suites covered (both engines, both tracks, cold compile, memory) and the full
   reproduce block (the [run protocol](#run-protocol) commands).
2. **`## Environment`** — fenced block: OS name + build, CPU model, `python -VV`, the five
   package pins (from `pip freeze`), pyperf settings line
   (`20 processes x 3 values x 1 warmup, loops auto-calibrated (>=100 ms), --affinity=4,
   REALTIME_PRIORITY_CLASS (elevated shell)`), repo commit — plus the stability-posture
   disclosure, verbatim:

   > *"Stability posture: pyperf's best-stability guidance assumes Linux-style CPU isolation
   > and system tuning; no Windows equivalent of `pyperf system tune` exists. This run applies
   > the settings pyperf documents for untuned machines — worker processes at
   > REALTIME_PRIORITY_CLASS and pinned to one CPU (`--affinity=4`) — and publishes each
   > benchmark's dispersion (mean ± std dev, median) rather than presenting the numbers as
   > unconditionally tight. Read the dispersion before reading precision into any single
   > value."*

3. **`## The workloads`** — one bullet per workload (id, owned dimension, suite, which gate
   ran); the encoded confinement caveat verbatim beside the encoded entries.
4. **`## Results — what this run actually shows`** — narrative + tables:
   - opens with the cross-runtime framing sentence (D13);
   - wall-time tables per track (caption names the track): per workload — Jinja2, Mako, and
     the labeled row `Heddle (reference — .NET 10, same machine, from <date> run)`; columns:
     harness-native time, ns/render, ± std dev, median, ratio vs the Heddle row; any pyperf
     instability warning quoted beside its row (D9);
   - the raw-track fairness statement, verbatim: *"Both engines' raw-suite ports run their
     untouched default output paths — Jinja2's standalone `autoescape=False` default and
     Mako's default `str`-only filter chain; no escape-bypass configuration or syntax exists
     anywhere in the raw templates."*;
   - where-Python-narrows prose: the Mako-vs-Jinja2 internal ranking per workload, shape
     effects, bulk-output amortization (`large-loop`, `encoded-loop`) — every narrowing or
     inversion named with numbers, as prominently as any Heddle win;
   - **cold parse/compile** subsection (Python-only): Jinja2 `from_string` vs Mako
     `Template(text=…)` per workload, labeled: *"Cold parse/compile cost is measured within
     this ecosystem only and is not comparable across ecosystems (AOT compilation and runtime
     parse/compile are different operations)."*;
   - **memory** subsection (Python-only): the Q5.1 table (column header "allocated bytes per
     render (peak live)" — mean/median/std — plus retained mean per cell), Jinja2 as
     within-ecosystem baseline, with the protocol's allocation label
     verbatim, the method note verbatim (above), and the sentence: *"These figures appear in
     this per-ecosystem report only; no table juxtaposes them with any other runtime's
     numbers."*;
   - the exclusion-rationale link (Django templates, legacy engines, native-extension
     bindings → the [plan document](../../../plan/phase-5-python.md#non-goals--scope-boundary));
   - the **Phase 8 forward note**, verbatim: *"A separate cross-check of this suite on the
     same physical machine under Ubuntu 24.04 is planned (Phase 8). Those Linux numbers will
     be published on their own and are never merged or cross-compared with the Windows numbers
     in this report."*
5. **`## Files`** — links to the five pyperf JSONs, the `pyperf stats` dumps, `memory.json`,
   and the generated tables.

Publication rules carried without restatement: immutability, no universal-superiority claims,
losses reported as prominently as wins, gate-failed suites publish no numbers, excluded cells
(if any materialize) print `excluded — documented evidence` with a link. The
[README testing-plan checklist](README.md#testing-plan) is the sign-off instrument for this
section.
