# Cross-stack benchmark run — 2026-07-25

This run measures **thirteen template engines across six ecosystems** — .NET, Rust, the JVM,
JS/Node, Python and Go — over the program's **eight protocol workloads**, in **two tracks**, on
one machine in one session finishing 2026-07-25 17:02:15. Every ecosystem ran its own standard
harness (BenchmarkDotNet, Criterion, JMH, mitata, pyperf, `go test` + benchstat) and contributed
that harness's default central-tendency statistic, per the
[metrics protocol](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md).

Reproduce the whole run:

```bash
./benchmarks/run-all.sh --tune-no-isolation --budget baseline
```

Per-ecosystem invocations are in [benchmarks/README.md](../../../benchmarks/README.md); the
.NET anchor alone is
`dotnet run -c Release --project src/Heddle.Performance -- --filter *<Suite>*`.

> **Read the platform caveat before the numbers.** This run is on **Linux**, and the program's
> protocol machine is the Windows 11 side of the same physical box
> ([metrics-protocol Q1.6](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md#machine-and-environment-q16)).
> It also does not follow the separate Linux cross-check protocol
> ([phase 8](../../../docs/spec/cross-stack-benchmarks/phase-8-linux-crosscheck/README.md)): it ran
> through `benchmarks/run-all.sh` with `taskset`+`nice` priority rather than Phase 8's plain-launch
> posture, and the box has no CPU-isolation boot entry. Every ranking here is internally
> consistent — one machine, one session, one tuned state, all engines measured against each other
> under identical conditions — and that is what the tables claim. What they do not claim is
> protocol standing. See [Limitations](#limitations).
>
> There is also **no Windows figure to compare against**: a Windows run was published on
> 2026-07-22, its results were subsequently found invalid, and its report has been withdrawn. The
> protocol-machine re-test is **TBD**. This is therefore the only published cross-stack run, and
> nothing here is corroborated by a second platform yet.

**The gate.** Every controlled-track cell passed the byte gate under the parity contract's closed
normalization list **N1, N2, N3, N3b, N4, N5**. N3b is the program-wide comparison-time rule that
strips **every** whitespace run, anywhere, to nothing from both the oracle and the candidate — so
the gate compares **non-whitespace bytes only**, and "byte-identical" throughout this report means
byte-identical after that disclosed pipeline, with entity spellings canonicalized by N5.
Whitespace differences therefore never disqualify a cell anywhere in this program. Encoded cells
additionally cleared the security floor. Every idiomatic-track cell passed the
functional-equivalence verifier. **All eleven gate steps were green, and all 38 measurement steps
exited 0** — see [summary.txt](summary.txt).

**Pre-protocol history.** The [2026-07-11](/benchmarks/2026-07-11/) and
[2026-07-18](/benchmarks/2026-07-18/) reports are the intra-.NET historical record. They are cited
as motivation for this program and are **not aggregated** into it.

## Environment

```
Ubuntu 24.04.4 LTS (Noble Numbat), Linux 6.14
AMD Ryzen 9 9950X, 1 CPU, 16 physical / 32 logical cores
governor=performance, cpufreq/boost=0, CPU isolation ABSENT (recorded delta)
run finished 2026-07-25 17:02:15 · mode MEASUREMENT · budget baseline · six ecosystems
```

| Ecosystem | Harness | Toolchain as run |
| --- | --- | --- |
| .NET | BenchmarkDotNet 0.15.8, `ShortRun(LaunchCount=1, WarmupCount=7, IterationCount=15)`, `[MemoryDiagnoser]` | SDK 10.0.110 · .NET 10.0.10, X64 RyuJIT x86-64-v4 |
| Rust | Criterion, warmup 4 s + measure 30 s, 100 samples | 1.97.1 (pinned by `rust-toolchain.toml`) |
| JVM | JMH 1.37, `Mode.AverageTime`, 3 forks × (1×2 s warmup + 9×1 s measure), `-prof gc` | OpenJDK 25.0.3 |
| JS | mitata 1.0.34, 54 aggregated passes per render track (median of per-pass `avg`) | node v24.18.0 |
| Python | pyperf 2.10.0, 20 processes × 9 values × 2 warmups | CPython 3.12.3 |
| Go | `go test -bench -count=42 -benchtime=1s` + benchstat | go1.26.5, templ v0.3.1020 |

Run lengths come from the **`baseline` measurement budget**
([ledger E6](../../../docs/spec/records.md#cross-spec-amendments-ledger)), which gives every
ecosystem a comparable share of machine time instead of whatever its harness defaulted to. The
result: **16.8–28.5 min per ecosystem**, a 1.7× spread, for a 2.36 h session.

| Ecosystem | Measure phase | | Ecosystem | Measure phase |
| --- | ---: | --- | --- | ---: |
| .NET | 16.8 min | | Python | 25.6 min |
| Rust | 20.5 min | | Go | 28.3 min |
| JVM | 21.5 min | | JS | 28.5 min |

### Toolchain drift from the pins — read this before the rankings

One of the seven pins was not met. The master runner *warns* rather than halts on a pin delta
(standing posture SR-3), so the run proceeded and the delta is recorded here alongside the
machine-readable [toolchain.json](toolchain.json):

| Toolchain | Pinned | Actually ran | Note |
| --- | --- | --- | --- |
| CPython | 3.14.6 | **3.12.3** | **two minor versions behind.** 3.13 and 3.14 both carry substantial interpreter performance work, so the Python rows are a pessimistic reading of Jinja2 and Mako |
| .NET SDK | (observed line recorded) | 10.0.110 · runtime .NET 10.0.10 | no drift |
| Rust | 1.97.1 | 1.97.1 | no drift |
| JDK | Temurin 25 | OpenJDK 25.0.3 | on pin (major); distribution differs |
| Node.js | v24.18.0 | v24.18.0 | exact |
| Go | go1.26.x | go1.26.5 | on pin |
| templ | v0.3.1020 | v0.3.1020 | exact |

The CPython delta does not affect the *within-ecosystem* comparison at all — Jinja2 and Mako ran
on the identical interpreter. It weakens the **cross-stack** Python rows as claims about the
ecosystem. Read them as honest about *this run* and pessimistic as statements about Python.

### Priority and affinity are not symmetric across harnesses

pyperf ran pinned to a single logical core via `--affinity`. The other five harnesses ran unpinned
under `taskset`+`nice -n -20`. This is an asymmetry in the measurement conditions, not a property
of the engines, and it is one reason the cross-stack Python rows carry the `reach/context` label
rather than fair-fight standing.

## The workloads, and why they are presented in two tiers

Eight workloads. "Golden output" is the byte length of the shared oracle in
[src/Heddle.Performance/GoldenCorpus](../../../src/Heddle.Performance/GoldenCorpus), verified by
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

Outputs from 35 KB to 832 KB. Every one of them exceeds 85,000 bytes once materialised as UTF-16,
so for the .NET engines each render lands on the Large Object Heap. **Read these with the caveats
attached to each**; they are stress tests, not page renders.

| # | Workload | What it measures | Suite | Golden output | As UTF-16 |
| ---: | --- | --- | --- | ---: | ---: |
| 6 | **`composed-page`** | layout/section/component composition | raw | 34,847 B | 108,802 B |
| 7 | **`large-loop`** | bulk iteration over 5,000 rows | raw | 192,780 B | 385,560 B |
| 8 | **`encoded-loop`** | escaping throughput, 5,000 rows | **encoded** | 831,685 B | 1,663,370 B |

`composed-page` carries a second, independent caveat beyond its size — it contains **no per-item
computation at all**. See [The `composed-page` caveat](#the-composed-page-caveat).

Every workload carries a controlled byte gate and an idiomatic verifier; the two encoded workloads
additionally carry the security floor. The
[consolidated tables](consolidated-tables.md) remain in **normative protocol order** — they are the
machine-generated, `--check`-reproducible appendix, and their ordering is fixed by the protocol
rather than by this report's presentation.

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
`python benchmarks/report/consolidate.py docs/benchmarks/2026-07-25`; verify with `--check`.

<!--@include: ./summary-tables.md-->

### What Tier 1 shows

Heddle is **fastest of the six .NET engines on all five** realistic-sized workloads, by **2.36× to
3.66×** — widest on `fragment-heavy` (per-call composition), narrowest on `conditional-heavy`
(branch dispatch).

Cross-stack it is **top-4 on all five**, beaten only by **Askama** (Rust, compile-time), **JTE**
(JVM, compiled to Java source) and, on two of the five, **eta** (JS). It is ahead of Tera, templ,
`text/template`, `html/template`, handlebars, Thymeleaf, Jinja2 and Mako on every one of them.

**Askama is the only engine faster than Heddle on all eight workloads.** It is a Rust compile-time
template engine — the same architectural bet Heddle makes, compile the document rather than
interpret it, executed in a language without a managed runtime.

### What Tier 2 shows, and how to read it

- **`encoded-loop` is the one workload where Heddle is not the fastest .NET engine.**
  Handlebars.Net led it on the previous run as well, so this is a reproduced result rather than
  noise. It is escaping throughput over a 5,000-row payload — an 832 KB output no page produces,
  but a real signal about the encoder's bulk path.
- **`composed-page` is where Heddle's cross-stack rank collapses to #11**, and that number is *not*
  a statement about composition machinery. It is an allocator cliff plus a degenerate workload,
  both quantified in [Sizing regimes](#sizing-regimes-and-the-85-000-byte-boundary) and
  [The `composed-page` caveat](#the-composed-page-caveat). Do not quote the #11 without them.
- `large-loop` at #5 is the least distorted of the three: it crosses the LOH boundary, but it also
  contains genuine per-row work, so the allocator is a smaller share of the measurement.

### Razor is now a parity twin, and Heddle is 1.37× faster than it

**Heddle 30.52 μs vs Razor 41.66 μs on byte-identical output** (Razor allocates 229.93 KB against
Heddle's 223.26 KB). Both render the same golden fragment sequence from the same shared
`TwinContent` fixtures, and both are held to the same byte-identical assertion — the parity gate
reports all five twins matching.

This is the **first parity-checked basis** the repository's "faster than Razor" claim has ever
had. Until 2026-07-25 the Razor benchmark rendered a larger, different page and sat outside every
gate ([ledger E5](../../../docs/spec/records.md#cross-spec-amendments-ledger)), so earlier Razor
figures describe a different workload and are not comparable with this one.

> **Caveat — this comparison lives entirely in Tier 2.** Razor is benchmarked on `composed-page`
> and nowhere else, so there is **no realistic-sized Razor figure in this run**. Both engines pay
> the same Large Object Heap cost on that workload, so the comparison is fair within .NET — but
> because that shared cost dominates, the 1.37× margin is compressed relative to the **2.36×–3.66×**
> Heddle shows over the other .NET engines at Tier 1 sizes. Read 1.37× as a conservative floor
> measured on the least favourable workload, not as Heddle's typical margin. Extending Razor to the
> other seven workloads is the fix; it has not been done.

### JS output is materialised, and JS numbers are stability-verified

Two harness properties that earlier runs could not claim:

- **Materialisation.** Every JS bench body forces its rendered string flat through
  `%FlattenString` and each run asserts `MATERIALISATION-CHECK` before writing artifacts
  ([ledger E4](../../../docs/spec/records.md#cross-spec-amendments-ledger)). Without it, V8 leaves
  `+=` template output as an unflattened `ConsString` rope and the timed region never produces the
  bytes. Measured directly: `render()` alone is **345 ns**, `flatten(render())` is **2,470 ns**,
  and a pure `Buffer.from` of the same 54 KB is **3,626 ns** — the published 4,273 ns is dominated
  by real materialisation. The run's highest implied throughput anywhere is **51.2 B/ns** (Askama
  on `composed-page`), which trips the report's 50 B/ns plausibility ceiling; that ceiling is
  **conservative for this hardware**, where a raw 55,466-byte `memcpy` costs 438 ns — 126 B/ns. The
  flag is a prompt to look, and the look confirms the cell.
- **Stability.** Each render track is measured as **54 independent processes**, and the Phase 4
  D13 verdict is computed from those same passes on every run. **Both tracks: `STABILITY:
  verified`** — cross-pass RSD median 1.76%, max 3.45% (controlled) / 3.02% (idiomatic), zero
  cells above the 5% threshold. See
  [stability-summary-controlled.md](js/stability-summary-controlled.md) and
  [stability-summary-idiomatic.md](js/stability-summary-idiomatic.md); the per-pass inputs ship as
  [stability-passes-controlled.json](js/stability-passes-controlled.json) and
  [stability-passes-idiomatic.json](js/stability-passes-idiomatic.json) so the verdict is
  independently recomputable.

Because the published JS statistic is the **median of 54 per-pass averages** and its dispersion is
the min…max spread *across* those passes, the JS interval means something different from the other
harnesses': it is cross-process variation, not within-process sampling noise.

### Reduced sample budgets did not cost precision

The `baseline` budget is far cheaper than the harness defaults it replaced — the JVM leg alone
dropped from 4.47 h to 21.5 min. Dispersion did not widen; on `composed-page` .NET's got
*tighter*, Heddle's standard deviation falling to 3.5% of its mean and Fluid's to 1.2%. A quiet,
tuned box at a modest iteration count beats a noisier box at a large one.

## Sizing regimes and the 85,000-byte boundary

The CLR allocates any object of **85,000 bytes or more on the Large Object Heap**. .NET strings are
UTF-16, so a rendered page crosses that line at **42,500 characters** — and every render that
crosses it allocates on the LOH and drives a full, blocking Gen2 collection.

Measured on this box, copying a `string` of increasing size (`new string(ReadOnlySpan<char>)`,
20,000 iterations after 2,000 warmup):

| chars | bytes (UTF-16) | ns/copy | B/ns |
| ---: | ---: | ---: | ---: |
| 8,000 | 16,000 | 721 | 22.2 |
| 20,000 | 40,000 | 1,031 | 38.8 |
| 40,000 | 80,000 | 1,994 | 40.1 |
| **42,400** | **84,800** | **2,103** | **40.3** |
| **54,340** | **108,680** | **12,190** | **8.9** |
| 100,000 | 200,000 | 23,215 | 8.6 |

Throughput collapses **4.5×** across the threshold and stays collapsed. And 20,000
`composed-page` renders through Heddle produce **76 Gen0 collections and 75 Gen2 collections** —
essentially every collection is a full one.

None of the other five ecosystems has this cliff. Their outputs are UTF-8 or Latin-1 at ~55 KB and
stay on the normal heap. So on Tier 2, the .NET column is paying a cost that is structural to the
runtime and absent everywhere else. Tier 1's five workloads are all comfortably below the boundary
(15,549 B golden → 31,098 B as UTF-16, the largest), which is why they compare cleanly.

### The `composed-page` caveat

Beyond its size, `composed-page` has a second problem: **it contains no per-item computation.** The
output is 17 pre-existing string fragments concatenated in order — no loop body, no branch, no
escaping, no formatting. Every compiled engine reduces it to about 17 `memcpy` calls, so the row
measures memory bandwidth and allocator behaviour, not template execution.

Floors measured on this box, all producing the same ~55 KB output:

| | ns/render | note |
| --- | ---: | --- |
| Rust — `Vec::extend_from_slice`, 55,466 B | **438** | raw `memcpy` floor, 126 B/ns |
| Rust — `String::with_capacity` + 17× `push_str` | 476 | the whole workload, by hand |
| Rust — Askama `render()` | 659 | 1.5× the raw floor |
| JS — `render()` alone, rope not flattened | 345 | this is the 2026-07-22 defect |
| JS — `flatten(render())` | 2,470 | the flatten costs 2.1 μs of it |
| JS — `Buffer.from(flat 54 KB, latin1)` | 3,626 | a pure copy of the same bytes |
| .NET — `string.Concat` of the same 17 fragments | **12,537–14,606** | the .NET floor for this workload |
| .NET — `StringBuilder` + `ToString()` | 26,320–27,037 | the idiomatic .NET approach |
| .NET — Heddle `Generate()` | 26,432–29,414 | BDN reports 30,520 |

Read against its own floor, **Heddle is within ~2× of `string.Concat` and marginally faster than a
plain `StringBuilder`.** There is no composition machinery to blame and very little engine work
left to win: the workload is an allocator benchmark that .NET loses on structural grounds. The
`#11 of 16` rank is real as a measurement and misleading as a statement about the engine.

Two further notes on this row:

- **The published `implied B/ns` figures for `composed-page` are understated by ~1.6×.** That
  column divides by the *golden* byte length (34,847 B), which is the **normalized** form — inter-tag
  whitespace is collapsed before the oracle is stored. The bytes actually rendered are **54,340
  (JS) / 55,466 (Rust) / 54,401 chars (.NET)**. Every other workload's golden matches its rendered
  output within 2.4%, so `composed-page` is the only affected row. Corrected, Askama's figure is
  **52.2 B/ns**, not the 32.8 printed — above the report's 50 B/ns plausibility ceiling, and still
  well below this machine's measured 126 B/ns `memcpy` floor. The ceiling constant is conservative
  for this hardware, not the Askama figure implausible.
- The fixture also exercises a known `@<<` layout-extend limitation documented in
  [GoldenCorpus/README.md](../../../src/Heddle.Performance/GoldenCorpus/README.md), which is why the
  golden output is the fragment sequence rather than a full chrome-bearing page.

### Nothing in this run is optimised away

The Tier 2 spread is wide enough to invite the question, so it was checked directly rather than
assumed:

- **Rust** — a raw 55,466-byte `memcpy` costs 438 ns on this box (126 B/ns). Askama's published
  1,063 ns is 2.4× that floor, and a direct probe of `render_composed_page()` outside Criterion
  returns 659 ns. The bytes are produced.
- **JS** — `render()` alone is 345 ns; `flatten(render())` is 2,470 ns. The 2.1 μs difference is
  the rope flatten, and a pure `Buffer.from` of the same 54 KB costs 3,626 ns on the same box. The
  published 4,273 ns is dominated by real materialisation, and every run asserts
  `MATERIALISATION-CHECK` before writing artifacts.
- **Go** — `render()` ends in `buf.String()`, which allocates and copies; benchstat reports
  57.32 KiB/op for templ, consistent with a genuine ~55 KB copy per render.

## Limitations

1. **Not the protocol machine, and unreplicated.** This is the Linux side of the protocol box;
   Q1.6 pins Windows 11. The rankings are internally valid; they do not carry protocol standing.
   The Windows protocol run is **TBD** — the 2026-07-22 attempt was invalidated and withdrawn — so
   no finding here has been reproduced on a second platform, and readers should treat the ordering
   as this run's result rather than as an established fact about the engines.
2. **Not the Phase 8 cross-check protocol either.** `run-all.sh` applies `taskset` + `nice -n -20`
   to the JS and Go timed runs; Phase 8's D2 rule is a plain launch. So this run is also not
   comparable with a published Linux cross-check.
3. **No CPU isolation.** The box has no `isolcpus` boot entry, so timed runs were not pinned to an
   isolated SMT pair — a measurement-quality delta against the protocol, recorded by the runner.
4. **CPU boost was off** (`cpufreq/boost=0`), part of the tuned state. Absolute times are
   therefore lower-clock figures and are not comparable with boost-enabled runs.
5. **CPython is two minor versions behind its pin** — see the drift table. The Python rows are a
   pessimistic reading of both engines.
6. **Measurement conditions are not symmetric across harnesses** — pyperf pinned to one logical
   core, the other five unpinned.
7. **The run executed as root**, so build outputs are root-owned. Harmless to the numbers,
   inconvenient for reproduction; the runner warns about it.
8. **.NET has no idiomatic track** (Phase 1 D15), so Heddle's practitioner-style performance is
   unmeasured and the idiomatic tables cannot be anchored to a Heddle idiomatic row.
9. **Encoded workloads are confined** — see the caveat under [The workloads](#the-workloads).
10. **Go contributes one engine across two surfaces** — `text/template` on the six raw workloads
    and `html/template` on the two encoded ones — plus templ. Read the Go rows accordingly.
11. **JS and Python carry `reach/context` evidence standing**, not `fair fight`; their rows answer
    "what does this ecosystem reach" rather than ranking engines against the others.
12. **The `short` measurement budget is unexercised.** This run used `--budget baseline`. The
    default `--budget short` (~10 min per ecosystem) has not been measured on real hardware, and
    for .NET it selects a different job shape (`WarmupCount=3, IterationCount=3`) than the one
    recorded above.
13. **The mitata heap column is not a materialisation test.** It reports a *net* heap delta across
    a batch, so garbage reclaimed inside the batch is invisible to it and a correct render can show
    a near-zero figure. The authoritative control is the harness-side `MATERIALISATION-CHECK`; the
    [consolidated tables](consolidated-tables.md) state this in full.
14. **One machine, one date.** Numbers are hardware-, platform- and date-specific. Run the suite on
    your own target with a page shaped like your real one before quoting anything.
15. **The two-tier presentation is this report's ordering, not a protocol change.** The normative
    workload numbering is unchanged and still recorded in `manifest.json` and the phase specs. The
    tier is *derived*, not declared: `consolidate.py` classifies a workload by whether its rendered
    output reaches 85,000 bytes as UTF-16, and orders every generated table accordingly. Nothing in
    this report is ordered by hand.
16. **The tier boundary depends on a measured constant.** `RENDERED_CHARS` in `consolidate.py`
    records each workload's rendered length, because the golden `byteLength` is the *normalized*
    form and understates `composed-page` by 1.56×. It was measured on 2026-07-25 and must be
    re-measured if templates or models change; a workload missing from it falls back to the golden
    size and is footnoted in the generated tables rather than silently misclassified.
17. **Razor is measured on one workload only.** It has no Tier 1 figure at all, so the 1.37× margin
    is a floor taken from the least favourable workload rather than a typical result. See the
    caveat under [Razor](#razor-is-now-a-parity-twin-and-heddle-is-1-37×-faster-than-it).
18. **The 50 B/ns plausibility ceiling is conservative for this hardware.** A raw 55,466-byte
    `memcpy` costs 438 ns on this box — 126 B/ns — so a compiled template that is largely a memcpy
    of literal chunks can legitimately sit above the ceiling, and Askama's `composed-page` cell
    (51.2 B/ns) does. A flag means *look at this cell*, not *this cell is impossible*; the
    authoritative materialisation control is the harness-side `MATERIALISATION-CHECK`.
19. **Absolute times are not comparable with the withdrawn 2026-07-22 run, in either direction.**
    Between the two, every non-Heddle .NET engine got 1.4–1.6× *faster* while Heddle got 1.2×
    *slower*, and Go templ moved 4.8×. Allocation figures are identical within 2%, so the work did
    not change — but "boost off" predicts everything slower, not a split by engine kind. This is
    unexplained. Do not mix figures across the two runs.
