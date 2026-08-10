# Breaking windows — policy

The executable side of [D2](cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows):
how breaking changes are batched, executed, and recorded. This document holds the
policy, the register of candidates for the **next** window, and the running record of the
**current open** window. Completed windows are condensed into
[cross-cutting-decisions.md § Release records](cross-cutting-decisions.md#release-records--as-shipped)
once reconciled against the shipped source.

## Policy (applies to every window)

1. **One window at a time, one migration per window.** All ratified breaking changes for
   a window land together; users touch their options/templates once. Between windows,
   everything ships additively (new options default to current behavior).

   **A window is opened by an explicit maintainer decision, not by a version component.** This rule
   originally read "one window per *major*", which was corrected when 2.1 was ratified as a window
   (2026-07-26): a minor may carry one, and the version number is a consequence of the window's scope
   rather than the thing that licenses it. What the scope decides is what may enter — 2.1's is **binary
   changes and minor API changes or additions**, and a *substantial* API change is refused entry and
   held for a major rather than argued in.
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
   shipped source and the result recorded in
   [cross-cutting-decisions.md § Release records](cross-cutting-decisions.md#release-records--as-shipped);
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
  *emitted* code, cannot rescue a build error. The escape hatch is `Precompile="false"` on the item.
  **The residue is bounded, and the question it was filed as (Q8.18) is closed rather than
  outstanding:** precompiled function calls are **statically bound at build time** — the emitted call
  names the chosen method directly and nothing in `PrecompiledRuntime` consults `options.Functions` at
  render — so the build-time inventory is not merely *a* scope for the proof, it is the only scope
  that can be correct for what precompiles. A host that registers extra overloads is served by the
  dynamic tier through the gauntlet's function-binding check. See
  [precompilation.md](../../precompilation.md#functions-in-precompiled-templates).

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

- **A `@using` alias, a `using static` and `global::` start binding types** (`ReflectionHelper.ResolveSimpleType`,
  `SymbolTypeIndex.TryResolve`, `UsingDirectives`, 2026-08-01). A `@using` body is the header of a C# using
  directive, and two of its three forms bind a name rather than open a namespace. Both tiers collected every body
  into one list of namespace strings and then looked for a match in it, so an alias and a `using static` were legal
  and completely inert: `@using(){{X = System.Linq}}` + `@model(){{X.Enumerable}}` threw on the engine and was
  `HED7007` on the build. Four spellings now resolve — a namespace alias qualifying a type, a type alias used alone
  or reaching a nested type, `using static` contributing the target's nested types, and the `global::` qualifier.
  **Judgement: additive, not window-gated — and the widening question is answered rather than waved past.** It *is* a
  widening: spellings that were refused now bind, and that acceptance cannot be withdrawn later without a window.
  (a) **Nothing that resolved before can move, structurally rather than by testing for it.** The directive arms were
  ordered *after* the assembly/symbol index, so they ran only where the index already had no answer; `global::` is
  ordered before it, and is a qualifier no index key carries, so it has never resolved to anything either.
  **Amended 2026-08-01:** the one visible consequence of that ordering was that where a spelling answers to both an
  alias and an ordinary import the *import* kept it, which is the opposite of C#'s precedence. That deviation has
  since been closed — the alias arm now binds the head *before* the index and commits, matching C# — so the arms are
  no longer additive by construction on that axis. The move is pinned by `NameLookupPrecedenceOracleTests`, which
  asks the C# compiler what it binds rather than restating a rule.
  (b) **No rendered byte changes for any template that renders today**, on either tier: every affected template was
  refused by both.
  (c) **The two tiers reach the same answer or the build degrades.** The arms are mirrored one for one and pinned by
  `TypeSpellingLockstepTests` / `TypeSpellingSymbolLockstepTests` over the same spelling set, and end to end by
  `AliasTypeResolutionTests`, which renders each new spelling through both tiers and compares bytes.
  **Deliberately not covered, and recorded so the gap is a choice:** static *member* access (`using static
  System.Math;` then `Max(a, b)`) is a different subsystem and is untouched; an alias target carrying type arguments,
  an alias target with interior whitespace, and an extern-alias qualifier are each left classified as a plain
  namespace body, which is inert.

- **A `using` namespace import stops exposing the namespaces nested inside it** (`ReflectionHelper`,
  `SymbolTypeIndex.TryResolveThroughImports`, 2026-08-01). Resolving a dotted spelling through an import was a
  string retry — `import + "." + typeName` looked up in the name index — which reached anything under the import at
  any depth. C# imports the types *declared in* a namespace and not the namespaces nested in it, so
  `@using(){{A}}` + `@model(){{Sub.Deep}}` resolved here and is `CS0246` there. The retry now counts a candidate
  only when its own namespace **is** the import. A nested *type* reports its outer type's namespace on both tiers
  (`Type.Namespace`, `ITypeSymbol.ContainingNamespace`), so `@using(){{A}}` + `A.Outer`'s `Outer.Inner` keeps
  resolving; a nested *namespace* does not.
  **Judgement: defect repair, not window-gated — and the narrowing is stated rather than waved past.** It *is* a
  narrowing: spellings that resolve today stop resolving, on both tiers at once.
  (a) **The rule was established by compiling and executing the same spellings, not recalled.** Seven spellings
  measured divergent (`TieAlpha.TieProbe` under `using Heddle.Tests`, `Text.StringBuilder` under `using System`,
  and deeper forms); the nested-*type* and namespace-*alias* neighbours measured as agreeing and are unchanged —
  an alias names the namespace itself, so `@using(){{X = A}}` + `X.Sub.Deep` still binds. The rows are in
  `NameLookupPrecedenceOracleTests`, which asks Roslyn for each answer rather than asserting a remembered one.
  (b) **Both tiers move together, so it is not tier drift**; they already agreed on the old behaviour, which is why
  24 differential cycles scored it green. The arms are mirrored one for one.
  (c) **The cost is real and is stated, not implied away.** No checked-in template, corpus fixture or sample carries
  an affected spelling; one integration test did (`@using(){{Heddle.Generator.IntegrationTests}}` +
  `@model(){{Fixtures.Cart}}`) and now pins the refusal beside a nested-type spelling that still resolves.
  A host template relying on the old reach recovers by importing the inner namespace, aliasing it, or spelling the
  type in full — all three of which work on both tiers today.

- **The release line is stated once, and every first-party assembly is signed** (phase 5, Q8.11;
  `Directory.Build.props`, `Directory.Build.targets`, shipped 2.1.0). Not a behavioural change and recorded
  only because it moves a shipped surface: nine per-project `<Version>` elements collapse into one
  `<VersionPrefix>`, the CI beta job's `--version-suffix` loses a leading dash it could no longer carry, and
  `Heddle.Demo.Models`/`Heddle.Demo.Wasm` gain strong names. Public-API and byte impact: none. The one
  user-visible correction is that `heddle-lsp --version` and the LSP `initialize` response stop reporting
  `1.0.0`, which they had done for the whole 2.0 line.

## Current window — 2.1 (open; as implemented, pending release)

**2.1 is a ratified breaking window** (maintainer ruling, 2026-07-26), scoped to **binary changes and
minor API changes or additions**. A *substantial* API change is what would call for 3.0, and that is
explicitly not on the table; anything of that size is refused entry to this window rather than being
argued into it.

**This paragraph previously read "2.1 opens no window"**, on the reasoning that each item was defect
repair and policy rule 1 ("one window per major") therefore stayed untouched. That was superseded: a
window is opened by an explicit maintainer decision, not by which version component carries it, and rule
1's wording is what gives way. The individual not-window-gated dispositions
[above](#explicit-not-window-gated-rulings) remain accurate about
*why each change is safe* — they simply no longer have to carry the weight of keeping the release
window-less.

Policy rule 5's as-shipped reconciliation still governs: the window ships a binary break in the
precompiled-manifest contract, and reconciliation is the only mechanism that keeps such a thing from being
assumed rather than verified.

**A correction, recorded here because it is exactly what that mechanism is for.** This paragraph previously
read that the break "had already occurred in 2.0.0's metadata". It had not. Checked against the `v2.0.0`
tag: the shipped generator emitted `schemaVersion: 2`, the shipped engine accepted `1–2`, and
`PrecompiledExtensionBinding` still had the real two-argument constructor. The break is **pending** and lands
in 2.1 — see row 1. The claim survived several documents because it was reasoned from the current source
rather than from the release, which is the failure mode rule 5 exists to catch.

**Window status: open and ratified** (binary + minor API scope). **Release status: pending** — recorded at implementation time;
reconcile against the shipped source when `v2.1.0` is tagged, then condense the reconciled record into
[cross-cutting-decisions.md § Release records](cross-cutting-decisions.md#release-records--as-shipped).

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 1 | Precompiled manifest schema floor and window (Q8.2, **corrected**) | `MinSupportedSchemaVersion = 1`, `Max = 2`, and the generator emitted `schemaVersion: 2` — **verified against the `v2.0.0` tag**, where `PrecompiledSchema.cs` did not yet exist and `PrecompiledExtensionBinding` still had a real two-argument constructor that the shipped emitter called. Schemas 1 and 2 are the only schemas that have ever shipped | ✅ `Min = Max = Current = 3` ([PrecompiledSchema.cs](../../../src/Heddle/Precompiled/PrecompiledSchema.cs)). Two parts. **(a) The floor rises 1 → 3**, excluding exactly the released set: 2.1 removes the two-argument constructor (the prop-layout fingerprint lands as an optional third parameter), so a schema 1–2 manifest's IL names a member that no longer exists, and a floor of `1` would accept it and then fault with `MissingMethodException` out of `PrecompiledTemplates.Register`. A 2.0.x-precompiled assembly now degrades cleanly with one `HED7102` (`SchemaVersionUnsupported`) callback, or throws under `PrecompiledMismatchPolicy.Strict`. No compatibility shim, by ruling. **(b) The three unreleased increments collapse into one**: what had been 3 (dynamic-member routing), 4 (prop-layout row) and 5 (per-carrier `BindDefinition`) are all schema 3, which also carries Q8.30's `RegisteredName` and Q8.31's `LinePathForm` — an increment no user could observe is not a migration step, and three would advertise a history that never existed. **The superseded record's reasoning was false and is withdrawn, not softened:** it claimed the constructor break "already shipped in 2.0.0 because schema 4's optional parameter removed the ctor then". Schema 4 never shipped; the break is pending and lands here. Demonstrated by `OldSchemaManifestRejectionTests` over both released schemas, against a reference facade under the real assembly's identity — the superseded pin constructed its binding through the optional parameter and therefore exercised a *new*-schema call |
| 2 | `<HeddleTemplate>` per-item metadata (Q8.12) | `Key`, `Name` and `Precompile` all inert from a real project: the targets restated each inside an `Include="@(HeddleTemplate)"` transform, and outside a target a cross-item `%()` reference evaluates to `""`, so each element overwrote the value the transform had copied. `Name` had additionally been *deleted* from the props by phase 5, on a review record that overreached its ask | ✅ All three effective ([Heddle.Generator.targets](../../../src/Heddle.Generator/build/Heddle.Generator.targets)). `Name` restored — first as a second spelling of `Key`, i.e. an override, which broke path imports and was **corrected under Q8.25** to be **additive**: the template keeps its key and gains the name, both spellings resolve, and importing a named template by its key warns (`HED7028`) instead of failing. `Name` therefore takes no part in `HED7002`/`HED7003` and does not suppress `HED7018`; `HED7004` keeps a `Name` arm for an unusable or already-taken name. `samples/codegen-t4-successor` demonstrates it with a named import-only partial; its rendered output is unchanged. **Scope moved a third time under Q8.30**: the manifest row now carries the name and the runtime registry answers to it, keys first, so `Name` is a lookup spelling at both tiers rather than at build time only — see row 6 |
| 3 | Generated `#line` file (Q8.12, fallout) | The registration key, indistinguishable from the file path while every key was path-derived | ✅ The template's root-relative path under `HeddleTemplateRoot`, its own (absolute) `AdditionalText.Path` outside it (Q8.27 — the old fallback was a bare filename no compiler could open). Only an explicit `Key` can move it now that `Name` is additive. **Which of the two forms a template uses is manifest data** (`PrecompiledTemplateInfo.LinePathForm`, Q8.31), not the header comment Q8.27 first emitted — a comment is unreadable to the symbolizers and editors that would want it. The comment is deleted; eight Verify snapshots and the sample's generated-source golden move by that line, and the sample's rendered output does not |
| 4 | Release-line statements (Q8.11) | Nine per-project `<Version>` elements (four on non-shipping projects), plus four npm manifests, `PINNED_VERSION` in the VS Code extension, an `lsp.yml` `--version`, four prose sentences, and a `1.0.0` const in the language server — eighteen statements, no lockstep test | ✅ One `<VersionPrefix>` in [Directory.Build.props](../../../Directory.Build.props); the un-centralisable statements gated by `VersionConsistencyTests`; the language server's version derived from its assembly. The CI beta job's `--version-suffix` lost a leading dash that a composed `VersionPrefix` can no longer carry |
| 5 | Strong naming (Q8.11) | `Heddle.Demo.Models` and `Heddle.Demo.Wasm` unsigned, drawing `CS8002` in the signed `Heddle.LanguageServices.Tests` | ✅ Both signed with `heddle.snk`. The remaining unsigned reference is third-party (Scriban) and is accepted at the reference through a declared list in `Directory.Build.targets`, not by a project-level `NoWarn` |
| 6 | Registered `Name` as a runtime lookup spelling (Q8.30) | The manifest carried keys only; a `Name` reached the build-time `@<<` import map and stopped there, so nothing at run time could resolve by it | ✅ `PrecompiledTemplateInfo.RegisteredName` on the row, and a second index in [PrecompiledTemplates](../../../src/Heddle/Precompiled/PrecompiledTemplates.cs) consulted **after** keys. **Keys win**, independently of registration order, enforced as a disjointness invariant from both directions (a name whose spelling a key owns is refused; a key arriving later evicts the name shadowing its spelling) — a name is an addition, and an addition that displaced an existing spelling would be the override Q8.25 corrected. The cross-assembly collision is the new `HED7104` / `RegisteredNameUnavailable`, reported through `OnFallback` and never a throw, because a broken addition costs the addition and nothing more; a duplicate **key** still throws, and the two are contrasted in one test so neither drifts into the other |
| 7 | Opted-out items validated and advised (Q8.28 / Q8.29) | The diagnostics loop `continue`d on `!Precompile` **before** key derivation, so a malformed `Key`, a malformed `Name` or an already-taken `Name` on an import-only item produced no diagnostic at all — the name silently failed to register and every `@<<` that used it drew `HED7011` at the *importer*. `HED7028` likewise could not fire for an import inside an opted-out file, the case it is most for | ✅ Key and name derivation moved ahead of the gate: an opted-out item raises the same `HED7004` faults an included one does, and is parsed in advisory-only mode so its own imports draw `HED7028`. `HED7029` was **not needed** and stays free — every fault had an existing home, and the one genuinely new collision class is cross-assembly and therefore runtime (`HED7104`, row 6). The opt-out's contract is unchanged and pinned: no entry point, no manifest entry. Its *template* errors stay unreported by decision, since the file is excluded from this build by request; they surface through any precompiled template that imports it |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 8 | `PrecompiledFallbackEvent` carriers (Q8.33) | One `Key` property holding two different kinds of string — a template key for per-request reasons, an assembly name for the registration-time ones — with no discriminator, so a host had to re-derive from `Reason` which of the two it had. The convention was stated in the type's XML doc and **nowhere in [precompilation.md](../../precompilation.md)**, which never mentioned the overload at all | ✅ `Key` **removed** and replaced by `TemplateKey` + `AssemblyName`, exactly one populated, constructed through `ForTemplate`/`ForAssembly` which each refuse the other carrier's reasons. Removal rather than narrowing, deliberately: narrowing leaves a 2.0 host silently reading `null` on the channel whose whole purpose is that failures are not silent, while removal is a compile error at the one line that must change. The reason → carrier mapping is enforced by an exhaustive classifier that **throws on an unclassified reason**, so a reason added later is unraisable until it is mapped. `precompilation.md` now carries the carrier table |
| 9 | Unnormalizable manifest `Name` (Q8.32) | Registration `continue`d past a `RegisteredName` the shared key rule refuses — a `..` segment, a trailing separator, whitespace — with **no event at all**, the one wholly silent drop in the registration path while both collision arms reported | ✅ Reported as `HED7104` / `RegisteredNameUnavailable`, naming the refused spelling and the requesting key. **No new id**: the registry row and the enum member were widened, because from the host's side the two causes are one situation with one remedy — a name it expected does not resolve, and the template is still reachable by its key. The population is manifests no build tier vetted, since the generator emits only names that registered at build time. A per-request gauntlet arm for `RegisteredName` was **rejected** (Q8.32 ruling): the name resolves to an entry whose every row the gauntlet already validates, so re-checking it validates nothing |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 10 | Assembly auto-loading and scan-all extension discovery (Q8.37) | `AssemblyHelper`'s **static constructor** `Assembly.Load`d the entry assembly's whole transitive `GetReferencedAssemblies()` closure plus every `DependencyContext.GetDefaultAssemblyNames()` entry — unconditionally, at type init, with `FileNotFoundException`, `FileLoadException`, `ReflectionTypeLoadException` and `BadImageFormatException` all swallowed — and `TemplateFactory`'s static constructor scanned that set for `[ExportExtensions]`. Because the scanned set decides extension **name ownership**, an assembly the integration layer never chose to load could take a name, or collide with an unrelated claimant and throw `TemplateOverrideException` out of a type initializer. `HeddleTemplate.Configure` existed but latched on first call, so a second call was silently dropped | ✅ **The engine loads nothing.** The walk, the dependency-context enumeration and the scan-all discovery are deleted; the assembly set is now what the host has already loaded **from disk into the default load context** (observed, never loaded — an in-memory assembly, a collectible or custom context, or an assembly not yet loaded is invisible), plus whatever the host **registers**. `[ExportExtensions]` is read per assembly at registration time, so a loaded-but-unregistered assembly offers no name. **What stops working:** a host that declares `[ExportExtensions]` in an assembly it never registers — its extensions no longer register, and a template calling one fails with `HED0002` (`Cannot find extension`), or, if precompiled, degrades per request with an `ExtensionBindingMismatch` naming the unresolved extension. A **second, narrower population**: a template naming a model or declaration type by string (`@model(Some.Lib.Order)`) in an assembly the host references but has never touched — the walk force-loaded it, so the spelling resolved; now it resolves only once that assembly is loaded or registered, and otherwise raises the same `Couldn't resolve type` the engine has always raised for an unknown spelling. *(Amended 2026-07-27: an adversarial review found the first implementation both narrower and more fragile than this row claimed — it keyed observation on `Assembly.Location`, which is empty for the entire application in a single-file or WASM publish, and it never rebuilt the type maps from observation, so an assembly loaded after the first resolution stayed invisible and the outcome depended on load order. Both are fixed; the row's rule is what ships.)* Every first-party template in the repository and all ten samples resolve unchanged, because using a type is what loads its assembly and a host that renders against a model has used it. **What to call instead:** `HeddleTemplate.Register(assembly)` per providing assembly (the added API; `Configure` is kept as its alias, with the one-shot latch removed so repeated calls now take effect). All ten samples already called `Configure` and needed no change; `Heddle.Generator.IntegrationTests` registers itself from a module initializer — the pattern a host follows. Two consequences worth naming: the `Microsoft.Extensions.DependencyModel` **package reference is gone** (the walk was its only consumer), and the engine no longer force-loads a host's dependency graph at type init. Pinned by `AssemblyRegistrationTests`, which compiles and loads an `[ExportExtensions]`-carrying assembly and asserts it offers neither its extension name nor its type spelling until registered, and holds `beforefieldinit` on `AssemblyHelper` so a reintroduced static constructor reddens. **Disposition home:** a landed item of the open window belongs to this running record; when the window closes it condenses into the release records with the rest |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 11 | Precompilation-coverage widening — the whole program (embedded C# as a compiled fragment, computed `@partial` names, the rendered-ref-struct split, per-node engine accessors, late-bound function sites, model-type routing, assembly configuration, the shared type-name ladder) | Each construct cost its template the precompiled tier; the run tier rendered it and nothing said so beyond `HED7031` | ✅ **Entirely additive; it consumes no window budget, and it is recorded here so that fact is verified rather than assumed.** Three parts. **(a) No signature moved.** `PrecompiledOptionsFingerprint`'s three-argument constructor, `RegisterModelAssemblies`/`UnregisterModelAssemblies`, `HeddleTemplate.Register`/`Configure` and `TemplateFactory.AddExtensions` are unchanged. The fingerprint is the trap worth naming: it is emitted **into every already-built consumer assembly**, so an optional fourth parameter would remove the three-argument member from metadata and fault existing code — a trap this window has already paid for once (row 1). It needed no change anyway, and the reason is now a rule stated on the type: **assembly configuration is not a fingerprint input**, because it decides *whether* a template precompiles and never a rendered byte. `PrecompiledTemplateInfo` grew `ModelTypeIsAmbient` the same way row 1's shapes did — as a real constructor beside the existing ones, never as an optional parameter. **(b) No schema increment.** Everything landed inside schema 3 (`LateBoundFunctionSchemaVersion`, `AmbientModelTypeSchemaVersion`), which is what the schema-tracks-breakage rule requires: an absent optional row reads as its old value, so an older manifest still registers. **(c) Additive surface only** — one new public attribute (`HeddleModelAssemblyAttribute`), two new MSBuild items appended to `@(ReferencePath)`, one new MSBuild property (`HeddleNodeFallback`, default on), one new `PrecompiledFallbackReason` member (`ModelTypeMismatch`, reported through the existing `HED7101` channel), and one **severity relaxation** (`HED7015` Error → Warning), which widens what builds rather than narrowing it. With every item unset the generated `.g.cs` is byte-identical, which is how each stage was gated. **`HeddleProbeExtensionHooks` and `HeddleBuildOptions.ProbeExtensionHooksProperty`/`PackageFoldersProperty`/`DefaultProbeExtensionHooks` were added and removed again inside this same window** — the hook-probing mechanism they configured was deleted before 2.1 shipped, so nothing outside the window ever saw them; the shipped 2.0 surface is untouched |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 12 | Binding the extensions instead of predicting them — the whole program (the Layer 1 static-init seam, the type-agnostic body, the per-call-site substitute, persisted engine observation, definition layering, computed engine identity, and the deletion of the hook probe) | A custom extension that overrode `InitStart`/`CompleteInit` cost its template the precompiled tier under `HED7015`; a bodied call to any extension the build had no typing row for cost it too; definition override/layering was refused wholesale; the generator decided what an extension did from a name-keyed table and a `"Heddle"` assembly-name compare | ✅ **Entirely additive; it consumes no window budget, and this row is the verification rather than the assertion.** Four parts. **(a) Additive public surface only, checked against the golden.** The full diff of `src/Heddle.Tests/TestTemplate/public-api-heddle.txt` across the program adds `PrecompileUnsupportedAttribute`, `ObserveMode`, `PrecompiledLateAccessor`, `PrecompiledRuntime.Init`/`InitDefinition`/`InitExtension`/`SiteFallback`, new `PrecompiledInitSite`/`PrecompiledInitBody` members, two `PrecompiledFallbackReason` members (`ExtensionInitCompileError`, `ExtensionInitTypingMismatch`, both **appended** so no existing member's numeric value moves), two `HeddleDiagnosticIds` constants (`HED7033`, `HED7034`) and the `HeddleBuildOptions` fields behind `HeddleObserveEngine`/`HeddleObserveIntermediatePath`. It **removes exactly three** members — `DefaultProbeExtensionHooks`, `ProbeExtensionHooksProperty`, `PackageFoldersProperty` — which row 11 already records as added and removed inside this same window, so nothing outside the window ever saw them. **(b) `PrecompiledTemplateInfo` gained a constructor, not an optional parameter.** The fifteen-value shape carrying `InitSites` is a real constructor beside the existing four, for the reason rows 1 and 11 both name: an optional parameter removes the narrower signature from metadata and faults every already-built consumer assembly whose manifest calls it. Verified in the golden, where all four earlier `CTOR` lines survive unchanged beside the new one. **(c) No schema increment.** `Min = Max = Current = 3` is untouched; `InitSites` is an optional manifest row that an older manifest simply omits, and the value it reads as — no call sites, nothing to fault — is exactly what an older manifest meant. **(d) One diagnostic retired, none renumbered.** `HED7015` no longer fires and is retired in place: constant, catalog row, registry row and published row all stay, because an id once shipped is never reused. `HED7033` is not its successor — it reports an extension author's declared choice and costs one call site, where `HED7015` reported a build's inability and cost the template. Behaviourally the program only *widens* what precompiles; a template that precompiled before still precompiles, byte-identically, which is how each stage was gated |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 13 | Binding the child-template host instead of reimplementing it (`@partial` and every `[ChildTemplateHost]` extension) | Generated code carried `@partial` itself: a normalized `TemplateKey`, a `PrecompiledPartialName` field for a computed name, and a `LazyInitializer`-memoised `PrecompiledRuntime.ResolvePartial` per call site. The extension was never constructed, and the copy was not byte-neutral — its `Render` called `HeddleTemplate.Generate` on the child and pushed the resulting string through the caller's renderer, materialising output the real `PartialExtension.RenderData` streams | ✅ **Additive; it consumes no window budget.** The call takes the ordinary bound-extension route: `PrecompiledRuntime.Init` constructs the extension and runs its real `InitStart`/`CompleteInit`, and a new engine-internal one-shot seam (`PrecompiledChildSupply`, in front of `HeddleTemplate.Compile(CompileContext)`, armed only for the drain of a site the build marked `HostsChildTemplate`) supplies the child. **(a) Public surface adds exactly one member**, `PrecompiledInitSite.HostsChildTemplate`, verified in the golden as a single added `PROP` line. **(b) Four public members are retired in place, not removed** — `PrecompiledRuntime.ResolvePartial` (all three overloads), `PrecompiledRuntime.EvaluatePartialName` and the `PrecompiledPartialName` type. The current generator emits none of them, but they are public and **generated code from earlier generator versions calls them**, exactly as row 1's constructor break taught: removing a member an already-built consumer assembly's IL names faults that assembly at registration. They keep working unchanged; removal is a next-window candidate. **(c) No schema increment** — `HostsChildTemplate` is a settable property an older manifest simply never writes, and the value it reads as (`false`, no child supply armed) is what an older manifest meant. **(d) One position corrected.** `PrecompiledInitSite.PositionStart/PositionLength` carried the *chain's* span, which indexes the shaped working document; it now carries the engine's own item position, which is what the engine puts on the extension, on the witness source item and on a fault raised while compiling the item. The two differ only where a trimmed directive line moves the shaped document off the source — and where they differ, the old value pointed a hook and a diagnostic at a coordinate in the author's file that had nothing to do with the call |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 14 | Binding the slot projection instead of reimplementing it (`@out` and every `[SlotProjection]` extension) | The generator carried `@out`'s compile-time reasoning: a bespoke `BuildOutCall` arm keyed on the role, a valueless/valued/bodied refusal trio, a prop-argument refusal, and a hand-written twin of the engine's `PropConversion.CanConvert(…, allowBoxToObject: false)` with the `HED5014` family behind it, plus its own `AllocateOutExtension` allocation path. Every one of them predicted what `OutExtension.InitStart` would do | ✅ **Additive; it consumes no window budget.** The call takes the ordinary bound-extension route: `PrecompiledRuntime.Init` constructs the extension and runs its real `InitStart`, which derives slot mode from `CompileContext.SlotParameterType` and raises its own diagnostics. **(a) Public surface adds nothing at all** — no site flag was needed, unlike row 13's `HostsChildTemplate`. `PrecompiledInitSite.SlotType` already carried the active slot parameter type for *every* call site, and `Init` already installed it on the synthesized compile scope, so the real hook reaches the same `_slotMode` with nothing new to carry; the check was made before a member was added, and `public-api-heddle.txt` is unchanged. **(b) One public member is retired in place, not removed** — `PrecompiledRuntime.BindOut`. The current generator emits no call to it, but it is public and generated code from earlier generator versions calls it, exactly as row 1's constructor break taught. `OutExtension.SetPrecompiledSlotMode` stays beside it (internal) because `BindOut` is its one caller; deleting it would break the retired member it serves. **(c) No schema increment** — nothing on the manifest row changed. **(d) One moment moves, and it is a build log rather than a byte.** A template whose slot value the engine refuses used to leave the precompiled tier at *build* time with a `HED7031` naming the reason; it now precompiles, and the projection's own hook reports the engine's `HED5012`/`HED5013`/`HED5014` when it runs at registration — a **template-scope** fault, which the gauntlet turns into a per-request fallback onto the dynamic tier, where the engine refuses the template with the same id, the same sentence and the same position. The diagnostic the reader receives is unchanged and arrives at the same moment it always did (first render); what is gone is the build-time warning that anticipated it. **(e) One refusal is kept, and measured rather than preferred.** A **bodied** projection stays a build refusal: outside a slot-declaring definition the hook accepts the body, and the engine compiles a body with no dynamic content in it to no processors at all — so the projection emits the chained value alone where an emitted body is a real strategy. Deleting the refusal made `[@out(){{BODY}}]` render `[BODY]` against the engine's `[]`, which is the rendered byte the refusal now cites. *(Amended 2026-08-10 by row 16: that byte is gone — the build emits the engine's own no-processor post-state, and the same four shapes including `[@out(){{BODY}}]` were measured byte-identical with the refusal removed. It is kept anyway, and the reason is no longer a byte: with the body emitted, `BuildSlotProjectionCall` is a passthrough and a declared `[SlotProjection]` changes nothing the build tier does, which `ExtensionRoleDispatchTests` currently pins through this very refusal. Retiring it is that role-dispatch decision, and is a candidate rather than a fact.)* **(f) One refusal reason changes spelling.** A `ref struct` slot value degrades naming the *model* sink (`CS1503`) rather than the slot sink (`CS0029`), because the projection's value is emitted into `Scope.ModelData` like every other call's; the `RefStructUse.Boxed` sink it named is gone with the twin that was its only caller |

| # | Change | 2.0.0 state | As implemented for 2.1.0 |
| --- | --- | --- | --- |
| 15 | Precompiling chains (`@a():b():c()` in output position, `@a(b():c())` as a call parameter) | Every multi-item chain cost its template the precompiled tier: `TemplateEmitter.BuildCall` refused on `chain.Chain.Count != 1`, and `BuildParamExpr` refused any chain parameter with more than one item. `PrecompiledInitSite.IsChainedConsumer` and `HasProducerToRight` existed and were read by `PrecompiledRuntime`, but the emitter never wrote either — sound only for as long as no chain reached the tier | ✅ **Additive; it consumes no window budget.** The chain is emitted as the engine's own `TemplateChain`, statement for statement, and each item binds through the same routes a lone call does. **(a) Public surface adds exactly one member**, `PrecompiledInitSite.ChainProducer`, verified in the golden as a single added `PROP` line. It is the seam for the one thing about a chain a build cannot name without predicting a hook: the engine threads `returnTypeChainedPrevious` right to left, so a consumer's `chainedType` is whatever the producer's own `InitStart` returned. The consumer's site names the producer's site; `Init` reads the answer off it, published there by the same run that produced it (`ResolvedReturnType`, internal — it exists only between one call's hook and the next one's, which is inside a single type initializer). Static field initializers run in textual order, and the emitter writes chain items right to left, so the producer's initializer always precedes the consumer's. **(b) No schema increment** — `ChainProducer` is a settable property an older manifest never writes, and the value it reads as (`null`, keep the type the carriage declares) is what an older manifest meant. **(c) No diagnostic added or retired.** `RefusalCategory.ChainCarrier` stays declared and stays reachable, narrowed to the bodied unnamed carrier and two grammar-unreachable floors. *(Amended 2026-08-10 by row 16: the carrier is emitted now, so what is left is an item of a chain the emitter could not build, a chain-item name it cannot bind, and the two floors.)* **(d) One refusal is kept, and measured rather than preferred.** A **bodied unnamed carrier** stays a build refusal for the same reason a bodied projection does (row 14e): the engine compiles a body with no dynamic content to no processors, so the carrier renders its model, while an emitted body is always a real strategy — `@( ){{text}}` renders the model on one tier and `text` on the other. *(Amended 2026-08-10 by row 16: retired. The premise "an emitted body is always a real strategy" was the defect, not the boundary — it made `@raw(){{mid}}` diverge too, on a call this row never named. The build emits the post-state, and the carrier precompiles.)* |
| 16 | Emitting the engine's no-processor body post-state, and the bodied unnamed carrier with it | `AbstractExtension.InitSubTemplate` has three post-states and the emitter produced two of them: every bodied call was handed `new BodyN()`, so a body the engine compiles to no processors — static text alone, nothing but directives, a chain that composes to nothing — installed a real strategy where the engine installs none. `InnerExist` was true on one tier and false on the other, and every hook that reads it diverged: `@raw(){{mid}}` over `"Hi"` rendered `<x>mid</x>` precompiled against `<x>Hi</x>` dynamic. The bodied unnamed carrier was refused outright for that byte (row 15d) | ✅ **Additive; it consumes no window budget.** **(a) A byte changes on the precompiled tier, and that is the point.** Byte identity between the tiers is the hard contract, and this was a violation of it: a template whose bodied call has an inert body now renders what the dynamic tier has always rendered. There is no compatible reading in which the old bytes were correct. **(b) No public surface at all.** The change is one emitter decision — the call site is handed `null` where a body class used to go — and `PrecompiledRuntime.Init`'s `body` parameter has documented that `null` since it was added ("the call's body compiled to no processors or the call has no body"). `public-api-heddle.txt` is unchanged. **(c) No schema increment, no diagnostic added or retired.** The site still carries its `PrecompiledInitBody` with the raw and shaped text; only the strategy argument moves. **(d) The predicate is the engine's own, read off `HeddleCompiler.CompileBody`**: a document element is added for a chain whose composed `returnTypeChainedPrevious` is non-null and for no other, so a body is empty exactly when no output chain survives shaping — which is what the shared `DocumentShaper` already computes on this side. It is read off the shaped element list rather than off the built segments because a definition body is registered before it is populated so a self-call can find it, and a half-built segment list would tell that call the body was empty. **(e) One refusal retired, one kept and one bound narrowed.** Row 15d's bodied-unnamed-carrier refusal is retired: the carrier takes the ordinary bound-call route and `@( ){{text}}` renders the model on both tiers. Row 14e's bodied projection is kept for the reason recorded there — no longer a byte, and no longer this program's to decide. And the per-call-site substitute's bound gains the clause its own header always implied: a refusal the consumer's build configuration makes (`RefusalCategory.HostSetup` — an expression-mode gate, a missing reference) is one the substitute meets too, because it compiles the same text under the same options, so the template degrades instead of raising the engine's refusal at first render. Found by the carrier retirement, which routed `wierd-whitespace.heddle` into that hole; the dynamic tier refuses that document under the sweep's own options and the corpus row now says so |

**Accepted residue, recorded rather than glossed.** (a) Roslyn has no per-reference suppression for
`CS8002` — the warning carries no source location and the `Csc` task takes only a project-wide disabled
list — so item 5's mechanism is *keyed on* the named third-party assembly rather than scoped to it: a
project referencing both a listed and an unlisted unsigned assembly would silence both. No project does.
(b) `src/Heddle.Performance` is under a change-nothing ruling (Q7.2); the only edit made there is the
deletion of its dead `<Version>` element, which item 4 requires and which needs no other change to that
project.

## Next-window candidate register

Nothing here is ratified. Rows are appended as owning specs record them and removed only
when a ratified window adopts them (recording the adoption) or an owning spec withdraws
them (recording the withdrawal in the ledger).

| Candidate | Precondition / trigger | Owning record |
| --- | --- | --- |
| Removal of the retired precompiled-partial API — `PrecompiledRuntime.ResolvePartial` (three overloads), `PrecompiledRuntime.EvaluatePartialName`, and the `PrecompiledPartialName` type | The current generator emits none of them (row 13), but assemblies built by earlier generator versions call them from their static initializers, so removal faults those assemblies at registration rather than at compile. Trigger: a ratified window whose schema floor already excludes every generator version that emitted them, so the manifest gate rejects such an assembly before its IL is reached | The [2.1 record](#current-window--21-open-as-implemented-pending-release) (row 13) |
| Default encoder swap (`WebUtility` → `HtmlEncoder.Create(UnicodeRanges.All)` or a successor choice) | `TemplateOptions.Encoder` shipped in 2.0.0; soak it, so hosts that need byte-exact output have a pin available before the default moves | The [2.0 record](cross-cutting-decisions.md#release-records--as-shipped) (item 2) |
| `AllowCSharp` property removal | `[Obsolete]` shipped in 2.0.0; soak it, and confirm `ExpressionMode` usage dominant in first-party surface and docs | The [2.0 record](cross-cutting-decisions.md#release-records--as-shipped) (item 4) |
| `[NotEncode]` type deletion | Revisit only inside a ratified window | The [2.0 record](cross-cutting-decisions.md#release-records--as-shipped) (exclusions) |
| **C# betterness in the runtime overload binder** — replace the flat Pareto rank with C#'s better-conversion-target rule (plus validations preserving the documented deviation set), landed **jointly** on both tiers so the generator matches by construction and the cast-pin emit guard mostly retires | Quantified and ready (phase 4 WI10): over the shipped built-in table, **0 of 480** argument combinations change their winning overload, **100** become bindable that are ambiguity errors today, and **38** stay ambiguous (`double`/`decimal` are mutually non-convertible, so neither target is closer) — so it is a pure widening with no rendered-byte change for any template that compiles today, but it *is* a widening: templates that error today would start rendering, and that acceptance cannot be withdrawn later. Trigger: adoption inside a ratified window, together with the extra validations for deviations 4–7 (whose enforcement the flat rank currently gets for free from ambiguity) and a decision on the residual `double`/`decimal` ambiguity's diagnostic | Generator program, phase 4 — expression-writers (D10 amendment / WI10), from the Q4.2(b) ruling; measured by `OverloadBetternessEvaluationTests` |
| **Member-visibility widening** — accept a `protected internal` getter (the plain-English reading of the sandbox's "public-or-internal" contract), surface inherited non-public and base-interface members | Phase 4 WI6 pinned today's narrow runtime behavior as normative (OQ1/Q3.1) and encoded it in one function (`MemberVisibility.IsAccessible`), so any of these becomes a one-line policy change plus a conformance-corpus row flip. Trigger: a ratified window, landed jointly on both tiers with a spec-wording update to `native-expressions.md`'s sandbox section | Generator program, phase 4 — expression-writers (D7 / OQ1) |
| **Dynamic-vs-typed visibility asymmetry** — the typed member tier accepts an `internal` getter regardless of assembly while the dynamic tier (binding in `Heddle`'s context) cannot see a foreign `internal` member | Phase 4 WI9 made the dynamic-tier binder context exist exactly once (`PrecompiledRuntime.DynamicMember`), so harmonizing the two tiers is now a single decision rather than two. Trigger: a spec clarification deciding *which* tier is right, then a window | Generator program, phase 4 — expression-writers (D11 / OQ3) |
| **Extension-name collision between unrelated types** — the runtime's `TemplateFactory.AddExtensions` throws `TemplateOverrideException`; the build tier degrades the call to dynamic with a recorded reason instead of erroring | Phase 3 landed the full runtime precedence in one shared rule-core (`ExtensionRegistrationRules`), so making the build tier error too is a one-line verdict change. Deliberately *not* done now: it is a host wiring error the build has no business failing on before the host is even assembled, and the runtime already surfaces it at first render | Generator program, phase 3 — binding-layer (Q3.3 / OQ3) |
| Must-surface precompiled mismatches stop degrading silently under the **default** policy (`ExtensionBindingMismatch`, `FunctionBindingMismatch` per-request; `SchemaVersionUnsupported`, `EngineVersionIncompatible` at registration), with an explicit opt-out policy value preserving today's blanket degrade | The fallback taxonomy ships first (done — [precompilation.md](../../precompilation.md#which-fallbacks-are-legitimate)); the two binding classes are provisional until phase 3 finishes its binding work, since a residual mismatch is only conclusive evidence of skew once the generator binds through the full runtime replacement precedence | Generator program, phase 5 — pipeline-config (D12b), from the Q2.2 fallback-legitimacy ruling |
