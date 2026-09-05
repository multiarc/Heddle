# Precompilation v2 — specification

**Status:** Specified — ready for implementation.
**Owning plan:** [docs/plan/precompilation-v2/](../../plan/precompilation-v2/README.md) (final, ratified; decisions
[PD1–PD11](../../plan/precompilation-v2/decisions.md); the [v3 window](../../plan/precompilation-v2/v3-window.md)).
**Assumed prior specs:** none — this initiative builds on the 2.1 line as found in source.

| Document | Purpose |
| --- | --- |
| [artifact-contract.md](artifact-contract.md) | The compiled form: container, sections, encodings, identity forms, site ids, parse facts, determinism — the one contract the build host and the runtime loader must agree on; referenced by phases 1–3. |
| [phase-1-compiled-form.md](phase-1-compiled-form.md) | Serializer, loader, marker cutover, deferred function binding, binding gate, refusal classes, the parity harness; the generator leaves the build. |
| [phase-2-build-integration.md](phase-2-build-integration.md) | `Heddle.Build`: targets, the out-of-process `heddle compile` host, incrementality, typed wrappers, build diagnostics, the MSBuild-evaluating test suite. |
| [phase-3-generated-sites.md](phase-3-generated-sites.md) | Generated member accessors, native expressions and embedded C# by site id; strict no-load-time-compilation mode; AOT posture; benchmarks and the NativeAOT sample. |
| [phase-4-removal-and-release-tail.md](phase-4-removal-and-release-tail.md) | Deletion of the generator and of every 2.x-only member, the stored 2.x-marker rejection fixture, the retired-property warning, retire-in-place of `HED70xx` ids, spec-record updates, docs, migration note, golden re-ratification, reconciliation. |

## Scope and goal

The precompiled tier stops being a second compiler. The real engine compiles each template at build time, out of
process, and serializes what it compiled — the shaped document, static pieces, the call graph, bound identities and
native-expression syntax trees — into an artifact embedded in the consumer's assembly. At load the engine
materializes the very objects its text path builds, so the render path is one code path and parity is structural.
Generated code shrinks to what cannot be data: compiled delegates, printed from the engine's bound trees and keyed by
stable site ids, with the data path complete underneath them.

In scope: the artifact contract, the loader, deferred function binding, the `Heddle.Build` package and host, the site
printer, strict mode, the evidence benchmarks, and the removal of `Heddle.Generator` with everything in `Heddle.dll`
that existed only for generated 2.x code. Out of scope: template semantics, the dynamic tier's behaviour, rendered
bytes, extension hook serialization, hot reload, non-.NET consumers, AOT of the dynamic tier
([plan non-goals](../../plan/precompilation-v2/README.md#non-goals)).

## Assumed state

Verified against source on `feature/benchmarks` (commit `654959b5`); each phase document carries the seams it
touches with paths and member names. The precompiled tier is a Roslyn source generator (`src/Heddle.Generator`, three
Roslyn builds, two suites) emitting C# that calls `Heddle.Precompiled.PrecompiledRuntime` and a manifest of eager
`PrecompiledTemplateInfo` rows behind `HeddleCompiledTemplatesAttribute` at schema 3; the engine's `RuntimeDocument`
already pre-encodes static pieces to UTF-8; the registry (`PrecompiledTemplates`), gauntlet (`PrecompiledGauntlet`),
fallback taxonomy (`PrecompiledFallbackReason`, `HED7101`–`HED7104`) and `TemplateResolver`'s registry-first ladder
are the surface hosts use; `src/Heddle.Tool` is the `heddle render` CLI on `net10.0`; the corpus intent table
(`src/TestCorpus/CorpusIntent.cs`) has 72 rows and five tiers; five test suites gate membership through
`test-classes.txt`.

## Global invariants

Every phase document is bound by these records; none restates them.

### GI-1 — `netstandard2.0`, Roslyn-free read path

**Decision.** Serializer and loader live in `Heddle.dll`; loading touches no `Microsoft.CodeAnalysis` type unless the
artifact carries an embedded-C# site under `ExpressionMode.FullCSharp`, which compiles through the engine's existing
`CSharpContext` path behind `HeddleFeatures.CSharpTierEnabled` (`HED9001` when disabled).
**Rationale.** Plan constraints; the read path must run wherever the engine runs, trimmed hosts included.

### GI-2 — Sandbox unchanged

**Decision.** Load-time compilation and member-path rebuilding run the engine's existing checks
(`MemberPathResolver`, `MemberVisibility`, `NativeExpressionCompiler`); `ExpressionMode` stays in the options
fingerprint; nothing in the compiled form can open an execution path the request's options close.
**Rationale.** Precedence rule 2 of the coding standards: security beats ergonomics and performance.

### GI-3 — No per-render allocation

**Decision.** Materialization happens once per registry entry and is memoized; a loaded strategy is the engine's own
`RuntimeDocument` strategy and allocates exactly what the dynamic tier allocates, proved by allocation-equality
benchmarks.
**Rationale.** The render path is hot and the plan's render claim is measured, not asserted.

### GI-4 — D11

**Decision.** The loader resolves type identities per [AC-4](artifact-contract.md#ac-4--type-identity) over
registered model assemblies (`AssemblyHelper.RegisterModelAssemblies`, `[HeddleModelAssembly]`) and assemblies
already loaded into the default load context, and compares everything reached through the member graph by identity.
It loads nothing.
**Rationale.** [D11](../common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded).

### GI-5 — Faults

**Decision.** A template fault is a positioned `HED####` diagnostic — at build through the host, at load through the
gauntlet's `HED7101` fallback taxonomy. A host or deployment fault throws (`PrecompiledRegistrationException`,
`PrecompiledMismatchException`, `PrecompiledStrictLoadException`, `ArgumentNullException`).
**Rationale.** Coding standards § error handling.

### GI-6 — v3 rejects 2.x artifacts

**Decision.** A `HeddleCompiledTemplatesAttribute` whose `SchemaVersion` is below
`PrecompiledSchema.CompiledFormSchemaVersion` (`4`) is a 2.x manifest; `PrecompiledTemplates.Register` throws
`PrecompiledRegistrationException` naming the assembly and the remedy, from phase 1. No member of `Heddle.dll` exists
to read, adapt or bridge a 2.x manifest.
**Rationale.** [PD1](../../plan/precompilation-v2/decisions.md#pd1--generator-removal-and-cutover),
[PD4](../../plan/precompilation-v2/decisions.md#pd4--artifact-compatibility): a 2.x manifest's IL names members the
engine does not carry.

### GI-7 — Retirement is in place

**Decision.** An id, `PrecompiledFallbackReason` member or constant that stops firing keeps its declaration, catalog
row, registry row and published mention; a public member with no possible caller is removed.
**Rationale.** [D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) for ids; dead surface
otherwise has to be documented and trimmed for nothing.

## Implementation plan

[D5](../common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order): phases
1 → 2 → 3 → 4, each landed as the vertical slices its *Implementation plan* numbers (`P1-W1`… `P4-W9`). A phase may
assume every earlier phase merged; nothing depends on a later phase. Phase 1 takes the generator out of the build;
phase 4 deletes its files. Nothing from phases 1–3 enters a
release: the v3 line ships whole with phase 4 ([PD8](../../plan/precompilation-v2/decisions.md#pd8--the-window)).

## Public API contract — summary

The complete signatures live in each phase's *Public API contract*; this is the map.

| Surface | Phase | Change |
| --- | --- | --- |
| `Heddle.Precompiled.IHeddleCompiledArtifact` | 1 | New: `Stream OpenArtifact()`; implemented by the generated `HeddleArtifact` class the marker attribute names. |
| `PrecompiledSchema.CompiledFormSchemaVersion` = 4 | 1 | New; `Min = Current = Max = 4`. |
| `PrecompiledRegistrationException(string assemblyName, int schemaVersion)`, `SchemaVersion` | 1 | New: the 2.x-marker rejection. |
| `PrecompiledFallbackReason.MemberBindingMismatch` | 1 | New, appended; must-surface; reported through `HED7101`. |
| `PrecompiledRefusalSite`, `PrecompiledRefusalClass`, `PrecompiledTemplateInfo.RefusalSites` | 1 | New: the declared-exception API. |
| `PrecompiledTemplateInfo` | 1, 4 | Rows are loader-constructed; `Strategy` materializes lazily; `IsPrecompiled` is true for every row; phase 4 removes the public constructors, `Capabilities`, `LinePathForm`, `InitSites`, `IsPrecompiled` and makes `Strategy` internal. |
| `PrecompiledTemplates.DefaultOptions`, `PrecompiledTemplates.BindTyped`, `HeddleBuildOptions.OutputProfileMetadata` | 2 | New ([PD2](../../plan/precompilation-v2/decisions.md#pd2--typed-entry-points), [PD3](../../plan/precompilation-v2/decisions.md#pd3--msbuild-surface)). |
| `Heddle.Generated.<SanitizedName>.Generate(...)` ×3 | 2 | Unchanged signatures; wrappers over `BindTyped`. |
| `IPrecompiledSiteTable`, `TemplateOptions.PrecompiledStrictLoad`, `PrecompiledStrictLoadException` | 3 | New ([PD10](../../plan/precompilation-v2/decisions.md#pd10--strict-no-load-time-compilation-mode)). |
| `PrecompiledRuntime` and the 2.x generated-code seams | 4 | Removed; enumerated in [phase 4 § P4-R2](phase-4-removal-and-release-tail.md#p4-r2--every-member-that-existed-only-for-generated-2x-code-is-removed). |

## Diagnostics — summary

New ids, claimed in the [registry](../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) with this
spec: `HED7035` (`Heddle.Build`/`Heddle` version mismatch, error, phase 2), `HED7036` (implementation assembly
unloadable, error, phase 2), `HED7037` (retired MSBuild property set, warning, phase 4). Retire-in-place and engine-id
forwarding per [PD11](../../plan/precompilation-v2/decisions.md#pd11--engine-ids-at-build) are tabulated in
[phase 4 § Diagnostics](phase-4-removal-and-release-tail.md#diagnostics).

## Cross-phase testing gates

Every landing runs the [regression gate](../common/testing-standards.md#regression-gates): `dotnet build -c Release`;
every test suite in the solution through its own `test-classes.txt` set-equality gate (three suites from phase 1,
four from phase 2); no diff under `src/Heddle.Language/generated/`; `dotnet run -c Release --project benchmarks/dotnet
-- gate` and, from phase 2, `gate-precompiled` and the technique benchmarks when a hot path is touched (phase 1's
allocation-equality gate is `CompiledFormAllocationTests` in `Heddle.Tests`); `npm run docs:build` when docs change. The
corpus sweep (`src/TestCorpus/CorpusIntent.cs`) is the single definition of what does not precompile, gated by set
equality; its intent table is redefined in
[phase 1 § P1-R8](phase-1-compiled-form.md#p1-r8--refusal-classes-and-the-intent-table).

## Back-compat and migration

Everything here is a v3 window item ([v3-window.md](../../plan/precompilation-v2/v3-window.md#window-items)): items 3,
7 and 9 land in phase 1, items 5 and 8 in phase 2, items 1, 2, 4, 6 and 10 in phase 4 together with the migration
note, the golden re-ratification commit (expected zero rendered-golden churn) and the post-release reconciliation.
Each phase's *Back-compat and migration* section names the items it lands.

## Performance considerations

The render path is the dynamic tier's; phase 1 proves allocation equality in-suite (`CompiledFormAllocationTests`),
phase 2 guards it with `TechniquePrecompiledBenchmarks` versus `TechniqueRuntimeBenchmarks` (equal allocation, mean
within error), and phase 3 adds `TechniquePrecompiledDataOnlyBenchmarks` and the cold-start row. Build-time cost is one
host process per changed compiling project.

## Standards compliance

Correctness first: parity is structural, and every claim about it is a byte comparison. Simplicity over abstraction:
one contract document, one host verb, one seam added (the form cursor). YAGNI: no speculative options. Retirement in place for ids, deletion for members.

## Deferred items

Consolidated from the plan's revisit triggers: an options-carrying typed-entry overload (hosts rendering under several
options shapes from typed entries); a self-contained build host per RID (build machines that cannot carry a .NET 10
runtime); generated code in a satellite assembly (the same-project intermediate compile proving too slow); load-time
body re-typing retiring refusal class (c) (byte parity proved over the corpus); the next-window candidate register's
rows this window does not adopt (default-encoder swap, `AllowCSharp` removal, `[NotEncode]` deletion, betterness in
the overload binder, visibility widenings, must-surface mismatches throwing by default).

## External references

- [.NET library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
- [MSBuild `ToolTask`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.utilities.tooltask) and the
  [canonical error format](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks)
- [Incremental builds with `Inputs`/`Outputs`](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-build-incrementally)
- [`AppContext` feature switches and ILLink substitutions](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options#trim-framework-library-features)
- [NativeAOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [`System.Reflection.Metadata` `ModuleDefinition.Mvid`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.moduledefinition.mvid)
- [NuGet package deprecation](https://learn.microsoft.com/en-us/nuget/nuget-org/deprecate-packages)
