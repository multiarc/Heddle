# Cross-stack benchmark run — 2026-08-03

This run measures **fifteen template engines across six ecosystems** — .NET, Rust, the JVM,
JS/Node, Python and Go — over the program's **eight protocol workloads**, in **two tracks**, on
one machine in one session finishing 2026-08-03 13:20:22. Each ranked table carries sixteen rows:
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
functional-equivalence verifier — **143 cells, 143 green**. Every recorded step — twelve gates,
the full measurement phase, the artifact copies and the report consolidation — exited 0, see
[summary.txt](summary.txt) — with one honesty caveat on the two JS render steps, which reported
success while measuring less than they were asked to ([Limitations](#limitations), item 3).

**Earlier reports.** `docs/benchmarks` keeps the latest run only
([ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger)). The reports of
2026-07-11, 2026-07-18, 2026-07-25 and 2026-08-02 are removed, not corrected; their numbers were
honest measurements of harnesses that no longer exist, and nothing here may be compared against
them.

## Environment

```
Windows 11 (10.0.26200.8894/25H2), AMD Ryzen 9 9950X, 1 CPU, 16 physical / 32 logical cores
protocol machine (metrics-protocol Q1.6) · no tuned-state capture on this platform (posture is procedural)
run finished 2026-08-03 13:20:22 · mode MEASUREMENT · budget short · six ecosystems
repo commit not captured by the runner; 5cd58406 was the newest commit when the run started
```

| Ecosystem | Harness | Toolchain as run |
| --- | --- | --- |
| .NET | BenchmarkDotNet 0.15.8, `ShortRun(LaunchCount=3, WarmupCount=3, IterationCount=3)`, `[MemoryDiagnoser]` | SDK 10.0.302 · .NET 10.0.10, X64 RyuJIT x86-64-v4 |
| Rust | Criterion, warmup 5 s + measure 26 s, 100 samples | rustc 1.97.1 (pinned by `rust-toolchain.toml`) |
| JVM | JMH 1.37, `Mode.AverageTime`, 5 forks × (1×2 s warmup + 5×1 s measure), `-prof gc` | JDK 25.0.4 LTS |
| JS | mitata 1.0.34 — **one pass per render track**; the intended 38-pass aggregation aborted (see [Limitations](#limitations)) | node v26.4.0 |
| Python | pyperf 2.10.0, 20 processes × 6 values × 1 warmup, `--affinity=4` | CPython 3.13.0 |
| Go | `go test -bench -count=28 -benchtime=1s` + benchstat | go1.26.5, templ v0.3.1020 |

Run lengths come from the **`short` measurement budget**, whose unit is **one engine, not one
ecosystem** ([ledger E6/E13/E14](../../../docs/spec/records.md#cross-spec-amendments-ledger)):
each engine's sixteen cells get ~10 minutes, so the .NET leg — six engines plus the Heddle-only
techniques, cold and internal sidebars — is about three times the other legs by construction. The
session ran 10:29:38 → 13:20:22, **2 h 51 min**, and these are the first measured leg durations on
the protocol machine (E14's projected table should be checked against them):

| Ecosystem | Measure phase | | Ecosystem | Measure phase |
| --- | ---: | --- | --- | ---: |
| .NET | 95.0 min | | Python | 16.4 min |
| Rust | 18.5 min | | Go | 19.6 min |
| JVM | 19.8 min | | JS | 0.8 min (aborted repeats) |

The .NET figure splits into 73.9 min for the eight cross-stack suites and 21.1 min for the three
Heddle-only sidebars.

### Toolchain drift from the pins — read this before the rankings

Two of the seven pins were not met. The master runner *warns* rather than halts on a pin delta
(standing posture SR-3), so the run proceeded and the deltas are recorded here alongside the
machine-readable [toolchain.json](toolchain.json):

| Toolchain | Pinned | Actually ran | Note |
| --- | --- | --- | --- |
| .NET SDK | (observed line recorded) | 10.0.302 · runtime .NET 10.0.10 | no drift |
| Rust | 1.97.1 | rustc 1.97.1 | exact |
| JDK | Temurin 25 | JDK 25.0.4 LTS | on pin (major); distribution differs — [toolchain.json](toolchain.json) records the `java version` banner of an Oracle JDK, where Temurin reports `openjdk version` |
| Node.js | v24.18.0 | **v26.4.0** | **two major versions ahead of the pin.** The JS rows ran on a newer V8 than the program pinned, so they describe node 26, not the pinned runtime |
| CPython | 3.14.6 | **3.13.0** | **one minor version behind.** 3.14 carries interpreter performance work, so the Python rows are a pessimistic reading of Jinja2 and Mako |
| Go | go1.26.x | go1.26.5 | on pin |
| templ | v0.3.1020 | v0.3.1020 | exact |

Neither delta affects the *within-ecosystem* comparison — both engines of a drifted ecosystem ran
on the identical toolchain. Both weaken the **cross-stack** rows of their ecosystem as claims
about that ecosystem's pinned state.

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

Full tables, per workload and per ecosystem, plus the allocation sidebars and the plausibility
register: **[consolidated tables](consolidated-tables.md)**.

## Findings

The tables below are **generated** by `benchmarks/report/consolidate.py` and embedded here — no
figure on this page is transcribed by hand. Regenerate with
`python benchmarks/report/consolidate.py docs/benchmarks/2026-08-03`; verify with `--check`.

<!--@include: ./summary-tables.md-->

### What Tier 1 shows

Heddle is **fastest of the six .NET engines on all five** realistic-sized workloads, by **2.31× to
3.50×** — widest on `fragment-heavy` (per-call composition), narrowest on `fortunes-encoded`
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

**The .NET idiomatic anchor exists now.** The rebuilt .NET leg measures both fairness tracks
([ledger E12](../../../docs/spec/records.md#cross-spec-amendments-ledger) discharged Phase 1
D15's controlled-only scope), and the idiomatic tables anchor to a real Heddle idiomatic row: on
every Tier 1 workload it prices Heddle's documented authoring style within 8% of the controlled
row (ratio 0.95–1.08 in the generated tables).

### What this run replaces, and why the .NET field moved

The removed 2026-08-02 report measured the engine and harness **as they stood before** the
expression-tier parity closures and the encoder fast path landed, and its published anchor was
the harness's pre-flip sink. In that run Heddle trailed Handlebars.Net on three of the five Tier 1
workloads; in this run, measured after those changes, every Tier 1 row leads the .NET field. That
is provenance, not a comparison: the program's standing rule is that **figures are never mixed
across runs**, the old directory is removed under
[ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger) rather than corrected,
and no cross-run delta is quoted here.

What this run's gate states about execution tiers: **five of the eight workloads render
precompiled** (`trivial-substitution`, `large-loop`, `mixed-page`, `conditional-heavy`,
`fragment-heavy`); `composed-page`, `fortunes-encoded` and `encoded-loop` are refused by
precompilation and render on the runtime backend — the two tiers are byte-identical by contract,
and the gate's `PRECOMPILED-COVERAGE` line records the split every run.

### What Tier 2 shows, and how to read it

- **`encoded-loop` is the one workload where Heddle is not the fastest .NET engine.**
  Handlebars.Net leads it at 0.97×, and Razor sits at 0.99× — inside overlapping dispersion.
  Handlebars.Net led this workload on the 2026-07-25 run and on the replaced 2026-08-02 run as
  well, across two rebuilds of the .NET harness, so the *ordering* is a reproduced result rather
  than noise. It is escaping throughput over a 5,000-row payload — an 852 KB output no page
  produces, but a real signal about the encoder's bulk path.
- **`composed-page` is where Heddle's cross-stack rank collapses to #11**, and that number is
  *not* a statement about composition machinery. It is an allocator cliff plus a degenerate
  workload — see [Sizing regimes](#sizing-regimes-and-the-85-000-byte-boundary) and
  [The `composed-page` caveat](#the-composed-page-caveat). Do not quote the #11 without them.
- `large-loop` at #6 is the least distorted of the three: it crosses the LOH boundary, but it also
  contains genuine per-row work, so the allocator is a smaller share of the measurement.

### Razor is measured on every workload now

Since the .NET leg rebuild
([ledger E12](../../../docs/spec/records.md#cross-spec-amendments-ledger)), ASP.NET Core Razor is
a full member of all eight workloads and both tracks, under the same parity gate as every other
engine — the earlier single-workload Razor figure is superseded. On the controlled track Heddle
leads it by **2.52× to 36.98×** at Tier 1 sizes and **1.84×** on `composed-page`; on
`encoded-loop` Razor is marginally ahead (0.99×, within overlapping dispersion). Both engines pay
the same Large Object Heap cost on the Tier 2 rows, which compresses margins there relative to
Tier 1.

### The JS leg is one pass per track in this run

The intended shape was 38 aggregated passes per render track (ledger E6/E14), each an independent
process, with the Phase 4 D13 stability verdict computed from them. That did not happen: the
`run.ps1` launcher read back a null exit code from its first pass, printed
`repeat 1/38 failed with exit code  - aborting the sequence`, and — because the null propagated as
success — the runner recorded both steps `OK`. The published JS artifacts are therefore **one
mitata pass per track**:

- **What stands:** the JS gate passed 16/16 cells, and the pass that produced the published
  artifacts asserted `MATERIALISATION-CHECK: clean` before writing them — every JS figure is
  materialised output, not the V8 `ConsString` rope artifact
  ([ledger E4](../../../docs/spec/records.md#cross-spec-amendments-ledger)).
- **What does not:** there is **no D13 stability verdict** for this run, and the JS dispersion
  column in the tables is a single pass's min…max, not cross-process variation. The JS rows
  already carry `reach/context` standing; in this run they are additionally single-pass numbers,
  and the node pin drift applies on top. Read them accordingly.

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
(2.8076 per thousand operations each) — essentially every collection is a full one.

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
  `RENDERED_CHARS` constants. In this run the highest implied throughput anywhere is **48.0 B/ns**
  (Askama, `composed-page` idiomatic), under the 50 B/ns plausibility ceiling; **no cell trips the
  ceiling** — see the [plausibility register](consolidated-tables.md#plausibility-register).
- The fixture also exercises a known `@<<` layout-extend limitation documented in
  [GoldenCorpus/README.md](../../../benchmarks/dotnet/GoldenCorpus/README.md), which is why the
  golden output is the fragment sequence rather than a full chrome-bearing page.

## Limitations

1. **The protocol machine, but unreplicated.** This run satisfies Q1.6's machine pin — the first
   published run that does — but no finding here has been reproduced on a second platform: the
   Linux cross-check (Phase 8) has no current published run, and every earlier report is removed
   under [ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger). Treat the
   ordering as this run's result.
2. **The `short` budget, not `baseline`.** Every leg ran the ~10 min-per-engine `short` shape
   (E6/E13/E14): .NET at `ShortRun` with `LaunchCount 3`, JMH at 5 forks × 5×1 s, Criterion at
   26 s, Go at `-count=28`, pyperf at 6 values. The ~30 min-per-engine `baseline` shape has never
   been measured on this machine, and E14's projected durations are unchecked beyond the leg
   totals recorded above.
3. **The JS leg collapsed to one pass per track, and the runner called it green.** The `run.ps1`
   launcher read a null exit code back from its first pass, aborted the 38-pass sequence, and
   propagated the null as exit 0, so `summary.txt` records both render steps `OK`. Consequences:
   no cross-process aggregation, no D13 stability verdict artifact, and a JS dispersion column
   that is within-pass min…max. The launcher defect must be fixed before the next run; a repeat
   sequence that aborts should fail its step.
4. **Windows has no captured tuned state.** The Linux runner records governor/boost and offers an
   isolation boot entry; this platform's runner prints procedural rules (quiet machine, AC power,
   High Performance plan, priority classes) and records nothing about the machine state beyond
   the toolchain block. Absolute times are not comparable with tuned boost-off runs on the same
   physical box.
5. **Two toolchains drifted from their pins** — node two majors ahead, CPython one minor behind
   (see the drift table). Within-ecosystem comparisons are unaffected; cross-stack rows for JS
   and Python are provisional as statements about those ecosystems' pinned state.
6. **Measurement conditions are not symmetric across harnesses** — pyperf pinned to one logical
   core, the other five unpinned; whether pyperf's elevated-priority path was active is not
   recorded in the artifacts.
7. **Three workloads render on Heddle's runtime backend**, because precompilation refuses them
   (`composed-page`, `fortunes-encoded`, `encoded-loop` — the gate's `PRECOMPILED-COVERAGE`
   line). The two tiers are byte-identical by contract, but readers should know both encoded
   workloads fall in this group.
8. **Encoded workloads are confined** — see the caveat under [The workloads](#the-workloads-and-why-they-are-presented-in-two-tiers).
9. **Go contributes one engine across two surfaces** — `text/template` on the six raw workloads
   and `html/template` on the two encoded ones — plus templ. Read the Go rows accordingly.
10. **JS and Python carry `reach/context` evidence standing**, not `fair fight`; their rows answer
    "what does this ecosystem reach" rather than ranking engines against the others. For JS in
    this run, item 3 applies on top.
11. **One machine, one date.** Numbers are hardware-, platform- and date-specific. Run the suite
    on your own target with a page shaped like your real one before quoting anything.
12. **The two-tier presentation is a derived ordering, not a protocol change.** The normative
    workload numbering is unchanged and still recorded in `manifest.json` and the phase specs.
    `consolidate.py` classifies a workload by whether its rendered output reaches 85,000 bytes as
    UTF-16 and orders every generated table accordingly; nothing in this report is ordered by
    hand.
13. **The tier boundary depends on measured constants carried from an earlier run.** The
    `RENDERED_CHARS` values in `consolidate.py` were measured on 2026-07-25 and were not
    re-measured here; they must be re-measured if templates or models change. A workload missing
    from them falls back to the golden size and is footnoted in the generated tables rather than
    silently misclassified.
14. **The mitata heap column is not a materialisation test.** It reports a *net* heap delta across
    a batch, so garbage reclaimed inside the batch is invisible to it and a correct render can
    show a near-zero figure. The authoritative control is the harness-side
    `MATERIALISATION-CHECK`; the [consolidated tables](consolidated-tables.md) state this in full.
15. **No figure here may be compared with any removed report.** The replaced 2026-08-02 run
    measured a different engine state through a different anchor sink; it is removed under
    [ledger E15](../../../docs/spec/records.md#cross-spec-amendments-ledger), not corrected. Do
    not mix figures across runs — this run's tables are the only ones this repository publishes.
