# Phase 2 — Build integration

Back to the [plan](README.md). Depends on [phase 1](phase-1-compiled-form.md).

## Goal

Ship the compiled form from the consumer's own build through one package, with the MSBuild and
registry surface hosts already use. The build runs the real engine and serializes its result.

## Scope

- **`Heddle.Build`**: props/targets plus the build host — `heddle compile` in `src/Heddle.Tool`,
  taking items, metadata, options and the implementation assemblies to bind over, compiling every
  template through `HeddleTemplate` with deferred function binding, and writing the artifact, the
  manifest source, the typed wrappers and diagnostics ([PD6](decisions.md#pd6--build-host)). The
  build fails when `Heddle.Build`'s version differs from the referenced `Heddle`
  ([PD4](decisions.md#pd4--artifact-compatibility)).
- **Inputs are declared**: `HeddleTemplate` items with `Key`, `Name`, `ModelType`, `Precompile`,
  `OutputProfile`; assembly items and the `HeddleModelAssembly` declaration; the option properties
  ([PD3](decisions.md#pd3--msbuild-surface)); implementation images resolved by the targets — one
  that cannot be found or loaded is a build error naming it. Same-project models and design-time
  builds per [PD7](decisions.md#pd7--same-project-models-and-design-time-builds).
- **Outputs**: an embedded artifact and a manifest behind `[assembly: HeddleCompiledTemplates(...)]`
  with one row per template carrying the identities the gauntlet checks; the typed wrappers
  ([PD2](decisions.md#pd2--typed-entry-points)).
- **Incrementality**: compile re-runs only when template text, item metadata, option properties,
  the `Heddle.Build` version, or the module identity (MVID or equivalent) of an implementation
  assembly bound over changes; artifact bytes are a function of exactly those inputs.
- **Diagnostics**: engine ids at `.heddle` positions ([PD11](decisions.md#pd11--engine-ids-at-build));
  the build-only facts the docs list keep their build ids; a "not fully precompiled" notice covers
  refusals, late-bound names and C# carried as data.
- **Registry and gauntlet** carry over unchanged, plus the new binding check for member paths and
  late-bound functions. Bindings are available to `ValidateAll` without materializing a strategy;
  strategies materialize on first resolve and are memoized.
- **In-repo consumers**: `samples/precompiled-app` moves; `benchmarks/dotnet/precompiled-html`
  folds into the main benchmark project through per-item `OutputProfile`.
- **Tests**: a new suite gated by its own `test-classes.txt` that evaluates real MSBuild — project
  file, targets, an actual `compile` run; the phase 1 harness gains a pass over host-built artifacts.

## Exit criteria

1. `samples/precompiled-app` and the benchmark project build with `Heddle.Build` alone and render
   the same bytes; `gate-precompiled` reports 8/8.
2. Every public item, metadatum and property is read by the host and exercised by an
   MSBuild-evaluating test; a design-time build yields compiling wrapper stubs.
3. A stale template, changed import, different `OutputProfile` request, swapped
   `[ExtensionReplace]` binding, renamed model member and missing function each produce a gauntlet
   reason and id through the unchanged API — none a render-time exception.
4. A template error fails the build at its `.heddle` position with the engine's id; the LSP shows
   the same id at the same position.
5. An unchanged rebuild runs no `compile`; a rebuilt referenced model project runs one.
6. A `Heddle.Build`/`Heddle` version mismatch fails the build with a positioned error naming both
   versions and the remedy.

## Validation scenarios

- Models in a referenced project: the host binds over its implementation output; no intermediate
  compile. Models in the project itself calling `Heddle.Generated.X.Generate`: one intermediate
  compile with stubs, reused on an unchanged rebuild.
- `net48`, `net8.0` and `net10.0` projects build with one `Heddle.Build` on a machine with a .NET
  10 runtime; a template reading a BCL member absent on `net48` builds, and `ValidateAll` on the
  `net48` host reports the miss ([window item 8](v3-window.md#window-items)).
- A typed wrapper whose artifact fails validation throws `PrecompiledMismatchException`; with the
  host's default options assigned, a late-bound function resolves against the host's registry.
