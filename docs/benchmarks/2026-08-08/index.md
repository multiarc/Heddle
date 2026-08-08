# Cross-stack benchmark run — 2026-08-08

This run measures **fifteen template engines across six ecosystems** — .NET, Rust, the JVM,
JS/Node, Python and Go — over the program's **eight protocol workloads**, in **two tracks**, on
one machine in one session finishing 2026-08-08 04:45:52. Each ranked table carries sixteen rows:
the .NET leg contributes six engines, and Go's standard library appears as `text/template` on raw
workloads and `html/template` on encoded ones. Every ecosystem ran its own standard harness
(BenchmarkDotNet, Criterion, JMH, mitata, pyperf, `go test` + benchstat) and contributed that
harness's default central-tendency statistic, per the
[metrics protocol](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md).

Reproduce the whole run:

```powershell
powershell -ExecutionPolicy Bypass -File benchmarks\run-all.ps1
```

Per-ecosystem invocations are in [benchmarks/README.md](../../../benchmarks/README.md); the
.NET anchor alone is
`dotnet run -c Release --project benchmarks/dotnet -- bench-crossstack --filter *<Suite>*`.

> **This is the protocol machine.** The program pins its cross-compared runs to the AMD Ryzen 9
> 9950X / Windows 11 box
> ([metrics-protocol Q1.6](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md#machine-and-environment-q16)),
> and that is where this run executed. What Windows does **not** have is the Linux runner's
> captured tuned state: there is no governor/boost control and no CPU-isolation boot entry on this
> side, so the machine posture is procedural — quiet machine, AC power, High Performance power
> plan, per-harness High priority classes — printed by the runner as rules rather than recorded as
> state. And the run is **unreplicated**: no cross-check on a second platform is currently
> published, so treat the ordering as this run's result rather than as an established fact about
> the engines. See [Limitations](#limitations).

**The gate.** Every controlled-track cell passed the byte gate under the parity contract's closed
normalization list **N1, N2, N3, N3b, N4, N5**. N3b is the program-wide comparison-time rule that
strips **every** whitespace run, anywhere, to nothing from both the oracle and the candidate — so
the gate compares **non-whitespace bytes only**, and "byte-identical" throughout this report means
byte-identical after that disclosed pipeline, with entity spellings canonicalized by N5.
Whitespace differences therefore never disqualify a cell anywhere in this program. Encoded cells
additionally cleared the security floor, and every idiomatic-track cell passed the
functional-equivalence verifier — **143 cells, 143 green**. Every recorded step — thirteen gates,
the full measurement phase, the artifact copies and the report consolidation — exited 0, see
[summary.txt](summary.txt). Unlike the replaced report there is no honesty caveat to attach: the
JS launcher defect that let an aborted repeat sequence report success was fixed before this run,
and the two JS render steps each ran their full 38-pass shape
([The JS leg ran its full 38-pass shape](#the-js-leg-ran-its-full-38-pass-shape-this-time)).

**Earlier reports.** `docs/benchmarks` keeps the latest run only
([ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger)). The reports of
2026-07-11, 2026-07-18, 2026-07-25, 2026-08-02 and 2026-08-03 are removed, not corrected; their
numbers were honest measurements of their own harnesses and toolchains, and nothing here may be
compared against them.

## Environment

```
Windows 11 (10.0.26200.8894/25H2), AMD Ryzen 9 9950X, 1 CPU, 16 physical / 32 logical cores
protocol machine (metrics-protocol Q1.6) · no tuned-state capture on this platform (posture is procedural)
run finished 2026-08-08 04:45:52 · mode MEASUREMENT · budget short · six ecosystems
repo commit not captured by the runner; 7674be01 was the newest commit when the run started
```

| Ecosystem | Harness | Toolchain as run |
| --- | --- | --- |
| .NET | BenchmarkDotNet 0.15.8, `ShortRun(LaunchCount=3, WarmupCount=3, IterationCount=3)`, `[MemoryDiagnoser]` | SDK 10.0.302 · .NET 10.0.10, X64 RyuJIT x86-64-v4 |
| Rust | Criterion, warmup 5 s + measure 26 s, 100 samples | rustc 1.97.1 (pinned by `rust-toolchain.toml`) |
| JVM | JMH 1.37, `Mode.AverageTime`, 5 forks × (1×2 s warmup + 5×1 s measure), `-prof gc` | JDK 25.0.4 LTS |
| JS | mitata 1.0.34 — **38 aggregated passes per render track**, each a separate node process | node v24.19.0 |
| Python | pyperf 2.10.0, 20 processes × 6 values × 1 warmup, `--affinity=4` | CPython 3.14.7 |
| Go | `go test -bench -count=28 -benchtime=1s` + benchstat | go1.26.5, templ v0.3.1020 |

Run lengths come from the **`short` measurement budget**, whose unit is **one engine, not one
ecosystem** ([ledger E6/E13/E14](../../../docs/spec/records.md#cross-spec-amendments-ledger)):
each engine's sixteen cells get ~10 minutes, so the .NET leg — six engines plus the Heddle-only
techniques, cold and internal sidebars — is about three times the other legs by construction. The
session ran 01:34:09 → 04:45:52, **3 h 12 min**. These are the second measured leg durations on
the protocol machine and the first with a real JS figure — the replaced report's JS leg aborted
after one pass, so its 0.8 min told E14's projection nothing:

| Ecosystem | Measure phase | | Ecosystem | Measure phase |
| --- | ---: | --- | --- | ---: |
| .NET | 96.5 min | | Python | 17.0 min |
| Rust | 18.5 min | | Go | 19.2 min |
| JVM | 19.9 min | | JS | 19.9 min |

The .NET figure splits into 75.4 min for the eight cross-stack suites and 21.1 min for the three
Heddle-only sidebars.

### Toolchains: on pin, no drift — a first

Every row of [toolchain.json](toolchain.json) records `drift: false`. This is the **first
published run of this program in which no toolchain drifted from its pin**, and it follows two
maintainer rulings issued on 2026-08-08, before the run:

- **Node.js is pinned to the major line `v24.x`**
  ([ledger E18](../../../docs/spec/records.md#cross-spec-amendments-ledger)). The property the
  pin holds constant — the V8 generation the JS rows describe — is major-versioned, and the old
  exact-string pin was only ever satisfiable by disabling engine enforcement. This run's
  **v24.19.0** satisfies the pin with `engine-strict` ON.
- **CPython is pinned to the minor line `3.14.x`**
  ([ledger E19](../../../docs/spec/records.md#cross-spec-amendments-ledger)) — same reasoning,
  minor-versioned: micro releases are bugfix. This run's **3.14.7** satisfies the pin.

Exactness is relocated, not lost: the precise versions a run actually used are recorded in
[toolchain.json](toolchain.json) and in the table above, and cross-run comparisons quote the
recorded version, never the pin.

| Toolchain | Pinned | Actually ran | Note |
| --- | --- | --- | --- |
| .NET SDK | (observed line recorded) | 10.0.302 · runtime .NET 10.0.10 | no drift |
| Rust | 1.97.1 | rustc 1.97.1 | exact |
| JDK | Temurin 25 | JDK 25.0.4 LTS | on pin (major); distribution differs — [toolchain.json](toolchain.json) records the `java version` banner of an Oracle JDK, where Temurin reports `openjdk version` |
| Node.js | v24.x (E18) | v24.19.0 | on pin, `engine-strict` enforced |
| CPython | 3.14.x (E19) | 3.14.7 | on pin |
| Go | go1.26.x | go1.26.5 | on pin |
| templ | v0.3.1020 | v0.3.1020 | exact |

Two consequences for reading the tables, relative to the replaced report: the JS rows describe
the pinned node 24 / V8 generation rather than a newer runtime, and the Python rows describe the
pinned CPython 3.14 interpreter generation — the earlier caveat that Jinja2 and Mako were read
pessimistically on 3.13 no longer applies.

### Priority and affinity are not symmetric across harnesses

pyperf ran pinned to a single logical core via `--affinity=4`. The other five harnesses ran
unpinned at High priority class. This is an asymmetry in the measurement conditions, not a
property of the engines, and it is one reason the cross-stack Python rows carry the
`reach/context` label rather than fair-fight standing. Whether pyperf's elevated-priority path
(which needs an elevated shell) was active is not recorded in the artifacts.

## The workloads, and why they are presented in two tiers

Eight workloads. "Golden output" is the byte length of the shared oracle in
[benchmarks/dotnet/GoldenCorpus](../../../benchmarks/dotnet/GoldenCorpus/README.md), verified by
SHA-256 against `manifest.json` by every harness before it measures anything.

This report presents them in **two tiers rather than the normative protocol order**, because three
of the eight produce outputs far outside the size of a real page, and at those sizes the
measurement stops being dominated by template execution and starts being dominated by the
allocator. The boundary is not editorial: it is the **85,000-byte Large Object Heap threshold** in
the CLR, and the five workloads below it behave in one regime while the three above it behave in
another. The evidence is in
[Sizing regimes and the 85,000-byte boundary](#sizing-regimes-and-the-85-000-byte-boundary).

### Tier 1 — realistic sizing

Outputs from 338 B to 15.5 KB — the range a real page, partial or fragment actually occupies.
**These are the primary results of this run.**

| # | Workload | What it measures | Suite | Golden output |
| ---: | --- | --- | --- | ---: |
| 1 | **`trivial-substitution`** | the per-render fixed-overhead floor | raw | 338 B |
| 2 | **`fortunes-encoded`** | escaping correctness at micro scale, 12 rows | **encoded** | 1,156 B |
| 3 | **`fragment-heavy`** | per-call composition, 48 calls | raw | 4,730 B |
| 4 | **`mixed-page`** | a realistic mixture, 36 rows | raw | 9,712 B |
| 5 | **`conditional-heavy`** | branch dispatch, 200 branches | raw | 15,549 B |

### Tier 2 — edge-case sizing

Rendered outputs from 54 KB to 852 KB. Every one of them exceeds 85,000 bytes once materialised
as UTF-16, so for the .NET engines each render lands on the Large Object Heap. **Read these with
the caveats attached to each**; they are stress tests, not page renders.

| # | Workload | What it measures | Suite | Golden output | Rendered | As UTF-16 |
| ---: | --- | --- | --- | ---: | ---: | ---: |
| 6 | **`composed-page`** | layout/section/component composition | raw | 34,847 B | 54,401 chars | 108,802 B |
| 7 | **`large-loop`** | bulk iteration over 5,000 rows | raw | 192,780 B | 192,780 chars | 385,560 B |
| 8 | **`encoded-loop`** | escaping throughput, 5,000 rows | **encoded** | 831,685 B | 851,685 chars | 1,703,370 B |

`composed-page` carries a second, independent caveat beyond its size — it contains **no per-item
computation at all**. See [The `composed-page` caveat](#the-composed-page-caveat).

Every workload carries a controlled byte gate and an idiomatic verifier; the two encoded workloads
additionally carry the security floor. The [consolidated tables](consolidated-tables.md) are the
machine-generated, `--check`-reproducible appendix; since
[ledger E7](../../../docs/spec/records.md#cross-spec-amendments-ledger) their ordering is also
tier-derived — computed from rendered size by `consolidate.py`, never declared per workload —
while the normative protocol numbering 1–8 stays recorded in `manifest.json` and the phase specs.

> Encoded workloads confine untrusted data to HTML text and attribute-value contexts, where flat
> and contextual encoders emit the same escaped output. These numbers therefore measure escaping
> cost in confined contexts and cannot show the differentiating benefit of contextual encoding; a
> contextual encoder's encoded-suite result must not be read as 'context awareness is pure
> overhead.'

## How to read these tables

> **Controlled track** — byte-identical, equivalently-authored templates under the parity
> contract's closed normalization pipeline. These numbers answer *"how fast is the engine"* on
> identical work. They may be used to compare engine execution speed on a given workload — and
> nothing else.

> **Idiomatic track** — functionally-equivalent templates written the way each engine's own
> documentation teaches, verified by the functional-equivalence gate. These numbers answer
> *"how fast is the practitioner experience."* They may be used to compare what a practitioner
> gets writing each engine's documented style; they must not be read as engine execution-speed
> rankings.

There is **no aggregate score, no overall winner and no single-number verdict** in this program.
One ranking per workload, and that is all that may be quoted.

> **The two `Heddle (utf8 sink)` / `Heddle (textwriter sink)` rows are techniques, not
> competitors.** They stream the render into a caller-owned buffer that is allocated once in
> untimed setup, pre-sized past the output's high-water mark, and reused across iterations —
> the sink's output is byte-verified against the gated string render in the same process before
> anything is timed, so the bytes are produced on every iteration, but nothing is materialised.
> Every competitor row, Heddle's own anchor included, materialises a string. Consequently their
> time is a streaming floor, their `Allocated` is steady-state GC pressure rather than the cost
> of owning the output (the buffer's working set is on the order of the output itself), and
> both columns compare different work from every other row. They are **excluded from every
> ranking and every margin** in this report; the tables mark them `‡` with the same caveat
> where they appear.

Full tables, per workload and per ecosystem, plus the allocation sidebars and the plausibility
register: **[consolidated tables](consolidated-tables.md)**.

## Findings

The tables below are **generated** by `benchmarks/report/consolidate.py` and embedded here — no
figure on this page is transcribed by hand. Regenerate with
`python benchmarks/report/consolidate.py docs/benchmarks/2026-08-08`; verify with `--check`.

<!--@include: ./summary-tables.md-->

### What Tier 1 shows

Heddle is **fastest of the six .NET engines on all five** realistic-sized workloads, by **2.25× to
3.64×** — widest on `fragment-heavy` (per-call composition), narrowest on `fortunes-encoded`
(escaping at micro scale).

Cross-stack it is **top-4 on all five**: **#2** on `fortunes-encoded` and `mixed-page` (behind
only **Askama**, the Rust compile-time engine), **#3** on `trivial-substitution` and
`fragment-heavy`, **#4** on `conditional-heavy`. The only engines ahead of it anywhere in Tier 1
are Askama (on all five), **JTE** (JVM, compiled to Java source — on two) and **eta** (JS — on
two). It is ahead of Tera, templ, `text/template`, handlebars, Thymeleaf, Jinja2, Mako and every
other .NET engine on all five.

**Askama is the only engine faster than Heddle on all eight workloads.** It is a Rust compile-time
template engine — the same architectural bet Heddle makes, compile the document rather than
interpret it, executed in a language without a managed runtime.

**The .NET idiomatic anchor holds.** The rebuilt .NET leg measures both fairness tracks
([ledger E12](../../../docs/spec/records.md#cross-spec-amendments-ledger) discharged Phase 1
D15's controlled-only scope), and the idiomatic tables anchor to a real Heddle idiomatic row: on
every Tier 1 workload it prices Heddle's documented authoring style within 10% of the controlled
row (ratio 0.99–1.10 in the generated tables).

### What this run replaces, and why

This run replaces the 2026-08-03 report under
[ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger)'s latest-run-only rule,
and the replacement is a **measurement-quality re-issue, not a new engine state**. Between the
two runs nothing under `src/` changed — every intervening commit touched the harness, the pins or
the report: the JS launcher fix (a null exit code no longer propagates as success), the E18/E19
pin re-scopes, and report annotations. What this run repairs is the measurement itself: the JS
leg ran its full 38-pass aggregation instead of collapsing to one pass per track, and all seven
toolchains ran on pin instead of two drifting. The .NET field ordering this run reports matches
what the replaced report reported, but that is provenance, not a comparison: the program's
standing rule is that **figures are never mixed across runs**, the old directory is removed
rather than corrected, and no cross-run delta is quoted here.

What this run's gate states about execution tiers: **five of the eight workloads render
precompiled** (`trivial-substitution`, `large-loop`, `mixed-page`, `conditional-heavy`,
`fragment-heavy`); `composed-page`, `fortunes-encoded` and `encoded-loop` are refused by
precompilation and render on the runtime backend — the two tiers are byte-identical by contract,
and the gate's `PRECOMPILED-COVERAGE` line records the split every run.

### What Tier 2 shows, and how to read it

- **`encoded-loop` is the one workload where Heddle is not the fastest .NET engine.**
  Handlebars.Net leads it at 0.97×, and Razor sits at 1.00× (1,761.1 μs against Heddle's
  1.766 ms) — inside overlapping dispersion. Handlebars.Net led this workload on the removed
  2026-07-25, 2026-08-02 and 2026-08-03 runs as well — across two rebuilds of the .NET harness —
  so the *ordering* is a reproduced result rather than noise. It is escaping throughput over a
  5,000-row payload — an 852 KB output no page produces, but a real signal about the encoder's
  bulk path.
- **`composed-page` is where Heddle's cross-stack rank collapses to #11**, and that number is
  *not* a statement about composition machinery. It is an allocator cliff plus a degenerate
  workload — see [Sizing regimes](#sizing-regimes-and-the-85-000-byte-boundary) and
  [The `composed-page` caveat](#the-composed-page-caveat). Do not quote the #11 without them.
- `large-loop` at #6 is the least distorted of the three: it crosses the LOH boundary, but it also
  contains genuine per-row work, so the allocator is a smaller share of the measurement.

### Razor is measured on every workload

Since the .NET leg rebuild
([ledger E12](../../../docs/spec/records.md#cross-spec-amendments-ledger)), ASP.NET Core Razor is
a full member of all eight workloads and both tracks, under the same parity gate as every other
engine — the earlier single-workload Razor figure is superseded. On the controlled track Heddle
leads it by **2.51× to 37.07×** at Tier 1 sizes and **1.90×** on `composed-page`; on
`encoded-loop` Razor is marginally ahead (a 1.00× ratio in the generated tables, within
overlapping dispersion). Both engines pay the same Large Object Heap cost on the Tier 2 rows,
which compresses margins there relative to Tier 1.

### The JS leg ran its full 38-pass shape this time

The replaced report disclosed that its JS leg had collapsed to one mitata pass per track: the
`run.ps1` launcher read a null exit code back from its first pass, aborted the 38-pass sequence,
and propagated the null as success. That defect was fixed before this run, and this run executed
the intended shape (ledger E6/E14): **38 consecutive passes per render track, each a separate
node process** — ~588 s per track — with the Phase 4 D13 stability verdict computed from them.

The verdicts, from the published stability artifacts:

- **Controlled track — `STABILITY: verified-with-disclosure`**
  ([stability-summary-controlled.md](js/stability-summary-controlled.md)): across 16 cells the
  cross-pass RSD of mitata's `avg` is **median 1.41%, max 5.11%** — the max is handlebars on
  `mixed-page`, the single cell in D13's 5–10% disclosure band (none above 10%, which would have
  blocked publication). Per D13 that band is publishable **only if the report carries the table**,
  so it is embedded below.
- **Idiomatic track — `STABILITY: verified`**
  ([stability-summary-idiomatic.md](js/stability-summary-idiomatic.md)): **median 1.77%, max
  4.77%** (handlebars on `large-loop`); no cell above 5%.

The published `controlled.json` / `idiomatic.json` carry the aggregate — `stats.avg` is the
median of the per-pass averages and `stats.min`/`stats.max` span the passes — so the JS
dispersion column in this report's tables is **cross-process variation**, not a single pass's
min…max. Every one of the 76 passes asserted `MATERIALISATION-CHECK: clean` before writing its
artifact, so every JS figure is materialised output, not the V8 `ConsString` rope artifact
([ledger E4](../../../docs/spec/records.md#cross-spec-amendments-ledger)). The JS rows keep
their `reach/context` evidence standing. The verdict is independently recomputable from the
published aggregates alone — each cell in [controlled.json](js/controlled.json) /
[idiomatic.json](js/idiomatic.json) carries its `perPassAvg` array, the 38 per-pass averages
the RSD is computed over; the raw per-pass captures stay with the run's out directory and are
not republished here, the same way the runner's logs are not.

<!--@include: ./js/stability-summary-controlled.md-->

## Sizing regimes and the 85,000-byte boundary

The CLR allocates any object of **85,000 bytes or more on the Large Object Heap**. .NET strings
are UTF-16, so a rendered page crosses that line at **42,500 characters** — and every render that
crosses it allocates on the LOH and drives a full, blocking Gen2 collection. Three of the eight
workloads are above the line once materialised; five are comfortably below it (31,098 B as UTF-16,
the largest), which is why the Tier 1 rows compare cleanly.

The magnitude of the cliff was measured when the boundary was first identified, on the 2026-07-25
run's box: string-copy throughput fell from 40.3 B/ns just below the threshold to 8.9 B/ns just
above it — a 4.5× step. That measurement is recorded in
[ledger E7](../../../docs/spec/records.md#cross-spec-amendments-ledger) and was **not re-derived
on this machine**; what this run's own artifacts show is the consequence: in the
[allocation sidebar](consolidated-tables.md#per-ecosystem-sidebars--not-cross-comparable),
Heddle's `composed-page` render reports Gen0, Gen1 and Gen2 collection counts that are **equal**
(3.1128 per thousand operations each) — essentially every collection is a full one.

None of the other five ecosystems has this cliff. Their outputs are UTF-8 or Latin-1 and stay on
the normal heap. So on Tier 2, the .NET column is paying a cost that is structural to the runtime
and absent everywhere else.

### The `composed-page` caveat

Beyond its size, `composed-page` has a second problem: **it contains no per-item computation.**
The output is 17 pre-existing string fragments concatenated in order — no loop body, no branch,
no escaping, no formatting. Every compiled engine reduces it to about 17 `memcpy` calls, so the
row measures memory bandwidth and allocator behaviour, not template execution.
[Ledger E7](../../../docs/spec/records.md#cross-spec-amendments-ledger) records the floor
measurements behind that analysis (a hand-written .NET `string.Concat` of the same fragments sits
within ~2× of Heddle's figure); they were taken on the 2026-07-25 run's box and are cited here as
prior evidence, not re-measured. The `#11 of 16` rank is real as a measurement and misleading as a
statement about the engine.

Two further notes on this row:

- **The `implied B/ns` numerator is the rendered size, not the golden.** The golden oracle is
  stored in its normalized form — inter-tag whitespace is collapsed before export — so on
  `composed-page` it is 1.56× smaller than what the engines emit (34,847 B stored against 54,401
  chars rendered). The generated tables divide by the rendered size, carried in `consolidate.py`'s
  `RENDERED_CHARS` constants. In this run the highest implied throughput anywhere is **43.9 B/ns**
  — the `‡` Heddle textwriter-sink streaming row on `composed-page`, which is excluded from every
  ranking; the highest *ranked* cell is Askama's `composed-page` controlled row at **37.4 B/ns** —
  all under the 50 B/ns plausibility ceiling; **no cell trips the ceiling** — see the
  [plausibility register](consolidated-tables.md#plausibility-register).
- The fixture also exercises a known `@<<` layout-extend limitation documented in
  [GoldenCorpus/README.md](../../../benchmarks/dotnet/GoldenCorpus/README.md), which is why the
  golden output is the fragment sequence rather than a full chrome-bearing page.

## Limitations

1. **The protocol machine, but unreplicated.** This run satisfies Q1.6's machine pin, but no
   finding here has been reproduced on a second platform: the Linux cross-check (Phase 8) has no
   current published run, and every earlier report is removed under
   [ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger). Treat the ordering
   as this run's result.
2. **The `short` budget, not `baseline`.** Every leg ran the ~10 min-per-engine `short` shape
   (E6/E13/E14): .NET at `ShortRun` with `LaunchCount 3`, JMH at 5 forks × 5×1 s, Criterion at
   26 s, Go at `-count=28`, pyperf at 6 values, mitata at 38 passes per track. The
   ~30 min-per-engine `baseline` shape has never been measured on this machine, and E14's
   projected durations are unchecked beyond the leg totals recorded above.
3. **Windows has no captured tuned state.** The Linux runner records governor/boost and offers an
   isolation boot entry; this platform's runner prints procedural rules (quiet machine, AC power,
   High Performance plan, priority classes) and records nothing about the machine state beyond
   the toolchain block. Absolute times are not comparable with tuned boost-off runs on the same
   physical box.
4. **Measurement conditions are not symmetric across harnesses** — pyperf pinned to one logical
   core, the other five unpinned; whether pyperf's elevated-priority path was active is not
   recorded in the artifacts.
5. **One controlled JS cell sits in the D13 disclosure band** — handlebars on `mixed-page` at
   5.11% cross-pass RSD, which is why the controlled verdict is `verified-with-disclosure`
   rather than `verified` and why the stability table is carried in this report. No cell is
   above 10%.
6. **Three workloads render on Heddle's runtime backend**, because precompilation refuses them
   (`composed-page`, `fortunes-encoded`, `encoded-loop` — the gate's `PRECOMPILED-COVERAGE`
   line). The two tiers are byte-identical by contract, but readers should know both encoded
   workloads fall in this group.
7. **Encoded workloads are confined** — see the caveat under [The workloads](#the-workloads-and-why-they-are-presented-in-two-tiers).
8. **Go contributes one engine across two surfaces** — `text/template` on the six raw workloads
   and `html/template` on the two encoded ones — plus templ. Read the Go rows accordingly.
9. **JS and Python carry `reach/context` evidence standing**, not `fair fight`; their rows answer
   "what does this ecosystem reach" rather than ranking engines against the others.
10. **One machine, one date.** Numbers are hardware-, platform- and date-specific. Run the suite
    on your own target with a page shaped like your real one before quoting anything.
11. **The two-tier presentation is a derived ordering, not a protocol change.** The normative
    workload numbering is unchanged and still recorded in `manifest.json` and the phase specs.
    `consolidate.py` classifies a workload by whether its rendered output reaches 85,000 bytes as
    UTF-16 and orders every generated table accordingly; nothing in this report is ordered by
    hand.
12. **The tier boundary depends on measured constants carried from an earlier run.** The
    `RENDERED_CHARS` values in `consolidate.py` were measured on 2026-07-25 and were not
    re-measured here; they must be re-measured if templates or models change. A workload missing
    from them falls back to the golden size and is footnoted in the generated tables rather than
    silently misclassified.
13. **The mitata heap column is not a materialisation test.** It reports a *net* heap delta across
    a batch, so garbage reclaimed inside the batch is invisible to it and a correct render can
    show a near-zero figure. The authoritative control is the harness-side
    `MATERIALISATION-CHECK`; the [consolidated tables](consolidated-tables.md) state this in full.
14. **No figure here may be compared with any removed report.** The replaced 2026-08-03 run
    measured the same engine code, but through a JS leg that collapsed to one pass per track and
    with two toolchains off pin; it is removed under
    [ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger), not corrected. Do
    not mix figures across runs — this run's tables are the only ones this repository publishes.
