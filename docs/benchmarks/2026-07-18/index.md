# Benchmark run — 2026-07-18

Raw [BenchmarkDotNet](https://benchmarkdotnet.org/) artifacts for the two phase-5 workload-breadth
suites (trivial-substitution and large-loop), which bracket the composed home page measured in the
[2026-07-11 run](../2026-07-11/index.md). Reproduce with
`dotnet run -c Release --project src/Heddle.Performance -- --filter *SubstitutionRenderBenchmarks*`
(and `*LoopRenderBenchmarks*`).

## Environment

```
BenchmarkDotNet v0.15.8 · Windows 11 (10.0.26200.8875/25H2)
AMD Ryzen 9 9950X 4.30GHz, 16 physical / 32 logical cores
.NET SDK 10.0.302 · .NET 10.0.10 runtime, X64 RyuJIT x86-64-v4
Commit b83c7150 (working tree)
```

## The workloads

Both workloads render **raw** (`OutputProfile.Text` on Heddle; the non-encoding path on every
twin) under Heddle's default `Native` expression tier, so the comparison measures templating, not
encoding. Each of the four competitor twins (Fluid, Scriban, DotLiquid, Handlebars.Net) is held
**byte-identical after normalization** to the Heddle oracle by a parity assertion that runs
before any timing. The templates are authored whitespace-free, so the normalizer's
whitespace-collapse has nothing to do here; the one difference it reconciles (via its documented
trailing-newline trim) is the single trailing LF the Heddle oracle renders from the `.heddle`
fixture's final newline, which no twin emits. Heddle is the ratio baseline; `[MemoryDiagnoser]` is enabled.

- **Trivial substitution** (`trivial-substitution.heddle`): one flat card whose output (~338
  chars) is dominated by ten scalar member substitutions with minimal literal glue and **no**
  composition — no layout, components, or loop.
- **Large loop** (`large-loop.heddle`): a single `@list` over **5,000** rows, each emitting two
  scalar members (~193 KB output) — output dominated by one large iteration. Models for all five
  engines are materialized once, so no op re-allocates its model.

## Results — what this run actually shows

The language assessment (retired — git history) predicted trivial substitution as the
shape where Heddle's composition advantage narrows or inverts. On this hardware and date the
picture is workload-dependent, and **not uniformly in Heddle's favor**:

- **Trivial substitution.** Heddle rendered fastest (118.7 ns), but the prediction holds on
  **memory**: Handlebars.Net allocated **less than half** of Heddle's bytes per op (736 B vs
  1,608 B — alloc ratio 0.46), so on allocation this workload **inverts** against Heddle. Fluid
  was 4.5x on time, DotLiquid 17.2x, Scriban 37.2x.
- **Large loop.** The **time** race is at its tightest here: Handlebars.Net rendered within ~8%
  of Heddle (660.4 μs vs 612.9 μs) while again allocating **less** (1.01 MB vs 1.14 MB — alloc
  ratio 0.88). Fluid was 1.6x, Scriban 2.1x, DotLiquid 6.7x.

Compare the [2026-07-11 composition page](../2026-07-11/index.md), where Heddle led the four
parity-checked engines by 2.0x on time with the least-or-tied-least allocation. No claim of
universal superiority follows from these runs: which engine is preferable depends on the workload
shape (and on whether render time or allocation matters more) — on scalar-substitution output
Handlebars.Net allocates materially less, and on a pure large loop it is within measurement
distance on time. Numbers are hardware- and date-specific; reproduce them yourself with the
command above.

## Files

Each suite has a rendered Markdown report (linked) plus `-report.csv` and `-report.html` siblings
in this directory.

- Trivial substitution — [`Heddle.Performance.SubstitutionRenderBenchmarks-report-github.md`](./Heddle.Performance.SubstitutionRenderBenchmarks-report-github.md)
  (+ `Heddle.Performance.SubstitutionRenderBenchmarks-report.csv`, `Heddle.Performance.SubstitutionRenderBenchmarks-report.html`)
- Large loop — [`Heddle.Performance.LoopRenderBenchmarks-report-github.md`](./Heddle.Performance.LoopRenderBenchmarks-report-github.md)
  (+ `Heddle.Performance.LoopRenderBenchmarks-report.csv`, `Heddle.Performance.LoopRenderBenchmarks-report.html`)

The parity contract that makes the comparison apples-to-apples is documented in
[src/Heddle.Performance/Runners](../../../src/Heddle.Performance/Runners/README.md).
