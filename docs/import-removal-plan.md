# Heddle `@import` Removal Plan — Retire the Legacy Include, Document the String Contract

*A focused, two-phase plan. Phase 1 removes the legacy `@import()` extension in favour of the two
constructs that already cover its real uses. Phase 2 writes down the unwritten string-contract on
`ProcessData` that `@import`'s replacements — and every custom extension — depend on. Every claim
below was verified against the source on this branch; file/line references point at the exact code.*

> **Version context — supersedes a Non-goal in the improvement plan.** Heddle 2.0 is **unreleased**.
> Removing `@import()` in the 2.0 window is therefore **evolution of an unshipped surface, not a
> breaking change**. This plan explicitly supersedes
> [improvement-plan.md:279](improvement-plan.md#L279) ("Removing `@import()` outright | Breaking …
> removal is a 3.0 decision") and the gradual-drain framing of B3 there
> ([improvement-plan.md:144–151](improvement-plan.md#L144-L151)): with no released version to break,
> the deprecation warning graduates directly to a removal. The two documents must not be read as
> contradicting — this one is the later decision.

---

## Sequencing

The two phases are **independent** — neither depends on the other and they may land in either order
or in parallel. They are grouped here only because both were surfaced by the same review of the
`@import`/`ProcessData` seam. Phase 1 is the substantive change (a language surface goes away);
Phase 2 is a documentation-only clarification of a contract that already holds.

| # | Phase | Depends on | Goal (one line) | Kind | Status |
|---|---|---|---|---|---|
| 1 | [Remove legacy `@import()`](#phase-1--remove-the-legacy-import-extension) | nothing | `@import()` no longer compiles; a positioned removal error steers authors to `@<<` / `@partial` | Evolution (unreleased surface) | in progress |
| 2 | [Document the `ProcessData` string contract](#phase-2--document-the-processdata-string-contract) | nothing | The value/string rail's "return a string" contract is written where authors read it | Docs-only | in progress |

**Ordering rationale.** No technical ordering constraint exists. If a release cadence forces a
choice, do Phase 1 first: it changes what the compiler accepts and so is the item an evaluator
notices, whereas Phase 2 hardens documentation around behaviour that is already correct.

---

## Phase 1 — Remove the legacy `@import` extension

- **Status:** in progress
- **Goal (one line):** A template that uses `@import()` no longer compiles; instead it produces a
  single positioned removal error that names the two replacements, and no `@import` reference
  survives in docs, samples, or tests except as removal/migration guidance.
- **Depends on:** nothing.
- **Changes an externally-visible contract:** yes — the `@import()` extension is removed. Permitted
  because 2.0 is unreleased (see the version-context note above).

### Goal

`@import()` is a **vestigial** extension, not grammar
([`[ExtensionName("import")]`](../src/Heddle/Extensions/ImportExtension.cs#L8)). Its `InitStart`
re-parses the target file into the extension body's **own already-isolated context** and **merges
nothing** into the importing document
([language-reference.md:989–999](language-reference.md#L989-L999)): a subsequent call to an
"imported" definition is unresolved (pinned by
[ImportFormsPinningTests I04:59–65](../src/Heddle.Tests/ImportFormsPinningTests.cs#L59-L65) — the
compile fails with `lib_badge` unresolved), and importing any position-bearing content (a definition
or output block) from a non-zero offset fails outright with *"Error while compiling import"*
([I05:67–80](../src/Heddle.Tests/ImportFormsPinningTests.cs#L67-L80)). Its **one** observable effect
is that errors in the target file surface on the importing compile (it validates the file). It
currently ships with a deprecation warning steering authors away from it
([ImportExtension.cs:14–22](../src/Heddle/Extensions/ImportExtension.cs#L14-L22), HED4003). This
phase completes that steer: the extension is retired, and the name `import` is kept alive **solely**
as a tombstone that emits a positioned removal error pointing at the replacements.

The user-visible outcome: authors who reach for `@import()` are told, at the call site, exactly what
to use instead — and no one adopting 2.0 learns a construct that is scheduled to disappear.

### Non-goals / scope boundary

- **Not touched:** `@<<{{ path }}` (the grammar-level composition import;
  [HeddleMainListener.cs:194–223](../src/Heddle/Language/HeddleMainListener.cs#L194-L223)) and
  `@partial{{ name }}` ([PartialExtension.cs](../src/Heddle/Extensions/PartialExtension.cs)). These
  are the replacements and stay exactly as they are.
- **Not a hard delete.** The `import` extension name must remain *recognized* so the compiler can
  emit a targeted removal error. It must **never** fall through to a generic "unknown extension"
  path — a tombstone, not a deletion (standing decision).
- **Not a grammar change.** `@import` was never grammar; removing it leaves the `.g4` and generated
  parser untouched.

### Design direction

**Chosen approach: tombstone the extension name, emit a positioned removal error, and sweep every
in-repo use — migrating docs/authoring content to `@<<` or `@partial`, and deleting or repurposing
the `@import`-specific tests and fixtures that pin behaviour with no replacement equivalent.**

The load-bearing argument for removal is that **`@import()` is inert — it provides no working
capability an author relies on.** It merges nothing (I04) and fails on any real position-bearing
content (I05); its only effect is incidental file-validation. So removal is not a trade of capability
for safety — there is no capability to lose, only the footgun (a construct that *looks* like an
include but behaves like neither of the two real ones). The table below is a **misconception map**:
what an author might *mistakenly* reach for `@import()` to do, and the construct that actually does
it:

| What an author might expect `@import()` to do | What `@import()` actually does | The construct that actually does it |
|---|---|---|
| Share definitions / layouts across files | Nothing — imported defs do not resolve (I04) | `@<<{{ path }}` — top-level composition ([HeddleMainListener.cs:194–223](../src/Heddle/Language/HeddleMainListener.cs#L194-L223)); merges definitions and **re-bases** the imported file's output chains to the import position, enforced at document scope (HED4004). |
| Embed a rendered chunk inline at a position | Fails on any position-bearing content from a non-zero offset (I05) | `@partial{{ name }}` ([PartialExtension.cs:18–28](../src/Heddle/Extensions/PartialExtension.cs#L18-L28)) — a compiled sub-template streamed through the caller's renderer with model + chained data — isolated and composable, not a raw text paste. |
| A "textual include" with isolated semantics | Re-parses into a dead, already-isolated context that merges nothing — the "surprising isolation semantics" the current warning names ([ImportExtension.cs:19](../src/Heddle/Extensions/ImportExtension.cs#L19)) | *(nothing — this behaviour is the footgun being removed, not a capability being lost)* |

Alternatives weighed:

- **Keep the deprecation warning (the improvement plan's B3).** Rejected: that plan assumed a
  released 2.0 and a 3.0 removal window. With 2.0 unreleased there is no compatibility to preserve,
  so paying the ongoing cost of a live-but-discouraged construct buys nothing.
- **Hard-delete the extension name.** Rejected: a bare `@import()` would then resolve as an unknown
  extension, producing a generic, unhelpful diagnostic. The tombstone converts that into a
  migration signpost at no real cost.

Removal is also **subtractive on the internals** — it simplifies rather than complicates:

- **Provenance plumbing shrinks with no LSP regression.** `ImportOrigin` is stamped at three sites —
  `@<<` ([HeddleMainListener.cs:230+](../src/Heddle/Language/HeddleMainListener.cs#L230)), `@import`
  ([ImportExtension.cs:29–53](../src/Heddle/Extensions/ImportExtension.cs#L29-L53)), and `@partial`
  ([PartialExtension.cs:50–74](../src/Heddle/Extensions/PartialExtension.cs#L50-L74)). Removal drops
  **only** the `@import` stamp site; the shared mechanism and the other two sites are untouched, so
  the LSP's diagnostic re-anchoring is unaffected.
- **A latent defect disappears with it.** A top-level `@<<` inside an `@import`ed file re-parses with
  a *fresh* listener at a non-zero offset (`Count == 1`), a case the `Offset != 0` guard exists
  partly to catch ([HeddleMainListener.cs:203–214](../src/Heddle/Language/HeddleMainListener.cs#L203-L214)).
  Once `@import` is gone that scenario cannot arise; the guard stays correct for genuine in-file
  nesting.
- **Dead internals to dispose as fallout.** Two internals exist only to serve the `@import()` path and
  go stale with it: the runtime-adapter `Parse(document, ParseContext, CompileContext)` overload
  ([DocumentParser.Runtime.cs:28–44](../src/Heddle/Language/DocumentParser.Runtime.cs#L28-L44)), whose
  sole caller is `ImportExtension.InitStart`; and the now-inaccurate "legacy `@import()` shim" comment
  on the `@<<` guard ([HeddleMainListener.cs:206–208](../src/Heddle/Language/HeddleMainListener.cs#L206-L208)),
  which describes a scenario that no longer exists once `@import` is removed. Both are subtractive
  cleanups, not new behaviour.

### Scope: the migration sweep

Removing the extension is small; sweeping the repo clean of `@import()` is the bulk of the work. A
grep for `@import` returns a large reference count, but the raw number is misleading and must be
triaged before it means anything: it double-counts **this plan's own ~44 self-references**, plus
non-Heddle false positives (a vendored CSS linter's `@import`
[csslint_email.js](../src/Heddle.Language/js/src/mode/css/csslint_email.js), `node_modules`, and
build outputs under `bin/`/`dist/`), plus meta-docs that *discuss* the removal. Excluding those, the
real in-repo `@import()` usages the sweep must dispose of fall into a handful of files — and they do
**not** all "migrate to `@<<`/`@partial`". The dispositions differ:

- **Docs — genuine migration/removal.** The language reference `#imports` section, built-in-extensions,
  patterns, the C# API page, and the `include → @<<` mapping in the "Coming from Liquid" page: rewrite
  to point at the replacements and describe `@import` only as removed.
- **`@import`-specific tests — delete or repurpose as tombstone tests, NOT migrate.** The form-pinning
  cases ([ImportFormsPinningTests I04–I07](../src/Heddle.Tests/ImportFormsPinningTests.cs#L59-L97)),
  the legacy-warning suite ([LegacyImportWarningTests](../src/Heddle.Tests/LegacyImportWarningTests.cs)),
  the compose-import nesting shims ([ComposeImportNestingTests](../src/Heddle.Tests/ComposeImportNestingTests.cs)),
  and the generator's precompiled-parity test pin **`@import`-specific behaviour**; there is no `@<<`/
  `@partial` equivalent to migrate them to. They are removed or repurposed to pin the tombstone.
- **A branch-scan harness — needs a replacement "Other block" construct.**
  [BranchSetCompilerTests C19:265–282](../src/Heddle.Tests/BranchSetCompilerTests.cs#L265-L282) uses
  `@import(){{…}}` only as a non-branch "Other block" to prove the branch scan does not misfire across
  it. Its property is not `@import`-specific, but its *vehicle* is; the sweep must re-express it with
  another Other block or the coverage is lost (see the spec-scope item in the register below).
- **A fixture — delete.** [ergo-import-inline.heddle](../src/Heddle.Tests/TestTemplate/ergo-import-inline.heddle)
  exists only to exercise the old include (I05); it cannot be migrated.
- **CHANGELOG** — record the removal.

This is stated as scope, not as per-file edit instructions; the goal is that after the sweep the only
surviving `@import` references are removal/migration guidance and tombstone tests.

### Back-compat / impact

The `@import` extension is removed. Its existing diagnostic **HED4003 is escalated from a deprecation
warning to a removal error** — the same id, with severity raised warning→error and the message
repurposed to removal guidance (resolved Q1). This is a contract change, permitted because **2.0 is
unreleased** — there is no prior released version whose templates could break.

**Precompiled-path parity must be preserved, and the placement of the detection decides whether it
can be.** Today the generator re-emits HED4003 only by scanning for a *leftmost, top-level* `@import`
output chain ([HeddleTemplateGenerator.cs:330–342](../src/Heddle.Generator/HeddleTemplateGenerator.cs#L330-L342)),
which structurally cannot see nested or consuming call shapes (e.g. an `@import()` inside an `@if`
body, as pinned in the compose-import nesting suite); and if a diagnostic lives only in
`ImportExtension.InitStart`, the generator's parse-only path never runs it at all. Either placement
makes cross-tier parity unreachable for whole classes of input. **Design direction: the removal must
be detected at a layer both tiers already share — the parse/listener layer — so parity is automatic
for every call shape.** The precedent is HED4004: `@<<` nesting is caught in the listener
([HeddleMainListener.ExitImport_block:214–222](../src/Heddle/Language/HeddleMainListener.cs#L214-L222)),
and the generator already forwards `parseContext.Errors` **with their `DiagnosticId`** as positioned
build errors ([HeddleTemplateGenerator.cs:304–313](../src/Heddle.Generator/HeddleTemplateGenerator.cs#L304-L313)),
so a parse-layer detection surfaces identically on both tiers with no generator-side special-casing.
The exact detection mechanism is a spec (HOW) concern; what the plan fixes is that it must live at a
shared layer, not at the current HED4003 re-emit site.

### Risks & mitigations

| Risk | Mitigation | Size |
|---|---|---|
| The tombstone accidentally falls through to "unknown extension" for some call shape | Success criteria require a positioned removal error, tested on both tiers, and a negative test asserting it is *not* the unknown-extension diagnostic | S |
| Precompiled and runtime tiers diverge on the removal diagnostic | Detect the removal at the parse/listener layer both tiers share (HED4004 precedent), so parity is automatic for every call shape — *not* at the current leftmost/top-level generator re-emit site, which misses nested and precompiled shapes; pin with a differential fixture covering a nested `@import()` | M |
| A test that uses `@import()` only as a vehicle (not for `@import`-specific behaviour) loses its coverage when swept — specifically the C19 branch-scan "Other block" harness | Re-express with another non-branch Other block; tracked as spec-scope item S1 in the register | S |
| A stray `@import` reference survives the sweep in docs/samples/tests | Success criterion: grep returns only migration guidance | S |

### Success criteria

Measurable statements a spec can turn into tests:

- [ ] Compiling a template that uses `@import()` produces a **positioned** removal error (an `HED*`
      id + call-site position) that names `@<<` and/or `@partial` as the replacement.
- [ ] That error is **not** the generic unknown-extension diagnostic (asserted explicitly).
- [ ] The **precompiled (generator) tier and the runtime tier emit the identical removal
      diagnostic** for the same `@import()` input — including a **nested/consuming** call shape
      (e.g. `@import()` inside an `@if` body), not only a top-level one — so the differential
      guarantee holds for every call shape.
- [ ] `@<<` and `@partial` are unaffected: their existing golden output is byte-identical and their
      diagnostics (incl. HED4004 nesting) are unchanged.
- [ ] A grep for `@import` across `docs/`, `samples/`, and the test projects
      (`src/**/*Tests*/`, e.g. `Heddle.Tests`, `Heddle.Generator.Tests`) — excluding `bin/`, `dist/`,
      `node_modules/`, and this plan — returns only removal/migration references and tombstone tests:
      no live usage, no fixture that still exercises the old include.
- [ ] The build stays warning/error-clean on all supported TFMs after the sweep.

### Validation scenarios

| Input | Expected outcome |
|---|---|
| A template with a top-level `@import(){{ file }}` | Positioned removal error naming the replacements; compilation fails; nothing rendered |
| The same template precompiled via the generator | Byte-identical removal diagnostic at build time |
| A **nested/consuming** `@import()` (e.g. inside an `@if` body), both tiers | Positioned removal error at the call on both tiers — the shape the current generator re-emit site would miss |
| A template using `@<<{{ file }}` to share definitions | Compiles and renders exactly as before (golden byte-identical) |
| A template using `@partial{{ name }}` | Compiles and renders exactly as before |
| A nested `@<<` (inside an `@if`/`@for` body) | Still raises HED4004 at the directive position (unchanged) |

### Open questions

See the consolidated [Open questions](#open-questions) register below (Q1–Q2 concern this phase). The
migration-completeness item once tracked as Q3 has been re-shelved as a **spec-scope sweep item** (it
is an empirical sweep task, not a user decision) — see the register's spec-scope note.

### External grounding

| Claim | Source |
|---|---|
| `@import` is an extension, not grammar | [ImportExtension.cs:8](../src/Heddle/Extensions/ImportExtension.cs#L8) |
| `@import()` merges nothing (imported defs unresolved) and fails on position-bearing content from a non-zero offset — it is vestigial | [ImportFormsPinningTests I04:59–65, I05:67–80](../src/Heddle.Tests/ImportFormsPinningTests.cs#L59-L80), [language-reference.md:989–999](language-reference.md#L989-L999) |
| Parse-layer detection gives both-tier parity (generator forwards `parseContext.Errors` with `DiagnosticId`) | [HeddleTemplateGenerator.cs:304–313](../src/Heddle.Generator/HeddleTemplateGenerator.cs#L304-L313), [HeddleMainListener.cs:214–222](../src/Heddle/Language/HeddleMainListener.cs#L214-L222) |
| The current HED4003 generator re-emit matches only leftmost/top-level `@import` chains (misses nested/consuming shapes) | [HeddleTemplateGenerator.cs:330–342](../src/Heddle.Generator/HeddleTemplateGenerator.cs#L330-L342) |
| A dead runtime-adapter overload serves only the `@import()` path | [DocumentParser.Runtime.cs:28–44](../src/Heddle/Language/DocumentParser.Runtime.cs#L28-L44) |
| `@<<` is the grammar-level composition import | [HeddleMainListener.cs:194–223](../src/Heddle/Language/HeddleMainListener.cs#L194-L223) |
| `@partial` is a compiled sub-template streamed through the caller's renderer | [PartialExtension.cs:18–28](../src/Heddle/Extensions/PartialExtension.cs#L18-L28) |
| `@import` currently emits HED4003 (deprecation warning) at InitStart | [ImportExtension.cs:14–22](../src/Heddle/Extensions/ImportExtension.cs#L14-L22) |
| The generator re-emits HED4003 for precompiled parity | [HeddleTemplateGenerator.cs:322–342](../src/Heddle.Generator/HeddleTemplateGenerator.cs#L322-L342) |
| Diagnostic ids: HED4003 = LegacyImportDirective, HED4004 = ComposeImportNotTopLevel | [HeddleDiagnosticIds.cs:117,124](../src/Heddle/Data/HeddleDiagnosticIds.cs#L117) |
| `ImportOrigin` is stamped at three sites; only `@import`'s is removed | [ImportExtension.cs:29–53](../src/Heddle/Extensions/ImportExtension.cs#L29-L53), [PartialExtension.cs:50–74](../src/Heddle/Extensions/PartialExtension.cs#L50-L74) |
| The offset-fragility scenario is `@import`-specific (fresh listener, `Count == 1`, offset > 0) | [HeddleMainListener.cs:203–214](../src/Heddle/Language/HeddleMainListener.cs#L203-L214) |
| Positioned-diagnostic + differential + multi-TFM testing culture | [testing-standards.md](spec/common/testing-standards.md) |

---

## Phase 2 — Document the `ProcessData` string contract

- **Status:** in progress
- **Goal (one line):** The value/string rail's "your textual contribution must be a string" contract
  is written where extension authors read it, so an author returning a boxed scalar knows it will be
  dropped by design.
- **Depends on:** nothing.
- **Changes an externally-visible contract:** no — this documents behaviour that already holds; no
  code changes.

### Goal

On the value/string rail, a non-string `ProcessData` result is coerced to the empty string via
`as string ?? string.Empty`
([RuntimeDocument.cs:240](../src/Heddle/Runtime/RuntimeDocument.cs#L240),
[:281](../src/Heddle/Runtime/RuntimeDocument.cs#L281)); the third site
([:315](../src/Heddle/Runtime/RuntimeDocument.cs#L315)) reads `as string ?? element.Piece ??
string.Empty`, where the `Piece`-first fallback is inert when a `Processor` is present (piece and
processor are exclusive), so it is equivalent for this contract. This is a **deliberate guard**: an
extension's textual contribution *is* a string; a non-string means "render-only / no textual value
on this rail" and must not leak an internal `ToString()` into concatenated output. The behaviour is
correct and stays. What is missing is that the contract is **unwritten** — this phase writes it down
on the surface authors implement and in the author-facing doc.

The user-visible outcome: an extension author reading the `ProcessData` API surface or the
custom-extensions guide learns that they must return a `string` for a textual value, and understands
that returning a non-string yields empty output on the value rail.

**Scope boundary (Non-goals).** This phase is docs-only. It does **not** change the guard, add a
DEBUG assert, or fix the pre-existing value-rail drop of non-string scalars (including `@out`'s on
the process/concat path) — those stay exactly as they are; the phase only writes the contract down
truthfully.

### Design direction

**Chosen approach: document the contract on the extension base's `ProcessData` surface
([AbstractExtension.cs:149](../src/Heddle/Core/AbstractExtension.cs#L149)) and in the author-facing
custom-extensions guide.** Docs only.

Why this and nothing more:

- **The built-in scalar *formatters* pre-stringify, so *they* are never dropped** — each returns a
  `string` at its own boundary before the rail sees it: `@int` returns `.ToString(invariant)`
  ([IntegerExtension.cs:36](../src/Heddle/Extensions/IntegerExtension.cs#L36)), `@string` coerces to
  string, `@guid` returns `.ToString(format)`. This is why documenting the contract does not require a
  code change: authors who follow it (return a string) are already safe.
- **The guard is not universally harmless, and the doc must say so.** `@out` on the value/string rail
  returns its chained value *raw* — `ProcessData` returns `scope.ChainedData` unstringified
  ([OutExtension.cs:100–101](../src/Heddle/Extensions/OutExtension.cs#L100-L101)) — so a non-string
  chained value **is dropped today** on the process/concat path (`ProcessData(...) as string ?? ""`).
  Only the *render* path stringifies it, and only with a plain `chained?.ToString()`, **not**
  `Convert.ToString(invariant)` ([OutExtension.cs:135](../src/Heddle/Extensions/OutExtension.cs#L135));
  the code's own comment names the value-rail drop "a separate, pre-existing issue not addressed here"
  ([OutExtension.cs:128–133](../src/Heddle/Extensions/OutExtension.cs#L128-L133)). So the guard is a
  guard that *also* silently drops otherwise-meaningful scalars on the value rail — the accurate thing
  to document is exactly that, so a **third-party** author knows to stringify at their own boundary
  rather than trusting the rail to do it. A doc closes that gap where a code change cannot (a third
  party's extension is not in this repo).
- **No DEBUG assert.** A runtime assertion on non-string results risks false positives on the
  legitimate "render-only, no textual value" case the guard exists to serve — so the guard is left
  exactly as-is and only documented. *(This resolves what could look like an open question — see the
  register below — as a settled decision, not an open one.)*

### Sections deliberately omitted (and why)

This phase is legitimately lean; several standard plan sections do not apply:

- **Back-compat / impact — omitted.** No code changes, so nothing to break and no version window to
  consider.
- **Risks & mitigations — omitted.** A documentation edit to a correct, shipped behaviour carries no
  material risk beyond ordinary review.
- **Validation scenarios — omitted.** There is no behaviour change to validate; the criteria below
  are documentation-presence checks, not test scenarios.

### Success criteria

- [ ] The `ProcessData` string contract is stated on the extension base surface
      ([AbstractExtension.cs:149](../src/Heddle/Core/AbstractExtension.cs#L149)) — an author reading
      the API doc there knows a textual result must be a `string`.
- [ ] The same contract appears in the author-facing custom-extensions guide, stated truthfully:
      *return a string; a non-string yields empty output on the value rail — a guard that also drops
      otherwise-meaningful scalars there, so stringify at your own boundary.*
- [ ] The documentation states plainly *why* the guard exists (no internal `ToString()` leaking into
      concatenated output) so the behaviour reads as intentional — while being honest that the same
      guard drops a non-string an author might have meant to emit, which is why the contract is
      "return a string," not "we will stringify for you."

### Open questions

None specific to this phase. The docs-only-vs-DEBUG-assert choice is **resolved** (docs only; no
assert) and recorded in the register below as resolved rather than open.

### External grounding

| Claim | Source |
|---|---|
| The value/string rail coerces non-string `ProcessData` to `""` | [RuntimeDocument.cs:240](../src/Heddle/Runtime/RuntimeDocument.cs#L240), [:281](../src/Heddle/Runtime/RuntimeDocument.cs#L281), [:315](../src/Heddle/Runtime/RuntimeDocument.cs#L315) |
| Built-in scalar *formatters* pre-stringify, so those are never dropped | [IntegerExtension.cs:36](../src/Heddle/Extensions/IntegerExtension.cs#L36) |
| `@out` returns its chained value raw on the value rail (so a non-string is dropped there); only the render path stringifies, via plain `ToString()` not `Convert.ToString` | [OutExtension.cs:100–101](../src/Heddle/Extensions/OutExtension.cs#L100-L101), [OutExtension.cs:135](../src/Heddle/Extensions/OutExtension.cs#L135) |
| The value-rail non-string drop is a known "separate, pre-existing issue not addressed here" | [OutExtension.cs:128–133](../src/Heddle/Extensions/OutExtension.cs#L128-L133) |
| `ProcessData` is the base surface authors implement | [AbstractExtension.cs:149](../src/Heddle/Core/AbstractExtension.cs#L149) |
| Author-facing home for the contract | [custom-extensions.md](custom-extensions.md) |

---

## Open questions

The register lives here (single-file plan). Each carries a recommended default and what it blocks.
Plan DoR = this section resolved. **Open section: empty — every question resolved below.**

- **Q1 — Tombstone diagnostic id: reuse HED4003 or mint a new one?** (Phase 1) — **RESOLVED**
  **Resolution: reuse HED4003, escalated warning→error** (user, 2026-07-12). The existing
  `LegacyImportDirective` id is kept; its severity is raised from warning to error and its message
  repurposed to removal guidance (use `@<<` / `@partial`). The spec must ensure the escalation is
  detected at the shared parse layer (see Back-compat / impact) so both tiers carry the same
  positioned HED4003 error for every call shape. *(The considered alternative — mint a new
  "ImportDirectiveRemoved" id and retire HED4003 — was declined; a single id for "don't use
  `@import`" is simpler and, for an unreleased version, warning-suppression continuity is a non-issue.)*

- **Q2 — Tombstone lifespan.** (Phase 1) — **RESOLVED**
  **Resolution: keep the tombstone (the positioned HED4003 removal error) indefinitely within 2.x**
  (user, 2026-07-12). No decay to a generic unknown-extension error is scheduled; the spec need not
  plan any future removal of the tombstone.

**Spec-scope items (empirical, not user decisions):**

- **S1 — the C19 branch-scan harness needs a replacement "Other block" construct.** (Phase 1)
  [BranchSetCompilerTests C19:265–282](../src/Heddle.Tests/BranchSetCompilerTests.cs#L265-L282) uses
  `@import()` only as a non-branch "Other block" to prove the branch scan does not misfire across it;
  the property is not `@import`-specific but its vehicle is. The spec must re-express it with another
  Other block (any non-branch extension call) or the coverage regresses. This is a sweep/spike task
  the spec owns, not a user question. *(This supersedes the old "Q3 migration-completeness" question:
  F1 established that `@import()` has no textual-inclusion capability for any in-repo use to rely on —
  it merges nothing (I04) and fails on real content (I05) — so the "is some fragment un-migratable
  because it needs textual inclusion?" framing is moot. The one genuine residual is this harness's
  vehicle, above.)*

**Resolved (recorded, not open):**

- **`ProcessData` non-string handling — docs-only, no DEBUG assert.** (Phase 2) Settled: the guard is
  correct and stays; a DEBUG assert is rejected because it would false-positive on the legitimate
  "render-only / no textual value" case. Documentation is the only change.

---

## External grounding (consolidated)

The load-bearing claims across both phases, each traceable to source on this branch:

| Claim | Source |
|---|---|
| 2.0 is unreleased → `@import` removal is evolution, not a breaking change (supersedes the Non-goal) | [improvement-plan.md:279](improvement-plan.md#L279) |
| `@import` is an extension (`[ExtensionName("import")]`), not grammar | [ImportExtension.cs:8](../src/Heddle/Extensions/ImportExtension.cs#L8) |
| `@<<` composition import merges defs and re-bases chains; nesting raises HED4004 | [HeddleMainListener.cs:194–223](../src/Heddle/Language/HeddleMainListener.cs#L194-L223) |
| `@partial` is a compiled, streamed sub-template | [PartialExtension.cs:18–28](../src/Heddle/Extensions/PartialExtension.cs#L18-L28) |
| Current `@import` deprecation warning HED4003 + normative message | [ImportExtension.cs:14–22](../src/Heddle/Extensions/ImportExtension.cs#L14-L22) |
| Generator re-emits HED4003 for precompiled parity | [HeddleTemplateGenerator.cs:322–342](../src/Heddle.Generator/HeddleTemplateGenerator.cs#L322-L342) |
| Diagnostic-id catalogue (HED4003 / HED4004) | [HeddleDiagnosticIds.cs:117,124](../src/Heddle/Data/HeddleDiagnosticIds.cs#L117) |
| `ImportOrigin` shared across three stamp sites; `@import` is one of them | [ImportExtension.cs:29–53](../src/Heddle/Extensions/ImportExtension.cs#L29-L53) |
| The value/string rail coerces non-string `ProcessData` to `""` (three sites) | [RuntimeDocument.cs:240](../src/Heddle/Runtime/RuntimeDocument.cs#L240), [:281](../src/Heddle/Runtime/RuntimeDocument.cs#L281), [:315](../src/Heddle/Runtime/RuntimeDocument.cs#L315) |
| Built-in scalar formatters pre-stringify; but `@out` drops a non-string on the value rail — a known "separate, pre-existing issue" | [IntegerExtension.cs:36](../src/Heddle/Extensions/IntegerExtension.cs#L36), [OutExtension.cs:100–101](../src/Heddle/Extensions/OutExtension.cs#L100-L101), [OutExtension.cs:128–135](../src/Heddle/Extensions/OutExtension.cs#L128-L135) |
| `ProcessData` base surface authors implement | [AbstractExtension.cs:149](../src/Heddle/Core/AbstractExtension.cs#L149) |
| Testing culture: positioned diagnostics, byte-identical goldens, precompiled-vs-runtime differential, multi-TFM | [testing-standards.md](spec/common/testing-standards.md) |
