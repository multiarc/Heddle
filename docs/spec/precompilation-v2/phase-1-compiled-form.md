# Phase 1 — Compiled form and loader

**Status:** Specified — ready for implementation. **Plan item:**
[phase-1-compiled-form.md](../../plan/precompilation-v2/phase-1-compiled-form.md). **Entry document:**
[README.md](README.md). **Contract:** [artifact-contract.md](artifact-contract.md). **Depends on:** nothing.

## Scope and goal

Give the engine a second front door. Besides compiling from text, `Heddle.dll` compiles from a compiled form: it
serializes a compiled `HeddleTemplate`'s post-compile graph into the artifact and later materializes the very objects
the text path builds — `RuntimeDocument`, `TemplateChain`, `TemplateItem`, the `IRuntimeParameter` family,
`DefinitionBaseExtension`, `PropsBinder` — running every extension's real `InitStart`/`CompleteInit` over the
deserialized body. This phase also lands the cutover of `PrecompiledTemplates.Register` to compiled-form markers,
deferred function binding as a text-path compile mode, the binding gate, the three refusal classes, the redefined
corpus intent table, and the parity harness. No build integration, no generated code, no file deletions.

## Assumed state

Verified against source on the `feature/benchmarks` branch (commit `654959b5`):

- The text path is `HeddleTemplate.Compile(CompileScope, string)` → `DocumentParser.Parse` →
  `HeddleCompiler.Compile(string, CompileScope, ParseContext, ExType)` → `compileScope.Compile()`
  (`ContextCompilation`, which drains `CompileContext.DelayedTemplates` calling `CompleteInit` and compiles the C#
  tier). `HeddleCompiler.CompileBody` runs shaping (`DocumentShaping.ShiftBySkippedTokens` → `TrimHiddenRemnantLines`
  → `RemoveDefinitions` → `ReplaceRawOutput` → `StripBranchSets`), compiles chains right-to-left through
  `CompileItem`, and returns `new RuntimeDocument(workingDocument, elements, compileScope)`
  (`src/Heddle/Runtime/HeddleCompiler.cs`).
- `RuntimeDocument` (`src/Heddle/Runtime/RuntimeDocument.cs`) slices pieces with `DocumentShaping.SlicePieces`
  into a private `DataProcessor { Processor, Piece, PieceUtf8 }` array and picks one of four private strategies
  (`DocumentStrategy`, `SingleStrategy`, `OptimizedStrategy`, `NormalStrategy`); `NormalStrategy`'s constructor
  eagerly UTF-8-encodes every piece and `DocumentStrategy` holds `_documentUtf8`. **Correction to
  [D4](../common/cross-cutting-decisions.md#d4--utf-8-static-piece-emission-u8-is-precompiled-only):** the runtime
  pre-encodes static pieces; phase 4 records the supersession.
- `IRuntimeParameter` implementations (`src/Heddle/Runtime/Parameters/`): `ChainedParameter`, `CompiledParameter`
  (`Func<object,object,object,object>` over model/chained/root), `PropsCompiledParameter`, `ConstantParameter`,
  `EmptyParameter`, `ModelParameter`, `RootModelParameter`, `DynamicParameter`, `RootDynamicParameter`,
  `PropsSlotParameter`. Accessors are built by `ModelParameter.GetPropertyChainAccessor` /
  `BuildNullSafePropertyChain` over `MemberPathResolver.TryResolve(ExType, string[])` (`Runtime/Expressions/`).
- `NativeExpressionCompiler.Compile(ExprNode, CompileScope, ParseContext, out ExType)` binds calls eagerly in
  `VisitCall` against `_registry` (`Options.Functions ?? FunctionRegistry.Default`, frozen in the constructor) and
  fails with `HED1001`/`HED1002`/`HED1012`/`HED1013`; `BindOverload`, `BuildCallArguments` and `FriendlyName` are
  internal statics. The text path has no deferral option.
- Prop conversions (`HeddleCompiler.BuildNumericConvert`) use `Convert.ChangeType`, not expression compilation;
  `PropsBinder(object[] frozenPrototype, DynamicSlot[] plan)` is the render-time binder.
- `AbstractExtension.InitStart` consults `PrecompiledBodySupply.TryConsume` (a one-shot supply, `src/Heddle/Core/`),
  reproducing the three `InitSubTemplate` post-states and recording the consumed data/chained types;
  `HeddleTemplate.Compile(CompileContext)` consults `PrecompiledChildSupply.TryConsume`. Both are entered only from
  `PrecompiledRuntime.Init`.
- `PrecompiledTemplates.Register(Assembly)` reads `HeddleCompiledTemplatesAttribute(Type manifestType, int
  schemaVersion, string engineVersion)`, gates on `PrecompiledSchema.IsSupported` (`Min = Max = Current = 3`) and
  `IsEngineCompatible`, then `Activator.CreateInstance(ManifestType)` as `IHeddleTemplateManifest.GetTemplates()`.
  `PrecompiledTemplateInfo` has four public constructors, all taking an eager `IProcessStrategy`; `IsPrecompiled =>
  Strategy != null`, read by `PrecompiledGauntlet.Validate`'s first step, `PrecompiledChildSupply` and
  `PrecompiledRuntime.ResolvePartialCore`. `PrecompiledGauntlet.Validate` order is marker → options → model type →
  extensions → init sites → functions → staleness.
- `HeddleTemplate`'s internal adapter constructor is `HeddleTemplate(IProcessStrategy, TextEncoder, RenderBudget,
  TemplateOptions, Type modelType)`; `TemplateResolver` constructs it in `ConsultPrecompiled` and in the hosted
  `Search` ladder.
- `ParseContext` (`src/Heddle/Language/ParseContext.cs`) exposes `Offset`, `InDefintionContext`,
  `DefinitionsBlock`, `DefenitionExists`, `GetDefenition`; `InitContext` carries `ParameterTemplate`,
  `CompileScope`, `ParseContext`.
- The corpus is 72 `.heddle` files under `src/Heddle.Tests/TestTemplate/`, declared in
  `src/TestCorpus/CorpusIntent.cs` with `CorpusTier { Precompiles, PrecompilesWithSiteFallback, DegradesToMarker,
  FallsBackSafely, FrontEndError }` and `CorpusRender { Standalone, WithModel, ResolveOnly }`.
- Type resolution over assemblies is `Native/AssemblyHelper` (`RegisterModelAssemblies`, observation with
  `IsObservable`) feeding `ReflectionHelper.ResolveType`; the plan's `ReflectionHelper.RegisterType` does not exist.
- `src/Heddle/Properties/AssemblyInfo.cs` grants `InternalsVisibleTo` to `Heddle.Tests`, `Heddle.LanguageServices`
  and `Heddle.Generator.IntegrationTests`.
- `Heddle.sln` builds `Heddle.Generator.csproj`, `Heddle.Generator.Roslyn411.csproj`, `Heddle.Generator.Roslyn530.csproj`,
  `Heddle.Generator.Tests`, `Heddle.Generator.IntegrationTests`; `.github/workflows/dotnet.yml` runs four generator
  legs; `benchmarks/dotnet/Heddle.Benchmarks.Dotnet.csproj` references `Heddle.Generator.csproj` as an analyzer and
  the `precompiled-html` satellite; `samples/precompiled-app` and `samples/codegen-t4-successor` reference it and run
  in `.github/workflows/samples.yml`.

## Requirements

### P1-R1 — The second front door is the compiler itself

**Decision.** `HeddleCompiler` gains an internal materialization entry that takes a decoded artifact, a template
row and a `CompileScope` and returns the root `RuntimeDocument`. It walks the row's `Documents`
([AC-7](artifact-contract.md#ac-7--documents)) and constructs, per element, the engine's own objects through the same
internal constructors the text path uses: `DocumentElement`, `TemplateChain.Add(TemplateItem)`, the
`IRuntimeParameter` for the recorded parameter kind, and `new RuntimeDocument(shapedText, elements, scope)` — so
strategy selection, `NeedsLocals` and UTF-8 pre-encoding are the text path's code. For each item it synthesizes the
`ParseContext` from the document's parse facts ([AC-7a](artifact-contract.md#ac-7a--parse-facts)), creates the
extension through `TemplateFactory.Create(name, position, parseContext, compileContext)` (registry and
`[ExtensionReplace]` semantics intact), derives the render type with `RenderTypeRules.Derive` over the live type,
calls `SetUpRenderType`, then runs the real `InitStart` and, through `CompileContext.DelayedTemplates`, the real
`CompleteInit`. The body compile a hook requests is served from the form: `CompileScope` carries a **form cursor**;
`AbstractExtension.InitSubTemplate` and `HeddleTemplate.Compile(CompileContext)` ask the cursor before parsing, and
the cursor answers with the item's recorded body (post-state 1, 2 or 3) or the named child's registry entry (bound
registry-first, then the engine's own dynamic compile under the request options). A hook that hands the cursor a
data or chained type differing from the recorded consumed types (AC-7) is a template-scope fault
`ExtensionInitTypingMismatch`, reported through the gauntlet before any render. `DefinitionBaseExtension.InitStart`
reads the materializing request's `MaxRecursionCount`, as on the text path.

**Rationale.** Hooks decide things the build must not predict (`ListExtension`'s element type, `OutExtension`'s slot
mode, `DefinitionBaseExtension`'s recursion limit); running them over supplied bodies is what makes parity
structural, and a body typed differently at load than at build would render members bound to the wrong type.

**Alternatives rejected.** Reconstructing hook outcomes from data (re-implements the extensions).

### P1-R2 — Serialization walks the compiled graph, never the text

**Decision.** The writer (internal, `src/Heddle/Precompiled/CompiledForm/`) is fed a compiled `HeddleTemplate` and
produces the template's rows for every section of [artifact-contract.md](artifact-contract.md). The compiler records,
during a text compile run with an internal `CompileContext.RecordForm` flag, the facts the runtime objects do not
expose: per item the parameter kind and its source (member path with resolved `MemberPathResolution.Properties`,
expression tree, constant value, C# source and namespaces), per body the raw and shaped text, post-state and consumed
types, per definition site the layout and prototype, per chain the item order, per document the parse facts.
Recording is off on every ordinary compile and allocates nothing then. The writer refuses (host error,
`InvalidOperationException`) a graph containing a fact it cannot encode; there is no partial artifact.

**Rationale.** Nothing may be re-derived from text; the form must be exactly what the engine decided.

**Alternatives rejected.** Reflecting over private fields of runtime objects (the delegates hold no data).

### P1-R3 — Registration reads compiled forms only; rows materialize lazily

**Decision.** `PrecompiledTemplates.Register` reads the marker. `SchemaVersion < PrecompiledSchema.CompiledFormSchemaVersion`
throws `PrecompiledRegistrationException(assemblyName, schemaVersion)` before `ManifestType` is touched. A supported
schema opens the artifact through `IHeddleCompiledArtifact.OpenArtifact()`, decodes `Header`, `Strings`, `Types`,
`Extensions`, `Functions`, `Members` and `Templates` into one `PrecompiledTemplateInfo` per row (loader-constructed;
`EntryPointType` resolved from the row's wrapper type name on the registering assembly; `RefusalSites` populated),
keeps the artifact bytes for materialization, and publishes the rows through the existing transactional snapshot
(duplicate key → `PrecompiledRegistrationException`, names → `HED7104`, idempotent per assembly). `Strategy`
materializes on first read and is memoized; a materialization fault (a hook reporting compile errors, a consumed-type
mismatch, an unresolvable type) is memoized and surfaces through the gauntlet as `ExtensionInitCompileError`,
`ExtensionInitTypingMismatch` or `MemberBindingMismatch`, never as an exception from `Strategy` on the registry path.
`IsPrecompiled` is `true` for every loader row. A schema above `MaxSupportedSchemaVersion` or an incompatible engine
version is `HED7102` (`SchemaVersionUnsupported` / `EngineVersionIncompatible`).

**Rationale.** [PD4](../../plan/precompilation-v2/decisions.md#pd4--artifact-compatibility): a 2.x manifest's IL
names members the engine does not carry, so it is refused before it can fault; lazy rows let `ValidateAll` run without
materializing and make startup proportional to templates rendered.

**Alternatives rejected.** Materializing every entry at registration (hooks run for templates never rendered); a
fallback event for a 2.x marker (a packaging defect the plan says throws).

### P1-R4 — The generator leaves the build

**Decision.** In this landing `Heddle.Generator.csproj`, `Heddle.Generator.Roslyn411.csproj`,
`Heddle.Generator.Roslyn530.csproj`, `Heddle.Generator.Tests` and `Heddle.Generator.IntegrationTests` are removed
from every `Heddle.sln` build configuration, their four legs leave `dotnet.yml`, and the merge gate names the three
remaining suites; `benchmarks/dotnet` drops the generator analyzer reference, the props/targets imports and the
`precompiled-html` satellite (the precompiled cells report no coverage until phase 2); `samples/precompiled-app` and
`samples/codegen-t4-successor` leave the `samples.yml` matrix until phase 2 rebuilds them on `Heddle.Build`. The
files stay on disk until phase 4 deletes them.

**Rationale.** With P1-R3 the generator's output cannot register, so nothing that depends on it can pass; taking it
out of the build keeps one buildable tree without a compatibility arm in the engine.

**Alternatives rejected.** Deleting the files here (the plan places deletion in the release tail).

### P1-R5 — The binding gate checks member paths and late-bound names

**Decision.** `PrecompiledGauntlet.Validate` gains a **bindings** step after extensions and before functions, run
per [AC-4](artifact-contract.md#ac-4--type-identity): the row's root model type is resolved by name; every `Members`
row is walked from its resolved start type through the live member graph and each hop compared by identity
([AC-5](artifact-contract.md#ac-5--member-identity)); any miss is `PrecompiledFallbackReason.MemberBindingMismatch`
(new, appended, per-template carrier, must-surface) with the pinned detail. Types needed only at materialization
(definition `:: T` types, site scope types) are resolved then, and a failure there is a memoized
`MemberBindingMismatch`. The functions step keeps its late-bound arm: a `Functions` row with no target whose name the
live registry lacks is `UnsupportedFunction` (`manifest=<late-bound> live=<unregistered>`), a name that is a
registered extension is `FunctionBindingMismatch` (`live=<extension>`). The first step (`!entry.IsPrecompiled`) is
never taken for a loader row. Order: options → model type → extensions → bindings → functions → staleness.

**Rationale.** Window item 9: a member renamed after the build or a function the deployment never registered must
surface from `ValidateAll` and the gauntlet before any render, independent of assembly load order.

**Alternatives rejected.** Reusing `ModelTypeMismatch` for type-ref failures (a different fact and remedy).

### P1-R6 — Deferred function binding is a text-path compile mode

**Decision.** `ExpressionOptions`/`CompileScope` gain an internal `DeferUnboundFunctions` flag. When set, both
arms that meet an unbound function name defer instead of failing: `NativeExpressionCompiler.VisitCall` with
`overloads.Count == 0` and a name that is neither a registered extension nor a definition (the `HED1002` predicate
unchanged), and `HeddleCompiler.CompileItem`'s definition → extension → registered-function fallback for a standalone
`@fn(args)` item whose name the registry lacks (the arm that adds `HED1001` and returns null). A deferred tree is
**not typed at build**: the compiler stops visiting it, records the syntax tree and its scope types as a late-bound
site ([AC-7](artifact-contract.md#ac-7--documents)) with one `Functions` row per unbound name (target absent), and
gives the site a **deferred** result type that `HeddleCompiler.CheckTypes` and the slot checks (`OutExtension`'s
`PropConversion.CanConvert`, prop-slot assignability) accept without a verdict. At load the loader compiles the tree
with `NativeExpressionCompiler` under the entry's scope types against the live
`TemplateOptions.Functions ?? FunctionRegistry.Default` and then runs the same `CheckTypes`/slot checks against the
bound overload's return type — the engine's own ranker, sentences and ids (`HED1001`, `HED1012`, `HED1013`,
`HED1008`, `HED0004`, `HED5014`…) — as a template-scope fault: `UnsupportedFunction` when the name is missing,
`ExtensionInitCompileError` otherwise, must-surface through the gauntlet before any render, never a render
exception. **The bodiless rule ([PD9](../../plan/precompilation-v2/decisions.md#pd9--calls-the-build-registry-cannot-bind)):**
deferral applies only where the site's result type shapes nothing — an output chain item's own parameter, a prop
argument whose slot type is declared, an argument inside such an expression. **Class (c):** every bodied or chained
consumer whose data parameter or chained parameter contains an unbindable call is a refusal site (the call, its
parameter and its body), because the hook or the consumer's `InitStart` would type from the call's result. Deferral is
off on every dynamic-tier compile.

**Rationale.** The registry at build cannot see a delegate registration; the call shape can, and the engine's ranker
at load is the dynamic tier's own selection.

**Alternatives rejected.** A build-time guess of the result type (picks an overload the engine never picks).

### P1-R7 — Embedded C# is data until phase 3

**Decision.** Under `ExpressionMode.FullCSharp` an embedded C# parameter is recorded as a `CSharp` site (source,
namespaces, scope type refs, position). At load the loader routes it through `CSharpContext.PushCompileExpression`
and `ContextCompilation.Compile(CompileScope)` exactly as the text path does, so `HeddleFeatures.CSharpTierEnabled`
gates it and `HED9001` degrades it. The build compiles the C# too (the engine's compile does) so C# errors are
build errors with the engine's ids at `.heddle` positions.

**Rationale.** [PD5](../../plan/precompilation-v2/decisions.md#pd5--embedded-c-before-generated-sites).
**Alternatives rejected.** Storing the compiled C# assembly bytes in the artifact (Roslyn on the read path).

### P1-R8 — Refusal classes and the intent table

**Decision.** A refusal is per site, return-shaped, and recorded as a *refusal site* (source text of the call
including its body, scope type refs, namespaces, position, class) and on the row's `RefusalSites`. At load a refusal
site is an internal `AbstractExtension` that compiles its text as its own document on first use under the ambient
request options, re-anchoring the fragment's errors onto the call's position. The three classes: **(a)** the bound
extension type declares `[PrecompileUnsupported]` (read off the live type at build); **(b)** a value the engine types
by reflection enumeration order (an `IEnumerable<T>` element type chosen among several implementations, an indexer
chosen by member order); **(c)** P1-R6's bodied or chained consumer over an unbindable call. Nothing else refuses: a
template the engine compiles is in the artifact, and a template the engine refuses is a build error.

`CorpusIntent.cs` is rewritten: `CorpusTier { Compiles, CompilesWithRefusal, EngineError }`,
`RefusalClass { UnsupportedExtension, ReflectionOrderValue, UnbindableCallTyping }`, and the row
`(name, tier, render, why, bom = false, mode = ExpressionMode.Native, refusals = none)`. Every row is classified by
what the engine does under the row's mode: rows that degraded only because the sweep lacked a model or the right mode
become `Compiles` with `WithModel` and the mode set; rows whose embedded C# the engine refuses under `Native` become
`EngineError`; `fn-unresolvable-marker.heddle` becomes `Compiles` (a late-bound value site);
`ext-site-fallback.heddle` becomes `CompilesWithRefusal` with `UnsupportedExtension`. The gate asserts, by set
equality, that each row's `RefusalSites` classes equal the intent row's `refusals`.

**Rationale.** The intent table is the sole definition of what does not precompile (plan success criterion 2), and a
site-sized refusal costs the site alone.

**Alternatives rejected.** Template-level refusal (a whole template leaving the tier for one site).

### P1-R9 — Parity harness

**Decision.** `src/Heddle.Tests/CompiledFormHarness.cs` (internal static): for a corpus entry, compile from text
under the row's options → writer → load into a fresh registry (`ResetForTests`) → materialize → render through
`HeddleTemplate` string, `TextWriter` and `IBufferWriter<byte>` sinks under `PrecompiledMismatchPolicy.Strict` with an
`OnFallback` sentinel (`FallbackGuard`, moved here from the generator integration suite: `Install`, `GuardedOptions`,
`Expect(key, reason)`, `Verify`; `ExpectRefusal(key, class)` declares a refusal) and byte-compare each sink against
the dynamic tier's render of the same text and model. Models for `WithModel` rows come from a `CorpusModels` table
in `Heddle.Tests`, set-equality gated against `CorpusIntent.NamesWithRender(WithModel)`. A second pass writes the
artifact to `TestOutput/` and loads it from bytes; a third stages each entry's real encoding on disk and runs the
gauntlet's staleness step.

**Rationale.** Testing standards' precompiled-tier posture: a test that does not pin the tier proves nothing about it,
and three sinks are the three code paths a piece can take.

### P1-R10 — Allocation equality

**Decision.** Phase 1 proves allocation equality inside `Heddle.Tests`: `CompiledFormAllocationTests` compiles each
corpus `Compiles` row from text, serializes, loads and materializes it, warms both tiers, then measures
`GC.GetAllocatedBytesForCurrentThread()` around a fixed number of renders on each of the three sinks and asserts the
loaded strategy's bytes per render equal the dynamic tier's. The BenchmarkDotNet `[MemoryDiagnoser]` row lands in
phase 2 with the build host: the existing `TechniquePrecompiledBenchmarks` class
(`benchmarks/dotnet/src/Bench/TechniqueBenchmarks.cs`) is retargeted to the compiled-form tier with its row names
unchanged, so the cross-stack report keys stay.

**Rationale.** Equal allocation is the measurable form of "the same objects render"; a delta names a place the loader
built something the text path does not.
**Alternatives rejected.** A benchmark row in phase 1 (the benchmark project reaches the engine through its public
surface only, and no host exists yet to build an artifact for it).

### P1-R11 — Late-bound sites re-bind per registry

**Decision.** A late-bound expression compiled at materialization is compiled against the registry the materializing
request carries; a later request under a different `TemplateOptions.Functions` instance triggers one re-compile,
memoized per registry instance (reference identity), matching the dynamic tier's per-options compile.

**Rationale.** The registry is part of the request on the dynamic tier; the compiled form keeps that contract.
**Alternatives rejected.** Binding once against `DefaultOptions` (a request-scoped registry would silently bind
another host's functions); refusing a second registry (the dynamic tier accepts one).

## Implementation plan

| # | Work item | Files | Shape | Done when |
| --- | --- | --- | --- | --- |
| P1-W1 | Contract types and writer/reader | add `src/Heddle/Precompiled/CompiledForm/{CompiledFormWriter,CompiledFormReader,TypeIdentityTable,SectionIds}.cs`; add `src/Heddle/Precompiled/IHeddleCompiledArtifact.cs`, `PrecompiledRefusalSite.cs`, `PrecompiledRefusalClass.cs`; edit `src/Heddle/Precompiled/PrecompiledSchema.cs` (`CompiledFormSchemaVersion = 4`; `Min = Current = Max = 4`) | every AC section encoded and decoded; AC-9 determinism | round-trip tests green on all TFMs |
| P1-W2 | Compile-time recording | edit `src/Heddle/Runtime/CompileContext.cs` (internal `RecordForm`, form record), `src/Heddle/Runtime/HeddleCompiler.cs` (record at `CompileItem`, `BindProps`, `CompileFromDefenition`, body compile, parse facts), `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs` (tree + scope types to the record), `src/Heddle/Core/AbstractExtension.cs` (consumed types to the record) | recording off by default; zero allocation when off | a compiled corpus entry yields a complete form |
| P1-W3 | Loader | add `src/Heddle/Runtime/HeddleCompiler.Form.cs`, `src/Heddle/Runtime/FormCursor.cs`, `src/Heddle/Precompiled/CompiledForm/RefusalSiteExtension.cs`; edit `src/Heddle/Runtime/CompileScope.cs` (cursor slot), `src/Heddle/Core/AbstractExtension.cs` (`InitSubTemplate` asks the cursor), `src/Heddle/HeddleTemplate.cs` (`Compile(CompileContext)` asks the cursor for a named child) | hooks run unchanged over supplied bodies; consumed-type check | corpus `Compiles` rows render byte-identical on three sinks |
| P1-W4 | Registration and rows | edit `src/Heddle/Precompiled/PrecompiledTemplates.cs` (marker gate, artifact decode), `src/Heddle/Precompiled/PrecompiledRegistrationException.cs` (assembly/schema constructor), `src/Heddle/Precompiled/PrecompiledTemplateInfo.cs` (internal loader constructor, lazy `Strategy`, memoized fault, `RefusalSites`, `IsPrecompiled`); `src/Heddle/Runtime/TemplateResolver.cs` unchanged | rows constructed without a strategy | registry tests green; `Entries` reports rows before any render |
| P1-W5 | Binding gate | edit `src/Heddle/Precompiled/PrecompiledGauntlet.cs` (bindings step; resolve-versus-compare order; type-ref comparison), `src/Heddle/Precompiled/PrecompiledFallbackReason.cs` (`MemberBindingMismatch`), `src/Heddle/Precompiled/PrecompiledFallbackEvent.cs` (classify per-template) | detail strings pinned | `PrecompiledGauntletTests` + `PrecompiledFallbackCarrierTests` green |
| P1-W6 | Deferred binding | edit `src/Heddle/Runtime/ExpressionOptions.cs`, `src/Heddle/Runtime/Expressions/NativeExpressionCompiler.cs` (`VisitCall` deferral arm; visiting stops), `src/Heddle/Runtime/HeddleCompiler.cs` (`CompileItem` fallback deferral arm; deferred typing through `CheckTypes` and slot checks; class (c) rule); add corpus fixtures `src/Heddle.Tests/TestTemplate/fn-standalone-late-bound.heddle`, `src/Heddle.Tests/TestTemplate/fn-typed-consumer-late-bound.heddle` | deferral only under the flag | `@(toUpper(model.Name))` and standalone `@toUpper(model.Name)` round-trip and bind at load; `@list toItems(model.Raw)` is class (c) |
| P1-W7 | Corpus | edit `src/TestCorpus/CorpusIntent.cs`; add `src/Heddle.Tests/CorpusModels.cs` | rows re-classified with `Why` | `CorpusIntentGateTests` set equality green |
| P1-W8 | Generator leaves the build | edit `Heddle.sln` (build configurations), `.github/workflows/dotnet.yml`, `.github/workflows/samples.yml`, `benchmarks/dotnet/Heddle.Benchmarks.Dotnet.csproj`, `benchmarks/dotnet/src/Engines/PrecompiledBackend.cs` (one assembly, keyed on presence), `CLAUDE.md` (gate names three suites) | no generator project or suite in the build or gate | build and three suites green |
| P1-W9 | Harness and allocation test | add `src/Heddle.Tests/CompiledFormHarness.cs`, `CompiledFormParityTests.cs`, `CompiledFormRoundTripTests.cs`, `CompiledFormBindingGateTests.cs`, `DeferredFunctionBindingTests.cs`, `LegacyMarkerRejectionTests.cs`, `LinkedSourceTests.cs`, `CompiledFormAllocationTests.cs`, `FallbackGuard.cs`; edit `src/Heddle.Tests/test-classes.txt` | three sinks, file-backed pass | exit criteria 1–5 green in one run |

## Public API contract

```csharp
namespace Heddle.Precompiled
{
    /// <summary>Implemented by the generated artifact class a HeddleCompiledTemplatesAttribute names. Opens the
    /// embedded compiled form; the loader owns and disposes the stream. Stateless; thread-safe.</summary>
    public interface IHeddleCompiledArtifact { System.IO.Stream OpenArtifact(); }

    public static class PrecompiledSchema
    {
        /// <summary>The first schema of the compiled form; a marker below it is refused by Register.</summary>
        public const int CompiledFormSchemaVersion = 4;
        public const int MinSupportedSchemaVersion = 4;
        public const int MaxSupportedSchemaVersion = 4;
        public const int CurrentSchemaVersion = 4;
    }

    public enum PrecompiledFallbackReason
    {
        // existing members unchanged; ExtensionInitTypingMismatch stays live (P1-R1)
        /// <summary>A type or member the compiled form binds does not resolve to the same identity in this
        /// process. Must surface; per-template carrier; reported through HED7101.</summary>
        MemberBindingMismatch
    }

    public class PrecompiledRegistrationException : System.Exception
    {
        /// <summary>A marker below CompiledFormSchemaVersion was registered. Key and ExistingAssemblyName are null;
        /// NewAssemblyName is the rejected assembly. Message: "Assembly '{name}' carries a Heddle 2.x precompiled
        /// manifest (schema {n}); this engine reads compiled-form artifacts only. Rebuild the assembly with the
        /// Heddle.Build package."</summary>
        public PrecompiledRegistrationException(string assemblyName, int schemaVersion);
        /// <summary>The rejected marker's schema; 0 for the duplicate-key constructor.</summary>
        public int SchemaVersion { get; }
    }

    /// <summary>The refusal class of one site the build could not precompile.</summary>
    public enum PrecompiledRefusalClass { UnsupportedExtension, ReflectionOrderValue, UnbindableCallTyping }

    /// <summary>One refusal site: rebuilt at load from its own source text; a declared exception under strict mode.</summary>
    public readonly struct PrecompiledRefusalSite
    {
        public int SiteOrdinal { get; }
        public PrecompiledRefusalClass Class { get; }
        /// <summary>The build's sentence: the extension author's [PrecompileUnsupported] reason, the enumeration-order
        /// fact, or the unbindable function name.</summary>
        public string Detail { get; }
        public int PositionStart { get; }
        public int PositionLength { get; }
    }

    public sealed class PrecompiledTemplateInfo
    {
        // no public constructor is added; rows are loader-constructed
        /// <summary>The template's refusal sites, in site-ordinal order; empty for a fully precompiled template.
        /// Available without materializing.</summary>
        public System.Collections.Generic.IReadOnlyList<PrecompiledRefusalSite> RefusalSites { get; }
        /// <summary>The typed wrapper class, resolved on the registering assembly; null when the artifact row names
        /// none.</summary>
        public System.Type EntryPointType { get; }
        /// <summary>True for every loader row. Reading it never materializes.</summary>
        public bool IsPrecompiled { get; }
        /// <summary>Materializes on first read and is memoized (thread-safe). Null when materialization faulted; the
        /// fault is memoized and reported by the gauntlet. Internal from phase 4.</summary>
        public Heddle.Runtime.IProcessStrategy Strategy { get; }
    }
}
```

## Diagnostics

No new `HED` id. `HED7101` gains detail forms `Member '<path>': manifest=<identity> live=<identity|unresolved>` and
`Type '<identity>': manifest=<assembly> live=<unresolved>` under `MemberBindingMismatch`, and `Body '<extension>' at
<position>: recorded=<data>/<chained> live=<data>/<chained>` under `ExtensionInitTypingMismatch`. `HED7102` fires for
an artifact above `MaxSupportedSchemaVersion` or from a newer or other-major engine. A 2.x marker throws
`PrecompiledRegistrationException` (host fault, no id). Load-time engine errors (a late-bound tree at load, a hook's
compile errors, a refusal site's compile) carry the engine's own ids and positions inside the fallback detail and
inside `TemplateCompileException` on the typed path.

## Testing plan

**TDD verdict.** Test-first: round-trip determinism, the marker rejection, the binding-gate reasons and details,
deferral (value site binds at load; bodied use is class (c)), the intent-table set equality, three-sink parity.
Test-with: the writer's per-section encoders (driven by the round-trip tests).

- `CompiledFormRoundTripTests` — serialize twice → identical bytes; serialize → load → serialize → identical; a
  corrupted section table → `PrecompiledRegistrationException`; a `framework` type resolves on every TFM; an
  artifact with `SchemaVersion` = 5, and one whose `EngineVersion` is the next major, each register nothing and raise
  `HED7102`.
- `LegacyMarkerRejectionTests` — a Roslyn-compiled assembly carrying `HeddleCompiledTemplates(typeof(M), 3, "2.1.0")`
  → `Register` throws `PrecompiledRegistrationException` with `SchemaVersion == 3`, the assembly name and
  `Heddle.Build` in the message; `Entries` unchanged; the near-neighbour at schema 4 registers.
- `CompiledFormParityTests` — every corpus row per P1-R9, all three passes; `EngineError` rows assert the engine's
  ids and positions from the text compile; `CompilesWithRefusal` rows assert `RefusalSites` and that the refusal site
  renders byte-identically after first use.
- `CompiledFormBindingGateTests` — a model property renamed between build and load (two compiled model assemblies)
  → `ValidateAll` reports `MemberBindingMismatch`, the request degrades and the dynamic tier raises `HED0001` at the
  `.heddle` position; a member type changed `List<A>` → `List<B>`, and one changed `int?` → `long?`, each report
  `MemberBindingMismatch` (type-ref comparison); a function absent from the live registry → `UnsupportedFunction`; a
  body scope type declared in an assembly not yet loaded validates clean (the gate compares, it does not resolve); the
  near-neighbour that binds passes.
- `DeferredFunctionBindingTests` — the plan's validation scenarios; standalone `@toUpper(model.Name)` is a late-bound
  site; a deferred tree with an operator the engine rejects raises the engine's id at load and at build the tree
  reports nothing; `@date(fn(x))` and `@out(fn(x))` compile at build with deferred typing and, at load, render when
  the bound overload's return type satisfies `[DataType]`/the slot and report `ExtensionInitCompileError` carrying
  `HED0004`/`HED5014` when it does not; `@item.Name` misspelled inside a class (c) body raises the same engine error
  on both tiers once the function is registered.
- Hook-typed body — an extension whose `InitStart` hands the body a different data type than recorded →
  `ExtensionInitTypingMismatch` through `TryResolve` before render, and the dynamic tier serves the request.
- `HeddleTemplate` loads touch no `Microsoft.CodeAnalysis` type unless a `CSharp` site exists (assembly-load probe,
  net8/net10), and `HED9001` under `Heddle.CSharpTierEnabled=false`.
- Definition library scenario (layering, props, slots, regions, `@partial`) — hooks' three post-states equal the
  text path's (asserted through `InnerExist`-sensitive built-ins: `@raw(){{mid}}`); `HED1001` versus `HED1002`
  selection inside a definition context matches the text path (parse facts).
- `LinkedSourceTests` — no `.csproj`, `.props` or `.targets` (including every `Directory.Build.props`) under `src/`,
  `samples/` or `benchmarks/`, other than `src/Heddle/Heddle.csproj`, contains a `Compile Include` reaching into
  `src/Heddle/` (plan success criterion 6; the one such link is `src/Heddle.Generator/Heddle.Generator.Common.props`,
  which leaves with phase 4).
- Concurrency: parallel first renders of one entry materialize once (`RegistrationConcurrencyTests` extended).
- `CompiledFormAllocationTests` — bytes per render equal on three sinks for every `Compiles` row (P1-R10).
- Gates: `Heddle.Tests` `test-classes.txt` updated; corpus set equality; `.gitattributes` `eol=lf` covers
  `src/Heddle.Tests/TestOutput/**`.

## Back-compat and migration

Window items 3 (2.x markers throw), 7 (bodiless rule) and 9 (binding checks) land here and are recorded in the
migration note by phase 4. Additive at the API level otherwise: one interface, one enum, one struct, four
`PrecompiledTemplateInfo` members' semantics stated, one exception constructor, two constants, one fallback reason
appended. No rendered byte changes.

## Performance considerations

Render path untouched: the loader produces the text path's objects. Materialization is compile-time work, once per
entry, memoized. Guarded by `CompiledFormAllocationTests` here and, from phase 2, by `TechniquePrecompiledBenchmarks`
(allocation equality, mean within error) and `gate-precompiled`.

## Standards compliance

DRY consolidates knowledge: member resolution, overload ranking, body post-states and parse-context behaviour exist
once and the loader calls them. YAGNI cuts a v3 registry type, a per-field artifact schema language, and load-time
re-typing of bodies. Abstraction is added at one seam — the form cursor on `CompileScope` — because it has two
consumers (body compiles and child compiles). Correctness over performance: `Strategy` materializes once, serialized,
rather than racing.

## Deferred items / non-goals

Generated sites (phase 3); build integration (phase 2); load-time body re-typing for class (c) (trigger: byte parity
proved over the corpus); a position map for typed wrappers (trigger: a debugger scenario the artifact's positions do
not serve).

## External references

- [`System.Linq.Expressions` `LambdaExpression.Compile`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.lambdaexpression.compile)
- [LEB128](https://en.wikipedia.org/wiki/LEB128)
- [SHA-256 in .NET (`SHA256.Create`)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.sha256)
- [`Assembly.GetManifestResourceStream`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly.getmanifestresourcestream)
