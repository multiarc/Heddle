# Open questions

> **Plan DoR: the Open section is empty.** Every question has been driven to a resolution and folded
> into the affected phase document(s). Questions are prefixed by phase (`P1`–`P8`). The consequential
> product / language-surface decisions were settled with the user during the Q&A debate; the narrower
> technical sub-choices are resolved by the plan author from the grounding, each carried with its lean
> and reversible.

## Open

*None. All questions resolved — see below. Plan DoR (questions) met.*

## Resolved

> Legend: **(user)** = decided by the user during the debate; **(author)** = resolved by the plan
> author (orchestrator) from grounding + standing rulings, reversible. Three items marked *(cost lean)*
> chose thoroughness over the cheaper option — flagged for the user to downgrade if desired.

### The named-content-region model (Phase 7) — the central design debate  [status: RESOLVED (user)]
- **Resolution.** Ratified in full as **[R7](common/standing-rulings.md#standing-rulings-user-set)**:
  public overridable regions declared as inner definitions with a leading colon (`<:name>` public,
  `<name>` private, `<:item :: Type>` typed); filled with the **normal `<name:name>` override** in a
  `@%…%@` block inside the call body — call-scoped, gated by public/private; override body runs in the
  region's own context; `@out()` and props unchanged; sibling-override idiom retained. This supersedes
  the earlier "named slots (`@slot`/`@out(name)`)" framing entirely. **Decided:** user, 2026-07-12.
- Subsumed by this ratification (previously tracked separately): the "unnamed slot = default" question
  (yes — `@out()` unchanged), per-region inheritance/narrowing (mirrors the prop rules), the
  diagnostic block (`HED5019`+), and the four-layer rollout (all at once).

### P1 — file-based API correctness (Phase 1)
- **P1-Q1** (author) — Watch only the root file, not the `@<<` import closure (imports = future seam).
- **P1-Q2** (author) — A failed recompile keeps the last-good document renderable and surfaces the error via `CompileResult`.
- **P1-Q3** (author) — Recompile on any watcher event resolving to the target existing (Changed / Created / Renamed-onto-target), so atomic-save editors work (R6).
- **P1-Q4** (author) — A live-document swap is coherent old-or-new for in-flight renders; the superseded document is released by reusing the deferred-dispose discipline **Phase 4 hardens** (today recompile overwrites `_runtimeDocument` and orphans the old one, so this per-superseded release is *new* work, not an existing path). No full multi-field atomicity unless testing shows tearing.
- **P1-Q5** (author) — No debounce this phase (recompiles are idempotent after the stale-recompile fix).

### P2 — authoring ergonomics (Phase 2)
- **P2-Q1** (author) — Misread lint claims **`HED4005`** (ergonomics family); see [diagnostic-ids](common/diagnostic-ids.md).
- **P2-Q2** (author) — Narrowest heuristic first: fire only on `{{ identifier }}` / `{{ dotted.path }}` with no `@`, `:`, operator, or nested `{{`.
- **P2-Q3** (author) — `@@` → `@` recognized everywhere a literal `@` is meaningful (top-level text + subtemplate bodies).
- **P2-Q4** (author) — Odd-length `@` runs pair greedily left-to-right (`@@` → one `@`; a trailing lone `@` starts a directive).

### P3 — HTML-context encoding lint (Phase 3)
- **P3-Q1** (author) *(cost lean)* — Cover the attribute position first; `<script>` and URL as a fast-follow within the phase on the same gate. *(User may narrow to attribute-only.)*
- **P3-Q2** (author) — Conservative / high-precision default; silence a site with the explicit encoder or `@raw`.
- **P3-Q3** (author) — The `Html` profile (`OutputProfile.Html` / `@profile(html)`) is the sole gate (implements R3); a richer explicit-context signal is a future seam.
- **P3-Q4** (author) — Diagnostic id **`HED2004`**.
- **P3-Q5** (author) — Message names the detected context + suggests the specific encoder; severity fixed at warning, no escalation knob this phase.

### P4 — engine robustness (Phase 4)
- **P4-Q1** (author) *(cost lean)* — `TryCompilation` gains **full parity** (runs finalization + Roslyn so a green dry run predicts `Compile`); document the added `FullCSharp` cost + idempotent assembly-cache side effect. *(User may prefer the cheaper explicit-partial contract.)*
- **P4-Q2** (author) — Release model-type check is a `TemplateOptions` bool defaulting **off**, excluded from `Equals`/`GetHashCode`, reproducing the DEBUG guard (precompiled path skipped). *(Note: under the revised R5 window the user could later choose default-on; not taken now — it adds hot-path cost.)*
- **P4-Q3** (author) — Disposal fix is safety-only, non-blocking (no torn counter, no dispose-under-active-render; no drain/stall).

### P5 — docs & benchmark credibility (Phase 5)
- **P5-Q1** (user) — **2.0 is releasing imminently:** reconcile docs *toward* `2.0.0`; the tag-cut + `CHANGELOG` `[Unreleased]`→`[2.0.0]` is the adjacent release action the wording anticipates. (Flipped the earlier "unreleased" lean.) **Decided:** user, 2026-07-12.
- **P5-Q2** (author) *(cost lean)* — Parity-check both new benchmark workloads against the four competitor engines. *(User may prefer Heddle-only to keep it cheap.)*
- **P5-Q3** (author) — Retire the four dangling `docs/spec/README.md` links ("specs retired — git history"), matching the existing v2.0 nine-phase row.
- **P5-Q4** (author) — Leave the append-only `records.md` historical rows untouched; scope reconciliation to the living index + user-facing docs.
- **P5-Q5** (author) — Leave the assessment's §2 note unedited (dated research artifact).

### P6 — `Range` type (Phase 6)
- **P6-Q1** (author) — Introduce a **dedicated Heddle `Range` value type** (start, last-exclusive, step), not `System.Range` — because a step is needed and `System.Range` has only Start/End `Index` with from-end semantics. Aligns with the user's "Range is range / delete `ForModel`". **(User** set the direction: delete `ForModel`, breaking accepted.)
- **P6-Q2** (author) — Name it `Heddle.Models.Range`; ship a minimal one-directional `System.Range` → `Range` interop (step 1) **in-phase** — it is tiny and public-API, so it belongs with the type it converts to (no deferral).
- **P6-Q3** (author) — Give the new type a readable `ToString()` (the sole intentional rendered-byte change for a standalone `@(range(...))`); still steer authors to `@for`.

### P7 — named content regions (Phase 7)
- **None** — the model is ratified in full (see the design-debate entry above and [R7](common/standing-rulings.md#standing-rulings-user-set)). Residual items are spec-level (HOW): the exact grammar realization of `<:name>` and the precise condition-to-id split within the reserved `HED5019`+ block.

### P8 — extension parameters (Phase 8)
> **Rescoped by the user, 2026-07-13:** Phase 8 was "content regions/slots for extensions" — rejected
> as confusing (an extension has no template body to host regions). It is now **named, typed input
> parameters for extensions** (optional when they carry a default, required when they do not — mirroring
> definition-prop semantics exactly), passed by name at the call site — call-site symmetry with
> definition props (context + params), *not* regions. Phase 8 is now independent of Phase 7.
- **P8-Q1** (author) — Attribute API shape for parameters: per-parameter `[Prop(name, type, default)]` on the extension class, mirroring the one-attribute-per-concern extension-attribute style.
- **P8-Q2** (author) — How the extension's C# reads its declared parameter values at render (a `Scope` / params accessor). *(Spec-level HOW; lean recorded so scope is clear.)*
- **P8-Q3** (author) — Required vs optional parameters mirror definition prop semantics: no default = required, a default = optional.
- **P8-Q4** (author) — Diagnostics reuse the existing prop family `HED5001`–`HED5004` (now for extensions) and **additively relax `HED5005` only** for a parameter-declaring extension; `HED5006` is untouched (it never fires on the extension surface — the extension short-circuits to `HED5005` first). A malformed-`[Prop]` declaration is diagnosed on **both** tiers: it reuses the declaration-side prop ids `HED5007`/`HED5009`/`HED5010`/`HED5015` on the dynamic tier, with the next free generator id `HED7017` as the build-time twin. No `HED5019`+ region ids (those are Phase 7 only).
