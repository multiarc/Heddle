# Phase 1 — template emitter: parity test matrix

Supplement to [phase-1-template-emitter.md](phase-1-template-emitter.md) (the owning plan; read
it first). This document enumerates the concrete test assets per work item so the phase spec can
instantiate the [testing standards](../spec/common/testing-standards.md) without re-deriving
coverage. Suite homes follow existing precedent: cross-tier byte comparisons live in
`src/Heddle.Generator.IntegrationTests` (driven by `DifferentialHarness` /
`CorpusDifferentialTests`), white-box generator rules in `src/Heddle.Generator.Tests` (the
`DefaultFunctionLockstepTests` pattern), and runtime-only behavior in `src/Heddle.Tests`
(all TFMs). Fixture names use an initiative-recognizable stem per the fixture conventions;
every new fixture directory is added to the `.gitattributes` `eol=lf` pins in the same change.

## TDD verdict (carried into the spec)

Differential and lockstep fixtures are **test-first**: each WI's fixtures are written failing
(or, for parity restorations, written asserting the *dynamic* tier's bytes so they fail on the
precompiled tier) before the fix/extraction lands. Mechanical refactors (WI13, delegation-only
adoptions) are **test-with**: the pre-existing suites are the spec, and the WI adds no fixtures
beyond what the matrix lists.

## Fix-first bug group

| WI | Suite / fixture | Kind | Pins |
|---|---|---|---|
| WI1 | `Heddle.Generator.IntegrationTests/ScopeParticipantDifferentialTests.cs` — `scope-nested-participant.heddle` | differential (byte, both tiers) | a `[ScopeChannel]` participant appearing only as a nested chain parameter provisions a locals frame precompiled; `@else` reads the same branch state on both tiers (the drift #3 regression pin) |
| WI1 | same suite — `scope-carrier-asymmetric.heddle` (definition body participates, caller content does not) and its mirror | differential | per-carrier frames: inner fresh / outer cleared-or-passthrough, matching `AbstractExtension`'s dynamic rules; bytes identical |
| WI1 | same suite — `scope-selfcall-participant.heddle` | differential | the `GetOrBuildDefinitionBody` pre-mark path under the recursive scan (self-calling definition with a participant) |
| WI1 | `Heddle.Tests` — `PrecompiledRuntimeTests` addition | unit | new `BindDefinition` overload semantics; old overloads byte-for-byte unchanged (both flags equal ⇒ identical binding to the legacy overload) |
| WI2 | `Heddle.Generator.Tests/HeddleGeneratorTests` addition + `Heddle.Generator.IntegrationTests/ProfileFlipTests.cs` extension — `profile-unknown-value.heddle` | negative (positioned diagnostic) | build produces the claimed `HED70xx` at the directive position with the valid-values message; the dynamic compile of the same fixture produces `HED2001` (asserted in the same test so the twin relationship is executable) |
| WI2 | `ProfileFlipTests` extension — `profile-case-variants.heddle` (`HTML`, `Text`, padded values) | differential | case/trim tolerance identical on both tiers, no diagnostic |
| WI3 | `Heddle.Generator.IntegrationTests/PropsTests.cs` additions — `props-nullable-widen-default.heddle` (`int?` default on `long?` prop), `props-nullable-identity-default.heddle` | differential + tier assertion | template precompiles (was: fallback) and renders byte-identically; boxed-value type reproduction (`Convert.ChangeType` twin) |
| WI3 | `Heddle.Generator.Tests` — `DefaultConvertibleLockstepTests.cs` | lockstep theory | symbol-side `DefaultConvertible` verdicts equal reflection-side `PropConversion.CanConvertTypes` over a `(source, target, expected)` row set covering every rule branch incl. the new `S?→W?` rows; this row set is the seed of phase 3's conformance corpus and is handed to it verbatim |

## Extractions

| WI | Suite / fixture | Kind | Pins |
|---|---|---|---|
| WI4 | `Heddle.Generator.Tests/ParticipantScanLockstepTests.cs` | lockstep | shared `ParticipantScan` verdict == `RuntimeDocument` `NeedsLocals` for every compiled fixture in the differential corpus; the shadowed-name fixture (`scope-shadowed-branch-name.heddle` — a definition named after a `[ScopeChannel]` extension) asserts the documented over-provision branch explicitly (OQ4's trigger) |
| WI5 | `Heddle.Tests/SlotRulesTests.cs` | unit theory | the five-way `HasOutValue` over every `CallParameter` shape (native expr / chain param / C# expr / prop args / first model segment / none); slot-type walk over base chains incl. re-declared and absent slot types |
| WI5 | `Heddle.Generator.IntegrationTests/SlotAndDefaultOutputTests.cs` (existing, unchanged) | regression gate | byte-identical before/after delegation — the extraction's no-op proof |
| WI6 | `Heddle.Tests/RegionFillResolverTests.cs` | unit theory | all five verdicts (`Matched`/`ForeignOrigin`/`Dangling`/`PrivateRegion`/`DefaultMissing`) from constructed parse trees; per verdict, both sides' reactions asserted to **match** (skip with parse-emitted error kept / retract + materialize / retract + error) per the OQ3 match-principle ruling |
| WI6 | `Heddle.Generator.IntegrationTests` additions — `region-fill-dangling.heddle`, `region-fill-private.heddle` | differential + negative (positioned diagnostic) | dangling: both tiers surface the same parse-emitted base-not-found error and the generator does not refuse to precompile; private: dynamic compile raises `HED5019`, precompiled build fails with the positioned twin ID (asserted in the same test — the WI2 twin pattern) |
| WI6 | `RegionTests` / `CompositionTests` (existing, unchanged) | regression gate | emitter adapter is behavior-neutral for correct templates |
| WI7 | `Heddle.Generator.Tests/CallTargetLockstepTests.cs` | lockstep theory | precedence rows: fill beats definition beats extension beats function; definition shadows a branch keyword; bodiless known-function compiles as function; chain-param / C#-expr shapes refuse the function path; both dispatch sites (emitter, `HeddleCompiler`) agree with the classifier per row |
| WI7 | same suite | invariant test | `DefaultFunctionTable` names ∩ built-in `[ExtensionName]` names = ∅ (the comment-only invariant made executable) |
| WI8 | `Heddle.Tests/EmbeddedCSharpNamesPinTests.cs` | pin | both embedded `.tcs` resources declare parameters spelled exactly `EmbeddedCSharpNames.Model/Chained/Root`, in that order; reviewed once by mutation (rename → red) |
| WI9 | `Heddle.Tests/ZeroOutputProtocolTests.cs` | conformance | for every built-in registered extension: `[ZeroOutput]` present ⇔ `InitStart` returns null (the runtime protocol) |
| WI9 | `Heddle.Generator.IntegrationTests/CustomExtensionTests.cs` addition — `zero-output-custom.heddle` + a `[ZeroOutput]` test extension | differential | custom zero-output block removed from output on both tiers (the F17 divergence pin) |
| WI10 | `Heddle.Tests/BodyModelRuleTableTests.cs` | conformance | each built-in's observed body/chained typing matches its `BodyModelRules` row (branch trio → parent model; `@for` → parent + boxed index; `@list` → element type incl. the non-generic→dynamic row; definition/caller/region rows) |
| WI10 | `Heddle.Generator.Tests` addition | conformance | the emitter's pinned emission branches cite the same table rows (assertion over the table, not over emitted text) |
| WI11 | `Heddle.Tests/OutputProfileRulesTests.cs`, `RenderTypeRulesTests.cs` | unit theory | `TryParse` case/trim/unknown rows; `ResolveUnnamedCarrier` over (profile × hasBody); `Derive` over all four bool pairs — the truth table from `[EncodeOutput]`/`[NotEncode]` |
| WI11 | `Heddle.Generator.IntegrationTests/EncoderDifferentialTests.cs` + `ProfileFlipTests.cs` (existing) | regression gate | encoding bytes identical across both adoptions (runtime half, then generator half after phase 5) |

## Strategy-shape differential suite (WI12)

`Heddle.Generator.IntegrationTests/StrategyShapeDifferentialTests.cs`, all rows byte-compared
across tiers; fixtures under the `strategy-` stem:

| Fixture | Pins |
|---|---|
| `strategy-static-only.heddle` | full-static body: runtime `DocumentStrategy` short-circuit == generated single-piece return |
| `strategy-single-processor.heddle` | single-part `Execute` (no concat) == runtime single-element strategy |
| `strategy-empty-body.heddle` | empty body returns `string.Empty` on both tiers |
| `strategy-alternation.heddle` | head piece / interleaved processors / tail piece ordering (the offset-walk contract) |
| `strategy-nonstring-value.heddle` | a processor returning a boxed non-string on the value path drops to `string.Empty` on both tiers — the pinned coercion rail; this fixture is the tripwire the rail change's joint landing (OQ2, resolved: the joint-land rule) must consciously edit |
| `strategy-adjacent-processors.heddle` | zero-length pieces between adjacent processors: no empty-piece divergence between `GetDocumentPieces` and the emitted shape |

## Corpus guardrail entries (added to the shared differential corpus)

Per [07's guardrail list](../research/generator-code-sharing/07-recommendations.md), the area-01
entries: non-leftmost `[ScopeChannel]` participants (WI1), `@profile(<typo>)` (WI2),
nullable-widening prop defaults (WI3), a shadowed participant name (WI4), a custom
`[ZeroOutput]` extension (WI9), and the strategy-shape set above. Entries land in
`CorpusDifferentialTests`' fixture set in the WI that pins them.

## Regression gate (every WI)

The standard combined gate per the
[testing standards](../spec/common/testing-standards.md#regression-gates): full Release build;
`Heddle.Tests` + `Heddle.Generator.Tests` + `Heddle.Generator.IntegrationTests` green on all
TFMs; goldens byte-identical for every fixture the WI did not add; grammar-stability check (no
grammar change is licensed by this phase); benchmarks not required except WI1 (render-path
frame provisioning — run the render suite; allocated bytes must not increase, and the fix can
only remove over-provisioned frames).
