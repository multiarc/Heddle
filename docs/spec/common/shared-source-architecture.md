# Shared-source architecture — what still governs

> **Reduced 2026-09-06 (phase 4):** the generator ↔ engine shared-source architecture is
> gone with the 2.x generator — no file is linked into another project's compile any
> more; every consumer references the `Heddle` assembly. What had ongoing force is kept
> below. Everything else collapsed into the
> [program record — generator ↔ engine code-sharing (closed)](cross-cutting-decisions.md#program-record--generator--engine-code-sharing-closed)
> and the [program record — precompilation v2 (closed)](cross-cutting-decisions.md#program-record--precompilation-v2-closed).

## The language front end

The `Heddle.Language` front end (parser, binding, projection) lives in `Heddle.dll` and
serves every host from the one implementation: the runtime engine, the `heddle compile`
build host (through the real engine, out of process), and the language service (which
layers LSP re-anchoring on top of the shared projection). There is no second
implementation to keep in lockstep and no per-host fork.

## Diagnostic catalog and projection — single source

- [HeddleDiagnosticCatalog.cs](../../../src/Heddle/Data/HeddleDiagnosticCatalog.cs) is the
  single code-side registry of diagnostic identity (id → title, severity, message).
  The catalog row is the single place severity lives.
- [HeddleDiagnosticProjection.cs](../../../src/Heddle/Language/HeddleDiagnosticProjection.cs)
  is the one drain rule over parse **and** compile channels — a neutral
  `(Id, Message, Fix, IsWarning, Offset, Length, ImportOrigin)` stream; host policies
  (LSP import re-anchoring, `HeddleCompileResult` rendering) stay host-side and layer
  on top.
- The standing gate is `DiagnosticIdTests`: constants completeness; the catalog
  bijection; the docs-registry gates parsing the actual markdown tables; catalog
  round-trip on message-format arity; projection equivalence across hosts
  (`DiagnosticCorpusVectors`); the `LineIndex` goldens.
- An ID whose condition becomes unreachable is **retired in place**, never deleted or
  reused.

## `LineIndex` — the `\n`-only rule

Normative, documented on [LineIndex.cs](../../../src/Heddle/Data/LineIndex.cs) itself:

> A line starts at offset 0 and after each `'\n'`; `'\r'` is never a terminator by itself; a
> `'\r'` adjacent to a `'\n'` belongs to the line that `'\n'` terminates; offsets and columns
> count UTF-16 code units.

## Template identity

Template identity and naming has one owner —
[D8](cross-cutting-decisions.md#d8--template-identity--naming-policy-has-one-owner) — and
shared code participates through the `TemplateKey` helpers only; no second key grammar
may exist on either side.
