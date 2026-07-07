# Specification conventions

How every phase specification under `docs/spec/` is structured, written, and linked. This
document is normative for spec **authors** (human or agent); the
[coding standards](coding-standards.md) and [testing standards](testing-standards.md) are
normative for the **implementation** the specs describe.

> Specs are contributor material and are **not published** to the docs site (same treatment
> as `assessment.md` and `docs/roadmap/`). See the
> [pending integration note](../README.md#publication-status) in the spec index.

## Relationship to the roadmap

Each spec elaborates exactly one roadmap phase document from
[docs/roadmap/](../../roadmap/README.md). The division of labour:

- **Roadmap** — *what* and *why*: goal, verified design direction, back-compat analysis,
  risks, success criteria, validation scenarios, grounding. The roadmap is the ratified
  input; a spec must not silently contradict it.
- **Spec** — *exactly how*: file-level implementation plan, final API signatures, grammar
  diffs, decision records that close every remaining question, concrete test assets, and
  work-item ordering. A spec is done when an implementer can execute it without making a
  single undocumented design decision.

If verification during spec writing shows a roadmap claim is wrong (a line number moved, a
seam behaves differently, an external fact changed), the spec **records the correction with
evidence** in the affected decision record and proceeds — it does not edit the roadmap
document.

## Sequential-but-separate implementation assumption

The specs assume phases are implemented **in roadmap order (1 → 9), each as its own
separate effort**, and the sequence **culminates in one combined v2.0 release**: v2.0 is
all nine phases together, plus the breaking deltas consolidated in
[migration-2.0.md](migration-2.0.md). During the sequence every phase is implemented
with compatibility defaults so the tree stays green and shippable throughout; the
default flips execute in the release tail per the migration spec. Whether interim 1.x
minors ship along the way is a release-management choice — the specified target is the
combined 2.0. Concretely, for phase *N*:

- The spec **may assume** phases 1..*N*−1 are merged and their public APIs exist. It must
  say explicitly, in its *Assumed state* section, which prior-phase artifacts it builds on
  (with links to those specs).
- The spec **must not depend on** anything from phases *N*+1..9. Forward pointers
  ("phase 8 will widen this seam") are allowed as non-normative notes.
- Where the roadmap describes an independent-shipping fallback (a phase working without an
  earlier one), the spec records it under *Deferred items* — the specified build order is
  sequential.

## Folder layout and naming

```
docs/spec/
  README.md                        ← master index (every document reachable from here)
  common/                          ← shared, phase-independent specs (this folder)
  phase-N-<slug>/                  ← one folder per phase; slug matches the roadmap file
    README.md                      ← the entry document (the spec proper)
    <topic>.md                     ← optional supplementary documents
```

- The phase slug matches the roadmap filename: `phase-1-native-expressions`,
  `phase-2-safe-output`, … `phase-9-demo-and-integration`.
- `README.md` is always the entry point. Supplementary documents (grammar spec, API
  reference, test plan) are optional — split when a section is large and self-contained,
  or when it serves a different audience than the entry document.
- **There is no size limit on any spec document.** Completeness always wins over length:
  material (worked examples, tables, per-item detail, rationale) is never condensed or
  omitted to keep a document short. The only length rule is the ban on padding and
  boilerplate.
- **Discoverability rule:** every supplementary document is listed in the entry document's
  *Documents* table with a one-line purpose, links back to the entry document in its first
  paragraph, and is reachable from the [master index](../README.md). No orphan files.

## Required structure of a phase spec entry document

Sections in this order (a supplementary doc may absorb a section's body, but the entry doc
keeps the heading with a summary and link):

1. **Header block** — status line (`Specified — ready for implementation`), link to the
   roadmap phase doc, links to the specs of assumed prior phases, and the *Documents* table
   when the spec spans multiple files.
2. **Scope and goal** — one or two paragraphs restating the phase goal and the boundary of
   this spec.
3. **Assumed state** — the codebase as this phase finds it: which prior phases are merged,
   which of their APIs/seams this spec uses, and the verified state of the seams this phase
   touches (actual file paths and current behavior, re-checked against source — not trusted
   from the roadmap).
4. **Design decisions** — numbered records `D1, D2, …`, each with **Decision**,
   **Rationale**, **Alternatives rejected** (with the reason), and where relevant
   **Grounding** (links). Every roadmap *Open question*, *lean*, and *pin during design*
   item for the phase must appear here as a closed decision. See
   [No open questions](#no-open-questions) below.
5. **Implementation plan** — ordered work items; for each: the files added/changed (exact
   repo paths), the shape of the change (signatures, grammar rule diffs, emitted code
   shapes), and the completion check. This is the section an implementer works down.
6. **Public API contract** — every new/changed public type and member with its signature,
   XML-doc-level description, and thread-safety notes. Additions must respect the
   [API design rules](coding-standards.md#api-design-and-compatibility).
7. **Diagnostics** — every new compile error/warning: its `HED*` ID (claimed from the
   [diagnostic registry](cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)),
   message text, trigger condition, and position semantics.
8. **Testing plan** — instantiates the [testing standards](testing-standards.md): the named
   fixtures/goldens/benchmarks this phase adds, the TDD verdict carried from the roadmap,
   the regression gate, and the phase's deferred phase 9 gallery item.
9. **Back-compat and migration** — what existing behavior is preserved (and how that is
   proven), and anything scheduled into the 2.0 breaking window per
   [D2](cross-cutting-decisions.md#d2--the-single-20-breaking-window).
10. **Performance considerations** — hot paths touched, allocation expectations, and which
    benchmark guards them.
11. **Standards compliance** — a short, *phase-specific* note on how the
    [balanced-mode principles](coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
    bite in this phase: where abstraction is warranted by a second consumer, where YAGNI
    cuts scope, where DRY consolidates knowledge. No boilerplate — only the calls actually
    made.
12. **Deferred items** — everything consciously excluded, each with the trigger that would
    revisit it.
13. **External references** — the grounding links used by this spec (primary sources
    preferred: Microsoft Learn, ANTLR docs, engine/library documentation).

## No open questions

A finished spec contains **no** "TBD", "TODO", "open question", or "decide at
implementation" text. The discipline:

- Take the roadmap's recorded *lean* as the default answer; verify it (source reading
  and/or web research); record it as a decision with the verification evidence.
- If verification contradicts the lean, decide the corrected answer and record both the
  correction and the evidence.
- If evidence is genuinely insufficient to distinguish alternatives, **choose the most
  reversible option**, record why it is reversible, and name the concrete trigger that
  would cause revisiting. That is a decision, not an open question.
- The single sanctioned home for **maintainer-level** unresolved items is the
  [open-questions register](../OPEN-QUESTIONS.md): entries there carry a provisional
  default that the specs implement, so work never blocks on them. A spec that depends on
  such an item cites the `OQ#` next to its decision and proceeds on the default.

## Amendments during implementation

Specs are living documents until their phase ships, and v2.0 is expected to absorb
**amendments discovered during implementation**. The mechanism is the
[cross-spec amendments ledger](cross-cutting-decisions.md#cross-spec-amendments-ledger):
a change to an earlier phase's recorded decision is proposed as a ledger entry (with the
evidence that forced it), ratified by the maintainer, and implemented by the phase that
needs it — the earlier spec's text is never silently retrofitted. The current end-state
of any decision is therefore *its spec plus the ledger*. Resolving an open-questions
register entry against its default follows the same path: ratification (or override)
lands as a ledger entry and the register row is closed.

## Writing style

Match the repository's documentation style (the roadmap docs are the reference):

- En- and em-dashes (`–`, `—`), sentence-case headings, GitHub-flavored Markdown tables.
- Template snippets in `heddle` code fences; C# in `csharp`; grammar in `antlr`.
- Relative links only. From a `docs/spec/phase-N-<slug>/` document:
  - source: `../../../src/Heddle/...`
  - roadmap: `../../roadmap/phase-N-<slug>.md`
  - common specs: `../common/coding-standards.md`
  - other phase specs: `../phase-M-<slug>/README.md`
  - published docs pages: `../../language-reference.md`
- Cite source locations as `path` + member name rather than bare line numbers where
  practical (line numbers drift); when a line number is load-bearing, state the anchor
  symbol next to it.
- Each spec must be readable stand-alone given the common specs — restate the minimal
  context it needs instead of requiring other phase specs open side-by-side.

## Index maintenance

When a phase spec lands, its row in the [master index](../README.md) is updated in the same
change: status, entry-document link, supplementary-document links, and a one-line summary
of the most consequential decisions. The index is the only place a reader should need to
start from.
