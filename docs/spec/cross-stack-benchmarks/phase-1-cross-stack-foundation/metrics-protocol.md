# Metrics & publication protocol — full text

Supplementary document of the [Phase 1 — cross-stack-foundation spec](README.md). This is the
**normative measurement and publication protocol** for every benchmark run the program publishes
(phases 1–7; phase 8 re-runs it under Linux, separately published). Phases 2–6 reference this
document instead of restating it. The workloads it measures are in [workloads.md](workloads.md);
the gate that must pass before any number exists is in
[parity-contract-v2.md](parity-contract-v2.md).

## Metric rules

1. **Wall time per render is the only cross-language-comparable number.** One "render" = one
   invocation of the engine's cached-template render path producing the workload's complete
   output (as a string or filled buffer), with the parsed/compiled template and the model
   constructed once outside the measurement loop. Cold parse/compile is excluded from this
   metric everywhere.
2. **Allocation/GC figures are per-ecosystem only** and every table or prose that shows them
   carries the non-comparability label (verbatim text below). No published table ever juxtaposes
   allocation/GC numbers from two runtimes.
3. **Cold parse/compile cost is per-ecosystem only** (Q1.3), labeled non-comparable, because AOT
   compilation (Heddle, Askama, JTE, templ) and runtime parse/compile are different operations.
   It never appears as a cross-language column.
4. **Dispersion is always published alongside the point estimate** (Q2.1) — a wall-time number
   without its harness-reported dispersion is non-conformant.

### Required label texts (verbatim)

Reports copy these sentences exactly wherever the corresponding data appears:

- Allocation/GC label: *"Allocation and GC figures are measured within one runtime and are not
  comparable across ecosystems: allocator designs differ and the numbers do not mean the same
  thing."*
- Encoded-suite confinement caveat: *"Encoded workloads confine untrusted data to HTML text and
  attribute-value contexts, where flat and contextual encoders emit the same escaped output.
  These numbers therefore measure escaping cost in confined contexts and cannot show the
  differentiating benefit of contextual encoding; a contextual encoder's encoded-suite result
  must not be read as 'context awareness is pure overhead.'"* — required in every report that
  presents encoded-suite results, adjacent to them.

## Wall-time statistic mapping (Q2.1)

Each ecosystem uses its standard harness and contributes **that harness's default
central-tendency point estimate** as "wall time per render", with the harness's dispersion
statistic published alongside. The mapping, fixed once here:

| Ecosystem | Harness | "Wall time per render" = | Dispersion published alongside |
|---|---|---|---|
| .NET (this phase) | BenchmarkDotNet (`0.15.8` on net10.0 — the protocol-run TFM) | the `Mean` column | `Error` and `StdDev` columns as reported |
| Rust (phase 2) | Criterion.rs | the point estimate of Criterion's `mean` (its reported estimate line) | the 95% confidence interval bounds |
| JVM (phase 3) | JMH, `Mode.AverageTime` | the `Score` (avgt) | the `Error` (99.9% CI) as reported |
| JS/Node (phase 4) | mitata | the reported `avg` | the printed percentile spread (min … max, p75/p99 as emitted) |
| Python (phase 5) | pyperf | the reported `mean` | the reported standard deviation |
| Go (phase 6) | `go test -bench` + benchstat | benchstat's `sec/op` summary (median-based) | benchstat's `±%` variation |

Per-harness versions and stability settings are pinned in each ecosystem phase's spec (Q1.6
lean, carried); this table is the statistic-selection contract they may not vary. Unit
convention: reports print each harness's native unit and, in any table containing the Heddle
reference row, also normalize to **nanoseconds per render** so the ratio column is dimensionless.

## Presentation rules (Q2.2 = A, Q6.2)

1. **Heddle reference row.** Every per-ecosystem report (phases 2–6) carries a clearly-labeled,
   wall-time-only Heddle reference row per workload, sourced from **this phase's protocol run**
   (the published `docs/benchmarks/<date>/` numbers — an excerpt, never a re-measurement or
   second analysis). Row label format:
   `Heddle (reference — .NET 10, same machine, from <date> run)`. The row carries no
   allocation/GC cells (dashes).
2. **Ratio anchor.** The per-ecosystem wall-time ratio column anchors to the Heddle reference
   row: `ratio = engine wall time / Heddle reference wall time`, uniform across phases 2–6.
3. **Non-comparable metrics baseline.** Within-ecosystem baselines for allocation/GC (and any
   other non-comparable metric) anchor to the ecosystem's **credibility pick** (the first-listed
   engine: Tera, Thymeleaf, Handlebars, Jinja2, html/template), fixed here so phase 7 inherits
   consistent within-ecosystem tables. Intra-.NET reports keep Heddle as baseline (existing
   practice, unchanged).
4. **Scope of ranking.** Heddle may be compared and ranked against any engine, wall-time-only,
   anywhere. Non-Heddle engines are never compared or ranked across ecosystems; engine-vs-engine
   comparison exists only within an ecosystem. Phase 7 groups Heddle-anchored comparisons per
   ecosystem and publishes no global cross-ecosystem leaderboard of non-Heddle engines.
5. **Dual-track labeling.** Every table names its track (`controlled` / `idiomatic`) in its
   caption; numbers from different tracks are never mixed in one table.

## Machine and environment (Q1.6)

All cross-compared runs for phases 1–7 execute on the one recorded machine:

- **Box:** AMD Ryzen 9 9950X (16 physical / 32 logical cores), Windows 11 — the machine of the
  published 2026-07-11/2026-07-18 runs (continuity is the point of Q1.6).
- Every run's report includes an **environment block** (fenced code) recording: harness name +
  version, OS name + build, CPU model, runtime/toolchain versions used by that run (e.g.
  `.NET SDK` + runtime, or `rustc`/`cargo` + crate versions), and the repo commit
  (`Commit <shorthash>`; append `(working tree)` when uncommitted — matching the existing
  reports' block shape).
- Per-harness stability settings (process priority, warmup counts, pinned cores, etc.) are
  resolved in each ecosystem spec and **recorded in that run's report**; the protocol does not
  impose one setting set across harnesses.
- The Ubuntu 24.04 cross-check on the same physical machine is Phase 8, separately published,
  never merged or cross-compared with Windows numbers inside phase 1–7 reports.

## Publication format

Every run publishes as a new date-stamped directory `docs/benchmarks/<yyyy-MM-dd>/` (existing
convention; published directories are immutable — corrections get a new date, never an edit).
Contents:

1. **`index.md`** with, in order:
   - H1 `# Benchmark run — <yyyy-MM-dd>`;
   - intro paragraph naming the suites covered and the full reproduce command(s)
     (`dotnet run -c Release --project src/Heddle.Performance -- --filter *<Suite>*` form for
     .NET; the ecosystem harness invocation for phases 2–6);
   - `## Environment` — the environment block defined above;
   - `## The workloads` — one bullet per workload measured: its id, dimension owned (from
     [workloads.md](workloads.md)), suite, and a one-line parity statement naming the gate that
     ran (controlled byte gate / idiomatic verifier) — plus, for encoded suites, the confinement
     caveat text;
   - `## Results — what this run actually shows` — narrative with numbers, subject to the
     honest-reporting rules below;
   - `## Files` — bullet list linking each harness artifact committed alongside.
2. **Harness artifacts** — the raw per-suite outputs (for BenchmarkDotNet: the
   `-report-github.md`, `-report.csv`, `-report.html` triplet per suite, copied from
   `BenchmarkDotNet.Artifacts/results/`).

## Honest-reporting rules

Carried from the repo's established posture (the 2026-07-18 report is the model) and made
protocol:

1. No universal-superiority claims, ever.
2. Every workload where Heddle loses on **any** reported metric is reported in the results
   narrative as prominently as the workloads where it wins (named, with the numbers).
3. Numbers are dated and hardware-specific; every report states this and carries its reproduce
   command.
4. The allocation label and (where applicable) the encoded caveat appear verbatim.
5. A suite whose parity gate failed publishes **no numbers** — the report either omits the suite
   with a stated reason or does not publish.
6. Excluded controlled-track cells print `excluded — documented evidence` with a link to the
   evidence record (never a blank cell, never a curve-graded number).

## The protocol's first exercise (this phase)

Phase 1 closes by executing this protocol once, intra-.NET, over all eight workloads:

- Suites: `TextRenderBenchmarks` (composed-page), `SubstitutionRenderBenchmarks`,
  `LoopRenderBenchmarks`, plus the five new benchmark classes
  ([README implementation plan](README.md#implementation-plan)); `net10.0`, `-c Release`,
  BenchmarkDotNet 0.15.8, `[MemoryDiagnoser]` on.
- Published under `docs/benchmarks/<run-date>/` in the format above. This publication is the
  **source of the Heddle reference rows** phases 2–6 excerpt (rule 1), which is why it must land
  before any ecosystem report.
- Its `## The workloads` section names each workload's owned dimension — satisfying the plan's
  success criterion that each new workload's documentation names its dimension — and its
  encoded-suite results carry the caveat text.
