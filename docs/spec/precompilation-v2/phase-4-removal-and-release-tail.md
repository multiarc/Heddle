# Phase 4 — Removal and release tail

**Status:** Specified — ready for implementation. **Plan item:**
[phase-4-removal-and-release-tail.md](../../plan/precompilation-v2/phase-4-removal-and-release-tail.md); the window's
release tail under [v3-window.md](../../plan/precompilation-v2/v3-window.md#execution-order). **Entry document:**
[README.md](README.md). **Depends on:** phases [1](phase-1-compiled-form.md), [2](phase-2-build-integration.md) and
[3](phase-3-generated-sites.md) merged with their evidence published.

## Scope and goal

End the second compiler in the v3 release. Delete `Heddle.Generator` and its suites from disk, everything in
`Heddle.dll` that existed only for generated 2.x code, and add the stored 2.x-marker rejection fixture; add the
retired-property warning; retire in place every `HED70xx` id without a fact to report; collapse the shared-source
architecture record and supersede D4; rewrite the published documentation; ship the deprecation, the migration note
and the single golden re-ratification commit; reconcile after `v3.0.0` is tagged.

## Assumed state

With phases 1–3 merged and verified against source (commit `654959b5`) plus their additions:

- `src/Heddle.Generator/` (three csproj, `build/`, `Directory.Build.props`, sources), `src/Heddle.Generator.Tests/`
  and `src/Heddle.Generator.IntegrationTests/` are on disk and outside every `Heddle.sln` build configuration, CI leg
  and gate since phase 1; `samples/Heddle.Samples.slnx` references the generator project;
  `src/Heddle/Properties/AssemblyInfo.cs` grants `InternalsVisibleTo` to `Heddle.Generator.IntegrationTests`;
  `src/TestCorpus/TestCorpus.props` and `src/TestInventory/TestInventory.props` are imported by the generator suites'
  csproj files; `.gitattributes` pins `src/Heddle.Generator.Tests/Snapshots/**` and
  `src/Heddle.Generator.IntegrationTests/Fixtures/**`.
- `src/Heddle.Tests/PipelineContractTests.cs` reads `src/Heddle.Generator/build/Heddle.Generator.props`,
  `Heddle.Generator.targets`, `HeddleTemplateGenerator.cs` and `Pipeline/ConfigReader.cs` off disk, and hosts
  `FindRepoFile` used by `OldSchemaManifestFixture` and `VersionConsistencyTests`. `OldSchemaManifestRejectionTests`
  proves schema 1–2 rejection against a Roslyn-compiled facade. `PrecompiledRuntimeTests` renders hand-written
  strategy shapes through `PrecompiledRuntime.Bind`.
- `PrecompiledSchema` has `Min = Current = Max = CompiledFormSchemaVersion = 4` and `PrecompiledTemplates.Register`
  throws for a marker below 4 (phase 1). `PrecompiledTemplateInfo.IsPrecompiled` is `true` for every loader row and is read by
  `PrecompiledGauntlet.Validate`'s first step, `PrecompiledChildSupply` and `PrecompiledRuntime.ResolvePartialCore`.
- `Heddle.Build.props`/`.targets` do not read `HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces`,
  `HeddleObserveIntermediatePath`, `HeddleObserveImplementationPath` (phase 2).
- `docs/spec/common/testing-standards.md` § *Precompiled-tier posture* names `FallbackGuard` /
  `DifferentialHarness.RenderViaResolver` and `DifferentialHarness.ExpectDegrade(gen, key)`; its suite table lists
  the generator suites and records no Release leg for `Heddle.Tool.Tests`; `.claude/rules/testing.md` and
  `api-compatibility.md` repeat generator rules; `CLAUDE.md` names the suites and packages; `docs/building.md`
  § *Testing* and § *Packaging* name the generator suites and the `Heddle.Generator` package; `docs/csharp-api.md`
  § *Build-time pre-compilation* mentions `HeddleEmitUtf8Pieces`; `docs/architecture.md` does not mention the
  generator; `docs/precompilation.md` has the heading set listed in phase 2's assumed state and a dated footer.
- The registry in `docs/spec/common/cross-cutting-decisions.md` lists `HED7001`–`HED7016` (with `HED7014` live and
  narrowed and `HED7015` retired), `HED7017`–`HED7025`, `HED7026`–`HED7027` (unclaimed), `HED7028`, `HED7029` (unclaimed), `HED7030`–`HED7034`,
  `HED7035`–`HED7037`, `HED7101`–`HED7104`; `DiagnosticIdTests` asserts constants ⇄ registry ⇄ a published page and
  excludes rows marked `deliberately unclaimed`.
- The next-window candidate register's first row is the retired precompiled-partial API (`PrecompiledRuntime.ResolvePartial`
  ×3, `EvaluatePartialName`, `PrecompiledPartialName`).

## Requirements

### P4-R1 — The deletion set

**Decision.** Delete `src/Heddle.Generator/`, `src/Heddle.Generator.Tests/`, `src/Heddle.Generator.IntegrationTests/`,
their `Heddle.sln` project entries and the "Generator Roslyn Variants" folder, the generator reference in
`samples/Heddle.Samples.slnx`, the `InternalsVisibleTo` for `Heddle.Generator.IntegrationTests`, the two
`.gitattributes` pins, the `Heddle.Generator` mentions in `src/TestInventory/TestInventory.props` and
`src/TestCorpus/TestCorpus.props` comments and `.gitattributes`, and the projection corpus's generator consumers
(`DiagnosticProjectionCorpusGeneratorTests`, `EngineObservationCorpusTests` go with their suites;
`DiagnosticCorpusVectors` keeps its engine and LSP consumers). The last 2.x `Heddle.Generator` package is deprecated
on NuGet with the *legacy* reason and the alternate package `Heddle.Build` on the day `v3.0.0` publishes. After this
`git grep -i generator src/` matches only the ANTLR parser generator and retired-in-place annotations.

**Rationale.** [PD1](../../plan/precompilation-v2/decisions.md#pd1--generator-removal-and-cutover); window items 1
and 2.
**Alternatives rejected.** Keeping the generator as an unsupported package (two build tiers to document, one
cutover promised).

### P4-R2 — Every member that existed only for generated 2.x code is removed

**Decision.** From `src/Heddle.Tests/TestTemplate/public-api-heddle.txt`, the following leave the public surface;
the golden moves by exactly these lines plus phase 1–3's additions. The rule: a member whose only caller was
generated 2.x code, the 2.x generator, or a test of either.

Types removed whole: `Heddle.Precompiled.PrecompiledRuntime` (every member: `Bind`, `BindDefinition` ×3,
`BindExtension`, `BindOut`, `Init`, `InitDefinition`, `InitExtension`, `SiteFallback`, `EvaluatePartialName`,
`ResolvePartial` ×3, `WithLocalsFrame`, `MemberAccessor`, `NativeAccessor`, `DynamicMember`, `Prop`, `RootModel`,
`CarrierValue`, `GenerateString` ×2, `GenerateToWriter`, `GenerateUtf8`, `WritePiece`), `PrecompiledInitSite`,
`PrecompiledInitBody`, `PrecompiledInitFault`, `PrecompiledInitFaultScope`, `PrecompiledLateAccessor`,
`PrecompiledPropSetter`, `PrecompiledPropEvaluator`, `PrecompiledPartialName`, `PrecompiledFunctionSite`,
`PrecompiledFunctions`, `RuntimeOperators`, `PrecompiledCapabilities`, `PrecompiledLinePathForm`,
`PrecompiledCallShape`, `ObserveMode`, `IHeddleTemplateManifest`. This adopts the candidate register's
`ResolvePartial` row.

Members removed: `PrecompiledTemplateInfo` — all four public constructors, `Capabilities`, `LinePathForm`,
`InitSites`, `IsPrecompiled` (a row's presence in `Entries` is the fact; its three readers go with the code below);
`Strategy` becomes internal (`EntryPointType`, `RefusalSites` stay). `PrecompiledSchema` —
`AmbientModelTypeSchemaVersion`, `DynamicMemberRoutingSchemaVersion`, `LateBoundFunctionMaxArity`,
`LateBoundFunctionSchemaVersion`, `LinePathFormSchemaVersion`, `PerCarrierLocalsSchemaVersion`,
`PropLayoutFingerprintSchemaVersion`, `RegisteredNameSchemaVersion`, `EmitsDynamicMemberRouting`,
`EmitsLateBoundFunctions`, `EmitsPerCarrierLocals`. `HeddleBuildOptions` — `BuildPropertyPrefix`,
`DefaultEmitUtf8Pieces`, `DefaultNodeFallback`, `DefaultObserveMode`, `DefaultObserveImplementationPath`,
`DefaultObserveIntermediatePath`, `EmitUtf8PiecesProperty`, `NodeFallbackProperty`, `ObserveEngineProperty`,
`ObserveImplementationPathProperty`, `ObserveIntermediatePathProperty`.

Internal 2.x-only code removed with them: `Core/PrecompiledBodySupply.cs`, `Core/PrecompiledChildSupply.cs`,
`Precompiled/PrecompiledSiteFallbackExtension.cs`, `Precompiled/DefaultFunctionTable.cs` rows (the gauntlet keeps the
built-in target identity string), `AbstractExtension.BindPrecompiled` and its precompiled-body flag (the loader
materializes a real `RuntimeDocument`, so `InnerExist` needs no second source), `DefinitionBaseExtension.SetPrecompiledProps`,
`OutExtension.SetPrecompiledSlotMode`, the `ExtensionParameterCarrier` constructor taking setters, the
`!entry.IsPrecompiled` first step of `PrecompiledGauntlet.Validate`, and the `HeddleTemplate.Compile(CompileContext)`
child-supply probe. Kept: `HeddleCompiledTemplatesAttribute`, `ContentHash`, `TemplateKey`,
`PrecompiledExtensionBinding`, `PrecompiledFunctionBinding`, `PrecompiledImport`, `PrecompiledOptionsFingerprint`
(read by hosts through `Entries`; constructors harmless), `PrecompileUnsupportedAttribute`, `HeddleModelAssemblyAttribute`,
the whole `PrecompiledTemplates`/`PrecompiledValidationReport`/`PrecompiledFallbackEvent` surface, and the internal
`HeddleTemplate` adapter constructor.

**Rationale.** Window item 2: no seam to a compiler that does not exist; a smaller surface to document and to trim.
**Alternatives rejected.** Retiring the members in place (retirement in place is for ids, whose numbers must not be
reused; a public method with no possible caller is dead surface).

### P4-R3 — The schema window restarts at the compiled form

**Decision.** The schema constants are phase 1's (`Min = Max = Current = CompiledFormSchemaVersion = 4`) and the
marker rejection is phase 1's (`PrecompiledRegistrationException(assemblyName, schemaVersion)`); above `Max`, or an
incompatible engine version, stays `HED7102`. This phase adds the fixture: a stored artifact (`src/Heddle.Tests/TestTemplate/compiled-form-v4.bin`, produced by the
harness at this phase from a fixed corpus subset) is the additivity proof every later form change must still read.

**Rationale.** [PD4](../../plan/precompilation-v2/decisions.md#pd4--artifact-compatibility), window item 3, and
breaking-windows rule 7's fixture obligation.

### P4-R4 — Retire in place

**Decision.** Each id in the [Diagnostics](#diagnostics) table below keeps its `HeddleDiagnosticIds` constant,
`HeddleDiagnosticCatalog` row, registry row (annotated *retired in place — v3* with the mapping) and a row in
`docs/precompilation.md` § *Retired build ids*. `PrecompiledFallbackReason.UnsupportedFunction` narrows to the
late-bound arm; `ExtensionInitTypingMismatch` stays live as the loader's consumed-type check (phase 1).

**Rationale.** D1 and `.claude/rules/errors-diagnostics.md`: an id once shipped is never reused; deleting any of the
four homes frees the number.

### P4-R5 — Retired properties warn

**Decision.** `Heddle.Build.targets` gains one `<Warning Code="HED7037">` per set property among
`HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces` (any value), positioned at the project file;
`HeddleObserveIntermediatePath` and `HeddleObserveImplementationPath` stay silent.

**Rationale.** [PD3](../../plan/precompilation-v2/decisions.md#pd3--msbuild-surface) and window item 4.
**Alternatives rejected.** Erroring (none changes a rendered byte).

### P4-R6 — Spec records

**Decision.** `docs/spec/common/shared-source-architecture.md` is reduced to the sections that still govern —
*Diagnostic catalog and projection — single source* (engine and LSP), *`LineIndex` — the `\n`-only rule*, *Template
identity* — with a first paragraph stating that `Heddle.Language` is the shared front end of the engine and the
language services and that no production project links `src/Heddle` source (gated by `LinkedSourceTests`). The
removed content (match principle, fallback-legitimacy principle as a generator rule, hard constraints on shared source,
the generator names no extension, linked-`Compile` conventions, the shared surface, the binding seam) collapses into a
dated program record *Program record — precompilation v2 (closed)* in `cross-cutting-decisions.md` following the two
existing records' shape, carrying the decisions with ongoing force (the fallback-legitimacy taxonomy owned by the
gauntlet; same fact, same id; corpus set equality). D4 gets a dated *Superseded* note after its rationale: the runtime
pre-encodes static pieces (`RuntimeDocument.NormalStrategy`, `DocumentStrategy`) and nothing emits pieces as code;
`HeddleEmitUtf8Pieces` is retired. `.claude/rules/api-compatibility.md` drops its two generator bullets and the
`src/Heddle.Generator/**` path; `errors-diagnostics.md` drops the path; `testing.md` names `FallbackGuard.Expect` /
`CompiledFormHarness.ExpectRefusal` as the declared-fallback APIs; `CLAUDE.md` names the four suites and the
`Heddle.Build` package; `testing-standards.md` § *Precompiled-tier posture* and its suite table are reworded from
generator output to artifact and list `Heddle.Build.Tests` with its Debug and Release legs; `findings-register.md`
rows naming generator code (`C`, `F-140`, `F-198`, the observation open item) are closed with a one-line disposition.
This spec's row in `docs/spec/README.md` moves to *Implemented — reconcile at v3.0.0*.

**Rationale.** Window item 10; a record that describes deleted code instructs a reader into recreating it.

### P4-R7 — Published documentation

**Decision.** `docs/precompilation.md` is rewritten to this outline: *Setup* (`Heddle` + `Heddle.Build`, the .NET 10
runtime prerequisite, host ≠ target consequence); *Templates and metadata* (items, `Key`, `Name`, `ModelType`,
`Precompile`, `OutputProfile`); *Assemblies the build binds over* (attribute and items, implementation images); *Compile
options* (the six surviving properties, the LSP mirror, the retired-property warning); *Typed entry points*
(`DefaultOptions`, baked profile, thrown mismatch); *The registry* (lookup by key and name, fallback carriers, gauntlet
and policy, `ValidateAll`, the staleness identity, deploying files, which fallbacks are legitimate — with
`MemberBindingMismatch` as must-surface and `RefusalSites` as the declared-exception API); *Functions* (the bodiless
rule); *Custom extensions* (hooks run at load; `[PrecompileUnsupported]`); *What precompiles* (the three refusal
classes and the `HED7031` notice); *Generated sites and strict mode* (the `[ExportFunctions]`/build-visible-declaration remedy for strict hosts);
*Build-time diagnostics* (surviving ids,
forwarded engine ids, *Retired build ids*); *The CLI* (`render`, `compile`). `docs/csharp-api.md` § *Build-time
pre-compilation* names `Heddle.Build`, `DefaultOptions`, `BindTyped`, the two AppContext switches and drops
`HeddleEmitUtf8Pieces`; `docs/building.md` names four suites and the `Heddle.Build` package; `docs/architecture.md`
adds `src/Heddle.Build` and `src/Heddle.Tool` rows and one paragraph on the compiled form; `docs/editor-support.md`
keeps its row; `docs/custom-extensions.md` § *Precompiled mode* is reworded (hooks run at load). Every page touched
refreshes its verification footer; `DiagnosticIdTests`, `PublicApiDocMentionTests`, `DocumentationLinkTests` and
`WorkspaceOptionParityTests` gate the result.

**Rationale.** Documentation currency: a change that alters observable behaviour names and updates the documents
describing it in the same landing.

### P4-R8 — Release-tail deliverables

**Decision.** In order, one landing each: (1) P4-R1/P4-R2/P4-R3/P4-R5 with the test rewrites of the *Testing plan*;
(2) the golden re-ratification commit — the public-API golden, the intent table's owner column,
`samples/precompiled-app/golden/discovery.txt` (the `precompiled=` column goes with `IsPrecompiled`), deleted Verify
snapshots; **zero rendered-golden churn** (every `.html`/text golden, every sample render, the benchmark corpus);
(3) the records and docs of P4-R6/P4-R7; (4) `CHANGELOG.md` `## [3.0.0]` with the migration note as its *Changed
(breaking)* section: package swap (`Heddle.Generator` → `Heddle.Build`), rebuild every precompiled assembly (2.x
markers throw), retired properties (`HED7037`/silent), typed entries' options source, baked profile and thrown
mismatch, the twin → engine-id mapping (the table below), the bodiless rule and class (c) remedy, the .NET 10 runtime
prerequisite and host ≠ target (`MemberBindingMismatch`), `MemberBindingMismatch` as a new must-surface reason, every
removed public member (P4-R2 list). After `v3.0.0` is tagged: NuGet deprecation of the last 2.x `Heddle.Generator`,
then reconciliation of every window item against shipped source recorded in `cross-cutting-decisions.md` § *Release
records*, condensing the 2.1 window there at the same time if it has not been.

**Rationale.** Breaking-windows rules 3, 4, 5.

## Implementation plan

| # | Work item | Files | Done when |
| --- | --- | --- | --- |
| P4-W1 | Delete the generator | remove `src/Heddle.Generator*/`; edit `Heddle.sln`, `samples/Heddle.Samples.slnx`, `.gitattributes`, `src/Heddle/Properties/AssemblyInfo.cs`, `src/TestInventory/TestInventory.props`, `src/TestCorpus/TestCorpus.props` | solution builds; `git grep -i generator src/` clean per P4-R1 |
| P4-W2 | Remove 2.x-only members | edit/remove per P4-R2 under `src/Heddle/Precompiled/`, `src/Heddle/Core/`, `src/Heddle/Extensions/OutExtension.cs`, `src/Heddle/Core/ExtensionParameterCarrier.cs`, `src/Heddle/HeddleTemplate.cs`, `src/Heddle/Runtime/TemplateResolver.cs` (`entry.Strategy` internal access), `src/Heddle/Precompiled/PrecompiledGauntlet.cs` (first step removed) | public golden equals the enumerated diff |
| P4-W3 | Stored fixture | add `src/Heddle.Tests/TestTemplate/compiled-form-v4.bin`, `src/Heddle.Tests/CompiledFormFixtureTests.cs` | fixture reads and renders |
| P4-W4 | Retired-property warning | edit `src/Heddle.Build/build/Heddle.Build.targets`; add `src/Heddle.Build.Tests/RetiredPropertyTests.cs`; edit `src/Heddle/Data/HeddleDiagnosticIds.cs`, `HeddleDiagnosticCatalog.cs` (`HED7037`), `docs/precompilation.md` (row) | `HED7037` fires once per set property; silent for the two paths |
| P4-W5 | Test rewrites | replace `PipelineContractTests.cs` with `BuildSurfaceContractTests.cs` (reads `src/Heddle.Build/build/*` and `src/Heddle.Tool/Compile/ResponseFile.cs`; hosts `FindRepoFile`); delete `OldSchemaManifestRejectionTests.cs`, `OldSchemaManifestFixture.cs` (superseded by phase 1's `LegacyMarkerRejectionTests`), `PrecompiledRuntimeTests.cs`; edit `PrecompiledRegistryTests.cs`, `PrecompiledValidationPassTests.cs`, `RegisteredNameLookupTests.cs`, `PrecompiledGauntletTests.cs` (rows through the loader's internal constructor over an in-memory artifact); delete `BindDefinitionOverloadTests.cs`, `InitSynthesisFidelityTests.cs`, `DefaultFunctionLockstepTests.cs` (their subjects are removed members); rewrite `DynamicMemberTests.cs` against `DynamicParameter` (the binder-context fact survives), `PropLayoutFingerprintTests.cs` against `PropLayout.Fingerprint` through the gauntlet, `NativeOperatorRulesTests.cs` without its `RuntimeOperators` rows, `PrecompiledFallbackCarrierTests.cs` over the surviving reason set; edit `DiagnosticIdTests.cs` (retired rows: constant present, not reusable); `test-classes.txt` in every surviving suite | four suites green, Debug and Release, all TFMs |
| P4-W6 | Ids and reasons | edit `src/Heddle/Data/HeddleDiagnosticIds.cs` (doc comments), `src/Heddle/Data/HeddleDiagnosticCatalog.cs` (rows kept), `src/Heddle/Precompiled/PrecompiledFallbackReason.cs` (doc comments), `docs/spec/common/cross-cutting-decisions.md` (registry annotations), `docs/precompilation.md` (*Retired build ids*) | `DiagnosticIdTests` green |
| P4-W7 | Records | edit `docs/spec/common/shared-source-architecture.md`, `cross-cutting-decisions.md` (program record, D4 note), `testing-standards.md`, `findings-register.md`, `docs/spec/README.md`, `.claude/rules/{api-compatibility,errors-diagnostics,testing}.md`, `CLAUDE.md` | `DocumentationLinkTests` green |
| P4-W8 | Docs | edit `docs/precompilation.md`, `csharp-api.md`, `building.md`, `architecture.md`, `custom-extensions.md`, `editor-support.md`, `README.md`, `docs/README.md` | `npm run docs:build` and the four documentation gates green |
| P4-W9 | Release tail | edit `CHANGELOG.md`, `Directory.Build.props` (`VersionPrefix` 3.0.0); the golden commit; after tag: NuGet deprecation, `cross-cutting-decisions.md` § *Release records* | every window item recorded shipped or dispositioned |

## Public API contract

```csharp
namespace Heddle.Precompiled
{
    public sealed class PrecompiledTemplateInfo
    {
        // removed: the four public constructors, Capabilities, LinePathForm, InitSites, IsPrecompiled
        // Strategy: internal
        /// <summary>The typed wrapper class, resolved on the registering assembly.</summary>
        public System.Type EntryPointType { get; }
        public System.Collections.Generic.IReadOnlyList<PrecompiledRefusalSite> RefusalSites { get; }
    }
}
```

Removed surface: P4-R2. Everything else keeps its phase 1–3 shape. `public-api-heddle.txt` is regenerated in the
golden commit and reviewed against the P4-R2 list line by line.

## Diagnostics

| Id | Severity | Disposition | Engine id at build / what replaced it |
| --- | --- | --- | --- |
| `HED7037` | Warning | new (claimed in the registry with this spec) | `{property} is retired and ignored; remove it from the project.` One per set property, project file `(1,1)` |
| `HED7005` | — | retired in place | nothing — the form carries strings as UTF-16, so an unpaired surrogate round-trips |
| `HED7006` | — | retired in place | `HED0002` |
| `HED7007` | — | retired in place | the engine's unresolved-type error, id-less, reported under `HED7012` with the engine's sentence |
| `HED7008` | — | retired in place | `HED0001` |
| `HED7016` | — | retired in place | `HED3005` |
| `HED7017` | — | retired in place | `HED5007`, `HED5008`, `HED5009`, `HED5010`, `HED5015` |
| `HED7019` | — | retired in place | nothing — the host is the engine; the pairing fact is `HED7035` |
| `HED7022` | — | retired in place | `HED2001` |
| `HED7023` | — | retired in place | the engine's ambiguous-type error, id-less, under `HED7012` |
| `HED7024` | — | retired in place | `HED5019` |
| `HED7025` | — | retired in place | `HED1012`, `HED1013` |
| `HED7030` | — | retired in place | nothing — an unnameable member is a site rebuilt from data, listed in `HED7031` |
| `HED7034` | — | retired in place | nothing — no observation exists |
| `HED7015` | — | retired | unchanged |
| `HED7001`–`HED7004`, `HED7009`–`HED7014`, `HED7018`, `HED7020`, `HED7021`, `HED7028`, `HED7031` (Info), `HED7032`, `HED7033`, `HED7035`–`HED7037`, `HED7101`–`HED7104` | — | live | — |

The registry's `HED7xxx` block description becomes "Build host (`HED70xx`) and precompiled runtime (`HED71xx`)".
`HED9001` unchanged.

## Testing plan

**TDD verdict.** Test-first: retired ids present and unclaimable; the public golden diff; the stored fixture reading;
`HED7037` per property. Test-with: the test rewrites (they follow the deletions).

- `CompiledFormFixtureTests` — `compiled-form-v4.bin` registers and renders its templates byte-identically to the
  text compile of the same fixtures (the rule-7 additivity proof for every later change).
- `RetiredPropertyTests` (`Heddle.Build.Tests`) — each of the three properties set → one `HED7037` at the project
  file; the two observe paths set → no diagnostic; none set → no diagnostic.
- `DiagnosticIdTests` — every retired id has a constant, a catalog row, a registry row marked retired and a published
  mention; no retired id is claimed by a new fact.
- `PublicApiSurfaceTests` — golden equals the enumeration; the review diff is the P4-R2 list.
- Suites: `Heddle.Tests`, `Heddle.LanguageServices.Tests`, `Heddle.Tool.Tests`, `Heddle.Build.Tests`, each with
  `test-classes.txt` set equality; `dotnet.yml` runs each in Debug on both OSes and `lsp.yml` runs each in Release on
  Windows.
- Plan validation scenarios: a consumer on `Heddle.Generator` at v3 sees the deprecation on restore and rebuilds
  with `Heddle.Build` (`samples/precompiled-app` is that consumer); a consumer that never precompiled: the engine
  suite's non-precompiled tests are byte-identical; the LSP builds and passes with no generator in the graph.
- Gates: build, four suites, `src/Heddle.Language/generated/` unchanged, `gate` + `gate-precompiled` 8/8, docs build,
  the four documentation gates (`DiagnosticIdTests`, `WorkspaceOptionParityTests`, `PublicApiDocMentionTests`,
  `DocumentationLinkTests`).

## Back-compat and migration

Window items 1, 2, 4, 6 and 10 land here; items 3, 5, 7, 8, 9 landed in phases 1–2 and are recorded in the migration
note. Breaking by design and ratified: every removed member is listed; rendered bytes do not change on any tier
(golden commit expected at zero rendered churn). Rule 5 reconciliation is P4-W9's last step.

## Performance considerations

Render path untouched. No benchmark row changes; `gate`/`gate-precompiled` and the phase 3 tables are re-run once on
the release commit and cited by the CHANGELOG.

## Standards compliance

YAGNI in its purest form: the deletion set is everything without a caller. DRY: the fallback-legitimacy taxonomy
moves to its one remaining owner (`precompilation.md`'s gauntlet section). Retirement in place over deletion for ids,
deletion over retirement for members — the two rules the standards already state.

## Deferred items / non-goals

Removal of the retired `HED70xx` constants (never — retirement is permanent); the other candidate-register rows
(default encoder swap, `AllowCSharp` removal, `[NotEncode]` deletion, betterness in the overload binder, member
visibility widenings, dynamic-vs-typed asymmetry, extension-name collision at build, must-surface mismatches throwing
by default) stay in the register with their triggers.

## External references

- [NuGet package deprecation](https://learn.microsoft.com/en-us/nuget/nuget-org/deprecate-packages)
- [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/)
- [.NET library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
