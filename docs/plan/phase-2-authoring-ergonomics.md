# Phase 2 — authoring-ergonomics

## Header

- **Status:** ready — Plan DoR
- **Goal (one line):** Add two additive author-facing quick wins — an `@@` literal-`@` escape and a compile-time warning for a `{{ x }}`-in-text misread — that lower the first-week onboarding tax without changing any existing syntax meaning.
- **Depends on:** nothing (both items are self-contained and additive; neither requires another phase's deliverable).
- **Changes an externally-visible contract:** yes — (1) the grammar gains a literal-`@` escape: `@@` moves from *always a compile error* to *emits `@`* (additive; no valid template exercises `@@` today); and (2) a new ergonomics **warning** diagnostic id is introduced for the misread lint. Both are additive; no existing valid template changes its rendered bytes.

## Goal

The assessment's §3.9 [syntax-ergonomics tax](../language-assessment.md#39-syntax-ergonomics--the-tax-itemized) lists four compounding onboarding frictions. Two of them are cheap, additive wins that this phase delivers:

1. **A literal-`@` escape via `@@`.** Today a bare literal `@` requires a [raw region](../language-reference.md#raw-blocks----and-) (`@{ … }@` or `@:`), and the doubling affordance that Razor authors reach for by reflex — `@@` — is a compile error. This phase makes `@@` emit a single literal `@`, complementing (not replacing) the existing raw-region affordances: raw regions stay the tool for bulk literal text; `@@` is the lightweight single-character escape.

2. **A "did you mean `@(…)`?" warning for a `{{ x }}`-in-text misread.** A bare `{{ Title }}` sitting in body text prints literal braces rather than interpolating — the [coming-from-liquid guide](../coming-from-liquid.md) calls it "the number-one misread" for authors arriving from Liquid, Jinja, Mustache, or Handlebars. This phase adds a compile-time **warning** that spots the pattern in literal text and points the author at `@(…)`. It never blocks compilation and never fires inside a legitimate `{{ … }}` body.

The user-visible outcome: authors who type `@@` or paste a Liquid-style `{{ Title }}` get the behavior or the guidance they expected, instead of a hard error (for `@@`) or a silent wrong render (for the brace misread).

## Non-goals / scope boundary

- **No change to `{{ }}` semantics.** `{{ … }}` remains a subtemplate/body delimiter. The misread item is a *diagnostic only* — it does not make `{{ x }}` interpolate. (Changing `{{ }}` meaning is permanently out of scope per standing ruling R4.)
- **No new whitespace-control syntax.** Whitespace trimming is already covered by [`@\`](../language-reference.md#whitespace-trimming-) and `TemplateOptions.TrimDirectiveLines`; this phase adds none.
- **No string interpolation in native expressions.** Deliberately excluded per the design's "removing a decision beats adding a feature" stance (`+`/`format` cover it) — see [native expressions](../native-expressions.md).
- **No touching of colon overloading, right-to-left chains, or the descend/step-back rule** (also R4 / later-or-never). This phase is exactly the two items above and nothing else.
- **Seam to later work:** contextual/attribute encoding lints (assessment §3.8) and any deeper diagnostic surface are a separate concern; this phase establishes the pattern of a text-position ergonomics lint but does not generalize it.

## Design direction

**`@@` → `@`: adopt the industry-standard sigil-doubling escape.** Character-doubling is the escape convention authors already carry — Razor's `@@`, and doubling generally. Heddle's own docs flag the *absence* of it as a surprise ([reference: Text and the `@` escape](../language-reference.md#text-and-the--escape), [assessment §3.9 item 2](../language-assessment.md#39-syntax-ergonomics--the-tax-itemized)). The change is safe precisely because `@@` is *always* an error today: at the top level the first `@` enters output mode (`START_OUT`) and the second `@` re-emits an `OUT` token without popping (`OUT_OUT_START`), so the parser cannot reduce it — there is no valid template anywhere that contains `@@`, so redefining it cannot break one. This is the minimal, expectation-matching option; the alternative (do nothing, keep steering authors to raw regions) leaves a documented papercut in place for no benefit.

**Misread lint: a compile-time WARNING, never a semantic change.** A bare `{{ Title }}` in text is a *valid* template today that renders the literal braces (see the coming-from-liquid example). Two options are therefore ruled out immediately: making it interpolate (violates R4 and R5 — it would change `{{ }}` meaning and the rendered bytes) and making it a hard error (violates R5 — it would break a template that legitimately prints braces). What remains is a diagnostic that leaves both compilation and output untouched and simply *advises*. This is feasible and low-risk because the compiler already has the literal text around every output block available at compile time as `workingDocument` substrings addressable by block offsets, and it already scans such gap text and warns when it holds unexpected content (the `HED3001` non-whitespace branch-gap warning is the direct precedent). The lint reuses that shape: scan text-position runs for a `{{ identifier }}` / `{{ dotted.path }}` pattern and warn "did you mean `@(identifier)`?". It joins the existing ergonomics diagnostic family (`HED4001`/`HED4002`), keeping related author-guidance diagnostics numbered together.

## Dependencies & ordering

- **Prerequisites:** none. Both items stand alone and touch independent surfaces (the `@@` escape is a lexer/grammar affordance; the misread lint is a compile-time diagnostic pass). They can land in either order and in either sequence relative to other phases.
- **What it unblocks:** nothing hard-depends on this phase. It is a good early confidence-builder — small, self-contained, and low-risk — and it establishes the precedent of a text-position ergonomics lint that later diagnostic work (e.g. attribute-encoding lints) can follow.

## Back-compat / impact

- **`@@` escape.** Additive. `@@` currently produces a positioned compile error (the syntax-listener diagnostic in the `HED0001`–`HED0003` core range), so no valid, compiling template contains it. Redefining `@@` to emit `@` cannot change the output of any template that compiles today. The existing byte-identical golden/differential corpus must stay byte-identical (measurable — see success criteria).
- **Misread lint.** Additive and non-breaking by construction: it is a warning, so it never fails a build, and it emits no output, so it never changes rendered bytes. Templates that intentionally print `{{ … }}` literals keep compiling and rendering exactly as before — they simply also carry a warning, which the author can ignore or silence by rephrasing.
- **Diagnostic surface.** One new stable `HEDxxxx` id enters the [claimed-id registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry); the LSP surfaces it as a `Diagnostic.code` of severity *warning*. No id is renumbered or reused.
- **Docs & editor grammar.** The `@@` change makes the [syntax-highlighting.md](../syntax-highlighting.md) note that "`@@` is not an escape … `@@` is a compile error" (lines 65–66) stale, and the TextMate grammar already *colors* `@@` as a literal-`@` escape (today only cosmetic, since it does not compile). Both are reconciled as in-phase doc/editor-grammar updates — mirroring Phase 1's precedent of updating the docs a fix invalidates. (These edits are downstream of the code change, not part of this WHAT.)
- **Satisfies R5** on both counts: nothing that compiles today stops compiling, and no existing rendered bytes change.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| The `@@` escape perturbs tokenization of an adjacent construct (e.g. `@@(x)`, `@@%`, odd runs like `@@@`), changing an existing render | Bound the new behavior to the doubled-`@` pair only; verify against the golden/differential byte-parity corpus that no template that compiles today changes bytes — and note `@@` was always an error, so no compiling template exercises it | S |
| Misread lint fires on literal-brace text the author *intended* (false positive) | Keep the heuristic narrow — only a bare single identifier or a dotted path, with no `@`, `:`, operator, or nested `{{` inside; warning-only, so a false positive is at worst noise the author ignores | M |
| Misread lint fires inside a real `{{ … }}` body or a raw region | Scope the scan to text-position runs only, excluding compiled subtemplate bodies and raw blocks (`@{ … }@`, `@:`) — the same literal-gap surface the compiler already isolates for `HED3001` | S |
| New diagnostic id collides with a future allocation | Claim the id in the cross-cutting [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) in the same change, per the D1 stable-id discipline | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] A template containing `a@@b` compiles and renders exactly `a@b`.
- [ ] A template containing `@@@@` compiles and renders exactly `@@` (two pairs → two literal `@`).
- [ ] A template that combines `@@` with an adjacent directive, e.g. `@@@(Title)`, renders a literal `@` followed by the output of `@(Title)` (greedy left-to-right pairing) — see open question Q4.
- [ ] `@@` emits a literal `@` in a subtemplate body as well as in top-level text (per the lean on Q3), verified by a template that uses it in each position.
- [ ] A template with `<p>{{ Title }}</p>` in body text **still compiles**, **still renders** `<p>{{ Title }}</p>` byte-for-byte, and emits exactly one misread warning positioned at the `{{`, whose message suggests `@(Title)`.
- [ ] The misread warning carries a stable `HEDxxxx` id (see Q1) and severity *warning*; it is surfaced by the LSP as a `Diagnostic.code` and never causes compilation to fail.
- [ ] A legitimate `{{ … }}` body — e.g. `@list(Articles){{ @(Title) }}` — emits **no** misread warning.
- [ ] Literal `{{ … }}` inside a raw region — e.g. `@{ {{ Title }} }@` — emits **no** misread warning.
- [ ] A text-position `{{ … }}` whose contents are *not* a bare identifier or dotted path — e.g. `{{ a + b }}` or `{{ a:b }}` — emits **no** warning (narrow heuristic, per Q2).
- [ ] The full existing golden/differential corpus renders byte-identically before and after this phase (no regression from either item).

## Validation scenarios

| Input | Expected outcome |
|---|---|
| `a@@b` | Compiles; renders `a@b`. |
| `@@@@` | Compiles; renders `@@`. |
| `Reach us at @@example.com` | Compiles; renders `Reach us at @example.com` (no raw region needed). |
| `@@@(Title)` | Compiles; renders literal `@` then the value of `@(Title)`. |
| `@list(Articles){{ @@ }}` | Compiles; renders a literal `@` per article (escape works in a subtemplate body). |
| `<p>{{ Title }}</p>` (in body text) | Compiles; renders `<p>{{ Title }}</p>`; emits one misread warning at the `{{`, suggesting `@(Title)`. |
| `<p>{{ Author.Name }}</p>` (in body text) | Compiles; renders literally; emits the misread warning (dotted path is in scope). |
| `@list(Articles){{ @(Title) }}` | Compiles; no misread warning (real subtemplate body). |
| `{{ a + b }}` / `{{ x:y }}` in text | Compiles; renders literally; **no** warning (not a bare identifier/dotted path). |
| `@{ {{ Title }} }@` | Compiles; renders `{{ Title }}` verbatim; **no** warning (raw region). |
| Existing golden/differential corpus | Byte-identical output before and after this phase. |

## Open questions

None — resolved (see [open-questions.md](open-questions.md)). Every P2 question (Q1–Q4) is folded into the design direction, risks, and success criteria above.

## External grounding

| Claim | Source |
|---|---|
| `@@` is currently a compile error; a literal `@` needs a raw region — the doubling affordance is absent | [language-reference.md — Text and the `@` escape](../language-reference.md#text-and-the--escape) ("there is no character-doubling escape (`@@` is a compile error)"); [Behavioral nuances summary](../language-reference.md#behavioral-nuances-summary) |
| `@@` produces two adjacent `OUT` tokens that never reduce — the first `@` pushes output mode, the second re-emits `OUT` without popping | [HeddleLexer.g4](../../src/Heddle.Language/HeddleLexer.g4) tokens `START_OUT` / `SUB_START_OUT` / `OUT_OUT_START` |
| Existing literal-`@` affordances the escape complements: raw block `@{ … }@` and raw line `@:` | [language-reference.md — Raw blocks](../language-reference.md#raw-blocks----and-) |
| The `{{ x }}`-in-text misread is "the number-one misread"; a bare `{{ Title }}` in text is a valid template that prints literal braces; the remedy is `@(…)` | [coming-from-liquid.md — `{{ }}` is a body, not interpolation](../coming-from-liquid.md); [assessment §3.9 item 1](../language-assessment.md#39-syntax-ergonomics--the-tax-itemized) |
| Both items are named as ergonomics quick wins (the `{{ }}` false friend and the absent `@@` escape) | [language-assessment.md §3.9](../language-assessment.md#39-syntax-ergonomics--the-tax-itemized) |
| The literal text around output blocks is available at compile time as document substrings addressable by block offsets, and the compiler already scans that gap text and warns (`HED3001`) | [HeddleCompiler.cs](../../src/Heddle/Runtime/HeddleCompiler.cs) (`CollectGap` / `ApplyGaps`, `workingDocument.Substring`) |
| Diagnostic ids are stable `HEDxxxx` codes allocated in feature-area blocks; `HED4xxx` = ergonomics; `HED4001`–`HED4004` are already claimed, so `HED4005` is the next free ergonomics id and `HED0005` the next free core id | [cross-cutting-decisions.md — D1 stable diagnostic ids + claimed-id registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) |
| Warning-severity ergonomics diagnostics that never block compilation are precedented (`HED4002` double-render) | [language-reference.md — Default output `-> chain`](../language-reference.md#default-output---chain) ("It is a warning, not an error"); [built-in-extensions.md](../built-in-extensions.md) |
