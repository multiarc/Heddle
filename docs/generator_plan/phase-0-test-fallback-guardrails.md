# Phase 0 — test-fallback-guardrails

## Header

- **Status:** implemented (2026-07-25) — WI1–WI8 landed; see [Implementation record](#implementation-record).
  **Step 0 of the program: lands before, and gates, every fix in phases 1–6.**
- **Goal (one line):** Make the test suite structurally unable to pass through an unintended fallback — the majority of end-to-end tests pin the precompiled tier (any fallback throws or fails the test), and fallback is exercised only by the handful of tests whose subject *is* fallback.
- **Depends on:** nothing. Everything else depends on this: each later phase's fix groups and extractions are verified under the guarded suite this phase creates.
- **Changes an externally-visible contract:** no. Test-only work plus one `InternalsVisibleTo` line in the engine (no public surface, no behavior, no bytes). The one spec touch is an amendment to [testing-standards.md](../spec/common/testing-standards.md) recording the posture (WI8), via the amendments mechanism.

## Goal

The research program found that the precompiled tier's dominant failure mode is **silent fallback**:
the gauntlet rejects a manifest entry, the render quietly takes the dynamic path, output bytes are
identical by design, and nothing anywhere notices. Two of the fifteen verified live drifts — the
content-hash input mismatch ([05 F1](../research/generator-code-sharing/05-pipeline-config.md)) and
the nested/generic AQN mismatch ([03 F1](../research/generator-code-sharing/03-binding-layer.md)) —
are precisely this shape, and both shipped because **no test renders real generator output through
the resolver/gauntlet path with fallback treated as failure**.

The current suite splits cleanly at the two fallback boundaries:

- **Build-time degrade (emitter refuses → no entry class): already guarded.** The differential
  harness throws `"Generated entry class not found (fell back)"`
  ([DifferentialHarness.cs:259](../../src/Heddle.Generator.IntegrationTests/DifferentialHarness.cs))
  and invokes generated code directly, so the ~130 differential/feature tests genuinely execute
  generated code, and a degrade fails loudly. Five files test degrade deliberately
  (`ChainedDefinitionFallbackTests`, `ContextEncodingFallbackTests`, `OutStaticBodyFallbackTests`,
  `BranchRoleUniversalityTests`, parts of `ExtensionParametersDifferentialTests`).
- **Runtime fallback (registration → `TryResolve` → per-request gauntlet): effectively unguarded.**
  The direct-invoke harness bypasses `PrecompiledTemplates.Register`,
  `TemplateResolver.ConsultPrecompiled`, and every `PrecompiledGauntlet` check. Exactly one
  end-to-end test pins the resolver-served precompiled adapter
  ([ResolverIntegrationTests.cs:36](../../src/Heddle.Generator.IntegrationTests/ResolverIntegrationTests.cs)),
  and only incidentally (its resolver root is nonexistent, so a fallback would fail to compile).
  The ~27 gauntlet tests in `Heddle.Tests` run against **hand-built manifests** — real generator
  output never meets the gauntlet in any test.

This phase closes the second boundary and formalizes the first, so that from phase 1 onward every
"the generator works" test is evidence about the *precompiled* path, not accidentally about the
dynamic one.

## Non-goals / scope boundary

- **No drift fixes.** The known live drifts stay broken through this phase; where a new guarded
  fixture would trip one of them today, the fixture lands **quarantined** (D6) and is handed to the
  owning phase as its acceptance test. Phase 0 must land green without masking anything.
- **No new gauntlet checks or manifest rows.** Adding prop-layout coverage to the gauntlet is
  phase 3's Q3.4; this phase only makes the *existing* gauntlet's verdicts test-visible.
- **No removal of the direct-invoke harness path.** Direct invocation stays as the emitter-isolation
  tool (it is what makes the differential tests fast and precise about *which* tier produced which
  bytes). This phase adds the gauntlet-crossing path beside it; it does not replace isolation with
  end-to-end everywhere (see D4 for the posture math).
- **No CI/pipeline changes** beyond the suite itself; no benchmark or golden-corpus changes.
- **No diagnostics work.** The degrade-visibility diagnostic for the generator's blanket
  `catch (Exception)` remains phase 6 territory (Q2.2).

## Design direction

### D1 — Two guard mechanisms, both on: `Strict` at resolve, a fallback sentinel everywhere

**Decision.** The guarded path sets `TemplateOptions.PrecompiledMismatchPolicy = Strict` (gauntlet
failure throws `PrecompiledMismatchException` at `TryResolve` —
[PrecompiledTemplates.cs:178-180](../../src/Heddle/Precompiled/PrecompiledTemplates.cs)) **and**
wraps the render in a `FallbackGuard` — an `IDisposable` test utility that hooks
`PrecompiledTemplates.OnFallback`, restores the previous hook on dispose, and fails the test on any
event whose key was not explicitly expected.

**Rationale.** `Strict` alone is not sufficient: code paths that consult the registry with
default-`Fallback` options (e.g. a generated `@partial` resolving its child, or any future
render-time raise) would still degrade silently. The sentinel catches every `RaiseFallback`
regardless of policy; `Strict` gives the sharper failure (typed exception with
`PrecompiledFallbackReason` + detail) at the primary resolve. The existing tests that *toggle*
`OnFallback` ([ResolverIntegrationTests.cs:63-76](../../src/Heddle.Generator.IntegrationTests/ResolverIntegrationTests.cs),
[PrecompiledRegistryTests.cs:62-68](../../src/Heddle.Tests/PrecompiledRegistryTests.cs)) already
prove the save/restore pattern; the guard packages it.

**Alternatives rejected.** Sentinel-only (loses the typed Strict failure at the primary resolve);
Strict-only (misses secondary resolves); asserting on HED7101 log output (no structured channel).

### D2 — `RenderViaResolver`: the gauntlet-crossing harness mode

**Decision.** `DifferentialHarness` gains a resolver-path twin of `Render`/`RenderWithOptions`:
run the generator, `PrecompiledTemplates.Register(gen.Assembly)`, construct a
`TemplateResolver(rootPath, checkFileChange)` and render via
`GetTemplate(key, …, TemplatePathType.None)` under D1's guards; render the dynamic reference
exactly as today; return both strings. Two sub-modes:

- **Registry-only (default):** resolver root points at a nonexistent marker directory (the
  [ResolverIntegrationTests.cs:40](../../src/Heddle.Generator.IntegrationTests/ResolverIntegrationTests.cs)
  trick) — a fallback that escapes both guards still cannot fake the render, because the dynamic
  fallback compile has nothing to read.
- **File-backed (`checkFileChange: true`):** the corpus is also written to a real temp directory
  and the resolver is created with the staleness check on, exercising
  `PrecompiledGauntlet.CheckStaleness`/`HashFile` against real generator-emitted hashes — the
  path on which [05 F1](../research/generator-code-sharing/05-pipeline-config.md) lives.

**Rationale.** This is the missing end-to-end seam: real generator output → registration →
resolver → gauntlet → precompiled adapter → render, with fallback impossible to miss. The
file-backed sub-mode is the only way any test can ever catch hash-rule drift.

### D3 — Registry isolation: `InternalsVisibleTo` + the existing serialized collection

**Decision.** Add one `InternalsVisibleTo("Heddle.Generator.IntegrationTests", …)` grant to
[AssemblyInfo.cs](../../src/Heddle/Properties/AssemblyInfo.cs) (same public key as the existing
`Heddle.Tests`/`Heddle.Performance`/`Heddle.LanguageServices` grants) so the integration suite can
call `PrecompiledTemplates.ResetForTests()` between tests. Resolver-path tests join the existing
`[Collection("PrecompiledRegistry")]` with `DisableParallelization = true`
([ResolverIntegrationTests.cs:99-100](../../src/Heddle.Generator.IntegrationTests/ResolverIntegrationTests.cs));
the unique-key tagging convention (`wi8-<tag>`) is kept as defense in depth.

**Rationale.** The registry is process-global; without reset, corpus-scale registration would leak
keys across tests and turn HED7002 duplicate detection into cross-test flakiness. The IVT route
mirrors the F7 precedent ([06 F7](../research/generator-code-sharing/06-diagnostics-utilities.md))
— same signing key, one line, no public surface.

### D4 — Coverage posture: every corpus entry crosses the gauntlet; feature suites keep isolation

**Decision.** The gauntlet-crossing posture is carried by the corpus suites, not by converting the
feature suites: `CorpusRenderParityTests` and `CorpusDifferentialTests` gain resolver-path variants
that render **every corpus entry** through `RenderViaResolver` (registry-only mode; one
representative pass file-backed), and each feature area contributes its templates to the corpus
rather than re-plumbing its own tests. Feature suites (`BranchTests`, `RegionTests`, `PropsTests`,
…) stay direct-invoke.

**Rationale — why this satisfies "the majority follows the precompiled path."** Every template in
the golden corpus that precompiles crosses the gauntlet at least once under fallback-as-failure,
while the feature suites keep the isolation that makes a red test point at the emitter rather than
at five layers of plumbing. Converting all ~130 tests to resolver-path would multiply registry
churn and suite time for no additional gauntlet coverage — the gauntlet's verdict is per-template,
not per-assertion. The success criteria below state the posture as a measurable invariant rather
than a test-count ratio.

> **Correction (2026-07-26, post-implementation audit).** This rationale originally opened *"the
> corpus is the union of the feature templates"*. **That is false**, and the claim was load-bearing:
> the feature suites (`BranchTests`, `RegionTests`, `PropsTests`, …) build their templates as
> **inline strings**, so their shapes are not in `Heddle.Tests/TestTemplate` and do not join the
> sweep. Roughly 130 feature tests therefore never cross the gauntlet, and D4's coverage argument
> does not reach them.
>
> What the phase actually delivers is stated precisely in the corrected criterion 2 below: **all 40
> precompiling corpus entries cross the gauntlet** (resolve-only — the verdict lands at
> `TryResolve`, before a byte), and **the model-less parity subset additionally byte-matches the
> dynamic reference** in both sub-modes. That is a real and useful floor, but it is *corpus*
> coverage, not *feature-suite* coverage.
>
> Closing the gap means feature areas contributing their template shapes to the corpus, which is
> the standing rule D8 records in [testing-standards](../spec/common/testing-standards.md) — it is
> how new areas are supposed to arrive, not a backfill anyone has done.
>
> **Residue owner: [phase 7 — shared test corpus](phase-7-shared-test-corpus.md)** (stage 0
> implemented 2026-07-26; the migration stages that would close *this* residue are stopped with cause
> — see its [implementation record](phase-7-shared-test-corpus.md#implementation-record-2026-07-26)
> and **Q8.42**. The mechanism exists; the backfill does not, so this residue is still open). Its diagnosis is that D8's rule was recorded as prose with no mechanism behind it:
> contributing a template means hand-adding a row to `Heddle.Tests.csproj`'s 112-file list and
> reaching it from another project by assembly-path traversal, so the rule cannot bind. Phase 7
> supplies the mechanism — one shared corpus home with declared per-entry intent, membership gated
> by set equality rather than the count this phase's WI4 pinned — and then backfills the feature
> shapes in reviewable stages. It also records the sharper form of the argument: hand-kept duplicate
> **test inputs** are hand-kept duplicate **rules** one level up, and this program's own suites carry
> 18 template literals duplicated character-for-character across the two tiers.

### D5 — Intent is declared: `ExpectPrecompiled` is the default, `ExpectDegrade` is explicit

**Decision.** The harness exposes the expectation explicitly: the default path asserts the entry
class exists *and* the manifest strategy is non-null (the `strategy: null` marker probe currently
copy-pasted in the fallback test files becomes a harness helper); the dedicated fallback tests
convert to an `ExpectDegrade(key)` helper that asserts the *marker* entry is present and no entry
class was generated. Runtime-side, `FallbackGuard.Expect(key, reason)` is the only way a fallback
event passes the sentinel.

**Rationale.** Today the deliberate-degrade tests each hand-roll the manifest probe
([ChainedDefinitionFallbackTests.cs:23-29](../../src/Heddle.Generator.IntegrationTests/ChainedDefinitionFallbackTests.cs),
[ContextEncodingFallbackTests.cs:23-29](../../src/Heddle.Generator.IntegrationTests/ContextEncodingFallbackTests.cs));
making intent a first-class harness concept keeps the "only a couple of tests expect fallback"
rule auditable — grep for `ExpectDegrade`/`Expect(` and you have the exhaustive list.

### D6 — Known live drifts land as a quarantined red-fixture register, not as green tests

**Decision.** Guarded fixtures that would fail **today** because of a phase 1–6 live drift are
written now but quarantined with an explicit skip naming the owning phase
(`[Fact(Skip = "known drift — phase 5 Q/F1: BOM hash mismatch; un-skip with that fix")]`), and the
plan's supplement-level list of them is the **handoff register**: each owning phase's fix-first
group un-skips its fixtures as its acceptance evidence. Initial register (from the research's
verified drifts): BOM'd/UTF-16 template under file-backed mode (phase 5 F1); nested and generic
extension/container types (phase 3 F1); inherited `[ExtensionName]` subclass (phase 3 F3);
non-leftmost `[ScopeChannel]` participant (phase 1 F11); `min(1, 2u)`-style overload tie
(phase 4 F3).

**Rationale.** Phase 0 must not block on any fix (it is step 0), must not mask known breakage
(green-by-omission), and must hand later phases executable acceptance tests instead of prose.
Success criteria require every skip to carry a phase link — an unexplained skip fails review.

### D7 — The guard proves itself: seeded-mismatch meta-tests

**Decision.** A small meta-suite corrupts a **hand-built** registration (wrong content hash, wrong
extension AQN, wrong fingerprint arity — the `PrecompiledGauntletTests` fixtures reused) and
asserts that the guarded render path fails in each mode: `Strict` throws the typed exception with
the right `PrecompiledFallbackReason`; sentinel-only mode fails via `FallbackGuard`; and — the
negative control — the same seeded mismatch under an *unguarded* render silently succeeds,
byte-identical. That last assertion is the standing proof of why this phase exists.

**Rationale.** A guardrail that has never been observed to fire is itself untested
infrastructure. The negative control pins the threat model in executable form.

### D8 — The posture becomes a testing-standards rule

**Decision.** At implementation time, amend [testing-standards.md](../spec/common/testing-standards.md)
(via the amendments mechanism in [spec-conventions.md](../spec/common/spec-conventions.md)) with
the rule: *end-to-end precompiled tests pin the precompiled tier — any fallback fails the test;
fallback behavior is tested only by dedicated tests that declare the expectation explicitly; new
feature areas contribute their templates to the gauntlet-crossing corpus.* Phases 1–6 test plans
inherit the rule instead of restating it.

## Dependencies & ordering

- **Depends on: nothing.** All work is test-side plus the one IVT line (D3).
- **Gates: phases 1–6.** Their fix-first groups are *verified* by un-skipping this phase's
  quarantined fixtures (D6), and their extraction WIs' "byte-neutral, suites unchanged" acceptance
  gates now include the resolver-path corpus sweep. The phase plans' own test matrices remain
  valid; this phase upgrades the floor they run on.
- **Internal ordering:** WI1→WI2→WI3 are strictly sequential (guard → mode → isolation); WI4–WI7
  fan out after WI3; WI8 last.

## Back-compat / impact

- **Engine surface:** one `InternalsVisibleTo` line ([AssemblyInfo.cs](../../src/Heddle/Properties/AssemblyInfo.cs)).
  No public API, no behavior, no rendered bytes. Not window-relevant.
- **Suite runtime:** the resolver-path corpus sweep adds one registration + render per corpus entry
  in a serialized collection; bounded and measured in WI4's done-when (target: the sweep adds less
  than the existing corpus differential suite's own runtime).
- **Flakiness surface:** global registry state is the risk; D3's reset + serialized collection +
  unique keys is the containment. File-backed mode writes to the test temp directory only.

## Risks & mitigations

- **Quarantine rot** (skipped fixtures outliving their phase): every skip names its owning phase
  and drift; the success criteria make an unexplained or orphaned skip a review failure; each
  owning phase's plan already lists un-skipping as acceptance evidence.
- **Registry cross-test leakage:** serialized collection + `ResetForTests` between tests + unique
  keys (D3). The meta-suite (D7) includes a leakage canary (register in one test, assert absent in
  the next).
- **False confidence from registry-only mode** (staleness path never exercised): the file-backed
  sub-mode exists precisely for this; at least one corpus pass runs file-backed (D4), and the BOM
  fixture sits quarantined on that path until phase 5 lands.
- **Guard bypass by future harness additions:** D8's standards rule plus the `ExpectDegrade` grep
  audit keep new tests inside the posture.

## Success criteria

1. Any gauntlet fallback raised during a test that did not declare it fails that test — proven by
   the seeded-mismatch meta-suite, including the unguarded-silent-success negative control (D7).
2. *(Corrected 2026-07-26 — the original wording, "every golden-corpus entry … byte-matches the
   dynamic reference", was met only by redefinition and is restated here as what the suite
   actually asserts.)* Two halves, both under `Strict` + sentinel with zero fallback events:
   **(a) coverage** — every corpus entry the manifest reports as precompiled crosses the gauntlet
   through `RenderViaResolver`, pinned at an **exact count** rather than a floor, so a template
   that stops precompiling reddens the gate; and **(b) byte parity** — the model-less parity
   subset byte-matches the dynamic reference, in registry-only *and* file-backed sub-modes.
   Byte parity is not asserted corpus-wide by design: model-carrying families are byte-checked by
   their own differential suites, and a handful of entries are import fragments (a bare `@else`
   continuation) that no tier can render standalone. The gauntlet's verdict lands at `TryResolve`,
   before a byte, so tier-selection coverage is complete even where byte comparison is meaningless.
3. The complete set of tests that expect fallback is enumerable by grepping the two intent APIs
   (`ExpectDegrade`, `FallbackGuard.Expect`) and matches today's dedicated fallback files plus
   nothing else.
4. The quarantine register exists, every entry names its owning phase and drift, and the suite is
   green with quarantines counted and reported (no unexplained skips).
5. `ResetForTests` is reachable from the integration suite; the leakage canary passes; suite-time
   overhead of the sweep is within the WI4 budget.
6. The testing-standards amendment is drafted and linked from this phase (landed with WI8).

## Validation scenarios

- **Seeded hash mismatch** (D7): corrupt `ContentHash` on a hand-built entry → `Strict` render
  throws `PrecompiledMismatchException(StaleContent)`; sentinel mode fails via guard; unguarded
  mode silently renders identical bytes (negative control documents the pre-phase-0 world).
- **Seeded AQN mismatch:** wrong `ExtensionTypeName` → same triple, reason
  `ExtensionBindingMismatch`.
- **Degrade intent:** a bodied custom-branch template (the `BranchRoleUniversalityTests` shape)
  under the default path fails with the entry-class/manifest assertion; under `ExpectDegrade`
  passes.
- **Corpus sweep:** full golden corpus registered and rendered via resolver, zero fallback events,
  byte parity with dynamic on every entry.
- **Quarantine handoff rehearsal:** locally un-skip the BOM fixture on current code → it fails with
  `StaleContent` through the guard, demonstrating the fixture is a real acceptance test for
  phase 5, then re-skip.

## Open questions

None open. **Q0.1 resolved (user, 2026-07-25): recommendation applied** — the corpus sweep is
the permanent posture carrier (D4 stands as written); feature suites keep direct-invoke
isolation and contribute their templates to the corpus. See the
[register](open-questions.md).

## External grounding

- Verified against source while planning: the harness throw-on-missing-entry
  ([DifferentialHarness.cs:259](../../src/Heddle.Generator.IntegrationTests/DifferentialHarness.cs)),
  direct `method.Invoke`/`Root`-field render paths (`:170`, `:266-268`), the single incidental
  end-to-end pin and the nonexistent-root trick
  ([ResolverIntegrationTests.cs:36-48](../../src/Heddle.Generator.IntegrationTests/ResolverIntegrationTests.cs)),
  `OnFallback` save/restore precedents, `ResetForTests` and its current `Heddle.Tests`-only
  reachability ([AssemblyInfo.cs:8-10](../../src/Heddle/Properties/AssemblyInfo.cs)),
  `Strict` throw site ([PrecompiledTemplates.cs:178-180](../../src/Heddle/Precompiled/PrecompiledTemplates.cs)),
  `EnableFileChangeCheck` default-off ([TemplateOptions.cs:11,125](../../src/Heddle/Data/TemplateOptions.cs)),
  and the resolver ctor surface ([TemplateResolver.cs:25-45](../../src/Heddle/Runtime/TemplateResolver.cs)).
- Research grounding: the silent-fallback threat model and the two shipped instances —
  [05 F1](../research/generator-code-sharing/05-pipeline-config.md),
  [03 F1](../research/generator-code-sharing/03-binding-layer.md); the gauntlet's coverage limits —
  [07 — recommendations](../research/generator-code-sharing/07-recommendations.md).

---

## Work items

- **WI1 — `FallbackGuard` + guarded options.** The sentinel (hook, restore, expected-set,
  fail-on-unexpected) and a `GuardedOptions()` factory (Strict + caller overrides). Done when the
  guard passes its own save/restore and expected/unexpected unit tests.
- **WI2 — `DifferentialHarness.RenderViaResolver`** in registry-only and file-backed sub-modes
  (D2), returning `(precompiled, dynamic)` like the existing paths. Done when the WI8-shape
  template from `ResolverIntegrationTests` renders identically through it under both sub-modes.
- **WI3 — Registry isolation plumbing:** the IVT grant, `ResetForTests` in the collection fixture,
  leakage canary. Done when the canary and the serialized collection run clean across the suite.
- **WI4 — Corpus resolver sweep:** resolver-path variants of `CorpusRenderParityTests` /
  `CorpusDifferentialTests` over every corpus entry (one file-backed pass), with the suite-time
  budget measured and recorded. Done when criteria 2 and 5 hold.
- **WI5 — Intent conversion:** `ExpectPrecompiled` default assertion (entry class + non-null
  manifest strategy) in the harness; the five dedicated fallback files converted to
  `ExpectDegrade`; the copy-pasted manifest probes deleted. Done when criterion 3 holds.
- **WI6 — Quarantined drift fixtures** per D6's initial register, each skip naming owner + drift.
  Done when criterion 4 holds and the phase-5 BOM rehearsal (validation scenario) has been
  executed once.
- **WI7 — Seeded-mismatch meta-suite** incl. the negative control (D7). Done when criterion 1
  holds.
- **WI8 — Testing-standards amendment + this plan's cross-references landed** (D8; README and the
  affected phase plans' test sections gain one-line pointers). Done when criterion 6 holds.

---

## Implementation record

Landed 2026-07-25. Build `dotnet build Heddle.sln -c Debug` green; `dotnet test Heddle.sln -c Debug`
green with exactly the five quarantined skips below and no others.

### Where the work landed

| WI | Files |
| --- | --- |
| WI1 | `src/Heddle.Generator.IntegrationTests/FallbackGuard.cs` (sentinel + `GuardedOptions`); its unit tests in `PrecompiledRegistryIsolationTests.cs` |
| WI2 | `DifferentialHarness.RenderViaResolver` / `SweepViaResolver` (+ `ResolverTarget`, `ResolverSweepResult`, `StageCorpus`, `AssertServedByPrecompiledAdapter`); `ResolverPathHarnessTests.cs` |
| WI3 | one `InternalsVisibleTo` line in `src/Heddle/Properties/AssemblyInfo.cs`; `PrecompiledRegistryTestBase` + leakage canary in `PrecompiledRegistryIsolationTests.cs`; `ResolverIntegrationTests` joins the base |
| WI4 | `CorpusResolverSweepTests.cs` |
| WI5 | `DifferentialHarness.ExpectPrecompiled` / `ExpectDegrade` / `ClassifyInManifest`; probes deleted from `ChainedDefinitionFallbackTests`, `ContextEncodingFallbackTests`, `OutStaticBodyFallbackTests`, `BranchRoleUniversalityTests`, `ExtensionParametersDifferentialTests` |
| WI6 | `QuarantinedDriftFixtures.cs` + `Fixtures/DriftFixtures.cs` (exports added to `Fixtures/BranchRoleExtensions.cs`) |
| WI7 | `SeededMismatchMetaTests.cs`; `DifferentialHarness.Generate`'s `rewriteManifest` hook |
| WI8 | `docs/spec/common/testing-standards.md` §*Precompiled-tier posture*; ledger entry **E8** in `docs/spec/records.md`; one-line pointers in each phase 1–6 plan's *Dependencies & ordering* |

### Quarantine register (WI6 / D6)

| Fixture | Owner | Drift | Observed failure today |
| --- | --- | --- | --- |
| `BomTemplate_StaysOnThePrecompiledTier_UnderFileBackedStaleness` | phase 5 | F1 content-hash input mismatch | `PrecompiledMismatchException(StaleContent)` — "hash mismatch" |
| `NestedExtensionType_BindsAndCrossesTheGauntlet` | phase 3 | F1 nested/generic AQN identity | build-time degrade (manifest entry absent) |
| `InheritedExtensionNameSubclass_CrossesTheGauntlet` | phase 3 | F3 inherited `[ExtensionName]` | `PrecompiledMismatchException(ExtensionBindingMismatch)` |
| `NonLeftmostScopeChannelParticipant_ProvisionsLocalsOnBothTiers` | phase 1 | F11 `needsLocals` participant scan | build-time degrade (nested chain parameters are refused) |
| `OverloadTie_ResolvesIdenticallyOnBothTiers` | phase 4 | F3 overload-rank tie | dynamic compile fails `HED1013` while the precompiled tier renders |

### Corrections to this plan, recorded against source

- **Nested-type AQN (phase 3 F1) is not reachable as an AQN comparison today.**
  `ExtensionBinder.CollectTypes` enumerates `INamespaceSymbol.GetTypeMembers()` only and never
  descends into nested types, so a nested extension degrades at build time *before* the two
  identity spellings can be compared. Phase 3's fix owns both halves — nested-container discovery
  and `+`-separated AQN formatting.
- **The non-leftmost `[ScopeChannel]` drift (phase 1 F11) is latent, not observable.** The emitter
  refuses *any* nested chain parameter (`@yell(@yell(this))` degrades identically), so the shape
  that would expose the leftmost-only participant scan never reaches the precompiled tier. The
  fixture is still phase 1's acceptance test — its fix is what makes the shape both precompile and
  provision the frame.
- **Corpus sweep coverage is resolve-only for the full precompiled set.** The gauntlet's verdict
  lands at `TryResolve`, before any byte is rendered, and a few corpus entries are import fragments
  (a bare `@else` continuation) that no tier can render standalone. Byte parity is asserted over the
  model-less parity subset in both sub-modes.
- **Registering real generator output loads the extra references.** The direct-invoke paths only
  ever *compiled* against `Heddle.Tests`; `PrecompiledTemplates.Register` instantiates the manifest,
  whose rows touch every generated entry class's static constructor. The harness now resolves those
  assemblies from their reference paths.
- Line-number drift in this plan's citations: the collection definition is at
  `ResolverIntegrationTests.cs:96-97` (cited `:99-100`), and the hand-rolled manifest probes were at
  `:21-30` in the two fallback files (cited `:23-29`). All other cited anchors verified accurate.

### WI4 suite-time budget

Measured on `net10.0`, Debug, second of two consecutive runs (noisy workstation — treat as
order-of-magnitude):

| Suite | Tests | Wall time |
| --- | --- | --- |
| `CorpusResolverSweepTests` (added by this phase) | 3 | ≈ 1.7 s (1.0 s + 451 ms + 230 ms) |
| `CorpusDifferentialTests` (existing) | 1 | ≈ 1.0 s |
| `CorpusRenderParityTests` (existing) | 9 | ≈ 3.1 s |

The sweep costs ≈ 1.7 s per TFM against ≈ 4.1 s for the two existing corpus suites it sits beside —
inside the budget as stated ("less than the existing corpus differential suite's own runtime") when
that phrase is read as the corpus suites; it is ≈ 1.7× the single `CorpusDifferentialTests` fact read
narrowly. Sharing one generator run and one registration across every target in a sweep is what keeps
it there: the marginal cost of a swept template is one `TryResolve` plus one render.
