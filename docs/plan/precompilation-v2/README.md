# Precompilation v2 — a serialized compiled form, not a second compiler

> Final plan, ratified by the maintainer, and the planning document of the **v3 breaking
> window** ([v3-window.md](v3-window.md)). Decisions: [decisions.md](decisions.md). Specs turn
> each phase into "exactly how"
> ([spec conventions](../../spec/common/spec-conventions.md#relationship-to-the-owning-plan)).

## Summary

The build-time tier is a Roslyn source generator that re-implements the engine from linked
source, binds through emitted text, and needs an intermediate "observe" compile to learn what the
engine would have typed. It costs a match principle, a differential corpus, seventeen refusal
categories, three Roslyn-version builds of one package and a standing architecture document — and
it renders slower than the tier it imitates.

v3 replaces it with **one compiler and a serialized result**:

1. The **real engine compiles the template at build time**, out of process in the `heddle` CLI,
   and **serializes what it compiled** — shaped document and pieces, call graph, bound identities
   (extensions, types, member paths, function call shapes, prop layouts, imports) and
   native-expression syntax trees — into an artifact embedded in the consumer's assembly.
2. At load, the engine **materializes the very objects its text path builds**, so the render path
   is the same code, UTF-8 piece handling included. Parity is structural.
3. **Only what cannot be data is generated code**: compiled delegates. Member accessors, native
   expressions and embedded C# become typed generated methods keyed by stable site ids, printed
   from the engine's bound trees. A site the printer declines is rebuilt at load from its
   serialized form, so generated code is an optimisation over a complete data path.

### Motivation — measured

Steady-state, `TechniquePrecompiledBenchmarks` vs `TechniqueRuntimeBenchmarks`
([consolidated-tables.md](../../benchmarks/2026-08-08/consolidated-tables.md), run
`windows-run-20260808-013409`):

| Workload / sink | Runtime tier | Precompiled tier | Ratio |
| --- | ---: | ---: | ---: |
| trivial-substitution / Utf8 | 132 ns, 208 B | 202 ns, 176 B | 1.53× |
| mixed-page / Utf8 | 2,797 ns, 4,016 B | 4,377 ns, 3,984 B | 1.56× |
| fragment-heavy / TextWriter | 2,954 ns | 2,968 ns | 1.00× |
| large-loop / Utf8 | 157.0 µs | 220.8 µs | 1.41× |
| large-loop / String | 1.14 ms, 1.20 MB | 2.57 ms, 1.58 MB | 2.25× |

Streaming sinks run 1.0–1.6× slower precompiled, the string sink up to 2.25× with 1.3–1.8× more
allocation; 5 of 8 corpus workloads precompiled; no benchmark measures startup
(`ColdCompileBenchmarks` covers `composed-page` only: `CompileHeddle` 527 µs, no precompiled row).
A tier that materializes the dynamic tier's own objects closes the render gap by construction,
and startup becomes the measurable claim.

### Outcome

- Same registry (`PrecompiledTemplates.Register` / `TryResolve` / `ValidateAll`), gauntlet,
  fallback taxonomy and `HED71xx` ids; same `HeddleTemplate` items, metadata, option properties
  and `.heddle-lsp.json` spellings; same `Heddle.Generated.<Name>.Generate(model)` entry points.
- Migration: swap `Heddle.Generator` for `Heddle.Build`, rebuild every precompiled assembly, drop
  the retired properties. No C# call-site change.
- Build-time template errors are the engine's own `HED` ids at `.heddle` positions.
- Precompiled render speed equals the dynamic tier; startup skips parse, shaping, binding and —
  with generated sites — expression compilation, which NativeAOT and interpreter hosts need.

### Non-goals

- No change to template semantics, to the dynamic tier's behaviour, or to rendered bytes.
- No serialization of extension hook state: hooks run at load over the deserialized body.
- No artifact file format for non-.NET consumers; no hot reload; no AOT of the dynamic tier.
- No new legitimate-degrade fallback class.

## Phases

| # | Phase | Depends on | Goal |
| --- | --- | --- | --- |
| 1 | [Compiled form and loader](phase-1-compiled-form.md) | — | Serialize a compiled template and re-materialize it into the engine's own objects; every corpus template round-trips byte-identically with no per-render allocation delta |
| 2 | [Build integration](phase-2-build-integration.md) | 1 | `Heddle.Build` produces artifact, manifest and typed wrappers from the real engine; registry, gauntlet, items and options carry over |
| 3 | [Generated sites and evidence](phase-3-aot-sites.md) | 1, 2 | Generated methods for accessors, native expressions and embedded C#; strict no-compilation mode; benchmarks prove render parity and startup; AOT sample |
| 4 | [Removal and release tail](phase-4-removal-and-release-tail.md) | 1–3 | The window's removals land together with the documentation, the migration note and the last-2.x deprecation |

## Success criteria

1. **Parity.** Every corpus template (`src/TestCorpus/CorpusIntent.cs`) the dynamic tier
   compiles renders byte-identically through the artifact on all three sinks under
   `PrecompiledMismatchPolicy.Strict` with a fallback sentinel
   ([posture](../../spec/common/testing-standards.md#precompiled-tier-posture)); declared
   exceptions are enumerable by one API.
2. **Coverage.** `gate-precompiled` reports 8/8 workloads; the intent table is the sole
   definition of what does not precompile, every row a phase 1 refusal class.
3. **Render.** Per workload × sink, precompiled mean within BenchmarkDotNet's reported error of
   the runtime tier and equal allocated bytes — measured, not asserted.
4. **Startup.** Registration + first render of `composed-page` through the artifact is strictly
   below `CompileHeddle`, with and without generated sites, published in the report.
5. **AOT.** Under strict mode the corpus renders from generated sites except at declared sites
   (dynamic-model hops are a declared class); a NativeAOT sample renders the typed workloads.
6. **Single source.** No `src/Heddle` file is linked into another production project; the build
   claims no id for a fact the engine diagnoses.
7. **Migration.** One package swap and a rebuild; no C# call-site change on the command line or
   in the IDE; runtime-registered functions precompile in bodiless value positions.
8. **Gates.** Build, four test suites with `test-classes.txt` set-equality, no diff under
   `src/Heddle.Language/generated/`, benchmarks and docs build pass on each landing.

## Constraints

- Load path is `netstandard2.0`-clean and Roslyn-free; `Microsoft.CodeAnalysis` stays behind
  `Heddle.CSharpTierEnabled` for the C# tier only.
- Sandbox posture unchanged: load-time compilation and member-path rebuilding run the engine's
  existing checks; `ExpressionMode` stays in the fingerprint.
- [D11](../../spec/common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded):
  the loader resolves type names over registered and already-loaded assemblies only.
- No per-render allocation; materialization once per entry.
- Build machines need a .NET 10 (or later) runtime; the host binds over implementation images on
  that runtime ([PD6](decisions.md#pd6--build-host)).
- Retirement is in place: an id, reason or category that stops firing keeps its constant.
