# Phase 3 — binding layer

## Header

- **Status:** proposed — not started
- **Goal (one line):** The generator and the runtime answer every binding question — extension
  identity and discovery, function exports, prop layouts, assignability, member paths, model type
  names — from one shared rule-core each, with the three live binding bugs (the false `HED7006`
  build error, the nested/generic AQN mismatch, the hand-mirrored `BranchRole` enum) fixed first
  and independently of any extraction.
- **Depends on:** Phase 4 — expression writers (owns `PrimitiveKind` + the implicit-numeric-widening
  table, the `MemberPathWalk`/`MemberVisibility` shared core, and the overload-rank core this phase's
  cross-container emission needs); Phase 1 — template emitter (owns `Data/RenderTypeRules.cs`,
  consumed not duplicated here, and is the second consumer of this phase's `PropLayoutCore`);
  Phase 5 — pipeline & config (owns the manifest `schemaVersion` constants — binding only if the
  prop-layout gauntlet row of OQ4 is adopted); Phase 6 — diagnostics & utilities (owns the
  `CSharpTypeNames` display tables; this phase owns the parse/binding direction of the same data).
  Source research: [03 — binding layer](../research/generator-code-sharing/03-binding-layer.md)
  (nine findings), synthesized in [07 — recommendations](../research/generator-code-sharing/07-recommendations.md).
- **Changes an externally-visible contract:** yes, behaviorally — no public API or template-syntax
  change, but (a) the false-`HED7006` fix makes builds succeed that today fail, (b) several fixes
  turn permanent silent fallbacks into working precompilation (gauntlet-visible: fallback events
  disappear, rendered bytes are unchanged because the precompiled tier must match the dynamic
  tier), (c) the `PropLayoutCore` unification corrects silent wrong output on drifted prop layouts
  toward the runtime's authoritative layout, and (d) if OQ4 is adopted, the precompiled manifest
  gains one additive row coordinated with Phase 5's `schemaVersion` constants. Analysis against
  [breaking-windows.md](../spec/common/breaking-windows.md) in *Back-compat / impact*: everything
  here is defect repair toward documented runtime behavior, shippable outside a breaking window.

## Goal

Area 03 of the research is the "same rule, two type systems" area: the build-time generator
answers over `ISymbol` the questions the runtime answers over `System.Type`, and the two
transcriptions have drifted in verified, user-visible ways. This phase delivers the binding
layer's share of the shared-code program in two independently shippable tranches.

**Tranche A — fix-first bug group.** Three confirmed defects ship before (and without) any
abstraction work:

1. **Inherited `[ExtensionName]` read** ([03 F3](../research/generator-code-sharing/03-binding-layer.md)).
   `ExtensionBinder.InspectType` reads the extension name with a declared-only
   `type.GetAttributes()` loop (`src/Heddle.Generator/Emit/ExtensionBinder.cs:199-209`,
   anchor `InspectType`) while the same file base-chain-walks for `[BranchRole]`, `[ScopeChannel]`
   and `[Prop]` (`ReadBranchRole` at `:249`, `HasAttribute` at `:266`, `ReadPropParameters` at
   `:285` — verified against source). The attribute is `Inherited = true` and the runtime reads it
   with `IsHaveAttribute<ExtensionNameAttribute>(true)`
   (`src/Heddle/Runtime/TemplateFactory.cs`, `LoadExtensions`), so `class MyIf : IfExtension`
   registers under `"if"` at runtime, takes the name over via the `IsAssignableFrom` override rule
   (`TemplateFactory.AddExtensions`), and is invisible to the generator. Consequences today: a
   bodied call on such a name raises **`HED7006` at Error severity — the build breaks on a
   template the runtime renders fine** (`Emit/TemplateEmitter.cs:650-654`,
   `Diagnostics/GeneratorDiagnostics.cs:44-48`, anchor `ExtensionNotBindable`), and where a base
   name is separately visible the manifest records the base type while the live registry holds the
   derived one — `ExtensionBindingMismatch` on every render. The fix is the same base-chain walk
   the file already uses three times; the declared-only read is an oversight, not a decision.
2. **AQN formatting for nested/generic types** ([03 F1](../research/generator-code-sharing/03-binding-layer.md)).
   The manifest identity string (`"<CLR full name>, <assembly simple name>"`) is produced by two
   unrelated formatters: Roslyn `FullyQualifiedFormat` minus `global::`
   (`Emit/ExtensionBinder.cs:211-216`, re-typed at `Binding/FunctionExportResolver.cs:123-127`
   and twice in `Emit/TemplateEmitter.cs:750-753`/`:790-793`, plus `Emit/NativeExpressionWriter.cs:144`)
   versus reflection `type.FullName + ", " + assembly`
   (`src/Heddle/Precompiled/PrecompiledGauntlet.cs:207-212`, anchor `AqnSansVersion`). For a
   nested type the two spell `Ns.Outer.Inner` vs `Ns.Outer+Inner`; for a generic container
   `Ns.C<T>` vs ``Ns.C`1`` — a permanent per-extension gauntlet mismatch with no build warning.
   Fixed by the shared `Precompiled/AqnFormatter.cs` (Tranche B artifact, but the *fix* — routing
   all five generator copies through it — ships with this tranche).
3. **`BranchRole` enum mirror** ([03 F9](../research/generator-code-sharing/03-binding-layer.md)).
   `Emit/ExtensionBinder.cs:10` hand-mirrors `Heddle.Attributes.BranchRole`'s numeric values,
   enforced only by a doc comment. Replaced by a linked `<Compile Include>` of the runtime enum —
   the same mechanism the generator already uses for `LinePosition`, `TemplateKey`,
   `DefaultFunctionTable` and friends (`src/Heddle.Generator/Heddle.Generator.csproj`, the
   `Shared\…` link block). The cheapest fix in the whole research report.

**Tranche B — shared rule-cores and abstractions.** The duplicated binding rules move into
shared netstandard2.0 files compiled into both assemblies as linked sources:
`Precompiled/AqnFormatter.cs` (F1); the `[ExportFunctions]` discovery rule-core over an
`ExportedMethodFacts` record with merge bookkeeping (F2); the extension-discovery predicate and
precedence table over an `ExtensionCandidate` record (F3); `PropLayoutCore<TType>` with an
ordered fault enum (F4 — the highest-payoff extraction: slot indices are the wire format between
the generator's frozen `object[]` prototype and the runtime's `ExtensionParameterCarrier`, and
drift there is silent wrong rendered output with **no gauntlet coverage**); the
`ITypeFacts<TType>`/`IMemberFacts` abstraction including the assignability relation with its
nullable corrections stated once in the Roslyn adapter (F6), backed by a shared
`(source, target, expected)` assignability conformance corpus run by both tiers; the
inherited-attribute/render-type predicates rule-core (F9, consuming Phase 1's
`Data/RenderTypeRules.cs`); and the model type-name work (F8): the reflection-free spelling
parser split out of `ReflectionHelper`, the keyword table linked over Phase 4's `PrimitiveKind`,
and the lookup-ordering/ambiguity-policy alignment.

The contract details of the abstraction and the rule-by-rule parity targets live in two
supplements: [the ITypeFacts abstraction contract](phase-3-binding-layer-typefacts.md) and
[the discovery-rules parity tables](phase-3-binding-layer-discovery-parity.md).

## Non-goals / scope boundary

- **No expression-semantics work.** Operator classification, overload ranking, literal
  round-tripping, and the `MemberPathWalk`/`MemberVisibility` shared core belong to Phase 4.
  This phase *adopts* that core in `SymbolTypeResolver`/`SymbolMemberResolver` once it exists
  (F7 is co-owned: Phase 4 builds the core, this phase owns the generator-side adoption and the
  accessibility-policy decision, OQ1).
- **No `PrimitiveKind` or widening-table authorship.** Phase 4 owns the shared `PrimitiveKind`
  enum and the C# §10.2.3 implicit-numeric-widening table (F5); this phase consumes them for
  prop-default conversion (`DefaultConvertible`/`PropConversion` alignment) and for the keyword
  table's value type. Recorded under *Dependencies & ordering*.
- **No render-type rule authorship.** Phase 1 owns `Data/RenderTypeRules.cs` (the
  `Derive(hasEncodeOutput, hasNotEncode)` rule); this phase contributes only the shared
  inherited-attribute-walk predicates that feed it and deletes the generator's local twin.
  Coordinate, don't duplicate.
- **No display-direction type-name tables.** Phase 6 owns `CSharpTypeNames` (type → C# spelling,
  used by diagnostics and hovers); this phase owns the parse/binding direction (spelling → type)
  and shares the underlying alias data with Phase 6 rather than growing a second table.
- **No manifest schema redesign.** If OQ4 lands, the prop-layout row is one additive row under
  Phase 5's `schemaVersion` regime; nothing else about the manifest changes in this phase.
- **No new language surface, no grammar change, no new template syntax.** The grammar-stability
  gate of the [testing standards](../spec/common/testing-standards.md) applies unmodified.
- **No runtime behavior changes outside the decided open questions.** The runtime is the
  authority this phase aligns *to*; the only places the runtime itself might move are the
  explicitly flagged authority questions (OQ1, OQ5), and each of those recommends *not* moving
  it in this phase.
- **No shared-project restructuring.** The linked `<Compile Include>` mechanism is the proven
  precedent; a dedicated `Heddle.Shared` project is out of scope until the cross-phase shared
  set outgrows it (the [07 synthesis](../research/generator-code-sharing/07-recommendations.md)
  threshold: ~20 files, revisited program-wide, not per phase).

## Design direction

**Fixes ship before abstractions.** Tranche A is deliberately extraction-free: the inherited-name
fix is a four-line loop change to the base-chain-walk shape already present in the same file; the
`BranchRole` link is one csproj line plus one deletion; the AQN fix routes five call sites
through one new shared file whose whole surface is a single pure function. Each is independently
shippable and independently testable, and none blocks on Phase 4. This mirrors the program-wide
sequencing rule ([07 — recommended sequencing](../research/generator-code-sharing/07-recommendations.md)):
confirmed live drift is fixed regardless of any extraction.

**The sharing mechanism is linked sources, netstandard2.0, adapters per side.** Hard constraints,
non-negotiable for every shared file this phase adds:

- Shared files compile under `netstandard2.0` and are added to `Heddle.Generator.csproj` as
  `<Compile Include="..\Heddle\…" Link="Shared\…" />` items — the existing pattern
  (`DefaultFunctionTable`, `TemplateKey`, `HeddleDiagnosticIds`, …).
- **No Roslyn types in shared files, no reflection-only assumptions either.** A shared file
  references neither `Microsoft.CodeAnalysis` nor `System.Reflection` beyond what
  netstandard2.0's core surface makes unavoidable; the type-system-facing seams go through
  `ITypeFacts<TType>`/`IMemberFacts`.
- The **Roslyn adapter lives in `Heddle.Generator`**, the **reflection adapter lives in
  `Heddle`**; the shared file contains neither. The generator takes no reference to `Heddle.dll`
  — everything it knows about the runtime it knows from linked source and symbol metadata.

**AqnFormatter: one identity function, both sides map into it.** New shared file
`src/Heddle/Precompiled/AqnFormatter.cs`. The research sketches
`Format(ns, nestingChainOutermostFirst, arity, assemblyName)`; source verification refines this —
a nested generic type's CLR name carries *per-segment* declared arity
(``Ns.Outer`1+Inner`1``), and both `Type.Name` and `ISymbol.MetadataName` already yield the
backtick-suffixed per-segment metadata name. The shared function therefore takes
`(string @namespace, IReadOnlyList<string> metadataNamesOutermostFirst, string assemblyName)`
and owns exactly the join rules: `.` between namespace and first segment, `+` between nesting
segments, `", "` before the assembly simple name. The runtime's
`PrecompiledGauntlet.AqnSansVersion` and the pinned constant at
`src/Heddle/Precompiled/DefaultFunctionTable.cs:40` (`ShimTargetTypeName`) are re-expressed
against it (the constant gains a lockstep test rather than a runtime rewrite — it is a `const`
in a linked file); the generator's five copies are deleted. The exact signature is a spec
decision; the plan pins the shape (pure, allocation-light, per-segment metadata names) and the
conformance obligation: for every corpus type, `AqnFormatter` output equals
`type.FullName + ", " + assembly` from live reflection.

**Export discovery: one eligibility-and-merge rule, and the silent-skip drift surfaces as a
diagnostic.** The runtime's rule (verified: `src/Heddle/Runtime/Expressions/FunctionRegistry.cs`,
`RegisterContainer` at `:127-156`, `Register` eligibility rejections at `:76-96`, `AddOrReplace`
merge at `:182-200`) is: container must be a public static class *or the export throws*; methods
`Public|Static|DeclaredOnly`, `IsSpecialName` skipped; per method — not open generic, not `void`,
no `ref`/`out`/pointer parameters, else `ArgumentException`; name `ToLowerInvariant`; second
container exporting the same name **merges** overloads (replace only on identical signature).
The generator's transcription (`Binding/FunctionExportResolver.cs`, `AddContainer` at `:99-138`)
silently skips ineligible containers, counts methods the runtime refuses (its `overloadCounts`
at `:117` includes `void`/generic/by-ref methods), and gives the whole name to the first
container. Because the gauntlet compares overload counts **exactly** — verified: both `>` and
`<` fail at `src/Heddle/Precompiled/PrecompiledGauntlet.cs:147-158` — one `void Log(string)`
helper in an export container today permanently un-precompiles every template calling any
function from it. The shared rule-core evaluates an `ExportedMethodFacts` record
(`isStatic, accessibility, isOpenGeneric, returnsVoid, hasByRefOrPointerParam, isSpecialName`),
applies the name rule, and runs the merge bookkeeping that produces manifest rows; each side's
adapter is a thin fact-mapper (the `MethodKind.Ordinary` ↔ `!IsSpecialName` correspondence is
stated once, in the Roslyn adapter, next to a comment naming the residual difference). The
runtime's throw-vs-silent-skip asymmetry becomes a build diagnostic (severity per OQ6's
recommendation; ID claimed from the `HED70xx` block per
[D1](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) at spec time —
`HED7017` is the last claimed ID, so the claim will be `HED7018`+, recorded in the registry in
the same change). Cross-container precedence follows OQ2. Per-dimension parity targets:
[discovery parity tables](phase-3-binding-layer-discovery-parity.md).

**Extension discovery: separate "what registers" from "what the generator can bind".** The
runtime predicate (verified: `TemplateFactory.LoadExtensions`) is *implements `IExtension`* +
*carries `[ExtensionName]` with `inherit: true`*; ordering by `[DataType]`/`[ChainedType]`
interface-ness; `AddExtensions` sorts `Replace` last and overrides on `Replace` or
`incumbent.IsAssignableFrom(candidate)`, else `TemplateOverrideException`. The generator's
predicate is narrower on **two independent axes** that today are conflated: *discovery* (it
requires `AbstractExtension` derivation and a declared-only name — so runtime-registered types
are invisible, producing the false `HED7006`) and *bindability* (its emitted code reproduces the
`AbstractExtension` render protocol, so an `IExtension`-direct implementor genuinely cannot be
bound). The shared rule-core implements the **runtime** predicate over an `ExtensionCandidate`
record (name list from the inherited walk, assembly, assignability-to-incumbent via
`ITypeFacts`), and the generator layers its bindability test *after* discovery: a name that
resolves under the runtime predicate but is not bindable **degrades to dynamic with a recorded
reason — never `HED7006`**. `HED7006` fires only when the name resolves to nothing under the
runtime's own rule, restoring its documented meaning ("the runtime will not find it either").
Precedence (`Replace`, assignability override, tie-break ordering) follows OQ3.

**PropLayoutCore: the wire format gets one implementation.** New shared
`PropLayoutCore<TType>.Build(layers, ITypeFacts<TType>, faultSink)` implements the layer walk
and slot indexing exactly once, with an **ordered fault enum** so both sides report the same
fault class in the same declaration order. Rules pinned to the runtime as authority (all
verified against `src/Heddle/Runtime/Expressions/PropLayout.cs`, `ResolveFromExtension` at
`:83-179`):

- the layer walk stops at `typeof(object)` (`PropLayout.cs:92`); the generator's unconditional
  walk (`ExtensionBinder.cs:285`, `t != null`) is corrected by the shared core;
- faults **accumulate per declaration and the walk continues** (the runtime's
  `continue`-per-fault shape); the generator's break-at-first-fault
  (`Emit/TemplateEmitter.cs:824-935`, `ResolveExtensionPropLayout`) is replaced;
- the unusable-type predicate is unified: runtime rejects
  `null / ContainsGenericParameters / IsPointer / IsByRef` (`PropLayout.cs:134`), the generator's
  local variant (`ExtensionBinder.cs:304-307`) differs (`IsUnboundGenericType` is narrower than
  `ContainsGenericParameters`; the by-ref arm is missing) — the shared predicate is expressed
  over `ITypeFacts` so neither side re-transcribes it;
- validation order per declaration (name validity → reserved names → same-level duplicate →
  unusable type → redeclaration assignability → default application) and the
  keep-base-slot-index redeclaration rule are the runtime's, verbatim;
- default application order matches the runtime (`ApplyDefault` semantics; the widen-then-box
  value rule consumes Phase 4's `PrimitiveKind` conversion table — see *Dependencies*).

Phase 1's emitter consumes the same core for **definition-side** layouts (`PropLayout.Resolve`'s
twin at `TemplateEmitter.ResolveExtensionPropLayout` and the definition path); this phase owns
the core and the extension-attribute-side adoption on both tiers, Phase 1 owns its emitter call
sites. Because prop-layout drift is **not** gauntlet-covered (verified:
`PrecompiledGauntlet.cs` checks options, extension identity, functions, staleness — no
parameter-index row), OQ4 proposes the missing guard.

**ITypeFacts/IMemberFacts: the relation lives in the adapters, the rules live in shared code.**
The assignability relation cannot be shared imperatively — it *is* the type graph. The shared
abstraction (full contract in [the ITypeFacts supplement](phase-3-binding-layer-typefacts.md))
carries `IsAssignableFrom`, `TryGetNullableUnderlying`, `IsInterface`, `IsValueType`, and
`PrimitiveKind` classification; the reflection adapter is a thin veneer over
`Type.IsAssignableFrom` (`Helpers/TypeExtension.cs:14-23`, `IsType`); the Roslyn adapter is
where the two known Roslyn-vs-CLR nullable corrections live (`int`→`int?` classified
`ImplicitNullable` but CLR-assignable; `int?`→`IComparable` classified boxing but **not**
CLR-assignable — today hand-written in `TemplateEmitter.RedeclarationAssignable` at
`:993-1025` with a second partial encoding inside `DefaultConvertible` and a third `Nullable<T>`
spelling in `SymbolTypeResolver.IsNonNullableValueType` at `:236-244`, which uses
`ConstructedFrom` where the emitter uses `OriginalDefinition`). All three spellings collapse
into the one adapter. Because a *third* CLR-vs-Roslyn disagreement (variance with value-type
arguments, `ValueTuple` conversions) would only surface by testing, the phase ships a shared
**assignability conformance corpus**: one data file of `(source, target, expected)` rows,
executed by a reflection-side test and a symbol-side test in the
`DefaultFunctionLockstepTests` mold (`src/Heddle.Tests/DefaultFunctionLockstepTests.cs`).

**Member paths (F7): adopt Phase 4's core, decide the policy here.** The three verified
visibility divergences (generator accepts `Accessibility.ProtectedOrInternal` where the
runtime's `getter.IsAssembly || getter.IsPublic` is false — `SymbolTypeResolver.cs:224-226` vs
`Runtime/Expressions/MemberPathResolver.cs:123`; the generator's `FindProperty` walks
`AllInterfaces` where `Type.GetProperty` on an interface does not walk base interfaces —
`SymbolTypeResolver.cs:204-214`; `[Hidden]` matched by unqualified attribute *name* at build
time — `SymbolTypeResolver.cs:229` — vs the real `Heddle.Attributes.HiddenAttribute` at run
time) all point the dangerous direction: the generator is more permissive, and because the same
resolver drives *emission*, extra permissiveness becomes emitted typed code that renders values
the dynamic tier rejects — a sandbox-contract bypass, flagged encoding/sandbox-relevant in
[07's live-drift table (row 4)](../research/generator-code-sharing/07-recommendations.md).
Phase 4 owns the shared `MemberPathWalk` + `MemberVisibility` decision table (the walk, the
dynamic-hop split, the failure index, the `MemberAccess` enum both sides map into); this phase
owns re-expressing `SymbolTypeResolver.ResolvePath`/`FindProperty`/`IsAccessible` and
`SymbolMemberResolver` as the Roslyn adapter of that core, and owns the accessibility-policy
call (OQ1). The `[Hidden]` match moves to a fully-qualified metadata-name comparison on the
symbol side regardless of OQ1's outcome — matching any `HiddenAttribute` from any namespace is
not defensible in either direction.

**Model type names (F8): make the generator parse what the runtime parses, and never bind what
the runtime would call ambiguous.** Verified: the runtime's `ReflectionHelper` resolves
generic/array/tuple/nested spellings via a **reflection-free** parser
(`ExtractGenericArguments` at `:342`, `TryFindMatchingAngleBracket` at `:373`,
`SplitTopLevelArguments` at `:395`), while the generator's `ResolveModelType`
(`SymbolTypeResolver.cs:57-101`) supports none of them — whole feature areas silently never
precompile (fallback-safe, but permanent). The parser is split out of `ReflectionHelper` into a
shared file and linked — directly sharable imperative code, zero reflection in it. The keyword
table becomes shared data over Phase 4's `PrimitiveKind` (note the verified asymmetry: the
runtime table contains `"dynamic"` → `typeof(object)`, `ReflectionHelper.cs:49`; the
generator's `Keywords` table omits it), co-owned with Phase 6 (they own the display direction;
one data source, two projections). Lookup ordering is a small shared rule-core over
`ITypeLookup.TryResolve(metadataName)`: the runtime resolves a bare short name by scanning
loaded assemblies and **throws "ambiguous"** on ties (`ReflectionHelper.cs:235-241`), while the
generator picks the first `@using` match then falls back to implicit
`System`/`System.Collections.Generic` (`SymbolTypeResolver.cs:92-98`) — namespaces the runtime
does *not* treat as implicit despite the comment above them claiming otherwise. That is the one
non-fallback-safe edge in F8: `:: List` (or any short name colliding with a `System` type) can
bind to **different types on the two tiers**, and emitted typed code then types member hops off
the wrong type. Policy per OQ5.

**Diagnostics discipline.** Any new or changed diagnostic follows
[D1](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx): IDs claimed
from the `HED70xx` generator block in the registry in the same change as the owning spec, with
message text, trigger, and position semantics in that spec's *Diagnostics* section. This phase
anticipates at most two claims (ineligible-export-container per OQ6; optionally an
ambiguous-type-name informational per OQ5) and **removes no shipped ID** — `HED7006` keeps its
ID and message with a corrected (narrower) trigger condition, which is a trigger fix, not a
re-numbering.

## Dependencies & ordering

- **Depends on Phase 4 for:** the shared `PrimitiveKind` enum and implicit-numeric-widening
  table (consumed by prop-default conversion and the keyword table — this phase must not ship
  its own numeric table); the `MemberPathWalk`/`MemberVisibility` core (consumed by the F7
  adoption work item); the overload-rank core (consumed by cross-container export *emission*
  under OQ2's recommendation — manifest bookkeeping does not wait for it, emission of
  multi-container names does).
- **Depends on Phase 1 for:** `Data/RenderTypeRules.cs` (the render-type derivation rule this
  phase's inherited-attribute predicates feed). Conversely, **Phase 1 depends on this phase
  for** `PropLayoutCore<TType>` (its emitter consumes the core for definition layouts) and
  `AqnFormatter` (its manifest builders are two of the five deleted copies).
- **Depends on Phase 5 only conditionally:** the OQ4 prop-layout manifest row is an additive
  schema change under Phase 5's `schemaVersion` constants; if OQ4 is declined, there is no
  Phase 5 edge.
- **Coordinates with Phase 6 on:** the keyword/alias data (this phase: parse direction; Phase 6:
  display direction — one linked data source).
- **Internal ordering (Tranche A has no prerequisites and starts immediately):**
  1. Tranche A fixes — inherited-name walk, `BranchRole` link, `AqnFormatter` + call-site
     routing (the formatter is authored here because the fix needs it; it is also the first
     Tranche B artifact).
  2. `ITypeFacts`/`IMemberFacts` + reflection/Roslyn adapters + assignability conformance
     corpus (the enabler for everything below in this phase).
  3. Export-discovery rule-core + merge bookkeeping + the OQ6 diagnostic (highest gauntlet
     leverage after Tranche A — the exact-count comparison makes any disagreement load-bearing).
  4. Extension-discovery rule-core + precedence (needs the assignability edge from step 2;
     completes the `HED7006` semantics started in Tranche A).
  5. `PropLayoutCore<TType>` (needs step 2; needs Phase 4's `PrimitiveKind` for the
     default-conversion arm — the layout/indexing algorithm itself does not, so the core can
     land with conversion behind a seam if Phase 4 trails).
  6. F8 type-name work — spelling-parser split (independent, can run any time after Tranche A),
     keyword table (after Phase 4's `PrimitiveKind`), lookup-ordering core (after step 2).
  7. F7 adoption — after Phase 4's core exists; last because it is adoption, not authorship.
- **Unblocks:** Phase 1's definition-layout consumption of `PropLayoutCore`; Phase 5's gauntlet
  row implementation (if OQ4 adopted); the program-wide deletion of the generator's private
  binding twins.

## Back-compat / impact

Analyzed against [breaking-windows.md](../spec/common/breaking-windows.md) and
[D2](../spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows).
The governing question per item: does it change behavior a user could *correctly* depend on, or
does it repair divergence from the documented, runtime-authoritative behavior?

- **False-`HED7006` fix: un-breaks builds.** Builds that today fail on templates the runtime
  renders fine start succeeding. Strictly additive in the compatibility sense (no correct build
  regresses); no window needed. The narrowed trigger is recorded against `HED7006`'s
  documentation in [precompilation.md](../precompilation.md) in the same change.
- **Silent fallback → working precompilation** (inherited names, AQN, export counts,
  discovery). Rendered bytes are unchanged by construction — the gauntlet's whole contract is
  that the precompiled tier matches the live registry or falls back — but *which tier renders*
  changes, which is observable via `PrecompiledFallbackEvent` telemetry and performance. This is
  the feature working as documented; additive, no window. The gauntlet-visible flip is called
  out in release notes.
- **`PropLayoutCore` adoption can change precompiled rendered output** — from silently-wrong
  slot assignments to the runtime's authoritative layout. Any template affected was rendering
  *differently under precompilation than under dynamic compilation*, which is the defect class
  this repo treats as always-fixable (the dynamic tier is the semantic definition). Not a
  breaking-window item; the differential fixture set plus (per OQ4) the new gauntlet row prove
  the direction of every change. **Note explicitly: prop-layout drift is not gauntlet-covered
  today**, so without OQ4 the only guard is the test suite — a strong argument for OQ4's
  recommended "yes".
- **Manifest schema (conditional).** OQ4's row is additive: new-generator manifests carry the
  row, the gauntlet checks it when present, and manifests without it (older generators) pass the
  check vacuously — schema evolution stays within Phase 5's `Min`/`Max` compatibility
  predicate. No re-precompilation is forced.
- **Runtime behavior: unchanged by default.** Every alignment in this phase moves the
  *generator* toward the runtime. The two places the runtime itself is questioned (OQ1
  visibility, OQ5 ambiguity policy) both recommend leaving the runtime as-is in this phase, with
  any widening recorded as a candidate in the
  [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register)
  only if a spec later ratifies it.
- **Public API surface:** no changes. Shared files are `internal`; adapters are `internal`;
  the `TemplateOptions` completeness invariant is untouched.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| A third Roslyn-vs-CLR assignability disagreement beyond the two known nullable corrections silently re-diverges the tiers | The assignability conformance corpus is a *data file* executed by both a reflection-side and a symbol-side test; adding a suspected row is a one-line change; the corpus seeds include variance-with-value-type-arguments and `ValueTuple` rows precisely because the research names them as untested | M |
| `AqnFormatter` mis-handles an exotic identity (generic type *definition* vs constructed, `+` in a namespace-less global type, multi-level nesting with mixed arity) and converts a working fallback into a wrong manifest string | The formatter's conformance test enumerates real types from the test assemblies (nested, generic-container, global-namespace) and asserts byte-equality against live `Type.FullName + ", " + assembly`; the five call sites are routed one at a time with the gauntlet suite green between each | M |
| Cross-container merge (OQ2) without Phase 4's overload-rank core leads the generator to emit a call the runtime would resolve to a different overload | Split adopted in OQ2's recommendation: merge bookkeeping (manifest rows) lands now — fixing the gauntlet count mismatch — while *emission* for names exported by more than one container degrades to dynamic with a recorded reason until the rank core exists; degrade is fallback-safe by construction | M |
| `PropLayoutCore` adoption changes a shipped precompiled template's output where the old generator's layout was wrong | That output was already diverging from the dynamic tier (the authoritative semantics); differential fixtures pin every affected shape (deep chains past `object`, multi-fault declarations, inherited re-declarations), and OQ4's gauntlet row turns any future recurrence into a visible fallback instead of silent wrong output | M |
| Tightening generator visibility to the runtime's rule (OQ1) makes templates that today *precompile and render* fall back or fail typed emission | The direction is mandated by the sandbox precedence rule (security beats ergonomics, [coding standards](../spec/common/coding-standards.md#precedence-when-principles-conflict)); affected members were rendering on the precompiled tier only — the dynamic tier already rejected them, so no template gains a new dynamic-tier error; differential fixtures enumerate `protected internal`, interface-inherited, and `[Hidden]`-foreign-namespace members | S |
| Linked shared files accidentally acquire a Roslyn or modern-BCL dependency and break the netstandard2.0 / analyzer packaging constraint | A build-enforced guard: the shared files compile in `Heddle` (netstandard2.0 TFM) by construction, and a test asserts the generator project's shared-link set matches the declared list; review checklist item: no `using Microsoft.CodeAnalysis` outside `Heddle.Generator` proper | S |
| The `HED7006` trigger narrowing hides a genuine typo (name resolves under the runtime predicate to an unbindable type, degrades quietly, user expected an error) | The degrade records a reason surfaced through the existing generator reporting channel (same mechanism as other bind refusals, e.g. `HED7015`'s neighborhood); the spec decides whether a low-severity info diagnostic accompanies the degrade — quiet degrade is the runtime-faithful default | S |
| Ordering coupling: this phase stalls on Phase 4's `PrimitiveKind`/`MemberPathWalk` | Tranche A, `ITypeFacts`, both discovery cores, and the spelling parser have no Phase 4 edge; `PropLayoutCore`'s conversion arm sits behind a seam so the core lands independently; only the F7 adoption item hard-waits | S |
| Two phases editing the same files (`TemplateEmitter`, `ExtensionBinder`) collide | Ownership split is by rule, not by file: this phase owns binding-rule call sites, Phase 1 owns emission shapes; the [D5](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order) declared order plus the amendments ledger arbitrate any overlap discovered at spec time | S |

## Success criteria

Measurable, checkable statements a spec can turn into tests.

- [ ] A fixture assembly with `class MyIf : IfExtension` (no declared `[ExtensionName]`)
      building a template with a bodied `if`-family call **compiles without `HED7006`**, its
      manifest records the derived type's AQN, and the gauntlet passes against a live registry
      where the derived type has taken over the name.
- [ ] `HED7006` still fires, at the call position, for a bodied call whose name matches **no**
      type under the *runtime* discovery predicate (the true-positive case is pinned by a
      negative test asserting ID + position per the
      [testing standards](../spec/common/testing-standards.md)).
- [ ] The `BranchRole` mirror at `ExtensionBinder.cs:10` is deleted; `Heddle.Attributes.BranchRole`
      is compiled into the generator via a `<Compile Include>` link; a repo-wide search finds
      exactly one definition of the enum.
- [ ] For every type in the AQN conformance set (non-nested, nested, doubly-nested,
      generic-container, generic-nested, global-namespace), the shared `AqnFormatter` output is
      byte-equal to `type.FullName + ", " + type.Assembly.GetName().Name`; the generator
      contains zero remaining inline `global::`-strip AQN constructions (verified by search:
      the five sites at `ExtensionBinder.cs:211-216`, `FunctionExportResolver.cs:123-127`,
      `TemplateEmitter.cs:750-753`/`:790-793`, `NativeExpressionWriter.cs:144` are gone).
- [ ] An export container containing a `void` method, an open-generic method, a by-ref-parameter
      method, and a property (`IsSpecialName`) produces manifest overload counts equal to what
      `FunctionRegistry.RegisterContainer` actually registers; the previously-failing gauntlet
      exact-count comparison (`PrecompiledGauntlet.cs:147-158`) passes.
- [ ] Two containers exporting the same function name produce manifest rows matching the
      runtime's merged registry; templates calling that name either emit correctly (rank core
      available) or degrade to dynamic with a recorded reason — in both cases the gauntlet
      reports no `FunctionBindingMismatch`.
- [ ] An ineligible export container (non-public or non-static) produces the OQ6 diagnostic at
      build time with a claimed registry ID — no silent skip.
- [ ] `PropLayoutCore<TType>` is the only implementation of extension prop-layout sequencing:
      the generator's `ResolveExtensionPropLayout` layout logic and `ExtensionBinder`'s layer
      walk delegate to it, `PropLayout.ResolveFromExtension` delegates to it, and a lockstep
      test proves identical slot order and identical ordered fault sequences for the
      differential fixture set (deep chain past `object`, multi-fault declaration lists,
      inherited re-declaration with default re-application, unusable-type variants incl.
      by-ref).
- [ ] The assignability conformance corpus (one data file) runs green under both the
      reflection-side and the symbol-side driver, including the two nullable-correction rows
      and the variance/`ValueTuple` probe rows; the corrections exist in exactly one place (the
      Roslyn adapter).
- [ ] `SymbolTypeResolver`/`SymbolMemberResolver` delegate member-path resolution to Phase 4's
      shared core; the three F7 divergences are closed per OQ1's decided policy, and the
      `[Hidden]` check matches the fully-qualified attribute name on both tiers.
- [ ] The generator resolves `List<int>`, `int[]`, `(int, string)`, and dotted-nested
      generic spellings to the same types as `ReflectionHelper.ResolveType` for a shared
      name-corpus (lockstep test); `"dynamic"` resolves on both tiers; and for every corpus name
      the runtime resolves as *ambiguous*, the generator declines to bind (degrades) rather than
      picking a candidate.
- [ ] No shared file added by this phase references `Microsoft.CodeAnalysis`; all shared files
      compile under netstandard2.0 in `Heddle` and are linked (not copied) into
      `Heddle.Generator`; the generator still takes no reference to `Heddle.dll`.
- [ ] Full regression gate green in one combined run: solution build all TFMs, full test suite,
      goldens byte-identical for untouched templates, grammar-stability check (no grammar
      change), benchmarks on the touched compile paths within the
      [testing-standards](../spec/common/testing-standards.md#regression-gates) acceptance.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| Template with a bodied call to `"if"`; referenced assembly registers `MyIf : IfExtension` with no declared name | Build succeeds (no `HED7006`); manifest extension row = `MyIf`'s AQN; render precompiled; gauntlet green |
| Extension declared as `namespace N { class Outer { class InnerExt : AbstractExtension … } }` | Manifest AQN `N.Outer+InnerExt, <asm>`; gauntlet identity check passes on first render |
| Export container gains `public static void Log(string s)` | Overload counts unchanged in the manifest (void method excluded on both tiers); no `FunctionBindingMismatch`; a lockstep test compares generator counts to `RegisterContainer`'s registrations |
| Second assembly exports `slugify` from a different container | Manifest carries both containers' rows per the merged registry; call emission degrades to dynamic (pre-rank-core) with a recorded reason; gauntlet green |
| `[ExportFunctions(typeof(InternalHelpers))]` where the container is `internal` | OQ6 diagnostic at build time naming the container; runtime behavior unchanged (still throws in `RegisterFrom`) |
| Extension inherits `[Prop("x", typeof(int))]` and re-declares `x` as `long` | Same ordered fault (`PropRedeclarationMismatch` class) from both tiers via the shared fault enum; identical slot layout on the compliant variant |
| Extension base chain of depth 4 where a base beyond the third layer declares props | Identical slot indices from both tiers (generator no longer walks past `object`; runtime rule authoritative) |
| Prop declaration list with two faults in one layer | Both faults reported, in declaration order, by both tiers (accumulate-and-continue; the generator no longer stops at the first) |
| Model member path through a `protected internal` getter | Per OQ1's decided policy — recommended: generator refuses typed emission (degrades), matching the dynamic tier's rejection; differential fixture asserts both tiers agree |
| `:: List` under `@using System.Collections` plus a host assembly also defining `List` | Generator declines to bind (degrade + per-OQ5 diagnostic); no typed code emitted off a possibly-wrong type; runtime behavior (ambiguity error) unchanged |
| `:: Dictionary<string, List<int>>` as a model type | Generator resolves it via the shared spelling parser to the same constructed type the runtime resolves; template precompiles where it previously fell back |
| Corrupting one row of the assignability corpus data file | Both the reflection-side and the symbol-side test fail on that row (proves both drivers actually consume the file) |

## Open questions

Genuine behavior decisions, each with a recommended answer. Per the
[spec conventions](../spec/common/spec-conventions.md#no-open-questions), the owning spec closes
every one of these as a decision record; the recommendations below are the plan's leans.

1. **OQ1 — Should `protected internal` getters be visible to member paths?** The generator
   accepts `Accessibility.ProtectedOrInternal` (`SymbolTypeResolver.cs:224-226`); the runtime's
   `getter.IsAssembly || getter.IsPublic` (`MemberPathResolver.cs:123`) excludes `FamORAssem`.
   `protected internal` is at least as accessible as `internal`, so the runtime's exclusion is
   arguably its own oversight — but the runtime is the sandbox boundary, and widening it is a
   security-surface change. **Recommended: the runtime is authoritative — not visible.** The
   generator tightens to match; the shared `MemberAccess` decision table (Phase 4) carries an
   explicit `ProtectedOrInternal → hidden` row so the call is documented, not incidental.
   Widening *both* tiers together is recorded as a candidate in the
   [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register)
   only if a spec ratifies the demand.
2. **OQ2 — Export precedence: first-container-wins or merge?** The runtime merges overloads
   across containers (`AddOrReplace`, replace only on identical signature); the generator gives
   the whole name to the first container (`FunctionExportResolver.cs:129-137`). **Recommended:
   merge — the runtime's registry semantics are the public contract** (`Register`'s documented
   behavior). Adopt merge in the shared bookkeeping so manifest rows match the live registry
   immediately; for *emission*, a name exported by more than one container degrades to dynamic
   until Phase 4's overload-rank core lets the generator prove which overload the runtime would
   pick. The degrade is fallback-safe; the manifest fix alone kills the standing gauntlet
   mismatch.
3. **OQ3 — `[ExtensionReplace]`: adopt or refuse-to-precompile?** The generator has no
   counterpart to `Replace`-last ordering and the `IsAssignableFrom` override rule (acknowledged
   at `ExtensionBinder.cs:234-235`). **Recommended: adopt the full runtime precedence in the
   shared `ExtensionCandidate` rule-core.** The assignability edge comes free from `ITypeFacts`
   (F6), the `[DataType]`/`[ChainedType]` interface-ness tie-break is readable from symbol
   metadata, and refuse-to-precompile would permanently un-precompile every host that uses
   `Replace` — a standing feature gap for the cost of one shared table. If spec verification
   finds the tie-break unreproducible symbolically in some corner, that corner (only) degrades
   to dynamic with a recorded reason.
4. **OQ4 — Add a prop-layout manifest row + gauntlet check?** Prop layouts are the one
   wire-format contract with **no** gauntlet coverage — drift is silent wrong output
   (`PrecompiledGauntlet.cs:21-56` checks options, extension identity, functions, staleness
   only). **Recommended: yes.** One additive manifest row per bound parameter-declaring
   extension — a fingerprint of the slot layout (ordered names + `AqnFormatter`-formatted slot
   types) — compared by the gauntlet like the extension-identity rows; mismatch → fallback, not
   wrong output. This is an additive schema change owned by Phase 5's `schemaVersion`
   constants and compatibility predicate; the check is vacuous for manifests predating the row.
   The alternative (test-suite-only coverage) leaves the strongest silent-wrong-output surface
   in the program unguarded at run time.
5. **OQ5 — Model type-name ambiguity and implicit namespaces: what may the generator bind?**
   The runtime scans loaded assemblies for bare names and throws on ambiguity
   (`ReflectionHelper.cs:235-241`); it has no implicit namespaces. The generator's implicit
   `System`/`System.Collections.Generic` fallback can bind a *different type* than the dynamic
   tier — the only non-fallback-safe F8 edge. **Recommended: the generator adopts the runtime's
   lookup order via the shared rule-core, drops its unilateral implicit-namespace fallback, and
   on any name whose reference-set resolution is ambiguous (or merely unprovable from
   references, which see fewer assemblies than the runtime's loaded-assembly scan) declines to
   bind — degrade to dynamic, optionally with a low-severity informational diagnostic.** The
   generator must never emit typed code off a type identity the runtime might not choose.
   Runtime ambiguity behavior (throw) is unchanged.
6. **OQ6 — Ineligible export container: what does the build say?** Runtime: hard
   `ArgumentException` at `RegisterFrom` time; generator today: silent skip, masking a host
   configuration error until first run. **Recommended: a new warning-severity diagnostic**
   (ID claimed from `HED70xx` — next free `HED7018` — in the owning spec's registry update),
   positioned on the template(s) whose compilation consulted the resolver, naming the container
   and the runtime consequence. Warning, not error, because the build cannot prove the host
   ever calls `RegisterFrom` on that assembly; the message points at the remedy per the
   [coding standards](../spec/common/coding-standards.md#error-handling-and-diagnostics).

## External grounding

All source claims re-verified against the working tree on 2026-07-25 while authoring this plan
(line numbers are anchored to the named members and must be re-confirmed at spec time per the
*(verify at implementation)* convention).

| Claim | Source |
|---|---|
| Declared-only `[ExtensionName]` read vs base-chain walks for `[BranchRole]`/`[ScopeChannel]`/`[Prop]` in the same file | [`ExtensionBinder.cs`](../../src/Heddle.Generator/Emit/ExtensionBinder.cs) — `InspectType` `:199-209` (declared-only `type.GetAttributes()`), `ReadBranchRole` `:249`, `HasAttribute` `:266`, `ReadPropParameters` `:285` (base-chain walks); [03 F3](../research/generator-code-sharing/03-binding-layer.md) |
| Runtime discovery: `IExtension` + inherited name, ordering, `Replace`-last, `IsAssignableFrom` override, `TemplateOverrideException` | [`TemplateFactory.cs`](../../src/Heddle/Runtime/TemplateFactory.cs) — `LoadExtensions`, `AddExtensions`; [`TypeExtension.cs`](../../src/Heddle/Helpers/TypeExtension.cs) — `IsHaveAttribute`/`GetAttributes` with `inherit: true` |
| `HED7006` fires at Error severity for bodied calls only; degrade path for bodiless calls | [`GeneratorDiagnostics.cs`](../../src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs) — `ExtensionNotBindable` `:44-48`; [`TemplateEmitter.cs`](../../src/Heddle.Generator/Emit/TemplateEmitter.cs) — `:650-654` |
| Gauntlet compares overload counts exactly — fails on both `>` and `<`; AQN via `AqnSansVersion` | [`PrecompiledGauntlet.cs`](../../src/Heddle/Precompiled/PrecompiledGauntlet.cs) — `CheckFunctions` `:147-158`, `AqnSansVersion` `:207-212` |
| Runtime export rule: public-static-class-or-throw, `DeclaredOnly`, `IsSpecialName` skip, four eligibility rejections, lowercase name, merge on `AddOrReplace` | [`FunctionRegistry.cs`](../../src/Heddle/Runtime/Expressions/FunctionRegistry.cs) — `Register` `:76-96`, `RegisterContainer` `:127-156`, `AddOrReplace` `:182-200` |
| Generator export transcription: silent skip, first-container-wins, counts include runtime-ineligible methods | [`FunctionExportResolver.cs`](../../src/Heddle.Generator/Binding/FunctionExportResolver.cs) — `AddContainer` `:99-138` (`overloadCounts` `:117`, precedence `:129-137`) |
| Runtime prop-layout walk stops at `typeof(object)`, accumulates faults, unusable-type predicate incl. by-ref | [`PropLayout.cs`](../../src/Heddle/Runtime/Expressions/PropLayout.cs) — `ResolveFromExtension` `:83-179` (`:92` stop rule, `:134` predicate) |
| Generator layer walk unconditional; distinct unusable-type predicate (no by-ref arm, `IsUnboundGenericType`) | [`ExtensionBinder.cs`](../../src/Heddle.Generator/Emit/ExtensionBinder.cs) — `ReadPropParameters` `:284-307` |
| Generator visibility rule (incl. `ProtectedOrInternal`), `AllInterfaces` walk, unqualified `HiddenAttribute` match, third `Nullable<T>` spelling | [`SymbolTypeResolver.cs`](../../src/Heddle.Generator/Binding/SymbolTypeResolver.cs) — `IsAccessible` `:219-234`, `FindProperty` `:190-217`, `IsNonNullableValueType` `:236-244` |
| Runtime keyword table incl. `"dynamic"`; reflection-free spelling parser; ambiguity throw on bare-name ties | [`ReflectionHelper.cs`](../../src/Heddle/Helpers/ReflectionHelper.cs) — `CSharpTypes` `:32-49` (`"dynamic"` `:49`), `ExtractGenericArguments` `:342`, `TryFindMatchingAngleBracket` `:373`, `SplitTopLevelArguments` `:395`, ambiguity `:235-241` |
| Generator implicit `System`/`System.Collections.Generic` fallback and keyword table without `dynamic` | [`SymbolTypeResolver.cs`](../../src/Heddle.Generator/Binding/SymbolTypeResolver.cs) — `Keywords` `:45-55`, `ResolveModelType` `:92-98` |
| Linked `<Compile Include>` precedent (`Shared\…` block); no `Heddle.dll` reference | [`Heddle.Generator.csproj`](../../src/Heddle.Generator/Heddle.Generator.csproj) — `:50-62` |
| Lockstep-test precedent; pinned AQN constant | [`DefaultFunctionLockstepTests.cs`](../../src/Heddle.Tests/DefaultFunctionLockstepTests.cs); [`DefaultFunctionTable.cs`](../../src/Heddle/Precompiled/DefaultFunctionTable.cs) — `ShimTargetTypeName` `:40` |
| Findings, drift classification, coverage note (F4–F7 + render-type not gauntlet-covered), extraction classes | [03 — binding layer](../research/generator-code-sharing/03-binding-layer.md) |
| Tier model, live-drift rows 4/6/7, sequencing (bug-fixes first; `PrimitiveKind` before `PropLayoutCore`/`MemberPathWalk`), shared-layout precedent | [07 — recommendations](../research/generator-code-sharing/07-recommendations.md) |
| Diagnostic-ID claiming rules, registry state (`HED7001`–`HED7017` claimed), amendments ledger | [cross-cutting decisions](../spec/common/cross-cutting-decisions.md) — D1, registry, ledger |
| Breaking-window policy applied in *Back-compat / impact* | [breaking-windows.md](../spec/common/breaking-windows.md) |
| Conformance-corpus/two-driver testing shape, negative-test and regression-gate requirements | [testing standards](../spec/common/testing-standards.md) |

Supplements: [phase-3-binding-layer-typefacts.md](phase-3-binding-layer-typefacts.md) (the
`ITypeFacts<TType>`/`IMemberFacts` abstraction contract and the assignability conformance
corpus), [phase-3-binding-layer-discovery-parity.md](phase-3-binding-layer-discovery-parity.md)
(the rule-by-rule discovery parity tables for extensions and exports).
