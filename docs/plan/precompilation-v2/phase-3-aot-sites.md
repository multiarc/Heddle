# Phase 3 — Generated sites and evidence

Back to the [plan](README.md). Depends on [phase 1](phase-1-compiled-form.md) for site ids and
[phase 2](phase-2-build-integration.md) for build wiring.

## Goal

Remove load-time compilation for the delegate classes the compiled form can only describe, by
emitting them as typed generated methods in the consumer's assembly — printed from the engine's
bound trees, never re-derived from template text — with the loader preferring a generated site
when one exists. A site the printer declines is rebuilt at load as before. Generated sites make
the precompiled tier compile-free at load on NativeAOT and interpreter hosts. Then prove the tier
with benchmarks.

## Scope

- **Site table**: generated methods addressable by site id, validated against the artifact's
  content hash; the loader consults it before rebuilding from data.
- **Member accessors**: generated with the null-safety and hop semantics of the engine's bound path.
- **Native expressions**: printed from the engine's typed, bound tree (after overload selection
  and numeric promotion) over the closed node vocabulary its compiler produces; constants stay
  constants; an unhandled node kind declines the site and is recorded.
- **Embedded C#**: generated per site with its binding context, compiled by the consumer's
  compiler; a fully generated assembly loads no `Microsoft.CodeAnalysis` type at run time.
- **Function calls stay data** ([PD9](decisions.md#pd9--calls-the-build-registry-cannot-bind)); a
  native expression whose typing depends on a late-bound call is not a generated site. A member
  the engine binds but generated C# cannot name (a referenced assembly's `internal`) is rebuilt
  from data, one site.
- **Strict mode** ([PD10](decisions.md#pd10--strict-no-load-time-compilation-mode)); dynamic-model
  hop sites (`:: dynamic`, model-less) are a declared class outside the NativeAOT claim.
- **Parity**: the phase 1 harness runs with and without the site table, both byte-identical to
  the dynamic tier.
- **Evidence**: `TechniquePrecompiledBenchmarks` over 8 workloads × 3 sinks against the runtime
  rows on the same machine and job; a cold row in `ColdCompileBenchmarks` and the harness contract
  under [benchmarks/docs](../../../benchmarks/docs/README.md) — registration + materialization +
  first render of `composed-page`, with and without the site table, beside `CompileHeddle`;
  artifact size and materialization time recorded; a NativeAOT-published sample rendering the
  typed workloads under strict mode, its publish log kept with the report.

## Exit criteria

1. Under strict mode the corpus renders from generated sites except at declared sites, by set
   equality against the intent table.
2. `gate-precompiled` passes with and without the site table.
3. Published tables show v3 rows for 8/8 workloads within BenchmarkDotNet's reported error of the
   runtime rows with equal allocation, and the cold table shows the artifact path strictly below
   `CompileHeddle`.
4. The NativeAOT sample publishes and renders under strict mode; the trimmed publish carries no
   `Microsoft.CodeAnalysis` when every C# site is generated.
5. No generated accessor allocates per call beyond the box the engine's accessor produces for a
   value-type result.

## Validation scenarios

- `@(price * 1.2m + fee)` with mixed numeric kinds: the printed method applies the engine's
  promotion from the bound tree; a constant division by zero stays `HED1018` at build.
- Embedded C# reading `root` and `chained` with a `@using`: compiles in the consumer's assembly;
  an assembly-load probe shows no Roslyn at run time.
- A body whose model type a custom extension's hook chooses: member sites inside it are generated
  when the build's engine run resolved that type, otherwise rebuilt.
