# Phase 7 — named-content-regions (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-7-named-content-regions.md](../../plan/phase-7-named-content-regions.md)
  (Plan DoR; model ratified as [R7](../../plan/common/standing-rulings.md#standing-rulings-user-set);
  P7 residual items — grammar realization and the `HED5019`+ split — closed here). Governance:
  [R1](../../plan/common/standing-rulings.md#standing-rulings-user-set) (sequenced last),
  [R5](../../plan/common/standing-rulings.md#standing-rulings-user-set) (additive; byte-identity gate),
  [diagnostic-ids](../../plan/common/diagnostic-ids.md) (`HED5019`+ block).
- **Assumes merged:** nothing structurally. Phase 7 is sequenced **last by policy** (R1) and consumes
  no other post-2.0 phase's deliverable. It builds on the already-stable props / single-slot /
  `PropLayout` machinery in the tree today. It shares the diagnostic-ID block with the other post-2.0
  phases (each claims disjoint IDs).

| Document | Purpose |
|---|---|
| this document | The whole of Phase 7: assumed state (four-layer seams re-verified), the closed decisions (grammar model, region-as-definition, region-context scope, call-scoped materialized fill, per-region narrowing via the existing check, the `HED5019`/`HED5020` split, native precompilation of region defaults and fills), the ordered implementation plan across all four layers, the public API/contract, the diagnostics, the testing plan, back-compat proof, performance, and standards notes. |
| [phase-7-grammar.md](phase-7-grammar.md) | The exact `HeddleParser.g4` diff for `<:name>` / `<:name :: Type>`, its additivity/ambiguity proof, listener wiring, and the ANTLR-4.13.1 regeneration work item. |
| [phase-7-region-table.md](phase-7-region-table.md) | The named-region table shape (mirroring `PropLayout`), where the anonymous `@out()` slot lives as the implicit default entry, and how the table threads through each of the four layers as they actually work. |

This spec is self-contained given the [common specs](../common/spec-conventions.md); it restates the
minimal context it needs. Source citations give `path` + member name; load-bearing line numbers carry
their anchor symbol and are marked *(verify at implementation — line numbers drift)*.

## Scope and goal

Additively let a Heddle **definition** expose more than one overridable content region. A component
declares its overridable regions as **inner definitions marked public with a leading colon** —
`<:name>` public, plain `<name>` private (the default) — optionally typed `<:item :: Article>`. The
component renders a region by calling it (`@heading()`), and projects a typed value into it the way
`@out(expr)` projects into the slot (`@item(this)`). A caller fills or overrides a public region with
the **normal `<name:name>` override** it already knows, inside a `@%…%@` block in the call body —
call-scoped, gated by public/private. An override body runs in the **region's own context** (the
component's props plus the region's model), exactly like the default body it replaces.

**In scope:** the `<:name>` grammar; the region-as-definition model and its region table; region-
context body scope; the call-scoped fill (matched public region), the private-region error, and the
undeclared-name additive passthrough; per-region type narrowing (via the existing check); the
`HED5019`/`HED5020` diagnostics;
and threading all of it through the four layers (dynamic runtime, LSP, generator, precompiled) in one
wave.

**Out of scope (plan non-goals, unchanged here):** any change to `@out()` / `@out(expr)` / the single
`out:: Type` header slot and its `HED5012`–`HED5018`/`HED5016`/`HED5017` diagnostics; any change to
props; removal of the sibling-override idiom; a new fill/slot directive; **extension** regions
(extensions have no template body — Phase 8 gives extensions *parameters*, not regions); channel-
threading / provide-inject. `@out()` and every existing single-slot template render **byte-identically
under both backends**.

## Assumed state

Re-verified against current source on branch `feature/lang_asmt`. The four "layers" are **not
uniform**; the corrections below are carried from the spike and re-confirmed here with evidence.

| Seam | Verified state |
|---|---|
| Definition grammar | `def: DEF_STARTNAME ID def_props? def_base? DEF_ENDNAME default_chain? subtemplate def_type?` — `ID` **mandatory**, so `<:name>` is a syntax error today. `def_base: DELIM ID` (`<child:base>`); `def_slot: ID DEF_TYPE ID` lives inside `def_props` (`out:: Type`). All `DEF`-mode tokens for `<:item :: Article>` already exist; the lexer needs no change. [`HeddleParser.g4`](../../../src/Heddle.Language/HeddleParser.g4), [`HeddleLexer.g4`](../../../src/Heddle.Language/HeddleLexer.g4) mode `DEF`. Grammar is generated+committed (`generate_cs.cmd`, ANTLR 4.13.1). See [phase-7-grammar.md](phase-7-grammar.md). |
| Definition parse / override | [`ParseContext.CreateDefinition`](../../../src/Heddle/Language/ParseContext.cs) (≈161–255) builds a `DefinitionItem`; the `def_base` branch resolves the base via `GetDefenition(baseName)` and, when **null**, emits `"Base definition {name} couldn't be found"` (≈217–220) — so a call-body `<x:x>` whose base is not in scope is a hard error today. [`HeddleMainListener.ExitDef`](../../../src/Heddle/Language/HeddleMainListener.cs) (≈69–109) adds/overrides via `OverrideWith`. A call body's subtemplate is parsed into its **own** sub-context whose `DefinitionsBlock` holds its `<name:name>` overrides, and that sub-context is attached as `OutputItem.Context` at [`ExitSubtemplate`](../../../src/Heddle/Language/HeddleMainListener.cs) (≈182–189). |
| Header slot rules (leave unchanged) | `HED5016`/`HED5017` are enforced in [`ParseContext.ParseDefProps`](../../../src/Heddle/Language/ParseContext.cs) (≈279–310), walk-restricted to the literal `out` slot — a **separate** code path from inner-definition parsing. Untouched by Phase 7 (SD-C). |
| **Front-end error copy is a parse→compile phase split (NEW seam — D5's retract target)** | The base-not-found error is emitted at **parse** into the **shared** `ParseContext.Errors`, but the result and the LSP read it through two *separate downstream* lists — a seam the prior draft did not examine (it cited only `ParseContext.cs`). [`DocumentParser.Parse`](../../../src/Heddle/Language/DocumentParser.Runtime.cs) (≈17–26) runs the full parse+listener walk (every `<x:x>` base-not-found goes into `ParseContext.Errors`, ≈217–220) and then, at ≈24, `CopyErrorsTo(context, compileContext, 0)` (≈38–43) copies those error **object references** one-shot into the **separate** `CompileContext.CompileErrors` list. This completes **before** compile: [`HeddleTemplate.Compile`](../../../src/Heddle/HeddleTemplate.cs) (≈319–334) runs `DocumentParser.Parse` at ≈323 to completion **before** `HeddleCompiler.Compile` at ≈326. The final `HeddleCompileResult` reads **only** `CompileContext.CompileErrors` — `result.Errors.AddRange(compileScope.CompileErrors)` (≈333/344), where `CompileScope.CompileErrors => CompileContext.CompileErrors` and the copy ctor shares the one list ([`CompileContext`](../../../src/Heddle/Runtime/CompileContext.cs) ≈112); [`HeddleCompileResult`](../../../src/Heddle/Data/HeddleCompileResult.cs) never re-reads `ParseContext.Errors` (`FillUpLines` walks only its own `Errors`, ≈54–73). The LSP unions **both** lists with **reference** dedup ([`DocumentAnalyzer.ProjectDiagnostics`](../../../src/Heddle.LanguageServices/DocumentAnalyzer.cs) ≈106–148: a `HashSet<HeddleCompileError>` over `CompileContext.CompileErrors` **then** `ParseContext.Errors`; [`HeddleCompileError`](../../../src/Heddle/Data/HeddleCompileError.cs) has **no** `Equals`/`GetHashCode` override, so identity is by reference). **Consequence:** a retract from `ParseContext.Errors` alone is **inert** — the same object is already in `CompileContext.CompileErrors` (result) and stays LSP-reachable there. D5's retract therefore removes the captured error **object reference** from **both** lists, using the same instance `CopyErrorsTo` preserves by reference. |
| **HED5001–HED5004 true raise-site (CORRECTION)** | Raised in [`HeddleCompiler.BindProps`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈1290–1383) — `DuplicatePropArgument`/`UnknownProp`/`PropTypeMismatch` per argument and `MissingRequiredProp` for unbound required slots — **not** in `PropLayout`. [`PropLayout.Resolve`](../../../src/Heddle/Runtime/Expressions/PropLayout.cs) raises only the **declaration-side** `HED5008`/`HED5009`/`HED5010`. `HED5005`/`HED5006` are raised in [`HeddleCompiler.CompileItem`/`CreateExtension`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈762–768, ≈1165–1171). The region diagnostics use reachable sites verified per review F4: **HED5020** at `EnterDef` (parse — the duplicate is never stored), **HED5019** at the fill step; narrowing reuses `WalkValidateDefinitionType` (no HED5021). |
| Definition compile / prop layout activation | [`HeddleCompiler.CreateExtension`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (definition branch ≈1144–1196): resolves `PropLayout` (cached, ≈1161/1238), the slot type (≈1162), sets `def.SlotMode`, binds props, then compiles the **caller content** (≈1180) and the **definition body** (≈1189) — the body under a save/set/restore of `CompileContext.ActivePropLayout`/`SlotParameterType` to *this* definition's own layout (≈1185–1195). Body compilation compiles `definition.ParameterTemplate` against `definition.Context` via `InitStart`→`InitSubTemplate`→`HeddleCompiler.Compile` ([`AbstractExtension`](../../../src/Heddle/Core/AbstractExtension.cs) ≈91–137). **`ActivePropLayout`/`SlotParameterType` are copied into every nested body-compile by the `CompileContext` child copy ctor** ([`CompileContext`](../../../src/Heddle/Runtime/CompileContext.cs) ≈105–122) — the proven at-depth seam a region fill's `RegionFillScope` reuses (D4). Prop reads resolve against `ActivePropLayout` ([`TryCompilePropRead`], ≈1030). |
| Definition resolution is per-context (BLOCKER-A seam) | `CompileBody` passes each output chain's `extensions.Context` into `CompileItem` (≈96, 736–746), and every nested body (`@if`/`@list`/`@for`) resolves against **its own** `ParseContext`, whose `DefinitionsBlock` was eagerly snapshot-copied at parse ([`ParseContext` ctor](../../../src/Heddle/Language/ParseContext.cs) ≈31 → [`DefinitionBlock`](../../../src/Heddle/Language/DefinitionBlock.cs) ≈14–20), then deep-copied per level by `IsolateContext` (≈116–125). So a fill installed only at the **top** of the callee context does **not** reach `@item(this)` nested in a loop — the reason the fill is a resolution-time `RegionFillScope` (D4), not a context install. `OverrideWith` (`DefinitionItem.cs` ≈34–44) is the sibling idiom's in-place parse-time mutation, which propagates for free but is **document-onward** (not call-scoped) and clobbers `ModelType`/`IsRegion` — so it is not used for the fill. |
| Slot carrier (unchanged) | [`OutExtension`](../../../src/Heddle/Extensions/OutExtension.cs) `_slotMode`, [`SlotContent`](../../../src/Heddle/Core/SlotContent.cs), and [`DefinitionBaseExtension`](../../../src/Heddle/Core/DefinitionBaseExtension.cs) `SlotMode`/`RenderCallerContent` carry the single `@out()` slot. Phase 7 adds no per-render state to these — region state is compile-time and bakes into the per-call-site compiled body. |
| LSP linkage (TRANSITIVE) | [`DocumentAnalyzer`](../../../src/Heddle.LanguageServices/DocumentAnalyzer.cs) drives `HeddleCompiler.Compile` directly (≈45) and projects definitions via `ProjectDefinitions` (≈150–166) into [`DefinitionInfo`](../../../src/Heddle.LanguageServices/DefinitionInfo.cs). Region diagnostics surface for free; region projection is additive. |
| Generator — links parse-model **sources**, not the runtime assembly | [`TemplateEmitter`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) reuses the ANTLR parse and reimplements **type resolution** over Roslyn symbols (`PropLayoutInfo`, ≈923–977). It does **not** reference the compiled `Heddle` runtime assembly's internals; it **compile-links the parse-model *sources*** — `<Compile Include="..\Heddle\Language\**\*.cs" …>` ([`Heddle.Generator.csproj`](../../../src/Heddle.Generator/Heddle.Generator.csproj) ≈49–52) — so `DefinitionItem`/`ParseContext`/`OverrideWith`/`IsolateContext` (and a new `DefinitionMaterializer`) are available *if* they are parse-model-pure (no `CompileScope`/`ExType`/runtime-only types). **F1 correction:** `BuildCall` resolves a call **only against the document root** `_parse` (≈493–494, ctor ≈91); `DefinitionBlock` is a flat snapshot with no downward chaining ([`DefinitionBlock.cs`](../../../src/Heddle/Language/DefinitionBlock.cs) ≈10–21). So an **inner** definition of a component body (`@heading()`) currently misses (≈493) → `reason = "named extension 'heading'"` (≈610) → the template is **silently** left un-precompiled ("no entry, no source — the render takes the byte-identical dynamic path", [`HeddleTemplateGenerator`](../../../src/Heddle.Generator/HeddleTemplateGenerator.cs) ≈246–248). **So "region defaults already precompile natively" is FALSE today** — only *document-scope* siblings are visible via `_parse`. **F2 correction:** that silent degrade is **not** HED7014 — `HED7014`/`UnresolvableFunction` ([`GeneratorDiagnostics`](../../../src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs) ≈113–114) is the *function-specific* marker (a separate `IsMarker` path). Phase 7 adds context-aware resolution + materialization to precompile inner-def calls (region defaults) and region fills natively (D8/WI4); the plain sibling-override idiom keeps this silent degrade (D11). |
| Precompiled runtime (SEPARATE mechanism) | [`PrecompiledRuntime.BindDefinition`](../../../src/Heddle/Precompiled/PrecompiledRuntime.cs) (≈47–75) builds the `DefinitionBaseExtension` carriers and installs the frozen prototype + `PrecompiledPropSetter[]` via [`DefinitionBaseExtension.SetPrecompiledProps`](../../../src/Heddle/Core/DefinitionBaseExtension.cs) (≈15–37) — it **bypasses** `PropsBinder`. No `BindRegion` is added; region defaults and materialized fills alike are inner definition bodies bound through the existing `BindDefinition`, reading the component's props via the existing carriage. |
| Diagnostic IDs | [`HeddleDiagnosticIds`](../../../src/Heddle/Data/HeddleDiagnosticIds.cs): highest `HED5xxx` is `HED5018`; **`HED5019` is next free**. [`DiagnosticIdTests.ConstantsMatchTheDiagnosticsTableOneToOne`](../../../src/Heddle.Tests/DiagnosticIdTests.cs) reflects over the consts and asserts a one-to-one match with an in-test expected set (currently ending at `HED5018`) — each new const must be added to both the class and the test's expected set. |

## Design decisions

Every P7 residual and lean is closed here.

### D1 — The `<:name>` / `<:name :: Type>` grammar realization
- **Decision.** Add exactly one alternative to `def` plus one helper rule (full diff, ambiguity/
  additivity proof, and the ANTLR-4.13.1 regen work item in [phase-7-grammar.md](phase-7-grammar.md)):
  ```antlr
  def:
        DEF_STARTNAME ID def_props? def_base? DEF_ENDNAME default_chain? subtemplate def_type?
      | DEF_STARTNAME DELIM ID def_region_type? DEF_ENDNAME subtemplate      // NEW: <:name> public region
      ;
  def_region_type: DEF_TYPE ID;                                             // NEW: <:item :: Article>
  ```
  No lexer edit. The region form carries no prop list, no base, no default output chain, and the type
  is **in-header** (R7's `<:name :: Type>`). Disambiguation from `<child:base>` and `<name>` is by
  position — a `DELIM` (`:`) immediately after `DEF_STARTNAME`, with nothing before it.
- **Rationale.** `< : name …` matches no production today, so the alternative is purely additive; the
  in-header type matches R7 verbatim; all tokens already exist so the lexer (and every existing
  token stream) is byte-identical.
- **Alternatives rejected.** Trailing `def_type` for the region type (`<:item>{{…}} :: Article`) —
  contradicts R7's ratified `<:item :: Article>` surface, and reads worse for a region's contract.
  A new lexer token for the leading colon — unnecessary; `DELIM` already exists in `DEF` mode.
  Making the leading `ID` optional in the old alternative — would make `<:name>` ambiguous with a
  nameless `<child:base>` and churn the generated context accessors.
- **Grounding.** [phase-7-grammar.md](phase-7-grammar.md); [`HeddleParser.g4`](../../../src/Heddle.Language/HeddleParser.g4); [`HeddleLexer.g4`](../../../src/Heddle.Language/HeddleLexer.g4).

### D2 — A region is an inner definition (the region-as-definition model)
- **Decision.** A region *is* a `DefinitionItem`. A public region (`<:name>`) sets
  `IsRegion = true, IsPublicRegion = true`; a component's private inner definition (plain `<name>`
  parsed inside a component body) sets `IsRegion = true, IsPublicRegion = false`. The region's model
  type is its `def_region_type` (`:: Type`), or `object`/abstract when omitted. The parser records
  each component's directly-declared regions into `DefinitionItem.Regions` (a `RegionDeclaration`
  list mirroring `PropDeclaration`). Nothing new is invented for rendering: a region is rendered by
  being called (`@heading()`), and a typed value is projected via the definition-call model argument
  (`@item(this)`), which already type-checks against the definition's model type.
- **Rationale.** Reuses the whole definition machinery (typing, abstract/late binding, recursion,
  composition) for free; keeps "region" a single visibility bit on a definition, exactly as R7 says.
- **Alternatives rejected.** Multiple anonymous `out::` slots — rejected in the debate (positional
  multi-slots don't compose with override/inheritance/narrowing). A parallel "region" concept
  separate from definitions — duplicates definition machinery (DRY/YAGNI violation).
- **Grounding.** R7; [phase-7-region-table.md](phase-7-region-table.md);
  [`DefinitionItem`](../../../src/Heddle/Language/DefinitionItem.cs).

### D3 — The named-region table (shape, default entry, four-layer carriage)
- **Decision.** A flattened, index-stable, ordinally-keyed `RegionLayout` (mirroring `PropLayout`),
  resolved once per component and cached in `CompileContext.ResolvedRegionLayouts`. The anonymous
  `@out()` slot is the table's **implicit default entry** and keeps its **existing machinery** (
  `SlotTypeName` + `OutExtension` + `SlotContent`); the table holds only the *named* regions. Shape,
  the byte-identity anchor, and the differing per-layer carriage are specified in
  [phase-7-region-table.md](phase-7-region-table.md).
- **Rationale.** `PropLayout` already solved "one shared table threaded through four layers"; the
  region table is the same precedent at the region seam, and a second consumer (Phase 8's parallel
  work, plus the LSP here) makes the abstraction warranted rather than speculative.
- **Alternatives rejected.** A generator-side `RegionLayoutInfo` reimplementation — YAGNI: the
  generator emits region *defaults* and *materialized fills* (D8) through the existing definition-body
  machinery, borrowing the component's `PropLayoutInfo`, so it needs no region carriage of its own. A
  precompiled `BindRegion` entry point — none needed: defaults and materialized fills alike are inner
  definition bodies bound through the existing `BindDefinition`, reading the component's props via the
  existing frozen-prototype carriage ([phase-7-region-table.md](phase-7-region-table.md) layer 4).
- **Grounding.** [phase-7-region-table.md](phase-7-region-table.md); [`PropLayout`](../../../src/Heddle/Runtime/Expressions/PropLayout.cs).

### D4 — Filling: a call-scoped `RegionFillScope` consulted at resolution (propagates to any depth)
The prior draft installed the materialized fill at the **top** of the callee's isolated context. Both
reviewers proved that does **not** reach region calls nested below the top of the call body:
definition resolution is **per-chain-context** — `CompileBody` passes `extensions.Context` into
`CompileItem` ([`HeddleCompiler.cs`](../../../src/Heddle/Runtime/HeddleCompiler.cs) ≈96, 736–746), and
every nested body (`@if`/`@list`/`@for`) resolves against **its own** `ParseContext`, whose
`DefinitionsBlock` was eagerly snapshot-copied at parse ([`ParseContext` ctor](../../../src/Heddle/Language/ParseContext.cs) ≈31 → [`DefinitionBlock`](../../../src/Heddle/Language/DefinitionBlock.cs) ≈14–20) and then deep-copied per level by `IsolateContext` (≈116–125). So a top-level install misses
`@item(this)` inside `feed`'s `Articles` loop (**the flagship fixture would fail while the differential
passes** — both tiers miss identically). The sibling idiom escapes this only because `OverrideWith`
**mutates the shared `DefinitionItem` in place at parse** (`HeddleMainListener.cs` ≈103) *before* the
snapshots copy the reference — a propagation property a single top-level install lacks.

- **Decision — resolution-time consultation of a call-scoped fill scope, threaded like `ActivePropLayout`.**
  1. **`RegionFillScope` on `CompileContext`.** A new `internal RegionFillScope RegionFillScope { get; set; }`
     on [`CompileContext`](../../../src/Heddle/Runtime/CompileContext.cs) — a small immutable map
     `regionName → materialized-fill DefinitionItem`. It is **copied in the child-context copy ctor**
     (≈105–122, beside `ActivePropLayout`/`SlotParameterType`), so every nested body-compile inherits
     it. This is the **proven at-depth seam**: `@(theme)` prop reads work inside a `@list` body *because*
     that copy ctor carries `ActivePropLayout` down (`InitSubTemplate` builds the child context at
     [`AbstractExtension.cs`](../../../src/Heddle/Core/AbstractExtension.cs) ≈103). The fill scope rides
     the identical path, so it provably reaches `@item(this)` at any depth.
  2. **Set at the callee body compile, call-scoped.** In `HeddleCompiler.CreateExtension`'s definition
     branch, the fill scope is set on `compileContext` in the **same save/set/restore block** that sets
     `ActivePropLayout`/`SlotParameterType` (≈1185–1195): `savedFill = compileContext.RegionFillScope;
     compileContext.RegionFillScope = fillScope; …compile def body…; compileContext.RegionFillScope =
     savedFill;`. Because each distinct `@feed(...)` call site is a distinct `OutputItem` compiled under
     its own fresh `CompileContext` (memoized per `OutputItem`; nested compile makes a fresh child
     context, `AbstractExtension.cs` ≈103), the scope is **call-scoped** — a second `@feed(...)` with no
     fills compiles feed's body with an empty/inherited scope and renders defaults (`region_feed_two_calls`).
  3. **Consulted at the one resolution point.** `CompileItem` (≈744–746) resolves definition-first;
     it now consults the ambient fill scope **before** the parse-context lookup:
     ```csharp
     var fill = compileScope.CompileContext.RegionFillScope;
     if (fill != null && fill.TryGet(name, out var filled)) definitionItem = filled;
     else if (parseContext.DefenitionExists(name)) definitionItem = parseContext.GetDefenition(name);
     ```
     This reaches every `@heading()`/`@item(this)`/`@divider()` at any depth in feed's body — the fill
     scope is on the parent-chained `CompileContext`, not the per-level `ParseContext`. No install into
     any `ParseContext`; no per-call `IsolateContext` of the fill is needed.
  4. **Materialized fill (`DefinitionMaterializer`).** For each matched public region, the scope entry is
     a purpose-built layered `DefinitionItem` (**not** a blanket `OverrideWith`, which clobbers
     `ModelType` and drops `IsRegion`, review F3): `BaseDefinition` = the region default;
     `ParameterTemplate`/`Context` = the override body; `ModelType` = the override's narrowing `:: Type`
     if declared else the region default's `ModelType`; `IsRegion = true`, `IsPublicRegion` = the
     region's; **`Position` = the override declaration's span** (so HED5019 and the narrowing error land
     *at the override*, review F5/E).
  5. **Self-reference → base (correct self-call termination).** When `CreateExtension` compiles region
     `R`'s fill **body**, it sets, for that body's compile only, a fill-scope variant where `R → R`'s
     base default (its `BaseDefinition`). So a self-call `@heading()` inside a `<heading:heading>` fill
     resolves to the base default and terminates; **sibling** calls (`@divider()` from a fill body)
     still resolve to their own scope entry — satisfying D6/R7 "exactly like the default body it
     replaces." This is the layered-order rule, applied precisely at the fill-scope map (no dependence
     on `FullOverride` or `IsolateContextWithTree`).
- **Rationale.** The fill scope reuses the *proven* `ActivePropLayout` propagation seam, so it reaches
  nested region calls by construction of the same inheritance the engine already relies on; call-scoping
  falls out of the per-call `CompileContext`; the self-reference rule gives correct self-call
  termination without the parse-time `FullOverride` machinery (which is unavailable to a fill).
- **Alternatives rejected.** Top-level install into the isolated callee context (prior draft) — proven
  not to reach nested region calls (both reviewers). Parse-time in-place layering like the sibling idiom
  — cannot be call-scoped (mutating the shared parse instance leaks document-onward to all calls), and
  the snapshots already exist by compile time. A dedicated `@fill` directive — redundant (R7).
- **Grounding.** [`CompileContext`](../../../src/Heddle/Runtime/CompileContext.cs) (≈105–122 copy ctor, ≈91/97 `ActivePropLayout`/`SlotParameterType`); [`HeddleCompiler`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈96/736–746 `CompileItem` resolution, ≈1185–1195 save/set/restore); [`AbstractExtension.InitSubTemplate`](../../../src/Heddle/Core/AbstractExtension.cs) (≈103); [`DefinitionItem`](../../../src/Heddle/Language/DefinitionItem.cs) (≈26, ≈34–44).

### D5 — Additivity via emit-then-retract on BOTH the runtime and parse error lists (no sweep)
The prior draft's document-finalization sweep over `_subContexts` was proven unreachable (review B):
`AddSubContext` receives the **pre-isolation shell** (`HeddleMainListener.cs` ≈154); the isolated
replacement that actually holds the parsed candidates (pushed at ≈157/162) is never linked into
`_subContexts`, and "top-level `HeddleCompiler.Compile`" has no discriminator (it re-enters per
sub-body via `AbstractExtension.InitSubTemplate` ≈104). Replaced with a reachable mechanism:

- **Decision — emit at parse, retract on match.** A call-body `<x:x>` whose base is unresolved today
  raises a hard `"Base definition x couldn't be found"` at
  [`ParseContext.CreateDefinition`](../../../src/Heddle/Language/ParseContext.cs) (≈217–220). Keep
  emitting it **at parse, into the root-shared `Errors` list** (`Errors` is shared up the whole context
  chain — [`ParseContext`](../../../src/Heddle/Language/ParseContext.cs) ≈36 `Errors = parentContext?.Errors ?? new List<>` — so it is always reachable, unlike `_subContexts`). Simultaneously capture a
  **`RegionFillCandidate`** {name, override body + its `ParseContext`, optional narrowing type,
  declaration position, **the emitted error object**, **origin = the caller-content `ParseContext`**}
  into a root-shared `RegionFillCandidates` list (initialized the same shared way as `Errors`;
  it is a per-document list, matched by a **linear `origin == extensionItem.Context` scan per call
  site** — acknowledged, not a correctness issue at expected candidate counts). Do
  **not** register the `<x:x>` in any `DefinitionsBlock` (so it never self-shadows the region default a
  self-call must reach). At each definition call site, the fill step matches candidates whose
  `origin == extensionItem.Context` (reference equality — the caller content), and on a **public** match
  **retracts** the error and adds the fill to the call's `RegionFillScope`; a **private** match retracts
  the error and raises **HED5019**; a candidate that matches **no** region (genuinely dangling, or not in
  a region-declaring callee's caller content) keeps its already-emitted error — byte-identical to today.

  **The retract removes the error object from BOTH downstream lists (phase-split seam).** The emitted
  error object was copied **by reference** into `CompileContext.CompileErrors` at parse time — before
  compile — by `DocumentParser.CopyErrorsTo` ([`DocumentParser.Runtime.cs`](../../../src/Heddle/Language/DocumentParser.Runtime.cs)
  ≈38–43), and the final `HeddleCompileResult` reads **only** that list (Assumed state: *Front-end error
  copy is a parse→compile phase split*). So a `ParseContext.Errors`-only removal is **inert** — the bogus
  "Base definition {name} couldn't be found" would survive on every genuinely-filled region and break the
  happy path. The retract therefore removes the **same object reference** from **both**:
  ```csharp
  compileScope.CompileErrors.Remove(candidate.Error);   // runtime-authoritative — the result reads this
  candidate.Origin.Errors.Remove(candidate.Error);      // parse list — the LSP union reference-dedups over it
  ```
  `compileScope.CompileErrors` is `CompileContext.CompileErrors` (delegated; the copy ctor shares the one
  list, [`CompileContext`](../../../src/Heddle/Runtime/CompileContext.cs) ≈112), and `candidate.Origin.Errors`
  is the shared parse list the base-not-found was emitted into (≈36). Because `CopyErrorsTo` preserved the
  reference, the *identical* instance is `.Remove`-able from each. **Additivity now actually holds:** a
  matched fill removes the error from result **and** LSP (gone from both); a genuinely dangling `<x:x>`
  retracts nothing, so its error object stays in both lists with byte-identical text and position — exactly
  as today.
- **Rationale.** The error exists by default (emitted at parse to the reachable shared list) and is
  removed **only** on a positive region-fill match at a reachable call site (via `extensionItem.Context`,
  which the compiler holds directly — no sweep, no `_subContexts` walk). A `<x:x>` invalid today keeps
  its identical error unless it is a genuine fill, so additivity (R5) holds exactly. `<x:x>` with an
  unresolved base is invalid in every template today, so emit-then-retract cannot change any template
  valid today.
- **Alternatives rejected.** Deferring the error + a `_subContexts` finalization sweep (prior draft) —
  the isolated candidate-bearing contexts are **not** in `_subContexts` (review B), so the sweep never
  reaches them and a today-invalid template would silently compile (breaks R5). Deferring without a
  reliable re-emit — same failure.
- **Grounding.** [`ParseContext`](../../../src/Heddle/Language/ParseContext.cs) (≈36 shared `Errors`, ≈156–159 `AddSubContext`, ≈217–220 base-not-found); [`DocumentParser.Runtime.cs`](../../../src/Heddle/Language/DocumentParser.Runtime.cs) (≈17–26 parse→`CopyErrorsTo`, ≈38–43 by-reference copy into `CompileErrors`); [`HeddleTemplate.Compile`](../../../src/Heddle/HeddleTemplate.cs) (≈319–334 parse-completes-before-compile, ≈333/344 result reads `CompileErrors`); [`CompileContext`](../../../src/Heddle/Runtime/CompileContext.cs) (≈112 shared `CompileErrors`); [`HeddleCompileResult`](../../../src/Heddle/Data/HeddleCompileResult.cs) (≈54–73 reads only its own `Errors`); [`DocumentAnalyzer.ProjectDiagnostics`](../../../src/Heddle.LanguageServices/DocumentAnalyzer.cs) (≈106–148 union of both lists, reference dedup); [`HeddleCompileError`](../../../src/Heddle/Data/HeddleCompileError.cs) (no `Equals`/`GetHashCode`); [`HeddleMainListener`](../../../src/Heddle/Language/HeddleMainListener.cs) (≈154, ≈157/162 isolation); [`AbstractExtension`](../../../src/Heddle/Core/AbstractExtension.cs) (≈104).

### D6 — Override-body scope = the region's own context (component props + region model)
- **Decision.** A region body — default **or** filled — compiles with (a) `ActivePropLayout` = the
  **enclosing component's** layout, (b) model type = the region's declared `:: Type` (or the ambient
  model when abstract), and (c) `SlotParameterType` = the **region's own** (i.e. `null` — a region
  declares no `out::` slot), **not** the enclosing component's slot type. Concretely, in
  `CreateExtension`'s definition branch, when the definition being compiled has `IsRegion == true`, the
  def-body save/set/restore (≈1185–1195) keeps the enclosing `ActivePropLayout` (`= savedLayout`)
  instead of pushing the region's own (empty) layout, while `SlotParameterType` is set to the region's
  own resolved slot type (`null`) as the existing code already does — so `@(theme)`/`@(title)` in
  `heading` resolve to the component's props, `@(Title)` in `item` resolves against `Article`, and an
  `@out(value)` inside a region body is the ordinary "no slot" error (**HED5012**), **never** a
  projection of the *component's* `@out()` slot (which a region is not the host of). A **filled** region
  keeps `IsRegion == true` because the D4 materializer sets it explicitly (no blanket `OverrideWith`,
  review F3). The generator's symbol-side twin is in [phase-7-region-table.md](phase-7-region-table.md)
  (layer 3).
- **Rationale.** An override body has the same scope as the default body it replaces (R7); a region
  is lexically nested in the component, so it resolves the component's props — a single rule that
  serves both the default and the fill, with no new scoping concept.
- **Alternatives rejected.** Caller-context scope for the override body (as `@out()` uses) —
  contradicts R7 and the "override = default-body scope" invariant; would surprise member resolution.
  Giving each region its own prop layout — a region declares no props; it borrows the component's.
- **Grounding.** [`HeddleCompiler.CreateExtension`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈1183–1195); [`TryCompilePropRead`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈1030).

### D7 — Per-region narrowing reuses the existing assignable-only check (no new id — review F4)
- **Decision.** A matched region override may narrow the region's model type only in the **assignable-
  only** direction. Because the D4 materializer builds the filled region as a layered `DefinitionItem`
  with `BaseDefinition` = the region default, this narrowing is **already validated by the pre-existing
  `WalkValidateDefinitionType`** ([`HeddleCompiler`](../../../src/Heddle/Runtime/HeddleCompiler.cs)
  ≈1423–1451), which runs in `CompileFromDefenition` on every base-chained definition and raises the
  id-less `"The new definition type <X> isn't assignable to base <Y>."` when the override type is not
  assignable to the region's declared type — the same assignable-only direction as `HED5008`/`HED5014`.
  **The materialized fill reaches this check on the ordinary definition-call path (traced):** `CompileItem`
  substitutes `definitionItem = filled` (≈744–746) and `CreateExtension` then compiles it as a
  base-chained definition (`definition != null` branch), calling `CompileFromDefenition` — which invokes
  `WalkValidateDefinitionType` (≈1406) — at **both** ≈1146 and ≈1189; the fill's `BaseDefinition` is the
  region default, so the `ModelType`-vs-region-base assignability check fires (twice, see below) exactly
  as for a `<child:base>`, with `region_typed_narrow` asserting it.
  **No `HED5021` is minted** (a region-specific id would double-report with this pre-existing error —
  review F4b). An override with no declared type inherits the region's type unchanged. A typed override
  body reading a member not on the region's type is the ordinary member-resolution error (`HED0001`) at
  the read site — the override body compiles under the region's type.
- **Double-report, accepted and documented (review E).** `CreateExtension` calls `CompileFromDefenition`
  **twice** per definition call site (≈1146 and ≈1189), and `WalkValidateDefinitionType` has **no
  `CompileErrors` dedup**, so the id-less narrowing error is added **twice** — a *pre-existing* behavior
  of every `<child:base>` narrowing, not new to regions. Phase 7 **accepts and documents** it rather than
  guarding a shared non-region path (out of region scope; YAGNI). The `region_typed_narrow` fixture
  asserts the positioned id-less error is present (at the override's `:: Type`, via the D4 fill
  `Position`), matching the pre-existing `<child:base>` count — **not** "a single error" (the prior
  draft mis-verified this). Collapsing the double-report to one is a separate pre-existing-bug fix.
- **Rationale.** Regions are definitions layered over their default (D4), so the definition-override
  type check the engine already performs *is* the region narrowing check — reusing it avoids a new id
  and a redundant region-specific twin (DRY/YAGNI), at the cost of inheriting the pre-existing
  double-report, which is honestly documented rather than papered over.
- **Alternatives rejected.** A dedicated `HED5021` for narrowing — dead-on-arrival double report with
  `WalkValidateDefinitionType`; suppressing/deduping the pre-existing error — churns a shared non-region
  code path (all `<child:base>`) for no region-scoped gain. A region id for typed-body member errors —
  redundant with `HED0001` (maintainer-confirmed, escalation #1).
- **Grounding.** [`HeddleCompiler.WalkValidateDefinitionType`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈1423–1451); [`PropLayout.Resolve`](../../../src/Heddle/Runtime/Expressions/PropLayout.cs) (≈104–118, the HED5008 direction); [`OutExtension.InitStart`](../../../src/Heddle/Extensions/OutExtension.cs) (≈62–67, the HED5014 direction).

### D8 — Native precompilation of region defaults and fills via context-aware resolution + materialization (OQ1)
- **Decision.** Per maintainer ruling **[OQ1](OPEN-QUESTIONS.md#oq1--does-overrideinheritance-stop-native-precompilation-of-a-region-fill--status-resolved-2026-07-13)** (RESOLVED 2026-07-13 — "Phase 7 **natively precompiles region fills** … Scope is bounded to
  definition composition (Phase 7)"), the generator precompiles region **defaults** and region **fills**
  **natively**. OQ1's bail lift is **bounded to the region-fill materialization** — the plain
  document-scope sibling-override idiom (D11) is **not** in scope and keeps the generator's pre-existing
  silent un-precompile. Two source-grounded generator capabilities make the region-default/region-fill
  path possible; both are new, risk-bearing generator work specified as WI4:
  - **Context-aware definition resolution (fixes review F1).** Today `TemplateEmitter.BuildCall`
    resolves a call **only against the document root** — `if (name.Length != 0 && _parse.DefenitionExists(name)) return BuildDefinitionCall(_parse.GetDefenition(name), …)`
    ([`TemplateEmitter.cs`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) ≈493–494), where
    `_parse` is the ctor-injected document root (≈91) and `DefinitionBlock` is a flat snapshot with no
    downward chaining ([`DefinitionBlock.cs`](../../../src/Heddle/Language/DefinitionBlock.cs) ≈10–21).
    So an **inner** definition of a component body (`@heading()` inside `feed`) misses at ≈493 and the
    template degrades. WI4 threads the **enclosing body's `ParseContext`** (and, for a fill, the
    materialized per-call context) into `BuildCall`/`PopulateBody` so inner-definition calls — region
    defaults **and** the materialized region/sibling fills — resolve against the enclosing component's
    definition scope. This also resolves a **self-call** correctly: the materialized fill's context
    binds the region name → the region default (D4), so `@heading()` inside a `<heading:heading>` fill
    body resolves to the base default and terminates — **no self-call escape hatch is needed**.
  - **Materialization (shared transform), bail lifted only for the region fill.** The D4 fill
    materialization is factored into `DefinitionMaterializer` in `src/Heddle/Language/` (parse-model-pure;
    consumed by both backends — F5). `TemplateEmitter.BuildDefinitionCall`'s
    `DefinitionInvolvesOverride`→`return null` bail (≈688; helper ≈1140–1149) is lifted **only when the
    call resolves to a materialized region fill** (a `DefinitionItem` produced by `DefinitionMaterializer`
    from a `RegionFillCandidate` matched against the callee's public region, carried in the ambient
    `RegionFillScope`): for that def the generator materializes the effective definition and emits its body
    via `GetOrBuildDefinitionBody` (≈832–866), **with the fill scope active so a self-call resolves to the
    region base default (D4 step 5) — no recursive self-call is ever emitted.** A definition that reaches
    `BuildDefinitionCall` via the ordinary flat `_parse.GetDefenition` path (a plain document-scope
    `<name:name>` sibling override — the most-derived layer, `DefinitionInvolvesOverride == true`) has **no
    fill scope** and therefore **no self-call→base rebind**, so the bail stays as-is and the template is
    silently un-precompiled and rendered on the dynamic tier — exactly as today (D11). The bail's own
    source comment (≈682–687) reserves generator-side document-order layering as a follow-up; Phase 7 does
    **not** pull that follow-up in.
- **The escape hatch is the generator's PRE-EXISTING silent un-precompile — not HED7014 (fixes review
  F2).** A `reason`-degrade today is **silent**: the template is "simply left un-precompiled: no entry,
  no source — the render takes the byte-identical dynamic path"
  ([`HeddleTemplateGenerator`](../../../src/Heddle.Generator/HeddleTemplateGenerator.cs) ≈246–248).
  `HED7014` is the **function-specific** `UnresolvableFunction` descriptor
  ([`GeneratorDiagnostics.cs`](../../../src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs)
  ≈113–114) — it cannot carry a region reason and its shipped text must not be reworded (D1/X3 stable-id
  discipline). After native resolution exists, the cases on the region-default / region-fill
  path that fall through are the generator's *pre-existing, non-region-specific* limits — a definition
  whose **model type is unresolvable/abstract at build** (`DefinitionBodyContext` returns a reason,
  [`TemplateEmitter`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) ≈879–882) or a genuinely
  `:: dynamic` shape the symbol tier rejects, **plus any other pre-existing bail a resolved definition
  can already hit today** — e.g. the fill's host component is itself a document-scope sibling override
  (its `@feed()` call bails, so the whole call, fills included, un-precompiles), or the fill body
  transitively reaches a sibling override or an unreproducible prop value. What the region-fill path
  never *adds* is an override- or self-call-specific fall-through of its own. These leave the template un-precompiled **silently** and
  the dynamic tier renders it byte-identically — exactly how any such definition is handled today. On the
  region-fill path, **region-fill override-presence and region-fill self-calls never trigger the
  fall-through** (the fill scope resolves them natively). Separately, the plain sibling-override idiom
  (not a region fill) keeps its own pre-existing silent degrade — the `DefinitionInvolvesOverride` bail
  stays on that flat-`_parse` path (D11) — which is the same silent-un-precompile mechanism, not a new
  diagnostic and not a wrong render.
- **Parity is differential-GATED, not "by construction" (fixes review F5).** Only **materialization**
  is shared source; body **emission** stays the generator's parallel symbol-based reimplementation
  (`NativeExpressionWriter`/`MemberPathWriter`). So dynamic == precompiled on fills is proven by the
  differential harness, not asserted as automatic.
- LSP gets fill diagnostics transitively and adds region projection; the precompiled runtime needs
  **no new API** ([phase-7-region-table.md](phase-7-region-table.md) layer 4).
- **Rationale.** OQ1: override/inheritance does not inherently stop precompilation — a call site is a
  materialized definition; the real limitations for the **region fill** were flat document-root
  resolution (F1) and child/parent-compiled-separately. Context-aware resolution + materialization lift
  both **for the region fill**, so overridden region fills precompile natively and the differential
  asserts byte-identity on them. The lift is scoped to the fill because the fill scope supplies the
  self-call→base rebind (D4 step 5) that makes a self-calling override terminate; the plain sibling
  override has no such rebind on the generator's flat `_parse` path, so OQ1 (region fills only) leaves it
  on the pre-existing silent degrade (D11).
- **Alternatives rejected.** Riding the override→dynamic fallback for the **region fill** (author-time
  OQ1 provisional default) — rejected by OQ1's resolution (native precompilation of region fills). A
  "layered self-call" escape hatch with a HED7014 named reason (an earlier draft) — **unimplementable**
  (F2: the degrade is silent, and HED7014 is a function-specific descriptor) and **unnecessary** for the
  fill (F1's context-aware resolution + the D4-step-5 fill-scope rebind render a fill's self-calls
  natively). Extending the bail lift to the plain sibling-override idiom — out of OQ1's bound and
  **unsafe**: a self-calling sibling override resolved via flat `_parse` has no self-call→base rebind, so
  lifting the bail for it would emit recursive precompiled code (D11).
- **Grounding.** [OQ1 RESOLVED](OPEN-QUESTIONS.md#oq1--does-overrideinheritance-stop-native-precompilation-of-a-region-fill--status-resolved-2026-07-13); [X2](common/cross-cutting.md#x2--the-four-layer-prop--region-realization-phase-7--phase-8); [`TemplateEmitter`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) (≈91, ≈493–494, ≈677–745, ≈832–866, ≈879–882, ≈1140–1149); [`HeddleTemplateGenerator`](../../../src/Heddle.Generator/HeddleTemplateGenerator.cs) (≈246–248); [`GeneratorDiagnostics`](../../../src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs) (≈113–114); [phase-7-region-table.md](phase-7-region-table.md).

### D11 — The plain sibling-override idiom keeps the pre-existing silent degrade (native lift is region-fill-only)
- **Decision.** OQ1's native precompilation is **bounded to the region fill** (a materialized fill
  matched against a callee's *public region*, carried in the ambient `RegionFillScope`). The pre-existing
  **plain sibling-override idiom** (`<name:name>` overriding a document-scope sibling definition, e.g.
  patterns.md `page_shell` + `<shell_header:shell_header>`) is **not** natively precompiled by Phase 7:
  it reaches `BuildDefinitionCall` via the flat `_parse.GetDefenition` path (the most-derived layer,
  because `OverrideWith` mutates the shared `DefinitionItem` in place at parse,
  [`HeddleMainListener.cs`](../../../src/Heddle/Language/HeddleMainListener.cs) ≈103), where
  `DefinitionInvolvesOverride` (≈688 / ≈1140–1149) is **true** and the bail stays. Such a template is
  **silently left un-precompiled** and rendered byte-identically on the dynamic tier — **exactly as
  today** (no backend change for the sibling idiom).
- **Rationale — why the lift cannot safely extend to siblings.** The bail exists because the generator
  resolves definitions flatly through `_parse.GetDefenition` (always the most-derived layer), so *"an
  override calling itself would recurse forever"* (the bail's own source comment,
  [`TemplateEmitter.cs`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) ≈682–687); the dynamic
  tier's `DefinitionResolver` resolves such a self-call to the **base** layer and terminates. Phase 7's
  self-call→base cure lives **only** in the `RegionFillScope` (D4 step 5), and a plain sibling override is
  **not** a fill candidate (D12 creates **no** `RegionFillCandidate` for an in-scope `<x:x>`), so it gets
  **no** fill-scope entry and **no** self-call→base rebind. Lifting the bail for it would therefore emit
  **recursive** precompiled code for the self-calling sibling `<x:x>{{ … @x() … }}` (a hang / stack
  overflow at render, diverging from the terminating dynamic tier) — the exact failure the bail prevents.
  The generator-side document-order layering that would make a sibling self-call resolve to its base is
  the follow-up the bail's comment (≈686–687) explicitly reserves; Phase 7 does **not** pull it in. So
  Phase 7 stays inside OQ1's bound (region fills), and the sibling idiom keeps its pre-existing,
  correct, silent degrade.
- **Alternatives rejected.** Extending the native materialization to the plain sibling-override idiom
  (an earlier draft) — **out of OQ1's ratified bound** (region fills only) and **unsafe**: the
  self-calling sibling has no self-call→base rebind on the flat `_parse` path, so it emits recursive code
  the differential corpus cannot pre-empt (the flagship `patterns_sibling_shell` sample,
  [`patterns.md`](../../patterns.md#components-with-multiple-content-regions) ≈415, does **not** self-call,
  so it would not exercise the recursion). Fixing it properly requires the reserved generator-side
  document-order layering follow-up, which is beyond Phase 7. A `region_sibling_selfcall` fixture
  (Testing plan) guards that a self-calling sibling override is **not** precompiled and renders correctly
  on the dynamic tier.
- **Grounding.** [`TemplateEmitter.BuildCall`/`BuildDefinitionCall`/`DefinitionInvolvesOverride`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) (≈493–494, ≈677–745, ≈682–687 bail comment, ≈1140–1149); [`HeddleMainListener.ExitDef`](../../../src/Heddle/Language/HeddleMainListener.cs) (≈103 `OverrideWith` in-place mutation); D4 step 5; D12; [`patterns.md` → components with multiple content regions](../../patterns.md#components-with-multiple-content-regions).

### D12 — Fill routing precedence: only an unresolved-base `<x:x>` becomes a fill (review D)
- **Decision.** A call-body `<x:x>` binds to a callee's public region (a fill) **only when its base
  `x` is not in scope** at the override — the shape that is a hard error today (D5). When the base **is**
  in scope (a document-scope or lexically-visible `x`), the `<x:x>` stays a **normal override** and is
  **never** rerouted to a region fill, *even if the callee also declares a public region named `x`*. The
  in-scope local override wins; no `RegionFillCandidate` is created for it (D5 only captures
  unresolved-base `<x:x>`). Because such an in-scope `<x:x>` is a **normal sibling override** (not a
  materialized fill), the generator treats it exactly as the pre-existing sibling idiom: the
  `DefinitionInvolvesOverride` bail stays and the template is silently un-precompiled, rendered on the
  dynamic tier (D11) — it is **never** on the region-fill native-precompilation path.
- **Rationale.** This makes D4/D5 self-consistent (candidates are exactly unresolved-base `<x:x>`) and
  **eliminates the F9 library-evolution flip**: a callee gaining a public region cannot silently convert
  an existing caller's in-scope `<x:x>` override into a fill, because that override never enters the
  candidate path. Every *genuine* region fill has an unresolved base (the region lives inside the
  callee, invisible at the call site), so the practical surface is unchanged; only the rare collision (a
  caller that has both an in-scope `x` and wants to fill a region `x`) resolves in favour of the local
  override.
- **Alternatives rejected.** "A matched public region always wins, even with an in-scope base" (the
  prior F9 wording) — reintroduces the silent flip, requires detecting the collision at every override,
  and contradicts D5's unresolved-base candidate rule. Failing the collision as an error — gratuitous;
  the local override is a well-defined meaning.
- **Grounding.** [`ParseContext.CreateDefinition`](../../../src/Heddle/Language/ParseContext.cs) (≈201–255, the `def_base` in-scope vs unresolved branch); D5.

### D9 — `@out()` and the header slot stay byte-identical (SD-C)
- **Decision.** No change to `OutExtension`, `SlotContent`, `DefinitionBaseExtension.SlotMode`, the
  `out:: Type` header grammar, `ParseDefProps`, or `HED5006`/`HED5012`–`HED5018`. The region table is
  additive metadata; empty for a single-slot template, in which case **no** region code runs.
- **Rationale.** The measurable spine of the phase (R5 byte-identity gate). The anonymous slot is the
  table's default entry precisely so the single-slot path is the pre-existing one.
- **Grounding.** SD-C; [phase-7-region-table.md](phase-7-region-table.md) (byte-identity anchor).

### D10 — The `HED5019`+ split: exactly TWO ids (review F4)
- **Decision.** Two ids, `HED5019`–`HED5020`, each an error fired only on the new region-declaration
  surface, each with a **reachable** raise site verified against source:
  - **HED5019** — a call-body override targets a region the callee declares **private**. Raised in the
    D4 fill step (`CreateExtension`) when a `RegionFillCandidate` matches an `IsRegion && !IsPublicRegion`
    region. Genuinely new — no pre-existing error covers it.
  - **HED5020** — a component declares two **public** regions with the same name. Raised by **upgrading
    the existing id-less duplicate error** at [`HeddleMainListener.EnterDef`](../../../src/Heddle/Language/HeddleMainListener.cs)
    (≈54–58) to `HED5020` **only when BOTH the already-stored entry and the incoming duplicate are public
    regions** — the exact predicate is
    `DefinitionsBlock.Definitions[name].IsPublicRegion && CurrentDefenition.IsPublicRegion`, both read at
    the duplicate-detection point (≈54); the incoming is `CurrentDefenition.IsPublicRegion`, and the
    existing is read off the stored dictionary entry keyed by the shared name. (A public region colliding
    with a private/document `<name>` fails only one side of the predicate → keeps the id-less message,
    F6.) This is the only reachable site — the name-keyed `Dictionary` never stores the second
    declaration, so a later `RegionLayout.Resolve` walk could not see the duplicate (review F4a).
  - **`HED5021` is NOT allocated** — a region-override narrowing mismatch is already caught by the
    pre-existing id-less `WalkValidateDefinitionType` (D7/F4b); minting `HED5021` would double-report.
  - No "unknown-region" id (an undeclared name keeps today's meaning, D5); no "missing-region" id
    (regions are default-backed). Message/trigger/position in [Diagnostics](#diagnostics--error-surface).
- **Rationale.** Only two conditions need a *new* reachable, positioned region id; the third
  (narrowing) reuses an existing check (F4b), and duplicate detection must live at `EnterDef` because
  the duplicate is never stored (F4a). Member errors reuse `HED0001` (D7, maintainer-confirmed).
- **Alternatives rejected.** Raising `HED5020` in `RegionLayout.Resolve` (the prior draft) — **dead
  code**: the duplicate is never stored to walk (F4a). Allocating `HED5021` — dead-on-arrival double
  report with `WalkValidateDefinitionType` (F4b). Dropping `HED5020` for the id-less error — loses the
  positioned region-specific message the plan asks for, and the upgrade is a trivial reachable change.
- **Grounding.** [diagnostic-ids](../../plan/common/diagnostic-ids.md); [`HeddleMainListener.EnterDef`](../../../src/Heddle/Language/HeddleMainListener.cs) (≈54–58); [`HeddleCompiler.WalkValidateDefinitionType`](../../../src/Heddle/Runtime/HeddleCompiler.cs) (≈1423–1451); [`HeddleDiagnosticIds`](../../../src/Heddle/Data/HeddleDiagnosticIds.cs).

## Implementation plan

Ordered; an implementer works straight down. Layer detail for WI3–WI6 is in
[phase-7-region-table.md](phase-7-region-table.md).

### WI1 — Grammar + regeneration (licensed)
- **Files.** [`src/Heddle.Language/HeddleParser.g4`](../../../src/Heddle.Language/HeddleParser.g4);
  regenerated `src/Heddle.Language/generated/HeddleParser.cs`, `HeddleParserListener.cs`,
  `HeddleParserBaseListener.cs`.
- **Change.** The D1 diff; regenerate via `generate_cs.cmd` (ANTLR 4.13.1); commit generated files
  with the `.g4` edit. `generated/HeddleLexer.cs` returns byte-identical.
- **Done when.** The grammar-stability gate shows an empty lexer diff and the exact two-rule parser
  addition; a `<:name>` / `<:name :: Type>` template parses without `HED0003`.

### WI2 — Parse model: region flags, region table, fill candidates (emit-then-retract), HED5020, guards
- **Files.** [`src/Heddle/Language/DefinitionItem.cs`](../../../src/Heddle/Language/DefinitionItem.cs)
  (add `IsRegion`, `IsPublicRegion`, `Regions`, `IsFillCandidate`; **copy the region flags in the copy
  ctor**; **do NOT add them to `OverrideWith`'s field-copy list**, review F3); new
  `src/Heddle/Language/RegionDeclaration.cs` (mirrors `PropDeclaration`); new `RegionFillCandidate`
  type + a **root-shared** `RegionFillCandidates` list on
  [`src/Heddle/Language/ParseContext.cs`](../../../src/Heddle/Language/ParseContext.cs) (initialized
  `parentContext?.RegionFillCandidates ?? new List<>()` — the same shared-up-the-chain pattern as
  `Errors` at ≈36, so it is reachable from any sub-context, review B); `CreateDefinition` region branch
  → `CreateRegionDefinition`; the `def_base` branch, on an **unresolved-base** `<x:x>`, **emits the
  base-not-found error to the shared `Errors`** (as today, ≈217–220) **and** captures a
  `RegionFillCandidate` {name, override body + its `ParseContext`, narrowing type, position, the emitted
  error object, origin = the caller-content `ParseContext`}, and returns the item flagged
  `IsFillCandidate` (D5);
  [`src/Heddle/Language/HeddleMainListener.cs`](../../../src/Heddle/Language/HeddleMainListener.cs)
  (`EnterDef` ≈42–67: when `CurrentDefenition.IsFillCandidate`, do **not** add to `DefinitionsBlock` and
  do **not** raise "Cannot create definition" (≈46–51) — the candidate is already captured; `ExitDef`
  ≈69–109 and `ExitSubtemplate` ≈182–189 skip a fill candidate but keep `CurrentDefenition` non-null so
  ≈184 is safe; the within-component `<region:region>` replace at ≈91–93 **preserves `IsRegion`/type**
  when the replaced entry was a region; the duplicate check ≈54–58 upgrades to **HED5020 only when
  BOTH the existing and incoming are public regions** — a public-vs-private/document collision keeps the
  id-less message, review F6).
- **Change.** Parse `<:name>` / `<:name :: Type>` (public regions, `IsRegion`+`IsPublicRegion`); mark
  component-body inner `<name>` defs `IsRegion` (private); record each region into the enclosing
  component's `Regions` **on the `EnterDef` store-success path only** (after the ≈54–58 duplicate check
  passes, alongside the `DefinitionsBlock` store at ≈60 — **not** eagerly in `CreateRegionDefinition` at
  ≈45), so a rejected duplicate `<:name>` never lands in `Regions` (region-table "Append ordering",
  review A3); a **`<:name>` at document
  scope** (no enclosing definition body — `InDefintionContext == false`) is a positioned **id-less**
  parse error `"A public region '<:{name}>' can only be declared inside a definition body."` (review
  F6 — no new id); capture unresolved-base `<x:x>` as a candidate (emit-then-retract, D5).
- **Done when.** `DefinitionItem.Regions` lists a component's public + private regions; a call-body
  `<heading:heading>` emits a base-not-found error (later retracted) and a `RegionFillCandidate`; a
  duplicate `<:heading>` raises HED5020; a document-scope `<:x>` errors id-less; `<:heading>` colliding
  with a private/document `<heading>` keeps the id-less duplicate message.

### WI3 — Dynamic tier: `RegionFillScope`, region-context scope, materialized fill, HED5019, retract
- **Files.**
  [`src/Heddle/Runtime/CompileContext.cs`](../../../src/Heddle/Runtime/CompileContext.cs) — add
  `internal RegionFillScope RegionFillScope { get; set; }`, **copied in the child copy ctor** (≈105–122,
  beside `ActivePropLayout`) so it inherits to every nested body-compile (D4); `ResolvedRegionLayouts`
  cache; new `src/Heddle/Runtime/Expressions/RegionLayout.cs` (+ a small `RegionFillScope` type — an
  immutable `name → DefinitionItem` map with a `WithSelfAsBase(name)` variant for D4 step 5) — resolves
  region types + lookup, **does not** detect duplicates (WI2/F4a); new
  **`src/Heddle/Language/DefinitionMaterializer.cs`** — the shared, **parse-model-pure** transform (no
  `CompileScope`/`ExType`) building the filled region as a layered `DefinitionItem` (base = region
  default, type-preserving, `IsRegion` set, `Position` = the override span, D4 step 4); consumed by both
  backends (WI4/F5); [`src/Heddle/Runtime/HeddleCompiler.cs`](../../../src/Heddle/Runtime/HeddleCompiler.cs)
  (`CreateExtension` definition branch: `RegionLayout.Resolve` + cache; the D4 route over
  `extensionItem.Context.RegionFillCandidates` matched by `origin == extensionItem.Context`; build the
  `RegionFillScope`, set it in the ≈1185–1195 save/set/restore block; the D6 layout+slot rule; on a
  private match raise **HED5019** and **retract** the candidate's error, on a public match retract and
  add the fill — the retract removes the captured error **object reference** from **both**
  `compileScope.CompileErrors` (== `CompileContext.CompileErrors`, the runtime-authoritative list the
  result reads) **and** `candidate.Origin.Errors` (the parse list the LSP union reference-dedups over);
  a `ParseContext.Errors`-only removal is inert because `CopyErrorsTo` already copied the object by
  reference into `CompileErrors` before compile (D5, Assumed state phase-split seam); D5; the D4 step-5
  self-reference variant while compiling a fill body; `CompileItem` ≈744–746 consults `RegionFillScope`
  before the parse-context lookup).
- **Change.** As D3/D4/D5/D6/D7/D10. The propagation is the `ActivePropLayout` seam (D4) — **no**
  `IsolateContext` copy for the fill. HED5019 at the fill step (+ retract); narrowing reuses
  `WalkValidateDefinitionType` (D7, accepted double-report). The materializer is the single source of
  the filled `DefinitionItem` both backends emit.
- **Done when.** `region_feed_full` renders the fill **including `@item(this)` at depth in the
  `Articles` loop**; `region_selfcall_to_default` renders the base default (no recursion);
  `region_feed_two_calls` renders the default on the un-filled second call; a dangling `<x:x>` keeps its
  base-not-found error; the single-slot differential corpus is byte-identical.

### WI4 — Generator: context-aware resolution + fill-scope threading + native materialization (gate)
This is **new, risk-bearing generator work** (OQ1 / review F1/A): it changes which templates
precompile — after it, **inner-definition calls** (region defaults and region fills) are precompiled
where today they silently fall to the dynamic tier. The plain sibling-override idiom is **not** part of
this lift (D11) — it keeps its pre-existing silent degrade.
- **Files.**
  [`src/Heddle.Generator/Emit/TemplateEmitter.cs`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs)
  — thread the enclosing body's `ParseContext` **and a `RegionFillScope`** through
  `BuildBody`/`PopulateBody`/`BuildCall` (the recursion at ≈516/545/567 already carries `bctx`; add the
  fill scope to it) so a call resolves against the enclosing definition scope **and** the ambient fill
  scope, not only `_parse` (≈493–494); lift `BuildDefinitionCall`'s
  `DefinitionInvolvesOverride`→`return null` bail (≈688) to materialize-then-emit **only for a
  materialized region fill carried in the `RegionFillScope`** (a plain sibling override resolved via flat
  `_parse` keeps the bail and stays silently un-precompiled, D11); extend the
  `GetOrBuildDefinitionBody` key (≈835) for **per-run dedup**; region/override body borrows the enclosing
  component's `PropLayoutInfo` (D6 twin); consumes the WI3 `DefinitionMaterializer` (linked via
  `..\Heddle\Language\**\*.cs`, [`Heddle.Generator.csproj`](../../../src/Heddle.Generator/Heddle.Generator.csproj) ≈49–52).
- **Change.**
  - **Context-aware + fill-scope resolution (F1/A).** `BuildCall` resolves a name against the current
    `RegionFillScope` first, then the enclosing `ParseContext`, then `_parse`. The fill scope threads
    through the nested body builds exactly as the dynamic tier's `RegionFillScope` inherits via the
    `CompileContext` copy ctor — so `@item(this)` in the `Articles` loop resolves to the fill **at
    depth**, and a self-call resolves to the base default (D4 step 5). Since feed's body class is
    digest-keyed per fill set, the fill scope bakes into that specific class.
  - **Negative-fill routing (review C).** If a call-site candidate matches a **private** region (HED5019)
    or matches **no** region (dangling base-not-found), the template does **not** precompile — the
    emitter returns a `reason` so the template is **silently un-precompiled** and the **dynamic tier
    raises the error** (never precompile a body whose dynamic compile errors — the gauntlet would
    otherwise short-circuit the dynamic compile and hide the error).
  - **Materialize / per-run dedup / no `RegionLayoutInfo`** — as before (F5/F8): materialization shared,
    emission parallel (differential-gated); the key digest disambiguates a filled body from the default
    within one run (no cross-build cache; the generator *does* compose `@<<` multi-file via `ImportReader`,
    [`HeddleTemplateGenerator`](../../../src/Heddle.Generator/HeddleTemplateGenerator.cs) ≈272–279, but
    into one per-run parse/emit, so the per-run dedup key still suffices — only `ImportOrigin` stamping is
    `ProvideLanguageFeatures`-gated, review F8).
- **Implementation gate (measurable pass/fail + pre-decided branch).** **Pass:** `region_feed_defaults`,
  a bare inner-def-call fixture, `region_feed_full` (incl. the depth `@item(this)`), `region_feed_two_calls`,
  and `region_selfcall_to_default` **precompile** (no un-precompile `reason`) and render byte-identically
  dynamic == precompiled. **Pre-decided branch:** (a) a region-**error** template
  (`region_private_override`) is **not** precompiled → the dynamic tier raises HED5019/the base-not-found
  error; (b) any sub-shape reaching the generator's *pre-existing* limit — an
  **unresolvable/abstract/`:: dynamic` model type** (`DefinitionBodyContext` reason, ≈879–882) — is left
  **silently un-precompiled** and rendered byte-identically by the dynamic tier (D8); (c) the plain
  sibling-override idiom (`patterns_sibling_shell`, and the self-calling `region_sibling_selfcall`) is
  **not** precompiled — the `DefinitionInvolvesOverride` bail stays (D11) — and is rendered
  byte-identically by the dynamic tier (which terminates a self-calling sibling at its base). Never a new
  diagnostic, never a wrong render, **never recursive precompiled code** for a self-calling override.
- **Done when.** The gate's Pass fixtures precompile natively and match the dynamic tier byte-for-byte;
  `region_private_override`, `region_abstract_model`, `patterns_sibling_shell`, and
  `region_sibling_selfcall` are silently un-precompiled and correct on the dynamic tier; `region_ctx_shadow`
  (F7 convergence) precompiles to the dynamic-tier bytes.

### WI5 — LSP: region projection (diagnostics already transitive)
- **Files.** [`src/Heddle.LanguageServices/DefinitionInfo.cs`](../../../src/Heddle.LanguageServices/DefinitionInfo.cs)
  (add `Regions`); [`DocumentAnalyzer.ProjectDefinitions`](../../../src/Heddle.LanguageServices/DocumentAnalyzer.cs)
  (populate from `DefinitionItem.Regions`); the completion/hover providers under
  [`Completion/`](../../../src/Heddle.LanguageServices/Completion/) (offer a callee's public region
  names at a call-body override position; hover shows region visibility/type).
- **Change.** Additive projection; no compile change (HED5019/HED5020 already surface via
  `HeddleCompiler.Compile`).
- **Done when.** Completion offers `heading`/`item` (not `divider`) at a `@feed(){{ @% <│ %@ }}`
  position; HED5019/HED5020 appear as editor diagnostics.

### WI6 — Diagnostics registry + docs
- **Files.** [`src/Heddle/Data/HeddleDiagnosticIds.cs`](../../../src/Heddle/Data/HeddleDiagnosticIds.cs)
  (add **two** consts: `RegionNotPublic = "HED5019"`, `DuplicateRegionDeclaration = "HED5020"`);
  [`src/Heddle.Tests/DiagnosticIdTests.cs`](../../../src/Heddle.Tests/DiagnosticIdTests.cs)
  (add `HED5019`/`HED5020` to the expected set); the registry + X3 reconciliation is done by the
  coordinator (this spec does **not** edit `common/cross-cutting.md` or the registry); the public-API
  golden ([`public-api-heddle.txt`](../../../src/Heddle.Tests/TestTemplate/public-api-heddle.txt)) for
  the two new public consts + `DefinitionInfo.Regions`; user docs
  ([language-reference](../../language-reference.md), [patterns](../../patterns.md)) gain the named-
  region section and keep the sibling-idiom recipe.
- **Done when.** `DiagnosticIdTests` passes; the API golden matches; docs build.

## Public API / contract

Additive only; no existing public signature changes.

- **`HeddleDiagnosticIds`** ([`src/Heddle/Data/HeddleDiagnosticIds.cs`](../../../src/Heddle/Data/HeddleDiagnosticIds.cs)) — **two** new `public const string`:
  - `RegionNotPublic = "HED5019"`
  - `DuplicateRegionDeclaration = "HED5020"`
- **`DefinitionInfo.Regions`** ([`src/Heddle.LanguageServices/DefinitionInfo.cs`](../../../src/Heddle.LanguageServices/DefinitionInfo.cs)) — new `public IReadOnlyList<RegionInfo> Regions { get; }`; new public `RegionInfo` (`string Name`, `bool IsPublic`, `string TypeName`, `ExType Type`, declaration offset/length), mirroring `PropInfo`. LSP-only assembly; consumed by tooling.
- **Language surface** — the `<:name>` / `<:name :: Type>` declaration and the call-body
  `<name:name>` region fill. Additive to templates; no host-code API.
- **Internal (not public):** `DefinitionItem.IsRegion`/`IsPublicRegion`/`Regions`/`IsFillCandidate`,
  `RegionDeclaration`, `RegionFillCandidate`, `RegionLayout`/`RegionSlot`, `RegionFillScope`,
  `DefinitionMaterializer`, `ParseContext.RegionFillCandidates`,
  `CompileContext.RegionFillScope`/`ResolvedRegionLayouts`. `DefinitionBaseExtension` stays internal; no
  new `PrecompiledRuntime` entry point.
- **Thread-safety.** No per-render mutable state is added to any extension instance. Region fills are
  resolved at **compile** into per-call-site compiled bodies (a per-call `RegionFillScope` on the
  call's `CompileContext`); render reads
  only the immutable compiled document and `Scope` lineage — the same posture as `PropsBinder`'s
  frozen prototype and `SlotContent`'s per-lineage carrier (coding-standards: state in `Scope`
  lineage, not on extensions).

## Diagnostics / error surface

**Two** compile **errors** (D1 registry severity), each with a reachable raise site (review F4), fired
**only on the new region-declaration surface** (a callee that declares regions), and positioned. The
coordinator reconciles the registry + X3 (this spec does not edit them).

| ID | Message | Trigger | Position |
|---|---|---|---|
| `HED5019` | `Region '{name}' of definition '{callee}' is private and cannot be overridden from a call site. Mark it public with '<:{name}>' in the definition, or remove this override.` | A call-body region-fill candidate matches a region the callee declares **private** (`IsRegion && !IsPublicRegion`) — raised in the D4 fill step (`HeddleCompiler.CreateExtension`). | The override declaration (`<divider:divider>`) in the caller content. |
| `HED5020` | `Definition '{callee}' declares more than one public region named '{name}'.` | A second **public** region (`<:name>`) whose name is already declared by **another public region** in the same component — raised by upgrading the id-less duplicate error at [`HeddleMainListener.EnterDef`](../../../src/Heddle/Language/HeddleMainListener.cs) (≈54–58) **only when both the existing and incoming are public regions** (the only reachable site — the duplicate is never stored, review F4a/F6). A public region colliding with a **private** or **document-scope** `<name>` keeps the id-less "definition already exists" message (only one public region exists → the HED5020 message would be wrong, F6). | The **second** `<:name>` declaration. |

Note on scope: HED5019 fires when a caller overrides a callee's declared **private** region (no public
region need exist); HED5020 fires on a duplicate **public** region declaration. Both fire only on the
new region-declaration surface — never on a template valid today (review F9).

Reused, not new (D5/D7 — no new id): a typed region override body reading a member not on the region's
type → `HED0001` (`PropertyNotFound`) / `HED1xxx` at the read site; a region-override **narrowing**
mismatch → the pre-existing id-less `"The new definition type <X> isn't assignable to base <Y>."`
(`WalkValidateDefinitionType`, D7/F4b — fires **twice**, accepted per D7/review E); a genuinely dangling
`<x:x>` matching no region → the pre-existing `"Base definition {x} couldn't be found"` — emitted at
parse and **not retracted** (D5 emit-then-retract; retracted only for a matched fill), so its text and
position are byte-identical to today; a **document-scope `<:name>`** → the id-less `"A public region
'<:{name}>' can only be declared inside a definition body."` (WI2/F6).

## Testing plan

**TDD verdict: yes.** Author the `.heddle` fixtures, LF-pinned goldens, and positioned negative
diagnostic tests first (red), then implement WI1–WI6 to green. Fixtures live under
`src/Heddle.Tests/TestTemplate/` beside the existing corpus; goldens are LF-pinned and asserted
byte-exact by the harness the repo already uses (`HeddleTemplateTests` byte compare).

Success/validation rows → fixtures (each a `.heddle` + `.golden`, or a diagnostic assertion):

| Fixture | Asserts (Plan success criterion / validation row) |
|---|---|
| `region_feed_full.heddle` | The ratified worked example: `heading` renders the override in region context (`@(theme)`/`@(title)` = feed props → `<h2 class="hero">Latest</h2>`); **each `Articles` item renders the typed override at depth** (`item :: Article`, `@(Title)`/`@(Id)` — the `@item(this)` call is nested in the `@list(Articles){{…}}` loop, so this fixture is the **BLOCKER-A depth trap**: it fails if the fill does not propagate below the top of the call body); `divider` renders its private default `<hr class="dark">`; the loose `<p class="lede">` renders through `@out()` in **caller** context. |
| `region_fill_at_depth.heddle` | A minimal isolate of the depth case: a region call nested inside `@if(x){{ @heading() }}` in the component body, filled at the call site → renders the **fill**, proving `RegionFillScope` reaches nested contexts (D4). Guards against a top-level-only install regressing silently. |
| `region_feed_defaults.heddle` | No override block → all defaults (`<h2 class="light">Home</h2>`, `<li>@(Title)</li>` per article, `<hr class="light">`) — regions are call-scoped, not required. |
| `region_feed_two_calls.heddle` | Two `@feed(...)` calls; the first overrides `heading`, the second (no override) renders `heading`'s **default** — the fill does not leak (call-scoped `RegionFillScope`). |
| `region_private_override.heddle` | `<divider:divider>` at a call → **HED5019** at the override position (positioned); the candidate's base-not-found error is retracted (single error). |
| `region_undeclared_override.heddle` | `<masthead:masthead>` where feed declares no `masthead` region **and** `masthead` is a document-scope definition (base **in scope**, D12) → a **normal local override**, no region routing, no region diagnostic; byte-identical to the pre-Phase-7 output (additivity). |
| `region_dangling_override.heddle` | `<ghost:ghost>` in a call body where `ghost` is neither a callee region nor in scope → the base-not-found error is emitted at parse and **not retracted** (dangling) — byte-identical to today's error text/position (D5 emit-then-retract additivity). |
| `region_duplicate.heddle` | Two `<:heading>` public regions in one component → **HED5020** at the second declaration (positioned). |
| `region_public_vs_private.heddle` | `<:heading>` colliding with a private/document `<heading>` (only one public) → the id-less "definition already exists" message, **not** HED5020 (F6). |
| `region_docscope_public.heddle` | `<:x>` declared at document scope (no enclosing definition) → the id-less "A public region … can only be declared inside a definition body." (F6). |
| `region_typed_badmember.heddle` | `<item:item>` override body reads a non-`Article` member → **HED0001** at the read site (typed against `Article`, D7). |
| `region_typed_narrow.heddle` | `<item:item>{{…}} :: NotArticle` (narrowing) where `NotArticle` is not assignable to `Article` → the pre-existing id-less `"The new definition type <NotArticle> isn't assignable to base <Article>."` (`WalkValidateDefinitionType`), positioned at the override — asserted **present and positioned**, fired the pre-existing count (twice, D7/review E), **not** "a single error". |
| `region_selfcall_to_default.heddle` | A `<heading:heading>` fill whose override body itself calls `@heading()` → resolves to the region's **base default** (not the fill) — **no infinite recursion** (D4 step 5 self-reference); identical dynamic == precompiled. |
| `region_sibling_from_fill.heddle` | A fill body calling a **sibling** region (`@divider()` inside a `<heading:heading>` fill) → resolves the sibling's scope entry (its default or fill) — "exactly like the default body it replaces" (D6/R7). |
| `region_props_compose.heddle` | A component with both typed props and named regions type-checks and renders; props and regions independent. |
| `patterns_sibling_shell.heddle` | The patterns.md sibling-override sample compiles and renders **unchanged** (regression); it remains **silently un-precompiled** on the generator (the pre-existing sibling-override degrade, D11) and is rendered byte-identically by the dynamic tier. |
| `region_sibling_selfcall.heddle` | A **self-calling** plain sibling override `<x:x>{{ … @x() … }}` (base `x` in scope, so a normal override, not a fill — D12) → the dynamic tier resolves the self-call to the **base** `x` and terminates; the generator **does not precompile** it (the `DefinitionInvolvesOverride` bail stays, D11) so **no recursive precompiled code is emitted**; rendered byte-identically by the dynamic tier. Guards the D11 boundary the flagship `patterns_sibling_shell` (no self-call) cannot exercise. |
| `region_ctx_shadow.heddle` | A nested inner definition shadowing a same-named extension/function it also calls → after F1 the generator emits the **definition** (converging to the dynamic tier), a **deliberate byte change** (D8/F7); golden pinned to the dynamic-tier bytes. |
| `region_abstract_model.heddle` | A component whose region model type is abstract/unresolvable at build (`:: dynamic`) → **silently** left un-precompiled (the generator's pre-existing limit, D8/F2 — **no diagnostic**) and rendered byte-identically by the dynamic tier. |

**Byte-identity corpus (hard gate).** Every existing single-slot / `@out()` fixture in the
differential corpus renders byte-identically under **both** the dynamic and precompiled backends after
the change; the dynamic == precompiled differential does not move. Per **OQ1** the differential
asserts **native** precompiled parity on overridden **region fills** — parity is differential-**gated**
(shared materialization + parallel emission, F5), not automatic:
- region-**default** templates (`region_feed_defaults`, `region_props_compose`) and a bare
  inner-definition-call fixture **precompile natively** (no un-precompile `reason`) and match the
  dynamic tier byte-for-byte — this is the F1 win (inner-definition calls precompile);
- region-**fill** templates (`region_feed_full` — **including the `@item(this)` fill at depth**,
  `region_fill_at_depth`, `region_feed_two_calls`, `region_selfcall_to_default`, `region_sibling_from_fill`)
  **precompile natively** and render byte-identically to the dynamic tier. **The depth fixtures are the
  BLOCKER-A guard:** because both tiers would miss a non-propagating fill *identically*, the differential
  alone cannot catch it — the depth fixtures assert against a **golden** (the filled bytes at depth), so a
  top-level-only install fails the golden even while the differential passes;
- the **plain sibling-override** samples `patterns_sibling_shell` and the self-calling
  `region_sibling_selfcall` (D11) are **not** precompiled (the `DefinitionInvolvesOverride` bail stays)
  and are rendered byte-identically by the dynamic tier — proving **no recursive precompiled code** is
  emitted for a self-calling override, and that the sibling idiom's rendered bytes are unchanged from
  today;
- **erroring region templates under the precompiled backend** (`region_private_override`,
  `region_dangling_override`) are **not** precompiled (review C) → the dynamic tier raises the error
  (HED5019 / base-not-found); the fixture asserts the error surfaces under the precompiled backend, not
  a silently-successful precompiled render;
- `region_abstract_model.heddle` is **silently** left un-precompiled (no diagnostic, D8/F2) and renders
  byte-identically on the dynamic tier — the honest, source-grounded escape (the generator's
  pre-existing unresolvable/abstract-model-type limit, never override- or self-call-triggered).

**Concurrency.** Render a region-declaring template (defaults and a fill) concurrently on many
threads and assert identical output and no shared-state corruption — confirming region state is
compile-time/`Scope`-lineage only, never per-render mutable state on an extension instance
(coding-standards). No new reserved `Scope` channel key is introduced.

**Regression gate (all must stay green).** grammar-stability (licensed regen: empty lexer diff, exact
parser diff); the byte-identity differential corpus under **both** backends; `DiagnosticIdTests`
one-to-one (updated expected set); `PublicApiSurfaceTests` golden (new consts + `DefinitionInfo`
surface); all TFMs incl. **netstandard2.0**; docs build.

## Back-compat and migration

No breaking change; no breaking window consumed (additive under R5).

- **Rendered output — proven byte-identical.** A definition with no named regions has an empty region
  table; `RegionLayout.Count == 0` and no region code runs (D3/D9), so every single-slot / `@out()`
  path is the pre-existing one. Proven by the byte-identity differential corpus under both backends
  not moving (Testing plan).
- **`@out()` / header slot / props — unchanged** (D9). `OutExtension`, `SlotContent`, `SlotMode`,
  `ParseDefProps`, and `HED5006`/`HED5012`–`HED5018` are untouched.
- **New surface is inert on old templates.** `<:name>` matches no production today (a `HED0003`
  syntax error), so no existing template uses it. A `<x:x>` override is routed as a region fill **only
  when its base is unresolved** (D4/D5/D12) — a shape invalid in every template today — so no valid
  template's diagnostics or resolution change; the emit-then-retract of the base-not-found error (D5)
  only removes it for a genuine fill.
- **Local override wins — no library-evolution flip (D12, review D).** A `<x:x>` whose base **is** in
  scope (a document-scope or lexically-visible definition of that name) stays a **normal override** and
  is never rerouted to a region fill — even if the callee also declares a public region of that name.
  So a callee **gaining** a public region cannot silently flip an existing caller's local `<x:x>`
  override into a fill (the flip the prior draft described is **removed**). The only region fills are
  `<x:x>` with an *unresolved* base, which resolve to nothing today. Precedence decision recorded in
  D12; edge (a caller intending to fill a region while also having a same-named in-scope definition)
  covered by a boundary fixture.
- **New diagnostics are additive.** `HED5019`/`HED5020` fire only on the new region-declaration
  surface; no template valid today acquires a new error or warning.
- **Sibling-override idiom retained, unchanged on both backends** — still compiles and renders
  identically (regression fixture). Its generator treatment is **unchanged**: the
  `DefinitionInvolvesOverride` bail stays, so it remains *silently* un-precompiled and rendered on the
  dynamic tier exactly as today (D11). OQ1's native lift is bounded to region **fills**; extending it to
  the sibling idiom is out of scope and unsafe for the self-calling case (would emit recursive
  precompiled code — D11), so no backend change and no golden moves for siblings.
- **Generator/dynamic convergence — a bounded, deliberate byte change (D8/review F7).** Context-aware
  resolution (D8) makes the generator honor the dynamic tier's definition-first precedence **at any
  depth**. For a template where a **nested inner definition shadows a same-named extension/function it
  also calls** — where the old generator emitted the extension (`_parse`-only resolution missed the
  inner def, `TemplateEmitter.cs` ≈505–526) while the dynamic tier rendered the definition — the
  precompiled bytes now **converge to the dynamic tier's**. This is a correctness fix (it removes a
  latent generator/dynamic divergence), but it is a *deliberate byte change* the blanket "no golden
  moves" claim does not cover. A pinning fixture (`region_ctx_shadow`) asserts the convergence and its
  golden is set deliberately.
- **Docs** gain the named-region section and keep the sibling-idiom recipe (WI6).

## Performance considerations

Zero render-path cost. All region work is **compile-time**: region-table resolution (cached like
`PropLayout`), fill materialization (the `DefinitionMaterializer` builds a small per-call
`RegionFillScope` map consulted at resolution — no per-call context deep-copy for the fill), and
region-context prop resolution all produce a single execution-ready compiled document. Render reads the
immutable document and `Scope` lineage — **no per-render
allocation** is added and no hot-path branch is introduced for single-slot templates (the empty-table
fast path is the existing path). Region fills **precompile natively** (OQ1): the materialized
call-site body is emitted as generated code, so a filled region on the precompiled backend pays the
precompiled tier's cost (no dynamic-compile-at-render). The fall-through to the dynamic tier is limited to
the generator's pre-existing, non-region-specific bails (D8) — the unresolvable/abstract-model-type
limit, or any other bail a resolved definition can already hit today (a host that is itself a sibling
override, an unreproducible prop value); region-fill override-presence and region-fill self-calls are
never themselves the trigger. The build-time materialization (shared transform + per-run body class) adds generator work but
**zero render-path cost**; distinct fills produce distinct body classes (per-run dedup key), and
identical/no-override calls share the default body (no code-size regression for single-slot templates).
The guarding benchmarks are the existing render/allocation suites plus the byte-identity differential;
a single-slot template's allocation profile must not move (asserted by the unchanged corpus).

## Standards compliance

- **DRY (warranted).** The region table mirrors `PropLayout` because there are two live consumers —
  the dynamic tier (fill + diagnostics) and the LSP projection — and Phase 8 is a second, parallel
  consumer of the same "shared table across four layers" precedent; the abstraction is earned, not
  speculative (coding-standards balanced mode).
- **YAGNI (cuts).** No generator `RegionLayoutInfo` and no precompiled `BindRegion` — the generator
  materializes fills through the **shared** `DefinitionMaterializer` and emits them via the existing
  `GetOrBuildDefinitionBody`/`BindDefinition`, borrowing the component's prop carriage; regions carry
  no own prop list, no base, no default chain; **two** region diagnostics, not three (narrowing reuses
  `WalkValidateDefinitionType`, member errors reuse `HED0001`, F4/D7); no self-call escape hatch
  (context-aware resolution renders self-calls natively, F1/D8).
- **SOLID / DRY.** Region work is added at the seam that already owns definition compilation
  (`CreateExtension`), reusing `IsolateContext` and the definition-override machinery (open/closed).
  Only **materialization** is factored **once** into the parse-model-pure `DefinitionMaterializer` and
  consumed by both backends; body **emission** stays each backend's own (the generator's symbol-based
  writers), so backend byte-identity on fills is **differential-gated**, not "by construction" (review
  F5).
- **Thread-safety / hot-path.** No per-render state on extension instances; state lives in the
  compiled document and `Scope` lineage; single-slot templates keep their exact zero-region code path
  (coding-standards no-per-render-alloc, netstandard2.0).

## Deferred items

| Item | Trigger |
|---|---|
| Native precompilation of a region/component whose **model type is abstract/unresolvable at build** (`:: dynamic` or unresolved) — the generator's pre-existing limit; such templates are silently un-precompiled and render on the dynamic tier (D8/F2). Not override- or self-call-specific. | The generator gaining build-time resolution of dynamic/abstract model shapes (a broad generator capability, well beyond regions). |
| Native precompilation of the **plain sibling-override idiom** (`<name:name>` overriding a document-scope sibling). Bounded out by OQ1 (region fills only) and unsafe without generator-side self-call resolution: on the flat `_parse` path a self-calling sibling has no self-call→base rebind, so lifting the `DefinitionInvolvesOverride` bail would emit recursive code (D11). It stays on the pre-existing silent degrade (rendered correctly on the dynamic tier). | The generator gaining **document-order layering** (an override body's by-name self-call resolving to its base layer) — the follow-up the bail's own source comment reserves ([`TemplateEmitter.cs`](../../../src/Heddle.Generator/Emit/TemplateEmitter.cs) ≈686–687). |
| Region **props** / **base**-inheritance / **default output chain** on the region header. | A ratified need; the grammar widening is compatible (D1 leaves room). |
| Extension regions / channel-threading / provide-inject. | Out of scope by R7; a new ratified model would reopen it (extensions get *parameters* in Phase 8, not regions). |
| A dedicated region-body member-error diagnostic (vs reused `HED0001`). | Maintainer-confirmed to reuse `HED0001` (escalation #1); revisit only if message quality demands a region-specific id. |
| A warning when a caller both region-fills and locally calls the same name (the F9 library-evolution flip). | The silent behavioral flip biting in practice (Back-compat). |

## External references

- [Vue — Slots (named & scoped)](https://vuejs.org/guide/components/slots.html) — precedent for named/
  scoped content regions (child owns data, caller owns presentation).
- [Blazor — Templated components (`RenderFragment`)](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components) — precedent for multiple typed content parameters.
- [MDN — the `<slot>` element](https://developer.mozilla.org/en-US/docs/Web/HTML/Element/slot) —
  platform precedent for named content regions.
- [ANTLR 4 — parser rules & ALL(\*)](https://github.com/antlr/antlr4/blob/master/doc/parser-rules.md) —
  the single-token-lookahead disambiguation the region alternative relies on.
- Repo primary sources cited inline (`HeddleParser.g4`, `HeddleCompiler.cs`, `PropLayout.cs`,
  `OutExtension.cs`, `DefinitionBaseExtension.cs`, `PrecompiledRuntime.cs`, `TemplateEmitter.cs`,
  `DocumentAnalyzer.cs`, `HeddleDiagnosticIds.cs`, `DiagnosticIdTests.cs`, `patterns.md`,
  `language-reference.md`).
