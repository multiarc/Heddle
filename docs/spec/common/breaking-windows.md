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

- **The precompiled schema floor rose 1 → 4** (phase 5, Q8.2; `PrecompiledSchema.MinSupportedSchemaVersion`,
  shipped 2.1.0). A manifest built by a 2.0.x generator at schema 1–3 is no longer accepted: registration
  reports `SchemaVersionUnsupported` (`HED7102`) once and every template in that assembly renders through the
  dynamic path. **Judgement: defect repair, not window-gated**, and rule 1's "one window per major" is not
  engaged — on three grounds.
  (a) **The break already shipped, in 2.0.0.** Schema 4 added the prop-layout fingerprint to
  `PrecompiledExtensionBinding` as an *optional third constructor parameter*, which removes the two-argument
  `.ctor(string, string)` from metadata. Every schema 1–3 manifest's IL names that constructor. So those
  manifests have been unrunnable since 2.0.0 regardless of the window; what 2.1 changes is only whether the
  engine *says so* or crashes.
  (b) **The old behaviour was a `MissingMethodException` at host startup**, thrown out of
  `PrecompiledTemplates.Register` — neither a degrade nor a render, and not something a user could correctly
  depend on, which is the [policy](#policy-applies-to-every-window)'s test. `Min = 1` advertised a support
  window the metadata could not honour; the fix retracts a false claim rather than withdrawing a working
  capability. `4` is exactly the boundary (1–3 were built against two arguments, 4+ against three), so no
  runnable manifest is excluded.
  (c) **No rendered byte changes, on either tier.** A rejected assembly falls back to the dynamic engine,
  which is byte-identical by design. Under `PrecompiledMismatchPolicy.Strict` a deployment that must never
  pay dynamic-compile cost throws instead — the documented, chosen posture for exactly this case, and the
  same treatment 1.x manifests received in the 2.0 window.
  **Accepted consequence, recorded rather than argued away:** a project precompiled by a 2.0.x generator and
  not rebuilt loses precompilation (or throws under `Strict`). `Heddle.Generator` and `Heddle` were already
  documented as version-locked. No compatibility shim: adding a real two-argument overload back would keep
  the faulting set *accepted*, which is the state being fixed. Demonstrated by
  `OldSchemaManifestRejectionTests`, which builds a manifest whose IL genuinely names the absent constructor —
  the earlier "old manifest" test constructed one through the optional parameter, so it exercised a
  *new*-schema call and could never have caught this.

- **The per-item `HeddleTemplate` metadata started working** (phase 5, Q8.12; `Heddle.Generator.targets`,
  shipped 2.1.0). `Key`, `Name` and `Precompile` were all inert from a real project: the targets restated each
  one as `<Key>%(HeddleTemplate.Key)</Key>` inside an `Include="@(HeddleTemplate)"` transform, and outside a
  target a cross-item `%()` reference evaluates to the empty string — so each element overwrote the value the
  transform had just copied. Only the generator suites, which inject `build_metadata.*` directly, ever saw the
  metadata at all. **Judgement: defect repair, not window-gated.** (a) No documented behaviour is withdrawn;
  three documented behaviours begin to occur. (b) No rendered byte changes: `Key`/`Name` change a registration
  key and the generated class name, `Precompile="false"` moves a file to the dynamic path, and the dynamic and
  precompiled tiers are byte-identical. (c) **A build can newly fail, and that is stated, not glossed:** a
  project that set `Key`/`Name` and called the generated entry class by its old path-derived name will not
  compile until the call is renamed — which is what `samples/codegen-t4-successor` needed, and its golden
  changed accordingly. A project relying on documented metadata being *ignored* is not a dependency the
  contract offers.

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
