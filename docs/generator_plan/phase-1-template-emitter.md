# Phase 1 — template emitter

## Header

- **Status:** **implemented (2026-07-26)** — D1–D14 and WI1–WI13 landed; see
  [Implementation record](#implementation-record). The program's last quarantined phase-0 fixture
  (`NonLeftmostScopeChannelParticipant_ProvisionsLocalsOnBothTiers`) is **un-skipped, reshaped and
  green**, so the quarantine register now carries **zero skips**. All four open questions were
  resolved (user, 2026-07-25 — see the [open-questions register](open-questions.md)) and folded in.
  Two diagnostics are claimed to this phase in the
  [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry): `HED7022`
  (unknown `@profile` value, Error, D3) and `HED7024` (call-site fill of a private region, Error,
  D7/Q1.3). `PrecompiledSchema` is bumped 4→5 for the per-carrier `BindDefinition` overload.
- **Goal (one line):** Resolve research area 01's drift between `TemplateEmitter` and the runtime
  compile/render pipeline — three live bugs fixed first, then the area's genuinely-sharable rules
  extracted into linked shared sources and its spec-only rules pinned as data tables plus
  differential tests — so the emitter's byte-affecting decisions exist exactly once or are
  test-pinned where representation forbids sharing.
- **Depends on:** nothing for the fix-first bug group and most extractions (independently
  startable). One work item (WI11, generator-side adoption of the profile/render-type rule files)
  depends on phase 5 linking the `OutputProfile`/`RenderType`/`ExpressionMode` enums; the shared
  `RegionFillResolver` is co-owned with phase 2 (this phase lands the file and the emitter-side
  adoption). Phases 2, 3, 4 artifacts are consumed, not produced — see *Dependencies & ordering*.
- **Changes an externally-visible contract:** yes, in three bounded ways. (1) Two parity-restoring
  fixes change rendered behavior/bytes of *affected precompiled templates* back to what the dynamic
  tier — the authoritative behavior — already produces (see *Back-compat / impact* for why this is
  not window-gated). (2) Two new build-time diagnostics (`HED70xx` IDs, claimed at spec time per the
  [registry rules](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)):
  one turns a silently-mis-precompiled `@profile` error into a build error, and one surfaces the
  runtime's `HED5019` private-region-fill error as a matching build-time error (the Q1.3
  match-principle ruling). (3) Two additive public API
  surfaces: a per-carrier `PrecompiledRuntime.BindDefinition` overload and a `[ZeroOutput]`
  attribute — both additive, existing signatures retained.

## Goal

Research area [01 — template emitter](../research/generator-code-sharing/01-template-emitter.md)
established that `src/Heddle.Generator/Emit/TemplateEmitter.cs` is a hand-maintained
reimplementation of the runtime's byte-affecting compile decisions, with twenty findings ranging
from confirmed live drift (the `needsLocals` scan, the `@profile` unknown-value handling, the
prop-default conversion table) to structurally duplicated rules that will drift on the next
unilateral change. The cross-area synthesis
([07 — recommendations](../research/generator-code-sharing/07-recommendations.md)) classifies each
finding as fix-now, sharable-as-code (linked `<Compile>` sources over already-linked parse types),
or sharable-only-as-spec (rule tables plus differential tests).

This phase executes area 01's slice of that program:

1. **Fix the three live bugs first**, independently of any extraction — the participant scan and
   its per-carrier flag OR ([01 F11](../research/generator-code-sharing/01-template-emitter.md),
   drift #3 in [07](../research/generator-code-sharing/07-recommendations.md)), the emitter's
   silent acceptance of an unknown `@profile` value the runtime rejects with `HED2001`
   ([01 F1](../research/generator-code-sharing/01-template-emitter.md), drift #12), and the
   `DefaultConvertible` gaps ([01 F7](../research/generator-code-sharing/01-template-emitter.md),
   drift #13).
2. **Extract the area's (B)-class rules** into new shared linked sources under
   `src/Heddle/Language/**` (auto-linked by the existing generator csproj glob) and
   `src/Heddle/Data/` (one csproj line each): the participant scan, the slot rules
   (`HasOutValue` + slot-type walk), the region-fill matching loop, the call-target precedence
   classifier, the embedded-C# identifier constants, the output-profile and render-type rules.
3. **Pin the area's (A)-class rules as data + tests**: the body model-typing table as linked data,
   zero-output classification as a declarative attribute, and the strategy-shape/coercion-rail
   parity contract as normative spec text backed by differential tests (the shared offset-walk
   code itself is phase 2's deliverable).
4. **Clean up the intra-generator duplication** catalogued in
   [01 F20](../research/generator-code-sharing/01-template-emitter.md) — all of it, since it has
   no cross-project constraints.

Throughout, the runtime engine is the authoritative behavior when aligning drift; every seam
named below was re-verified against current source while authoring this plan (member anchors
cited next to line numbers, which drift).

## Non-goals / scope boundary

- **No re-planning of artifacts owned by sibling phases.** This phase *consumes* but does not
  produce: the shared document segmentation / `SlicePieces` walk and the runtime-side
  `RegionFillResolver` adoption (phase 2); `PropLayoutCore<TType>` and the assignability
  conformance corpus — the vehicles for
  [01 F5/F6/F8/F9](../research/generator-code-sharing/01-template-emitter.md) (phase 3); the
  `PrimitiveKind` neutral enum, the shared implicit-numeric table, and everything in the
  expression writers — [01 F7's table half, F10's emitters](../research/generator-code-sharing/01-template-emitter.md)
  (phase 4); linking the `OutputProfile`/`ExpressionMode`/`RenderType` enums and deleting the
  emitter's `Mode()`/`Profile()` string mapping —
  [01 F15](../research/generator-code-sharing/01-template-emitter.md) (phase 5); the shared
  version-less-AQN formatter for [01 F18](../research/generator-code-sharing/01-template-emitter.md)
  (phase 3, area 03 F1). Where this phase's fixes touch those seams (e.g. F7's point fix inside
  the emitter's existing table), the fix is minimal and the structural extraction is left to the
  owning phase.
- **No change to the `maxRecursionCount` build-baked posture**
  ([01 F19](../research/generator-code-sharing/01-template-emitter.md)) — intentional per the
  precompilation spec's D23; the options-fingerprint gap it leaves is resolved in *Open
  questions* (OQ1: the call is the precompilation spec's, and the default posture records the
  divergence as intentional).
- **No behavior change to correct templates.** Every extraction is byte-neutral by construction
  and gated on the existing snapshot/differential/golden suites; the only behavior deltas are the
  three parity-restoring bug fixes and WI6's region-fill error-surfacing alignment (the OQ3
  ruling), each individually called out in *Back-compat / impact*.
- **No new runtime render-path allocations.** Shared rule files run at compile/emit time only;
  the per-carrier locals fix strictly *reduces* over-provisioned frames relative to today's OR
  (see D2). The [coding standards' performance rules](../spec/common/coding-standards.md#performance-rules-the-render-path-is-hot)
  bind every work item.
- **No grammar changes, no new sanctioned seams.** All shared files operate on strings, plain
  enums, and the already-linked parse types (`OutputChain`, `OutputItem`, `CallParameter`,
  `DefinitionItem`, `ParseContext`, `RegionFillCandidate`, …); per-side knowledge is injected as
  `Func<…>` delegates, never as new plugin interfaces.
- **No `Heddle.Shared` project.** The linked-`<Compile>` mechanism is proven and the synthesis's
  revisit trigger ("shared set grows past ~20 files") is not reached by this phase; the shared-file
  inventory below adds nine.

## Design direction

The mechanism is the one that already works: `Heddle.Generator.csproj` links
`..\Heddle\Language\**\*.cs` wholesale (verified — the "Shared front-end sources, D4 step 2"
item group, excluding only `DocumentParser.Runtime.cs`) and links `Data/` files individually
(`HeddleDiagnosticIds.cs`, `CompileError.cs`, …). New shared rule files therefore go under
`src/Heddle/Language/**` whenever their inputs are parse types (zero csproj edits), and under
`src/Heddle/Data/` with one explicit `<Compile Include>` line when they are enum/string rule
tables that belong beside the enums they interpret. Hard constraints, restated from the research
program and honored by every file below: shared code is `netstandard2.0`-clean, contains no
Roslyn types, and the generator never references `Heddle.dll` — anything per-side (symbol vs.
reflection lookups) enters through injected delegates.

The second structural idea: where the two sides genuinely cannot share code because they operate
on different representations (parse tree vs. compiled extension instances; `ISymbol` vs. `Type`),
this phase does not force a leaky abstraction. It pins the rule as **linked data plus a lockstep
test** — the pattern `DefaultFunctionLockstepTests` already proves — so a unilateral change turns
a silent byte divergence into a red test naming the rule.

### Shared-file inventory (new files this phase creates)

| New file | Contents | Replaces / pins | Linked how |
|---|---|---|---|
| `src/Heddle/Language/ParticipantScan.cs` | Full-chain, parameter-recursing `[ScopeChannel]` participant scan over `OutputChain`/`OutputItem`/`CallParameter` with injected `Func<string,bool>` | `TemplateEmitter.ScanHostsParticipant` + the leftmost-only probe in `PopulateBody`; lockstep-pins `RuntimeDocument.ComputeNeedsLocals` | auto-glob |
| `src/Heddle/Language/SlotRules.cs` | `HasOutValue(CallParameter)` (the canonical five-way test) + `SlotTypeName(DefinitionItem)` / `HasSlot(DefinitionItem)` base-chain walk | `OutExtension.HasOutValue` (delegates); `TemplateEmitter.DefinitionHasSlot`, the duplicate walk in `SlotBodyContext`, the approximate `hasValue` in `BuildOutCall`; `HeddleCompiler.ResolveSlotType`'s walk | auto-glob |
| `src/Heddle/Language/RegionFillResolver.cs` | The candidate-matching loop (origin filter, region lookup, public check, default lookup) returning a per-candidate verdict enum; materialization stays at call sites | the loop inside `TemplateEmitter.TryBuildGeneratorFillScope`; phase 2 adopts it in `HeddleCompiler.BuildRegionFillScope` | auto-glob |
| `src/Heddle/Language/CallTargetRules.cs` | `ResolveCallTarget(name, hasBody, CallParameter, …predicates) → CallTargetKind` — fill scope → definitions → extension-wins → bodiless shape-compatible function → unknown, plus the function-shape predicate | the precedence knowledge duplicated between `TemplateEmitter.BuildCall`/`BuildChainItemExpr` and `HeddleCompiler.CompileItem` | auto-glob |
| `src/Heddle/Language/BodyModelRules.cs` | `BodyModelSource` / `ChainedModelSource` enums + the built-in `extensionName → (BodyModelSource, ChainedModelSource)` table | the prose-comment-only body model-typing rule ([01 F3](../research/generator-code-sharing/01-template-emitter.md)) | auto-glob |
| `src/Heddle/Language/Expressions/EmbeddedCSharpNames.cs` | `const string Model = "model"`, `Chained = "chained"`, `Root = "root"` | the emitter's regex literals and `var model = …` local in `BuildCSharpExpr`/`EmitBodyClass`; pin-tests the `.tcs` parameter lists | auto-glob |
| `src/Heddle/Data/OutputProfileRules.cs` | `TryParse(string, out OutputProfile)` (trim + ordinal-ignore-case `text`/`html`, false otherwise); `ResolveUnnamedCarrier(OutputProfile, bool hasBody) → (UnnamedCarrierKind, RenderType)` | the string matching in `ProfileExtension.InitStart` and `TemplateEmitter.MapProfilePerChain`; the carrier decision in `HeddleCompiler.UnnamedCarrierName` and `TemplateEmitter.AllocateEmptyExtension` | one csproj line |
| `src/Heddle/Data/RenderTypeRules.cs` | `static RenderType Derive(bool hasEncodeOutput, bool hasNotEncode)` — the two-bool truth table | the identical ternary in `HeddleCompiler` (`CreateExtension` render-type derivation) and `TemplateEmitter.DerivedRenderTypeLiteral` | one csproj line |
| `src/Heddle/Attributes/ZeroOutputAttribute.cs` | Declarative marker: this extension emits nothing and its block is removed from the piece stream | the hardcoded `model`/`using`/`import`/`profile` list in `TemplateEmitter.IsDirectiveName` (via `ExtensionBinder` reading the attribute symbolically) | not linked — runtime type read by name from symbols, like `ScopeChannelAttribute` |

### Design decisions

#### D1 — Bugs ship before extractions, as minimal in-place patches

**Decision.** WI1–WI3 (the fix-first group) patch the existing generator code minimally and ship
independently of, and before, every extraction; the later extractions then *replace* the patched
code with the shared implementation. **Rationale.** [07's sequencing](../research/generator-code-sharing/07-recommendations.md)
puts confirmed live drift ahead of structural work because each fix is small, user-visible, and
must not wait on shared-file design review; and a fix embedded in an extraction cannot be
reverted or cherry-picked alone. **Alternatives rejected:** fixing F11 only via the
`ParticipantScan` extraction (couples a behavior fix to a refactor and makes the diff
unreviewable as either).

#### D2 — needsLocals: full recursive scan + per-carrier flags via an additive `BindDefinition` overload

**Decision.** Two coupled corrections, both aligning on the runtime
(`RuntimeDocument.ItemNeedsLocals`, `src/Heddle/Runtime/RuntimeDocument.cs:168-190` — carrier
unwrap and recursion into `ChainedParameter`, verified):
(a) the generator's scan recurses into nested chain parameters instead of probing only
`chain.Chain[0]` (`TemplateEmitter.PopulateBody`, `TemplateEmitter.cs:371-375`, and
`ScanHostsParticipant`, `:1404-1416` — both leftmost-only today, verified);
(b) the definition-call path stops OR-ing the definition body's and caller content's flags into
one value (`BuildDefinitionCall`, `TemplateEmitter.cs:1137-1140`, verified) and instead passes
each carrier its own body's flag. Because the current
`PrecompiledRuntime.BindDefinition(…, bool needsLocals, …)` applies one flag to both the inner
and outer carrier (`PrecompiledRuntime.cs`, `BindDefinition` — verified: the same `needsLocals`
reaches both `BindPrecompiled` calls), this requires a new **additive** overload taking
`bodyNeedsLocals` and `callerContentNeedsLocals`; the existing overloads remain, unchanged, so
assemblies generated by older generator versions keep binding. **Rationale.** The OR suppresses
the runtime's deliberate frame-*clearing* for a non-participating body
(`AbstractExtension.GetInnerResult`, `src/Heddle/Core/AbstractExtension.cs:36-43`, verified: a
non-participating body under a provisioned parent gets a cleared frame precisely so it never
reads the parent's branch state) — the OR is not "harmless over-provision", it changes which
frame a body sees. **Alternatives rejected:** changing the existing overload's semantics
(breaks already-shipped generated assemblies — a binary-compat violation for zero gain);
leaving the OR and only fixing the scan (leaves drift #3's second asymmetry live).

#### D3 — Unknown `@profile` value becomes a positioned generator build error (new HED70xx)

**Decision.** `TemplateEmitter.MapProfilePerChain` (`TemplateEmitter.cs:418-427`, verified: an
unrecognized value falls through with a comment asserting — incorrectly for the precompiled tier —
that "the template falls back") gains the runtime's third branch: an unknown trimmed value
produces a generator diagnostic, severity **Error**, positioned at the `@profile` directive, with
message text mirroring the runtime's `HED2001` ("Unknown output profile '{value}'. Valid values:
text, html." — `ProfileExtension.InitStart`, `src/Heddle/Extensions/ProfileExtension.cs:31-37`,
verified). The ID is the next free `HED70xx` (currently `HED7018`; `HED7001`–`HED7017` are
claimed — verified against `GeneratorDiagnostics.cs` and the
[registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)); the
phase spec claims it in the registry **in the same change**, per D1 of the cross-cutting
decisions. **Rationale.** The runtime is unambiguously authoritative here — `HED2001` is a
documented compile error — and today the precompiled tier *masks* it: the template precompiles
with the flip ignored and renders output the dynamic tier would never produce, and the gauntlet
cannot catch it (the options fingerprint keeps the compile-time profile). Error severity matches
precedent: `HED7006` and `HED7017` are build errors for template states the runtime rejects.
**Alternatives rejected:** warning + refuse-to-precompile (defers the failure to first dynamic
render for an error that is statically knowable at build; splits the failure surface across
tiers for no benefit); reusing `HED2001` itself as the generator ID (generator diagnostics live
in the `HED70xx` block by D1's area allocation, and `HED7017` set the twin-ID precedent).

#### D4 — `DefaultConvertible` gains the runtime's `Nullable<S> → Nullable<W>` row; the nullable probe unifies on `OriginalDefinition`

**Decision.** `TemplateEmitter.DefaultConvertible` (`TemplateEmitter.cs:942-974`, verified) adds
the source-nullable row mirroring `PropConversion.CanConvertTypes`
(`src/Heddle/Runtime/Expressions/PropConversion.cs:44-46`, verified: `Nullable<S>` converts to
`Nullable<W>` when the underlyings are identical or implicitly-numeric-widening); and the
emitter's two nullable-underlying probes unify on `OriginalDefinition`
(`TryFormatPropValue` uses `ConstructedFrom` at `TemplateEmitter.cs:1511-1513`;
`DefaultConvertible`/`NullDefaultLegal` use `OriginalDefinition` at `:952-955`/`:981-982` —
verified). A private static helper `TryGetNullableUnderlying(ITypeSymbol, out ITypeSymbol)`
becomes the single probe. **Rationale.** A missing row is a safe over-refusal (the template
falls back to the dynamic tier) but it is drift #13, and the asymmetric probe is a latent bug
generator-side; aligning is strictly enabling — more templates precompile, and the differential
gate proves the newly-precompiled ones render byte-identically. **Alternatives rejected:**
waiting for phase 4's shared `PrimitiveKind` table (that extraction restructures the numeric
table; this is a two-line semantic gap fix that should not wait on it — the extraction will
subsume the patched code later, per D1).

#### D5 — `ParticipantScan.cs`: shared scan is the generator's scanner; the runtime copy is lockstep-pinned, not replaced

**Decision.** The shared scan walks `OutputChain → OutputItem → CallParameter.ChainParameter`
recursively with one injected `Func<string,bool> hasScopeChannel`, and exposes
`BodyHostsParticipant(ParseContext, Func<string,bool>)` and
`ChainHostsParticipant(OutputChain, Func<string,bool>)`. The generator deletes both of its scans
and calls it (binder-backed predicate). The runtime **keeps**
`RuntimeDocument.ComputeNeedsLocals` (which walks the *compiled* item tree, where carrier wrap
and definition-shadowing are already resolved) and gains a lockstep differential test asserting
the shared scan's verdict equals the runtime's over the fixture corpus — including a
shadowed-name fixture where the parse-level scan's documented over-provision is asserted
explicitly. **Rationale.** The two sides genuinely see different trees: the runtime scan runs
post-compile over extension *instances* (attribute check with inheritance, carrier unwrap —
`RuntimeDocument.cs:168-190`), which cannot be expressed over parse types without re-encoding
the whole factory/shadowing pipeline into the shared file. Sharing the parse-level rule fixes
the generator's live gap by construction; the lockstep test converts residual dual-maintenance
into a loud failure. The generator's pre-definition-resolution over-provision (the existing
comment at `TemplateEmitter.cs:372-373`) is retained and now *documented in the shared file*:
over-provision only ever adds an unread frame, which is behavior-invisible (publish/read happens
only in `[ScopeChannel]` participants) and emit-time only. **Alternatives rejected:** replacing
the runtime scan with the parse-level scan (adds frame allocations for shadowed-name documents
on the render path — violates the no-new-render-allocations rule for zero correctness gain);
an `ITypeFacts`-style adapter (Tier-3 machinery for a predicate one delegate covers).

#### D6 — `SlotRules.cs`: both sides adopt; the generator's `hasValue` approximation is deleted

**Decision.** `HasOutValue(CallParameter)` is moved verbatim from `OutExtension`
(`src/Heddle/Extensions/OutExtension.cs:143-155`, verified — the canonical five-way test), and
`OutExtension` delegates to it. The slot-type walk (`first non-empty SlotTypeName down the base
chain`) becomes `SlotRules.SlotTypeName`/`HasSlot`; `TemplateEmitter.DefinitionHasSlot`
(`TemplateEmitter.cs:1644-1650`, verified), the second emitter copy of the walk, and
`HeddleCompiler.ResolveSlotType`'s walk all route through it. `BuildOutCall`'s approximate
`hasValue` (`TemplateEmitter.cs:1152-1155` — `!cp.IsModelTypeParameter` plus special cases,
verified) is replaced by the shared `HasOutValue`. **Rationale.** [01 F13](../research/generator-code-sharing/01-template-emitter.md):
both halves are pure functions of already-linked types; the approximation is equivalent today
and breaks on the next `CallParameter` carrier — replacing it now is a zero-byte-change
extraction that removes a booby trap. **Alternatives rejected:** none serious; this is the
cheapest extraction in the area.

#### D7 — `RegionFillResolver.cs`: shared matching, runtime-matched reactions, materialization stays outside

**Decision.** The resolver exposes one method that, given the caller context's candidates, the
caller origin, and the callee `DefinitionItem`, yields per-candidate verdicts:
`Matched(candidate, declaration, regionDefault)`, `ForeignOrigin`, `Dangling`,
`PrivateRegion`, `DefaultMissing`. It performs no materialization —
`DefinitionMaterializer.Materialize` (already shared) is invoked by each side on `Matched`.
The emitter's `TryBuildGeneratorFillScope` loop (`TemplateEmitter.cs:1300-1339`, verified)
becomes a thin adapter that reacts to each verdict **exactly as the runtime reacts** (the
Q1.3/OQ3 match-principle ruling — the generator behaves as if it were part of the dynamic
engine; `HeddleCompiler.BuildRegionFillScope`, `HeddleCompiler.cs:1686-1735`, verified):

- `Matched` → retract the candidate's parse-emitted error, materialize, mark consumed — both
  sides, unchanged.
- `ForeignOrigin` and `Dangling` → skip (`continue`), leaving the candidate's parse-emitted
  base-not-found error in place to surface through the generator's existing parse-diagnostic
  forwarding exactly as it surfaces from the dynamic compile — **no refusal to precompile**;
  this supersedes the earlier draft's "any fault verdict → refuse" posture.
- `DefaultMissing` → skip, defensive, parse error left in place (the runtime's byte-identical
  posture).
- `PrivateRegion` → reproduce the runtime's retract-and-raise: the parse-emitted error is
  retracted and a positioned build-time error matching `HED5019`'s message and anchoring
  (`RetractCandidateError` + the `RegionNotPublic` raise, once per candidate) is emitted; the
  generator-side twin ID is claimed at spec time per the registry rules (the `HED7017` twin
  precedent — see D3).

The runtime side's reactions are unchanged and normative; phase 2 adopts the shared resolver in
`HeddleCompiler.BuildRegionFillScope`. **Rationale.**
[01 F12](../research/generator-code-sharing/01-template-emitter.md): the byte-relevant
materialization is already shared; the duplicated matching/precedence half is the drift surface.
The verdict enum now exists to make reaction *parity* reviewable and testable — a verdict
theory asserts both sides map every verdict to the same outcome — rather than (as originally
drafted) to document a generator-strict/runtime-lenient asymmetry; per the ruling, differences
may legitimately exist only in warning channels the build tier lacks, and errors always match.
**Alternatives rejected:** the original refuse-on-any-fault posture (superseded by the Q1.3
ruling — refusal made the generator stricter than the engine it must match, and un-precompiling
a template the runtime compiles is itself a divergence); sharing reaction *code* (the channels
differ — compile-scope error lists vs. generator diagnostics — so reactions stay thin per-side
adapters over one shared semantics).

#### D8 — `CallTargetRules.cs`: the precedence order becomes one classifier both sides call

**Decision.** A pure classifier pins the name-resolution precedence
([01 F14](../research/generator-code-sharing/01-template-emitter.md)): ambient fill scope →
enclosing parse-context definitions → extension (extension beats registered function) →
registered function, bodiless and function-shape-compatible only (no chain parameter, no C#
expression) → unknown. Inputs are the name, `hasBody`, the `CallParameter`, and injected
predicates (`fillsContains`, `definitionExists`, `isExtension`, `isFunction`); output is a
`CallTargetKind`. The emitter's `BuildCall` head (`TemplateEmitter.cs:525-657`, verified) and
`BuildChainItemExpr` both route their dispatch through it (their *emission* per kind is
untouched); the runtime's `HeddleCompiler.CompileItem` adopts it for the same dispatch decision.
The classifier also pins, as an executable assertion rather than the current comment
(`TemplateEmitter.cs:625-627`), the registry invariant that default-function names do not
collide with built-in extension names — a lockstep test enumerates `DefaultFunctionTable`
against the built-in extension registry. **Rationale.** The precedence is pure string/flag
logic over linked types; the "asserted only in a comment" invariant is exactly the kind of
knowledge that must exist once. **Alternatives rejected:** classifying emission strategies too
(`out`/`partial`/branch/`list`/`for` special-casing is generator-tier knowledge with no runtime
twin — forcing it into the shared kind enum couples the classifier to one consumer's internals).

#### D9 — `EmbeddedCSharpNames.cs` + pin tests for the `.tcs` contract

**Decision.** Three shared consts (`model`/`chained`/`root`). The emitter's exclusion regexes
and its `var model = …` local emission (`BuildCSharpExpr`, `TemplateEmitter.cs:2086-2094`;
`EmitBodyClass`, `:2414-2415`/`:2453-2454` — verified) derive from them. Because the runtime's
side of the contract is literal text inside template files
(`src/Heddle/LanguageTemplates/CSharpClassTemplate.tcs:9` and `CSharpPreparseTemplate.tcs:11`
declare `(@(ModelType) model, @(ChainedType) chained, @(RootModelType) root)` — verified),
consts cannot flow into them; instead a pin test reads both embedded `.tcs` resources and
asserts their parameter lists spell exactly the three consts. **Rationale.**
[01 F16](../research/generator-code-sharing/01-template-emitter.md): renaming a `.tcs`
parameter silently changes what user C# means on the dynamic tier only; a red pin test is the
cheapest possible tripwire, and the consts remove the generator's second copy. **Alternatives
rejected:** generating the `.tcs` signature from the consts at runtime (rebuilds a working
text-template pipeline to avoid a two-line test).

#### D10 — Zero-output classification: a public `[ZeroOutput]` attribute, binder-read

**Decision.** New `ZeroOutputAttribute` in `Heddle.Attributes` (additive public API), applied to
the four directive extensions (`model`/`using`/`import`/`profile`). `ExtensionBinder` reads it
symbolically (by full name with inheritance, exactly like its existing `ScopeChannel` handling)
into `Info.IsZeroOutput`; `TemplateEmitter.IsZeroOutput`/`IsDirectiveName`
(`TemplateEmitter.cs:208-215`, verified) reclassifies via the binder, falling back to the name
list only for unresolvable names (a template with no reference closure yet). The runtime's
protocol (a null `InitStart` return — `HeddleCompiler`'s `returnTypeChainedPrevious == null`)
stays authoritative; a runtime test asserts, for every built-in registered extension, that
attribute presence and the null-`InitStart` protocol agree. The directive-*specific* name checks
(`ExtractDirectives`' handling of `model`/`using`, `MapProfilePerChain`'s `"profile"`) are out
of scope — they read directive payloads, not the zero-output classification. Custom extension
authors get the attribute documented in `custom-extensions.md` (same-change docs rule): declaring
it makes the precompiled tier remove the block from the piece stream exactly as the dynamic tier
does. **Rationale.** [01 F17](../research/generator-code-sharing/01-template-emitter.md) /
area 02 F4 both land here: today a new zero-output extension diverges silently (block kept as
rendered output precompiled, removed dynamically); a declarative marker is symbol-readable —
the only channel the generator has. **Alternatives rejected:** a linked `const string[]`
(fixes built-ins only; custom extensions stay divergent — the attribute solves both for one
extra public type); a flag on `ExtensionNameAttribute` (mutating a shipped attribute's surface
vs. one new opt-in attribute — the segregation rule in the coding standards prefers the latter).

#### D11 — `OutputProfileRules.cs` + `RenderTypeRules.cs`: runtime adoption first, generator adoption gated on phase 5

**Decision.** The two rule files land beside the enums they interpret, with explicit csproj
link lines added for the generator. Runtime adoption (`ProfileExtension.InitStart` parses via
`TryParse`; `HeddleCompiler.UnnamedCarrierName` and the render-type ternary route through the
rules) is self-contained and lands with the files. Generator adoption (`MapProfilePerChain`,
`AllocateEmptyExtension`, `DerivedRenderTypeLiteral`) requires the enums to compile inside the
generator, which is **phase 5's** enum-linking deliverable — until it lands, the generator keeps
its string/bool forms and this work item's generator half waits. **Rationale.**
[01 F1/F4](../research/generator-code-sharing/01-template-emitter.md): these are the
encoding-deciding rules — the highest-severity drift class in the file — and the pure parts are
two small pure functions. Splitting adoption avoids a cross-phase lockstep landing.
**Alternatives rejected:** neutral (non-enum) return types to duck the phase-5 dependency
(perpetuates the string-typed profile plumbing the program exists to delete); this phase linking
the enums itself (re-plans phase 5's owned artifact).

#### D12 — Body model-typing: linked data table + per-side conformance, no shared resolver

**Decision.** `BodyModelRules.cs` carries the table from
[01 F3](../research/generator-code-sharing/01-template-emitter.md):
`if/ifnot/elif/elseif/else → (Parent, —)`, `for → (Parent, Int32Index)`,
`list → (ElementOfData, —)`, definition body → `(Declared, —)`, caller content →
`(SlotOrData, —)`, region body → `(DeclaredOrParent, —)`. The two resolvers stay per-side
(symbol vs. reflection type derivation is Tier-3 material owned elsewhere); conformance is
enforced by tests: a generator-side test asserts each pinned emission branch (the branch trio,
`@for`, `@list`, `DefinitionBodyContext`, `SlotBodyContext`, `TryRegionBodyContext` —
`TemplateEmitter.cs:545-622` and the definition paths, verified) declares the table's source for
its body, and a runtime-side test asserts each built-in extension's observed typing matches its
row. **Rationale.** The rule exists today only as prose comments guarded by engine-assembly
checks; as linked data it becomes reviewable and diffable, and a change to (say)
`ListExtension`'s element-type derivation now fails a named test instead of silently keeping the
old generator typing. **Alternatives rejected:** a shared resolver over an `ITypeFacts` adapter
(Tier-3 scope, phases 3/4 territory, and the *choice* — not the type math — is the drift
surface here).

#### D13 — Strategy-shape / coercion-rail parity: normative spec text + differential tests, no code

**Decision.** The phase spec gains a normative "generated strategy shape" section pinning, as
contract: (1) the body is a document-ordered alternation of literal pieces and processor calls,
head/advance/tail exactly as both sides implement it today; (2) the value path coerces every
processor result with `as string ?? string.Empty` and concatenates in document order
(`EmitBodyClass`, `TemplateEmitter.cs:2475-2486`, verified — including the empty/single/concat
three-case shape); (3) the runtime's `NormalStrategy` piece-fallback (`?? element.Piece`) and
its `DocumentStrategy`/full-optimize short-circuits are byte-equivalent optimizations, and any
change to them or to the rail is a cross-tier contract change that must update this spec text
and both implementations together — the **joint-land rule** ratified by the OQ2 ruling: the
`OutExtension` comment (`src/Heddle/Extensions/OutExtension.cs:128-133`, verified) already flags
a planned rail change (non-string drop), and when it ships the runtime rail and the emitted
`Execute` shape change in the same landing, so the generated equivalent matches the dynamic
engine at all times (see *Open questions*, resolved).
New `StrategyShapeDifferentialTests` exercise the corners per the
[test matrix](phase-1-template-emitter-test-matrix.md). The shared offset-walk *code* is
phase 2's `DocumentShaping.cs`; when it lands, the emitter's walk consumes it and the spec text
stays the contract. **Rationale.** [01 F2](../research/generator-code-sharing/01-template-emitter.md):
every byte of every template flows through this shape, and its asymmetries are optimization
twins that cannot share code — spec + differential tests is the correct artifact class.

#### D14 — F20 cleanups: all of them, mechanical, snapshot-gated

**Decision.** All eight rows of [01 F20](../research/generator-code-sharing/01-template-emitter.md)'s
table are executed as one cleanup work item: one index-returning lone-surrogate scanner (in
`PieceWriter`, the emitter's copy deleted); one `StripGlobal` helper used by all four call
sites; version-less AQNs taken from `ExtensionBinder.Info.AqnSansVersion` /
`FunctionExportResolver.ExportEntry` instead of hand-concatenation in `ExtensionBindingsArray`
(`TemplateEmitter.cs:2591`, verified); one generic base-chain walk helper subsuming the five
near-identical walks (two of which move into `SlotRules` per D6); `RecordExtensionBinding`
collapsed to one method with a defaulted assembly parameter (`:2221-2231`, verified); the
manifest builders' shared prologue extracted (`BuildManifestEntry`/`BuildMarkerManifestEntry`,
`:2520-2568`, verified — the fingerprint/imports/key block is verbatim-duplicated);
`DedupeUnresolvable` memoized per emit; the escape tables unified on one table with string and
char adapters. **Rationale.** No cross-project constraints, pure DRY-on-knowledge; the
generator snapshot suite plus byte-identical goldens make every step mechanically verifiable.
**Alternatives rejected:** deferring to "whenever touched" (these rows are precisely the code
the extractions above will sit next to; doing them once now keeps every later diff clean).

### Work items

Ordering follows [07's sequencing](../research/generator-code-sharing/07-recommendations.md):
bugs (WI1–WI3, independently shippable, any relative order), then extractions by
effort-to-value, then spec/table pins, then cleanups. Per
[D5 of the cross-cutting decisions](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order),
item *N* may assume 1..*N*−1 merged; WI11's generator half additionally gates on phase 5 (an
explicit cross-phase gate, restated in *Dependencies & ordering*). Test fixtures per work item
are named in the [test matrix supplement](phase-1-template-emitter-test-matrix.md).

- **WI1 — needsLocals fix (D2).** Files: `TemplateEmitter.cs` (`PopulateBody` scan,
  `ScanHostsParticipant`, `BuildDefinitionCall` flag split, `AllocateDefinitionExtension`
  signature), `src/Heddle/Precompiled/PrecompiledRuntime.cs` (new `BindDefinition` overload,
  XML-doc'd). Byte/behavior impact: **yes — parity restoration** (see Back-compat).
  Completion: the four new differential fixtures render precompiled == dynamic; existing
  suites and snapshots green (snapshots re-ratified only for the emitted flag arguments).
- **WI2 — unknown `@profile` build error (D3).** Files: `TemplateEmitter.cs`
  (`MapProfilePerChain`), `Diagnostics/GeneratorDiagnostics.cs` (new descriptor), registry table
  row (same change), `docs/precompilation.md` diagnostics list. Behavior impact: **yes — build
  error on previously-silently-mis-precompiled templates** (see Back-compat). Completion:
  positioned-diagnostic negative test green (`HED70xx` ID + position, per
  [testing standards](../spec/common/testing-standards.md#fixtures-and-goldens)); the same
  fixture's dynamic compile still yields `HED2001`.
- **WI3 — `DefaultConvertible` alignment (D4).** Files: `TemplateEmitter.cs`
  (`DefaultConvertible`, `TryFormatPropValue`, `NullDefaultLegal` — shared nullable probe).
  Byte impact: none (tier change only — affected templates move from dynamic to precompiled,
  differential-gated). Completion: `Nullable<S>`-default fixtures precompile and render
  byte-identically on both tiers; a theory covering the runtime's `PropConversion` row set
  passes symbol-side.
- **WI4 — `ParticipantScan.cs` (D5).** Files: new shared file; `TemplateEmitter.cs` deletes
  both scans and adopts; lockstep test. Byte impact: none beyond WI1 (WI4 is the refactor of
  WI1's patched logic into the shared form). Completion: generator has zero private participant
  scans; lockstep test green over the corpus including the shadowed-name over-provision fixture.
- **WI5 — `SlotRules.cs` (D6).** Files: new shared file; `OutExtension.cs` delegates;
  `TemplateEmitter.cs` (three call sites); `HeddleCompiler.cs` (`ResolveSlotType`). Byte
  impact: none. Completion: `HasOutValue` exists once; slot fixtures byte-identical; the
  five-way theory passes against both call paths.
- **WI6 — `RegionFillResolver.cs` (D7).** Files: new shared file; `TemplateEmitter.cs`
  (`TryBuildGeneratorFillScope` becomes an adapter with runtime-matched reactions);
  `Diagnostics/GeneratorDiagnostics.cs` (the `HED5019`-twin descriptor), registry table row
  (same change). Behavior impact: **yes — error-matching alignment per the OQ3 ruling**
  (dangling/foreign-origin/default-missing candidates no longer refuse to precompile;
  private-region fills error at build — see Back-compat). Byte impact: none for correct
  templates. Completion: region/fill suites (`RegionTests`, `CompositionTests`) green
  unchanged; verdict-enum theory asserts both sides' reactions match per verdict; the
  dangling and private-region fixtures behave per the runtime (matrix); phase 2 handoff note
  recorded in the spec.
- **WI7 — `CallTargetRules.cs` (D8).** Files: new shared file; `TemplateEmitter.cs` (dispatch
  in `BuildCall`/`BuildChainItemExpr`); `HeddleCompiler.cs` (dispatch in `CompileItem`). Byte
  impact: none. Completion: precedence theory (definition-shadows-branch,
  extension-beats-function, function-shape refusals) passes against both sides; the
  default-function/extension-name collision invariant test green.
- **WI8 — `EmbeddedCSharpNames.cs` (D9).** Files: new shared file; `TemplateEmitter.cs`
  (regexes, locals); pin test over both `.tcs` resources. Byte impact: none. Completion: pin
  test red when a `.tcs` parameter is renamed (proven by mutation during review), green as-is.
- **WI9 — `[ZeroOutput]` attribute (D10).** Files: new attribute; the four directive
  extensions; `ExtensionBinder` (`Info.IsZeroOutput`); `TemplateEmitter.cs`
  (`IsZeroOutput` via binder); `docs/custom-extensions.md`. Byte impact: none for built-ins
  (same four names classify identically); **enables** custom zero-output extensions to
  precompile correctly (previously divergent — new capability, additive). Completion:
  runtime protocol-agreement test green; a custom `[ZeroOutput]` extension fixture renders
  identically on both tiers.
- **WI10 — `BodyModelRules.cs` (D12).** Files: new shared file; conformance tests both sides.
  Byte impact: none. Completion: both conformance suites green; the table is cited from the
  emitter's branch comments (comment-to-data pointer, no logic change).
- **WI11 — `OutputProfileRules.cs` + `RenderTypeRules.cs` (D11).** Files: two new `Data/`
  files + two csproj link lines; runtime adoption immediately; generator adoption **after
  phase 5's enum links merge**. Byte impact: none. Completion (runtime half): profile/encoding
  suites green unchanged; (generator half): `MapProfilePerChain`/`AllocateEmptyExtension`/
  `DerivedRenderTypeLiteral` consume the rules, `ProfileFlipTests` and encoding differentials
  green, the emitter's private ternary deleted.
- **WI12 — strategy-shape parity spec + tests (D13).** Files: spec text (phase spec);
  `StrategyShapeDifferentialTests` + corpus entries. Byte impact: none (pins the current
  rail as-is — verified matching on both sides today, so the OQ2 ruling's "fix where needed"
  clause has no present work). Completion: every matrix row green; the spec section
  cross-references the `OutExtension` planned-change comment and records OQ2's resolved
  joint-land rule.
  - Per the OQ2 ruling, the emitted `Execute` shape must match the runtime rail at all
    times: when the rail change ships, runtime, generator, and this WI's spec text move in
    **one landing** (a breaking-window item — see Back-compat). Until then WI12 keeps
    pinning the current rail, and the `strategy-nonstring-value` fixture is the tripwire
    that landing must consciously edit.
- **WI13 — F20 cleanups (D14).** Files: `TemplateEmitter.cs`, `PieceWriter.cs`,
  `NativeExpressionWriter.cs`, `ExtensionBinder.cs`, `FunctionExportResolver.cs`. Byte impact:
  none. Completion: generator snapshot suite byte-identical; each table row's "copies"
  reduced to one, checked off in the spec.

## Dependencies & ordering

- **Phase 0 posture (landed):** every test in this phase runs under the gauntlet-crossing guardrails — see the
  [precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture) rule. This phase's
  fix-first group un-skips phase 0's quarantined `NonLeftmostScopeChannelParticipant_ProvisionsLocalsOnBothTiers`
  fixture (F11) as acceptance evidence.

- **Consumed from phase 2** (document shaper): the shared `DocumentShaping.cs` offset-walk /
  `SlicePieces` (this phase's WI12 pins the contract; the emitter's walk migrates onto phase 2's
  code when it lands — a forward pointer, not a gate) and the runtime-side adoption of
  `RegionFillResolver` (this phase lands the file and emitter side in WI6; phase 2 completes the
  pair). Neither blocks any WI here.
- **Consumed from phase 3** (binding layer): `PropLayoutCore<TType>` and the assignability
  conformance corpus — the structural successors of the code WI3 point-fixes; the shared AQN
  formatter (area 03 F1) that will subsume WI13's `AqnSansVersion` reuse. Not gates.
- **Consumed from phase 4** (expression writers): the `PrimitiveKind` neutral enum and the shared
  implicit-numeric table, which will replace the emitter's `IsImplicitNumericWidening` that WI3
  leaves otherwise untouched. Not a gate.
- **Consumed from phase 5** (pipeline/config): the linked `OutputProfile`/`ExpressionMode`/
  `RenderType` enums. **Hard gate for WI11's generator half only**; everything else in this
  phase is independent of phase 5.
- **Supplied to phase 5** (pipeline/config, per the Q2.2 fallback-legitimacy ruling): the
  taxonomy of this phase's *intentional* emitter refusals — the points where the emitter
  deliberately returns/degrades to the dynamic tier rather than throwing (e.g. WI3's remaining
  `DefaultConvertible` over-refusals, WI9's unresolvable-name fallback branch). Phase 5
  consumes it when narrowing the blanket `catch (Exception)` in `HeddleTemplateGenerator.cs`
  to a researched legitimate-fallback set; everything outside the taxonomy is a defect that
  must throw and surface. Not a gate in either direction. Note: after the Q1.3 ruling,
  region-fill fault verdicts are *no longer* refusals and do not enter the taxonomy.
- **Internal ordering:** WI1–WI3 first (any order, independently shippable); WI4 after WI1
  (it refactors WI1's fix); WI5–WI10 in listed order (effort-to-value, no hard dependencies
  among them); WI11's runtime half any time, generator half after phase 5; WI12 any time after
  WI1 (its fixtures assume the locals fix); WI13 last (it cleans the code the extractions
  finish reshaping).
- **Unblocks:** phase 2's `RegionFillResolver` runtime adoption (file exists after WI6);
  phase 3's fault-enum work (WI6's verdict-enum precedent); the program-wide differential-corpus
  guardrails listed in [07](../research/generator-code-sharing/07-recommendations.md) gain their
  area-01 entries here.

## Back-compat / impact

Per instruction from the [breaking-windows policy](../spec/common/breaking-windows.md) and
[D2](../spec/common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows),
every byte- or behavior-affecting change is called out individually:

- **WI1 (needsLocals) — behavior-affecting, parity restoration, not window-gated.** Templates
  whose only `[ScopeChannel]` participant sits in a nested chain parameter currently render
  *differently* precompiled vs. dynamic (stale/parent branch state — drift #3); templates whose
  definition body and caller content differ in participation get an over-shared frame on the
  precompiled tier. The fix changes precompiled output **only for templates in the drift set**,
  and changes it *to the bytes the dynamic tier already produces*. Rationale for not
  window-gating: the breaking-windows policy batches ratified *contract changes* — changes to
  the engine's correct, documented behavior. Precompilation's contract is byte-parity with the
  dynamic compile (the gauntlet, the `HED7016` drift-warning machinery, and the differential
  suite all exist to enforce exactly this); output that violates parity is a defect, and the
  [testing standards' fix-forward rule](../spec/common/testing-standards.md#the-canonical-loop)
  governs defect fixes. The dynamic tier — the behavior hosts see whenever precompilation is
  absent or falls back — is unchanged. Deliverable: a release-notes entry naming the drift set
  ("templates with non-leftmost scope-channel participants; definition calls with asymmetric
  carrier participation").
- **WI2 (`@profile` unknown value) — build-surface change, not window-gated.** Projects that
  precompile a template containing `@profile(){{typo}}` currently build and render (with the
  flip silently ignored — output the dynamic tier would refuse to compile). After WI2 they get
  a positioned build error. This surfaces an *existing* template error earlier; the authoritative
  tier already rejects these templates with `HED2001`, so no correct template is affected.
  Precedent: `HED7006`/`HED7017` already hard-error at build for runtime-rejected states.
- **WI3 (`DefaultConvertible`) — tier change only, byte-neutral.** Previously-refused templates
  (a `Nullable<S>` default on a `Nullable<W>` prop) move from dynamic fallback to precompiled;
  their bytes are proven identical by the differential gate before the change ships. No
  currently-precompiled template changes bytes.
- **WI6 (region-fill reactions) — error-matching alignment, not window-gated.** Per the OQ3
  ruling, the generator stops refusing to precompile on fault verdicts and reacts as the
  runtime reacts. Every affected template is an error template on the authoritative tier
  already: a dangling candidate carries a parse-emitted error on both tiers (unchanged — the
  generator merely stops un-precompiling over it), and a private-region fill that previously
  refused (deferring the runtime's `HED5019` to first dynamic render) now fails the build with
  the matching positioned error. No correct template changes bytes or tier; same
  earlier-surfacing rationale and precedent as WI2.
- **Coercion-rail change (OQ2 ruling) — future, window-gated, joint-land.** Not shipped by
  this phase. When the planned non-string rail change is scheduled into the
  [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register),
  it lands as **one window item** covering the runtime rail, the emitted `Execute` shape, and
  WI12's spec text — the generated equivalent matches the dynamic engine at all times, so the
  two tiers never publish different rails.
- **WI9 (`[ZeroOutput]`) and WI1's `BindDefinition` overload — additive public API.** New
  attribute type; new overload with existing overloads retained (assemblies generated by older
  generator versions continue to bind — the overload addition follows the
  [API compatibility rules](../spec/common/coding-standards.md#api-design-and-compatibility)).
  Both carry XML docs and same-change updates to `custom-extensions.md` /
  `precompilation.md` as applicable.
- **All remaining extractions (WI4–WI5, WI7–WI8, WI10–WI13) — byte-neutral by construction**, gated by the
  existing snapshot, golden, and differential suites; goldens change for **no** existing
  fixture (a golden diff in these WIs is a defect, per the golden-change policy). New fixtures
  pin the previously-divergent behaviors; no existing golden is regenerated.
- **Diagnostic registry.** The two new IDs (D3's `@profile` error, D7's `HED5019` twin) are
  claimed in the
  [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) by the
  phase spec in the same change that introduces each; no shipped ID is reused or renumbered.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| WI1's flag split changes frames for templates *outside* the identified drift set (unforeseen interaction with the pre-mark in `GetOrBuildDefinitionBody`, `TemplateEmitter.cs:1244-1246`, which seeds `HostsParticipant` from the definition context before population) | The pre-mark path is re-derived in the spec against the full recursive scan; the differential corpus gains fixtures for self-calling definitions with and without participants; full suite must be byte-identical outside the new fixtures | M |
| The parse-level shared scan and the runtime's compiled-tree scan disagree somewhere the lockstep corpus misses (e.g. a definition shadowing a `[ScopeChannel]` extension name) | The shadowing case is analyzed in the spec (over-provision only, behavior-invisible) and pinned by an explicit fixture; the lockstep test runs over the whole differential corpus, not a hand-picked list | M |
| Runtime adoption in WI5/WI7 (`SlotRules`, `CallTargetRules` inside `HeddleCompiler`) perturbs dynamic-tier behavior — the tier every host relies on | Adoption is delegation-only (identical logic relocated); the full `Heddle.Tests` suite on all TFMs plus goldens byte-identical is the gate; any behavioral diff is a red gate, fix-forward | M |
| Phase 5's enum linking slips and WI11's generator half stalls, leaving the string-typed profile plumbing live longer | WI11 is split by design (runtime half independent); the generator half is a small mechanical adoption once unblocked; no other WI depends on it | S |
| `netstandard2.0` / no-Roslyn constraint violated by an extracted file (e.g. an accidental `ITypeSymbol` in a signature) | Constraint is restated per file in the spec's API contract; the generator build itself enforces it (linked files compile in a `netstandard2.0` analyzer project with no Roslyn reference available to shared code) — violation is a build break, not a latent defect | S |
| The gauntlet does not cover render-type/profile decisions, so a defect in WI11's rule files would be silent wrong output | The encoding differential fixtures (`@profile` flip matrix, `[EncodeOutput]`/`[NotEncode]` truth table) assert bytes on both tiers before and after adoption; the truth-table theory enumerates all four bool pairs | M |
| WI2's build error surprises a host with a long-broken but never-dynamically-rendered template | Release-notes entry with the exact message and fix (correct the `@profile` value); the error names valid values, per the error-message standard | S |
| Snapshot churn from WI13 obscures a real regression | WI13 lands last and alone (no logic change in the same PR); snapshot diffs are reviewed against the F20 table row-by-row | S |

## Success criteria

- [ ] A template whose only `[ScopeChannel]` participant appears as a nested chain parameter
      renders byte-identically precompiled and dynamic (currently divergent); a definition call
      whose body participates but whose caller content does not (and vice versa) provisions
      frames per-carrier exactly as the dynamic tier does.
- [ ] `PrecompiledRuntime.BindDefinition`'s existing overloads are binary-unchanged; the new
      per-carrier overload is additive and XML-documented.
- [ ] A template containing `@profile(){{<unknown>}}` produces a positioned generator build
      error with the claimed `HED70xx` ID, and the same fixture produces `HED2001` from the
      dynamic compiler; the registry table contains the new ID in the same change.
- [ ] A dangling region-fill candidate precompiles with the runtime's skip semantics (the
      parse-emitted error surfaces identically on both tiers; no refusal); a private-region
      fill fails the build with the positioned `HED5019`-twin error while the same fixture's
      dynamic compile raises `HED5019` (the OQ3 match principle).
- [ ] A `Nullable<S>` default on a `Nullable<W>` prop precompiles; its rendered bytes equal the
      dynamic tier's; the emitter contains exactly one nullable-underlying probe.
- [ ] All nine inventory files exist, compile in both `Heddle` and `Heddle.Generator`
      (`netstandard2.0`, no Roslyn types in shared sources), and each "replaces" cell's
      generator-side copy is deleted (verified by review checklist in the spec).
- [ ] `OutExtension.HasOutValue`'s logic exists exactly once; `BuildOutCall` no longer contains
      the `!cp.IsModelTypeParameter` approximation.
- [ ] The call-target precedence, region-fill matching verdicts (reactions matching the
      runtime per the OQ3 ruling), body model-typing table,
      embedded-C# names, and zero-output classification each have a green lockstep/conformance/
      pin test naming the rule (fixtures per the
      [test matrix](phase-1-template-emitter-test-matrix.md)).
- [ ] The strategy-shape spec section exists and every `StrategyShapeDifferentialTests` row is
      green, including the non-string-processor-result pin.
- [ ] Every F20 table row's copy count is 1; the generator snapshot suite is byte-identical
      across WI13.
- [ ] One combined run: full solution build, full `Heddle.Tests` + generator test suites on all
      TFMs, goldens byte-identical for every fixture this phase did not add — per the
      [regression gate](../spec/common/testing-standards.md#regression-gates); no benchmark
      regression (compile-path only; render path untouched except WI1's strictly-fewer frames).

## Validation scenarios

| Input | Expected outcome |
|---|---|
| Fixture with `@else` reading branch state set by a participant nested in a chain parameter, rendered on both tiers | Identical bytes; before WI1 the precompiled render shows stale/parent state — the fixture is the regression pin |
| Definition call: body has a participant, caller content does not | Precompiled inner carrier gets a fresh frame, outer carrier gets the clear-or-passthrough behavior — matching `AbstractExtension`'s dynamic-tier rules; bytes identical across tiers |
| `@profile(){{htlm}}` in a precompiled template | Build fails with the new positioned `HED70xx` error naming valid values; dynamic compile of the same template yields `HED2001` at the same position |
| `@profile(){{HTML}}` (case variant) | Parses as `Html` on both tiers (ordinal-case-insensitive), no diagnostic — the rule file's `TryParse` theory covers case and whitespace variants |
| Extension `[Prop("x", typeof(long?))]` called with an `int?` constant default | Precompiles after WI3; rendered value's boxed CLR type matches the runtime prototype (`Convert.ChangeType` reproduction) |
| A custom extension marked `[ZeroOutput]` whose block sits mid-document | Block removed from output on both tiers; before WI9 the precompiled tier renders divergently |
| A definition named `if` shadowing the branch keyword | Both tiers resolve the definition (classifier precedence); lockstep theory asserts the shared classifier and both dispatch sites agree |
| A dangling region-fill candidate | Both tiers skip the candidate (`continue`) and keep the parse-emitted base-not-found error; the generator no longer refuses to precompile; the verdict theory asserts the reactions match (the OQ3 match principle) |
| A call-site fill of a private region | Dynamic compile raises `HED5019`; the precompiled build fails with the matching positioned twin error (same message and override anchoring, retract mechanics reproduced) |
| A `.tcs` template parameter renamed (mutation test during review) | The embedded-C# pin test fails naming the const that no longer matches |
| A body whose only processor returns a non-string on the value path | Both tiers drop it to `string.Empty` (the pinned rail); the fixture documents the rail so a future rail change must touch it |
| Any fixture that existed before this phase | Byte-identical goldens; any diff is a defect by the golden-change policy |

## Open questions

All four questions are **resolved** (user, 2026-07-25); the rulings are recorded in the
[open-questions register](open-questions.md) (Q1.1–Q1.4) and folded into the design decisions
and work items above. None remain open; the entries below record each ruling in place.

- **OQ1 — `maxRecursionCount` and the options fingerprint
  ([01 F19](../research/generator-code-sharing/01-template-emitter.md)).** Resolved (user,
  2026-07-25): recommendation applied — the call is decided once in the precompilation spec,
  which owns it; the default posture records the build-baked divergence as intentional (D23)
  unless that spec adds the field at the next schema bump. This phase changes nothing either
  way ([register Q1.1](open-questions.md)).
- **OQ2 — the non-string coercion rail's planned change.** Resolved (user, 2026-07-25): the
  generated equivalent must match the dynamic engine — when the runtime rail changes, the
  emitted `Execute` shape changes in the same landing (the **joint-land rule**), and any
  present mismatch is fixed now (authoring-time verification found none: both sides implement
  the `as string ?? string.Empty` rail today, per D13). WI12 keeps pinning the current rail
  until the change ships; the rail change stays a breaking-window candidate that moves both
  tiers plus WI12's spec text through the window together
  ([register Q1.2](open-questions.md); folded into D13, WI12, and Back-compat).
- **OQ3 — runtime behavior on dangling region-fill candidates.** Resolved (user, 2026-07-25) —
  **the match principle**, recorded program-wide in the register: the generator matches the
  runtime's validation rules and mechanics, behaving as if it were part of the dynamic engine.
  Dangling candidates are skipped as the runtime skips them (no hard refusal, no
  un-precompile), and cases the runtime raises as errors (the `HED5019` retract path for
  private-region fills) surface as matching build-time errors. The build tier cannot always
  carry the same *warnings* through the same channel — such differences may legitimately
  exist, but the program strives for matching, and errors always match
  ([register Q1.3](open-questions.md); folded into D7, WI6, Back-compat, and the validation
  scenarios).
- **OQ4 — generator over-provision on shadowed participant names.** Resolved (user,
  2026-07-25): recommendation applied — D5's safe over-provision is kept (behavior-invisible,
  emit-time-only); revisit only on the named trigger: the shadowing lockstep fixture's
  "over-provision asserted" branch ever needing to change, at which point the shared scan
  gains a `definitionExists` predicate. No design change ([register Q1.4](open-questions.md)).

## External grounding

| Claim | Source |
|---|---|
| All twenty area findings, kind classification (A/B), and the recommended extraction order this plan executes | [01 — template emitter](../research/generator-code-sharing/01-template-emitter.md) |
| Live-drift ranking (#3, #12, #13), tier model, shared-layout convention (`Language/**` auto-glob, `Data/` per-file links), sequencing (bugs → links → Tier 2 → spec pins), test guardrails | [07 — recommendations](../research/generator-code-sharing/07-recommendations.md) |
| Generator csproj links `..\Heddle\Language\**\*.cs` (excl. `DocumentParser.Runtime.cs`) and individual `Data/` files; `netstandard2.0`; Roslyn `PrivateAssets` | `src/Heddle.Generator/Heddle.Generator.csproj` *(read)* |
| Leftmost-only scans; OR'd carrier flag; pre-mark seeding | `TemplateEmitter.cs` — `PopulateBody` (:371-375), `ScanHostsParticipant` (:1404-1416), `BuildDefinitionCall` (:1137-1140), `GetOrBuildDefinitionBody` (:1244-1246) *(read)* |
| Runtime recursive scan with carrier unwrap; frame clear for non-participating bodies; single-flag `BindDefinition` | `src/Heddle/Runtime/RuntimeDocument.cs` — `ItemNeedsLocals` (:168-190); `src/Heddle/Core/AbstractExtension.cs` — `GetInnerResult` (:36-43); `src/Heddle/Precompiled/PrecompiledRuntime.cs` — `BindDefinition` *(read)* |
| Runtime `HED2001` on unknown `@profile`; emitter's silent fall-through | `src/Heddle/Extensions/ProfileExtension.cs` — `InitStart` (:26-37); `TemplateEmitter.cs` — `MapProfilePerChain` (:418-427) *(read)*; `HeddleDiagnosticIds.UnknownOutputProfile` = `HED2001` *(read)* |
| Missing `S?→W?` row; `ConstructedFrom` vs `OriginalDefinition` split | `src/Heddle/Runtime/Expressions/PropConversion.cs` — `CanConvertTypes` (:44-46); `TemplateEmitter.cs` — `DefaultConvertible` (:942-974), `TryFormatPropValue` (:1510-1514) *(read)* |
| Canonical `HasOutValue`; approximate generator probe; duplicate slot walks | `src/Heddle/Extensions/OutExtension.cs` — `HasOutValue` (:143-155); `TemplateEmitter.cs` — `BuildOutCall` (:1152-1155), `DefinitionHasSlot` (:1644-1650) *(read)* |
| Fill-matching loop and runtime reactions (skip-on-dangling, `HED5019` retract-and-raise); shared `DefinitionMaterializer` | `TemplateEmitter.cs` — `TryBuildGeneratorFillScope` (:1300-1339), `Rebound` (:1275-1286); `src/Heddle/Runtime/HeddleCompiler.cs` — `BuildRegionFillScope` (:1686-1735), `RetractCandidateError` (:1744-1748) *(read)* |
| Precedence chain incl. comment-only registry invariant; strategy shapes and the coercion rail; manifest duplication; `Mode()` default | `TemplateEmitter.cs` — `BuildCall` (:506-657), `EmitBodyClass` (:2402-2493), `BuildManifestEntry`/`BuildMarkerManifestEntry` (:2520-2568), `Mode` (:2572-2580) *(read)* |
| `.tcs` identifier contract as literal text | `src/Heddle/LanguageTemplates/CSharpClassTemplate.tcs` (:9), `CSharpPreparseTemplate.tcs` (:11) *(read)* |
| `HED7001`–`HED7017` claimed; registry rules; twin-ID precedent (`HED7017`); amendments ledger | `src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs` *(read)*; [cross-cutting decisions](../spec/common/cross-cutting-decisions.md) |
| Window policy scope (ratified contract changes, one migration per window); fix-forward rule; golden-change policy; suite homes and differential precedent | [breaking-windows.md](../spec/common/breaking-windows.md); [testing-standards.md](../spec/common/testing-standards.md); `src/Heddle.Generator.IntegrationTests/DifferentialHarness.cs`, `CorpusDifferentialTests.cs`, `ProfileFlipTests.cs`; `src/Heddle.Generator.Tests/DefaultFunctionLockstepTests.cs` *(listed/read)* |
| Style, prose-first structure, no-open-questions discipline this plan follows | [spec-conventions.md](../spec/common/spec-conventions.md); [coding-standards.md](../spec/common/coding-standards.md) |

Line numbers above were verified against the working tree while authoring this plan and are
cited with their anchor members because they will drift; the spec re-verifies each seam per the
*(verify at implementation)* convention.

---

## Normative — the generated strategy shape and the coercion rail (D13 / WI12)

This section is contract, not description. It is the artifact D13 calls for: the two backends
implement the same body shape with different optimizations, so the shape cannot be shared as code
and is pinned here plus in `StrategyShapeDifferentialTests`.

1. **Document-ordered alternation.** A body is a document-ordered alternation of literal pieces
   and processor calls: head piece (when the first element starts past offset 0), then each
   element's processor, advancing the offset past it, then the tail piece. The walk itself is
   phase 2's shared `DocumentShaping.SlicePieces<T>`; both tiers consume it, so the *segmentation*
   is code-shared and only the emission differs.
2. **Render path.** Pieces are written straight to the sink; each processor is invoked for its
   render effect. No coercion happens on this path — a processor that emits a boxed non-string
   stringifies it itself (`OutExtension.RenderData` is the canonical example).
3. **Value path — the coercion rail.** Every processor result is coerced with
   `as string ?? string.Empty` and the parts are concatenated in document order, in a three-case
   shape: no parts → `string.Empty`; one part → that part; more → `string.Concat(…)`. The runtime's
   `NormalStrategy` adds a piece fallback (`?? element.Piece`) and its `DocumentStrategy` /
   full-optimize short-circuits skip the concat entirely; those are **byte-equivalent
   optimizations** of this rail, not a second rail.
4. **The render/value asymmetry is deliberate and pinned.** A boxed non-string reaching the value
   path is dropped to empty while the render path stringifies it. `OutExtension` flags this as
   slated to change.
5. **Joint-land rule (Q1.2, ratified).** Any change to the rail, to the three-case shape, or to the
   optimizations above is a **cross-tier contract change**. The runtime rail, the emitted `Execute`
   shape and this section move through the breaking window in **one landing**, so the generated
   equivalent matches the dynamic engine at all times. The tripwire that landing must consciously
   edit is `StrategyShapeDifferentialTests.TheEmittedValuePathCarriesThePinnedCoercionRail` together
   with its render-path companion. Authoring- and implementation-time verification both found **no
   present mismatch**: both tiers implement `as string ?? string.Empty` today.

## Implementation record

**Landed 2026-07-26.** Suite: 4642 passed, 0 failed, **0 skipped** (from 4246/2 skipped);
`dotnet build Heddle.sln -c Debug` and `dotnet test Heddle.sln -c Debug` green on every runnable
TFM (`Heddle.Tests`' net6.0 leg cannot run on this box — pre-existing, unrelated).

### Fix group

- **WI1 (D2) — `needsLocals`.** Both generator probes replaced by the shared, full-chain,
  parameter-recursing `Language/ParticipantScan.cs`; `BuildDefinitionCall` stopped OR-ing the two
  carriers' flags and now passes each its own, through a new additive
  `PrecompiledRuntime.BindDefinition(… bodyNeedsLocals, callerContentNeedsLocals …)` overload
  (existing overloads retained and unchanged; the 10-arg one now forwards with both flags equal, so
  it is byte-identical to before). `PrecompiledSchema` bumped 4→5 with a
  `PerCarrierLocalsSchemaVersion` gate, following phase 4's `DynamicMemberRouting` precedent.
  Pinned by `ScopeParticipantDifferentialTests` (five fixtures; the two asymmetric ones verified red
  against the reverted OR) and `ParticipantScanLockstepTests`.
  - **Correction to the plan.** The scan half is **latent** on the precompiled tier, not
    observable: every shape that puts a participant off the leftmost position is one the emitter
    already refuses for an unrelated reason. The *observable* half of drift #3 is entirely the
    carrier-flag OR, and the fixtures had to be built around a non-`[ScopeChannel]` reader
    (`@peek`) to make it visible at all.
- **WI2 (D3) — unknown `@profile`.** `MapProfilePerChain` gained the runtime's third branch:
  `HED7022`, Error, at the directive, one per directive. Twin asserted against `HED2001` in the same
  test, including identical anchoring.
- **WI3 (D4) — `DefaultConvertible`.** The `Nullable<S> → Nullable<W>` row added and the file's two
  nullable probes folded onto one `TryGetNullableUnderlying` over `OriginalDefinition`. Pinned by
  the linked `PropDefaultConversionVectors` driven through both tiers.
  - **Correction to the plan.** The new row is **unreachable from an extension `[Prop]` default**:
    an attribute constant's `TypedConstant.Type` can never be a `Nullable<S>`. It is landed for
    table identity with `PropConversion` (and is asserted as such); the user-visible enabling delta
    on that path is nil.

### Extractions

`Language/ParticipantScan.cs`, `Language/SlotRules.cs`, `Language/CallTargetRules.cs`,
`Language/BodyModelRules.cs`, `Language/Expressions/EmbeddedCSharpNames.cs`,
`Data/OutputProfileRules.cs`, `Data/RenderTypeRules.cs` (the last two with one csproj link line
each, plus `Data/RenderType.cs`); `Language/RegionFillResolver.cs` (phase 2's file) adopted
emitter-side. Structural acceptance is asserted by `EmitterSharedRuleAdoptionTests`; byte
neutrality by the unchanged snapshot/golden/differential suites (the only snapshot delta across the
whole phase is `schemaVersion: 4` → `5`).

- **WI6 (D7 / Q1.3)** is the behavior-affecting extraction: the emitter no longer refuses to
  precompile on a fault verdict. Dangling/default-missing candidates are skipped and their
  parse-emitted error is now **forwarded at build** (it used to be filtered out of the build
  channel entirely, so it surfaced only on the first dynamic render); a private-region fill
  retracts that error and raises `HED7024`, the `HED5019` twin.
  - **Bounded residual, recorded.** The surviving-candidate forwarding is gated on a completed body
    build: when the emitter degrades for an unrelated reason it never visits the call sites and has
    no verdict to report, so those templates keep the pre-phase-1 behavior (the dynamic tier
    raises). Closing it means resolving fill scopes in a pass independent of emission.
- **WI7 (D8)** fixed a real inversion found during the work: the emitter tried the function tier
  **before** the extension binder, so a host-exported function sharing a name with a registered
  extension bound as a function at build and as the extension at run. The shared classifier's
  extension-wins arm resolves it to the runtime's answer.
- **WI9 (D10)** — `[ZeroOutput]`, additive public API, applied to the four built-in directives and
  read symbolically by `ExtensionBinder` into `Info.IsZeroOutput`. The hard-coded name list survives
  only as the unresolvable-name fallback.

### Not folded

The **definition-side** `PropLayout.Resolve` / `TemplateEmitter.ResolvePropLayout` pair is
**declined**, for the reason phase 3 anticipated. `PropLayoutCore.Build` runs name-validity,
reserved-name, same-level-duplicate and unusable-type checks that the definition path deliberately
does not (they are parser pre-checks there), and it owns a different message vocabulary for both
the unresolved-type and the re-declaration faults. Folding without changing those four messages is
not possible, and folding *with* the extra checks would make the build tier stricter than the
engine — a match-principle violation. The index-ordering rule the fold was meant to protect is
already gated end-to-end by the props differentials, which compare rendered bytes and therefore
slot indices.
