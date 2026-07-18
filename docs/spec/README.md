# Specification index

The master index of Heddle's specification material — every spec document is reachable
from here ([conventions → index maintenance](common/spec-conventions.md#index-maintenance)).
Specs are contributor material, **not published** to the docs site
([D9](common/cross-cutting-decisions.md#d9--spec-pages-stay-unpublished)).

## Common specs (initiative-independent)

| Document | Purpose |
| --- | --- |
| [spec-conventions.md](common/spec-conventions.md) | How specs are structured, written, and linked; document shapes; the no-open-questions discipline; the amendment mechanism. |
| [coding-standards.md](common/coding-standards.md) | Repository style, error/diagnostic rules, hot-path performance rules, API compatibility, balanced-mode SOLID/DRY/YAGNI. |
| [testing-standards.md](common/testing-standards.md) | The canonical spec→implement→gate loop, suite homes, fixture/golden conventions, regression gates. |
| [cross-cutting-decisions.md](common/cross-cutting-decisions.md) | Shared decisions `D1`–`D9`, the claimed diagnostic-ID registry, and the amendments-ledger mechanism. |
| [breaking-windows.md](common/breaking-windows.md) | Breaking-change policy and the next-window candidate register. |

## Historical records

| Document | Purpose |
| --- | --- |
| [records.md](records.md) | Append-only record of completed work: per-window as-shipped records, the accumulated amendments ledger, registry corrections. |

## Initiatives

| Initiative | Status | Documents |
| --- | --- | --- |
| **Post-2.0 improvement effort** | Specs retired — git history; residue folded into the open 2.0 window (two items re-land additively in 2.1) | [Assessment](../language-assessment.md) (input) → `improvement-plan.md` (ratified items + tracker) → `improvement-spec.md` (normative requirements) — both **retired (git history only)**. Most consequential decisions: renderer-seam render budgets (C1), `TextEncoder`-seam encoder with byte-identical default (B2), context-encoding extensions (C2). (Originally claimed `HED4003`; its ownership moved to the `@import` removal effort below and, with both specs retired, now lives in the [amendments ledger](records.md#cross-spec-amendments-ledger).) Grounding: the [2.0 window record](records.md#the-20-breaking-window--as-shipped-record). |
| **Post-2.0 roadmap (phased)** | Specified — ready for implementation | Eight phase specs under [`post-2.0/`](post-2.0/README.md) (see the [set index](post-2.0/README.md)) elaborating the [phased plan](../plan/README.md): P1 file-watcher correctness, P2 `@@`+misread lint, P3 HTML-context lint, P4 engine robustness, P5 docs/benchmark credibility, P6 the breaking `Range` type (2.0 window), P7 [named content regions](post-2.0/phase-7-named-content-regions.md) ([grammar](post-2.0/phase-7-grammar.md), [region table](post-2.0/phase-7-region-table.md)), P8 extension parameters. New diagnostics `HED2004`/`HED4005`/`HED5019`–`HED5020`/`HED7017`. Cross-cutting decisions (shared disposal primitive, four-layer prop/region realization, 2.0-window membership) in [common/cross-cutting.md](post-2.0/common/cross-cutting.md). |
| **Legacy `@import` removal & `ProcessData` string contract** | Shipped in 2.0.0; specs retired — git history | `import-removal-plan.md` (two phases + resolved Q1/Q2/S1) → `import-removal-spec.md` — both **retired (git history only)**. Phase 1 removes `@import()` — a breaking change vs the released 1.x, absorbed into the open 2.0 major breaking window (R1): a positioned parse-layer removal error via a registered no-op tombstone, reusing/**amending** `HED4003` (warning → error; registry row + [ledger](records.md#cross-spec-amendments-ledger) updated, improvement-spec B3 unedited) and withdrawing the `@import()` next-window candidate (pulled into 2.0). Phase 2 documents the `ProcessData` value-rail string contract (docs-only). |
| **v2.0 nine-phase effort** | Shipped in 2.0.0; specs retired | Roadmap, phase specs (`phase-1` … `phase-9`), and the open-questions register are removed from the tree (git history only). Their durable residue lives on in the common specs above — the decisions `D1`–`D9`, the diagnostic registry, the amendments ledger, and the [2.0 window record](records.md#the-20-breaking-window--as-shipped-record) (including the two items that slipped the window). |
