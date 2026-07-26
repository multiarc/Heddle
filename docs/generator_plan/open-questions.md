# Generator code-sharing plan — open-questions register

The consolidated Q&A register for the program. Numbering is `Q<phase>.<n>`, matching each phase
plan's own Open-questions section.

**Every entry states one final answer.** Each records the *question* (enough that a reader knows
what was asked and why it mattered), the *resolution* (the final answer only — superseded
intermediate rulings are deleted, not preserved), and *where it lives* now: the spec, plan section
or source file that owns the decision. Where a reversal is itself something a future reader must
not re-litigate — "`Name` is **not** an override", "schemas 4 and 5 never shipped" — the entry
states the final rule and one sentence on what it replaced and why.

Nothing here is the only copy of a decision unless it is marked as having no owner. Where a
resolution's rationale had lived only in this register, it was moved into the owning spec first; the
mapping from claim block to owning document is
[D10](../spec/common/cross-cutting-decisions.md#d10--documentation-authority-is-mapped-and-it-is-conditional).

## Status

- **Q0.1–Q6.3** — pre-authoring questions, all resolved (user, 2026-07-25) and folded into the
  phase plans.
- **Q7.1–Q8.39** — opened *after* the seven phases landed, by the two post-implementation reviews,
  the six phase audits, the phase-8 docs sweep, and the landings themselves. `Q8.36` was never
  allocated; the gap is deliberate, not a missing entry.
- **Not finished, and what remains:**

  | Question | State |
  | --- | --- |
  | Q8.6 | No ruling; stands at its stated default (leave the compile-channel drain unscheduled). |
  | Q8.8 | Ruled; **formally unclosable on this box** — needs a Windows `net48` run. |
  | Q8.13 | Ruled ("implement it"); **not implemented**. |
  | Q8.15 | Ruled ("root-cause it"); reproduction failed, root cause unestablished, **stays open**. |
  | Q8.20 | Narrow fix landed; the general pattern is unresolved and now collides with a ratified rule — see Q8.40. |
  | Q8.21, Q8.22, Q8.23 | No ruling; each stands at a stated default, none implemented. |
  | Q8.32 | (a) rejected, (b) landed, and the recommended validation subset landed as `PrecompiledTemplates.ValidateAll`. The delegate-only indirection slice is named and deliberately not started. |
  | Q8.37 | Ruled ("remove auto-loading in 2.1"); **not implemented**, and its window disposition is blocked on Q8.39. |
  | Q8.39, Q8.40 | **Awaiting ruling.** Both are conflicts between two records that were decided independently. |

  Everything not listed is ruled and landed.

## Program-wide principles

Two rulings carry a principle referenced by name across the phase plans:

- **The match principle (Q1.3, generalized by Q2.1/Q3.5/Q3.6).** The runtime dynamic engine is the
  primary source of truth; the generator must match its validation rules, errors and throws,
  behaving as if it were part of the dynamic engine. The generator cannot always surface the same
  *warnings* through the same channel; such differences may legitimately exist, but the program
  strives for matching, and **errors always match**.
- **The fallback-legitimacy principle (Q2.2).** Catch-and-degrade is legitimate only for a small,
  researched set of conditions (stale cached data, genuine change-tracking logic); everything else
  is an error that must surface — thrown or reported, never silently degraded.

---

## Phase 0 — test-fallback-guardrails

- **Q0.1 — Should the feature suites be converted wholesale to the resolver path, or is the corpus
  sweep the posture carrier?**
  **Resolution.** The corpus sweep is the permanent posture carrier: feature suites keep
  direct-invoke isolation (a red test points at the emitter, not at five layers of plumbing) and
  contribute templates to the corpus instead.
  *Where it lives:* [phase 0 D4](phase-0-test-fallback-guardrails.md); the rule is normative in
  [testing-standards.md — Precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture)
  (ledger E8), with the single-sourcing mechanism added by E9.

## Phase 1 — template-emitter

- **Q1.1 — Does `maxRecursionCount` belong in the precompiled options fingerprint?**
  **Resolution.** No — it stays build-baked and the fingerprint stays silent on it. The divergence
  is intentional; the precompilation spec owns any future call to add the field at a schema bump.
  Phase 1 changed nothing here, as planned.
  *Where it lives:* [precompilation.md](../precompilation.md#compile-options-msbuild-properties);
  phase 1's closure note.

- **Q1.2 — When does the planned non-string coercion-rail change ship relative to the generator?**
  **Resolution.** The **joint-land rule**: the generated equivalent must match the dynamic engine at
  all times, so when the runtime rail changes the emitted `Execute` shape changes in the same
  landing, together with the spec text. Authoring-time verification found no present mismatch (both
  tiers implement the `as string ?? string.Empty` rail). The rail change itself remains a future
  window item; it is not yet a row in the next-window register, and phase 1's back-compat section
  states that when it is scheduled it lands as **one** window item covering runtime, emitter and spec.
  *Where it lives:* [phase 1 D13 / WI12 / Back-compat](phase-1-template-emitter.md).

- **Q1.3 — Should the generator raise a diagnostic for dangling region-fill candidates?**
  **Resolution (the match principle).** No hard refusal: the emitter reacts per verdict exactly as
  the runtime does. A dangling candidate is skipped, and its parse-emitted error is now *forwarded
  at build* (it used to be filtered out of the build channel entirely); a private-region fill raises
  `HED7024`, the build-tier twin of `HED5019`. The reserved `HED7022` was **not** needed for
  dangling-fill visibility — the ruling turns that case into a skip whose existing error simply
  surfaces — and was used for the unknown-`@profile` error instead.
  *Where it lives:* [phase 1 D7 / WI6](phase-1-template-emitter.md); registry rows `HED7022`,
  `HED7024`.

- **Q1.4 — Should the participant scan's over-provision on shadowed names be tightened?**
  **Resolution.** Keep the safe over-provision; revisit only on the named trigger. It is documented
  in `Language/ParticipantScan.cs` and the shadowed-name branch is asserted explicitly by
  `ParticipantScanLockstepTests.AShadowedParticipantNameOverProvisionsAndThatIsTheRuling`, so the
  trigger now has a test to go red.
  *Where it lives:* [`ParticipantScan.cs`](../../src/Heddle/Language/ParticipantScan.cs); phase 1.

## Phase 2 — document-shaper

- **Q2.1 — Empty-default-chain asymmetry: which side is intended?**
  **Resolution.** The runtime is the source of truth and the generator matches it: `DocumentShaper`
  stops skipping empty default chains and models the runtime's zero-length element. Bytes are
  unaffected; the characterization pin asserts the *matched* behaviour rather than the old one.
  *Where it lives:* [phase 2's shaping work items](phase-2-document-shaper.md).

- **Q2.2 — What replaces the generator's blanket `catch (Exception)` degrade?**
  **Resolution (the fallback-legitimacy principle).** The blanket catch is deleted. Only a specific,
  researched set of conditions degrades; every other exception is a defect that surfaces — an
  emitter throw is `HED7020` at Error, that one template emits nothing, and the rest of the pass and
  the manifest are unaffected. The legitimate-vs-must-surface taxonomy was researched path by path
  across the generation-time degrade paths and the runtime gauntlet's `PrecompiledFallbackReason`
  classes, and is specified rather than left as prose.
  *Where it lives:* [phase 5 D12 / WI10](phase-5-pipeline-config.md);
  [precompilation.md — which fallbacks are legitimate](../precompilation.md#which-fallbacks-are-legitimate);
  registry row `HED7020`; the default-policy half is a
  [next-window candidate](../spec/common/breaking-windows.md#next-window-candidate-register).

## Phase 3 — binding-layer

- **Q3.1 — Member-visibility policy** *(joint with Q4.1)*.
  **Resolution.** Follow the runtime: its narrower sandbox is normative and the generator tightens
  to match. The widening (accepting `protected internal`, inherited non-public, base-interface
  members) is a next-window candidate, not a defect.
  *Where it lives:* [phase 4 D7 / `MemberVisibility.IsAccessible`](phase-4-expression-writers.md);
  [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register).

- **Q3.2 — `[ExportFunctions]` precedence.** **Resolution.** Follow the runtime — merge semantics.
  *Where it lives:* [phase 3](phase-3-binding-layer.md).

- **Q3.3 — Is `[ExtensionReplace]` supported?**
  **Resolution.** Yes, in full: the generator binds the extension exactly as the runtime's
  replacement precedence resolves it. There is no reason a precompiled template cannot honour a
  runtime interface replacement. The remaining asymmetry — the runtime throws
  `TemplateOverrideException` on a collision between unrelated types while the build tier degrades
  with a recorded reason — is a deliberate next-window candidate, not a defect: it is a host wiring
  error the build has no business failing on before the host is assembled.
  *Where it lives:* [phase 3 / `ExtensionRegistrationRules`](phase-3-binding-layer.md);
  [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register).

- **Q3.4 — Prop-layout manifest row and gauntlet check.** **Resolution.** Additive schema row plus a
  gauntlet arm, coordinated with phase 5.
  *Where it lives:* [phase 3](phase-3-binding-layer.md) / [phase 5](phase-5-pipeline-config.md);
  [precompilation.md — the gauntlet](../precompilation.md#the-validation-gauntlet-and-mismatch-policy).

- **Q3.5 — Model type-name resolution: ambiguity and implicit namespaces.**
  **Resolution.** Match the runtime **exactly**, not merely degrade: the generator reproduces the
  runtime's resolution semantics and outcomes, surfacing the runtime's ambiguity error as the
  matching build-time error `HED7023`. Where the runtime's own logic is defective, both sides are
  fixed and then both must match — which is what happened: the runtime's order-dependent silent pick
  for a short name matched by several imported namespaces became the same ambiguity error, recorded
  as defect repair rather than a breaking change because a non-deterministic pick is not something a
  user can correctly depend on.
  *Where it lives:* [phase 3 F8](phase-3-binding-layer.md);
  [breaking-windows — not-window-gated rulings](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  registry row `HED7023`.

- **Q3.6 — Ineligible `[ExportFunctions]` container: silent skip or diagnostic?**
  **Resolution (the match principle).** A matching build-time **error**, `HED7021` — not the warning
  first recommended — because the runtime throws `ArgumentException` from
  `FunctionRegistry.RegisterFrom` for the same input.
  *Where it lives:* registry row `HED7021`; [phase 3](phase-3-binding-layer.md).

## Phase 4 — expression-writers

- **Q4.1 — Member-visibility policy** *(joint with Q3.1)*. **Resolution.** Use the runtime
  behaviour. *Where it lives:* see Q3.1.

- **Q4.2 — Overload-selection semantics: keep Heddle's flat Pareto rank, or adopt C# betterness?**
  **Resolution, in two parts.** *Near term:* keep the runtime behaviour and match it in the
  generator — degrade on ambiguity, cast-pinned emission — and, per Q8.1, stop being silent about a
  call the ranker has **proved** illegal (`HED7025`). *Longer term:* adopting C#'s
  better-conversion-target rule in the **runtime**, with the generator matching by construction, is
  evaluated and quantified: over the shipped built-in table **0 of 480** argument combinations change
  their winning overload, **82** become bindable that are ambiguity errors today, and **62** stay
  ambiguous (`double`/`decimal` are mutually non-convertible). It is therefore a pure widening with
  no rendered-byte change — and because it *is* a widening, it is a ratified-window item landed
  jointly on both tiers, not a fix.
  *Where it lives:* [phase 4 D10 / WI10](phase-4-expression-writers.md), measured by
  `OverloadBetternessEvaluationTests`;
  [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register).

- **Q4.3 — Dynamic-binder context.** **Resolution.** Reproduce the runtime's Heddle-context binding;
  `PrecompiledRuntime.DynamicMember` is the single place it exists. The residual asymmetry between
  the typed and dynamic member tiers over a foreign `internal` getter is a next-window candidate
  needing a spec clarification about which tier is right.
  *Where it lives:* [phase 4 D11 / WI9](phase-4-expression-writers.md);
  [next-window register](../spec/common/breaking-windows.md#next-window-candidate-register).

## Phase 5 — pipeline-config

- **Q5.1 — What happens to the `Precompile` and `Name` item metadata?**
  **Resolution.** Both are wired. `Precompile="false"` is a per-item opt-out that keeps the template
  in the `@<<` import map; `Name` is an **additional** spelling the template answers to at both
  tiers. **`Name` was not removed** — this entry once recorded "`Name` removed per the
  recommendation" and phase 5 implemented that removal, but the ask was only to wire `Precompile`,
  and the removal was reversed under Q8.12. Final semantics are Q8.25's (additive, never an
  override) plus Q8.30's (a runtime lookup spelling as well as a build-time one).
  *Where it lives:* [precompilation.md — Setup](../precompilation.md#setup) (the three-metadata
  table); [records.md — the 2.1 record, row 2](../spec/records.md#the-21-release--as-shipped-record);
  [phase 5 §Q8.12](phase-5-pipeline-config.md).

- **Q5.2 — Do the `View`/`PartialView`/`Master` resolver arms consult the precompiled registry?**
  **Resolution.** Yes — **every** resolver arm consults it. The hosted arms use a three-tier search
  ladder, registry → cache → disk, each walked in search-location order, so tier order beats
  location order (which is how the cache tier has always behaved). On a hit the gauntlet runs against
  the arm's *real* effective options, so a manifest built under `Native` is refused by the
  fingerprint check for a `FullCSharp` arm with no special-casing.
  *Where it lives:* [precompilation.md — the registry](../precompilation.md#the-registry--for-dynamic-call-sites);
  [phase 5](phase-5-pipeline-config.md).

## Phase 6 — diagnostics-utilities

- **Q6.1 — Should forwarded warnings carry their real IDs?**
  **Resolution.** Yes, and as a stated principle: **if diagnostics can surface early, they must — on
  both tiers.** Real IDs plus the `Fix` are forwarded at build time.
  *Where it lives:* [phase 6](phase-6-diagnostics-utilities.md). The principle is presently unmet for
  eleven compile-channel warnings — see Q8.6.

- **Q6.2 — The LSP's default output profile.**
  **Resolution.** Beyond aligning the default: the LSP follows the same configuration surface the
  runtime permits and **wires all options**, at full parity with the runtime option set and defaults
  through the shared names/defaults table.
  *Where it lives:* [phase 6 — WorkspaceConfig work item](phase-6-diagnostics-utilities.md).

- **Q6.3 — Diagnostic-catalog `MessageFormat` end-state.** **Resolution.** Consumed rows only;
  revisit on the named triggers. *Where it lives:*
  [phase 6 catalog](phase-6-diagnostics-utilities-catalog.md).

---

# Post-implementation questions

Opened after the seven phases landed. They are recorded here rather than in a phase plan because
this file is the register: the Q7.* entries in particular were written into
[phase-7-shared-test-corpus.md](phase-7-shared-test-corpus.md) and initially missed this file,
which is the bookkeeping failure this section exists to correct.

## Phase 7 — shared test corpus

- **Q7.1 — Do the LanguageServices corpus templates join the shared corpus?** They serve editor-tier
  completion/hover/diagnostics, for which the "renders correctly" axis does not exist; joining would
  need a fourth `Tier` value (`EditorOnly`).
  **Resolution.** Keep them separate. No `EditorOnly` tier; the intent table's three-value `Tier`
  axis stands. Revisit only if the editor tier ever needs a shape the corpus already has.
  *Where it lives:* [phase 7](phase-7-shared-test-corpus.md).

- **Q7.2 — Do the benchmark and sample corpora converge, and should `TestCorpus.props` serve
  `Heddle.Performance`?** Those trees are governed by the parity contract and the golden-corpus
  spec, whose byte requirements are stricter and differently motivated; `Heddle.Performance` also
  carries a fourth copy of the path-traversal helper phase 7 D2 deletes.
  **Resolution.** Leave `Heddle.Performance` out of scope — a benchmark effort is mid-flight there
  and must not be disturbed. The shared props file serves the four *test* projects only.
  **Accepted residue:** its path-traversal helper survives, so the failure class phase 7 D2
  eliminates is removed from the test suites but not from the benchmark project; revisit once the
  benchmark work settles. One edit was nonetheless made there — the deletion of its dead
  `<Version>` element, which Q8.11's single-sourcing required and which needed no other change to
  the project. That narrowing of "change nothing within it" is ratified in the 2.1 record's accepted
  residue, not an implementer's reading.
  *Where it lives:* [phase 7](phase-7-shared-test-corpus.md);
  [records.md — 2.1 accepted residue (b)](../spec/records.md#the-21-release--as-shipped-record).

- **Q7.3 — Delete or relocate the six checked-in written artifacts inside the corpus directory?**
  They are written by tests via `File.WriteAllText` into a tree three projects copy from.
  **Resolution.** Relocate, do not delete: they move outside the shared corpus glob so no file
  inside it is written by a test, and the writing tests are repointed.
  *Where it lives:* [phase 7 WI4](phase-7-shared-test-corpus.md).

- **Q7.4 — Is migration stage 5 in phase 7 or a follow-on?**
  **Resolution.** All stages, including 5, land inside phase 7 — the D4 coverage residue is closed
  completely rather than left as a tail. Each stage keeps its own byte-neutral gate and suite-time
  measurement, so the open-ended scope is bounded by per-stage acceptance rather than by stopping
  early.
  *Where it lives:* [phase 7 WI9](phase-7-shared-test-corpus.md).

## Q8 — post-audit and post-landing questions

- **Q8.1 — Should a function call the generator has *proved* illegal fail the build?** The generator
  computed `BindOutcome.Ambiguous` out of the shared `OverloadRank` core and then reported nothing,
  so a provably illegal template got a green build with zero diagnostics and a hard `HED1013` at
  first render — contradicting both program principles.
  **Resolution.** It is a build **error**, `HED7025`, reported at the call's `.heddle` position and
  deduplicated per call site, with the runtime's own sentence for the same input plus the run-tier id
  it twins. The side condition is mandatory and is implemented as an early return *before*
  `OverloadRank.Bind` runs: an argument the estimator returns `Unknown` for gets no rank token, so
  nothing is proved and nothing is reported. `BindOutcome.None` follows `Ambiguous`; exports behave
  identically. The refusal itself is unchanged — the template still degrades and no rendered byte
  moves on either tier — what changed is that the build stops being silent. Accepted consequence: a
  project containing an ambiguous overload call that builds green today starts failing.
  *Where it lives:* registry row `HED7025`;
  [breaking-windows — the overload-ambiguity silent degrade became a build error](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings)
  (defect repair, not window-gated, with the four grounds);
  [precompilation.md — build-time diagnostics](../precompilation.md#buildtime-diagnostics);
  [phase 4 D10](phase-4-expression-writers.md). Residue: Q8.18 (closed), Q8.19.

- **Q8.2 — The pending binary break in `PrecompiledExtensionBinding`'s constructor: which gate?**
  The 2-arg `.ctor` no longer exists in metadata while `MinSupportedSchemaVersion = 1` still accepted
  manifests referencing it, so the fault landed as a `MissingMethodException` out of `Register()` at
  host startup rather than as a clean rejection.
  **Resolution.** Ship the break as declared in 2.1 with **no compatibility shim**, and raise the
  schema floor so the gate rejects cleanly. The floor is **3**, and `Min = Max = Current = 3`.
  Verified against the `v2.0.0` tag: **schemas 1 and 2 are the only schemas that have ever shipped**
  — the shipped generator emitted `schemaVersion: 2`, the shipped engine accepted `1–2`, and the
  shipped `PrecompiledExtensionBinding` had a real 2-arg constructor the shipped emitter called. So
  the three unreleased increments (dynamic-member routing, the prop-layout row, the per-carrier
  `BindDefinition` overload) collapse into one schema 3, which also carries Q8.30's `RegisteredName`
  and Q8.31's `LinePathForm`: an increment no user could observe is not a migration step. The
  rejection is **demonstrated**, not asserted, by `OldSchemaManifestRejectionTests` over a reference
  facade carrying the pre-break surface under the real assembly's identity, plus a control arm that
  admits the same bytes at `Min` and observes the `MissingMethodException` the gate prevents.
  **This replaces a disposition that was factually wrong**: it argued the break "already shipped in
  2.0.0, because schema 4's optional parameter removed the 2-arg ctor then". Schema 4 never shipped,
  so the break is *pending*, which is the opposite of the claim; the false ground was withdrawn
  outright rather than softened, and the ruling's literal `4` became `3` because the collapse makes
  `4 > Max` impossible.
  *Where it lives:* [records.md — 2.1 record, row 1](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — the precompiled schema floor rises 1 → 3](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  [phase 5 §Q8.2](phase-5-pipeline-config.md). Reason-code fallout: Q8.24. The standing rule it
  produced: Q8.35.

- **Q8.3 — Fold the runtime onto the two generator-only "shared" cores?** `ExtensionRegistrationRules`
  and `TypeSpelling` were called only by the generator while the runtime hand-inlined both rules, so
  mutating the shared copy reddened zero runtime tests — transcriptions, not sources of truth.
  **Resolution.** Fold. `TemplateFactory.AddExtensions` resolves collisions through
  `ExtensionRegistrationRules.Resolve` and `LoadExtensions` sorts by its `OrderingKey`;
  `ReflectionHelper.ResolveType` drives `TypeSpelling` through a reflection `ITypeLookup<Type>`
  adapter, with its five duplicate parser methods and the tuple regex deleted; `ExportBookkeeping`
  gained tests. Acceptance was the property whose absence was the defect: mutating each shared rule
  reddens at least one **runtime** test. The fold surfaced two divergences, both fixed in the shared
  file so the tiers move together — the legal one-element tuple `(int)` the parser had refused, and a
  whitespace-padded top-level spelling the run tier had rejected.
  *Where it lives:* [phase 3 post-audit](phase-3-binding-layer.md#post-audit-work-items-2026-07-26);
  [breaking-windows — type-spelling parity](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings).

- **Q8.4 — Should the generator model `[ExportExtensions]` in its extension discovery?** The runtime
  scans only assemblies carrying the attribute; the generator scanned every referenced assembly, so
  it precompiled extensions the runtime will never register — a permanent silent per-request fallback.
  **Resolution.** Close it. `ExtensionBinder.CollectExported` reproduces
  `TemplateFactory.ObtainExtensions`' scope exactly: the engine assembly whole and unconditional,
  every other assembly only through `[ExportExtensions]` (named types, or all for the parameterless
  form, which short-circuits the assembly's remaining attributes as the runtime's `break` does).
  `ExportExtensionsScopeTests` supplies the kind of assembly no test had. Two fixture debts fell out
  and were paid.
  *Where it lives:* [breaking-windows — the generator stops precompiling extensions the host never
  exported](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  [phase 3](phase-3-binding-layer.md). The *other* half of this seam — the runtime discovering from a
  set the host never chose — is [D11](../spec/common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded)
  and Q8.37, and the two must be reconciled together.

- **Q8.5 — Fix `TemplateEmitter.StripGlobal`'s hard-coded assembly name?** It built a bare dotted type
  name and fed it to `RecordExtensionBinding`, whose `assembly` parameter defaults to the literal
  `"Heddle"`, so a user-defined nested or out-of-engine branch-role extension recorded
  `Ns.Outer.Inner, Heddle` where the gauntlet computes `Ns.Outer+Inner, <realAsm>` — on a path no
  fixture exercised.
  **Resolution.** Fixed, TDD: the reproducing fixture landed red first, because the defect's whole
  character was that no fixture reached it. Extension bindings are now recorded from the resolved
  type facts (bare nested name plus the real `AssemblyName`) at every call site, and `StripGlobal` no
  longer exists in the generator — phase 3's "zero remaining inline `global::`-strip AQN
  constructions" criterion is now true.
  *Where it lives:* `RecordExtensionBinding` call sites in
  [`TemplateEmitter.cs`](../../src/Heddle.Generator/Emit/TemplateEmitter.cs);
  [phase 1 D14 row 2](phase-1-template-emitter.md).

- **Q8.6 — Should the compile-channel drain be scheduled? (open, at default)** The generator runs no
  compile-channel stage, so eleven id-carrying warnings (`HED1016`, `HED2002`–`HED2004`,
  `HED3001`–`HED3005`, `HED4002`, `HED4005`, `HED5011`) never reach a build diagnostic, and phase 6's
  forwarded-ID fix is correct but **latent** — nothing can fire it.
  **Default (no ruling).** Leave unscheduled and recorded. Q6.1's early-surfacing principle is
  therefore knowingly unmet for those eleven, which is a disposition rather than an oversight.
  Phase 6's assessment: realistically a small phase, not a work item.
  *Where it lives:* **no owning spec** — this register and [phase 6](phase-6-diagnostics-utilities.md).

- **Q8.7 — `docs/native-expressions.md` deviation 1 is wrong; how far does the correction go?** It
  claims `==`/`!=` on unrelated reference/**mixed** types compiles to a total `object.Equals`, while
  the runtime guards with `IsReferenceish(left) && IsReferenceish(right)` — so under the authority
  convention the sentence was a live trap: aligning to it would introduce a bug in the name of fixing
  drift.
  **Resolution.** Widened into a full post-implementation documentation sweep, authored as its own
  plan so the corrections are auditable rather than folded invisibly into code landings. Deviation 1
  is one of **four** false normative claims in that one document — deviation 6 (user-defined
  operators are not honoured for `&`/`^`/`|`, shifts or any unary), the shift row's
  `int`-right-operand rule (any integral is accepted and converted, so the doc *understates* what
  compiles), and the lifted-operands claim that equality lifts — plus seven more imprecise enough to
  mislead an implementer. The generator's shared rule tables describe every one of those divergences
  correctly, so the tree's most accurate account of native-expression semantics is
  `NativeOperatorRules.cs` and the document the convention points at is the least accurate. That
  inversion is why the sweep's first design decision narrows the authority convention (→ Q8.9)
  rather than only fixing sentences.
  *Where it lives:* [phase 8 — docs sweep](phase-8-docs-sweep.md) (WI1/WI2 and the class-N evidence
  table, which quotes the source lines for each false claim).

- **Q8.8 — `net48`/`net6.0` verification.** `net48` is Windows-only and has never run on this box;
  `net6.0` aborts with `MSB4181`.
  **Resolution.** Take the default: drift #9 (`ToString("R")`) is formally **unclosed** until a
  Windows `net48` run, and no work on this box can close it. `"R"` genuinely is
  shortest-round-trippable on CoreCLR, so the 23 cases here are a revert-detector, not proof of the
  fix's sufficiency.
  *Where it lives:* **no owning spec** — this register.

- **Q8.9 — Where does the narrowed authority convention live, and is it retroactive?**
  **Resolution.** In [cross-cutting-decisions.md as **D10**](../spec/common/cross-cutting-decisions.md#d10--documentation-authority-is-mapped-and-it-is-conditional),
  in two parts: *(a)* a **closed** nine-row mapping from claim block to normative home, with the
  tie-break that the home whose *diagnostics* the claim can produce wins (a diagnostic has a registry
  owner, prose does not); and *(b)* the condition — a home outranks the implementations only for a
  claim carrying a verification marker or covered by a gate. Two things the question left implicit
  are stated there: a claim block **absent** from the table has no normative document at all (the
  implementations are the authority and the runtime is the tie-break), and a document acquires a
  block by being added to the table, not by asserting authority in its own prose. Non-retroactive:
  already-ratified D-items stand, because relitigating outcomes under a rule that did not exist when
  they were taken is not a correction.
  *Where it lives:* D10.

- **Q8.10 — If phase 7 has not landed, does phase 8's D9 ship, slip, or transcribe?** D9 would make
  qualifying doc examples executable by single-sourcing them from phase 7's shared corpus.
  **Resolution.** D9 is **rejected as designed**. Documentation has a different job from a test
  fixture — it explains, and byte-identity with a corpus entry is not a property worth buying. No
  `@include:` from corpus templates, no corpus intent rows for doc examples, and phase 8 no longer
  blocks on phase 7. WI14 is repurposed from the include spike to *deleting* the
  `"--- Verbatim from docs/custom-extensions.md ---"` coupling in `ScopeChannelDocExampleTests`: what
  that fixture asserts about `Scope` channel behaviour stays where it is genuinely a behaviour test;
  what goes is the claim to be the document's bytes, which nothing enforced and which this ruling
  makes deliberately false. Done-when requires the behavioural coverage to be either still asserted
  elsewhere or recorded as dropped, so the deletion cannot quietly lose a test.
  **One cost accepted knowingly**, recorded in D9 so it is not rediscovered as a surprise: the
  rejected design's strongest point survives — this program shipped a byte-changing literal formatter
  change and a profile default flip, either of which can invalidate a documented output, and review is
  a weaker guard than a gate. D11's currency rule carries that residual risk, and a stale doc example
  found later is a docs defect to fix, not grounds to reopen this.
  *Where it lives:* [phase 8 D9 / WI14](phase-8-docs-sweep.md) (the superseded design is preserved in
  a collapsed block there — the program's convention for reversed decisions).

- **Q8.11 — Should the nine `<Version>` elements be centralised as part of the 2.1 bump?**
  **Resolution.** Yes, and additionally strong-name every first-party assembly so the CS8002
  warnings stop. One `<VersionPrefix>2.1.0</VersionPrefix>` in `Directory.Build.props` replaces all
  nine. The real inventory was **fourteen kinds** of statement, not nine, and it included the live
  drift nobody had found: `LspServer.InformationalVersion = "1.0.0"`, which is what
  `heddle-lsp --version` printed and what the LSP `initialize` response reported for the whole 2.0
  line, guarded only by a test comparing it against itself — now *derived* from the assembly, so the
  statement no longer exists. Also load-bearing: the CI beta job's `--version-suffix` had to lose its
  leading dash, because a composed prefix/suffix is joined with one. Signing covered
  `Heddle.Demo.Models`, `Heddle.Demo.Wasm` and — found by the gate rather than by the warning —
  `Heddle.LanguageServices.Tests.Corpus`, whose csproj already claimed to be signed. Scriban is
  accepted through a declared unsigned-reference list in `Directory.Build.targets`, keyed *on* the
  named assembly rather than scoped to it, because Roslyn has **no** per-reference suppression for
  `CS8002` (the warning carries no source location, `Csc` takes only a project-wide list, and
  `NoWarn` metadata on a `PackageReference` was *measured* to have no effect). Gate:
  `VersionConsistencyTests`, 17 cases.
  *Where it lives:* [records.md — 2.1 rows 4 and 5 plus accepted residue (a)](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — the release line is stated once](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  [phase 5 §Q8.11](phase-5-pipeline-config.md). Warning-regression gating in general: Q8.26.

- **Q8.12 — Who fixes the sample that still passes the removed `Name` item metadata?** The sample was
  golden-checked, so the golden encoded the wrong outcome.
  **Resolution.** Implement `Name` rather than fix the sample: the Q5.1 record overreached its ask,
  and the defect was that the feature was never wired, not that the metadata existed. Final semantics
  are Q8.25's and Q8.30's.
  **The larger defect this uncovered is the load-bearing part.** `Name` was not merely unread:
  **none** of the three metadata worked from a real project. `Heddle.Generator.targets` restated each
  as `<Key>%(HeddleTemplate.Key)</Key>` inside an `Include="@(HeddleTemplate)"` transform — and since
  the transform already copies every metadatum while a cross-item `%()` reference outside a target
  evaluates to the empty string, each element *overwrote* the copied value with `""`. So `Key` and
  `Precompile="false"` were inert too, and nothing noticed because every test injects
  `build_metadata.*` directly and so never crosses that file. Fixed by deleting the elements, with two
  gates behind it: a structural set-equality pin
  (`PipelineContractTests.EveryDeclaredItemMetadataIsReadByTheGeneratorAndNotNulledByTheTargets`,
  which also refuses any restatement) and the behavioural one — `samples/codegen-t4-successor`, whose
  named import-only partial fails the sample build with `HED7011` if the metadata stops flowing from
  a real csproj.
  *Where it lives:* [records.md — 2.1 row 2](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — the per-item `HeddleTemplate` metadata started working](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  [phase 5 §Q8.12](phase-5-pipeline-config.md). Fallout: Q8.25 (import map), Q8.27 (`#line` form).

- **Q8.13 — Does the value-path coercion rail need a byte-level fixture? (ruled, NOT implemented)**
  `native-expressions.md` §4 is normative: a boxed non-string reaching the value path is dropped to
  empty while the render path stringifies it. Phase 1's audit found this pinned as emitted *shape*
  plus render-path behaviour, with **no byte-level fixture on either tier**.
  **Resolution.** Implement it: build the machinery — a host extension whose `ProcessData` consumes
  its body's `Execute` result and returns a non-string — and pin §4 at byte level on both tiers. The
  joint-land rule (Q1.2) may rewrite the rail later; a byte-level pin is what makes that rewrite
  safe, not a reason to skip it.
  *Where it lives:* **not yet implemented.** Today's tripwire is
  `StrategyShapeDifferentialTests`' `strategy-nonstring-value`, which is a shape pin;
  [phase 1's audit residue](phase-1-template-emitter.md).

- **Q8.14 — Should `[EncodeOutput]` + `[NotEncode]` on one extension be an error?**
  `RenderTypeRules.Derive`'s fourth truth-table row is unreachable from any real extension, so either
  the combination is meaningful and deserves a fixture, or it is incoherent and should be an error on
  both tiers.
  **Resolution.** Neither — closed as **not implementable and not needed**, with no diagnostic on
  either tier and no analyzer. **This withdraws an earlier ruling** ("make it a diagnostic on two
  surfaces: `HED7026` at the use site, `HED7027` from a declaration-side analyzer"), whose premise was
  that an author can declare the contradiction. They cannot: `NotEncodeAttribute` is
  `AttributeTargets.Property` and `EncodeOutputAttribute` is `AttributeTargets.Class`, so
  co-declaring them on one **type** is `CS0592` — a compiler *error* in the extension author's own
  build, which is exactly the surface the analyzer was to occupy, at a stronger severity and
  delivered by the language. Nor is there a use-site divergence: the pair is observable only in
  forged or IL-authored metadata, where both tiers evaluate the same shared `RenderTypeRules.Derive`
  and both answer `RenderType.Raw`, indistinguishable from an extension carrying neither attribute.
  What landed instead is the finding in executable form on both tiers, written to redden if
  `NotEncodeAttribute`'s targets ever widen — at which point this assessment expires and both
  diagnostics become required. `RenderTypeRules`' fourth row is unchanged: unreachable from any
  declaration, not wrong.
  *Where it lives:* the registry's
  [`HED7026`–`HED7027` deliberately-unclaimed row](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry),
  which carries the reasoning and the reopening condition; `ContradictoryEncodingAttributeTests`
  (build tier) and `TheNotEncodeVetoRowIsUnreachableFromAnyDeclaration` (run tier), both of which
  state the reopening condition in their own doc comments.

- **Q8.15 — Stabilise the two intermittently-failing tests. (ruled; root cause unestablished, OPEN)**
  Phase 3's audit observed `Heddle.Tests.BodyModelRuleTableTests` (a *different* row failing on each
  of two consecutive solution runs) and
  `Heddle.Generator.Tests.CallTargetAdoptionTests.AnExportedFunctionDoesNotStealARegisteredExtensionName`.
  This matters beyond tidiness: every mutation result in these audits rested on "this test went red
  because of my change", and flakiness poisons that inference.
  **Resolution (ruling).** Root-cause it and decide — do not paper over it. Identify the actual
  mechanism rather than adding tolerance; legitimate outcomes include serialising the affected
  collection, isolating the shared artifact, or removing a dependency on something genuinely
  non-deterministic. **A retry attribute is not an acceptable resolution** — it preserves exactly the
  property that makes a surviving mutant invisible.
  **Investigation record (2026-07-26): flakiness reproduced, but NOT the two named tests.** Recorded
  in full because the negative results are the valuable part.
  *Pressure applied:* 30 × full-solution runs in the working tree (failures on iterations 7, 9 and
  16–29); 25 × full-solution runs in an isolated `git archive HEAD` copy (**all clean**); 50 legs of
  `Heddle.Generator.Tests` with both TFMs concurrent at `maxParallelThreads=32` (clean); 150
  cold-start processes × 32 threads rendering the `BodyModelRuleTableTests` rows (clean); 6 solution
  runs against a deliberate concurrent builder (clean).
  *What the working-tree failures actually were:* not the named tests. A concurrent MSBuild
  invocation rewriting the test projects' `bin/**` while test hosts read from it — triggered by other
  agents editing `.csproj` files, which makes MSBuild's `IncrementalClean` delete and re-copy the
  whole content-file set. That is an artifact of running several agents against one working tree, not
  a product defect, and the 25 clean isolated runs confirm it.
  *Hypotheses positively ruled out, with evidence, so they need not be re-tested:* the process-global
  precompiled registry (every class touching it is already in the serialized
  `[Collection("PrecompiledRegistry")]` — the discipline is complete); in-process class parallelism
  in `Heddle.Generator.Tests`; ANTLR's process-static prediction caches (real shared mutable state,
  but 0 failures in 150 cold-start 32-thread bursts — see Q8.22); `ReflectionHelper.Reconfigure`'s
  unlocked dictionary rebuild (a genuine race, unreachable here — `_configured`-guarded, and the
  caller lives in another process); an order dependency in `BodyModelRuleTableTests` (it is the one
  class of ~60 that does not call `HeddleTemplate.Configure`; 14/14 green in isolation); and a Roslyn
  metadata race (impossible — default `ExpressionMode` is `Native`, so that test never enters the C#
  tier).
  *No code change was made, deliberately.* With neither named test reproduced, any change would be
  tolerance dressed as a fix. In particular `Heddle.Generator.Tests` was **not** blanket-serialised,
  because serialising without a demonstrated race is the same defect in a different costume.
  **The blocker is diagnostic, not analytic:** the original observation captured test *names* but not
  failure *messages*, and for `BodyModelRuleTableTests` "expected Ada, got null" (a type/extension
  resolution fault) and "expected Ada, got Ada0" (a chained-channel fault) have disjoint root causes.
  **Whoever next observes either failure must capture the assertion text.**
  *Where it lives:* **no owning spec** — this register.

- **Q8.16 — `RegionTests.LocationOffsetOf` returns a hard-coded `0`.** A helper that reads as a
  position assertion and asserts nothing is worse than an absent assertion, because it looks like
  coverage.
  **Resolution.** Fixed: the offset is derived independently, from the diagnostic's own line span
  over the same bytes the generator was handed, and the file identity is asserted too — so the
  assertions that read as position assertions are ones.
  *Where it lives:* `TemplateOffsetOf` in
  [`Heddle.Generator.IntegrationTests/RegionTests.cs`](../../src/Heddle.Generator.IntegrationTests/RegionTests.cs),
  whose doc comment records what the old helper failed to catch.

- **Q8.17 — `SymbolTypeIndex.Cache` is an unbounded static `Dictionary<Compilation, …>`.** Correct
  and lock-guarded, but it pins every `Compilation` the process has seen — fine for a one-shot build,
  questionable for a long-lived IDE session where the analyzer sees a new `Compilation` per
  keystroke-batch.
  **Resolution.** Give the cache a real operational contract, in three parts: a clear operation API
  (`Get`/`Contains`/`Clear` with `Count`/`Capacity`/`MaxIdleGenerations`/`Generation` observable),
  observable occupancy against a stated capacity (default 8, LRU on admission), and **genuine
  staleness eviction** — a *generation* is one compilation admitted (an edit epoch), and an entry
  untouched for more than `MaxIdleGenerations` (default 2) generations is evicted **whether or not
  the cache is full**, so age is an independent eviction reason rather than a tie-break under
  capacity pressure. The clock is the admission counter and never a wall clock: a generator whose
  behaviour depends on elapsed time is a generator whose output is not a function of its inputs.
  **One premise of the question does not hold, and the correction strengthens the answer:** editing a
  template and editing it back does *not* restore an identical-but-old entry — a `Compilation` is
  immutable and the reverted state is yet another new instance — so the real leak shape is
  *retained-but-unreachable*, which is worse than described and is exactly what an age bound collects.
  **Deliberately not done:** no weak-reference or `ConditionalWeakTable` keying (it would fix pinning
  by construction but gives neither observable occupancy nor an age, and `ConditionalWeakTable` is
  not enumerable on `netstandard2.0`, which the generator targets); no timer, background sweep,
  `IDisposable`/flush hook, hit/miss statistics, per-entry cost accounting, or configuration surface —
  capacity and idle tolerance are constructor parameters used by the tests, not MSBuild properties.
  The generator's other per-process statics (`DefaultFunctionBinder.RowsByName`,
  `NativeExpressionWriter.DefaultShims`, `GeneratorDiagnostics.ForwardedDescriptors`) were left alone:
  they are bounded lookup tables keyed by strings, not by compilations, so the retention question does
  not arise. Nothing under `src/Heddle/` was touched.
  *Where it lives:* [`SymbolTypeIndexCache.cs`](../../src/Heddle.Generator/Binding/SymbolTypeIndexCache.cs),
  whose doc comment is the normative statement of the eviction rule and its rationale;
  `SymbolTypeIndexCacheTests`, including `EvictionCannotChangeWhatTheIndexAnswers`.

- **Q8.18 — `HED7025` proves illegality against the *build-time* function inventory. Is that the
  right scope?** A host may add an overload at run time through `TemplateOptions.Functions.Register`,
  which could make an ambiguous set unambiguous — so a template legal *for that host* would fail the
  build, and unlike the emitted-code case the gauntlet cannot rescue a build error.
  **Resolution.** Closed; the premise was wrong and the question dissolves. Precompiled function
  calls are **statically bound at build time** — the emitted call names the shim method resolved
  through a build-time table, and nothing in `PrecompiledRuntime` consults `options.Functions` at
  render — so precompiled code cannot resolve a runtime-registered function at all. The build-time
  inventory is not merely *a* scope, it is the only scope that can be correct for what precompiles;
  a host registering extra overloads is served by the dynamic tier through the gauntlet's
  function-binding check. The one residual, recorded rather than reopened: `HED7025` is an error, so
  it fails the build before any tier is chosen. That is acceptable because the escape hatch is real
  and was verified — `Precompile="false"` `continue`s before key derivation and emit, so no
  diagnostic can fire for an opted-out item while it stays in the import map. **No code change, no
  option added.**
  *Where it lives:* [precompilation.md — functions in precompiled templates](../precompilation.md#functions-in-precompiled-templates);
  the residue paragraph of
  [breaking-windows' `HED7025` entry](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings).

- **Q8.19 — Two illegal calls in one template report once. Should the emitter continue past an
  unwritable construct? (landed in part)** `BuildBody` abandoned at the first construct it could not
  write, so the author fixed one, rebuilt, and met the next.
  **Resolution.** Collect them — where doing so is contained, and stop and report the cost where it
  is not. Both halves happened, and both are the answer:
  **The element walk was contained and landed.** `PopulateBody` records the first reason, skips the
  refusing element and keeps walking, with no call-site and no signature changes (three locals in one
  method). Soundness rests on sibling elements being independent — they share the same immutable
  `BodyContext`, and nothing a refused element touches can make a later legal element illegal — so
  skipping one cannot manufacture a refusal, or a green, that a fresh walk would not reach. Refusal
  still propagates: a nested body's refusal still fails its parent up to `Emit`, where a null root
  still means no `.g.cs` and no manifest row. Collecting is about how many diagnostics one build
  surfaces, never about emitting past a refusal — a template compiled with its illegal elements
  quietly dropped would be far worse than one-at-a-time reporting, and that guard is a test of its own.
  **The expression walk was NOT contained and was not attempted.** `NativeExpressionWriter.Write` is
  string-or-`null` composition: every node returns `null` as soon as a child does, because the parent
  has nothing to compose. Continuing there means fabricating placeholder text for the failed child
  and discarding it — the restructuring the ruling said to stop at rather than half-do. That
  asymmetry is deliberate and is the honest boundary of this landing.
  **One behavioural consequence, pinned rather than left to be discovered:** a delegate-only function
  *after* the first unwritable construct is now reached, so `Emit` takes the `HED7014`
  fallback-marker arm instead of returning a plain unsupported result — the build gains a warning and
  a marker manifest row where before there was no row at all. That is correct: the template really
  does call a function no metadata can represent, and walk order was the only reason it stayed hidden.
  *Where it lives:* [precompilation.md — how many of these one build reports](../precompilation.md#buildtime-diagnostics),
  including the match-principle note that a build may surface a set no single dynamic compile
  produces while every collected report is one the runtime raises for the same input;
  `CollectedRefusalDiagnosticTests`.

- **Q8.20 — Should a test ever read another project's build output? (narrow fix landed; general
  question open)** `CorpusResolverSweepTests` string-substitutes its own assembly path to reach
  `src/Heddle.Tests/bin/Debug/<tfm>/Heddle.Tests.dll` and climbs `../../..` to read the corpus, while
  nothing ordered its project after `Heddle.Tests`; under `dotnet test Heddle.sln` MSBuild could run
  its `VSTest` before or during that build, which was *reproduced*.
  **Resolution (narrow fix, landed).** The missing `ProjectReference` is added with
  `ReferenceOutputAssembly="false"` — build order only, deliberately not a compile-time reference.
  This suite hands hand-filtered reference sets to the compilations it creates, so pulling
  `Heddle.Tests` and its transitive graph into its own compile would put a second set of names in
  scope for no benefit: the DLL is loaded **by path at run time**, not bound at compile time. No
  cycle and no duplicate-type problem.
  **The general question stands:** the fix orders the build, it does not stop a test reading another
  project's output, and the traversal is still there. A ratified rule now says it must not be — see
  **Q8.40**.
  *Where it lives:* [phase 5's landing](phase-5-pipeline-config.md);
  [testing-standards — test-input single-sourcing](../spec/common/testing-standards.md#test-input-single-sourcing).

- **Q8.21 — Should the generator test projects carry `DisableTestParallelization`? (open, at
  default)** `Heddle.Tests` and `Heddle.LanguageServices.Tests` both do, justified by "Heddle uses
  process-global static state"; the two generator suites do not, though they link the same front-end
  sources. No race was found, so this is about **stating an invariant**, not fixing a defect.
  **Default (no ruling).** Document the asymmetry deliberately rather than changing it: serialising
  without a demonstrated race costs suite time for no evidence. Not implemented.
  *Where it lives:* **no owning spec** — this register.

- **Q8.22 — ANTLR's process-static prediction caches. (open, at default)** `HeddleLexer`/`HeddleParser`
  share `decisionToDFA` and `sharedContextCache` across every parse in the process, mutated during
  `AdaptivePredict` under `PredictionMode.SLL`. 150 cold-start 32-thread bursts produced no
  misbehaviour, but the runtime *is* used concurrently in production (`BranchConcurrencyTests`,
  file-watcher reloads on background threads) and `Antlr4.Runtime.Standard`'s thread-safety guarantee
  for this is documented nowhere in the repo.
  **Default (no ruling).** Record the reliance explicitly in the concurrency section of the spec, and
  revisit if any concurrent-parse fault is observed. Not implemented.
  *Where it lives:* **no owning spec yet** — this register.

- **Q8.23 — `PipelineContractTests` uses fixed shared temp paths. (open, at default)**
  `Path.GetTempPath()/heddle-root` and `.../elsewhere/shared/banner.heddle`, where every other test
  uses a `Guid`-suffixed directory. Harmless today because they are pure path arithmetic never
  materialised on disk, but it is the one place the convention is broken, and a future test that
  *creates* that path would collide across the two TFM legs.
  **Default (no ruling).** Convert to the `Guid`-suffixed convention. Not implemented.
  *Where it lives:* **no owning spec** — this register.

- **Q8.24 — A 1.x manifest's rejection reason changed from `EngineVersionIncompatible` to
  `SchemaVersionUnsupported`.** With the raised floor the schema check runs first, so a manifest
  declaring schema 1 is rejected before the engine-version check is reached — and two shipped
  documents name the old gate.
  **Resolution.** Closed as invalid: the question was speculation. **There are no 1.x manifests** —
  precompilation shipped in 2.0.0 — so the reason-code change has no population to affect. Two
  corrections follow. The shipped documents naming `EngineVersionIncompatible` as the gate for "old"
  manifests describe a case that cannot occur, which is a **docs defect** rather than a behavioural
  question. And on whether an option is needed for throwing on a wrong manifest: it is not, and none
  was added — `PrecompiledMismatchPolicy` is pre-existing 2.0 API (`Strict` throws, `Fallback`
  degrades) and phase 0's guardrails depend on `Strict`; nothing in this program introduced a switch
  for it. **No code change.**
  *Where it lives:* the resolution is here; the docs correction it implies is **not yet recorded in
  the phase-8 sweep's item list** and is carried by this entry. (`EngineVersionIncompatible` remains
  a live reason for a schema-3 manifest built against a different engine version — see
  [precompilation.md's carrier table](../precompilation.md#what-a-fallback-event-names) — so only the
  "gate for 1.x" claim is wrong.)

- **Q8.25 — An explicit `Key`/`Name` makes a template unimportable by its path.** The `@<<` import map
  is keyed by the same derived key as the registry, so a named item's path spelling stopped resolving
  and the importer drew `HED7011`.
  **Resolution — `Name` is additive, and it is NOT an override.** The template keeps its path-derived
  (or explicit `Key`) key **and** gains the name; both spellings resolve, and nothing that resolved
  before may stop resolving. This corrects the first implementation, which made `Name` a second
  spelling of `Key` and silently broke every `@<<` that named a file by its path — the reason the
  reversal is recorded rather than quietly collapsed.
  The resolution model is **two passes over the import map, keys first, names second**, so additivity
  is structural rather than conditional: every key is already in the map when the first name is
  considered. A name whose spelling is taken (by another template's key, or by another name) is
  dropped and reported at `HED7004` against the name; the template's own key is unaffected, because
  **a broken addition costs the addition and nothing more**. A name equal to the template's own key
  adds nothing and advises nothing.
  Every downstream rule was **re-derived rather than assumed to carry over**, and four changed.
  `HED7018` is suppressed by `Key` **only** — an additive name leaves the flattened key in place, so
  the warning is still about something real (verified with a three-arm test: bare warns, `Key`
  silences, `Name` still warns and names the flattened key that really registered).
  `HED7002`/`HED7003` are over **keys only**, since a name registers no manifest row and is never a
  registry lookup, so it can neither duplicate nor case-shadow a key. `Key`+`Name` is **not a
  conflict** — it is two names for one template, which is the point. And `HED7028` is claimed for a
  new advisory: a **warning** where an import resolved through the key of a template that also has a
  registered name, fired once per distinct import spelling at the importer's `@<<{{…}}` block. Claiming
  a new id was right here where declining one was right before: every other `HED70xx` key diagnostic
  reports something *unusable*, and this reports something that *works*.
  *Where it lives:* [precompilation.md — the `Name` metadata row](../precompilation.md#setup);
  registry row `HED7028`;
  [breaking-windows — the per-item metadata entry's correction](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  [phase 5 §Q8.12 landing 2](phase-5-pipeline-config.md), which holds the full re-derivation table.
  Gate: `TemplateNameMetadataTests` (33 cases), whose fixture doc states plainly that the first
  implementation was an override and was corrected.

- **Q8.26 — Warning regressions are not gated.** Q8.11 removed the eight `CS8002` warnings and the
  build has no `TreatWarningsAsErrors`, so nothing prevents them — or any other warning class — from
  coming back.
  **Resolution.** Leave it as is. No `TreatWarningsAsErrors`: it obstructs quick proof-of-concept
  work and fast testing feedback. The signing *cause* stays gated (a project losing its strong name
  reddens `VersionConsistencyTests.EveryFirstPartyProjectUnderSrcIsStrongNamed`); warning regressions
  in general are deliberately not gated. **Closed; no change.**
  *Where it lives:* this register; the gate is named in
  [records.md — 2.1 row 4](../spec/records.md#the-21-release--as-shipped-record). This ruling is what
  lets `HED7028` and the opted-out advisories be additive by construction.

- **Q8.27 — Should `#line` name a path the compiler can open?** Q8.12 separated the `#line` file from
  the registration key; the alternative to the repo-relative value is the absolute
  `AdditionalText.Path`, which would put machine-specific paths into eight `Verify` snapshots and
  every generated-source golden.
  **Resolution — absolute where it costs nothing, root-relative where it does not.** *Outside*
  `HeddleTemplateRoot` no anchor exists and the old fallback was the template's bare filename — a
  name no compiler can open and one that collides across directories — so it is now the template's
  own `AdditionalText.Path`, absolute in any real build. *Under* the root the form stays
  root-relative: `HeddleTemplateRoot` is an absolute machine path, so an absolute `#line` would put
  this machine's layout into the sample golden and every rooted snapshot, and scrubbing it back out
  would pin a placeholder instead of the value. `#line` is emitted from exactly one place and no third
  form was introduced. **Which of the two forms a template used is manifest data**
  (`PrecompiledTemplateInfo.LinePathForm`), not the header comment this ruling first emitted — see
  Q8.31, which replaced the carrier without touching the decision.
  *Where it lives:* [records.md — 2.1 row 3](../spec/records.md#the-21-release--as-shipped-record);
  [phase 5 §Q8.27](phase-5-pipeline-config.md).

- **Q8.28 / Q8.29 — A `Precompile="false"` item's key/name faults are never reported, and `HED7028`
  cannot fire for the case it is most for.** The diagnostics loop `continue`d before key derivation,
  so a malformed `Key`, a malformed `Name` or an already-taken `Name` on an import-only item produced
  **no diagnostic at all** — the name silently failed to register and every `@<<` that used it drew
  `HED7011` at an innocent importer. Symmetrically, the advisory could not fire for an import *inside*
  an opted-out file. Two ends of one defect, and they got one answer.
  **Resolution.** Derive and validate key *and* name before the `Precompile` gate, so an opted-out
  item raises exactly the `HED7004` faults an included one does, and parse it in **advisory-only**
  mode so its own imports draw `HED7028` at the importer's own `@<<{{…}}` block. `HED7029` was **not
  needed** and stays free — every fault already had a home.
  **What deliberately did not change**, stated so it is a decision and not an omission: an opted-out
  item's *template* diagnostics — a missing import, a parse error — stay unreported. Draining every
  opted-out file's parse channels would turn previously-green builds red over templates the author
  explicitly excluded from this build, which is far larger than what was asked. It is a suppression,
  not an amnesty: the moment a precompiled template imports the file, the importer's own parse pulls
  the same content through the same channels and raises them. The boundary is pinned from both sides,
  and the opt-out's contract is pinned too — a clean opted-out item with a working name yields no
  manifest row under either spelling and no entry class, while the importer that does precompile still
  resolves the import.
  *Where it lives:* [records.md — 2.1 row 7](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — an opted-out item's key/name metadata is validated](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  the registry's [`HED7029` deliberately-unclaimed row](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry),
  which records the two candidates considered and declined; [phase 5 §Q8.28/Q8.29](phase-5-pipeline-config.md).

- **Q8.30 — Nothing pins that a registered `Name` is *not* a runtime registry key.** The question
  proposed asserting that a runtime lookup by name **misses**.
  **Resolution — the opposite: the engine answers to names, at both tiers.** If `Name` is a useful
  key for imports it is a useful key full stop, and the build-time-only scope was an artifact of the
  wiring rather than a designed boundary. So `PrecompiledTemplateInfo` carries `RegisteredName`, the
  generator emits it, and the registry answers to it; only a name that actually registered at build
  time reaches the manifest, so the two tiers hold the same spellings for the same templates.
  `Name`'s scope has now moved **three** times — override → additive/import-only → additive plus
  runtime — and the fixture doc states all three, because each step invalidated the previous step's
  rules rather than extending them.
  **Resolution order: keys win**, always, and independently of which assembly registered first. Three
  grounds in order of weight. *Additivity* — an addition that displaced an already-resolving spelling
  is exactly the override Q8.25 corrected, and re-introducing it at the runtime tier would undo that
  correction on the surface where it is hardest to see. *The match principle* — the build tier's
  import map has been keys-first since Q8.25, so the runtime uses the same order and the tiers cannot
  disagree about what a spelling means. *Determinism* — one dictionary holding both would make the
  winner depend on host load order. Mechanically it is **two indexes with a disjointness invariant**,
  enforced from *both* directions because either can happen first across assemblies: a name whose
  spelling a key already owns is refused at insert, and a key arriving later **evicts** the name
  shadowing its spelling.
  **The new collision class does not throw.** A registered name colliding with another registered
  template is `HED7104` / `RegisteredNameUnavailable`, reported once per lost name through
  `OnFallback`. It is a **runtime** id because the collision spans assemblies and the build tier
  structurally cannot see it: the generator reads referenced assemblies' *symbols*, but a manifest's
  rows live in a `GetTemplates` method **body**, i.e. IL. Within one compilation the same fault is
  still `HED7004`. It deliberately does not join the duplicate-**key** throw, and the contrast is
  asserted in one test so neither can drift into the other: two templates claiming one key is
  unresolvable, whereas a name/key collision is fully resolved by the ordering rule, so Q8.25's
  principle decides the rest — a broken addition costs the addition and nothing more, and throwing
  would take a whole assembly's registration down over an alias.
  One asymmetry follows from the opt-out and stands deliberately: an opted-out item contributes no
  manifest row, so its `Name` is a build-time import spelling only (Q8.28 considered warning about
  that and declined — it is the feature's intended shape).
  *Where it lives:* [precompilation.md — lookup by key, and by registered name](../precompilation.md#lookup-by-key-and-by-registered-name);
  [records.md — 2.1 row 6](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — the precompiled registry answers to a registered `Name`](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings)
  (additive, with the shadowing hazard stated); registry row `HED7104`;
  [phase 5 §Q8.30/Q8.31](phase-5-pipeline-config.md). Schema: **3**, shared with Q8.31 — see Q8.2 for
  why the number went *down*.

- **Q8.31 — The `#line` relativity marker is prose in generated code, not a machine-readable form.**
  A stack-trace symbolizer, an IDE or the LSP cannot act on a comment.
  **Resolution.** Record the fact in the manifest and delete the comment. The carrier is
  `PrecompiledTemplateInfo.LinePathForm`, a three-member `PrecompiledLinePathForm` enum:
  `RootRelative`, `TemplatePath`, and `Unspecified` for a row that makes no claim. Q8.27's two prose
  sentences map onto the first two members exactly, so the *decision* is untouched — only where it is
  written down changed. **An enum, not a bool**, and `Unspecified` is the reason: a fallback-marker
  entry has no generated source and therefore no `#line` directives to describe, so it must be able
  to say nothing rather than be forced to claim one of two forms. The enum file is **linked into the
  generator** rather than having its member names restated as string literals, so a rename breaks the
  generator's own compile instead of emitting a manifest that will not compile in the consumer. Both
  halves are asserted together — the form is recorded *and* the prose is absent — so an implementation
  that added the field without deleting the comment cannot pass; two carriers for one fact is the
  state this closes.
  *Where it lives:* [records.md — 2.1 row 3](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — the `#line` relativity marker moves into the manifest](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings);
  [phase 5 §Q8.30/Q8.31](phase-5-pipeline-config.md).

- **Q8.32 — `RegisteredName` is the only manifest field the gauntlet does not validate.** Two
  sub-questions: *(a)* should the gauntlet check it per request at all, and *(b)* registration
  `continue`d past a `RegisteredName` that fails `TemplateKey.TryNormalize` with **no event at all** —
  the only wholly silent drop in the registration path.
  **Resolution.** *(a) Rejected as over-engineering:* a registered name resolves to an entry whose
  every row is already gauntleted, so a per-request name check re-validates nothing. **No per-request
  gauntlet arm for `RegisteredName`.** *(b) Landed:* an unnormalizable `RegisteredName` reports
  `HED7104` / `RegisteredNameUnavailable` through the same channel the two collision arms use, so the
  registration path has no silent drop left — every discarded manifest row is now either reported or a
  throw. **One reason and one id for both causes:** a refused spelling and a collided spelling are one
  situation from the host's side — a name it expected does not resolve, and the template is still
  reachable by its key — with the same remedy, so splitting them would advertise a distinction the
  host cannot act on. The detail names **both** the refused spelling and the key of the template that
  asked for it, pinned by its own assertion. No throw (Q8.25's rule holds), and no build-tier change
  (the generator already refuses such a name with `HED7004` and so cannot emit one; this arm exists
  for manifests no build tier vetted).
  **The ruling reframed the question around a third validation stage, and the feasibility assessment
  it demanded came back split.** The proposal was that generated code bind through indirection and one
  post-configuration pass resolve every indirection at once, moving work *out* of the per-request path.
  Findings, all verified in source:
  - **The dynamic runtime does not late-bind; it is *deferred-resolution*, and the resolution point is
    sealed.** `NativeExpressionCompiler` calls `_registry.Freeze()` in its constructor, so every later
    `Register` throws. The resolved target is then baked into the expression tree —
    `Expression.Call(null, chosen.Method, …)` for methods and, decisively,
    `Expression.Invoke(Expression.Constant(chosen.Target, …))` for delegates, i.e. the delegate
    instance is a **constant, not a cell**. Extensions resolve the same way. So neither tier carries
    an indirection; they differ only in which phase performs the one lookup. Adding a re-pointable
    cell to the generator would be inventing a third binding mode and then changing the source of
    truth to match *it* — what the ruling named as disqualifying.
  - **Generated code cannot carry indirection cheaply, for three independent reasons.** Prop values
    are emitted as `PrecompiledPropSetter(<slot.Index>, …)`, so a late-resolved extension type writes
    into the wrong slots — deleting the guarantee phase 3's prop-layout fingerprint exists to provide.
    Function overload choice *and* argument casts are baked into the emitted C# text, so a cell can
    only be typed as the build-time signature and buys exactly one bit that `CheckFunctions` already
    answers. And `PrecompiledRuntime.Bind`'s pinned invariant is that the extension is never mutated
    after `Bind` returns, which is what makes the `static readonly` field and the lock-free render
    correct. Cost is stated as a shape argument and **not** a measurement — the tier has never been
    benchmarked — but what is verifiable from code is that `E0.RenderData(...)` reads a
    `static readonly` field of an exact type, the only signal making that call devirtualizable, and
    that dropping `readonly` converts every substitution site in every template to a virtual call. The
    indirection with the worst cost is the one with the least validation value.
  - **The ruling's actual goal needs no indirection**, and is a bigger win than the mechanism it
    replaces. Gauntlet steps 2 and 3 are pure comparisons of manifest facts against live registries,
    so they can run once after configuration over `PrecompiledTemplates.Entries`. Two findings make
    this decisive. First, **the typed entry point runs no gauntlet at all** — `Templates_X.Generate`
    goes straight to `PrecompiledRuntime.GenerateString`, and the gauntlet is reached only through
    `TryResolve` — so for a host on the documented *recommended* API a binding mismatch was **never
    detected**; the pass is not a re-timing, it is the only check such a host can have. (This also
    corrects the ground sub-question (a) was rejected on: "the entry's rows are already gauntleted per
    request" is true only on the dynamic call path. The rejection stands on its own terms — a name
    check re-validates nothing either way — but the coverage gap it assumed away is real and is what
    the pass closes.) Second, it genuinely removes per-request work: a precompiled hit is not entered
    into `TemplatesCache`, so validation runs on every request.
  - **The contained subset, recommended and landed:** an aggregate, host-callable
    post-configuration pass that runs the existing gauntlet over every registered entry and reports
    **all** failures at once — `PrecompiledTemplates.ValidateAll`. No generated-code change, no
    manifest row, no schema bump, no `PrecompiledRuntime` invariant touched. It shares its
    collect-don't-stop report shape with Q8.19's.
  - **Named and deliberately not started:** the `HED7014` **delegate-only** case is the one place
    indirection would strictly *add* coverage, since today a single delegate-only call makes the whole
    template fall back. It is not contained — a closure's signature is not in metadata, so the emitter
    has no return type to keep typing the expression with, and an `object`-typed unknown-return node
    cascades through operator legality, the coercion rail and member hops. A bounded slice exists
    (delegate-only calls in output-only position) but still needs an emitter change, a manifest row and
    a per-feature schema gate. **This is the "stop and report the cost" branch.**
  *Where it lives:* [records.md — 2.1 row 9](../spec/records.md#the-21-release--as-shipped-record) for
  (b); [precompilation.md — validating everything once, after configuration](../precompilation.md#validating-everything-once-after-configuration)
  and the typed-entry-point warning for the subset; registry row `HED7104`. The assessment itself has
  no other home and is recorded above. Residue: Q8.38 (closed as not a question, with its constraint
  retained).

- **Q8.33 — `PrecompiledFallbackEvent.Key` carries two different kinds of string and a host cannot
  tell which it has.** A template key for per-request reasons, an assembly name for the
  registration-time ones — pre-existing 2.0 convention, made load-bearing by `HED7104`.
  **Resolution. Split it: one field must not carry two kinds of key.** `Key` is **removed** and
  replaced by `TemplateKey` and `AssemblyName`, of which exactly one is populated, and the public
  constructor by two factories, `ForTemplate` and `ForAssembly`, so the carrier is chosen by the call
  rather than by a positional string whose meaning the reader has to look up. The "documented
  ambiguity, zero break" option was rejected: documenting an overload does not make a host able to
  branch on it, and `Detail` is a human-readable format string, not an API.
  **Removal, not narrowing.** Both options break a 2.0 host; only one of them tells it. Narrowing
  `Key` to template keys leaves a host that logged the rejected assembly silently logging null, on the
  channel whose entire purpose is that failures are not silent. Removal is a compiler error at the one
  line that has to change, with a mechanical fix.
  **The mapping is pinned from both sides.** Each factory refuses a reason belonging to the other
  carrier, both refuse a blank carrier, and the classifier behind them is an **exhaustive switch that
  throws on `default`** — so a reason added later cannot be raised at all until it has been assigned a
  carrier; the declaration side checks that classification against `Enum.GetValues` in both
  directions. Neither side can be made green by editing only the other. A reflection test also pins
  the **absence** of a `Key` member and of any public constructor: re-adding
  `Key => TemplateKey ?? AssemblyName` as a convenience would restore exactly the ambiguity this
  closes, and it compiles.
  **Deliberately not done:** no `KeyKind` discriminator (once separate carriers exist the populated
  one *is* the discriminator); no change to which reasons fire, when, with what `Detail`, or to what
  `Strict` throws; and `default(PrecompiledFallbackEvent)` still bypasses both factories, as it does
  for every value type — stated in the type's own documentation rather than defended against.
  *Where it lives:* [precompilation.md — what a fallback event names](../precompilation.md#what-a-fallback-event-names)
  (the carrier table); [records.md — 2.1 row 8](../spec/records.md#the-21-release--as-shipped-record);
  [breaking-windows — `PrecompiledFallbackEvent.Key` is removed](../spec/common/breaking-windows.md#explicit-not-window-gated-rulings)
  (a declared 2.1 break, four grounds).

- **Q8.34 — A name that resolved can stop resolving because an unrelated assembly loaded, and the
  only notice is `OnFallback`.** The eviction half of the disjointness invariant is what makes key
  precedence order-independent, so it is not in question; its *observability* is. Is this a host
  configuration error, or a legitimate late-binding outcome?
  **Resolution: that dichotomy is the wrong question, and the engine must not answer it.** Deciding
  which it is means deciding for the host when and in what order assemblies may register — the
  coupling that turns a library into a framework. The general rule is now
  [**D11 — the engine does not decide which assemblies are loaded**](../spec/common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded):
  the engine must not load or scan by default, it *may* report, and it *may* suggest an architectural
  pattern in documentation and nothing more. So `HED7104`-through-`OnFallback` is the right ceiling
  rather than a gap, and `Strict` deliberately does not extend here because `Strict`'s subject is
  degradation to the dynamic tier and no degradation occurs. **No behavioural change to the eviction
  path.**
  **The verification this ruling demanded found that the second consequence does not hold today** —
  the engine *does* auto-load, by default, and more strongly than the question anticipated. Filed as
  **Q8.37** with the full chain; the scope is precise because verification also found the rest clean:
  `PrecompiledTemplates.Register` is explicitly opt-in with no scan-all overload, there is no module
  initializer anywhere in `src/` or in generated code, the LSP never calls `Register` and loads
  through a collectible tracked context, and the typed entry point does not trigger the walk at all.
  *Where it lives:* [D11](../spec/common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded).
  Residue: the documentation pattern D11 permits is **not yet an item in any phase-8 plan**.

- **Q8.35 — `Min == Max == Current` makes the support window a single point.** Every future manifest
  change would then be a whole-assembly rejection until rebuilt — correct when a change is genuinely
  binary-breaking, unnecessarily severe when it is purely additive.
  **Resolution: the schema number tracks breakage, not releases.** The manifest schema is bumped only
  when an **already-emitted** manifest can no longer be read or bound; a purely additive change reads
  through a per-feature `PrecompiledSchema.<Feature>SchemaVersion` gate and the number stays put.
  Assembly/package version is independent — a release with no schema change is normal, not a
  contradiction. **Additivity is proved per change, never asserted:** the proof obligation is a
  fixture holding a manifest emitted *before* the change and still read correctly after it, because
  the failure mode (a constructor signature that no longer binds) is invisible in source and appears
  only in emitted IL, exactly as Q8.2's fixture had to demonstrate. Where that fixture cannot be
  produced, the change is breaking by default and the number bumps.
  This does **not** license relaxing `Min` retroactively for the schema-3 break, which stays real on
  its demonstrated `MissingMethodException`; the rule applies from here on. No code change and no
  test: the obligation attaches to a future change, so a test today would have no pre-change manifest
  to hold.
  *Where it lives:* [breaking-windows — policy item 7](../spec/common/breaking-windows.md#policy-applies-to-every-window),
  placed with the other six because it governs how every future window is *composed*.

- **Q8.37 — The engine auto-loads and auto-scans assemblies by default, which D11 forbids (defect;
  ruled, NOT implemented).** `AssemblyHelper`'s **static constructor** `Assembly.Load`s the entry
  assembly's entire transitive reference closure plus every `DependencyContext` default assembly name,
  unconditionally and with loader failures swallowed; `TemplateFactory`'s static constructor scans
  that set for `[ExportExtensions]`; the precompiled path reaches both through gauntlet step 2's
  `TemplateFactory.TryGetExtensionType`, so any precompiled template carrying at least one extension
  binding triggers it — effectively all of them. Because the scanned set decides extension **name
  ownership**, and a collision between unrelated claimants throws `TemplateOverrideException` out of a
  static constructor, an assembly the integration layer never chose to load can take a name or fail
  type initialization.
  **Resolution (ruling).** Remove the auto-loading in 2.1 and add proper extension points where they
  are missing. Not gated behind a switch and not narrowed: the static-constructor walk and the
  scan-all discovery go, and what replaces them is explicit host registration on the shape
  `PrecompiledTemplates.Register` already has. Where a host can only reach today's behaviour through
  the scan, that is a **missing extension point to add**, not a reason to keep the walk.
  **Two obligations that come with it, not optional.** The break needs a `breaking-windows.md`
  disposition stating what stops working for a host that relied on discovery and what it must call
  instead — the population is every host that declares `[ExportExtensions]` and registers nothing. And
  it must be reconciled with the README's post-implementation **finding 3** in the same change: the
  generator scans *all referenced* assemblies while the runtime scans only
  `[ExportExtensions]`-carrying ones (Q8.4 aligned the generator to the runtime's *rule*, not to a
  host-chosen *set*), and the two are halves of one seam, so fixing one side alone widens the drift.
  **Blocked:** the disposition depends on whether 2.1 opens a window — Q8.39.
  *Where it lives:* [D11's known-violation paragraph](../spec/common/cross-cutting-decisions.md#d11--the-engine-does-not-decide-which-assemblies-are-loaded);
  not yet implemented, and no window disposition written.

- **Q8.38 — Which `TemplateOptions` does a post-configuration validation pass validate against?**
  **Resolution.** Closed as **not a question**: the entry described a constraint at length and then
  offered three shapes without asking anything a ruling could answer. The constraint is real and is
  retained here — four of the gauntlet's inputs are **per-request**, not per-configuration
  (`OutputProfile`, `ExpressionMode` and `TrimDirectiveLines` in the fingerprint comparison, and
  `options.Functions` in the function check), so a pass run once after configuration can only be
  complete with respect to one options shape, and step 1 exists *because* the fingerprint is a
  per-request check and cannot simply be hoisted with steps 2 and 3. That is an **implementation fact
  for whoever builds the subset**, to be resolved by the code and pinned by a test. If building it
  turns up a decision that genuinely needs the user — an API shape, a default that could mislead —
  that gets raised then, as a question with an actual question in it.
  *Where it lives:* the constraint above; the subset is Q8.32's.

- **Q8.39 — Does 2.1 open a breaking window, or not? Two ratified records say different things.
  (AWAITING RULING)** [records.md](../spec/records.md#the-21-release--as-shipped-record) states
  plainly: *"Not a breaking window. **2.1 opens no window**: each item below is dispositioned as
  defect repair … so policy rule 1 ('one window per major') is untouched."* The **Q8.37 ruling**
  instead calls 2.1 *"the current breaking window"* and schedules the removal of assembly
  auto-loading into it — and that removal is not defect repair by the policy's own test: a host that
  declares `[ExportExtensions]` and registers nothing works today and would stop, which is a
  behaviour users can correctly depend on.
  The conflict is not cosmetic, because the two readings license different things. If 2.1 opens no
  window, the auto-load removal needs a per-item not-window-gated disposition and every *other* break
  wanting in has to argue separately. If 2.1 does open one, then policy rule 1 — one window per
  **major** — is what needs amending, since 2.1 is a minor, and the window then has a consolidated
  contents table and a migration note as deliverables (rules 2 and 4), neither of which exists.
  Three ways out, genuinely different: **(a)** 2.1 opens a window, policy rule 1 is amended to "one
  window per ratified opening" and the release gets its planning document; **(b)** 2.1 stays
  window-less and the auto-load removal is dispositioned individually, accepting a break landing
  outside a window; **(c)** the removal moves to 3.0 and 2.1 ships the explicit registration API
  additively, with discovery deprecated but still working. Recorded rather than resolved: whichever is
  chosen changes what else may land in 2.1, so it is a maintainer decision.

- **Q8.40 — A ratified testing rule forbids a cross-project output read that a ratified landing
  accepted. Which governs? (AWAITING RULING)** Raised by consolidating the register against the specs.
  [testing-standards' *Test-input single-sourcing*](../spec/common/testing-standards.md#test-input-single-sourcing)
  (ledger E9, normative) states: *"Shared test assets are reached from the consumer's own output
  directory, **never** by walking up out of `bin/<cfg>/<tfm>` into a sibling project. Path traversal
  to another project's assets encodes the configuration name, the TFM directory and the project
  nesting as assumptions, and its failure mode is a test that finds nothing and passes."*
  `CorpusResolverSweepTests` still does exactly that — `HeddleTestsDll()` string-substitutes
  `"Heddle.Generator.IntegrationTests"` → `"Heddle.Tests"` in its own assembly location, and
  `CorpusDir()` climbs `Path.Combine(dir, "..", "..", "..")` to read `TestTemplate` — and returns
  `null` on miss, which is the silent-skip failure mode E9 names. Q8.20's landing left this
  deliberately, adding only a `ProjectReference` for build ordering and recording that "the fix orders
  the build, it does not stop a test reading another project's output".
  Neither record is wrong on its own terms and they were decided independently: E9 governs *assets*
  and its remedy is phase 7's shared corpus location; Q8.20's read is of a **compiled assembly** used
  as a `MetadataReference`, which the corpus move does not address. So the question a ruling has to
  answer is whether E9's "never" covers a sibling project's *output assembly* as well as its assets,
  and if so what replaces the read — phase 7's corpus location plus a copied reference, an
  `ItemGroup` that copies the DLL into the consumer's own output, or a narrower rule that permits an
  ordered, declared read. Recorded rather than resolved because either answer changes what phase 7
  must deliver.
