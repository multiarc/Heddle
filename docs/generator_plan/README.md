# Generator ↔ engine code-sharing resolution — plan

> A seven-phase program (0–6) that resolves the duplication documented in
> [docs/research/generator-code-sharing/](../research/generator-code-sharing/00-overview.md):
> phase 0 first makes the test suite fail loudly on any unintended precompiled→dynamic
> fallback (the program's step 0 and hard gate); then every rule the source generator and
> the runtime engine currently maintain as two hand-kept copies moves into one shared
> artifact (linked source, rule table, or spec-pinned contract), and the fifteen verified
> live drifts — several of them user-visible bugs — are fixed first, as independently
> shippable groups inside each phase, each verified under the phase-0 guardrails.

All phases: **status proposed — not started.** Phase numbering follows the research-document
numbering (area 0N → phase N), not execution order; execution order is governed by phase 0
(the program's step 0 and gate), the fix-first groups, and the artifact-ownership
dependencies below.

## Phases

| # | Phase | Depends on | Goal (one line) | Status |
|---|---|---|---|---|
| 0 | [test-fallback-guardrails](phase-0-test-fallback-guardrails.md) | — (**gates 1–6**) | Make the test suite structurally unable to pass through an unintended fallback — resolver/gauntlet-crossing renders of real generator output with fallback-as-failure (Strict + sentinel), corpus-wide; fallback exercised only by tests that declare it; known live drifts handed to phases 1–6 as quarantined red fixtures | proposed |
| 1 | [template-emitter](phase-1-template-emitter.md) | 0 (gate); 5 (enum links, hard gate for one WI); consumes 2, 3, 4 | Resolve the emitter↔runtime drift — three live bugs fixed first (needsLocals scan + per-carrier flags, unknown-`@profile` silent acceptance, `DefaultConvertible` gaps), then nine linked shared sources and the representation-bound rules pinned as data tables plus differential tests | proposed |
| 2 | [document-shaper](phase-2-document-shaper.md) | 0 (gate); unblocks 1 | One implementation of every byte-affecting document-shaping machine — `WidenToWholeLine` (clamp drift fixed first, shippable alone), the five rebasing passes, the branch-set strip machine, generic `SlicePieces<T>`, and the region-fill matching rule — shared via linked `Language/` sources, proven byte-neutral by the existing golden/differential suites | proposed |
| 3 | [binding-layer](phase-3-binding-layer.md) | 0 (gate); 4 (PrimitiveKind, member-path core, rank core); 5 (conditional, OQ4 manifest row); 6 (alias-table boundary) | The generator and runtime answer every binding question — extension identity/discovery, function exports, prop layouts, assignability, member paths, model type names — from one shared rule-core each, with the three live binding bugs (false HED7006, nested/generic AQN mismatch, BranchRole mirror) fixed first | proposed |
| 4 | [expression-writers](phase-4-expression-writers.md) | 0 (gate); 5 (schema constants, gate for the DynamicMember routing step only); co-ratifies OQ1 with 3 | One set of shared, Roslyn-free rule tables (numeric kinds, operator legality, member visibility, hop form, literal formatting, overload rank) under `src/Heddle/Language/**` so the emitters and `NativeExpressionCompiler` can no longer disagree — `ToString("R")` round-trip and unguarded binary emission fixed first | proposed |
| 5 | [pipeline-config](phase-5-pipeline-config.md) | 0 (gate); provider to 1, 3, 4, 6 | One canonical definition each for the pipeline-level generator↔runtime contracts — content hash, key↔path derivation, `.heddle` extension rule, schema/engine versioning, option names/defaults — with the four live bugs (hash-input mismatch, silent key fallback, fabricated engine version, dead item metadata) fixed first | proposed |
| 6 | [diagnostics-utilities](phase-6-diagnostics-utilities.md) | 0 (gate); 1 (profile parsing), 4 (escape-set boundary), 5 (`TemplateKey.Relativize`) — WI1–WI8 have no dependency | One diagnostic identity across build tier, run tier, and editor — shared diagnostic catalog and projection, one line-index rule, single copies of the small utility tables — opened by an independently shippable fix group (forwarded-warning ID/Fix loss, HED7017 doc gap, line-index `\r` mismatch) | proposed |

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
- **Authority convention (applies everywhere):** the runtime engine's observable behavior is
  normative when aligning drift, except where a plan's D-item records the runtime itself as
  the defect; expression semantics defer first to
  [docs/native-expressions.md](../native-expressions.md). Anything that would *widen*
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

## Open questions

See [open-questions.md](open-questions.md) for the consolidated Q&A register — 21 questions
across the seven phases (20 distinct: Q3.1/Q4.1 are one joint ruling), each carried with the
owning plan's recommended answer as a provisional default. **None are resolved; the plans
are not at DoR until the register is ruled on** (fix-first groups are unaffected unless a
plan notes otherwise). The highest-leverage rulings are Q3.1/Q4.1 (member-visibility policy
— jointly ratified by phases 3 and 4 before either adopts the shared core) and Q5.1
(`Precompile` item metadata — wire or remove).
