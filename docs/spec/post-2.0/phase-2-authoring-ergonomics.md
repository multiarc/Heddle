# Phase 2 — authoring-ergonomics (spec)

## Header

- **Status:** Specified — ready for implementation. The runtime/`CompileResult` design (the byte-identity and corpus-scan authority) is complete and self-contained; two consciously-accepted, most-reversible limits are documented inline — the editor-only `HED4005` coordinate skew under comments/`@\` (D5 *Editor-path limit*, shared with `HED3001`/`HED2003`) and the bounded warning-position imprecision under preceding hidden tokens (WI2). **Orchestrator FYI (shared source, not edited here):** `DocumentAnalyzer.cs:45` passes the original document into `HeddleCompiler.Compile` where the runtime passes the clean `optimizedDocument`; a future reconciliation would sharpen the editor fidelity of `HED4005` **and** the pre-existing `HED3001`/`HED2003`.
- **Source plan:** [phase-2-authoring-ergonomics.md](../../plan/phase-2-authoring-ergonomics.md)
- **Assumes merged:** nothing. Both items are self-contained and additive; neither consumes another phase's deliverable (plan *Dependencies & ordering*). This spec binds only to surfaces already in `main` at authoring time (verified below).

| Document | Purpose |
|---|---|
| this document | Specifies both Phase 2 items end-to-end: the `@@` → `@` literal-`@` escape and the `HED4005` `{{ … }}`-in-text misread warning. |

Governing common specs (normative, not restated): [spec-conventions](../common/spec-conventions.md), [coding-standards](../common/coding-standards.md), [testing-standards](../common/testing-standards.md), [cross-cutting-decisions](../common/cross-cutting-decisions.md) (D1 diagnostic IDs + registry; the grammar-stability gate; the generated-code rule), [breaking-windows](../common/breaking-windows.md). Standing rulings [R4](../../plan/common/standing-rulings.md) (no `{{ }}` meaning change) and [R5](../../plan/common/standing-rulings.md) (additive; nothing that compiles today changes bytes) bound the scope.

## Scope and goal

Two additive author-facing quick wins, exactly as scoped by the plan and nothing more:

1. **`@@` → `@` literal-`@` escape.** Today a bare literal `@` requires a raw region (`@{ … }@` or `@:`); the reflexive doubling affordance `@@` is a **compile error**. This spec makes `@@` emit a single literal `@`, in top-level text **and** in subtemplate bodies, complementing (not replacing) raw regions.
2. **`HED4005` — a `{{ … }}`-in-text misread warning.** A bare `{{ Title }}` in body text prints literal braces rather than interpolating. This spec adds a compile-time **warning** that spots the narrowest form of that pattern in literal text and suggests `@(…)`. It never blocks compilation, never changes rendered bytes, and never fires inside a real `{{ … }}` body or a raw region.

**Out of scope (plan *Non-goals*, R4):** any change to `{{ }}` semantics (the lint is diagnostic-only); new whitespace-control syntax; string interpolation in native expressions; colon overloading, right-to-left chains, the descend/step-back rule; generalizing the text-position lint beyond the one pattern below.

## Assumed state

Re-verified against current `main` source (not trusted from the plan). Line numbers are load-bearing pins the implementer must re-confirm; each carries its anchor symbol.

| Seam | Verified state |
|---|---|
| `@@` tokenization | `src/Heddle.Language/HeddleLexer.g4`: `START_OUT` (`OUT_ST WS* -> type(OUT), pushMode(OUT_MODE)`, ~line 60) and `SUB_START_OUT` (~line 82) enter `OUT_MODE`; `OUT_OUT_START` (`OUT_ST WS* -> type(OUT)`, ~line 193) re-emits `OUT` with **no** mode action. So `@@` produces two adjacent `OUT` tokens. The lexer has **13 modes**. |
| `@@` is an error today — **except** when the second `@` opens a comment | `src/Heddle.Language/HeddleParser.g4`: `outblock: OUT chain subtemplate?` (line 51); `chain: call (DELIM call)*` (line 53); `call` requires `extension_id? OUT_PARAMSTART …` (line 55). Two *directive* `OUT` tokens never reduce. **Empirically confirmed** (probe compile, `typeof(object)` model): `a@@b` → `Success=False`, `[1,2:1]HED0003: extraneous input '@' expecting {ID, OUT_PARAMSTART}`. Same for `@@@@`, `@@@(Title)`, `@@%`, `@@(x)`, `Reach us at @@example.com`. **CORRECTION (verified against source — the plan/earlier-draft claim “no compiling template contains `@@`” is FALSE):** when the character after the first `@` is `*`, the second `@` begins a **comment** `@*…*@`, not a second directive. In `OUT_MODE` (and reached from default/`SUB_BLOCK` via `START_OUT`/`SUB_START_OUT`), `OUT_COMMENT` (HeddleLexer.g4:181–182) is a **hidden-channel** rule with **no `popMode`**, so after the comment the lexer is still in `OUT_MODE` and a following `partial(…)` reduces as the call. Thus `@` + `@*comment*@` + `partial(…)` is a **compiling** outblock whose source **starts with contiguous `@@`**. In-tree proof: `src/Heddle.Tests/TestTemplate/template.heddle:57` = `@@*Comment Test 8*@partial(@*Comment Test 9*@){{partial@*Comment Test 10*@}}` (inside a `@list(){{ … }}` `SUB_BLOCK` body). This is the only contiguous-`@@` shape that compiles today (`@@\`, `@@{…}@`, `@@%…`, `@@<<…`, `@@(x)`, `@@@…` all still fail to parse — the `@\`/raw/def/import/second-directive continuations do not form a call after the leading directive `@`). The `@@` escape (D1/WI1) therefore **must not fire when the second `@` is a comment start** — see the `{_input.LA(1) != '*'}?` predicate in D1/D2/WI1. |
| Literal `{{ ident }}` in text | **Empirically confirmed**: `<p>{{ Title }}</p>` → `Success=True`, **no diagnostics**, renders `<p>{{ Title }}</p>` verbatim. Cause: default-mode `TEXT: (~[@]+ \| .)` (HeddleLexer.g4 line 66) greedily consumes the whole run because it contains no `@`; `UNCONNECTED_SUB_ST`/`UNCONNECTED_SUB_CL` (lines 62–64) only win a length-tie against `TEXT`, which happens **only** when a `@` sits immediately after `{{`. A bare-identifier/dotted-path body has no `@`, so **the misread is always wholly inside one `TEXT` token** and never a `SUB_START`. `{{ Author.Name }}`, `{{ a + b }}`, `{{ x:y }}`, `x {{ Title }} y` likewise compile clean and render literally. |
| Raw region substitution | `src/Heddle/Runtime/HeddleCompiler.cs` — `ReplaceRawOutput` (line 553) replaces each `RawOutputItem.BlockPosition` span with `RawOutputItem.Text` via `ExStringBuilder.Replace`, computes `seed = BlockPosition.Length - Text.Length`, and shifts every later `OutputChain.BlockPosition` back by `seed`. Iterates `RawOutputItems` in reverse (document order right-to-left) so left items keep original coordinates until their turn. **This is the collapse path the `@@` escape reuses.** Empirically: `@{ {{ Title }} }@` → renders ` {{ Title }} ` verbatim (raw content is inert). |
| Raw item construction | `src/Heddle/Language/ParseContext.cs` — `CreateRawOutputItem(HeddleParser.RawContext)` (line 447): reads `raw.GetText()`, sets `oneLineStyle = text.StartsWith("@:")`, and for a non-one-line block asserts `text.Length >= 4` (else throws “Raw block is wrongly formatted”), stripping the `@{`/`}@` (or `@:`) wrapper into `Text`. A bare `@@` (length 2) would hit that throw — so this method needs the `@@` special-case (WI1). |
| The `HED3001` gap-scan precedent | `HeddleCompiler.cs` — `ProcessBranchSets` (line 194) → `CollectGap` (line 345): computes a gap span between two chain `BlockPosition`s, reads `workingDocument.Substring(gapStart, gapLength)`, and on non-whitespace adds a `HeddleCompileWarning { DiagnosticId = HeddleDiagnosticIds.BranchTextStripped }` to `compileScope.CompileWarnings`. This is the **mechanism** the misread lint mirrors (substring a text span → warn on `compileScope.CompileWarnings`), not the exact span set. |
| Where warnings surface | `HeddleCompiler.cs` line 358 etc. add to `compileScope.CompileWarnings`; `CompileScope.CompileWarnings => CompileContext.CompileWarnings` (`src/Heddle/Runtime/CompileScope.cs` line 17). The template exposes them as `HeddleTemplate.Context.CompileWarnings` (used by `DoubleRenderWarningTests`). **Verified plan-relevant correction:** `DocumentParser.Runtime.cs`’s `CopyErrorsTo` (line 38) copies only `ParseContext.Errors` → `CompileContext.CompileErrors`; **`ParseContext.Warnings` is not propagated to the result.** Therefore the misread lint must add to `compileScope.CompileWarnings` (a compiler pass), **not** to `ParseContext.Warnings` (a listener pass) — see D6. |
| `CompileBody` entry state — **two coordinate spaces at entry, not one** | `HeddleCompiler.cs` — `CompileBody(document, compileScope, parseContext, chainedType)` (line 75): `workingDocument = document`; then, in order, `ShiftBySkippedTokens` (line 80, positions only), `TrimHiddenRemnantLines` (lines 81–82, **only when `Options.TrimDirectiveLines`**), `RemoveDefinitions` (83), `ReplaceRawOutput` (84), `ProcessBranchSets` (85) mutate positions and/or `workingDocument`. **CORRECTION (verified — the earlier “one coordinate space at entry” premise is FALSE):** the `document` passed in is the **hidden-token-excised** text (`tree.GetText()`, i.e. comments and `@\`+WS removed) on the runtime path — `HeddleTemplate.cs:326` passes `optimizedDocument` — while the `BlockPosition` lists come off the walk in **original-source** coordinates and are only rebased onto the clean document by `ShiftBySkippedTokens` (using `parseContext.SkippedTokens`, the hidden tokens, added at `DocumentParser.cs:95–98`). So **at entry the clean `document` and the original-coordinate `BlockPosition`s do NOT share a coordinate space**; they coincide only *after* `ShiftBySkippedTokens`. (`GetBlockPosition` subtracts `_offset` = `AbsoluteOffset`, making positions body-relative; `AbsoluteOffset` = 0 for the top-level body.) The editor path is different again: `DocumentAnalyzer.cs:45` passes the **original** `text` (not `optimizedDocument`) into `HeddleCompiler.Compile`, so after `ShiftBySkippedTokens` its `workingDocument` (original) and its `BlockPosition`s (clean) diverge — the pre-existing skew shared by `HED3001`/`HED2003`. The misread scan (D5) therefore runs **after** `ShiftBySkippedTokens` (positions rebased to the clean document the runtime scans), not before. |
| Structured-span lists | `ParseContext`: `OutputChains`, `DefaultChains`, `RawOutputItems`, `DefinitionsBlock.Positions` (all `public`/accessible on the context). **`DefaultChains` is *not* a misread-exclusion span** — `ShiftBySkippedTokens` (`HeddleCompiler.cs:583/604/628`) rebases only `OutputChains`, `DefinitionsBlock.Positions`, and `RawOutputItems`; `DefaultChains` is never rebased, and a `default_chain` (`DEF_OUT chain`, HeddleParser.g4:47) carries no `{{ … }}` body, so it is deliberately excluded from D5's scan-exclusion set. `EnterDefinition` (`HeddleMainListener.cs` line 22; its `AddNewBlockPosition` call at line 25) and `ExitImport_block` (line 216; its `AddNewBlockPosition` call at line 308) both call `DefinitionsBlock.AddNewBlockPosition`, so **definition bodies and `@<<{{ … }}` import blocks both live in `DefinitionsBlock.Positions`** and are excludable by one check. A real `@call(){{ body }}` chain’s `BlockPosition` spans `OUT … subtemplate` (covers the `{{ }}` body) — `CreateOutputChain` sets `BlockPosition = GetBlockPosition(context)` over the `OutblockContext` (line 417). |
| Diagnostic IDs | `src/Heddle/Data/HeddleDiagnosticIds.cs`: `HED4001`–`HED4004` are the highest `HED4xxx` (`RangeStepNotPositive`, `DefinitionRendersTwice`, `LegacyImportDirective`, `ComposeImportNotTopLevel`). **`HED4005` is the next free** ergonomics id. `src/Heddle.Tests/DiagnosticIdTests.cs` — `ConstantsMatchTheDiagnosticsTableOneToOne` (line 46) reflects over the const list and asserts a one-to-one match with an inline `expected` set and equal counts; a new const **must** be added to both the class and that set. |
| Generated grammar | `src/Heddle.Language/generated/` holds committed ANTLR output (`HeddleLexer.cs`, `HeddleParser.cs`, `HeddleLexer.tokens`, `*.interp`, listener/visitor). `src/Heddle.Language/generate_cs.cmd` regenerates via `antlr-4.13.1-complete.jar` (`-Dlanguage=CSharp`, `-package Heddle.Language`, `-o generated`). Coding-standards: generated code is never hand-edited — a `.g4` change is edit → regen → commit both. testing-standards regression gate item 3: a spec that changes grammar commits exactly one regen and diff-reviews it. |
| Line-ending pins | `.gitattributes` already pins `src/Heddle.Tests/TestTemplate/** text eol=lf` (line 18) and `*.heddle text eol=lf` (line 7). New fixtures placed under `TestTemplate/` are therefore already LF-pinned — **no new `.gitattributes` root is introduced** (WI4). |
| Downstream docs | `docs/syntax-highlighting.md` lines 65–67 state “**`@@` is not an escape** … `@@` is a compile error … The highlight is cosmetic only”. `docs/language-reference.md` line 135 (“there is no character-doubling escape (`@@` is a compile error)”) and line 1157 (“there is no `@@` doubling escape”) say the same. The shipped TextMate grammar (`docs/coloring-scheme/heddle.tmLanguage.json`) **already colors `@@`** as a literal-`@` escape. These three doc spots are the reconciliation surface (WI5). |

## Design decisions

Every Phase 2 open question (P2-Q1…Q4) and plan lean is closed here.

### D1 — The `@@` escape is realized in the **lexer** (a licensed grammar change + regen), not at the compiler level

- **Decision.** Add two lexer rules to `HeddleLexer.g4` that recognize the two-character `@@` and re-type it as an existing `RAW` token — one in default (top-level text) mode, one in `SUB_BLOCK` mode — and map that token to a one-character literal `@` in `ParseContext.CreateRawOutputItem`. The existing `ReplaceRawOutput` path then collapses `@@` → `@` in the working document. This requires an ANTLR regen (WI1); the spec is **licensed to change the grammar** and commits exactly one regen.
- **Rationale.** A compiler-only realization is **impossible**: `@@` currently produces two `OUT` tokens that fail to parse (`HED0003`) *before any compiler pass runs* — verified empirically (`a@@b` → `HED0003` at the second `@`). Only the front end (the lexer) can stop that parse error. The minimal front-end change is to stop the second `@` from ever entering `OUT_MODE`; recognizing the `@@` pair as a single literal token does exactly that. Re-typing it as `RAW` (rather than inventing a token type) means: (a) `RAW` is already excluded from the parser `text` rule and already has the top-level/`SUB_BLOCK` alternative `raw: RAW`, so the parser needs **no** change (`HeddleParser.g4` is untouched); (b) the collapse reuses `ReplaceRawOutput` verbatim — an escaped literal `@` *is* one character of raw, verbatim output — so **no new compiler pipeline stage** is added. Only `HeddleLexer.g4` changes.
- **Alternatives rejected.**
  - *Pre-lex string substitution* (replace `@@`→`@` in the document before lexing) — rejected: it cannot distinguish `@@` inside raw blocks, comments, or C# string literals from a genuine top-level escape, and it corrupts all downstream offsets.
  - *A dedicated `ESCAPED_AT` token type + a new parser alternative + a new `EscapedAt` list + a new collapse pass* — rejected: it adds a token type (renumbering the parser vocabulary and enlarging the regen diff), a parser-rule change, and a compiler pass, for behavior identical to reusing `RAW`. YAGNI / precedence rule 3 (simplicity).
  - *Compiler-only handling* — rejected as impossible (above).
- **Grounding.** `HeddleLexer.g4` `START_OUT`/`SUB_START_OUT`/`OUT_OUT_START`; `HeddleParser.g4` `raw: RAW` (line 10) and `text: ~(… | RAW)` (line 109); `HeddleCompiler.ReplaceRawOutput`; empirical probe (Assumed state). **Regen-diff note (verified against `generated/HeddleLexer.tokens`):** a rule of the form `NAME: … -> type(RAW)` reuses the existing `RAW` token type and is **not** allocated its own token-type constant — confirmed for the sibling `-> type(…)` rules (`START_OUT`, `SUB_RAW`, `RW_LINE`, `OUT_OUT_START` are all absent from `HeddleLexer.tokens`). So `AT_ESCAPE`/`SUB_AT_ESCAPE` add **no** token-type number and **do not renumber** any later token; the regen diff is the two new rules plus their generated ATN, with the vocabulary unchanged. ANTLR maximal-munch: the two-char `@@` beats the one-char `START_OUT` (`@`) by length, independent of rule order ([ANTLR lexer rules — longest match](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md)). The escape mirrors [Razor’s `@@`](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/razor#escape-an--symbol).

### D2 — Exact tokenization and greedy left-to-right pairing (closes P2-Q3, P2-Q4)

- **Decision.** `@@` is recognized as the escape **everywhere a literal `@` is meaningful: top-level text and subtemplate bodies** (P2-Q3), **except when the character immediately after the pair is `*`** — a comment-adjacency guard (the `{_input.LA(1) != '*'}?` predicate, D1/WI1). Runs of `@` **pair greedily, left to right** (P2-Q4): each maximal `@@` pair (whose next char is not `*`) emits one literal `@`; a trailing lone `@`, or a `@@` that fails the guard, is left to the existing `START_OUT`/directive machinery. Because the escape rule matches the 2-char `@@` and `START_OUT` matches the 1-char `@`, ANTLR’s longest-match rule performs the greedy pairing automatically at each scan position; when the guard fails the escape rule does not match and the lexer falls back to `START_OUT`/`SUB_START_OUT` (the single `@`). The behavior of every enumerated form is therefore fixed:

  | Input (top-level text) | Tokenization | Renders |
  |---|---|---|
  | `a@@b` | `TEXT(a)` · `RAW(@@→@)` · `TEXT(b)` | `a@b` |
  | `@@@@` | `RAW(@@→@)` · `RAW(@@→@)` | `@@` |
  | `a@@`⟨EOF⟩ | `TEXT(a)` · `RAW(@@→@)` (guard: `LA(1)` = EOF ≠ `*` → fires) | `a@` |
  | `@@@`⟨EOF⟩ | `RAW(@@→@)` · `OUT` (lone trailing `@`, no chain) | **compile error `HED0003`** — the odd tail's leftover single `@` opens a directive with no call; error today, error after (D-BC1) |
  | `@@@(Title)` | `RAW(@@→@)` · `OUT (Title)` | `@` + value of `@(Title)` |
  | `@@(x)` | `RAW(@@→@)` · `TEXT((x))` | `@(x)` (both `(` and `x` are plain text after the escape) |
  | `@@%` | `RAW(@@→@)` · `TEXT(%)` | `@%` (the `%` is plain text; `@%` is **not** re-lexed as `DEF_START`, which requires a contiguous `@%` starting at a fresh `@`) |
  | `@@*Comment*@partial(…)` | guard fails (`LA(1)` = `*`): `OUT` · `OUT_COMMENT`(hidden) · `ID(partial)` · … | **unchanged** — directive `@` + comment + `partial(…)` call (byte-identical to today; template.heddle:57) |
  | `Reach us at @@example.com` | `TEXT(Reach us at )` · `RAW(@@→@)` · `TEXT(example.com)` | `Reach us at @example.com` |
  | `@list(Articles){{ @@ }}` | body ` @@ ` → `TEXT( )` · `RAW(@@→@)` · `TEXT( )` | ` @ ` per item (escape works in a subtemplate body; the surrounding spaces lex as `SUB_TEXT -> type(TEXT)`, **not** `WS`) |

- **Rationale.** Longest-match at each position gives deterministic, order-free greedy pairing with no special-casing beyond the one-character comment-adjacency guard. An **odd run** (`@@@`, `a@@@b`, …) pairs its leading `@@` greedily and leaves the final lone `@` to the existing `START_OUT`/directive machinery; with no call following, that lone `@` is the pre-existing `HED0003` error — unchanged from today (error→error, D-BC1), so the greedy-pairing story is complete for the odd tail. The two/three-`@` distinction (`@@(x)` → literal `@(x)`; `@@@(x)` → literal `@` + directive `@(x)`) is exactly the affordance authors want: `@@(x)` shows a literal `@(x)`; `@@@(x)` escapes one `@` and then interpolates. The guard is required for back-compat: without it the 2-char `@@` would win maximal munch over the directive-`@`-then-comment reading and retokenize `@@*Comment*@partial(…)` (template.heddle:57) into `RAW(@)` + `TEXT(*Comment*)` + `@partial(…)`, changing rendered bytes and breaking the byte-identical-corpus gate (D-BC1/WI4). **Accepted limit:** with the guard, an author cannot escape a literal `@` that is *immediately* followed by `*` via `@@*` (that stays directive-`@` + comment-start); to output a literal `@*`, use a raw region (`@{@*}@`) or a raw line (`@:@*`). Documented in WI5.
- **Alternatives rejected.** *Right-to-left or global even/odd counting* — rejected: not expressible in a regular lexer rule and surprising for `@@@`. *Restrict the escape to top-level text only* — rejected: P2-Q3 requires subtemplate bodies too, and the `SUB_BLOCK` rule is a one-line mirror of the top-level rule.
- **Grounding.** ANTLR maximal munch (above); `SUB_BLOCK` already carries a raw path (`SUB_RAW: RAW_BLOCK -> type(RAW)`, HeddleLexer.g4 line 74) so a `SUB_*` escape composes with the same parser `raw` alternative in the recursive `heddle` body rule.

### D3 — Where the escape does **not** apply (bounded blast radius)

- **Decision.** The escape rule is added **only** to default mode and `SUB_BLOCK` mode. It is **not** added to `CS`/`CS_NESTED`/interpolated-string modes (C# owns `@` there — verbatim strings), nor to `OUT_MODE`, `CALL`, `CALL_RETURNED`, `IMPORT_MODE`, or `DEF`/`DEF_PROPS`. Inside a raw block `@{ … }@`, a raw line `@:…`, or a comment `@* … *@`, `@@` stays verbatim because those rules consume their whole span as one token before the escape rule can see the inner characters (e.g. `@{ @@ }@` renders ` @@ `).
- **Rationale.** These are precisely the positions where a literal `@` is *not* free text: C# expression context, directive-call context, and already-verbatim regions. Confining the rule to the two text-bearing modes is the smallest change that satisfies P2-Q3 and guarantees no existing compiling construct is retokenized (D-BC1). `@(x)@@` (an escape immediately after a call closes, in `CALL_RETURNED`) is a **deferred non-goal** (see *Deferred items*) — it is an error today and stays one; no plan scenario exercises it, and adding a `CALL_RETURNED` rule would widen the regen surface for no required behavior.
- **Alternatives rejected.** *Add the escape to every `@`-bearing mode for symmetry* — rejected by YAGNI and by the back-compat proof cost (more modes to prove inert). *Interpret `@@` inside raw regions* — rejected: raw is verbatim by definition (R4-adjacent invariant), and it would be a surprising special case.
- **Grounding.** HeddleLexer.g4 modes `CS`/`CS_NESTED` (lines 287, 311), `RAW_BLOCK`/`RAW_LINE` fragments (lines 43–45), `COMMENT_BLOCK` (line 42); probe (`@{ {{ Title }} }@` verbatim).

### D4 — The misread lint id, message, and severity (closes P2-Q1)

- **Decision.** The misread lint claims **`HED4005`** (ergonomics family), a public constant `HeddleDiagnosticIds.LiquidStyleInterpolationMisread`, **severity warning** (a `HeddleCompileWarning`; never blocks a build). Exact `Error`, `Fix`, and position are pinned in *Diagnostics / error surface*. The LSP surfaces it as `Diagnostic.code = "HED4005"` via the existing `HeddleCompileError.DiagnosticId` → `Diagnostic.code` mapping (D1); no LSP-side change is needed beyond the id existing.
- **Rationale.** P2-Q1 fixed `HED4005`; it is verified next-free (Assumed state) and belongs to `HED4xxx` (template semantics & ergonomics). Warning severity is mandatory under R5/D1: the pattern is a *valid* template today, so a hard error would break it.
- **Alternatives rejected.** *Error severity* — rejected (R5: breaks a template that legitimately prints braces). *A `HED6xxx` tooling-only code* — rejected: this is a compile diagnostic that must appear in `CompileResult` for non-LSP hosts too, and `HED6xxx` is reserved for non-compile tooling messages (D1 registry).
- **Grounding.** cross-cutting-decisions D1 + registry; open-questions P2-Q1.

### D5 — The misread lint is a compiler pass scanning literal-text spans in the post-shift coordinate space (closes P2-Q2)

- **Decision.** Add a compiler pass `ScanBraceMisreads(parseContext, compileScope, workingDocument)` invoked in `HeddleCompiler.CompileBody` **immediately after `ShiftBySkippedTokens` (line 80) and before `TrimHiddenRemnantLines`/`RemoveDefinitions`/`ReplaceRawOutput`** — i.e. the first statement *after* the shift, while `workingDocument` (still `== document`, unmutated) and the shifted exclusion-span `BlockPosition`s (`OutputChains`/`RawOutputItems`/`DefinitionsBlock.Positions`) **now share the clean, hidden-token-excised coordinate space** on the runtime path. **This placement is a correction:** the earlier “run before `ShiftBySkippedTokens`” design was wrong because at entry the runtime’s `document` is already the clean `optimizedDocument` while the `BlockPosition`s are still in original coordinates (Assumed-state correction), so the exclusion would compare a clean-document match offset against original-coordinate spans and mis-skip. In particular `template.heddle:57`’s `{{partial@*Comment Test 10*@}}` excises its comment to a clean `{{partial}}` that the D7 regex matches; only when the exclusion spans are in the **same** clean coordinate space (post-shift) does that match fall inside the `partial(…){{partial}}` chain span and get skipped. The pass:
  1. Finds every match of the fixed regex (D7) in `workingDocument`.
  2. For each match, takes the (clean-document) offset of its opening `{{` and **skips** it if that offset lies within any span in `parseContext.OutputChains`, `parseContext.RawOutputItems`, or `parseContext.DefinitionsBlock.Positions` (the last covers definition bodies **and** `@<<{{ … }}` import blocks) — the **three** lists `ShiftBySkippedTokens` rebases (HeddleCompiler.cs:583/604/628), now all in the post-shift/clean coordinate space. (`DefaultChains` is deliberately **not** an exclusion span: a `default_chain` is `DEF_OUT chain` with **no** `{{ … }}` subtemplate body — HeddleParser.g4:47 — so no misread can ever sit inside one; and `ShiftBySkippedTokens` never rebases `DefaultChains`, so listing it would introduce a coordinate mismatch for zero benefit. Any real `{{ … }}` in a definition lives in the definition's `subtemplate` body, already covered by `DefinitionsBlock.Positions`.)
  3. For every surviving match, adds one `HeddleCompileWarning` (`HED4005`) at position `AbsoluteOffset + matchStart`, length 2 (see *Diagnostics* for the coordinate-space caveat on the reported position).
- **Rationale.** Because a bare-identifier/dotted-path body has no `@`, the misread is always a substring of one literal `TEXT` token that renders verbatim (Assumed state). Running the scan **after `ShiftBySkippedTokens`** makes the three exclusion-span lists (`OutputChains`/`RawOutputItems`/`DefinitionsBlock.Positions`) and the scanned `workingDocument` one coordinate space (on the runtime path), so excluding those spans isolates exactly the literal-text runs: a real `@call(){{ ident }}` body falls inside the chain span (skipped); a raw region falls inside a `RawOutputItems` span (skipped); a definition body or `@<<` import falls inside a `DefinitionsBlock` span (skipped). Running the scan **before `ReplaceRawOutput`** still avoids the raw-substitution timing hazard (after `ReplaceRawOutput`, a raw region’s inner `{{ Title }}` — and a WI1 `@@`→`@` collapse — would appear in `workingDocument` as bare text with its `RawOutputItems` span consumed, and false-fire); before `RemoveDefinitions`/`TrimHiddenRemnantLines` the definition/raw spans are still present and valid to exclude against. Adding to `compileScope.CompileWarnings` is the surface that actually reaches `CompileResult` (Assumed-state correction: `ParseContext.Warnings` is not copied out).
- **Editor-path limit (documented).** On the **editor** path `DocumentAnalyzer.cs:45` passes the **original** `text` (not `optimizedDocument`); after `ShiftBySkippedTokens` its `workingDocument` is original while the spans are clean, so the scan’s exclusion is coordinate-skewed **whenever hidden tokens (comments / `@\`) precede a block** — the same pre-existing skew that already affects `HED3001`/`HED2003` on this path. Consequence: the editor may surface an occasional spurious `HED4005` on a real `@if(F){{ ident }}` body preceded by a comment that the runtime `CompileResult` correctly suppresses. This is an **accepted, editor-only, warning-tier limit** (surfacing is free; positional/exclusion fidelity under comments/`@\` is best-effort), most-reversibly closed by reconciling the two `HeddleCompiler.Compile` callers so the editor also passes the clean document — a change to `DocumentAnalyzer.cs` (shared source, outside this spec’s two files) flagged to the orchestrator, not undertaken here.
- **Alternatives rejected.**
  - *A parse-time listener pass (like `HED4003`/`HED4004`)* — rejected: those use `ParseContext.Errors`, which **is** copied out; `ParseContext.Warnings` is **not** (verified `CopyErrorsTo`), so a listener warning would silently never surface.
  - *Running the scan before `ShiftBySkippedTokens`* — rejected: the runtime `document` is clean but the spans are original-coordinate at that point, so exclusion mis-skips (the template.heddle:57 false-fire above).
  - *Scanning `workingDocument` after raw replacement* — rejected: raw inner text becomes bare and false-fires.
  - *A regex over the whole original document at top level only* — rejected: misses nested-body literals and cannot cheaply exclude nested structured spans; the per-body scan reuses the exclusion lists already local to each context.
- **Grounding.** `HeddleCompiler.CompileBody` ordering (lines 78–85); `ShiftBySkippedTokens` (rebases `OutputChains`/`DefinitionsBlock.Positions`/`RawOutputItems` to the clean document, lines 574–644); `HeddleTemplate.cs:326` (runtime passes `optimizedDocument`) vs `DocumentAnalyzer.cs:45` (editor passes original `text`); `ProcessBranchSets`/`CollectGap` (the `workingDocument.Substring` → warn precedent); `DocumentParser.Runtime.cs` `CopyErrorsTo`; `HeddleMainListener` `AddNewBlockPosition` sites.

### D6 — Cardinality: one warning per literal occurrence

- **Decision.** The pass emits exactly one `HED4005` per surviving regex match (per literal `{{ … }}`). Multiple misreads in one `TEXT` run each warn, each positioned at its own `{{`. Per-body scanning follows the same cardinality as the `HED3001`/`HED4002` compile passes: a definition body compiled at *N* call sites is scanned *N* times (documented, matching the existing double-render precedent). The top-level document body is compiled once, so a top-level `<p>{{ Title }}</p>` warns exactly once — the success criterion.
- **Rationale.** Matches the plan success criterion (“exactly one misread warning” for a single top-level occurrence) and the existing compile-pass cardinality (`DoubleRenderWarningTests.W08` codifies the same per-compile counting). No dedup machinery is warranted (YAGNI); warning-only severity makes any definition-body repeat harmless noise.
- **Alternatives rejected.** *Global dedup by position* — rejected: adds state for a case no scenario requires; inconsistent with `HED3001`/`HED4002`.
- **Grounding.** `DoubleRenderWarningTests.W04`/`W08`.

### D7 — The exact misread pattern (closes P2-Q2)

- **Decision.** The pass uses this single compiled, culture-invariant regex (ASCII identifiers), matched against `workingDocument` (the same clean, post-shift text D5/WI2 name; at the insertion point `workingDocument == document`):

  ```
  \{\{[ \t]*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*\}\}
  ```

  Capture group 1 is the suggested path (e.g. `Title`, `Author.Name`). A match fires the warning (subject to the D5 span exclusions) **only** when the braces wrap a single identifier or dotted path with nothing else. The pattern therefore **cannot** match when the braces contain `@`, `:`, any operator (`+ - * / % < > = ! & | ^ ~ ? [ ]`), whitespace *inside* the path, a nested `{{`, an empty body, or a non-ASCII identifier — every one of P2-Q2’s exclusions falls out of the character classes. Inter-token whitespace is limited to spaces/tabs (`[ \t]`), not newlines, so a match is confined to one line.
- **Rationale.** This is the narrowest heuristic (plan risk row: false positives are `M`-sized; keep it tight). ASCII-only and single-line keep precision high — a missed suggestion on an exotic identifier is acceptable; a false positive is the risk to avoid (precedence rule 3, plan mitigation). Dotted paths are explicitly in scope (validation scenario `<p>{{ Author.Name }}</p>`). Excluding `:` covers `{{ x:y }}`; the operator classes and required contiguity cover `{{ a + b }}`.
- **Alternatives rejected.** *Unicode `\w` identifiers* — rejected: widens false positives (e.g. accented prose in braces) for a narrow gain; a most-reversible widening trigger is recorded in *Deferred items*. *Allowing whitespace/newlines inside the path* — rejected: it would match multi-line prose. *A hand-rolled scanner instead of a regex* — rejected: the regex is total, readable, and compile-time only (no render-path cost); a scanner is more code for identical behavior.
- **Grounding.** open-questions P2-Q2; plan validation-scenarios table; empirical probe (`{{ a + b }}`, `{{ x:y }}` compile clean and must not warn).

### D-BC1 — Back-compat is proven by construction and by the byte-identical corpus

- **Decision.** No template that compiles today changes its bytes. The `@@` escape fires on the two-char `@@` in text/`SUB_BLOCK` **only when the next character is not `*`** (the D1/D2 comment-adjacency guard). The one contiguous-`@@` shape that *compiles* today — directive `@` immediately followed by a comment `@*…*@` and then a call (template.heddle:57) — is `@@*…`, which the guard **excludes**, so it retokenizes identically to today. Every other contiguous-`@@` shape (`@@`, `@@@@`, `@@@(…)`, `@@(x)`, `@@%`, `@@\`, `@@{…}@`, `@@<<…`) is a `HED0003` compile error today (verified: the `@\`/raw/def/import/second-directive continuations do not form a call after the leading directive `@`), so turning it into a literal `@` is additive, not a byte change to a compiling template. Every non-doubled `@`-construct (`@(`, `@%`, `@<<`, `@{`, `@:`, `@*`, `@\`, `@ @` with a space) is unaffected because none begins with a contiguous `@@`. The misread lint emits no output and never fails a build. Proof obligations: the full existing golden/differential corpus stays byte-identical (WI4 regression gate, **including template.heddle** — its line 57 `@@*` case is the load-bearing guard fixture), and a corpus scan asserts **zero** new `HED4005` on existing fixtures.
- **Rationale / grounding.** R5; plan *Back-compat / impact*; the corrected `@@` fact (Assumed state: `@@` compiles today *only* as directive-`@`-then-comment `@@*…`, which the guard preserves; every other contiguous `@@` is a `HED0003` error).

## Implementation plan

Ordered; an implementer works straight down. WI1–WI3 are independent of each other except that WI3’s id must exist before WI2 compiles; WI4/WI5 close the change.

### WI1 — `@@` → `@` lexer escape (grammar change + regen)

- **Files.** `src/Heddle.Language/HeddleLexer.g4`; `src/Heddle/Language/ParseContext.cs`; regenerated `src/Heddle.Language/generated/**` (committed).
- **Change.**
  - In **default mode**, immediately before `START_OUT` (~line 60), add:

    ```antlr
    AT_ESCAPE: '@@' {_input.LA(1) != '*'}? -> type(RAW);
    ```

  - In **`SUB_BLOCK` mode**, immediately before `SUB_START_OUT` (~line 82), add:

    ```antlr
    SUB_AT_ESCAPE: '@@' {_input.LA(1) != '*'}? -> type(RAW);
    ```

    The trailing semantic predicate `{_input.LA(1) != '*'}?` is the **comment-adjacency guard** (D1/D2): after matching `@@`, `_input.LA(1)` is the next character; when it is `*`, the second `@` is a comment start (`@*…*@`, `OUT_COMMENT`, hidden, no `popMode`), so the escape must **not** fire and the lexer falls back to `START_OUT`/`SUB_START_OUT` (the single directive `@`), preserving template.heddle:57 byte-for-byte. `_input.LA(1)` is the standard ANTLR C#-runtime lookahead available in a lexer action; the predicate at the end of the rule is evaluated after the two `@` are consumed. (Placement is cosmetic for longest-match — the 2-char `@@` wins over the 1-char `START_OUT`/`SUB_START_OUT` regardless of order — but placing the rule beside its sibling documents intent.) At EOF, `_input.LA(1)` returns `IntStreamConstants.EOF` (−1) ≠ `'*'`, so a trailing `@@` at end-of-input escapes (`a@@`⟨EOF⟩ → `a@`).
  - In `ParseContext.CreateRawOutputItem`, immediately after `var text = raw.GetText();` (`ParseContext.cs:453`; the `raw == null` guard is at line 451, **before** `GetText()`), and before the `oneLineStyle` computation (line 455), add:

    ```csharp
    if (text == "@@")
    {
        return new RawOutputItem
        {
            BlockPosition = GetBlockPosition(context),
            Text = "@"
        };
    }
    ```

  - Run `src/Heddle.Language/generate_cs.cmd` (ANTLR 4.13.1) and commit the regenerated `generated/**` in the same commit as the `.g4` edit. No `HeddleParser.g4` change is made or needed.
- **Done when.** `a@@b`→`a@b`, `@@@@`→`@@`, `a@@`⟨EOF⟩→`a@`, `@@@(Title)`→`@` + `@(Title)` value, `@@(x)`→`@(x)`, `@@%`→`@%`, `Reach us at @@example.com`→`Reach us at @example.com`, and `@list(Articles){{ @@ }}`→` @ ` per item, all render as pinned in D2 (WI4 goldens); **and `@@*Comment*@partial(…)` (the guard case) renders byte-identically to today** — verified by the existing `template.heddle` golden staying byte-identical **and** a dedicated `at-escape-comment-adjacent.heddle` regression fixture (WI4). The grammar-stability gate is satisfied by the single reviewed regen diff (no token-type renumbering — D1 regen-diff note); `HeddleParser.cs` behavior is unchanged (proven by the byte-identical corpus).

### WI2 — `HED4005` misread lint (compiler pass)

- **Files.** `src/Heddle/Runtime/HeddleCompiler.cs`.
- **Change.**
  - Add a `static readonly System.Text.RegularExpressions.Regex` field compiled from the D7 pattern (`RegexOptions.CultureInvariant | RegexOptions.Compiled`).
  - Add `private static void ScanBraceMisreads(ParseContext parseContext, CompileScope compileScope, string workingDocument)` implementing D5/D6/D7: iterate `Regex.Matches(workingDocument)`; for each, let `at = m.Index` (clean-document offset of `{{`); skip if `Contains(b, at)` is true for the `BlockPosition` of any element in the **three** exclusion lists — read `.BlockPosition` off each element of `parseContext.OutputChains` (`List<OutputChain>`) and `parseContext.RawOutputItems` (`List<RawOutputItem>`), and iterate `parseContext.DefinitionsBlock.Positions` (`List<BlockPosition>`) directly (a local `static bool Contains(BlockPosition b, int i) => i >= b.StartIndex && i < b.StartIndex + b.Length;`); otherwise add

    ```csharp
    compileScope.CompileWarnings.Add(new HeddleCompileWarning
    {
        Error = $"'{{{{ {path} }}}}' in text renders literal braces — '{{{{ … }}}}' is a subtemplate body, not interpolation, so the value of '{path}' is not printed.",
        Fix = $"To output the value, use '@({path})'.",
        Position = new BlockPosition(parseContext.AbsoluteOffset + at, 2),
        DiagnosticId = HeddleDiagnosticIds.LiquidStyleInterpolationMisread
    });
    ```

    where `path = m.Groups[1].Value`.
  - Invoke `ScanBraceMisreads(parseContext, compileScope, workingDocument);` **immediately after `ShiftBySkippedTokens(parseContext);` (line 80) and before `TrimHiddenRemnantLines`/`RemoveDefinitions`/`ReplaceRawOutput`** (D5). At that point `workingDocument == document` (unmutated) and the three exclusion-span lists (`OutputChains`/`RawOutputItems`/`DefinitionsBlock.Positions`) are rebased to the same clean coordinate space on the runtime path, so the exclusion is aligned. Do **not** invoke it before the shift.
  - **Reported-position coordinate.** `at` is a clean-document (post-shift) offset, so `Position = new BlockPosition(parseContext.AbsoluteOffset + at, 2)` is in the compiled-document coordinate space. This equals the original-source offset whenever no hidden token (comment / `@\`) precedes the match within the body — the overwhelmingly common case; when hidden tokens precede it, the reported offset is short of the original by their excised length. This is an accepted, most-reversible, warning-tier imprecision (revisit trigger: a report that the reported offset is materially wrong under comment-heavy bodies, closed by an inverse-`ShiftBySkippedTokens` mapping over `parseContext.SkippedTokens`). It is **not** anchored to an `OutputItem.Position` because a bare literal `{{ ident }}` in text has no output item.
- **Done when.** `<p>{{ Title }}</p>` compiles (`Success=True`), renders byte-identically, and yields exactly one `HED4005` at the `{{`; `<p>{{ Author.Name }}</p>` warns with suggestion `@(Author.Name)`; `{{ a + b }}`, `{{ x:y }}`, `@if(Flag){{ Title }}` (real body), `@{ {{ Title }} }@` (raw), `@<<{{ layout }}` (import), **and every existing corpus fixture including `template.heddle` (its line-57 `{{partial}}` clean-form is excluded by the post-shift chain span)** yield **no** `HED4005`.

### WI3 — Claim `HED4005` (id constant + tests + registry)

- **Files.** `src/Heddle/Data/HeddleDiagnosticIds.cs`; `src/Heddle.Tests/DiagnosticIdTests.cs`; `docs/spec/common/cross-cutting-decisions.md`.
- **Change.**
  - In `HeddleDiagnosticIds`, after `ComposeImportNotTopLevel` (`HED4004`, line 125), add:

    ```csharp
    /// <summary>A literal <c>{{ identifier }}</c> / <c>{{ dotted.path }}</c> appears in body text, where it
    /// renders verbatim braces rather than interpolating (the number-one misread for authors arriving from
    /// Liquid/Jinja/Mustache/Handlebars). Warning severity; never fires inside a real <c>{{ … }}</c> body or a
    /// raw region. Suggests <c>@(identifier)</c>.</summary>
    public const string LiquidStyleInterpolationMisread = "HED4005";
    ```

  - In `DiagnosticIdTests.ConstantsMatchTheDiagnosticsTableOneToOne`, add `"HED4005"` to the `expected` set (the `"HED4001", … "HED4004",` line). The count assertions then pass with the new const.
  - In `cross-cutting-decisions.md` *Claimed diagnostic IDs (registry)*, add a row (path relative to `docs/spec/common/`): `| `HED4005` | this spec ([phase-2-authoring-ergonomics](../post-2.0/phase-2-authoring-ergonomics.md)) | `{{ … }}`-in-text misread warning (suggests `@(…)`); severity warning |`. Per SD-C, `HED4005` is the only new id.
- **Done when.** `DiagnosticIdTests` passes; the registry lists `HED4005`; no other id is renumbered.

### WI4 — Fixtures, goldens, and regression corpus

- **Files.** New `.heddle` fixtures + `generated-*.html` goldens under `src/Heddle.Tests/TestTemplate/` (already LF-pinned; **no `.gitattributes` change**); new xUnit tests in `src/Heddle.Tests/` (see *Testing plan*).
- **Change.** Add the escape and lint fixtures/goldens listed in *Testing plan*; add `AtEscapeTests` and `BraceMisreadWarningTests` (modeled on `DoubleRenderWarningTests`). Extend a corpus scan to assert zero new `HED4005` and byte-identical renders across the existing corpus.
- **Done when.** All new tests green on every TFM (`net6.0;net8.0;net10.0`, `+net48` on Windows); the full existing golden/differential corpus is byte-identical.

### WI5 — Reconcile downstream docs and confirm the editor grammar

- **Files.** `docs/syntax-highlighting.md`; `docs/language-reference.md`.
- **Change.**
  - `syntax-highlighting.md` lines 65–67: replace the “**`@@` is not an escape** … `@@` is a compile error … The highlight is cosmetic only” bullet with a statement that `@@` **is** the literal-`@` escape and the TextMate grammar’s existing `@@` coloring is now **correct** (no grammar edit needed).
  - `language-reference.md` line 135 (*Text and the `@` escape*) and line 1157 (*Behavioral nuances summary*): replace “there is no character-doubling escape (`@@` is a compile error)” / “there is no `@@` doubling escape” with the `@@` → literal-`@` rule, noting raw regions remain the tool for bulk literal text and `@@` is the single-character escape. **Document the one accepted limit (D2):** `@@` immediately followed by `*` is **not** an escape (it stays directive-`@` + comment-start `@*…`), so to output a literal `@*` use a raw region (`@{@*}@`) or raw line (`@:@*`).
- **Confirm (no code change).** The shipped `docs/coloring-scheme/heddle.tmLanguage.json` already colors `@@`; verify it still does and leave it. The Ace reference highlighter (`src/Heddle.Language/js/…`) is out of scope for this phase (cosmetic; not a compile surface).
- **Done when.** No doc states `@@` is an error; `cd docs && npm run docs:build` succeeds (testing-standards gate item 5, run from an uppercase-drive cwd on Windows).

## Public API / contract

- **New public constant:** `Heddle.Data.HeddleDiagnosticIds.LiquidStyleInterpolationMisread = "HED4005"` (`public const string`). Additive; carries XML docs (WI3). No other public type or member changes. `TemplateOptions` gains **no** property (the lint is unconditional and free at compile time), so the `TemplateOptions` copy-constructor / `Equals` / `GetHashCode` completeness invariant is not engaged.
- **Diagnostic object shape (existing types):** the lint produces a `HeddleCompileWarning` (`: HeddleCompileError`) with `Error`, `Fix`, `Position` (`BlockPosition`), and `DiagnosticId`; it surfaces via `CompileResult` / `HeddleTemplate.Context.CompileWarnings` and, for tooling, as `Diagnostic.code = "HED4005"` (D1). No new severity, list, or channel is introduced.
- **Thread safety.** The regex is a single immutable `static readonly` instance (`System.Text.RegularExpressions.Regex` is thread-safe for matching); the pass holds no per-render or per-instance mutable state (it runs at compile time and writes only to the per-compile `CompileScope`). Consistent with coding-standards (no mutable shared state on the render path).

## Diagnostics / error surface

| ID | Severity | Message (`Error` / `Fix`) | Trigger | Position |
|---|---|---|---|---|
| `HED4005` | warning | `Error`: `'{{ <path> }}' in text renders literal braces — '{{ … }}' is a subtemplate body, not interpolation, so the value of '<path>' is not printed.`  ·  `Fix`: `To output the value, use '@(<path>)'.` | A literal `{{ identifier }}` or `{{ dotted.path }}` (D7 regex, ASCII, single-line, nothing but the path inside) sits in body text — i.e. its `{{` is **not** inside any `OutputChains` / `RawOutputItems` / `DefinitionsBlock.Positions` span (D5). Never fires for a real `@call(){{ … }}` body, a raw region (`@{ … }@` / `@:`), a definition body, or an `@<<{{ … }}` import. | `BlockPosition(AbsoluteOffset + offsetOf("{{"), 2)` — offset of the opening `{{` in the **post-shift (clean, hidden-token-excised) document** the scan runs on, length 2. Equals the original-source offset when no comment/`@\` precedes the match in the body (common case); otherwise short by the excised length (accepted warning-tier imprecision, D5/WI2). On the editor path (`DocumentAnalyzer` passes original text) the exclusion is coordinate-skewed under preceding comments/`@\` — accepted editor-only limit (D5). One warning per occurrence (D6). |

`<path>` is the captured identifier/dotted path (regex group 1). No new **error** diagnostics are introduced (the `@@` escape *removes* a `HED0003` case; it adds none).

## Testing plan

**TDD verdict.** Test-first for both items — every success-criterion row below is written as a failing fixture/assertion before implementation, then driven green (testing-standards canonical loop). The `@@` grammar change is test-with for the regen mechanics (the regen is a build artifact) but test-first for observable render behavior.

**Suite home.** `src/Heddle.Tests` (xUnit, all TFMs). Fixtures + goldens under `src/Heddle.Tests/TestTemplate/` (sibling-pair `expr`-style stems; already LF-pinned). Model: `DoubleRenderWarningTests` (warning-count + golden + corpus scan) and `NativeExpressionSmokeTests` (`Render` helper).

### `@@` escape — `AtEscapeTests` (render assertions, one per success-criterion row)

Inline-template render tests (no model needed; `typeof(object)`), asserting exact output:

| Test | Template | Expected render |
|---|---|---|
| `AtAtBetweenText` | `a@@b` | `a@b` |
| `TwoPairs` | `@@@@` | `@@` |
| `EscapeThenDirective` | `@@@(Title)` (model with `Title="T"`) | `@T` |
| `EscapeThenParenText` | `@@(x)` | `@(x)` |
| `EscapePercent` | `@@%` | `@%` |
| `EmailAddress` | `Reach us at @@example.com` | `Reach us at @example.com` |
| `AtAtAtEof` | `a@@` | `a@` (guard: `LA(1)` = EOF ≠ `*`) |
| `OddRunTailErrors` | `@@@` | `Success=False`; exactly one `HED0003` (odd tail: leading `@@`→`@`, leftover lone `@` = directive with no call); no render — same error class as today (D2, D-BC1) |
| `CommentAdjacentAtAtUnchanged` | `@@*c*@partial(){{p}}` (model with a `partial` def rendering `p`) | renders exactly as today — directive `@` + comment + `partial` call (the guard suppresses the escape); assert the render is **identical** to the same template lexed by the pre-change grammar (byte-for-byte, no literal `@` and no literal `*c*`). |
| `EscapeInSubtemplateBody` | `@list(Articles){{ @@ }}` (two-item list) | ` @ ` per item |

Plus `AtEscapeGolden`: a fixture `at-escape.heddle` combining the **text-position** escape forms (`a@@b`, `@@@@`, `@@(x)`, `@@%`, and an email line) — deliberately no model-bearing directive, so it renders under the generic `typeof(object)` corpus harness — with a committed `generated-at-escape.html` golden (verbatim `Assert.Equal`, `\r\n`→`\n` normalized on read), proving the escape composes and the render is stable. The subtemplate-body case is covered by the inline `EscapeInSubtemplateBody` test above.

Plus `at-escape-comment-adjacent.heddle` (WI1 guard regression) + `generated-at-escape-comment-adjacent.html`: a fixture exercising the `@@*comment*@call(…)` shape both at top level and inside a `SUB_BLOCK` body (mirroring template.heddle:57), whose golden proves the guard leaves the comment-adjacent construct byte-identical. The existing **`template.heddle` golden staying byte-identical** is the primary in-corpus proof of the guard.

### `HED4005` misread lint — `BraceMisreadWarningTests`

Warning-count/position via `t.Context.CompileWarnings.Count(w => w.DiagnosticId == HeddleDiagnosticIds.LiquidStyleInterpolationMisread)` (the `DoubleRenderWarningTests` shape):

| Test | Template | Assertion |
|---|---|---|
| `SingleIdentifierWarnsOnceAndRendersLiterally` | `<p>{{ Title }}</p>` | `Success`; render == `<p>{{ Title }}</p>`; exactly one `HED4005`; `Position` at the `{{`; `Fix` suggests `@(Title)`. |
| `DottedPathWarns` | `<p>{{ Author.Name }}</p>` | one `HED4005`; suggestion `@(Author.Name)`. |
| `ExpressionBodyNoWarn` | `{{ a + b }}` | zero `HED4005`; renders literally. |
| `ColonBodyNoWarn` | `{{ x:y }}` | zero `HED4005`; renders literally. |
| `RealSubtemplateBodyNoWarn_WithDirective` | `@list(Articles){{ @(Title) }}` | zero `HED4005`. |
| `RealSubtemplateBodyNoWarn_BareIdentifier` | `@if(Flag){{ Title }}` | zero `HED4005` (real body, inside the chain span). |
| `RawRegionNoWarn` | `@{ {{ Title }} }@` | zero `HED4005`; renders ` {{ Title }} `. |
| `ImportBlockNoWarn` | `@<<{{ layout }}` (resolved via a test `ImportReader`, per the existing `ImportFormsPinningTests` harness) | zero `HED4005` (the import block’s span is in `DefinitionsBlock.Positions`). |
| `CommentBeforeRealBodyNoWarn` (F2 coordinate regression) | `@* note *@@if(Flag){{ Title }}` | zero `HED4005` — the comment excises to a clean `@if(Flag){{ Title }}` whose `{{ Title }}` clean offset falls inside the post-shift chain span (proves the scan runs **after** `ShiftBySkippedTokens`; a scan before the shift false-fires here). |
| `CommentBeforeTextMisreadWarns` (F2 coordinate regression) | `@* note *@<p>{{ Title }}</p>` | exactly one `HED4005` at the (clean-document) `{{`; renders `<p>{{ Title }}</p>` (comment stripped) — proves a genuine text-position misread preceded by a comment still fires and is excluded-correctly. |
| `TwoOccurrencesWarnTwice` | `<p>{{ A }}</p><p>{{ B }}</p>` | two `HED4005`, positioned at each `{{`. |

`BraceMisreadGolden`: fixture `brace-misread.heddle` (a bare `{{ Title }}` in text) with `generated-brace-misread.html` golden proving the warning does **not** alter output (the `ErgoDoubleRenderGolden` pattern).

### Negative / positioned-diagnostic and corpus tests

- **Positioned assertions** (testing-standards: assert `HED*` id **and** position, never “compilation failed/succeeded” alone): the two warn-cases above assert `DiagnosticId == "HED4005"` and `Position.StartIndex` at the `{{`.
- **Corpus scan** (extend the `DoubleRenderWarningTests.W08` pattern): over every `TestTemplate/*.heddle`, assert **zero** `HED4005` on all pre-existing fixtures (only the new `brace-misread.heddle` may carry one) and byte-identical renders.
- **Full golden/differential corpus byte-identity** (regression gate): the existing dynamic == precompiled differential corpus renders byte-identically before/after both items (D-BC1). This is the primary back-compat proof.

### Regression gate (one combined run, testing-standards)

1. `dotnet build -c Release` — whole solution, all TFMs.
2. `dotnet test src/Heddle.Tests` — full suite, all TFMs, zero failures; goldens byte-identical for untouched templates.
3. **Grammar-stability / regen:** this spec is licensed to change the grammar (WI1); the gate is satisfied by committing exactly one `generate_cs.cmd` regen diff alongside the `.g4` edit and diff-reviewing it. All other `generated/**` stays as regenerated. The lint (WI2) declares **no** grammar change.
4. **Benchmarks:** none required — no render-path or hot compile path is touched materially (see *Performance*). Run the existing compile/render benchmarks only as a spot-check that compile-time cost is within BenchmarkDotNet error.
5. `cd docs && npm run docs:build` — WI5 changed docs pages.

**`netstandard2.0`:** all new engine code (the escape mapping in `ParseContext`, the regex pass in `HeddleCompiler`) uses only BCL APIs present on `netstandard2.0` (`string.StartsWith`, `System.Text.RegularExpressions.Regex`, `RegexOptions.Compiled`). No modern-only API or `#if` fork is introduced (coding-standards; D7).

**Samples / gallery.** No new gallery project is added. Precedent: the comparable ergonomics warning `HED4002` shipped its user-visible demonstration as a `TestTemplate` golden fixture (`ergo-double-render.heddle`), not a gallery project. The curated 10-sample gallery is one-per-integration-pattern; a syntax affordance + a lint warrants a golden fixture, not an 11th integration app (YAGNI / precedence rule 3). The `at-escape.heddle` and `brace-misread.heddle` goldens are the demonstrable user-visible artifacts.

## Back-compat and migration

- **Preserved and proven.** No template that compiles today changes bytes (D-BC1). Proof: (a) the `@@` escape fires only on the two-char `@@` in text/`SUB_BLOCK` **whose next char is not `*`** (the comment-adjacency guard, D1/D2); the sole contiguous-`@@` shape that compiles today — directive-`@`-then-comment `@@*…*@call(…)` (template.heddle:57) — is `@@*…`, which the guard excludes, so it retokenizes identically; every other contiguous `@@` (`a@@b`, `@@@@`, `@@@(Title)`, `@@%`, `@@(x)`, `@@example.com`, `@@\`, `@@{…}@`) is a `HED0003` error today (verified). Every non-doubled `@`-construct is untouched (D3). (b) The misread lint emits no output and is warning-only. (c) The full golden/differential corpus stays byte-identical (**including `template.heddle`**), and a corpus scan asserts zero new `HED4005` on existing fixtures (WI4).
- **Additive contract change (plan-declared).** A contiguous `@@` **not** followed by `*` moves from *a compile error* to *emits `@`* — additive, since the prior behavior was a non-compiling error, not a valid render. The one compiling `@@*…` shape (directive-`@`-then-comment) is deliberately unchanged by the guard. One new stable id `HED4005` enters the registry; none is renumbered. R5 is satisfied on both counts.
- **Breaking window.** None. Neither item is a breaking change; nothing is scheduled into a breaking window ([breaking-windows.md](../common/breaking-windows.md)), and no migration note is required (no existing behavior is removed or reinterpreted for a compiling template).

## Performance considerations

- **Compile-time only; zero render-path cost.** Both items live entirely in the front end / compile pass. The `@@` escape resolves during lexing + the existing `ReplaceRawOutput` (already run once per compile); it adds no render-time work. The misread scan is one `Regex.Matches` over each body’s text at compile time — “compile once, render many”, so it is off every hot render path (coding-standards). No `Scope`, no per-render allocation, no delegate on the render path is touched.
- **Compile-path allocation.** The regex is a single `static readonly` compiled instance (one-time JIT of the compiled pattern at first use). Per compile, `Regex.Matches` allocates a match collection proportional to the number of `{{`-shaped substrings — negligible for template-sized inputs and bounded by document length. `ReplaceRawOutput` already allocates on raw substitution; `@@` reuses that path with no new allocation shape.
- **Guarding benchmark.** No new benchmark is mandated (no hot path touched). The existing `Heddle.Performance` compile/render suite is run as a spot-check (gate item 4): allocated bytes and mean time must stay within BenchmarkDotNet error — compile-time is expected flat, render-time unchanged.

## Standards compliance

- **YAGNI (the dominant call here).** Reusing `RAW` + `ReplaceRawOutput` for the escape, and a single regex compile pass for the lint, avoids a new token type, a new parser rule, a new pipeline stage, and a new options knob — each of which was a real temptation and each rejected because no scenario needs it (D1, D5, D7). The lint is unconditional (no `TemplateOptions` flag) precisely because YAGNI forbids a speculative toggle absent a scenario needing both values.
- **DRY (single representation of knowledge).** The `@@`→`@` collapse is expressed once, in the raw-substitution path both items’ raw already flows through; the misread “what is literal text” knowledge is expressed once as “not inside the three structured-span exclusion lists”, reusing the exact `BlockPosition` lists the compiler already maintains rather than re-deriving text runs.
- **SRP / open-closed.** Both changes stay in the stage they belong to: the escape in the lexer + parse-context raw builder; the lint as a compile pass beside `ProcessBranchSets`. No cohesive step is shredded into ceremony classes, and no new plugin seam is invented for an internal with one implementation (precedence rule 5).
- **Correctness first (precedence rule 1).** The back-compat proof (byte-identical corpus — including `template.heddle` — plus the guarded fact that every contiguous `@@` except the comment-adjacent `@@*…` was a compile error) is the acceptance test; the narrow D7 heuristic trades recall for precision so a warning never misleads on a valid template.

## Deferred items

| Item | Trigger to revisit |
|---|---|
| `@@` escape in `CALL_RETURNED` (a literal `@@` immediately after a call closes, e.g. `@(x)@@`) | A user report or fixture showing the “escape right after a directive” position is a real papercut; add a `CALL_RETURN_AT_ESCAPE` rule mirroring D2 and a regen. Error today, stays an error until then. |
| Unicode identifiers in the misread heuristic (D7 is ASCII-only) | Evidence of non-ASCII member names common enough that missed suggestions matter; widen the identifier class to `\p{L}`-based and re-measure false positives. Most-reversible: it only *adds* matches. |
| Newline-spanning or operator-bearing `{{ … }}` misread detection | A broader ergonomics-lint effort (assessment §3.8 attribute-encoding lints and beyond) that generalizes the text-position lint; this phase establishes the pattern but does not generalize it (plan *seam to later work*). |
| A dedicated gallery sample for the two affordances | A future docs/gallery pass that decides syntax affordances warrant gallery coverage; today the `TestTemplate` goldens suffice (HED4002 precedent). |

## External references

- [ANTLR4 lexer rules — maximal munch / longest match and mode structure](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md) — grounds D1/D2 (the 2-char `@@` wins over the 1-char `@` by length; per-mode rules).
- [ASP.NET Core Razor — escaping `@` with `@@`](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/razor#escape-an--symbol) — the industry-standard doubling affordance this mirrors.
- [.NET `System.Text.RegularExpressions.Regex` / `RegexOptions.Compiled` / thread safety](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex) — grounds the compile-time, thread-safe scan (available on `netstandard2.0`).
- Repository sources (verified, cited inline above): `src/Heddle.Language/HeddleLexer.g4`, `src/Heddle.Language/HeddleParser.g4`, `src/Heddle.Language/generate_cs.cmd`, `src/Heddle/Runtime/HeddleCompiler.cs` (`CompileBody`, `ReplaceRawOutput`, `ProcessBranchSets`/`CollectGap`), `src/Heddle/Language/ParseContext.cs` (`CreateRawOutputItem`, `GetBlockPosition`, span lists), `src/Heddle/Language/HeddleMainListener.cs` (`AddNewBlockPosition` sites), `src/Heddle/Language/DocumentParser.Runtime.cs` (`CopyErrorsTo`), `src/Heddle/Data/HeddleDiagnosticIds.cs`, `src/Heddle.Tests/DiagnosticIdTests.cs`, `src/Heddle.Tests/DoubleRenderWarningTests.cs`, `.gitattributes`, `docs/syntax-highlighting.md`, `docs/language-reference.md`, `docs/coming-from-liquid.md`.
