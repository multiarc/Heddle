# Heddle Ace v2 Migration — Gaps & Open Questions

**Related plan:** [ace-v2-migration-plan.md](ace-v2-migration-plan.md)

This document identifies gaps, ambiguities, and risky areas discovered during verification
of the plan against the actual codebase, specifications, and build pipelines.

> **Status: RESOLVED (2026-07-08).** All gaps below have been decided by the maintainer and
> folded into [ace-v2-migration-plan.md](ace-v2-migration-plan.md) §0 (decisions D-A…D-D)
> and the workstreams. The original findings are kept for the record; the **Resolution
> summary** table below maps each gap to its decision.

## Resolution summary

| Decision | Effect | Resolves gaps |
| --- | --- | --- |
| **D-A** — Ace tokenizer already extended for multiple modes safely | The "stateless regex" risks are moot; classification is per-mode | 4, 5, 9, "overlapping `:`" |
| **D-B** — Ace-first; recognize/classify all tokens; **no pixel parity**; VS Code handled separately | Success bar = correct Ace classification; WS3 (TextMate) deferred; WS9 parity redefined as coverage | 10, "parity definition", plus rescopes WS3 |
| **D-C** — Keep the Ace `v1.32.6` pin; upgrade later | No pin change now | "Ace version pinning" |
| **D-D** — Codegen (generate + propagate) is a build step, **CI fails on diff** | One enforced mechanism replaces the ad-hoc drift guard; local builds may need local regen | 1, 2, 3, 11, "no `.g4`↔JS sync enforcement" |
| Diagnostics: **parse-only baseline required, semantic HED2xxx–HED5xxx stretch via reused WASM worker** | WS5 direction fixed | 6, 7 |
| Completions: **hard-code registry defaults (baseline), engine-source via WASM (stretch)** | WS6 direction fixed | 8 |
| Beautify: **explicit `formatOptions` schema on `exports.formatOptions`** | WS4 scoped | 12 |

---

## Gaps & Open Questions

### 1. WS1: JS grammar regeneration — the root files are already v2, but the mode's copy is stale

**Concern:** The plan assumes the JS grammar must be regenerated from scratch. Verification
shows the situation is more specific:
- **Root `js/HeddleLexer.js`** (generated output) **already has** the v2 additions
  (`TRUE`, `FALSE`, `NULL`, `INT_LIT`, `REAL_LIT`, `STRING_LIT`, `CHAR_LIT`, `OP_*`,
  `THIS`, `ASSIGN`) and the `DEF_PROPS` mode.
- **The copy `js/src/mode/heddle/HeddleLexer.js`** (the one the Ace mode actually consumes)
  is still v1.x — ~35 tokens, ending at `CALL_WS`, no `DEF_PROPS` mode.

**Why it matters:** `build_ace.sh` copies `../../js/src/` into the Ace clone, so the Ace
bundle uses the **stale** copy even though the root files are current. The real WS1 task is
a **propagation/copy step**, not a full regen.

**Evidence:**
- [src/Heddle.Language/js/HeddleLexer.js](src/Heddle.Language/js/HeddleLexer.js) — v2 root version (includes `DEF_PROPS` mode).
- [src/Heddle.Language/js/src/mode/heddle/HeddleLexer.js](src/Heddle.Language/js/src/mode/heddle/HeddleLexer.js) — v1.x copy (no `DEF_PROPS`).
- [src/Heddle.Language/build_ace.sh](src/Heddle.Language/build_ace.sh) — copies `js/src/`.
- [src/Heddle.Language/generate_js.cmd](src/Heddle.Language/generate_js.cmd) — outputs to `js/`, not `js/src/mode/heddle/`.

**Recommended decision:** Rewrite WS1 around propagation. Pick one:
- **A** — add a documented copy step after regen: copy the generated `js/*.js` into
  `js/src/mode/heddle/` (excluding the hand-written `*Extended`/`Tokenizer`/`Listener` wrappers).
- **B** — change `generate_js.cmd` to emit directly into `js/src/mode/heddle/`.
- **C** — change `build_ace.sh` to pull the generated files from `js/` as well as `js/src/`.
  **Question for maintainer:** which is the intended design?

---

### 2. WS1: ANTLR output location vs. the location the mode consumes

**Concern:** `generate_js.cmd` passes `-o "js"`, but the mode imports from
`js/src/mode/heddle/`. There is no build step wiring the two together, so the copy drifts
(gap 1 is the symptom).

**Why it matters:** Without a defined mechanism, WS1 is incomplete even when the `.g4`
regen succeeds.

**Evidence:**
- [src/Heddle.Language/generate_js.cmd](src/Heddle.Language/generate_js.cmd) — `-o "js"`.
- [src/Heddle.Language/build_ace.sh](src/Heddle.Language/build_ace.sh) — `cp -R ../../js/src/ ./`.

**Recommended decision:** Document the exact propagation command (or automate it in
`generate_js.cmd`) and add it to the regen procedure so it can't be skipped.

---

### 3. WS1: `Extended` / `Listener` / `ParseContext` wrappers and semantic tokens

**Concern:** `HeddleParserExtended.js` is a thin error-listener wrapper;
`HeddleParserListener.js` is an auto-generated empty listener; `ParseContext.js` only
captures errors — none of them walk the AST to emit editor tokens.

**Why it matters:** If the JS worker is expected to emit semantic Heddle token
classifications (not just parse errors), a new AST visitor is required. The current code
does not do this.

**Evidence:**
- [src/Heddle.Language/js/src/mode/heddle/HeddleParserExtended.js](src/Heddle.Language/js/src/mode/heddle/HeddleParserExtended.js) — minimal wrapper.
- [src/Heddle.Language/js/src/mode/heddle/ParseContext.js](src/Heddle.Language/js/src/mode/heddle/ParseContext.js) — error capture only.

**Recommended decision:** Tie this to the WS5 choice: parse-only baseline needs no change;
semantic classification requires a specified AST visitor mirroring the C# side.

---

### 4. WS2: disambiguating `:` (ternary vs named-arg vs def-base vs prop-delim) in a stateless tokenizer

**Concern:** `heddle_highlight_rules.js` uses stateless regex rules with `onMatch` state
transitions. A bare `/:/` rule cannot know whether it follows `OP_QUESTION` (ternary), an
argument name (named arg), a def name (base), or a prop name.

**Why it matters:** These `:` uses are meant to be classified differently (§4 table). Wrong
classification produces confusing coloring.

**Evidence:**
- [src/Heddle.Language/js/src/mode/heddle_highlight_rules.js](src/Heddle.Language/js/src/mode/heddle_highlight_rules.js) — `heddle-call` state.
- Plan §4 — ternary `:` is `keyword.operator`; named-arg `:` is `punctuation.operator`.

**Recommended decision:** Decide whether to (a) track last-token context in `onMatch`
callbacks (with a concrete example), or (b) accept identical coloring for all `:` uses
(simpler, but diverges from the table). Document the choice.

---

### 5. WS2: function-call detection needs lookahead the tokenizer doesn't expose

**Concern:** "`ID` immediately followed by `(` → `support.function`" implies lookahead. The
Ace tokenizer processes matches sequentially; either a combined `ID(` regex is used (which
swallows the `(` into the ID token) or a post-tokenization pass is required.

**Why it matters:** Without it, all identifiers color identically and function names are not
distinguished.

**Evidence:**
- [src/Heddle.Language/js/src/mode/heddle_highlight_rules.js](src/Heddle.Language/js/src/mode/heddle_highlight_rules.js) — current ID rule has no lookahead.

**Recommended decision:** Choose: (A) leave IDs uniform (drop function coloring in the
regex layer), (B) atomic `ID(` rule with careful token emission, or (C) defer function-name
coloring to the WASM/LSP path (WS5 stretch).

---

### 6. WS5: diagnostics source (baseline vs stretch) is undecided

**Concern:** The plan offers parse-only (baseline) and WASM-backed semantic diagnostics
(stretch) without declaring which is required.

**Why it matters:** Effort, integration points, and acceptance criteria differ greatly.

**Evidence:**
- Plan §5 WS5 — "Decide the diagnostics source".
- [src/Heddle.Demo.Wasm/wwwroot/heddle-demo-worker.js](src/Heddle.Demo.Wasm/wwwroot/heddle-demo-worker.js) — postMessage wiring exists.
- [docs/archive/spec/phase-9-demo-and-integration/wasm-demo.md](docs/archive/spec/phase-9-demo-and-integration/wasm-demo.md) — protocol documented.

**Recommended decision:** Declare parse-only (HED0003) as the required baseline and
semantic HED2xxx–HED5xxx as an explicit stretch. **Question:** is semantic diagnostics in
Ace a must-have for v2?

---

### 7. WS5: WASM service exists — reuse or isolate for Ace annotations?

**Concern:** The phase-9 WASM worker and protocol exist, but the plan doesn't say whether
Ace should reuse that worker for annotations or instantiate its own, nor whether the
service already emits HED codes vs render output only.

**Why it matters:** Avoids redundant or incompatible integration work.

**Evidence:**
- [src/Heddle.Demo.Wasm/DemoProtocol.cs](src/Heddle.Demo.Wasm/DemoProtocol.cs) — request/response DTOs.
- [docs/archive/spec/phase-9-demo-and-integration/wasm-demo.md](docs/archive/spec/phase-9-demo-and-integration/wasm-demo.md) — `Diagnostic` type with HED codes.

**Recommended decision:** If WS5 stretch is chosen, specify reuse-vs-isolate and document
the wire-up into `heddle_worker.js` / `session.setAnnotations`.

---

### 8. WS6: sourcing function completions from the registry isn't reachable from the browser

**Concern:** The C# `FunctionRegistry` (built-ins such as `range`, `upper`, `trim`, `len`,
`format`, …) has no runtime bridge to the static JS completer.

**Why it matters:** Hard-coding creates a maintenance coupling (add in C# → update JS by
hand); deferring leaves the feature incomplete.

**Evidence:**
- [src/Heddle/Runtime/Expressions/FunctionRegistry.cs](src/Heddle/Runtime/Expressions/FunctionRegistry.cs) — default built-ins.
- [src/Heddle.Language/js/src/mode/heddle_completions.js](src/Heddle.Language/js/src/mode/heddle_completions.js) — static list, no functions.

**Recommended decision:** Baseline = hard-code the defaults and note the maintenance burden;
stretch = expose the registry through the WASM API and query it.

---

### 9. WS2: new `heddle-def-props` state — entry/exit conditions unspecified

**Concern:** The current `heddle-def` state has no `(` rule, so the plan must specify which
rule pushes `heddle-def-props`, what pops it (`)` at which depth), and how it coexists with
the existing `heddle-generic-type` state (`<` `>` in `List<string>`).

**Why it matters:** Missing entry/exit logic makes the state machine skip or hang on prop
lists.

**Evidence:**
- [src/Heddle.Language/js/src/mode/heddle_highlight_rules.js](src/Heddle.Language/js/src/mode/heddle_highlight_rules.js) — `heddle-def` state has no `(` handling.

**Recommended decision:** Provide the concrete push rule, the full `heddle-def-props` rule
set (name, `:`, type, `=`, literals, `,`), and the pop rule.

---

### 10. WS3: existing "Ace: …" cross-references in the grammar need auditing

**Concern:** `heddle.tmLanguage.json` carries `"comment": "Ace: …"` notes mapping scopes to
Ace token classes. Some may be stale relative to the current highlight rules.

**Why it matters:** Stale references break the auditable mirror relationship and prevent
verifying coloring parity.

**Evidence:**
- [docs/coloring-scheme/heddle.tmLanguage.json](docs/coloring-scheme/heddle.tmLanguage.json) — Ace comments.
- [src/Heddle.Language/js/src/mode/heddle_highlight_rules.js](src/Heddle.Language/js/src/mode/heddle_highlight_rules.js) — emitted classes.

**Recommended decision:** Audit and correct all existing Ace comments before finalizing the
§4 table; add this audit to WS9.

---

### 11. WS10: drift guard is unspecified

**Concern:** The plan asks for a check that flags divergence between the `.g4` token
vocabulary and the JS `symbolicNames`, but doesn't say what runs it, how it compares, where
it lives, or what failure looks like.

**Why it matters:** Without automation, the exact drift that caused this whole effort
(gap 1) recurs silently.

**Evidence:**
- Plan §5 WS10 item 4. No such script exists in the repo today.

**Recommended decision:** Specify the implementation — e.g. a script that extracts the
`tokens { … }` block from `HeddleLexer.g4`, extracts `symbolicNames` from the generated JS,
compares them, and fails CI on mismatch; document it in [docs/building.md](docs/building.md).

---

### 12. WS4: beautify option schema not scoped

**Concern:** `ext/beautify.js` is a generic HTML beautifier with no Heddle-specific option
schema, yet the plan mentions "optional line breaks for long lists via `formatOptions`".

**Why it matters:** The accepted parameters and their persistence across invocations are
undefined.

**Recommended decision:** Specify the `formatOptions` schema (e.g.
`{ maxLineLength, breakLongPropLists }`) and how `beautify` reads/respects it.

---

## Confusing / risky areas

All four are **resolved** by the decisions in the plan's §0 and testing scope; each is
annotated below.

- **Overlapping `:` operators.** Four roles (def base, prop delim, named arg, ternary) all
  land on `:` and are classified differently in §4, but a stateless regex tokenizer could
  not separate them without context tracking.
  **Resolution (D-A):** the tokenizer was extended to support multiple modes safely, so
  each `:` is classified within its own mode — no shared-state ambiguity remains. WS2/WS9
  add token-stream fixtures that pin each `:` role to its expected Ace class so a
  regression is caught in the loop.
- **Ace version pinning.** [build_ace.sh](src/Heddle.Language/build_ace.sh) hardcodes
  `--branch v1.32.6` with no fallback/update procedure.
  **Resolution (D-C):** the pin is **intentionally kept**; an Ace upgrade is a separate,
  later effort and is listed as out of scope (plan §9). No action in this plan; the risk is
  accepted and documented.
- **No `.g4`↔JS sync enforcement.** After editing `.g4`, the C# (`generated/`) and JS
  (`js/`) outputs lived in separate trees with no CI check that both were regenerated.
  **Resolution (D-D):** codegen + propagation is now a build step and **CI fails on any
  diff** (plan WS1/WS10) — this single enforced gate is the drift guard. WS9 mirrors the
  C# corpora as JS fixtures so front-end divergence also fails the loop.
- **Vague "parity" definition (WS9).** "Equivalent coloring" between Ace and VS Code was
  undefined.
  **Resolution (D-B):** success is redefined as **every v2 token recognized and correctly
  classified in Ace** (a coverage assertion, plan §7/§8), *not* pixel/visual parity. VS
  Code is a separate, deferred track (WS3). The ambiguous criterion is removed.

---

## Verified-correct claims

Confirmed accurate by code inspection:

1. The v2 `.g4` files exist and are final ([HeddleLexer.g4](src/Heddle.Language/HeddleLexer.g4), [HeddleParser.g4](src/Heddle.Language/HeddleParser.g4)).
2. The C# engine is v2-complete (tokens, rules, extensions merged).
3. The JS grammar **root** output is already regenerated to v2 (`js/HeddleLexer.js` has the v2 tokens + `DEF_PROPS`).
4. The Ace **mode copy** is stale (`js/src/mode/heddle/HeddleLexer.js` is v1.x). *(Refines the plan's premise — see gap 1.)*
5. The §4 token→scope table matches the grammar specs for literals/operators/`this` ([phase-1 grammar](docs/archive/spec/phase-1-native-expressions/grammar.md), [phase-5 grammar](docs/archive/spec/phase-5-props-and-slots/grammar.md)).
6. The WASM demo infrastructure exists ([src/Heddle.Demo.Wasm](src/Heddle.Demo.Wasm)).
7. VS Code grammar mirrors Ace via one-way sync ([copy-grammar.js](editors/vscode/build/copy-grammar.js)).
8. Specs/roadmap are contributor-only, not published ([spec README publication status](docs/archive/spec/README.md)).
9. `docs/public/ace/` build outputs are gitignored.
10. The `FunctionRegistry` exists and is extensible ([FunctionRegistry.cs](src/Heddle/Runtime/Expressions/FunctionRegistry.cs)).

> **Most consequential correction:** WS1 is a **propagation/copy** problem (root JS grammar
> is already v2; the mode's copy and the Ace build path are stale), not a from-scratch
> regeneration. Re-scope WS1 accordingly.
