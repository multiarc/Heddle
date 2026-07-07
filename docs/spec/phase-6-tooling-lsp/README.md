# Phase 6 specification — editor tooling / LSP

Status: **Specified — ready for implementation.**
Roadmap source: [phase 6 — editor tooling / LSP](../../roadmap/phase-6-tooling-lsp.md).
Prior phases assumed merged
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
[phase 1 — native expressions](../phase-1-native-expressions/README.md),
[phase 2 — safe output](../phase-2-safe-output/README.md),
[phase 3 — branching](../phase-3-branching/README.md),
[phase 4 — ergonomics](../phase-4-ergonomics/README.md),
[phase 5 — props & slots](../phase-5-props-and-slots/README.md).

### Documents

| Document | Purpose |
| --- | --- |
| README.md (this file) | The spec proper: assumed state, decision records, implementation plan, API contract, diagnostics, testing, back-compat, performance. |
| [protocol.md](protocol.md) | The LSP protocol supplement: method-by-method behavior, DTO subset, position mapping, semantic-token legend, completion context detection, distribution mechanics, and the contract-test message fixtures. |

## Scope and goal

A Language Server Protocol implementation and a VS Code extension giving `.heddle` authors
typed member completion, live positioned diagnostics, hover types, semantic coloring, and
go-to-definition across imports — all as **projections of the real engine pipeline**
(`ProvideLanguageFeatures` token stream, `HeddleCompileError`/`Warning` with `HEDxxxx`
IDs, the compiler's `ExType` threading), never a re-implementation. Zero grammar change;
the engine gains only additive internal plumbing (scope-map retention, import-origin
marking) behind the existing `ProvideLanguageFeatures` flag.

Deliverables: (a) the `Heddle.LanguageServices` facade package (zero-breaking-change
extraction — nothing moves out of `Heddle`); (b) the facade API (document management,
debounced reanalysis, diagnostics, completion, hover, go-to-definition, semantic tokens);
(c) the `heddle-lsp` server executable — a thin hand-rolled LSP layer over StreamJsonRpc,
stdio transport; (d) the VS Code extension reusing the TextMate grammar, distributed per
the ratified security decision (explicit install only — never `dotnet tool exec`);
(e) the artificial-type completion rule for abstract definitions; (f) typed completion
driven by model types, phase 5 prop declarations, and the phase 1 function registry;
(g) the [OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)
host-registration knowledge sources — the one-shot assembly scan for extension and
declarative function exports (D23/D24), with template-local declarations ratified as
the second source (D24); (h) import-site anchoring of imported-file diagnostics (D25).
Out of scope with triggers ([Deferred items](#deferred-items)): rename, find references,
document symbols, pull diagnostics, incremental sync, semantic-token deltas, MSBuild
project evaluation, Native AOT, and the browser/WASM host (phase 9).

## Assumed state

Every seam below was **re-verified against the current source** (July 2026; searches
excluded `bin/`, `obj/`, `node_modules/`, `generated/`). Phase 1–5 artifacts are cited
from their merged specs; everything else is current-source fact. Corrections and
refinements to roadmap claims are marked ⚠ and closed in the decision records.

| Seam | Verified state |
| --- | --- |
| [TemplateOptions](../../../src/Heddle/Data/TemplateOptions.cs) | `ProvideLanguageFeatures` is a plain bool (default `false`); phase 2 fixed its copy-ctor omission and added the reflection completeness test. `RootPath` (default `AppContext.BaseDirectory`), `FileNamePostfix`, `FullPath => RootPath + TemplateName + FileNamePostfix`. Phase 1 added `ExpressionMode`/`Functions`; phase 2 `OutputProfile`; phase 4 `TrimDirectiveLines`. The facade constructs one options template per workspace from configuration (D18). |
| [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) | `Parse(string, CompileContext, out string)` is public static. Editor mode (`ProvideLanguageFeatures`) parses `LL_EXACT_AMBIG_DETECTION` directly (no SLL attempt); syntax errors land in `ParseContext.Errors` and are copied to `CompileContext.CompileErrors`; hidden-channel tokens are collected into `ParseContext.SkippedTokens` (`List<BlockPosition>` — position only, no kind). |
| [ParseContext](../../../src/Heddle/Language/ParseContext.cs) | Public surface: `Tokens` (`IEnumerable<HeddleToken>`), `SubContexts`, `SkippedTokens`, `Errors`, `Warnings`, `OutputChains`, `DefaultChains`, `DefinitionsBlock`. `AddToken` is internal and no-ops unless `ProvideLanguageFeatures`. The parse-coordinate offset `_offset` is private with no accessor — D2 adds the internal `AbsoluteOffset` property the scope map needs. Sub-bodies get child `ParseContext`s sharing the parent's `Errors`/`Warnings` lists. |
| [HeddleToken / HeddleTokenType](../../../src/Heddle/Language/HeddleToken.cs) | 20 pre-phase-1 values (`Id` = 0 through `ParseError` = 19 — count re-verified against the current enum); phase 1 appended `Operator`, `Literal`, `FunctionName` (23 total). `HeddleToken` carries type + `BlockPosition` only — no literal kind, no resolved-symbol data (drives the semantic-token mapping choices in [protocol.md](protocol.md#semantic-tokens)). |
| [BlockPosition](../../../src/Heddle/Strings/Core/BlockPosition.cs) / [LinePosition](../../../src/Heddle/Data/HeddleCompileResult.cs) | `BlockPosition` = `StartIndex` + `Length` in **UTF-16 code units** (.NET string indexes) — exactly LSP's default `utf-16` position encoding, so mapping is line-splitting only (D11). `HeddleCompileResult` computes `LinePosition` lazily via a line-offset binary search; ⚠ its line map mutates errors in place and is coupled to the result object — the facade builds its own immutable `LineMap` (D11) instead of reusing it. |
| [HeddleCompileError](../../../src/Heddle/Data/HeddleCompileError.cs) / [Warning](../../../src/Heddle/Data/HeddleCompileWarning.cs) | Phase 1 added `DiagnosticId` (`"HED1003"`-style, null for unassigned legacy messages). `HeddleCompileWarning.Fix` carries remediation text. ⚠ `HeddleCompileResult` exposes **errors only** (`ErrorList`); warnings live on `ParseContext.Warnings` / `CompileContext.CompileWarnings` and never reach the result object — the facade reads both lists directly (D10), a detail the roadmap's "diagnostics source" sentence glosses over. |
| [HeddleCompiler](../../../src/Heddle/Runtime/HeddleCompiler.cs) | `Compile(string document, CompileScope, ParseContext, ExType chainedType)` is public static (line 22). **Every body compile funnels through it**: `AbstractExtension.InitStart` → `InitSubTemplate` → `new CompileScope(new CompileContext(compileScope.CompileContext, dataType), …)` → `HeddleCompiler.Compile(parameterTemplate, newContext, bodyParseContext, chainedType)` ([AbstractExtension.cs](../../../src/Heddle/Core/AbstractExtension.cs), `InitSubTemplate`). The definition branch of `CreateExtension` compiles the caller body against `parseContext` and the definition body against `definition.Context`, with `dataType` = the pinned `acceptType` when it isn't `object`, otherwise the **caller's** type (lines 442–465). This single funnel is where the scope map records (D2). |
| [CompileContext](../../../src/Heddle/Runtime/CompileContext.cs) / [CompileScope](../../../src/Heddle/Runtime/CompileScope.cs) | `CompileErrors`/`CompileWarnings`/`ScopeType`/`RootScopeType`/`Options` public; the private copy ctor (line 73) shares the error/warning lists and `CompiledItems` across child compiles — the scope map rides the same route (D2). `CompileScope` pairs a `CompileContext` with the `CSharpContext` (namespaces, Roslyn method list). |
| [ContextCompilation](../../../src/Heddle/Runtime/ContextCompilation.cs) | The Roslyn pass is `internal static class ContextCompilation` with extension methods `Compile(this CompileContext)` / `Compile(this CompileScope)`. ⚠ `HeddleTemplate.TryCompilation` (simulate mode) **skips** this pass entirely — a host using `TryCompilation` for validation never sees C#-tier Roslyn errors. The facade therefore drives the pipeline directly (parse → compile → optional Roslyn) rather than through `HeddleTemplate` (D10), and gets to choose the Roslyn policy per document (D9). |
| [ExType](../../../src/Heddle/Data/ExType.cs) / [ReflectionHelper](../../../src/Heddle/Helpers/ReflectionHelper.cs) | `ExType` wraps `Type` + `IsDynamic`. `ReflectionHelper.ResolveType(name, imports)` resolves against **static** name maps (`_shortNames`/`_fullNames`) built by `Reconfigure()` from `AssemblyHelper.GetAssemblies()`. ⚠ `AssemblyHelper`'s assembly list is process-static, seeded from the entry assembly's `DependencyContext`, extendable only via the one-shot `Configure(Assembly)` — there is **no path for ALC-loaded model assemblies to become resolvable today**. D14 adds the internal register/unregister seam; the static caches holding `Type` refs are exactly the reload-leak hazard the roadmap flags. |
| `MemberPathResolver` (phase 1, `src/Heddle/Runtime/Expressions/MemberPathResolver.cs` per its [spec](../phase-1-native-expressions/README.md#d8--member-path-knowledge-single-sourced-the-dry-extraction)) | Owns the member visibility filter (instance/static, `CanRead`, `[Hidden]`, getter accessibility) extracted from `CompileModelAccessor`. Completion listing must apply the identical filter — D3 adds the internal `GetVisibleProperties(Type)` helper on it (the DRY move). |
| [TemplateFactory](../../../src/Heddle/Runtime/TemplateFactory.cs) | Static name→type registry, ordinal case-sensitive; multi-name aliases registered per `[ExtensionName]` attribute (phase 2's `raw` on `EmptyExtension`, phase 3's `elif`/`elseif` on `ElifExtension`, `else` on `ElseExtension`). Phase 1 added internal `Exists(string)` and noted "phase 6 can widen it" — D3 adds internal `RegisteredNames()`. The one-time static-ctor scan (`ObtainExtensions`) walks `AssemblyHelper.GetAssemblies()` for the **assembly-level** `[assembly: ExportExtensions]` attribute ([ExportExtensionsAttribute](../../../src/Heddle/Attributes/ExportExtensionsAttribute.cs): `AttributeTargets.Assembly`, `AllowMultiple`; parameterless ctor = `All`, type-array ctors = an explicit `Extensions` list) and registers `IExtension` implementations carrying `[ExtensionName]` (replacement ordering per `[ExtensionReplace]`); hosts can also register at runtime through the public `AddExtensions`/`LoadAddExtensionsFromAssembly`. ⚠ The registry is append-only for process lifetime — **no unregister API exists and the scan never re-runs** — the two facts D23's default-ALC pin and restart-to-rescan staleness rest on. |
| `FunctionRegistry` (phase 1, `src/Heddle/Runtime/Expressions/FunctionRegistry.cs` per its [spec](../phase-1-native-expressions/README.md#d12--functionregistry-semantics)) | Public API is `Default`, ctor, `Register`, `Contains`, `IsFrozen` — ⚠ **no enumeration exists** (phase 1 deferred it), yet completion needs names and signatures. D3 adds internal `EnumerateOverloads()`; the public enumeration API stays deferred. D24 adds the public `RegisterFrom(Assembly)` plus the assembly-level `[ExportFunctions]` attribute — the ledger-tracked amendments of phase 1's function surface. Phase 4 added `range(start, last[, step])` to the default set. |
| Import machinery | `HeddleMainListener.ExitImport_block` resolves `Path.Combine(Options.RootPath, path)` and parses the imported file synchronously into an isolated context whose definition set **starts as a copy** of the current one (`IsolateContextWithTree` copies every entry into the isolated `DefinitionsBlock`). The imported file's declarations then layer onto that copy under the ordinary declaration rules — ⚠ a plain same-name declaration is the compile error `"The definition <name> with the same name already exists"` (`EnterDef` keeps the existing entry and discards the new one); only the explicit override form (`<name:base>`, including full override `<name:name>`) replaces, via `OverrideWith`/direct replacement in `ExitDef`. The closing `Definitions.Clear()`-then-re-add is **mechanical registry replacement of the current set by that merged copy** — exactly [phase 4 D11](../phase-4-ergonomics/README.md#d11--two-imports-verified--composes-import-parses-into-an-unreachable-context)'s source-verified semantics (fixtures I01). ⚠ An earlier draft of this row read the `Clear()` as "imported definitions replace the current definition set"; the July 2026 gap analysis (G7) corrected it — D26 closes the correction and D16 is amended in place. Imported top-level chains splice at the import site as zero-length blocks. ⚠ The isolated context **shares** the importing context's `Errors`/`Warnings` lists (`ParseContext` ctor: `Errors = parentContext?.Errors ?? …`) and the imported parse writes into the same `CompileContext` — nothing in `ParseContext`/`CompileContext` entries discriminates an imported-parse diagnostic from an importing-document one, and its `BlockPosition`s are in the **imported** file's coordinate space (gap G6; D25 adds the marker). [PartialExtension](../../../src/Heddle/Extensions/PartialExtension.cs) treats its body text as a template name compiled via a delayed child compile (`FileReader` reads `Options.FullPath`) whose result errors `AddRange` into the importing compile the same coordinate-foreign way (`CompleteInit`). These are the go-to-definition file targets (D16). |
| Strong naming | Both library assemblies sign with [heddle.snk](../../../heddle.snk); the existing `InternalsVisibleTo` entries in [AssemblyInfo.cs](../../../src/Heddle/Properties/AssemblyInfo.cs) carry the full public key. `Heddle.LanguageServices` must sign with the same key and its IVT entry must include the key blob (D3). |
| TFMs / SDK | Engine libraries target `netstandard2.0;net6.0;net8.0;net10.0`; [global.json](../../../global.json) pins SDK `10.0.100` (`rollForward: latestMinor`) — the .NET 10 SDK the per-RID tool packaging (D19) requires is already the repo's baseline. |
| TextMate grammar | [heddle.tmLanguage.json](../../coloring-scheme/heddle.tmLanguage.json): `scopeName` `text.html.heddle`, `fileTypes: ["heddle"]`, HTML host + embedded C# — the extension copies it at build time (D21); VS Code layers semantic tokens on top, so TextMate remains the no-server fallback (platform behavior, nothing to build). |
| Phase 1 artifacts used | `HeddleTokenType.Operator/Literal/FunctionName`; the public `ExprNode` AST in `Heddle.Language.Expressions` — abstract `ExprNode` plus the eight sealed phase 1 nodes `BinaryNode`, `UnaryNode`, `TernaryNode`, `LiteralNode`, `PathNode`, `IndexNode`, `CallNode`, `MethodCallNode` and the `ExprOperator` enum (phase 5's `ThisNode` is the ninth node — next row) — reachable via `CallParameter.NativeExpression`; `HeddleDiagnosticIds`; `ExpressionMode`; the diagnostic-ID plumbing that phase mapped to LSP `Diagnostic.code` by design ([cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)). |
| Phase 5 artifacts used | `DefinitionItem.PropDeclarations`/`SlotTypeName`, `PropDeclaration` (name, type name, `HasDefault`, `DefaultValue`, position), `NamedArgument` on `CallParameter.PropArguments`, `ThisNode` — the typed prop completion source (D12). |
| Phases 2–4 artifacts used | Phase 2 `OutputProfile` (workspace setting → compile option, D18) and the `raw` alias; phase 3 branch extensions and orphan diagnostics (`HED3001`–`HED3004` arrive as ordinary positioned warnings/errors — nothing tooling-specific to add); phase 4 `range` in the default registry and the `HED4002` lint (its spec routes per-diagnostic severity configuration to this phase — recorded as deferred, D20 note). |

## Design decisions

Numbering is local to this spec; cross-cutting decisions are cited by link. Every item
the roadmap resolved for this phase (all pre-resolved July 2026), every lean, and every
pin-during-design item is carried below as a closed record with its verification.

### D1 — `Heddle.LanguageServices` is a new package referencing `Heddle`; nothing moves

**Decision.** The facade ships as a new NuGet package `Heddle.LanguageServices` (project
`src/Heddle.LanguageServices/`) with a plain `<ProjectReference>` to `Heddle`. **No
existing type moves assemblies, no facades over existing types, no `[TypeForwardedTo]`.**
Everything tooling-adjacent that exists today — `TemplateOptions.ProvideLanguageFeatures`,
`DocumentParser`, `ParseContext`, `HeddleToken`/`HeddleTokenType`,
`HeddleCompileError`/`Warning` — stays public in `Heddle` unchanged. The facade is new
code by construction; the engine touches are additive — internal plumbing (scope-map
retention D2, import-origin marking D25) plus D24's two public members on the engine's
own registration surface (`[ExportFunctions]`, `FunctionRegistry.RegisterFrom` —
function-export API for hosts, not tooling API; ledger-tracked amendment of phase 1's
function surface). Dependency direction: `Heddle.LanguageServices → Heddle → Heddle.Language`;
`Heddle.LanguageServer → Heddle.LanguageServices + StreamJsonRpc`; nothing references
back. Binary-compat proof obligations: (a) the public-surface snapshot test (WI2) pins the
`Heddle` and `Heddle.Language` public APIs as golden files and fails on any removal or
signature change; (b) `dotnet test src/Heddle.Tests` runs unmodified; (c) the flag-off
compile benchmark shows zero change (D2).
**Rationale.** This is the roadmap's ratified July 2026 resolution (6.1), re-verified
here against source: the extraction that *would* break consumers — physically moving
`HeddleToken`/`ParseContext`/`DocumentParser` out of `Heddle` — is not needed for the
split and is explicitly not done. Breaking-change count: zero, by construction rather
than by shimming.
**Alternatives rejected.** Moving the language-feature types into the new package with
`[TypeForwardedTo]` shims (works for binary compat but breaks source compat for consumers
compiling against `Heddle` alone, adds a forwarding assembly to service forever, and buys
nothing — the roadmap marks it optional 2.0-window hygiene, not a phase 6 prerequisite);
shipping the facade inside `Heddle` (drags StreamJsonRpc-adjacent concerns and net8+-only
ALC code into a `netstandard2.0` engine package); a metapackage aggregating both (no
consumer needs the aggregate).
**Grounding.** [Roadmap 6.1](../../roadmap/phase-6-tooling-lsp.md#61-language-service-facade--separate-heddlelanguageservices-package-decided-july-2026);
[.NET library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes).

### D2 — Scope map: one recording site in the body-compile funnel, gated by the flag

**Decision.** `CompileContext` gains an internal `ScopeMap` field, created in the two
root constructors **only when `Options.ProvideLanguageFeatures` is true** (otherwise
`null`), and reference-copied in the private copy ctor so all child compiles share one
map. `ScopeMap` (internal, `src/Heddle/Runtime/ScopeMap.cs`) is an append-only list of
`ScopeMapEntry` readonly structs: `(int Offset, int Length, ExType ModelType,
ExType ChainedType)` plus a `RootType` recorded once. The single recording site is the
top of `HeddleCompiler.Compile(document, compileScope, parseContext, chainedType)`:

```csharp
compileScope.CompileContext.ScopeMap?.Record(
    parseContext.AbsoluteOffset, document.Length,
    compileScope.ScopeType, chainedType);
```

`ParseContext` gains `internal int AbsoluteOffset => _offset;`. Because every body —
extension bodies, definition bodies (once per call site with that site's effective
type), imports, partial roots — funnels through this method (verified in Assumed state),
the map is engine-accurate by construction: it retains exactly what the compiler
computed, keyed by absolute document span. Production compiles (`ProvideLanguageFeatures`
false) pay one null check per body compile and allocate nothing.
**Rationale.** The roadmap's "the work is *retaining* them keyed by `BlockPosition`
instead of discarding them", made concrete at the one seam all body compiles share.
Recording per call site (definition bodies re-compile per invocation) is what makes the
artificial-type rule (D13) a pure query over recorded data.
**Alternatives rejected.** Recording inside `CreateExtension`/`InitializeTemplate`
(multiple sites, misses partial/import roots); keying by `ParseContext` object identity
(spans are what position queries need; contexts are not exposed); retaining full
`CompileScope`s (holds Roslyn state and model `Type` graphs alive — a reload-leak
amplifier, D14).

### D3 — Internal engine seams, exposed via `InternalsVisibleTo`

**Decision.** [AssemblyInfo.cs](../../../src/Heddle/Properties/AssemblyInfo.cs) gains
`[assembly: InternalsVisibleTo("Heddle.LanguageServices, PublicKey=<heddle.snk blob>")]`
(same key blob as the existing `Heddle.Tests` entry; `Heddle.LanguageServices` signs with
[heddle.snk](../../../heddle.snk)). The complete list of internal engine additions this
phase makes, all additive:

| Addition | File | Purpose |
| --- | --- | --- |
| `CompileContext.ScopeMap` + `ScopeMap`/`ScopeMapEntry` | [CompileContext.cs](../../../src/Heddle/Runtime/CompileContext.cs), new `ScopeMap.cs` | D2 retention |
| `ParseContext.AbsoluteOffset` | [ParseContext.cs](../../../src/Heddle/Language/ParseContext.cs) | D2 span keying |
| `TemplateFactory.RegisteredNames(): IReadOnlyCollection<string>` | [TemplateFactory.cs](../../../src/Heddle/Runtime/TemplateFactory.cs) | Extension-name completion (aliases included — the registry holds one entry per `[ExtensionName]`) |
| `FunctionRegistry.EnumerateOverloads(): IEnumerable<(string Name, MethodInfo Method, Type[] ParameterTypes, Type ReturnType)>` | `src/Heddle/Runtime/Expressions/FunctionRegistry.cs` (phase 1 file) | Function completion + hover signatures |
| `MemberPathResolver.GetVisibleProperties(Type): IEnumerable<PropertyInfo>` | `src/Heddle/Runtime/Expressions/MemberPathResolver.cs` (phase 1 file) | Member completion under the **identical** filter `TryResolve` uses (refactored to call it internally) |
| `AssemblyHelper.RegisterModelAssemblies(IReadOnlyList<Assembly>)` / `UnregisterModelAssemblies()` | [AssemblyHelper.cs](../../../src/Heddle/Native/AssemblyHelper.cs) | D14 — adds/removes the workspace model assemblies from the static list and metadata references, invalidates `_allTypes`, calls `ReflectionHelper.Reconfigure()` |
| `ParseContext.ImportOrigin` + the internal `ImportOrigin` class | [ParseContext.cs](../../../src/Heddle/Language/ParseContext.cs), new `src/Heddle/Language/ImportOrigin.cs` | D25 — import-provenance marker, inherited by child contexts through the ctor |
| `HeddleCompileError.ImportOrigin` (inherited by `HeddleCompileWarning`) | [HeddleCompileError.cs](../../../src/Heddle/Data/HeddleCompileError.cs) | D25 — per-entry provenance stamp (internal; `ToString()` and the public shape untouched) |

The facade additionally consumes the already-internal `ContextCompilation.Compile(this
CompileScope)` (D9) and the already-internal `TemplateFactory.LoadExtensions(Assembly)` /
`LoadExtensions(IEnumerable<Type>)` overloads (D23 — the same two branches the engine's
own `ObtainExtensions` runs, so scan semantics are never re-implemented). D25 also adds
flag-gated stamping code **inside existing internals** (`ExitImport_block`,
`ImportExtension.InitStart`, `PartialExtension.CompleteInit`, the `HeddleCompiler.Compile`
funnel, the phase 5 layout resolver and the definition branch of `CreateExtension`) —
behavior detailed there; no exposed members beyond the two marker rows above. The two
*public* engine additions this phase makes (`ExportFunctionsAttribute`,
`FunctionRegistry.RegisterFrom`) are not tooling seams — they are the engine's own
declarative-registration surface, specified in D24 and listed in the
[public API contract](#public-api-contract). The LSP
**server** consumes only the facade's public API — internals
stop at the facade boundary; the server assembly gets no IVT grant.
**Rationale.** The roadmap's "integrated properly" clause: same repo, same version
lockstep, no public API grown on the engine for tooling's sake. Each seam is a projection
of knowledge that already exists exactly once (the member filter, the registry contents),
honoring the DRY consolidation rule.
**Alternatives rejected.** Making these members public (no second consumer beyond the
facade; public API is forever); reflection over privates from the facade (version-fragile,
defeats the compile-time lockstep the IVT gives).

### D4 — LSP library: thin hand-rolled layer over StreamJsonRpc 2.25.29 (landscape re-verified)

**Decision.** The server hand-writes its LSP layer over **StreamJsonRpc 2.25.29** (exact
pin; patch updates within 2.25.x allowed at implementation without spec change). The
July 2026 re-verification of the roadmap's landscape record, via the NuGet flat-container
version lists (primary source, current day):

| Package | Verified latest | Status |
| --- | --- | --- |
| `StreamJsonRpc` | **2.25.29** | Active (Microsoft; 2.25.x line current — matches the roadmap's claim) |
| `OmniSharp.Extensions.LanguageServer` | **0.19.9** (unchanged since September 2023) | **Dormancy confirmed** — no release in ~34 months; the April 2024 "Repo Support Status?" issue ([#1221](https://github.com/OmniSharp/csharp-language-server-protocol/issues/1221)) remains without a maintainer resolution |
| `EmmyLua.LanguageServer.Framework` | **0.9.2** | Active but pre-1.0, tiny adoption — stays the documented fallback |

Heddle needs six capability areas (D7), so owning a small protocol layer removes the
framework-longevity bet: StreamJsonRpc handles transport, dispatch, request cancellation
(`$/cancelRequest` maps to `CancellationToken` natively), and error envelopes; the server
hand-writes `System.Text.Json` DTOs for exactly the messages it implements
([protocol.md — DTO subset](protocol.md#dto-subset)). The diagnostics-only milestone
(WI6) is the explicit checkpoint to reassess the layer's size before further capabilities;
`EmmyLua.LanguageServer.Framework` and OmniSharp remain the fallbacks if it outgrows
"thin".
**Rationale.** Maintainer-ratified (roadmap index, July 2026); the dormancy claim that
motivated it is re-verified above, not trusted.
**Alternatives rejected.** OmniSharp (dormant — confirmed); EmmyLua framework (pre-1.0
single-maintainer dependency for a one-maintainer project — swaps one longevity bet for
another); CLaSP / `Microsoft.VisualStudio.LanguageServer.Protocol` (prerelease-only /
publicly stale since May 2022 — carried from the roadmap's grounding).
**Grounding.** NuGet flat-container version lists for
[StreamJsonRpc](https://www.nuget.org/packages/StreamJsonRpc),
[OmniSharp.Extensions.LanguageServer](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer),
[EmmyLua.LanguageServer.Framework](https://www.nuget.org/packages/EmmyLua.LanguageServer.Framework)
(queried July 2026); [roadmap 6.2 landscape record](../../roadmap/phase-6-tooling-lsp.md#62-lsp-server-srcheddlelanguageserver-new-project).

### D5 — Protocol version: LSP 3.17 semantics as the normative baseline

**Decision.** The server implements **LSP 3.17** semantics and advertises nothing that
differs between 3.17 and 3.18. Verified July 2026: the official
`specification-current` URL now redirects to the 3.18 document, and the
`vscode-languageserver-protocol` npm package is at 3.18.2, **but** the 3.18 specification
page still opens with "This document describes the current 3.18.x version of the language
server protocol and is under development." Every capability in the v1 set (D7) is stable
since 3.16/3.17, so pinning 3.17 costs nothing and avoids building on in-flight wording.
Position encoding: the server does not advertise `positionEncoding` (3.17 optional
capability) and uses the mandatory default `utf-16` — which is exactly what
`BlockPosition` already stores (Assumed state).
**Rationale.** Freshness verification refined the roadmap's landscape (which recorded
EmmyLua as "LSP 3.18" without pinning Heddle's own target); the "under development"
banner makes 3.17 the last finalized text.
**Alternatives rejected.** Targeting 3.18 (no needed feature is 3.18-only; the draft
status would make the hand-written DTOs chase wording changes); advertising
`positionEncoding: utf-8` (would force offset transcoding for zero benefit — .NET strings
are UTF-16).
**Grounding.** [LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/);
[LSP 3.18 specification (status line)](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.18/specification/).

### D6 — Transport and serialization: stdio, header-delimited, source-generated STJ

**Decision.** Stdio only in v1. Wiring: `new JsonRpc(new HeaderDelimitedMessageHandler(
Console.OpenStandardOutput(), Console.OpenStandardInput(), formatter))` where
`formatter` is a `SystemTextJsonFormatter` configured as below — LSP *is* UTF-8 JSON
with `Content-Length` framing; both classes verified
present in StreamJsonRpc's extensibility documentation. The formatter's
`JsonSerializerOptions` gets: `TypeInfoResolver` = the source-generated `LspJsonContext`
(`[JsonSerializable]` over every DTO in the subset — no reflection-metadata warm-up on
the first message), `AllowDuplicateProperties = false`, and default (non-`Strict`)
unmapped-member handling — clients legitimately send properties the DTO subset does not
model, and they must be ignored, not faulted. The server registers plain target methods
on `JsonRpc` (no dynamic proxies, so no interceptor wiring).
**Rationale.** The roadmap's ".NET 10 opportunities" pins, carried and re-verified.
**Alternatives rejected.** Newtonsoft `JsonMessageFormatter` (drags Newtonsoft.Json into
the graph); named-pipe/socket transports (no v1 client needs them — Deferred items);
MessagePack (LSP mandates JSON).
**Grounding.** [StreamJsonRpc extensibility docs](https://microsoft.github.io/vs-streamjsonrpc/docs/extensibility.html)
(re-fetched July 2026 — `HeaderDelimitedMessageHandler` and `SystemTextJsonFormatter`
confirmed); [What's new in .NET 10 libraries — serialization](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries#serialization).

### D7 — The v1 capability set (pinned)

**Decision.** The server implements exactly:

1. **Lifecycle** — `initialize` / `initialized` / `shutdown` / `exit`, `$/cancelRequest`
   (via StreamJsonRpc), `$/setTrace` (accepted, ignored), `workspace/didChangeConfiguration`.
2. **Document sync** — `textDocument/didOpen`, `didChange` (full sync, D8), `didClose`,
   `didSave` (accepted, no-op — analysis keys on buffer content, not disk).
3. **Diagnostics** — server-push `textDocument/publishDiagnostics` on open and after each
   debounced reanalysis; cleared (empty array) on `didClose`.
4. **Semantic tokens** — `textDocument/semanticTokens/full` only.
5. **Completion** — `textDocument/completion`, trigger characters `@ ( . : ,`; items are
   fully materialized (no `completionItem/resolve`).
6. **Hover** — `textDocument/hover` (markdown).
7. **Go-to-definition** — `textDocument/definition`.

Everything else answers per protocol defaults (capability not advertised; unknown
`$/`-notifications ignored; unknown requests get `MethodNotFound`, which StreamJsonRpc
produces automatically). Delivery order during implementation is the roadmap's: 1–3
first (the WI6 checkpoint), then 4, 5, 6, 7.
**Rationale.** Exactly the roadmap 6.2 capability list; "later: rename, find references,
document symbols" stays later (Deferred items). Fully-materialized completion items keep
the protocol layer stateless — the item count per request is bounded (members of one
type + registries), so lazy `resolve` buys nothing.
**Alternatives rejected.** Pull diagnostics (`textDocument/diagnostic`, 3.17): push is
universally supported and simpler in a thin layer; pull's lazy evaluation matters for
expensive polyglot analyzers, not a sub-100 ms template compile — deferred with trigger.
Semantic-token delta/range: full documents are small; deltas add server-side state per
document for no measured need — deferred.

### D8 — Full-document sync, 300 ms debounce, request-forced analysis, staged cancellation

**Decision.** `textDocumentSync.change = Full` (kind 1). Rationale is structural, not
lazy: the ANTLR lexer has no incremental mode — any change reparses the full document —
so incremental sync would only save change-transmission bytes while adding the classic
range-application drift bugs; Heddle templates are page-sized (the benchmark home page is
the *large* case), and full sync eliminates an entire failure class in a hand-rolled
layer. Reanalysis debounce: **300 ms** after the last `didChange` per document (meets
success criterion 2's "within one second of typing pause" with margin). A completion/
hover/definition request against a stale analysis cancels the pending timer and runs the
analysis synchronously inside the request (the request's `CancellationToken` governs).
Superseding changes cancel in-flight analysis via a per-document CTS; cancellation is
checked **between stages** (parse → engine compile → Roslyn pass) — an individual ANTLR
parse is not interruptible, which bounds cancellation latency at one stage's duration
(documented; acceptable at template sizes).
**Alternatives rejected.** Incremental sync (above — deferred, trigger: measured
transmission cost on documents ≥ 1 MB); zero debounce (compiles per keystroke waste CPU
for intermediate states no one sees); adaptive debounce (tuning knob without a scenario).

### D9 — Compile-on-type: engine compile always; Roslyn only when the document really uses C#

**Decision.** Every analysis runs parse + `HeddleCompiler.Compile` (expression-tree tier
— Roslyn-free; phases 1–5 made this the common case). The Roslyn pass
(`ContextCompilation.Compile(CompileScope)`, internal, via IVT) additionally runs only
when **both**: the workspace `expressionMode` is `fullCSharp` (D18), and the compiled
document actually registered C# methods (`CSharpContext.Methods.Count > 0`). The Roslyn
assemblies therefore load lazily on the first analysis that needs them — native-tier
workspaces never load Roslyn at all (cold-start relevant, D19/Performance). The facade
runs the pipeline directly (never `HeddleTemplate`): construct `CompileContext` from the
workspace options template with `ProvideLanguageFeatures = true`, `DocumentParser.Parse`,
`HeddleCompiler.Compile`, dispose the returned `RuntimeDocument` immediately (analysis
wants the side channels — tokens, errors, warnings, scope map — not the render tree),
then optionally the Roslyn pass, collecting `CompileErrors`/`CompileWarnings` after each
stage.
**Rationale.** Roadmap risk mitigation ("scope-map compilation can skip Roslyn entirely
when the document avoids the C# tier") made mechanical; the `TryCompilation`-skips-Roslyn
verification (Assumed state) shows why driving the pipeline directly is required for
C#-tier diagnostics fidelity.
**Alternatives rejected.** Roslyn on a separate longer debounce (two freshness states per
document confuse the diagnostics story; the pass is compile-time-bounded and rare);
never running Roslyn in the LSP (silently hides the C#-tier errors success criterion 2's
scenario matrix includes).

### D10 — `AnalysisResult` content: strictly a projection of engine data

**Decision.** One analysis produces an immutable `DocumentAnalysis` carrying: the source
`text` + `version`; the `LineMap` (D11); `Tokens` (the `ParseContext.Tokens` list,
document order); `SkippedTokens`; `Diagnostics` — the merge of `CompileContext.
CompileErrors` and `CompileWarnings` (⚠ warnings never reach `HeddleCompileResult`;
verified — the facade reads the context lists; entries present in both a `ParseContext`
list and a `CompileContext` list are the same object references — `DocumentParser.Parse`
copies by `AddRange` — so the merge dedupes by reference identity), each as an immutable
`HeddleDiagnostic { Id, Message, Fix, Severity, Offset, Length, ImportedFrom }` (one
projection rule applies during this merge, facade-side — import-attributed entries
re-anchor to their import site per D25; the engine lists are never mutated. No
function-related projection exists: D24's scanned exports register into the real
workspace registry, so a false `HED1001` never reaches this merge); `Definitions` — one
`DefinitionInfo` per `DefinitionsBlock.Definitions` entry (name, header span, model type
name, resolved `ExType` or null, prop declarations with resolved types,
slot type, base name, source file); `Imports` — one `ImportLink` per `@<<` (site span,
raw path, resolved absolute path) and per `@partial` target (site span, resolved
`FullPath`); the `ScopeMapView` (D2 entries + root type); and `CSharpTierUsed`. Nothing
in the facade re-derives what the engine computed; every field names its engine source.
**Rationale.** The roadmap's maintenance rule ("every feature must be a projection of
engine data, never a re-implementation") turned into the result-type contract.
**Alternatives rejected.** Exposing `ParseContext`/`CompileContext` directly (mutable
lists, engine lifetime coupling, would freeze internal shapes into the facade's public
API); a database/workspace-symbol model (YAGNI at six capabilities).

### D11 — Positions: facade speaks UTF-16 offsets; the protocol layer owns line/character

**Decision.** All facade APIs take and return absolute UTF-16 code-unit offsets
(`BlockPosition` semantics — verified identical to LSP's default encoding). Each
`DocumentAnalysis` carries an immutable `LineMap` built once from the analyzed text
(line-start offset array; `\n` terminates lines; a preceding `\r` belongs to the
terminated line, matching the existing `HeddleCompileResult` splitting behavior);
`OffsetToPosition`/`PositionToOffset` are binary-search/index lookups. The protocol layer
converts at the boundary and nowhere else. Diagnostics with `default(BlockPosition)`
(0:0 — the engine's "no position" convention for I/O-level failures) map to the document
start with zero length. The position-mapping torture suite (Testing plan) pins hidden
channel (`@*…*@`, `@\`), CRLF, and multi-line-construct offsets against hand-computed
expectations, plus the D25 zero-width import-site anchors (a re-anchored imported
diagnostic must land on the exact `@<<`/`@import()`/`@partial` site line/character in
both CRLF and LF variants).
**Rationale.** One conversion boundary is the classic defense against LSP mapping drift
(the roadmap names it the classic LSP regression); the facade staying offset-based keeps
it reusable by the phase 9 WASM host, which has no LSP positions.
**Alternatives rejected.** Reusing `HeddleCompileResult`'s line mapping (mutates errors
in place, coupled to result lifetime — verified in Assumed state); line/character
throughout the facade (forces every engine-position consumer through a conversion).

### D12 — Completion contexts, sources, and trigger characters

**Decision.** Trigger characters: `@`, `(`, `.`, `:`, `,` (plus explicit invocation).
Context is detected from the token stream before the offset plus the innermost scope-map
span; the normative detection table, item-kind shapes, and worked examples live in
[protocol.md — completion](protocol.md#completion). The contexts and their engine
sources, summarized: after `@` (and after chain `:`) → definitions in scope +
extension names from `TemplateFactory.RegisteredNames()` (the live registry is
authoritative; the expected v1 contents — 23 offerable names including the aliases
`raw`, `elif`, `elseif`, `else`, with `""` filtered out — are enumerated with their
provenance in [protocol.md — name sources](protocol.md#name-sources-the-v1-registry-contents))
+ standalone-callable registered
functions from `FunctionRegistry.EnumerateOverloads()` (the 17 phase 1 built-ins plus
phase 4's `range`, likewise pinned there, plus any D24-scanned function exports) —
workspace-scanned exports need no separate source because they enter the real
registries (`TemplateFactory` per D23, the workspace `FunctionRegistry` per D24);
inside call parens at expression
position → members of the scope's model type (D13 rule) via
`MemberPathResolver.GetVisibleProperties`, plus `this` (phase 5), `::`, function names,
and `true`/`false`/`null`; after `.` → members of the segment-by-segment-resolved prefix
type under the member-tier filter (unresolvable prefix → empty list, never a guess);
after `::` → root-model members (`ScopeMapView.RootType`); named-argument position in a
definition call → props not yet passed, inheritance-flattened per phase 5 D6, rendered
`name: type` with `= default`/`required` detail; import paths after `@<<{{` → no items
in v1 (file-system completion deferred). Every member item carries the property's CLR
type as `detail`; function items carry the full signature list.
**Rationale.** Covers the roadmap success criteria 1 and the phase-obligation list
(extension names registry-driven including multi-name aliases, typed member paths, phase
5 props with types/required/defaults, phase 1 functions with signatures, directives).
**Alternatives rejected.** Textual/word-based fallback completion when types are unknown
(the roadmap's differentiator is *correct* completion; wrong suggestions are worse than
none); snippet-expanding definition calls (editor-taste feature, deferred).

### D13 — Abstract-definition completion: the artificial type is the scope map's intersection

**Decision.** For an expression position inside a definition body, completion takes
**all** scope-map entries recorded for the innermost enclosing span — one per compiled
call site, because definition bodies compile per invocation (verified,
`CreateExtension` lines 442–465) — and offers the **name-based intersection**: a member
name is offered iff a readable, visible, non-`[Hidden]` property of that name exists on
*every* recorded model type. This single rule covers both cases of the ratified decision:

- **Pinned** (`:: Type` resolving to anything but `object`): the compiler passes the
  pinned `acceptType` at every call site → the entry set is one distinct type → the
  intersection is exactly that type's members.
- **Abstract** (no `::`, `:: object`, or `:: dynamic`): the compiler passes each
  **caller's** type → the intersection is across observed call sites. A member missing at
  even one site is excluded (referencing it would break that site). The intersection is
  by *name*, not type: a name whose property types differ across sites is still offered,
  with `detail` = `varies by call site` and hover listing the per-site types. Zero
  recorded entries (definition never called) → **no member completion** — only
  definitions, extensions, functions, and keywords.

Dynamic entries (`ExType.IsDynamic`) contribute no members and, when present alone,
produce no member completion (matching phase 1's `HED1004` posture that dynamic scopes
have no static surface).

Worked example (the C07/C08 corpus of [protocol.md — completion](protocol.md#worked-examples-the-executable-rows-of-languageservicecompletiontests)):
abstract `<panel>` is called as `@panel(Article)` and `@panel(Menu)`.

1. *Inputs* — the scope map holds two entries for the body span, one per compiled call
   site: `ModelType = Article` and `ModelType = Menu` (neither dynamic).
2. *Per-type visible members* (`MemberPathResolver.GetVisibleProperties` filter):
   `Article` → { `Title : string`, `Summary : string`, `Author : Author`,
   `Rating : int` }; `Menu` → { `Title : LocalizedString`, `Items : IReadOnlyList<MenuItem>` }.
3. *Name intersection* — { `Title` }. `Summary`, `Author`, `Rating` (missing on `Menu`)
   and `Items` (missing on `Article`) are excluded: referencing any of them would break
   the other call site.
4. *Output* — one member item: `Title` with `detail` = `varies by call site` (the
   property types differ: `string` vs `LocalizedString`); hover lists
   `Article.Title : string` and `Menu.Title : LocalizedString` (D15). Had the types
   agreed, `detail` would be the shared CLR type.
5. *Zero-call-site variant* — with both calls removed, the span has no recorded
   entries: no member items at all; definitions, extensions, functions, and keywords
   are still offered (C08).

**Rationale.** This is the roadmap's ratified July 2026 rule, made mechanical: because
the compiler itself substitutes the caller's type for abstract definitions (verified:
`if (acceptType != typeof(object)) dataType = acceptType;` — otherwise the caller's
`dataType` flows through), the scope map already records exactly the call-site type set;
the facade computes an intersection instead of maintaining a parallel call-graph
analysis.
**Alternatives rejected.** Type-based intersection (excludes same-name members with
differing types — the ratified rule explicitly keeps them); union of call-site members
(offers members that break other sites — the exact failure the rule exists to prevent);
completing `object`'s members for never-called definitions (noise with no signal).

### D14 — Model assemblies: collectible ALC, engine registration seam, verified reload

**Decision.** The facade loads the configured model assemblies (D18) into one collectible
`AssemblyLoadContext` (`ModelAssemblyContext`, `isCollectible: true`; dependency probing
via `AssemblyDependencyResolver` when a `.deps.json` sits next to the assembly, else
same-directory probing), then calls `AssemblyHelper.RegisterModelAssemblies(...)` (D3) —
required because engine type resolution reads `AssemblyHelper`'s static list (verified;
no other path exists). Reload protocol, in order, on any watched assembly change
(`FileSystemWatcher`, 500 ms settle): (1) cancel in-flight analyses; (2) drop **every**
type-derived cache — all `DocumentAnalysis` instances (they hold `ExType`s → `Type`s →
the ALC); (3) `UnregisterModelAssemblies()` — clears the engine's static name maps and
metadata references, which hold `Type`/`Assembly` refs and would otherwise pin the
context forever; (4) `Unload()`, retaining only a `WeakReference`; (5) load the new
generation, re-register, reanalyze open documents. `ModelAssemblyReloadTests` asserts
collection via the documented `WeakReference` + `GC.Collect()` polling loop; a context
surviving 10 collections in production logs a `window/logMessage` warning and the server
continues (worst-case fallback: the client's restart command). Staleness is documented:
types update on rebuild, not on source edit. One workspace per server process (the
engine caches are process-static — verified), the standard LSP topology anyway.
**Rationale.** Roadmap risk record, plus this spec's verified finding that the engine's
own static caches (`ReflectionHelper._shortNames`/`_fullNames`,
`AssemblyHelper._allTypes`, `MetadataReferences`) are reload-leak roots the roadmap's
generic "drop every type-derived cache" warning did not name — the unregister seam
exists precisely to clear them. Extension-exporting workspace assemblies never enter
this collectible context: D23 loads them into the **default** ALC precisely so the
leak-root enumeration above stays complete (`TemplateFactory`'s static dictionary has
no unregister and would otherwise pin the context forever), and reloads neither rescan
nor touch `TemplateFactory`.
**Alternatives rejected.** `MetadataLoadContext` inspection (non-invokable `Type`s fork
`ExType` and the whole resolution path — the parallel implementation the maintenance
rule forbids); restart-per-rebuild as the *primary* strategy (loses all warm state per
build; kept as fallback); `AppDomain`s (not on .NET Core).
**Grounding.** [How to use and debug assembly unloadability in .NET](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability);
[AssemblyDependencyResolver](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblydependencyresolver).

### D15 — Hover content

**Decision.** Hover answers for: a member-path segment → the resolved property and its
CLR type (`Author.Name : string`, rendered as a fenced `csharp` signature line), with
"varies by call site" + the per-site list inside abstract definition bodies (D13); a
definition name (declaration or call) → the header signature reconstructed from the
`DefinitionInfo` (`<card(style: string = "plain", compact: bool = false)> :: Article`),
or `abstract — bound per use site: Article, Menu` when unpinned; a prop name → its
declaration (`style: string = "plain"` + declaring definition); a registered-function
name → all overload signatures; an extension name → `extension '<name>'` plus its other
registered aliases. No XML-doc extraction in v1 (model assemblies rarely ship XML files
into bin directories; deferred with trigger). Range = the hovered token's span.
**Rationale.** Success criterion 4 verbatim, extended to the phase 5 surfaces.
**Alternatives rejected.** XML-doc loading (above); markdown tables in hover (VS Code
renders them poorly in the hover widget).

### D16 — Go-to-definition and the import graph: per-file analysis, engine-mirroring resolution

**Decision.** The facade maintains one `DocumentAnalysis` per file and builds the import
closure itself: each `ImportLink` resolves with the **same rule the engine uses**
(`Path.Combine(RootPath, path)` for `@<<` — verified in `ExitImport_block`;
`Options.FullPath` composition for `@partial`), and the target file is analyzed from the
open buffer when open, else from disk. Name binding mirrors the engine's observable
behavior — **as corrected by the July 2026 gap analysis (G7) and pinned in D26**; the
pre-correction draft of this record over-read the `Definitions.Clear()` in
`ExitImport_block` as wholesale replacement. The actual rule (source-verified, and
[phase 4 D11](../phase-4-ergonomics/README.md#d11--two-imports-verified--composes-import-parses-into-an-unreachable-context)'s
authoritative semantics): the visible definition set at an offset is the walk state —
declarations and imports processed in document order, an import contributing its merged
copy set (the isolated snapshot starts as a copy of the pre-import set; the imported
file's declarations layer onto it under the ordinary rules). A plain same-name
redeclaration is a compile error and the **first** declaration survives in the registry;
only an explicit override layer (`<name:base>`, including full override) replaces the
entry. A definition reference therefore resolves to the **surviving registry entry** at
or before it: the newest *legal override layer* when layers exist — the only case where
"last declaration wins" survives — otherwise the first declaration, even while a
later plain duplicate sits in the buffer drawing its error (D26). Targets: definition call → declaration
header span in its owning file; `@<<` path and `@partial` target → the file (position
0); prop named-argument → the `PropDeclaration` span. Unresolvable targets return null —
never a guess. The closure is cycle-bounded (visited set); paths outside `RootPath`
resolve normally (the engine imposes no import sandbox — the LSP matches it exactly).
**Rationale.** Success criterion 3; per-file analysis keeps positions file-local —
imported `DefinitionItem` positions in a merged parse are relative to the *imported*
file's text (verified), so mining the importing file's merged `DefinitionsBlock` would
collide offset spaces.
**Alternatives rejected.** Mining the merged parse (position ambiguity above; re-parses
imports once per importer instead of once per file); a workspace-wide symbol index
(deferred until find-references needs one).

### D17 — Semantic tokens: sparse emission over the TextMate base

**Decision.** `semanticTokensProvider = { legend, full: true }`. The legend contains
exactly six token types (`property`, `function`, `keyword`, `operator`, `macro`,
`comment`) and zero modifiers. Mapping is per `HeddleTokenType` value with a deliberate
**not-emitted** class — punctuation, C#-tier token spans, `Literal` (the token carries no
kind — string vs number cannot be told apart; verified `HeddleToken` shape), and
`ParseError` (diagnostics own errors) stay uncolored by semantic tokens so the TextMate
layer keeps classifying them lexically. `SkippedTokens` (hidden channel — comments, `@\`)
are emitted as `comment`. The full 23-value table, ordering/encoding rules, and a worked
delta-encoding example live in [protocol.md — semantic tokens](protocol.md#semantic-tokens).
**Rationale.** VS Code applies semantic tokens *on top of* grammar highlighting
(platform-verified in the roadmap), so sparse emission is free fidelity: semantic tokens
add value exactly where the grammar cannot (resolved members, registry functions),
and the grammar keeps doing what regexes do well (literals, embedded C#).
**Alternatives rejected.** Emitting every token (forces a wrong single color onto
`Literal`; re-colors embedded C# worse than the TextMate C# include); adding a literal
kind to `HeddleToken` (public engine API change for a cosmetic win — deferred with
trigger).

### D18 — Configuration: `.heddle-lsp.json` at the workspace root wins over client settings

**Decision.** Workspace configuration schema (all fields optional):

```json
{
  "assemblies": ["bin/Debug/net10.0/MyApp.dll"],
  "rootPath": "Views",
  "outputProfile": "html",
  "expressionMode": "native",
  "fileNamePostfix": ""
}
```

Relative paths resolve against the workspace root. `rootPath` feeds
`TemplateOptions.RootPath` (import/partial resolution); `outputProfile` (`text`/`html`) →
`TemplateOptions.OutputProfile`; `expressionMode` (`memberPathsOnly`/`native`/
`fullCSharp`) → `TemplateOptions.ExpressionMode` — so diagnostics match the host's
compile options (roadmap 6.3). `assemblies` serves double duty: the paths load into the
collectible model ALC for typed completion/hover (D14) **and** are scanned once at
workspace load for the engine's declarative exports — extensions (D23) and functions
(D24), the two halves of
[OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)'s
resolved scan channel. There is **no hand-declared name list of any kind** (the
provisional `functions` array was dropped by the OQ2 resolution): host registrations
reach the editor declaratively via the scan, or not at all (the delegate-only
limitation, D24). The VS Code extension contributes mirror settings
(`heddle.model.assemblies`, `heddle.workspace.rootPath`, `heddle.compile.outputProfile`,
`heddle.compile.expressionMode`, `heddle.compile.fileNamePostfix`, plus the client-only
`heddle.server.path` and `heddle.trace.server`) and forwards them via
`workspace/didChangeConfiguration`. Precedence: a `.heddle-lsp.json` present at the
workspace root wins field-by-field over client settings — it is the editor-agnostic
carrier (Neovim/Helix users get full configuration without VS Code). The server watches
the file and re-configures: every configuration application builds a fresh options
template — triggering D14's reload when `assemblies` changed (never a D23 rescan, which
is one-shot per process) and re-populating a fresh function registry from the retained
scan handles (D24 — likewise no rescan). Absent
any configuration the server runs typeless: everything works except model-typed
completion/hover, which degrade to the no-type behavior (no member items — D12's
no-guessing rule), and host-registration knowledge, which requires configured
assemblies by construction (D23/D24) — the default registries and template-local
declarations remain.
**Rationale.** Roadmap 6.1's v1 lean ("a `.heddle-lsp.json` or VS Code setting") pinned
to *both*, with a deterministic precedence; file-wins keeps one source of truth
checkable into the repo.
**Alternatives rejected.** Settings-only (locks non-VS-Code editors out of v1); MSBuild
project evaluation for assembly discovery (the roadmap's named follow-up — deferred).

### D19 — Distribution: version-pinned `dotnet tool install` + platform-specific VSIX bundling; `dotnet tool exec` stays rejected

**Decision.** Two explicit-install channels, both carrying the ratified security
decision — the extension **never** triggers download-and-execute from a package feed;
specifically it never invokes `dotnet tool exec` (or any alias of it) under any code
path, including first-run and "server missing" recovery:

1. **dotnet tool** — package `Heddle.LanguageServer`, `ToolCommandName heddle-lsp`,
   single-target `net10.0`, packed with the seven-RID `<RuntimeIdentifiers>` list plus
   `any` ([protocol.md — tool packaging](protocol.md#tool-packaging-srcheddlelanguageserverheddlelanguageservercsproj)) —
   the .NET 10 SDK produces the manifest package, per-RID framework-dependent
   `PublishReadyToRun` packages (cross-packable from one CI runner — nothing is
   self-contained), and the platform-agnostic fallback. Documented install is
   version-pinned: `dotnet tool install --global Heddle.LanguageServer --version
   <x.y.z>`; the docs page also shows the repo-local tool-manifest variant, recommended
   for teams so the pin lives in source control.
2. **VSIX bundling** — one VSIX per VS Code target (`win32-x64`, `win32-arm64`,
   `linux-x64`, `linux-arm64`, `alpine-x64`, `darwin-x64`, `darwin-arm64`;
   `vsce publish --target <t>`, supported since VS Code 1.61), each embedding the matching
   per-RID publish output under `server/`. The client launches the bundled server as
   `dotnet exec <extensionPath>/server/Heddle.LanguageServer.dll` — `dotnet exec` runs a
   **local file** and shares nothing with the rejected feed-restoring `dotnet tool
   exec`; framework-dependent bundling also sidesteps the VSIX zip-extraction
   execute-bit problem on POSIX. Requires the .NET 10 runtime; the client checks
   `dotnet --list-runtimes` for `Microsoft.NETCore.App 10.` at activation.

Server discovery order in the client: (1) `heddle.server.path`; (2) the bundled
`server/Heddle.LanguageServer.dll`; (3) `heddle-lsp` on `PATH`. Failure UX when none is
found or the runtime check fails: one non-modal error notification naming the exact
remedy (the pinned install command, or the .NET 10 runtime link), an output-channel log
entry, no retry loop — the extension stays activated so TextMate highlighting keeps
working (platform behavior, success criterion 5). The server answers `--version` for the
client's handshake pre-check (D20). Full mechanics — csproj properties, VSIX layout,
launch/discovery pseudocode — in [protocol.md](protocol.md#distribution-mechanics).
**Rationale.** Carries the maintainer-ratified rejection (roadmap index + phase doc,
July 2026, including the mechanical unfitness record: cold `dotnet tool exec` blocks on
a confirmation prompt under a non-TTY stdin — a stdio LSP client's exact topology) and
the roadmap's .NET 10 packaging analysis (per-RID framework-dependent + R2R
recommended). The RID-split package needs the .NET 10 SDK to install — already the repo
baseline (Assumed state) and implied by a `net10.0` tool.
**Alternatives rejected.** `dotnet tool exec` one-shot execution (**rejected for
security** — supply-chain surface at editor launch; ratified); self-contained bundling
(~30 MB per VSIX vs ~2–3 MB, adds a per-OS build matrix, and the runtime is required
for model loading anyway); Native AOT server (structural conflict: no
`Assembly.Load`/no runtime codegen — carried verbatim from the roadmap's evaluation;
posture unchanged: no AOT in v1); one universal VSIX with all RIDs (7× download size).
**Grounding.** [dotnet tool exec](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec);
[What's new in the SDK for .NET 10 — platform-specific tools](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk#platform-specific-net-tools);
[Publishing extensions — platform-specific](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#platformspecific-extensions);
[roadmap — one-shot execution rejection record](../../roadmap/phase-6-tooling-lsp.md#oneshot-execution-dotnet-tool-exec--evaluated-and-rejected-july-2026).

### D20 — Tooling-only messages are not compile diagnostics; no `HED6xxx` IDs are claimed

**Decision.** This phase introduces **no new compile-time diagnostics** — the facade and
server surface the engine's existing positioned `HED*` errors/warnings and add none of
their own. Tooling-condition messages (server/client version mismatch, missing .NET
runtime, model assembly load failure, ALC collection failure, configuration file parse
errors) are **not** `HeddleCompileError`s: they surface through LSP channels —
`window/showMessage` (user-actionable conditions: version mismatch, missing runtime,
bad configuration), `window/logMessage` + the client output channel (operational logs:
load failures, collection warnings). Version handshake: the client reads the extension's
own version and the server's `initialize` response `serverInfo.version`; a major-version
mismatch produces a `window/showMessage` warning naming both versions and the pinned
install command (D19) — the session continues (LSP is compatible-by-omission; a mismatch
degrades features, it does not corrupt). The `HED6xxx` block therefore stays entirely
unclaimed, reserved for any future phase 6.x compile-time diagnostic. The phase 4 spec's
deferred item routing per-diagnostic severity configuration to phase 6 stays deferred
here too (trigger: user demand for suppressing `HED4002`-class lints; the natural design
is an LSP-side severity override map, not engine changes).
**Rationale.** [Cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
IDs exist to make *template* problems stable across surfaces; a version mismatch is a
host-environment condition with no template position — forcing an ID and a fake position
would pollute the mapping this phase exists to deliver (`DiagnosticId` →
`Diagnostic.code`).
**Alternatives rejected.** Claiming `HED6001`-style IDs for tooling conditions (above);
modal error dialogs (hostile for a background tool).

### D21 — Project layout, TFMs, and packaging metadata

**Decision.**

| Project / folder | Contents | TFM(s) | Packaging |
| --- | --- | --- | --- |
| `src/Heddle.LanguageServices/` | Facade: `HeddleLanguageService`, `DocumentAnalysis`, `LineMap`, `ModelAssemblyContext`, workspace config reader | `net8.0;net10.0` | NuGet `Heddle.LanguageServices`; signed with heddle.snk |
| `src/Heddle.LanguageServer/` | Server exe: `Program`, `LspServer` (target methods), DTOs + `LspJsonContext`, dispatch glue | `net10.0` | NuGet tool `Heddle.LanguageServer` (`heddle-lsp`), per-RID per D19 |
| `src/Heddle.LanguageServices.Tests/` | Facade + protocol + reload + lifecycle tests | `net10.0` | not packed |
| `editors/vscode/` | Extension: `package.json`, `src/extension.ts`, grammar copy step, smoke test | — | VSIX per D19 |

The facade targets `net8.0;net10.0` — **not** `netstandard2.0`/`net48` — because
collectible `AssemblyLoadContext` requires CoreCLR; this is a deliberate, documented
narrowing relative to the engine (the engine's own TFMs are untouched). The new test
project exists because [Heddle.Tests](../../../src/Heddle.Tests) multi-targets `net48` on
Windows, which the facade cannot satisfy; it is registered as an additional suite home
(Testing plan) and CI runs both. Engine-side white-box tests (scope map, flag-off
neutrality, public-surface snapshot) stay in `Heddle.Tests`. The extension's
`package.json`: `engines.vscode: ^1.91.0` (floor imposed by `vscode-languageclient`
10.x — verified `engines` field), dependency `vscode-languageclient ^10.1.0` (verified
current July 2026), dev-dependencies `@vscode/vsce` 3.9.2 (verified) and the current
`@vscode/test-electron` 2.x line for the smoke test; `contributes.languages` registers
`.heddle` + the copied grammar; activation event `onLanguage:heddle`. Repo-root
`Heddle.sln` gains the three new projects.
**Rationale.** Version pins verified against the npm registry and NuGet (July 2026);
folder names follow the roadmap (`src/Heddle.LanguageServer`, `editors/vscode`).
**Alternatives rejected.** Facade tests inside `Heddle.Tests` behind TFM conditions
(`net48` build would need constant `#if` fencing); `netstandard2.0` facade with the ALC
behind a capability check (dead surface on every consumer that matters — the server and
the WASM host are both CoreCLR).

### D22 — Docs deliverable: a new `editor-support.md` page

**Decision.** The published docs gain `docs/editor-support.md`: installing the VS Code
extension (marketplace) and the dotnet tool (version-pinned command), the configuration
schema (D18) with a worked `.heddle-lsp.json`, the
model-assembly staleness note
("types update on rebuild"), the host-registration section — how the one-shot D23/D24
`assemblies` scan surfaces `[ExportExtensions]` extensions and `[ExportFunctions]`
functions (with the restart-to-rescan staleness rule and the dedicated-export-assembly
recommendation), the runtime-parity recipe (the host calls
`FunctionRegistry.RegisterFrom` on the same assemblies, so editor and host see one
set), and the delegate-only limitation (purely runtime `Register(name, delegate)`
registrations stay invisible to the editor — their calls draw editor-only `HED1001`,
never a host-compile change) — the no-server
fallback behavior, wiring instructions for
generic LSP clients (Neovim `lspconfig` stanza launching `heddle-lsp`), and the
troubleshooting table (missing runtime, no server found, stale types, extension or
function not offered — export attribute missing, method not an eligible public static,
or restart needed; name lookup is ordinal and case-sensitive, and exported names are
the lowercase method names). Cross-links:
[syntax-highlighting.md](../../syntax-highlighting.md) (grammar-only setups → full LSP),
[getting-started.md](../../getting-started.md), [csharp-api.md](../../csharp-api.md)
(the facade package for programmatic hosts). VitePress sidebar entry added.
**Rationale.** Coding standards require same-phase docs for public API additions; editor
support is user-facing enough for its own page (precedent: phase 1's dedicated
`native-expressions.md`).
**Alternatives rejected.** Folding into `syntax-highlighting.md` (that page is about
grammars/colors; install + config + troubleshooting would triple it off-topic).

### D23 — Workspace extension exports: one-shot scan into the default ALC, real-registry registration (OQ2, gap G4a)

**Decision.** Implements the extension half of
[OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)'s
**resolution** (the scan is ratified as provisionally specified — July 2026; the same
resolution extends it to the declarative function exports of D24, which reuses this
scan's loaded assemblies). The server process never runs host startup code, so runtime
extension registrations are invisible to it and their calls draw a false `HED0002`
(`Cannot find extension <name>`). The channel that closes this is **declarative and
engine-true**: at the first application of a configuration whose `assemblies` list (D18)
is non-empty — initialize-time or a later `didChangeConfiguration` if the list was empty
until then — the facade runs a **one-shot extension scan**, exactly once per server
process, under the same writer gate as the D14 reload critical section and before the
first analysis of that configuration:

1. Each configured assembly file is read as bytes and loaded with
   `AssemblyLoadContext.Default.LoadFromStream` — into the **default (non-collectible)
   ALC, never the D14 model ALC**, and byte-loaded so no file lock is taken (the same
   file stays rebuildable for D14's watch loop; the loaded copy is a snapshot by
   construction). One documented corner: when the assembly's simple name collides with
   an assembly on the server's default probing path, the runtime loads the probing-path
   assembly instead of the stream (BCL rule) — logged, practically irrelevant for
   host-named assemblies.
2. The assembly-level `[assembly: ExportExtensions]` attribute is read
   (`GetCustomAttributes<ExportExtensionsAttribute>()` — the **verified** shape: an
   assembly-target, `AllowMultiple` attribute whose parameterless ctor means `All` and
   whose type-array ctors carry an explicit `Extensions` list; the per-type markers are
   `IExtension` + `[ExtensionName]`). No attribute → the assembly contributes no
   extensions (it was still loaded once — the accepted probe cost).
3. With the attribute: `All` → internal `TemplateFactory.LoadExtensions(assembly)`;
   explicit list → internal `TemplateFactory.LoadExtensions(attr.Extensions)` — via the
   D3 IVT, the **same two branches the engine's own `ObtainExtensions` runs**, so the
   `IExtension`/`[ExtensionName]` filter, `[ExtensionReplace]` ordering and the
   DataType/ChainedType interface ordering are the engine's, never re-implemented. All
   results feed one public `TemplateFactory.AddExtensions(...)` call. The loaded
   `Assembly` handles are retained facade-side: the same one-shot snapshot is the input
   of D24's `[ExportFunctions]` probe and its per-configuration `RegisterFrom`
   re-application — no second load, no second scan latch.
4. Failures degrade per D20: an unreadable/invalid file or a `TemplateOverrideException`
   is a `window/logMessage` error and the remaining registrations proceed. Dependencies
   of the byte-loaded copy resolve through a facade-installed
   `AssemblyLoadContext.Default.Resolving` handler that byte-loads matches from the
   configured assemblies' directories (mirroring D14's same-directory fallback); an
   unresolvable dependency surfaces later as a logged extension-instantiation failure.

Because registration lands in the **real `TemplateFactory`**, everything downstream is
engine-true with no facade special-casing: `HED0002` stops arising for scanned names in
the engine itself, `RegisteredNames()` feeds completion (D12), and hover's
`extension '<name>'` works unchanged. **Staleness is documented, not mitigated**: the
registry is process-append-only with no unregister API and the scan never re-runs —
a new export, a changed extension body, or an `assemblies` addition after the one-shot
scan requires a **server restart** (the client's restart command suffices);
`editor-support.md` (D22) carries the rule. D14 reconciliation, explicit: extension
types live in the default ALC, so `TemplateFactory`'s process-lifetime `Type` references
are **not** a new leak root for the collectible model context — the D14 enumeration
stays complete and the reload protocol is untouched (reloads never rescan). Type-identity
caveat, documented: when one file serves as both extension exporter and model carrier
(or extension `[DataType]`/`[ChainedType]` attributes reference types that also load
into the model ALC), the default-ALC byte copy and the collectible path copy are
distinct `Type` identities, so typed extension-overload selection can degrade in the
editor (never in production) — the docs recommend a dedicated extension-export assembly.
Security posture: the scan executes user-configured local build outputs (module
initializers, extension ctors at analysis time) — the same trust the `assemblies`
opt-in already grants for model loading (whose static ctors also run), and categorically
different from the rejected `dotnet tool exec` surface (D19), which downloads and
executes feed content without a user-declared artifact.
**Rationale.** OQ2's ratified scan, made mechanical at the verified seams: extensions are
assembly-declarative **by the engine's own design** (the `ObtainExtensions` scan is the
production registration path), so the editor channel reuses the engine's registration
machinery instead of building a parallel truth; the default-ALC pin follows from the
two verified registry facts (no unregister, no rescan) plus D14's collection gate.
**Alternatives rejected.** Scanning inside the collectible model ALC (registered `Type`s
would pin the context forever and permanently fail D14's `WeakReference` gate);
`LoadFromAssemblyPath` into the default ALC (locks the file on Windows — breaks the D14
rebuild watch whenever one file serves both roles); relying on `TemplateFactory`'s
static-ctor scan after `RegisterModelAssemblies` (ordering-fragile — the static ctor
runs once at first compile, which may precede configuration, and never re-runs);
`MetadataLoadContext` attribute probing before loading (a parallel inspection stack —
D14's rejection carried); executing a host-provided startup/init assembly (the
code-execution channel OQ2 exists to avoid); recording the limitation only (OQ2's named
alternative — rejected there because D20's contract is faithful diagnostics).
**Grounding.** [OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026);
[TemplateFactory.cs](../../../src/Heddle/Runtime/TemplateFactory.cs) (`ObtainExtensions`,
`AddExtensions`, the ordinal registry — re-verified July 2026);
[ExportExtensionsAttribute.cs](../../../src/Heddle/Attributes/ExportExtensionsAttribute.cs);
[AssemblyLoadContext.LoadFromStream](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext.loadfromstream)
(byte-stream loading; the default-probing-path precedence remark — verified July 2026).

### D24 — Declarative function exports: scanned `[ExportFunctions]` assemblies register into the real workspace registry (OQ2, gap G4b)

**Decision.** Implements the function half of
[OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)'s
**resolution** (July 2026 — it overrides the provisional `functions` configuration
array, which is dropped without a replacement-in-kind: no hand-declared signature list
exists anywhere in this phase). Host-registered functions cannot exist in the server
process because the server never runs host startup code; the channel that closes the
gap is the same one D23 uses for extensions — **assembly-declarative exports, read by
the one-shot scan and registered into the real registry** — extended here with the
engine surface it needs. Three parts:

**(1) The export marker — `[assembly: ExportFunctions(...)]`.** New public attribute
`Heddle.Attributes.ExportFunctionsAttribute`
(`src/Heddle/Attributes/ExportFunctionsAttribute.cs`), shipped in `Heddle.dll` next to
[ExportExtensionsAttribute](../../../src/Heddle/Attributes/ExportExtensionsAttribute.cs)
and deliberately mirroring its verified shape: `AttributeTargets.Assembly`,
`AllowMultiple = true`, ctors `(Type container)` and `(params Type[] containers)`,
property `IReadOnlyCollection<Type> Containers`. **No parameterless "all" form
exists** — extensions have a structural marker (`IExtension` + `[ExtensionName]`) that
makes an assembly-wide scan well-defined; function containers are plain static classes
with no such marker, so "all" would sweep in every helper class. Export contract,
pinned:

- A **container** is a `public static` class. Every public static method it declares
  becomes one registrable function; a helper that must not be exported is made
  non-public (the container is the unit of export — no per-method opt-out).
- **Name derivation:** the function name is the method name lowercased with the
  invariant culture (`MethodInfo.Name.ToLowerInvariant()`) — the derivation
  [phase 1 D13](../phase-1-native-expressions/README.md#d13--the-default-built-in-set-frozen-invariant-exception-safe)'s
  built-ins already follow (`StartsWith` → `startswith`); lookup thereafter is ordinal
  and case-sensitive per
  [phase 1 D12](../phase-1-native-expressions/README.md#d12--functionregistry-semantics).
  Methods whose names differ only by case collapse onto one function name and become
  overloads (or replace, when parameter types also match).
- **Eligibility** is exactly `Register(string, MethodInfo)`'s rule set (phase 1 D12):
  static, closed (no open generics), non-`void` return, no `ref`/`out`/pointer
  parameters. Overloads group by derived name and bind under D12's ranked rules; an
  exact name + parameter-type duplicate across containers follows D12's replace
  semantics — last registered wins, with the processing order pinned (attribute
  declaration order, then the ctor's type-array order) so the outcome is deterministic.
- An **invalid export** — a non-public or non-static container type, or an ineligible
  public static method — is a host programming error, not a template diagnostic.

**(2) The runtime-parity API — `FunctionRegistry.RegisterFrom(Assembly)`.** Phase 1's
`FunctionRegistry` gains one public member (together with the attribute, the
ledger-tracked amendment of phase 1's function surface):

```csharp
public void RegisterFrom(System.Reflection.Assembly assembly);
```

It reads every `[assembly: ExportFunctions(...)]` on the assembly and registers each
discovered method through the **exact `Register(string, MethodInfo)` path** — replace
on exact signature, overload otherwise, `InvalidOperationException` when frozen
(D12's freeze-on-first-compile is untouched: hosts call it during startup before the
first compile, exactly like `Register`), `ArgumentException` naming the offender for
an invalid export. Re-applying it for the same assembly is idempotent (every method
replaces itself); pre-freeze mutation stays non-thread-safe, as D12 documents for
`Register`. This method is the **single implementation of export discovery**: hosts
call it, so the runtime registry holds the exact set the editor scan sees — and the
phase 7 generator discovers the same attribute over compilation metadata references
(per [OQ1's resolution](../OPEN-QUESTIONS.md#oq1--scope-of-function-support-in-precompiled-templates-from-gap-g1--resolved-july-2026);
non-normative forward note). Build-, runtime- and editor-parity is therefore
mechanical: one attribute, one contract, three readers.

**(3) The editor mechanism — the D23 scan feeds `RegisterFrom`.** The one-shot D23
scan probes each byte-loaded assembly for `[ExportFunctions]` alongside
`[ExportExtensions]` (same pass, same default-ALC snapshot, same once-per-process
latch), and the facade retains the loaded `Assembly` handles (D23). Registration
composes with D18 and D12 without touching freeze semantics: **every configuration
application builds a fresh options template (D18); when the retained export set is
non-empty the facade gives it a fresh `new FunctionRegistry()` (Default built-ins
pre-registered, D12) and calls `RegisterFrom` for each retained handle before that
configuration's first analysis, under the same writer gate as the D23 scan** — no
frozen registry is ever mutated, and a compile-option-only reconfiguration re-applies
registration from the retained handles without rescanning. With no exports,
`TemplateOptions.Functions` stays null (= `FunctionRegistry.Default`) — zero
divergence from a bare host. An LSP-side invalid export degrades per D20
(`window/logMessage`; the offending container is skipped, the rest proceeds) —
divergence-safe, because the host's own `RegisterFrom` throws on the same input: no
*running* host can disagree with the editor view.

Because registration lands in the **real registry the analyses compile against**,
everything downstream is engine-true with no facade special-casing — the function
mirror of D23's extension story: the false `HED1001` **stops arising in the engine
itself** for exported names (the span-anchored facade suppression the dropped config
list needed is deleted with it — the D10 merge keeps only the D25 projection rule);
completion and hover serve exported functions through the same `EnumerateOverloads()`
as built-ins, with real `MethodInfo` signatures and no marker text; return types
participate in expression typing, so member completion after an exported call works
(structurally impossible under the display-string config list); `HED1012`/`HED1013`
overload diagnostics and the
[phase 1 D11](../phase-1-native-expressions/README.md#d11--name-resolution-definition--extension--registered-function)
resolution order — including the `HED1016` extension-shadowing warning — fire exactly
as in the host's compile.

**Knowledge source 2 — declarations within the template (ratified).** OQ2's resolution
names exactly two knowledge sources; the second is the template itself, and this spec
already treats it that way — recorded here explicitly instead of implicitly:
definitions declare names first-class per phase 1 D11's resolution order (definition →
extension → function), and every feature reads them from the analysis's
`DefinitionInfo` set — completion's callable-name context lists definitions first
(D12), hover reconstructs their headers (D15), navigation binds to the surviving
registry entry (D16/D26). No behavior change was needed; this note closes the
verification the resolution asked for.

**Documented limitation (recorded, not mitigated).** Purely-runtime registrations —
`Register(string, Delegate)` closures and anything else not representable in assembly
metadata — remain invisible to the editor: their calls draw `HED1001` in the editor
only (the host's own compile, which runs the registration, never sees it).
`editor-support.md` (D22) documents the remedy: export the functions declaratively and
`RegisterFrom` the assembly. **Staleness** follows D23's rule verbatim: the scan is
one-shot — a new export, a changed signature, or an `assemblies` change after the scan
requires a server restart. **D14 reconciliation:** exported `MethodInfo`s live in the
default ALC, so a frozen registry pins nothing in the collectible model context — the
D14 leak-root enumeration stays complete, and model reloads neither rescan nor rebuild
the registry. **Type-identity caveat** (shared with D23's): an exported method whose
parameter/return types also load into the collectible model ALC sees distinct `Type`
identities in the editor, so typed calls over model-ALC values can degrade there
(never in production) — the dedicated-export-assembly recommendation covers function
exports too.

**Trust model (reworked — stronger than the dropped declaration list).** Exported
signatures are **real assembly metadata** of user-configured local build outputs — the
same trust the `assemblies` opt-in already grants for model loading and the D23
extension scan (D23's security-posture record applies verbatim). Nothing is
hand-declared, so the old "trusted without verification" caveat and its
wrong-declaration failure mode are gone with the config list.
**Rationale.** OQ2's resolution verbatim: the ratified scan is *extended to also yield
declaratively exported functions* — the same assembly-declarative model OQ1's
resolution binds against, keeping editor knowledge and precompilability the same set —
and the config list is dropped. Mirroring `[ExportExtensions]` keeps one convention
for one concept (assembly-level export markers the engine owns), and routing
everything through `Register(string, MethodInfo)` keeps phase 1 D12 the single
semantics owner (replace, overloads, freeze) — discovery is the only new code.
**Alternatives rejected.** The provisional `functions` configuration array (**dropped
by the OQ2 resolution**: hand-declared display strings duplicate knowledge the
assembly already carries, cannot participate in typing or overload binding, and their
wrong-declaration failure mode forced a trust caveat real metadata does not need);
stub registration of declared shapes into the engine registry (the earlier draft's
rejection carries: synthesizing callables from configuration text is runtime codegen
over config data); a parameterless `All` ctor (no structural marker distinguishes a
function container — above); a class-level marker attribute found by type-walking (the
assembly-level probe reads only attribute metadata — no type enumeration of arbitrary
assemblies — and mirrors the verified `ObtainExtensions` convention); per-method
export attributes (per-method noise; the container is the natural unit for grouping,
docs, and OQ1's build-time binding); exporting method names as-written (mixed-case
function names diverge from the all-lowercase built-in convention and D13's
derivation, inviting `TitleCase` vs `titlecase` confusion at call sites); facade-side
attribute reading that bypasses `RegisterFrom` (forks discovery semantics — the parity
property *is* the point).
**Grounding.** [OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)
(the binding resolution);
[OQ1](../OPEN-QUESTIONS.md#oq1--scope-of-function-support-in-precompiled-templates-from-gap-g1--resolved-july-2026)
(the build-time consumer of the same marker);
[phase 1 D12](../phase-1-native-expressions/README.md#d12--functionregistry-semantics)
(ordinal names, replace-on-exact-signature, ranked overloads, freeze) and
[phase 1 D13](../phase-1-native-expressions/README.md#d13--the-default-built-in-set-frozen-invariant-exception-safe)
(the lowercase-name precedent);
[ExportExtensionsAttribute.cs](../../../src/Heddle/Attributes/ExportExtensionsAttribute.cs)
(the mirrored shape — re-verified July 2026); D23 (scan mechanics, retained handles,
security posture, type-identity caveat).

### D25 — Imported-file diagnostics re-anchor to the import site; the `ImportOrigin` marker (gap G6)

**Decision.** [Phase 4 D11](../phase-4-ergonomics/README.md#d11--two-imports-verified--composes-import-parses-into-an-unreachable-context)
pins that `@<<` / `@import()` target errors surface **on the importing compile with
positions in the imported file's coordinate space** (shared error lists — re-verified:
`ParseContext` shares `Errors`/`Warnings` with children, `ExitImport_block` and
`ImportExtension.InitStart` parse into the importing `CompileContext`, and
`PartialExtension.CompleteInit` `AddRange`s the child compile's errors). Mapping such an
entry through the importing document's `LineMap` (D11) squiggles an unrelated span.
Verified: **nothing discriminates these entries today** (`HeddleCompileError` carries
only `Position`/`Error`/`Exception` plus phase 1's `DiagnosticId`), so this phase adds
the marker as an engine seam (flag-gated like D2 — production compiles see one boolean
check per import block and one null check per body compile, no allocation):

- `internal sealed class ImportOrigin { internal string Path; internal BlockPosition
  Site; }` (`src/Heddle/Language/ImportOrigin.cs`) — `Path` = the resolved absolute path
  of the file whose parse/compile produced the artifact (the **deepest** origin);
  `Site` = the import/partial site span in the coordinate space of the document under
  analysis. Deliberately mutable: instance **sharing** is the re-anchoring mechanism.
- `ParseContext.ImportOrigin` (internal, ctor-inherited: `ImportOrigin =
  parentContext?.ImportOrigin`) and `HeddleCompileError.ImportOrigin` (internal;
  `HeddleCompileWarning` inherits it) — both `null` outside imports.
- **Stamp sites** (all only when `ProvideLanguageFeatures`):
  1. `HeddleMainListener.ExitImport_block` — before the imported parse: create
     `origin = new ImportOrigin(resolvedPath, GetAbsoluteBlockPosition(context))`, set
     it on the isolated context, snapshot the counts of the four lists
     (`CompileContext.CompileErrors`/`CompileWarnings` and the shared
     `ParseContext.Errors`/`Warnings`). After the parse and the definition merge:
     every appended entry with a `null` marker is stamped with `origin`; an appended
     entry already carrying a marker (a nested import's) gets `entry.ImportOrigin.Site
     = origin.Site` — re-anchoring the **shared instance**, so `Path` keeps the deepest
     file while `Site` bubbles up to this document's coordinates. Every merged
     definition whose `Context.ImportOrigin` is non-null gets the same `Site`
     re-anchor (idempotent; the shared instance propagates to all of that definition's
     body sub-contexts and to entries already stamped with it). Entries appearing in
     both a `ParseContext` and a `CompileContext` list are the same objects
     (`AddRange` — verified), so per-object stamping needs no dedup.
  2. `ImportExtension.InitStart` — the same bracket around its `DocumentParser.Parse`
     call; origin = (resolved target path, the extension's absolute `Position`). No
     definition merge exists on this path (phase 4 D11: nothing merges).
  3. `PartialExtension.CompleteInit` — the same bracket around `InnerTemplate.Compile`;
     origin = (the child context's `Options.FullPath`, the extension's `Position`).
     Partials share G6's failure mode (verified `AddRange`), so they get the same fix.
  4. `HeddleCompiler.Compile` (the D2 funnel) — when `parseContext.ImportOrigin` is
     non-null, bracket the body compile and stamp appended entries with it. This
     catches **compile-time** diagnostics inside imported definition *bodies* compiled
     at the importing document's call sites (`HED0001` member misses, `HED1xxx`
     expression errors, phase 2/3 errors…), which no parse bracket sees. The same
     check **skips the D2 scope-map `Record`** for such compiles — foreign offsets must
     not enter the analyzed document's offset-keyed map (the imported file's own
     analysis owns those spans).
  5. Header-derived compile-time diagnostics — **the phase 5 growth case**: the phase 5
     layout resolver (`PropLayout`, emitting `HED5008`/`HED5009`/`HED5010` at
     prop-declaration positions) and the definition branch of `CreateExtension`
     (model-type/inheritance checks) stamp the entries they add with
     `definition.Context.ImportOrigin` when non-null. These two sites position
     diagnostics by `DefinitionItem`/`PropDeclaration` spans while compiling the
     *caller's* chain, outside every bracket above. The parse-time phase 5 header
     checks (`HED5007` duplicates, `HED5015`–`HED5017` slot/reserved-name errors —
     emitted during the imported walk per phase 5 WI3) are already covered by stamp
     site 1.
- **Facade projection** (the D10 merge): an entry with a non-null marker surfaces in
  the importing document's `DocumentAnalysis` re-anchored to the import site — a
  **zero-width range** at `Site.StartIndex`, message prefixed `imported '<path>': ` and
  the new `HeddleDiagnostic.ImportedFrom` set (both use the origin path rendered
  `RootPath`-relative with forward slashes when under the root, else absolute).
  `Id`, severity and `Fix` are unchanged, so `HED*` codes stay filterable at the site.
  The diagnostic appears at its **true span only in the imported file's own analysis** —
  D16's per-file model already produces one when the file is open (or is pulled into
  the closure), and that root parse carries no marker.

Nested imports (A → B → C) resolve mechanically: C's artifacts are stamped during B's
parse with (C, site-in-B), and A's bracket re-anchors the shared instance to
(C, site-in-A) — the message names the true file, the squiggle sits on the `@<<` in the
analyzed document.
**Rationale.** D10's projection contract demands positions that are meaningful in the
analyzed document; phase 4 D11's compile semantics (shared lists, foreign coordinates)
are correct for the engine and frozen by its fixtures — so the fix belongs in the
editor projection, discriminated by a marker the engine records at the only moments the
provenance is knowable. The shared-instance design makes nested re-anchoring one
mutation instead of a graph walk.
**Alternatives rejected.** Leaving D10/D11's wholesale merge (the foreign-offset
squiggle is exactly the mapping-drift class D11 exists to prevent); mapping imported
offsets into the imported file and emitting cross-file LSP diagnostics for it
(publishing diagnostics for files the user never opened, against the analyzed
`version`-tagging model — and push diagnostics are per-document); span-heuristic
attribution without a marker (imported offsets are indistinguishable from valid local
offsets — verified, no discriminator exists); a full-span (non-zero-width) site range
(would shadow real local diagnostics inside the import block's own text; the
zero-width anchor is the pinned shape); `relatedInformation` click-through to the true
span (the right long-term UX — deferred with its DTO cost, the marker already carries
the data); stamping only at parse time (misses the call-site-compiled body and
header-resolution diagnostics — stamp sites 4–5 exist because of the verified compile
flow).
**Grounding.** [Phase 4 D11](../phase-4-ergonomics/README.md#d11--two-imports-verified--composes-import-parses-into-an-unreachable-context);
[HeddleMainListener.cs](../../../src/Heddle/Language/HeddleMainListener.cs)
(`ExitImport_block` — isolation, shared lists, merge; re-verified July 2026);
[ParseContext.cs](../../../src/Heddle/Language/ParseContext.cs) (ctor list sharing,
`IsolateContextWithTree` copy semantics);
[DocumentParser.cs](../../../src/Heddle/Language/DocumentParser.cs) (`AddRange`
reference copy); [ImportExtension.cs](../../../src/Heddle/Extensions/ImportExtension.cs);
[PartialExtension.cs](../../../src/Heddle/Extensions/PartialExtension.cs)
(`CompleteInit` `AddRange`); [phase 5 diagnostics
table](../phase-5-props-and-slots/README.md#diagnostics) (`HED5007`–`HED5010`,
`HED5015`–`HED5017` positions: parse-time header checks vs. resolver-time "Position:
decl").

### D26 — Duplicate definitions: go-to-definition targets the surviving registry entry (gap G7)

**Decision.** Corrects this spec's pre-gap-analysis narrative (the Assumed-state import
row and D16 said imported definitions "replace the current definition set" and that the
"last declaration at or before it wins" — both amended in place above) against
[phase 4 D11](../phase-4-ergonomics/README.md#d11--two-imports-verified--composes-import-parses-into-an-unreachable-context)'s
authoritative, source-verified semantics: a plain same-name declaration is the compile
error `"The definition <name> with the same name already exists"` (legacy-unassigned
message, no `HED` ID — `EnterDef` keeps the existing registry entry and discards the
new one); only the explicit override form (`<name:base>`, including full override
`<name:name>`) replaces the entry (`ExitDef` → `OverrideWith`, or direct replacement
inside definition contexts); an illegal override whose base does not match the current
entry draws the same error and changes nothing; and `ExitImport_block`'s
`Clear()`-then-re-add is mechanical replacement of the current set by a merged copy
that **started as a copy** of it. The go-to-definition rule (D16), pinned per case:

| Buffer state at the reference | Registry survivor (engine-verified) | Go-to-definition target |
| --- | --- | --- |
| Single declaration | It | Its header span |
| Legal override layers (`<name:base>` at or before the reference) | The newest layer | The newest layer's header — the **only** case where "last declaration wins" survives |
| Plain same-name duplicate (invalid, but live while typing) | The **first** declaration | The first declaration's header; the duplicate site carries the engine's error |
| Illegal override (base mismatch) | The pre-existing entry | That entry's header |
| Imported duplicate vs. a pre-import local declaration | The pre-import declaration (the imported plain re-declaration errors during the merge) | The pre-import declaration's header; the error re-anchors to the import site per D25 |

**Rationale.** Go-to-definition must mirror what the compile actually keeps (the
projection rule): hover, prop completion and typed member completion all read the
surviving `DefinitionItem`'s data, so navigation targeting a rejected declaration would
point at a header whose types/props the rest of the tooling is *not* using. The
invalid-duplicate case is transient editor state — the engine already flags it at the
duplicate site, which is remediation enough.
**Alternatives rejected.** Last-textual-declaration for plain duplicates (contradicts
the registry — the compiler binds every use to the first declaration, so navigation
would disagree with hover/completion built from it); returning both locations as a
`Location[]` (D7/D16 pin a single target, and the duplicate is already marked by its
error — ambiguity UI adds nothing); treating imported sets as wholesale replacement
(the corrected reading — contradicts phase 4 D11's fixtures I01 and the verified
`EnterDef` error path).
**Grounding.** [Phase 4 D11](../phase-4-ergonomics/README.md#d11--two-imports-verified--composes-import-parses-into-an-unreachable-context)
(authoritative import semantics, fixtures I01);
[HeddleMainListener.cs](../../../src/Heddle/Language/HeddleMainListener.cs) (`EnterDef`
duplicate error + first-wins, `ExitDef` `OverrideWith`/replacement + base-mismatch
error, `ExitImport_block` merge — re-verified July 2026);
[ParseContext.cs](../../../src/Heddle/Language/ParseContext.cs)
(`IsolateContextWithTree` — the copied definition set).

## Implementation plan

Work items in execution order; each runs the canonical loop (spec-first per the
[TDD verdict](#testing-plan)). "Check" is the completion gate. WI1–WI5 are facade-side
(editor-less); WI6 is the roadmap's diagnostics-only checkpoint; WI7+ complete the
protocol and clients.

**WI1 — Engine plumbing (D2, D3, D25) + the D24 engine surface.**
Files: `src/Heddle/Runtime/ScopeMap.cs` (new), `src/Heddle/Runtime/CompileContext.cs`
(field + root ctors + copy ctor), `src/Heddle/Runtime/HeddleCompiler.cs` (the one
`Record` call + the D25 funnel bracket and foreign-context `Record` skip),
`src/Heddle/Language/ParseContext.cs` (`AbsoluteOffset`, `ImportOrigin` inheritance),
`src/Heddle/Language/ImportOrigin.cs` (new — D25 marker),
`src/Heddle/Language/HeddleMainListener.cs` (`ExitImport_block` bracket + merge
re-anchor), `src/Heddle/Data/HeddleCompileError.cs` (internal `ImportOrigin`),
`src/Heddle/Extensions/ImportExtension.cs` + `src/Heddle/Extensions/PartialExtension.cs`
(D25 brackets), the phase 5 layout resolver
`src/Heddle/Runtime/Expressions/PropLayout.cs` + the definition branch of
`CreateExtension` (D25 header-derived stamping),
`src/Heddle/Runtime/TemplateFactory.cs` (`RegisteredNames`),
`src/Heddle/Runtime/Expressions/FunctionRegistry.cs` (internal `EnumerateOverloads` +
public `RegisterFrom` — D24), `src/Heddle/Attributes/ExportFunctionsAttribute.cs`
(new — the D24 public attribute),
`src/Heddle/Runtime/Expressions/MemberPathResolver.cs` (`GetVisibleProperties`
extraction), `src/Heddle/Native/AssemblyHelper.cs` (register/unregister seam),
`src/Heddle/Properties/AssemblyInfo.cs` (IVT).
Check: new `ScopeMapTests` in `Heddle.Tests` green (spans/types recorded for nested
bodies, per-call-site definition entries, null map when the flag is off; no entries for
import-marked contexts); new `ImportOriginTests` in `Heddle.Tests` green (entries from
an imported parse/compile carry the marker; nested A→B→C imports end site-anchored in
A's coordinates with C's path; imported-definition body and header diagnostics stamped;
flag-off runs stamp nothing); new `FunctionExportTests` in `Heddle.Tests` green (D24
engine surface: lowercase name derivation, overload grouping, replace-on-exact across
containers in pinned order, freeze → `InvalidOperationException`, invalid export →
`ArgumentException`, idempotent re-application); full existing
suite green; `LanguageServiceBenchmarks` flag-off run shows allocations and mean
unchanged (gate rule 4).

**WI2 — Public-surface snapshot (D1 proof).**
Files: `src/Heddle.Tests/PublicApiSurfaceTests.cs` (new) + goldens
`public-api-heddle.txt`, `public-api-heddle-language.txt` (sorted, normalized signature
dump of all public types/members via reflection).
Check: goldens generated and reviewed; the test fails on any public-surface removal or
change for the rest of the phase (and beyond — it is a standing gate).

**WI3 — Facade project, analysis pipeline, diagnostics (D9–D11, D18, D25
re-anchoring).**
Files: `src/Heddle.LanguageServices/Heddle.LanguageServices.csproj`,
`HeddleLanguageService.cs`, `HeddleLanguageServiceOptions.cs`, `DocumentAnalysis.cs`,
`HeddleDiagnostic.cs` (incl. `ImportedFrom`), `DefinitionInfo.cs`, `ImportLink.cs`,
`ScopeMapView.cs`,
`LineMap.cs`, `WorkspaceConfig.cs` (the `.heddle-lsp.json` reader);
`src/Heddle.LanguageServices.Tests/` project skeleton + the **failing** analysis
assertions on the blog corpus (fixtures under
`src/Heddle.LanguageServices.Tests/Corpus/`, mirroring the
[language-reference](../../language-reference.md) blog model), + `PositionMappingTests`
with the torture fixtures referenced from
[src/Heddle.Tests/TestTemplate](../../../src/Heddle.Tests/TestTemplate) by relative path.
Check: diagnostics assertions green (each scenario-matrix row asserts `HED*` ID + offset
span); warnings verified present (the `CompileWarnings` merge); the D25 import matrix
green (zero-width site anchor, `imported '<path>':` prefix, `ImportedFrom` set, true
spans in the imported file's own analysis); `LineMap` torture
rows green (the D24 rows gate in WI5 — they need the scan).

**WI4 — Facade completion / hover / definition (D12, D13, D15, D16, D26
targets).**
Files: `src/Heddle.LanguageServices/Completion/CompletionProvider.cs`,
`ContextDetector.cs`, `HoverProvider.cs`, `DefinitionProvider.cs`, result DTOs; tests
`LanguageServiceCompletionTests` (the [worked-example table](protocol.md#completion) +
the abstract-definition matrix: two call sites sharing `Title`, only one with `Rating` →
`Title` offered, `Rating` not; hover on a shared member with differing types lists
both),
`LanguageServiceHoverTests` (incl. a scanned export's overload fences once WI5 lands),
`LanguageServiceDefinitionTests` (cross-file layout +
page fixture, F12 both directions; the D26 duplicate/override matrix).
Check: all green editor-less; zero-Roslyn assertion for native-tier corpus analyses
(`CSharpTierUsed == false`).

**WI5 — Model assembly loading + reload (D14) + the one-shot export scan (D23
extensions, D24 functions).**
Files: `src/Heddle.LanguageServices/ModelAssemblyContext.cs`, `ModelAssemblyManager.cs`,
`ExtensionRegistrar.cs` (the D23 scan: byte-load into the default ALC, attribute probe,
`TemplateFactory` registration, the `Resolving` handler, once-per-process latch,
retained assembly handles), `FunctionExportRegistrar.cs` (the D24 half:
`[ExportFunctions]` probe over the retained handles + per-configuration `RegisterFrom`
application into the fresh workspace registry);
tests `ModelAssemblyReloadTests` (load a corpus model assembly built on the fly, analyze,
rebuild with an added property, reload, assert: new property completes, old context's
`WeakReference` dies within the GC loop — and the extension registry and function
registrations are untouched by the reload), `ExtensionScanTests` (D23 rows, Testing
plan) and `FunctionExportScanTests` (D24 rows, Testing plan).
Check: the leak test green on `net10.0`; typeless-workspace degradation rows green;
scanned extension completes and draws no `HED0002`; scanned function export completes
with its real signature, its call draws no `HED1001`, and member completion after the
call is typed by the real return type; a delegate-only name still draws `HED1001`
(documented limitation row); a compile-option-only reconfiguration re-applies
`RegisterFrom` to the fresh registry from the retained handles without rescanning; the
post-scan `assemblies` change logs the restart-to-rescan note instead of rescanning.

**WI6 — LSP server: transport, lifecycle, diagnostics (D4–D8; the checkpoint).**
Files: `src/Heddle.LanguageServer/Heddle.LanguageServer.csproj` (tool + RID metadata per
D19), `Program.cs`, `LspServer.cs`, `Protocol/` DTOs + `LspJsonContext.cs`,
`DocumentStore.cs` (debounce + CTS per D8); tests `ServerLifecycleTests` +
`ProtocolContractTests` (in-proc `JsonRpc` pair over `Nerdbank.Streams`
`FullDuplexStream.CreatePair()` — StreamJsonRpc's own dependency, no editor involved)
with recorded-message fixtures under `Fixtures/messages/`
([protocol.md — contract fixtures](protocol.md#contract-test-fixtures)).
Check: initialize handshake fixture round-trips byte-comparable (modulo whitespace);
didOpen → publishDiagnostics fixture green incl. `HED*` `Diagnostic.code`; **checkpoint
review recorded in the PR: protocol-layer size sanity check before WI7** (roadmap
verification-loop step 5).

**WI7 — Server: semantic tokens, completion, hover, definition (D7, D12–D17).**
Files: `SemanticTokensHandler.cs`, `CompletionHandler.cs`, `HoverHandler.cs`,
`DefinitionHandler.cs` in the server project (each a thin projection: facade call +
LSP-shape mapping); contract fixtures per method.
Check: per-method fixtures green; the semantic-token encoding walkthrough example from
[protocol.md](protocol.md#semantic-tokens) asserted literally.

**WI8 — Tool packaging + CI (D19).**
Files: server csproj packaging properties; `.github/workflows/` job additions (pack tool
incl. per-RID packages from one runner, `--version` smoke, publish on tag).
Check: `dotnet pack` locally produces the manifest + per-RID + `any` packages;
`dotnet tool install --add-source ./nupkg` + `heddle-lsp --version` round-trips on the
CI matrix.

**WI9 — VS Code extension (D19, D21).**
Files: `editors/vscode/package.json`, `src/extension.ts` (server discovery order, runtime
check, failure UX, restart command, settings forwarding), `build/copy-grammar.js`
(build-time copy from [docs/coloring-scheme/heddle.tmLanguage.json](../../coloring-scheme/heddle.tmLanguage.json)
— single source of truth, the copy is never edited), `src/test/smoke.test.ts`
(`@vscode/test-electron`: activate on a `.heddle` file, assert the client reaches
`Running` against a stub server path), CI packaging job (`vsce package --target` matrix).
Check: smoke test green in CI; manual verification of success criteria 1–5 in VS Code
recorded in the PR.

**WI10 — Cold start, combined gate, docs (D22).**
Files: `src/Heddle.Performance/LanguageServiceBenchmarks.cs` (compile flag-off vs flag-on
on the benchmark home-page template corpus, `[MemoryDiagnoser]`); cold-start guard inside
`ServerLifecycleTests` (initialize → first publishDiagnostics on the benchmark home-page
template, asserted < 5 s as the CI-safe automated bound; the < 2 s success criterion is
verified on the dev machine in the phase-exit combined run and recorded in the PR);
`docs/editor-support.md` + cross-links + sidebar.
Check: the full [regression gate](../common/testing-standards.md#regression-gates) in one
combined run — build all TFMs; `dotnet test src/Heddle.Tests` **unmodified and green**;
`dotnet test src/Heddle.LanguageServices.Tests` green; grammar-stability check
(`src/Heddle.Language/generated/` has no diff — no-grammar phase); flag-off benchmark
unchanged, flag-on within the documented budget (Performance); docs build green
(uppercase-drive cwd on Windows). Roadmap success criteria 1–6 asserted together.

## Public API contract

All additions are additive ([API design rules](../common/coding-standards.md#api-design-and-compatibility)).
`Heddle.Language` gains **no** public members; `Heddle` gains exactly two —
`ExportFunctionsAttribute` and `FunctionRegistry.RegisterFrom`, D24's ledger-tracked
amendment of phase 1's function surface (engine registration API, not tooling API) —
and everything else engine-side stays internal (D1/D3). XML docs required on every
member below. The facade is the otherwise-only new public surface; the server and
extension are applications, not libraries.

```csharp
namespace Heddle.LanguageServices
{
    /// <summary>Workspace-level configuration; immutable — construct anew to reconfigure.</summary>
    public sealed class HeddleLanguageServiceOptions
    {
        /// <summary>Model assemblies for typed completion/hover; empty = typeless analysis.
        /// Also the input of the one-shot export scan — extensions (D23) and
        /// declaratively exported functions (D24).</summary>
        public IReadOnlyList<string> AssemblyPaths { get; init; }
        /// <summary>Template root for import/partial resolution (TemplateOptions.RootPath).</summary>
        public string RootPath { get; init; }
        // Compile options mirrored from the host (D18):
        public OutputProfile OutputProfile { get; init; }
        public ExpressionMode ExpressionMode { get; init; }
        public string FileNamePostfix { get; init; }
    }

    /// <summary>Document manager and analysis entry point. One instance per workspace;
    /// thread-safe: public members may be called from any thread, analyses are
    /// serialized per document, results are immutable snapshots.</summary>
    public sealed class HeddleLanguageService : IDisposable
    {
        public HeddleLanguageService(HeddleLanguageServiceOptions options);

        /// <summary>Analyzes a document version; returns the immutable analysis.
        /// Cancellation is honored between pipeline stages (D8).</summary>
        public DocumentAnalysis Analyze(string path, string text, int version,
            CancellationToken cancellationToken = default);

        /// <summary>The latest completed analysis for the path, or null.</summary>
        public DocumentAnalysis GetAnalysis(string path);

        /// <summary>Releases the document's analysis state.</summary>
        public void Close(string path);

        /// <summary>Completion items for the UTF-16 offset (D12/D13 semantics).</summary>
        public CompletionResult GetCompletions(string path, int offset,
            CancellationToken cancellationToken = default);

        /// <summary>Hover content for the offset, or null (D15).</summary>
        public HoverResult GetHover(string path, int offset);

        /// <summary>Definition target for the offset, or null (D16).</summary>
        public DefinitionTarget GetDefinition(string path, int offset);

        /// <summary>Runs the D14 reload protocol; invalidates existing analyses —
        /// open documents must be re-analyzed by the caller.</summary>
        public void ReloadModelAssemblies();
    }

    /// <summary>Immutable snapshot of one analyzed document version. Every member is a
    /// projection of engine data (D10). Safe to share across threads.</summary>
    public sealed class DocumentAnalysis
    {
        public string Path { get; }
        public int Version { get; }
        public string Text { get; }
        public LineMap Lines { get; }
        public IReadOnlyList<HeddleToken> Tokens { get; }
        public IReadOnlyList<BlockPosition> SkippedTokens { get; }
        public IReadOnlyList<HeddleDiagnostic> Diagnostics { get; }
        public IReadOnlyList<DefinitionInfo> Definitions { get; }
        public IReadOnlyList<ImportLink> Imports { get; }
        public ScopeMapView Scopes { get; }
        public bool CSharpTierUsed { get; }      // true when the Roslyn pass ran (D9)
    }

    /// <summary>Positioned diagnostic projected from HeddleCompileError/Warning.
    /// Id is the stable HEDxxxx code or null (legacy) — the LSP Diagnostic.code;
    /// Fix carries HeddleCompileWarning.Fix or null. Import-attributed entries arrive
    /// re-anchored per D25 (zero Length at the import site, prefixed Message,
    /// ImportedFrom set).</summary>
    public sealed class HeddleDiagnostic
    {
        public string Id { get; }
        public string Message { get; }
        public string Fix { get; }
        public HeddleDiagnosticSeverity Severity { get; }   // Error | Warning
        public int Offset { get; }
        public int Length { get; }
        /// <summary>Workspace-relative (or absolute, when outside RootPath) path of the
        /// imported/partial file whose analysis produced this entry (D25); null for
        /// diagnostics of the analyzed document itself.</summary>
        public string ImportedFrom { get; }
    }

    /// <summary>Immutable line-start index over one document text (UTF-16 offsets, D11).</summary>
    public sealed class LineMap
    {
        public int LineCount { get; }
        public (int Line, int Character) OffsetToPosition(int offset);
        public int PositionToOffset(int line, int character);
    }

    /// <summary>One definition visible in a document. ModelType is null when
    /// unresolved or abstract; IsPinned = ':: Type' resolving to non-object (D13);
    /// Props are inheritance-flattened (phase 5 D6 rules).</summary>
    public sealed class DefinitionInfo
    {
        public string Name { get; }
        public string SourcePath { get; }
        public int Offset { get; }
        public int Length { get; }
        public string ModelTypeName { get; }
        public ExType ModelType { get; }
        public bool IsPinned { get; }
        public IReadOnlyList<PropInfo> Props { get; }
        public string SlotTypeName { get; }
        public string BaseName { get; }
    }

    /// <summary>One prop of a definition (phase 5 declaration, resolved).</summary>
    public sealed class PropInfo
    {
        public string Name { get; }
        public string TypeName { get; }
        public ExType Type { get; }
        public bool IsRequired { get; }
        public object DefaultValue { get; }
        public int DeclarationOffset { get; }
        public int DeclarationLength { get; }
    }

    /// <summary>An @&lt;&lt; import or @partial target resolved with engine rules (D16);
    /// ResolvedPath is null when the file does not exist.</summary>
    public sealed class ImportLink
    {
        public ImportLinkKind Kind { get; }                  // Import | Partial
        public int Offset { get; }
        public int Length { get; }
        public string RawPath { get; }
        public string ResolvedPath { get; }
    }

    /// <summary>Read-only view over the compiler's retained scope map (D2).
    /// GetModelTypesAt returns all model types recorded for the innermost body span
    /// containing the offset — one entry per compiled call site (D13).</summary>
    public sealed class ScopeMapView
    {
        public ExType RootType { get; }
        public IReadOnlyList<ExType> GetModelTypesAt(int offset);
    }

    /// <summary>Severity of a projected diagnostic; the protocol layer maps it 1:1 to
    /// LSP DiagnosticSeverity (Error → 1, Warning → 2).</summary>
    public enum HeddleDiagnosticSeverity { Error, Warning }

    /// <summary>Kind of an ImportLink (D16): an @&lt;&lt; import or an @partial target.</summary>
    public enum ImportLinkKind { Import, Partial }

    /// <summary>Item classes completion produces (D12); the protocol layer maps each to
    /// the numeric LSP CompletionItemKind pinned in the protocol.md item-shapes table
    /// (Property → 10, Definition → 7, Extension → 3, Function → 3, Prop → 5,
    /// Keyword → 14).</summary>
    public enum CompletionItemKind { Property, Definition, Extension, Function, Prop, Keyword }

    /// <summary>Completion outcome for one offset (D12/D13). Immutable; items are fully
    /// materialized — no resolve round-trip (D7).</summary>
    public sealed class CompletionResult
    {
        /// <summary>Items for the offset; empty when the detected context yields none
        /// (detection rule 7 — the no-guessing rule).</summary>
        public IReadOnlyList<CompletionItem> Items { get; }
    }

    /// <summary>One completion item; label/detail/insert-text semantics per the
    /// protocol.md item-shapes table.</summary>
    public sealed class CompletionItem
    {
        public string Label { get; }
        public CompletionItemKind Kind { get; }
        /// <summary>CLR type, header/overload signature, prop declaration, or
        /// "varies by call site" (D13); null for keyword items.</summary>
        public string Detail { get; }
        /// <summary>Text to insert; equals Label for every kind except Prop, which
        /// inserts "name: " with the trailing space.</summary>
        public string InsertText { get; }
    }

    /// <summary>Hover payload (D15): markdown content plus the hovered token's span —
    /// the source of LSP Hover.contents (markdown MarkupContent) and Hover.range.</summary>
    public sealed class HoverResult
    {
        public string Markdown { get; }
        public int Offset { get; }
        public int Length { get; }
    }

    /// <summary>Go-to-definition target (D16), projected onto one LSP Location:
    /// SourcePath → uri; Offset/Length → range (the declaration-header span, the
    /// PropDeclaration span, or a zero-length span at offset 0 for file targets —
    /// imports and partials).</summary>
    public sealed class DefinitionTarget
    {
        public string SourcePath { get; }
        public int Offset { get; }
        public int Length { get; }
    }
}
```

The two engine-side public additions (D24 — `Heddle.dll`; the ledger-tracked amendment
of phase 1's function surface):

```csharp
namespace Heddle.Attributes
{
    /// <summary>Assembly-level declarative function export (D24). Each container is a
    /// public static class whose eligible public static methods become registrable
    /// functions under their lowercase-invariant method names. Read by
    /// FunctionRegistry.RegisterFrom (runtime), the phase 6 workspace scan (editor),
    /// and — phase 7 — the source generator (build): one attribute, three readers.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ExportFunctionsAttribute : Attribute
    {
        public ExportFunctionsAttribute(Type container);
        public ExportFunctionsAttribute(params Type[] containers);
        public IReadOnlyCollection<Type> Containers { get; }
    }
}

namespace Heddle.Runtime.Expressions
{
    public sealed class FunctionRegistry        // existing phase 1 type — additive member only
    {
        /// <summary>Registers every function the assembly exports via
        /// [assembly: ExportFunctions(...)]: for each container, each eligible public
        /// static method under its lowercase-invariant name, through the exact
        /// Register(string, MethodInfo) path (replace-on-exact-signature, overloads
        /// ranked per phase 1 D12). Throws InvalidOperationException when frozen and
        /// ArgumentException for an invalid export (non-public/non-static container,
        /// ineligible method — named in the message). Idempotent per assembly; not
        /// thread-safe pre-freeze (same rule as Register).</summary>
        public void RegisterFrom(System.Reflection.Assembly assembly);
    }
}
```

Internal additions (not public API; listed for the implementer): the engine seams table
in D3; facade-internal `ModelAssemblyContext`, `ModelAssemblyManager`,
`ExtensionRegistrar` (D23), `FunctionExportRegistrar` (D24), `ContextDetector`,
`WorkspaceConfig`; server-internal `LspServer`, `DocumentStore`, the DTO subset,
`LspJsonContext`.

Thread-safety summary: `DocumentAnalysis`, the query results (`CompletionResult`/
`CompletionItem`, `HoverResult`, `DefinitionTarget`), and everything they reference are
immutable snapshots; `HeddleLanguageService` serializes analysis per document (per-document
`SemaphoreSlim`) and guards the assembly-generation swap with a reader/writer gate so an
analysis never observes a half-reloaded generation; the engine's static registries are
written only inside the reload critical section (single workspace per process, D14) —
plus the one-shot D23/D24 export registration, which runs under the same writer gate
before the first analysis of the configuration that triggered it: the extension half
(process-global `TemplateFactory`) never runs again, while the function half re-applies
`RegisterFrom` from the retained assembly handles to each configuration generation's
fresh, not-yet-frozen registry (D24) under that same gate.
Nothing adds render-path state — the engine render path is untouched by this phase.

## Diagnostics

**This phase claims no diagnostic IDs.** It introduces no compile-time errors or
warnings; the `HED6xxx` block remains reserved and unclaimed (D20 records why
tooling-only conditions do not qualify and how they surface instead). Prior claims
verified against the merged specs: `HED0001`–`HED0004`, `HED1001`–`HED1017`,
`HED2001`–`HED2003`, `HED3001`–`HED3004`, `HED4001`–`HED4002`, `HED5001`–`HED5018`.

What this phase does with existing diagnostics: `HeddleCompileError.DiagnosticId` →
LSP `Diagnostic.code` (string), `source` = `"heddle"`, severity mapped
`HeddleCompileError` → Error / `HeddleCompileWarning` → Warning, warning `Fix` text
appended to the message as a `Fix:` line (LSP diagnostics carry no remediation field
without code actions — deferred), `null` IDs → `code` omitted. One projection rule
from the gap-resolution decisions, which mints no ID: import-attributed
entries surface at the import site with a path-prefixed message (D25 — the imported
file's own analysis carries the true spans). The gap-analysis draft's second rule —
facade-side `HED1001` suppression for hand-declared function names — was removed by
the [OQ2 resolution](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)
together with the `functions` configuration list: D24's scanned exports register into
the real workspace registry, so the false `HED1001` never arises in the engine, while
delegate-only registrations still draw a real, editor-only `HED1001` (D24's documented
limitation). The duplicate-definition error D26
leans on is the pre-existing legacy-unassigned engine message
(`The definition <name> with the same name already exists` — `code` omitted). Full
mapping in [protocol.md — diagnostics](protocol.md#diagnostics).

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md). Suite homes: the
standard two, **plus** `src/Heddle.LanguageServices.Tests` (xUnit, `net10.0`) recorded as
this phase's additional home (D21 — the facade cannot target `net48`); `dotnet test
src/Heddle.Tests` remains the untouched regression gate.

**TDD verdict (carried from the roadmap, unweakened).** Yes for the facade;
contract-style for the protocol; smoke-only for the client. The facade's editor-less
position → completion/hover/diagnostic assertions on the blog corpus are written first —
they *are* the spec of the scope map and the artificial-type rule (WI3/WI4). The protocol
layer is kept honest by recorded-message contract tests (JSON in → JSON out fixtures,
[protocol.md](protocol.md#contract-test-fixtures)) rather than classic TDD. The VS Code
client gets one activation smoke test; the facade rule keeps logic out of it.

**Named test assets:**

| Asset | Home | Content |
| --- | --- | --- |
| `ScopeMapTests` | Heddle.Tests | White-box (IVT): spans + types for nested bodies (`@list` narrowing, `@if` step-back — body records the *caller's* model, not `bool`), per-call-site definition entries, `null` map when the flag is off; no entries recorded for import-marked contexts (D25's `Record` skip). |
| `ImportOriginTests` | Heddle.Tests | White-box (IVT), the D25 marker: imported parse entries stamped (path + site); nested A→B→C ends site-anchored in A's coordinates with C's path (shared-instance re-anchor); imported-definition body compile and header-resolver entries stamped; `@import()` and `@partial` brackets; flag-off runs stamp nothing. |
| `PublicApiSurfaceTests` | Heddle.Tests | The D1 compat gate: reflection dump of `Heddle` + `Heddle.Language` public surfaces vs goldens (the goldens include D24's two additive members). |
| `FunctionExportTests` | Heddle.Tests | The D24 engine surface: `[ExportFunctions]` attribute shape; `RegisterFrom` discovery — lowercase-invariant name derivation (`TitleCase` → `titlecase`), overload grouping by derived name, replace-on-exact-signature across containers in the pinned processing order, freeze → `InvalidOperationException`, invalid export (non-static container; void/ref/open-generic method) → `ArgumentException` naming the offender, idempotent re-application, built-in override via an exported exact signature. |
| `LanguageServiceDiagnosticsTests` | LanguageServices.Tests | The scenario matrix: unclosed `{{` (HED0003), member typo (HED0001), definition-inheritance type mismatch, `expressionMode` ≠ `fullCSharp` + C# expression, phase 2/3/4/5 diagnostics sampled — each asserting ID + exact offset span; warnings-present check (the `CompileWarnings` merge). The D25 import matrix: imported-file syntax error → zero-width diagnostic at the `@<<` site, `imported '<path>':` prefix, original `Id` preserved, `ImportedFrom` set; imported-header `HED5010` (resolver-time) and imported-body `HED0001` (call-site compile) re-anchored; nested A→B→C anchored at A's site with C's path; `@import()` and `@partial` variants; the imported file analyzed on its own → true spans, no marker. The D24 rows: a scanned exported function's call draws no `HED1001` (the workspace registry resolved it — engine-true); a case-mismatched call (`Titlecase` vs the exported `titlecase`) still flagged (ordinal lookup); a delegate-only (unexported) name still draws `HED1001` (the documented limitation); `HED1002` unaffected. |
| `LanguageServiceCompletionTests` | LanguageServices.Tests | The D12 context table + [worked examples](protocol.md#completion); the abstract-definition matrix (Title ∈ both sites → offered; Rating ∈ one → excluded; zero call sites → no members; `varies by call site` detail); props (names, types, required/default, already-passed filtering); registry aliases; `MemberPathsOnly` workspaces still complete member paths; the D24-scanned `titlecase` export offered indistinguishably from built-ins (real signature detail via `EnumerateOverloads`) and member completion after its call typed by the real return type; the D23-scanned `badge` extension offered among extension names. |
| `LanguageServiceHoverTests` | LanguageServices.Tests | Member CLR types; pinned vs abstract definition signatures; prop declarations; function overloads — including a scanned export's overload fences (D24, registry-driven, indistinguishable from built-ins). |
| `LanguageServiceDefinitionTests` | LanguageServices.Tests | Cross-file layout + page fixture (F12 both directions across `@<<`); `@partial` file targets; the D26 matrix: override layers (`<a:a>`, `<b:a>`) → newest legal layer targeted; plain duplicate → first/surviving declaration targeted while the duplicate site carries the error; imported duplicate vs. pre-import local → local targeted; unresolvable → null. |
| `PositionMappingTests` | LanguageServices.Tests | The torture set: hidden-channel comments, `@\`, CRLF vs LF pairs, multi-line definitions — offsets ↔ line/character round-trips against hand-computed expectations; reuses the whitespace fixtures from [TestTemplate](../../../src/Heddle.Tests/TestTemplate); plus the D25 anchor rows — a re-anchored imported diagnostic maps to the exact `@<<`/`@import()`/`@partial` site line/character (zero width) in CRLF and LF variants, including an import site preceded by hidden-channel content. |
| `ModelAssemblyReloadTests` | LanguageServices.Tests | The D14 loop: load → analyze → rebuild → reload → new member completes → old ALC `WeakReference` collected (GC polling loop); the reload leaves the D23 extension registry and the D24 function registrations (default-ALC `MethodInfo`s) untouched. |
| `ExtensionScanTests` | LanguageServices.Tests | The D23 rows: corpus export assembly (`[assembly: ExportExtensions]` + `[ExtensionName("badge")]`) scanned at workspace load → `badge` completes and its call draws no `HED0002`; a no-attribute assembly contributes nothing; `TemplateOverrideException` logged, not thrown; staleness — a post-scan `assemblies` change and a rebuilt export assembly do **not** rescan (the restart-to-rescan log note asserted); the scan runs once even when configuration arrives only via `didChangeConfiguration`. |
| `FunctionExportScanTests` | LanguageServices.Tests | The D24 facade rows: the corpus `[assembly: ExportFunctions(typeof(CorpusFunctions))]` export scanned at workspace load → `titlecase` registered into the workspace registry, its call draws no `HED1001`, member completion after the call typed by the real return type; with no exports `TemplateOptions.Functions` stays null (Default — bare-host parity); a compile-option-only reconfiguration re-applies `RegisterFrom` to the fresh registry from the retained handles (no rescan, no frozen-registry mutation); an LSP-side invalid export logged and skipped per D20; staleness shared with `ExtensionScanTests` (post-scan changes → restart note). |
| `ProtocolContractTests` | LanguageServices.Tests | Recorded-message fixtures over in-proc `FullDuplexStream.CreatePair()` (Nerdbank.Streams): initialize handshake, didOpen → publishDiagnostics (codes present), semanticTokens/full encoding (the literal walkthrough example), completion/hover/definition per worked example, imported-diagnostic re-anchoring, exported-function completion (no `HED1001`), scanned-extension completion, cancellation, shutdown/exit. |
| `ServerLifecycleTests` | LanguageServices.Tests | Process-level: spawn the built server, stdio handshake, `--version`, malformed-header resilience, exit codes; the CI cold-start guard (< 5 s automated bound; < 2 s criterion verified at phase exit on the dev machine, recorded in the PR). |
| `smoke.test.ts` | editors/vscode | Activation on `.heddle`, client reaches `Running`, TextMate grammar contributes scopes. |
| `LanguageServiceBenchmarks` | Heddle.Performance | `[MemoryDiagnoser]`: benchmark home-page compile with `ProvideLanguageFeatures` off (must equal pre-phase baseline) and on (documented budget, Performance section). |
| Corpus fixtures | LanguageServices.Tests/Corpus | The blog model (`Blog`/`Article`/`Author`/`Comment`) templates: nested `@list` bodies, `@if` step-back, `:: Article` definitions, one abstract definition with two typed call sites, layout + page import pair (including an imported file carrying a deliberate member typo and a bad prop type for the D25 rows), props/slot cards from phase 5's examples; an export assembly carrying `[assembly: ExportExtensions]` (one `[ExtensionName("badge")]` extension) and `[assembly: ExportFunctions(typeof(CorpusFunctions))]` (a public static `CorpusFunctions` class whose `TitleCase` method exports `titlecase`) (D23/D24). |

**Regression gate** (one combined run, per the standards): build all TFMs; `dotnet test
src/Heddle.Tests` — **zero modifications to that project except the four new
engine-side test files** (`ScopeMapTests`, `ImportOriginTests`,
`PublicApiSurfaceTests`, `FunctionExportTests`), goldens byte-identical; `dotnet test src/Heddle.LanguageServices.Tests`;
grammar-stability check (no-grammar phase — `generated/` diff-free); benchmarks per
WI10's check; docs build. Roadmap success criteria: 1 → completion worked examples
(typed `@list` body + `::`), 2 → diagnostics matrix + debounce timing, 3 → cross-file
definition tests, 4 → hover tests, 5 → smoke test + TextMate fallback (platform
behavior, verified manually), 6 → the entire editor-less facade suite.

**Deferred phase 9 gallery item** (recorded per the standards): the demo page's WASM host
consumes this same facade; its headless smoke test (one completion + one diagnostic
round-trip) becomes the standing cross-host contract test, plus the `editors/vscode`
walkthrough sample in the gallery. Owned by this phase, delivered when the phase 9
harness exists.

## Back-compat and migration

Everything is additive; nothing lands in the
[2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)
from this phase.

| Surface | Behavior | Proof |
| --- | --- | --- |
| `Heddle` / `Heddle.Language` public API | No moves, no signature changes; `Heddle` gains exactly two additive members — `ExportFunctionsAttribute` + `FunctionRegistry.RegisterFrom` (D24, the ledger-tracked phase 1 amendment); `Heddle.Language` unchanged | `PublicApiSurfaceTests` goldens (WI2, additions included); existing suite unmodified |
| Existing consumers of `ProvideLanguageFeatures` | Identical token stream; additionally a populated scope map they never see (internal) | Existing suite; `ScopeMapTests` flag-off row |
| Production compiles (flag off) | One null check per body compile; zero allocation | `LanguageServiceBenchmarks` flag-off parity |
| `HeddleCompileResult` warnings gap | Preserved as-is — the facade reads `CompileWarnings` directly; no result-shape change | Assumed-state verification; diagnostics tests |
| NuGet layout | Gains `Heddle.LanguageServices` + the `Heddle.LanguageServer` tool; existing package IDs/contents untouched | Pack output review (WI8) |
| Engine TFMs | Untouched (`netstandard2.0;net6.0;net8.0;net10.0`); the facade's narrower TFMs are a property of the new package only | csproj diffs |

Migration notes shipped with the phase: none required for existing users; the
`editor-support.md` page documents the new opt-in surfaces.

## Performance considerations

- **Render path: untouched.** No file on the render path changes. The scope map lives on
  `CompileContext` (compile-time object) and records only during compiles with the flag
  on.
- **Compile path, flag off: provably free.** Two null/flag checks per body compile (the
  D2 `?.` and the D25 origin check — origins are never set with the flag off) plus one
  flag check per import/partial block; zero allocation;
  guarded by the `LanguageServiceBenchmarks` flag-off run (allocated bytes not increased,
  mean within BenchmarkDotNet's reported error — gate rule 4).
- **Compile path, flag on: documented budget.** Scope-map recording is one list append
  of a 4-field struct per body compile; D25 stamping adds one `ImportOrigin` allocation
  per import/partial plus a walk over the entries the imported parse/compile appended
  (proportional to its diagnostic count — zero on clean imports beyond the count
  snapshots). Budget: flag-on compile of the benchmark
  home-page template stays within **10 % mean time and 5 % allocated bytes** of flag-off
  (recorded as the phase baseline; exceeding it is a red gate).
- **Cold start (< 2 s criterion).** Served by: per-RID R2R server assemblies (D19), the
  source-generated STJ context (no reflection warm-up on the first message, D6), lazy
  Roslyn (native-tier workspaces never load it, D9), and analysis starting on `didOpen`
  without waiting for configuration round-trips. Automated CI guard at 5 s; the 2 s
  target verified at phase exit (WI10).
- **Steady state.** 300 ms debounce bounds compile frequency; per-document analysis
  serialization prevents pile-ups; completion requests reuse the current analysis unless
  stale. No AOT (D19) — the JIT + R2R posture is deliberate and recorded.

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made in this phase:

- **DRY where knowledge must not diverge:** member listing shares the member-tier filter
  via `MemberPathResolver.GetVisibleProperties` (D3) — the same single-representation
  rule phase 1 established for resolution; import/partial path resolution reuses the
  engine's composition rules rather than re-deriving them (D16); function-export
  discovery exists once (`FunctionRegistry.RegisterFrom`, D24) — the facade scan calls
  it instead of re-reading attributes, and phase 7's generator binds against the same
  attribute contract.
- **The maintenance rule as SRP:** every LSP capability is a projection —
  facade = engine data reshaped, server handler = facade call + DTO mapping. No compiler
  logic exists outside `Heddle` (the roadmap's one-maintainer mitigation, enforced by the
  D10 contract).
- **YAGNI cutting scope:** no pull diagnostics, no token deltas, no rename/references/
  symbols, no snippet completion, no XML docs in hover, no MSBuild evaluation, no AOT —
  each in [Deferred items](#deferred-items) with a trigger. The facade exposes no
  speculative extensibility seams; the protocol DTO subset grows only when a capability
  ships (D4).
- **Open/closed at the sanctioned seam:** the engine opens exactly one new internal seam
  class (`ScopeMap`) plus enumeration accessors over existing registries — no new plugin
  interfaces for single-implementation internals (precedence rule 5).
- **Security beats ergonomics:** the distribution design refuses the convenient
  zero-install path (`dotnet tool exec`) on ratified supply-chain grounds (D19), and
  completion never guesses when types are unknown (D12) — wrong-but-helpful loses to
  correct-but-silent. Host registrations reach the editor through one declarative scan
  of user-configured local build outputs (D23 extensions, D24 function exports) —
  never by executing host startup code, and never via hand-declared signature lists
  (dropped by the OQ2 resolution).

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Rename (definitions), find references, document symbols | The roadmap's "later" list; first user demand — references needs the workspace symbol index D16 declined to build |
| Pull diagnostics (`textDocument/diagnostic`) | Clients dropping push support, or measured wasted analyses on hidden documents |
| Incremental text sync | Documents ≥ 1 MB in real use, or measured change-transmission cost |
| Semantic-token delta/range requests | Measured full-token latency on real documents |
| Literal-kind semantic tokens (string vs number) | Requires a `HeddleToken` kind field — engine API change; bundle with the next engine-token change |
| File-path completion inside `@<<{{ }}` / `@partial` bodies | User demand; needs workspace file enumeration policy |
| XML-doc extraction for hover | Model assemblies shipping XML docs in practice |
| Per-diagnostic severity overrides (`HED4002`-class lints) | Carried from phase 4; user demand — LSP-side override map, no engine change |
| `relatedInformation` click-through for import-anchored diagnostics (site → true span in the imported file) | First user demand — needs the `DiagnosticRelatedInformation` DTO; the D25 marker already carries the data |
| Dynamic-extension development workflow — rescanning / hot-reloading extension and function exports without a server restart (the hot export-development loop) | Explicitly a **separate future effort** per the [OQ2 resolution](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026): revisit when the maintainer schedules the dynamic-extension development-experience effort. Needs a `TemplateFactory` unregister/regenerate seam (engine change); until then D23's restart-to-rescan staleness rule stands |
| MSBuild project evaluation for model-assembly discovery | The roadmap's named follow-up to the v1 "configure assembly paths" answer |
| Named-pipe / socket transports | A client that cannot spawn stdio children |
| `TypeForwardedTo` extraction of language-feature types out of `Heddle` | 2.0-window hygiene only, per the roadmap — explicitly not a phase 6 prerequisite |
| Native AOT server | Only if the LSP compiles exclusively native-tier **and** model inspection moves to metadata-only — both large unforced bets (roadmap evaluation carried) |
| Browser/WASM facade host + cross-host contract test | Phase 9 (recorded in the testing plan) |

## External references

Primary sources verified for this spec (July 2026), plus the roadmap's grounding set
re-checked where load-bearing. Web notes: the NuGet/npm registry APIs were used directly
(`api.nuget.org` flat-container, `registry.npmjs.org`) after the npmjs.com HTML page
returned 403 to automated fetch; no other retries were needed.

- [StreamJsonRpc — NuGet](https://www.nuget.org/packages/StreamJsonRpc) — 2.25.29 current stable (flat-container version list, July 2026).
- [StreamJsonRpc extensibility docs](https://microsoft.github.io/vs-streamjsonrpc/docs/extensibility.html) — `HeaderDelimitedMessageHandler`, `SystemTextJsonFormatter` (re-fetched; both confirmed).
- [StreamJsonRpc exception handling docs](https://microsoft.github.io/vs-streamjsonrpc/docs/exceptions.html) — unhandled target-method exceptions map to `JsonRpcErrorCode.InvocationError` (`-32000`) in the error response (the per-method error behavior in [protocol.md](protocol.md#error-and-cancellation-behavior)).
- [OmniSharp.Extensions.LanguageServer — NuGet](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer) — 0.19.9 still latest; dormancy confirmed. [Issue #1221](https://github.com/OmniSharp/csharp-language-server-protocol/issues/1221) — unresolved support-status question.
- [EmmyLua.LanguageServer.Framework — NuGet](https://www.nuget.org/packages/EmmyLua.LanguageServer.Framework) — 0.9.2 latest; the documented fallback.
- [LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) — the normative baseline (D5).
- [LSP 3.18 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.18/specification/) — verified still carrying the "under development" status line, while `specification-current` redirects to it (D5's evidence).
- [vscode-languageclient — npm](https://www.npmjs.com/package/vscode-languageclient) — 10.1.0 current; `engines.vscode ^1.91.0` (registry metadata).
- [@vscode/vsce — npm](https://www.npmjs.com/package/@vscode/vsce) — 3.9.2 current.
- [Publishing extensions — platform-specific](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#platformspecific-extensions) — `vsce publish --target`, the seven-target matrix.
- [Semantic Highlight Guide — VS Code API](https://code.visualstudio.com/api/language-extensions/semantic-highlight-guide) — semantic tokens layer on top of grammar scopes (D17's fallback basis).
- [What's new in the SDK for .NET 10 — platform-specific tools](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk#platform-specific-net-tools) — per-RID tool packaging (D19).
- [dotnet tool exec](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec) — the rejected one-shot path (D19).
- [Assembly unloadability in .NET](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability) — cooperative unload, `WeakReference` verification (D14).
- [AssemblyDependencyResolver](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblydependencyresolver) — dependency probing for loaded model assemblies (D14).
- [AssemblyLoadContext.LoadFromStream](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext.loadfromstream) — byte-stream loading into the default ALC (no file lock) and the default-probing-path precedence remark D23 footnotes (verified July 2026).
- [What's new in .NET 10 libraries — serialization](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries#serialization) — `AllowDuplicateProperties`, why not `Strict` (D6).
- [Nerdbank.Streams — NuGet](https://www.nuget.org/packages/Nerdbank.Streams) — 2.13.16; `FullDuplexStream.CreatePair()` for in-proc contract tests.
- [IDE support — templ docs](https://templ.guide/developer-tools/ide-support/) — the adoption precedent (roadmap grounding, carried).
