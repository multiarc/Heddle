# Phase 3 — binding layer

## Header

- **Status:** **implemented (2026-07-26)** — Tranche A (WI-A1–A4) and Tranche B (WI-B1–B5) landed;
  see [Implementation record](#implementation-record). Both quarantined phase-0 fixtures
  (`NestedExtensionType_BindsAndCrossesTheGauntlet`, `InheritedExtensionNameSubclass_CrossesTheGauntlet`)
  are **un-skipped and green**. All six open questions were resolved (user, 2026-07-25 — see the
  [open-questions register](open-questions.md)) and folded in as committed scope. Two diagnostics are
  claimed to this phase in the
  [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry): `HED7021`
  (ineligible `[ExportFunctions]` container/method, Error, Q3.6) and `HED7023` (ambiguous type name,
  Error, Q3.5) — `HED7022` is left reserved for phase 1 per the central allocation.
- **Goal (one line):** The generator and the runtime answer every binding question — extension
  identity and discovery, function exports, prop layouts, assignability, member paths, model type
  names — from one shared rule-core each, with the three live binding bugs (the false `HED7006`
  build error, the nested/generic AQN mismatch, the hand-mirrored `BranchRole` enum) fixed first
  and independently of any extraction.
- **Depends on:** Phase 4 — expression writers (owns `PrimitiveKind` + the implicit-numeric-widening
  table, the `MemberPathWalk`/`MemberVisibility` shared core, and the overload-rank core this phase's
  cross-container emission needs); Phase 1 — template emitter (owns `Data/RenderTypeRules.cs`,
  consumed not duplicated here, and is the second consumer of this phase's `PropLayoutCore`);
  Phase 5 — pipeline & config (owns the manifest `schemaVersion` constants — a firm edge now
  that the prop-layout gauntlet row of OQ4 is adopted); Phase 6 — diagnostics & utilities (owns the
  `CSharpTypeNames` display tables; this phase owns the parse/binding direction of the same data).
  Source research: [03 — binding layer](../research/generator-code-sharing/03-binding-layer.md)
  (nine findings), synthesized in [07 — recommendations](../research/generator-code-sharing/07-recommendations.md).
- **Changes an externally-visible contract:** yes, behaviorally — no public API or template-syntax
  change, but (a) the false-`HED7006` fix makes builds succeed that today fail, (b) several fixes
  turn permanent silent fallbacks into working precompilation (gauntlet-visible: fallback events
  disappear, rendered bytes are unchanged because the precompiled tier must match the dynamic
  tier), (c) the `PropLayoutCore` unification corrects silent wrong output on drifted prop layouts
  toward the runtime's authoritative layout, and (d) per OQ4's ruling (adopted), the precompiled
  manifest gains one additive row coordinated with Phase 5's `schemaVersion` constants. Analysis against
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
- **No manifest schema redesign.** OQ4's prop-layout row (adopted) is one additive row under
  Phase 5's `schemaVersion` regime; nothing else about the manifest changes in this phase.
- **No new language surface, no grammar change, no new template syntax.** The grammar-stability
  gate of the [testing standards](../spec/common/testing-standards.md) applies unmodified.
- **No runtime behavior changes outside the decided open questions.** The runtime is the
  authority this phase aligns *to*; the two flagged authority questions are resolved (user,
  2026-07-25) without moving it — OQ1 keeps the runtime's narrower sandbox as normative (the
  generator tightens), and OQ5 moves the runtime only if implementation judges its resolution
  logic defective, in which case both tiers are fixed in lockstep and stay matched.
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
build error.** The runtime's rule (verified: `src/Heddle/Runtime/Expressions/FunctionRegistry.cs`,
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
runtime's throw-vs-silent-skip asymmetry becomes a build **error** (per OQ6's ruling, an
instance of the register's match principle — the runtime throws `ArgumentException`, so the
build fails the same way; ID claimed from the `HED70xx` block per
[D1](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) at spec time —
`HED7017` is the last claimed ID, so the claim will be `HED7018`+, recorded in the registry in
the same change). Cross-container precedence follows OQ2's ruling (merge). Per-dimension parity
targets:
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
Precedence (`Replace`, assignability override, tie-break ordering) follows OQ3's ruling (full
runtime-precedence adoption).

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
parameter-index row), OQ4's adopted ruling supplies the missing guard (manifest row + gauntlet
check, coordinated with Phase 5 — that dependency is now firm).

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
adoption (OQ1 — resolved: follow runtime; the joint ratification with Phase 4's Q4.1 is
settled). The `[Hidden]` match moves to a fully-qualified metadata-name comparison on the
symbol side independently of the OQ1 ruling — matching any `HiddenAttribute` from any namespace is
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
the wrong type. Policy per OQ5's ruling (match the runtime exactly — an instance of the
register's match principle, replacing the earlier degrade-only lean): the shared lookup
rule-core reproduces the runtime's resolution semantics and *outcomes* — the implicit-namespace
fallback is dropped (the runtime has none), and a name the runtime's rule resolves as ambiguous
surfaces as a matching build-time **error** (claimed ID), not a silent degrade. The build-time
reference set sees fewer assemblies than the runtime's loaded-assembly scan; the owning spec
pins how the identical ordering/ambiguity semantics apply to each tier's assembly universe so
outcomes agree *(verify at implementation)*. If implementation judges the runtime's own
resolution logic defective, both tiers are fixed in lockstep and stay matched — never a
one-sided fix.

**Diagnostics discipline.** Any new or changed diagnostic follows
[D1](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx): IDs claimed
from the `HED70xx` generator block in the registry in the same change as the owning spec, with
message text, trigger, and position semantics in that spec's *Diagnostics* section. This phase
anticipates two claims (the ineligible-export-container error per OQ6's ruling; the
ambiguous-type-name error per OQ5's ruling — both Error severity, mirroring runtime throws
under the register's match principle) and **removes no shipped ID** — `HED7006` keeps its
ID and message with a corrected (narrower) trigger condition, which is a trigger fix, not a
re-numbering.

## Dependencies & ordering

- **Phase 0 posture (landed):** every test in this phase runs under the gauntlet-crossing guardrails — see the
  [precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture) rule. This phase's
  fix-first group un-skips phase 0's quarantined `NestedExtensionType_BindsAndCrossesTheGauntlet` (F1) and
  `InheritedExtensionNameSubclass_CrossesTheGauntlet` (F3) fixtures as acceptance evidence.

- **Depends on Phase 4 for:** the shared `PrimitiveKind` enum and implicit-numeric-widening
  table (consumed by prop-default conversion and the keyword table — this phase must not ship
  its own numeric table); the `MemberPathWalk`/`MemberVisibility` core (consumed by the F7
  adoption work item); the overload-rank core (consumed by cross-container export *emission*
  under OQ2's ruling — manifest bookkeeping does not wait for it, emission of
  multi-container names does).
- **Depends on Phase 1 for:** `Data/RenderTypeRules.cs` (the render-type derivation rule this
  phase's inherited-attribute predicates feed). Conversely, **Phase 1 depends on this phase
  for** `PropLayoutCore<TType>` (its emitter consumes the core for definition layouts) and
  `AqnFormatter` (its manifest builders are two of the five deleted copies).
- **Depends on Phase 5 (firm):** the OQ4 prop-layout manifest row (adopted — user, 2026-07-25)
  is an additive schema change under Phase 5's `schemaVersion` constants; the formerly
  conditional edge is now committed.
- **Coordinates with Phase 6 on:** the keyword/alias data (this phase: parse direction; Phase 6:
  display direction — one linked data source).
- **Internal ordering (Tranche A has no prerequisites and starts immediately):**
  1. Tranche A fixes — inherited-name walk, `BranchRole` link, `AqnFormatter` + call-site
     routing (the formatter is authored here because the fix needs it; it is also the first
     Tranche B artifact).
  2. `ITypeFacts`/`IMemberFacts` + reflection/Roslyn adapters + assignability conformance
     corpus (the enabler for everything below in this phase).
  3. Export-discovery rule-core + merge bookkeeping + the OQ6 error diagnostic (highest gauntlet
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
  row implementation (OQ4 adopted); the program-wide deletion of the generator's private
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
  breaking-window item; the differential fixture set plus OQ4's adopted gauntlet row prove
  the direction of every change. **Note explicitly: prop-layout drift is not gauntlet-covered
  today** — OQ4's ruling closes exactly that gap.
- **Manifest schema (OQ4 — adopted).** The row is additive: new-generator manifests carry the
  row, the gauntlet checks it when present, and manifests without it (older generators) pass the
  check vacuously — schema evolution stays within Phase 5's `Min`/`Max` compatibility
  predicate. No re-precompilation is forced.
- **Runtime behavior: unchanged by default.** Every alignment in this phase moves the
  *generator* toward the runtime. The two places the runtime itself was questioned are resolved
  (user, 2026-07-25) leaving it as-is: OQ1 keeps the narrower sandbox normative, and OQ5 moves
  the runtime only if implementation judges its resolution logic defective — then both tiers
  are fixed in lockstep and stay matched. Any *widening* of the sandbox remains a candidate in
  the [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register)
  only if a spec later ratifies it.
- **Public API surface:** no changes. Shared files are `internal`; adapters are `internal`;
  the `TemplateOptions` completeness invariant is untouched.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| A third Roslyn-vs-CLR assignability disagreement beyond the two known nullable corrections silently re-diverges the tiers | The assignability conformance corpus is a *data file* executed by both a reflection-side and a symbol-side test; adding a suspected row is a one-line change; the corpus seeds include variance-with-value-type-arguments and `ValueTuple` rows precisely because the research names them as untested | M |
| `AqnFormatter` mis-handles an exotic identity (generic type *definition* vs constructed, `+` in a namespace-less global type, multi-level nesting with mixed arity) and converts a working fallback into a wrong manifest string | The formatter's conformance test enumerates real types from the test assemblies (nested, generic-container, global-namespace) and asserts byte-equality against live `Type.FullName + ", " + assembly`; the five call sites are routed one at a time with the gauntlet suite green between each | M |
| Cross-container merge (OQ2) without Phase 4's overload-rank core leads the generator to emit a call the runtime would resolve to a different overload | Split adopted in OQ2's ruling: merge bookkeeping (manifest rows) lands now — fixing the gauntlet count mismatch — while *emission* for names exported by more than one container degrades to dynamic with a recorded reason until the rank core exists; degrade is fallback-safe by construction | M |
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
      build time at **Error** severity with a claimed registry ID — the build fails as the
      runtime's `RegisterFrom` registration (`ArgumentException`) would; no silent skip, no
      warning-only pass.
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
      shared core; the three F7 divergences are closed per the OQ1 ruling (runtime
      authoritative — the generator tightens), and the
      `[Hidden]` check matches the fully-qualified attribute name on both tiers.
- [ ] The generator resolves `List<int>`, `int[]`, `(int, string)`, and dotted-nested
      generic spellings to the same types as `ReflectionHelper.ResolveType` for a shared
      name-corpus (lockstep test); `"dynamic"` resolves on both tiers; the implicit
      `System`/`System.Collections.Generic` fallback is removed (a lockstep row pins that a
      name resolvable only through it binds on neither tier); and for every corpus name the
      runtime resolves as *ambiguous*, the generator raises the matching build-time ambiguity
      error (claimed ID) — the same outcome as the runtime's throw, never a pick and never a
      silent degrade (OQ5 ruling).
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
| `[ExportFunctions(typeof(InternalHelpers))]` where the container is `internal` | OQ6 **error** diagnostic at build time naming the container — the build fails as the runtime registration would; runtime behavior unchanged (still throws in `RegisterFrom`) |
| Extension inherits `[Prop("x", typeof(int))]` and re-declares `x` as `long` | Same ordered fault (`PropRedeclarationMismatch` class) from both tiers via the shared fault enum; identical slot layout on the compliant variant |
| Extension base chain of depth 4 where a base beyond the third layer declares props | Identical slot indices from both tiers (generator no longer walks past `object`; runtime rule authoritative) |
| Prop declaration list with two faults in one layer | Both faults reported, in declaration order, by both tiers (accumulate-and-continue; the generator no longer stops at the first) |
| Model member path through a `protected internal` getter | Per the OQ1 ruling (follow runtime): generator refuses typed emission (degrades), matching the dynamic tier's rejection; differential fixture asserts both tiers agree |
| `:: List` under `@using System.Collections` plus a host assembly also defining `List` | Build fails with the matching ambiguity error (OQ5 ruling — same outcome as the runtime's ambiguity throw; claimed ID); no typed code emitted off a possibly-wrong type; runtime behavior (ambiguity error) unchanged |
| `:: Dictionary<string, List<int>>` as a model type | Generator resolves it via the shared spelling parser to the same constructed type the runtime resolves; template precompiles where it previously fell back |
| Corrupting one row of the assignability corpus data file | Both the reflection-side and the symbol-side test fail on that row (proves both drivers actually consume the file) |

## Open questions (all resolved)

All six questions are **resolved (user, 2026-07-25)** — rulings recorded in the
[open-questions register](open-questions.md) (entries Q3.1–Q3.6) and folded into the sections
above; none remain open, and the joint-ratification precondition with Phase 4 (OQ1/Q4.1) is
satisfied. Per the [spec conventions](../spec/common/spec-conventions.md#no-open-questions),
the owning spec turns each ruling into a decision record. OQ5 and OQ6 are instances of the
register's program-wide **match principle** (the generator matches the runtime's validation
rules, errors, and throws; warning-channel differences may legitimately exist), cited rather
than restated where they apply.

1. **OQ1 — Should `protected internal` getters be visible to member paths?** **Resolved
   (user, 2026-07-25): follow runtime — not visible.** The runtime's narrower sandbox
   (`getter.IsAssembly || getter.IsPublic`, `MemberPathResolver.cs:123`) is normative; the
   generator tightens, dropping its `Accessibility.ProtectedOrInternal` acceptance
   (`SymbolTypeResolver.cs:224-226`). The joint ruling with Phase 4's Q4.1 is settled the same
   way, so the shared `MemberAccess` decision table (Phase 4) carries an explicit
   `ProtectedOrInternal → hidden` row and the call is documented, not incidental.
   Widening *both* tiers together remains recorded as a candidate in the
   [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register)
   only if a spec ratifies the demand.
2. **OQ2 — Export precedence: first-container-wins or merge?** **Resolved (user, 2026-07-25):
   follow runtime — merge.** The runtime's registry semantics (`AddOrReplace`, replace only on
   identical signature) are the public contract; the generator's first-container-wins
   (`FunctionExportResolver.cs:129-137`) is replaced. Merge lands in the shared bookkeeping so
   manifest rows match the live registry immediately; for *emission*, a name exported by more
   than one container degrades to dynamic until Phase 4's overload-rank core lets the
   generator prove which overload the runtime would pick. The degrade is fallback-safe; the
   manifest fix alone kills the standing gauntlet mismatch.
3. **OQ3 — `[ExtensionReplace]`: adopt or refuse-to-precompile?** **Resolved (user,
   2026-07-25): adopt the runtime approach** — the full runtime precedence (`Replace`-last
   ordering, the `IsAssignableFrom` override rule, the `[DataType]`/`[ChainedType]`
   interface-ness tie-break) lands in the shared `ExtensionCandidate` rule-core; the generator
   binds the extension exactly as the runtime's replacement precedence resolves it — there is
   no reason a precompiled template cannot honor a runtime interface replacement. The
   assignability edge comes free from `ITypeFacts` (F6). If spec verification finds the
   tie-break unreproducible symbolically in some corner, that corner (only) degrades to
   dynamic with a recorded reason.
4. **OQ4 — Add a prop-layout manifest row + gauntlet check?** **Resolved (user, 2026-07-25):
   yes — adopted.** Prop layouts were the one wire-format contract with **no** gauntlet
   coverage — drift is silent wrong output (`PrecompiledGauntlet.cs:21-56` checks options,
   extension identity, functions, staleness only). One additive manifest row per bound
   parameter-declaring extension — a fingerprint of the slot layout (ordered names +
   `AqnFormatter`-formatted slot types) — compared by the gauntlet like the
   extension-identity rows; mismatch → fallback, not wrong output. The additive schema change
   is owned by Phase 5's `schemaVersion` constants and compatibility predicate — the formerly
   conditional Phase 5 dependency is now firm (see *Dependencies & ordering*) — and the check
   is vacuous for manifests predating the row.
5. **OQ5 — Model type-name ambiguity and implicit namespaces: what may the generator bind?**
   **Resolved (user, 2026-07-25): match the runtime exactly** — an instance of the register's
   match principle, and a **replacement** of the plan's earlier degrade-only lean. The
   generator reproduces the runtime's resolution semantics and *outcomes* via the shared
   lookup rule-core: a name the runtime's rule resolves as ambiguous
   (`ReflectionHelper.cs:235-241`) surfaces as a matching build-time **error** (claimed ID),
   not a silent degrade, and the generator's unilateral implicit
   `System`/`System.Collections.Generic` fallback (`SymbolTypeResolver.cs:92-98`) is dropped
   so both sides agree exactly — the runtime has no implicit namespaces. If implementation
   judges the runtime's own resolution logic defective, both tiers are fixed in lockstep and
   stay matched — never a one-sided fix. The shared spelling parser still supplies
   generics/arrays/tuples; the generator still never emits typed code off a type identity the
   runtime might not choose, and runtime ambiguity behavior (throw) is unchanged.
6. **OQ6 — Ineligible export container: what does the build say?** **Resolved (user,
   2026-07-25): the generator follows the runtime's exact validation rules and errors — it
   throws, so the build errors.** The runtime raises a hard `ArgumentException` at
   `RegisterFrom` time; the generator raises a matching **Error-severity** diagnostic
   (upgraded from the plan's earlier warning lean, per the register's match principle) — the
   build fails as the runtime registration would, instead of masking a host configuration
   error until first run. The ID-claim discipline is unchanged: claimed from `HED70xx` — next
   free `HED7018` — in the owning spec's registry update, positioned on the template(s) whose
   compilation consulted the resolver, naming the container and the runtime consequence, with
   the message pointing at the remedy per the
   [coding standards](../spec/common/coding-standards.md#error-handling-and-diagnostics).

## Addendum — `ReflectionHelper` rework already on the branch (orchestration, 2026-07-25)

This plan was authored against a `ReflectionHelper` that has since been substantially reworked on
`feature/benchmarks` (commit `97edb26`, +119/−28, with `NestedTypeResolutionTests` /
`GenericTypeResolutionTests` — 388 lines, 27 tests — landing beside it). The rework moves the
runtime side of F8/OQ5, so **the normative baseline this phase must match is the reworked file, not
the behavior the plan and [03](../research/generator-code-sharing/03-binding-layer.md) describe.**
Three concrete corrections:

1. **The runtime now accepts dotted nested spellings, by two independent mechanisms.**
   `RegisterType` additionally registers a dotted alias for every nested type
   (`shortName.Replace('+', '.')` into both `_shortNames` and `_fullNames`,
   [ReflectionHelper.cs:93-95](../../src/Heddle/Helpers/ReflectionHelper.cs)), and the
   `Type.GetType` fallback walks a `.`→`+` retry ladder right-to-left
   (`A.B.C.D` → `A.B.C+D` → `A.B+C+D` → …), stopping at the first hit. The plan's framing of F1 as
   "generator emits `Ns.Outer.Inner`, runtime requires `Ns.Outer+Inner`, therefore mismatch" is
   therefore **no longer the whole story for type *resolution*** — though it still stands for the
   gauntlet's `ExtensionTypeName` check, which is a string comparison rather than a resolution.
   Phase 3 must establish which of the two paths each drift actually rides before writing the fix.

2. **The bare-short-name tie no longer throws "ambiguous" — it silently picks.** The plan states
   (Design direction, F8; OQ5's ruling; the grounding table) that the runtime "throws *ambiguous*
   on bare-name ties at `ReflectionHelper.cs:235-241`". On the current branch that line range is a
   generic `Couldn't resolve` throw, and the short-name arm
   ([ReflectionHelper.cs:232-245](../../src/Heddle/Helpers/ReflectionHelper.cs)) resolves a tie with
   `types.FirstOrDefault(t => imports.Contains(t.Namespace))` — a **first-match pick over an
   assembly-scan-ordered list**, with no tie-break when two imports both match, throwing only when
   *no* import matches. The ambiguity throw (spelled `ambigous` in the message text) survives only
   on the dotted/full-name arms (`:205-210`, `:221-222`).

   This directly engages OQ5's escape clause — *"If the runtime's logic itself is found defective,
   both tiers are fixed in lockstep and stay matched — never a one-sided fix."* An order-dependent
   silent pick is not a rule the generator can match by construction, and reproducing it would bake
   assembly-load order into build output. **Phase 3 must treat this as the defect case**: fix the
   short-name arm to throw ambiguous consistently with the dotted arms, then match the generator to
   the fixed rule — not reproduce the pick. This is a behavior change on the runtime and needs the
   [breaking-windows](../spec/common/breaking-windows.md) judgement made explicitly.

3. **The alias registration *widens* the tie surface that (2) mishandles.** Registering
   `Outer+Nested` additionally as `Outer.Nested` means a nested type can now collide with a real
   namespaced type of the same dotted spelling. The code comment at
   [ReflectionHelper.cs:82](../../src/Heddle/Helpers/ReflectionHelper.cs) asserts such a collision
   "surfaces as the existing *ambiguous* error rather than a silent pick" — **true on the full-name
   arm, false on the short-name arm** per (2). The comment is currently inaccurate for one of the
   two arms it describes; fixing (2) is what makes it true, after which the comment should be
   re-read rather than trusted.

Line numbers cited elsewhere in this plan were spot-checked against the reworked file and mostly
survived (`CSharpTypes` `:32`, `"dynamic"` `:49`, `ExtractGenericArguments` `:342`,
`TryFindMatchingAngleBracket` `:373`, `SplitTopLevelArguments` `:395` all still accurate); the
ambiguity anchor `:235-241` is the one that did not. Re-confirm all of them at spec time regardless.

Related: phase 0's implementation found that `ExtensionBinder.CollectTypes` enumerates
`INamespaceSymbol.GetTypeMembers()` only and **never descends into nested types**, so a nested
extension degrades at build time before the two identity spellings can be compared at all. F1's fix
must therefore cover nested-container *discovery* as well as `+`-separated *formatting*.

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

## Implementation record

Landed 2026-07-26. `dotnet build Heddle.sln -c Debug` green; `dotnet test Heddle.sln -c Debug` green —
**4246 passed, 0 failed, 2 skipped** (phase 1's `NonLeftmostScopeChannelParticipant…` fixture × 2 TFMs,
the only quarantine entry left). Baseline at start of phase: 3814 / 0 / 6.

### Tranche A — the fix-first bug group

| WI | What landed | Files |
|---|---|---|
| **A1 — AQN formatting (F1)** | Shared [`Precompiled/AqnFormatter.cs`](../../src/Heddle/Precompiled/AqnFormatter.cs) owning the join rules (`.` namespace, `+` nesting, `", "` assembly) over per-segment metadata names, with a reflection adapter ([`ReflectionTypeIdentity`](../../src/Heddle/Precompiled/ReflectionTypeIdentity.cs)) and a Roslyn adapter ([`SymbolTypeIdentity`](../../src/Heddle.Generator/Binding/SymbolTypeIdentity.cs)). All generator copies routed through it. | `AqnFormatter.cs`, `ReflectionTypeIdentity.cs`, `SymbolTypeIdentity.cs`, `PrecompiledGauntlet.cs`, `ExtensionBinder.cs`, `FunctionExportResolver.cs`, `TemplateEmitter.cs` |
| **A2 — nested-container discovery (F1, second half)** | `ExtensionBinder.CollectTypes` now recurses through `INamedTypeSymbol.GetTypeMembers()`. It previously enumerated namespace members only, so a nested extension degraded at build time *before* the two identity spellings could be compared. | `ExtensionBinder.cs` |
| **A3 — inherited `[ExtensionName]` + precedence (F3)** | Base-chain name read (the walk the file already used three times); the runtime discovery predicate (`IExtension` + inherited name) separated from the generator's *bindability* test; runtime precedence in shared [`ExtensionRegistrationRules`](../../src/Heddle/Language/Binding/ExtensionRegistrationRules.cs); `HED7006`'s trigger narrowed to "resolves to nothing under the runtime's own rule". | `ExtensionBinder.cs`, `ExtensionRegistrationRules.cs`, `TemplateEmitter.cs` |
| **A4 — `BranchRole` mirror (F9)** | Enum split into [`Attributes/BranchRole.cs`](../../src/Heddle/Attributes/BranchRole.cs) and linked into the generator; the hand-mirrored copy deleted. Consumers and two probe-compilation test harnesses updated for the linked-source CS0433 hazard. | `BranchRole.cs`, `Heddle.Generator.csproj`, `ExtensionBinder.cs`, `DocumentShaper.cs`, `TemplateEmitter.cs`, tests |

### Tranche B — shared rule-cores

| WI | What landed | Files |
|---|---|---|
| **B1 — `ITypeFacts` + assignability corpus (F6)** | [`Language/Binding/ITypeFacts.cs`](../../src/Heddle/Language/Binding/ITypeFacts.cs); reflection adapter [`ReflectionTypeFacts`](../../src/Heddle/Runtime/Expressions/ReflectionTypeFacts.cs); Roslyn adapter [`SymbolTypeFacts`](../../src/Heddle.Generator/Binding/SymbolTypeFacts.cs) holding the two nullable corrections **once**; shared [`AssignabilityCorpus`](../../src/Heddle/Language/Binding/AssignabilityCorpus.cs) (30 rows) run by two drivers. | + `AssignabilityCorpusReflectionTests`, `AssignabilityCorpusSymbolTests` |
| **B2 — export discovery (F2 / Q3.2 / Q3.6)** | Shared [`ExportRules`](../../src/Heddle/Language/Binding/ExportRules.cs) + [`ExportBookkeeping`](../../src/Heddle/Language/Binding/ExportBookkeeping.cs); `FunctionRegistry` and `FunctionExportResolver` both route through them; merge semantics replace first-container-wins; **export signature discovery** lands, and [`ExportFunctionBinder`](../../src/Heddle.Generator/Binding/ExportFunctionBinder.cs) makes exports participate in the shared overload rank (phase 4's WI8 remainder); `HED7021` at Error. | + `ExportDiscoveryTests`, `ExportMergeLockstepTests` |
| **B3 — `PropLayoutCore` (F4 / Q3.4)** | [`Language/Binding/PropLayoutCore.cs`](../../src/Heddle/Language/Binding/PropLayoutCore.cs) with the ordered `PropFault` vocabulary and `PropFaults.FaultOrder`/`Message` (phase 6's WI5 handoff); both tiers adopt it; the prop-layout manifest row + gauntlet check with a `PrecompiledSchema` bump 3 → 4. | + `PropLayoutCoreReflectionTests`, `PropLayoutCoreSymbolTests`, `PropLayoutFingerprintTests` |
| **B4 — model type names (F8 / Q3.5)** | Shared [`TypeSpelling`](../../src/Heddle/Language/Binding/TypeSpelling.cs) parser; [`SymbolTypeIndex`](../../src/Heddle.Generator/Binding/SymbolTypeIndex.cs) reproducing the runtime's lookup rule; implicit namespaces dropped; `dynamic` added to the symbol alias map; the **runtime's short-name tie fixed in lockstep** and `HED7023` raised for the same input. | + `TypeSpellingLockstepTests`, `TypeSpellingSymbolLockstepTests`, `AliasTableLockstepTests` (third arm) |
| **B5 — member paths (F7 / Q3.1)** | `SymbolTypeResolver` re-expressed as the Roslyn adapter of phase 4's `MemberPathWalk`/`MemberVisibility`; the three divergences closed (`ProtectedOrInternal`, inherited-internal, `AllInterfaces`), `[Hidden]` matched by full metadata name. | + `MemberVisibilitySymbolConformanceTests` |

### Corrections to this plan, found against the source

1. **The plan's headline F2 scenario is unreachable as described.** "One `void Log(string)` helper in
   an export container permanently un-precompiles every template calling any function from it" cannot
   happen through `[ExportFunctions]`: `FunctionRegistry.RegisterContainer` wraps the
   `ArgumentException` from `Register` and rethrows, so the **whole container fails to register** and
   the host throws at startup — the manifest count never gets compared. Under the match principle the
   correct build-tier response is therefore `HED7021` for the ineligible *method* too, not a silently
   adjusted count. Implemented that way.
2. **There were four generator AQN copies, not five.** `NativeExpressionWriter.cs:144` no longer
   builds an AQN — it reads `ExportEntry.ContainerAqnSansVersion`.
3. **F8's implicit-namespace framing was half right.** Dropping the implicit
   `System`/`System.Collections.Generic` fallback does not make `List<int>` unresolvable: the runtime
   resolves a **globally unique short name** with no import at all, and the build tier now does the
   same. The real divergence was that the generator had no uniqueness rule and a hard-coded namespace
   list instead.
4. **`ITypeFacts.GetPrimitiveKind` is `GetNumericKind` over phase 4's `NumericKind`** — phase 4 landed
   `NumericKind`, not `PrimitiveKind`.
5. **No third Roslyn-vs-CLR assignability disagreement exists** in the corpus's variance and
   `ValueTuple` probes; the Roslyn adapter with its two nullable corrections matches the CLR on all
   30 rows.
