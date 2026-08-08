---
paths:
  - "docs/spec/**"
---

# Spec and decision workflow

Details: [spec-conventions.md](../docs/spec/common/spec-conventions.md), [cross-cutting-decisions.md](../docs/spec/common/cross-cutting-decisions.md) (which also carries the condensed release and program records).

- Specs live under `docs/spec/`, indexed from `docs/spec/README.md` (update the index in the same change); they are contributor material, never published (D9).
- Plan = what/why (ratified input); spec = exactly how. A finished spec has **no open questions** — every point is a closed decision with evidence, or a most-reversible default plus a named revisit trigger.
- Implementation follows the owning plan's declared item order (D5): assume 1..N−1 merged; never depend on later items.
- Changing an earlier recorded decision goes through the amendment mechanism (evidence → maintainer ratification → implemented by the amending effort); earlier spec text is never silently retrofitted. The accumulated E1–E28 ledger is closed — new amendments are recorded as dated notes in the owning spec (see cross-cutting-decisions.md § Cross-spec amendments ledger).
- Balanced-mode principle precedence on conflicts: correctness/back-compat → sandbox security → simplicity → measured hot-path performance → abstraction/extensibility. DRY consolidates knowledge (rule of three); YAGNI cuts presumptive features, never tests/benchmarks/docs.
- Writing style: sentence-case headings, en/em dashes, relative links only, cite `path` + member name over bare line numbers.
