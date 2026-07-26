# Generator code-sharing plan — open-questions register

The consolidated Q&A register for the seven phases. Numbering is `Q<phase>.<n>`, matching each
phase plan's own Open-questions section.

**All questions are resolved (user, 2026-07-25) and folded into the phases — the plans are at
DoR.** Each entry below records the question, the ruling, and the folding target. Two rulings
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
