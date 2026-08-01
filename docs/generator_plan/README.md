# Generator ↔ engine code-sharing resolution — plan

> A seven-phase program (0–6) that resolves the duplication documented in
> [docs/research/generator-code-sharing/](../research/generator-code-sharing/00-overview.md):
> phase 0 first makes the test suite fail loudly on any unintended precompiled→dynamic
> fallback (the program's step 0 and hard gate); then every rule the source generator and
> the runtime engine currently maintain as two hand-kept copies moves into one shared
> artifact (linked source, rule table, or spec-pinned contract), and the fifteen verified
> live drifts — several of them user-visible bugs — are fixed first, as independently
> shippable groups inside each phase, each verified under the phase-0 guardrails.

**All seven phases (0–6) are implemented.** The quarantine register phase 0 opened is empty — every
red fixture was earned green by its owning phase, none deleted and none weakened (one, phase 1's
F11, was *reshaped*; see that phase's record). Suite: **5348 passed / 0 failed / 0 skipped**
against a pre-program baseline of 2630 / 0 / 0 — the count more than doubled, and most of the growth
came *after* the phases, from the post-implementation audits and rulings adding coverage the phases
had claimed but not held (see the findings section). Diagnostic IDs `HED7018`–`HED7028` were claimed
in registry order. One item is recorded as **not delivered** and named here rather than buried in a
phase: phase 2's WI9 paired before/after benchmark (argued structurally, not measured). The
[compile-channel drain gap](#closed-program-level-gap--the-compile-channel-drain) below was the
second, and is now closed.
Phase numbering follows the research-document
numbering (area 0N → phase N), not execution order; execution order is governed by phase 0
(the program's step 0 and gate), the fix-first groups, and the artifact-ownership
dependencies below. **[Phase 7](phase-7-shared-test-corpus.md) is a follow-on, not one of
the original seven**: it extends the same single-artifact principle from production rules to *test
inputs* — hand-kept duplicate test inputs being hand-kept duplicate rules one level up — and owned
the D4 coverage residue that finding 7 below records, which is now closed. **[Phase 8](phase-8-docs-sweep.md) is the
second such follow-on** (from the Q8.7 ruling, implemented 2026-07-26): it applies the same principle
to the *prose*, starting with the documents this program's own authority convention makes normative —
where a wrong sentence is a latent bug rather than a nuisance, as four of them turned out to be.

## Phases

| # | Phase | Depends on | Goal (one line) | Status |
|---|---|---|---|---|
| 0 | [test-fallback-guardrails](phase-0-test-fallback-guardrails.md) | — (**gates 1–6**) | Make the test suite structurally unable to pass through an unintended fallback — resolver/gauntlet-crossing renders of real generator output with fallback-as-failure (Strict + sentinel), corpus-wide; fallback exercised only by tests that declare it; known live drifts handed to phases 1–6 as quarantined red fixtures | **implemented (2026-07-25)** — WI1–WI8 landed; five quarantined fixtures registered, posture pinned in [testing-standards](../spec/common/testing-standards.md#precompiled-tier-posture) |
| 1 | [template-emitter](phase-1-template-emitter.md) | 0 (gate); 5 (enum links, hard gate for one WI); consumes 2, 3, 4 | Resolve the emitter↔runtime drift — three live bugs fixed first (needsLocals scan + per-carrier flags, unknown-`@profile` silent acceptance, `DefaultConvertible` gaps), then nine linked shared sources and the representation-bound rules pinned as data tables plus differential tests | **implemented (2026-07-26)** — D1–D14 / WI1–WI13 landed; the program's **last** quarantined fixture un-skipped (reshaped — the participant-scan drift is latent on the precompiled tier, so it now pins degrade-parity + the reachable neighbour + the scan rule) so the register carries **zero skips**; shared cores in [`Language/ParticipantScan.cs`](../../src/Heddle/Language/ParticipantScan.cs), [`SlotRules.cs`](../../src/Heddle/Language/SlotRules.cs), [`CallTargetRules.cs`](../../src/Heddle/Language/CallTargetRules.cs), [`BodyModelRules.cs`](../../src/Heddle/Language/BodyModelRules.cs), [`Expressions/EmbeddedCSharpNames.cs`](../../src/Heddle/Language/Expressions/EmbeddedCSharpNames.cs), [`Data/OutputProfileRules.cs`](../../src/Heddle/Data/OutputProfileRules.cs), [`Data/RenderTypeRules.cs`](../../src/Heddle/Data/RenderTypeRules.cs); `HED7022`/`HED7024` claimed; `PrecompiledSchema` bumped 4→5 for the per-carrier `BindDefinition` overload; `[ZeroOutput]` added |
| 2 | [document-shaper](phase-2-document-shaper.md) | 0 (gate); unblocks 1 | One implementation of every byte-affecting document-shaping machine — `WidenToWholeLine` (clamp drift fixed first, shippable alone), the five rebasing passes, the branch-set strip machine, generic `SlicePieces<T>`, and the region-fill matching rule — shared via linked `Language/` sources, proven byte-neutral by the existing golden/differential suites | **implemented (2026-07-25)** — WI1–WI9 landed; clamp drift fixed (generator wrong on all three sub-cases, runtime normative), `DocumentShaper.cs` 398→118 lines, shared core in [`Language/DocumentShaping.cs`](../../src/Heddle/Language/DocumentShaping.cs) + [`Language/RegionFillResolver.cs`](../../src/Heddle/Language/RegionFillResolver.cs); WI9's paired BDN before/after not run (see plan) |
| 3 | [binding-layer](phase-3-binding-layer.md) | 0 (gate); 4 (NumericKind, member-path core, rank core); 5 (schema constants, OQ4 manifest row); 6 (alias-table boundary) | The generator and runtime answer every binding question — extension identity/discovery, function exports, prop layouts, assignability, member paths, model type names — from one shared rule-core each, with the three live binding bugs (false HED7006, nested/generic AQN mismatch, BranchRole mirror) fixed first | **implemented (2026-07-26)** — Tranche A + B landed; both phase-3 quarantined fixtures un-skipped and green; shared cores under [`Language/Binding/`](../../src/Heddle/Language/Binding/) + [`Precompiled/AqnFormatter.cs`](../../src/Heddle/Precompiled/AqnFormatter.cs); `HED7021`/`HED7023` claimed; `PrecompiledSchema` bumped 3→4 for the OQ4 prop-layout row; the runtime's short-name-tie pick fixed in lockstep per Q3.5 (judgement in [breaking-windows](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings)) |
| 4 | [expression-writers](phase-4-expression-writers.md) | 0 (gate); 5 (schema constants, gate for the DynamicMember routing step only); co-ratifies OQ1 with 3 | One set of shared, Roslyn-free rule tables (numeric kinds, operator legality, member visibility, hop form, literal formatting, overload rank) under `src/Heddle/Language/**` so the emitters and `NativeExpressionCompiler` can no longer disagree — `ToString("R")` round-trip and unguarded binary emission fixed first | **implemented (2026-07-25)** — WI1–WI10 landed; both fix-group bugs fixed and the phase-0 overload-tie fixture un-skipped; shared cores under [`Language/Expressions/`](../../src/Heddle/Language/Expressions/) + [`Language/Members/`](../../src/Heddle/Language/Members/); `PrecompiledSchema` bumped 2→3 for `DynamicMember` routing; Q4.2 betterness evaluation filed as a window candidate (analysis only). WI2's interim guard subsumed by WI5 |
| 5 | [pipeline-config](phase-5-pipeline-config.md) | 0 (gate); provider to 1, 3, 4, 6 | One canonical definition each for the pipeline-level generator↔runtime contracts — content hash, key↔path derivation, `.heddle` extension rule, schema/engine versioning, option names/defaults — with the four live bugs (hash-input mismatch, silent key fallback, fabricated engine version, dead item metadata) fixed first | **implemented (2026-07-25)** — WI1–WI10 landed; all four live bugs fixed (phase-0 BOM fixture un-skipped and green); `HED7018`/`HED7019`/`HED7020` claimed; Q2.2 fallback taxonomy spec'd and the generator's blanket catch replaced by a per-template error |
| 6 | [diagnostics-utilities](phase-6-diagnostics-utilities.md) | 0 (gate); 1 (profile parsing), 4 (escape-set boundary), 5 (`TemplateKey.Relativize`) — WI1–WI8 have no dependency | One diagnostic identity across build tier, run tier, and editor — shared diagnostic catalog and projection, one line-index rule, single copies of the small utility tables — opened by an independently shippable fix group (forwarded-warning ID/Fix loss, HED7017 doc gap, line-index `\r` mismatch) | **implemented (2026-07-26, two passes)** — **Pass 1 (WI1–WI8):** shared [`Data/HeddleDiagnosticCatalog.cs`](../../src/Heddle/Data/HeddleDiagnosticCatalog.cs) (82 rows), [`Data/LineIndex.cs`](../../src/Heddle/Data/LineIndex.cs) (`\n`-only rule normative), [`Helpers/CSharpTypeNames.cs`](../../src/Heddle/Helpers/CSharpTypeNames.cs), projection, `SanitizeName` IVT cleanup; WI2 was already closed by phase 5. **Pass 2 (WI9–WI11 + reconciliation):** LSP full options parity with a completeness gate (6 wired, 11 documented exclusions) and the **LSP default profile aligned `Text`→`Html`** (new editor diagnostics, no build or byte change — see the plan's back-compat note); `TemplateOptions.FullPath`/`RenderPath` fixes (both carried live defects); escape fold completed — **one** string-escape, char-escape and lone-surrogate implementation repo-wide; alias↔`NumericKind` boundary gated for the first time. WI5's twin-vocabulary unification + `FaultOrder` were taken by phase 3 |
| 7 | [shared-test-corpus](phase-7-shared-test-corpus.md) | 0 (posture + corpus sweep); 1–6 landed (their byte-neutrality gates are the invariant) | Extend the program's single-artifact principle from production rules to **test inputs** — one shared, intent-declaring template corpus consumed by every tier's tests (source templates *and* goldens), so a shape exists exactly once, both tiers compile the same bytes, and phase 0's D4 residue closes | **implemented (2026-07-26) — stage 0, plus stages 1–5 ruled not to run.** The sharing mechanism, the shared accessor, the intent table and set-equality gating are landed and green; the marker tier gained its member (Q8.43). The inline-string backfill is **declined, not deferred**: stage 1's "18 character-for-character duplicates" are shared *substrings* — corrected in place — and single-sourcing them would have required unifying two projects' fixture models, changing what each suite tests to make two texts identical. **Q8.42** ruled option 2: share what is genuinely shared, leave what is not, and phase 0's D4 residue closes on that ruling rather than on a backfill |
| 8 | [docs-sweep](phase-8-docs-sweep.md) | 0–6 landed + the six audits (their behaviour changes are the input); **Q8.2** gates its stage 4, **phase 7** stage 0 gates its stage 5 | Bring the prose back into agreement with the shipped code — **normative documents that outrank code first** (`native-expressions.md` carries four false claims, not just Q8.7's one), then the behaviour-change prose, with gates (diagnostics across all ID blocks, option names/defaults, public-API mentions, citations, version consistency) so the agreement stops depending on memory | **implemented (2026-07-26)** — stage 0 corrected the four false normative claims in `native-expressions.md` (the anchor rehearsal executed: replacing the `IsReferenceish` guard reddens exactly one test, so the trap was in the prose alone) and the seven imprecise ones; the authority convention is narrowed to mapped-and-conditional (E10) with verification footers as its marker; **four gates landed, every one demonstrated red** — diagnostics (16 of 85 ids were documented nowhere; all 85 now are), options and editor defaults, documented members against the API golden (which found `architecture.md` naming a method that does not exist), and links/citations (which found a moved `GoldenCorpus` and, through it, a `.gitattributes` `-text` pin that had applied to nothing since the move); the CHANGELOG carries the window's two newest breaks; testing-standards gains *Documentation currency* (E11) |

Supplements (linked from their main plans):
[1: test matrix](phase-1-template-emitter-test-matrix.md) ·
[2: extraction map](phase-2-document-shaper-extraction-map.md) ·
[3: ITypeFacts contract](phase-3-binding-layer-typefacts.md),
[3: discovery parity tables](phase-3-binding-layer-discovery-parity.md) ·
[4: rule tables](phase-4-expression-writers-rule-tables.md) ·
[6: diagnostic catalog](phase-6-diagnostics-utilities-catalog.md)

## Ordering rationale

- **Phase 0 is step 0, first priority, and a hard gate (user ruling).** No phase 1–6 fix or
  extraction proceeds until the suite fails loudly on unintended fallback: today the
  majority of tests either bypass the resolver/gauntlet entirely or could be silently served
  by the dynamic tier, which is exactly how the hash-mismatch and AQN-mismatch drifts
  shipped. Phase 0 lands the guardrails (Strict + fallback sentinel, gauntlet-crossing
  corpus sweep, explicit `ExpectDegrade` intent) and hands each later phase its quarantined
  red fixtures as executable acceptance tests. It needs no other phase and touches no engine
  behavior (one `InternalsVisibleTo` line).
- **Every phase opens with an independently shippable fix group.** The research verified
  fifteen live drifts ([07 — recommendations](../research/generator-code-sharing/07-recommendations.md),
  "Confirmed live drift"); each phase's WI1-tier items fix its share of them with no
  cross-phase dependency, so bug-fixing is never blocked on extraction sequencing. If only
  one thing ships after phase 0, it should be the union of the six fix groups — verified by
  un-skipping phase 0's quarantined fixtures.
- **After phase 0, phases 2 and 5 are the natural starters.** Neither depends on any other phase, and both
  are providers: phase 2's `SlicePieces<T>` unblocks phase 1's piece-emission work; phase 5's
  enum/fingerprint `<Compile>` links gate one phase-1 work item and the schema constants gate
  phase 4's routing step. Phase 5's WI3 (the links) is explicitly flagged as extractable
  first if consumers sequence earlier.
- **Phase 4's `NumericKind` table is the single biggest enabler** (Tier-3 item 1 in the
  synthesis): phases 1 and 3 both consume it, and phase 4's own operator-legality and
  overload-rank work items build on it. Within phase 4 it is sequenced first for that reason.
- **Phases 1, 3, 4, 6 can otherwise proceed in parallel** — ownership boundaries below keep
  them from colliding; the few co-owned seams (member-visibility ruling, escape-set fold,
  region-fill resolver halves) are named in both plans with an explicit land-order rule.
- **Tier discipline from the synthesis carries through:** Tier-1 links and Tier-2 direct
  extractions before Tier-3 rule-cores; Tier-4 spec/attribute work rides along with whichever
  phase touches the area.

## Cross-phase notes

- **Artifact ownership (who lands the shared file; everyone else consumes):**
  - Phase 1 — `Data/OutputProfileRules.cs`, `Data/RenderTypeRules.cs`, `Language/ParticipantScan.cs`,
    `Language/SlotRules.cs`, call-target precedence resolver, embedded-C# identifier consts,
    zero-output marker design, body model-typing table, `RegionFillResolver` file + emitter adoption.
  - Phase 2 — `Language/DocumentShaping.cs` (clamp, rebasing passes, branch strip, safe
    `ApplyRemove`), `SlicePieces<T>`, `RegionFillResolver` runtime adoption.
  - Phase 3 — `Precompiled/AqnFormatter.cs`, export/extension discovery rule-cores,
    `PropLayoutCore<TType>` + fault enum, `ITypeFacts`/assignability + conformance corpus,
    `BranchRole` link, generator-side adoption of the member-path core.
  - Phase 4 — `NumericKind` + conversion/promotion tables, `OperatorLexeme`,
    `NativeOperatorRules`, `MemberPathWalk`/`MemberVisibility` core + runtime adoption,
    `MemberHopRule`, `LiteralFormatter` + `CSharpEscape`, overload-rank core,
    `PrecompiledRuntime.DynamicMember`.
  - Phase 5 — `Precompiled/ContentHash.cs`, `TemplateKey` extensions
    (`TryMakeRelative`/`ToPath`/extension consts), `Precompiled/PrecompiledSchema.cs`,
    the enum/fingerprint/capabilities `<Compile>` links, `HeddleBuildOptions`,
    manifest-builder dedupe.
  - Phase 6 — `Data/HeddleDiagnosticCatalog.cs` + projection, `Data/LineIndex.cs`,
    `Helpers/CSharpTypeNames.cs` (display/alias direction), LSP/Tool adoption items,
    `SanitizeName` IVT cleanup.
- **Diagnostic-ID claims must be serialized.** Phases 1, 3, and 5 each provisionally
  reference "the next free generator ID" (HED7018/HED7019) for different diagnostics
  (dangling-fill visibility, ineligible export container, out-of-root key / engine-version
  fallback). Per the
  [claimed-registry rules](../spec/common/cross-cutting-decisions.md), IDs are claimed once,
  at spec-authoring time, in registry order — the numbers in the plans are placeholders, and
  whichever phase reaches spec first claims first. Do not copy the plan literals into code.
  **Allocated so far (2026-07-26),
  centrally, in claim order:** `HED7018` out-of-root template
  key (phase 5 D3), `HED7019` engine-version fallback (phase 5 D6), `HED7020` emitter fault
  (phase 5 D12a), `HED7021` ineligible `[ExportFunctions]` container/method (phase 3, Q3.6),
  `HED7023` ambiguous type name (phase 3, Q3.5). `HED7022` unknown `@profile`
  value (phase 1 D3) and `HED7024` call-site fill of a private region (phase 1 D7 / Q1.3). Phase 1
  used the reserved `HED7022` as instructed; the dangling-fill *visibility* diagnostic it was
  originally reserved for turned out not to be warranted — Q1.3's ruling makes a dangling candidate
  a skip whose already-existing parse error is simply forwarded, so no new ID describes it — and the
  number went to the phase's first genuinely new condition instead.
- **Authority convention (applies everywhere):** the runtime engine's observable behavior is
  normative when aligning drift, except where a plan's D-item records the runtime itself as
  the defect. Which *document* may outrank the implementations, and under what condition, is
  [D10](../spec/common/cross-cutting-decisions.md#d10--documentation-authority-is-mapped-and-it-is-conditional) —
  the mapping plus the rule that a normative home outranks code only for a claim that is **verified or
  gated**. This bullet used to grant `native-expressions.md` unconditional authority over expression
  semantics; that was narrowed after four of its normative claims turned out to be false, each in a
  direction that would have made a reader break working code (E10). Anything that would *widen*
  behavior (member visibility, overload semantics, coercion rail) is a
  [breaking-windows](../spec/common/breaking-windows.md) candidate, not a drift fix.
- **Parity-restoring fixes are argued as not window-gated** in each plan's Back-compat
  section (defect fixes under the parity contract's fix-forward posture), with release-note
  deliverables named. The one sharpened finding worth repeating: the phase-2 clamp fix
  changes *tier selection* only — today's failure mode always throws before divergent bytes
  can render — so no byte change ships from it.
- **Extraction is byte-neutral by acceptance gate.** Every extraction WI carries the same
  done-when: golden corpus, differential suites, and snapshot tests unchanged before/after
  the swap — and, once phase 0 lands, *unchanged under the gauntlet-crossing resolver sweep
  with zero fallback events*. Characterization pins land *before* code moves (phase 2's WI2
  is the model).
- **Quarantine handoff (phase 0 → 1–6).** Phase 0 lands guarded fixtures for the known live
  drifts as explicitly-skipped red tests, each skip naming its owning phase; the owning
  phase's fix-first group un-skips them as its acceptance evidence (initial register:
  BOM/UTF-16 staleness → 5; nested/generic AQN and inherited `[ExtensionName]` → 3;
  non-leftmost `[ScopeChannel]` → 1; overload-tie ambiguity → 4). An unexplained or orphaned
  skip is a review failure.
- **Shared-file placement:** files under `src/Heddle/Language/**` are auto-linked into the
  generator by the existing csproj glob (zero csproj edits); `Data/`, `Precompiled/`, and
  `Helpers/` placements each add one `<Compile Include … Link="Shared\…">` line following the
  D4-step-2 precedent in `Heddle.Generator.csproj`.

## Relationship to research

Each phase resolves one research document and cites its findings by number rather than
restating evidence: [01](../research/generator-code-sharing/01-template-emitter.md),
[02](../research/generator-code-sharing/02-document-shaper.md),
[03](../research/generator-code-sharing/03-binding-layer.md),
[04](../research/generator-code-sharing/04-expression-writers.md),
[05](../research/generator-code-sharing/05-pipeline-config.md),
[06](../research/generator-code-sharing/06-diagnostics-utilities.md);
sequencing and the tier model come from
[07](../research/generator-code-sharing/07-recommendations.md). Where a plan's verification
*corrected* a research claim, the correction is recorded in the plan with evidence (phase 2:
clamp sub-case 3 unreachable today; phase 5: `TemplateOptions.Equals` omits `ExpressionMode`;
phase 6: the id-carrying warnings sit in the compile channel the generator never drains).

## Post-implementation review findings (2026-07-26)

Two independent reviewers — a verifier (completion vs. each plan's acceptance criteria) and an
adversary (defect hunt) — reviewed the landed program. Both mutation-tested pins to confirm they
fire. Findings that survived orchestrator verification, most severe first:

1. **P1 — `PrecompiledExtensionBinding` binary break.** The prop-layout field landed as an *optional
   constructor parameter*, so the 2-arg `.ctor(string, string)` no longer exists in metadata
   (confirmed by reflection over the built assembly). Every manifest emitted by the pre-program
   generator references it. `MinSupportedSchemaVersion = 1` *accepts* those manifests, and the
   fault then lands at `Activator.CreateInstance` inside `RegistrationLock` with no catch — a
   `MissingMethodException` out of `PrecompiledTemplates.Register()` at host startup, neither a
   degrade nor a render. `BindDefinition` solved the same problem correctly with three real
   overloads. **Fix:** add a real 2-arg overload, or raise `MinSupportedSchemaVersion` so the gate
   rejects what would fault; then check in a binary manifest fixture built at an older schema, so
   the support window is *demonstrated* rather than asserted.
   (**closed 2026-07-26** by phase 5's Q8.2 landing; **the number and the reasoning were corrected the same
   day** — see the Q8.2 register entry.) `MinSupportedSchemaVersion` is **3**, the exact boundary of the
   faulting set: verified against the `v2.0.0` tag, the released generator emitted `schemaVersion: 2` and the
   released engine accepted `1–2`, so **schemas 1 and 2 are the only released shapes** and the three
   unreleased increments above 2 collapse into a single schema 3. A 2.0.x-precompiled assembly now degrades
   with one `HED7102` (`SchemaVersionUnsupported`) instead of crashing at startup, and the break ships as
   declared at 2.1 with no shim — a *pending* break, not one that "already shipped in 2.0.0", which is what
   the first landing wrongly claimed. The window is *demonstrated*: `OldSchemaManifestFixture` compiles a
   manifest against a reference facade carrying the pre-break surface under the real assembly's identity,
   with the real `Heddle` excluded from the reference set, so its IL genuinely names the absent
   `.ctor(string, string)`; both released schemas are covered. It is built
   at test time rather than checked in, because a committed `.dll` cannot be re-derived or reviewed and the
   construction *is* the evidence. Note what the superseded pin could not be: `new
   PrecompiledExtensionBinding("a", "b")` binds to the three-parameter constructor against today's assembly,
   so it exercised a new-schema call wearing an old-schema shape — the substitution through which this break
   reached release behind a green suite.
2. ~~**Three "shared" cores have no runtime caller**~~ (**closed 2026-08-01** — all three, and the
   mutation sentence that closed the item was itself wrong.) ~~the numeric widening table (a live
   second copy in `TemplateEmitter`, with *no* test referencing it)~~ (**closed 2026-07-26** by the
   phase-4 audit — `TemplateEmitter.IsImplicitNumericWidening` now delegates to
   `NumericTable.IsImplicit` through the Roslyn facts adapter, with the deleted body kept as an
   exhaustive `SpecialType`-squared characterization pin in `GeneratorNumericTableAdoptionTests`; the
   plan's false "the lockstep test covers both copies" parenthetical is corrected in place).
   ~~`TypeSpelling` (a re-implementation; the original survives in `ReflectionHelper`)~~ — the
   original does not survive: `ReflectionHelper.ResolveType` (`ReflectionHelper.cs:537`) *is* a call
   to `TypeSpelling.TryResolve`, and the surrounding message mapping reads the shared
   `TypeSpellingFault`; the only other caller is the generator's. ~~`ExtensionRegistrationRules`
   (runtime keeps its own copy in `TemplateFactory.AddExtensions`)~~ — there is no second copy:
   `TemplateFactory.cs:131` calls `Resolve` and `:250` calls `OrderingKey`. **The mutation claim was
   measured and is false in both directions:** dropping the more-derived arm of `Resolve` reddens
   three runtime tests, three generator tests and eight integration rows; inverting `OrderingKey`
   reddens one runtime test and zero generator ones — the opposite of what this line said.
3. ~~**`[ExportExtensions]` is unmodelled by the generator.** The runtime only scans assemblies
   carrying the attribute; the generator scans all referenced assemblies, so it precompiles
   extensions the runtime never registers → permanent silent per-request fallback.~~
   (**closed 2026-07-26**, in two parts.) The generator half was already wrong when this was written:
   Q8.4 aligned the generator to the runtime's rule, so `ExtensionBinder` reads
   `ExportExtensionsAttribute` and registers only what an `[assembly: ExportExtensions(...)]`
   occurrence names — pinned by `ExportExtensionsScopeTests`. What remained was the *set* the two
   tiers scan, and Q8.37 changed the runtime's: it is now the assemblies a host **registers**, not
   the assemblies that happen to be loaded. The residue is therefore **irreducible and deliberate**,
   not drift: the generator binds what the compilation references, because that is the only fact a
   build has, and the host's registration set is a run-time fact the build must not bake in
   (aligning the generator to a *host-chosen set* is exactly what
   [D11](../spec/common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded)
   forbids). The two are reconciled per request by gauntlet step 2, which resolves each bound name
   against the live registry and reports `ExtensionBindingMismatch` with
   `live=<unresolved>` when the owning assembly was never registered. So the outcome is a
   *reported* per-request fallback, not a silent one — and making that class of mismatch refuse to
   degrade under the default policy is already a [next-window candidate](../spec/common/breaking-windows.md#next-window-candidate-register).
4. ~~**`StripGlobal`'s AQN path hard-codes `assembly: "Heddle"`**, so a user-defined nested or
   out-of-engine branch-role extension emits `Ns.Outer.Inner, Heddle` where the gauntlet computes
   `Ns.Outer+Inner, <realAsm>` — drift #6's shape surviving on a path no fixture exercises.~~
   (**closed 2026-07-26** by the phase-1 audit.) `StripGlobal` is **deleted**: the branch arm's manifest
   row now takes `ExtensionBinder.Info.BareTypeName` (metadata `+` for a nested type) and
   `Info.AssemblyName`, the same values every other binding row already used, and
   `AllocateBodyExtension` takes the assembly instead of letting it default to the literal. Byte-neutral
   — the four engine branch-role extensions are top-level, so both spellings agree — which is also why
   the defect itself is **not reachable by a test**: the arm gates on `IsEngineAssembly`, so a nested or
   out-of-engine role extension cannot enter it, and the generic custom path it falls through to was
   already correct. The reintroduction guard is therefore a source-shape pin
   (`EmitterSharedRuleAdoptionTests.ManifestTypeNamesComeFromTheBinderNotFromStringSurgery`), stated as
   such rather than dressed up as behavioural coverage.
5. **Drift #9 (`ToString("R")`) has never been exercised where it manifests.** `"R"` *is*
   shortest-round-trippable on .NET Core, so the 60 000-value round-trip suite passes identically
   under the bug; reverting the fix reddens exactly one literal-string assertion. The only leg that
   would catch it is `net48`, which is `Condition="'$(OS)' == 'Windows_NT'"` and has never run here.
   **Confirmed by mutation and mitigated as far as this box allows (2026-07-26, phase-4 audit):** the
   revert reddened exactly one test as predicted, and all four round-trip legs passed under the bug.
   The guard is now a *format-identity* pin (the formatter's text must equal G17/G9 text over the
   corners and the randomized value space, with a meta-assertion that `"R"` and G17/G9 really do differ
   for a large share of the sample), so the same revert now reddens 23 cases across 5 methods on every
   CoreCLR leg. **Still unverifiable here:** the defect itself, which only manifests under a .NET
   Framework host — that check is `net48`-only and is being run separately on Windows.
6. **The suite is not fully green: `dotnet test` exits 1.** The `net6.0` leg aborts (SDK absent on
   this box) with `MSB4181`. The reported 4808/0/0 is the sum of the legs that *ran*; `net48` and
   `net6.0` are unverified.
7. **Coverage narrower than claimed** — phase 0's resolver byte-parity covers 9 templates, not the
   corpus (the rest are resolve-only), and D4's rationale that "the corpus is the union of the
   feature templates" is false: feature suites use inline strings, so ~130 feature tests never
   cross the gauntlet. The `>= 25` corpus floor sits against an actual 40, letting 15 vanish
   silently. Five corpus tests carry `if (dir == null) return;` — a silent no-op if layout changes.
   **Owned by [phase 7](phase-7-shared-test-corpus.md)**: the floor, the traversal and the five
   `dir == null` no-ops **are fixed** (stage 0, 2026-07-26), and resolver byte-parity rose from 9/10
   templates to **32**. The inline-string residue is **closed by ruling** (**Q8.42**, 2026-07-26)
   rather than by migration: a template belongs in the shared corpus because both tiers must compile
   the same bytes, and a fixture whose tiers are fed different inputs and asserted against different
   outputs is not serving that requirement. So the ~130 figure is retired as a debt measure — what
   replaces it is a per-fixture judgement, recorded in the intent table for everything that is
   shared. The claim corrected alongside it: the "18 character-for-character duplicates" were shared
   *substrings*, none of them a whole template on either side.
8. ~~**Phase 4's reshaped overload-tie fixture pins a policy violation.**~~ (**closed 2026-07-26** by
   the Q8.1 ruling — `HED7025`. `BindOutcome` is propagated out of both binders as a `BindRefusal`, and
   an `Ambiguous`/`None` front over arguments the estimator typed is now a build **error**; the fixture
   was reshaped a second time and pins the build error together with the runtime `HED1013`, so tier
   agreement is still pinned but on the corrected verdict. The side condition — report only when no
   argument estimate is `Unknown` — is an early return before the ranker runs, pinned by three cases
   and mutation-verified. No rendered byte moves; the refusal is unchanged and the silence is what
   ended. Window disposition: defect repair, not window-gated, argued in `breaking-windows.md`.
   Residue: Q8.18/Q8.19.) It asserted *no build
   diagnostic* is emitted, so an ambiguous overload call yields a green build and a hard `HED1013`
   at first render — contrary to both the match principle and the fallback-legitimacy ruling.
   **Confirmed by the phase-4 audit (2026-07-26); behaviour deliberately unchanged, fix queued.** The
   generator has *already computed* the illegality (`BindOutcome.Ambiguous`) and then reports nothing,
   which is the shape `HED7021` was created to fix on the phase-3 side; and `precompilation.md` puts
   the closest analogue (`UnsupportedFunction`) on the *legitimate* side of the fallback line only
   because the build "refused **on purpose** … and warned `HED7014`" — the refusal is legitimate, the
   silence is not. Fix: propagate `BindOutcome` out of the two binders instead of collapsing every
   refusal to `null`, and report a `HED70xx` **error** when the outcome is `Ambiguous`/`None` *and* no
   argument estimate is `Unknown` (that side condition is what keeps it a proof about the runtime
   rather than a guess). Not done in the audit because a new ID is a cross-cutting-decisions registry
   change, not an audit edit. Reasoning in full in
   [phase 4 — the overload-tie fixture and the match principle](phase-4-expression-writers.md#phase-4-audit-2026-07-26).
9. **Two further undelivered items** beyond the ones named below: ~~phase 6's D12.5 projection-
   equivalence corpus~~ (**closed 2026-07-26** — `DiagnosticCorpusVectors` is now one shared table
   asserted by all three hosts, so the run tier, build tier and editor are compared to each other
   rather than to three sets of hand-written expectations) and ~~phase 2's WI5 allocation
   benchmark (its done-when said "proven, not argued")~~ (**recorded as undelivered 2026-07-26** —
   the clause is now marked not-delivered in the plan, with the argument that stands in its place
   labelled as an argument: the render path allocates identically *by construction* because
   `SlicePieces` changed how `GetDocumentPieces` walks and not what it returns, while the compile
   path did gain one closure and two capturing delegates per body, which the WI9 note had glossed
   as zero. Not measured: a credible paired run means holding a two-file revert of landed code
   across a multi-minute BenchmarkDotNet run, and this box's paired timings are untrustworthy).
   Also: **two** blanket `catch (Exception)`
   sites survive in the generator (`HeddleTemplateGenerator.cs:404` and `:511` — the phase-5 one now
   *reports* rather than degrades, so intent holds, but the "no blanket catch" claim is literally
   false); the catalog is **91** rows, not 80 — it was 82 when this was written and has grown with
   every id claimed since (re-count with `grep -c 'Add(HeddleDiagnosticIds\.'`, which is the shape
   `DiagnosticIdTests` gates).

10. **Phase 1's coverage was thinner than its claims (found by the phase-1 audit, 2026-07-26; all
   fixed except where noted).** `BodyModelRules` was enforced only by a `Debug.Assert`, so in Release
   the table constrained nothing, and its build-tier acceptance test asserted the table against its own
   `InlineData`; the two "the generator-side copy is deleted" pins pinned method *names*, so a
   duplicate under any other name passed; `ParticipantScanLockstepTests` was six hand-picked rows, not
   the whole-corpus sweep its matrix promised; the reshaped F11 fixture's "reachable neighbour" clause
   used two *leftmost* participants — a shape the old buggy probe already handled — and a third clause
   duplicated a lockstep test verbatim; WI7's actual fix (extension beats a same-named exported
   function at build time) had no test at all; and the `BindDefinition` overload's "existing overloads
   unchanged" criterion was asserted only at the signature level. The test matrix's six "corpus
   guardrail entries" had **zero** fixture files and named a fixture set that does not exist — the
   matrix is corrected rather than backfilled, and eleven further rows that named never-created files
   are corrected too. Remaining open: the value-path coercion rail has no byte-level fixture on either
   tier (shape and render-path only). Full detail in
   [phase 1 — post-implementation audit](phase-1-template-emitter.md#post-implementation-audit-2026-07-26).

**Process note, recorded as a lesson:** the program was implemented entirely in the working tree
with nothing committed. A reviewer subagent reverting a mutation with `git checkout` therefore
destroyed implementer work (phase 3's prop-layout fingerprint check), which had to be reconstructed
from its red test. Future runs of this shape should commit each phase before review, and reviewers
that mutate should work on a clone.

### Finding 10 — the item metadata never worked from a real project (found 2026-07-26, fixed)

The sharpest finding of the post-implementation work, because it invalidates a *delivered*
acceptance claim rather than an unverified one.

`Heddle.Generator.targets` restated each `HeddleTemplate` metadatum as
`<Key>%(HeddleTemplate.Key)</Key>` inside an `Include="@(HeddleTemplate)"` transform. The transform
already copies every metadatum, and **outside a target a cross-item `%()` reference evaluates to the
empty string** — so each element *overwrote* the copied value with `""`. `Key` was inert.
`Precompile="false"` was inert. `Name` was inert.

So **phase 5's Q5.1 deliverable — "wire `Precompile` properly" — did not work end to end**, and its
acceptance tests could not have shown it: every suite injects `build_metadata.*` directly into the
analyzer-config provider and **no test crosses the targets file at all**. The phase reported the
work as landed in good faith; the gap was in what the tests could see, which is the same failure
mode the six phase audits found repeatedly, here reaching a shipped MSBuild surface.

It also reframes Q8.12. The record said `Name` was dead code and removing it was harmless — true as
far as it went, but the reason all three were dead was the targets file, not the metadata's
existence. Restoring `Name` (the user's ruling) is what surfaced it.

Fixed by deleting the restatements. Gated two ways, because one would not have caught it:
structurally by `PipelineContractTests.EveryDeclaredItemMetadataIsReadByTheGeneratorAndNotNulledByTheTargets`
(set equality *plus* a refusal of any future restatement), and behaviourally by the sample gallery,
which is the only place a real MSBuild evaluation runs.

The behavioural gate was **rebuilt once**, and how it broke is instructive. Its first form leaned on
`Name` renaming the generated entry class, which `Program.cs` then called by name — so a metadatum that
stopped flowing failed the sample's compile. Q8.25 then ruled `Name` **additive**, so it stopped moving
the class name and that gate silently became a no-op: the sample would have kept building with the
metadata inert again. It is now an import-only partial (`templates/_banner.heddle`, `Precompile="false"`)
carrying `Name="Banner"` and imported as `@<<{{Banner}}` — a metadatum that stops flowing now fails the
build with `HED7011`. **A behavioural gate is coupled to the semantics it gates**; when the semantics are
re-derived, the gate has to be re-derived with them, or the gate quietly stops gating.

**Standing lesson:** a build-surface contract verified only through injected analyzer-config values
is unverified. Where a claim depends on MSBuild evaluation, something must actually evaluate MSBuild.

## Closed program-level gap — the compile-channel drain

Recorded unscheduled 2026-07-25 because no phase owned it; **closed 2026-08-01**.

Q6.1's ruling states the principle *"if diagnostics can surface early, they must — on both
tiers."* Phase 6's D5/WI6 delivered the **structural** half: one shared
[`HeddleDiagnosticProjection`](../../src/Heddle/Language/HeddleDiagnosticProjection.cs) that
drains parse *and* compile channels. D5 was explicit that this only pays out *"once the generator
runs compile-channel stages"*, and no phase 0–6 scheduled that, so the generator stayed on the
parse-channel overload — and the two channels are separate lists. `ParseContext.Warnings` still
has exactly one producer (the id-less SLL-fallback warning at `DocumentParser.cs:147`);
everything else went to `CompileContext.CompileWarnings`, which the generator never read. That is
why eleven id-carrying warnings reached no build-time diagnostic, and it was a scope gap in the
program rather than a defect in phase 6.

**All eleven now surface at build time** — `HED1016`, `HED2002`, `HED2003`, `HED2004`, `HED3001`,
`HED3002`, `HED3004`, `HED3005`, `HED4002`, `HED4005`, `HED5011` — under the front end's own ids,
so a report is the same identity, sentence and fix on both tiers. The generator still does not run
`HeddleCompiler`; what changed is that the conditions it already decides now say so:

- The shaping-time three are raised by the **shared** machine the generator was already driving.
  [`BranchSetLint`](../../src/Heddle/Language/BranchSetLint.cs) is the observer
  `DocumentShaping.StripBranchSets` always accepted and the generator always passed `null` for
  (`HED3001`/`HED3002`/`HED3004`/`HED3005`), and
  [`OutputLints`](../../src/Heddle/Language/OutputLints.cs) holds the two document scans that read
  nothing but text and parse spans (`HED4005`/`HED2004`). Both were lifted out of `HeddleCompiler`,
  which now drives them instead of owning them.
- The remaining five are decided per tier — the run tier from instantiated extensions and reflected
  types, the build tier from parse data and Roslyn symbols — but their text is written once, in
  [`CompileWarningFactory`](../../src/Heddle/Language/CompileWarningFactory.cs). Mutating a sentence
  there reddens the *runtime's* own test while the cross-tier parity test stays green, which is the
  demonstration that there is one source and not two.

**What a build still cannot decide**, and why — three of the eleven are narrower on the build tier
than at run time, and the corpus names the residue per fixture rather than leaving it to be
inferred:

- `HED1016` sees the default function table and each assembly's `[ExportFunctions]`, which is all a
  build has. A registry the host fills at run time is invisible to it, so the build tier can only
  ever say *less* here, never more — the same bound `HED7014` already lives under.
- `HED5011` asks the shared member walk, so it answers for the symbols the compilation can see; a
  model type the generator cannot resolve degrades before the question is reached.
- `HED2003` reads `[EncodeOutput]` off the bound extension type. A producer the binder cannot
  resolve is a template that degrades anyway.

Nothing here widens a warning the engine does not raise: `NothingIsForwardedThatTheRunTierWouldNotRaise`
gates that direction, and `AForwardedEntrySaysWhatTheRunTierSays` compares both tiers' live output
for the same bytes rather than restating either. The early-surfacing principle is met for all
eleven; `HED3003`, the orphan-terminal **error** on the same event stream, is deliberately still
run-tier-only (see the note below).

**Not taken, deliberately — and it is a live defect, not a tidy exclusion.** The branch-set observer
also carries `HED3003`: an orphan `@else` is a compile *error*, and measuring it showed the engine
refusing the template (`CompileResult.Success == false`) while the generator emits an entry class,
writes the manifest row and reports nothing — so the precompiled tier renders bytes the dynamic tier
will not compile. Draining that arm was declined here because turning it on makes a consumer build
fail on the strength of the generator's own classification, which is a different class of risk from
the eleven warnings and a different blast radius. It is checked in as a skipped red test —
`CompileChannelDrainTests.ATemplateTheEngineRefusesIsNotSilentlyPrecompiled`, rehearsed red — and
either half of a fix satisfies it: surface the refusal, or leave the template on the dynamic tier.
The shared observer already takes an optional error sink; the generator passes `null`.

## Unverified platform surface

See [unverified-platform-surface.md](unverified-platform-surface.md). Every verification claim
in this program was measured on Linux with .NET 8 and .NET 10 only. That page records what was
therefore *not* measured — .NET Framework's `netstandard2.0` behaviour, Windows, culture, and a
declared `net6.0` test container that fails to start while the run still exits 0 — so the gap is
legible later rather than inferred from a green build.

## Open questions

See [open-questions.md](open-questions.md) for the Q&A register. **All 21 questions are
resolved (user, 2026-07-25) and folded into the phases — the plans are at DoR.** Two rulings
carry program-wide principles restated in the register and inherited by every phase: the
**match principle** (the runtime dynamic engine is the primary source of truth; the generator
matches its validation rules, errors, and throws as if it were part of the dynamic engine —
warning-channel differences may legitimately exist, errors always match) and the
**fallback-legitimacy principle** (catch-and-degrade only for a researched set of genuinely
fallback-worthy conditions such as staleness/change tracking; everything else surfaces as an
error). Notable scope changes from the rulings: phase 5 gains the fallback-taxonomy
research + narrowed catch (from Q2.2) and the all-resolver-arms registry consultation
(Q5.2); phase 3's ineligible-container diagnostic is an error, not a warning (Q3.6), and
type-name resolution must match the runtime exactly rather than degrade (Q3.5); phase 4
gains the C#-betterness-in-runtime evaluation task (Q4.2); phase 6's LSP work expands to
full options parity (Q6.2).
