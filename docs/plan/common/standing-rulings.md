# Standing rulings & conventions

Cross-cutting decisions and constraints that govern **every** phase in this plan. Stated once here;
each phase honors them without restating the rationale. These are settled — a phase may not
re-litigate them.

## Provenance

This plan implements the bounded, actionable fixes named in
[docs/language-assessment.md](../../language-assessment.md). The user set the scope and priority
directly; the rulings below capture those decisions plus the conventions the phases share.

## Standing rulings (user-set)

| # | Ruling | Consequence for the phases |
|---|---|---|
| **R1** | **Multi-region content (named regions) is delivered additively and sequenced last.** | The named-content-region feature is the final large change: [Phase 7](../phase-7-named-content-regions.md), **definitions only**. ([Phase 8](../phase-8-extension-parameters.md) — extension *parameters* — is a separate additive feature, **not** part of the region work; see the sequencing note below.) The existing single unnamed slot (`out:: Type`, `@out()`/`@out(expr)`, diagnostics HED5012–HED5018) keeps working byte-identically. It is the one non-quick-win feature, scoped so it cannot destabilize the earlier, low-risk phases. The ratified model is [R7](#standing-rulings-user-set). |
| **R2** | **The plan lives in `docs/plan/`.** | README index + `phase-N-<slug>.md` + this `common/` folder + [open-questions.md](../open-questions.md). This is a deliberate departure from the repo's prior flat `docs/<name>-plan.md` convention, chosen by the user. |
| **R3** | **Context must be explicit; the engine is a universal, byte-significant text engine.** | Heddle generates HTML, CSS, CSV, JSON, whitespace-significant plain text, and mixed/byte output. No output context may be inferred from content. The [Phase 3](../phase-3-html-context-encoding-lint.md) HTML-context lint fires **only** when an HTML context is explicitly declared (`OutputProfile.Html` / `@profile(html)`), never under `Text`/CSV/byte output, and is diagnostic-only (no auto-escaping, no rendered-byte change). |
| **R4** | **The excluded set stays out.** | Permanently out of scope: changing the meaning of `{{ }}`, colon overloading, right-to-left chains, full Go-`html/template`-style contextual auto-escaping, and ecosystem/community items. These are structural or byte-changing and no phase plans them. |
| **R5** *(revised 2026-07-12)* | **Additive by default; a bounded 2.0 breaking window is open for fundamental improvements.** | Because 2.0 is a major release ([Phase 5](../phase-5-docs-and-benchmark-credibility.md)), a change **may** break existing templates, host code, or public API **when it fundamentally improves the engine or syntax** — every such change carries a migration note and lands in the 2.0 window. Cosmetic or low-value breakage is still excluded ("lower the window *a little*"). Phases that are pure bug-fixes or naturally additive ([1](../phase-1-file-based-api-correctness.md)–[4](../phase-4-engine-robustness.md)) stay that way; the window *enables* breaking where it is fundamentally better, it does not mandate it. **Ratified breaking change:** delete `ForModel` entirely in favour of a proper `Range` type ([Phase 6](../phase-6-range-type.md)). |
| **R6** | **A partial fix that exposes a worse bug is not acceptable.** | Specifically for [Phase 1](../phase-1-file-based-api-correctness.md): all three compounding file-watcher defects (wrong filter, never-armed watcher, stale second-recompile) are one deliverable — fixing a subset would surface a latent bug (a hot reload silently serving a stale/unfinalized document) worse than today's honest no-op. |
| **R7** | **The named-content-region model is ratified** (see [Phase 7](../phase-7-named-content-regions.md)). | A component exposes overridable regions as inner definitions marked public with a leading colon (`<:name>` public, `<name>` private). Callers override public regions with the **normal override syntax** (`<name:name>` in a `@%…%@` block) inside the call body — call-scoped, gated by public/private. An override body runs in the region's own context (component props + the region's model), exactly like the default body it replaces. `@out()` is unchanged (anonymous default slot, caller context). Regions are a **definition-side** concept (an extension has no template body to host them); [Phase 8](../phase-8-extension-parameters.md) is unrelated — it gives extensions named *parameters*, not regions. |

## Conventions shared by all phases

- **WHAT/WHY, not HOW.** These are plan documents. They name the outcome, the approach, and the
  trade-off — never file-internal signatures, grammar diffs, code, or algorithm steps. Those are
  the spec's job ([spec-author](../../spec/README.md) consumes this plan).
- **Byte-identity is the back-compat gate for additive phases.** The repository runs a
  golden/differential corpus that asserts the dynamic and precompiled backends render
  byte-identically. Additive phases keep that corpus byte-identical as a measurable success
  criterion (new behavior is proven additive by the corpus not moving). A phase that takes a
  **ratified breaking change** under the R5 window instead carries a **migration note** and updates
  the affected goldens deliberately — the diff is a reviewed event, not a silent change. The
  dynamic == precompiled differential still holds in both cases.
- **R4's excluded set is not auto-reopened by the R5 window.** Opening a bounded breaking window
  does not by itself put the [R4](#standing-rulings-user-set) items (the `{{ }}` meaning, colon
  overloading, right-to-left chains, full contextual auto-escaping) back in scope — they stay
  excluded unless the user explicitly reopens one. The window exists to let the *in-scope* phases
  choose the cleaner, fundamentally-better implementation even when it breaks.
- **Diagnostics are stable `HEDxxxx` ids.** Any new diagnostic is allocated per the shared
  [diagnostic-ids](diagnostic-ids.md) plan and registered in the repository's
  [claimed-diagnostic-ids registry](../../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry).
  A diagnostic that touches a template valid today is **warning** severity so it never breaks a
  build (R5).
- **Grounding.** Every load-bearing claim about current behavior is cited to a primary source
  (repo file, doc, or a verified read-only audit) in each phase's *External grounding* table.
- **Measurable success criteria.** Each criterion is written so a spec can turn it into a test.

## Notes on cross-phase sequencing

Phases 1–5 are **each independently shippable** — every one is its own increment and none *consumes*
another's deliverable — but they are **not all surface-disjoint**: [Phase 1](../phase-1-file-based-api-correctness.md)
and [Phase 4](../phase-4-engine-robustness.md) both touch `HeddleTemplate`'s dispose/lifecycle
surface (`_runners` / `_disposeAfterComplete` / `_runtimeDocument`), so they **coordinate** rather
than proceed blind to each other. Phase 1's safe document-swap-and-release of a *superseded* runtime
document builds on the disposal synchronization Phase 4 establishes; whichever lands second
reconciles the shared bookkeeping (neither is blocked on the other, but their diffs must be merged
with the shared fields in view). The five are ordered by priority/value, not by a hard technical
dependency — see the [README ordering rationale](../README.md#ordering-rationale).

The **named-content-region feature is sequenced last by policy ([R1](#standing-rulings-user-set))** —
risk isolation, because it is the one large, cross-cutting, four-layer change:
[Phase 7](../phase-7-named-content-regions.md) delivers overridable content regions for
**definitions**. The single-slot and [`PropLayout`](../../../src/Heddle/Runtime/Expressions/PropLayout.cs)
machinery it builds on is already stable in the tree today.
[Phase 8](../phase-8-extension-parameters.md) (**extension parameters**) is **not** part of the region
feature and **does not depend on Phase 7** — it gives custom extensions the same named, typed
**parameter** surface (optional or required, mirroring definition props) that definitions already have
(props), mirroring the *existing* prop mechanism, not the new regions. It is a self-contained additive feature that sits last as a discrete item but
could land at any time.
[Phase 6](../phase-6-range-type.md) (the `Range` type) is **not** the policy-sequenced-last feature —
it is a small, self-contained **breaking** cleanup grouped with the fixes under the
[R5](#standing-rulings-user-set) 2.0 window, sequenced by value, not by R1.
