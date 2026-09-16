# Benchmark run — 2026-09-16

This run is the **precompilation v3 evidence run** the phase 3 specification asks for
([phase-3-generated-sites.md § P3-R8 / P3-R9](../../../docs/spec/precompilation-v2/phase-3-generated-sites.md)):
intra-.NET only, on the protocol machine, over the program's eight protocol workloads. It publishes
three things — the technique tables (compiled-form tier with the generated site table on, the same
tier with the table off, and the runtime tier, each over 8 workloads × 3 sinks), the cold-start
table (`StartupBenchmarks`), and the `gate-precompiled` materialisation trailer — plus the
NativeAOT sample's publish log. It is **not** a cross-stack run: no other engine and no other
ecosystem was measured, and the [2026-08-08 cross-stack report](./../2026-08-08/index.md) stays the
program's cross-engine reference.

Reproduce (from the repository root, `-c Release`, `net10.0`):

```powershell
dotnet run -c Release --project benchmarks/dotnet -- gate
dotnet run -c Release --project benchmarks/dotnet -- gate-precompiled            # site table on (default)
dotnet exec --runtimeconfig <runtimeconfig with "Heddle.Precompiled.UseGeneratedSites": false> `
    benchmarks/dotnet/bin/Release/net10.0/Heddle.Benchmarks.Dotnet.dll gate-precompiled   # table off
dotnet run -c Release --project benchmarks/dotnet -- bench-techniques
dotnet run -c Release --project benchmarks/dotnet -- bench-startup
dotnet publish samples/precompiled-aot -c Release -r win-x64                     # after a prior build
```

The "table off" arm is the engine's own `Heddle.Precompiled.UseGeneratedSites` `AppContext` switch
supplied through `runtimeconfig.json`; the harness prints which arm ran as `SITE-TABLE: on|off`
in its output (see [gate-precompiled-on.log](../../../docs/benchmarks/2026-09-16/dotnet/gate-precompiled-on.log) and
[gate-precompiled-off.log](../../../docs/benchmarks/2026-09-16/dotnet/gate-precompiled-off.log)).

## Environment

```
Windows 11 (10.0.26200.9457/25H2), AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 16 physical / 32 logical cores
protocol machine (metrics-protocol Q1.6) · posture procedural: quiet machine, AC power, High performance power plan (set by BenchmarkDotNet)
BenchmarkDotNet 0.15.8 · .NET SDK 10.0.401 · .NET 10.0.12 (10.0.1226.42308), X64 RyuJIT x86-64-v4 · [MemoryDiagnoser]
technique suites: ShortRun(LaunchCount=3, WarmupCount=3, IterationCount=5) — the harness's committed `short` budget
startup suite:    ColdStart(LaunchCount=20, WarmupCount=0, IterationCount=1, InvocationCount=1) — one cold invocation per process
NativeAOT publish: ILCompiler 10.0.12, win-x64, MSVC 14.51.36231 (VS 18 Community) link
Commit 02e27c22 (working tree) — the tree carries the fixes listed under Files; the benchmark harness edits are part of it
```

The technique tables use the harness's committed default job (`short`: 3 launches × 5 iterations),
not the `baseline` budget (10 launches × 5): the three technique suites are 72 cases and took
32.5 minutes at `short`; `baseline` would have taken roughly 100 minutes. The 2026-08-08 technique
tables were taken at the older `ShortRun` default (3 launches × 3 iterations), so this run's
error bars are slightly tighter than that run's at the same launch count.

## The workloads

All eight are the program's protocol workloads ([workloads.md](../../../benchmarks/docs/workloads.md)),
controlled track only, and every cell passed the controlled byte gate (`gate`: 152 cells, 0 failed,
corpus SHA-256 + length verified) and the strict precompiled gate (`gate-precompiled`: 8/8 workloads
× 3 sinks byte-identical between the compiled-form tier and the runtime tier, with the site table on
and with it off) before anything was timed.

- `trivial-substitution` — per-render fixed-overhead floor (raw).
- `fortunes-encoded` — escaping correctness and cost at micro scale (encoded).
- `mixed-page` — realistic mixture: literals + substitutions + modest loop + conditionals (raw).
- `fragment-heavy` — per-row fragment dispatch plus nested per-call composition (raw).
- `conditional-heavy` — branch evaluation and control-flow dispatch (raw).
- `composed-page` — layout/section/component composition machinery (raw).
- `large-loop` — bulk iteration and output writing (raw).
- `encoded-loop` — escaping throughput at scale (encoded).

*Encoded workloads confine untrusted data to HTML text and attribute-value contexts, where flat
and contextual encoders emit the same escaped output. These numbers therefore measure escaping cost
in confined contexts and cannot show the differentiating benefit of contextual encoding; a
contextual encoder's encoded-suite result must not be read as 'context awareness is pure overhead.'*

## Results — what this run actually shows

### Technique tables — compiled-form tier against the runtime tier (exit criterion 3)

Three suites, one machine, one job. The runtime and data-only tables come from one `bench-techniques`
invocation (10:53); the site-table-on table was re-taken on its own (12:01, same job, log
[TechniquePrecompiledBenchmarks-20260916-120150.log](../../../docs/benchmarks/2026-09-16/dotnet/Heddle.Benchmarks.Dotnet.Bench.TechniquePrecompiledBenchmarks-20260916-120150.log))
after a harness fix described under allocation below:
`TechniqueRuntimeBenchmarks` (text compiled at runtime), `TechniquePrecompiledBenchmarks` (the
compiled-form artifact with the generated site table serving every site it covers) and
`TechniquePrecompiledDataOnlyBenchmarks` (the same artifact with the table switched off, every site
rebuilt from its serialized form). "Within" means the two means differ by no more than the sum of
the two rows' BenchmarkDotNet `Error` columns (half of the 99.9% confidence interval); the
percentage is the precompiled mean relative to the runtime mean.

| Workload | Sink | Runtime mean ± error | Site table on mean ± error | Data-only mean ± error | On vs runtime | Off vs runtime | Alloc runtime | Alloc on | Alloc off |
|---|---|---:|---:|---:|:---:|:---:|---:|---:|---:|
| trivial-substitution | Utf8 | 135.5 ns ± 1.4 ns | 109.2 ns ± 3.3 ns | 112.9 ns ± 4.8 ns | outside (−19.4%) | outside (−16.7%) | 208 B | 96 B | 96 B |
| trivial-substitution | TextWriter | 108.6 ns ± 1.9 ns | 84.4 ns ± 1.5 ns | 85.6 ns ± 0.5 ns | outside (−22.3%) | outside (−21.2%) | 200 B | 88 B | 88 B |
| trivial-substitution | String | 151.8 ns ± 1.2 ns | 126.5 ns ± 3.4 ns | 133.3 ns ± 1.4 ns | outside (−16.7%) | outside (−12.2%) | 1,720 B | 1,608 B | 1,608 B |
| fortunes-encoded | Utf8 | 713.6 ns ± 6.9 ns | 690.8 ns ± 2.9 ns | 682.3 ns ± 6.3 ns | outside (−3.2%) | outside (−4.4%) | 2,304 B | 2,200 B | 2,200 B |
| fortunes-encoded | TextWriter | 633.4 ns ± 3.3 ns | 619.7 ns ± 3.5 ns | 627.4 ns ± 7.5 ns | outside (−2.2%) | within (−0.9%) | 2,296 B | 2,192 B | 2,192 B |
| fortunes-encoded | String | 712.0 ns ± 2.1 ns | 687.6 ns ± 6.3 ns | 705.8 ns ± 20.3 ns | outside (−3.4%) | within (−0.9%) | 7,120 B | 7,016 B | 7,016 B |
| mixed-page | Utf8 | 2,834.2 ns ± 38.1 ns | 2,792.1 ns ± 45.9 ns | 2,855.7 ns ± 85.3 ns | within (−1.5%) | within (+0.8%) | 6,896 B | 6,808 B | 6,808 B |
| mixed-page | TextWriter | 2,426.3 ns ± 20.6 ns | 2,422.6 ns ± 17.6 ns | 2,450.4 ns ± 60.6 ns | within (−0.2%) | within (+1.0%) | 6,888 B | 6,800 B | 6,800 B |
| mixed-page | String | 2,716.8 ns ± 25.4 ns | 2,706.7 ns ± 25.2 ns | 2,767.7 ns ± 82.8 ns | within (−0.4%) | within (+1.9%) | 47,896 B | 47,808 B | 47,808 B |
| fragment-heavy | Utf8 | 5,539.5 ns ± 93.2 ns | 5,570.9 ns ± 39.5 ns | 5,571.1 ns ± 88.5 ns | within (+0.6%) | within (+0.6%) | 8,400 B | 8,304 B | 8,304 B |
| fragment-heavy | TextWriter | 4,893.3 ns ± 38.0 ns | 4,958.6 ns ± 118.2 ns | 4,857.6 ns ± 42.9 ns | within (+1.3%) | within (−0.7%) | 8,392 B | 8,296 B | 8,296 B |
| fragment-heavy | String | 5,312.6 ns ± 68.1 ns | 5,310.4 ns ± 136.5 ns | 5,338.2 ns ± 236.8 ns | within (−0.0%) | within (+0.5%) | 33,936 B | 33,840 B | 33,840 B |
| conditional-heavy | Utf8 | 18.35 µs ± 262.1 ns | 18.42 µs ± 142.5 ns | 18.42 µs ± 332.2 ns | within (+0.4%) | within (+0.4%) | 37,784 B | 37,680 B | 37,680 B |
| conditional-heavy | TextWriter | 14.32 µs ± 32.0 ns | 14.44 µs ± 94.7 ns | 14.38 µs ± 46.4 ns | within (+0.8%) | within (+0.4%) | 37,776 B | 37,672 B | 37,672 B |
| conditional-heavy | String | 14.80 µs ± 151.3 ns | 14.79 µs ± 198.4 ns | 14.89 µs ± 96.0 ns | within (−0.1%) | within (+0.6%) | 103,192 B | 103,088 B | 103,088 B |
| composed-page | Utf8 | 25.15 µs ± 813.7 ns | 24.47 µs ± 394.5 ns | 24.37 µs ± 813.0 ns | within (−2.7%) | within (−3.1%) | 16,624 B | 16,528 B | 16,528 B |
| composed-page | TextWriter | 21.83 µs ± 205.5 ns | 21.97 µs ± 224.0 ns | 21.80 µs ± 314.0 ns | within (+0.7%) | within (−0.1%) | 16,616 B | 16,520 B | 16,520 B |
| composed-page | String | 30.23 µs ± 1,274.0 ns | 30.92 µs ± 989.2 ns | 33.15 µs ± 1,558.6 ns | within (+2.3%) | outside (+9.7%) | 216,070 B | 215,980 B | 215,984 B |
| large-loop | Utf8 | 168.04 µs ± 1,058.8 ns | 170.07 µs ± 2,870.8 ns | 168.18 µs ± 550.5 ns | within (+1.2%) | within (+0.1%) | 660,968 B | 660,880 B | 660,880 B |
| large-loop | TextWriter | 140.72 µs ± 1,710.3 ns | 141.39 µs ± 3,787.2 ns | 141.76 µs ± 3,866.4 ns | within (+0.5%) | within (+0.7%) | 660,960 B | 660,872 B | 660,872 B |
| large-loop | String | 1.218 ms ± 4,619.2 ns | 1.220 ms ± 15.75 µs | 1.217 ms ± 3,050.7 ns | within (+0.2%) | within (−0.1%) | 1,470,831 B | 1,470,796 B | 1,470,796 B |
| encoded-loop | Utf8 | 717.29 µs ± 7,388.1 ns | 718.02 µs ± 5,417.1 ns | 719.84 µs ± 7,363.5 ns | within (+0.1%) | within (+0.4%) | 2,792,016 B | 2,791,920 B | 2,791,920 B |
| encoded-loop | TextWriter | 601.23 µs ± 13.16 µs | 589.82 µs ± 4,793.9 ns | 595.51 µs ± 7,047.5 ns | within (−1.9%) | within (−1.0%) | 2,792,008 B | 2,791,912 B | 2,791,912 B |
| encoded-loop | String | 3.952 ms ± 8,751.5 ns | 3.949 ms ± 8,474.7 ns | 3.952 ms ± 9,574.7 ns | within (−0.1%) | within (−0.0%) | 6,075,514 B | 6,075,580 B | 6,075,590 B |

*Allocation and GC figures are measured within one runtime and are not comparable across
ecosystems: allocator designs differ and the numbers do not mean the same thing.*

What the table says, stated against the plan's exit criterion 3 ("v3 rows for 8/8 workloads within
BenchmarkDotNet's reported error of the runtime rows with equal allocation"):

- **Means:** 18 of 24 cells are within error with the site table on, 19 of 24 with it off. Every
  cell outside with the table on is **faster** than the runtime tier: the three `trivial-substitution`
  sinks (−16.7% to −22.3%) and the three `fortunes-encoded` sinks (−2.2% to −3.4%); none is slower.
  With the table off, the outside cells are the same faster ones (three `trivial-substitution` sinks,
  `fortunes-encoded`/Utf8) plus `composed-page`/String, slower by 9.7% at this job. Read per workload,
  7/8 are within error on every sink in either arm, and the one miss is `composed-page`/String.
- **The two slower cells, re-measured at the baseline budget** (10 launches × 5 iterations, same
  machine, artifacts under [dotnet/results/baseline-cells/](../../../docs/benchmarks/2026-09-16/dotnet/results/baseline-cells/)):

  | Cell | Runtime mean ± error | Precompiled mean ± error | Verdict |
  |---|---:|---:|---|
  | `large-loop` / TextWriter (table on) | 145.99 µs ± 0.57 µs | 142.8 µs ± 1.42 µs | faster by 2.2%; the short-job +5.6% was noise |
  | `composed-page` / String (data-only) | 30.13 µs ± 0.36 µs | 31.79 µs ± 0.60 µs | slower by 5.5%, outside error, and the third sample in a row to say so |

  `composed-page`/String is therefore a real, sink-specific difference: the same workload's Utf8 and
  TextWriter sinks are within error in both arms, both arms allocate the same bytes as the runtime tier
  within ±100 B on a 216 KB output, and both collect Gen2 slightly *less* often per render than the
  runtime tier (13.1–13.2 against 14.0 per thousand renders), so it is not an allocation or GC
  difference. It is confined to materialising the largest output as a string through the compiled-form
  adapter, and it is left open here rather than explained away.
- **Allocation is equal between the two compiled-form arms, and below the runtime tier.** Table on
  and data-only allocate the same bytes in 22 of 24 cells (the two String cells differ by 4–10 B, the
  output string's own rounding), and both sit 88–112 B per render below the runtime tier on the Utf8 and
  TextWriter sinks (the String sink within ±100 B). The +24 B per render an earlier take of this table
  showed between the arms was the harness, not the engine: the table-on suite routed every render
  through `Engines.Precompiled.Bound(workload)`, whose per-call lookup allocates 24 B (measured on its
  own; identical with the switch on and off, and `Generate` on the bound template allocates the same
  bytes in both arms), most likely the closure the compiler hoists for the `Array.Find` lambda before
  the cache hit returns. The suite now renders through the bound template like its data-only twin.
  Exit criterion 5 holds: the printed accessor boxes a value-type result exactly once, as the engine's
  own `Expression.Convert` does, and `Heddle.Tests` now pins the render-level rule corpus-wide
  (`GeneratedSiteTests.TableOnRenderAllocatesNoMoreThanDataPath`, 59 rows × 3 TFMs) beside the
  per-accessor `GeneratedAccessorAllocationTests`.

### Cold start — the artifact path against a runtime compile (exit criterion 4)

*Startup figures are cold per-process measurements from one machine and are not comparable across
machines, across runs, or with warm render figures; the artifact path is compared only against
CompileHeddle in the same run.*

`StartupBenchmarks`, `composed-page`, 20 launches, **one iteration of one invocation per launch**
(so every sample is a fresh process's first use of the engine), `[MemoryDiagnoser]`. The model is
built in `[GlobalSetup]`; the corpus assertion runs in `[GlobalCleanup]` on the measured output, so
nothing renders in the process before the timed call.

| Row | Mean | Error | StdDev | Median of 20 launches | Allocated |
|---|---:|---:|---:|---:|---:|
| Heddle (startup — `CompileHeddle`: `new HeddleTemplate(new CompileContext(…))` + first render) | 296.9 ms | 57.76 ms | 66.51 ms | 281.6 ms | 19,886.6 KB |
| Heddle (startup — `RegisterAndRenderCompiledForm`: `Register` + `BindTyped` + first render, table on) | 285.7 ms | 5.64 ms | 6.49 ms | 282.5 ms | 935.6 KB |
| Heddle (startup — `RegisterAndRenderCompiledFormDataOnly`: same, table off) | 284.8 ms | 5.88 ms | 6.78 ms | 281.3 ms | 935.6 KB |

The `CompileHeddle` mean carries one 579 ms launch (BenchmarkDotNet flags four outliers on that row);
its median is 281.6 ms. **The three rows are indistinguishable**: the artifact path is not strictly
below the runtime compile in a fresh process — it is the same number within noise, with the table on
or off. Exit criterion 4 is **not met** on this evidence.

What the ~280 ms is, established with a throwaway probe outside BenchmarkDotNet (fresh process,
`Stopwatch`, `Heddle.dll` from this tree; the probe is not part of the repository): the first
`CompileContext` a process constructs costs **115–180 ms** of one-time engine initialisation
(`TemplateFactory`'s static constructor, extension discovery by reflection, `Heddle.Language`
type loading, JIT of the engine's entry paths; no `Microsoft.CodeAnalysis` assembly is loaded),
`HeddleTemplate.Configure` on the host assembly costs a further ~64 ms, `PrecompiledTemplates.Register`
of the 107 KB artifact costs ~14–16 ms, and the runtime parse-and-compile of `composed-page` itself
costs ~45–68 ms JIT-cold. Both rows pay the initialisation; the artifact path then pays the loader
(decode, gauntlet, site table) where the runtime path pays the compiler, and the two come out even.
Where the artifact path does win is memory: 936 KB against 19.9 MB allocated by a cold compile.

An earlier shape of this suite in the same tree (job doubled by the harness's default `ShortRun`
config, default iteration count per launch, a `[GlobalSetup]` render through the runtime tier) is
kept out of this report on purpose: its "cold" rows were 95% warm cache hits, and its
`CompileHeddle` ran JIT-warm against a JIT-cold loader. The harness was corrected in this tree
(`benchmarks/dotnet/src/Bench/StartupBenchmarks.cs`, `BenchRunner.cs`) before the table above was taken.

### Materialisation trailer (`gate-precompiled`)

Both arms, from [gate-precompiled-on.log](../../../docs/benchmarks/2026-09-16/dotnet/gate-precompiled-on.log) and
[gate-precompiled-off.log](../../../docs/benchmarks/2026-09-16/dotnet/gate-precompiled-off.log); times are `Stopwatch` wall time for
register + bind + first render in the gate's already-warm process, and are **not** startup figures.

| Workload | Rendered | Table on | Table off |
|---|---:|---:|---:|
| composed-page | 47,460 chars | 0.7 ms | 0.7 ms |
| trivial-substitution | 339 chars | 0.3 ms | 0.3 ms |
| large-loop | 192,781 chars | 1.3 ms | 1.1 ms |
| mixed-page | 9,741 chars | 0.3 ms | 0.3 ms |
| conditional-heavy | 15,550 chars | 0.4 ms | 0.4 ms |
| fragment-heavy | 6,058 chars | 0.4 ms | 0.3 ms |
| fortunes-encoded | 1,125 chars | 0.3 ms | 0.3 ms |
| encoded-loop | 781,686 chars | 4.2 ms | 3.7 ms |

`PRECOMPILED-ARTIFACT: 107186 bytes` (the embedded `Heddle.CompiledForm` image for all eight
workloads). Both arms: `8/8 workloads precompiled, 0 failure(s)`, parity on three sinks.

### NativeAOT sample (P3-R9)

`samples/precompiled-aot` published with `PublishAot=true`, `InvariantGlobalization=true`,
`Heddle.CSharpTierEnabled=false` (trimmed) and `Heddle.Precompiled.StrictLoad=true`, `win-x64`:
[precompiled-aot-publish.log](../../../docs/benchmarks/2026-09-16/precompiled-aot-publish.log). The native binary (7,219,200 bytes) ran
in `--capture` mode and rendered all seven workloads byte-identical to the sample's goldens under
strict load, the three tiers (typed wrapper, registry, dynamic twin) agreeing; the publish directory
contains no `Microsoft.CodeAnalysis*` file and the loaded set at run time
([precompiled-aot-native-assemblies.txt](../../../docs/benchmarks/2026-09-16/precompiled-aot-native-assemblies.txt)) contains no
`Microsoft.CodeAnalysis` assembly. Getting there needed four trimming fixes in this tree (the manifest type's constructor, model members,
export containers and `System.Decimal`'s conversion operators were all being trimmed away;
`HeddleCompiledTemplatesAttribute`, `PrecompiledTemplates.BindTyped`, the three export attributes and
`NumericPromotion` carry the annotations). P3-W4 is met: `dotnet build -c Release` of `Heddle.csproj`
reports zero trim/AOT analyzer warnings on `net8.0` and `net10.0` (the analyzers do not run on `netstandard2.0`), every site that
stays reflective carries a per-site `[UnconditionalSuppressMessage]` naming its rooting story
(model types rooted by `[DynamicallyAccessedMembers]`, D11 identity resolution, the Roslyn and dynamic
tiers declared outside the claim), and the codes are pinned as errors in `Heddle.csproj` so a local
build and the CI leg fail the same way. The publish log carries only the two framework
`IL3053` notes (`Microsoft.CSharp`, `System.Linq.Expressions` produced AOT analysis warnings of their
own) and nothing from `Heddle.dll`. On this machine the ILCompiler toolchain probe needs the Visual Studio Installer
directory on `PATH` (`vswhere.exe`) or the native link step fails with MSB3073; that is an
environment note, not a repository one.

### Against the 2026-08-08 runtime rows

The runtime tier's rows, this run against the [2026-08-08 run](./../2026-08-08/index.md)'s
`TechniqueRuntimeBenchmarks`. Only three workloads are comparable: `trivial-substitution`,
`fortunes-encoded` and `encoded-loop` have the same corpus bytes in both runs and are identical
within error, with identical allocation. The other five workloads were re-authored on 2026-08-08
**after** that run was taken (commits `247aba95`, `6ce15fc6`, `658bb540`, `5d2160f2`, `a625989c`
on that date: `composed-page` became a whole composed page instead of seventeen pre-existing
fragments — the 2026-08-08 report's own caveat says so — and `fragment-heavy`, `large-loop`,
`mixed-page` and `conditional-heavy` gained model and template changes), so their rows measure
different templates and different output, and no engine regression or improvement can be read from
them. They are listed for completeness, not compared.

| Workload | Sink | 2026-08-08 runtime mean ± error | 2026-09-16 runtime mean ± error | Change | Alloc 08-08 | Alloc 09-16 | Comparable |
|---|---|---:|---:|---:|---:|---:|:---:|
| trivial-substitution | Utf8 | 132.0 ns ± 3.1 ns | 135.5 ns ± 1.4 ns | +2.7% | 208 B | 208 B | yes |
| trivial-substitution | TextWriter | 108.5 ns ± 3.8 ns | 108.6 ns ± 1.9 ns | +0.1% | 200 B | 200 B | yes |
| trivial-substitution | String | 152.9 ns ± 1.2 ns | 151.8 ns ± 1.2 ns | −0.7% | 1,720 B | 1,720 B | yes |
| fortunes-encoded | Utf8 | 710.3 ns ± 11.1 ns | 713.6 ns ± 6.9 ns | +0.5% | 2,304 B | 2,304 B | yes |
| fortunes-encoded | TextWriter | 629.6 ns ± 7.8 ns | 633.4 ns ± 3.3 ns | +0.6% | 2,296 B | 2,296 B | yes |
| fortunes-encoded | String | 736.8 ns ± 4.4 ns | 712.0 ns ± 2.1 ns | −3.4% | 7,120 B | 7,120 B | yes |
| encoded-loop | Utf8 | 723.38 µs ± 5,340.4 ns | 717.29 µs ± 7,388.1 ns | −0.8% | 2,792,016 B | 2,792,016 B | yes |
| encoded-loop | TextWriter | 594.91 µs ± 11.17 µs | 601.23 µs ± 13.16 µs | +1.1% | 2,792,008 B | 2,792,008 B | yes |
| encoded-loop | String | 3.949 ms ± 19.54 µs | 3.952 ms ± 8,751.5 ns | +0.1% | 6,075,514 B | 6,075,514 B | yes |
| mixed-page | Utf8 | 2,797.3 ns ± 132.0 ns | 2,834.2 ns ± 38.1 ns | +1.3% | 4,016 B | 6,896 B | no — workload changed |
| mixed-page | TextWriter | 2,215.0 ns ± 24.5 ns | 2,426.3 ns ± 20.6 ns | +9.5% | 4,008 B | 6,888 B | no — workload changed |
| mixed-page | String | 2,558.0 ns ± 17.9 ns | 2,716.8 ns ± 25.4 ns | +6.2% | 45,016 B | 47,896 B | no — workload changed |
| fragment-heavy | Utf8 | 3,429.6 ns ± 225.9 ns | 5,539.5 ns ± 93.2 ns | +61.5% | 3,120 B | 8,400 B | no — workload changed |
| fragment-heavy | TextWriter | 2,953.8 ns ± 276.0 ns | 4,893.3 ns ± 38.0 ns | +65.7% | 3,112 B | 8,392 B | no — workload changed |
| fragment-heavy | String | 3,253.8 ns ± 351.7 ns | 5,312.6 ns ± 68.1 ns | +63.3% | 23,080 B | 33,936 B | no — workload changed |
| conditional-heavy | Utf8 | 18.41 µs ± 227.1 ns | 18.35 µs ± 262.1 ns | −0.3% | 35,384 B | 37,784 B | no — workload changed |
| conditional-heavy | TextWriter | 14.30 µs ± 315.5 ns | 14.32 µs ± 32.0 ns | +0.1% | 35,376 B | 37,776 B | no — workload changed |
| conditional-heavy | String | 14.78 µs ± 375.3 ns | 14.80 µs ± 151.3 ns | +0.2% | 100,792 B | 103,192 B | no — workload changed |
| composed-page | Utf8 | 1,872.6 ns ± 257.9 ns | 25.15 µs ± 813.7 ns | +1243.1% | 184 B | 16,624 B | no — workload changed |
| composed-page | TextWriter | 1,336.0 ns ± 284.4 ns | 21.83 µs ± 205.5 ns | +1533.7% | 128 B | 16,616 B | no — workload changed |
| composed-page | String | 23.67 µs ± 598.0 ns | 30.23 µs ± 1,274.0 ns | +27.7% | 233,411 B | 216,070 B | no — workload changed |
| large-loop | Utf8 | 156.97 µs ± 1,842.5 ns | 168.04 µs ± 1,058.8 ns | +7.1% | 390,568 B | 660,968 B | no — workload changed |
| large-loop | TextWriter | 129.37 µs ± 700.3 ns | 140.72 µs ± 1,710.3 ns | +8.8% | 390,560 B | 660,960 B | no — workload changed |
| large-loop | String | 1.144 ms ± 21.50 µs | 1.218 ms ± 4,619.2 ns | +6.4% | 1,200,421 B | 1,470,831 B | no — workload changed |

*Allocation and GC figures are measured within one runtime and are not comparable across
ecosystems: allocator designs differ and the numbers do not mean the same thing.*

No universal-superiority claim follows from any table above. Numbers are dated and
hardware-specific; reproduce them with the commands at the top.

## Files

- [dotnet/results/…TechniqueRuntimeBenchmarks-report-github.md](../../../docs/benchmarks/2026-09-16/dotnet/results/Heddle.Benchmarks.Dotnet.Bench.TechniqueRuntimeBenchmarks-report-github.md) (+ `.csv`, `.html`)
- [dotnet/results/…TechniquePrecompiledBenchmarks-report-github.md](../../../docs/benchmarks/2026-09-16/dotnet/results/Heddle.Benchmarks.Dotnet.Bench.TechniquePrecompiledBenchmarks-report-github.md) (+ `.csv`, `.html`)
- [dotnet/results/…TechniquePrecompiledDataOnlyBenchmarks-report-github.md](../../../docs/benchmarks/2026-09-16/dotnet/results/Heddle.Benchmarks.Dotnet.Bench.TechniquePrecompiledDataOnlyBenchmarks-report-github.md) (+ `.csv`, `.html`)
- [dotnet/results/…StartupBenchmarks-report-github.md](../../../docs/benchmarks/2026-09-16/dotnet/results/Heddle.Benchmarks.Dotnet.Bench.StartupBenchmarks-report-github.md) (+ `.csv`, `.html`)
- [dotnet/BenchmarkRun-20260916-105312.log](../../../docs/benchmarks/2026-09-16/dotnet/BenchmarkRun-20260916-105312.log) — the `bench-techniques` run
- [dotnet/Heddle.Benchmarks.Dotnet.Bench.StartupBenchmarks-20260916-113006.log](../../../docs/benchmarks/2026-09-16/dotnet/Heddle.Benchmarks.Dotnet.Bench.StartupBenchmarks-20260916-113006.log) — the `bench-startup` run
- [dotnet/Heddle.Benchmarks.Dotnet.Bench.TechniquePrecompiledBenchmarks-20260916-120150.log](../../../docs/benchmarks/2026-09-16/dotnet/Heddle.Benchmarks.Dotnet.Bench.TechniquePrecompiledBenchmarks-20260916-120150.log) — the re-taken table-on class
- [dotnet/results/baseline-cells/](../../../docs/benchmarks/2026-09-16/dotnet/results/baseline-cells/) (+ [dotnet/baseline-cells-BenchmarkRun-20260916-121304.log](../../../docs/benchmarks/2026-09-16/dotnet/baseline-cells-BenchmarkRun-20260916-121304.log)) — the two cells at the baseline budget
- [dotnet/gate.log](../../../docs/benchmarks/2026-09-16/dotnet/gate.log), [dotnet/gate-precompiled-on.log](../../../docs/benchmarks/2026-09-16/dotnet/gate-precompiled-on.log), [dotnet/gate-precompiled-off.log](../../../docs/benchmarks/2026-09-16/dotnet/gate-precompiled-off.log)
- [precompiled-aot-publish.log](../../../docs/benchmarks/2026-09-16/precompiled-aot-publish.log), [precompiled-aot-native-assemblies.txt](../../../docs/benchmarks/2026-09-16/precompiled-aot-native-assemblies.txt)
