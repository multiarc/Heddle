# Phase 4 — Removal and release tail

Back to the [plan](README.md). Depends on phases 1–3; the window's release tail under
[v3-window.md](v3-window.md#execution-order).

## Goal

End the second compiler in the v3 release and ship the window's deliverables.

## Scope

- **Removals** ([window items 1–4, 6](v3-window.md#window-items)): `src/Heddle.Generator` (three
  csproj), `src/Heddle.Generator.Tests`, `src/Heddle.Generator.IntegrationTests`, their solution
  rows, CI legs, analyzer packaging and observe machinery; everything in `Heddle.dll` that
  existed for generated 2.x code, enumerated by the spec from `public-api-heddle.txt`; the
  `PrecompiledSchema` window restarting at the v3 schema; the retired properties' warning; every
  `HED70xx` id whose fact no longer exists retired in place (constant, catalog row, registry row
  and published mention stay), the projection corpus dropping the generator host.
- **Spec records** (item 10): `shared-source-architecture.md` reduced to what still governs
  (`Heddle.Language` as the LSP front end, the diagnostic catalog and projection single source, the
  `LineIndex` rule, template identity) with the rest collapsed into a program record; D4 recorded
  as superseded by a dated note; generator bullets removed from `.claude/rules/api-compatibility.md`
  and the merge gate. The testing standards' precompiled-tier posture is kept, reworded from
  "generator output" to "artifact".
- **Documentation**: `precompilation.md` rewritten for v3 (setup, items, options, registry and
  gauntlet carry over; observe, node-fallback, refusal-category and build-diagnostic sections are
  replaced by the v3 boundary and the bodiless rule); `csharp-api.md`, `building.md`,
  `editor-support.md`, `custom-extensions.md` updated in the same landing; documentation-currency
  gates green.
- **Release-tail deliverables**: NuGet deprecation of the last 2.x `Heddle.Generator`; the
  [migration note](v3-window.md#migration-note) with release notes and CHANGELOG; the golden
  re-ratification commit (expected zero rendered-golden churn; the intent table's owner column and
  the deleted suites' snapshots are its only churn); reconciliation after `v3.0.0` is tagged.

## Exit criteria

1. `git grep -i generator` under `src/` matches only the ANTLR parser generator and retired-in-place
   annotations; the solution carries four test suites, each gated by `test-classes.txt`.
2. `Heddle.dll`'s public API golden contains no member whose only caller was generated 2.x code,
   and every removed member is listed in the migration note.
3. 2.x assembly rejection per [PD4](decisions.md#pd4--artifact-compatibility) is covered by a
   fixture.
4. `DiagnosticIdTests` passes with retired ids present and unclaimed for reuse; the documentation
   gates pass; each retired property behaves per [PD3](decisions.md#pd3--msbuild-surface).
5. Build, tests, benchmarks gate and docs build are green; the reconciliation records every window
   item as shipped or dispositioned.

## Validation scenarios

- A consumer still on `Heddle.Generator` at v3: restore shows the deprecation; the fix is the
  package swap and rebuild.
- A consumer that never precompiled: no observable change.
- The LSP builds and passes its suite with no generator project in the graph.
