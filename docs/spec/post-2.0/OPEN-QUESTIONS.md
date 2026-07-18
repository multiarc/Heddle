# Open questions (maintainer register)

> **Only maintainer-level items.** Every plan open question (P1–P8) is closed inside its phase spec as
> a decision with evidence; nothing author-level is parked here. The one item below was raised to the
> maintainer during spec authoring and **resolved** — no work blocks.

## Open

*None.*

## Resolved

### OQ1 — Does override/inheritance stop native precompilation of a region fill?  [status: RESOLVED 2026-07-13]
- **Question.** [Phase 7](phase-7-named-content-regions.md) fills a region by reusing the existing
  `<name:name>` override, and the generator today refuses `DefinitionInvolvesOverride` (degrading such
  a call to the dynamic tier via `HED7014`). Should a call that *overrides* a region be natively
  precompiled, or ride that pre-existing override→dynamic fallback?
- **Provisional default (was implemented at author time).** Ride the existing fallback: region
  *defaults* precompile natively, an *overridden* fill degrades to the dynamic tier exactly as today's
  sibling-override idiom does; backends stay byte-identical via the fallback.
- **Affected specs / decisions.** Phase 7 D8 (four-layer rollout), the precompiled-layer row of
  [phase-7-region-table.md](phase-7-region-table.md), the generator work item, and the
  precompiled-parity testing plan. Cross-cutting [X2](common/cross-cutting.md#x2--the-four-layer-prop--region-realization-phase-7--phase-8).
- **Resolution (maintainer, 2026-07-13).** **Override/inheritance does not inherently stop
  precompilation.** A call site is a *materialized* definition with a final form at all times;
  override/inheritance only changes *which* materialization path is taken and *when*. The real
  limitation is compiling child and parent *separately* rather than compiling the whole set together.
  Therefore Phase 7 **natively precompiles region fills** by compiling the whole parent+child+override
  materialization together (multi-file needs more checks + cache-busting); the dynamic tier stays a
  genuine **escape hatch only** and is never justified by "override stops precompilation." The
  generator's `DefinitionInvolvesOverride`→dynamic degrade is lifted for the region-fill case. Phase 7
  D8 and the region-table precompiled row are revised to specify the native path; the precompiled
  differential now asserts byte-identity on *overridden* fills natively, not via fallback. Scope is
  bounded to definition composition (Phase 7); [Phase 8](phase-8-extension-parameters.md)'s
  custom-extension bodied-call fallback is a different mechanism and is unchanged.

### OQ2 — How to dispose the four spec/plan files deleted by commit `702a26d0`?  [status: RESOLVED 2026-07-15]
- **Question.** The "Clean-up" commit `702a26d0` deleted `docs/improvement-plan.md`,
  `docs/improvement-spec.md`, `docs/import-removal-plan.md`, and `docs/import-removal-spec.md`, but
  the master index, the diagnostic registry, `breaking-windows.md`, and `records.md` still linked them
  and treated them as **active owning records** (HED4003 registry ownership; the two next-window
  breaking candidates; the 2.1 encoder/`[Obsolete]` dispositions). Restore the files, or retire them?
- **Resolution (maintainer standing ruling SR-A, 2026-07-15).** **Retire the initiatives and relocate
  their still-live normative content** — exactly the treatment already given the retired v2.0 nine-phase
  effort (shipped/superseded; specs live in git history only). Every dangling link to the four files is
  removed (converted to plain-text "(retired — git history)" citations, per the
  [spec-conventions writing-style rule](../common/spec-conventions.md#writing-style)); the two initiative
  rows in [spec/README.md](../README.md) are converted to the "shipped; specs retired, git history only"
  treatment; and the normative ownership those files held is moved into the surviving docs — the
  **`HED4003`** registry-row ownership now resolves through the
  [claimed diagnostic-ID registry](../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  + the [amendments ledger](../records.md#cross-spec-amendments-ledger) (its live normative home), and both
  **next-window breaking candidates** (default encoder swap B2, `AllowCSharp` removal B1) now name the
  [2.0 window record](../records.md#the-20-breaking-window--as-shipped-record) + ledger as their owning
  record in [breaking-windows.md](../common/breaking-windows.md). Nothing is orphaned. `records.md`'s
  append-only integrity is respected: its retired-spec **links** are de-linked to the plain-text form its
  own header already mandates, while every historical fact is preserved.
- **Affected specs / decisions.** [Phase 5](phase-5-docs-and-benchmark-credibility.md) D3/D4/WI2/WI3 and
  the link-check gate; [spec/README.md](../README.md); [cross-cutting-decisions.md](../common/cross-cutting-decisions.md)
  registry `HED4003`; [breaking-windows.md](../common/breaking-windows.md) next-window register; [records.md](../records.md).

### OQ3 — Is the 2.0 breaking window open or closed?  [status: RESOLVED 2026-07-15]
- **Question.** [records.md](../records.md)'s 2.0 as-shipped record framed 2.0.0 as released and its
  byte-changing window as **closed** (the encoder default-swap was deferred to the "3.0 register" on that
  basis), while [Phase 6](phase-6-range-type.md) lands a ratified **breaking** change that requires the
  2.0 window to still be **open** (R5). The two premises conflicted.
- **Resolution (maintainer standing ruling SR-B, 2026-07-15).** The 2.0 window is **OPEN** and 2.0 is
  imminent/**unreleased** — there is no `v2.0.0` tag; the `2.0.0` contents sit under `[Unreleased]` in the
  [CHANGELOG](../../../CHANGELOG.md). `records.md`'s "as-shipped / byte-changing window closed" wording was
  **premature**. It is reconciled minimally so that, set-wide, **no doc claims the 2.0 window is closed**:
  the as-shipped record's intro now states the window remains open pending the tag, and the default-swap
  deferral is re-grounded on its unmet **precondition** (a shipped-and-soaked `TemplateOptions.Encoder`
  pin) rather than window closure. Phase 6's open-window premise is now consistent with the shared record.
- **Affected specs / decisions.** [records.md](../records.md) (as-shipped record + amendments ledger);
  [breaking-windows.md](../common/breaking-windows.md); [Phase 6](phase-6-range-type.md) D4/D9;
  [Phase 5](phase-5-docs-and-benchmark-credibility.md) D4 (records.md now receives a bounded correction).

### OQ4 — `TryCompilation` full-parity cost (plan P4-Q1)  [status: RESOLVED — see Phase 4 spec]
- **Question.** Plan P4-Q1 (author cost lean): should `TryCompilation` gain **full parity** (run
  finalization + Roslyn so a green dry run predicts `Compile`), documenting the added `FullCSharp` cost,
  or take the cheaper explicit-partial contract?
- **Resolution.** Closed as a decision with evidence in [Phase 4](phase-4-engine-robustness.md) (full-parity
  `TryCompilation`; the `FullCSharp` dry-run cost + assembly-cache side effect documented there). Recorded
  here only for register completeness — the decision record lives in the phase spec.

### OQ6 — Fix the pre-existing plain-custom `[EncodeOutput]` cross-tier divergence in Phase 8? (P8-J-E1)  [status: RESOLVED 2026-07-15]
- **Question.** Phase 8's H1 fix made the **parameterized** custom-extension path derive the precompiled
  inner render type from `[EncodeOutput]`/`[NotEncode]`, matching the dynamic tier. But the **pre-existing**
  non-parameterized generator path `TemplateEmitter.AllocateCustomExtension` **hard-codes** `RenderType.Raw`,
  so a **no-parameter** custom extension marked `[EncodeOutput]` renders **`Raw` on the precompiled tier**
  while the **dynamic tier resolves `Encode`** — a silent, byte-changing cross-tier (XSS-class) divergence
  that exists today **independent of parameters** (raised as review finding **P8-J-E1**). Fix it in Phase 8,
  or leave it as a separate pre-existing item?
- **Resolution (maintainer, 2026-07-15).** **Fix it in-scope.** A known silent cross-tier encoding
  divergence should not ship unfixed beside the parameterized path that already resolves the identical
  concern. [Phase 8](phase-8-extension-parameters.md) [D8](phase-8-extension-parameters.md#d8--plain-custom-extension-render-type-derives-from-encodeoutputnotencode-resolves-p8-j-e1)/[WI5b](phase-8-extension-parameters.md#wi5b)
  change `AllocateCustomExtension` to **derive** the render type from the extension's `[EncodeOutput]`/`[NotEncode]`
  — the **exact** expression `HeddleCompiler.InitializeTemplate` (≈1460) evaluates on the dynamic tier and the
  **same** literal the parameterized path emits — instead of hard-coding `Raw`, reusing the
  `Info.HasEncodeOutput`/`Info.HasNotEncode` flags Phase 8 already adds to `ExtensionBinder.Info`. The
  byte-identity gate holds: the change moves bytes **only** in the previously-divergent case (a precompiled
  no-parameter `[EncodeOutput]` extension), aligning precompiled **to** the dynamic tier's already-correct
  encoding; no existing corpus fixture is a plain `[EncodeOutput]` custom extension (had one been, the
  differential would already be failing), and a new `EncodedBare` cross-tier differential fixture guards the
  alignment. No new diagnostic id; no cross-cutting change (this is not a prop-layer contract).
- **Affected specs / decisions.** [Phase 8](phase-8-extension-parameters.md) Scope, Assumed state, D8 (new),
  WI5b (new), WI5 parenthetical, Back-compat, Testing plan. No `HeddleDiagnosticIds`/`GeneratorDiagnostics`
  change; [cross-cutting X2/X3](common/cross-cutting.md) unchanged.

### OQ5 — HTML-context lint position coverage (plan P3-Q1)  [status: RESOLVED — see Phase 3 spec]
- **Question.** Plan P3-Q1 (author cost lean): cover the attribute position first with `<script>`/URL as a
  fast-follow within the phase, or narrow to attribute-only?
- **Resolution.** Closed as a decision with evidence in [Phase 3](phase-3-html-context-encoding-lint.md)
  (the lint covers attribute, `<script>`, and URL positions within the phase on one gate). Recorded here
  only for register completeness — the decision record lives in the phase spec.
