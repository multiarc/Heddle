# Phase 3 supplement — body-execution entry-point inventory

Supplement to the [phase 3 specification](README.md). This is the phase's core engineering
artifact: the complete, source-verified inventory of every code path that begins executing
a subtemplate body, each with its file/member anchor, its frame decision, and the isolation
fixture that proves it. The roadmap names missing an entry point as the phase's top risk —
a missed entry leaks branch state across bodies silently. This inventory converts that risk
into a checklist: every row is an isolation test written failing before frames are wired
(the [TDD verdict](README.md#testing-plan)).

## The funnel finding (why the inventory is small)

Verification against current source (July 2026) produced the phase's most consequential
structural fact: **every render-time subtemplate body execution in the engine flows through
exactly two protected members** —
[`AbstractExtension.GetInnerResult(in Scope)`](../../../src/Heddle/Core/AbstractExtension.cs)
(the process path, line 30) and
[`AbstractExtension.RenderInnerResult(in Scope)`](../../../src/Heddle/Core/AbstractExtension.cs)
(the render path, line 35), which delegate to the body's compiled
`IProcessStrategy.Execute/Render` — plus the root scope construction in
[`HeddleTemplate.Generate`](../../../src/Heddle/HeddleTemplate.cs) (which
`PartialExtension` also routes through). A repo-wide search for `GetInnerResult`,
`RenderInnerResult`, `Strategy.Execute`, and `Strategy.Render` call sites (excluding
`bin/`, `obj/`, `generated/`) confirms there are **no other callers** of a compiled body's
strategy: `RuntimeDocument.ProcessData/RenderData` exist but have no external callers
(`SingleProcessor`/`CanOptimizeSelf` are likewise unreferenced), and `IProcessStrategy` is
internal with its four implementations private to `RuntimeDocument`.

Consequence: installing the frame inside the two funnel members plus the root `Generate`
scope construction covers **every** body-execution entry point below without touching any
individual extension. The per-extension rows exist to *prove* coverage (one isolation
fixture each), not to receive code changes.

## Inventory — body-executing entry points

"Frame decision" states what the body execution receives per
[README D2/D5](README.md#design-decisions): a **fresh frame** when the body's compiled
document contains a `[ScopeChannel]` participant, otherwise a **cleared** (null) frame —
in both cases the executing body never sees its parent body's frame.

| # | Entry point | Anchor (file · member) | Frame decision | Isolation fixture / test |
| --- | --- | --- | --- | --- |
| E1 | Root generate | [HeddleTemplate.cs](../../../src/Heddle/HeddleTemplate.cs) · `Generate` — both `new Scope(...)` sites (the `NET8_0_OR_GREATER` branch at line 129 and the down-level branch at line 139) | Fresh root frame per `Generate` call, provisioned iff `_runtimeDocument.NeedsLocals` | `BranchIsolationTests.RootLevelSetIsIndependentAcrossGenerateCalls` — two sequential `Generate` calls with opposite conditions on one compiled template |
| E2 | Process-path funnel | [AbstractExtension.cs](../../../src/Heddle/Core/AbstractExtension.cs) · `GetInnerResult(in Scope)` — delegates to `_processStrategy.Execute` (line 32) | Fresh/cleared frame installed here — covers every `ProcessData`-path body below | `BranchIsolationTests.FunnelProcessPathInstallsFreshOrClearedFrame` — white-box (`InternalsVisibleTo`): a `[ScopeChannel]` test reader captures `scope.Locals` inside (a) a participating body → fresh non-null frame, not reference-equal to the parent level's, (b) a non-participating body under a provisioned parent → `null`, (c) a non-participating body under a non-provisioned parent → the unmodified incoming scope (fast path). Also exercised transitively by every E4–E12 row |
| E3 | Render-path funnel | [AbstractExtension.cs](../../../src/Heddle/Core/AbstractExtension.cs) · `RenderInnerResult(in Scope)` — delegates to `_processStrategy.Render` (line 39) | Fresh/cleared frame installed here — covers every `RenderData`-path body below | `BranchIsolationTests.FunnelRenderPathInstallsFreshOrClearedFrame` — the E2 assertions repeated through the render path (`Generate` drives `RenderData`; the process path is forced via composition, e.g. the body consumed as a chained value). Also exercised transitively by every E4–E12 row |
| E4 | Definition invocation-site body (`@mydef(X){{caller content}}`) | [DefinitionBaseExtension.cs](../../../src/Heddle/Core/DefinitionBaseExtension.cs) · `ProcessData` line 24 / `RenderData` line 38 — `GetInnerResult(scope)` over the call-site body | Fresh frame per invocation (via E2/E3) | `BranchIsolationTests.CallerContentSetIsIndependentOfInvocationSiblings` — an `@if` sibling of the invocation cannot satisfy an `@else` inside the caller content |
| E5 | Definition body (the `@%def …%@` template) | [DefinitionBaseExtension.cs](../../../src/Heddle/Core/DefinitionBaseExtension.cs) · `DefinitionParameterTemplate.ProcessData` line 26 / `.RenderData` line 40 — a second `DefinitionBaseExtension` whose `_subTemplate` is the definition's own body | Fresh frame per invocation (via E2/E3) | `BranchIsolationTests.DefinitionBodySetIsIndependentOfCallerContent` — complete sets on both sides of an `@out()` render independently |
| E6 | `@list` iteration | [ListExtension.cs](../../../src/Heddle/Extensions/ListExtension.cs) · `ProcessData` (both the counted loop, line 70, and the `LinearList` loop, line 87) / `RenderData` line 110 — `GetInnerResult`/`RenderInnerResult` **per element** | Fresh frame **per iteration** (each funnel call installs anew) | `BranchIsolationTests.ListIterationResetsBranchState` — alternating per-element conditions over the `branching-list-alternating.heddle` fixture; roadmap success criterion 3 |
| E7 | `@for` iteration | [ForIndexExtension.cs](../../../src/Heddle/Extensions/ForIndexExtension.cs) · `ProcessData` line 30 / `RenderData` line 45 — funnel call per index | Fresh frame per iteration | `BranchIsolationTests.ForIterationResetsBranchState` — `@if(@i % 2 == 0)…@else` inside `@for` |
| E8 | `@if` / `@ifnot` body | [IfExtension.cs](../../../src/Heddle/Extensions/IfExtension.cs) · `ProcessData` line 27 / `RenderData` line 42; [IfNotExtension.cs](../../../src/Heddle/Extensions/IfNotExtension.cs) · lines 24/35/46 — `GetInnerResult(scope.Parent())` | Fresh frame for the branch body — an inner set cannot clobber the outer set | `BranchIsolationTests.NestedSetInsideIfBodyIsIndependent` — `branching-nested.heddle`; roadmap success criterion 4 |
| E9 | `@out(){{…}}` bodied projection | [OutExtension.cs](../../../src/Heddle/Extensions/OutExtension.cs) · `ProcessData` line 22 / `RenderData` line 33 — funnel over the re-scoping body | Fresh frame; the bodiless `@out()` splices an already-rendered string and executes no body (README D11) | `BranchIsolationTests.OutProjectionCarriesNoBranchState` — `branching-out-projection.heddle` |
| E10 | `@swap(){{…}}` body | [SwapExtension.cs](../../../src/Heddle/Extensions/SwapExtension.cs) · `ProcessData` line 18 / `RenderData` line 24 | Fresh frame (via E2/E3) | `BranchIsolationTests.SwapBodyGetsFreshFrame` |
| E11 | Bodied value containers — `@(X){{…}}` (`EmptyExtension`), `@html(X){{…}}` (`EmptyHtmlExtension`, including the `RenderProxy`/`HtmlEncodedRenderer` path) | [EmptyExtension.cs](../../../src/Heddle/Extensions/EmptyExtension.cs) · lines 14/31; [EmptyHtmlExtension.cs](../../../src/Heddle/Extensions/EmptyHtmlExtension.cs) · lines 15/32 via [AbstractHtmlExtension.cs](../../../src/Heddle/Core/AbstractHtmlExtension.cs) · `RenderData` line 30 (`scope.RenderProxy(...)` copies the `Locals` field like every transform) | Fresh frame for the body | `BranchIsolationTests.RescopingContainerBodyGetsFreshFrame` — also proves the frame survives the encoded-renderer proxy |
| E12 | Format/default bodies — `@date`, `@time`, `@int`, `@money`, `@guid` read a format/locale from the body; `@string` reads a **default value** from it (executed only when the model is `null`) | Six distinct anchor sets — see [E12 anchor detail](#e12-anchor-detail--the-six-formatdefault-body-extensions) below (the six are *not* one uniform pattern: five derive `AbstractHtmlExtension`, `@guid` derives `AbstractExtension`; four execute the body unconditionally, `@int` only for a non-null model, `@string` only for a null model; `@string` alone uses the render funnel E3, the other five call the process funnel E2 on both paths) | Fresh/cleared frame (via E2/E3) — format bodies are bodies like any other | `BranchIsolationTests.FormatBodyCannotReadEnclosingBranchState` — a `[ScopeChannel]` test reader inside a `@date` format body reads nothing; `BranchIsolationTests.StringDefaultBodyExecutesUnderClearedFrame` — the `@string` null-model default body (the one conditionally-executed, render-funnel body in the group) also reads nothing |
| E13 | `@partial()` | [PartialExtension.cs](../../../src/Heddle/Extensions/PartialExtension.cs) · `ProcessData` line 20 / `RenderData` line 25 — `InnerTemplate?.Generate(scope.ModelData, scope.ChainedData)` routes into E1; the caller's `Locals` cannot cross because `Generate` constructs a brand-new root `Scope` | Fresh root frame by construction — **no code change needed**; the fixture is the proof | `BranchIsolationTests.PartialGetsFreshRootFrame` — `branching-partial-parent.heddle` / `branching-partial-child.heddle`; plus the negative: an orphan `@else` in the child is a positioned `HED3003` in the child's compile |
| E14 | Compile-time directive bodies (`Scope.Null`) — `@model(){{…}}`, `@using(){{…}}`, `@import(){{…}}`, `@partial(){{…}}` read their body once during `InitStart` | [ModelExtension.cs](../../../src/Heddle/Extensions/ModelExtension.cs) · line 17, [UsingExtension.cs](../../../src/Heddle/Extensions/UsingExtension.cs) · line 17, [ImportExtension.cs](../../../src/Heddle/Extensions/ImportExtension.cs) · line 14, [PartialExtension.cs](../../../src/Heddle/Extensions/PartialExtension.cs) · line 34 — all `GetInnerResult(Scope.Null)` | Via E2 — `Scope.Null.WithLocals(...)` is an ordinary struct copy; directive bodies are plain text in practice and provision nothing | `ScopeChannelTests.DirectiveBodyExecutionUnderScopeNullDoesNotThrow` |

### E12 anchor detail — the six format/default-body extensions

Every anchor below re-verified against source (July 2026). "Funnel calls" names the exact
member and line where the body executes; "body executes" is the condition under which the
funnel is entered at all — the isolation guarantee must hold in each case, which is why the
two behavioral outliers (`@string`, `@guid`) get called out instead of being folded into
"same pattern".

| Extension | Class · base | Funnel calls (member · line) | Body meaning | Body executes when |
| --- | --- | --- | --- | --- |
| `@date` | [DateExtension.cs](../../../src/Heddle/Extensions/DateExtension.cs) · `AbstractHtmlExtension` | `ProcessDataInternal` → `GetInnerResult(parentData)` line 32; `RenderDataInternal` → `GetInnerResult(parentData)` line 43 | Date format string (default `"d"`) | Every execution — the format is read *before* the `DateTime` type check (lines 35/46) |
| `@time` | [TimeExtension.cs](../../../src/Heddle/Extensions/TimeExtension.cs) · `AbstractHtmlExtension` | `ProcessDataInternal` → `GetInnerResult(parentScope)` line 27; `RenderDataInternal` → line 42 | Time format string (default `"t"`) | Every execution — read before the type check |
| `@int` | [IntegerExtension.cs](../../../src/Heddle/Extensions/IntegerExtension.cs) · `AbstractHtmlExtension` | `ProcessDataInternal` → `GetInnerResult(parentScope)` line 30; `RenderDataInternal` → line 75 | Numeric format string | Model non-null only — both members return early on `null` (lines 27–28 / 71–72) before touching the body |
| `@money` | [MoneyExtension.cs](../../../src/Heddle/Extensions/MoneyExtension.cs) · `AbstractHtmlExtension` | `ProcessDataInternal` → `GetInnerResult(parentData)` line 36; `RenderDataInternal` → line 72 | Culture/locale name for `"c"` formatting | Every execution — the locale is read *before* the null check (lines 37/73) |
| `@string` | [StringExtension.cs](../../../src/Heddle/Extensions/StringExtension.cs) · `AbstractHtmlExtension` | `ProcessDataInternal` → `GetInnerResult(parentScope)` line 29; `RenderDataInternal` → **`RenderInnerResult(parentScope)`** line 53 | **Default value**, not a format | Only when `ModelData == null` — the group's one conditionally-executed body, and the only one that streams through the render funnel (E3) instead of returning a string through E2 |
| `@guid` | [GuidExtension.cs](../../../src/Heddle/Extensions/GuidExtension.cs) · **`AbstractExtension`** (no `[EncodeOutput]`) | `ProcessData` → `GetInnerResult(parentData)` line 26; `RenderData` → line 35 | Guid format string | Only when `ModelData is Guid` — the type check precedes the body read (lines 23/31) |

All six execute the body against `scope.Parent()`-derived scopes, all reach it through the
E2/E3 funnel members, and none is a `[ScopeChannel]` participant — so their bodies receive
a cleared (`null`) frame unless the body itself contains a participant, exactly like every
other body. The five `AbstractHtmlExtension` derivatives reach the funnel from inside
`ProcessDataInternal`/`RenderDataInternal` (the sealed wrappers
[AbstractHtmlExtension.cs](../../../src/Heddle/Core/AbstractHtmlExtension.cs) lines 9–37
add only encoding, and the `RenderProxy` wrap at line 30 copies `Locals` like every
transform); `@guid` overrides `ProcessData`/`RenderData` directly.

## Inventory — verified non-entry points (frame flows through unchanged)

These are the composition paths that execute *at the current body level* and must **share**
the current frame — the "sibling blocks in one body share the frame" half of the design.
Each is verified not to construct a body execution and has a test pinning the sharing.

| # | Path | Anchor | Frame behavior | Proof |
| --- | --- | --- | --- | --- |
| N1 | Chain execution (`@a():b()`) | [TemplateChain.cs](../../../src/Heddle/Runtime/TemplateChain.cs) · `ProcessData` line 34 / `RenderData` line 45 — items receive `scope.Chain(result)`, which copies `Locals` | Shared — a branch extension used inside a chain publishes into the enclosing body's frame (the roadmap's composition rule) | `BranchProtocolTests.ChainedBranchPublishesIntoEnclosingFrame` |
| N2 | Item dispatch | [TemplateItem.cs](../../../src/Heddle/Runtime/TemplateItem.cs) · `ProcessData` line 17 / `RenderData` line 35 — `scope.Model(model)` copies `Locals` | Shared | Covered by every protocol test |
| N3 | Nested chain parameters (`@out(a():b())`) | [ChainedParameter.cs](../../../src/Heddle/Runtime/Parameters/ChainedParameter.cs) · `GetParameter` line 32 — executes the inner chain against the current scope | Shared — participants inside nested chain parameters count toward the enclosing document's `NeedsLocals` walk (README D2) | `BranchProtocolTests.NestedChainParameterBranchPublishes` |
| N4 | `@<<{{…}}` parse-time import | [HeddleMainListener.cs](../../../src/Heddle/Language/HeddleMainListener.cs) · `ExitImport_block` line 203 — imported chains are appended to the importing context's `OutputChains` (lines 221–225) as zero-length blocks at the import position | Shared — imported blocks are siblings of the importing body; they participate in its branch sets and its static scan (README D16) | `BranchSetCompilerTests.ImportedBranchBlocksJoinTheImportingBodyLevel` |
| N5 | `@import()` render path | [ImportExtension.cs](../../../src/Heddle/Extensions/ImportExtension.cs) · `ProcessData`/`RenderData` lines 24–31 — both no-ops; the extension acts entirely at compile time (its body is E14) | No render-time body exists; the blocks it parses in are siblings of the importing body at runtime (README D16) | By inspection, pinned by `BranchSetCompilerTests.ImportExtensionParsedBlocksCoordinateAtRuntimeOnly` — an `@else` arriving via `@import()` draws no static diagnostic (the scan already ran) yet binds to the importing level's open set at render time |
| N6 | `@param()` | [ParamExtension.cs](../../../src/Heddle/Extensions/ParamExtension.cs) · `InitStart` line 11 returns `dataType` **without** calling `base.InitStart` — no body is ever compiled; `ProcessData` returns `ModelData`, `RenderData` is a no-op | No body execution exists — **this corrects the roadmap's resolution note** that listed `@param()` bodies as frame recipients (README D12) | By inspection; `BranchIsolationTests.ParamHasNoBodyExecution` pins the shape |
| N7 | Roslyn / native-expression parameters | [CompiledParameter](../../../src/Heddle/Runtime/Parameters/CompiledParameter.cs) · `ParameterImplementation` is `Func<object, object, object, object>` (line 9), invoked as `(scope.ModelData, scope.ChainedData, scope.RootData)` in `GetParameter` (line 18) — no `Scope` crosses into compiled code | Not applicable — expression code cannot reach the channel | By construction (sandbox note in README D3), pinned by `ScopeChannelTests.CompiledParameterDelegateCarriesNoScope` — a white-box shape assertion on the delegate type, so a future signature change re-opens the sandbox question visibly |

## Completeness argument

1. A body executes only through an `IProcessStrategy` (`Execute`/`Render`).
2. `IProcessStrategy` instances are created only by `RuntimeDocument`'s constructor and
   reachable only via `RuntimeDocument.Strategy`.
3. `RuntimeDocument.Strategy` is consumed in exactly two places:
   `AbstractExtension.InitStart` (stored as `_processStrategy`, invoked only by the two
   funnel members) and `HeddleTemplate.Compile` (stored as `_processStrategy`, invoked only
   by `Generate`).
4. Therefore E1–E3 dominate every body execution; E4–E14 enumerate the funnel's callers so
   each observable body kind has a dedicated isolation proof, and N1–N7 enumerate every
   scope-threading path that is *not* a body descent.

Step 3 was verified by searching `src/Heddle` (excluding `bin/`, `obj/`,
`generated/`, `node_modules/`) for `Strategy.Execute`, `Strategy.Render`,
`_processStrategy`, `GetInnerResult`, and `RenderInnerResult`. The complete match set
(re-verified July 2026):

- **`.Strategy` reads (the only two):**
  [AbstractExtension.cs](../../../src/Heddle/Core/AbstractExtension.cs) · `InitStart`
  line 97 (`_processStrategy = subTemplate?.Strategy`) and
  [HeddleTemplate.cs](../../../src/Heddle/HeddleTemplate.cs) · the private
  `Compile(CompileScope, string, bool)` line 264 (`_processStrategy = rtdoc?.Strategy`).
- **`_processStrategy` invocations:** `AbstractExtension.GetInnerResult` line 32
  (`.Execute`), `AbstractExtension.RenderInnerResult` line 39 (`.Render`), and
  `HeddleTemplate.Generate` lines 130/140 (`.Render`, once per TFM branch; the null guard
  at line 102 only tests it). No other member touches either field.
- **`IProcessStrategy` calls outside those fields:** only `RuntimeDocument`'s own
  `ProcessData` (line 130) and `RenderData` (lines 132–135), which forward to its
  `Strategy` — and those two members have **zero callers**: a `RuntimeDocument` is held
  only as `AbstractExtension._subTemplate` and `HeddleTemplate._runtimeDocument`, and is
  never placed into a chain or processor list. `SingleProcessor` (line 143) and
  `CanOptimizeSelf` (line 141) are likewise declared but unreferenced.
- **`GetInnerResult`/`RenderInnerResult` call sites** are exactly the extension members
  enumerated in rows E4–E14 (plus the definitions in `AbstractExtension` itself) — the
  sweep found no additional caller.

Any future extension that executes a body must call the protected funnel
members (they are the only access to `_processStrategy`), so the funnel guarantee holds for
custom extensions by construction — this is stated as a contract note in
[custom-extensions.md](../../custom-extensions.md) by work item WI8.
