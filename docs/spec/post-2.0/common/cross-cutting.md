# Cross-cutting decisions — post-2.0 set

Decisions shared by two or more phases in this set, stated once here (DRY) and cited by the phase
specs rather than restated. This complements the repo-global
[cross-cutting-decisions.md](../../common/cross-cutting-decisions.md) (D1–D9 + the diagnostic
registry); the items below are **initiative-scoped**. Each was verified against current source during
spec authoring; corrections to the plan's framing are recorded with evidence.

Back to the [set index](../README.md).

## X1 — The shared `HeddleTemplate` disposal primitive (Phase 1 ↔ Phase 4)

**Decision.** Phase 4 owns the disposal-synchronization fix on the fields
[Phase 1](../phase-1-file-watcher.md) and [Phase 4](../phase-4-engine-robustness.md) both touch on
`HeddleTemplate` (`src/Heddle/HeddleTemplate.cs`): `_runners`, `_disposeAfterComplete`,
`_runtimeDocument`, `_processStrategy`. Phase 4 introduces a lock-free, zero-render-alloc primitive —
`Interlocked` bookkeeping over `_runners`, `_disposeAfterComplete` promoted to `int` (set via
`Interlocked.Exchange`), a one-shot `_teardownDone` CAS guard, and the helpers
`EnterRender()` / `ExitRender()` / `Teardown()` with a memory-ordering proof that teardown can never
run while a render executes. **Phase 1's live-document swap-and-release of a *superseded* runtime
document builds directly on this primitive** — a render takes its single volatile snapshot of
`_runtimeDocument` **after** `EnterRender()` increments `_runners`, so the release gate provably
covers every render that could hold the snapshot. Because `FileSystemWatcher` raises its handlers on
threadpool threads **concurrently** (and the stress test also calls `Recompile` concurrently), Phase 1
serializes the whole capture-superseded → publish (document-then-strategy) → enqueue → drain sequence
under a Phase-1-owned `_publishGate` lock that **renders never take** (it is off the render hot path);
a superseded document is disposed only under that lock and only when `Volatile.Read(_runners) == 0`, so
it is released **exactly once** and never while any render holds it. The store block additionally
honors an already-requested dispose (checking `_disposeAfterComplete` under the gate) so a watcher
callback landing after `Teardown()` disposes the fresh artifact instead of leaking or resurrecting it.
Phase 1 adds the release plumbing (`_publishGate`, a `volatile _supersededDocs` queue, `DrainLocked`,
and a drain under the gate in `Teardown`), and **restructures** `ExitRender` so the `_runners == 0`
detection is split out of Phase 4's fused `Interlocked.Decrement(ref _runners) == 0 && _disposeAfterComplete != 0`
condition: Phase 1 must drain superseded documents whenever `_runners` reaches 0 **regardless** of the
dispose flag, so it hooks a gated drain onto the bare-zero detection (which Phase 4's fused condition does
not expose as a standalone branch) rather than onto the dispose-gated `Teardown()` call. It does **not**
change Phase 4's `_runners`/`_disposeAfterComplete`/`_teardownDone` atomic-state semantics and does **not**
re-specify the primitive.

**Rationale.** Arming the watcher (Phase 1) without Phase 4's synchronization would introduce a live
recompile-vs-render/dispose race that does not exist today only because the watcher is inert
(verified: `EnableRaisingEvents` is never set in any `.cs`; the recompile at the `_runtimeDocument`
overwrite is uncoordinated with `Render`'s counter). Declared order therefore treats Phase 4's
disposal decision as the assumed foundation for Phase 1
([D5](../../common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order)).
**Field-diff reconciliation:** whichever phase lands second merges the two diffs to the shared fields;
Phase 1's spec carries the explicit reconciliation table, and no change is made to Phase 4's
`_runners`/`_disposeAfterComplete`/`_teardownDone` semantics.

## X2 — The four-layer prop / region realization (Phase 7 & Phase 8)

**Decision.** A "typed named contract passed at the call site" is realized across **four layers**, and
those layers are **not uniform** — a fact both [Phase 7](../phase-7-named-content-regions.md) (a new
named-region table) and [Phase 8](../phase-8-extension-parameters.md) (a new population source for the
prop table) thread their feature through, each layer *as it actually works*:

| Layer | How the prop/region contract is realized (verified) |
|---|---|
| **Dynamic runtime** | `HeddleCompiler.BindProps` resolves against `PropLayout` and **emits `HED5001`–`HED5004`** (≈1290–1383); `HED5005`/`HED5006` in `CompileItem`/`CreateExtension` (≈762 / ≈1165). `PropLayout.Resolve` raises only declaration-side `HED5008`/`HED5009`/`HED5010`. |
| **LSP** | **Transitive** — `DocumentAnalyzer` drives `HeddleCompiler.Compile` (≈45); there is no direct `PropLayout`/`PropsBinder` reference, so new diagnostics surface for free. |
| **Generator** | Its **own** Roslyn-symbol reimplementation `PropLayoutInfo` ("Reimplements `PropLayout.Resolve` over symbols") in `TemplateEmitter`; it does not link the `internal` runtime types. |
| **Precompiled** | A **separate** mechanism — `PrecompiledRuntime.BindDefinition` installs a frozen `object[]` prototype + `PrecompiledPropSetter[]` via `DefinitionBaseExtension`, **bypassing `PropsBinder`**. |

**Corrections to the plan's framing (recorded with evidence).** The plan described "three layers over
the internal `PropLayout`/`PropsBinder`"; verification shows (a) `HED5001`–`HED5004` are raised in
`HeddleCompiler`, not `PropsBinder`; (b) the precompiled tier does **not** use `PropsBinder` (separate
setter path); (c) the LSP link is transitive. Both phases describe their new source through each layer
accurately rather than assuming uniform reuse.

**Rationale.** Mirroring the proven prop machinery (not inventing a parallel one) keeps the backends at
parity; the differential harness that asserts dynamic == precompiled byte-identity is the gate. Both
phases roll their change out across all four layers in one wave.

**Precompilation of overrides (maintainer ruling, [OQ1](../OPEN-QUESTIONS.md), 2026-07-13).**
Override/inheritance does **not** inherently stop precompilation — a call site is a *materialized*
definition with a final form; the real limitation is compiling child and parent *separately* rather
than the whole set together. So [Phase 7](../phase-7-named-content-regions.md) **natively precompiles
region fills** (compiling the whole parent+child+override materialization together; multi-file needs
more checks + cache-busting), lifting the generator's `DefinitionInvolvesOverride`→dynamic degrade for
that case; the dynamic tier is a genuine **escape hatch only**. This is definition-composition scope;
[Phase 8](../phase-8-extension-parameters.md)'s **custom-extension bodied-call** fallback is a
different, pre-existing mechanism and is unchanged.

## X3 — Coordinated diagnostic-ID allocation (Phase 2 / 3 / 7 / 8)

**Decision.** The new diagnostics are allocated once, collision-free, and claimed into the repo-global
[registry](../../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) in the change that
introduces each ([D1](../../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)):

| ID(s) | Phase | Kind | Notes |
|---|---|---|---|
| `HED2004` | 3 | warning | `MissingContextEncoder`. Next-free in the output/encoding family. |
| `HED4005` | 2 | warning | `LiquidStyleInterpolationMisread`. Next-free in the ergonomics family. |
| `HED5019`–`HED5020` | 7 | compile errors | `RegionNotPublic` (override targets a private region) / `DuplicateRegionDeclaration` (two public regions same name). Fire only on the new public-region surface. A narrowing mismatch reuses the pre-existing id-less `WalkValidateDefinitionType`; a typed-override member error reuses `HED0001` — **no `HED5021`**. |
| `HED7017` | 8 | build-time error | Generator twin for a malformed extension `[Prop]` (incl. an inherited-`[Prop]` re-declaration widening). Phase 8 **reuses** call-time `HED5001`–`HED5004` and declaration-side `HED5007`/`HED5008`/`HED5009`/`HED5010`/`HED5015`, and **relaxes `HED5005` only** — no new call-time id. |

**Grounding / discipline.** Verified against `HeddleDiagnosticIds.cs` (highest allocated: `HED2003`,
`HED4004`, `HED5018`) and `GeneratorDiagnostics.cs` (`HED7016`), and pinned by
`DiagnosticIdTests.ConstantsMatchTheDiagnosticsTableOneToOne` — every new **runtime** const
(`HED2004`, `HED4005`, `HED5019`–`HED5020`) must be added to both the const list **and** the
diagnostics table in the same change, or that test fails. `HED7017` is a generator
`DiagnosticDescriptor` (separate home), so it needs no `HeddleDiagnosticIds` const. `HED5006` and the
header-slot diagnostics `HED5016`/`HED5017` are **untouched** by this set. Phase 6 keeps `HED4001`
(no new id).

## X4 — Phase 6 lands in the still-open 2.0 breaking window

**Decision.** `git tag -l` shows only `v1.0.0`/`v1.0.1` — **no `v2.0.0` tag exists**, and
`CHANGELOG.md` `[Unreleased]` states 2.0 "has **not** shipped yet … a single breaking window." So the
2.0 breaking window is still open at the code level, and [Phase 6](../phase-6-range-type.md)'s deletion
of `ForModel` is added to it (R5 revised) with a migration note as the window deliverable
([D2](../../common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows),
[breaking-windows.md](../../common/breaking-windows.md)). [Phase 5](../phase-5-docs-and-benchmark-credibility.md)
reconciles the docs *toward* "2.0.0 as the current release line," worded to be correct-and-consistent
the moment the adjacent release action lands (the `v2.0.0` tag cut, package publish, and
`CHANGELOG` `[Unreleased]` → dated `[2.0.0]` move) — which the specs anticipate, not perform.

**Handling of `records.md`.** Under the maintainer rulings **SR-A** (retire-and-relocate) and **SR-B**
(window-open reconciliation), Phase 5 applies a **bounded correction** to `records.md`: it de-links the
retired-file citations to the plain-text form the file's own append-only header mandates, and reconciles
the premature "2.0 window closed" wording to **window-open** (the as-shipped intro, the item-2/item-4
dispositions, and the amendments-ledger slippage rows). Every recorded fact and the append-only
as-shipped **table** rows are **preserved** — no historical fact is altered or removed; the change is a
correction, not a rewrite. Phase 6 still records its own window membership via an **amendments-ledger
append**. This keeps the append-only discipline intact while the two facts the docs must stop
conflating — "2.0 as a feature set" vs. "2.0.0 as a released tag" — are separated in the release-forward
direction, with the whole set reading the 2.0 breaking window as **still open** until the `v2.0.0` tag
is cut.
