# Generator code-sharing plan — open-questions register

The consolidated Q&A register for the seven phases. Numbering is `Q<phase>.<n>`, matching each
phase plan's own Open-questions section.

**Pre-authoring questions (Q0.1–Q6.3): all resolved (user, 2026-07-25) and folded into the phases.**
**Post-implementation questions (Q7.1–Q8.27):** opened after the phases landed, by the two
post-implementation reviews, the six phase audits, and the phase-8 docs sweep authored from
the Q8.7 ruling — see
[the section below](#post-implementation-questions-opened-2026-07-26). **Q7.4 and Q8.1–Q8.5 are
ruled (user, 2026-07-26); the remainder stand at their stated defaults**, which are the operative
decision until revisited.

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
  manifest against a **reference facade** carrying the pre-schema-4 surface under the real assembly's
  identity (name, version, public key), with the real `Heddle` excluded from the reference set — so the
  emitted IL genuinely names `.ctor(string, string)` and cannot bind to the current three-parameter form.
  Three assertions: the 2-arg constructor is absent from metadata; the fixture declaring schema 3 is
  rejected cleanly (`SchemaVersionUnsupported`/`HED7102`, no throw, nothing registered); and the *same
  bytes* declaring schema `Min` are admitted and fault with `MissingMethodException` — the control arm that
  makes "the gate prevents a startup crash" evidence rather than narration, and that shows 4 is exactly
  where the boundary belongs. Mutating the facade to declare the optional third parameter — reproducing the
  substitution the ruling forbade — reddens the suite.
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
- **Q8.10 — If phase 7 has not landed when phase 8's stages 0–4 are done, does D9 ship, slip, or
  transcribe?** D9 makes qualifying doc examples executable by **single-sourcing** them from phase 7's
  shared corpus and including them into the page, so the doc and the test read the same bytes. That
  needs phase 7 stage 0 to exist. **Ruling (user, 2026-07-26): keep the docs as refined, separate prose — do not single-source them from the corpus.** Documentation has a *different job* from a test fixture: it explains, and byte-identity with a corpus entry is not a property worth buying. So phase 8's D9 is **rejected as designed**: no `@include:` from corpus templates, no corpus intent rows added for doc examples, and phase 8 no longer blocks on phase 7. Doc examples stay hand-written and are kept honest by review, not by transcription-equality. (The `ScopeChannelDocExampleTests` anti-pattern is still an anti-pattern — the answer is to delete the false coupling, not to formalise it.)
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

  **Implemented (2026-07-26).** `Name` is a **second spelling of `Key`** — one setting, so it shares every
  downstream rule instead of acquiring parallel ones: the same `TemplateKey` normalisation, the same `HED7002`
  and `HED7003` participation, and the same `HED7018` suppression, now stated as a deliberate decision rather
  than inherited by accident (that warning's premise is that the flattened key was *not* asked for, and an
  explicit key asks for exactly the key it names). **`HED7028` was not claimed.** The fault class `HED7004`
  already names is "this item's explicit key metadata is unusable", and both new faults are instances of it —
  a value the normalizer refuses, and two spellings of one setting naming two different keys — at the same
  severity, the same position, with the same remediation and one call site. Its message was generalised to
  carry the offending metadatum and the reason, so a further spelling or reason needs no further descriptor.
  `HED7028` remains unclaimed and free.

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

  **The sample's golden changed, by exactly one line.** `Name="BuildReport"` is now correct, so the key is
  `BuildReport.heddle`, the entry class is `Heddle.Generated.BuildReport`, and `Program.cs` plus the README
  call it. A second change was needed to keep that golden honest: the emitted `#line` directives named the
  *key*, indistinguishable from the file path only while every key is path-derived — with `Name` set they
  pointed at `BuildReport.heddle`, a path that exists nowhere. The `#line` file is now the template's
  root-relative path, byte-identical wherever no explicit key is set, which is why no snapshot moved. Import-map
  fallout registered as Q8.25; the `#line` path form as Q8.27.

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
  `HED7014` chose *warning + degrade* for it. **Default if unruled:** keep the error as ruled and
  keep `Precompile="false"` as the escape hatch; the scenario needs a host that both registers extra
  overloads of a name the generator already knows *and* writes a template call that only those extra
  overloads satisfy, which no first-party or sample code does. Candidate refinements, in increasing
  cost: (a) restrict the error to `Ambiguous` and let `None` degrade with a warning, since `None` is
  the arm a *single* added overload most easily rescues; (b) suppress the error for any name the
  compilation cannot prove closed, which in practice means every name — i.e. withdraw the fix; (c) give
  the host a build-time declaration of its run-time registrations, which is a new surface. *Related:*
  Q8.1, and `HED7014`'s legitimacy argument in
  [precompilation.md](../precompilation.md#which-fallbacks-are-legitimate).

- **Q8.19 — Two illegal calls in one template report once. Should the emitter continue past an
  unwritable construct to collect the rest?** `BuildBody` abandons at the first construct it cannot
  write, so `@(min(1, 2u)) @(max(1, 2u))` yields one `HED7025`, not two; the author fixes one, rebuilds,
  and meets the next. This is pre-existing emitter shape rather than anything Q8.1 introduced (it is
  equally true of `HED7008`), but Q8.1 is the first error where the one-at-a-time surfacing is the
  *whole* user experience, so it is now worth asking. **Default if unruled:** leave it — collecting
  the remainder means continuing a body build whose result is discarded, and the dynamic tier reports
  one error at a time too (`Fail` returns null and unwinds), so today's behaviour *matches*. Pinned as
  a fact, not a bug, by
  `AmbiguousOverloadDiagnosticTests.TwoCallSitesInOneTemplateReportOnceBecauseTheBodyBuildAbandonsAtTheFirst`,
  which reddens if a later change makes the emitter continue.

## Opened by the Q8.2 / Q8.11 / Q8.12 landing (2026-07-26)

- **Q8.24 — A 1.x manifest's rejection reason changed from `EngineVersionIncompatible` to
  `SchemaVersionUnsupported`.** The 2.0 window's as-shipped record and the CHANGELOG both state that
  1.x precompiled assemblies fall back because "the engine-version gate rejects 1.x manifests". With
  `MinSupportedSchemaVersion = 4` that is no longer the gate that fires: `Register` runs the **schema**
  check first, and every 1.x manifest declares schema 1, so it is now rejected as
  `SchemaVersionUnsupported` (`HED7102`) before the engine-version check is reached. The observable
  outcome is identical — one callback, whole-assembly fallback, `Strict` throws — but a host that
  branches on `PrecompiledFallbackEvent.Reason` (a public enum) sees a different member, and two shipped
  documents name the wrong one. **Default if unruled:** treat it as a documentation correction owned by
  phase 8's sweep, not a behaviour change: both reasons are in the same "must surface / packaging defect"
  row of the fallback taxonomy, and the ordering of two gates that both reject the same input is not a
  contract. Worth a ruling because the alternative — checking engine version first so the *older* and
  more specific diagnosis wins — is defensible and cheap.

- **Q8.25 — An explicit `Key`/`Name` makes a template unimportable by its path.** The `@<<` import map
  is keyed by the same derived key as the registry, so `@<<{{ templates/report.heddle }}` no longer
  resolves once that item carries `Name="BuildReport"` — the importer draws `HED7011`, and the fix is to
  import the *key*. This is pre-existing for `Key` and was simply unreachable while the metadata was
  inert (Q8.12), so wiring the feature made it reachable for the first time. **Default if unruled:**
  leave it and document the import path as key-relative, since the runtime's own `ImportReader` is
  key-based and a second, path-based lookup would be a second rule. Worth a ruling because the intuitive
  reading of `@<<{{ some/path }}` is a path, and the failure is a build error rather than a fallback.

- **Q8.26 — Warning regressions are not gated.** `Q8.11` removed the eight `CS8002` warnings and the
  build has no `TreatWarningsAsErrors`, so nothing prevents them — or any other warning class — from
  coming back. The signing half is now held by
  `VersionConsistencyTests.EveryFirstPartyProjectUnderSrcIsStrongNamed`, but that gates the *cause* for
  one warning, not warnings in general; the tree currently carries fourteen xUnit-analyzer warnings and
  four `NU1510`s that no gate mentions. **Default if unruled:** leave it. `TreatWarningsAsErrors` on a
  tree with eighteen live warnings is a landing of its own, and the two candidate mechanisms
  (`TreatWarningsAsErrors` plus a `NoWarn` allow-list, or a CI step asserting a warning census) differ in
  who pays: the first reds local builds, the second only CI. Recorded so "the warnings stopped" is known
  to be a state rather than a property.

- **Q8.27 — Should `#line` name a path the compiler can open, rather than a repo-relative one?** Q8.12
  separated the `#line` file from the registration key, and the value it now emits is the template's path
  relative to `HeddleTemplateRoot` — resolvable when the compiler's working directory is the project
  directory, which is the normal case, and not otherwise. The alternative is the absolute
  `AdditionalText.Path`, which is what a `#line` is really for, but it would put machine-specific
  absolute paths into eight `Verify` snapshots and every generated-source golden. **Default if unruled:**
  keep the relative path; it is what shipped, it is golden-stable, and it is correct for the working
  directory MSBuild actually uses. Revisit if a debugger or error-list mis-resolution is ever observed.
