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

| Initiative | Status | Summary |
| --- | --- | --- |
| [cross-stack-benchmarks](cross-stack-benchmarks/README.md) | Specified — ready for implementation | Eight-phase cross-stack benchmark program (foundation; Rust, JVM, JS, Python, Go ecosystems; consolidated report; Linux cross-check): one golden corpus, parity contract v2 with the N1–N5 + N3b strip-at-comparison pipeline, and one metrics/publication protocol owned by Phase 1; per-ecosystem harnesses under top-level `benchmarks/<ecosystem>/`; wall time per render as the only cross-comparable metric, with honest-reporting rules and no cross-ecosystem ranking of non-Heddle engines. |
