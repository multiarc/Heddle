# Cross-cutting decisions

Shared technical decisions that more than one phase spec relies on. Each is a closed
decision in the [spec-conventions](spec-conventions.md#required-structure-of-a-phase-spec-entry-document) format; phase
specs link here instead of restating. Numbering is stable — later specs cite `D1`, `D2`, …

## D1 — Stable diagnostic IDs (`HEDxxxx`)

**Decision.** `HeddleCompileError` and `HeddleCompileWarning` gain a stable string
diagnostic ID of the form `HED` + four digits (e.g. `HED1003`), surfaced in `ToString()`
and available to tooling (phase 6 maps it to LSP `Diagnostic.code`). IDs are allocated in
per-phase blocks — the first digit is the owning phase:

| Block | Owner |
| --- | --- |
| `HED0001`–`HED0999` | Core engine (pre-existing errors/warnings, assigned as they are touched) |
| `HED1xxx` | Phase 1 — native expressions |
| `HED2xxx` | Phase 2 — safe output |
| `HED3xxx` | Phase 3 — branching |
| `HED4xxx` | Phase 4 — ergonomics |
| `HED5xxx` | Phase 5 — props & slots |
| `HED6xxx` | Phase 6 — tooling / LSP |
| `HED7xxx` | Phase 7 — build-time compilation |
| `HED8xxx` | Phase 8 — streaming & async |
| `HED9xxx` | Phase 9 — demo & integration |

Every phase spec's *Diagnostics* section claims concrete IDs from its block with message
text and trigger condition; an ID once shipped is never reused or renumbered.

**Rationale.** The roadmap's .NET 10 review flagged this as a cross-cutting action that
must exist before the first new compiler warning ships; per-phase blocks let specs be
written independently without ID collisions. **Implementation timing:** the ID field and
plumbing are a phase 1 work item (the first phase implemented in the sequential order),
so every later phase simply uses it. **Alternatives rejected:** sequential global
numbering (couples spec authoring order); severity-encoded prefixes like `HEDW`/`HEDE`
(severity is already a property; encoding it in the ID breaks when severity is
reclassified).

## D2 — The single 2.0 breaking window

**Decision.** **v2.0 is the combined release of all nine phases**, and all ratified
breaking changes land in it together, absorbed as **one** migration: the phase 2
output-profile default flip (`Text` → `Html`), the phase 2 encoder swap (`WebUtility` →
pluggable `HtmlEncoder` default), and phase 4's `TrimDirectiveLines` default-on.
Everything else in phases 1–9 is additive. Each contributing phase is implemented
**off/compatible by default** so the tree stays green and shippable throughout the
sequence; the flips execute in the release tail, with the 2.0 change documented in each
phase's migration note.

**Rationale.** Maintainer-ratified (July 2026, recorded in the roadmap index); one
migration is cheaper for users than three. **Alternatives rejected:** per-phase majors
(version churn), silent behavior change in a minor (violates the
[breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)).

The executable consolidation of the window — full contents table (five items, including
the two later-discovered riders: phase 1's `AllowCSharp` obsoletion and phase 7's
generator-default flip), execution order, migration-note deliverable, and the
precompiled-assembly rebuild consequence — is [migration-2.0.md](migration-2.0.md).

## D3 — One-time C# 14 first-class-span audit

**Decision.** A one-time repository audit for C# 14 first-class-span overload-resolution
changes (implicit span conversions changing which overload binds) runs as a **phase 1
kickoff work item**; findings (or the explicit "no impact" result) are recorded in the
phase 1 spec's assumed-state section. Later phases do not repeat it.

**Rationale.** `LangVersion latest` means the compiler upgrade already applied
repo-wide; the roadmap's .NET 10 round called for exactly one audit. Scheduling it into
the first sequentially-implemented phase makes it unmissable without creating a phase of
its own.

## D4 — UTF-8 static-piece emission (`"…"u8`) belongs to phase 7

**Decision.** Emitting template static text as `"…"u8` UTF-8 literals is specified in
phase 7 (build-time compilation) only, designed so phase 8's `IBufferWriter<byte>` sink
consumes those pieces without transcoding. Phase 8 does not pre-build this for the
runtime path; the runtime engine keeps `string`/`char` internals.

**Rationale.** The roadmap identifies this as the best cross-phase optimization, but it
only pays where static pieces are known at build time; sequential order (7 before 8)
means phase 8 can bind to phase 7's emitted shape. **Alternative rejected:** UTF-8
caching of static pieces in the runtime document (adds dual-encoding complexity to every
render path for a win the precompiled path gets for free).

## D5 — Sequential implementation order

**Decision.** Phases are implemented in roadmap order 1 → 9, each as a separate effort,
and the sequence culminates in the single combined **v2.0** release ([D2](#d2--the-single-20-breaking-window),
[migration-2.0.md](migration-2.0.md)). Phase *N*'s spec assumes 1..*N*−1 merged and may
use their public APIs; independent-shipping fallbacks in the roadmap are recorded as
deferred, not specified. Amendments discovered during implementation flow through the
[amendments ledger](#cross-spec-amendments-ledger) with maintainer ratification. (Full
rule in [spec-conventions](spec-conventions.md#sequential-but-separate-implementation-assumption).)

**Rationale.** User direction for this spec effort; it removes conditional designs
("if phase 1 shipped, then…") and lets later specs bind to concrete earlier APIs.

## D6 — The `Scope` publish/read channel is public API

**Decision.** The phase 3 local-context channel (`Scope.Publish` / `Scope.TryRead` over a
lazily-created per-body frame, exact naming pinned in the phase 3 spec) is **public**
extension API, documented in `custom-extensions.md`. It is the sanctioned way for sibling
extensions to coordinate declaratively. Later phases treat it as available infrastructure
(candidate consumers: phase 5 slot parameter passing diagnostics, phase 9 sample
extensions) but must record any new reserved keys in their own specs.

**Rationale.** Ratified in the roadmap's open-question resolution round; a public channel
turns branching from a special case into the first consumer of a general capability
(open/closed applied at the right seam). Reserved-key hygiene keeps independent consumers
collision-free.

## D7 — .NET 10 stance: no conditional compilation by default

**Decision.** Phase code does **not** add `#if NET10_0_OR_GREATER` paths unless a spec
records a verified, benchmarked win that cannot be had otherwise. .NET 10 value arrives
via the runtime (JIT escape analysis, encoder scan improvements, array de-abstraction)
and the language (C# 14 on all TFMs). Designs must not rely on JIT rescue for allocations
that provably escape (verified negatives in the roadmap: phase 3 context frames, phase 5
props arrays, phase 1 compiled-delegate boxing).

**Rationale.** The roadmap's version-verified .NET 10 review reached this meta-finding;
conditional TFM forks multiply the test matrix for near-zero benefit here.

## D8 — Template identity & naming policy is owned by phase 7

**Decision.** The consolidated template identity policy (file-driven templates key on the
full resolver-relative path; non-file templates use integration-owned keys via an open
key-policy seam; uniqueness enforced for pre-compilation only; discovery is public API)
lives in the phase 7 spec. Other phases that need a template's identity (phase 6 go-to-
definition, phase 9 gallery) reference phase 7's definitions rather than inventing their
own. The two key-policy details the roadmap left open (normalization rule, replace
mechanics) are **closed in the phase 7 spec** per the no-open-questions rule.

**Rationale.** One owner prevents three divergent notions of "template name"; phase 7 is
where identity becomes load-bearing (assembly-embedded keys must match runtime lookups).

## D9 — Spec and roadmap pages stay unpublished

**Decision.** `docs/spec/**`, like `docs/roadmap/**` and `assessment.md`, is contributor
material excluded from the published docs site. The `srcExclude` additions in
`docs/.vitepress/config.mts` are a pending integration step recorded in the
[spec index](../README.md#publication-status) and the roadmap index — they are applied
when the roadmap/spec set goes live, not piecemeal.

**Rationale.** Mirrors the roadmap's own convention ("no changes outside this folder"
until the integration step); keeps this effort docs-only and reviewable as one unit.

## Claimed diagnostic IDs (registry)

The live allocation state of the [D1](#d1--stable-diagnostic-ids-hedxxxx) blocks. A spec
claiming a new ID updates this table in the same change; an ID once listed is never
reused or renumbered. Message texts, triggers, and position semantics live in the owning
spec's *Diagnostics* section — this table is the collision guard and lookup index.

| IDs | Owner | Notes |
| --- | --- | --- |
| `HED0001`–`HED0003` | [Phase 1](../phase-1-native-expressions/README.md#diagnostics) | Pre-existing diagnostics touched by the shared resolver / legacy shapes / syntax listener |
| `HED0004` | [Phase 4](../phase-4-ergonomics/README.md#diagnostics) | Pre-existing `CheckTypes` return-type message, assigned as touched (text unchanged) |
| `HED1001`–`HED1017` | [Phase 1](../phase-1-native-expressions/README.md#diagnostics) | Native-expression tier |
| `HED2001`–`HED2003` | [Phase 2](../phase-2-safe-output/README.md#diagnostics) | Output profiles |
| `HED3001`–`HED3004` | [Phase 3](../phase-3-branching/README.md#diagnostics) | Branching |
| `HED4001`–`HED4002` | [Phase 4](../phase-4-ergonomics/README.md#diagnostics) | Ergonomics |
| `HED5001`–`HED5018` | [Phase 5](../phase-5-props-and-slots/README.md#diagnostics) | Props & slots |
| `HED6xxx` | — reserved, none claimed | [Phase 6 D20](../phase-6-tooling-lsp/README.md#design-decisions): tooling-only messages are not compile diagnostics |
| `HED7001`–`HED7015` | [Phase 7](../phase-7-build-time-compilation/README.md#diagnostics) | Generator build-time (shares the HED scheme per its D13); `HED7014` unresolvable-function warning (no default or `[ExportFunctions]` binding — delegate-only remainder) and `HED7015` unevaluable-extension error added by the gap-resolution round |
| `HED7101`–`HED7103` | [Phase 7](../phase-7-build-time-compilation/README.md#diagnostics) | Runtime registration/fallback |
| `HED8xxx` | — reserved, none claimed | [Phase 8 D14](../phase-8-streaming-async/README.md#design-decisions): sink APIs throw host errors, no compile diagnostics |
| `HED9001` | [Phase 9](../phase-9-demo-and-integration/README.md#diagnostics) | C# tier disabled by the trimming feature switch |

## Cross-spec amendments ledger

Because phases are specified and implemented sequentially ([D5](#d5--sequential-implementation-order)),
a later spec may amend an earlier phase's recorded artifact; the amendment is
**implemented by the phase that makes it**, never retrofitted into the earlier phase's
text. Readers of an earlier spec consult this ledger for the current end-state; an
implementer of the earlier phase builds it as written and lets the later phase apply its
own amendment when its turn comes.

| Amendment | Made by | Amends |
| --- | --- | --- |
| `mode CALL` gains a trailing `CALL_UNKNOWN: . ;` catch-all token so an unrecognized character (`=`, `$`) becomes a positioned `HED0003` parser error. Evidence: grammar.md §Lexer changes note 7 assumed such a character "is reported by the lexer and the parse then fails on the gap", but the lexer silently *skips* unrecognized characters, and for `X += 1` (→ `X + 1`) and `$"x{A}"` (→ `"x{A}"`) the remaining tokens parse cleanly, so corpus rows N02/N09 did not fail without the catch-all | Phase 1 implementation | Phase 1 [grammar.md](../phase-1-native-expressions/grammar.md) §Lexer changes (note 7) and the token vocabulary |
| `FunctionRegistry.Default` grows from the frozen seventeen names to eighteen (`range`) | [Phase 4 D2](../phase-4-ergonomics/README.md#design-decisions) | Phase 1 D13's frozen default set |
| Standalone-function carrier resolution routed through the shared `UnnamedCarrierName` helper (Html-profile redirect coverage) | [Phase 2 D3](../phase-2-safe-output/README.md#design-decisions) | Phase 1's function-carrier call site |
| `TemplateResolver.RemoveFromCache` gets a safe-enumeration fix (collect matching keys, then remove) — D8's "value-based and needs no change" is refined: the original removed dictionary entries *during* a `foreach` over the same dictionary, which throws `InvalidOperationException` on .NET Framework (surfaced by phase 2's `RemoveFromCacheEvictsByInstance` on `net48`). Value-based eviction semantics are unchanged; the method now also correctly evicts a template cached under more than one profile-suffixed key | Phase 2 implementation | Phase 2 [D8](../phase-2-safe-output/README.md#design-decisions) ("RemoveFromCache … needs no change") |
| The Html redirect applies to the **leaf** unnamed carrier only; **nested chain-parameter producers stay raw** (a `chainParameter` flag threaded through `CompileItem`/`CreateExtension`). Evidence: `@(upper(X))` verifiably compiles as `@() ← upper(X)` — an unnamed sink whose parenthesized chain holds a standalone-function carrier (also unnamed), not a single native-expression carrier as D3/M09/D10 assumed. Redirecting both carriers double-encoded `@(upper(X))` (`&amp;lt;…`) and fired a spurious `HED2003`, contradicting M09/X09's single-encode expectation. Redirecting the leaf only keeps encoding at the emitting leaf (consistent with D10's container-forwarding rule) and makes `@(upper(X))` equal `@upper(X)`; this reverses D3's *Alternatives rejected* line "redirecting only top-level chain sinks" (its premise — that `@(upper(X))` is a native single carrier — is refuted by the verified parse) | Phase 2 implementation | Phase 2 [D3](../phase-2-safe-output/README.md#design-decisions) (redirect scope) and the M09/X09 rows of its [testing plan](../phase-2-safe-output/README.md#testing-plan) |
| `@out(X)` parameter goes from evaluated-and-ignored to `HED5012` | [Phase 5 D13](../phase-5-props-and-slots/README.md#design-decisions) | The runtime half of phase 1 corpus row P20 |
| `[NotEncode]` revisit trigger closed permanently (not revived, deletion not scheduled) | [Phase 5 D14](../phase-5-props-and-slots/README.md#design-decisions) | Phase 2 D12's deferred revisit |
| Internal `FunctionRegistry.EnumerateOverloads()` added for completion/hover | [Phase 6 D3](../phase-6-tooling-lsp/README.md#design-decisions) | Phase 1's `FunctionRegistry` surface (internal-only; public enumeration stays deferred) |
| Precompiled manifest moves to schemaVersion 2 (u8 twins normative; engine accepts {1, 2}); generated piece writes go through `PrecompiledRuntime.WritePiece` | [Phase 8 D7](../phase-8-streaming-async/README.md#design-decisions) | Phase 7's schemaVersion 1 manifest and its non-normative sink-write sketch |
| `AllowCSharp` obsoletion timing pinned to the 2.0 window | [Migration W1](migration-2.0.md#w1--allowcsharp-obsoletion-lands-in-20) | Phase 1 D6's "no earlier than 2.0" |
| Flag-gated `ImportOrigin` marker (internal fields on `ParseContext`/`HeddleCompileError`, count-bracketed stamping at the import/partial/compile-funnel sites, phase 5 layout-resolver stamping) so the LSP re-anchors imported-file diagnostics | [Phase 6 D25](../phase-6-tooling-lsp/README.md#design-decisions) | Phase 5's layout-resolver seam and the phase 4 D11-frozen import surfaces (additive, `ProvideLanguageFeatures`-only; compile behavior unchanged) |
| Phase 6's "imported definitions replace the set / last declaration wins" narrative corrected: plain duplicates are compile errors, only override layers replace; go-to-definition targets the surviving registry entry | [Phase 6 D26](../phase-6-tooling-lsp/README.md#design-decisions) | Phase 6's own assumed state and D16, reconciled to phase 4 D11's verified semantics |
| Declarative function export added to the engine's public surface: assembly-level `[ExportFunctions(...)]` (`Heddle.Attributes.ExportFunctionsAttribute`) + `FunctionRegistry.RegisterFrom(Assembly)` — lowercase-invariant name derivation, phase 1 D12 replace/overload/freeze semantics unchanged; one discovery contract, three readers (host, LSP scan, generator) | [Phase 6 D24](../phase-6-tooling-lsp/README.md#design-decisions) (per the [OQ2 resolution](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)) | Phase 1's function surface (D12 `FunctionRegistry` API) |
| **OQ2 resolved (July 2026)**: the one-shot scan ratified (D23) and extended to `[ExportFunctions]` exports registered via `RegisterFrom` into the real workspace registry; the provisional `functions` config array dropped; template declarations ratified as the second name source; the dynamic-extension development workflow deferred to its own maintainer-scheduled effort | [Phase 6 D23–D24](../phase-6-tooling-lsp/README.md#design-decisions) (revised) | The prior OQ2 provisional-default entry — register row closed |
| **OQ1 resolved (July 2026) — overrides the provisional default**: direct-binding precompiled functions — the generator discovers `[ExportFunctions]` exports over the compilation's metadata references (symbol inspection, milestone-1 capable) and binds generated calls directly to the exporting types; built-ins stay behind the public `PrecompiledFunctions` shim; `FunctionBindings` records actual `(name, target, overloadCount)` rows and the gauntlet polices missing exports, foreign replacements, delegates, and count drift; `HED7014` narrows to names resolvable from neither the default set nor any referenced export | [Phase 7 D21](../phase-7-build-time-compilation/README.md#design-decisions) (revised per the [OQ1 resolution](../OPEN-QUESTIONS.md#oq1--scope-of-function-support-in-precompiled-templates-from-gap-g1--resolved-july-2026)) | The prior OQ1 provisional-default row (default-registry-only) — register row closed |
| Corpus row **C19** retargeted: the `@else`-output-block-inside-`@import()` scenario it sketched is **not realizable on the base engine** — the `@import()` extension re-parses its fragment into the importing `ParseContext` via `DocumentParser.Parse`, and any position-bearing content (an output block *or* a definition) parsed from an inline document throws in `ParseContext.GetBlockPosition` (negative `BlockPosition`), a pre-existing limitation independent of phase 3 (`ImportExtension`/`ParseContext`/`DocumentParser` are unmodified; `@import()` is designed to import definitions from file-loaded templates). The ratified splice for branch blocks that coordinate at runtime is `@<<` (parse-time), which **is** realizable and covered by C11/N4. The N5/D16 guarantee — content parsed in by `@import()` is not reached by the static branch scan — is preserved structurally (the extension's `InitStart` runs *after* `ProcessBranchSets` in the compile loop) and re-pinned by asserting the branch scan classifies `@import()` as an Other block and emits no `HED3xxx` for a set spanning it. Also recorded: D15's "exactly 320 000 B, any additional byte is a defect" is the **no-JIT-rescue worst case**; on net8.0+ the JIT partially stack-allocates the per-iteration `ScopeLocals`, so the measured `RenderListIfElse10K` − `RenderListIfPair10K` delta is *lower* (well within budget — the "must not increase" gate holds), and existing templates stay allocation-identical by construction (`NeedsLocals == false` ⇒ no frame) | Phase 3 implementation | Phase 3 [README](../phase-3-branching/README.md) corpus row C19 / [D16](../phase-3-branching/README.md#design-decisions)/N5 and the [D15](../phase-3-branching/README.md#design-decisions) allocation acceptance number |
| `OutExtension.RenderData` now renders the chained value's string form (`chained is string s ? s : chained?.ToString()`) instead of `ChainedData as string`. Evidence: F02/the roadmap validation scenario pin `@for(3){{<i>@out()</i>}}` → `<i>0</i><i>1</i><i>2</i>` "as today", but a bodiless `@out()` rendered a boxed non-string (the `int` index a counted `@for` threads on the chained channel) as **empty** — `as string` on a boxed `int` is `null` — so the index silently dropped on the render path (the process path already returned the raw object). The one-line fix makes render/process consistent and is a superset of the spec's "`ForIndexExtension` is the only render-path touch" claim; verified byte-identical against the whole existing golden corpus (a boxed-non-string `@out()` rendering empty was never intentional) | Phase 4 implementation | Phase 4 [README](../phase-4-ergonomics/README.md) D1 / Performance section ("only render-path touch") and testing-plan row F02 |
| Phase 4 I05 / the D11 `@import()` "renders nothing" row corrected: `@import(){{ergo-import-library.heddle}}` from a **non-zero offset** does **not** render nothing — the library's definition block is re-parsed at the body context's offset and hits `ParseContext.GetBlockPosition` with a negative index (the pre-existing C19 limitation), surfacing as a positioned `Error while compiling import`. The inline golden `generated-ergo-import-inline.html` (`BEFORE\n\nAFTER\n`) is therefore unachievable and is dropped; I05 pins the failing compile (same file as the composition golden — criterion 4 holds), and the offset-0 "parses into a dead context, merges nothing" behavior is pinned by I04 (`Cannot find extension or registered function 'lib_badge'`, HED1001 — not HED0002 as D11/I04 cited) | Phase 4 implementation | Phase 4 [README](../phase-4-ergonomics/README.md) D11 table (`@import()` rows) and testing-plan rows I04/I05/I08 |
| Phase 4 trim-torture row **T12** imports an **output-free** `ergo-import-empty.heddle` (one definition, no output chain, no default) rather than the named `ergo-import-library.heddle`. Evidence: the composition library carries a top-level `@lib_badge()` chain and a `<lib_footer> -> ()` default that render at the import position and document end (I02), so the row's pinned on-output `X` is unreachable with it; the trim behavior under test (a whole-line `@<<` swallowing its line) is identical for any import target | Phase 4 implementation | Phase 4 [README](../phase-4-ergonomics/README.md) testing-plan row T12 |
| Slot projection carries the `SlotContent` on a **dedicated preserved `Scope` field** (`internal readonly object SlotCarrier`, installed by a new `WithSlot` transform, copied by every transform) instead of D11's chained channel; the runtime guard fires from a parse-time `OutputItem.IsChainedConsumer` mark on a composed `@out`. Evidence: D11's "carrier down the existing chained channel (`scope.Chain(slotContent)`)" cannot reach an `@out(expr)` nested in a loop body — `@list`/`@for` overwrite `ChainedData` with the per-iteration index (the phase 4 `@for(3){{@out()}}`→`0 1 2` behavior), so the flagship picker `<picker(out:: MenuOption)>{{ <ul>@list(Options){{ <li>@out(this)</li> }}</ul> }}` would see the index, not the carrier, and the guard as worded ("`ChainedData` is not the carrier") cannot distinguish a legitimate loop body (index on `ChainedData`) from a chained composition (`@a():out(X)`) — both have a non-carrier `ChainedData`. A field preserved by all `Scope` transforms reaches the nested `@out` and yields D11's slot-compose case (a non-slot definition's caller content projecting the *outer* slot) by construction, since the caller content is pre-rendered under a scope that still carries the outer field; the guard instead keys off chain position (`@out` is not the leftmost chain item ⇒ throw `TemplateProcessingException`). `Scope` therefore grows two reference fields (`PropsData` + `SlotCarrier`), not one — both are struct fields (no heap allocation; `TextRenderBenchmarks` mean stays within reported error). Also: the D6 prop-layout cache (`CompileContext.ResolvedPropLayouts`) keys on a stable definition identity (`Name` + declaration `Position`) rather than the `DefinitionItem` **instance**, because context isolation copies definitions per body — the D6 two-site invariant (both call sites of one definition share one resolved `PropLayout` instance) is unreachable with an instance key | Phase 5 implementation | Phase 5 [D11](../phase-5-props-and-slots/README.md#design-decisions) (carrier channel + runtime guard), [D7](../phase-5-props-and-slots/README.md#design-decisions) (`Scope` +1 field), and the D6 `ResolvedPropLayouts` cache key |
| Generated-`@partial` options funnel pinned (the D7 example left it underspecified — `ResolvePartial("…", options)` with no stated source of `options` in a static generated body): the generated call site closes over the **ambient `TemplateOptions`** a new `PrecompiledRuntime.GenerateString(root, model, chained, callerData, options)` overload establishes on a `[ThreadStatic]` field for the duration of a render (a `null` `options` inherits the current ambient, so nested partials keep the outermost render's options; the registry adapter passes the request options, the typed entry passes `null` → a fresh `TemplateOptions`). The generated body calls the parameterless `ResolvePartial(key)` (dynamic-tier caller) or `ResolvePartial(key, typeof(callerModel))` (typed caller) — the second overload types the **dynamically-compiled** child by the caller-site model type the runtime `PartialExtension` uses, so a mixed-mode precompiled-parent/runtime-child render binds identically. Registry-first resolution is options-independent (a precompiled child returns its `Strategy`); only the dynamic-compile fallback reads the ambient. The ambient is read **only** by `ResolvePartial`, so the dynamic path is byte-unchanged (570/570). Smallest reversible choice for an underspecified funnel; both mixed-mode directions are differential-gated | Phase 7 implementation | Phase 7 [generated-code.md](../phase-7-build-time-compilation/generated-code.md) `@partial` example (`ResolvePartial("…", options)` with no options source) and [D7](../phase-7-build-time-compilation/README.md#d7--runtime-hookup-repeatable-registration-gauntlet-order-lazy-partial-routing) (lazy partial routing) |
| D4's "single Roslyn entry (`ContextCompilation.Compile`)" refined: `Microsoft.CodeAnalysis` is reachable from **three** places, not one — `ContextCompilation.Compile` (the finalize/emit pass), `CSharpContext.ParseAndGetResultType` (the parse-time semantic-model pass), and `AssemblyHelper`'s `MetadataReference` machinery (built eagerly in its static ctor and reachable from core type discovery). Guarding only `ContextCompilation.Compile` would leave the other two keeping the whole graph. The `HeddleFeatures.CSharpTierEnabled` guard is therefore applied at both compile entries (early-return past the guard, HED9001 at the finalize entry), and the `MetadataReference` concern is lifted out of `AssemblyHelper`'s reachable surface into a new `Heddle.Native.RoslynReferenceProvider` (a weak per-`Assembly` `MetadataReference` cache reached only by the gated `GetApplicationReferences`), with `AssemblyHelper.AssemblyCache` reduced to `Assembly` values and the eager warm-up split into a non-Roslyn `EnsureApplicationAssembliesWalked`. Result: the trimmed WASM publish contains **zero** `Microsoft.CodeAnalysis*` assets (purity gate green) at 8.75 MB. HED9001 is an **internal** id (`HeddleFeatures.CSharpTierDisabledDiagnosticId`), not a public `HeddleDiagnosticIds` constant, so the phase's "no public API surface" holds (`PublicApiSurfaceTests` unchanged) | Phase 9 implementation | Phase 9 [D4](../phase-9-demo-and-integration/README.md#d4--roslyn-stays-out-of-the-bundle-the-heddlecsharptierenabled-feature-switch) ("single Roslyn entry") and [Diagnostics](../phase-9-demo-and-integration/README.md#diagnostics) (HED9001 home) |
| Phase 7's codegen "**no runtime Heddle dependency**" reconciled: the phase-7 generator emits *precompiled render strategies* that call `Heddle.Precompiled.PrecompiledRuntime`, so the render **runtime** (`Heddle.dll`) is intentionally linked — precompilation moves parse/compile to build time, not the render. The achievable, structurally-asserted property is that the **code generator** (`Heddle.Generator`) is never a runtime dependency (`samples/codegen-t4-successor` asserts `Heddle.Generator.dll` is absent from the runtime output). Also recorded: consuming the generator by **ProjectReference** (not the NuGet package) requires importing `build/Heddle.Generator.props`/`.targets` by hand and adding `Heddle.Language.dll` + `Antlr4.Runtime.Standard.dll` as `<Analyzer>` items (the package bundles these into `analyzers/dotnet/cs`); `EmitCompilerGeneratedFiles` output must be kept out of the compile set (`EnableDefaultCompileItems=false` or a `Compile Remove`), else the emitted `*.g.cs` are double-compiled | Phase 9 implementation | Phase 7 gallery item "codegen … no runtime Heddle dependency" ([phase 9 D13 row 8](../phase-9-demo-and-integration/README.md#d13--the-sample-inventory-ten-samples--one-walkthrough-row-every-deferred-item-lands-roadmap-suggestions-reconciled)) |
| Member expressions inside a definition body that is **both** imported via `@<<` and re-parsed at a non-zero offset render mis-positioned (a leading char dropped / a stray char leaked) — the pre-existing C19/I05 import-offset limitation, now observed for `@<<` (not only `@import()`). `samples/definition-library` keeps the library's imported definition bodies static and places all member expressions in the page-side override/narrow bodies (compiled in the page itself, unaffected). No engine change; recorded so the sample's shape reads as deliberate | Phase 9 implementation | Phase 3/4 import-offset limitation (C19 / phase 4 I05 ledger rows) as it applies to `@<<`-imported definition bodies |
