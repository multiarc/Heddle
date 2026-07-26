---
paths:
  - "docs/spec/**"
  - "docs/generator_plan/**"
  - "docs/plan/**"
---

# Spec and decision workflow

Details: [spec-conventions.md](../docs/spec/common/spec-conventions.md), [cross-cutting-decisions.md](../docs/spec/common/cross-cutting-decisions.md), records in [docs/spec/records.md](../docs/spec/records.md).

- Specs live under `docs/spec/`, indexed from `docs/spec/README.md` (update the index in the same change); they are contributor material, never published (D9).
- Plan = what/why (ratified input); spec = exactly how. A finished spec has **no open questions** — every point is a closed decision with evidence, or a most-reversible default plus a named revisit trigger. Maintainer-level unresolved items go to the initiative's `OPEN-QUESTIONS.md` register with a provisional default.
- Implementation follows the owning plan's declared item order (D5): assume 1..N−1 merged; never depend on later items.
- Changing an earlier recorded decision goes through the amendments ledger (evidence → maintainer ratification → implemented by the amending effort); earlier spec text is never silently retrofitted. Current truth = spec + ledger.
- Balanced-mode principle precedence on conflicts: correctness/back-compat → sandbox security → simplicity → measured hot-path performance → abstraction/extensibility. DRY consolidates knowledge (rule of three); YAGNI cuts presumptive features, never tests/benchmarks/docs.
- Writing style: sentence-case headings, en/em dashes, relative links only, cite `path` + member name over bare line numbers.
