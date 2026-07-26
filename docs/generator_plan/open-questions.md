# Generator code-sharing plan — open-questions register

The consolidated Q&A register for the seven phases. Numbering is `Q<phase>.<n>`, matching each
phase plan's own Open-questions section.

**Pre-authoring questions (Q0.1–Q6.3): all resolved (user, 2026-07-25) and folded into the phases.**
**Post-implementation questions (Q7.1–Q8.12):** opened after the phases landed, by the two
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
  Windows will be checked separately. **Default if unruled:** treat drift #9 as unclosed until that
  run happens.

## Opened by the phase-8 docs sweep (2026-07-26)

Recorded when [phase 8](phase-8-docs-sweep.md) was authored. None blocks its stages 0–3.

- **Q8.9 — Where does the narrowed authority convention live, and is it retroactive?** Phase 8 D3
  narrows the convention (*"expression semantics defer first to `docs/native-expressions.md`"*) so
  that a normative document outranks the implementations **only for claims that carry a verification
  marker or are covered by a gate**; an unmarked, ungated claim becomes evidence of intent, not an
  authority. The placement question is whether that lives in
  [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md) (outliving this program) or
  stays in the program README. The sharper half is retroactivity: several landed phase D-items
  resolved drift *by citing* that document, and narrowing the convention makes those citations weaker
  evidence than they were when ratified. **Default if unruled:** land it as a new cross-cutting
  decision with a ledger entry, README bullet becomes a pointer, and treat it as
  **non-retroactive** — already-ratified D-items stand, and the narrowing governs future alignments
  only. Re-auditing seven phases' evidence chains costs far more than the anchor defect warrants, and
  each of those D-items also carries independent source evidence.
- **Q8.10 — If phase 7 has not landed when phase 8's stages 0–4 are done, does D9 ship, slip, or
  transcribe?** D9 makes qualifying doc examples executable by **single-sourcing** them from phase 7's
  shared corpus and including them into the page, so the doc and the test read the same bytes. That
  needs phase 7 stage 0 to exist. **Default if unruled: slip** — D9's work item moves to a phase-7
  follow-on and phase 8 closes with the omission recorded as not-delivered (the posture phase 2's WI9
  and phase 6's D5 established). Hand-transcription is explicitly **not** the fallback: it is the
  defect D9 exists to remove (`ScopeChannelDocExampleTests` is the in-tree example, comment and all),
  and shipping it would leave a second copy for phase 7 to clean up.
- **Q8.11 — Should the nine `<Version>` elements be centralised as part of the 2.1 bump?** Four of
  the nine sit on non-shipping projects, all nine are overridden by CI from the git tag
  (`.github/workflows/dotnet.yml`), and `Directory.Build.props` excludes `Version` *by an explicit
  comment* — so the nine are hand-maintained documentation of the release line with no lockstep test,
  the duplication class phase 6 spent its WI7 deleting from source. The wider surface matters too:
  13 files must change for 2.0.0→2.1.0 and five more are coupled, the riskiest being
  `editors/vscode/src/extension.ts`'s `PINNED_VERSION`, which pins a NuGet tool version outside any
  consistency check. **Default if unruled: yes** — hoist one `<VersionPrefix>`, delete the four
  non-shipping elements, and point phase 8's version-consistency gate at the single property. It is a
  build change, so it lands inside **Q8.2's** work item (which owns the 2.1 declaration and must ship
  it atomically with `MinSupportedSchemaVersion = 4`), not inside the docs sweep; if that work item
  declines it, the gate simply asserts the nine agree.
- **Q8.12 — Who fixes the sample that still passes the removed `Name` item metadata?**
  `samples/codegen-t4-successor/CodegenT4Successor.csproj` carries
  `<HeddleTemplate Include="templates\report.heddle" Name="BuildReport" />`, and
  `Heddle.Generator.props` no longer reads it (Q5.1 removed it), so the sample silently registers
  under its filename key rather than its intended name — and the sample is golden-checked, so the
  golden currently encodes the wrong outcome. It is a live user-facing artifact, not prose, so phase 8
  records it rather than editing it (its D2 rule: the sweep corrects documents, never code).
  **Default if unruled:** fold into the post-audit work-item queue beside Q8.1/Q8.5 — a one-line
  csproj fix plus a golden re-ratification. The only real risk here is forgetting it, which is what
  this entry prevents.
