# Post-2.0 roadmap — specification set

> The **HOW, exactly** for the phased [post-2.0 plan](../../plan/README.md): eight phase specs that
> close every plan open question, lean, and pin into a decided, buildable artifact. These are
> contributor material, **not published** to the docs site
> ([D9](../common/cross-cutting-decisions.md#d9--spec-pages-stay-unpublished)), and are consumed by
> [spec-orchestrate](../../../) for implementation. Governance (the standing rulings R1–R7, the
> WHAT/HOW boundary, the byte-identity back-compat gate) lives in the plan's
> [standing rulings](../../plan/common/standing-rulings.md) and is honored here without restatement.

Phases 1–5 are additive fixes; Phase 6 is a ratified **breaking** cleanup in the still-open 2.0
window; Phase 7 is the one large cross-cutting language feature; Phase 8 is a separate additive
feature. Every phase's *Assumed state* was **re-verified against current source** (not trusted from
the plan) — the corrections found are recorded in each spec and summarized in
[common/cross-cutting.md](common/cross-cutting.md).

## Common specifications

Every phase and common doc is linked here — no orphans.

| Document | Shared by | Purpose |
|---|---|---|
| [common/cross-cutting.md](common/cross-cutting.md) | all phases | The four cross-cutting decisions the set depends on: the shared `HeddleTemplate` disposal primitive (P1↔P4), the four-layer prop/region realization (P7 & P8), the coordinated diagnostic-ID allocation (P2/P3/P7/P8), and Phase 6's membership in the still-open 2.0 breaking window. Points at the repo-global [diagnostic registry](../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry). |
| [common/cross-cutting-decisions.md](../common/cross-cutting-decisions.md) (repo-global) | all specs | Shared decisions D1–D9, the **claimed diagnostic-ID registry** (where this set's `HED2004`/`HED4005`/`HED5019`–`HED5020`/`HED7017` are claimed), and the amendments ledger. |
| [common/coding-standards.md](../common/coding-standards.md) · [common/testing-standards.md](../common/testing-standards.md) · [common/breaking-windows.md](../common/breaking-windows.md) (repo-global) | all specs | Repository style, error/perf/compat rules; the spec→implement→gate loop and regression gates; the breaking-window policy Phase 6 lands under. |

## Phase specifications

| # | Spec | Source plan | Status / consequential decisions (one line) |
|---|---|---|---|
| 1 | [phase-1-file-watcher](phase-1-file-watcher.md) | [plan](../../plan/phase-1-file-based-api-correctness.md) | Ready. Three watcher defects as one deliverable (R6); recompile into a **fresh** `CompileScope` (resetting the reused scope's `Compiled` flags would leak diagnostics / collide the Roslyn key); superseded-document release consumes Phase 4's disposal primitive. |
| 2 | [phase-2-authoring-ergonomics](phase-2-authoring-ergonomics.md) | [plan](../../plan/phase-2-authoring-ergonomics.md) | Ready. `@@`→`@` as a **lexer** change (retype `@@` to the existing `RAW` token — no parser change; one ANTLR regen work item); misread lint = warning **HED4005** as a compiler pass on `CompileWarnings`. |
| 3 | [phase-3-html-context-encoding-lint](phase-3-html-context-encoding-lint.md) | [plan](../../plan/phase-3-html-context-encoding-lint.md) | Ready. Warning **HED2004** (`MissingContextEncoder`); left-only adjacent-literal heuristic for attribute/`<script>`/URL positions; gated strictly on an explicitly declared `Html` profile (R3); byte-neutral by construction. |
| 4 | [phase-4-engine-robustness](phase-4-engine-robustness.md) | [plan](../../plan/phase-4-engine-robustness.md) | Ready. Lock-free disposal (`Interlocked` `EnterRender`/`ExitRender`/`Teardown`); full-parity `TryCompilation`; opt-in `TemplateOptions.ValidateModelType` (default off, excluded from identity). No new diagnostic id. |
| 5 | [phase-5-docs-and-benchmark-credibility](phase-5-docs-and-benchmark-credibility.md) | [plan](../../plan/phase-5-docs-and-benchmark-credibility.md) | Ready. One canonical 2.0.0-forward version statement; dead-link retirement across three living docs; two new parity-checked benchmark workloads (trivial-substitution, large-loop). Docs + `Heddle.Performance` only. |
| 6 | [phase-6-range-type](phase-6-range-type.md) | [plan](../../plan/phase-6-range-type.md) | Ready (**breaking**, 2.0 window). New `public readonly struct Heddle.Models.Range`; deletes `ForModel`; `Range.FromSystemRange` interop; readable `ToString()`; 17-file break surface + migration note. |
| 7 | [phase-7-named-content-regions](phase-7-named-content-regions.md) ([grammar](phase-7-grammar.md), [region table](phase-7-region-table.md)) | [plan](../../plan/phase-7-named-content-regions.md) | Ready. Public overridable regions as leading-colon inner definitions (`<:name>`, parser-only grammar diff); fill via the existing `<name:name>` override bound call-scoped; region table across four layers with **native precompilation** of region defaults and overridden region fills (maintainer ruling [OQ1](OPEN-QUESTIONS.md); the plain sibling-override idiom keeps its pre-existing silent degrade); errors **HED5019**/**HED5020** (narrowing reuses the existing check; no HED5021); `@out()` byte-identical. |
| 8 | [phase-8-extension-parameters](phase-8-extension-parameters.md) | [plan](../../plan/phase-8-extension-parameters.md) | Ready. `Heddle.Attributes.PropAttribute` on extension classes; `Scope.TryGetParameter`/`GetParameter` read channel; reuses the prop diagnostics, relaxes `HED5005` only, generator twin **HED7017**. Call-site symmetry with definition props. |

## Open questions

See [OPEN-QUESTIONS.md](OPEN-QUESTIONS.md). It holds **only maintainer-level items, each with a
provisional default the specs implement now** — never an author-level "I didn't decide". Every plan
open question (P1–P8) is closed inside its phase spec as a decision with evidence.
