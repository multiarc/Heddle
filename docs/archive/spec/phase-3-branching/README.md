# Phase 3 specification — branching via declarative local context flow

Status: **Specified — ready for implementation.**

> **Addendum (post-2.0 language work).** Branch-set classification is now `[BranchRole]`-driven
> (opener / continuation / terminal) rather than keyed on the fixed `@if`/`@elif`/`@else` name set —
> the D6 rationale anticipated this. Any extension carrying `[BranchRole]` gets identical set
> semantics; see [branch-role-universalization-spec.md](../../../../improvements/branch-role-universalization-spec.md).
> Archive content below is otherwise frozen.

Roadmap source: [phase 3 — branching via declarative local context flow](../../roadmap/phase-3-branching.md).
Prior phases assumed merged
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
[phase 1 — native expressions](../phase-1-native-expressions/README.md) (conditions like
`@elif(Count > 3)` compile through the native tier; the `HEDxxxx` plumbing —
`DiagnosticId`, `HeddleDiagnosticIds`, `ToError(position, id)` — exists) and
[phase 2 — safe output](../phase-2-safe-output/README.md) (`OutputProfile`; containers
forward raw and encoding happens once at the emitting leaf — the new branch extensions obey
the same container rule). This phase claims IDs from the `HED3xxx` block per
[cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
and concretely specifies
[cross-cutting D6](../common/cross-cutting-decisions.md#d6--the-scope-publishread-channel-is-public-api)
(the publish/read channel is public API).

### Documents

| Document | Purpose |
| --- | --- |
| README.md (this file) | The spec proper: assumed state, decision records, implementation plan, public API contract, diagnostics, testing, back-compat, performance. |
| [entry-points.md](entry-points.md) | The body-execution entry-point inventory — every code path that begins executing a subtemplate body, with anchors, frame decisions, isolation fixtures, and the completeness argument. The phase's core engineering artifact. |

## Scope and goal

`@elif` (alias `@elseif`) and `@else` arrive as ordinary `[ExtensionName]` extensions that
coordinate with a preceding `@if`/`@ifnot` through a **published local context** — no
keywords, no chain syntax, **zero grammar change** (`src/Heddle.Language/generated/` must
have no diff). Text between the blocks of one branch set is stripped at compile time, with
a warning when it contains anything beyond whitespace — the industry-standard adjacency
semantics ratified July 2026. The underlying mechanism is a general public extension
capability: `Scope.Publish` / `Scope.TryRead` over a lazily provisioned per-body frame,
with `BranchState` and its reserved key public so custom extensions can read a set's state
or drive one.

```heddle
@if(IsFeatured){{ <b>Featured</b> }}
@elif(IsArchived){{ <i>Archived</i> }}
@else(){{ Regular }}
```

In scope: the `Scope` promotion to a declared `readonly struct` plus its new internal frame
field, the frame-provisioning design and the body-execution entry-point inventory, the
public channel API (`Publish`/`TryRead`, `BranchState`, `[ScopeChannel]`), the two new
extensions plus additive publishing in `IfExtension`/`IfNotExtension`, the compile-time set
recognition/stripping/validation, this phase's diagnostics, the test/benchmark plan, and
the published-docs updates. Out of scope (with triggers, in
[Deferred items](#deferred-items)): auto-published per-item outcome records, channel
enumeration/removal APIs, richer adjacency lints.

## Assumed state

Every seam below was **re-verified against the current source** (July 2026). Corrections
to roadmap claims found during verification are marked ⚠ and closed in the decision
records. Searches excluded `bin/`, `obj/`, `node_modules/`, `generated/`.

| Seam | Verified state |
| --- | --- |
| [Scope](../../../src/Heddle/Data/Scope.cs) | `public struct Scope` (not yet `readonly struct`), six fields, **all** `readonly` (`ModelData`, `ChainedData`, `ParentModelData`, `CallerData`, `Renderer`, internal `RootData`); every member (`Parent` ×2, `Chain`, `Model` ×2, `RenderProxy`) is `readonly`-marked, pure, and `[MethodImpl(AggressiveInlining)]` except `RenderProxy`; static `Scope.Null` at line 60. Repo-wide search: **zero** `ref Scope` / `ref readonly Scope` — the entire dispatch surface (`IExtension`, `IProcessStrategy`, `IDataProcessor`, `IRuntimeParameter`) takes `in Scope`. The internal ctor is `(object root, object data, object model, object chained, IScopeRenderer renderer, object parent = null)` — roadmap promotion claim checks out exactly. |
| [AbstractExtension](../../../src/Heddle/Core/AbstractExtension.cs) | `GetInnerResult(in Scope)` (line 30) and `RenderInnerResult(in Scope)` (line 35) delegate to `_processStrategy.Execute/Render` when a compiled body exists, else return/render the literal `_innerResult`. `InitStart` (line 88) compiles the body via `InitSubTemplate` (line 54) → `HeddleCompiler.Compile(parameterTemplate, newContext, parseContext, chainedType)` and stores `_subTemplate`/`_processStrategy`. **These two members are the funnel for every render-time body execution** — the completeness argument is in [entry-points.md](entry-points.md#completeness-argument). |
| [HeddleTemplate.Generate](../../../src/Heddle/HeddleTemplate.cs) | Line 100; constructs the root scope as `new Scope(data, callerData, data, chained, renderer)` at two TFM-split sites (lines 129, 139). `_runtimeDocument`/`_processStrategy` are set together at compile (line 263–264). |
| [IfExtension](../../../src/Heddle/Extensions/IfExtension.cs) / [IfNotExtension](../../../src/Heddle/Extensions/IfNotExtension.cs) | Truthiness verified: `@if` renders iff `ModelData != null && (!(ModelData is bool b) \|\| b)`; `@ifnot` renders iff the negation (its `null` case renders — line 21–25 of IfNotExtension). Both compile their body against `parent` (`base.InitStart(initContext, parent, chainedType, null)`) and execute it under `scope.Parent()` — the condition value never becomes the body's model. Roadmap truthiness claim (`null` → false; `bool` → itself; non-null non-bool → true) confirmed for both. |
| [ParamExtension](../../../src/Heddle/Extensions/ParamExtension.cs) | ⚠ `InitStart` (line 11) returns `dataType` **without calling `base.InitStart`** — no body is ever compiled; `RenderData` is a no-op. The roadmap's resolution note "`@swap()`/`@param()` bodies follow the same rule: fresh frame" is half-stale: `@param()` has **no body execution at all**. Corrected in D12. |
| [DefinitionBaseExtension](../../../src/Heddle/Core/DefinitionBaseExtension.cs) | Invocation-site body via `GetInnerResult(scope)` (lines 24, 38); definition body via the nested `DefinitionParameterTemplate` (a second `DefinitionBaseExtension`, lines 26, 40). Both route through the funnel. Recursion guard is a `ThreadLocal<int>` — the class carries no cross-render mutable state. |
| [HeddleCompiler.Compile](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Line 22. Preprocessing order verified: `ShiftBySkippedTokens` (line 28) aligns positions to the clean document, `RemoveDefinitions` (line 29), `ReplaceRawOutput` (line 30); then the `parseContext.OutputChains` loop (line 32) compiles chains **in document order**, removing directive chains whose threaded type ends `null` via `RemoveEmptyItem` (lines 55–58, method at line 97: `ExStringBuilder.ApplyRemove` + a reverse shift loop over later chains); then `DefaultChains` (line 65). One `Compile` invocation = one body = one `ParseContext` — the natural scope for the set scan (D10). |
| [ExStringBuilder](../../../src/Heddle/Strings/ExStringBuilder.cs) | `ApplyRemove(BlockPosition, ref string)` (line 341) removes a span and returns its length (the shift seed); built on `Replace` (line 306). This is the exact machinery the stripping reuses. |
| [DocumentParser.Parse](../../../src/Heddle/Language/DocumentParser.cs) | Returns `tree.GetText()` (lines 77, 92) — the **clean document with hidden-channel content already excised** (comments `@* … *@`, `@\` trimmed whitespace); hidden tokens are recorded as `SkippedTokens` (lines 84–87) and `ShiftBySkippedTokens` re-aligns block positions to the clean text. ⚠ Refinement of the roadmap's comment claim: a comment between branch blocks never *reaches* the working document — the gap the scan sees is already comment-free (D10). |
| [HeddleMainListener](../../../src/Heddle/Language/HeddleMainListener.cs) | `EnterSubtemplate` (line 136) pushes a child `ParseContext` with `offset = body start`; `ExitSubtemplate` (line 175) attaches it as `OutputItem.Context` — every non-empty body owns its chains, so branch sets are per-body by construction. `ExitImport_block` (line 203) parses `@<<{{…}}` imports at walk time and appends the imported chains to the **importing** context's `OutputChains` as zero-length blocks at the import position (lines 221–225). |
| [ParseContext](../../../src/Heddle/Language/ParseContext.cs) | `OutputChains` in document order (siblings append on `ExitOutblock`); `DefenitionExists` (line 340) is the definition-shadow check `CompileItem` uses; `CreateOutputChain` (line 274) sets `OutputChain.BlockPosition` to the **whole block span** (call + body), offset-relative — so gaps between sibling blocks are directly computable. `OutputItem.Position` stays in parse coordinates (never shifted) — the position every existing diagnostic uses. |
| [TemplateFactory](../../../src/Heddle/Runtime/TemplateFactory.cs) | Static `Dictionary<string, Type>` registry (line 34), ordinal case-sensitive. Registered names verified from the `[ExtensionName]` sweep: `""`, `html`, `date`, `time`, `int`, `money`, `guid`, `string`, `list`, `if`, `ifnot`, `for`, `out`, `swap`, `param`, `partial`, `import`, `using`, `model`, plus phase 2's `raw` and `profile` — **`elif`, `elseif`, and `else` are all free**. A colliding `[ExportExtensions]` assembly throws `TemplateOverrideException` at static-init time. Phase 1 added `internal static bool Exists(string)`. |
| [RuntimeDocument](../../../src/Heddle/Runtime/RuntimeDocument.cs) | Constructor (line 15) receives the `DocumentElement[]` execute items (each a `TemplateChain` of `TemplateItem`s) before optimization — the natural place to compute the frame-provisioning flag (D2). `GetDocumentPieces` (line 91) slices the working document around chain positions — a stripped span never reaches it, so stripped text cannot render. `IProcessStrategy` and its four private strategy implementations are internal. |
| [TemplateChain](../../../src/Heddle/Runtime/TemplateChain.cs) / [TemplateItem](../../../src/Heddle/Runtime/TemplateItem.cs) / [ChainedParameter](../../../src/Heddle/Runtime/Parameters/ChainedParameter.cs) | Chain items execute at the current body level via `scope.Chain(result)` (TemplateChain lines 39, 53); item dispatch is `scope.Model(Parameter.GetParameter(scope))` (TemplateItem lines 19–21, 37–39); nested chain parameters execute via `ChainedParameter.GetParameter` (line 32). All are scope *transforms*, not body descents — the frame reference rides through every one of them. `ChainedParameter` collapses single-item chains to the `TemplateItem` (ctor lines 16–23) — the `NeedsLocals` walk must handle both shapes. |
| Warnings plumbing | Compile-stage warnings live on [`CompileContext.CompileWarnings`](../../../src/Heddle/Runtime/CompileContext.cs) (line 63; the list is **shared by reference** into child contexts, line 80) surfaced via `CompileScope.CompileWarnings`; parse-stage warnings live on `ParseContext.Warnings`. [`HeddleCompileWarning`](../../../src/Heddle/Data/HeddleCompileWarning.cs) carries `Fix`. This phase's warnings go to `CompileContext.CompileWarnings`. |
| Phase 1/2 artifacts used | `HeddleCompileError.DiagnosticId`, `HeddleDiagnosticIds`, `ToError(position, id)` (phase 1 D1); native-tier conditions thread `ExType == bool` into `InitStart` with zero extension changes (phase 1 D18); `elif`/`else` carry no `[EncodeOutput]`, so under phase 2's `Html` profile they are `RenderType.Raw` containers that forward bodies verbatim while leaf outputs inside the bodies encode themselves (phase 2 D10 container rule — no double encoding by construction). |
| Tests / benchmarks | `InternalsVisibleTo("Heddle.Tests")` and `("Heddle.Performance")` declared ([AssemblyInfo.cs](../../../src/Heddle/Properties/AssemblyInfo.cs)) — white-box assertions on `ScopeLocals`/`NeedsLocals` are possible. Fixture home [src/Heddle.Tests/TestTemplate](../../../src/Heddle.Tests/TestTemplate); render benchmark [TextRenderBenchmarks](../../../src/Heddle.Performance/TextRenderBenchmarks.cs). |

## Design decisions

Numbering is local to this spec; cross-cutting decisions are cited by link. Every roadmap
resolution-round outcome, lean, and "pinned-during-design" item for this phase is
re-verified and closed below.

### D1 — `Scope` becomes a declared `readonly struct` with one added frame field

**Decision.** [`Scope`](../../../src/Heddle/Data/Scope.cs) is declared
`public readonly struct Scope` and gains one field, `internal readonly ScopeLocals Locals`
(a reference to the internal mutable frame class, D3), added as a trailing optional
parameter of the internal constructor. All six pure transforms (`Parent` ×2, `Chain`,
`Model` ×2, `RenderProxy`) copy `Locals` unchanged; a new internal
`Scope WithLocals(ScopeLocals locals)` transform replaces it (used only by the frame
installation points, D2). `Scope.Null` keeps `Locals == null`.
**Rationale.** Research-confirmed by the roadmap and re-verified here: all fields already
`readonly`, all members `readonly`-marked, no `ref Scope` usage anywhere — the promotion
makes the no-defensive-copy guarantee structural instead of per-member, and `in` parameters
are C# 7.2 compile-time features valid on every TFM including `netstandard2.0`. Adding the
`readonly` modifier and an internal field is source- and binary-compatible.
**Alternatives rejected.** Frame on `IScopeRenderer` (couples the channel to the renderer
lifetime; breaks under `RenderProxy`'s `HtmlEncodedRenderer` wrapping and under phase 8's
sink replacement); a public frame field (the frame type is an implementation detail —
consumers get `Publish`/`TryRead`).
**Grounding.** [Avoid memory allocations and data copies](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/),
[the `in`-modifier and readonly structs](https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/).

### D2 — Frame provisioning is compile-time (`[ScopeChannel]`), not first-publish

**Decision.** A body execution receives a fresh frame **iff its compiled document
statically contains a channel participant**. Mechanics:

- `[ScopeChannel]` (D6) marks extension classes that publish or read the channel.
- [`RuntimeDocument`](../../../src/Heddle/Runtime/RuntimeDocument.cs)'s constructor
  computes `internal bool NeedsLocals { get; }` by walking its `DocumentElement[]`: every
  `TemplateChain` item's `Extension` type is tested with the existing
  `IsHaveAttribute<ScopeChannelAttribute>(true)` helper
  ([TypeExtension.cs](../../../src/Heddle/Helpers/TypeExtension.cs) line 74), and every
  item whose `Parameter` is a `ChainedParameter` recurses into the nested chain (both the
  collapsed `TemplateItem` and the `TemplateChain` shape). Nested bodies are separate
  `RuntimeDocument`s and do not contribute — the flag is strictly per body level.
- The three installation points (the complete render-time set, per
  [entry-points.md](entry-points.md)): `HeddleTemplate.Generate` passes
  `_runtimeDocument.NeedsLocals ? new ScopeLocals() : null` to the root `Scope`;
  `AbstractExtension.GetInnerResult`/`RenderInnerResult` execute the body under
  `scope.WithLocals(new ScopeLocals())` when `_needsLocals` (captured from
  `_subTemplate.NeedsLocals` in `InitStart`), under `scope.WithLocals(null)` when the
  incoming `scope.Locals != null` (isolation: a body never sees its parent's frame), and
  under the **unmodified `scope`** otherwise — the fast path that keeps non-participating
  templates copy-free.

⚠ This **corrects the roadmap's "lazily created on first publish" wording** with a proof:
`Locals` is a `readonly` field on a struct received by `in`, and sibling extensions receive
independent struct copies derived from the body-level scope — a publish cannot install a
frame reference that earlier-made copies would observe. The only shapes that satisfy
"siblings share automatically" are (a) a pre-existing shared cell (an allocation per body —
violates the zero-cost goal) or (b) compile-time provisioning. (b) preserves the roadmap's
actual budget exactly: *a list body without branches allocates nothing*, and a publishing
body costs one small allocation at entry — the same "one allocation instead of three" the
roadmap's .NET 10 analysis budgeted for first publish, moved from first-publish time to
body entry of the same execution.
**Rationale.** The flag is exact (not conservative): built-in branch extensions always
interact with the channel when executed, and custom participants declare themselves. Per
[cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)
no design may rely on JIT escape analysis deleting the frame — it provably escapes through
the `in Scope` dispatch surface.
**Alternatives rejected.** Frame cell allocated per body unconditionally (an allocation per
`@list` iteration for every existing template — hot-path regression); a renderer-hosted
frame stack with save/restore (see D1); `Unsafe.AsRef` write-through (mutates only the
immediate caller's copy — broken through `TemplateItem`'s `scope.Model(...)` dispatch copy,
and monstrous besides).

### D3 — The channel API: `Publish`/`TryRead`, reserved keys, error contract

**Decision.** Final names are the roadmap's working names. On `Scope`:

```csharp
public void Publish(string key, object value);
public bool TryRead(string key, out object value);
```

Semantics, pinned:

- Keys are ordinal, case-sensitive strings. `null` key → `ArgumentNullException` (both
  methods). `null` **values are legal**: after `Publish(k, null)`, `TryRead(k, out v)`
  returns `true` with `v == null`. Last-write-wins within the frame; there is no removal
  API (YAGNI — a trigger sits in [Deferred items](#deferred-items)).
- The prefix `heddle.` is **reserved for the engine**. `Publish` with a `heddle.`-prefixed
  key other than `BranchState.ReservedKey` → `ArgumentException`; publishing
  `BranchState.ReservedKey` requires the value to be a `BranchState`
  (else `ArgumentException`) and routes to the dedicated slot (D4). `TryRead` never throws
  for unknown reserved keys — it returns `false`.
- `Publish` on a scope without a frame (`Locals == null`) →
  `InvalidOperationException` whose message names the remedy: *"No local context frame is
  available here — mark the extension with [ScopeChannel] so the compiler provisions one
  for bodies that contain it."* This is a host-programming error per the
  [error-handling standards](../common/coding-standards.md#error-handling-and-diagnostics),
  not a template diagnostic. `TryRead` without a frame returns `false`.
- Thread safety: a frame belongs to one body execution of one render invocation and is
  reached only through `Scope` values the engine hands to extension calls on that thread;
  `Publish`/`TryRead` are unsynchronized by design. The documented contract (XML docs +
  `custom-extensions.md`): a `Scope` must not be stored on the extension instance or used
  after the call that received it — the pre-existing extension threading contract.
- Native-expression and C#-tier code cannot reach the channel: compiled parameters receive
  `(model, chained, root)` objects, never a `Scope` — publishing is an extension-only
  capability, so the channel adds no sandbox surface.

**Rationale.** Two methods and one struct are the whole public mechanism
([cross-cutting D6](../common/cross-cutting-decisions.md#d6--the-scope-publishread-channel-is-public-api));
the reserved prefix keeps future engine keys collision-free without a registry, and
throwing on frameless `Publish` makes the one misuse (forgetting the attribute) loudly
diagnosable instead of silently dropped.
**Alternatives rejected.** `TryPublish` returning `bool` (invites silently-dropped
publishes — the failure the throw exists to surface); a key-object scheme instead of
strings (keys must be documentable in template-facing docs and diagnosable in logs);
per-key subscription callbacks (speculative — no consumer).

### D4 — `BranchState` is a public `readonly struct` with a dedicated frame slot

**Decision.** `public readonly struct BranchState` in `Heddle.Data`
(`src/Heddle/Data/BranchState.cs`): one property `bool Satisfied`, one constructor, and
`public const string ReservedKey = "heddle.branch"`. The frame class `ScopeLocals`
(internal, `src/Heddle/Data/ScopeLocals.cs`) holds a **dedicated slot** for it
(`BranchState? _branch`) plus a lazily created `Dictionary<string, object>` overflow map
for user keys — the roadmap's recommended inline-slot shape, identical code on
`netstandard2.0` through `net10.0` (no `#if`, per
[cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)).
Internal accessors `Scope.PublishBranch(in BranchState)`, `Scope.TryReadBranch(out BranchState)`,
and `Scope.ClearBranch()` are the built-in extensions' zero-boxing fast path; the public
`Publish`/`TryRead` route the reserved key to the same slot, boxing only on public reads.
"Satisfied" means *some branch in the current set has already fired* — condition semantics,
never output sniffing (an `@if` whose body rendered empty still satisfies the set).
**Rationale.** A class would cost one heap allocation per `@if` execution in provisioned
bodies (hot loops); the struct + slot makes the whole branch protocol allocation-free
beyond the frame itself. Public struct + public key is the ratified protocol surface:
custom extensions can read the state to render alongside a set or publish it to drive one.
**Alternatives rejected.** `sealed class BranchState` (per-publish allocation);
an `enum` state (the roadmap protocol is exactly one bool; an enum invites invented
states); keeping the key internal (contradicts the ratified public-protocol decision —
only the *frame class* stays internal).

### D5 — Frame scoping falls out of the funnel: per body, per iteration, nested-isolated

**Decision.** Frame lifetime = one body execution, implemented entirely at the three D2
installation points. Consequences, each pinned by an isolation fixture in
[entry-points.md](entry-points.md): sibling blocks in one body share the frame (the
body-level scope value threads through `TemplateChain`/`TemplateItem` transforms, which
copy `Locals`); each `@list`/`@for` **iteration** is a distinct funnel call → fresh frame
per element; nested bodies get fresh (or cleared) frames → an inner set cannot clobber or
*read* the outer set; the root frame spans one `Generate` call; `DefaultChains` elements
execute as root-body siblings and share the root frame. Body isolation is strict: even a
non-provisioned body executes with `Locals == null` rather than inheriting the parent's
frame — `TryRead` never sees across a body boundary.
**Rationale.** One rule ("a body execution owns its frame") with zero per-extension code;
strict isolation keeps the mental model identical for reads and writes — the roadmap's
nested-set and per-iteration requirements are corollaries, not special cases.
**Alternatives rejected.** Parent-chain fallback reads (React-Context-style flowing
parent→child) — the ratified design flows **across siblings** like CSS counters, and
cross-body reads would make `@elif` inside a nested body silently bind to the outer set,
exactly the clobbering the roadmap excludes.

### D6 — Channel participation is declared by `ScopeChannelAttribute`, not a marker interface

**Decision.** `[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]`
`public sealed class ScopeChannelAttribute : Attribute` in `Heddle.Attributes`
(`src/Heddle/Attributes/ScopeChannelAttribute.cs`). It declares "this extension publishes
to or reads from the local context channel"; the D2 walk provisions a frame for every body
that contains such an extension. Applied to `ElifExtension` and `ElseExtension`;
deliberately **not** applied to `IfExtension`/`IfNotExtension` (D7). `Inherited = true`
means derived custom extensions keep participation automatically.
**Rationale.** The Framework Design Guidelines say to avoid empty marker interfaces and
use a custom attribute when marking types
([Interface design](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/interface),
[CA1040](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1040));
attributes are also Heddle's established extension-metadata idiom (`[EncodeOutput]`,
`[DataType]`, `[ChainedType]`), checked with the same `IsHaveAttribute` helper at compile
time only — never at render time.
**Alternatives rejected.** Marker interface `IScopePublisher` (FDG-discouraged; also
poorer discoverability next to the existing attribute family); provisioning every body
unconditionally (per-iteration allocation for all existing templates); a conservative
"any non-built-in extension might publish" rule (allocates for every custom-extension
body forever — punishes the common case to avoid one documented attribute).

### D7 — `@if`/`@ifnot` publish opportunistically; existing templates allocate nothing new

**Decision.** `IfExtension`/`IfNotExtension` publish `BranchState` in both `ProcessData`
and `RenderData` via an internal no-op-without-frame path: when `scope.Locals == null` the
publish silently does nothing. They do **not** carry `[ScopeChannel]` and therefore do not
trigger provisioning by themselves. Since the frame exists exactly when some participant
(an `@elif`, `@else`, or attributed custom extension) shares the body, the elided publish
is **semantically unobservable**: nothing that could read the state can exist in a
frameless body (readers must be attributed; frames never cross bodies; hosts never hold a
`Scope`).
**Rationale.** This is the back-compat and performance keystone: every existing template
contains only `if`/`ifnot` (no participants — `elif`/`else` did not exist), so **no
existing template ever provisions a frame** — the render path stays allocation-identical
and the D2 fast path keeps it copy-identical, making the
[regression gate](../common/testing-standards.md#regression-gates)'s "allocated bytes must
not increase" hold literally rather than by ratified exception. The protocol table's
"@if always publishes" remains true in every observable execution.
**Alternatives rejected.** Attributing `If`/`IfNot` (one `ScopeLocals` allocation per
iteration for every existing `@list`+`@if` template — a real hot-path regression the
benchmark gate would rightly reject); making the public `Publish` also silently no-op
without a frame (hides real bugs in custom extensions — the built-ins' elision is safe
only because their publish is provably unreadable in that situation).

### D8 — The branch protocol (normative runtime state machine)

**Decision.** The protocol table below is normative; it is encoded as the table-driven
`BranchProtocolTests` before the extensions are written (TDD verdict). Shared truthiness
lives in one internal helper, `BranchCondition.IsTruthy(object)`
(`src/Heddle/Extensions/BranchCondition.cs`):
`value != null && (!(value is bool b) || b)` — extracted verbatim from today's verified
`IfExtension`/`IfNotExtension` semantics, which both extensions are rewired to consume
(their standalone behavior is byte-identical). Publishing happens in **both**
`ProcessData` and `RenderData` so composition paths (chained or nested-chain usage)
behave identically to document-level blocks.

| Extension | State absent | `Satisfied == false` | `Satisfied == true` |
| --- | --- | --- | --- |
| `@if(c)` / `@ifnot(c)` | evaluate; render body iff predicate true; publish `Satisfied = predicate` (starts a set; `@ifnot`'s predicate is `!IsTruthy(c)`) | same — a new `@if` always overwrites (new set) | same — overwrites (new set) |
| `@elif(c)` / `@elseif(c)` | behaves exactly like `@if(c)` (defined standalone semantics; compile-time lint HED3002 where statically visible) | evaluate; render body iff truthy; publish `Satisfied = truthy(c)` | render nothing; leave state unchanged (republish-unchanged is a no-write) |
| `@else()` | **error** — "no matching `@if`": positioned compile error HED3003 where statically detectable, `TemplateProcessingException` with the same message otherwise (D9) | render body; **clear the state** (terminal — publishes nothing) | render nothing; **clear the state** (terminal) |

**Exhaustive transition enumeration (normative).** The compact table above is the design
view; the enumeration below is the complete row set `BranchProtocolTests` encodes
one-to-one (WI1) — every extension × pre-state × condition-value combination, with the
publish action explicit per row. `T` = `BranchCondition.IsTruthy(condition value)`.
Pre-state is the frame's branch slot before the block runs; post-state after.

One **verified operational fact** governs every row: *the condition is always evaluated*.
Item dispatch computes the parameter before the extension ever runs
([TemplateItem.cs](../../../src/Heddle/Runtime/TemplateItem.cs) — `Parameter.GetParameter(scope)`
precedes `Extension.ProcessData/RenderData`, lines 19–21 and 37–39), and an extension
cannot suppress it. A satisfied set therefore short-circuits **bodies only, never
conditions** — unlike keyword engines, where `{% elif %}` conditions after a satisfied
branch are skipped, an `@elif` condition's evaluation cost (and any C#-tier side effect)
is incurred on every pass. Rows P17–P18 and P21 evaluate-and-ignore by design.

| # | Extension | Pre-state | Condition | Body renders | Publish action | Post-state |
| --- | --- | --- | --- | --- | --- | --- |
| P01 | `@if(c)` | none | `T` | yes | write `Satisfied = true` (starts a set) | `Satisfied == true` |
| P02 | `@if(c)` | none | `!T` | no | write `Satisfied = false` (starts a set) | `Satisfied == false` |
| P03 | `@if(c)` | `Satisfied == false` | `T` | yes | overwrite `Satisfied = true` (new set) | `Satisfied == true` |
| P04 | `@if(c)` | `Satisfied == false` | `!T` | no | overwrite `Satisfied = false` (new set) | `Satisfied == false` |
| P05 | `@if(c)` | `Satisfied == true` | `T` | yes | overwrite `Satisfied = true` (new set) | `Satisfied == true` |
| P06 | `@if(c)` | `Satisfied == true` | `!T` | no | overwrite `Satisfied = false` (new set) | `Satisfied == false` |
| P07 | `@ifnot(c)` | none | `T` | no | write `Satisfied = false` (starts a set) | `Satisfied == false` |
| P08 | `@ifnot(c)` | none | `!T` | yes | write `Satisfied = true` (starts a set) | `Satisfied == true` |
| P09 | `@ifnot(c)` | `Satisfied == false` | `T` | no | overwrite `Satisfied = false` (new set) | `Satisfied == false` |
| P10 | `@ifnot(c)` | `Satisfied == false` | `!T` | yes | overwrite `Satisfied = true` (new set) | `Satisfied == true` |
| P11 | `@ifnot(c)` | `Satisfied == true` | `T` | no | overwrite `Satisfied = false` (new set) | `Satisfied == false` |
| P12 | `@ifnot(c)` | `Satisfied == true` | `!T` | yes | overwrite `Satisfied = true` (new set) | `Satisfied == true` |
| P13 | `@elif(c)` / `@elseif(c)` | none | `T` | yes | write `Satisfied = true` (acts as `@if`; HED3002 where statically visible) | `Satisfied == true` |
| P14 | `@elif(c)` / `@elseif(c)` | none | `!T` | no | write `Satisfied = false` (acts as `@if`; HED3002 where statically visible) | `Satisfied == false` |
| P15 | `@elif(c)` / `@elseif(c)` | `Satisfied == false` | `T` | yes | write `Satisfied = true` | `Satisfied == true` |
| P16 | `@elif(c)` / `@elseif(c)` | `Satisfied == false` | `!T` | no | write `Satisfied = false` | `Satisfied == false` |
| P17 | `@elif(c)` / `@elseif(c)` | `Satisfied == true` | `T` (evaluated, ignored) | no | **no write** (republish-unchanged elided) | `Satisfied == true` |
| P18 | `@elif(c)` / `@elseif(c)` | `Satisfied == true` | `!T` (evaluated, ignored) | no | **no write** | `Satisfied == true` |
| P19 | `@else()` | none | — (any parameter evaluated, discarded; HED3004 additionally where statically visible) | no — **error**: HED3003 where statically visible, `TemplateProcessingException` with the same message at runtime, on both the process and render paths | none | none (unchanged — a further orphan `@else` errors again) |
| P20 | `@else()` | `Satisfied == false` | — (any parameter evaluated, discarded; HED3004 where statically visible) | yes | **clear the slot** (terminal — publishes nothing) | none |
| P21 | `@else()` | `Satisfied == true` | — (any parameter evaluated, discarded; HED3004 where statically visible) | no | **clear the slot** (terminal) | none |

Row semantics pinned:

- **Both paths, identically.** Every row holds on the `ProcessData` path and the
  `RenderData` path, and is position-independent — the same through document-level
  dispatch, chained usage, and nested chain parameters (that is why publishing happens in
  both members).
- **Non-rendering process rows return `string.Empty`, never `null`** — matching
  `IfExtension` today (its non-rendering arms return `string.Empty`,
  [IfExtension.cs](../../../src/Heddle/Extensions/IfExtension.cs) lines 22/30), preserving
  the `AbstractExtension` never-null contract and keeping the threaded type `string`.
- **"Clear the slot"** means the frame's dedicated `BranchState` slot is emptied: a
  subsequent `TryReadBranch` — and a public `TryRead(BranchState.ReservedKey, …)` —
  returns `false`. Clearing is what makes P19's repeat-error behavior possible.
- **Frameless degradation (`Locals == null`)** can occur only for `@if`/`@ifnot` (D7 —
  they carry no `[ScopeChannel]`): rows P01–P12 keep their render column and drop the
  publish column as a silent no-op, which is unobservable because no reader can exist in a
  frameless body. `@elif`/`@else` always run with a frame — their own attribute provisions
  it.
- **All writes are slot writes** through the internal zero-boxing accessors
  (`PublishBranch`/`ClearBranch`, D4); the overflow map is never touched by the protocol.

Bodies compile against `parent` exactly like `IfExtension` today
(`base.InitStart(initContext, parent, chainedType, null)`; execution under
`scope.Parent()`) — the condition value never becomes the body's model, and the threaded
return type stays `string`. Conditions from the phase 1 native tier arrive as boxed `bool`
(`ExType == bool`) and hit the `bool` arm of `IsTruthy`; member paths and C#-tier values
keep the truthy rules — one reconciled semantics for all tiers, unchanged from today's
`IfExtension`. Neither new extension carries `[EncodeOutput]`, so under phase 2's `Html`
profile they are raw-forwarding containers: static markup in branch bodies stays raw and
leaf outputs encode themselves exactly once (phase 2 D10).
**Rationale.** Roadmap protocol table adopted verbatim, with "republish unchanged"
implemented as no-write (observationally identical, one fewer slot store) and the
truthiness single-sourcing being this phase's DRY obligation.
**Alternatives rejected.** Output-sniffing satisfaction (an `@if` with an empty body would
un-satisfy the set — explicitly excluded by the roadmap); publishing only in `RenderData`
(breaks composition through `ProcessData` paths).

### D9 — Orphan reconciliation: which cases are error, warning, or render-as-if

**Decision.** The severity split, reconciling the roadmap phase doc's protocol table and
compile-time validation section with its validation-scenario table and the roadmap README:

| Case | Compile time (statically visible) | Runtime (composition paths) |
| --- | --- | --- |
| `@else` with no preceding branch block at this body level | **Error HED3003** | `TemplateProcessingException`, same message |
| Second `@else` in one set (`@if…@else…@else`) | **Error HED3003** (the first `@else` closed the set) | `TemplateProcessingException` |
| `@elif` with no preceding branch block | **Warning HED3002**; renders as `@if` (starts a set) | acts as `@if`, no diagnostic |
| `@elif` after `@else` | **Warning HED3002**; acts as `@if` and starts a **new** set (a following `@else` legally closes it) | acts as `@if` |
| `@else`/`@elif` preceded by a `[ScopeChannel]` custom-extension block | **no diagnostic** (state statically unknown — the custom block may publish) | protocol table applies to whatever state exists |
| `@else` with a non-empty parameter | **Warning HED3004**; parameter ignored | parameter value ignored |

⚠ This closes an **internal roadmap inconsistency**: the phase doc's validation-scenario
row labels `@elif` after `@else` "error — the set was closed (no state)", while its
protocol table and compile-time-validation section define state-absent `@elif` as
warning-plus-acts-like-`@if`. The protocol table wins: `@elif` has well-defined no-state
semantics (that is what makes it standalone-friendly), the compiler must not be stricter
than the runtime state machine it mirrors (a template assembled via composition would work
at runtime yet fail statically), and the roadmap README's resolution summary names only
`@else` when listing the error ("a second `@else`/orphan `@else` is a 'no matching `@if`'
error") — confirming `@else` is the only orphan **error**. The custom-publisher row preserves
roadmap success criterion 7 (a custom matcher satisfying a set so `@else` stays silent)
from being destroyed by a false-positive HED3003.
**Rationale.** Error exactly where rendering is undefined (`@else` cannot act alone
because it is terminal and publishes nothing); warning where semantics are defined but
probably unintended; silence where the compiler cannot know.
**Alternatives rejected.** Erroring on `@elif` after `@else` (the scenario-row reading —
diverges compile-time from runtime semantics and mislabels a defined behavior); erroring
on `@else` after a custom publisher (breaks the ratified public protocol); runtime
silent-skip for orphan `@else` (hides authoring bugs; the roadmap ratified the error).

### D10 — Compile-time scan: two machines (strip and orphan) in one document-order pass

**Decision.** One new private method,
`HeddleCompiler.ProcessBranchSets(ParseContext parseContext, CompileScope compileScope, ref string workingDocument)`,
called in `Compile` immediately after `ReplaceRawOutput` (line 30) and before the
`OutputChains` compile loop (line 32) — the point where chain `BlockPosition`s and
`workingDocument` are position-consistent. Because `Compile` runs once per body over that
body's own `ParseContext` (verified — `OutputItem.Context` per body, offsets body-relative),
the scan is per-body-level with zero extra bookkeeping; `DefaultChains` are exempt (single
synthetic chains at document end — nothing to strip or orphan-check).

**Block classification** (per chain, in document order): take the chain's **leftmost item**
(`chain.Chain[0]`; empty chains classify Other). If
`chain.Context.DefenitionExists(name)` → **Other** (definition shadowing mirrors
`CompileItem`). Else by name, ordinal: `if`/`ifnot` → **Opener**; `elif`/`elseif` →
**Continuation**; `else` → **Terminal**. Else, when
`TemplateFactory.TryGetExtensionType(name, out var t)` (new internal accessor) succeeds and
`t` carries `[ScopeChannel]` → **Participant**; otherwise **Other**. Branch extensions used
*not* as the leftmost item (e.g. `@out():else()`) are composition, invisible to the scan,
governed at runtime by D8/D9.

**Strip machine** (adjacency + HED3001). A *set*, for stripping, is an Opener (or a
Continuation acting as one) followed by consecutive Continuations and at most one Terminal;
a Terminal completes the set; any Other/Participant block, and any new Opener, ends the
current set (a new Opener starts its own). For each **adjacent pair inside one set**, the
gap is `[prev.BlockPosition.StartIndex + prev.BlockPosition.Length, next.BlockPosition.StartIndex)`
in working coordinates (guard: skip non-positive gaps — zero-length imported blocks make
them possible). If the gap text is non-whitespace, emit warning **HED3001** positioned at
the *following* block's first `OutputItem.Position` (parse coordinates — the same position
semantics as every existing diagnostic), exactly one warning per stripped region. Then
remove the gap with `ExStringBuilder.ApplyRemove(new BlockPosition(gapStart, gapLength), ref workingDocument)`
and shift every later chain's `BlockPosition` back by the removed length using the verified
`RemoveEmptyItem` reverse-loop pattern (only `OutputChains` need shifting at this stage:
skipped tokens, definitions, and raw items were already consumed by the three preprocessing
steps). Whitespace-only gaps are stripped silently — normally formatted multi-line sets
stay warning-free. Comments between blocks were excised before the scan ever runs
(`tree.GetText()`), so they can neither render nor break adjacency. The stripped span never
reaches `RuntimeDocument.GetDocumentPieces` — rendering it is impossible by construction.
Text before the Opener and after the set's last block is untouched.

**Orphan machine** (HED3002/HED3003/HED3004) — mirrors the *runtime* frame, in which
non-branch blocks do **not** clear state: track `state ∈ {None, Open, Closed, Unknown}`
across the same pass. Opener → `Open`. Continuation: from `None`/`Closed` → warn HED3002,
then `Open`; from `Open`/`Unknown` → `Open`. Terminal: from `None`/`Closed` → error
HED3003 (state unchanged, so a third orphan `@else` errors again); from `Open`/`Unknown` →
`Closed`; additionally HED3004 when its leftmost item has a non-empty parameter — precisely:
any shape other than the one that compiles to `EmptyParameter`, i.e. other than
`CallParameter.IsModelTypeParameter` (`ChainParameter == null && CSharpExpression == null`,
[CallParameter.cs](../../../src/Heddle/Language/CallParameter.cs) line 19) **with**
`ModelParameter` absent or its first segment empty (the exact test `CompileItem` applies,
[HeddleCompiler.cs](../../../src/Heddle/Runtime/HeddleCompiler.cs) lines 277–297); member
paths, native expressions, C# expressions, and nested chains all warn. Participant →
`Unknown`. Other →
**state unchanged** (an intervening `@date(...)` block ends *stripping* adjacency but at
runtime leaves the frame intact — `@else` after it still legally binds to the open set;
the published docs state "‘@else’ binds to the set state, not to a specific `@if`").
**Rationale.** The two machines answer two different ratified questions — "which text is
swallowed" (adjacency, industry-standard surface) and "which blocks are statically
erroneous" (a conservative mirror of the runtime state machine) — and conflating them
produces either wrong stripping or false orphan errors; the roadmap's "a directive between
branches ends the set" statement is a strip-machine rule, while its error definition ("no
preceding `@if`… or the set was already closed by an `@else`") is an orphan-machine rule,
and both are honored verbatim.
**Alternatives rejected.** One machine where Other blocks reset orphan state (false
HED3003 for `@if … @date(...) … @else`, which runs correctly today's-runtime-wise);
stripping around Participant blocks (their text semantics are unknown — swallowing text
next to a custom extension is unjustifiable); scanning before `ShiftBySkippedTokens`
(original coordinates, but the working document is already comment-free, so gap detection
would misalign); rescanning after `@import()`'s compile-time parse (those chains join a
context whose scan already ran — recorded as a documented limitation in D16 instead of a
re-entrant scan).

### D11 — `@out()` projection: every body owns its frame; splices carry no state

**Decision.** Closing roadmap pin 1: the caller-content body of a definition invocation
(`@mydef(X){{caller content}}`) executes under **its own fresh frame** (it is a body —
funnel entry E4), the definition's body under its own (E5), and a bodied `@out(){{…}}`
projection under its own (E9); a bodiless `@out()` splices the already-rendered string and
executes nothing. No branch state crosses any of these boundaries in either direction.
This generalizes the roadmap's lean ("the frame follows the `Scope` it renders under") to
the uniform rule the lean was reaching for: *fresh frame per body, no exceptions*.
Consequences pinned by `branching-out-projection.heddle`: a set inside caller content and a
set inside the definition body are independent; an `@if` before `@out()` and an `@elif`
after it in the same definition body are **the same body level** — the `@out()` block is a
non-branch block, so stripping stops at it while the set state persists across it (D10).
**Rationale.** Uniformity eliminates the projection special case entirely; the mechanism
(strings spliced via `ChainedData`) never carried a `Scope`, so there is nothing a frame
could even attach to.
**Alternatives rejected.** Executing projected content in the invocation site's sibling
frame (would let caller-side branch state leak into definition internals — the exact
cross-body coupling D5 forbids).

### D12 — `@partial()` fresh root frame; `@swap()` via the funnel; `@param()` corrected

**Decision.** Closing roadmap pin 2 and the `@swap`/`@param` resolution note:
`@partial()` gets a fresh **root** frame with no code change —
`PartialExtension.ProcessData/RenderData` call `InnerTemplate?.Generate(...)`, which
constructs a brand-new root `Scope`, so the caller's frame cannot cross (fixture pair
`branching-partial-parent/child.heddle` proves it, including the negative: an orphan
`@else` in the child is a positioned HED3003 from the child's own compile). `@swap(){{…}}`
is an ordinary funnel body (fresh/cleared frame, entry E10). ⚠ `@param()` — corrected: it
**never executes a body** (verified: `InitStart` returns without compiling one, line 11),
so the roadmap's "fresh frame for `@param()` bodies" note has nothing to attach to; the
inventory records it as verified non-entry N6.
**Rationale.** Both pins resolve to "consistency wins" with source evidence; the `@param`
correction removes a phantom work item.
**Alternatives rejected.** None viable — the partial path is structurally fresh.

### D13 — Registry names: `elif` + `elseif` on one class, `else` on another

**Decision.** `ElifExtension` carries `[ExtensionName("elif")]` and
`[ExtensionName("elseif")]` (the registry yields one entry per attribute —
`AllowMultiple = true`, verified by phase 2's D5 precedent); `ElseExtension` carries
`[ExtensionName("else")]`. All three names verified free in the current registry (assumed
state). A user assembly exporting any of them via `[ExportExtensions]` now throws
`TemplateOverrideException` at static-init time — release note, same treatment as
phase 2's `raw`/`profile`. The scan (D10) and docs treat `elseif` as a pure alias; diagnostics
echo whichever spelling the template used (the parsed `ExtensionName`).
**Rationale.** Roadmap-ratified surface; one class per behavior, two spellings for the
two conventions users arrive with (Python/Jinja `elif`, C#/VB `elseif`).
**Alternatives rejected.** Separate `ElseIfExtension` class (duplicate with no divergent
behavior); `else if` with a space (impossible — extension names are single identifiers,
and zero-grammar-change is a phase invariant).

### D14 — `@else` takes no condition; a supplied parameter warns and is ignored

**Decision.** `@else(X)`, `@else(@expr)`, and `@else(a():b())` compile (zero grammar
change means the parameter parses like any call parameter), the parameter value is
evaluated by the normal item dispatch and **ignored** by `ElseExtension`, and the scan
emits warning **HED3004** at the block ("use `@elif(...)` for a conditional branch").
`@else()` with empty parens is the canonical form.
**Rationale.** The parameter slot cannot be removed without grammar work; silently
accepting `@else(IsArchived)` would read like an `@elif` and mislead — a positioned
warning converts the likeliest authoring mistake into guidance.
**Alternatives rejected.** Hard error (a template that renders deterministically should
not fail compilation for a lint-grade mistake); honoring the parameter as a condition
(that *is* `@elif` — duplicating it under a second name creates two spellings with
different set semantics, since `@else` is terminal).

### D15 — Allocation budget and `ScopeLocals` shape

**Decision.** The recorded budget: **zero** new render-path allocations and zero extra
struct copies for templates without channel participants (all existing templates — D2 fast
path + D7); **one `ScopeLocals` allocation per body execution of a participating body**
(e.g. one per `@list` iteration whose body contains `@elif`/`@else`/custom participants),
plus one `Dictionary<string, object>` only when a custom key is first published
(the branch protocol never touches the overflow map). The frame provably escapes the
`in Scope` dispatch surface, so no JIT rescue is budgeted
([cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)).
Concretely: a `ScopeLocals` instance is **32 bytes on x64** — 16 B object overhead
(header + method-table pointer, grounding in [External references](#external-references)) +
8 B `Dictionary<string, object>` overflow reference + 2 B `BranchState?` slot
(`bool` value + `bool` hasValue), padded to the 8-byte boundary — so the documented cost
of a 10 000-iteration participating list body is exactly 10 000 × 32 B = **320 000
allocated bytes per render** and nothing else (no overflow map, no boxing). These are the
acceptance numbers the
[benchmark table](#benchmark-definitions-and-acceptance-numbers) carries.
Guarded by the named benchmarks `BranchRenderBenchmarks.RenderListNoBranches` (allocation
parity with the pre-phase baseline) and `RenderListIfElse10K` vs `RenderListIfPair10K`
(the documented per-iteration frame cost of the new pattern against the old
`@if`+`@ifnot` pair; acceptance per the
[regression-gate benchmark rule](../common/testing-standards.md#regression-gates), with the
allocation delta between those two being this design's ratified, documented cost — not a
regression of an existing path).
**Rationale.** The roadmap's .NET 10 analysis is adopted wholesale: inline slot + lazy
overflow beats a dictionary-first frame by two allocations on the hot path and is identical
on every TFM.
**Alternatives rejected.** `Dictionary`-only frame (three allocations at first publish);
generic key/value inline slot pairs beyond the branch slot (no second hot consumer —
revisit trigger recorded); `FrozenDictionary`/`GetAlternateLookup` (wrong tools —
mutable single-body lifetime, literal keys; verified negatives carried from the roadmap).

### D16 — Imports: `@<<` splices into the importing body level; `@import()` is compile-time only

**Decision.** `@<<{{path}}` (parse-time import) appends the imported document's chains to
the importing context's `OutputChains` as zero-length blocks at the import position
(verified — `ExitImport_block`), so imported blocks are **siblings of the importing body**:
they share its frame at runtime and participate in its D10 scan (gap arithmetic guards the
zero-length positions; nothing is ever stripped around them since their gaps are
non-positive). `@import()` (the extension) acts entirely at compile time — its body (the
path text) is a compile-time funnel execution (inventory E14) and its render members are
no-ops; blocks it parses in join a context whose scan has already run, so branch blocks
arriving via `@import()` coordinate **at runtime only** (the D8 protocol still governs
them; static stripping/orphan diagnostics do not reach them). Both facts are documented in
`built-in-extensions.md` by WI8.
**Rationale.** Imports are textual splicing, not body descent — introducing frames there
would break the phase 4-verified fact that `@<<` content behaves as if written in place.
The `@import()` scan limitation is the reversible, honest option: a re-entrant scan inside
the compile loop would mutate `OutputChains` mid-iteration (the exact hazard the current
code structure avoids).
**Alternatives rejected.** Fresh frames per imported document (imported `@elif` could
never continue an importing-side set — surprising and useless); deferring the whole scan
until after all `InitStart`s (would require a second document-surgery pass over an
already-sliced document).

### D17 — Docs deliverable

**Decision.** [built-in-extensions.md](../../built-in-extensions.md) gains `elif`/`elseif`
and `else` entries (Conditions group), a "Branch sets" subsection documenting adjacency,
stripping, set-state binding ("`@else` binds to the set state, not to a specific `@if`"),
the orphan rules, and the import notes from D16.
[custom-extensions.md](../../custom-extensions.md) gains the channel section:
`Publish`/`TryRead`, `[ScopeChannel]`, the reserved-key scheme, the thread-safety
contract, the funnel contract note (bodies execute only through
`GetInnerResult`/`RenderInnerResult`), and two worked examples — a publisher/consumer pair
(zebra striping) and a `BranchState` participant that satisfies a set — both maintained as
executable doc tests. [language-reference.md](../../language-reference.md) gains a short
"Branching" subsection under control flow linking to both.
[csharp-api.md](../../csharp-api.md) is untouched (the channel is extension-author API;
its home is `custom-extensions.md`).
**Rationale.** Matches the API-docs rule in the
[coding standards](../common/coding-standards.md#api-design-and-compatibility) and the
roadmap's docs size estimate; executable doc tests keep criterion 7's samples honest.
**Alternatives rejected.** A dedicated branching page (the material is section-sized;
phase 1's `native-expressions.md` precedent applied to a much larger corpus).

## Implementation plan

Work items in execution order; each runs the
[canonical loop](../common/testing-standards.md#the-canonical-loop). "Check" is the
completion gate.

**WI1 — Failing executable spec (protocol + inventory first).**
Files: `src/Heddle.Tests/BranchProtocolTests.cs` (the D8 enumeration P01–P21 plus the D9
runtime column as `[Theory]` rows: pre-state × extension × condition →
rendered/published/cleared/error, observed through a `[ScopeChannel]` test-reader
extension),
`src/Heddle.Tests/BranchIsolationTests.cs` (one failing fixture per
[entry-points.md](entry-points.md) row E1–E14 — including the two funnel white-box rows
E2/E3 and both E12 body kinds — plus the N1–N7 sharing/non-entry proofs),
`src/Heddle.Tests/ScopeChannelTests.cs` (D3 semantics rows),
`src/Heddle.Tests/BranchSetCompilerTests.cs` (the D10 stripping/diagnostic corpus),
fixtures `branching-flagship.heddle`, `branching-interleaved.heddle`,
`branching-list-alternating.heddle`, `branching-nested.heddle`,
`branching-out-projection.heddle`, `branching-partial-parent.heddle`,
`branching-partial-child.heddle` under `src/Heddle.Tests/TestTemplate/` (goldens captured
in WI6). Check: all new tests fail for the right reason (`Cannot find extension <elif>`,
missing members); suite compiles.

**WI2 — `Scope` promotion + channel types (D1, D3, D4, D6).**
Files: `src/Heddle/Data/Scope.cs` (declare `readonly struct`; add
`internal readonly ScopeLocals Locals` + ctor parameter; copy in all six transforms; add
`WithLocals`, `Publish`, `TryRead`, and internal `PublishBranch`/`TryReadBranch`/`ClearBranch`),
`src/Heddle/Data/ScopeLocals.cs` (new internal: branch slot + lazy overflow map),
`src/Heddle/Data/BranchState.cs` (new public),
`src/Heddle/Attributes/ScopeChannelAttribute.cs` (new public).
Check: `ScopeChannelTests` API rows green (reserved keys, null key/value, last-write-wins,
frameless throw/false); existing suite green byte-identical (the promotion and the unused
field are inert).

**WI3 — Frame provisioning + installation (D2, D5).**
Files: `src/Heddle/Runtime/RuntimeDocument.cs` (compute `internal bool NeedsLocals` in the
ctor: the D2 walk over `DocumentElement[]`, recursing `ChainedParameter` chains),
`src/Heddle/Runtime/Parameters/ChainedParameter.cs` (`internal IDataProcessor Processor`
accessor for the walk), `src/Heddle/Core/AbstractExtension.cs` (capture `_needsLocals` in
`InitStart`; install frames in `GetInnerResult`/`RenderInnerResult` with the fast path),
`src/Heddle/HeddleTemplate.cs` (root frame at both `Generate` scope sites from
`_runtimeDocument.NeedsLocals`).
Check: `BranchIsolationTests` rows green using the WI1 test-reader/test-publisher
extensions (built-ins not yet wired); `RenderListNoBranches` benchmark shows byte-equal
allocations.

**WI4 — Branch extensions (D7, D8, D13, D14 runtime half).**
Files: `src/Heddle/Extensions/BranchCondition.cs` (new internal helper),
`src/Heddle/Extensions/IfExtension.cs` / `IfNotExtension.cs` (publish via
`PublishBranch` no-op path; rewire predicates to the helper — behavior byte-identical),
`src/Heddle/Extensions/ElifExtension.cs`, `src/Heddle/Extensions/ElseExtension.cs` (new;
shapes in the [API contract](#public-api-contract); `@else`'s runtime orphan throw is
`TemplateProcessingException`).
Check: `BranchProtocolTests` table fully green, including chained/nested-chain composition
rows and the `@ifnot`+`@else` row; concurrency test not yet required (WI7).

**WI5 — Compile-time scan (D9, D10, D14 compile half, D16).**
Files: `src/Heddle/Runtime/HeddleCompiler.cs` (`ProcessBranchSets` + call after
`ReplaceRawOutput`), `src/Heddle/Runtime/TemplateFactory.cs`
(`internal static bool TryGetExtensionType(string name, out Type type)`),
`src/Heddle/Data/HeddleDiagnosticIds.cs` (four constants).
Check: `BranchSetCompilerTests` green — the full
[compile-scan corpus](#compile-scan-corpus-branchsetcompilertests-normative) C01–C19
(stripping: whitespace-only silent, non-whitespace warned once per gap with exact
position, comments/`@\` gaps intact sets, directive-between-branches ends stripping but
not the orphan state, CRLF/LF variants, two-sets scenario, imported zero-length blocks,
definition-shadowed names skipped, `@else(X)` HED3004), all orphan rows from the D9 table
with positioned IDs.

**WI6 — Wire success criteria: goldens + negatives.**
Files: goldens `generated-branching-flagship-featured.html` / `-archived.html` /
`-regular.html`, `generated-branching-interleaved-*.html` (byte-identical to the flagship
outputs), `generated-branching-list-alternating.html`, `generated-branching-nested.html`,
`generated-branching-out-projection.html`, `generated-branching-partial.html`.
Check: flagship renders exactly one branch and nothing else per condition state; the
interleaved variant renders identically with exactly one warning per stripped region
(criteria 1); isolation fixtures green (criteria 3, 4); negative rows (orphans) green
(criterion 5).

**WI7 — Concurrency, benchmarks, combined gate.**
Files: `src/Heddle.Tests/BranchConcurrencyTests.cs` (parallel renders of one compiled
template with opposite conditions — criterion 6),
`src/Heddle.Performance/BranchRenderBenchmarks.cs` (new, `[MemoryDiagnoser]`:
`RenderListNoBranches`, `RenderListIfPair10K`, `RenderListIfElse10K`,
`RenderFlagshipNoPublish`).
Check: the full [regression gate](../common/testing-standards.md#regression-gates) in one
combined run — build all TFMs; full suite (incl. `net48` on Windows) byte-identical;
**grammar stability**: `src/Heddle.Language/generated/` has no diff (criterion 2);
existing render benchmarks (`TextRenderBenchmarks`) unchanged within reported error and
allocations not increased; the new benchmarks meet the per-benchmark acceptance numbers
in the [benchmark table](#benchmark-definitions-and-acceptance-numbers) (zero-delta rows
at exactly 0 B; `RenderListIfElse10K` at exactly the documented 320 000 B frame delta)
and are recorded as the phase baseline (criterion 8).

**WI8 — Published docs + executable doc examples (D17, criterion 7).**
Files: `docs/built-in-extensions.md`, `docs/custom-extensions.md`,
`docs/language-reference.md` (per D17);
`src/Heddle.Tests/ScopeChannelDocExampleTests.cs` (compiles and runs both
`custom-extensions.md` samples, including the `BranchState` participant driving a set).
Check: `cd docs && npm run docs:build` passes (uppercase-drive cwd on Windows); doc-sample
tests green; criterion 7 asserted.

## Public API contract

All additions are additive
([API design rules](../common/coding-standards.md#api-design-and-compatibility)); XML docs
required on every member below. Thread-safety notes inline.

```csharp
namespace Heddle.Data
{
    /// <summary>Per-node render data view. Now a declared readonly struct: all members
    /// are pure transforms; instances are passed by 'in' and must not be stored beyond
    /// the call that received them (frames are single-threaded per render lineage).</summary>
    public readonly struct Scope
    {
        /// <summary>Publishes a value into the current body's local context frame for
        /// later sibling extensions (document order) to read. Last write wins. Keys are
        /// ordinal; the "heddle." prefix is reserved — only BranchState.ReservedKey is
        /// accepted from it, and its value must be a BranchState.
        /// Throws InvalidOperationException when no frame exists — mark the publishing
        /// extension with [ScopeChannel] so bodies containing it get a frame.</summary>
        public void Publish(string key, object value);

        /// <summary>Reads a value published earlier in the current body execution.
        /// Returns false (never throws) when the key is absent or no frame exists.
        /// Frames never cross body boundaries: nested bodies, partials, and each
        /// list/for iteration start fresh.</summary>
        public bool TryRead(string key, out object value);

        // Internal channel members are specified member-by-member in
        // "Internal additions" below — nothing else on Scope changes.
    }

    /// <summary>The public branch-set protocol state, published under ReservedKey by
    /// @if/@ifnot/@elif and consumed/cleared by @else. Custom extensions may read it to
    /// render alongside a set or publish it to drive one. Immutable value type.</summary>
    public readonly struct BranchState
    {
        /// <summary>The reserved channel key of the branch protocol: "heddle.branch".</summary>
        public const string ReservedKey = "heddle.branch";

        /// <summary>Creates a state with the given satisfaction. Publish it under
        /// ReservedKey to drive a set (e.g. a custom matcher publishing
        /// new BranchState(true) so a following @else stays silent).</summary>
        public BranchState(bool satisfied);

        /// <summary>True when some branch of the current set has already fired
        /// (condition semantics — an empty rendered body still satisfies the set).</summary>
        public bool Satisfied { get; }
    }

    public static class HeddleDiagnosticIds
    {
        public const string BranchTextStripped   = "HED3001";
        public const string ElifWithoutIf        = "HED3002";
        public const string ElseWithoutIf        = "HED3003";
        public const string ElseConditionIgnored = "HED3004";
    }
}

namespace Heddle.Attributes
{
    /// <summary>Declares that an extension publishes to or reads from the Scope local
    /// context channel. Bodies containing such an extension are provisioned with a
    /// locals frame at compile time; without it, Scope.Publish throws and Scope.TryRead
    /// returns false. Inherited by derived extensions. Checked at compile time only.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ScopeChannelAttribute : Attribute { }
}

namespace Heddle.Extensions
{
    /// <summary>Continues a branch set: renders its body when no earlier branch of the
    /// set fired and its own condition is truthy; publishes the updated BranchState.
    /// With no set open it behaves exactly like @if (and draws HED3002 where statically
    /// visible). Stateless — safe for concurrent renders.</summary>
    [ExtensionName("elif")]
    [ExtensionName("elseif")]
    [ScopeChannel]
    public class ElifExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType,
            ExType chainedType, ExType parent);   // body compiles against parent, like @if
        public override object ProcessData(in Scope scope);
        public override void RenderData(in Scope scope);
    }

    /// <summary>Terminal branch: renders its body when no earlier branch of the set
    /// fired, then clears the set state (publishes nothing). With no set open it is the
    /// "no matching @if" error — HED3003 at compile time where statically visible,
    /// TemplateProcessingException otherwise. Its parameter is ignored (HED3004).
    /// Stateless — safe for concurrent renders.</summary>
    [ExtensionName("else")]
    [ScopeChannel]
    public class ElseExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType,
            ExType chainedType, ExType parent);   // body compiles against parent, like @if
        public override object ProcessData(in Scope scope);   // non-rendering rows return string.Empty (D8)
        public override void RenderData(in Scope scope);
    }

    // IfExtension / IfNotExtension: no signature changes; they publish BranchState
    // opportunistically (no-op without a frame, D7) and share BranchCondition.IsTruthy.
    // Deliberately NOT [ScopeChannel] — they alone never provision a frame.
}
```

**Internal additions** — the complete non-public surface this phase adds, with the exact
signature and contract each implementer-facing member carries (no "and friends" — this
list is exhaustive):

| Member | Signature | Contract / thread-safety |
| --- | --- | --- |
| `Scope.Locals` | `internal readonly ScopeLocals Locals;` + trailing optional ctor parameter `ScopeLocals locals = null` | The frame reference (D1). Copied unchanged by all six existing transforms (`Parent()` ×2, `Chain`, `Model` ×2, `RenderProxy`); `Scope.Null` keeps it `null`. Never exposed publicly. |
| `Scope.WithLocals` | `internal Scope WithLocals(ScopeLocals locals)` | Returns a copy with only `Locals` replaced (all six data fields carried over). Called **only** by the three D2 installation points. `[MethodImpl(AggressiveInlining)]`, mirroring the sibling transforms (performance section). |
| `Scope.PublishBranch` | `internal void PublishBranch(in BranchState state)` | Zero-boxing branch-slot write. Silent no-op when `Locals == null` — the D7 opportunistic path; safe because a frameless body provably has no reader. |
| `Scope.TryReadBranch` | `internal bool TryReadBranch(out BranchState state)` | `false` when `Locals == null` or the slot is empty; never throws. |
| `Scope.ClearBranch` | `internal void ClearBranch()` | Empties the slot (P20/P21); no-op when `Locals == null`. |
| `ScopeLocals` | `internal sealed class ScopeLocals` in new file `src/Heddle/Data/ScopeLocals.cs`; fields `private BranchState? _branch;` and `private Dictionary<string, object> _overflow;`; members `internal void SetBranch(in BranchState state)`, `internal bool TryGetBranch(out BranchState state)`, `internal void ClearBranch()`, `internal void Set(string key, object value)`, `internal bool TryGet(string key, out object value)` | The D4 frame: dedicated slot + overflow map created lazily on the first `Set` with `StringComparer.Ordinal`. All key **validation** (null key, reserved prefix, `BranchState` type check) lives in `Scope.Publish`/`TryRead` — the frame trusts its callers. Unsynchronized by design: one instance belongs to one body execution of one render lineage (D3). Instance size 32 B on x64 (the D15 arithmetic). |
| `RuntimeDocument.NeedsLocals` | `internal bool NeedsLocals { get; }` | Computed once in the constructor by the D2 walk over `DocumentElement[]` (recursing `ChainedParameter` chains in both collapsed and chain shapes); immutable afterwards — safe to read from concurrent renders. |
| `AbstractExtension._needsLocals` | `private bool _needsLocals;` | Captured in `InitStart` as `_subTemplate?.NeedsLocals ?? false`; written once at compile time, only read at render — the same once-written discipline as `_innerResult` (thread-safety summary below). |
| `ChainedParameter.Processor` | `internal IDataProcessor Processor => _callParameterChain;` | Exposes the collapsed-or-chain processor (ctor lines 16–23 of [ChainedParameter.cs](../../../src/Heddle/Runtime/Parameters/ChainedParameter.cs)) to the `NeedsLocals` walk. Read-only projection of an existing readonly field. |
| `TemplateFactory.TryGetExtensionType` | `internal static bool TryGetExtensionType(string name, out Type type)` | Ordinal `TryGetValue` over the static registry dictionary ([TemplateFactory.cs](../../../src/Heddle/Runtime/TemplateFactory.cs) line 34). Compile-time only (the D10 Participant classification); the registry is populated once in the static constructor, so concurrent compile-time reads are safe. |
| `HeddleCompiler.ProcessBranchSets` | `private static void ProcessBranchSets(ParseContext parseContext, CompileScope compileScope, ref string workingDocument)` | The D10 two-machine scan; called once per `Compile` invocation between `ReplaceRawOutput` and the chain-compile loop. Compile-time only. |
| `BranchCondition.IsTruthy` | `internal static bool IsTruthy(object value)` in new file `src/Heddle/Extensions/BranchCondition.cs` | `value != null && (!(value is bool b) \|\| b)` — extracted verbatim from today's `IfExtension`/`IfNotExtension` (D8). Pure static; no state. |

Thread-safety summary: extension instances stay stateless across renders — the four branch
extensions add **no instance fields** (`_needsLocals` on `AbstractExtension` is written
once at `InitStart` and only read at render, like `_innerResult`); all per-render state
lives in `ScopeLocals` instances created inside a single render lineage and never shared;
`[ScopeChannel]` reflection happens at compile time only. The parallel-render test (WI7)
is the standards-mandated proof.

## Diagnostics

Claimed from the `HED3xxx` block
([registry](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx));
consistent with prior claims (`HED0001`–`0003`, `HED1001`–`1017`, `HED2001`–`2003` taken —
verified against both prior specs). This phase touches no pre-existing diagnostic message,
so no new `HED0xxx` ID is claimed. Positions are `OutputItem.Position` values — the same
parse-coordinate semantics as every existing item diagnostic. `<…>` are message
parameters. `Fix` text exists only on the warning rows because only
[`HeddleCompileWarning`](../../../src/Heddle/Data/HeddleCompileWarning.cs) declares a
`Fix` property (line 4); [`HeddleCompileError`](../../../src/Heddle/Data/HeddleCompileError.cs)
has no such member (verified) — HED3003's remedy therefore lives in its docs entry (D17),
not on the diagnostic object.

| ID | Severity | Message text | Trigger / position |
| --- | --- | --- | --- |
| HED3001 | Warning | `Text between branch blocks is never rendered.` `Fix`: `Move it before the '@if', after the last branch, or into a branch body.` | A stripped set-internal gap contains non-whitespace (D10 strip machine). Exactly one warning per stripped region. Position: the first item of the branch block **following** the gap. Whitespace-only gaps strip silently; comments/`@\` never appear in gaps (hidden channel, excised pre-scan). |
| HED3002 | Warning | `'@<name>' has no preceding '@if' or '@ifnot' in this scope — it starts a new branch set, behaving like '@if'.` `Fix`: `Open the set with '@if(...)' or '@ifnot(...)', or use '@if' directly if a standalone condition is intended.` | `elif`/`elseif` block with orphan state `None`/`Closed` (D9) — including after an `@else`. `<name>` echoes the spelling used. Position: the block's first item. Suppressed when a `[ScopeChannel]` participant precedes (state `Unknown`). |
| HED3003 | Error | `'@else' has no matching '@if' in this scope.` | `else` block with orphan state `None`/`Closed` (first orphan, second `@else` of a set, `@else` after `@else`). Position: the block's first item. Suppressed when state is `Unknown`. The identical condition reached only at runtime (composition — e.g. a branch extension inside a chain) throws `TemplateProcessingException` with the same message. |
| HED3004 | Warning | `'@else' takes no condition — its parameter is ignored.` `Fix`: `Use '@elif(...)' for a conditional branch, or remove the parameter.` | `else` block whose leftmost item has any non-empty parameter shape (member path, expression, C#, or nested chain). Position: the block's first item. The parameter still compiles and evaluates; `ElseExtension` ignores its value. |

Runtime error (not an ID — render-time): `TemplateProcessingException` with the HED3003
message text, thrown by `ElseExtension` in both `ProcessData` and `RenderData` when no
`BranchState` is readable (D9, composition-only paths).

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md); suite homes,
fixture conventions (`branching-` stem, sibling goldens, line-ending normalization), and
gates apply as written there.

**TDD verdict (carried from the roadmap, unweakened).** Yes, emphatically — a state
machine plus a text transform: the D8/D9 protocol table is encoded as table-driven tests
*before* the extensions exist; the entry-point inventory doubles as the isolation-test
checklist written failing before frames are wired (converting the phase's top risk into a
visible red); the stripping scan is torture-fixture-first, and that corpus is written to
be shared with phase 4.3's directive-line trimming, which operates on the same
document-surgery seam.

**Named test assets** (all in [src/Heddle.Tests](../../../src/Heddle.Tests) unless noted):

| Asset | Content |
| --- | --- |
| `BranchProtocolTests` | The D8 enumeration P01–P21 as `[Theory]` rows (pre-state × extension × condition value → rendered output, post-state observed via a `[ScopeChannel]` test reader), including the evaluate-and-ignore rows P17/P18/P21; truthiness rows for all value categories (null, bool, non-bool, phase 1 typed-bool conditions); `@ifnot`+`@else` inversion; composition rows (branch extension chained and inside a nested chain parameter publishing into the enclosing frame); the custom-publisher-satisfies-set row (criterion 7); runtime orphan `@else` throwing `TemplateProcessingException` via a composition path (P19's runtime column). |
| `BranchIsolationTests` | One test per [entry-points.md](entry-points.md) row: root, the two funnel members themselves (white-box `Locals` assertions — fresh / cleared / untouched fast path, rows E2–E3), list/for iterations (alternating conditions), nested sets, out projection, swap, definition invocation + definition body, partial (incl. child orphan negative), rescoping/encoded containers, format bodies (both E12 kinds: the unconditional `@date` format body and the null-model `@string` default body), `Scope.Null` directive bodies, plus the N-row sharing proofs. |
| `ScopeChannelTests` | D3 semantics: last-write-wins, null values, null-key throws, reserved-prefix rejection, `BranchState` boxing round-trip through the public route, frameless `Publish` throw / `TryRead` false, publisher/consumer extension pair end-to-end; plus the E14 `Scope.Null` directive-body row and the N7 sandbox shape pin (`CompiledParameterDelegateCarriesNoScope`). |
| `BranchSetCompilerTests` | The D10 corpus, enumerated row-by-row in the [compile-scan corpus](#compile-scan-corpus-branchsetcompilertests-normative) below — every row asserts output bytes **and** the exact diagnostic set (ID + position), per the positioned-diagnostics rule in the testing standards. |
| `BranchConcurrencyTests` | Parallel renders of one compiled template with opposite per-thread conditions — zero cross-talk (criterion 6; the standards' model concurrency test). |
| `ScopeChannelDocExampleTests` | The two `custom-extensions.md` samples compiled and executed (executable doc test, criterion 7). |
| Fixtures/goldens | `branching-flagship.heddle` → `generated-branching-flagship-{featured,archived,regular}.html` (criterion 1); `branching-interleaved.heddle` → byte-identical outputs + exactly one warning per region; `branching-list-alternating.heddle`, `branching-nested.heddle`, `branching-out-projection.heddle`, `branching-partial-parent/child.heddle` with sibling goldens. Normative content and expected outputs per fixture in the [fixture definitions](#fixture-definitions-normative) below. |
| `BranchRenderBenchmarks` ([src/Heddle.Performance](../../../src/Heddle.Performance)) | `[MemoryDiagnoser]`: `RenderListNoBranches`, `RenderListIfPair10K` vs `RenderListIfElse10K` (criterion 8 + hot-loop scenario), `RenderFlagshipNoPublish`. Definitions and per-benchmark acceptance numbers in the [benchmark table](#benchmark-definitions-and-acceptance-numbers) below. |

### Fixture definitions (normative)

The content shapes below are normative for what each fixture must exercise; exact static
text is free within that shape (goldens are captured in WI6 and reviewed against these
expectations).

| Fixture | Content shape | Model / condition states | Expected |
| --- | --- | --- | --- |
| `branching-flagship.heddle` | Static HTML header, the three-block set from [Scope and goal](#scope-and-goal) (bodies `<b>Featured</b>` / `<i>Archived</i>` / `Regular`), static footer. Blocks separated by newline-only gaps (whitespace-only → stripped silently). | `{ bool IsFeatured, bool IsArchived }`: featured = `(true, *)`, archived = `(false, true)`, regular = `(false, false)` | Exactly one branch body present per golden, header/footer byte-identical across all three, zero diagnostics (criterion 1). |
| `branching-interleaved.heddle` | The flagship with distinctive non-whitespace text (e.g. `MUST-NOT-RENDER-1`, `MUST-NOT-RENDER-2`) in both intra-set gaps. | Same three states | Goldens **byte-identical to the flagship's**; exactly two HED3001 warnings, one per gap, each positioned at the following block's first item (criterion 1, second half). |
| `branching-list-alternating.heddle` | `@list(Items)` whose body holds an `@if`/`@elif`/`@else` set over per-element flags. | `Items` sequenced so consecutive iterations take the `if`, `elif`, and `else` branches in mixed order | Per-element outputs correct in sequence — proves the frame resets each iteration; no state bleeds between elements (criterion 3). |
| `branching-nested.heddle` | Outer set `@if(Outer){{ …inner complete set over InnerA… }}@else(){{OUTER-ELSE}}`. | `(Outer, InnerA)` ∈ `(true, true)`, `(true, false)`, `(false, –)` | `(t,t)` → inner if-body only; `(t,f)` → inner else-body only; `(f,–)` → `OUTER-ELSE` only. The inner set (including its terminal `@else` clearing its **own** frame) never unsatisfies or satisfies the outer set (criterion 4). |
| `branching-out-projection.heddle` | A definition whose body is `@if(A){{X}}@out()@elif(B){{Y}}@else(){{Z}}`; the invocation's caller content holds its own complete set. | `(A, B)` states covering each branch, caller-content conditions independent | Per D11: the definition-body set state persists across the non-branch `@out()` block (stripping stops at it, state does not); the caller-content set and the definition-body set are fully independent. |
| `branching-partial-parent.heddle` | Opens a set (`@if(A){{P-IF}}`), invokes `@partial(){{child}}`, then `@else(){{P-ELSE}}`. | Parent `A` × child condition states | The child renders by its own conditions only; the parent's `@else` still binds to the parent set across the `@partial` block (a non-branch Other block — state persists). No cross-contamination in either direction. |
| `branching-partial-child.heddle` | A complete, self-contained set over the child's model. | Child condition states | Correct standalone branch rendering under a fresh root frame (E13). The **negative** (orphan `@else` in a child → positioned HED3003 from the child's own compile) uses an inline child variant in the test, not this fixture — a fixture that fails compilation cannot carry a golden. |

### Compile-scan corpus (`BranchSetCompilerTests`, normative)

Template sketches use `A`/`B` for conditions and digits for bodies; every row asserts the
rendered bytes for both condition values **and** the exact diagnostic set (ID + position).

| # | Scenario (template sketch) | Expected |
| --- | --- | --- |
| C01 | `@if(A){{1}}`, newline, `@else(){{2}}` (whitespace-only gap) | Gap stripped silently; renders `1`/`2` by `A`; zero diagnostics. |
| C02 | `@if(A){{1}} STRAY @else(){{2}}` | Gap (incl. `STRAY`) stripped; **one** HED3001 positioned at the `@else` block's first item; output byte-identical to C01. |
| C03 | `before @if(A){{1}}@else(){{2}} after` | `before ` and ` after` render — stripping is set-internal only. |
| C04 | `@if(A){{1}} @* note *@ @else(){{2}}` | Comment excised before the scan (hidden channel); the remaining gap is whitespace-only → silent; set intact. |
| C05 | `@\`-trimmed whitespace between the blocks | Hidden channel, same as C04: set intact, no warning. |
| C06 | `@if(A){{1}}@using(){{System.Text}}@else(){{2}}` | The directive chain classifies Other → the stripping run ends at it, but the orphan state persists → compiles clean, `@else` binds; the directive chain is later removed by `RemoveEmptyItem` as today. |
| C07 | `@if(A){{1}} @date(D){{yyyy}} @else(){{2}}` | Other block splits the set for **stripping only**: the two gaps around `@date` are untouched and render; no diagnostics; `@else` still binds to the open set at runtime. |
| C08 | `@if(A){{1}}@else(){{2}} @if(B){{3}}@else(){{4}}` | Two independent sets — `1`/`2` per `A`, `3`/`4` per `B`; the inter-set space renders (it is outside both sets: the first `@else` completed set 1). |
| C09 | `@if(A){{1}}@if(B){{2}}@else(){{3}}` | The second Opener ends set 1 and starts set 2; `@else` binds to set 2 (runtime: the second `@if` overwrites the state — P05/P06). |
| C10 | C02 authored with CRLF and with LF line endings | Identical stripping, output, and diagnostic positions after the standard line-ending normalization. |
| C11 | `@if(A){{1}}@<<{{else-fragment}}` where the fragment holds `@else(){{2}}` | Imported chains join as zero-length blocks at the import position (N4): they participate in the scan, the non-positive gap guard skips them, nothing is stripped around them; `@else` binds; zero diagnostics. |
| C12 | A definition named `else` (`@%else …%@`) invoked standalone | `DefenitionExists` → Other: no HED3003, the definition renders (D10 classification mirrors `CompileItem`). |
| C13 | `@if(A){{1}}@else(X){{2}}` | HED3004 at the `@else` block's first item; the parameter compiles, evaluates, and is ignored; terminal behavior unchanged (renders `2` iff `A` falsy). |
| C14 | `@else(){{2}}` as the first block | HED3003 error, positioned at the block's first item; compilation fails. |
| C15 | `@if(A){{1}}@else(){{2}}@else(){{3}}` | HED3003 at the **second** `@else` (the first closed the set); a variant with a third orphan `@else` errors again at the third (state stays `Closed`/`None`). |
| C16 | `@elif(A){{1}}` with no opener | HED3002 warning; renders exactly as `@if(A){{1}}` (P13/P14). |
| C17 | `@if(A){{1}}@else(){{2}}@elif(B){{3}}@else(){{4}}` | HED3002 at the `@elif` (state `Closed`); it starts a **new** set; the second `@else` legally closes it — no HED3003 anywhere (the D9 reconciliation row). |
| C18 | A `[ScopeChannel]` custom test-publisher block, then `@else(){{2}}` | No diagnostic (state `Unknown` — the custom block may publish); at runtime the D8 protocol governs whatever state exists (criterion 7's static half). |
| C19 | An `@import()` whose parsed-in content holds `@else(){{2}}`, after `@if(A){{1}}` | No static diagnostic and no stripping (the parsed-in chains join after the scan ran — D16); the `@else` coordinates at runtime only (N5 pin). |

### Benchmark definitions and acceptance numbers

All four run under `[MemoryDiagnoser]`, before/after on one machine per the
[regression-gate benchmark rule](../common/testing-standards.md#regression-gates).
"0 B delta" means allocated bytes byte-equal to the pre-phase baseline of the same
benchmark; "within error" means mean time within BenchmarkDotNet's reported error.

| Benchmark | Template / loop | Acceptance |
| --- | --- | --- |
| `RenderListNoBranches` | `@list` over 10 000 items; body renders member outputs, **no** channel participants | **0 B** allocation delta; mean within error. Guards the D2 fast path + D7 (no existing template provisions a frame). |
| `RenderListIfPair10K` | 10 000 items; body uses the pre-phase idiom `@if(c){{…}}@ifnot(c){{…}}` | The old-pattern baseline for the trade; itself **0 B** delta vs pre-phase (no participants — the opportunistic publish is a frameless no-op). |
| `RenderListIfElse10K` | 10 000 items; body `@if(c){{…}}@else(){{…}}` | Mean within error of `RenderListIfPair10K`; allocation delta vs `RenderListIfPair10K` **exactly one `ScopeLocals` per iteration = 10 000 × 32 B = 320 000 B on x64** (D15 arithmetic) and nothing else — no overflow map (the protocol never touches it), no boxing (internal slot accessors). Any additional byte is a defect, not a ratified trade. |
| `RenderFlagshipNoPublish` | The flagship document rewritten in the pre-phase idiom (`@if`/`@ifnot` alternatives, no `elif`/`else`, no participants) — criterion 8's realistic never-publishes template | **0 B** allocation delta and mean within error vs the same template on the pre-phase engine — proves the `Scope` promotion (D1), the added field copy, and the opportunistic-publish no-op (D7) are free on a real document. |

**Regression gate** (one combined run, per the standards): build all TFMs
(`netstandard2.0;net6.0;net8.0;net10.0`, tests add `net48` on Windows); full suite with
every pre-existing golden byte-identical (no existing template forms a branch set, so any
diff is a stripping-scan bug by definition); **grammar-stability check** — `generated/`
has no diff (criterion 2, verified not assumed); `TextRenderBenchmarks` and the new
benchmarks before/after on one machine — allocated bytes not increased for existing
templates, mean within reported error. Red gate → fix code; a protocol-table change is a
design re-ratification, never a fix.

**Deferred phase 9 gallery item** (recorded per the standards): the
`samples/custom-extensions` demo — the public channel plus a branch-protocol participant,
golden-asserted in CI. Owned by this phase, delivered when the phase 9 harness exists.

## Back-compat and migration

Everything is additive; nothing lands in the
[2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window) from
this phase.

| Surface | Behavior | Proof / note |
| --- | --- | --- |
| All existing templates | Byte-identical output; allocation- and copy-identical render path (no participants → no frames, D2/D7) | Full suite + goldens; `RenderListNoBranches` |
| Grammar / `generated/` | Untouched | Gate rule 3 (grammar-stability check) |
| `Scope` consumers | `readonly struct` promotion + internal field are source- and binary-compatible; the entire dispatch surface already takes `in Scope` | Existing suite; D1 verification |
| `IfExtension`/`IfNotExtension` standalone | Behavior identical (shared helper extracted verbatim; publish is a frameless no-op) | `BranchProtocolTests` truthiness rows + existing goldens |
| New registry names `elif`, `elseif`, `else` | A user assembly exporting any of them via `[ExportExtensions]` now throws `TemplateOverrideException` at startup | Release note (same class as phase 2's `raw`/`profile`) |
| Orphan-`@else` error, stripping, warnings | New-extension behavior only — no existing template can contain a branch set | By definition + the untouched-suite gate |
| Templates naming a definition `else`/`elif` | Unaffected — definition shadowing wins in both the compiler and the scan (D10) | `BranchSetCompilerTests` shadow row |
| Hosts caching compiled templates | No options/identity change in this phase (`TemplateOptions` untouched) | No new completeness-test entries needed |

Migration notes shipped with the phase: the registry-collision release note and the
`built-in-extensions.md` branch-set semantics (notably "text between branch blocks is
stripped with a warning" for authors arriving from the pre-ratification draft behavior).

## Performance considerations

- **Render path, existing templates: untouched by construction.** No participants → no
  provisioning → the funnel's fast path passes the incoming `scope` through unchanged (no
  struct copy, no allocation, no branch beyond two null/bool checks per body call).
  Guarded by `RenderListNoBranches` + the existing `TextRenderBenchmarks` in the gate.
- **Render path, branch sets:** one `ScopeLocals` per participating body execution (D15
  budget), one `bool`-slot write per branch block, zero boxing on the protocol path (D4),
  and only the winning branch's body executes (short-circuit is inherent — no thunks or
  laziness machinery anywhere in this design). The 10k-iteration cost is measured by
  `RenderListIfElse10K` against the `RenderListIfPair10K` old-pattern baseline and
  recorded as the phase's documented trade.
- **`Scope` stays pass-by-`in`** everywhere; the struct grows by one pointer field
  (copied only inside the existing pure transforms, where "the cost of copying a value is
  negligible if the types are small" — grounding carried from the roadmap). The promotion
  removes even the theoretical defensive-copy class of costs.
- **Compile path:** one attribute walk per compiled document (reflection, compile-time
  only), one document-order scan with at most one `ApplyRemove` string rebuild per
  stripped gap — the same cost class as the existing `RemoveEmptyItem` directive removal;
  no benchmark budget needed beyond the standing compile-cost runners in
  [src/Heddle.Performance/Runners](../../../src/Heddle.Performance/Runners).
- No `[MethodImpl(AggressiveInlining)]` additions — `WithLocals` mirrors the existing
  transforms' attribute for consistency, but no new inlining claims are made without a
  benchmark.

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made in this phase:

- **DRY where knowledge must not diverge:** truthiness exists once
  (`BranchCondition.IsTruthy`) and is consumed by all four branch extensions — the
  divergence between `@if` and `@elif` semantics would be a protocol bug, not a style
  issue. The stripping reuses `ApplyRemove` + the `RemoveEmptyItem` shift pattern instead
  of a second position-arithmetic implementation.
- **Open/closed at the sanctioned seam:** the channel is the roadmap-ratified open point
  (branching is deliberately its *first consumer*, not a compiler special case);
  participation arrives as an attribute exactly like the existing registry metadata rather
  than a new plugin mechanism; `ProcessBranchSets` stays a private method of the cohesive
  compiler step (no ceremony classes to satisfy SRP's letter).
- **YAGNI cutting scope:** no key removal/enumeration API, no `TryPublish`, no
  parent-frame fallback reads, no generic inline slots beyond the branch slot, no
  auto-published outcome records, no re-entrant scan for `@import()` — each with a trigger
  below.
- **Interface segregation / FDG:** the capability marker is an attribute, not an empty
  interface (CA1040), and no member was added to any shipped interface (`IExtension`,
  `IScopeRenderer` untouched — the binary-compat rule).
- **Measured hot-path performance over abstraction:** frame provisioning is compile-time
  precomputation feeding two branchy-but-allocation-free checks at render, chosen over the
  "cleaner" always-allocate design specifically because precedence rule 4 demands the
  benchmark-visible outcome; conversely the strict body isolation (D5) costs one struct
  copy in provisioned lineages and was kept because predictable semantics beat that
  micro-cost (rule 3 over rule 4).
- **Correctness/back-compat first:** D7's opportunistic publish exists solely so existing
  templates keep an allocation-identical render path — the design bends toward rule 1, and
  the elision is proven unobservable rather than assumed.

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Auto-published outcome record for every sibling item (`{extension name, produced output}`) | Real custom-extension usage of the channel demonstrating the need (roadmap "recorded direction, not v1") |
| Channel key removal / enumeration API | A consumer that must retract published state; today last-write-wins covers the known patterns |
| Generic inline key/value slots in `ScopeLocals` beyond the branch slot | A measured hot multi-key consumer of the general channel (D15) |
| Parent-frame fallback reads (`TryReadIncludingAncestors`) | A ratified scenario for cross-body ambient state; would need its own clobbering analysis (D5) |
| Static scan coverage for blocks parsed in by `@import()` at compile time | Evidence of real templates splitting branch sets across `@import()` (D16); `@<<` already participates |
| Adjacency/branch lints beyond HED3002/3004 (e.g. unreachable `@elif` after a constant-true condition) | Phase 6 diagnostics/LSP |
| Frame pooling for hot-loop participating bodies | `RenderListIfElse10K` showing the per-iteration frame dominating a real workload |
| `samples/custom-extensions` gallery item | Phase 9 harness (testing plan) |

## External references

Primary sources verified for this spec (July 2026); the roadmap's grounding set was
re-checked where load-bearing.

- [Jinja2 — template designer documentation](https://jinja.palletsprojects.com/en/stable/templates/) — re-verified: `elif`/`else` are integral parts of `{% if %}…{% endif %}` ("can be used like in Python"); text "between" branches is grammatically inside a branch body. The adjacency precedent for D10.
- [Liquid — control flow tags](https://shopify.github.io/liquid/tags/control-flow/), [Handlebars — built-in helpers](https://handlebarsjs.com/guide/builtin-helpers.html), [Go text/template](https://pkg.go.dev/text/template) — carried from the roadmap's verified set: none of the four mainstream grammars can express text between branches.
- [Framework Design Guidelines — interface design](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/interface) and [CA1040 — avoid empty interfaces](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1040) — avoid marker interfaces; use a custom attribute (D6).
- [Avoid memory allocations and data copies (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/) — `in`/`ref readonly` pass references; small-struct copy cost is negligible (D1, performance section).
- [The `in`-modifier and the readonly structs in C# (Microsoft DevBlogs)](https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/) — defensive-copy elimination is structural for declared readonly structs (D1).
- [What's new in .NET 10 runtime — stack allocation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#stack-allocation) and [dotnet/runtime — object stack allocation design](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/object-stack-allocation.md) — carried verified negative: the frame escapes through non-inlined `in Scope` dispatch; no JIT rescue (D2, D15).
- [Managed threading best practices (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices) — per-invocation state needs no synchronization; the D3 thread-safety contract's basis (carried from the roadmap).
- [Managed object internals, Part 1 — the layout of a managed object (Microsoft DevBlogs)](https://devblogs.microsoft.com/premier-developer/managed-object-internals-part-1-layout/) and [dotnet/runtime discussion #79190 — object header size](https://github.com/dotnet/runtime/discussions/79190) — 64-bit per-object overhead is 16 B before fields (4 B object header + 4 B alignment padding + 8 B method-table pointer), fields padded to the 8 B boundary; grounds the D15 `ScopeLocals` = 32 B instance-size arithmetic behind the 320 000 B benchmark acceptance number.
