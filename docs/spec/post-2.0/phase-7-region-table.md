# Phase 7 — the named-region table and its four-layer threading

Supplementary to [phase-7-named-content-regions.md](phase-7-named-content-regions.md) (the entry
document — read it first). This document fixes the **shape of the named-region table**, where the
anonymous `@out()` slot lives in it (the byte-identity anchor), and exactly how the table is carried
through each of the four layers **as they actually work** (the layers are not uniform — see the
entry document's *Assumed state*).

Source citations give `path` + member; load-bearing line numbers are marked *(verify at
implementation)*.

## The table shape (mirrors `PropLayout`)

The precedent is [`PropLayout`](../../../src/Heddle/Runtime/Expressions/PropLayout.cs) — a flattened,
index-stable table resolved once per definition and cached. The region table mirrors it:

```csharp
// Heddle.Runtime.Expressions (new file RegionLayout.cs), internal — the dynamic-tier table.
internal sealed class RegionSlot
{
    internal string Name;          // ordinal, case-sensitive (StringComparer.Ordinal)
    internal bool   IsPublic;      // <:name> => true; a component's private inner <name> => false
    internal ExType ModelType;     // resolved <:name :: Type>; null/object when abstract
    internal string ModelTypeName; // the unresolved name, for diagnostics
    internal BlockPosition Position;   // the declaration site (for HED5020)
    internal DefinitionItem Definition;// the region's own definition (its default body)
}

internal sealed class RegionLayout
{
    internal IReadOnlyList<RegionSlot> Slots { get; }   // declaration order
    internal bool TryGet(string name, out RegionSlot slot);  // compile-time only; render never sees a name
    internal int Count { get; }
    internal static RegionLayout Resolve(DefinitionItem component, CompileScope scope); // caches like PropLayout
}
```

`RegionLayout.Resolve` reads the component's parse-time `Regions` list, resolves each region's
`:: Type` through `ReflectionHelper.ResolveType` (the same resolver `PropLayout` uses), and builds the
ordinal lookup. It **does not** detect duplicates — a duplicate `<:name>` is rejected earlier at
parse, and the name-keyed `DefinitionsBlock` never stores the second declaration, so **HED5020 is
raised at [`HeddleMainListener.EnterDef`](../../../src/Heddle/Language/HeddleMainListener.cs)
≈54–58** (the id-less duplicate error upgraded to HED5020 for a public region), **not** here (review
F4a — a `Resolve`-side check would be dead code, since the duplicate is never stored to walk).
**Append ordering (pinned):** the enclosing component's `Regions` list is appended **on the
store-success path only** — the `RegionDeclaration` is added to the parent's `Regions` at the same point
`EnterDef` stores the region into `DefinitionsBlock` (after the ≈54–58 duplicate check **passes**, at
≈60), **not** eagerly inside `CreateRegionDefinition` (which runs at ≈45, before the check). So a
duplicate `<:name>` rejected at ≈54–58 (`return` before ≈60) is **never** appended to `Regions`.
`Regions` therefore never contains two public entries of one name, resolving the entry-doc/region-table
data-source question in favour of the parse-time list. `RegionLayout` is cached in
`CompileContext.ResolvedRegionLayouts` keyed by the same `Name + "@" + Position` identity
`ResolveLayoutCached` uses for props ([`HeddleCompiler.ResolveLayoutCached`](../../../src/Heddle/Runtime/HeddleCompiler.cs)
≈1238–1249, verify), so two call sites of one component share the instance.

The raw per-definition metadata the table resolves from is stored on the parse model:

```csharp
// Heddle.Language.DefinitionItem — new, additive, defaulting to the empty/false state.
public bool IsRegion { get; internal set; }            // declared inside a component body
public bool IsPublicRegion { get; internal set; }      // the <:name> form
public IReadOnlyList<RegionDeclaration> Regions { get; internal set; } = Array.Empty<RegionDeclaration>();
```

`RegionDeclaration` mirrors [`PropDeclaration`](../../../src/Heddle/Language/PropDeclaration.cs)
(`Name`, `TypeName`, `IsPublic`, `Position`), set by the parser
([phase-7-grammar.md](phase-7-grammar.md)).

## Where the anonymous `@out()` slot lives — the byte-identity anchor

The anonymous slot is modelled as the table's **implicit default entry** and keeps its **existing
machinery unchanged**. Concretely:

- A definition's single `@out()` / `out:: Type` slot is still carried by `SlotTypeName` +
  [`OutExtension`](../../../src/Heddle/Extensions/OutExtension.cs) +
  [`SlotContent`](../../../src/Heddle/Core/SlotContent.cs) exactly as today. The named-region table
  (`Regions`) holds **only** the named regions.
- When a definition declares **no** named regions, `Regions` is empty, `RegionLayout.Count == 0`, and
  **every compile path is bit-identical to today** — no region code runs. This is the additive/byte-
  identity property: the default entry (the anonymous slot) is the whole table for a single-slot
  template, and its code path is the pre-existing one.

The table therefore never displaces `@out()`; it sits beside it. `@out()`, `@out(expr)`, the single
`out:: Type` header slot, and their diagnostics `HED5012`–`HED5018`/`HED5016`/`HED5017` are untouched
(entry document, D9).

## Layer 1 — dynamic runtime (`HeddleCompiler`)

The dynamic tier owns region declaration, region-context body compilation, and the call-scoped fill.
All of it lives in [`HeddleCompiler.CreateExtension`](../../../src/Heddle/Runtime/HeddleCompiler.cs)
(the definition branch, ≈1144–1196, verify) — the same method that installs `PropLayout`/`SlotMode`.
The full algorithm (region-context scope, call-body fill-candidate routing from
`extensionItem.Context.RegionFillCandidates` matched by `origin == extensionItem.Context`, the
`RegionFillScope` propagation, the `DefinitionMaterializer` layering, emit-then-retract of the
base-not-found error) is specified in the entry document (D4/D5/D6). The table's role here:

- **The fill reaches any depth via `RegionFillScope` on `CompileContext`, not a top-level install.**
  A matched fill is a materialized `DefinitionItem` placed in a small per-call `RegionFillScope` map
  set in the ≈1185–1195 save/set/restore block and **copied into every nested body-compile by the
  `CompileContext` child copy ctor** (≈105–122) — the exact seam that carries `ActivePropLayout` to
  depth (why `@(theme)` resolves inside a `@list` body). `CompileItem` (≈744–746) consults the scope
  before the parse-context lookup, so `@item(this)` nested in the `Articles` loop resolves the fill.
  A top-level install into the isolated callee context was proven **not** to reach nested region calls
  (entry doc D4 / review A).
- `RegionLayout.Resolve(component)` yields the public regions the fill matches against; the fill step
  raises **HED5019** (private-region override) and retracts the candidate's parse-emitted base-not-found
  error. **The retract removes the captured error object from BOTH downstream lists** — the
  runtime-authoritative `CompileContext.CompileErrors` (which the `HeddleCompileResult` reads;
  `compileScope.CompileErrors` delegates to it) **and** the parse-side `ParseContext.Errors` (over which
  the LSP `DocumentAnalyzer.ProjectDiagnostics` union reference-dedups). A `ParseContext.Errors`-only
  removal would be **inert**: `DocumentParser.CopyErrorsTo` ([`DocumentParser.Runtime.cs`](../../../src/Heddle/Language/DocumentParser.Runtime.cs)
  ≈38–43) copies the error object **by reference** into `CompileErrors` at parse — before compile — so the
  same instance must be removed from both to clear it from the result **and** the editor (entry doc D5 /
  Assumed-state phase-split seam). **HED5020** is raised at `EnterDef` (parse); narrowing reuses the
  pre-existing `WalkValidateDefinitionType` — no HED5021 (entry doc D7/D10, F4).
- Rule (D6): when `CreateExtension` compiles a definition whose `IsRegion` is true, it keeps the
  **enclosing** `ActivePropLayout` (`= savedLayout`) while `SlotParameterType` stays the region's own
  (`null`), so a region body resolves the component's props but an `@out(value)` in it is the ordinary
  "no slot" error — never the component's slot.

Props already proved the diagnostic-raise shape: `HeddleCompiler` raises **HED5001**–**HED5004** in
[`BindProps`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈1290–1383, verify) — **not** in
`PropLayout`. The region diagnostics: **HED5019** at the fill step (`CreateExtension`), **HED5020** at
`EnterDef` (parse), narrowing at `WalkValidateDefinitionType` — no HED5021 (F4).

## Layer 2 — LSP (`Heddle.LanguageServices`)

Linkage is **transitive**: [`DocumentAnalyzer`](../../../src/Heddle.LanguageServices/DocumentAnalyzer.cs)
calls `HeddleCompiler.Compile` (≈45, verify), so region declaration/fill diagnostics
(HED5019/HED5020, and the reused HED0001/HED1xxx type errors in typed override bodies) surface in the
editor **for free** — no LSP compile change (region diagnostics are HED5019/HED5020, plus the reused
HED0001/HED1xxx type errors in typed override bodies). One consequence to note for the D5 emit-then-retract:
`ProjectDiagnostics` (≈106–148) unions **both** `CompileContext.CompileErrors` **and**
`ParseContext.Errors` with a **reference**-keyed `HashSet<HeddleCompileError>` (the type has no
`Equals`/`GetHashCode` override), so a matched fill's base-not-found error is cleared from the editor
**only because** the D5 retract removes the *same object reference* from **both** lists — a
`ParseContext.Errors`-only removal would leave it in `CompileErrors` and it would still show. The additive LSP work is projection so
completion/hover can see regions:
[`DocumentAnalyzer.ProjectDefinitions`](../../../src/Heddle.LanguageServices/DocumentAnalyzer.cs)
(≈150–166, verify) already enumerates `DefinitionsBlock.Definitions` into
[`DefinitionInfo`](../../../src/Heddle.LanguageServices/DefinitionInfo.cs); it gains a
`IReadOnlyList<RegionInfo> Regions` (name, `IsPublic`, type) built from `DefinitionItem.Regions`, and
the completion provider offers a callee's **public** region names at a call-body `<│` override
position. LSP does **not** reimplement the table — it reads the parse model.

## Layer 3 — source generator (`Heddle.Generator`)

The generator reuses the ANTLR parser (it consumes `DefinitionItem`/`OutputItem`) and reimplements
**type resolution** over Roslyn symbols (`PropLayoutInfo`,
[`TemplateEmitter`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) ≈923–977, verify). Per
maintainer ruling **[OQ1](phase-7-named-content-regions.md#d8--native-precompilation-of-region-defaults-and-fills-via-context-aware-resolution--materialization-oq1)** (entry document D8) it precompiles region
**defaults** and region **fills** **natively**. The plain sibling-override idiom is **not** in scope —
it keeps the generator's pre-existing silent un-precompile (entry document D11). Two new generator
capabilities (entry document WI4 — new, risk-bearing work):

- **Context-aware definition resolution (fixes review F1 — the blocker).** Today `BuildCall` resolves
  a call **only against the document root** `_parse` —
  `if (name.Length != 0 && _parse.DefenitionExists(name)) return BuildDefinitionCall(_parse.GetDefenition(name), …)`
  (≈493–494); `_parse` is the ctor-injected document root (≈91) and `DefinitionBlock` is a flat
  snapshot with no downward chaining ([`DefinitionBlock.cs`](../../../src/Heddle/Language/DefinitionBlock.cs)
  ≈10–21). So an **inner** definition of a component body (`@heading()`) currently **misses** at ≈493
  → `reason = "named extension 'heading'"` (≈610) → the template is silently un-precompiled. **So
  "region defaults already precompile natively" was FALSE** — only document-scope siblings resolve via
  `_parse`. WI4 threads the enclosing body's `ParseContext` **and a `RegionFillScope`** through
  `BuildCall`/`PopulateBody`/`BuildBody` (the recursion at ≈516/545/567 already carries `bctx`; the fill
  scope rides alongside it), so inner-definition calls (region defaults) and materialized fills resolve
  against the enclosing component's definition scope **and the ambient fill scope at any depth** — the
  generator's parallel to the dynamic tier's `RegionFillScope`-on-`CompileContext` inheritance. This is
  the **BLOCKER-A fix on the generator side**: `@item(this)` nested in the `Articles` loop resolves the
  fill because the fill scope threads into the nested body build (not only the top call). A **self-call**
  resolves natively too: while building region R's fill body the scope rebinds `R → R`'s base default —
  **no self-call escape hatch, no self-call scan**.
- **Region-context body scope (D6 symbol twin).** When it builds a region/override body
  (`GetOrBuildDefinitionBody`/`DefinitionBodyContext`, ≈832–887, verify), it uses the **enclosing
  component's** `PropLayoutInfo` for the body's `BodyContext` rather than the region's own, so props
  resolve byte-identically to the dynamic tier. A region declares no props; it borrows the component's.
- **Materialization + bail lift (region fill only).** The `DefinitionInvolvesOverride`→`return null`
  bail at `BuildDefinitionCall` (≈688; `DefinitionInvolvesOverride` ≈1140–1149) is lifted **only when the
  call resolves to a materialized region fill** carried in the ambient `RegionFillScope`: it calls the
  **shared** parse-model-pure `DefinitionMaterializer` (in `src/Heddle/Language/`, compile-linked into
  the generator via `<Compile Include="..\Heddle\Language\**\*.cs">`,
  [`Heddle.Generator.csproj`](../../../src/Heddle.Generator/Heddle.Generator.csproj) ≈49–52) over
  `item.Context.RegionFillCandidates` + the callee's regions to produce the final-form definition, then
  emits its body via `GetOrBuildDefinitionBody` **with the fill scope active** so a self-call resolves to
  the region base default (D4 step 5) — no recursive self-call is emitted. A definition reaching
  `BuildDefinitionCall` via the flat `_parse.GetDefenition` path (a **plain document-scope
  `<name:name>` sibling override** — the most-derived layer, `DefinitionInvolvesOverride == true`) has
  **no** fill scope and **no** self-call→base rebind, so the bail **stays** and the template is silently
  un-precompiled, rendered on the dynamic tier exactly as today (entry document D11 — extending the lift
  to it would emit recursive precompiled code for a self-calling sibling). **Only materialization is
  shared; body emission stays the generator's parallel symbol-based writers
  (`NativeExpressionWriter`/`MemberPathWriter`)**, so dynamic == precompiled on region fills is
  **differential-gated, not "by construction"** (review F5).
- **Per-run body-class dedup — NOT cross-build cache-busting (fixes review F8).** `_definitionBodies`
  is a **per-run instance field** (≈287–288); the incremental pipeline re-runs on any change, so there
  is no cross-build cache to bust. The key extension
  `def.Name + "@" + def.Position + "#" + digest(ordered overriddenName→override-body Position)` exists
  **only** to split a *filled* body from the unfilled default (which share `Name@Position`) into
  distinct emitted classes within one run; empty digest ⇒ shares the default body. No `ImportOrigin`/
  multi-file input (populated only under `ProvideLanguageFeatures`, which the generator does not set —
  [`ParseContext`](../../../src/Heddle/Language/ParseContext.cs) ≈47–53).
- **Escape hatch — the generator's PRE-EXISTING silent un-precompile, not HED7014 (fixes review F2).**
  A `reason`-degrade is silent ("no entry, no source — the render takes the byte-identical dynamic
  path", [`HeddleTemplateGenerator`](../../../src/Heddle.Generator/HeddleTemplateGenerator.cs)
  ≈246–248); `HED7014` is the *function-specific* `UnresolvableFunction`
  ([`GeneratorDiagnostics`](../../../src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs)
  ≈113–114). After context-aware resolution, the only region/override fall-through is the generator's
  pre-existing limit — a definition whose **model type is unresolvable/abstract at build**
  (`DefinitionBodyContext` reason, ≈879–882) — left **silently** un-precompiled, rendered on the
  dynamic tier byte-identically. Never override- or self-call-triggered; never HED7014.
- **Negative-fill routing (fixes review C).** A call-site fill candidate that matches a **private**
  region (HED5019) or matches **no** region (dangling base-not-found) is a template the dynamic tier
  will **error** on. The generator must **not** precompile it — `BuildDefinitionCall` returns a `reason`
  so the template is silently un-precompiled and the **dynamic tier raises the error**. Emitting such a
  template would make the precompiled render succeed while the dynamic compile errors, and the runtime
  gauntlet short-circuits the dynamic compile of a precompiled template — so the error would never
  surface. Precompiling only *matched* fills keeps the backends honest.
- **Convergence byte change (review F7).** Context-aware resolution makes the generator honor the
  dynamic tier's definition-first precedence at depth, so a nested inner definition that shadows a
  same-named extension/function it also calls now emits the **definition** (matching the dynamic tier)
  where the old generator emitted the extension (`TemplateEmitter` ≈505–526). This is a deliberate,
  pinned byte change (entry doc Back-compat), not covered by "no golden moves".

The generator needs **no** `RegionLayoutInfo` reimplementation (YAGNI): it emits the materialized
definition through the existing definition-body machinery, borrowing the component's `PropLayoutInfo`.

## Layer 4 — precompiled runtime (`PrecompiledRuntime`)

The precompiled binding surface is **separate** from `PropsBinder`: generated static initializers
call [`PrecompiledRuntime.BindDefinition`](../../../src/Heddle/Precompiled/PrecompiledRuntime.cs)
(≈47–75, verify), which builds the `DefinitionBaseExtension` carriers directly and installs the
frozen props prototype + `PrecompiledPropSetter[]` via
[`DefinitionBaseExtension.SetPrecompiledProps`](../../../src/Heddle/Core/DefinitionBaseExtension.cs)
(≈15–37, verify) — it **bypasses** `PropsBinder`/`InitStart`. Under OQ1 this layer carries the
materialized region fill **natively** (it does not fall back):

- **Region fills (native).** A materialized fill (layer 3) is emitted as an ordinary
  `BindDefinition` call: the outer carrier's body is the caller content (the loose `@out()` body,
  caller context) and its `DefinitionParameterTemplate` inner carrier's body is the **materialized
  component body** — with each overridden region's body baked in as the region's inner definition
  body. The region override bodies read the component's props through the **same** carriage: the
  component's frozen `object[]` prototype + `PrecompiledPropSetter[]` installed via
  `SetPrecompiledProps`, indexed by `PrecompiledRuntime.Prop(scope, index)` (≈104–110, verify). No
  `BindRegion`, no new `BindDefinition` overload — the materialized region body is just another inner
  definition body threaded through the existing carriers.
- **Region defaults (native).** A region default is likewise an inner `BindDefinition` body that
  borrows the component's props carriage (layer 3).

So the precompiled layer's Phase-7 work is **zero new API** — it binds the materialized carriers
through the existing `BindDefinition`, and the region override bodies thread through the existing
prop carriage. The dynamic == precompiled differential is asserted over (a) the unchanged
single-slot/`@out()` corpus, (b) the region-**default** corpus (incl. inner-definition calls, the F1
win), and (c) the region-**fill** corpus (incl. `region_selfcall_to_default`) — all of which precompile
**natively** and match the dynamic tier byte-for-byte. The only fall-through is the pre-existing
unresolvable/abstract-model-type silent un-precompile (`region_abstract_model` — no diagnostic),
rendered byte-identically on the dynamic tier (entry document, *Testing plan*).

## DRY / second consumer

The region work is a genuine DRY consolidation, not speculative abstraction, on two axes. **The
region table** has two consumers — the dynamic tier (fill matching + diagnostics) and the LSP
(completion/hover projection) — while the generator/precompiled path borrows the component's existing
prop carriage rather than duplicating a region carriage; the table gives the dynamic tier an
index-stable, cached, ordinally-keyed lookup of a component's public regions, and the LSP reads the
same parse-model `Regions` — the `PropLayout` precedent at the region seam. **The fill
materialization** (`DefinitionMaterializer`) is single-sourced in `src/Heddle/Language/` (parse-model-
pure) and consumed by *both* backends, so the final-form definition the dynamic tier compiles and the
generator emits is the **same object graph**. Body **emission**, however, stays each backend's own
(the generator's parallel symbol-based writers), so backend byte-identity on overridden fills is
**differential-gated**, not automatic (review F5).
