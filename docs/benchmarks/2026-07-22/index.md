# Cross-stack benchmark run — 2026-07-22

This is the first run of the cross-stack benchmark program to publish numbers. It measures
**thirteen template engines across six ecosystems** — .NET, Rust, the JVM, JS/Node, Python and
Go — over the program's **eight protocol workloads**, in **two tracks**, on one machine in one
session finishing 2026-07-22 04:49:41. Every ecosystem ran its own standard harness
(BenchmarkDotNet, Criterion, JMH, mitata, pyperf, `go test` + benchstat) and contributed that
harness's default central-tendency statistic, per the
[metrics protocol](../../spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md).

Reproduce the whole run:

```powershell
# Windows (the protocol machine)
.\benchmarks\run-all.ps1
```

```bash
# Linux twin — different priority posture, not drop-in comparable
./benchmarks/run-all.sh --tune
```

Per-ecosystem invocations are in [benchmarks/README.md](../../../benchmarks/README.md); the
.NET anchor alone is
`dotnet run -c Release --project src/Heddle.Performance -- --filter *<Suite>*`.

**The gate.** Every controlled-track cell passed the byte gate under the parity contract's
closed normalization list **N1, N2, N3, N3b, N4, N5**. N3b is the program-wide comparison-time
rule that strips **every** whitespace run, anywhere, to nothing from both the oracle and the
candidate — so the gate compares **non-whitespace bytes only**, and "byte-identical" throughout
this report means byte-identical after that disclosed pipeline, with entity spellings
canonicalized by N5. Whitespace differences therefore never disqualify a cell anywhere in this
program. Encoded cells additionally cleared the security floor. Every idiomatic-track cell
passed the functional-equivalence verifier. All eleven gate steps were green — see
[summary.txt](summary.txt).

**Pre-protocol history.** The [2026-07-11](../2026-07-11/) and [2026-07-18](../2026-07-18/)
reports are the intra-.NET historical record. They are cited as motivation for this program and
are **not aggregated** into it.

## Environment

```
Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 16 physical / 32 logical cores
run finished 2026-07-22 04:49:41 · mode MEASUREMENT · six ecosystems
```

| Ecosystem | Harness | Toolchain as run |
| --- | --- | --- |
| .NET | BenchmarkDotNet 0.15.8 | SDK 10.0.302 · .NET 10.0.10, X64 RyuJIT x86-64-v4 |
| Rust | Criterion (100 samples, linear) | pinned 1.97.1 via `rust-toolchain.toml` |
| JVM | JMH 1.37, `Mode.AverageTime`, 5 forks × (5×10 s warmup + 5×10 s measure) | JDK 23.0.2, HotSpot 23.0.2+7-58 |
| JS | mitata 1.0.34 | node 26.4.0 (x64-win32), clk ~5.51 GHz |
| Python | pyperf 2.10.0, 20 measurement runs | CPython 3.13.0, MSC v.1941 64-bit |
| Go | `go test -bench` ×20 + benchstat | go1.26 (goos windows, goarch amd64) |

### Toolchain drift from the pins — read this before the rankings

Three ecosystems did **not** run at the version
[benchmarks/README.md](../../../benchmarks/README.md) pins. The master runner warns rather than
halts on a pin delta (standing posture SR-3), so the run proceeded and the deltas are recorded
here:

| Toolchain | Pinned | Actually ran | Note |
| --- | --- | --- | --- |
| JDK | Temurin 25 | **JDK 23.0.2** (HotSpot) | built through the runner's `-Dmaven.compiler.release=23` fallback |
| Node.js | v24.18.0 | **node 26.4.0** | two major versions ahead; V8 differs materially |
| CPython | 3.14.6 | **3.13.0** | one minor version behind, and 3.14 carries substantial interpreter performance work |
| Rust | 1.97.1 | 1.97.1 (pinned by file) | no drift; the version is not restated in Criterion's artifacts |
| Go | go1.26.5 asserted | go1.26 | asserted by the runner before measuring |

These deltas do not affect the *within-ecosystem* comparisons at all — both engines in each
ecosystem ran on the identical toolchain. They do weaken the **cross-stack** rankings as claims
about the ecosystems: the JVM figures are from an older JDK than intended, and the Python
figures from an older CPython than intended, while Node ran ahead. Read the cross-stack tables
as honest about *this run* and provisional as statements about the ecosystems. See
[Limitations](#limitations).

### Priority and affinity are not symmetric across harnesses

pyperf ran with `--affinity=4`, pinned to a single logical core. The other five harnesses ran
unpinned under the Windows High priority class. This is an asymmetry in the measurement
conditions, not a property of the engines, and it is another reason the cross-stack Python rows
carry the reach/context label rather than fair-fight standing.

## The workloads

Eight workloads in the normative protocol order. "Golden output" is the byte length of the
shared oracle in
[src/Heddle.Performance/GoldenCorpus](../../../src/Heddle.Performance/GoldenCorpus), verified by
SHA-256 against `manifest.json` by every harness before it measures anything.

1. **`composed-page`** — layout/section/component composition; raw suite; 34,847 B. Controlled
   byte gate + idiomatic verifier.
2. **`trivial-substitution`** — the per-render fixed-overhead floor; raw suite; 338 B.
   Controlled byte gate + idiomatic verifier.
3. **`large-loop`** — bulk iteration over 5,000 rows; raw suite; 192,780 B. Controlled byte
   gate + idiomatic verifier.
4. **`mixed-page`** — a realistic mixture, 36 rows; raw suite; 9,712 B. Controlled byte gate +
   idiomatic verifier.
5. **`conditional-heavy`** — branch dispatch, 200 branches; raw suite; 15,549 B. Controlled
   byte gate + idiomatic verifier.
6. **`fragment-heavy`** — per-call composition, 48 calls; raw suite; 4,730 B. Controlled byte
   gate + idiomatic verifier.
7. **`fortunes-encoded`** — escaping correctness at micro scale, 12 rows; **encoded** suite;
   1,156 B. Controlled byte gate + security floor + idiomatic verifier.
8. **`encoded-loop`** — escaping throughput, 5,000 rows; **encoded** suite; 831,685 B.
   Controlled byte gate + security floor + idiomatic verifier.

For the two encoded workloads (7–8):

> Encoded workloads confine untrusted data to HTML text and attribute-value contexts, where
> flat and contextual encoders emit the same escaped output. These numbers therefore measure
> escaping cost in confined contexts and cannot show the differentiating benefit of contextual
> encoding; a contextual encoder's encoded-suite result must not be read as 'context awareness
> is pure overhead.'

## How to read these tables

> **Controlled track** — byte-identical, equivalently-authored templates under the parity
> contract's closed normalization pipeline. These numbers answer *"how fast is the engine"* on
> identical work. They may be used to compare engine execution speed on a given workload —
> and nothing else.

> **Idiomatic track** — functionally-equivalent templates written the way each engine's own
> documentation teaches, verified by the functional-equivalence gate. These numbers answer
> *"how fast is the practitioner experience."* They may be used to compare what a practitioner
> gets writing each engine's documented style; they must not be read as engine
> execution-speed rankings.

**The statistic.** Each figure is its harness's own default central-tendency point estimate,
with that harness's native dispersion beside it. This mapping is fixed by the protocol and this
report may not vary it:

| Ecosystem | Wall time per render = | Dispersion published alongside |
| --- | --- | --- |
| .NET | BenchmarkDotNet's `Mean` | `Error` and `StdDev` as reported |
| Rust | Criterion's `mean` point estimate | the 95% confidence-interval bounds |
| JVM | JMH's `Score` (avgt) | the `Error` (99.9% CI) as reported |
| JS | mitata's `avg` | the emitted min … max spread |
| Python | pyperf's mean | the standard deviation |
| Go | benchstat's `sec/op` summary (median-based) | benchstat's `±%` variation |

The only arithmetic applied anywhere in this report is conversion to ns/render, the `vs Heddle`
ratio, and the implied-throughput figure. No statistic is re-derived from raw samples.

**The ratio.** Every `vs Heddle` column anchors to the Heddle row —
`ratio = engine wall time ÷ Heddle wall time` — so **below 1.00 means the engine beat Heddle**.
Heddle runs only in the .NET anchor; the Heddle row appearing in every other ecosystem's table
is that same .NET measurement, taken in this same run on this same machine, not a re-measurement.

**Ranking scope.** Heddle may be compared against any engine, wall-time-only. The per-ecosystem
tables are the normative view. The cross-stack ranked tables are published under the
[2026-07-25 amendment to Phase 7 D6](../../spec/cross-stack-benchmarks/phase-7-consolidated-report/README.md#d6--ranking-scope-enforcement-q62-and-the-no-score-rule-made-checkable):
they rank per workload only. **There is no aggregate score, no geomean, no medal count and no
overall winner anywhere in this report**, so no single-number verdict can be quoted from it. The
prose below is Heddle-anchored throughout and never ranks two non-Heddle engines from different
ecosystems against each other.

**Evidence class.** Rust, JVM and Go rows are **fair-fight** evidence — compiled or same-class
peers. JS and Python rows are **reach/context** evidence, not a fair fight: they show where the
workload lands in those ecosystems, not a like-for-like engine contest. Within .NET, the four
Liquid/Handlebars twins are the strictest fair fight in the program, being byte-parity asserted
against Heddle before any timing.

**Implied B/ns.** Golden output size ÷ wall time. This is **not** a performance metric — it is a
plausibility check. A figure larger than the machine's realistic store bandwidth means the
harness is not materialising the full output, so the cell is a measurement artifact rather than
engine speed. See the [plausibility register](consolidated-tables.md#plausibility-register).

## Results — what this run actually shows

The full table set lives in **[consolidated-tables.md](consolidated-tables.md)** — 8 cross-stack
ranked tables per track, 6 per-ecosystem tables per track, the cold-compile tables, the
per-ecosystem allocation sidebars, and the plausibility register. It is generated from the raw
artifacts by [`benchmarks/report/consolidate.py`](../../../benchmarks/report/consolidate.py) and
is the byte-reproducibility anchor for every number quoted below.

### The .NET fair fight — Heddle wins seven of eight

Against the four byte-parity-asserted twins, on the controlled track:

| Workload | Heddle | Fastest twin | Heddle's rank |
| --- | ---: | --- | --- |
| composed-page | 25.31 μs | Fluid.Core 67.63 μs | **1 of 5** |
| trivial-substitution | 113.6 ns | Handlebars.Net 380.0 ns | **1 of 5** |
| large-loop | 514.7 μs | Handlebars.Net 668.3 μs | **1 of 5** |
| mixed-page | 2.601 μs | Handlebars.Net 7.285 μs | **1 of 5** |
| conditional-heavy | 13.97 μs | Handlebars.Net 34.06 μs | **1 of 5** |
| fragment-heavy | 2.880 μs | Fluid.Core 10.897 μs | **1 of 5** |
| fortunes-encoded | 669.0 ns | Handlebars.Net 1,733.8 ns | **1 of 5** |
| encoded-loop | 1.988 ms | **Handlebars.Net 1.925 ms** | **2 of 5** |

**Heddle loses `encoded-loop`.** Handlebars.Net renders it in 1.925 ms against Heddle's
1.988 ms — 3.3% faster, and the gap is larger than either engine's dispersion
(Heddle ±0.0364 ms, Handlebars.Net ±0.0340 ms), so it is a real result and not noise. This is
the largest raw-output workload in the set (831,685 B) with escaping on every field: at that
size the per-node dispatch advantage Heddle holds on the smaller workloads is amortised away and
what remains is escaping and buffer throughput, where Handlebars.Net is marginally better.

Where Heddle wins it wins by wide margins — 2.7× on `composed-page`, 3.3× on
`trivial-substitution`, 2.8× on `mixed-page`, 2.6× on `fortunes-encoded`, 3.8× on
`fragment-heavy` against the fastest twin in each case. `large-loop` is the tightest win at
1.30×.

### The cross-stack picture — Heddle is mid-field, and last on the flagship workload

This is the part the .NET-only reports could not show.

| Workload | Heddle | Heddle's cross-stack rank | Engines faster than Heddle |
| --- | ---: | --- | --- |
| composed-page | 25.31 μs | **11 of 16** | **every** non-.NET engine — all ten of them |
| trivial-substitution | 113.6 ns | 3 of 15 | eta (JS), Askama (Rust) |
| large-loop | 514.7 μs | 6 of 15 | Askama, Tera (Rust); JTE (JVM); eta, handlebars (JS) |
| mixed-page | 2.601 μs | 3 of 15 | Askama (Rust), eta (JS) |
| conditional-heavy | 13.97 μs | 4 of 15 | Askama (Rust), JTE (JVM), eta (JS) |
| fragment-heavy | 2.880 μs | 3 of 15 | Askama (Rust), JTE (JVM) |
| fortunes-encoded | 669.0 ns | **2 of 15** | Askama (Rust) |
| encoded-loop | 1.988 ms | 6 of 15 | Askama, Tera (Rust); JTE (JVM); templ (Go); Handlebars.Net (.NET) |

**The `composed-page` result is the sharpest negative finding in this run and deserves stating
plainly: every single non-.NET engine measured — all ten of them, in all five other ecosystems —
renders `composed-page` faster than Heddle.** Askama does it in 1.458 μs against Heddle's
25.31 μs, a 17× gap. Even the two engines chosen as their ecosystems' *credibility* picks rather
than performance picks beat Heddle here: Thymeleaf at 21.63 μs and Go's `text/template` at
19.4 μs. `composed-page` is layout/section/component composition — precisely the shape Heddle's
design story is about — so this is not a peripheral workload for it.

**Heddle's best cross-stack showing is `fortunes-encoded`**, where only Askama (424.1 ns) is
faster than its 669.0 ns, and it beats JTE (1.481 μs), templ (1.892 μs), Mako (10.6 μs) and
Go's `html/template` (11.55 μs). Its other strong results are the small and mid-sized shapes:
3rd on `trivial-substitution`, `mixed-page` and `fragment-heavy`.

**Askama is the only engine in the run that is faster than Heddle on all eight controlled
workloads**, by margins from 1.6× (`fortunes-encoded`) to 17.4× (`composed-page`). The next
closest are JTE and eta, each ahead of Heddle on five of the eight; Handlebars.Net, Thymeleaf,
Jinja2 and Mako each beat it on exactly one; Fluid.Core, Scriban and DotLiquid on none.

### The JS large-output numbers are partly a measurement artifact

`eta` renders `composed-page` in 330.7 ns. The golden output is 34,847 B, so that figure implies
**105.4 B/ns — about 105 GB/s** of output production on one core while also executing template
logic. That is above this machine's realistic single-core store bandwidth, so it cannot be
happening.

The mechanism: V8 represents string concatenation as an unflattened `ConsString` rope, and
mitata's `do_not_optimize()` consumes the returned reference without forcing it flat. The JS
harness therefore times rope *construction* on concatenation-dominated workloads, not output
production. A second, independent signal agrees: mitata reports ~945 B of heap per render for
`handlebars` on `composed-page` — 3% of the 34,847 B output, a thirty-fold shortfall far outside
that estimate's sampling error.

Both JS `composed-page` cells are affected, in both tracks. **They are published, ranked, and
flagged** rather than withheld, with the throughput column making the problem visible in the
table itself — see the
[plausibility register](consolidated-tables.md#plausibility-register). No other cell in the run
exceeds the ceiling, and the mid-range JS cells (`mixed-page`, `conditional-heavy`,
`fragment-heavy`) have plausible throughput and heap ratios inside the noise band, so they are
not called artifacts. The consequence for reading the tables: **the `eta` and `handlebars` rows at
the top of the `composed-page` ranking must not be read as engine speed.** Discounting them, the
largest credible margin over Heddle on that workload is Askama's 17.4×.

### Controlled against idiomatic

The .NET anchor shipped **controlled-track-only** (Phase 1 D15), so the idiomatic tables carry
the Heddle reference row and the phases 2–6 engines only — ten rows rather than fifteen.

Writing each engine's documented style rather than an equivalently-authored template moves some
engines materially. JTE's `conditional-heavy` more than doubles, from 4.959 μs controlled to
10.81 μs idiomatic. Askama's `large-loop` goes from 56.47 μs to 75.44 μs and its
`conditional-heavy` from 3.75 μs to 5.43 μs. Tera's `mixed-page` nearly quadruples, 5.608 μs to
22.24 μs. In the other direction JTE's `encoded-loop` *improves* idiomatically, 1.25 ms to
910.7 μs. Because .NET has no idiomatic track, none of this can be compared to Heddle's own
practitioner experience — an asymmetry worth closing in a future run.

### No universal superiority

> No engine measured by this program is universally fastest, and this report declares no
> overall winner. Every figure above is workload-shaped, dated, and specific to one machine
> (Windows 11, AMD Ryzen 9 9950X); a different workload mix, machine, or date can reorder any
> table here. Nothing in this report may be quoted as evidence of the universal superiority of
> any engine — Heddle included.

## Per-ecosystem sidebars — not cross-comparable

> Allocation and GC figures are measured within one runtime and are not comparable across
> ecosystems: allocator designs differ and the numbers do not mean the same thing.

The sidebar tables are in
[consolidated-tables.md](consolidated-tables.md#per-ecosystem-sidebars--not-cross-comparable):
.NET allocation from BenchmarkDotNet's `[MemoryDiagnoser]`, JVM from JMH's `-prof gc`, Python
from `tracemalloc`, Go from `-benchmem` via benchstat. No table or sentence in this report
juxtaposes them across runtimes.

Within .NET, the allocation story tracks the timing story but not perfectly: on
`composed-page` Heddle allocates 227.86 KB against Handlebars.Net's 227.59 KB — Handlebars.Net
allocates **less**, by 0.3 KB, while taking 2.9× the time. Scriban allocates 5.07× Heddle's
figure.

Two sidebars are absent. **JS has no memory sidebar** — the JS phase shipped time-only
(Phase 4 D3), so mitata's heap estimate is used here only as the plausibility signal above and
is not published as a memory result. **Rust has no allocation report**: `alloc-report.txt` was
produced by the run (`rust / measure / alloc-report` = OK in [summary.txt](summary.txt)) but the
`copy-criterion` step copied only `target/criterion`, so the artifact never reached this
directory.

For the JVM figures:

> **Disclosed limitation — isolated-microbenchmark JIT profiles.** These numbers come from
> isolated JMH microbenchmarks. Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the
> JVM" (ACM SAC 2026, arXiv:2605.23570), show that isolated JMH runs can settle into
> tiered-JIT compilation profiles unrepresentative of the same code running inside a larger
> application, and that warmup iteration counts alone do not buy profile realism. The figures
> here are steady-state per-render measurements — internally consistent across the two
> engines, which are measured under the identical regime — and are not a prediction of
> in-application rendering performance.

## Cold compile / parse

Per-ecosystem only, never cross-compared (Q1.3): a Rust compile-time template, a JVM
precompiled class, a Python `compile()` and a JS `new Function()` are not the same event. The
.NET anchor measured no cold suite in this run, so there is no Heddle row and no ratio column.
Tables in
[consolidated-tables.md](consolidated-tables.md#cold-compile--parse--per-ecosystem).

## Limitations

1. **Toolchain drift on three ecosystems** — JDK 23.0.2 against a Temurin 25 pin, node 26.4.0
   against a 24.18.0 pin, CPython 3.13.0 against a 3.14.6 pin. Within-ecosystem comparisons are
   unaffected; cross-stack rankings are provisional as statements about the ecosystems. This is
   the single biggest caveat on this run.
2. **The JS `composed-page` cells are measurement artifacts** — V8 rope construction rather than
   output production, in both tracks and for both engines. Published and flagged, not withheld.
3. **Measurement conditions are not symmetric** — pyperf pinned to one logical core via
   `--affinity=4`; the other five harnesses unpinned under Windows High priority.
4. **`rust / measure / summarize` exited 1** — the run's only non-zero exit. It failed by design
   because `benchmarks/rust/heddle-reference.toml` was `pending = true` under standing ruling
   SR-6 while no protocol run existed to transcribe. **This run discharged SR-6**: the reference
   file now carries the eight Heddle values sourced from this directory.
5. **Rust allocation figures are absent** — produced but never copied (see above).
6. **Razor is not parity-checked** — it renders the full `Views/home.cshtml` page, a larger and
   different payload rather than the golden output. It is measured and ranked on wall time like
   every other row, but its implied-throughput figure is withheld because the golden byte length
   does not describe what it produced.
7. **.NET has no idiomatic track** (Phase 1 D15), so Heddle's practitioner-style performance is
   unmeasured and the idiomatic tables cannot be anchored to a Heddle idiomatic row.
8. **Encoded workloads are confined** — see the caveat under
   [The workloads](#the-workloads).
9. **Go contributes one engine across two surfaces** — `text/template` on the six raw workloads
   and `html/template` on the two encoded ones are the same engine, not two engines.
10. **One machine, one date.** Numbers are hardware- and date-specific. A Linux cross-check
    exists as a separate protocol
    ([benchmarks/linux-crosscheck](../../../benchmarks/linux-crosscheck/README.md)) and is not
    part of this report.

## Files

Generated tables and the run manifest:

- **[consolidated-tables.md](consolidated-tables.md)** — every table in this report, generated
  from the raw artifacts below. Regenerate and verify with:
  ```
  python benchmarks/report/consolidate.py docs/benchmarks/2026-07-22
  python benchmarks/report/consolidate.py --check docs/benchmarks/2026-07-22
  ```
- **[summary.txt](summary.txt)** — the runner's 39-step manifest with exit codes and durations.

Raw harness artifacts, by ecosystem:

- **`dotnet/results/`** — the BenchmarkDotNet `-report.csv` / `-report-github.md` /
  `-report.html` triplet for each of the eight protocol suites, plus the two Heddle-internal
  suites (`PropsRenderBenchmarks`, `SinkRenderBenchmarks`) which are not competitor tables and
  appear only in the .NET sidebar.
- **`rust/criterion/`** — Criterion's `benchmark.json`, `estimates.json` and `tukey.json` per
  cell, plus the `cold/` parse sidebar.
- **`jvm/jmh-result.json`** — the 32-element JMH result array with `primaryMetric` and the
  `-prof gc` secondary metrics.
- **`js/`** — mitata's `controlled` / `idiomatic` / `cold-compile` JSON and text views.
- **`python/`** — pyperf JSON per engine and track, `bench_cold_compile.json`, and
  `memory.json` from `tracemalloc`.
- **`go/`** — the raw 20-sample `bench-*.txt` runs and the `benchstat-*.txt` aggregates.

### What was changed in these artifacts

These files are **not** byte-verbatim runner output. On 2026-07-25, when this report was
assembled, the following edits were made and nothing else:

- Renamed the directory from `windows-run-20260721-232229/` to `2026-07-22/` (the run's finish
  date) to match the `docs/benchmarks/<yyyy-MM-dd>/` convention and the path pattern that
  [windows-source.schema.json](../../../benchmarks/linux-crosscheck/windows-source.schema.json)
  requires.
- Deleted two stale artifacts from an unrelated `Dry`-job invocation
  (`FortunesRenderBenchmarks-report*-20260721-1116*`) which contained only `NA`/`?` values and a
  "Benchmarks with issues" trailer.
- Deleted Criterion's `base/` directories (the *previous* run's data, not this one) and the
  `new/sample.json` per-sample dumps.
- Stripped `stats.samples` and `stats.debug` from the mitata JSON — the latter being a stringified
  generated function per entry, not measurement data.
- Repaired the two `go/benchstat-*.txt` files, which PowerShell had written as UTF-8 bytes
  through a legacy code page (`Γöé`/`┬╡`/`┬▒` for `│`/`µ`/`±`), and removed their byte-order
  marks along with `summary.txt`'s.
- Stripped ANSI escape sequences from the three `js/*.txt` files so they render as text.
- Replaced the machine hostname and three absolute toolchain paths with placeholders in the
  Python and JVM JSON.

No measurement value was altered by any of these edits. Total size went from 2.7 MB to 923 KB.

### Verification performed

`consolidate.py --check` passes against the committed `consolidated-tables.md`. In addition,
these cells were checked by hand against artifacts the extractor does **not** read, to catch a
parser that is self-consistently wrong:

- The five `MixedRenderBenchmarks` rows against `-report-github.md` (the extractor reads the
  `.csv`) — all five means match.
- `idiomatic-fragment-heavy/tera` against Criterion's `estimates.json` read directly —
  14,575.757… ns, matching the published 14.58 μs and its 95% CI.
- The eight mitata group headers in `js/controlled.txt` and `js/idiomatic.txt`, confirming the
  positional workload mapping the JSON forces the extractor to assume (the JSON carries the
  engine alias but no workload name). Both files list the protocol order.
- `grep` confirms zero `geomean` rows reached the published tables; benchstat emits them and the
  extractor reads past them, because Phase 7 D6(c) forbids any aggregate score.

### Deviations from the published-report spec

- The assembly script lives at
  [`benchmarks/report/consolidate.py`](../../../benchmarks/report/consolidate.py) rather than
  inside this directory as Phase 7 D2 specifies, so that one tool serves every run instead of
  being copied per report. Its output is committed here as the byte-reproducibility anchor. D2's
  four-file rule governs a *consolidated* report directory; this is a run report in the
  [Phase 1 publication format](../../spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md)
  (`index.md` plus harness artifacts).
- The cross-stack ranked tables exist under the
  [2026-07-25 amendment to Phase 7 D6](../../spec/cross-stack-benchmarks/phase-7-consolidated-report/README.md#d6--ranking-scope-enforcement-q62-and-the-no-score-rule-made-checkable)
  and the matching amendment to
  [Q6.2](../../plan/open-questions.md#q62--which-engine-is-the-ratio-baseline-in-the-per-ecosystem-report-tables-given-heddle-is-not-an-in-process-participant--status-resolved).
  The ban on aggregate scores is untouched.

Numbers are hardware- and date-specific; reproduce them yourself with the commands at the top of
this page.
