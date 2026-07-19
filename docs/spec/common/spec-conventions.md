# Specification conventions

How specifications under `docs/spec/` — and the top-level spec documents they index — are
structured, written, and linked, for **any** spec initiative. This
document is normative for spec **authors** (human or agent); the
[coding standards](coding-standards.md) and [testing standards](testing-standards.md) are
normative for the **implementation** the specs describe.

> Specs are contributor material and are **not published** to the docs site
> ([D9](cross-cutting-decisions.md#d9--spec-pages-stay-unpublished)). Every spec document
> is reachable from the [master index](../README.md).

## Relationship to the owning plan

Every spec elaborates exactly one **ratified planning document** (a roadmap phase, an
improvement-plan item set, or an equivalent). The division of labour:

- **Plan** — *what* and *why*: goal, verified design direction, back-compat analysis,
  risks, success criteria, priorities and item ordering. The plan is the ratified input;
  a spec must not silently contradict it.
- **Spec** — *exactly how*: file-level implementation plan, final API signatures, grammar
  diffs (when licensed), decision records that close every remaining question, concrete
  test assets, and work-item ordering. A spec is done when an implementer can execute it
  without making a single undocumented design decision.

If verification during spec writing shows a plan claim is wrong (a line number moved, a
seam behaves differently, an external fact changed), the spec **records the correction
with evidence** in the affected decision/requirement record and proceeds — it does not
edit the plan document.

## Dependency order

A spec initiative declares its implementation order in its owning plan. For item *N* in
that order ([D5](cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order)):

- The spec **may assume** items 1..*N*−1 are merged and their public APIs exist. It says
  explicitly, in its *Assumed state* (or equivalent) section, which prior artifacts it
  builds on.
- The spec **must not depend on** later items. Forward pointers ("a later item will widen
  this seam") are allowed as non-normative notes.
- Cross-initiative dependencies point only at **shipped** releases, never at another
  initiative's unimplemented spec.

## Document shapes, folder layout, and naming

Two sanctioned shapes:

- **Folder-shaped** (large initiatives): `docs/spec/<initiative>/` with `README.md` as the
  entry document and optional supplementary documents.
- **Single-document** (compact initiatives): one file, either `docs/<name>-spec.md` at the
  docs root or `docs/spec/<initiative>.md`. All required *content* (below) still applies; sections may
  be merged where the document stays navigable.

```
docs/spec/
  README.md                        ← master index (every document reachable from here)
  common/                          ← shared, initiative-independent specs (this folder)
  <initiative>/ or <initiative>.md ← one folder or file per initiative
```

- **There is no size limit on any spec document.** Completeness always wins over length:
  material (worked examples, tables, per-item detail, rationale) is never condensed or
  omitted to keep a document short. The only length rule is the ban on padding and
  boilerplate.
- **Discoverability rule:** every supplementary document is listed in its entry document's
  *Documents* table with a one-line purpose, links back to the entry document in its first
  paragraph, and is reachable from the [master index](../README.md). No orphan files.

## Required structure of a spec

The following content is required in this order for folder-shaped entry documents;
single-document specs carry the same content and may merge sections (e.g. merging
*Design decisions* into numbered requirement records, or hoisting shared constraints
into a *Global invariants* section — both satisfy the intent):

1. **Header block** — status line (`Specified — ready for implementation`), link to the
   owning plan item(s), links to assumed prior specs, and the *Documents* table when the
   spec spans multiple files.
2. **Scope and goal** — one or two paragraphs restating the goal and this spec's boundary.
3. **Assumed state** — the codebase as this work finds it: which prior items are merged,
   which of their APIs/seams this spec uses, and the verified state of the seams this work
   touches (actual repo paths and current behavior, re-checked against source — not
   trusted from the plan).
4. **Design decisions / requirements** — numbered records with stable IDs (`D1, D2, …` or
   `<item>-R1, -R2, …`), each with **Decision**, **Rationale**, **Alternatives rejected**
   (with the reason), and where relevant **Grounding** (links). Every plan-level open
   point for the item must appear here as a closed decision. See
   [No open questions](#no-open-questions).
5. **Implementation plan** — ordered work items; for each: the files added/changed (exact
   repo paths), the shape of the change (signatures, grammar rule diffs, emitted code
   shapes), and the completion check.
6. **Public API contract** — every new/changed public type and member with its signature,
   XML-doc-level description, and thread-safety notes, respecting the
   [API design rules](coding-standards.md#api-design-and-compatibility).
7. **Diagnostics** — every new compile error/warning: its `HED*` ID (claimed from the
   [diagnostic registry](cross-cutting-decisions.md#claimed-diagnostic-ids-registry) in
   the same change), message text, trigger condition, and position semantics.
8. **Testing plan** — instantiates the [testing standards](testing-standards.md): named
   fixtures/goldens/benchmarks, the TDD verdict, the regression gate, and the sample /
   gallery item when the change is user-visible.
9. **Back-compat and migration** — what existing behavior is preserved (and how that is
   proven), and anything scheduled into a breaking window per
   [D2](cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows) /
   [breaking-windows.md](breaking-windows.md).
10. **Performance considerations** — hot paths touched, allocation expectations, and which
    benchmark guards them.
11. **Standards compliance** — a short, *item-specific* note on how the
    [balanced-mode principles](coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
    bite here: where abstraction is warranted by a second consumer, where YAGNI cuts
    scope, where DRY consolidates knowledge. No boilerplate — only the calls actually
    made.
12. **Deferred items / non-goals** — everything consciously excluded, each with the
    trigger that would revisit it.
13. **External references** — the grounding links used by this spec (primary sources
    preferred: Microsoft Learn, ANTLR docs, engine/library documentation).

## No open questions

A finished spec contains **no** "TBD", "TODO", "open question", or "decide at
implementation" text. The discipline:

- Take the plan's recorded *lean* as the default answer; verify it (source reading and/or
  web research); record it as a decision with the verification evidence.
- If verification contradicts the lean, decide the corrected answer and record both the
  correction and the evidence.
- If evidence is genuinely insufficient to distinguish alternatives, **choose the most
  reversible option**, record why it is reversible, and name the concrete trigger that
  would cause revisiting. That is a decision, not an open question.
- *(Verify at implementation)* markers are permitted only for **facts** this spec pinned
  against the current source that the implementer must re-confirm before relying on them
  (line positions, seam behavior) — never for undecided design.
- Maintainer-level unresolved items live in an initiative-owned **open-questions
  register** (`docs/spec/<initiative>/OPEN-QUESTIONS.md`, created only when needed):
  entries carry a provisional default that the specs implement, so work never blocks on
  them. A spec that depends on such an item cites the `OQ#` next to its decision and
  proceeds on the default. A register closes with its initiative; an initiative that
  manages to close every question against ratified prior decisions and most-reversible
  defaults never creates one.

## Amendments during implementation

Specs are living documents until their work ships, and every release is expected to
absorb **amendments discovered during implementation**. The mechanism is the
[cross-spec amendments ledger](cross-cutting-decisions.md#cross-spec-amendments-ledger):
a change to an earlier recorded decision is proposed as a ledger entry (with the evidence
that forced it), ratified by the maintainer, and implemented by the effort that needs it —
the earlier spec's text is never silently retrofitted. The current end-state of any
decision is therefore *its spec plus the ledger*. This applies across initiatives:
later efforts amend earlier efforts' decisions through the same mechanism.

## Writing style

Match the repository's documentation style:

- En- and em-dashes (`–`, `—`), sentence-case headings, GitHub-flavored Markdown tables.
- Template snippets in `heddle` code fences; C# in `csharp`; grammar in `antlr`.
- Relative links only. From a `docs/spec/common/` document: source is
  `../../../src/Heddle/...`, published docs pages are `../../language-reference.md`,
  top-level spec documents are `../../<name>-spec.md`. From a
  `docs/spec/<initiative>/` document, add one more `../` for the docs root.
- Cite source locations as `path` + member name rather than bare line numbers where
  practical (line numbers drift); when a line number is load-bearing, state the anchor
  symbol next to it.
- Documents removed from the tree (retired plans, specs, registers) are cited as
  **plain text with a "(retired)" qualifier**, never linked — they exist only in git
  history.
- Each spec must be readable stand-alone given the common specs — restate the minimal
  context it needs instead of requiring other specs open side-by-side.

## Index maintenance

When a spec lands or changes status, its row in the [master index](../README.md) is
updated in the same change: status, entry-document link, supplementary-document links,
and a one-line summary of the most consequential decisions. The index is the only place
a reader should need to start from.
