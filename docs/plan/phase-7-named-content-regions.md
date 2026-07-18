# Phase 7 — named-content-regions

## Header

- **Status:** ready — Plan DoR
- **Goal (one line):** Additively let a component expose more than one overridable content region — declared as inner definitions marked public with a leading colon (`<:name>`) and filled at the call site with the normal `<name:name>` override syntax — while `@out()` and today's single slot stay byte-identical.
- **Depends on:** nothing structurally. Sequenced **last by policy** ([R1](common/standing-rulings.md#standing-rulings-user-set)), deliberately after the bounded phases (1–6); the ratified model itself is [R7](common/standing-rulings.md#standing-rulings-user-set). Builds on the already-stable props / single-slot machinery — the shared [`PropLayout`](../../src/Heddle/Runtime/Expressions/PropLayout.cs) table is the precedent it mirrors, not a dependency it introduces.
- **Changes an externally-visible contract:** yes, additively only — (a) a new **inner-definition marker** for a **public** region (`<:name>`, optionally typed `<:name :: Type>`) — a genuinely new declaration surface no existing template uses; (b) the existing `<name:name>` override, when written inside a call body, **binds to a public region of the callee only when its name matches a public region that callee declares**, and is then **call-scoped** — an override whose name matches no public region keeps today's local-definition-override meaning byte-for-byte; (c) a reserved **`HED5019`+** block of compile-error diagnostics that fire **only** on the new public-region surface (a callee that declares `<:name>` regions). `@out()`, the single header slot (`out:: Type`), props, and every existing template's rendered bytes are unchanged.

## Goal

Today a Heddle definition exposes exactly **one** content region: the single anonymous `@out()` slot, which splices the caller's `{{ … }}` body ([Parameterized slots](../language-reference.md#parameterized-slots-out-type)). A component that needs several independently-fillable regions — a header, a body, a footer — has no first-class way to say so. The sanctioned workaround is the **sibling-definition-override idiom**: expose each extra region as its own definition with default content, and let the caller replace it with a `<name:name>` override ([patterns → components with multiple content regions](../patterns.md#components-with-multiple-content-regions)). It works and stays typed, but it is more ceremony than the component-framework norm, and the assessment names it "the most visible expressiveness gap versus the component-framework state of the art" ([§3.4](../language-assessment.md)).

This phase closes that gap **additively**. A component declares its overridable content regions as **inner definitions marked public with a leading colon** — `<:name>` is public (overridable by a caller), plain `<name>` is private (the default). A region may be typed and scoped like any definition (`<:item :: Article>`). The component renders a region by *calling* it (`@heading()`), and projects a value into a typed region the same way `@out(expr)` projects into the slot (`@item(this)`). A caller fills or overrides a public region using the **normal override syntax it already knows** — an ordinary `<name:name>` override inside a `@%…%@` block in the call body — with that override applying only to that one call. Nothing new is invented for the fill mechanism; the feature is the leading-colon public/private marking plus binding the existing override to a specific call. The user-visible outcome is that multi-region components stop being an idiom and become a declared, type-checked contract, at parity with Vue named/scoped slots and Blazor's multiple `RenderFragment` parameters — while every existing single-slot / `@out()` template renders byte-identically under both the dynamic and precompiled backends.

## Non-goals / scope boundary

- **No change to `@out()`.** The anonymous default slot is untouched: `@out()` renders the caller's loose body in the **caller's** context, and `@out(expr)` projects a typed value into it, exactly as today. Full back-compat is the spine of this phase, not a side effect.
- **No change to props.** Typed named props ([Props](../language-reference.md#props-nameprop-type--default)) are unchanged; regions **compose** with props, they do not alter them.
- **The sibling-override idiom is not removed.** It stays a valid, documented pattern ([patterns](../patterns.md#components-with-multiple-content-regions)); named regions are the ergonomic upgrade, not a replacement that breaks existing templates.
- **No new fill/slot directive.** Filling reuses the existing `<name:name>` [override](../language-reference.md#inheritance-and-override-childbase) mechanism wholesale — no `@fill`, no `<template #name>` analogue, no second way to splice content.
- **Content regions are a definition-side concept only.** They live in a definition's template body; a custom C# extension has no template body to host regions, so extensions do **not** get regions in this plan. (The separately-scoped [Phase 8](phase-8-extension-parameters.md) gives extensions named *parameters*, not regions — an unrelated feature.)
- **No channel-threading / provide-inject semantics.** A region is filled at the call that renders it; content does not thread outward through intermediate wrappers. (The broader "slots on all extensions with outward passthrough" model debated earlier resolved *away* from extension regions entirely — regions are definition-only.)
- **Phases 1–6 and 8** are unrelated increments and are not touched here.

This phase is fully self-contained: the region model is definition-side and needs no follow-on to be complete.

## Design direction

**Chosen approach (ratified — [R7](common/standing-rulings.md#standing-rulings-user-set)): public overridable regions declared as inner definitions with a leading-colon marker, filled by the existing override syntax bound to a single call.** The model was settled through a long design debate with the user; this section writes it up, it does not reopen it.

Four decisions define the model, each chosen because it *reuses* an existing, proven Heddle mechanism rather than adding a parallel one:

- **Regions are inner definitions.** A region *is* a definition, so it inherits the whole definition machinery for free: it can be typed (`:: Article`) or left [abstract / late-bound](../language-reference.md#abstract-definitions-and-late-type-binding), it composes with props, it can recurse, and it is rendered by being *called* (`@heading()`, `@item(this)`). Projecting a value into a typed region is exactly the `@out(expr)` shape already defined for the slot ([Parameterized slots](../language-reference.md#parameterized-slots-out-type)). No new concept of "region" is introduced beyond a visibility bit on a definition.

- **Public vs private is a single leading-colon marker.** `<:name>` is public (overridable from a call site); plain `<name>` is private (the safe default — the component's own internal helper, e.g. `<divider>`). This makes the overridable surface an **explicit, opt-in contract**: a component author decides exactly which regions callers may replace, and everything else stays encapsulated. The marker sits unambiguously by position — nothing precedes the colon in `<:name>`, whereas the existing inheritance/override form `<child:base>` always has a name before the colon — so it extends the definition-header grammar without colliding with `<child:base>` or plain `<name>`.

- **Filling reuses the existing override, bound to one call — additively.** Inside a call body, an ordinary `<name:name>` override in a `@%…%@` block **binds to the callee's matching public region when — and only when — its name matches a public region that callee declares** (e.g. `<heading:heading>{{ … }}` against a callee that declares `<:heading>`). This is the same override authors already use for the sibling idiom. The new rules apply only on the new public-region surface: a **matched public region** makes the override **call-scoped** (it applies to that one call, not document-onward); overriding a **private-but-declared** region is a positioned compile error (`HED5019`+); and an override whose name matches **no** declared region of the callee **keeps its current meaning** — a locally-scoped definition override in the call body, byte-identical to today. That last rule is precisely why the change is additive: since public regions are a new declaration surface, every existing callee declares none, so every existing call-body override is untouched. Reusing the override wholesale means the feature inherits its composition-without-coupling story, its static type-checking, and its zero-render-cost compilation ([§3.4](../language-assessment.md)) with no new fill semantics to specify.

- **Override-body scope = the default body it replaces.** An override runs in the **region's own context** — the component's props plus the model the region is rendered with (`@item(this)` → the `Article`) — exactly like the default body it stands in for. There is no new scoping rule: `@(title)`/`@(theme)` inside an overridden `heading` still resolve to the component's props, because the override *is* the region's body for that call. This is the one point authors coming from caller-context slots must internalize, and it falls straight out of "an override body has the same scope as the body it overrides."

Why this beats the alternatives weighed in the debate: a **dedicated fill directive** (a `@fill`/`<template #name>` analogue) was rejected as redundant surface — the override already does exactly this. **Multiple anonymous `out::` slots** (relaxing the one-slot rule into N positional slots) was rejected because positional/anonymous multi-slots read worse and do not compose with override/inheritance/narrowing the way named definitions do. **Extension-declared regions** were considered and dropped — a region needs a template body to live in, which a C# extension does not have; that debate resolved into keeping regions definition-only (extensions instead get named *parameters* in the unrelated [Phase 8](phase-8-extension-parameters.md)). And `@out()` was kept as the anonymous default slot precisely so the model is purely additive.

**Why this phase is large: one value threaded through four layers that each assume singularity.** A read-only audit confirms that "the slot" is today a single value tracked independently in four places, every one of which is written for exactly one region:

1. the **dynamic runtime compiler** and its slot primitives — [`OutExtension`](../../src/Heddle/Extensions/OutExtension.cs), [`SlotContent`](../../src/Heddle/Core/SlotContent.cs), [`DefinitionBaseExtension`](../../src/Heddle/Core/DefinitionBaseExtension.cs), and [`HeddleCompiler`](../../src/Heddle/Runtime/HeddleCompiler.cs);
2. the source **generator** [`Heddle.Generator`](../../src/Heddle.Generator/Emit/TemplateEmitter.cs), which carries its own parallel slot tracking;
3. the **LSP** [`Heddle.LanguageServices`](../../src/Heddle.LanguageServices) (completion, hover, diagnostics);
4. the **precompiled binding surface** [`PrecompiledRuntime`](../../src/Heddle/Precompiled/PrecompiledRuntime.cs).

Props already solved this exact "one shared table threaded through all four layers" problem with [`PropLayout`](../../src/Heddle/Runtime/Expressions/PropLayout.cs) — that is the precedent this phase mirrors for a named-region table. The rollout is **all four layers at once** (user-chosen; no degrade-to-dynamic path), so the dynamic and precompiled backends stay at parity from day one. This cross-cutting reach — not conceptual novelty — is why the phase is the plan's one non-quick-win. Notably, the region surface is **distinct from** the header slot and does not disturb it: a region is a leading-colon marker on an **inner definition** (`<:name>`), so it extends the definition-header grammar that already carries `<child:base>` inheritance and plain `<name>` — whereas the single typed slot is declared in the definition's **header prop list** (`out:: Type`) and policed there by the header walk (`HED5016` invalid-slot-declaration / `HED5017` multiple-slots in [`ParseContext`](../../src/Heddle/Language/ParseContext.cs)). Those header-slot rules and their diagnostics stay **unchanged** — the region model neither relaxes nor reuses them; it adds a new inner-definition marker rather than touching the prop-list slot grammar, so `@out()` and the one header slot are exactly as they are.

## Dependencies & ordering

**Nothing must land first.** This phase depends on no other phase's deliverable; the props / single-slot / `PropLayout` machinery it builds on is already stable in the tree today. It is sequenced **last by policy** ([R1](common/standing-rulings.md#standing-rulings-user-set)) — the risk-isolation ruling that places the named-content-region feature last: it is the one cross-cutting, four-layer change, so it lands after the bounded, low-risk phases have shipped. (The model it implements is the ratified [R7](common/standing-rulings.md#standing-rulings-user-set).)

**What this phase unblocks:** nothing else depends on it — it is a self-contained definition-side feature. ([Phase 8 (extension parameters)](phase-8-extension-parameters.md) is **independent** of this phase; it mirrors the *existing* definition-prop mechanism, not this region model.)

## Back-compat / impact

Governed by [R5](common/standing-rulings.md#standing-rulings-user-set) (additive) and the byte-identity back-compat gate. This feature is **designed to be back-compatible** — it takes no breaking-window change.

- **Rendered output:** every existing single-slot / `@out()` template renders **byte-identically**, under both the dynamic and precompiled backends. The differential corpus that asserts dynamic == precompiled does not move. This is the measurable spine below (the anonymous default slot becomes the default entry in the region table, exactly the way the single slot is preserved).
- **`@out()`:** unchanged — anonymous default slot, caller's loose body, caller context, byte-for-byte.
- **Props:** unchanged; regions compose with typed props without altering prop semantics.
- **The sibling-override idiom:** still compiles and renders identically; it remains a documented pattern, not a deprecated one.
- **New surface is inert on old templates.** The `<:name>` public marker is a genuinely new declaration surface that no existing template uses. The `<name:name>` override, by contrast, is **existing** surface — it only takes on region-binding meaning when its name matches a public region of a callee that declares one; against every existing callee (which declares no public regions) it keeps its current local-definition-override meaning, so nothing about old sources changes.
- **New diagnostics are additive.** The reserved `HED5019`+ block ([diagnostic-ids](common/diagnostic-ids.md)) is **compile errors**, but they fire only on the *new* public-region surface — overriding a **private-but-declared** region, a **duplicate public-region declaration**, a **type mismatch** against a matched public region — and only when the callee declares public regions. An override whose name matches no public region keeps today's meaning, so no template valid today acquires a new error or warning.
- **Docs:** [language-reference](../language-reference.md) and [patterns](../patterns.md#components-with-multiple-content-regions) gain the named-region section and keep the sibling-idiom recipe. (Doc edits are downstream of the code change, not part of this WHAT.)

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| The region state must be threaded through **four independent layers** (dynamic runtime, generator, LSP, precompiled) that each assume a single slot; a partial rollout diverges the backends and breaks the dynamic == precompiled guarantee. | Mirror the [`PropLayout`](../../src/Heddle/Runtime/Expressions/PropLayout.cs) precedent — one shared named-region table threaded through all four layers; roll out **all four at once** (no degrade-to-dynamic path); the existing differential harness asserts byte-identity across backends as a gate. | L |
| A single-slot / `@out()` template renders differently after the change (back-compat regression). | Model the anonymous default slot as the **default entry** in the region table so the single-slot path is unchanged; the byte-identity corpus under **both** backends is a hard success criterion. | M |
| Authors expect a call-site override body to run in the **caller's** context (as `@out()` does) but it runs in the **region's** context (component props + region model), producing surprising member resolution. | It **is** override semantics — the override body has the same scope as the default body it replaces; document it against the existing override model and cover it with an explicit validation scenario (an overridden `heading` reading the component's `title` prop). | M |
| The leading-colon public marker `<:name>` is a new use of `:` in the definition header alongside `<child:base>` inheritance and plain `<name>`, risking grammar ambiguity. | Disambiguated by position — nothing precedes the colon in `<:name>`, a name always precedes it in `<child:base>` — so the three forms are distinguishable; the exact grammar is a spec (HOW) concern resolved on this settled shape. | M |
| Scope creep toward extension-declared regions or outward channel-threading re-opens the debated broader model. | Non-goals pin regions to **definitions** and to call-scoped fill; extension regions are out of scope entirely (extensions get *parameters*, not regions — the unrelated [Phase 8](phase-8-extension-parameters.md)). | S |
| An existing call-body `<name:name>` override could change meaning if its name happens to match a region once a callee **gains** a `<:name>` public-region declaration. | The override binds only against a callee's *declared public regions*; against every callee that declares none (every template today) it keeps its local-definition-override meaning byte-for-byte. Adding a public region to a callee is a deliberate new-surface edit, and the byte-identity corpus gates that no pre-region template moves. | S |
| New compile errors (`HED5019`+) surprise authors. | They fire **only** on the new public-region surface (override a **private-but-declared** region, duplicate public-region declaration, type mismatch on a matched region); an override of a name the callee does not declare stays today's local override, not an error; no template valid today is affected; messages are positioned and name the offending region. | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test. Back-compat first.

- [ ] **Byte-identical single slot (both backends).** Every existing single-slot / `@out()` fixture in the differential corpus renders byte-identically under **both** the dynamic and precompiled backends after this phase; the dynamic == precompiled differential does not move.
- [ ] **`@out()` unchanged.** The anonymous default slot renders the caller's loose body in the **caller's** context, byte-for-byte identical to today, in a component that also declares named regions.
- [ ] **Two-region component renders each override in place.** A component with two public regions renders each region's call-site override in its declared position; a region left un-overridden renders its default body.
- [ ] **Region-targeting override is call-scoped.** A `<name:name>` override inside a call body targets the callee's matching **public** region and applies to that call only — a second call to the same component with no override renders the region's default body.
- [ ] **Private-but-declared region override is a positioned error.** Overriding a region the callee declares **private** (e.g. `<divider:divider>` against a callee that declares `<divider>`) at a call site is a positioned compile error from the `HED5019`+ block.
- [ ] **Override of an undeclared name is not an error (additivity).** A `<name:name>` override in a call body whose name matches **no** public region of the callee is **not** a region diagnostic — it keeps today's local-definition-override semantics and renders byte-identically to today. (This is the property that makes the whole feature additive; the sibling-override idiom is one instance of it.)
- [ ] **Duplicate public-region declaration is a positioned error.** Declaring two public regions with the same name in one component is a positioned compile error from the `HED5019`+ block.
- [ ] **Typed region override is type-checked.** A typed region's override body is compiled against the region's declared type — e.g. an `<item:item>` override where `item :: Article` binds `@(Title)` to `Article`, and a body reading a non-`Article` member fails to compile at that site.
- [ ] **Override body runs in the region's context.** In an overridden region, the component's props resolve (e.g. `@(title)`/`@(theme)` in an overridden `heading` read the component's props, not the caller's model), demonstrating region-context (not caller-context) scope.
- [ ] **Sibling-override sample still compiles.** The multi-region sibling-override sample from [patterns.md](../patterns.md#components-with-multiple-content-regions) compiles and renders unchanged.
- [ ] **Props compose with regions.** A component declaring both typed props and named regions type-checks and renders, with props and regions independent.
- [ ] **New diagnostics are additive.** No template valid today gains any new diagnostic; `HED5019`+ fires only when a callee declares public regions (the new surface), never on a call-body override against a callee that declares none.

## Validation scenarios

The scenarios use the ratified worked example: a `feed` component (props `title`, `theme`) exposing a public `heading`, a public typed `item :: Article`, and a private `divider`.

```heddle
@%
  <feed(title: string, theme: string = "light")>
  {{
    @%
      <:heading>{{ <h2 class="@(theme)">@(title)</h2> }}      @* public, overridable *@
      <:item :: Article>{{ <li>@(Title)</li> }}               @* public + typed/scoped *@
      <divider>{{ <hr class="@(theme)"> }}                     @* private *@
    %@
    @heading() @out() <ul>@list(Articles){{ @item(this) }}</ul> @divider()
  }} :: Blog
%@
```

Call site — normal override, call-scoped, public regions only:

```heddle
@feed(myBlog, title: "Latest", theme: "dark")
{{
  @%
    <heading:heading>{{ <h2 class="hero">@(title)</h2> }}
    <item:item>{{ <li><a href="/a/@(Id)">@(Title)</a></li> }}
  %@
  <p class="lede">Fresh:</p>            @* → @out(), caller context *@
}}
```

| Input | Expected outcome |
|---|---|
| `@feed(myBlog, title: "Latest", theme: "dark")` with the two overrides above. | `heading` renders `<h2 class="hero">Latest</h2>` (override body, region context: `@(title)` = the `title` prop); each `Articles` item renders the overridden `<li><a …>…</a></li>` (region typed `Article`); `divider` renders its private default `<hr class="dark">`; the loose `<p class="lede">Fresh:</p>` renders through `@out()` in the caller's context. |
| `@feed(myBlog, title: "Home")` with **no** override block. | All regions render their defaults: `<h2 class="light">Home</h2>`, `<li>@(Title)</li>` per article, `<hr class="light">`. Confirms overrides are call-scoped, not required. |
| A second `@feed(...)` call later in the same document with no override, after a first call that overrode `heading`. | The second call renders `heading`'s **default** — the override from the first call does not leak (call-scoped, unlike a document-onward `<name:name>` redefinition). |
| Call body overrides the **private** region: `<divider:divider>{{ … }}`. | Positioned compile error (`HED5019`+): `divider` is private and not overridable. |
| Call body overrides a name the callee does **not** declare as a region: `<masthead:masthead>{{ … }}`. | **No** region error — `masthead` matches no public region, so the override keeps today's local-definition-override meaning (a call-body-scoped `masthead` definition); rendered output is byte-identical to today. |
| Component declares two regions named `heading`. | Positioned compile error (`HED5019`+): duplicate region name. |
| `<item:item>` override body reads a member not on `Article`. | Compile error at the override site — the override body is type-checked against the region's `Article` type. |
| An existing single-slot component using only `@out()` (no regions), rendered under both backends. | Byte-identical output to today, dynamic and precompiled; differential unchanged. |
| The sibling-override sample from [patterns.md](../patterns.md#components-with-multiple-content-regions). | Compiles and renders unchanged — the idiom stays valid. |

## Open questions

None — resolved (see [open-questions.md](open-questions.md); ratified via the design debate, [standing-rulings R7](common/standing-rulings.md#standing-rulings-user-set)). The model — public regions as leading-colon inner definitions, filled by the existing `<name:name>` override bound call-scoped and gated by public/private, with override bodies in region context, `@out()` unchanged — is fixed. Residual items are spec-level (HOW) only: the exact grammar realization of the `<:name>` marker, and the precise split of situations across the reserved `HED5019`+ block (overriding a private-but-declared region, a duplicate public-region declaration, a type mismatch on a matched public region), whose count follows the ratified surface ([diagnostic-ids](common/diagnostic-ids.md)). Inheritance / narrowing per region mirrors the prop rules (assignable-only narrowing, matching the `HED5008` / `HED5014` direction).

## External grounding

| Claim | Source |
|---|---|
| A definition today exposes exactly one content region (the single `@out()` / `out:: Type` slot); a second `out::` is a compile error. | [language-reference → Parameterized slots](../language-reference.md#parameterized-slots-out-type) |
| The sanctioned multi-region idiom today is sibling definitions overridden with `<name:name>`; it works and stays typed but is more ceremony than the framework norm. | [patterns → components with multiple content regions](../patterns.md#components-with-multiple-content-regions) |
| The multi-slot gap is "the most visible expressiveness gap versus the component-framework state of the art." | [language-assessment §3.4](../language-assessment.md) |
| Filling reuses the existing `<name:name>` override / composition-without-coupling mechanism (call-scoped here); overrides are statically type-checked and compile to a single execution-ready document at zero render-time cost. | [language-reference → Inheritance and override](../language-reference.md#inheritance-and-override-childbase), [language-assessment §3.4](../language-assessment.md) |
| A region is an inner definition, so it composes with typed props and abstract / late type binding, and is rendered by being called; `@out(expr)` is the existing shape for projecting a typed value. | [language-reference → Definitions](../language-reference.md#definitions---), [Props](../language-reference.md#props-nameprop-type--default), [abstract definitions and late type binding](../language-reference.md#abstract-definitions-and-late-type-binding) |
| Typed props and the one typed slot are the current 2.0 component contract (props *and* slot content compile-checked); this is the Vue scoped-slot pattern, statically compiled. | [language-assessment §3.6](../language-assessment.md), [language-reference → Props](../language-reference.md#props-nameprop-type--default) |
| Slot state is one value threaded through four independent layers (dynamic runtime, generator, LSP, precompiled), each assuming a single slot — the reason the phase is large. | [`OutExtension.cs`](../../src/Heddle/Extensions/OutExtension.cs), [`SlotContent.cs`](../../src/Heddle/Core/SlotContent.cs), [`DefinitionBaseExtension.cs`](../../src/Heddle/Core/DefinitionBaseExtension.cs), [`Heddle.Generator` TemplateEmitter](../../src/Heddle.Generator/Emit/TemplateEmitter.cs), [`Heddle.LanguageServices`](../../src/Heddle.LanguageServices), [`PrecompiledRuntime.cs`](../../src/Heddle/Precompiled/PrecompiledRuntime.cs) |
| Props already solved the shared-table-across-four-layers problem with `PropLayout` — the precedent this phase mirrors for a named-region table. | [`PropLayout.cs`](../../src/Heddle/Runtime/Expressions/PropLayout.cs) |
| The header slot (`out:: Type`) is declared in the definition's **prop list** (grammar `def_slot`) and policed by the header walk (`HED5016` invalid-slot / `HED5017` multiple-slots), separate from the inner-definition grammar (`def` / `<child:base>` / `<name>`); regions are a new leading-colon marker on inner definitions and leave the header-slot mechanism and `HED5016`/`HED5017` **unchanged**. | [`HeddleParser.g4`](../../src/Heddle.Language/HeddleParser.g4) (`def_slot` vs `def`), [`ParseContext.cs`](../../src/Heddle/Language/ParseContext.cs), [language-reference → Parameterized slots](../language-reference.md#parameterized-slots-out-type) |
| New region diagnostics allocate from the reserved `HED5019`+ block in the props / slots family; they are compile errors that fire only on the new public-region surface (a callee declaring `<:name>` regions), so they are additive. | [diagnostic-ids](common/diagnostic-ids.md) |
| Precedent for multiple named / scoped content regions in a component framework: Vue named + scoped slots (child owns data, caller owns presentation). | [Vue — Slots (named & scoped)](https://vuejs.org/guide/components/slots.html) |
| Precedent for multiple typed content parameters: Blazor templated components with multiple `RenderFragment` / `RenderFragment<T>` parameters. | [Blazor — Templated components (RenderFragment)](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components) |
| Precedent for named content regions in the platform: Web Components named `<slot>` elements. | [MDN — the `<slot>` element](https://developer.mozilla.org/en-US/docs/Web/HTML/Element/slot) |
