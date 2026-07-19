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

None in flight. Everything specified to date shipped in **`v2.0.0`** (released
2026-07-19); the durable outcomes live in the documents above — the decisions `D1`–`D9`,
the [claimed diagnostic-ID registry](common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry),
the [next-window candidate register](common/breaking-windows.md#next-window-candidate-register),
and the [2.0 window record](records.md#the-20-breaking-window--as-shipped-record). A new
initiative adds its documents here per the
[conventions](common/spec-conventions.md#index-maintenance).
