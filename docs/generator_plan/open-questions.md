# Generator code-sharing plan — open-questions register

The consolidated Q&A register for the seven phases. Numbering is `Q<phase>.<n>`, matching each
phase plan's own Open-questions section.

**Pre-authoring questions (Q0.1–Q6.3): all resolved (user, 2026-07-25) and folded into the phases.**
**Post-implementation questions (Q7.1–Q8.35):** opened after the phases landed, by the two
post-implementation reviews, the six phase audits, the phase-8 docs sweep authored from
the Q8.7 ruling, and the landings themselves — see
[the section below](#post-implementation-questions-opened-2026-07-26). **Q7.4 and Q8.1–Q8.5 are
ruled (user, 2026-07-26); the remainder stand at their stated defaults**, which are the operative
decision until revisited.

**Q8.32–Q8.35 are ruled (user, 2026-07-26)**
([section](#opened-by-the-q828q831-landing-2026-07-26--awaiting-rulings)). **Q8.33 landed 2026-07-26**
(the fallback event's two carriers, a declared 2.1 break). **Q8.32 landed in part** — sub-question (b),
the silent registration drop, is closed; sub-question (a) was rejected by the ruling; the late-binding
third stage the ruling reframed it around is still conditional on an unmade feasibility assessment.
**Ruled but unimplemented: Q8.13, Q8.14, Q8.17, Q8.19, Q8.35** — Q8.19's emitter-walk cost is the other
outstanding feasibility assessment. Q8.34 requires no behavioural change and closes with one
verification and a phase-8 item.

Each resolved entry below records the question, the ruling, and the folding target. Two rulings
carry a program-wide principle referenced by several phases:

- **The match principle (Q1.3, generalized by Q2.1/Q3.5/Q3.6):** the runtime dynamic engine is
  the primary source of truth; the generator must match its validation rules, errors, and
  throws — behaving as if it were part of the dynamic engine. The generator cannot always
  surface the same *warnings* through the same channel; such differences may legitimately
  exist, but the program strives for matching, and *errors* always match.
- **The fallback-legitimacy principle (Q2.2):** catch-and-degrade is legitimate only for a
  small, researched set of conditions (stale cached data, genuine change-tracking logic);
  everything else is an error that must surface — thrown or reported, never silently degraded.

## Phase 0 — test-fallback-guardrails

- **Q0.1 — Feature suites: wholesale resolver-path conversion, or corpus sweep as posture
  carrier?** **Ruling: recommendation applied** — the corpus sweep is the permanent posture
  carrier; feature suites keep direct-invoke isolation and contribute templates to the corpus.
  *Folded into:* [phase 0 D4](phase-0-test-fallback-guardrails.md) (unchanged).

## Phase 1 — template-emitter

- **Q1.1 — `maxRecursionCount` in the precompiled options fingerprint?** **Ruling:
  recommendation applied** — decided once in the precompilation spec; default posture records
  the divergence as intentional (D23) unless that spec adds the field at the next schema bump.
  *Folded into:* phase 1 Open questions (closure note); the precompilation spec owns the call.
  **Implemented (2026-07-26):** phase 1 changed nothing here, as planned — the build-baked
  `maxRecursionCount` and the fingerprint's silence on it are both unchanged.
- **Q1.2 — Scheduling of the planned non-string coercion-rail change.** **Ruling:** the
  generated equivalent must match the dynamic engine — when the runtime rail changes, the
  emitted `Execute` shape changes in the same landing (joint-land rule), and any present
  mismatch is fixed now. The breaking-window candidacy of the rail change itself stands, but
  generator and runtime move through the window together. *Folded into:* phase 1 WI12 and
  Back-compat.
- **Q1.3 — Runtime diagnostic on dangling region-fill candidates?** **Ruling (the match
  principle):** the generator matches the runtime's validation rules and mechanics — dangling
  candidates are skipped as the runtime skips them (no hard refusal), and cases the runtime
  raises as errors (HED5019 retract) surface as matching build-time errors. Warning-channel
  differences may exist where the build tier has no equivalent; strive for matching.
  *Folded into:* phase 1's region-fill work items (emitter adoption of `RegionFillResolver`
  aligns verdict handling to runtime semantics). **Implemented (2026-07-26):** the emitter reacts
  per verdict as the runtime does; a dangling candidate's parse-emitted error is now *forwarded at
  build* (it used to be filtered out of the build channel entirely), and a private-region fill
  raises `HED7024`, the `HED5019` twin. The reserved `HED7022` was **not** needed for
  dangling-fill visibility — the ruling turns it into a skip whose existing error simply surfaces —
  and was used for the unknown-`@profile` error instead.
- **Q1.4 — Tighten the participant scan's over-provision on shadowed names?** **Ruling:
  recommendation applied** — keep the safe over-provision; revisit on the named trigger.
  *Folded into:* phase 1 (unchanged). **Implemented (2026-07-26):** the over-provision is kept and
  documented in `Language/ParticipantScan.cs`, with the shadowed-name branch asserted explicitly by
  `ParticipantScanLockstepTests.AShadowedParticipantNameOverProvisionsAndThatIsTheRuling` — the
  named trigger now has a test to go red.

## Phase 2 — document-shaper

- **Q2.1 — Empty-default-chain asymmetry: which side is intended?** **Ruling:** the runtime
  dynamic engine is the primary source of truth; the generator matches logically wherever
  applicable — `DocumentShaper` stops skipping empty default chains and models the runtime's
  zero-length element. Bytes are unaffected; the characterization pin now asserts the
  *matched* behavior. *Folded into:* phase 2's shaping work items (disposition changed from
  "leave + document" to "align to runtime").
- **Q2.2 — The generator's blanket `catch (Exception)` degrade.** **Ruling (the
  fallback-legitimacy principle):** stop swallowing. Replace the blanket catch with a
  specific, researched catch set covering only conditions that genuinely warrant fallback
  (stale cached data, genuine change-tracking logic); every other exception is a defect that
  must throw and pass through so it surfaces. This requires a careful path-by-path research
  task — generation-time degrade paths *and* the runtime gauntlet's
  `PrecompiledFallbackReason` classes — producing an explicit legitimate-fallback vs
  must-surface taxonomy. *Folded into:* phase 5 (owns the catch site in
  `HeddleTemplateGenerator.cs` and the gauntlet-policy taxonomy, as a new D-item + WI), with
  phases 1/2 supplying the intentional-refusal taxonomy and phase 3 coordinating binding
  mismatch classes; phase 2's original deferral note is superseded.

## Phase 3 — binding-layer

- **Q3.1 — Member-visibility policy** *(joint with Q4.1)*. **Ruling: follow runtime** — the
  runtime's narrower sandbox is normative; the generator tightens. *Folded into:* phases 3
  and 4 as planned (recommendation confirmed).
- **Q3.2 — `[ExportFunctions]` precedence.** **Ruling: follow runtime** — merge semantics.
  *Folded into:* phase 3 (recommendation confirmed).
- **Q3.3 — `[ExtensionReplace]` support.** **Ruling: adopt the runtime approach** — the
  generator binds the extension exactly as the runtime's replacement precedence resolves it;
  there is no reason a precompiled template cannot honor a runtime interface replacement.
  *Folded into:* phase 3 (recommendation confirmed, stated as full-precedence adoption).
- **Q3.4 — Prop-layout manifest row + gauntlet check.** **Ruling: recommendation applied** —
  additive schema row, coordinated with phase 5. *Folded into:* phase 3 / phase 5 (unchanged).
- **Q3.5 — Model type-name resolution: ambiguity and implicit namespaces.** **Ruling:** the
  runtime must be matched **exactly** — the generator reproduces the runtime's resolution
  semantics and outcomes (including surfacing the runtime's ambiguity error as a matching
  build-time error), not merely degrading. If the runtime's logic itself is found defective,
  both sides are fixed — and then both must match. *Folded into:* phase 3 F8 work items
  (replaces the degrade-only posture; shared spelling parser supplies generics/arrays/tuples).
- **Q3.6 — Ineligible export container: silent skip vs diagnostic.** **Ruling (the match
  principle):** the generator follows the runtime's validation rules and errors exactly — the
  runtime throws, so the generator raises a matching **error** (not the previously
  recommended warning) at build time. *Folded into:* phase 3 (diagnostic severity upgraded;
  ID claimed from the registry at spec time).

## Phase 4 — expression-writers

- **Q4.1 — Member-visibility policy** *(joint with Q3.1)*. **Ruling: use runtime behavior.**
  *Folded into:* phase 4 (recommendation confirmed).
- **Q4.2 — Overload-selection semantics.** **Ruling:** keep the runtime behavior and match it
  in the generator now (degrade-on-ambiguity + cast-pinned emission). Additionally, the user
  asks whether adopting the C# native betterness schema (plus extra validations for Heddle's
  documented limitations) in the **runtime**, with the generator then matching by
  construction, makes sense — this is recorded as a follow-up evaluation task: assess
  feasibility and behavioral delta of C#-betterness-in-runtime; if adopted, it is a
  breaking-window candidate landed jointly on both tiers. *Folded into:* phase 4 (near-term
  posture unchanged; new evaluation WI + next-window candidate entry).
- **Q4.3 — Dynamic-binder context.** **Ruling: reproduce runtime behavior** —
  `PrecompiledRuntime.DynamicMember` reproduces the runtime's Heddle-context binding.
  *Folded into:* phase 4 (recommendation confirmed).

## Phase 5 — pipeline-config

- **Q5.1 — `Precompile`/`Name` item metadata.** **Ruling: wire precompilation properly** —
  `Precompile` is implemented (per-item opt-out that keeps the template in the import map);
  `Name` removed per the recommendation. *Folded into:* phase 5 (WI promoted from
  conditional to committed).
- **Q5.2 — `View`/`PartialView`/`Master` resolver arms and the precompiled registry.**
  **Ruling: yes — precompiled templates are consulted everywhere**; expected to be a small
  fix. *Folded into:* phase 5 as a new committed WI (registry consultation wired into all
  resolver arms, following the `None`-arm precedent; the search-order/key-mapping detail is
  specified there rather than deferred).

## Phase 6 — diagnostics-utilities

- **Q6.1 — Forwarded-warning real-ID change.** **Ruling:** if diagnostics can surface early,
  they must — on both tiers. Ship as a fix (real IDs + Fix forwarded at build time), with the
  early-surfacing principle recorded for runtime and generator alike. *Folded into:* phase 6
  (recommendation confirmed and broadened into a stated principle).
- **Q6.2 — LSP default output profile.** **Ruling:** beyond aligning the default — the LSP
  follows the same configuration surface the runtime permits and **wires all options** (full
  parity with the runtime option set and defaults, via the shared names/defaults table).
  *Folded into:* phase 6 (WorkspaceConfig work item expanded from default-alignment to full
  options parity).
- **Q6.3 — Diagnostic-catalog `MessageFormat` end-state.** **Ruling: recommendation
  applied** — consumed rows only; revisit on the named triggers. *Folded into:* phase 6
  (unchanged).

---

# Post-implementation questions (opened 2026-07-26)

Everything above was resolved before the phases were authored. The questions below were opened
*after* the seven phases landed, by the two post-implementation reviews and the six phase audits.
They are recorded here because this file — not a phase plan — is the register: the Q7.* entries in
particular were written into
[phase-7-shared-test-corpus.md](phase-7-shared-test-corpus.md) and initially missed this file,
which is the bookkeeping failure this section exists to correct.

**Ruled (user, 2026-07-26): Q7.4, Q8.1, Q8.2, Q8.3, Q8.4, Q8.5.** The rest stand at their stated
defaults, which are treated as the operative decision until revisited. Each entry records the
question, the ruling or default, and where it is folded.

## Phase 7 — shared test corpus

- **Q7.1 — Do `src/Heddle.LanguageServices.Tests/Corpus`' three `.heddle` templates join the shared
  corpus?** They serve editor-tier completion/hover/diagnostics, for which the "renders correctly"
  axis does not exist; joining would need a fourth `Tier` value (`EditorOnly`).
  **Ruling (user, 2026-07-26): keep them separate.** *Folded into:* phase 7 (no `EditorOnly` tier;
  the intent table's three-value `Tier` axis stands). Revisit only if the editor tier ever needs a
  shape the corpus already has.
- **Q7.2 — Do the benchmark and sample corpora converge, and should `TestCorpus.props` serve
  `Heddle.Performance` regardless?** `src/Heddle.Performance/TestTemplates` (9),
  `benchmarks/dotnet/templates/**` (18) and `samples/**/templates` (9) are governed by the parity
  contract and the golden-corpus spec, whose byte requirements are stricter and differently
  motivated. Separately, `Heddle.Performance` carries a **fourth copy of the path-traversal helper**
  — the same failure class phase 7 D2 deletes. **Ruling (user, 2026-07-26): leave
  `Heddle.Performance` alone entirely — change nothing within it.** A new benchmark effort is
  mid-flight there and must not be disturbed. *Folded into:* phase 7 (the shared props file serves
  the four *test* projects only; `Heddle.Performance` is explicitly out of scope). **Accepted
  residue:** its path-traversal helper survives, so the failure class phase 7 D2 eliminates is
  removed from the test suites but not from the benchmark project. Recorded as accepted, not as an
  oversight — revisit once the benchmark work settles.
- **Q7.3 — Delete or relocate the six checked-in written artifacts** (`test-<name>.html` × 5,
  `test.html`)? They sit inside the corpus directory and are written by tests via
  `File.WriteAllText` into a tree three projects would copy from. They look like debugging aids, but
  confirming that requires ruling that nothing reads them. **Ruling (user, 2026-07-26):
  relocate**, do not delete. *Folded into:* phase 7 WI4 — they move outside the shared corpus glob
  so no file inside it is written by a test, and the writing tests are repointed at the new
  location.
- **Q7.4 — Is migration stage 5 ("the remaining feature-shape families") in phase 7 or a
  follow-on?** D4's criteria scope stages 1–4 definitively; stage 5's edge is soft, and a ruling
  lets stages 0–4 be sized. **Ruling (user, 2026-07-26): all stages, including 5, land inside
  phase 7.** The D4 coverage residue is closed completely rather than left as a tail. *Folded into:*
  phase 7 WI9 (stage 5 promoted from conditional to committed); each stage keeps its own
  byte-neutral gate and suite-time measurement, so the open-ended scope is bounded by per-stage
  acceptance rather than by stopping early.

## Post-audit behavioural questions

- **Q8.1 — The overload-tie silent degrade: make it a build error?** Both reviewers and phase 4's
  own audit agree the current state violates two ratified principles. The generator **has already
  computed** the illegality (`BindOutcome.Ambiguous` from the shared `OverloadRank` core) and then
  reports nothing, so a provably-illegal template gets a **green build with zero diagnostics** and a
  hard `HED1013` at first render. That contradicts the **match principle** ("errors always match")
  and the **fallback-legitimacy principle** ("everything else surfaces as an error" — the closest
  legitimate analogue, `UnsupportedFunction`, is legitimate *because* the build refused on purpose
  **and warned** `HED7014`). Phase 4's reshaped quarantine fixture currently *pins the silence*
  (`Assert.DoesNotContain(… Severity == Error)`).
  The fix needs a new diagnostic ID (**next free is `HED7025`**) and a load-bearing side condition:
  report only when the outcome is `Ambiguous`/`None` **and** no argument estimate is `Unknown`,
  because today's `null` return conflates "provably ambiguous" with "an argument I could not type".
  **This is a build-surface change — a project that builds today would start failing.**
  **Ruling (user, 2026-07-26): make it a build error, `HED7025`.** The generator must not stay
  silent about an illegality it has already proved. Accepted consequence: a project whose template
  contains an ambiguous overload call and which builds green today will start failing its build —
  the same posture phase 5's emitter-fault error and phase 3's `HED7021` took. The side condition is
  mandatory: report only when no argument estimate is `Unknown`. Phase 4's quarantine fixture must
  stop pinning the silence. *Folded into:* a post-audit work item; `HED7025` claimed in registry
  order with rows in `cross-cutting-decisions.md` and `precompilation.md`.
  **Implemented (2026-07-26).** `BindOutcome` is propagated out of both binders as a `BindRefusal`
  (`Bound` / `Unproven` / `ProvenIllegal`) instead of collapsing to `null`; `NativeExpressionWriter`
  records a proven-illegal call at its `.heddle` position and `TemplateEmitter.DrainUnresolvable`
  reports `HED7025` at Error, deduplicated per call site, in every result branch — the same channel
  `HED7008` uses. The refusal itself is unchanged: the template still degrades, so no rendered byte
  moves on either tier; what changed is that the build stops being silent about it. The message is
  the runtime's own sentence for the same input (candidate signatures included, spelled through the
  shared alias table) plus the run-tier id it is the twin of, so the two tiers say the same thing.
  **The side condition is implemented as an early return before `OverloadRank.Bind` runs**, in both
  binders: an argument the estimator returns `Unknown` for has no rank token at all, so no front is
  ever computed for it and there is nothing to report — pinned by
  `AmbiguousOverloadDiagnosticTests.AnUnknownArgumentEstimateStaysASilentDegrade` (and its nested and
  export twins), and mutation-verified by making `TryDescribe` hand `Unknown` a token, which reddens
  exactly those cases. `BindOutcome.None` follows `Ambiguous` as the audit proposed. Exports behave
  identically (phase 3 made them rankable) and are covered on both arms. Breaking-window disposition
  recorded in [breaking-windows.md](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings)
  as **defect repair, not window-gated**, with the residue in Q8.18.
- **Q8.2 — The P1 binary break: which mechanism?** *(Partially ruled: the user has ruled "bump to
  2.1 and resolve as a binary breaking change".)* What remains open is the **gate**:
  `PrecompiledExtensionBinding`'s 2-arg `.ctor` no longer exists in metadata, while
  `MinSupportedSchemaVersion = 1` still *accepts* manifests that reference it — so the fault lands
  as a `MissingMethodException` out of `Register()` at host startup rather than as a clean rejection.
  Raising `MinSupportedSchemaVersion` to 4 exactly excludes the faulting set (schema 1–3 manifests
  were built against the 2-arg ctor; 4+ against the 3-arg). **Default if unruled:** bump the version
  to 2.1, raise `MinSupportedSchemaVersion` to 4 so the gate rejects cleanly instead of faulting,
  add a binary fixture built at the old schema so the rejection is *demonstrated*, and record the
  break in the CHANGELOG and `breaking-windows.md`.
  **Ruling (user, 2026-07-26): raise `MinSupportedSchemaVersion` to 4.** The break ships as declared
  (version 2.1); no compatibility shim. The gate must reject cleanly rather than advertise a support
  window the metadata cannot honour, and the rejection must be demonstrated by a manifest fixture
  built at the old schema — not asserted. *Folded into:* a post-audit work item.
  **Implemented (2026-07-26):** `MinSupportedSchemaVersion = 4`, landed atomically with Q8.11's 2.1 bump.
  The demonstration is `OldSchemaManifestRejectionTests` over `OldSchemaManifestFixture`, which compiles a
  manifest against a **reference facade** carrying the pre-break surface under the real assembly's
  identity (name, version, public key), with the real `Heddle` excluded from the reference set — so the
  emitted IL genuinely names `.ctor(string, string)` and cannot bind to the current three-parameter form.
  Three assertions: the 2-arg constructor is absent from metadata; a fixture declaring a schema below the
  floor is rejected cleanly (`SchemaVersionUnsupported`/`HED7102`, no throw, nothing registered); and the
  *same bytes* declaring schema `Min` are admitted and fault with `MissingMethodException` — the control arm
  that makes "the gate prevents a startup crash" evidence rather than narration. Mutating the facade to
  declare the optional third parameter — reproducing the substitution the ruling forbade — reddens the suite.

  **CORRECTED (2026-07-26, same day): the number was wrong, and so was the reason given for it.** The user
  checked the release and the premise collapsed. Verified against the `v2.0.0` tag: the shipped generator
  emitted `schemaVersion: 2` (`HeddleTemplateGenerator.cs:380`), the shipped engine accepted
  `Min = 1, Max = 2` (`PrecompiledTemplates.cs`, two private consts — `PrecompiledSchema.cs` did not exist
  yet), and the shipped `PrecompiledExtensionBinding` had a **real 2-arg `.ctor`** which the shipped emitter
  called (`TemplateEmitter.cs:2591`). **Schemas 1 and 2 are the only released schemas. Schemas 3, 4 and 5 were
  all unreleased.**

  Two consequences, both landed. First, **the reasoning in `breaking-windows.md` was false and is replaced,
  not softened**: it argued the break "already shipped, in 2.0.0, because schema 4's optional parameter
  removed the 2-arg ctor then". Schema 4 never shipped, so the constructor break has never shipped either —
  it is a genuine *pending* 2.1 binary break, which is the opposite of the claim. A normative document cannot
  carry a false premise, and grounds (a) of that disposition is withdrawn outright; (b) and (c) carry it.

  Second, **the unreleased history collapses into a single schema 3.** `Min = Max = Current = 3`, and
  everything that had been 3, 4 and 5 becomes 3: `DynamicMemberRoutingSchemaVersion`,
  `PropLayoutFingerprintSchemaVersion`, `PerCarrierLocalsSchemaVersion`, plus Q8.30's
  `RegisteredNameSchemaVersion` and Q8.31's `LinePathFormSchemaVersion`. Rationale: an increment no user
  could observe is not a migration step, and three of them would advertise a history that never existed while
  leaving the window claiming to read shapes no generator ever emitted. One increment past the released `2`
  carries all of it. `Min = 3` — not the `4` the ruling's words named, because the collapse makes `4 > Max`
  impossible; `3` is the faithful reading of "reject every released schema, accept only the new one".

  **The gate that happened to hold, re-derived.** `Min == PropLayoutFingerprintSchemaVersion` was asserted as
  an identity — "the floor is the schema at which the 3-arg constructor became the only one" — and it held at
  4 for a reason that no longer exists. It still holds, at 3, and for the *same* stated reason, so the
  identity is kept rather than deleted; what changed is that it now also implies `Min == Max == Current`,
  which the test states explicitly so the point-shaped window is visible rather than incidental.

  **The fixture now targets the released shapes.** `OldSchemaManifestRejectionTests` reddened nothing when
  retargeted, which is itself the evidence that the construction technique was right and only the numbers were
  wrong: the rejection arm became a `[Theory]` over schema **1 and 2** — both released, both in the break's
  victim set, written as literals because they are facts about the tag and deriving them from the current floor
  would make the test agree with any floor at all — and the control arm still admits the same bytes at `Min`
  and observes the `MissingMethodException`. `PrecompiledRegistryTests`' out-of-window edges became
  `[MemberData]` derived from the window: as literals (`3` and `6` against a `4–5` window) the collapse would
  have left the "below the floor" case testing *acceptance* while the test kept passing.
  `PropLayoutFingerprintTests.AManifestPredatingTheRowStillPasses` is renamed
  `ARowWithNoFingerprintIsCheckedVacuously` with its schema claim removed, because it could never have been
  evidence for it; `PrecompiledRegistryTests`' five schema-1 registrations now ask for
  `MinSupportedSchemaVersion` (none of them was ever about a number) and its unsupported-schema test became
  a `[Theory]` covering **both** edges — the below-floor case had no coverage at all, which is how the
  faulting set stayed accepted. Reason-code fallout registered as Q8.24.
- **Q8.3 — Fold the runtime onto the two remaining generator-only "shared" cores?**
  `Language/Binding/ExtensionRegistrationRules.cs` and `Language/Binding/TypeSpelling.cs` are called
  **only** by the generator; the runtime still hand-inlines both rules
  (`TemplateFactory.AddExtensions`, `ReflectionHelper`). Mutating the shared copy reddens **zero**
  runtime tests, so the files are transcriptions rather than sources of truth — the "reads as fixed
  and is not" shape. `ExportBookkeeping<TPayload>` has no test at all.
  **Ruling (user, 2026-07-26): fold.** *Folded into:* a post-audit work item; acceptance is that
  mutating each shared rule reddens at least one **runtime** test, which is the property whose
  absence made these transcriptions rather than sources of truth.
  **Implemented (2026-07-26):** `TemplateFactory.AddExtensions` resolves collisions through
  `ExtensionRegistrationRules.Resolve` and `LoadExtensions` sorts by its `OrderingKey`;
  `ReflectionHelper.ResolveType` drives `TypeSpelling` through a reflection `ITypeLookup<Type>`
  adapter and its five duplicate parser methods plus the tuple regex are deleted — the record's
  "split out of `ReflectionHelper`" claim is now true, and corrected where it was not.
  `ExportBookkeeping` gained `ExportBookkeepingTests` in `Heddle.Tests`. Mutation-verified per rule
  (see [phase 3's post-audit section](phase-3-binding-layer.md#post-audit-work-items-2026-07-26)).
  The fold surfaced two divergences, both fixed in the shared file so the tiers move together: the
  parser refused the legal one-element tuple `(int)` that reflection has always resolved, and it now
  tolerates a whitespace-padded top-level spelling the run tier used to reject.
- **Q8.4 — Model `[ExportExtensions]` in the generator's extension discovery?** The runtime only
  scans assemblies carrying the attribute; the generator scans all referenced assemblies, so it
  binds and precompiles extensions the runtime will never register → a permanent silent per-request
  fallback. Pre-existing and fallback-safe, but it is a live instance of the failure mode the
  program exists to eliminate, and no phase owns it.
  **Ruling (user, 2026-07-26): close it.** *Folded into:* a post-audit work item; acceptance
  requires a fixture using an extension in an assembly *without* the attribute, since no test uses
  such an assembly today.
  **Implemented (2026-07-26):** `ExtensionBinder.CollectExported` reproduces
  `TemplateFactory.ObtainExtensions`' scope — the engine assembly whole and unconditional, every other
  assembly only through `[ExportExtensions]` (named types, or all for the parameterless form, which
  short-circuits the assembly's remaining attributes as the runtime's `break` does).
  `ExportExtensionsScopeTests` supplies the missing kind of assembly. Two fixture debts fell out and
  were paid: probe compilations now declare their exports through one single-sourced helper, and the
  integration-tests assembly's nine declared-but-unexported extension types joined its
  `[ExportExtensions]` list.
- **Q8.5 — Fix `TemplateEmitter.StripGlobal`'s hard-coded assembly name?** It builds a bare dotted
  type name and feeds it to `RecordExtensionBinding`, whose `assembly` parameter **defaults to the
  literal `"Heddle"`**. For a user-defined nested or out-of-engine branch-role extension the
  manifest records `Ns.Outer.Inner, Heddle` where the gauntlet computes
  `Ns.Outer+Inner, <realAsm>` — drift #6's shape surviving on a path **no fixture exercises**.
  Phase 3's success criterion claimed zero remaining inline `global::`-strip AQN constructions.
  **Ruling (user, 2026-07-26): fix.** *Folded into:* a post-audit work item, TDD — the reproducing
  fixture lands red first, because the defect's whole character is that no fixture reaches it.
- **Q8.6 — Schedule the compile-channel drain?** Recorded as a program-level gap: the generator
  runs no compile-channel stage, so eleven id-carrying warnings (`HED1016`, `HED2002`–`HED2004`,
  `HED3001`–`HED3005`, `HED4002`, `HED4005`, `HED5011`) still never reach a build diagnostic, and
  phase 6's forwarded-ID fix is correct but **latent** — nothing can fire it. Q6.1's ruling ("if
  diagnostics can surface early, they must — on both tiers") is therefore unmet for those eleven.
  Phase 6's assessment: realistically a small phase, not a work item.
  **Default if unruled:** leave unscheduled and recorded.
- **Q8.7 — `docs/native-expressions.md` deviation 1 is wrong.** It states `==`/`!=` on "unrelated
  reference/**mixed** types" compiles to a total `object.Equals`; the runtime guards with
  `IsReferenceish(left) && IsReferenceish(right)`, so `string == int` is `HED1008` on **both** tiers.
  Under the authority convention this document outranks both implementations, so the wrong sentence
  is a live trap: aligning to it would introduce a bug in the name of fixing drift.
  **Ruling (user, 2026-07-26): widened — plan a post-implementation documentation sweep covering
  *all* docs, not just this sentence.** Deviation 1 is one instance of a general problem: seven
  phases plus six audits changed behaviour, added seven diagnostics, altered a default, narrowed a
  catch, changed a runtime resolution rule and shipped a binary break, and the prose documentation
  has been updated only where a phase happened to touch it. The sweep is authored as its own effort
  with its own plan, so the doc corrections are auditable rather than folded invisibly into code
  landings. *Folded into:* [phase 8 — docs sweep](phase-8-docs-sweep.md) (authored 2026-07-26;
  proposed, not started). **Sharpened while authoring it:** deviation 1 is one of **four** false
  claims in that one document — deviation 6 (user-defined operators are *not* honored for
  `&`/`^`/`|`, shifts, or any unary operator), the shift row's `int`-right-operand rule (any integral
  is accepted and converted, so the doc **understates** what compiles and "fixing" the code to match
  would break working templates), and the lifted-operands claim that equality lifts (lifting exists
  on the numeric paths only; a `bool`/`bool?` equality is `HED1008` and the bitwise pair has no
  diagnostic at all) — plus seven more claims imprecise enough to mislead an implementer. The
  generator's shared rule tables already describe every one of those divergences **correctly**, in
  comments beside the verdicts that encode them, so the tree's most accurate account of
  native-expression semantics is `Language/Expressions/NativeOperatorRules.cs` and the document the
  convention points at is the least accurate. That inversion is why phase 8's first design decision
  narrows the authority convention rather than only fixing the sentences.
- **Q8.8 — `net48`/`net6.0` verification.** `net48` is `Condition="'$(OS)' == 'Windows_NT'"` and has
  never run on this box; `net6.0` aborts with `MSB4181`. **Drift #9 (`ToString("R")`) can only be
  observed on `net48`** — `"R"` genuinely is shortest-round-trippable on CoreCLR, so the fix's
  *sufficiency* is unverified even though a revert is now caught by 23 cases. The user has said
  Windows will be checked separately. **Ruling (user, 2026-07-26): take the default** — drift #9 is formally **unclosed** until the Windows `net48` run. No work on this box can close it; the guard here is a revert-detector (23 cases), not proof of the fix's sufficiency.
- **Q8.13 — Does the value-path coercion rail need a byte-level fixture?** `native-expressions.md`
  §4 is normative: a boxed non-string reaching the value path is dropped to empty while the render
  path stringifies it. Phase 1's audit found this is pinned as emitted *shape* plus render-path
  behaviour, with **no byte-level fixture on either tier**. Closing it needs new fixture machinery —
  a host extension whose `ProcessData` consumes its body's `Execute` result and returns a
  non-string. **Ruling (user, 2026-07-26): implement it.** Build the machinery — a host extension whose `ProcessData` consumes its body's `Execute` result and returns a non-string — and pin §4 at byte level on both tiers. The joint-land rule may rewrite the rail later; a byte-level pin is what makes that rewrite safe, not a reason to skip it.
- **Q8.14 — Should `[EncodeOutput]` + `[NotEncode]` on one extension be an error?**
  `RenderTypeRules.Derive`'s fourth truth-table row ("`[NotEncode]` vetoes") is **unreachable from
  any real extension** — nothing in the tree carries both attributes, so the row is exercised only
  by the truth-table theory. Either the combination is meaningful and deserves a fixture, or it is
  incoherent and should be a registration/build **error on both tiers** under the match principle.
  **Ruling (user, 2026-07-26): make it a diagnostic, on two surfaces.** The combination is incoherent, so (a) at the **use site**, a compile-time **error** when a template uses such an extension, and (b) at the extension's **own** project build, a **warning** raised by an analyzer where the extension is declared — so the author who wrote the contradiction sees it in their own build instead of only their consumers seeing it in theirs. Two IDs: `HED7026` (use-site error), `HED7027` (declaration-site analyzer warning). This is Q6.1's early-surfacing principle applied to extension *authorship*, and the program's first declaration-side analyzer — a new surface, to be treated as such rather than as a rule-table tweak.
- **Q8.15 — Stabilise the two intermittently-failing tests?** Phase 3's audit observed
  `Heddle.Tests.BodyModelRuleTableTests` (a *different* row failing on each of two consecutive
  solution runs; passes in isolation and in its leg alone) and
  `Heddle.Generator.Tests.CallTargetAdoptionTests.AnExportedFunctionDoesNotStealARegisteredExtensionName`
  (failed once in a solution run, then passed in isolation, three leg runs and two solution runs).
  Both go through the generator, under concurrent multi-TFM execution. This matters beyond tidiness:
  **every mutation result in these audits rested on "this test went red because of my change"**, and
  flakiness poisons that inference — an intermittently-red suite is how a real surviving mutant goes
  unnoticed. **Ruling (user, 2026-07-26): root-cause it and decide — do not paper over it.** Identify the actual mechanism (shared mutable state across TFM legs, xUnit parallelism, the process-global precompiled registry, an ordering dependency) rather than adding tolerance. Legitimate outcomes include serialising the affected collection, isolating the shared artifact, or removing a dependency on something genuinely non-deterministic. **A retry attribute is not an acceptable resolution** — it preserves exactly the property that makes a surviving mutant invisible.
- **Q8.15 investigation record (2026-07-26) — reproduced flakiness, but NOT the two named tests; root
  cause of those two remains unestablished.** Recorded in full because the negative results are the
  valuable part.
  **Pressure applied:** 30 × full-solution runs in the working tree (failures on iterations 7, 9 and
  16–29); 25 × full-solution runs in an isolated `git archive HEAD` copy (**all clean**); 50 legs of
  `Heddle.Generator.Tests` with both TFMs concurrent at `maxParallelThreads=32` (clean); 150
  cold-start processes × 32 threads rendering the `BodyModelRuleTableTests` rows (clean); 6 solution
  runs against a deliberate concurrent builder (clean).
  **What the working-tree failures actually were:** not the named tests. A concurrent MSBuild
  invocation rewriting the test projects' `bin/**` while test hosts read from it — triggered by other
  agents editing `.csproj` files, which makes MSBuild's `IncrementalClean` delete and re-copy the
  whole content-file set. **That is an artifact of running several agents against one working tree,
  not a product defect**, and the 25 clean isolated runs confirm it.
  **Hypotheses positively ruled out, with evidence, so they need not be re-tested:** the
  process-global precompiled registry (every class touching it is already in the serialized
  `[Collection("PrecompiledRegistry")]` — the discipline is complete); in-process class parallelism in
  `Heddle.Generator.Tests`; ANTLR's process-static prediction caches (real shared mutable state, but
  0 failures in 150 cold-start 32-thread bursts — see Q8.22); `ReflectionHelper.Reconfigure`'s
  unlocked dictionary rebuild (a genuine race in the code, unreachable here — `_configured`-guarded,
  and the caller lives in another process); an order dependency in `BodyModelRuleTableTests` (it is
  the one class of ~60 that does not call `HeddleTemplate.Configure`; 14/14 green in isolation); and a
  Roslyn metadata race (impossible — default `ExpressionMode` is `Native`, so that test never enters
  the C# tier).
  **No code change was made**, deliberately: with neither named test reproduced, any change would be
  tolerance dressed as a fix — what the ruling forbids. In particular `Heddle.Generator.Tests` was
  **not** blanket-serialised, because serialising without a demonstrated race is the same defect in a
  different costume.
  **Q8.15 stays OPEN.** The blocker is diagnostic, not analytic: the original observation captured
  test *names* but not failure *messages*, and for `BodyModelRuleTableTests` "expected Ada, got null"
  (a type/extension-resolution fault) and "expected Ada, got Ada0" (a chained-channel fault) have
  disjoint root causes. **Whoever next observes either failure must capture the assertion text.**
- **Q8.20 — Should a test ever read another project's build output?** `CorpusResolverSweepTests`
  (`:50-84`) string-substitutes its own assembly path to reach
  `src/Heddle.Tests/bin/Debug/<tfm>/Heddle.Tests.dll`, loads it as a `MetadataReference`, and reads
  the corpus from `src/Heddle.Tests/TestTemplate` — while
  `Heddle.Generator.IntegrationTests.csproj` references only `Heddle.Generator` and `Heddle`, so
  **nothing orders it after `Heddle.Tests`**. Under `dotnet test Heddle.sln` MSBuild may run its
  `VSTest` before or during that build; this was *reproduced* (the iteration-7 failure). Three tests
  are exposed, and to their credit they fail loudly rather than skipping.
  **Default:** add the missing `ProjectReference` as the narrow fix now, and treat the general
  pattern (cross-project output reads) as something phase 7's shared corpus location should remove
  rather than formalise.
  **Implemented (2026-07-26, phase 5's landing):** the `ProjectReference` is added with
  `ReferenceOutputAssembly="false"` — build order only, and deliberately not a compile-time reference.
  This suite hands hand-filtered reference sets to the compilations it creates (it already strips
  `Heddle.Generator` to avoid `CS0433` on the phase-5 linked types), so pulling `Heddle.Tests` and its
  transitive graph into its own compile would put a second set of names in scope for no benefit: the DLL is
  loaded **by path at run time**, not bound at compile time. No cycle (`Heddle.Tests` references only
  `Heddle`) and no duplicate-type or reference-set problem — the suite builds clean on both TFMs. The
  general question stands: the fix orders the build, it does not stop a test reading another project's
  output.
- **Q8.21 — Should the generator test projects carry `DisableTestParallelization`?**
  `Heddle.Tests` and `Heddle.LanguageServices.Tests` both do, justified in `AssemblyInfo.cs` by
  "Heddle uses process-global static state"; `Heddle.Generator.Tests` and
  `Heddle.Generator.IntegrationTests` do not, though they link the same front-end sources. No race
  was found, so this is about **stating an invariant**, not fixing a defect. **Default:** document the
  asymmetry deliberately rather than changing it, since serialising without a demonstrated race costs
  suite time for no evidence.
- **Q8.22 — ANTLR's process-static prediction caches.** `HeddleLexer`/`HeddleParser` share
  `decisionToDFA` and `sharedContextCache` across every parse in the process, mutated during
  `AdaptivePredict` under `PredictionMode.SLL`. 150 cold-start 32-thread bursts produced no
  misbehaviour, but the runtime *is* used concurrently in production (`BranchConcurrencyTests`,
  file-watcher reloads on background threads) and `Antlr4.Runtime.Standard`'s thread-safety guarantee
  for this is documented nowhere in the repo. **Default:** record the reliance explicitly in the
  concurrency section of the spec, and revisit if any concurrent-parse fault is ever observed.
- **Q8.23 — `PipelineContractTests` uses fixed shared temp paths** (`Path.GetTempPath()/heddle-root`,
  `.../elsewhere/shared/banner.heddle`) where every other test uses a `Guid`-suffixed directory.
  Harmless today because they are pure path arithmetic never materialised on disk, but it is the one
  place the convention is broken, and a future test that *creates* that path would collide across the
  two TFM legs. **Default:** convert to the `Guid`-suffixed convention.
- **Q8.16 — `RegionTests.LocationOffsetOf` returns a hard-coded `0`.** Reported by phase 1's audit,
  not its artifact, so it was left. A helper that reads as a position assertion and asserts nothing
  is worse than an absent assertion, because it looks like coverage. **Ruling (user, 2026-07-26): fix it.** Compute the real offset, so assertions that read as position assertions actually are ones.
- **Q8.17 — `SymbolTypeIndex.Cache` is an unbounded static `Dictionary<Compilation, …>`.** Phase 3's
  own artifact, correct and lock-guarded, but it pins every `Compilation` it has ever seen for the
  process lifetime. Fine for a one-shot build; questionable for a long-lived IDE session where the
  analyzer sees a new `Compilation` per keystroke-batch. **Ruling (user, 2026-07-26): give the cache a real operational contract**, in three parts: (a) a **clear operation API** rather than a bare static dictionary; (b) **observable capacity/occupancy**; (c) **genuine staleness eviction** — the motivating case is that editing a template and editing it back restores an entry that is *identical yet old*, and retaining it has no value, so **age must participate in eviction, not just identity**. Design the eviction rule explicitly and record it; do not merely cap the size.
- **Q8.9 — Where does the narrowed authority convention live, and is it retroactive?** Phase 8 D3
  narrows the convention (*"expression semantics defer first to `docs/native-expressions.md`"*) so
  that a normative document outranks the implementations **only for claims that carry a verification
  marker or are covered by a gate**; an unmarked, ungated claim becomes evidence of intent, not an
  authority. The placement question is whether that lives in
  [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md) (outliving this program) or
  stays in the program README. The sharper half is retroactivity: several landed phase D-items
  resolved drift *by citing* that document, and narrowing the convention makes those citations weaker
  evidence than they were when ratified. **Ruling (user, 2026-07-26): put it in [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md)** — record there whatever matters to the *global* context, and the **documentation mapping** in particular (which document is normative for which claim block, and therefore which claims can be relied on when aligning drift). That mapping is the part a future reader most needs and the part currently spread across a registry column, a plan bullet and an implicit convention. Non-retroactive as proposed: already-ratified D-items stand.

  **Landed (2026-07-26)** as `cross-cutting-decisions.md` **D10 — "Documentation authority is mapped, and
  it is conditional"**, in two parts, because the ruling asked for two different things and they are only
  useful together. *(a)* The **mapping**: a nine-row table naming one normative home per claim block, with
  an explicit tie-break for claims that span two homes — the home whose *diagnostics* the claim can
  produce wins, since a diagnostic has a registry owner and prose does not. *(b)* The **condition**: a home
  outranks the implementations only for a claim carrying a verification marker or covered by a gate.

  Two things stated that the question left implicit and a future reader would otherwise have to guess.
  A claim block **absent** from the table has no normative document at all — the implementations are the
  authority and the runtime engine is the tie-break — so the table is closed rather than illustrative.
  And a document acquires a block **by being added to the table**, not by asserting authority in its own
  prose, which is how the convention became diffuse in the first place. Non-retroactive as ruled: already
  ratified resolutions stand, and the reason is recorded (relitigating outcomes under a rule that did not
  exist when they were taken is not a correction).
- **Q8.10 — If phase 7 has not landed when phase 8's stages 0–4 are done, does D9 ship, slip, or
  transcribe?** D9 makes qualifying doc examples executable by **single-sourcing** them from phase 7's
  shared corpus and including them into the page, so the doc and the test read the same bytes. That
  needs phase 7 stage 0 to exist. **Ruling (user, 2026-07-26): keep the docs as refined, separate prose — do not single-source them from the corpus.** Documentation has a *different job* from a test fixture: it explains, and byte-identity with a corpus entry is not a property worth buying. So phase 8's D9 is **rejected as designed**: no `@include:` from corpus templates, no corpus intent rows added for doc examples, and phase 8 no longer blocks on phase 7. Doc examples stay hand-written and are kept honest by review, not by transcription-equality. (The `ScopeChannelDocExampleTests` anti-pattern is still an anti-pattern — the answer is to delete the false coupling, not to formalise it.)

  **Landed (2026-07-26).** Phase 8's D9 is rewritten as **rejected**, with the superseded design kept
  inside a collapsed block rather than deleted, so the plan records what was decided *against* — the
  program's convention for reversed decisions. The consequences are propagated rather than left in the
  D-item: the phase header's phase-7 dependency is gone, the stage table's stage 5 no longer says
  "blocked", both D9 risk rows are struck as void, and the docs-site mechanism risk (an `@include:` path
  outside VitePress's `srcDir`) is retired unanswered — it no longer needs answering.

  **WI14 is repurposed, not dropped.** It was the include spike; it is now the deletion of
  `ScopeChannelDocExampleTests`' *"--- Verbatim from docs/custom-extensions.md ---"* coupling. The
  distinction that keeps the deletion honest: what that fixture asserts about `Scope` channel behaviour
  stays where it is genuinely a behaviour test — what goes is the *claim to be the document's bytes*,
  which nothing enforced and which the ruling makes deliberately false, since the doc is now free to
  diverge by design. Done-when requires the behavioural coverage to be either still asserted elsewhere or
  recorded as dropped with a reason, so the deletion cannot quietly lose a test.

  **One cost accepted knowingly**, recorded in D9 so it is not rediscovered as a surprise: the old
  rationale's strongest point survives rejection — this program shipped a byte-changing literal formatter
  change and a profile default flip, either of which can invalidate a documented output, and review is a
  weaker guard than a gate. D11's currency rule carries that residual risk. A stale doc example found
  later is a docs defect to fix, not grounds to reopen this.
- **Q8.11 — Should the nine `<Version>` elements be centralised as part of the 2.1 bump?** Four of
  the nine sit on non-shipping projects, all nine are overridden by CI from the git tag
  (`.github/workflows/dotnet.yml`), and `Directory.Build.props` excludes `Version` *by an explicit
  comment* — so the nine are hand-maintained documentation of the release line with no lockstep test,
  the duplication class phase 6 spent its WI7 deleting from source. The wider surface matters too:
  13 files must change for 2.0.0→2.1.0 and five more are coupled, the riskiest being
  `editors/vscode/src/extension.ts`'s `PINNED_VERSION`, which pins a NuGet tool version outside any
  consistency check. **Ruling (user, 2026-07-26): yes — centralise versioning, and additionally sign all of our own assemblies** so the strong-name warnings stop. Two deliverables: one `<VersionPrefix>` (or equivalent) in `Directory.Build.props` replacing the per-project `<Version>` elements, and strong-naming for every first-party assembly currently unsigned (the `CS8002` sources — `Heddle.Demo.Models`, `Heddle.Demo.Wasm`). Third-party unsigned references (Scriban) are not ours to sign; handle those explicitly rather than by blanket suppression, and say which mechanism was used. Both land with Q8.2's 2.1 bump.
  **Implemented (2026-07-26):** one `<VersionPrefix>2.1.0</VersionPrefix>` in `Directory.Build.props`; all nine
  `<Version>` elements deleted, including `Heddle.Performance`'s — a single-line deletion needing no other
  change to that project, which is Q7.2's stated boundary. The real inventory was **fourteen kinds** of
  statement: the nine elements, four npm manifest/lockfile pairs, `PINNED_VERSION` in
  `editors/vscode/src/extension.ts`, an `lsp.yml` `--version`, four prose release-line sentences, two
  `engineVersion` test assertions plus eight `Verify` snapshots, and — the live drift nobody had found —
  `LspServer.InformationalVersion = "1.0.0"`, which is what `heddle-lsp --version` printed and what the LSP
  `initialize` response reported for the whole 2.0 line, guarded only by a test comparing it against itself.
  That one is now *derived* from the assembly rather than gated, so the statement no longer exists. Also
  load-bearing and easy to miss: the CI beta job's `--version-suffix "-beta.N"` had to lose its leading dash,
  because a composed `VersionPrefix`/`VersionSuffix` is joined with one and would otherwise produce
  `2.1.0--beta.N`. **Signing:** `Heddle.Demo.Models`, `Heddle.Demo.Wasm`, and — found by the gate rather than
  by the warning — `Heddle.LanguageServices.Tests.Corpus`, whose csproj already carried a comment claiming it
  was signed. Eight `CS8002` → zero. **Scriban:** a declared accepted-unsigned-reference list in a new root
  `Directory.Build.targets`, keyed on the named assembly and applied only to signed projects — stated there
  rather than inside `Heddle.Performance`, which is untouchable. Recorded honestly: Roslyn has **no**
  per-reference suppression for `CS8002` (the warning carries no source location and `Csc` takes only a
  project-wide list; `NoWarn` metadata on a `PackageReference` was *measured* to have no effect), so the
  mechanism is keyed *on* the reference rather than scoped to it, and a project referencing both a listed and
  an unlisted unsigned assembly would silence both. **The gate** is `VersionConsistencyTests` (17 cases):
  exactly one `<VersionPrefix>`, no project restating a version, the built assembly agreeing, the four npm
  manifests, the VS Code pin, the workflow `--version`, the suffix's shape, the four prose sentences, the
  CHANGELOG section and its compare link, no version literal in `LspServer.cs`, every `src/` project signed,
  and no blanket `CS8002` suppression. Warning-regression gating in general is out of scope: Q8.26.
- **Q8.12 — Who fixes the sample that still passes the removed `Name` item metadata?**
  `samples/codegen-t4-successor/CodegenT4Successor.csproj` carries
  `<HeddleTemplate Include="templates\report.heddle" Name="BuildReport" />`, and
  `Heddle.Generator.props` no longer reads it (Q5.1 removed it), so the sample silently registers
  under its filename key rather than its intended name — and the sample is golden-checked, so the
  golden currently encodes the wrong outcome. It is a live user-facing artifact, not prose, so phase 8
  records it rather than editing it (its D2 rule: the sweep corrects documents, never code).
  **Ruling (user, 2026-07-26) — with a correction to the record.** The user's instruction: *"I didn't ask to remove `Name`, I only asked to wire `Precompile` true/false."* So **implement optional custom name mapping** via `Name`, and the sample that still passes it becomes correct rather than stale.

  **The record overreached.** Q5.1 above reads *"`Name` removed per the recommendation"*, and phase 5 implemented that removal. Whatever the recommendation said, removal was not the ask — the ask was to wire `Precompile`. `Name` was dead code (declared as `CompilerVisibleItemMetadata`, never read), so removing it changed no behaviour and the sample's `Name="BuildReport"` was always ignored; the defect was that the *feature was never wired*, not that the metadata existed. Restoring it as a real optional key mapping is the smaller, better fix and closes Q8.12 as a side effect.

  Scope: `Name` sets the template's registration key, overriding the path-derived key; it must interact correctly with `TemplateKey` normalisation, the duplicate-key check (`HED7002`), the case-only-twin check (`HED7003`), and the out-of-root warning (`HED7018`). A malformed or colliding `Name` needs a diagnostic — claim `HED7028` if a new one is required rather than reusing `HED7004`.

  **Implemented (2026-07-26), then CORRECTED the same day (Q8.25). Both landings are recorded, because the
  first one shipped and was wrong.**

  *Landing 1 — superseded.* `Name` was a **second spelling of `Key`** — one setting, so it shared every
  downstream rule instead of acquiring parallel ones: the same `TemplateKey` normalisation, the same `HED7002`
  and `HED7003` participation, the same `HED7018` suppression (stated as deliberate: "that warning's premise is
  that the flattened key was *not* asked for, and an explicit key asks for exactly the key it names"), and a
  `Key`+`Name` disagreement reported as `HED7004`. `HED7028` was declined on the reasoning that both new faults
  were instances of `HED7004`'s "this item's explicit key metadata is unusable" class. **That reading was an
  override**, and it silently broke every `@<<` that named a file by its path on a named item — the defect
  Q8.25 opened and ruled on.

  *Landing 2 — what shipped.* `Name` is **additive**: the template keeps its path-derived (or explicit `Key`)
  key **and** gains the name; both spellings resolve. Every rule above was re-derived rather than adjusted, and
  four of them changed. `HED7018` is suppressed by `Key` **only** (an additive name leaves the flattened key in
  place, so the warning is still about something real). `HED7002`/`HED7003` are over keys **only** (a name
  registers no manifest row and is never a registry lookup, so it can neither duplicate nor case-shadow a key);
  an unusable name — a value the normalizer refuses, or a spelling another template already answers to — is
  still `HED7004`, but against the name alone and without un-precompiling the template. `Key`+`Name` is **not a
  conflict**: it is two names for one template, which is the point, so that `HED7004` arm is gone. `HED7028`
  **is** claimed, for the new advisory. `HED7004`'s generalised message stays — it is still carrying two reasons.
  The full re-derivation table is in the [phase 5 record](phase-5-pipeline-config.md).

  **The larger defect this uncovered.** `Name` was not merely unread: **none** of the three metadata worked
  from a real project. `Heddle.Generator.targets` restated each as `<Key>%(HeddleTemplate.Key)</Key>` inside
  an `Include="@(HeddleTemplate)"` transform — and since the transform already copies every metadatum, while
  a cross-item `%()` reference outside a target evaluates to the empty string, each element *overwrote* the
  copied value with `""`. So `Key` and `Precompile="false"` were inert too, and nothing noticed because every
  test injects `build_metadata.*` directly and so never crosses this file. Fixed by deleting the elements.
  Two gates stand behind it now: a structural set-equality pin
  (`PipelineContractTests.EveryDeclaredItemMetadataIsReadByTheGeneratorAndNotNulledByTheTargets`, which also
  refuses any restatement) and the behavioural one — `samples/codegen-t4-successor`, whose generated entry
  class is *named by* its `Name` metadatum and called by name, so a metadatum that stops flowing fails that
  build in CI.

  **The sample, twice.** Landing 1 made `Name="BuildReport"` rename the key, the entry class and the
  `Program.cs` call, changing the golden by one line. Landing 2 makes the class name stop moving, so that gate
  evaporated and was replaced with a real use of the feature: `templates/_banner.heddle`
  (`Precompile="false"`, `Name="Banner"`) imported as `@<<{{Banner}}`, which fails the sample's build with
  `HED7011` if the metadata stops flowing from a real csproj. The entry class is back to `Templates_Report`, the
  rendered output is byte-identical (the import line carries `@\` so it emits nothing), and only the
  generated-source golden moves.

  **The `#line` separation stands.** The emitted `#line` directives named the *key*, indistinguishable from the
  file path only while every key is path-derived; landing 1 exposed that through `Name`, and with `Name`
  additive only `Key` can still reach it — the separation is the same separation and is still needed. The
  `#line` path *form* is Q8.27, ruled and landed. Import-map fallout was registered as Q8.25 and is the
  correction above.

## Opened by the Q8.1 landing (2026-07-26)

- **Q8.18 — `HED7025` proves illegality against the *build-time* function inventory. Is that the
  right scope?** Q8.1's build error rests on a proof, and the proof is relative to the overload set
  the generator can see: the shipped built-in table plus every `[assembly: ExportFunctions(...)]`
  method. A host may add an overload at run time through `TemplateOptions.Functions.Register`, whose
  documented rule is "same name + identical parameter types replaces; otherwise **adds an
  overload**". Such an addition can make an ambiguous set unambiguous (`min(uint, uint)` would resolve
  `min(1, 2u)`) or an inapplicable set applicable (`min(int, int, int)` would resolve `min(1, 2, 3)`) —
  so a template that is legal *for that host* now fails the build, and unlike the emitted-code case
  the gauntlet's `FunctionBindings` overload-count check cannot rescue it. A delegate registration is
  invisible in assembly metadata, which is precisely the blindness `HED7014` exists for — and
  `HED7014` chose *warning + degrade* for it. **Ruling (user, 2026-07-26) — the premise was wrong, and the question dissolves.** The user's
  observation, verified in source: precompiled function calls are **statically bound at build time**.
  `NativeExpressionWriter` emits a direct call to a shim method resolved through a build-time
  `DefaultShims` table (`:25`, `:98`, `:154`); nothing in `PrecompiledRuntime` consults
  `options.Functions` at render. So **precompiled code cannot resolve a runtime-registered function
  at all** — the build-time inventory is not merely *a* scope, it is the only scope that can be
  correct for what precompiles. A host that registers extra overloads is handled by
  `PrecompiledGauntlet.CheckFunctions`, which detects the divergence and degrades to the dynamic tier.

  The one residual, stated for the record rather than as a reopening: `HED7025` is an **error**, so it
  fails the build before any tier is chosen, and the gauntlet cannot rescue a failed build. That is
  acceptable because the escape hatch is real and was verified — `Precompile="false"` `continue`s at
  `HeddleTemplateGenerator.cs:193`, *before* key derivation and emit, so no diagnostic can fire for an
  opted-out item while it stays in the import map. **Closed; no code change, and no option added.**
- **Q8.19 — Two illegal calls in one template report once. Should the emitter continue past an
  unwritable construct to collect the rest?** `BuildBody` abandons at the first construct it cannot
  write, so `@(min(1, 2u)) @(max(1, 2u))` yields one `HED7025`, not two; the author fixes one, rebuilds,
  and meets the next. This is pre-existing emitter shape rather than anything Q8.1 introduced (it is
  equally true of `HED7008`), but Q8.1 is the first error where the one-at-a-time surfacing is the
  *whole* user experience, so it is now worth asking. **Ruling (user, 2026-07-26): collect all of them, if it is not a major undertaking.** Assess the
  cost honestly first — `BuildBody` abandoning at the first unwritable construct is long-standing
  emitter shape, and unwinding it is not obviously cheap. If continuing past the first error is
  contained (collect refusals, keep walking, report each at its own span, and still emit nothing for
  the template), do it. If it turns out to require restructuring the body walk, stop and report the
  cost rather than half-doing it — a partial rewrite of the walk is worse than the current honest
  one-at-a-time behaviour.
- **Q8.24 — A 1.x manifest's rejection reason changed from `EngineVersionIncompatible` to
  `SchemaVersionUnsupported`.** The 2.0 window's as-shipped record and the CHANGELOG both state that
  1.x precompiled assemblies fall back because "the engine-version gate rejects 1.x manifests". With
  `MinSupportedSchemaVersion = 4` that is no longer the gate that fires: `Register` runs the **schema**
  check first, and every 1.x manifest declares schema 1, so it is now rejected as
  `SchemaVersionUnsupported` (`HED7102`) before the engine-version check is reached. The observable
  outcome is identical — one callback, whole-assembly fallback, `Strict` throws — but a host that
  branches on `PrecompiledFallbackEvent.Reason` (a public enum) sees a different member, and two shipped
  documents name the wrong one. **Ruling (user, 2026-07-26) — closed as invalid; the question was speculation.** *"We have NO 1.x
  manifests, 2.0 is the first version with pre-compilation."* Correct: precompilation shipped in 2.0.0,
  so no 1.x manifest has ever existed and the reason-code change has no population to affect. The
  entry was reasoning about a hypothetical migration path.

  Two corrections follow. First, the shipped documents that name `EngineVersionIncompatible` as the
  gate for "old" manifests are describing a case that cannot occur — that is a **docs defect for
  phase 8**, not a behavioural question. Second, on *"why is throwing an error on a wrong manifest a
  problem, and why do we need an option for that"*: it is not a problem, and **no option was added**.
  `PrecompiledMismatchPolicy` is pre-existing 2.0 API (`Strict` throws, `Fallback` degrades) and
  phase 0's guardrails depend on `Strict`; nothing in this program introduced a switch for this. The
  entry read as though it were proposing one, which it was not. **No code change.**
- **Q8.25 — An explicit `Key`/`Name` makes a template unimportable by its path.** The `@<<` import map
  is keyed by the same derived key as the registry, so `@<<{{ templates/report.heddle }}` no longer
  resolves once that item carries `Name="BuildReport"` — the importer draws `HED7011`, and the fix is to
  import the *key*. This is pre-existing for `Key` and was simply unreachable while the metadata was
  inert (Q8.12), so wiring the feature made it reachable for the first time. **Ruling (user, 2026-07-26) — this is a CORRECTION to what was just landed.** *"Name is an
  optional additional name register for import to use, not an override, just like I asked it to be."*

  The landed implementation made `Name` a *second spelling of `Key`*, i.e. an override: the
  path-derived key is replaced, so `@<<{{ templates/report.heddle }}` stops resolving and draws
  `HED7011`. That is wrong. **`Name` must be additive**: the template keeps its path-derived key *and*
  gains the registered name, so imports by **either** spelling resolve. Nothing that resolved before
  may stop resolving.

  Add a **warning** where a named template is imported by path, stating that the template has a
  registered name and that the name-first spelling is preferred for named templates — guidance, not a
  break. Claim `HED7028` for it (still free).

  Note this also changes the `HED7018` interaction recorded under Q8.12: an explicit `Name` no longer
  replaces the key, so it cannot suppress an out-of-root warning about the path-derived key. Only an
  explicit `Key` does. Re-derive that rather than assuming it carries over.

  **Landed (2026-07-26).** The resolution model is **two passes over the import map, keys first, names
  second** — so additivity is structural rather than conditional: a registered name cannot displace a key
  spelling, because every key is already in the map when the first name is considered. A name that finds its
  spelling taken (by another template's key, or by another name) is dropped and reported at `HED7004` against
  the name; the template's own key is unaffected, because a broken addition must cost the addition and nothing
  more. A name equal to the template's own key adds nothing and advises nothing.

  `HED7028` — *"Named Heddle template imported by key rather than by its registered name"*, **Warning** —
  fires in `ParseAndReport`, at the importer's `@<<{{…}}` block, once per distinct import spelling, when the
  import **resolved** through the key of a template that also has a registered name. It is not gated on the
  template being otherwise clean: the import resolved, so the advice is valid regardless. It stays silent for
  an unnamed template (every pre-existing project), for an import that already uses the name, and for a name
  that could not be registered. Claiming a new id was right here where landing 1 was right to decline one:
  every other `HED70xx` key diagnostic reports something *unusable*, and this reports something that *works*.

  **The re-derivations.** `HED7018`: only `Key` suppresses — verified with a three-arm test (bare warns,
  `Key` silences, `Name` still warns and names the flattened key that really registered), not assumed.
  `HED7002`/`HED7003`: keys only, which is the population they had before `Name` was wired; the alias namespace
  has its own collision rule at `HED7004`. `Key`+`Name`: **not a conflict** — landing 1's "no defensible
  precedence between two equally explicit requests" dissolves, because additive `Name` and `Key` are not
  competing for one slot. `#line`: the key↔file separation stands (only `Key` can now diverge from the file);
  the *form* is Q8.27. The sample: entry class back to `Templates_Report`, `Name` demonstrated by a named
  import-only partial instead of by a rename. The `breaking-windows.md` disposition is corrected in place — its
  rename clause now applies to `Key` alone, and `Name` moves nothing.

  The gate is `TemplateNameMetadataTests` (33 cases, up from 15), whose fixture doc states plainly that the
  first implementation was an override and was corrected. The correction itself landed TDD: a test importing a
  named template by path, red with exactly `HED7011`, before any fix.
- **Q8.26 — Warning regressions are not gated.** `Q8.11` removed the eight `CS8002` warnings and the
  build has no `TreatWarningsAsErrors`, so nothing prevents them — or any other warning class — from
  coming back. The signing half is now held by
  `VersionConsistencyTests.EveryFirstPartyProjectUnderSrcIsStrongNamed`, but that gates the *cause* for
  one warning, not warnings in general; the tree currently carries fourteen xUnit-analyzer warnings and
  four `NU1510`s that no gate mentions. **Ruling (user, 2026-07-26): leave it as is.** No `TreatWarningsAsErrors` — it obstructs quick
  proof-of-concept work and fast testing feedback. The signing *cause* stays gated (a project losing
  its strong name reddens a test); warning regressions in general are deliberately not gated.
  **Closed; no change.**
- **Q8.27 — Should `#line` name a path the compiler can open, rather than a repo-relative one?** Q8.12
  separated the `#line` file from the registration key, and the value it now emits is the template's path
  relative to `HeddleTemplateRoot` — resolvable when the compiler's working directory is the project
  directory, which is the normal case, and not otherwise. The alternative is the absolute
  `AdditionalText.Path`, which is what a `#line` is really for, but it would put machine-specific
  absolute paths into eight `Verify` snapshots and every generated-source golden. **Ruling (user, 2026-07-26): prefer absolute paths, and where relative is unavoidable, mark it
  properly rather than churning it.** Use absolute paths wherever that is workable; where a relative
  form is genuinely the right answer — multi-template setups, complex definition chains, the cases
  where a single absolute anchor cannot express the mapping — keep it and **mark the relativity
  explicitly** so a reader knows which form a given `#line` is in. Explicitly not a mandate to convert
  everything: the instruction is to be reasonable and label, not to fixate on rewriting.

  **Landed (2026-07-26): absolute where it costs nothing, relative-and-labelled where it does.**

  *Made absolute.* **Outside `HeddleTemplateRoot`** no anchor exists, and the old fallback was the template's
  *bare filename* — a name no compiler can open and one that collides across directories. It is now the
  template's own `AdditionalText.Path`, which is absolute in any real build. Snapshot cost: zero
  machine-specific text, because the five affected `Verify` snapshots use synthetic relative paths
  (`views/version.heddle`), so they simply gained the `views/` prefix they should always have had.

  *Kept relative, and marked.* **Under the root** the form stays root-relative. Absolute here was weighed and
  rejected on a real cost: `HeddleTemplateRoot` is an absolute machine path, so an absolute `#line` would put
  this machine's layout into `samples/codegen-t4-successor/golden/generated-source.cs.txt` and into any rooted
  snapshot, and the pinned artifacts would stop being comparable at all. Scrubbing the paths back out would pin
  a placeholder instead of the value, which is worse than pinning a relative path honestly. So the relativity
  is **marked**: every generated file now carries, directly under `// <auto-generated/>`, either
  `// #line file names below are RELATIVE to HeddleTemplateRoot.` or
  `// #line file names below are the template's own path (no HeddleTemplateRoot anchor applies).` A reader can
  now tell which form a given `#line` is in without knowing the project's configuration — which is the whole of
  what the ruling asked for. Cost: one line per generated template file (five snapshots plus the sample
  golden; the three manifest-only snapshots carry no `#line` and no header).

  **Not converted:** everything else. `#line` is emitted from exactly one place, both forms are now correct for
  their case, and no third form was introduced.

## Opened by the Q8.25 / Q8.27 landing (2026-07-26)

- **Q8.28 — A `Precompile="false"` item's key/name metadata faults are never reported.** The
  diagnostics loop `continue`s on `!template.Precompile` *before* key derivation, so a malformed
  `Key`, a malformed `Name` or an already-taken `Name` on an import-only item produces **no
  diagnostic at all** — the name silently fails to register and every `@<<` that used it draws
  `HED7011` somewhere else, pointing at the importer rather than at the item that is actually
  wrong. This is pre-existing for `Key` (and deliberate there: an opted-out item registers nothing,
  so an unusable registration key costs nothing), but Q8.25 changes the calculus for `Name`: an
  import-only partial under a friendly name is the *primary* use case for the pair, and it is
  exactly the case whose faults are unreportable. The fix is small — derive and report the name for
  every readable item, before the `Precompile` gate — but it is a new diagnostic population on a
  previously silent path, so it is a ruling and not a tidy-up.
  **Ruling (user, 2026-07-26): fix it and add a diagnostic.** Derive and validate key *and* name for
  opted-out items too, reporting the same faults an included item would raise, plus a diagnostic for
  anything with no existing home (claim `HED7029` if needed). The population whose faults are
  currently unreportable is exactly the population the feature is for.

  **Landed (2026-07-26), with Q8.29 — one change, because they are one defect.** The `!template.Precompile`
  `continue` moved to *after* key and name derivation, so an opted-out item now raises exactly the faults an
  included one does: a malformed `Key`, a malformed `Name` and an already-taken `Name` are all `HED7004`
  against the item that is wrong, instead of surfacing as `HED7011` at an innocent importer.

  **`HED7029` was not needed, and the id stays free.** Every fault this exposes already had a home: the three
  above are instances of `HED7004`'s "this item's explicit key metadata is unusable" class, and the advisory is
  `HED7028`. Two candidates were considered and declined. *(1)* "This item declares `Name` but is opted out, so
  the name is a build-time import spelling only and the runtime cannot answer to it" — true under Q8.30, and
  declined because that pairing is the feature's **intended** shape (Q8.28's own words: an import-only partial
  under a friendly name is the primary use case), and a warning on the intended use is noise. *(2)* The
  cross-assembly name/key collision Q8.30 introduces — declined *as a build-time id* because the build tier
  cannot see it: a referenced assembly's manifest rows live in a `GetTemplates` method **body**, which is IL and
  not symbol metadata. That one got a **runtime** id instead, `HED7104`, recorded under Q8.30.

  **What deliberately did not change**, stated so it is a decision and not an omission. An opted-out item's
  *template* diagnostics — a missing import, a parse error — stay unreported. The ruling asks for its metadata
  validated and its imports advised; draining every opted-out file's parse channels would turn previously-green
  builds red over templates the author explicitly excluded from this build, which is a much larger change than
  was ruled. It is a suppression, not an amnesty: as soon as a precompiled template imports the file, the
  importer's own parse pulls the same content through the same channels and raises them. The mechanism is
  `ParseAndReport`'s `advisoryOnly` flag, and the boundary is pinned from both sides — an opted-out file with a
  missing import reports nothing, a precompiled file with the same missing import still reports `HED7011`.

  **The invariant is pinned, not assumed:** `AValidatedOptedOutItemStillContributesNoEntryPointAndNoManifestEntry`
  asserts a clean opted-out item with a working name yields no manifest row under either spelling and no entry
  class, while the importer that *does* precompile still resolves the import. Six new tests were confirmed red
  against the pre-change ordering before the fix.
- **Q8.29 — `HED7028` cannot fire for an import-only *named* template, which is the case it is most
  for.** The advisory is raised from `ParseAndReport`, which only runs for items that reach the
  emit loop; the *importer* is what raises it, so a named `Precompile="false"` partial imported by
  path **is** advised (the importer is precompiled). But a named partial imported by path *from
  another* `Precompile="false"` partial is not, because that importer never parses. Whether the
  advisory should reach imports inside opted-out files is the same question as Q8.28 from the other
  end, and the answer probably has to be the same one.
  **Ruling (user, 2026-07-26): same answer as Q8.28.** `HED7028` must be able to fire for an import
  inside an opted-out file. The two are one defect seen from either end — an opted-out template is
  still a participant in the import graph, so it must be parsed enough to validate and advise even
  though it contributes no entry point and no manifest entry.

  **Landed (2026-07-26) with Q8.28, as one change.** An opted-out item is now parsed through the same
  `ParseAndReport`, in `advisoryOnly` mode: the import reader runs — which is what produces the advisory data,
  so the rule lives in one place rather than being reimplemented for the opt-out — and `HED7028` is reported at
  the importer's own `@<<{{…}}` block. Nothing else is. Pinned by
  `Hed7028FiresForAnImportInsideAnOptedOutFile`, which asserts the diagnostic lands in the *opted-out* file's
  path, so an implementation that advised from somewhere else cannot pass. See Q8.28 for why the
  advisory-only scope is narrow and for the two-sided pin that keeps it scoped to the opt-out.
- **Q8.30 — Nothing pins that a registered `Name` is *not* a runtime registry key.** Q8.25 scopes
  `Name` to `@<<` import resolution on the user's words ("an optional additional name register for
  import to use"). The generator honours that — the manifest carries only keys — and
  `NameDoesNotChangeTheRegistrationKeyOrTheEntryClass` pins the manifest side. What is **not**
  pinned is the runtime: no test asserts that `TemplateOptions`/`PrecompiledTemplates` lookup by a
  registered name *misses*. If a future change registers aliases in the manifest "for symmetry",
  nothing reddens, and the two surfaces would silently disagree about what a name is. A one-line
  negative assertion would close it; whether the runtime *should* answer to names at all is the
  larger question underneath, and is a ruling.
  **Ruling (user, 2026-07-26) — scope EXPANDED: the engine must support name search.** The user's
  words: *"Add an assert for engine behavior to support name search, obvious and should be derived
  from `Name` being a useful key for import, including other answers like adding a diagnostics
  required."*

  This **reverses the import-only scope** the Q8.25 landing assumed. If `Name` is a useful key for
  imports it is a useful key full stop: a template registered under a name should resolve by that name
  at run time as well as at build time, and the asymmetry was an artifact of the wiring rather than a
  designed boundary. So the manifest carries the registered name, the runtime registry/resolver
  consults it, and a lookup by name resolves the same template a lookup by key does. The assertion
  asked for is of **that** behaviour — not, as this question originally proposed, that a runtime name
  lookup misses. Diagnostics this requires (most obviously a name colliding with another template's
  key across the registry) are in scope. Manifest schema change — coordinate the bump with Q8.31.

  **Landed (2026-07-26).** `PrecompiledTemplateInfo` gains `RegisteredName`; the generator emits it; the registry
  answers to it. `Name`'s scope has now moved **three** times and the fixture doc
  (`RegisteredNameLookupTests`) states all three, because each step invalidated the previous step's rules rather
  than extending them: **override** (Q8.12 landing 1) → **additive, import-only** (Q8.25) → **additive, plus
  runtime** (Q8.30). Only a name that actually registered at build time reaches the manifest, so the two tiers
  hold the same spellings for the same templates.

  **Resolution order — the decision, stated rather than left implicit. Keys win.** A lookup string matching one
  template's key and another's registered name resolves to the **key owner**, always, and independently of the
  order the assemblies registered in. Three grounds, in order of weight. *Additivity:* a name is an addition, and
  an addition that displaced a spelling which already resolved is exactly the override Q8.25 corrected —
  re-introducing it at the runtime tier would undo that correction on the surface where it is hardest to see.
  *The match principle:* the build tier's import map has been two passes, keys first, names second, since Q8.25,
  so the runtime uses the same order and the tiers cannot disagree about what a spelling means. *Determinism:*
  one dictionary holding both would make the winner depend on which assembly registered first, which is the
  host's business, not the engine's.

  Mechanically it is **two indexes with a disjointness invariant** — no spelling is ever in both — enforced from
  *both* directions, because either can happen first across assemblies: a name whose spelling a key already owns
  is refused at insert, and a key arriving later **evicts** the name that was shadowing its spelling. A one-sided
  implementation passes exactly one of the two order tests, and which one depends on host load order, so both are
  asserted separately plus a third that pins the property over both orders.

  **The new collision class, and why it does not throw.** A registered name colliding with another *registered*
  template — by key or by name — is `HED7104` / `PrecompiledFallbackReason.RegisteredNameUnavailable`, reported
  once per lost name through `OnFallback`. It is a **runtime** id because the collision spans assemblies and the
  build tier structurally cannot see it: the generator reads referenced assemblies' *symbols*, but a manifest's
  rows live in a `GetTemplates` method **body**, i.e. IL. Within one compilation the same fault is still
  `HED7004`. It does **not** join the duplicate-key throw, and the contrast is asserted in one test so neither can
  drift into the other: two templates claiming one *key* is unresolvable — either could be the one the host meant,
  and picking silently is the illegitimate fallback the taxonomy forbids — whereas a name/key collision is fully
  resolved by the ordering rule above, with no ambiguity about which template renders. Q8.25's principle then
  decides the rest: **a broken addition costs the addition and nothing more**, and throwing would take a whole
  assembly's registration down over an alias.

  **The behaviour assertion the ruling asked for** is `ALookupByRegisteredNameFindsTheSameEntryAsALookupByKey`:
  both spellings resolve, to the *same object* (reference identity — two indexes holding different objects for one
  template would satisfy a weaker check while being the exact drift the manifest field prevents), and the entry
  keeps reporting its **key**, which is the identity the staleness check and every diagnostic are written against.
  It was confirmed red before the implementation, missing at the name lookup. `TryResolve` is asserted separately,
  because a name that resolved only through `TryGet` would be invisible to the resolver and `PrecompiledRuntime`,
  which is every real caller.

  One asymmetry follows from the opt-out and is left standing deliberately: an opted-out item contributes no
  manifest row, so its `Name` is a build-time import spelling only. Q8.28 considered warning about that and
  declined — it is the feature's intended shape, not a fault.

  Schema: **3**, shared with Q8.31 — see the Q8.2 correction above for why the number went *down*.
- **Q8.31 — The `#line` relativity marker is prose in generated code, not a machine-readable
  form.** Q8.27's marking is a comment line. That is exactly what the ruling asked for (a reader
  can tell which form a `#line` is in) and it is what the snapshots pin, but a *tool* — a stack-trace
  symbolizer, an IDE, the LSP — cannot act on a comment. If any consumer ever needs the anchor
  programmatically, the honest carrier is the manifest (which already records per-template
  metadata), not a comment. Recorded so the choice is visible rather than discovered later; no
  consumer needs it today.
  **Ruling (user, 2026-07-26): record the choice in the manifest and remove the comment from generated
  code.** The `#line` path form becomes machine-readable manifest data instead of prose in the
  generated file, and the header comment Q8.27 added is deleted. One schema bump shared with Q8.30,
  which also adds a manifest field.

  **Landed (2026-07-26).** The carrier is `PrecompiledTemplateInfo.LinePathForm`, a three-member
  `PrecompiledLinePathForm` enum: `RootRelative`, `TemplatePath`, and `Unspecified` for a row that makes no claim.
  Q8.27's two prose sentences map onto the first two members exactly, so the *decision* Q8.27 took is untouched —
  only where it is written down changed. The comment under `// <auto-generated/>` is gone from every generated
  file.

  **An enum, not a bool**, and `Unspecified` is the reason: a fallback-marker entry has no generated source and
  therefore no `#line` directives to describe, so it must be able to say nothing rather than be forced to claim
  one of two forms. A bool would have made the marker row assert something false.

  The enum file is **linked into the generator** (`Heddle.Generator.csproj`, beside `PrecompiledCapabilities`)
  rather than having its member names restated as string literals, so a rename breaks the generator's own compile
  instead of emitting a manifest that will not compile in the consumer.

  **Both halves are asserted together** — the form is recorded *and* `#line file names below` is absent from the
  generated source — so an implementation that added the field without deleting the prose cannot pass; two
  carriers for one fact is the state this closes. A third test pins the two forms as genuinely *distinguished*
  (one compilation with a rooted and an out-of-root template records a different value for each), which a constant
  would have satisfied in the single-template tests.

  Cost, as predicted: the eight Verify snapshots and the sample golden move. Deltas reviewed line by line before
  acceptance and they are exactly four kinds — the deleted comment, `schemaVersion: 5` → `3` (the Q8.2 collapse),
  and the two new manifest rows. The sample's rendered `codegen-output.txt` is **unchanged**, which is the
  assertion that the emitted code moved and the emitted output did not.

## Opened by the Q8.28–Q8.31 landing (2026-07-26) — awaiting rulings

These four are new. **Q8.19 is not** — it carries forward from the Q8.1 landing with a ruling
already recorded ("collect all of them, if it is not a major undertaking") and is still
unimplemented; the cost assessment that ruling asked for has not been done. It is listed here only
so the outstanding set is complete in one place.

- **Q8.32 — `RegisteredName` is the only manifest field the gauntlet does not validate.** Every
  other row the manifest carries is re-checked per request at the gate: the options fingerprint,
  extension bindings (now including the prop-layout fingerprint), function bindings, staleness. A
  name is checked **only at registration**, and only for collisions against spellings *this process
  already holds*. Nothing re-establishes at request time that the name still describes the template
  it claims, so a manifest naming a template it does not own — a hand-written manifest, a
  third-party emitter, a stale assembly rebuilt against different sources — resolves by that name
  and renders precompiled output with no fallback event. That is the silent-fallback shape this
  whole program exists to eliminate, reappearing on the one field added last.

  Two sub-questions, because they may not get the same answer. *(a)* Should the gauntlet check
  `RegisteredName` per request at all, given that a name adds no new *rendering* surface — the entry
  it resolves to is the same object a key lookup returns, and that entry's own rows are already
  gauntleted? *(b)* Registration currently `continue`s past a `RegisteredName` that fails
  `TemplateKey.TryNormalize` **with no event at all** — the only wholly silent drop in the
  registration path, since both collision arms report `HED7104`. The generator cannot emit such a
  name (it emits only names that registered at build time), so the population is exactly the
  non-generator manifests, which is also the population sub-question (a) is about.

  **Ruling (user, 2026-07-26) — sub-question (a) is rejected as over-engineering, and the question is
  reframed.** *"How the fuck we can check that? It's just a template pointer."* Correct: a registered
  name resolves to an entry, and that entry's every row is already gauntleted per request, so a
  per-request name check re-validates nothing and buys nothing. **No per-request gauntlet arm for
  `RegisteredName`.** Sub-question (b) stands separately — a wholly silent drop is not acceptable
  regardless, so an unnormalizable `RegisteredName` must report through the same `HED7104` channel the
  two collision arms use.

  **What the ruling asks for instead is a different validation stage, and it generalizes past names.**
  The user's words: *"we can delay function connection (e.g. create a function pointer and then replace
  the pointer at runtime even though build time artifact is the same). If this is even possible at
  dynamic runtime today then let's match it in generator by using fake function pointers + additional
  validation layer that can be done pre-run but after all configured and ready for runtime (assemblies
  loaded and all function pointers resolved and linked as well as extensions and everything else in the
  engine)."*

  So: the generator's difficulty is not that it cannot validate, it is that it validates at the wrong
  *time* — build time, when the host's configuration does not exist yet. The proposal is a **third
  stage** between build and render: generated code binds through indirection rather than to a resolved
  target, and once the host has finished configuring (assemblies loaded, functions registered,
  extensions bound), one validation pass resolves every indirection and reports everything wrong at
  once — before any render, rather than per request. That is where a whole class of currently-awkward
  checks belongs, and it is the *opposite* of adding gauntlet arms: work moves **out** of the
  per-request path.

  **This is subject to a feasibility assessment first, on the Q8.19 pattern, and the ruling explicitly
  conditions on it** (*"if this is even possible at dynamic runtime today"*). Two things to establish
  and report honestly before any code: whether the dynamic runtime already late-binds function
  connection in this way (if it does not, matching it means changing the source of truth, which is a
  much larger change than was ruled), and whether generated code can carry indirection without
  regressing the precompiled tier's reason for existing — a delegate hop per call site would trade the
  tier's whole advantage for validation timing. If it does not come out contained, stop and report the
  cost rather than half-doing it. **Recorded as a distinct piece of work, not folded into a phase.**

  **Landed (2026-07-26) — sub-question (b) only.** An unnormalizable `RegisteredName` in
  `PrecompiledTemplates.Register`'s pass 2 now reports `HED7104` /
  `PrecompiledFallbackReason.RegisteredNameUnavailable` instead of being `continue`d past in silence. The
  registration path has no silent drop left: every discarded manifest row is now either reported or a throw.

  **One reason and one id for both causes, not a second row in the registry.** A refused spelling and a collided
  spelling are one situation from the host's side — a name it expected to resolve does not, and the template is
  still reachable by its key — with the same remedy, so splitting them across two ids would advertise a
  distinction the host cannot act on. The registry row for `HED7104` and the enum member's own documentation are
  widened to say so, rather than a new id being claimed.

  The detail names **both** the refused spelling and the key of the template that asked for it. A report naming
  only the spelling tells a host with fifty templates that something, somewhere, lost a name; that half is pinned
  by its own assertion, because dropping it is a mutation the resolution assertions do not catch.

  **Deliberately not done, so it reads as a decision:**
  - **No per-request gauntlet arm for `RegisteredName`** — sub-question (a), rejected by the ruling.
    `PrecompiledGauntlet` is untouched apart from one call-site rename forced by Q8.33's field split.
  - **No throw.** Q8.25's rule holds: a broken addition costs the addition and nothing more. The template stays
    registered under its key and the rest of the assembly registers normally.
  - **No build-tier change.** The generator already refuses such a name with `HED7004` and so cannot emit one; this
    arm exists for manifests no build tier vetted, and the two tiers still agree.
  - **The late-binding third stage the ruling reframed the question around is not attempted here.** It remains
    conditional on the feasibility assessment the ruling asked for, unstarted, and is not folded into this landing.

  One residue, recorded because mutation testing found it rather than assumed it away: also *indexing* the refused
  spelling would pass every test, because `TryGet` normalizes before consulting the name index, so a spelling
  outside `TryNormalize`'s range is unreachable by any lookup and the arbitration arms only ever compare normalized
  strings. The state is unreachable, so the mutant is extensionally equal to the code; the argument is written into
  the fixture next to the test rather than being left for the next reader to re-derive.

  **Feasibility assessment (2026-07-26).** Both conditions the ruling set are answered, and they point the
  same way: **the timing goal is achievable and worth doing; the indirection mechanism is not.**
  Recommendation — **proceed with a named subset (validation only); do not proceed with indirection.**
  Evidence in full in the scratchpad report; the load-bearing parts:

  - **The dynamic runtime does not late-bind — it is *deferred-resolution*, and the resolution point is
    sealed shut.** `NativeExpressionCompiler` takes `options.Functions` and calls `_registry.Freeze()` in
    its constructor, so every later `Register` throws ("*…is now frozen. Register functions before the
    first compile*"). The resolved target is then baked into the expression tree: `Expression.Call(null,
    chosen.Method, …)` for methods and — decisive — `Expression.Invoke(Expression.Constant(chosen.Target,
    …))` for delegate registrations, i.e. **the delegate instance itself is a constant, not a cell**.
    Extensions resolve the same way: `HeddleCompiler` instantiates via `TemplateFactory` at compile and
    stores the instance on the compiled item, which the resolver caches. So *neither* tier carries an
    indirection; they differ only in which phase performs the one lookup (dynamic: compile; generated:
    build). Adding a re-pointable cell to the generator would therefore not be matching the source of
    truth, it would be inventing a third binding mode and then changing the source of truth to match
    *it* — the outcome the ruling named as disqualifying.
  - **Generated code cannot carry indirection cheaply, for three independent reasons.** Prop values are
    emitted as `PrecompiledPropSetter(<slot.Index>, …)`, so a late-resolved extension type writes into
    the wrong slots — which is exactly what phase 3's OQ4 prop-layout fingerprint exists to prevent, so
    name-based late binding would delete the guarantee the fingerprint was created to provide. Function
    overload choice *and* argument casts are baked into the emitted C# text, so a cell can only be typed
    as the build-time signature and buys exactly one bit — "is a compatible target present?" — which
    `CheckFunctions` already answers from manifest rows. And `PrecompiledRuntime.Bind`'s pinned invariant
    is *"the extension is never mutated after `Bind` returns"*, which is what makes the `static readonly`
    field and the lock-free render correct; a stage-3 re-point is that mutation. Cost, stated honestly as
    a shape argument and **not** a measurement: the tier has never been benchmarked here
    (`src/Heddle.Performance` has no precompiled benchmark, and no published report in
    `docs/benchmarks/` carries a precompiled row — the `Heddle` anchor rows are the dynamic tier), so no
    number supports or refutes "trades the tier's whole advantage". What is verifiable from code is that
    `E0.RenderData(...)` reads a `static readonly` field of an exact type — the only signal making that
    call devirtualizable, since `AbstractExtension.RenderData` is `abstract` and no extension in the
    chain is `sealed` — and that dropping `readonly` to permit a re-point converts every substitution
    site in every template to a virtual call. The indirection with the worst cost (extensions, the
    per-substitution hot path) is the one with the least validation value.
  - **The ruling's actual goal needs no indirection.** Gauntlet steps 2 and 3 are pure comparisons of
    manifest facts against live registries, so they can run once after configuration over
    `PrecompiledTemplates.Entries`. Two findings make this a bigger win than the mechanism it replaces.
    First, **the typed entry point runs no gauntlet at all**: `Templates_X.Generate(model)` goes straight
    to `PrecompiledRuntime.GenerateString`, and the gauntlet is reached *only* through
    `PrecompiledTemplates.TryResolve`, whose only production callers are `TemplateResolver.Search` and
    `ConsultPrecompiled`. For a host on the documented recommended API an extension- or function-binding
    mismatch is **never detected today**, so the third stage is not a re-timing — it is the only check
    such a host can ever have. Second, it genuinely removes per-request work: a precompiled hit is not
    entered into `TemplatesCache`, so `Validate` runs on every request, allocating a `List`, a `HashSet`
    and several LINQ pipelines in `CheckFunctions` and hashing every content/import file under
    `EnableFileChangeCheck`.
  - **The contained subset.** *(A)* An aggregate, host-callable post-configuration pass that runs the
    existing gauntlet over every registered entry and reports **all** failures at once rather than the
    first. `Entries` and `Validate(entry, options)` are already public, so this is an aggregate API, a
    report shape and documentation — **no generated-code change, no manifest row, no schema bump**, no
    `PrecompiledRuntime` invariant touched, no `FunctionRegistry` change. It should share its
    collect-don't-stop report type with Q8.19. *(B)* Sub-question (b) above — `HED7104` for an
    unnormalizable `RegisteredName` — is independent of all of this and stays as ruled.
  - **Named and deliberately not started:** the `HED7014` **delegate-only** case is the one place
    indirection would strictly *add* coverage rather than trade it, since today a single delegate-only
    call makes the whole template fall back. It is not contained: a closure's signature is not in
    metadata, so the emitter has no return type to keep typing the expression with, and an
    `object`-typed unknown-return node cascades through operator legality, the coercion rail and member
    hops. A bounded slice exists (delegate-only calls in output-only position) but still needs an emitter
    change, a manifest row and a per-feature schema gate. This is the "stop and report the cost" branch.
  - **One question the subset raises rather than settles:** filed as **Q8.38**.

- **Q8.33 — `PrecompiledFallbackEvent.Key` carries two different kinds of string and a host cannot
  tell which it has.** For per-request reasons `Key` is a template key; for the registration-time
  reasons (`SchemaVersionUnsupported`, `EngineVersionIncompatible`, and now
  `RegisteredNameUnavailable`) it is an **assembly name**. This is pre-existing 2.0 convention and
  the landing kept it rather than widening it — deliberately, and the docs now state it — but
  `HED7104` is the first reason that made the overload load-bearing for something a host might want
  to *act* on: "which template stopped resolving under which spelling" is recoverable only by
  parsing `Detail`, which is a pinned human-readable format string, not an API. Options: leave it
  (documented ambiguity, zero break); add a discriminator property (`KeyKind`, additive, no break);
  or add the contested spelling and its owner as their own properties. Nothing forces this today —
  no shipped host branches on it — so it is recorded as a design question, not a defect.

  **Ruling (user, 2026-07-26): split it. One field must not carry two kinds of key.** *"Why don't we
  have two separate collections for two separate keys?"* The "documented ambiguity, zero break" option
  is rejected: documenting an overload does not make a host able to branch on it, and `Detail` is a
  human-readable format string, not an API. The event gets **separate carriers for separate things** —
  a template key where there is one, an assembly name where the event is about an assembly — so a host
  reads the field it means instead of inferring which meaning it got from the `Reason`.

  Notes for the implementer, since the ruling settles the shape and not the compatibility mechanics.
  `PrecompiledFallbackEvent` is **shipped 2.0 public API** and `Key` is a public property; removing it
  is a binary break and needs a breaking-window disposition, whereas adding a second property and
  narrowing `Key`'s *meaning* is a behavioural break with no compile-time signal — the worse of the
  two, because a 2.0 host reading `Key` for an assembly name would silently start reading null. Decide
  it as a 2.1 break either way, record it, and pin the reason→populated-carrier mapping from both
  sides so no reason can be added later that populates neither.

  **Landed (2026-07-26).** `Key` is **removed** and replaced by two properties — `TemplateKey` and `AssemblyName` —
  of which exactly one is populated. The public constructor is replaced by two factories,
  `PrecompiledFallbackEvent.ForTemplate` and `ForAssembly`, so the carrier is chosen by the call rather than by a
  positional string whose meaning the reader has to look up.

  **Removal, not narrowing, and the reasoning is the ruling's own.** Both options break a 2.0 host; only one of them
  tells it. Narrowing `Key` to template keys leaves a host that logged the rejected assembly silently logging null,
  on the channel whose entire purpose is that failures are not silent. Removal is a compiler error at the one line
  that has to change, with a mechanical fix. Dispositioned as a declared 2.1 break in
  `docs/spec/common/breaking-windows.md`, on the same footing as Q8.2's schema-floor rise, with the population
  argument stated: the only way to see this type is to consume the precompiled tier, and Q8.2 already requires that
  population to rebuild for 2.1.

  **The mapping is pinned from both sides, which was the explicit ask.** Code side: each factory refuses a reason
  belonging to the other carrier, both refuse a blank carrier, and the classifier behind them is an **exhaustive
  switch that throws on `default`** — so a reason added later cannot be raised at all until it has been assigned a
  carrier. Declaration side: `PrecompiledFallbackCarrierTests` declares the assembly-scoped set and checks it
  against `Enum.GetValues` in both directions, so a new reason with no declaration and a declaration the code
  disagrees with are each red. Neither side can be made green by editing only the other.

  A reflection test also pins the **absence** of a `Key` member and of any public constructor. That is the mutant
  worth guarding: re-adding `Key => TemplateKey ?? AssemblyName` as a convenience would restore exactly the
  ambiguity this closes, and it compiles.

  **Deliberately not done:**
  - **No `KeyKind` discriminator.** The ruling asked for separate carriers, and once they exist the populated one
    *is* the discriminator; a third property would be a second way to ask the same question.
  - **No change to which reasons fire, when, with what `Detail`, or to what `Strict` throws.**
    `PrecompiledMismatchException` never carried the union field and is untouched.
  - **`default(PrecompiledFallbackEvent)` still bypasses both factories**, as it does for every value type. Stated
    in the type's own documentation rather than defended against — nothing in the engine produces one, and making
    the struct unconstructible-by-default is not available in C#.

  Cost: the public-surface golden moves by exactly four lines and nothing else (reviewed line by line), and the
  generator suite's `FallbackGuard` collapses the two carriers for matching and display — a test-side choice, noted
  in its own remarks so it is not mistaken for the engine blurring them again.

- **Q8.34 — A name that resolved can stop resolving because an unrelated assembly loaded, and the
  only notice is `OnFallback`.** The eviction half of the disjointness invariant is what makes key
  precedence order-independent, so it is not in question. What is in question is its *observability*:
  a host that registered assembly A, resolved `"Banner"` successfully, then loaded assembly B whose
  template key is `"Banner"`, silently gets a different template from the same lookup string
  afterwards. `HED7104` is reported, but only through `OnFallback`, which is an optional callback
  most hosts never set — and `Strict` policy does not apply, because this is not a fallback to the
  dynamic tier: both spellings still resolve precompiled, just to different templates than before.
  So the loudest available signal is one a default host does not hear. Whether that is acceptable
  turns on whether the situation is a *host configuration error* (two assemblies disagreeing about a
  spelling, arguably worth throwing or at least honouring `Strict`) or a *legitimate late-binding
  outcome* (the ordering rule working as designed, worth only a callback).

  **Ruling (user, 2026-07-26): this is an integration-layer question and the engine must not answer it.**
  The reasoning, in the user's words: *"engine should not suggest a solution to that (e.g. Razor mistake
  and how we ended up with ASP.NET dependency). We are not responsible of how each assembly is
  configured and when configured."* The framing in the question — configuration error *versus* legitimate
  late binding — is the wrong dichotomy, because deciding which it is means deciding for the host when
  and in what order assemblies may register. That is the coupling that turns a library into a framework.

  Three concrete consequences, all of them bounded:
  - **Registration may report the potential conflict** — a warning diagnostic, or an exception where the
    situation is genuinely unresolvable — because reporting is the engine's business.
  - **The engine must not load assemblies by default.** Discovery-by-default would take the ordering
    decision away from the integration layer, which is exactly what the ruling forbids. If anything
    currently auto-loads or auto-scans on the precompiled path, that is a defect to find and record,
    not a feature to keep. **Verify this rather than assume it.**
  - **The engine may suggest an architectural pattern**, in documentation, for a host that wants
    ordering guarantees — and nothing more than suggest. Where that lands is a phase-8 item.

  So `HED7104`-through-`OnFallback` is the right ceiling, not a gap: `Strict` deliberately does not
  extend here, because `Strict`'s subject is degradation to the dynamic tier and no degradation occurs.
  **No behavioural change to the eviction path.**

  **Verification (2026-07-26) — the ruling's second consequence does not hold today: the engine *does*
  auto-load, by default, and it is stronger than the shape the question anticipated.** Not an enumeration
  of assemblies the host already loaded — `AssemblyHelper`'s **static constructor** forces
  `Assembly.Load` over the entry assembly's whole transitive `GetReferencedAssemblies()` closure (loader
  failures swallowed) *and* over every `DependencyContext.GetDefaultAssemblyNames()` entry. There is no
  switch: `AssemblyHelper.Configure(startupAssembly)` is not an opt-in gate but a one-shot
  (`if (!_configured)`) that runs *after* the static ctor has already walked everything.
  `TemplateFactory`'s static constructor then scans every assembly in that set for
  `[ExportExtensions]`. It reaches the precompiled path through gauntlet step 2 —
  `PrecompiledTemplates.TryResolve` → `PrecompiledGauntlet.CheckExtensions` →
  `TemplateFactory.TryGetExtensionType` → both static ctors — so any precompiled template carrying at
  least one `ExtensionBindings` row triggers it, which is effectively all of them (the sample manifest
  carries `("html", "Heddle.Extensions.EmptyHtmlExtension, Heddle")`). This is the forbidden shape
  precisely because the scanned set decides **extension name ownership**: `AddExtensions` resolves
  collisions by walk order and raises `TemplateOverrideException` *from a static constructor* when an
  unrelated claimant collides, so an assembly the integration layer never chose to load can win a name or
  fail type initialization. **Recorded as a defect, not fixed — filed as Q8.37**, because the code is not
  on the precompiled path proper (it is the shared discovery the dynamic tier also depends on) and
  removing it has a back-compat surface, so it needs a ruling rather than an audit edit.

  What verification found **clean**, so the defect is precisely scoped: `PrecompiledTemplates.Register`
  is explicitly opt-in, takes one `Assembly`, and has no scan-all overload, no `GetAssemblies` call and no
  reflection-based manifest discovery — the samples and the cross-stack benchmark harness all pass an
  explicit assembly (`PrecompiledTemplates.Register(typeof(Program).Assembly)`); there is **no module
  initializer anywhere** in `src/` and none in generated code; the **LSP server never calls `Register`**
  and its own loading is workspace-configuration-driven through a collectible `ModelAssemblyContext` plus
  the tracked `RegisterModelAssemblies`/`Unregister` pair; and the **typed entry point does not trigger
  the walk at all** (`PrecompiledRuntime.GenerateString` touches neither `TemplateFactory` nor
  `AssemblyHelper`, because generated code names its extension types directly). So the auto-load is
  reached by the resolver/gauntlet path only. Note also that this defect and the README's
  post-implementation **finding 3** (`[ExportExtensions]` unmodelled by the generator) are two halves of
  one seam — the runtime discovers from a set the host did not choose, the generator scans a different set
  — and whichever ruling closes Q8.37 must be checked against finding 3 so the tiers agree on which set
  is authoritative.

- **Q8.35 — `Min == Max == Current` makes the support window a single point.** The Q8.2 collapse was
  right — schemas 3, 4 and 5 were never released, so there was no window to preserve. But the
  constants now say something stronger than "we collapsed unreleased churn": they say this engine
  reads exactly one schema. Every future manifest change is therefore a **whole-assembly rejection**
  for every assembly built against the previous version, until rebuilt — correct behaviour when a
  change is genuinely binary-breaking (which schema 3's was, per Q8.2's demonstrated
  `MissingMethodException`), and unnecessarily severe when it is purely additive. A new optional
  manifest field, read through a `PrecompiledSchema.<Feature>SchemaVersion` gate of the kind
  `RegisteredNameSchemaVersion` already establishes, needs no rejection at all: an older manifest
  simply carries no value for it. The question is whether the floor should trail `Current` whenever
  the delta is additive, and if so what test proves a candidate change *is* additive rather than
  asserting it. Wanted before 2.2, not before 2.1 — 2.1 ships one schema and one window either way.

  **Ruling (user, 2026-07-26): the schema number tracks breakage, not releases.** *"Let's keep the same
  manifest version if the change is non-breaking but let's keep version increment to 2.1 as already
  started."* Two separate versions, decoupled deliberately:
  - **Manifest schema** — bumped only when a change is genuinely breaking, i.e. when an
    already-emitted manifest's IL can no longer bind or can no longer be read correctly. A purely
    additive change reads through a per-feature `PrecompiledSchema.<Feature>SchemaVersion` gate (the
    pattern `RegisteredNameSchemaVersion` already establishes) and the number **stays put**. This is
    the same correction the Q8.2 collapse made, promoted from a one-off cleanup to the standing rule:
    schemas 3, 4 and 5 existed because internal churn was being numbered.
  - **Assembly/package version** — 2.1 stands as already started, independent of the above. A release
    with no schema change is normal, not a contradiction.

  What this does *not* license: relaxing `Min` retroactively for the schema-3 break, which stays a
  real break by the Q8.2 ruling and its demonstrated `MissingMethodException`. The rule applies to
  changes from here on. And the additivity claim must be **proved per change, not asserted** — the test
  that a change is additive is that a manifest emitted before it still reads correctly afterwards,
  which means a fixture holding a pre-change manifest, not a reviewer's judgement. Fold into
  `docs/spec/common/breaking-windows.md` as a named rule so future landings apply it without
  re-deriving it.

  **Landed (2026-07-26).** The rule is `breaking-windows.md` **policy item 7**, placed with the other six
  rather than in the not-window-gated section, because it governs how every future window is *composed* —
  it is not a one-off disposition. It states both halves: the number bumps only when an already-emitted
  manifest can no longer be read or bound, and an additive change is read through a per-feature
  `<Feature>SchemaVersion` gate instead.

  The proof obligation is written as the operative part, since that is what makes the rule enforceable
  rather than aspirational: a fixture holding a **pre-change** manifest that still reads correctly
  afterwards, with "the reviewer judged it additive" explicitly rejected as evidence — the failure mode is
  a constructor signature that no longer binds, which is invisible in source and appears only in emitted
  IL, exactly as the Q8.2 fixture had to demonstrate. Where that fixture cannot be produced, the change is
  breaking by default and the number bumps.

  **Deliberately not done:** no code change. `Min = Max = Current = 3` stays as the Q8.2 collapse left it
  — the rule governs changes from here on and does not retroactively relax `Min` for the schema-3 break,
  which remains real on its demonstrated `MissingMethodException`. No test was added either: the
  obligation attaches to a future change, so a test today would have no pre-change manifest to hold.

## Opened by the Q8.32/Q8.34 assessment pass (2026-07-26) — awaiting rulings

- **Q8.37 — The engine auto-loads and auto-scans assemblies by default, which Q8.34's ruling forbids
  (defect).** Found by the verification Q8.34's ruling demanded; full chain recorded in that entry.
  `AssemblyHelper`'s static constructor `Assembly.Load`s the entry assembly's entire transitive reference
  closure plus every `DependencyContext` default assembly name, unconditionally and with loader failures
  swallowed; `TemplateFactory`'s static constructor scans that set for `[ExportExtensions]`; the
  precompiled path reaches both through gauntlet step 2's `TemplateFactory.TryGetExtensionType`. Because
  the scanned set decides extension **name ownership**, and a collision between unrelated claimants throws
  `TemplateOverrideException` out of a static constructor, an assembly the integration layer never chose
  to load can take a name or fail type initialization — the ordering decision the ruling says must stay
  with the host.

  Not fixed in the assessment pass, deliberately, because the code is **shared engine discovery** rather
  than a precompiled-path detail: the dynamic tier depends on the same scan, and hosts today rely on
  `[ExportExtensions]` being found without registering anything, so removing or gating it is a
  behavioural break needing a window disposition — not an audit edit. The question is therefore *what
  replaces it*: an explicit registration call the host makes (matching `PrecompiledTemplates.Register`'s
  already-clean opt-in shape, which is the in-repo precedent), an opt-in switch that preserves today's
  behaviour for existing hosts, or narrowing the walk without removing it. Whatever is chosen must be
  reconciled with the README's post-implementation **finding 3** — the generator scans *all referenced*
  assemblies while the runtime scans only `[ExportExtensions]`-carrying ones — since the two are halves of
  one seam and a fix to either alone widens the drift.

  **Ruling (user, 2026-07-26): remove the auto-loading in 2.1 — the current breaking window — and add
  proper extension points where they are missing.** *"Remove auto-loading in 2.1 (current breaking window)
  and add proper (if missing) extensions."* So this is not gated behind a switch and not narrowed: the
  static-constructor walk and the scan-all discovery go, and what replaces them is explicit host
  registration on the shape `PrecompiledTemplates.Register` already has. Where a host can only reach
  today's behaviour through the scan — i.e. where removing it leaves a real need with no API — that is a
  **missing extension point to add**, not a reason to keep the walk.

  Two obligations that come with it, not optional. The break needs a `breaking-windows.md` disposition
  stating what stops working for a host that relied on discovery and what it must call instead — the
  population is every host that declares `[ExportExtensions]` and registers nothing. And it must be
  reconciled with README **finding 3** in the same change, because the generator scanning *all referenced*
  assemblies while the runtime scans only `[ExportExtensions]`-carrying ones is the other half of this seam;
  fixing one side alone widens the drift, which the entry above already states.

- **Q8.38 — A post-configuration validation pass has to be told which `TemplateOptions` it is validating
  against, and nothing says which.** Raised by Q8.32's feasibility assessment, which recommends the
  validation-only subset. Four of the gauntlet's inputs are **per-request**, not per-configuration:
  `OutputProfile`, `ExpressionMode` and `TrimDirectiveLines` (step 1's fingerprint comparison) and
  `options.Functions` (step 3). A single pass run "after the host has finished configuring" can therefore
  only be complete with respect to one options shape, and a host that renders under more than one — two
  profiles, or a request-scoped `FunctionRegistry` — gets an answer that is true for one shape and silent
  about the others. Options: the pass takes one `TemplateOptions` and its report says so explicitly; it
  takes a set and reports per (entry, options) pair; or the engine defines a host-declared canonical
  options instance and validates only that. The third is the smallest API and the most likely to mislead,
  so this wants a ruling before the subset is built rather than a default chosen during implementation.
  Note the constraint is not incidental: step 1 exists *because* the fingerprint is a per-request check,
  so it cannot simply be hoisted with steps 2 and 3.

  **Ruling (user, 2026-07-26) — closed as not a question.** *"I don't think you can coherently raise
  questions. It's just a prose about a problem."* Correct, and the fault is in the entry, not the subject:
  it describes a constraint at length and then offers three shapes without asking anything a ruling could
  answer. The constraint itself is real and stays recorded above — a per-request fingerprint cannot be
  hoisted into a per-configuration pass — but it is an **implementation fact for whoever builds the
  subset**, to be resolved by the code and pinned by a test, not escalated. If building it turns up a
  decision that genuinely needs the user (an API shape, a default that could mislead), that gets raised
  then, as a question with an actual question in it.
