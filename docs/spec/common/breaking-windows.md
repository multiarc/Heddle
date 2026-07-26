# Breaking windows — policy

The executable side of [D2](cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows):
how breaking changes are batched, executed, and recorded. This document is
instruction-only — policy plus the register of candidates for the **next** window.
Completed windows are recorded in the [historical records](../records.md), never here.

## Policy (applies to every window)

1. **One window per major, one migration per window.** All ratified breaking changes for
   a major land together; users touch their options/templates once. Between windows,
   everything ships additively (new options default to current behavior).
2. **Contents are ratified, not accumulated.** Each window item needs a maintainer-ratified
   decision record in its owning spec; the window's consolidated table, execution order,
   and whatever no single spec owns are assembled in that window's own planning document
   when the window opens — never retrofitted into this policy.
3. **Execution order discipline.** Default flips and byte-changing swaps execute together
   in the release tail, followed by exactly **one** golden re-ratification commit reviewed
   against pre-measured churn tables — goldens change once, attributably (the
   [testing standards](testing-standards.md#regression-gates)' fix-forward rule).
4. **The migration note is a deliverable.** One page shipped with the release notes:
   what changed / how to keep old behavior, per item — nothing else. Folding it into the
   release's CHANGELOG entry satisfies this.
5. **As-shipped verification.** After the release, the window is reconciled against the
   shipped source and the result recorded in the [historical records](../records.md);
   items that slipped are dispositioned through the
   [amendments ledger](cross-cutting-decisions.md#cross-spec-amendments-ledger) — a
   window item is never silently assumed shipped.
6. **The next window has a register, not a plan.** Breaking candidates discovered between
   windows accumulate in the register below, each with the trigger/precondition that
   would let a ratified window adopt it. The register becomes a window only by an
   explicit maintainer decision to open one; adding a row requires a decision record in
   an owning spec.
7. **The manifest schema version tracks breakage, not releases.** `PrecompiledSchema`'s
   number is bumped only when an **already-emitted** manifest can no longer be read or can no
   longer bind — i.e. when the change is genuinely breaking and old assemblies must be rejected
   at the gate and rebuilt. A purely **additive** change does not bump it: the new field is read
   through a per-feature `PrecompiledSchema.<Feature>SchemaVersion` gate (the shape
   `RegisteredNameSchemaVersion` establishes), and a manifest emitted before the change simply
   carries no value for it. A release with no schema change is normal, not a contradiction —
   package version and schema version are independent, and conflating them is what produced
   three unreleased schema numbers for internal churn in the 2.1 cycle.

   **Additivity is proved per change, never asserted.** The proof obligation is a fixture holding
   a manifest emitted *before* the change and still read correctly after it — a reviewer's
   judgement that a change "only adds a field" is not evidence, because the failure mode
   (a constructor signature that no longer binds) is invisible in source and appears only in the
   emitted IL. Where the fixture cannot be produced, the change is breaking by default and the
   number bumps.

## Explicit not-window-gated rulings

Recorded so a *narrowing* change that touched runtime behaviour is not mistaken later for an
unrecorded breaking change.

- **Short-name type resolution: the order-dependent silent pick became the ambiguity error**
  (phase 3, Q3.5 / OQ5's escape clause; `ReflectionHelper.ResolveSimpleType`, 2026-07-26).
  A bare type name matched by several types used to be resolved with
  `types.FirstOrDefault(t => imports.Contains(t.Namespace))` — a first-match pick over an
  assembly-scan-ordered list. Where **two or more** imported namespaces both declared the name,
  which type won depended on the order `Assembly.GetTypes()` happened to yield them. That arm now
  raises the same `"the type name is ambigous"` error the dotted/full-name arms have always
  raised. One import matching still resolves; no import matching still fails as before.
  **Judgement: defect repair, not window-gated.** (a) The behaviour it replaces is
  non-deterministic across runs and deployments, so no user could *correctly* depend on it —
  the [policy](#policy-applies-to-every-window)'s test for a breaking change. (b) The file's own
  `RegisterType` comment already documented the intended contract as "surfaces as the existing
  *ambiguous* error rather than a silent pick"; the fix makes the code match its stated contract
  on the arm where it did not. (c) It **narrows** — it converts a silent wrong-type bind into a
  positioned error — and the register's widening test does not apply. (d) OQ5 required the two
  tiers to stay matched, and the build tier cannot reproduce an assembly-load-order pick by
  construction; reproducing it would bake load order into build output. The generator raises
  `HED7023` for the same input, and the two-driver corpus
  (`TypeSpellingLockstepTests` / `TypeSpellingSymbolLockstepTests`) pins the agreement.

- **The generator stops precompiling extensions the host never exported** (phase 3, Q8.4;
  `ExtensionBinder.CollectExported`, 2026-07-26). The runtime registers extensions from the engine
  assembly and, beyond it, only what an `[assembly: ExportExtensions(...)]` attribute names; the
  generator scanned every referenced assembly. **Judgement: defect repair, not window-gated**, on
  three grounds. (a) No template that renders today stops rendering: a bound-but-unexported
  extension produced a manifest row naming an extension the live registry cannot resolve, so the
  gauntlet already rejected the entry on *every* request and the dynamic tier already served it. The
  change removes dead precompiled output and a permanent per-request fallback; it does not change a
  rendered byte. (b) Where the build verdict *does* change — a **bodied** call to an unexported name
  is now `HED7006` at Error, where the build previously succeeded — the template was already broken
  at run time: `TemplateFactory.Create` raises `HED0002` for exactly that name. Converting a
  first-render failure into a build failure is the match principle, and is the same argument phase 3
  made for the false-`HED7006` fix in the opposite direction. No *correct* build regresses. (c) A
  **bodiless** call degrades quietly instead of precompiling, which is a tier change with identical
  bytes. Hosts that intended those extensions to be live were always required to export them; the
  fix makes the build say so.

- **The overload-ambiguity silent degrade became a build error** (phase 4, Q8.1; `HED7025`,
  `DefaultFunctionBinder`/`ExportFunctionBinder`, 2026-07-26). A template calling an ambiguous or
  inapplicable function overload — `@(min(1, 2u))` — used to build **green with zero diagnostics**
  and fail at first render with `HED1013`. The generator had already *computed* the illegality
  (`BindOutcome.Ambiguous` out of the shared `OverloadRank` core) and then reported nothing. It now
  raises `HED7025` at Error. **This is a build-surface change: a project that builds today will
  start failing its build**, which is why it is argued here rather than assumed away by the ruling.
  **Judgement: defect repair, not window-gated**, on four grounds.
  (a) The [policy](#policy-applies-to-every-window)'s test is whether a user could *correctly*
  depend on the old behaviour. No one could: the affected template **has never rendered**. The
  build produced no precompiled entry for it, the dynamic tier compiled it, and the dynamic compile
  fails with `HED1013`. The only thing the old behaviour delivered was the *timing* of the failure —
  first render instead of build — and later, less informative failure is not a contract.
  (b) It changes **no rendered byte**, for any template, on either tier. The refusal is unchanged
  (the template still degrades); what changed is that the build now says why. This is exactly the
  argument phase 3's Q8.4 row makes for its `HED7006` half: "converting a first-render failure into
  a build failure is the match principle… no *correct* build regresses."
  (c) It **narrows**, so the register's widening test does not apply. The widening in this area —
  C# betterness in the runtime binder, which would make 82 today-ambiguous combinations *bind* — is
  a separate, still-unratified [candidate](#next-window-candidate-register) and is unaffected: it
  remains a joint-land change inside a window, and when it lands the same templates stop being
  ambiguous on both tiers at once, so `HED7025` simply stops firing for them.
  (d) The alternative — a *warning* — was considered and rejected on the ratified rulings rather
  than on taste. `HED7014`'s warning is legitimate because the build refuses something it cannot
  *decide*; here the build has a proof. The match principle says errors always match, and this is
  an error on the run tier.
  **The bounded residue, recorded rather than glossed:** the proof is relative to the function
  inventory visible at build time. A host may add an overload at run time through
  `TemplateOptions.Functions.Register`, which can turn an ambiguous set unambiguous or an
  inapplicable one applicable — so a template that is legal *for that host* now fails the build.
  The generator has no way to see a delegate registration (the same blindness `HED7014` exists
  for), and the gauntlet's `FunctionBindings` overload-count check, which catches this skew for
  *emitted* code, cannot rescue a build error. Filed as an open question (Q8.18) rather than
  treated as settled; the escape hatch today is `Precompile="false"` on the item.

- **Type-spelling parity from folding the runtime onto the shared parser** (phase 3, Q8.3;
  `ReflectionHelper.ResolveType` / `TypeSpelling`, 2026-07-26). Two spellings changed, both
  **widenings**, neither window-gated. (a) `(int)` — a one-element tuple — now resolves on the
  *build* tier as `ValueTuple<int>`, which the run tier has always resolved; the shared parser's
  two-element floor was a refusal the extraction itself introduced, so this is drift repair toward
  the runtime. (b) A whitespace-padded top-level spelling (`" int "`) now resolves on the *run* tier
  where its own dispatch used to throw, because the shared parser trims every recursion. No resolved
  type changes on either tier, and depending on `ResolveType` *throwing* for padded input is not a
  dependency the contract offers.

- **The precompiled schema floor rises 1 → 3, and the unreleased schemas collapse into one** (phase 5, Q8.2 as
  corrected; `PrecompiledSchema.MinSupportedSchemaVersion`, **ships in 2.1.0 — not yet shipped**). A manifest built
  by a 2.0.x generator is no longer accepted: registration reports `SchemaVersionUnsupported` (`HED7102`) once and
  every template in that assembly renders through the dynamic path.

  **This entry replaces an earlier version of itself that was factually wrong, and the correction is the point.**
  The superseded text argued the break was "already shipped, in 2.0.0, because schema 4's optional third parameter
  removed the two-argument constructor then". Verified against the `v2.0.0` tag, that is false in every particular:
  the shipped generator emitted `schemaVersion: 2`, the shipped engine accepted `Min = 1, Max = 2` (as two private
  consts — `PrecompiledSchema.cs` did not exist yet), and the shipped `PrecompiledExtensionBinding` had a **real
  two-argument constructor** which the shipped generator called. **Schemas 1 and 2 are the only schemas that have
  ever shipped.** Schema 3, 4 and 5 were all unreleased, so the constructor break has *not* happened in any release:
  it is a genuine, pending 2.1 binary break, which is the opposite of what the old disposition claimed. A normative
  document must not carry a false premise, so the sentence is replaced rather than annotated.

  **The collapse.** Because nothing above schema 2 was ever observable, the three unreleased increments
  (dynamic-member routing at 3, the prop-layout row at 4, the per-carrier `BindDefinition` overload at 5) are
  collapsed into **one**: schema **3** carries all of them, plus Q8.30's registered name and Q8.31's `#line` path
  form. Carrying three increments would advertise a migration history no user could have had, and would leave the
  support window claiming to read manifest shapes no generator ever emitted. `Min = Max = Current = 3` — the window
  is a point: every schema below it is a released shape whose IL is unrunnable, and nothing above it exists.

  **Judgement: still not window-gated, but on repaired grounds** — the old (a) is withdrawn, and the remaining two
  carry it.
  (a) *Withdrawn.* The break has not shipped. It lands in 2.1, declared.
  (b) **What is being withdrawn was never a working capability.** With `Min = 1` the gate *accepts* a released
  manifest and the fault then lands as a `MissingMethodException` out of `PrecompiledTemplates.Register` — a
  host-startup crash, neither a degrade nor a render, and not something a user could correctly depend on, which is
  the [policy](#policy-applies-to-every-window)'s test. `Min = 1` advertises a support window the metadata cannot
  honour; the change retracts a false claim rather than removing something that worked. `3` is exactly the boundary
  (1–2 were built against two arguments, 3 against three), so no runnable manifest is excluded.
  (c) **No rendered byte changes, on either tier.** A rejected assembly falls back to the dynamic engine, which is
  byte-identical by design. Under `PrecompiledMismatchPolicy.Strict` a deployment that must never pay
  dynamic-compile cost throws instead — the documented, chosen posture for exactly this case.
  **Accepted consequence, recorded rather than argued away:** a project precompiled by a 2.0.x generator and not
  rebuilt loses precompilation (or throws under `Strict`). This is a real cost to a real population — every 2.0
  consumer — and it is accepted because the alternative is a startup crash. `Heddle.Generator` and `Heddle` were
  already documented as version-locked. No compatibility shim: restoring a real two-argument overload would keep the
  faulting set *accepted*, which is the state being fixed. Demonstrated by `OldSchemaManifestRejectionTests`, which
  builds manifests whose IL genuinely names the absent constructor at **both** released schemas and shows the clean
  rejection, plus a control arm admitting the same bytes at `Min` to show the crash the gate prevents — the earlier
  "old manifest" test constructed its binding through the optional parameter, so it exercised a *new*-schema call
  and could never have caught this.

- **The per-item `HeddleTemplate` metadata started working** (phase 5, Q8.12; `Heddle.Generator.targets`,
  shipped 2.1.0). `Key`, `Name` and `Precompile` were all inert from a real project: the targets restated each
  one as `<Key>%(HeddleTemplate.Key)</Key>` inside an `Include="@(HeddleTemplate)"` transform, and outside a
  target a cross-item `%()` reference evaluates to the empty string — so each element overwrote the value the
  transform had just copied. Only the generator suites, which inject `build_metadata.*` directly, ever saw the
  metadata at all. **Judgement: defect repair, not window-gated.** (a) No documented behaviour is withdrawn;
  three documented behaviours begin to occur. (b) No rendered byte changes: `Key` changes a registration key and
  the generated class name, `Name` adds an `@<<` import spelling, `Precompile="false"` moves a file to the
  dynamic path, and the dynamic and precompiled tiers are byte-identical. (c) **A build can newly fail, and that
  is stated, not glossed:** a project that set `Key` and called the generated entry class by its old
  path-derived name will not compile until the call is renamed. A project relying on documented metadata being
  *ignored* is not a dependency the contract offers.

  **This disposition was corrected (Q8.25).** As first written it read "a project that set `Key`/`Name` and
  calls the generated entry class by its old name will not compile until renamed", and cited
  `samples/codegen-t4-successor` as the instance. That was true of an implementation in which `Name` *overrode*
  the key — which is what shipped first and was wrong. With `Name` additive, **`Name` moves nothing**: the key,
  the manifest row and the generated class name are all unchanged by it, so it cannot break a call site and
  cannot make an import stop resolving. The rename clause now applies to `Key` alone, and the sample no longer
  needs one. `Name`'s only new emission is `HED7028`, a **warning** where a named template is imported by its
  key — additive by construction, since no pre-existing project sets `Name` at all and the build has no
  `TreatWarningsAsErrors` (Q8.26). The superseded sentence is replaced rather than annotated in place: a
  normative document must not carry two dispositions for one change.

- **The precompiled registry answers to a registered `Name`** (phase 5, Q8.30; `PrecompiledTemplates`,
  `PrecompiledTemplateInfo.RegisteredName`, ships in 2.1.0). A template built with `Name="BuildReport"` now resolves
  by that spelling at run time as well as at build time: the manifest row carries the name and `TryGet`/`TryResolve`
  consult a name index after the key index.
  **Judgement: additive, not window-gated — and the widening question is answered rather than waved past.** It *is* a
  widening: a lookup string that missed before can now hit, and that acceptance cannot be withdrawn later without a
  window. Three grounds for taking it anyway.
  (a) **The widening is scoped to opt-in data that no existing project has.** The new resolution path exists only for
  a template whose item carries `Name`, and `Name` did nothing at all from a real csproj until 2.1 (Q8.12's targets
  defect: every per-item metadatum was overwritten with `""`). So the population whose lookups change behaviour is
  exactly the population that adds the metadatum in 2.1 or later — there is no 2.0 project whose resolution can move.
  (b) **Nothing that resolved before stops resolving, and this is structural rather than tested-for.** Keys are
  consulted first and the two indexes are kept disjoint, so a name can neither displace a key nor shadow one. The
  ordering is the same one the build tier's import map has used since Q8.25, which is why the tiers cannot disagree.
  (c) **No rendered byte changes.** A name resolves to the same entry the key resolves to — the same
  `IProcessStrategy`, validated by the same gauntlet.
  **The shadowing hazard, stated plainly because it is the one real risk:** a host that had a template keyed
  `shared/banner.heddle` and a *different* template named `shared/banner.heddle` would, before 2.1, see the name do
  nothing; after 2.1 the name is a live spelling. It still cannot take the key's spelling — the key wins, and the
  colliding name is dropped with an `HED7104` callback — so the hazard is *reported*, not silent, and the resolved
  template is unchanged. What a host cannot do is rely on a `Name` being ignored, which is not a dependency the
  contract offers (the same reasoning Q8.12 used for the metadata starting to work at all).

- **The `#line` relativity marker moves from generated code into the manifest** (phase 5, Q8.31;
  `PrecompiledTemplateInfo.LinePathForm`, ships in 2.1.0). The comment line Q8.27 emitted under
  `// <auto-generated/>` is deleted; the same fact is a manifest field a tool can read.
  **Judgement: not a breaking change, and not behavioural at all.** (a) The removed line is a **comment in generated
  code** — not public API, not rendered output, and not something a program could have consumed, which is precisely
  why Q8.31 judged it the wrong carrier. (b) Generated-source goldens move by exactly one deleted line (five Verify
  snapshots and the sample golden), reviewed as part of this change. (c) No rendered byte changes: the sample's
  `codegen-output.txt` is unchanged, which is the assertion that the emitted *code* changed and the emitted *output*
  did not.

- **An opted-out item's key/name metadata is validated, and its imports advised** (phase 5, Q8.28 / Q8.29;
  `HeddleTemplateGenerator`, ships in 2.1.0). A `Precompile="false"` item now raises the same `HED7004` a precompiled
  item would for a malformed `Key`, a malformed `Name` or an already-taken `Name`, and its own `@<<` imports can draw
  the `HED7028` advisory.
  **Judgement: defect repair, not window-gated — with one honest caveat.** (a) **A build can newly fail**, and that is
  the point rather than a side effect: the fault being reported was always real, and reporting it *nowhere* meant the
  author instead met `HED7011` at an innocent importer, pointing at the wrong file. A project relying on a broken
  metadatum being unreported is not a dependency the contract offers — and again, no 2.0 project could have set the
  metadata at all. (b) **`HED7028` is a warning**, and the build has no `TreatWarningsAsErrors` (Q8.26), so the
  advisory cannot fail anything. (c) **The opt-out's contract is unchanged and pinned**: an opted-out item still
  contributes no entry point and no manifest entry.
  **Deliberately not done, and recorded so it is a choice rather than an oversight:** an opted-out item's *template*
  errors — a missing import, a parse error — stay unreported. The ruling asks for its metadata validated and its
  imports advised; promoting every opted-out file's parse diagnostics to build errors would red previously-green
  builds over templates the author explicitly told this build not to compile, which is a far larger change than was
  ruled. Those faults are not forgiven: the moment a precompiled template imports the file, the importer's parse pulls
  the same content through the same channels and raises them.

- **`PrecompiledFallbackEvent.Key` is removed and replaced by two carriers** (phase 5, Q8.33;
  `PrecompiledFallbackEvent`, **ships in 2.1.0 — not yet shipped**). The event's single `Key` property held a
  *template key* for the per-request reasons and an *assembly name* for the registration-time ones
  (`SchemaVersionUnsupported`, `EngineVersionIncompatible`, `RegisteredNameUnavailable`). It is replaced by
  `TemplateKey` and `AssemblyName`, exactly one of which is populated, and the public constructor by the two
  factories `ForTemplate` / `ForAssembly` which enforce that mapping. **This is a real binary break on shipped 2.0
  public API**, stated as such rather than argued away: a host that reads `event.Key` will not compile against 2.1.

  **Judgement: a declared 2.1 break, not window-gated** — on the same footing as Q8.2's schema-floor rise, and on
  four grounds.
  (a) **The alternative is the worse break, not a smaller one.** Keeping `Key` and narrowing its meaning to template
  keys is a *behavioural* break with no compile-time signal: a 2.0 host that reads `Key` to log which assembly was
  rejected would silently start logging null, on the diagnostic channel whose entire purpose is to stop failures
  being silent. Removal converts that into a compiler error at the one line that has to change, and the fix is
  mechanical (`Key` → `TemplateKey` or `AssemblyName`, chosen by which reasons the host handles). The
  [policy](#policy-applies-to-every-window)'s test — could a user *correctly* depend on the old behaviour — cuts
  both ways here, so the tie is broken by which break a user can *see*.
  (b) **The affected population is already rebuilding for 2.1, and is the same population.** The only way to observe
  this type is to consume the precompiled tier, and Q8.2's floor rise already requires every 2.0-precompiled
  assembly to be rebuilt against the 2.1 generator or lose precompilation. There is no host that keeps working
  across 2.1 *and* reads this property.
  (c) **No rendered byte changes, and no fallback decision changes.** Which reasons fire, when they fire, what
  `Detail` says, and what `Strict` throws are all untouched; only the shape of the payload handed to `OnFallback`
  moves. `PrecompiledMismatchException` is unaffected — it never carried the union field.
  (d) **The mapping is enforced from both sides, so the split cannot silently re-collapse.** `ForTemplate` refuses a
  registration-time reason, `ForAssembly` refuses a per-request one, both refuse a blank carrier, and the classifier
  behind them is an exhaustive switch that throws for a reason nobody has classified — so a reason added later
  cannot be raised until it has been assigned a carrier. The declaration side is
  `PrecompiledFallbackCarrierTests`, which checks that classification against the whole enum in both directions and
  pins the *absence* of a `Key` member by reflection, so a well-meaning convenience re-addition reds.
  **Cost, as expected and reviewed:** the public-surface golden (`public-api-heddle.txt`) moves by exactly the four
  lines of this change and nothing else, and `FallbackGuard` (generator integration suite) collapses the two
  carriers for *display and matching only*, which is a test-side choice — the engine keeps them apart.

- **The release line is stated once, and every first-party assembly is signed** (phase 5, Q8.11;
  `Directory.Build.props`, `Directory.Build.targets`, shipped 2.1.0). Not a behavioural change and recorded
  only because it moves a shipped surface: nine per-project `<Version>` elements collapse into one
  `<VersionPrefix>`, the CI beta job's `--version-suffix` loses a leading dash it could no longer carry, and
  `Heddle.Demo.Models`/`Heddle.Demo.Wasm` gain strong names. Public-API and byte impact: none. The one
  user-visible correction is that `heddle-lsp --version` and the LSP `initialize` response stop reporting
  `1.0.0`, which they had done for the whole 2.0 line.

## Next-window candidate register

Nothing here is ratified. Rows are appended as owning specs record them and removed only
when a ratified window adopts them (recording the adoption) or an owning spec withdraws
them (recording the withdrawal in the ledger).

| Candidate | Precondition / trigger | Owning record |
| --- | --- | --- |
| Default encoder swap (`WebUtility` → `HtmlEncoder.Create(UnicodeRanges.All)` or a successor choice) | `TemplateOptions.Encoder` shipped in 2.0.0; soak it, so hosts that need byte-exact output have a pin available before the default moves | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (item 2) |
| `AllowCSharp` property removal | `[Obsolete]` shipped in 2.0.0; soak it, and confirm `ExpressionMode` usage dominant in first-party surface and docs | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (item 4) |
| `[NotEncode]` type deletion | Revisit only inside a ratified window | The [2.0 record](../records.md#the-20-breaking-window--as-shipped-record) (exclusions) |
| **C# betterness in the runtime overload binder** — replace the flat Pareto rank with C#'s better-conversion-target rule (plus validations preserving the documented deviation set), landed **jointly** on both tiers so the generator matches by construction and the cast-pin emit guard mostly retires | Quantified and ready (phase 4 WI10): over the shipped built-in table, **0 of 480** argument combinations change their winning overload, **82** become bindable that are ambiguity errors today, and **62** stay ambiguous (`double`/`decimal` are mutually non-convertible, so neither target is closer) — so it is a pure widening with no rendered-byte change for any template that compiles today, but it *is* a widening: templates that error today would start rendering, and that acceptance cannot be withdrawn later. Trigger: adoption inside a ratified window, together with the extra validations for deviations 4–7 (whose enforcement the flat rank currently gets for free from ambiguity) and a decision on the residual `double`/`decimal` ambiguity's diagnostic | [phase 4 — expression-writers](../../generator_plan/phase-4-expression-writers.md) (D10 amendment / WI10), from the Q4.2(b) ruling; measured by `OverloadBetternessEvaluationTests` |
| **Member-visibility widening** — accept a `protected internal` getter (the plain-English reading of the sandbox's "public-or-internal" contract), surface inherited non-public and base-interface members | Phase 4 WI6 pinned today's narrow runtime behavior as normative (OQ1/Q3.1) and encoded it in one function (`MemberVisibility.IsAccessible`), so any of these becomes a one-line policy change plus a conformance-corpus row flip. Trigger: a ratified window, landed jointly on both tiers with a spec-wording update to `native-expressions.md`'s sandbox section | [phase 4 — expression-writers](../../generator_plan/phase-4-expression-writers.md) (D7 / OQ1) |
| **Dynamic-vs-typed visibility asymmetry** — the typed member tier accepts an `internal` getter regardless of assembly while the dynamic tier (binding in `Heddle`'s context) cannot see a foreign `internal` member | Phase 4 WI9 made the dynamic-tier binder context exist exactly once (`PrecompiledRuntime.DynamicMember`), so harmonizing the two tiers is now a single decision rather than two. Trigger: a spec clarification deciding *which* tier is right, then a window | [phase 4 — expression-writers](../../generator_plan/phase-4-expression-writers.md) (D11 / OQ3) |
| **Extension-name collision between unrelated types** — the runtime's `TemplateFactory.AddExtensions` throws `TemplateOverrideException`; the build tier degrades the call to dynamic with a recorded reason instead of erroring | Phase 3 landed the full runtime precedence in one shared rule-core (`ExtensionRegistrationRules`), so making the build tier error too is a one-line verdict change. Deliberately *not* done now: it is a host wiring error the build has no business failing on before the host is even assembled, and the runtime already surfaces it at first render | [phase 3 — binding-layer](../../generator_plan/phase-3-binding-layer.md) (Q3.3 / OQ3) |
| Must-surface precompiled mismatches stop degrading silently under the **default** policy (`ExtensionBindingMismatch`, `FunctionBindingMismatch` per-request; `SchemaVersionUnsupported`, `EngineVersionIncompatible` at registration), with an explicit opt-out policy value preserving today's blanket degrade | The fallback taxonomy ships first (done — [precompilation.md](../../precompilation.md#which-fallbacks-are-legitimate)); the two binding classes are provisional until phase 3 finishes its binding work, since a residual mismatch is only conclusive evidence of skew once the generator binds through the full runtime replacement precedence | [phase 5 — pipeline-config](../../generator_plan/phase-5-pipeline-config.md) (D12b), from the Q2.2 fallback-legitimacy ruling |
