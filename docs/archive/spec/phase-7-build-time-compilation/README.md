# Phase 7 specification — build-time template pre-compilation

Status: **Specified — ready for implementation.**
Roadmap source: [phase 7 — build-time template pre-compilation](../../roadmap/phase-7-build-time-compilation.md).
Prior phases assumed merged
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
[phase 1 — native expressions](../phase-1-native-expressions/README.md) (the public
`ExprNode` AST the emitter maps 1:1 to C#, `FunctionRegistry`, `ExpressionMode` with the
`AllowCSharp` bridge, the `HEDxxxx` plumbing),
[phase 2 — safe output](../phase-2-safe-output/README.md) (`OutputProfile` on options
identity and the profile-suffixed resolver cache key),
[phase 3 — branching](../phase-3-branching/README.md) (`ScopeLocals`, the publish/read
channel, the branch extensions generated code binds),
[phase 4 — ergonomics](../phase-4-ergonomics/README.md) (`TrimDirectiveLines` on options
identity and the trim-suffixed cache key),
[phase 5 — props & slots](../phase-5-props-and-slots/README.md) (whose D7 kept props
carriage behind `PropsBinder`/`Scope.WithProps` for this phase's swap), and
[phase 6 — tooling / LSP](../phase-6-tooling-lsp/README.md) (the `PublicApiSurfaceTests`
gate, and its D24 declarative function-export surface — the assembly-level
`[ExportFunctions]` attribute plus `FunctionRegistry.RegisterFrom(Assembly)` — which is
the discovery contract this phase's D21 binds against). This phase claims the `HED7xxx` block per
[cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx),
owns template identity per
[cross-cutting D8](../common/cross-cutting-decisions.md#d8--template-identity--naming-policy-is-owned-by-phase-7),
and delivers the `"…"u8` static-piece emission per
[cross-cutting D4](../common/cross-cutting-decisions.md#d4--utf-8-static-piece-emission-u8-belongs-to-phase-7).
No grammar change — a second backend over the shared front end.

### Documents

| Document | Purpose |
| --- | --- |
| README.md (this file) | The spec proper: assumed state, decision records, implementation plan, public API contract, diagnostics, testing, back-compat, performance. |
| [generated-code.md](generated-code.md) | The generated-code shape reference: emission conventions, the piece table and `"…"u8` tier, `#line` mapping, and seven worked template → C# examples (member paths, native expressions, embedded C#, `:: dynamic`, props definitions, branch sets, function calls). |
| [identity-and-metadata.md](identity-and-metadata.md) | The template identity policy owned by this phase: the pinned key normalization algorithm, the manifest/attribute metadata format, the options fingerprint and validation gauntlet, and the mismatch policy semantics. |

## Scope and goal

Pre-compile `.heddle` templates at build time into the consuming assembly plus
discovery metadata — the Razor compiled-views shape — so a precompiled template is
**looked up, not parsed and compiled** at runtime: no ANTLR parse, no expression trees,
no Roslyn at startup, and `Microsoft.CodeAnalysis` never loads when every rendered
template is precompiled. Templates are validated by the build (a broken template fails
compilation at the `.heddle` position) and become debuggable (real generated C#, real
stack traces). The runtime stays **fully dynamic**: no AOT/trimming constraints on
`Heddle`/`Heddle.Language`; precompiled and runtime-compiled templates coexist in one
process (mixed mode is first-class).

In scope: the `Heddle.Generator` NuGet (a Roslyn incremental source generator), the
front-end sharing seam it requires, the emitter (structure → C#), the two-layer
discovery metadata, the runtime registry with its validation gauntlet and
`PrecompiledMismatchPolicy`, typed entry points, the consolidated template identity
policy (the two key-policy questions closed as D1 and D2), the `"…"u8` piece emission
shaped for phase 8, a minimal `Heddle.Tool` CLI proving the T4-successor scenario, and
the differential harness that gates both backends permanently. Milestone 1 = syntactic
generation (the project's own compilation binds member paths); milestone 2 — committed,
not contingent — adds `ISymbol` binding for native template diagnostics (D3).

## Assumed state

Seams re-verified against source (July 2026); prior-phase additions from their specs.

| Seam | Verified state |
| --- | --- |
| [TemplateResolver](../../../src/Heddle/Runtime/TemplateResolver.cs) | Caches `HeddleTemplate` in a `Dictionary<string, HeddleTemplate>(StringComparer.OrdinalIgnoreCase)`. Keys are `Path.Combine(_rootPath, viewName)` plus, after phases 2/4, the suffix `+ "|" + profile` (phase 2 D8) `+ "|trim"` when trimming (phase 4 D10) via the private `CacheKey` helper. Search-location templates hard-code backslashes (`@"\views\{1}\{0}"`), and `Search` normalizes with `viewName.Replace("~/", "/").Replace('/', '\\')` — **current key behavior is OS-dependent and Windows-shaped**, confirming the roadmap's suspicion (D1 records the correction and the pinned rule). A second latent quirk recorded for accuracy: the hosted-view location strings are drive-rooted on Windows, so `Path.Combine(_rootPath, @"\views\…")` *discards* `_rootPath` (a rooted second argument wins) — the precompiled key derivation (D1) never routes through these location strings. |
| [FileReader](../../../src/Heddle/FileReader.cs) | `GetFileName()` = `Path.Combine(options.RootPath, TemplateName + FileNamePostfix)`; throws `ArgumentException` on unreadable files. This is the anchor semantics the key policy's "resolver-relative" is defined against. |
| [HeddleTemplate](../../../src/Heddle/HeddleTemplate.cs) | `Compile(CompileContext)` = `FileReader` read → `DocumentParser.Parse` → `HeddleCompiler.Compile` → `CompileScope.Compile()` (Roslyn pass) → stores `_runtimeDocument`/`_processStrategy`. `Generate(data, chained, caller)` builds a `ScopeRenderer` (size-adaptive high-water mark), a root `Scope` via the **internal** ctor, calls `_processStrategy.Render(scope)`. `Compiled => _runtimeDocument != null`. `Configure(Assembly)` forwards to `AssemblyHelper.Configure`. |
| [AssemblyHelper.Configure](../../../src/Heddle/Native/AssemblyHelper.cs) | **One-shot**: a private `_configured` flag makes every call after the first a no-op. The roadmap's "the existing registration pattern … the same scan collects the manifest" is therefore imprecise — precompiled-manifest registration needs its own repeatable API (D7 records the correction). Extension discovery itself lives in [TemplateFactory](../../../src/Heddle/Runtime/TemplateFactory.cs)'s static ctor scanning `AssemblyHelper.GetAssemblies()` for `ExportExtensionsAttribute`, with `TemplateOverrideException` on non-replacing name collisions — the posture D2 mirrors. |
| [RuntimeDocument](../../../src/Heddle/Runtime/RuntimeDocument.cs) / [IProcessStrategy](../../../src/Heddle/Runtime/IProcessStrategy.cs) | Both **internal**. `GetDocumentPieces` slices static text around processor positions in document order — the piece order the emitter mirrors. `IProcessStrategy { string Execute(in Scope); void Render(in Scope); }` is the exact contract generated bodies implement; D5 promotes it to public. |
| [AbstractExtension](../../../src/Heddle/Core/AbstractExtension.cs) / [TemplateItem](../../../src/Heddle/Runtime/TemplateItem.cs) | The render protocol generated code must reproduce: `TemplateItem.RenderData` evaluates the parameter delegate, swaps the model (`scope.Model(value)` — pre-swap model becomes `ParentModelData`), and calls `Extension.RenderData`. An extension's body lives in **private** fields (`_processStrategy`/`_innerResult`) settable only through `InitStart`, which compiles via `HeddleCompiler` — hence the `PrecompiledRuntime.Bind` seam (D5). |
| [PartialExtension](../../../src/Heddle/Extensions/PartialExtension.cs) | **Stale-claim correction:** partials do *not* route through the resolver at render time today. `InitStart` schedules a delayed compile (`CompileContext.AddDelayedCompileTemplate`), `CompleteInit` compiles an inner `HeddleTemplate` from disk via `FileReader` at **compile time**; render just calls `InnerTemplate.Generate`. The render-time registry consult the roadmap describes is *new* behavior introduced by this phase for both backends' partial resolution (D7). |
| [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) / [HeddleMainListener](../../../src/Heddle/Language/HeddleMainListener.cs) | The front end's `CompileContext` surface is narrow and enumerable: `Options.ProvideLanguageFeatures` (prediction mode), `CompileErrors` (error copy), and — **stale-claim correction** to "pure text → structure, no file IO": the listener reads `@<<` import files directly (`File.OpenText(Path.Combine(_compileContext.Options.RootPath, path))`, `HeddleMainListener.cs`, the import handler). D4 introduces `ParserSettings` + `ImportReader` to close both couplings. |
| [Heddle.csproj](../../../src/Heddle/Heddle.csproj) / [Heddle.Language.csproj](../../../src/Heddle.Language/Heddle.Language.csproj) | Confirmed: `Heddle`'s netstandard2.0 build references `Microsoft.CodeAnalysis.CSharp` 4.1.0 + `Microsoft.Extensions.*`; `Heddle.Language` targets netstandard2.0 with `Antlr4.Runtime.Standard` 4.13.1 as its only dependency. Both strong-named (`heddle.snk`). Package version is currently `1.0.0$(VersionSuffix)` — the roadmap's `engineVersion: "2.3.0"` sketch was illustrative only. |
| Prior-phase state (from merged specs) | `TemplateOptions` carries `OutputProfile`, `ExpressionMode`, `Functions`, `TrimDirectiveLines`, all in the copy ctor with the reflection completeness test; `HeddleCompileError.DiagnosticId` + `HeddleDiagnosticIds` exist (phase 1); `Scope` is a declared `readonly struct` with `ScopeLocals` and `PropsData` (phases 3/5); branch extensions coordinate via `BranchState`/`"heddle.branch"`; `PublicApiSurfaceTests` pins the `Heddle`/`Heddle.Language` public surfaces against goldens (phase 6). |
| Function machinery (phase 1 D12/D13, phase 4 D2, phase 6 D24) | `FunctionRegistry.Default` binds the **eighteen** default names (seventeen + phase 4's `range`) by explicit `MethodInfo` to `internal static class BuiltInFunctions` (`src/Heddle/Runtime/Expressions/BuiltInFunctions.cs`). Phase 6 D24 added the declarative export surface: `Heddle.Attributes.ExportFunctionsAttribute` (assembly-level, `AllowMultiple`, `(Type container)`/`(params Type[] containers)` ctors — no parameterless "all" form; container = public static class; every public static method = one function; name = `MethodInfo.Name.ToLowerInvariant()`; phase 1 D12 eligibility/replace/overload rules) and the runtime-parity API `FunctionRegistry.RegisterFrom(Assembly)`, which registers every export through the exact `Register(string, MethodInfo)` path in the pinned order (attribute declaration order, then ctor type-array order). Verified consequences this phase must design around (gap G1): `BuiltInFunctions` is internal to `Heddle` with **no IVT to consumer assemblies**, so generated code in the *user's* assembly has no legal call target for the default functions; the generator neither references `Heddle.csproj` (D4/D12 packaging gate) nor shared-sources `BuiltInFunctions`, so it cannot enumerate the default signatures on its own; unknown-function rejection (`HED1001`) is emitted by the **back end** (`HeddleCompiler.CompileItem` / `NativeExpressionCompiler`, phase 1 D11) — the shared parse front end never sees function names, so no "forwarded front-end error" for them can exist; and `[ExportFunctions]` containers in *referenced* assemblies are public types whose attribute metadata and method signatures are fully visible to the generator as `ISymbol`s. D21 closes all of it. |
| [DefinitionBaseExtension](../../../src/Heddle/Core/DefinitionBaseExtension.cs) | The definition carrier's recursion guard: `private int _maxRecursionCount` is assigned **only** in `InitStart` (`initContext.CompileScope.Options.MaxRecursionCount`); both `ProcessData` and `RenderData` gate on `_recursionCount.Value >= _maxRecursionCount` (a `ThreadLocal<int>` counter). `PrecompiledRuntime.BindDefinition` bypasses `InitStart` by design (D5), so without an explicit limit the field stays `0` and the `>=` guard throws `TemplateProcessingException("Recursion hit it's maximum")` on the **first** invocation of every precompiled definition (gap G3). `TemplateOptions.MaxRecursionCount` defaults to `100` in both constructors ([TemplateOptions.cs](../../../src/Heddle/Data/TemplateOptions.cs)) and participates in neither `Equals`/`GetHashCode` nor the resolver cache key. D23 closes this. |
| Back-end document-shaping machines | Re-verified for D22 (gap G2): the strip/orphan machines (`ProcessBranchSets`, phase 3 D10), trim widening + remnant-line trimming (`WidenToWholeLine`/`TrimHiddenRemnantLines`, phase 4 D6–D8), zero-output-chain and definition-span removal (`RemoveEmptyItem`/`RemoveDefinitions`), the expression-mode gates (`HED1014` and the ID-less `"C# Code Not allowed here, see TemplateOptions.AllowCSharp Property"` error at [HeddleCompiler.cs](../../../src/Heddle/Runtime/HeddleCompiler.cs) `CompileItem`), `@profile`'s document-order flip (phase 2 D4, `CompileContext.OutputProfile`), and prop-layout resolution/shadowing (phase 5 D9) are **all private back-end behavior inside `HeddleCompiler` and its collaborators** — none of it lives in the shared parse front end the generator compiles in via D4. The front end contributes exactly: the clean document (comments and `@\` splices excised at parse — hidden-channel tokens never reach it), block positions, definition blocks, raw-output items, editor tokens, and syntax errors (`HED0003`). Everything else the emitter must reproduce or refuse — D22 enumerates which, machine by machine. |

## Design decisions

### D1 — Key normalization: ordinal case-sensitive, `/`-separated, root-relative — closed

**Decision.** This closes the roadmap's open question 1, per its lean; the full
algorithm and test table are pinned in
[identity-and-metadata.md § key normalization](identity-and-metadata.md#the-key-normalization-algorithm).
The rule: a precompiled key is the template's path **relative to the resolver root**
(`TemplateOptions.RootPath` at runtime, `HeddleTemplateRoot` at build, default
`$(MSBuildProjectDirectory)`), normalized by one shared pure function
`TemplateKey.Normalize` — backslashes → `/`, duplicate separators collapsed, leading
`./`/`/` stripped, `.`/`..` segments rejected, `.heddle` appended when no extension is
present (mirroring `TemplateResolver.Search`), **case preserved**, compared **ordinal
case-sensitive** — running identically at emit and lookup time (one source file,
shared-sourced into the generator, D12).
**Verification against source.** `TemplateResolver` today is OS-dependent: hard-coded
`\` location templates, `Replace('/', '\\')`, an `OrdinalIgnoreCase` cache — a
Windows-only rule that cannot round-trip a Linux host. The precompiled key space gets
the strict rule; the dynamic cache keeps its behavior untouched (inertness). **Why
case-sensitive:** folding would collide two legitimately distinct Linux files
(`Home.heddle`/`home.heddle`) into one key — a correctness failure; sensitivity merely
makes a case-sloppy Windows lookup *miss*, degrading safely to the dynamic path. The
registry keeps a case-insensitive **shadow index used only for diagnosis** (`HED7103`
`CaseMismatch` callback — never serves entries); the generator warns on case-only key
twins (`HED7003`). **Composition with the dynamic cache key:** the `|profile`/`|trim`
suffixes phases 2/4 added are *compatibility* facts, not identity — they live in the
`OptionsFingerprint`, enforced by the gauntlet, never in the key. **Migration:** the
dynamic path needs none; Windows hosts relying on case-insensitive lookups see a
`Fallback` recompile plus the `HED7103` callback naming the exact casing difference.
**Alternatives rejected.** Inheriting `OrdinalIgnoreCase` (cannot round-trip Linux;
collides distinct Linux files); OS-native comparison (build OS ≠ host OS is the entire
problem); case-folding at emit only (code lookups would still miss).

### D2 — Precompiled key replacement: duplicate = registration error; no replace flag in v1 — closed

**Decision.** This closes the roadmap's open question 2 per its lean. v1 ships **no**
replace flag. Registering a manifest whose key ordinally equals an already-registered
key throws `PrecompiledRegistrationException` with this exact message:

> Precompiled template key '{key}' from assembly '{newAssembly}' is already registered by
> assembly '{existingAssembly}'. Duplicate keys across precompiled manifests are not
> supported; an explicit replacement marker (a future `Replace` flag on the manifest
> entry, mirroring `[ExtensionReplace]`) is not yet available. Rename one template, give
> it an explicit `Key` metadata, or exclude it from pre-compilation with
> `<HeddleTemplate Remove="…" />`.

Registration is transactional per manifest (all-or-nothing — no half-registered
assembly); re-registering the **same** assembly is an idempotent no-op, not a duplicate.
**Recorded trigger to revisit:** a concrete layering deployment — a base precompiled
template assembly plus a customization assembly overriding individual templates — with a
real user attached; the reserved mechanism is a `Replace` boolean on the manifest entry
(schema bump), resolved with the "replacements register last" ordering
`TemplateFactory.AddExtensions` uses.
**Rationale.** Same posture as extension names (`TemplateOverrideException`); building
replacement before a layering use case exists is a presumptive feature
([YAGNI](../common/coding-standards.md#yagni-applied)); the error text names the future
mechanism so the upgrade is discoverable from the failure itself.
**Alternatives rejected.** Shipping the flag now (no consumer; widens the v1 schema);
last-registration-wins (silently renders a different compilation than shipped — exactly
what `Strict` exists to forbid).

### D3 — Two milestones: syntactic emission, then `ISymbol` binding (both committed)

**Decision.** Two milestones, both in scope (the roadmap ratified (b) as committed, not
contingent on complaints):

- **Milestone 1 — syntactic generation.** Member paths and native expressions emit as
  ordinary typed C# against the declared `@model`; the consuming project's own
  compilation binds them. A bad member path surfaces as a C# error (e.g. CS1061)
  **remapped to the `.heddle` span** via the span form of `#line` — documented as
  designed for exactly this DSL remapping. Exit: roadmap success criteria 1–8 green
  except native member diagnostics; differential green over the full corpus; fixtures
  assert the *reported* file/line/column of seeded member typos.
- **Milestone 2 — `ISymbol` typed binding.** The generator resolves the model type and
  every member path against the `Compilation` (`GetTypeByMetadataName`,
  `ITypeSymbol.GetMembers` walking the runtime member resolver's tier order) and
  reports **native Heddle diagnostics** (`HED7007`/`HED7008`) at the exact template
  span before the C# compiler sees the generated code. Strictly additive: same emitted
  code, better errors. Exit: seeded-mistake fixtures report `HED7008` (not remapped
  CSxxxx) for covered cases; differential unchanged; caching asserts green (symbols
  touched only in the final diagnostics stage, never cached).

**Why reflection is impossible in-generator (the roadmap's grounding correction,
reproduced):** inside a source generator the user's model types exist only as Roslyn
`ITypeSymbol`s in the compilation being built — the target assembly does not exist yet,
so `System.Reflection` over it cannot work; generators introspect user code exclusively
through the compilation/symbol APIs. The runtime backend's binding is reflection over
live types (`HeddleCompiler` via `Type.GetProperty`, `ModelParameter`'s expression
trees), so it **cannot run in-generator**; the backends necessarily diverge at type
binding, and the differential harness gates that divergence.
**Alternatives rejected.** Shipping only milestone 1 (`#line` fidelity is a mitigation,
not a diagnostics story — ratified); symbol binding first (delays correct behavior for
better errors — v1 correctness ships early, per the roadmap).

### D4 — Front-end sharing: `ParserSettings` seam + shared sources ("beside"), no type moves

**Decision.** The generator reuses the existing parse pipeline without moving public
types between assemblies. Two steps:

1. **Seam (runtime-inert refactor in `Heddle`):** new
   `public sealed class ParserSettings` in `Heddle.Language` (`RootPath`,
   `ProvideLanguageFeatures`, `ImportReader` — null → the current
   `File.OpenText(Path.Combine(RootPath, path))` behavior). `DocumentParser` gains the
   core overload `Parse(string, ParserSettings, out string)`; the existing
   `Parse(string, CompileContext, out string)` **stays** as a thin adapter (builds
   `ParserSettings` from `context.Options`, copies `ParseContext.Errors`/`Warnings`
   into `CompileContext` afterwards — the copy that already happens for syntax errors
   becomes the single copy point; `HeddleMainListener` redirects its direct
   `CompileErrors.Add` calls to `ParseContext.Errors` and routes `@<<` reads through
   `ImportReader`). Observable behavior unchanged — same errors, same order (gated by
   the full suite + goldens).
2. **Sharing ("beside"):** `Heddle.Generator` compiles the front-end host files as
   **linked shared sources** (the dependency closure: `DocumentParser`, `ParseContext`
   and its item types, `HeddleSyntaxErrorListener`, `HeddleCompileError`/`Warning`,
   `BlockPosition`, the `Heddle.Strings.Core`/`Heddle.Exceptions` pieces they use) and
   references `Heddle.Language.dll` (the ANTLR parser) packed into the analyzer folder.
   The known file list is enumerated in WI2; the closure completes mechanically by
   compiler errors, **gated** by the packaging test: the generator builds with **no**
   reference to `Heddle.csproj` and ships no `Microsoft.CodeAnalysis.*` assets.

The `ImportReader` seam makes the generator correct *and* feeds the manifest: its
reader serves import content from `AdditionalFiles` (never disk — determinism,
incrementality) and records the transitive `(path, contentHash)` closure for `Imports`.
**Rationale.** Moving `DocumentParser`/`ParseContext` into `Heddle.Language` would need
`TypeForwardedTo` shims, break the `Parse(string, CompileContext, …)` method reference
(its parameter type cannot move), and churn the phase 6 surface goldens — all for a
second consumer the shared-source route serves equally; parse knowledge still exists
exactly once (the same files compile into both assemblies).
**Alternatives rejected.** Move + `TypeForwardedTo` (binary-compat hazard, golden churn
— a deferred item with trigger); duplicating a simplified parser in the generator
(permanent semantic drift — the phase's defining risk squared).

### D5 — Generated-code shape: strategy classes + bound extension instances + one piece hook

**Decision.** Full reference with worked examples in
[generated-code.md](generated-code.md); the load-bearing choices:

- **One static class per template** (D11 names it): the piece table (string constants +
  the u8 tier per D15), one generated `IProcessStrategy` per compiled body,
  `static readonly` pre-constructed extension instances, the typed entry points.
- **`IProcessStrategy` is promoted from internal to public** (members unchanged) —
  generated code in the *user's* assembly implements it. Additive; the phase 6 surface
  goldens gain it with review.
- **Extensions are never inlined — bound, ratified.** Every call site (including
  trivial unnamed `@(…)` output and branch extensions) renders through a
  pre-constructed instance resolved **at build time** against the referenced
  assemblies' `[ExtensionName]` attributes (D9). Generated code reproduces the
  `TemplateItem` protocol literally: evaluate the parameter as typed C#,
  `scope.Model(value)`, call `RenderData`. Bodies are installed via the new public seam
  `PrecompiledRuntime.Bind(extension, body, renderType, needsLocals, line, column)` —
  public entry, internal access to `AbstractExtension`'s private body fields (only
  `InitStart` can set them today); `needsLocals` routes phase 3's `ScopeLocals` frame
  provisioning through an engine-side decorator, keeping frame semantics in one place
  (the same decorator wraps a participant-hosting document root via `WithLocalsFrame`,
  and `BindDefinition` constructs the engine-internal definition carrier itself —
  `DefinitionBaseExtension` is `internal` and stays so; full contract in
  [generated-code.md](generated-code.md#the-generated-support-api)).
  Encoding, culture, branching, and props semantics therefore ride the *same* extension
  code both backends share. Props keep the `object[]` carriage with typed generated
  construction (`BindDefinition` + indexed `Prop` reads — the halfway point of the swap
  phase 5 D7 reserved; see
  [generated-code.md example 5](generated-code.md#example-5--definition-with-phase-5-props)).
- **Root entry:** `PrecompiledRuntime.GenerateString(root, model, chained, caller)`
  owns renderer creation, the internal root `Scope` ctor, and the size-adaptive buffer
  high-water mark (mirroring `HeddleTemplate.Generate`); phase 8 adds sink overloads
  here.
- **Baseline language level C# 7.3** (the default-`LangVersion` floor of
  netstandard2.0/net48 consumers). Conditional tiers read `ParseOptionsProvider` →
  `LanguageVersion`: `#line` span form ≥ 10, `file`-scoped scaffolding ≥ 11 (`internal`
  mangled below), u8 pieces ≥ 11 (D15), collection expressions ≥ 12. Per
  [cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)
  no tier is load-bearing — every one has a plain fallback.
- **Emission conventions:** hint names `{SanitizedName}.g.cs` + `__HeddleManifest.g.cs`;
  `// <auto-generated/>`; `global::`-qualified references; deterministic output;
  `#line hidden` on scaffolding.

**Rationale.** Binding instances keeps a security/logic patch in an extension package
effective for precompiled templates without regeneration (ratified — inlining rejected
outright, including built-in fast paths); the `TemplateItem`-literal protocol makes the
differential harness a semantics gate rather than a reimplementation audit.
**Alternatives rejected.** Inlined extension bodies (patching hazard, duplicated per
template — ratified rejection); a public setter on `AbstractExtension`'s body fields
(invites runtime mutation of shared instances; `Bind` keeps "body set exactly once,
before first render").

### D6 — Metadata: discovery attribute + typed generated manifest; the fingerprint pinned

**Decision.** Two layers, exactly the roadmap's shape, fully specified in
[identity-and-metadata.md](identity-and-metadata.md): the assembly-level
`[HeddleCompiledTemplates(manifestType, schemaVersion, engineVersion)]` discovery marker
(one reflection touch per assembly — the `RazorCompiledItemAttribute` model), and a
generated manifest class implementing `IHeddleTemplateManifest` yielding one typed
`PrecompiledTemplateInfo` record per template. The pinned compatibility algorithm:
**schema gate** — accepted iff `schemaVersion == 1`; **engine gate** — accepted iff the
manifest's `engineVersion` has the same **major** as the loaded `Heddle` assembly and is
**≤** it (additive-within-a-major is guaranteed by the single 2.0 window,
[cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window));
failure ignores the whole manifest with one `HED7102` callback. **`OptionsFingerprint`
is a typed triple, not a hash** — `(OutputProfile, ExpressionMode, TrimDirectiveLines)`,
exactly the output-byte-changing set phases 2/4 put in the resolver cache key
(`AllowCSharp` is derived — it *is* `ExpressionMode.FullCSharp` per phase 1 D6's
bridge); compared field-wise, first differing field = the fallback reason detail.
**`ContentHash` = SHA-256 over raw file bytes, lowercase hex, no normalization** — any
byte difference (including line endings) changes rendered output, so byte-exact is the
correct staleness signal; `Imports` carries the transitive `@<<` closure as
`(key, contentHash)` pairs recorded by the generator's `ImportReader` (D4). Engine
version sits at layer 1 deliberately: one gate, one callback.
**Alternatives rejected.** Attribute-blob metadata per template (typed records need no
parsing and stay schema-versioned); fingerprint-as-hash (cannot say *why* it
mismatched); normalizing line endings before hashing (reports "fresh" for changed
rendered bytes).

### D7 — Runtime hookup: repeatable registration, gauntlet order, lazy partial routing

**Decision.** A new public static registry `PrecompiledTemplates` in `Heddle.Precompiled`
(full API in the contract section; gauntlet detail in
[identity-and-metadata.md § validation gauntlet](identity-and-metadata.md#the-validation-gauntlet)):

- **Registration** — `Register(Assembly)`: reads the attribute, runs the schema/engine
  gate, instantiates the manifest once, adds entries transactionally (D2). **Repeatable
  and idempotent per assembly** — the correction to `AssemblyHelper.Configure`'s
  verified one-shot `_configured` gate; `HeddleTemplate.Configure(assembly)` also calls
  `Register` for convenience, but `Register` is the primary API precisely because
  `Configure` is one-shot. Lock-guarded with copy-on-write publication; `TryGet` is a
  lock-free volatile read.
- **Consultation in `TemplateResolver.GetTemplate`** (before the cache probe and file
  check): derive the root-relative path from `viewName` (or the matched search
  location), `TemplateKey.Normalize`, `TryGet`; on hit run the per-request gauntlet —
  marker-entry short-circuit (`UnsupportedFunction`, D21), options fingerprint,
  extension bindings vs the live registry (new internal
  `TemplateFactory.TryGetExtensionType` + the `BindingResolver` seam, D9), function
  bindings vs the request's effective function registry (D21), staleness
  only under `EnableFileChangeCheck`; all pass → a `HeddleTemplate` in **precompiled
  adapter mode** (new internal ctor setting `_processStrategy` from the entry,
  `CompileResult` = success — host code cannot tell the difference); any failure →
  policy per D8. A registry **miss** is not a failure — the dynamic path proceeds
  untouched.
- **Staleness mechanics** mirror the dynamic path's watcher model, never per-request
  hashing: the verdict is computed at first lookup and cached; under
  `EnableFileChangeCheck` watchers on the root file and every import-closure file
  invalidate the cached verdict. Per-render cost zero; per-lookup cost after first: one
  volatile read.
- **Partials route through the registry.** `PartialExtension.CompleteInit` consults
  `PrecompiledTemplates` (key = `Normalize(templateName + FileNamePostfix)`) before its
  `FileReader` compile — a runtime-compiled template's partial can bind a precompiled
  entry. Generated `@partial` code calls `PrecompiledRuntime.ResolvePartial` lazily on
  first render (`LazyInitializer.EnsureInitialized` — at-most-once),
  registry-then-dynamic-compile — a precompiled template can render a runtime-compiled
  partial. Both mixed-mode directions served; the assumed-state correction (partials
  bind at compile time today) is a recorded behavior change **for the generated backend
  only** — the dynamic path's compile-time embedding is unchanged.
- **Discovery is public:** `Entries` enumeration plus the `OnFallback` callback — the
  integration layer can route, validate, and report coverage.

**Alternatives rejected.** Registration solely via `Configure` (verified one-shot — a
second assembly's manifest would be silently dropped); per-request content hashing (a
disk read per lookup; the watcher model is what `EnableFileChangeCheck` already means);
an instance registry on `TemplateResolver` (typed entry points and partials need
process-wide lookup, and the engine's registry pattern is static).

### D8 — `PrecompiledMismatchPolicy { Fallback, Strict }` on `TemplateOptions`, default `Fallback` — ratified

**Decision.** `public enum PrecompiledMismatchPolicy { Fallback = 0, Strict = 1 }` in
`Heddle.Data`; `TemplateOptions.PrecompiledMismatchPolicy` (default `Fallback`), copied
in the copy ctor (the phase 2 completeness test covers it automatically), **not** in
`Equals`/`GetHashCode` (it changes failure handling, never output bytes). Semantics,
ratifying the roadmap's defaulting note: **`Fallback`** — any gauntlet failure → the
unchanged dynamic path, plus one `OnFallback` callback with the reason and a
`HeddleCompileWarning` (`HED7101`) on the recompile's result (a silent fallback that
quietly re-adds compile cost is the failure mode designed against). **`Strict`** — an
entry that *exists* but fails the gauntlet throws `PrecompiledMismatchException`
carrying key + reason; a plain registry **miss** is not a `Strict` failure — strictness
polices divergence, not coverage (ratified).
**Rationale.** Safe-by-default for mixed-mode hosts; flip per deployment where dynamic
compile cost must be impossible. Options-level placement gives per-compile granularity
and rides the existing resolver threading.
**Alternatives rejected.** Default `Strict` (breaks mixed-mode-by-default, the
roadmap's first-class scenario); a process-global static policy (coarser than the
request-scoped options the gauntlet already receives, and a second home for one
policy).

### D9 — Extension binding: from assemblies at build and runtime; the match seam — ratified

**Decision.** Extension logic is **never inlined** (ratified; including built-in fast
paths). Build time: the generator resolves every extension name by scanning the
`Compilation`'s referenced `IAssemblySymbol`s for `[ExtensionName]` types (needed in
milestone 1 already — generated code must name the concrete type to construct),
applying `TemplateFactory.LoadExtensions`'s interface-ordering/`[ExtensionReplace]`
precedence; an unresolved name is build error `HED7006`. The manifest records each
binding as `(name, AQN without version)`. Runtime: the gauntlet compares recorded
bindings against the live registry; **the match rule is integration-suppliable**
(ratified) via `PrecompiledTemplates.BindingResolver`, engine default = AQN *without*
version — an overridden extension is the divergence that matters, patch-version churn
is not. Divergence → per D8 policy.
**Rationale.** A logic/security fix in an extension package reaches precompiled
templates by updating the package, without regeneration; the runtime check makes
`[ExtensionReplace]` overrides visible instead of silently divergent.
**Alternatives rejected (ratified).** Inlining (duplication + patching hazard); binding
by versioned AQN (every patch release would invalidate every precompiled template).

### D10 — No MSBuild-task fallback — ratified

**Decision.** The generator is the only driver; SDK-style projects with
source-generator support are the audience, legacy build setups out of scope. Recorded
trigger to revisit: a real generator-hostile environment with a real user attached —
and then as a separate driver over the same emitter, never a fork of it.
**Rationale.** A second driver for the same emitter is maintenance without a customer
(the roadmap's ratified wording); every emitter feature would need drive-path-matrix
testing for an audience that has not materialized.
**Alternatives rejected.** Shipping an MSBuild task now (no customer; doubles the
driver test matrix); building task hooks into the generator package "in case"
(dead surface area — [YAGNI](../common/coding-standards.md#yagni-applied)).

### D11 — Typed entry points are the recommended host API; naming pinned — ratified

**Decision.** Each precompiled template emits a static class
`{HeddleGeneratedNamespace}.{SanitizedName}` — namespace default
`{RootNamespace}.HeddleTemplates`; `SanitizedName` = the key's segments PascalCased,
identifier-invalid characters → `_`, joined by `_`, extension dropped
(`views/home/index.heddle` → `Views_Home_Index`); two keys sanitizing to one identifier
is build error `HED7010`. Entry points: `public static string Generate(TModel model,
object chained = null, object callerData = null);` with `TModel` = the declared
`@model` type, `object` for `:: dynamic`, and model-less templates emitting
`Generate()` plus the `(object, object, object)` shape the registry adapter uses. Typed
entry points are the **recommended** host API for templates known at compile time
(ratified): compile-checked model type, no lookup, no gauntlet; the key-based registry
serves dynamic call sites — both ship. Sink overloads are **not** emitted in v1 —
phase 8's addition at the `PrecompiledRuntime` funnel (the sequential rule forbids
depending forward).
**Alternatives rejected.** Nested classes mirroring directories (deep nesting, no
call-site benefit); interceptor-based call rewriting (per call site, only literal
arguments statically resolvable, raises the `Microsoft.CodeAnalysis` floor to 4.11+ —
deferred past v1 as a possible migration aid, per the roadmap's evaluation).

### D12 — Packaging: `Heddle.Generator` analyzer NuGet; `Microsoft.CodeAnalysis.CSharp` 4.4.0 floor

**Decision.** New project `src/Heddle.Generator/Heddle.Generator.csproj`:
netstandard2.0 (the only target loadable in both the .NET-Framework-hosted VS compiler
and the SDK compiler — RS1041), `IsRoslynComponent=true`, `IncludeBuildOutput=false`,
`EnforceExtendedAnalyzerRules=true`, strong-named with `heddle.snk`. Reference
`Microsoft.CodeAnalysis.CSharp` **4.4.0** with `PrivateAssets="all"` — the pinned floor:
the oldest line with reliable incremental caching (the community-documented baseline),
maps to VS 2022 17.4+/SDK 7.0.1xx+ per the official Roslyn↔VS version table, stays far
below the 5.x compilers of VS 2026/.NET 10 SDK (older-referencing generators load fine
on newer hosts; the reverse fails), and v1 uses nothing newer — the features that would
raise the floor (interceptors' location API) are exactly the deferred ones. Package
layout per the cookbook: the generator assembly plus its generation-time dependencies —
`Heddle.Language.dll`, `Antlr4.Runtime.Standard.dll` — all inside `analyzers/dotnet/cs`
(NuGet does not restore package dependencies for analyzer assemblies);
`Microsoft.CodeAnalysis*` is never shipped — the host compiler provides it. The package
carries `buildTransitive/Heddle.Generator.props` + `.targets`:

- default include `<HeddleTemplate Include="**/*.heddle" Exclude="bin/**;obj/**" />`
  unless `$(EnableDefaultHeddleTemplates) == 'false'`;
- `HeddleTemplate` items mapped to `AdditionalFiles`;
- `<CompilerVisibleProperty Include="HeddleOutputProfile;HeddleExpressionMode;HeddleTrimDirectiveLines;HeddleMaxRecursionCount;HeddleTemplateRoot;HeddleGeneratedNamespace;HeddleEmitUtf8Pieces" />`;
- `<CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="Key" />` (+
  `Name`, `Precompile`) — surfaced to the generator as
  `build_metadata.AdditionalFiles.*` via `AnalyzerConfigOptionsProvider.GetOptions(text)`.

The shared key-normalization source file (D1) and the front-end closure (D4) compile in
as linked sources. The core `Heddle` package stays runtime-only and unrestricted.
**Alternatives rejected.** 4.0.1 floor (VS 2022 RTM reach, but pre-4.4 incremental
caching pitfalls are the documented hazard the design-doc rules exist to avoid); 4.11+
(narrows host reach for features v1 defers).

### D13 — Generator diagnostics share the `HED` scheme; forwarded engine diagnostics keep their IDs

**Decision.** Generator-emitted Roslyn `DiagnosticDescriptor`s use the `HED7xxx` block
directly as their `Id` (category `"Heddle.Precompile"`), not a generator-specific
prefix: Roslyn requires only a stable unique ID string, Razor's `RZxxxx` build
diagnostics prove DSL-prefixed IDs work in build output, and one scheme keeps one
registry ([D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)),
one docs table, one LSP `Diagnostic.code` mapping. Front-end errors surfaced through
the generator are forwarded with their **own** `DiagnosticId` at the `.heddle`
position; one that predates the ID plumbing is wrapped as `HED7012` (error) / `HED7013`
(warning). Runtime-side conditions use `HED71xx` (`HeddleCompileWarning`s and callback
events, never Roslyn diagnostics). Allocation: `HED70xx` build-time, `HED71xx` runtime.
**Alternatives rejected.** A `HG`/`HEDG` prefix (fragments the registry and tooling
mapping for zero benefit); reusing forwarded errors' IDs for generator-specific
conditions (conflates who detected what).

### D14 — Build-time options mirror `TemplateOptions`; one `HeddleExpressionMode` knob

**Decision.** MSBuild properties map 1:1 to the identity-bearing options:
`HeddleOutputProfile` (`Text`|`Html`, default `Text`), `HeddleExpressionMode`
(`MemberPathsOnly`|`Native`|`FullCSharp`, default `Native`), `HeddleTrimDirectiveLines`
(default `false`); defaults are stated explicitly in the props file and flip together
with the engine defaults in the 2.0 window
([cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)).
One further property mirrors a compile-time input that is **baked rather than
identity-bearing**: `HeddleMaxRecursionCount` (positive integer, default `100` — the
`TemplateOptions` constructor default; never flips), the definition-carrier recursion
limit D23 threads through `BindDefinition`; it participates in neither the fingerprint
nor the gauntlet (D23 records why).
The roadmap's "the `AllowCSharp` trust decision becomes a build-time property" is
delivered *as* `HeddleExpressionMode=FullCSharp` — phase 1 D6 made `AllowCSharp` a
bridge over `ExpressionMode`, so a second boolean would be a second representation of
one fact. Unparsable values are build error `HED7009` — the generator never guesses a
default from a typo. Per-item metadata: `Key` (D1 seam), `Name` (entry-class override,
D11), `Precompile` (`false` = import source only — hashed, no entry; `HED7011`'s remedy
for import-only layouts). Per-item *option* overrides are deliberately absent (trigger
under deferred items).
**Alternatives rejected.** A separate `HeddleAllowCSharp` boolean (a second
representation of one fact — two knobs could disagree; the bridge already decides);
per-item option overrides (one compilation has one fingerprint — a per-item override
would need per-item fingerprint and gauntlet semantics for a project layout nobody has;
deferred with its trigger); guessing a default from an unparsable value (a typo could
silently widen the sandbox — `HED7009` fails the build instead).
**Grounding.** `CompilerVisibleProperty`/`CompilerVisibleItemMetadata` →
`build_property.*`/`build_metadata.AdditionalFiles.*` is the documented mechanism.

### D15 — u8 piece tier: opt-in in v1, validated always, lone surrogates downgrade — corrects one roadmap nuance

**Decision.** Every static piece is emitted through **one** emitter hook (`EmitPiece`):
always the string constant (`internal const string P3 = "…";`), and — when
`HeddleEmitUtf8Pieces=true` **and** `LanguageVersion` ≥ 11 — the UTF-8 twin
`internal static ReadOnlySpan<byte> P3U8 => "…"u8;` (compiler-embedded UTF-8 bytes in
the assembly's static data; no allocation, no encoding call, no static ctor; u8 literals
are runtime constants — not `const`, not interpolatable — fine for sink writes). Default
**`false` in v1**: nothing consumes the bytes until phase 8's `IBufferWriter<byte>` sink
exists, and unconditional emission duplicates static data; phase 8's package update
flips the default and adds sink entry points at the `PrecompiledRuntime` funnel,
consuming the u8 properties **without transcoding** — the same generated type serves
both sinks because both piece forms come from the one hook (the delivery of
[cross-cutting D4](../common/cross-cutting-decisions.md#d4--utf-8-static-piece-emission-u8-belongs-to-phase-7)).
`Capabilities` records `Utf8Pieces` when emitted. **Validation runs regardless of the
toggle:** a static piece with an unpaired surrogate raises `HED7005` and suppresses the
u8 twin for that template — a **warning**, not the error the roadmap sketched: the
template is legal under the runtime backend today (string output preserves the lone
surrogate; failing the build would be a regression), while a u8 literal with one is a
hard C# error the generator must pre-empt. The differential harness carries the
lone-surrogate fixture; the u8-parity test asserts compiler-encoded bytes equal
`Encoding.UTF8.GetBytes` for well-formed corpus pieces.
**Alternatives rejected.** Unconditional u8 in v1 (dead static data for a tier nothing
reads yet); build error on lone surrogates (breaks templates the dynamic backend
accepts); runtime UTF-8 piece caching (rejected in cross-cutting D4).

### D16 — Incremental pipeline: value-equatable models, collected import map, tracked steps

**Decision.** Pipeline stages (all intermediate models are value-equatable records
holding no symbols, syntax, or compilations — the design-doc caching rule):

1. `AdditionalTextsProvider` filtered to `.heddle`, combined per-file with
   `AnalyzerConfigOptionsProvider.GetOptions(text)` → `TemplateInput(path, content hash,
   content, key/name metadata, precompile flag)`. Tracking name
   `"Heddle.TemplateInputs"`. Equality: value equality over `(path, contentHash, key,
   name, precompile)` with `Content` **excluded** from `Equals`/`GetHashCode` (the hash
   already witnesses it — unchanged files compare in O(1), the design-doc's
   extract-early rule).
2. Global `AnalyzerConfigOptionsProvider` + `ParseOptionsProvider` →
   `GlobalConfig(profile, mode, trim, maxRecursion, root, ns, emitU8, languageVersion)`.
   Tracking name `"Heddle.GlobalConfig"`. Equality: plain field-wise value equality
   (enums, strings, `int`, bool — `LanguageVersion` is an enum, safe to hold).
3. `TemplateInputs.Collect()` → the **import map** — `@<<` resolution needs other
   files' content, so parse cannot be import-blind. Tracking name `"Heddle.ImportMap"`.
   Equality: entries sorted ordinally by key, sequence equality over `(key,
   contentHash)` — content itself is fetched by the `ImportReader` at parse time, not
   compared.
4. `CompilationProvider` → the **function-export table** (D21 discovery): the
   selector probes `Compilation.Assembly` and `SourceModule.ReferencedAssemblySymbols`
   for `[ExportFunctions]`, applies the D24/D12 table-construction rules, and projects
   the merged default ∪ export table into a value-equatable model of strings and ints
   (rows sorted ordinally; symbols touched only inside the selector, never held by the
   model — the same rule step 6's milestone-2 stage follows). Tracking name
   `"Heddle.FunctionExports"`. Equality: ordered sequence equality over
   `(name, containerTypeName, methodName, parameterTypeNames, returnTypeName)` rows.
   The selector re-runs when the compilation changes, but an unchanged reference set
   produces an **equal** table, so downstream emit stays cached — the same honest
   contract as the import map.
5. Per-template `Combine(input, importMap, exportTable, config)` → parse (shared front end, D4;
   `ImportReader` serves from the map and records the closure) → emit →
   `GeneratedOutput(hintName, sourceText, diagnostics, manifestRecord)`. Tracking name
   `"Heddle.Emit"`. Equality: value equality over all four (sourceText by content,
   diagnostics as an ordered sequence) — this is the equality that keeps downstream
   outputs cached.
6. `RegisterSourceOutput` for template sources + one manifest/attribute output over the
   collected records. Milestone 2 adds a final diagnostics-only stage combining
   `CompilationProvider` (tracking name `"Heddle.SymbolDiagnostics"`; symbols touched
   only there, never cached — they never enter a pipeline model).

The honest incrementality contract: editing one template re-*runs* parse/emit for
templates whose combined inputs changed (the collected import map changes for all), but
unchanged templates produce **equal** `GeneratedOutput`, so downstream source outputs
stay cached — "touching one template regenerates only that template" holds at the
`AddSource` level. Every stage carries `WithTrackingName` with the names above; the
caching tests assert step run-reasons against exactly these names.
**Alternatives rejected.** Two-phase import-list extraction to narrow the map (knowing
imports requires a parse — circular); file IO from the pipeline (breaks determinism and
caching; imports must be `AdditionalFiles` — `HED7011` enforces).

### D17 — Mixed-mode precedence and the inertness guarantee

**Decision.** Resolution order everywhere (`TemplateResolver.GetTemplate`,
`PartialExtension`, `PrecompiledRuntime.ResolvePartial`): (1) registry hit + gauntlet
pass → adapter, zero parse, zero compile; (2) miss, or gauntlet failure under
`Fallback` → today's dynamic path completely unchanged. With zero manifests the consult
is one `volatile` read and one failed `TryGetValue` — proven inert by a resolver
micro-benchmark (zero allocation delta, time within BenchmarkDotNet error) and
byte-identical suite output. The precedence matrix (hit; miss → dynamic; edited +
`EnableFileChangeCheck` → runtime recompile wins; edited without the flag → precompiled
renders, staleness detectable via `ContentHash`) is pinned by fixtures before the
wiring lands — the roadmap's risk mitigation, kept.
**Alternatives rejected.** Consulting the registry *after* the dynamic cache probe (the
first dynamic compile would seed the cache and shadow a subsequently registered
manifest, and the per-request gauntlet must see the request's options before any cached
answer is trusted — registry-first is what makes "looked up, not parsed and compiled"
true); an opt-in resolver flag gating the consult (zero-configuration is the point;
inertness is proven by the benchmark, not bought with a setting).

### D18 — Non-goal restated: no AOT/trimming constraints; the exact boundary

**Decision.** Native AOT of the runtime libraries stays an explicit non-goal (ratified):
`Heddle`/`Heddle.Language` keep runtime Roslyn emit, compiled expression trees,
reflection binding, `:: dynamic`, hot reload — all unsupported or degraded under Native
AOT — and pre-compilation already delivers the startup win AOT chased. The boundary:
the **generator package** assumes only a netstandard2.0 compiler host (analyzer assets
are build-time only); the **consuming app** still references the full dynamic `Heddle`
runtime (the fallback path is load-bearing by design; no strip configuration ships);
the **generated code** is plain reflection-free C# — incidentally AOT-friendly, but no
phase 7 test or contract asserts AOT behavior. Revisit only on concrete repeated
demand, as a separate slimmed package, never as constraints on the engine.
**Alternatives rejected.** Adding AOT/trimming annotations to `Heddle` now (the engine's
core mechanisms — runtime Roslyn emit, compiled expression trees, reflection binding —
are structurally unsupported under Native AOT per the official limitations; annotations
cannot fix that, only warn about it); asserting AOT-friendliness of generated code in
phase 7 tests (would harden an incidental property into a contract this phase does not
own, recreating the constraint the ratified non-goal exists to refuse).

### D19 — Test harness: `Verify.SourceGenerators` + `CSharpGeneratorDriver`; new suite home

**Decision.** Generator tests live in a new `src/Heddle.Generator.Tests` project
(net8.0;net10.0): snapshot tests via **`Verify.SourceGenerators`** over
`CSharpGeneratorDriver` (multi-file outputs and diagnostics handled natively), caching
tests via the driver's step tracking (`trackIncrementalGeneratorSteps: true`, asserting
run reasons), diagnostics tests asserting ID + mapped `.heddle` span. A separate
`src/Heddle.Generator.IntegrationTests` project consumes the generator **as an
analyzer** with the fixture corpus as `AdditionalFiles` — the differential-harness and
`AssemblyLoad`-probe home. The new homes extend the
[testing-standards suite table](../common/testing-standards.md#suite-homes), recorded
here because `Heddle.Tests` must not reference `Microsoft.CodeAnalysis` test packages
(that would drag Roslyn into every engine test run and the net48 target).
**Alternatives rejected.** `Microsoft.CodeAnalysis.Testing` (analyzer/codefix-shaped
harness; heavier for pure-generation snapshot flows); raw string-compare snapshots
(loses the multi-file/diagnostic handling Verify provides).

### D20 — Differential authority and the drift gate

**Decision.** The runtime path is the **semantic reference**: byte-identical output
between backends across the full fixture corpus (`src/Heddle.Tests/TestTemplate/**`),
CI-resident from the day the phase ships. On divergence the fix goes to the emitter,
never to the runtime to match it — unless the divergence exposes a runtime bug (a
maintainer ratification event). Every future language phase must land in both backends;
the harness is the permanent regression system for that obligation — the phase's
defining risk, accepted with this mitigation.
**Alternatives rejected.** Symmetric authority — deciding per divergence which backend
is "right" (semantics would drift toward whichever backend is easier to change; the
asymmetric rule keeps the shipped, battle-tested runtime the oracle, with maintainer
ratification as the sole escape hatch); independent golden baselines per backend
instead of cross-comparison (two baselines can each drift with their own bugs and stay
green; the live differential cannot).

### D21 — Functions in precompiled templates: direct binding to discovered `[ExportFunctions]` exports; built-ins through the public `PrecompiledFunctions` shim (OQ1)

**Decision.** This implements the **maintainer resolution** of
[OQ1](../OPEN-QUESTIONS.md#oq1--scope-of-function-support-in-precompiled-templates-from-gap-g1--resolved-july-2026)
(July 2026 — the resolution **overrides the provisional default** an earlier revision
of this record implemented; the superseding ledger entry closes the register row).
Build-time function binding is an **integration problem**, solved the same way the
runtime host solves it: the consuming project references the assemblies carrying its
functions, the generator **searches those references first, then compiles, binding
directly to the discovered types**. Five parts:

1. **Discovery — the compilation's metadata references, before emission.** The
   declarative contract is phase 6 D24's, unchanged and shared: the assembly-level
   `Heddle.Attributes.ExportFunctionsAttribute` (`(Type container)` /
   `(params Type[] containers)` ctors, no parameterless "all" form), container =
   public static class, every public static method = one function, name =
   `MethodInfo.Name.ToLowerInvariant()`, eligibility/replace/overload semantics =
   phase 1 D12's `Register(string, MethodInfo)` rule set — one attribute, one
   contract, **three readers**: the host's `FunctionRegistry.RegisterFrom(Assembly)`,
   phase 6's one-shot editor scan, and this generator. The generator's read is pure
   **`ISymbol` metadata inspection**: it probes `Compilation.Assembly.GetAttributes()`
   (a project may export its own containers) and every
   `Compilation.SourceModule.ReferencedAssemblySymbols` entry's `GetAttributes()` for
   the attribute; container types arrive as `INamedTypeSymbol` typed constants. This
   is **milestone-1 capable by construction** — it reads attribute metadata and public
   method signatures of *already-built referenced assemblies* through the same symbol
   surface D9's extension binder already walks for `[ExtensionName]` (both APIs exist
   since Roslyn 3.0, far below D12's 4.4.0 floor), and it is categorically different
   from milestone 2's deferred `ISymbol` work, which binds *model types and member
   paths of the compilation's own source* (D3 is untouched: reflection over the target
   assembly stays impossible; symbol metadata of references was never the constrained
   part). From the probe the generator builds the **same name → overload table
   `RegisterFrom` would build at runtime**: it starts from the 18 `DefaultFunctionTable`
   rows (the pre-registered built-ins a fresh `FunctionRegistry` starts with, phase 1
   D12), then processes exports in a pinned deterministic order — referenced assemblies
   in `ReferencedAssemblySymbols` order, the compilation's own assembly last, and
   within one assembly D24's order (attribute declaration order, then ctor type-array
   order). Per method the D12 eligibility filter is evaluated over symbols
   (public static ordinary method, non-`void` return, `!IsGenericMethod`, no
   `ref`/`out`/pointer parameters); the name is the method name lowercased invariant;
   an exact name + parameter-type match (parameter sequence equality under
   `SymbolEqualityComparer.Default`, metadata-name equality against
   `DefaultFunctionTable`'s type-name strings for default rows) **replaces** the
   existing row, anything else joins the name's overload set — exactly what
   `RegisterFrom` produces. The host's own `RegisterFrom` call order is its choice;
   in the one case where order is observable — a cross-assembly exact-signature
   collision — a host order disagreeing with the pinned build order surfaces as a
   `FunctionBindingMismatch` through part 4's rows: the pinned order buys determinism,
   the gauntlet buys safety. An **invalid export** (non-public/non-static container, or
   an ineligible public static method) is skipped, mirroring the editor scan's D24
   posture — divergence-safe, because the host's own `RegisterFrom` throws on the same
   input, so no *running* host can disagree with the build's view; the skipped
   container's names then resolve nowhere and draw `HED7014` at any call site, whose
   message carries the remedy. Pipeline placement (D16): discovery is the
   `"Heddle.FunctionExports"` stage — a `CompilationProvider` selector projecting the
   merged table into a value-equatable model of strings and ints (rows sorted
   ordinally; symbols touched only inside the selector, never cached), combined into
   the per-template emit stage.
2. **Emission — direct binding; the shim only where the target is unnameable.** A
   `CallNode` (or a standalone call resolved to a function, part 3) whose name is in
   the merged table emits a **direct static call** on the binding target:
   - **All of the name's overloads are default rows** → the public shim:
     `@(upper(Name))` emits `PrecompiledFunctions.Upper(m?.Name)`. The shim survives
     the resolution *only* for built-ins because `BuiltInFunctions` is internal to
     `Heddle` with no consumer IVT (assumed-state row) — there is no nameable target
     to bind directly. `public static class PrecompiledFunctions` in
     `Heddle.Precompiled` (assembly `Heddle`): one public static method per default
     overload — 18 names, 35 overloads (the phase 1 D13 table as amended by phase 4
     D2's `range`), PascalCased names matching `BuiltInFunctions`, each a one-line
     delegation to the exact internal method `FunctionRegistry.Default` binds — public
     entry, internal access, the same posture as `Bind` (D5). Delegation keeps phase 1
     D13's pinned semantics (invariant culture, uniform null rules, never-throw,
     phase 4 D3's sanctioned `range` step-guard exception) in **one** implementation.
     The shim's signature source is the shared-source table
     `src/Heddle/Precompiled/DefaultFunctionTable.cs` (internal; rows of
     `(name, shimMethodName, parameterTypeNames, returnTypeName)` — strings and
     primitives only, compiled into **both** `Heddle` and the generator like
     `TemplateKey`, D1/D12), and the `DefaultFunctionLockstepTests` gate survives
     unchanged: (a) set-equality both ways between table rows and
     `FunctionRegistry.Default` via the internal `EnumerateOverloads()` (amendments
     ledger); (b) a matching public static shim method per row.
   - **All of the name's overloads are one exported container's single C# method
     group** → the exporting type directly: `@(titlecase(Name))` with
     `[assembly: ExportFunctions(typeof(Acme.Web.TemplateFunctions))]` in a referenced
     assembly emits `global::Acme.Web.TemplateFunctions.TitleCase(m?.Name)`. The
     consuming compilation references that assembly **by construction** — discovery
     found the attribute there — so the type is nameable and the call binds with no
     registry dispatch, no shim hop, a stack frame that names the host's own method,
     and the same patching property extension binding has (D9): a fixed function
     package updates precompiled behavior without regeneration, gauntlet-policed.
   - **The name's overload set spans targets** (an export replacing or overloading a
     default name without covering every overload; two export assemblies sharing a
     name; same-container methods differing only by case, which D24 collapses onto one
     function name) → the emitter generates a private static **forwarder group** in
     the generated file: one forwarding method per merged-table overload, all carrying
     one C# method name (the function name PascalCased), each a one-line `#line
     hidden` delegation to its true target (shim method for surviving default rows,
     exporting container method for export rows) — and the call site invokes the
     forwarder group. The forwarder is scaffolding, not identity: the manifest records
     the true targets (part 4), never the forwarder.

   In every case the *consumer's* C# compiler binds the overload (milestone 1 has no
   argument types to rank): C# overload resolution over a closed candidate set that
   equals the runtime registry's overload set for the name reproduces the phase 1 D12
   rank by construction — D12 was designed as "C# betterness in miniature" (exact >
   implicit widening > boxing; normal form preferred over expanded `params` form) —
   and the function differential fixtures (WI5a) are the permanent gate on that
   equivalence. D12's no-candidate/ambiguity outcomes (`HED1012`/`HED1013`) surface in
   milestone 1 as remapped C# errors at the `.heddle` span (the D3 `#line` story).
   **Resolution order mirrors the runtime exactly** (phase 1 D11): definition →
   build-visible extension (D9) → registered function, where "registered" now means
   **the default table ∪ the discovered exports**. A discovered export colliding with
   a default name follows D12 replace semantics precisely as `RegisterFrom` would
   produce them: exact signature → **the export wins** (the emitted call binds the
   exporting type; that overload's shim method is simply never called by this
   compilation); different signature → overload-set union, ranked per D12 (the
   forwarder group when the surviving set spans targets).
3. **The un-precompilable remainder — `HED7014` narrows to it.** After the resolution,
   the only registrations that cannot be bound at build time are those **not
   representable in an assembly**: `Register(string, Delegate)` closures and other
   imperative-only registrations that exist solely after host startup code runs. A
   `CallNode` (or a standalone call with a function-compatible parameter shape,
   phase 1 D11) whose name resolves to **neither the default table nor any discovered
   export** is build **warning `HED7014`** at the call (reworded — the diagnostics
   table pins the text), and the template becomes a **fallback-marker entry**,
   mechanics unchanged: no entry class, no strategy, but a manifest entry carrying the
   key, hashes, fingerprint, and a `FunctionBindings` row with a `null` target for
   each unresolvable name. At run time a marker entry short-circuits the gauntlet with
   reason `UnsupportedFunction` — `Fallback` recompiles dynamically (one `HED7101`
   warning; a typo'd name then fails that compile with the engine's own `HED1001`),
   `Strict` throws `PrecompiledMismatchException` with the pinned detail string. The
   marker is what makes `Strict` loud: a plain omission would be a registry miss,
   which strictness deliberately does not police (D8); the marker converts "this
   template *needed* a dynamic compile" into a policed divergence and keeps
   `PrecompiledTemplates.Entries` truthful for coverage reporting
   (`IsPrecompiled == false`). Build-time standalone-call triage mirrors phase 1 D11:
   definition → build-visible extension (D9) → the merged function table; a miss on
   all three is `HED7006` when the parameter shape can only be an extension's,
   `HED7014` + marker when the shape is function-compatible (a delegate registration
   could legally satisfy it at run time — the generator must not hard-fail what the
   runtime would accept).
4. **Parity and divergence — the integration owns parity; the manifest and gauntlet
   are the safety net.** The parity rule is the OQ1 resolution's, documented in
   `docs/precompilation.md` (WI12): **the build must reference the assemblies the host
   registers from** — every `options.Functions.RegisterFrom(assembly)` in host startup
   pairs with a build-time reference to the same assembly, and both sides read the
   same `[ExportFunctions]` metadata, so the build's merged table and the host's
   registry are the same set by the shared contract, not by convention. Violations are
   never silent: the manifest's `FunctionBindings` section (format pinned in
   [identity-and-metadata.md](identity-and-metadata.md#the-metadata-format), gauntlet
   step in [the gauntlet](identity-and-metadata.md#the-validation-gauntlet)) now
   records the **actual bound target** — one row per distinct `(name, target)` pair
   the template's generated code calls, target = the exporting type as
   AQN-sans-version (mirroring `ExtensionBindings`' shape and comparison rule, D9), or
   `Heddle.Runtime.Expressions.BuiltInFunctions, Heddle` (the shim's forwarding
   target) for shim-bound defaults — plus the row's **bound overload count** (the
   merged table's per-target count for the name, which the runtime cannot recompute
   once builds can bind exports). The per-request gauntlet checks every row against
   the request's effective registry (`options.Functions ?? FunctionRegistry.Default`):
   a `null`/`Default` request passes without enumeration **only when every row targets
   `BuiltInFunctions`** — `Default` is a frozen singleton (phase 1 D12) that cannot
   have diverged from the default rows, but it equally cannot contain an export, so an
   export-bound row against a bare-`Default` request is the parity violation "the host
   forgot `RegisterFrom`" and fails as `FunctionBindingMismatch` (`<missing>` detail).
   Otherwise, per called name: every live registration's target must be declared on
   one of the recorded target types **and** each recorded `(name, target)` row's live
   overload count must equal its recorded count — a live registry **missing a bound
   export**, carrying a **different exact-signature replacement** (a host `range` from
   a container the build never saw), a **delegate registration** under a bound name,
   or **differing overload counts** each fail with reason `FunctionBindingMismatch`
   and the pinned detail strings
   ([identity-and-metadata.md § reason and detail strings](identity-and-metadata.md#reason-and-detail-strings-pinned)):
   a fresh dynamic compile could bind differently than the baked call, which is
   exactly the divergence the gauntlet exists to catch — `Fallback` recompiles,
   `Strict` throws, never a silent split between backends. Names the template does not
   call are never checked — a host's unrelated registrations cost nothing.
5. **Why `Functions` stays out of `OptionsFingerprint`:** the fingerprint is the
   typed, value-comparable, compilation-wide option triple that the dynamic cache key
   also distinguishes (D6). A `FunctionRegistry` is a runtime object with no
   build-time value representation, it is in neither `TemplateOptions.Equals` nor the
   dynamic cache key (phase 2 D6 / phase 4 D9 kept it out), and a fingerprint field
   would kick **every** template of a compilation to dynamic on any host registration
   — including templates that call no functions at all. The per-name
   `FunctionBindings` check is strictly finer-grained and only fails templates whose
   output could actually diverge.

The emitter also reproduces the two literal-argument compile checks tied to built-in
calls, since both are type-free and AST-local (milestone 1 capable): phase 1's
`HED1015` composite-`format` literal hole check and phase 4's `HED4001` literal
`range`-step check, re-emitted with their own engine IDs per D22's ID rule. Each check
runs **only while every merged-table overload of its name is still default-bound** —
a discovered export that replaces `format` or `range` suppresses the corresponding
check, exactly phase 4 D3's "host-replaced `range` is exempt" clause, which direct
binding makes reachable in-generator (under the old default-registry-only model it was
vacuous at build time).
**Rationale.** Direct binding is the OQ1 resolution's core: no registry dispatch for
anything discoverable at build time, and the discovery is the same declarative
contract the runtime and editor already read — one attribute, one semantics owner
(phase 1 D12), three readers, so build-, runtime- and editor-parity is mechanical
rather than aspirational. The shim persists exactly where a direct target cannot be
named (internal built-ins), keeping the never-throw/invariant-culture contract in one
implementation; the lockstep test still makes "the table forgot a built-in" a red
build; and the forwarder-group trick extends the existing "C# betterness reproduces
the D12 rank over a closed set" argument to merged sets without the generator ever
ranking anything itself.
**Alternatives rejected.** The provisional default-registry-only model (the earlier
revision of this record — **superseded by the OQ1 maintainer resolution**: it degraded
every host-registered function to a fallback marker even when the export sat in a
referenced assembly the build could see, making precompilation and host functions
mutually exclusive in practice); a dispatch-by-name entry point
(`PrecompiledRuntime.CallFunction("upper", object[] args)`) — string dispatch and
argument boxing on the render path, no compile-time overload binding, undebuggable
frames, and it would need its own binder that *can* drift from D12; making
`BuiltInFunctions` public — widens phase 1's deliberately-internal surface and freezes
its type name into every consumer's generated code forever (the shim is the stable
façade); `[InternalsVisibleTo]` for consumer assemblies — impossible (consumer
assembly names are unknown and unbounded; IVT-to-everyone is `public` with extra
steps); loading referenced assemblies and running `RegisterFrom` in-generator
(generators must not load or execute user code, D3 — symbol metadata carries
everything discovery needs); registry-dispatch stubs for exports (re-imports the
string-dispatch rejection for the one case where a direct target exists by
construction); putting `Functions` in the fingerprint (part 5).
**Grounding.** [OQ1 — maintainer resolution](../OPEN-QUESTIONS.md#oq1--scope-of-function-support-in-precompiled-templates-from-gap-g1--resolved-july-2026);
[phase 6 D24](../phase-6-tooling-lsp/README.md#d24--declarative-function-exports-scanned-exportfunctions-assemblies-register-into-the-real-workspace-registry-oq2-gap-g4b)
(the export contract: attribute shape, name derivation, eligibility, processing
order, `RegisterFrom`);
phase 1 [D11/D12/D13](../phase-1-native-expressions/README.md#design-decisions)
(resolution order, registry semantics, the built-ins table);
phase 4 [D2/D3](../phase-4-ergonomics/README.md#design-decisions) (`range`, the step
guard and its replaced-`range` exemption); the verified assumed-state row above
(internal `BuiltInFunctions`, back-end `HED1001`, the D24 surface);
[Compilation.GetAssemblyOrModuleSymbol](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.compilation.getassemblyormodulesymbol)
and [IModuleSymbol.ReferencedAssemblySymbols](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.imodulesymbol.referencedassemblysymbols)
(Microsoft Learn — referenced-assembly `IAssemblySymbol` access from a `Compilation`;
both present since Roslyn 3.0, under the D12 floor);
[C# overload resolution — better function member](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions)
(normal-form-over-expanded and betterness ranking, the equivalence claim in part 2).

### D22 — The emitter's reimplementation surface: back-end document-shaping machines reproduced; unevaluable custom compile-time hooks refused

**Decision.** This closes gap G2 and **records a correction to this spec's own earlier
draft text**: the strip/orphan machines, trim widening, remnant-line trimming,
built-in directive removal, `@profile` scoping, the expression-mode gates, and
prop-layout resolution were attributed to "the shared front end" in
[generated-code.md](generated-code.md) — they are **back-end** behavior inside
`HeddleCompiler` and its collaborators (assumed-state row above, with anchors), which
the generator never runs. The corrected text stands in generated-code.md; this
decision pins, machine by machine, what the emitter **reimplements** versus
**refuses**.

**(i) Reimplemented** — each against its owning spec's pinned semantics, each gated by
the differential harness (D20) over the named corpus fixtures (WI6a):

| Machine | Semantics source | Emitter obligation | Diagnostics re-emitted | Milestone |
| --- | --- | --- | --- | --- |
| Branch strip + orphan analysis | Phase 3 D10 (`HeddleCompiler.ProcessBranchSets` — block classification incl. definition shadowing, set adjacency, gap stripping, the four-state orphan machine) | Reproduce both machines over the parsed chains at generation; participant classification uses the symbol-level `[ScopeChannel]` scan (the same scan that feeds `needsLocals`, D5) | `HED3001`–`HED3004` at `.heddle` spans | 1 |
| Trim widening + remnant lines | Phase 4 D6–D8 (`WidenToWholeLine` predicate verbatim, `TrimHiddenRemnantLines` over the skipped-token map, widening at the removal sites, the pinned pipeline order incl. composition with the strip machine) | Reproduce when `HeddleTrimDirectiveLines=true`; the D6 code block is the normative implementation for both backends | — (trimming has no diagnostics) | 1 |
| Zero-output-chain + definition-span removal | `HeddleCompiler.RemoveEmptyItem` / `RemoveDefinitions` | Remove the spans of the four engine built-in directives the generator special-cases — `@model`, `@using`, `@profile`, `@import` — plus `@% … %@` definitions and `@<<` imports, in the back end's pinned order | — | 1 |
| `@profile` document-order flip | Phase 2 D4 (effective profile on `CompileContext`, flipped mid-document, inherited by child bodies) and D2/D13 (`ProfileExtension`'s compile-time value/position checks) | The emitter's document walk carries an effective-profile state: initialized from the compilation-wide `HeddleOutputProfile` (D14), flipped at each `@profile(){{…}}` in document order, scoped into bodies exactly as the `CompileContext` copy chain scopes it; at each call site it selects the bound carrier + `RenderType` per phase 2 D3's redirect (example 1's `Empty` vs `EmptyHtml` note). The `OptionsFingerprint` records the compilation-wide **option** value only — directive flips are content, not options, identically on both backends. The discouraged paren form `@profile(html)` follows the ordinary parameter path, exactly as the runtime back end resolves it (member-path treatment — a remapped C# error in milestone 1) | `HED2001` (unknown/empty value), `HED2002` (directive after compiled output) — both engine-back-end checks the special case reproduces | 1 |
| Expression-mode gates | Phase 1 D7 (`HED1014` in `CompileItem`'s native branch) and the pre-ID C#-tier gate (`HeddleCompiler.CompileItem`: `"C# Code Not allowed here, see TemplateOptions.AllowCSharp Property"`) | Enforce `HeddleExpressionMode` in the emitter's parameter dispatch — a rejected construct is never emitted, never executed | `HED1014` with its own ID; the ID-less C#-tier message wrapped as `HED7012` with the engine's exact text | 1 |
| Literal-argument built-in checks | Phase 1 `HED1015`, phase 4 `HED4001` (both AST-local, type-free) | Run in the emitter's `CallNode` path against the D21 table | `HED1015`, `HED4001` | 1 |
| Prop-layout resolution + shadowing | Phase 5 D9 (prop-first first-segment resolution, `this.` escape, `::`-root exemption), phase 5's layout construction (indices, literal defaults, inheritance flattening) | **Milestone 1 — the syntactic subset:** layout indices, prototype arrays, prop-first name resolution, duplicate-header checks — everything decidable from the template text alone. The type-dependent checks (`HED5008` re-declaration assignability, `HED5009` default convertibility, `HED5010` type resolution) surface in milestone 1 as remapped C# errors where the declared type appears in emitted code (casts, typed prototype literals — the D3 `#line` story), and as native diagnostics in **milestone 2**. **`HED5011` shadowing detection is milestone-2-only** — it requires knowing whether the scope's model type exposes a same-name member, which is an `ISymbol` question; the interim guarantee is the differential gate: prop-vs-model *precedence* is syntactic and identical in both backends, so rendered bytes cannot diverge — only the warning is missing until milestone 2 | `HED5008`–`HED5010` (M2), `HED5011` (M2) | 1 / 2 as stated |

**(ii) Refused — fail-closed.** A bound extension type **outside the engine assembly**
that overrides `InitStart` or `CompleteInit` (the two virtual compile-time hooks on
`AbstractExtension` — symbol-visible as method overrides anywhere in the hierarchy
below `AbstractExtension`) is build **error `HED7015`** at the call. Rationale for
refusing rather than binding: `Bind` reproduces exactly the *base* `InitStart`
behavior (D5); an override may run arbitrary compile-time logic — including returning
`null` to become a zero-output chain, the removal/trim input of phase 4 D7's "any
custom extension whose `InitStart` returns null" rule — that the generator can neither
execute (generators must not run user code, D3) nor infer from symbols. Emitting
anything would be plausible-looking code with silently skipped compile-time semantics
— the phase's defining risk in its purest form — and unlike D21's function case there
is no narrow runtime check that could police the divergence afterwards. Rationale for
an **error** rather than a warning + marker: the extension *did* resolve (contrast
`HED7014`, where a runtime delegate registration is the expected legal outcome);
degrading
silently would flip every template using a directive-style custom extension to dynamic
compilation, defeating the phase without a red build. The remedy is in the message:
exclude the template, or keep custom compile-time behavior out of the two hooks.
Engine-assembly extensions are exempt — their compile-time behavior *is* the pinned
emitter knowledge above plus the existing special cases (definitions via
`BindDefinition`, partials via `ResolvePartial`, branch `needsLocals`), and the
differential corpus gates all of it. Custom extensions that do **not** override either
hook inherit exactly the base behavior `Bind` reproduces — they stay first-class
(D9; WI6's test-only `[ExtensionName]` assembly remains the fixture).

**Diagnostic-ID rule for reimplemented conditions** (a recorded widening of D13's
wrapper wording, allocation unchanged): a condition the emitter re-detects that has an
engine ID keeps that exact ID and message (`HED1014`, `HED1015`, `HED2001`, `HED2002`,
`HED3001`–`HED3004`, `HED4001`, `HED5007`–`HED5011`) — one condition, one ID,
regardless of which component detected it; an ID-less engine message is wrapped as
`HED7012`/`HED7013` exactly like an ID-less forwarded front-end diagnostic.
**Rationale.** The emitter cannot *share* these machines — they are private, they run
interleaved with reflection-typed chain compilation (`CompileItem`'s threaded `ExType`s),
and reflection over live model types is impossible in-generator (D3) — so the choice is
reimplement-under-differential-gate or refuse; the table draws the line at what is
decidable from template text + symbols, which is exactly the generator's information
boundary.
**Alternatives rejected.** Running `HeddleCompiler` inside the generator (needs live
types, expression trees, and the `Heddle` assembly the packaging gate excludes —
structurally impossible, D3/D12); binding InitStart-overriding customs anyway ("most
overrides are benign") — silent semantic divergence with no gate, the exact failure
mode D20 exists to prevent; a warning + fallback-marker for `HED7015`'s condition
(rejected above — resolvable-but-unevaluable deserves a red build, `HED7006`'s
precedent); leaving the misattributed "shared front end" text and handling the gap in
implementation notes (the spec is the contract — wrong attribution means no WI, no
tests, and an implementer who trusts the front end to do work it does not do).
**Grounding.** Phase 3 [D10](../phase-3-branching/README.md#design-decisions); phase 4
[D6–D8](../phase-4-ergonomics/README.md#design-decisions); phase 2
[D4](../phase-2-safe-output/README.md#design-decisions); phase 5
[D9](../phase-5-props-and-slots/README.md#design-decisions); phase 1
[D7/D11](../phase-1-native-expressions/README.md#design-decisions); the verified
assumed-state row (anchors into `HeddleCompiler.cs`).

### D23 — `MaxRecursionCount` is baked at build: `BindDefinition` carries it; build wins over runtime options

**Decision.** This closes gap G3. `PrecompiledRuntime.BindDefinition` gains an
`int maxRecursionCount` parameter (after `needsLocals`, before `line`/`column` — full
signature in [generated-code.md](generated-code.md#the-generated-support-api)); the
engine-side implementation assigns the constructed carrier's recursion limit directly
(engine-internal access — the same posture as `Bind`'s body-field access;
`DefinitionBaseExtension` stays internal and unchanged in shape). Without this, every
precompiled definition call throws on first render: the verified guard compares
`_recursionCount.Value >= _maxRecursionCount` and the field is assigned only in the
`InitStart` that `BindDefinition` deliberately bypasses (assumed-state row above).

- **Value source:** new MSBuild property `HeddleMaxRecursionCount` — a positive
  integer, default `100`, which is byte-for-byte `TemplateOptions.MaxRecursionCount`'s
  constructor default (verified,
  [TemplateOptions.cs](../../../src/Heddle/Data/TemplateOptions.cs)). It joins D12's
  `CompilerVisibleProperty` list, D14's property table, and D16's `GlobalConfig`
  pipeline model; an unparsable or non-positive value is `HED7009` (the generator
  never guesses). The emitter bakes the value as a literal argument at every
  `BindDefinition` call site (worked examples 4 and 5 show the baked literal).
- **Build wins over runtime options — the rule and its rationale.** A precompiled
  definition carrier uses the baked build value regardless of the request's
  `TemplateOptions.MaxRecursionCount`. This is not new semantics: in the runtime
  backend the limit is **already compile-time state** — `InitStart` reads it from the
  compile scope's options once, and the resolver cache key excludes
  `MaxRecursionCount`, so two hosts with different limits already share whichever
  compile ran first. Build-wins is first-compile-wins with the build as the first
  compile; the carrier is baked, and re-reading request options at render would mutate
  shared per-template static state per render (contradicting D5's "never mutated after
  `Bind` returns" and the extension thread-safety contract).
- **Fingerprint and manifest participation: none, recorded.** `MaxRecursionCount` does
  not join `OptionsFingerprint`: the fingerprint is pinned to the output-byte-changing
  set the *dynamic cache key* distinguishes (D6), and `MaxRecursionCount` is in
  neither `TemplateOptions.Equals` nor that key — a fingerprint field would fail
  templates over an option the dynamic path itself does not distinguish, a false
  divergence (and for outputs that *would* differ — a render that exceeds one limit
  but not the other — the dynamic path has the identical first-compile-wins exposure
  today). It is not recorded in the manifest either: nothing at run time consults it —
  the carrier is baked, and the divergence note below is the documented contract. The
  back-compat section carries the note verbatim.

**Rationale.** The parameter route is the only one that reaches the carrier —
`BindDefinition` constructs it (D5), so the limit must arrive through that call; an
MSBuild property is the established D14 channel for compile-time inputs; matching the
`TemplateOptions` default keeps the zero-configuration case byte-identical between
backends (differential-gated by the recursion fixture).
**Alternatives rejected.** Reading the request's options at render (shared-state
mutation, above; also the render path has no options in scope — `Scope` carries none
by design); threading the limit through `GenerateString` (its signature is pinned and
bound by phase 8's D7, and the limit is per-carrier compile-time state, not per-render
state); a gauntlet check or fingerprint field (above — polices what the dynamic path
does not); defaulting the parameter to `0`/absent (the verified `>=` guard makes an
unset limit throw on first use — exactly the G3 defect); a per-item metadata override
(`MaxRecursionCount` per template) — no scenario, same posture as D14's rejected
per-item option overrides.
**Grounding.** [DefinitionBaseExtension.cs](../../../src/Heddle/Core/DefinitionBaseExtension.cs)
(`InitStart`-only assignment, `>=` guard, `ThreadLocal` counter);
[TemplateOptions.cs](../../../src/Heddle/Data/TemplateOptions.cs) (default `100`; absent
from `Equals`/`GetHashCode`); phase 4 [D9/D10](../phase-4-ergonomics/README.md#design-decisions)
(what the dynamic cache key contains).

## Implementation plan

Ordered; milestone 1 = WI1–WI10 including the inserted WI5a and WI6a, milestone 2 =
WI11; WI12 closes docs.

**WI1 — Front-end seam (D4 step 1).**
Files: `src/Heddle/Language/ParserSettings.cs` (new),
`src/Heddle/Language/DocumentParser.cs` (core overload + adapter),
`src/Heddle/Language/HeddleMainListener.cs` (new ctor, `ImportReader` routing, error
redirect to `ParseContext.Errors`), `src/Heddle/Language/ParseContext.cs` (internal
`CreateDefinition` overload off `CompileContext`).
Check: full engine suite + goldens byte-identical; `PublicApiSurfaceTests` diff is only
the additive `ParserSettings`/overload; a unit test drives `Parse` with a custom
`ImportReader` and asserts an identical `ParseContext` versus the file-based path.

**WI2 — Generator project + packaging (D12).**
Files: `src/Heddle.Generator/Heddle.Generator.csproj`,
`src/Heddle.Generator/build/Heddle.Generator.props`/`.targets`, linked shared sources
(front-end closure: `DocumentParser.cs`, `HeddleMainListener.cs`, `ParseContext.cs`,
`DefinitionBlock.cs`, `DefinitionItem.cs`, `OutputChain.cs`, `OutputItem.cs`,
`RawOutputItem.cs`, `CallParameter.cs`, `HeddleToken.cs`, `HeddleSyntaxErrorListener.cs`,
`ParserSettings.cs`, `Strings/Core/BlockPosition.cs`,
`Data/HeddleCompileError.cs`/`HeddleCompileWarning.cs`, the exception types they
construct — closure completed mechanically), the shared `TemplateKey.cs` (WI7) and
`DefaultFunctionTable.cs` (WI5a), a `Templater.sln` entry.
Check: `dotnet pack` yields `analyzers/dotnet/cs` containing exactly
`Heddle.Generator.dll`, `Heddle.Language.dll`, `Antlr4.Runtime.Standard.dll`; a
packaging test asserts no `Microsoft.CodeAnalysis*` asset and no
`Heddle.csproj` reference.

**WI3 — Incremental pipeline (D16).**
Files: `src/Heddle.Generator/HeddleTemplateGenerator.cs` (`IIncrementalGenerator`),
`src/Heddle.Generator/Pipeline/TemplateInput.cs`, `GlobalConfig.cs`,
`GeneratedOutput.cs`, `ConfigReader.cs` (property/metadata parsing incl. `HED7009`;
`HeddleMaxRecursionCount` parses as a positive integer, D23).
Check: caching tests green — editing one template leaves other templates' `AddSource`
steps `Cached`; a reflection test asserts all pipeline models are value-equatable with
no Roslyn types.

**WI4 — Emitter core: pieces + member paths + unnamed output (D5, D15).**
Files: `src/Heddle.Generator/Emit/TemplateEmitter.cs`, `Emit/PieceWriter.cs` (the single
`EmitPiece` hook), `Emit/MemberPathWriter.cs`, `Emit/LineMapper.cs` (`#line`
span/classic tiers); runtime counterpart `src/Heddle/Precompiled/PrecompiledRuntime.cs`
(`Bind`, `GenerateString`, `WithLocalsFrame`, `RootModel`) + `IProcessStrategy` made
public.
Check: differential slice 1 (static text + member paths) byte-identical; snapshot
goldens for
[generated-code.md example 1](generated-code.md#example-1--static-text-and-typed-member-paths);
the `HED7005` lone-surrogate fixture warns and suppresses the u8 twin.

**WI5 — Emitter: native expressions, embedded C#, `:: dynamic`.**
Files: `Emit/NativeExpressionWriter.cs` (phase 1 `ExprNode` → C#, 1:1; `CallNode`
against the D21 merged function table — shim calls for default-bound names, direct
exporting-type calls for export-bound names, forwarder groups for merged sets),
`Emit/CSharpVerbatimWriter.cs` (verbatim paste + `@using` imports),
`Emit/DynamicWriter.cs` (C# `dynamic` access).
Check: differential slices for expression/C#/dynamic fixtures; a
`MemberPathsOnly`-mode fixture fails with the emitter's reimplemented `HED1014` at the
expression span (D22), never emitted C#; a non-`FullCSharp` embedded-C# fixture fails
with the `HED7012`-wrapped C#-tier gate message, never emitted C#.

**WI5a — Function binding: export discovery, shim, default table, marker entries (D21).**
Files: `src/Heddle.Generator/Binding/FunctionExportScanner.cs` (the D21/D16
`"Heddle.FunctionExports"` discovery stage: `[ExportFunctions]` probe over
`Compilation.Assembly` + `ReferencedAssemblySymbols`, D24/D12 table construction over
`IMethodSymbol`s — eligibility filter, lowercase-invariant name derivation,
replace-on-exact-signature, pinned processing order — projected into the
value-equatable export table), `src/Heddle/Precompiled/PrecompiledFunctions.cs` (the
35 shim overloads delegating to `BuiltInFunctions`),
`src/Heddle/Precompiled/DefaultFunctionTable.cs` (shared-sourced into the generator —
joins WI2's linked-source list), `Emit/FunctionCallWriter.cs` (merged-table
resolution, shim/direct/forwarder-group emission, `HED7014` + marker-entry production,
the re-emitted `HED1015`/`HED4001` literal checks with the replaced-name exemption),
`src/Heddle.Tests/DefaultFunctionLockstepTests.cs` (the D21 lockstep asserts),
`src/Heddle.Generator.Tests/FunctionExportDiscoveryTests.cs` (the ranking-reproduction
gate: for a fixture export assembly, the generator's discovered table equals the table
`FunctionRegistry.RegisterFrom` builds at runtime for the same assembly — name set,
per-name overload signatures, replace outcomes, processing-order tie-breaks — compared
via the internal `EnumerateOverloads()`; includes the case-collapse, exact-signature
default replacement, and cross-assembly collision rows).
Check: lockstep test green (table ↔ `FunctionRegistry.EnumerateOverloads()` both ways;
shim ↔ table 1:1); discovery test green (generator table == `RegisterFrom` table);
differential green over `expr-functions.heddle` (all 18 names exercised — the
overload-equivalence gate) **and** over `expr-functions-exported.heddle` (a fixture
template calling a function exported by a test-only `[ExportFunctions]` assembly,
rendered byte-identically with the runtime backend registering the same assembly via
`RegisterFrom` — the direct-binding parity gate); the worked
[example 7](generated-code.md#example-7--function-calls-shim-bound-built-ins-directly-bound-exports-and-the-unresolvable-negative-oq1--d21)
snapshot golden (shim call + direct export call); a fixture calling a name resolvable
from neither the default table nor any referenced export produces `HED7014` at the
call span, no generated entry class, and a manifest marker entry with the pinned
null-target `FunctionBindings` row.

**WI6 — Emitter: definitions, imports, overrides, props, branches, extension binding.**
Files: `Emit/DefinitionResolver.cs` (document-order layering exactly as
`HeddleCompiler`), `Emit/PropsWriter.cs` (the phase 5 D7 representation swap;
milestone-1 syntactic subset per D22), `Emit/BranchWriter.cs` (bound
`If`/`Elif`/`Else` instances), `Emit/ExtensionBinder.cs` (`IAssemblySymbol` scan,
`HED7006`; the `HED7015` `InitStart`/`CompleteInit`-override refusal, D22); runtime
counterpart: the `BindDefinition` (incl. the D23 `maxRecursionCount` parameter and
carrier-limit assignment) and `Prop` members of
`src/Heddle/Precompiled/PrecompiledRuntime.cs` (+
`PrecompiledPropSetter`/`PrecompiledPropEvaluator` in the same folder).
Check: differential green over composition (`layout.heddle` + `home.heddle`
`<body:body>` override), props, and branch fixtures; a test-only `[ExtensionName]`
assembly (no hook overrides) renders identically; a test-only extension overriding
`InitStart` raises `HED7015` at the call; the `dynamic-recursion.heddle` differential
fixture renders identically and a depth-limit test asserts
`TemplateProcessingException` at exactly the baked limit on both backends (default 100
and an overridden `HeddleMaxRecursionCount=3` build).

**WI6a — Document-shaping reimplementation (D22): strip/orphan, trim, directive
removal, profile flip.**
Files: `Emit/DocumentShaper.cs` (the emitter-side pipeline in the phase 4 D8 order:
skipped-token alignment → remnant-line trimming → definition/import removal with
widening → raw-output splice → strip/orphan machines → zero-output-chain removal with
widening), `Emit/BranchSetScanner.cs` (phase 3 D10 classification + the two machines;
`[ScopeChannel]` via symbols), `Emit/TrimWidener.cs` (the phase 4 D6 predicate,
verbatim), `Emit/ProfileTracker.cs` (phase 2 D4 document-order flip state).
Check: differential byte-identical against the runtime compile across the
strip/trim/profile corpus — `branching-flagship.heddle`,
`branching-interleaved.heddle`, `branching-nested.heddle`,
`branching-list-alternating.heddle`, `branching-out-projection.heddle` (strip/orphan);
`ergo-trim-preamble.heddle`, `ergo-import-library.heddle`,
`ergo-import-composition.heddle`, `ergo-import-inline.heddle` (trim + removal, run
under both `HeddleTrimDirectiveLines` values); `profile-flagship.heddle`,
`profile-directive.heddle` (mid-document flip); the phase 4 trim torture rows re-run
as inline differential cases in `Heddle.Generator.IntegrationTests`; the reimplemented
`HED3001`–`HED3004` fixtures report the same IDs and messages at `.heddle` spans.

**WI7 — Identity, manifest, attribute (D1, D6).**
Files: `src/Heddle/Precompiled/TemplateKey.cs` (shared-sourced into the generator),
`src/Heddle/Precompiled/HeddleCompiledTemplatesAttribute.cs`,
`IHeddleTemplateManifest.cs`, `PrecompiledTemplateInfo.cs` (+ the import/fingerprint/
extension-binding/function-binding/capabilities records — `FunctionBindings` as
`(name, target, overloadCount)` rows incl. the marker-entry null-target shape, D21);
generator side `Emit/ManifestEmitter.cs`
(`HED7002`–`HED7004`, `HED7010`, `HED7011`; marker entries for `HED7014` templates).
Check: `TemplateKeyNormalizationTests` table green (the cross-OS rows of
[identity-and-metadata.md](identity-and-metadata.md#normalization-test-table)); manifest
snapshot golden (incl. a populated `FunctionBindings` section and a marker entry);
duplicate-key/case-twin fixtures raise their diagnostics.

**WI8 — Runtime registry, gauntlet, policy, adapter (D2, D7, D8, D9).**
Files: `src/Heddle/Precompiled/PrecompiledTemplates.cs`,
`PrecompiledFallbackEvent.cs`/`PrecompiledFallbackReason.cs`,
`PrecompiledMismatchException.cs`, `PrecompiledRegistrationException.cs`;
`src/Heddle/Data/PrecompiledMismatchPolicy.cs`; `src/Heddle/Data/TemplateOptions.cs`
(policy property + copy ctor); `src/Heddle/HeddleTemplate.cs` (adapter ctor, `Configure`
calls `Register`); `src/Heddle/Runtime/TemplateResolver.cs` (consult before probe);
`src/Heddle/Runtime/TemplateFactory.cs` (internal `TryGetExtensionType`);
`src/Heddle/Extensions/PartialExtension.cs` (registry consult).
Check: gauntlet matrix green (options × functions × extensions × staleness ×
`Fallback`/`Strict`, incl. miss-under-`Strict` = dynamic, the marker-entry
`UnsupportedFunction` short-circuit, and the `FunctionBindingMismatch` rows —
replaced-`range`, added-overload, delegate-under-a-bound-name, and
export-bound-but-unregistered (`<missing>`, incl. the bare-`Default`-request case) —
with their pinned detail strings); duplicate
registration throws the D2 message verbatim; discovery enumeration test (incl.
`IsPrecompiled == false` for marker entries); inertness benchmark (D17) within error.

**WI9 — Differential harness + mixed-mode matrix (D19, D20).**
Files: `src/Heddle.Generator.IntegrationTests/` (generator consumed as analyzer, corpus
as `AdditionalFiles`; `DifferentialRenderTests`, `MixedModePrecedenceTests`,
`PartialRoutingTests` both directions, `DynamicTemplateParityTests`, u8-parity test).
Check: full-corpus differential byte-identical on all TFMs; precedence matrix green;
the `Strict`/`Fallback` scenario pair green with callback-reason asserts.

**WI10 — Startup proof + benchmarks.**
Files: `src/Heddle.Performance/PrecompiledStartupBenchmarks.cs`
(`FirstRender_Precompiled` vs `FirstRender_Dynamic` — the phase's raison-d'être metric,
time-to-first-render), `src/Heddle.Performance/PrecompiledRenderBenchmarks.cs`
(steady-state parity vs `RenderTemplateEngine`); `StartupProbeTests` hooking
`AppDomain.CurrentDomain.AssemblyLoad` — no `Microsoft.CodeAnalysis*`/ANTLR activity
rendering only precompiled templates; lazy load on the first dynamic compile.
Check: benchmark recorded (orders of magnitude on first render); probe green.

**WI11 — Milestone 2: `ISymbol` binding (D3).**
Files: `src/Heddle.Generator/Binding/SymbolModelBinder.cs` (`HED7007`),
`Binding/SymbolMemberResolver.cs` (tier-order member walk, `HED7008`); a final pipeline
stage combining `CompilationProvider`.
Check: seeded-mistake fixtures report `HED7007`/`HED7008` at template spans (no CSxxxx
for covered cases); differential unchanged; caching asserts still green.

**WI12 — CLI + docs.**
Files: `src/Heddle.Tool/` (dotnet tool `heddle render <template> --model-json <file>
[--out <file>] [--root <dir>]` hosting the full dynamic engine — the T4-successor
proof: invoked from an `Exec` build step, the output assembly carries generated sources
but no runtime Heddle dependency); docs: new published page `docs/precompilation.md`
(package setup, MSBuild options, typed entry points, mismatch policy, discovery API),
updates to `docs/csharp-api.md` and `docs/custom-extensions.md` (precompiled-mode
requirements: parameterless ctor, no registry-mutation reliance).
Check: codegen scenario (enum from JSON via `Exec` + `heddle render`) green; docs
build per the regression gate; polished samples remain phase 9 deliverables.

## Public API contract

All new types carry XML docs; every addition lands in the phase 6
`PublicApiSurfaceTests` goldens in the same change. New namespace `Heddle.Precompiled`
(assembly `Heddle`); the generated-code support API (`PrecompiledRuntime`,
`PrecompiledPropSetter`/`Evaluator`) is contracted in
[generated-code.md](generated-code.md#the-generated-support-api).

```csharp
namespace Heddle.Precompiled
{
    /// <summary>Assembly-level discovery marker emitted by the generator. One per assembly.</summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class HeddleCompiledTemplatesAttribute : Attribute
    {
        public HeddleCompiledTemplatesAttribute(Type manifestType, int schemaVersion, string engineVersion);
        public Type ManifestType { get; }  public int SchemaVersion { get; }  public string EngineVersion { get; }
    }

    /// <summary>Implemented by the generated manifest. Instantiated once per Register call.</summary>
    public interface IHeddleTemplateManifest { IReadOnlyList<PrecompiledTemplateInfo> GetTemplates(); }

    /// <summary>One precompiled template. Immutable; safe to share across threads.</summary>
    public sealed class PrecompiledTemplateInfo
    {
        public string Key { get; }
        public Type EntryPointType { get; }       // null iff !IsPrecompiled (fallback-marker entry, D21)
        public Type ModelType { get; }            // null when the template declares no model
        public bool IsDynamic { get; }
        public string ContentHash { get; }        // lowercase-hex SHA-256 of raw bytes
        public IReadOnlyList<PrecompiledImport> Imports { get; }
        public PrecompiledOptionsFingerprint OptionsFingerprint { get; }
        public IReadOnlyList<PrecompiledExtensionBinding> ExtensionBindings { get; }
        public IReadOnlyList<PrecompiledFunctionBinding> FunctionBindings { get; }  // D21; empty when no functions called
        public PrecompiledCapabilities Capabilities { get; }
        public IProcessStrategy Strategy { get; } // the generated root body; null iff !IsPrecompiled
        /// <summary>False for a fallback-marker entry (a build-degraded template, HED7014):
        /// the gauntlet short-circuits it with UnsupportedFunction and Strategy is null (D21).</summary>
        public bool IsPrecompiled { get; }        // == (Strategy != null)
    }

    public readonly struct PrecompiledImport
    { public string Key { get; } public string ContentHash { get; } }

    public readonly struct PrecompiledOptionsFingerprint
    { public OutputProfile Profile { get; } public ExpressionMode ExpressionMode { get; } public bool TrimDirectiveLines { get; } }

    public readonly struct PrecompiledExtensionBinding
    { public string Name { get; } public string ExtensionTypeName { get; } } // AQN sans version

    /// <summary>One called function name and its ACTUAL build-time binding target (D21):
    /// the discovered exporting type as AQN sans version (e.g.
    /// "Acme.Web.TemplateFunctions, Acme.Web"), or the shim's forwarding-target type
    /// ("Heddle.Runtime.Expressions.BuiltInFunctions, Heddle") for shim-bound defaults.
    /// One row per distinct (name, target) pair — a merged overload set spanning targets
    /// carries one row per target. OverloadCount is the merged table's per-target
    /// overload count for the name at build (0 on a null-target row). A null target marks
    /// a name resolvable from neither the default table nor any referenced export
    /// (delegate-only remainder) and appears only on fallback-marker entries.</summary>
    public readonly struct PrecompiledFunctionBinding
    { public string Name { get; } public string TargetTypeName { get; } public int OverloadCount { get; } } // null target = unresolvable at build

    [Flags]
    public enum PrecompiledCapabilities { None = 0, StringOutput = 1, Utf8Pieces = 2 }

    /// <summary>Process-wide precompiled registry. Registration is lock-guarded with
    /// copy-on-write publication; lookups are lock-free volatile reads.</summary>
    public static class PrecompiledTemplates
    {
        /// <summary>Attribute read + schema/engine gate + transactional entry registration.
        /// Idempotent per assembly; thread-safe; repeatable.</summary>
        public static void Register(Assembly assembly);
        public static IReadOnlyCollection<PrecompiledTemplateInfo> Entries { get; }
        public static bool TryGet(string key, out PrecompiledTemplateInfo entry); // normalizes, then ordinal lookup
        /// <summary>Per-request fallback/diagnostic callback (HED71xx). Invoked outside locks.</summary>
        public static Action<PrecompiledFallbackEvent> OnFallback { get; set; }
        /// <summary>Integration-supplied binding matcher; null = AQN-sans-version default (D9).</summary>
        public static Func<PrecompiledExtensionBinding, Type, bool> BindingResolver { get; set; }
    }

    public readonly struct PrecompiledFallbackEvent
    {
        public string Key { get; }
        public PrecompiledFallbackReason Reason { get; }
        public string Detail { get; }        // e.g. "OutputProfile: manifest=Text request=Html"
        public string DiagnosticId { get; }  // HED7101 / HED7102 / HED7103
    }

    public enum PrecompiledFallbackReason
    { SchemaVersionUnsupported, EngineVersionIncompatible,
      UnsupportedFunction,                            // gauntlet step 0 — marker entries (D21)
      OptionsMismatch, ExtensionBindingMismatch,
      FunctionBindingMismatch,                        // gauntlet step 3 (D21)
      StaleContent, StaleImport, CaseMismatch }

    // PrecompiledRuntime (Bind, BindDefinition — incl. the D23 maxRecursionCount
    // parameter — WithLocalsFrame, Prop, RootModel, GenerateString, ResolvePartial),
    // PrecompiledFunctions (the D21 built-in shim: 18 names, 35 overloads, delegating
    // to the internal BuiltInFunctions — discovered [ExportFunctions] exports bind
    // directly to their public containers and need no engine surface), and
    // PrecompiledPropSetter/Evaluator are contracted in generated-code.md (link above).

    /// <summary>The shared key-normalization rule (D1). Pure; identical at emit and lookup time.</summary>
    public static class TemplateKey
    {
        public static string Normalize(string relativePath);   // ArgumentException on '..'/empty result
        public static bool TryNormalize(string relativePath, out string key);
    }

    /// <summary>Thrown by PrecompiledTemplates.Register when a staged key ordinally equals an
    /// already-registered key (D2). Transactional: nothing from the staged manifest was published.</summary>
    public class PrecompiledRegistrationException : Exception
    {
        public PrecompiledRegistrationException(string key, string existingAssemblyName, string newAssemblyName);
        public string Key { get; }                    // the colliding, normalized key
        public string ExistingAssemblyName { get; }   // the assembly whose entry is already registered
        public string NewAssemblyName { get; }        // the assembly whose registration was rejected
        // Message: the D2 text verbatim, with the three values substituted.
    }

    /// <summary>Thrown on a per-request gauntlet failure under PrecompiledMismatchPolicy.Strict (D8).
    /// A registry miss never throws — strictness polices divergence, not coverage.</summary>
    public class PrecompiledMismatchException : Exception
    {
        public PrecompiledMismatchException(string key, PrecompiledFallbackReason reason, string detail);
        public string Key { get; }
        public PrecompiledFallbackReason Reason { get; }
        /// <summary>The pinned per-reason format string
        /// (identity-and-metadata.md § reason and detail strings).</summary>
        public string Detail { get; }
        // Message: "Precompiled template '{key}' failed validation ({reason}): {detail}."
    }
}

namespace Heddle.Data
{
    public enum PrecompiledMismatchPolicy { Fallback = 0, Strict = 1 }

    public partial class TemplateOptions
    {
        /// <summary>Fallback (default): dynamic recompile + HED7101 warning. Strict: throw.
        /// Copied by the copy ctor; not in Equals/GetHashCode (never changes output bytes).
        /// A registry miss is unaffected by this policy.</summary>
        public PrecompiledMismatchPolicy PrecompiledMismatchPolicy { get; set; }
    }
}

namespace Heddle.Runtime
{
    public interface IProcessStrategy   // promoted from internal; members unchanged
    { string Execute(in Scope scope); void Render(in Scope scope); }
}

namespace Heddle.Language
{
    /// <summary>Front-end inputs decoupled from CompileContext (D4).</summary>
    public sealed class ParserSettings
    {
        public string RootPath { get; set; }
        public bool ProvideLanguageFeatures { get; set; }
        /// <summary>Resolves @&lt;&lt; import content by root-relative path;
        /// null = read from RootPath on disk (current behavior).</summary>
        public Func<string, string> ImportReader { get; set; }
    }

    public static partial class DocumentParser
    {
        public static ParseContext Parse(string document, ParserSettings settings, out string cleanDocument);
        // existing Parse(string, CompileContext, out string) unchanged — now an adapter
    }
}
```

Generated-code surface (in the *consumer's* assembly, normative per
[generated-code.md](generated-code.md)): one public static class per template with the
D11 `Generate` entry points; manifest and strategy classes are non-public scaffolding.
Thread safety: registry mutation lock-guarded, reads lock-free;
`PrecompiledTemplateInfo` and everything it holds immutable; generated strategy and
extension instances hold no per-render state (the existing extension contract);
`ResolvePartial` memoizes via `LazyInitializer`; `OnFallback`/`BindingResolver` are
read into locals before use and invoked outside locks.

## Diagnostics

Build-time IDs are Roslyn `DiagnosticDescriptor`s (category `Heddle.Precompile`,
`isEnabledByDefault: true`), located at the `.heddle` position where one exists (D13);
runtime IDs surface via `OnFallback` and, for `HED7101`, additionally as a
`HeddleCompileWarning` on the fallback recompile's result.

| ID | Severity | Trigger | Message (exact) |
| --- | --- | --- | --- |
| HED7001 | Error | An `AdditionalFiles` `.heddle` source cannot be read (IO/decoding failure at generation time) | `Heddle template '{path}' could not be read: {reason}.` |
| HED7002 | Error | Two templates in one compilation normalize to the same key (position: the second file) | `Duplicate Heddle template key '{key}': '{path1}' and '{path2}' normalize to the same key. Set an explicit Key metadata on one, or exclude it from pre-compilation.` |
| HED7003 | Warning | Two keys in one compilation differ only by case | `Heddle template keys '{key1}' and '{key2}' differ only by case. Keys are case-sensitive; on case-insensitive file systems these files cannot coexist, and case-sloppy lookups will miss both.` |
| HED7004 | Error | Explicit `Key` metadata is empty/whitespace, contains a `.`/`..` segment, or normalizes to an empty string | `Invalid Key metadata '{value}' on Heddle template '{path}': keys must be non-empty relative paths without '.' or '..' segments.` |
| HED7005 | Warning | A static piece contains an unpaired surrogate; the u8 twin is suppressed for the template (string output unaffected) | `Static text in '{path}' contains an unpaired surrogate; UTF-8 pre-encoded pieces are disabled for this template. Byte-sink renders will transcode at run time.` |
| HED7006 | Error | An extension name does not resolve against any referenced assembly's `[ExtensionName]` types (position: the call) | `Cannot find extension <{name}> in the referenced assemblies. Reference the assembly that defines it, or correct the name.` |
| HED7007 | Error (M2) | The `@model`/`::` type name does not resolve in the compilation (position: the directive) | `Model type '{typeName}' is not defined in this compilation or its references.` |
| HED7008 | Error (M2) | A member path does not resolve on the model type, mirroring the runtime tier order (position: the path segment) | `'{typeName}' does not contain an accessible member '{member}' (member path '{path}').` |
| HED7009 | Error | An MSBuild option value is unparsable (`HeddleOutputProfile`, `HeddleExpressionMode`, `HeddleTrimDirectiveLines`, `HeddleEmitUtf8Pieces`) or, for `HeddleMaxRecursionCount`, unparsable or non-positive (D23) | `Invalid value '{value}' for {property}. Allowed: {allowed}.` |
| HED7010 | Error | Two keys sanitize to one generated class identifier | `Heddle templates '{key1}' and '{key2}' both generate entry class '{className}'. Set a Name metadata on one to disambiguate.` |
| HED7011 | Error | An `@<<` import path is not among the compilation's `.heddle` `AdditionalFiles` (position: the import) | `Import '{path}' is not included in this compilation. Add it as a <HeddleTemplate> item (use Precompile="false" for import-only files).` |
| HED7012 | Error | A forwarded front-end — or emitter-reimplemented back-end (D22) — `HeddleCompileError` carrying no `DiagnosticId` | `Heddle template error: {message}` |
| HED7013 | Warning | A forwarded front-end — or emitter-reimplemented back-end (D22) — `HeddleCompileWarning` carrying no `DiagnosticId` | `Heddle template warning: {message}` |
| HED7014 | Warning | A template calls a function name resolvable from neither the default built-in set nor any referenced assembly's `[ExportFunctions]` exports — an expression-context `CallNode`, or a standalone call with a function-compatible parameter shape resolving to neither a build-visible extension, nor a default function, nor a discovered export (D21; position: the call) | `Function '{name}' is not a default built-in and is not exported by any referenced assembly ([assembly: ExportFunctions]). Only functions representable in assembly metadata can be bound at build time — delegate-only host registrations cannot — so this template is not precompiled: it will compile dynamically at run time (HED7101 under PrecompiledMismatchPolicy.Fallback; PrecompiledMismatchException under Strict). If '{name}' is exported by an assembly, reference that assembly in this project and register it at run time with FunctionRegistry.RegisterFrom; if it is not registered by the host, the dynamic compile will fail with HED1001.` |
| HED7015 | Error | A bound extension type outside the engine assembly overrides `InitStart` or `CompleteInit` (D22; position: the call) | `Extension <{name}> ({typeName}) overrides {member}, which runs compile-time logic the generator cannot evaluate at build time; precompiled binding would silently skip it. Exclude this template from pre-compilation (Precompile="false" or <HeddleTemplate Remove="…" />), or keep the extension's compile-time behavior in the base implementation.` |
| HED7101 | Warning (runtime) | A precompiled entry exists but failed the gauntlet under `Fallback`; fired once per fallback recompile | `Precompiled template '{key}' was not used ({reason}: {detail}); recompiled dynamically.` |
| HED7102 | Warning (runtime) | A whole manifest is ignored at registration (schema/engine gate); one callback per manifest | `Precompiled manifest in assembly '{assembly}' ignored: {reason}. All its templates use the dynamic path.` |
| HED7103 | Warning (runtime) | A lookup missed ordinally but would hit case-insensitively (shadow index, D1) | `No precompiled template for '{requested}', but '{actual}' differs only by case. Keys are case-sensitive; use the exact file casing.` |

Front-end diagnostics forwarded by the generator keep their own `HED` IDs and
severities (D13), and back-end conditions the emitter reimplements keep their engine
IDs too (D22): `HED1014`, `HED1015`, `HED2001`, `HED2002`, `HED3001`–`HED3004`,
`HED4001`, and (milestone 2) `HED5007`–`HED5011` are re-emitted by the generator with
the engine's exact message texts at `.heddle` positions. Sandbox rule restated: an embedded-C# construct under a
non-`FullCSharp` build mode, and a native expression under `MemberPathsOnly`, are
rejected by the **emitter's reimplemented mode gates** (D22 — the engine enforces these
in its back end, `HeddleCompiler.CompileItem`, which the generator never runs):
`HED1014` for the native case, the ID-less C#-tier message wrapped as `HED7012` for the
embedded-C# case — never emitted, never executed. Function emission is equally
whitelist-shaped under the D21 direct-binding model: outside the embedded-C# tier,
generated code can call only the 18 `PrecompiledFunctions` shim methods and the public
static methods of `[ExportFunctions]` containers discovered in the compilation's
references — a template name never widens into an arbitrary method call, and an
undiscoverable name degrades to `HED7014` + marker, never to emitted code.

### Position semantics (pinned per ID)

Build-time diagnostics attach to `.heddle` files through Roslyn's external-file location
factory — `Location.Create(path, textSpan, lineSpan)` — so IDEs and build logs point at
the template, never at generated C#. "Zero-width at (1,1)" below means an empty span at
the file start: the condition is file-level, and an empty span keeps IDE squiggles from
painting the whole file. Where two files participate, the diagnostic sits on the
**later** one in the generator's deterministic input order (ordinal by normalized key),
so the "first" occurrence stays clean and the report is stable across builds.

| ID | Location |
| --- | --- |
| HED7001 | The unreadable file, zero-width at (1,1) — the content is unavailable, so no finer position exists. |
| HED7002 | The later of the two colliding templates, zero-width at (1,1); the message names both paths. |
| HED7003 | The later of the case-twin pair, zero-width at (1,1); the message names both keys. |
| HED7004 | The template whose `Key` metadata is invalid, zero-width at (1,1) — the metadata lives in the project file, not in the template. |
| HED7005 | The exact line/column of the **first** unpaired surrogate UTF-16 unit in the static piece. |
| HED7006 | The extension call — from `@` through the closing `)` of the call's parameter list. |
| HED7007 | The type-name span inside `@model(){{…}}` / the definition's `:: T`. |
| HED7008 | The failing member-path **segment**, not the whole path (`@(A.Bx.C)` squiggles `Bx`). |
| HED7009 | `Location.None` — an MSBuild property value has no source file; the message names the property. |
| HED7010 | The later of the two templates sanitizing to one class name, zero-width at (1,1); the message names both keys and the class. |
| HED7011 | The `@<<{{…}}` import block in the importing file. |
| HED7012 / HED7013 | The forwarded (or reimplemented, D22) error's/warning's `BlockPosition` mapped to its `.heddle` line/column; zero-width at (1,1) when no position was recorded. |
| HED7014 | The call — the function name through the closing `)` of its argument list (for a standalone call, the whole call block from `@`). |
| HED7015 | The extension call — from `@` through the closing `)` of the call's parameter list (the HED7006 rule; the message carries the type and the overridden member). |
| HED7101–HED7103 | No Roslyn location — runtime conditions surfaced as `PrecompiledFallbackEvent` callbacks; `HED7101` additionally becomes a `HeddleCompileWarning` on the fallback recompile's result with no position (a whole-template condition). |

## Testing plan

TDD verdict (carried from the roadmap, unweakened): **the emitter uses
differential-driven development** — every emitter feature starts by selecting the
corpus fixtures it must render byte-identically; the harness is the test, written first
by construction; the runtime path is authoritative (D20). Classic TDD applies to the
enumerable surfaces (gauntlet matrix, registry rules, key normalization, policy
semantics); `#line` mapping is fixture-first. Named assets:

| Asset | Home | Purpose |
| --- | --- | --- |
| `DifferentialRenderTests` | Heddle.Generator.IntegrationTests | The backbone: every corpus fixture generated + rendered on both backends, byte-compared. CI-resident from day one. |
| `TemplateKeyNormalizationTests` | Heddle.Tests | Table-driven cross-OS round-trip: Windows-shaped and Linux-shaped inputs on every OS (pure-function, OS-independent by construction) — the [normalization table](identity-and-metadata.md#normalization-test-table) is part of the spec. |
| `PrecompiledGauntletTests` | Heddle.Tests | The mismatch matrix: {options, function binding (replaced `range`, added overload, delegate under a bound name, export bound at build but missing from the live registry — incl. the bare-`Default`-request case — and marker-entry `UnsupportedFunction`), extension binding, stale content, stale import, schema gate, engine gate} × {`Fallback`, `Strict`} + miss-under-`Strict`; asserts callback reason, the [pinned detail strings](identity-and-metadata.md#reason-and-detail-strings-pinned) verbatim, `HED7101` on the recompile result, and exception type/message under `Strict`. |
| `DefaultFunctionLockstepTests` | Heddle.Tests | The D21 lockstep gate: `DefaultFunctionTable` rows ↔ `FunctionRegistry.Default` via the internal `EnumerateOverloads()` (set-equality both ways over name/parameter types/return type) and `PrecompiledFunctions` shim methods ↔ table rows 1:1 (public static, matching signature). A built-in added by a later phase without a table+shim update is a red build. |
| `FunctionExportDiscoveryTests` | Heddle.Generator.Tests | The D21 ranking-reproduction gate: for the fixture `[ExportFunctions]` assembly, the generator's discovered merged table equals the table `FunctionRegistry.RegisterFrom` builds for the same assembly (via `EnumerateOverloads()`) — name derivation incl. case collapse, D12 eligibility filtering, exact-signature replacement of a default, overload union, and the pinned processing order; the `expr-functions-exported.heddle` differential fixture (WI5a) is the behavioral half of the same gate. |
| `PrecompiledRegistryTests` | Heddle.Tests | Registration idempotence, transactional duplicate failure (D2 message verbatim), discovery enumeration, case-shadow `HED7103`, concurrency (parallel `Register` + `TryGet`). |
| `MixedModePrecedenceTests` / `PartialRoutingTests` | Heddle.Generator.IntegrationTests | The D17 matrix; partial routing both directions byte-identical to all-dynamic. |
| `GeneratorSnapshotTests` / `GeneratorCachingTests` / `GeneratorDiagnosticTests` | Heddle.Generator.Tests | Verify.SourceGenerators goldens for every worked example; step-tracking run-reason asserts; diagnostic ID + span asserts (incl. milestone 1 remapped-CS1061 location fixtures, replaced by `HED7008` asserts in milestone 2). |
| `StartupProbeTests` | Heddle.Generator.IntegrationTests | AssemblyLoad-event probe: zero `Microsoft.CodeAnalysis*`/ANTLR for precompiled-only rendering; lazy load on first dynamic compile. |
| `PrecompiledStartupBenchmarks` | Heddle.Performance | **The phase's headline metric**: `FirstRender_Precompiled` vs `FirstRender_Dynamic` time-to-first-render. |
| `PrecompiledRenderBenchmarks` + resolver inertness benchmark | Heddle.Performance | Steady-state parity vs `RenderTemplateEngine`; the D17 zero-manifest inertness gate. |
| `Utf8PieceParityTests` | Heddle.Generator.IntegrationTests | Compiler-encoded u8 bytes == `Encoding.UTF8.GetBytes` per corpus piece; the lone-surrogate fixture (string parity + `HED7005`). |

Regression gate: the standard combined run
([testing standards](../common/testing-standards.md#regression-gates)) — full suite all
TFMs, goldens byte-identical, grammar-stability check (no-grammar phase:
`src/Heddle.Language/generated/` shows no diff), benchmarks on touched paths, docs
build — plus this phase's standing additions closing every verification-loop iteration:
the differential run, the inertness gate, the startup probe. Phase exit = roadmap
success criteria 1–8 green in one combined run. Deferred phase 9 gallery items:
`samples/precompiled-app` and `samples/codegen-t4-successor`, both golden-asserted, the
precompiled sample's output equal to its dynamic twin.

## Back-compat and migration

- **Purely additive.** New package, new namespace, generated code in consumer
  assemblies, a registry consulted before the existing path. With zero manifests the
  resolver is provably inert (D17); the existing suite passes unchanged in a project
  that also installs the generator (success criterion 7).
- **Public surface**: all additions enter the phase 6 `PublicApiSurfaceTests` goldens
  with review; no existing signature changes; `IProcessStrategy`'s promotion is
  additive. The **front-end seam (WI1)** is observable-behavior-neutral, gated by the
  full suite + goldens before any generator work starts.
- **`TemplateOptions.PrecompiledMismatchPolicy`** joins the copy ctor (the completeness
  test covers it automatically) and stays out of identity — no dynamic cache-key change.
- **Function registrations** (D21): declaratively exported functions
  (`[assembly: ExportFunctions]` + `RegisterFrom`, phase 6 D24) precompile by direct
  binding when the build references the exporting assembly — the parity rule is the
  integration's (build references = runtime registrations), and every violation is a
  per-template, per-name detected divergence (`FunctionBindingMismatch`: a replaced or
  extended default, a delegate under a bound name, a bound export missing from the
  live registry) — never a silent split between backends. Delegate-only registrations
  remain un-precompilable: their call sites draw build warning `HED7014` + a marker
  entry and keep today's dynamic behavior byte-identically. Hosts that never touch
  `Functions` and templates that bind only defaults see the frozen-`Default` fast
  path — zero checks.
- **`MaxRecursionCount` divergence note** (D23): precompiled definition carriers use
  the build's `HeddleMaxRecursionCount`; a host whose runtime
  `TemplateOptions.MaxRecursionCount` differs renders precompiled templates with the
  **build** value. This mirrors the dynamic path's existing exposure — the resolver
  cache excludes the option, so a cached dynamic compile already serves its
  first-compile limit to every later request. Remedy when the limits must agree: set
  `HeddleMaxRecursionCount` to the host's runtime value (documented in
  `docs/precompilation.md`, WI12).
- **Precedence surprise, documented loudly** (WI12): a precompiled template edited on
  disk *without* `EnableFileChangeCheck` renders the precompiled version; `ContentHash`
  keeps the staleness detectable/loggable even then.
- **2.0 window**: nothing here breaks; the generator's MSBuild defaults (`Text`/`false`)
  flip together with the engine defaults per
  [cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window),
  recorded in the props file next to each default.
- **Semantic drift** is the permanent exposure, not breakage: every future language
  change lands in both backends, gated by the CI-resident differential harness (D20).

## Performance considerations

- **Startup is the product**: `PrecompiledStartupBenchmarks` measures
  time-to-first-render precompiled vs dynamic (expected orders of magnitude — no ANTLR,
  no expression trees, no Roslyn); `StartupProbeTests` proves `Microsoft.CodeAnalysis*`
  never loads.
- **Render path**: generated code renders through the same extension instances and
  `Scope`/`ScopeRenderer` machinery — no new per-render allocations
  (`GenerateString` mirrors `HeddleTemplate.Generate`'s high-water-mark buffer).
  `PrecompiledRenderBenchmarks` guards parity; the roadmap's .NET 10 JIT de-abstraction
  note (concrete statics, direct property access, sealed instances) is expected upside,
  asserted only as "not worse than dynamic".
- **Lookup path**: one normalized-key computation + one dictionary probe per
  `GetTemplate`/partial resolution (not the render path); zero-manifest inertness
  benchmarked. The D21 function-binding check costs zero when `options.Functions` is
  null and every binding row targets `BuiltInFunctions` (the frozen-`Default` fast
  pass) and otherwise one registry enumeration per distinct called name per gauntlet
  run — never on the render path. Bound function calls themselves — shim, direct
  export, or forwarder-group hop — are direct static invocations, at worst equal to
  the runtime tier's bound `Expression.Call` (differential-benchmarked under
  `PrecompiledRenderBenchmarks`).
- **Generator throughput**: incremental caching per D16; the caching tests are the
  guard. Generation cost follows "compile once, render many" — moderate allocation is
  acceptable; regressions are what the step-tracking asserts catch.
- **u8 pieces** (when enabled): transcoding moves to the C# compiler — zero startup
  cost, zero GC objects for byte-sink writes; dormant in v1 (D15).

## Standards compliance

**DRY as knowledge-sharing** is the spine — the parse front end (shared sources, D4),
the key rule (`TemplateKey`, one file in two projects, D1), extension semantics
(bound instances, never reimplemented, D5/D9), and function semantics (phase 6 D24's
`[ExportFunctions]` contract as the **one** discovery source with three readers —
host `RegisterFrom`, editor scan, generator — direct binding to the exported
containers themselves, the `PrecompiledFunctions` shim delegating to the one internal
`BuiltInFunctions` implementation, and `DefaultFunctionTable` as the one shared
built-in signature source, D21)
each exist exactly once because divergence there is a correctness bug — precisely the
"two call sites must never diverge semantically" test. The **sanctioned exception** is
D22's reimplementation surface: the back-end document-shaping machines cannot be
shared (private, reflection-coupled), so they are reproduced under the differential
gate — DRY traded away knowingly, with D20's harness as the permanent drift alarm and
the refusal rule (`HED7015`) fencing off what cannot even be reproduced. **Open/closed at ratified seams only**: the key-policy seam,
`BindingResolver`, and `OnFallback` are the roadmap-ratified open points; internals with
one implementation (emitter, gauntlet) stay concrete. **YAGNI cuts**: no replace flag
(D2), no sink overloads (phase 8), no per-item option overrides, no interceptors, no
MSBuild-task fallback (D10) — each with a recorded trigger. **Abstraction warranted by
a second consumer**: `IProcessStrategy` goes public exactly because generated code is
its second implementor; `ParserSettings` exists exactly because the generator is the
front end's second host. Precedence rule 1 drove D1's case-sensitivity call and D15's
warning-not-error correction over stricter alternatives.

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Precompiled `Replace` flag (explicit layering across manifests) | A concrete base-plus-customization deployment with a real user (D2; schema-version bump reserved). |
| Precompilation of delegate-backed function registrations (`Register(string, Delegate)` — the only remainder after the OQ1 resolution made `MethodInfo`-representable exports directly bound, D21) | A host with functions that genuinely cannot be expressed as `[ExportFunctions]` public static exports (captured state, factory-composed closures) *and* that needs those templates precompiled; the remedy documented today (`docs/precompilation.md`, WI12) is a declaratively exported static wrapper — the export path `HED7014`'s message names. |
| `TextWriter`/`IBufferWriter<byte>` entry points; u8 default-on | Phase 8's sink API — added at the `PrecompiledRuntime` funnel; the emitter hook is already in place (D15). |
| Moving the front end into `Heddle.Language` (`TypeForwardedTo`) | A second engine-external front-end consumer (e.g. an engine-free LSP parse mode) making shared sources unwieldy (D4). |
| Per-item MSBuild option overrides (`OutputProfile` per template) | A project that genuinely mixes profiles across its own precompiled templates (D14). |
| Fully-typed props carriage in generated bodies (fields instead of the `object[]` contract) | A differential-proven benchmark win over the shared-prototype path; would fork the phase 5 funnel semantics, so it needs its own harness slice (D5, [generated-code.md example 5](generated-code.md#example-5--definition-with-phase-5-props)). |
| Interceptor-based migration of runtime call sites to typed entry points | Post-v1 demand for zero-diff migration; requires raising the `Microsoft.CodeAnalysis` floor to 4.11+ (D11/D12). |
| MSBuild-task driver for generator-hostile builds | A real such environment with a real user attached (D10). |
| AOT-slimmed runtime package | Concrete repeated demand; never as constraints on `Heddle`/`Heddle.Language` (D18). |
| Roadmap-listed independent-shipping fallback (member paths without phase 1) | Not applicable under sequential order — phase 1 is merged; the emitter's `ExprNode` mapping assumes it. |
| `samples/precompiled-app`, `samples/codegen-t4-successor` | Phase 9 gallery harness (each golden-asserted; the differential rule enforced at gallery level). |

## External references

- [Source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
  (Microsoft Learn) — generators introspect user code through compilation/symbol APIs,
  never by loading it (D3's grounding correction, reproduced).
- [Incremental generators design doc](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
  and [source generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
  (dotnet/roslyn) — pipeline/caching rules, `AdditionalTextsProvider`,
  `CompilerVisibleProperty`/`CompilerVisibleItemMetadata` →
  `build_property.*`/`build_metadata.AdditionalFiles.*`, and the `analyzers/dotnet/cs`
  packaging + dependency-packing recipe (D12, D14, D16).
- [Supported Roslyn package version mappings](https://learn.microsoft.com/en-us/visualstudio/extensibility/roslyn-version-support)
  (Microsoft Learn) — 4.4.0 ↔ VS 2022 17.4; 5.0.0 ↔ VS 2026 18.0 (D12's floor; fetched
  directly after web search surfaced the page without its table) — and
  [RS1041](https://github.com/dotnet/roslyn/blob/main/docs/roslyn-analyzers/rules/RS1041.md)
  (compiler extensions target netstandard2.0).
- Andrew Lock — [creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/),
  [snapshot testing](https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/),
  [performance pitfalls](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/),
  [MSBuild settings](https://andrewlock.net/creating-a-source-generator-part-13-providing-and-accessing-msbuild-settings-in-source-generators/)
  (D12, D16, D19); [Verify.SourceGenerators](https://github.com/VerifyTests/Verify)
  (VerifyTests) — multi-file + diagnostics snapshots (D19).
- [UTF-8 string literals](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types#utf-8-string-literals)
  (Microsoft Learn) and
  [endjin — C# 11 UTF-8 string literals](https://endjin.com/blog/2023/02/dotnet-csharp-11-utf8-string-literals)
  — `"…"u8` is `ReadOnlySpan<byte>` over compiler-embedded assembly data; not `const`;
  unpaired surrogates are compile errors (D15).
- [`#line` preprocessor directives](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/preprocessor-directives#error-and-warning-information)
  (Microsoft Learn) — the span form designed for DSL remapping; `#line hidden` (D3, D5) —
  and the [C# 10 enhanced `#line` proposal](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-10.0/enhanced-line-directives.md)
  (dotnet/csharplang) — the operand semantics the mapping rules pin: 1-based UTF-16
  line/character positions, `charOffset` = the unmapped prefix length on the following
  generated line, default 0 (generated-code.md § `#line` mapping rules).
- [`RazorCompiledItemAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.razor.hosting.razorcompileditemattribute)
  / [`RazorCompiledItemLoader`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.razor.hosting.razorcompileditemloader)
  and [Razor file compilation in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/view-compilation)
  (Microsoft Learn) — the assembly-attribute discovery prior art (D6).
- [Native AOT limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)
  (Microsoft Learn) — the structural facts behind the ratified non-goal (D18).
- [C# language specification — expressions: overload resolution and better function member](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions)
  (Microsoft Learn) — the betterness ranking (exact > implicit conversion; normal form
  preferred over expanded `params` form) behind D21's claim that consumer-compiler
  overload resolution over a closed candidate set (shim, exporting container, or
  forwarder group) reproduces the phase 1 D12 rank.
- [Compilation.GetAssemblyOrModuleSymbol(MetadataReference)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.compilation.getassemblyormodulesymbol)
  and [IModuleSymbol.ReferencedAssemblySymbols](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.imodulesymbol.referencedassemblysymbols)
  (Microsoft Learn, fetched July 2026) — a compilation's metadata references surface as
  `IAssemblySymbol`s whose assembly-level attributes (`GetAttributes()`) the generator
  reads for D21's `[ExportFunctions]` discovery; both members exist since Roslyn 3.0,
  below D12's 4.4.0 floor.
