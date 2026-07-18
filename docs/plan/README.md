# Plan — post-assessment fixes & 2.0 improvements

> A phased plan that began from the bounded, actionable issues in
> [docs/language-assessment.md](../language-assessment.md) and grew, by user direction, to include
> three fundamental improvements for the imminent **2.0** release: a proper `Range` type (retiring
> `ForModel`), a named-content-region model for definitions (Phase 7), and named parameters for
> custom extensions (Phase 8). Phases 1–5 are additive fixes; Phase 6 is a small **breaking** cleanup;
> Phase 7 is the named-region language feature; Phase 8 is a separate additive feature giving
> extensions the parameter surface definitions already have. Governance for all phases — including the
> **bounded 2.0 breaking window** — lives in [standing rulings](common/standing-rulings.md).

## Phases

| # | Phase | Depends on | Goal (one line) | Status |
|---|---|---|---|---|
| 1 | [file-based-api-correctness](phase-1-file-based-api-correctness.md) | — | Restore working recompile-on-change for file-compiled templates (three compounding watcher bugs). | ready |
| 2 | [authoring-ergonomics](phase-2-authoring-ergonomics.md) | — | Add a `@@` literal-`@` escape and a `{{ x }}`-in-text misread warning. | ready |
| 3 | [html-context-encoding-lint](phase-3-html-context-encoding-lint.md) | — | Under an explicitly declared HTML profile, warn when `@(value)` sits in an attribute/script/URL position without `@attr`/`@js`/`@url`. | ready |
| 4 | [engine-robustness](phase-4-engine-robustness.md) | — | Close the concurrent-dispose race, the `TryCompilation` false-confidence gap, and add an opt-in Release model-type check. | ready |
| 5 | [docs-and-benchmark-credibility](phase-5-docs-and-benchmark-credibility.md) | — | One coherent (2.0-forward) version status, no dangling spec links, and more than one benchmark workload. | ready |
| 6 | [range-type](phase-6-range-type.md) | — | **Breaking:** replace `ForModel` with a proper `Range` type; remap `@for`/`range()`. | ready |
| 7 | [named-content-regions](phase-7-named-content-regions.md) | — (sequenced last) | Public, overridable, optionally-typed content regions on definitions (`<:name>` + normal override). | ready |
| 8 | [extension-parameters](phase-8-extension-parameters.md) | — | Custom extensions declare named, typed parameters (optional or required, mirroring definition props; attribute-based), passed by name at the call site — call-site symmetry with definition props. | ready |

## Ordering rationale

- **Phases 1–5 — additive bounded fixes**, ordered by priority/value (the user's stated priorities
  blended with the assessment's severity): file-watcher correctness first (a reproduced, top-priority
  bug), then the two ergonomics wins, the explicit-context encoding lint, the engine-robustness
  hardening, and the docs/benchmark credibility pass.
- **Phase 6 — `Range` type** is a small, self-contained cleanup grouped with the fixes, but it is a
  **ratified breaking change** (deletes `ForModel`) taken under the R5 2.0 window — hence its own
  phase with a migration note.
- **Phase 7 — the named-content-region feature**, sequenced last among the large changes because it
  is the one cross-cutting language feature; it delivers overridable content regions for
  **definitions** across all four layers (dynamic runtime, generator, LSP, precompiled binding).
- **Phase 8 — extension parameters** is a separate, self-contained additive feature: it gives custom
  extensions the same named, typed **parameter** surface (optional or required, mirroring definition
  props) definitions already have (props), passed by name at the call site. It is **independent of Phase 7** (it mirrors the *existing*
  definition-prop mechanism, not the new region model) and could land at any time; it sits last as a
  discrete feature.

**Provenance.** Phases 1–5 derive from the assessment. Phase 6 (`Range`/`ForModel`), Phase 8
(extension parameters), and the opening of the 2.0 breaking window (revised [R5](common/standing-rulings.md))
are **user-added scope** beyond the assessment, decided during the Q&A debate; Phase 7's design is
the ratified outcome of that debate ([R7](common/standing-rulings.md)).

## Common documents

| Document | Shared by | Purpose |
|---|---|---|
| [standing-rulings.md](common/standing-rulings.md) | all phases | User-set rulings (R1–R7), the bounded 2.0 breaking window, the WHAT/HOW boundary, the back-compat gate, and cross-phase sequencing. |
| [diagnostic-ids.md](common/diagnostic-ids.md) | phases 2, 3, 7, 8 | Coordinated allocation of the new `HEDxxxx` diagnostics (`HED4005`, `HED2004`, `HED5019`+) plus Phase 8's reuse/relaxation of the prop family (`HED5001`–`HED5004` reused for extensions, `HED5005` relaxed, declaration-side `HED5007`/`HED5009`/`HED5010`/`HED5015` on the dynamic tier, generator twin `HED7017`) so they cannot collide. |

## Open questions

See [open-questions.md](open-questions.md) for the Q&A register. **Plan DoR = the Open section is
empty — and it is:** every question (P1–P8) is resolved and folded into its phase document. The
consequential product/design decisions were settled with the user (the named-region model is
ratified in full — [R7](common/standing-rulings.md)); the remaining author-level choices are
resolved from grounding, each carried with its lean, and any residual is spec-level (HOW) only.
