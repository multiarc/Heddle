# Phase 1 — template emitter: parity test matrix

Supplement to [phase-1-template-emitter.md](phase-1-template-emitter.md) (the owning plan; read
it first). This document enumerates the concrete test assets per work item so the phase spec can
instantiate the [testing standards](../spec/common/testing-standards.md) without re-deriving
coverage. Suite homes follow existing precedent: cross-tier byte comparisons live in
`src/Heddle.Generator.IntegrationTests` (driven by `DifferentialHarness` /
`CorpusDifferentialTests`), white-box generator rules in `src/Heddle.Generator.Tests` (the
`DefaultFunctionLockstepTests` pattern), and runtime-only behavior in `src/Heddle.Tests`
(all TFMs).

**What a "fixture" is in `Heddle.Generator.IntegrationTests` (corrected 2026-07-26).** That suite
holds **no `.heddle` files at all**: `DifferentialHarness.Generate` takes `(key, content)` pairs and
a fixture is an inline template string keyed by a `views/<stem>.heddle` path. The stems below are
therefore *keys*, and reading them as filenames — as the first draft of this matrix invited — is what
produced the "six corpus guardrail entries with zero fixture files" finding. The only on-disk
`.heddle` corpus is `src/Heddle.Tests/TestTemplate/**` (the shared corpus
`CorpusDifferentialTests`/`CorpusRenderParityTests`/`CorpusResolverSweepTests` sweep), whose files
*are* pinned `eol=lf` in `.gitattributes`. See *Corpus guardrail entries* below for which of this
phase's guardrails live where, and why.

**Names, corrected 2026-07-26.** Several rows below named suite files that were never created under
that name (the implementation used a different one). Each row now names the file that exists; the
audit trail is the phase spec's Implementation record.

## TDD verdict (carried into the spec)

Differential and lockstep fixtures are **test-first**: each WI's fixtures are written failing
(or, for parity restorations, written asserting the *dynamic* tier's bytes so they fail on the
precompiled tier) before the fix/extraction lands. Mechanical refactors (WI13, delegation-only
adoptions) are **test-with**: the pre-existing suites are the spec, and the WI adds no fixtures
beyond what the matrix lists.

## Fix-first bug group

| WI | Suite / fixture | Kind | Pins |
|---|---|---|---|
| WI1 | `Heddle.Generator.IntegrationTests/ScopeParticipantDifferentialTests.cs` — keys `views/scope-carrier-asymmetric.heddle` and its `-mirror`, `-both`, `-neither` twins | differential (byte, both tiers) | per-carrier frames: the definition body's flag and the caller content's flag are independent — inner fresh / outer cleared-or-passthrough, matching `AbstractExtension.GetInnerResult`'s dynamic rules; bytes identical. **This is the observable half of drift #3** |
| WI1 | same suite — key `views/scope-selfcall-participant.heddle` | differential | the `GetOrBuildDefinitionBody` pre-mark path under the recursive scan (self-calling definition with a participant) |
| WI1 | `Heddle.Generator.IntegrationTests/QuarantinedDriftFixtures.NonLeftmostScopeChannelParticipant_ProvisionsLocalsOnBothTiers` | degrade parity + differential | the **latent** half: a participant reachable only as a nested chain parameter is a shape the emitter refuses for an unrelated pre-existing reason, so it degrades identically to a participant-free twin of the same syntax. There is no `scope-nested-participant` byte fixture and cannot be one until that refusal is lifted — the plan's original row for it is superseded by the Implementation record's "Correction to the plan" |
| WI1 | `Heddle.Tests/BindDefinitionOverloadTests.cs` | unit | the new `BindDefinition` overload's four flag pairs, and both legacy overloads still applying one flag to both carriers — i.e. "existing overloads binary-**and behaviourally** unchanged", the success criterion the public-API golden only covers at the signature level |
| WI2 | `Heddle.Generator.IntegrationTests/ProfileFlipTests.cs` — keys `views/profile-unknown-value.heddle`, `views/profile-unknown-twice.heddle` | negative (positioned diagnostic) | build produces `HED7022` at the directive position with the valid-values message; the dynamic compile of the same fixture produces `HED2001` (asserted in the same test so the twin relationship is executable); one diagnostic per directive |
| WI2 | `ProfileFlipTests` — key `views/profile-case-variants.heddle` | differential | case/trim tolerance identical on both tiers, no diagnostic |
| WI3 | `Heddle.Generator.IntegrationTests/NullableDefaultDifferentialTests.cs` — keys `views/props-nullable-widen-default.heddle`, `views/props-nullable-identity-default.heddle` (**not** `PropsTests.cs`) | differential + tier assertion | template precompiles (was: fallback) and renders byte-identically; boxed-value type reproduction (`Convert.ChangeType` twin) |
| WI3 | `Heddle.Generator.Tests/DefaultConvertibleLockstepTests.cs` + `Heddle.Tests/DefaultConvertibleReflectionTests.cs` over the linked `Heddle.Tests/PropDefaultConversionVectors.cs` | lockstep theory | symbol-side `DefaultConvertible` verdicts equal reflection-side `PropConversion.CanConvertTypes` over a `(source, target, expected)` row set covering every rule branch incl. the new `S?→W?` rows. Per the Implementation record the `S?→W?` row is **unreachable from an extension `[Prop]` default** (a `TypedConstant.Type` is never `Nullable<S>`) and is landed for table identity only — the vector set asserts it as such |

## Extractions

| WI | Suite / fixture | Kind | Pins |
|---|---|---|---|
| WI4 | `Heddle.Tests/ParticipantScanLockstepTests.cs` | lockstep + whole-corpus sweep | (a) six named rows stating the divergence set the fix closes, against a verbatim transcription of the legacy leftmost-only probe; (b) `TheSharedScanAgreesWithTheRuntimeOverTheWholeCorpus` — the sweep this matrix promised and the plan's risk table names as the mitigation: all 62 `TestTemplate/**` templates compiled, shared verdict never narrower than `RuntimeDocument.NeedsLocals`, the six templates that do provision a frame pinned by name (so the sweep cannot go vacuous), and an **empty** named allow-list for corpus over-provision; (c) the shadowed-name over-provision (Q1.4's trigger) as an inline template — there is no `scope-shadowed-branch-name.heddle` file and the corpus contains no shadowing fixture |
| WI5 | `Heddle.Tests/SlotRulesTests.cs` | unit theory | the five-way `HasOutValue` over every `CallParameter` shape (native expr / chain param / C# expr / prop args / first model segment / none); slot-type walk over base chains incl. re-declared and absent slot types |
| WI5 | `Heddle.Generator.IntegrationTests/SlotAndDefaultOutputTests.cs` (existing, unchanged) | regression gate | byte-identical before/after delegation — the extraction's no-op proof |
| WI5 | `Heddle.Generator.Tests/EmitterSharedRuleAdoptionTests.TheEmitterHasNoPrivateSlotWalk` | structural | name-**independent**: the emitter reads `SlotTypeName` only through `SlotRules`, and no statement re-forms the `IsModelTypeParameter`-plus-carriers approximation. (Rewritten 2026-07-26: it used to assert the absence of the method *name* `DefinitionHasSlot`, which a copy under any other name passed) |
| WI6 | `Heddle.Tests/RegionFillResolverTests.cs` | unit theory | all five verdicts (`Matched`/`ForeignOrigin`/`Dangling`/`PrivateRegion`/`DefaultMissing`) from constructed parse trees; per verdict, both sides' reactions asserted to **match** (skip with parse-emitted error kept / retract + materialize / retract + error) per the OQ3 match-principle ruling |
| WI6 | `Heddle.Generator.IntegrationTests/RegionTests.cs` — keys `views/region-private.heddle`, `views/region-dangling.heddle` (**not** `region-fill-*.heddle`) | differential + negative (positioned diagnostic) | dangling: both tiers surface the same parse-emitted base-not-found error and the generator does not refuse to precompile; private: dynamic compile raises `HED5019`, precompiled build fails with the positioned twin `HED7024` (asserted in the same test — the WI2 twin pattern) |
| WI6 | `RegionTests` / `CompositionTests` (existing, unchanged) | regression gate | emitter adapter is behavior-neutral for correct templates |
| WI7 | `Heddle.Tests/CallTargetRulesTests.cs` (**not** `Heddle.Generator.Tests/CallTargetLockstepTests.cs`) | lockstep theory | precedence rows: fill beats definition beats extension beats function; definition shadows a branch keyword; bodiless known-function compiles as function; chain-param / C#-expr shapes refuse the function path; the unnamed carrier is never classified |
| WI7 | same suite | invariant test | `DefaultFunctionTable` names ∩ built-in `[ExtensionName]` names = ∅ (the comment-only invariant made executable) |
| WI7 | `Heddle.Generator.Tests/CallTargetAdoptionTests.cs` | build-tier differential | **added 2026-07-26**: the real inversion WI7's record says it fixed — a host-exported function named after a registered extension — had no test. A synthetic compilation exports a function named `raw`; the emitter must bind `EmptyExtension` (the `@raw` type) and emit no call into the container, i.e. reach the runtime's answer |
| WI8 | `Heddle.Tests/EmbeddedCSharpNamesPinTests.cs` | pin | both embedded `.tcs` resources declare parameters spelled exactly `EmbeddedCSharpNames.Model/Chained/Root`, in that order; reviewed once by mutation (rename → red) |
| WI9 | `Heddle.Tests/ZeroOutputProtocolTests.cs` | conformance | for every built-in registered extension: `[ZeroOutput]` present ⇔ `InitStart` returns null (the runtime protocol) |
| WI9 | `Heddle.Generator.IntegrationTests/ZeroOutputDifferentialTests.cs` — keys `views/zero-output-custom.heddle`, `views/zero-output-builtin.heddle` (**not** `CustomExtensionTests.cs`) + the `[ZeroOutput]` `@note` test extension in `Fixtures/BranchRoleExtensions.cs` | differential | custom zero-output block removed from output on both tiers (the F17 divergence pin); the four built-in directives still classify identically through the binder |
| WI10 | `Heddle.Tests/BodyModelRuleTableTests.cs` | conformance | each row read as a **prediction about observable output**, never against itself: the `Body` column predicts whether the body can bind the enclosing model's member (so `@list`'s `ElementOfData` is discriminated from the branch/`@for` rows' `Parent`), and the `Chained` column predicts what `@out()` splices inside a `@for` body. (Rewritten 2026-07-26: three of these tests asserted the table against literals copied from the table) |
| WI10 | `Heddle.Generator.Tests/EmitterSharedRuleAdoptionTests` | conformance | the emitter **consumes** the rows: `TemplateEmitter.TryNestedBodyContext` derives the nested body's build context from the row (`Parent` → the enclosing typed context, `ElementOfData` → the dynamic tier), and the tests read the consequence off the generated source. (2026-07-26: the emitter's only previous link to the table was a `Debug.Assert`, i.e. nothing in Release, and the build-tier test was a theory whose `InlineData` was the table's own rows) |
| WI11 | `Heddle.Tests/OutputProfileAndRenderTypeRuleTests.cs` (one file, **not** two) | unit theory | `TryParseProfile` case/trim/unknown rows; `ResolveUnnamedCarrier` over (profile × hasBody); `Derive` over all four bool pairs — the truth table from `[EncodeOutput]`/`[NotEncode]` |
| WI11 | `Heddle.Generator.IntegrationTests/EncoderDifferentialTests.cs` + `ProfileFlipTests.cs` (existing) | regression gate | encoding bytes identical across both adoptions. Both halves landed: phase 5's enum links merged, so the emitter consumes `OutputProfileRules`/`RenderTypeRules` and its private ternary is gone |

## Strategy-shape differential suite (WI12)

`Heddle.Generator.IntegrationTests/StrategyShapeDifferentialTests.cs`, all rows byte-compared
across tiers; fixtures under the `strategy-` stem:

| Fixture | Pins |
|---|---|
| `strategy-static-only.heddle` | full-static body: runtime `DocumentStrategy` short-circuit == generated single-piece return |
| `strategy-single-processor.heddle` | single-part `Execute` (no concat) == runtime single-element strategy |
| `strategy-empty-body.heddle` | empty body returns `string.Empty` on both tiers |
| `strategy-alternation.heddle` | head piece / interleaved processors / tail piece ordering (the offset-walk contract) |
| `strategy-nonstring-value.heddle` | **corrected 2026-07-26** — the implemented row asserts the rail against the **emitted source**: every `Execute` part is `…ProcessData(scope.Model(…)) as string ?? string.Empty` and multi-part bodies `string.Concat`. It is a *shape* pin, not a byte comparison of a boxed non-string; the stem is kept because the joint-land rule (OQ2/Q1.2) names this test as the tripwire the rail change must consciously edit. The value path *dropping* a boxed non-string to `string.Empty` is asserted nowhere as rendered bytes — see the note below |
| `strategy-boxed-index.heddle` | the render-path companion (not in the original matrix): a boxed `int` on the chained channel — the `@for` index a non-slot `@out()` returns — is stringified identically on both tiers. This is the observable half of the render/value asymmetry §4 of the plan's normative section pins |
| `strategy-adjacent-processors.heddle` | zero-length pieces between adjacent processors: no empty-piece divergence between `GetDocumentPieces` and the emitted shape |

**Residual, recorded rather than papered over.** The plan's normative §4 ("a boxed non-string reaching
the value path is dropped to empty while the render path stringifies it") has coverage on the render
side (`strategy-boxed-index`) and on the emitted-shape side (`strategy-nonstring-value`), but no
fixture drives a boxed non-string through the **value** path on both tiers and compares bytes. The
value path is reached only when a body's result is consumed as a string by an enclosing host, and
every construct that does so on the precompiled tier stringifies before the rail. Closing it needs a
host extension whose `ProcessData` consumes its body's `Execute` result and returns a non-string —
new fixture machinery, not a missing assertion. The rail is identical on both tiers today (verified
against `RuntimeDocument`'s four strategies and `EmitBodyClass`), so this is a coverage gap, not a
divergence.

## Corpus guardrail entries — corrected 2026-07-26

The original text here said the six area-01 guardrails "land in `CorpusDifferentialTests`' fixture
set". **They did not, and the row was wrong in two ways at once.** `CorpusDifferentialTests` has no
fixture set of its own: it sweeps `src/Heddle.Tests/TestTemplate/**`, and none of the six shapes was
ever added there. What *did* land is a dedicated differential/lockstep asset per shape, which is a
strictly stronger gate than the corpus sweep for five of the six — the corpus sweep pins
*classification* (precompiles / marker / falls back) and compile-safety, while these pin **rendered
bytes on both tiers**. The table states where each guardrail actually lives:

| 07 guardrail (area 01) | Where it lives | Kind of gate |
|---|---|---|
| non-leftmost `[ScopeChannel]` participants (WI1) | `QuarantinedDriftFixtures.NonLeftmostScopeChannelParticipant_…` + `ScopeParticipantDifferentialTests` | degrade parity for the latent half; byte parity for the observable per-carrier half |
| `@profile(<typo>)` (WI2) | `ProfileFlipTests` — `views/profile-unknown-value.heddle` | positioned-diagnostic negative + `HED2001` twin |
| nullable-widening prop defaults (WI3) | `NullableDefaultDifferentialTests` | byte parity + tier assertion |
| a shadowed participant name (WI4) | `ParticipantScanLockstepTests.AShadowedParticipantNameOverProvisionsAndThatIsTheRuling` | rule-level lockstep (Q1.4's named trigger) |
| a custom `[ZeroOutput]` extension (WI9) | `ZeroOutputDifferentialTests` | byte parity |
| the strategy-shape set (WI12) | `StrategyShapeDifferentialTests` | byte parity per row + one emitted-shape pin |

**What the shared corpus gained instead.** Area 01's whole-corpus guardrail is
`ParticipantScanLockstepTests.TheSharedScanAgreesWithTheRuntimeOverTheWholeCorpus` (added
2026-07-26): it runs the shared participant scan and the runtime's compiled-tree scan over **every**
`TestTemplate/**` template and pins both the never-narrower contract and the exact set of templates
that provision a frame. That is the sweep the WI4 row promised.

**Why the other five are not `TestTemplate` files, stated so the next reader does not re-open it.**
Three of the five cannot be: `@profile(<typo>)` is an error template and every corpus consumer
(`CorpusRenderParityTests`, `DoubleRenderWarningTests`, the resolver sweep) renders what it finds, so
it would have to be threaded through `ExpectedDiagnosticFixtures` and excluded from three other
suites; the `[ZeroOutput]` and nullable-prop fixtures depend on test extensions that live in the
generator integration-test assembly, not in `Heddle.Tests`. The remaining two are byte-compared
already. Moving any of them into a shared corpus is **phase 7's** shared-test-corpus deliverable, not
a phase-1 omission to backfill — but the claim that they were already there was false and is
withdrawn here.

## Regression gate (every WI)

The standard combined gate per the
[testing standards](../spec/common/testing-standards.md#regression-gates): full Release build;
`Heddle.Tests` + `Heddle.Generator.Tests` + `Heddle.Generator.IntegrationTests` green on all
TFMs; goldens byte-identical for every fixture the WI did not add; grammar-stability check (no
grammar change is licensed by this phase); benchmarks not required except WI1 (render-path
frame provisioning — run the render suite; allocated bytes must not increase, and the fix can
only remove over-provisioned frames).
