# Specification index

The master index of Heddle's specification material — every spec document is reachable
from here ([conventions → index maintenance](common/spec-conventions.md#index-maintenance)).
Specs are contributor material, **not published** to the docs site
([D9](common/cross-cutting-decisions.md#d9--spec-pages-stay-unpublished)).

## Common specs (initiative-independent)

| Document | Purpose |
| --- | --- |
| [spec-conventions.md](common/spec-conventions.md) | How specs are structured, written, and linked; document shapes; the no-open-questions discipline; the amendment mechanism. |
| [coding-standards.md](common/coding-standards.md) | Repository style, the comment rules (C1–C8), error/diagnostic rules, hot-path performance rules, API compatibility, balanced-mode SOLID/DRY/YAGNI. |
| [testing-standards.md](common/testing-standards.md) | The canonical spec→implement→gate loop, suite homes, fixture/golden conventions, regression gates, precompiled-tier posture, test-input single-sourcing, documentation currency. |
| [cross-cutting-decisions.md](common/cross-cutting-decisions.md) | Shared decisions `D1`–`D11`, the condensed release records and collapsed program records of the two completed initiatives, the claimed diagnostic-ID registry, and the (closed) amendments-ledger mechanism. |
| [breaking-windows.md](common/breaking-windows.md) | Breaking-change policy, the running record of the current open window (2.1), and the next-window candidate register. |
| [shared-source-architecture.md](common/shared-source-architecture.md) | The generator ↔ engine code-sharing architecture: constraints on shared source, linked-`Compile` conventions, the diagnostic catalog single source, the `LineIndex` rule. |
| [review-protocol.md](common/review-protocol.md) | The mandatory reviewer protocol: exhaustive catalogue over first-found; inventories, class expansion, coverage ledger; findings land as tests. |
| [findings-register.md](common/findings-register.md) | Recurring mistake classes, code that looks wrong but is correct, open items, and the unverified platform surface. Code and tests never cite it. |

Both completed initiatives — the cross-stack benchmark program and the generator ↔ engine
code-sharing program — are collapsed into
[cross-cutting-decisions.md](common/cross-cutting-decisions.md)'s program records; the
benchmark harnesses' operational contract lives in
[benchmarks/docs/](../../benchmarks/docs/README.md). Full retired documents:
`git show c4691266:docs/spec/` and `git show c4691266:docs/generator_plan/`.
