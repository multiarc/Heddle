# Phase 3 — html-context-encoding-lint (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-3-html-context-encoding-lint.md](../../plan/phase-3-html-context-encoding-lint.md) (governed by [standing rulings](../../plan/common/standing-rulings.md) R3/R4/R5 and [open questions](../../plan/open-questions.md) P3-Q1..Q5)
- **Assumes merged:** nothing new in this initiative. Builds only on **already-shipped 2.0** surface — the output-profile machinery (`OutputProfile.Html`, `@profile()`), the `@attr`/`@js`/`@url` encoders, and the `@raw` opt-out. No prior post-2.0 phase is a dependency (this phase is independently shippable per the plan's ordering rationale).

| Document | Purpose |
|---|---|
| this document | The complete implementation of the phase 3 HTML-context encoding lint (`HED2004`): the explicit-profile gate, the bare-`@(value)` trigger predicate, the local adjacent-literal heuristic scan (attribute / `<script>` / URL), the diagnostic surface, and the test corpus. |

## Scope and goal

Under an **explicitly declared HTML output profile**, emit one warning-tier diagnostic (`HED2004`) when a bare `@(value)` output sits in an HTML **attribute value**, a **`<script>` block**, or a **URL component** position without the matching context encoder (`@attr` / `@js` / `@url`), and point the author at that encoder. The lint is a compile-time scan of the literal text adjacent to each output block (skipping the source spans of sibling interpolations, whose rendered values are unknown at compile time). It is a lint, not a fix: it never selects, applies, or rewrites an encoder, and it changes **zero rendered bytes**.

**In scope (this phase):**
- The explicit-context gate: the lint fires **only** under `OutputProfile.Html` / `@profile(html)`, never under `Text`/CSV/byte output, and never inferred from content (ruling R3).
- Three covered positions, all on the same gate and substrate: attribute value (primary), `<script>` content (fast-follow), URL component (fast-follow) — P3-Q1 resolves all three into this phase. **P3-Q1 is closed by maintainer decision (2026-07-15): keep full attribute + `<script>` + URL coverage; the plan's "(User may narrow to attribute-only)" lean is resolved *toward* full coverage, not narrowed.** The `<script>` heuristic's earlier wrong-encoder defect (an interior `<` such as `i < n` mis-routing to `@attr`) is fixed in [D3](#d3--the-local-adjacent-literal-heuristic-scan-and-its-exact-context-classification-closes-p3-q1-defines-the-accepted-false-positivesnegatives) (Step 1 anchors on a closed `<script …>` start tag), so the script context ships correctly.
- One new diagnostic id `HED2004`, warning severity, no escalation knob (P3-Q4/Q5).

**Out of scope (permanent or deferred, see [Deferred items](#deferred-items)):**
- Auto-escaping / auto-selecting an encoder, and any HTML parsing / tokenizer / tag-nesting model (ruling R4 — permanent).
- Transitive / cross-call context propagation — the lint is **single-hop** by construction (a definition body's `@(value)` whose output lands in an attribute at a call site is a documented false negative).
- CSS-context encoding, and any behavior under a non-`Html` profile (inert by construction).
- A build-time source-generator twin of the diagnostic (the generator ports only byte-affecting logic; see [D6](#d6--diagnostic-surface-runtime-compiler--editor-no-generator-twin)).

## Assumed state

Re-verified against current source (not trusted from the plan). File paths and members below are the seams this spec binds to.

| Seam | Verified state (path · member) |
|---|---|
| Effective output profile at compile time | `src/Heddle/Runtime/CompileContext.cs` · `CompileContext.OutputProfile { get; set; }` — an `OutputProfile` initialized from `TemplateOptions.OutputProfile` in every root ctor, **snapshotted** into child contexts by the private copy ctor, and **flipped by `@profile()`** in document order. Confirmed the flip site: `src/Heddle/Extensions/ProfileExtension.cs` · `InitStart` sets `context.OutputProfile = profile` while the directive item compiles. Consequence: the effective profile is correct **per block only during the document-ordered compile loop**, not before it. This is the same value the existing `HED2002`/`HED2003` gates read. |
| The document-ordered compile loop + adjacent-literal substrate | `src/Heddle/Runtime/HeddleCompiler.cs` · `CompileBody` — `foreach (var extensions in parseContext.OutputChains)` iterates output blocks in document order with the mutated `string workingDocument` in scope. Each `extensions` is an `OutputChain` exposing `BlockPosition` (working-document coordinates, kept consistent with `workingDocument` by `ShiftBySkippedTokens`/`ReplaceRawOutput`/`ProcessBranchSets`/`RemoveEmptyItem`) and `Chain` (`List<OutputItem>`). The producing branch (`returnTypeChainedPrevious != null`) adds the element and performs no further mutation of the current block. **Substrate detail (verified):** a **producing** bare `@(value)` block keeps its `@(…)` source span physically in `workingDocument` — `RemoveEmptyItem` (`HeddleCompiler.cs:394`) elides only *zero-output* blocks, and `RuntimeDocument.GetDocumentPieces` (`src/Heddle/Runtime/RuntimeDocument.cs:105,119`) *skips* each producing block's `BlockPosition` span at render rather than deleting it. So `workingDocument` left of a candidate still contains earlier siblings' un-elided `@(…)` directive source; the D3 left-scan therefore must **skip other producing output blocks' `BlockPosition` spans** (unresolved interpolation source, not HTML literal) to keep its "adjacent literal" characterization accurate. |
| The precedent substrate access | `src/Heddle/Runtime/HeddleCompiler.cs` · `CollectGap` reads `workingDocument.Substring(gapStart, gapLength)` around a block's `BlockPosition` with defensive bounds (`gapStart < 0 || gapStart + gapLength > workingDocument.Length` → return). `ProcessBranchSets` is a scan over `parseContext.OutputChains` that emits positioned warnings (`HED3001`) using `nextLeftmost.Position` for reporting while indexing `workingDocument` by `BlockPosition` — the exact two-coordinate pattern this lint reuses. |
| Bare-`@(value)` recognition | `src/Heddle/Runtime/HeddleCompiler.cs` · `UnnamedCarrierName` and `WarnOnRedundantEncoding` recognize the bodiless unnamed carrier as `item.ExtensionName.Length == 0 && string.IsNullOrEmpty(item.ParameterTemplate)`; under `OutputProfile.Html` it resolves to the `html` encoding leaf (element-text encode). `OutputItem.ExtensionName` is `""` for `@(...)` and non-empty (`"raw"`, `"attr"`, `"js"`, `"url"`, `"html"`, `"string"`) for the named forms — confirmed `src/Heddle/Language/OutputItem.cs` and the `[ExtensionName("")]`/`[ExtensionName("raw")]` pair on `src/Heddle/Extensions/EmptyExtension.cs`. |
| Encoders the lint recommends (already shipped) | `src/Heddle/Extensions/AttrExtension.cs` (`@attr`, `[EncodeOutput]`, attribute escape), `src/Heddle/Extensions/JsExtension.cs` (`@js`, raw leaf, JS-string escape), `src/Heddle/Extensions/UrlExtension.cs` (`@url`, `Uri.EscapeDataString` component escape). `@raw` is the second name on `EmptyExtension` (verbatim opt-out). Documented at [built-in-extensions.md → Encoding contexts](../../built-in-extensions.md#encoding-contexts). |
| Diagnostic-id surface | `src/Heddle/Data/HeddleDiagnosticIds.cs` — highest `HED2xxx` is `RedundantEncodingExtension = "HED2003"`; **`HED2004` is next-free**. One-to-one pinned by `src/Heddle.Tests/DiagnosticIdTests.cs` · `ConstantsMatchTheDiagnosticsTableOneToOne` (the `expected` set lists `HED2001`..`HED2003` and must gain `HED2004`). Registry: [cross-cutting-decisions.md → claimed diagnostic IDs](../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) — the dedicated `HED2004` row (owned by this phase) is **already present** at `cross-cutting-decisions.md:157` (added during spec authoring; the file is git-modified), so WI1's registry step is idempotent/pre-applied, not fresh drift; `HED2001`–`HED2003` remain the built-in-extensions rows. |
| Editor surface (surfacing free; classification fidelity bounded) | `src/Heddle.LanguageServices/DocumentAnalyzer.cs` calls `HeddleCompiler.Compile(...)` directly and iterates `compileContext.CompileWarnings` (each carries `DiagnosticId`) — so a warning added in `CompileBody` **surfaces** in the editor with no extra work, and its `Position` (`leftmost.Position`, original-source, D7) is correct there. **Fidelity caveat (verified):** `DocumentAnalyzer.cs:45` passes the **original** `text` into `Compile`, whereas the runtime (`HeddleTemplate.cs:326`) passes the hidden-token-excised `optimizedDocument`; since `ShiftBySkippedTokens` rebases `BlockPosition`s to the clean document, the D3 left-scan (which indexes `workingDocument` by `BlockPosition.StartIndex`) reads slightly skewed text in the editor whenever comments / `@\` precede a block — the same pre-existing skew shared by `HED3001`/`HED2003`. So the editor may show an occasional spurious or missing `HED2004` under comment-heavy markup that the runtime `CompileResult` classifies correctly. **Surfacing is free; classification fidelity under comments/`@\` is best-effort** (warning-tier, editor-only). Most-reversibly closed by reconciling the two `Compile` callers (editor also passing the clean document) — a `DocumentAnalyzer.cs` change (shared source, outside this spec) flagged to the orchestrator, not undertaken here. |
| Generator surface (no twin needed) | `src/Heddle.Generator/Emit/DocumentShaper.cs` doc-comment: it ports **"only the byte-affecting half of `HeddleCompiler.ProcessBranchSets` — the adjacency strip"**, not the warnings. `HED3001`/`HED2003` have no generator twin; the generator owns the `HED7xxx` family. `HED2004` follows the same rule. |

**Plan corrections:** none. Every load-bearing plan claim (substrate exists at zero render cost; `HED2004` free; the profile is the sole compile-time context notion) is confirmed against source above.

## Design decisions

Every P3 open question, lean, and pin appears here as a CLOSED decision.

### D1 — The `Html` profile is the sole gate; checked at per-block compile time (closes P3-Q3, implements R3)

- **Decision.** The lint fires **only** when `compileScope.CompileContext.OutputProfile == OutputProfile.Html` **at the moment the bare `@(value)` block is compiled** in `CompileBody`'s document-ordered `OutputChains` loop. It is never active under `OutputProfile.Text` or any non-`Html` profile, and context is **never** inferred from the literal content — the `<a title="@(X)">`-shaped string under `Text` produces nothing. The gate is the effective profile *for that block*, so a mid-document `@profile(html)`/`@profile(text)` flip is honored exactly as it is for `HED2002`/`HED2003` (profile applies to output **after** the directive in document order).
- **Rationale.** Heddle has exactly one notion of output context — the document-scoped `OutputProfile` (verified `CompileContext.OutputProfile`); there is no positional HTML context in the engine. Reading the profile at compile-loop time (not before) is required for correctness because `@profile()` flips it during the loop (`ProfileExtension.InitStart`); a pre-loop scan would read a stale profile. This reuses the proven per-block gate of the two existing profile diagnostics.
- **Alternatives rejected.** *Content sniffing / heuristic "looks like HTML"* — violates R3 (a `<a title="@(X)">` literal under `Text` may be CSV, a code generator, or documentation *about* HTML). *A pre-compile-loop scan pass (like a second `ProcessBranchSets`)* — would read `CompileContext.OutputProfile` before any `@profile()` flip and misclassify every template that flips mid-document; rejected for that correctness gap even though the substrate is equally reachable there. *A new richer explicit-context signal (editor hint distinct from the profile)* — deferred as a future seam (plan §Design direction); the profile is the sole gate this phase.
- **Grounding.** `src/Heddle/Extensions/ProfileExtension.cs` · `InitStart`; `src/Heddle/Runtime/HeddleCompiler.cs` · `WarnOnRedundantEncoding` (`if (compileScope.CompileContext.OutputProfile != OutputProfile.Html) return;`); R3 in [standing-rulings.md](../../plan/common/standing-rulings.md).

### D2 — Trigger predicate: a bodiless unnamed carrier (bare `@(value)`) is the only shape that can warn

- **Decision.** A given output chain is a **lint candidate** iff its leftmost item `leftmost = chain.Chain[0]` satisfies **both**:
  1. `leftmost.ExtensionName.Length == 0` (the unnamed `@(...)` form — the sink that element-text-encodes under `Html`), and
  2. `string.IsNullOrEmpty(leftmost.ParameterTemplate)` (**bodiless** — no `{{ … }}` body).

  Any output already using an encoder or opt-out — `@attr(X)`, `@js(X)`, `@url(X)`, `@raw(X)`, `@html(X)`, `@string(X)` — has a **non-empty** `ExtensionName` and is therefore **never** a candidate. A **bodied** `@(value){{…}}` (non-empty `ParameterTemplate`) is a raw rescoping container (its markup body is authored, not element-text-encoded) and is **never** a candidate. The check is on the parse-level `ExtensionName`, which stays `""` for `@(...)` even though the compiler later resolves the carrier name to `"html"` under `Html` — so bare `@()` is cleanly distinguished from an explicit `@html()`. Candidacy is judged strictly on the **leftmost** item `chain.Chain[0]` (document-leftmost, because `CompileBody` compiles each chain via `.Reverse()`): a **chained** carrier whose leftmost item is a bare `@(...)` — e.g. `@(value):upper()` — **is** a candidate, because that leftmost sink still element-text-encodes under `Html`; switching `Chain[0]` to a named encoder or `@raw` removes candidacy. A chain whose leftmost item is already a named encoder/opt-out is never a candidate.
- **Rationale.** This is the exact shape (`ExtensionName.Length == 0 && string.IsNullOrEmpty(ParameterTemplate)`) the compiler already uses in `UnnamedCarrierName`/`WarnOnRedundantEncoding` to identify the sink that auto-encodes for element text under `Html`. It is precisely the population that "silently element-text-encodes in a non-element context," and it makes every silencing path (D4) free: switching the site to any named encoder or `@raw` removes candidacy by construction, satisfying the plan's "Silenceable" success criterion with no extra code.
- **Alternatives rejected.** *Warn on any output chain regardless of encoder* — would fire on `@attr(X)` etc., defeating the whole purpose and breaking the "no warn when the correct encoder is used" criteria. *Inspect the resolved carrier name (`"html"`) instead of the parse `ExtensionName`* — would conflate bare `@()` with explicit `@html()`, which the author chose deliberately; `@html()` in an attribute is the author's call and out of this lint's remit (a bare `@()` is the "no signal at all" case the assessment names).
- **Grounding.** `src/Heddle/Runtime/HeddleCompiler.cs` · `UnnamedCarrierName`, `WarnOnRedundantEncoding`; `src/Heddle/Language/OutputItem.cs`; `src/Heddle/Extensions/EmptyExtension.cs`.

### D3 — The local adjacent-literal heuristic scan and its exact context classification (closes P3-Q1; defines the accepted false positives/negatives)

- **Decision.** For a lint candidate (D2) under the `Html` gate (D1), classify the block's position by a **left-only** heuristic scan over `workingDocument`, indexed by the block's working-document coordinates `blockStart = extensions.BlockPosition.StartIndex`. The scan reads `workingDocument` characters to the left of `blockStart`, **skipping the source span of every other producing output block to the left** (their `BlockPosition` spans, current coordinates): those spans are unresolved interpolations whose rendered value is unknown at compile time, so their source characters are **not** HTML literal. As the scan walks left and index `i` enters another block's `[StartIndex, StartIndex + Length)` span, it resets `i` to that span's `StartIndex - 1` and continues — so a `<`, `>`, or quote that appears **inside a sibling `@(…)` source** (e.g. the `>` in `data-n="@(count > 0)@(label)"`) never skews the classification. Call the resulting literal-only left text `L` (workingDocument left of `blockStart` with all other output-block source spans excised). The scan performs **no** HTML parsing, builds no tag model, and never looks across a call boundary. The classifier returns one of `{ Attribute, Script, Url, None }`; `None` emits nothing.

  **Bounds discipline (defensive, mirrors `CollectGap`/`WidenToWholeLine`).** If `blockStart < 0` or `blockStart > workingDocument.Length`, return `None` (a shifted position overshot; never dereference past the end).

  **Step 1 — `<script>`-element containment (checked first; anchored on a real start tag, never a bare `<`).** Over `L`, let open = the last case-insensitive index of "<script" such that (a) the character immediately after the matched "<script" is a tag-name boundary — whitespace, '/', or '>' (`[\s/>]`) — and (b) an **unquoted** '>' occurs after that match and before `blockStart` — determined by scanning rightward from the end of the matched `<script` toward `blockStart`, tracking quoted-attribute-value state (a `"` or `'` seen outside a quote opens a quoted value; only the matching quote closes it; characters inside a skipped sibling span are ignored) and taking the first '>' seen **outside** any quoted value as the start tag's real close — so a '>' inside a quoted attribute value such as `src="a>b"` does **not** satisfy (b), and let close = the last case-insensitive index of "</script" before `blockStart` whose immediately-following character is a tag-name boundary — whitespace, '/', '>', or the end of `L` (the same `[\s/>]`-or-EOF boundary check (a) applies to `open`, so a `</scriptx>`/`</scripting>` does not over-match as an end tag). (`<script`/`</script` occurrences that fall inside a skipped sibling span do not count.) If `open` exists and (`close` does not exist **or** `close < open`) → return **`Script`** (suggest `@js`). Anchoring on a **closed `<script …>` start tag** (not on a bare `<`) is what keeps an interior `<`/`>` in the script body — the ubiquitous `for (var i = 0; i < n; i++)`, a comparison, a generic `List<int>` — as script text: it stays **`Script`** and never mis-routes to `Attribute`. When the block sits inside the `<script …>` **start tag itself** (e.g. `<script src="@(X)">`), condition (b) fails — no **unquoted** `>` has occurred before the block — so Step 1 does not fire and the block falls through to Steps 2/3, classifying as the `src` attribute value. This holds even when an *earlier* attribute value of the same start tag contains a literal `>` (e.g. `<script src="a>@(X)">` or `<script data-x="a>b" src="/p?q=@(X)">`): the quote-aware scan skips the `>` inside `src="a>`/`data-x="a>b"` as inside a quoted value, so (b) still fails and the block classifies as the `src` attribute (or, for `/p?q=@(X)`, `Url`) — never mis-routed to `Script`/`@js`. Requirement (a) also distinguishes a real `<script>`/`<script src=…>` from a custom element whose name merely starts with “script” (`<script-loader>`, `<scriptlet>`), which does not match `open`.

  **Step 2 — nearest tag boundary (element-text vs inside-a-tag).** When Step 1 did not return, scan `i` over `L` from the nearest literal character left of `blockStart` down to the start of `L`:
  - the first `>` encountered → the block is in **element text**; return **`None`** (the default element-text encoder is correct; no warning).
  - the first `<` encountered → the block is inside an (unclosed) tag; set `tagStart = i`, go to Step 3.
  - reaching the start of `L` with neither → **element text**; return **`None`** (plain text before any tag).

  **Step 3 — inside a tag → `Attribute` or `Url`.** Let `T = L` from `tagStart` up to `blockStart` (the tag text from `<` to the block, sibling spans already excised). Determine the enclosing quoted attribute value by **quote parity** over `T`:
  - if `CountChar(T, '"')` is odd → open double-quoted value, `q = T.LastIndexOf('"')`;
  - else if `CountChar(T, '\'')` is odd → open single-quoted value, `q = T.LastIndexOf('\'')`;
  - else → **not in a quoted value** (unquoted position or between attributes): return **`Attribute`** (generic suggestion `@attr`).

  When in a quoted value, let `valueSoFar = T.Substring(q + 1)` and derive `attrName` by walking left from `q`: skip whitespace, require `=` (if the char before the whitespace run is not `=`, return `Attribute`), skip whitespace, read the identifier run `[A-Za-z0-9:_-]` — that is `attrName` (compared **ordinal-case-insensitively**). Return **`Url`** iff `attrName ∈ URL_ATTRS` **and** `valueSoFar` contains any of `'?'`, `'&'`, `'='` **or** ends with `'/'` (a query-component or path-segment position); otherwise return **`Attribute`**.

  `URL_ATTRS` (fixed, documented set): `href`, `src`, `action`, `formaction`, `cite`, `poster`, `background`, `manifest`, `data`, `longdesc`, `usemap`, `srcset`.

- **Accepted false positives (documented limits).**
  - **HTML comment `<!-- @(X) -->`.** Step 1 finds no closed `<script …>`; Step 2's nearest boundary is the comment's `<` with no intervening `>` → in-tag; Step 3 finds no open quote → returns `Attribute`. The lint **fires** (suggesting `@attr`) though the output is inert inside a comment. This is the canonical documented false positive — the scan cannot distinguish a comment from an open tag without an HTML model (R4).
  - **Stray literal `<` in text**, e.g. `a < b @(X)` on the same line with no later `>` — same mechanism: Step 2 finds the `<` → in-tag; Step 3 finds no quote → `Attribute`. Rare; documented.
  - **A `<script…>`-shaped literal inside an attribute value or a raw-text element (Step 1 `Script` false positives).** Because Step 1 does no tag-name / attribute / raw-text-element model (R4), these are accepted, documented limits:
    - `<p data-x="<script>">@(X)</p>` — the `<script>` sits inside an attribute value, but Step 1 sees an unclosed `<script …>` before the element-text `@(X)` and returns **`Script`** (suggests `@js`) though the real context is element text under `<p>`.
    - `<textarea><script></textarea>@(X)` / `<title><script></title>@(X)` — inside a raw-text/RCDATA element the inner `<script>` is inert text, but Step 1 still sees an unclosed `<script` with no `</script` and returns **`Script`**.
    - `<!-- <script> --> <p>@(X)</p>` (or a `<script` mention inside `<![CDATA[ … ]]>`) — an **unmatched** `<script` inside an HTML comment or CDATA section satisfies Step 1 (the comment/tag's own `>` closes the start tag; no `</script` follows) and returns **`Script`** though the real context is element text. Because `L` spans the *entire* working document left of the block and Step 1 takes the **last** `<script`, a single unmatched `<script>` mention upstream (in a comment, a CDATA block, or instructional prose) routes **every** subsequent element-text `@(value)` to `Script` until a `</script` literal appears — a **document-spanning** reach. (A *fully* commented `<!-- <script></script> -->` is correctly `None`: the commented `</script` gives `close > open`.)
    In each of these the lint over-fires to `Script` rather than `None`/element-text. Warning-tier and silenced per-site by the encoder or `@raw` (D4); enumerated here and in the WI4 user doc as the accepted precision cost of the model-free `<script>` heuristic.
- **Attribute-branch imprecision (documented limits).** The `Attribute` result covers every in-tag position that is not a recognized quoted value, so its message is phrased “inside an HTML tag (attribute value or an unquoted/name position)” and does **not** claim “attribute value” specifically:
  - **Attribute-name and tag-name injection.** `<div @(Attrs)>` (attribute-name position) and `<@(Tag)>` (tag-name position) both classify as `Attribute` (Step 2 finds `<` with no intervening `>` → in-tag; Step 3 finds no open quote → `Attribute`). `@attr` is not a *correct* encoder for a name/tag position — no shipped encoder is — so the suggestion is the **generic in-tag signal**, emitted deliberately per the [D4](#d4--conservative--high-precision-posture-silence-via-the-explicit-encoder-or-raw-closes-p3-q2) posture (the security signal “this bare `@()` is inside a tag, not element text” is valuable, `@attr` is a safe non-corrupting suggestion, and `@raw` silences a deliberate one). Documented, not routed separately.
  - **Mixed-quote poisoning.** `<a title='it"s' href="/s?q=@(X)">` — the naive quote-parity of Step 3 counts a `"` inside the single-quoted `title` value, so the double-quote count over the tag prefix is even and the `href` value is not recognized as an open double-quoted value; the block degrades to a generic `Attribute` instead of `Url`. Under-classification (never over-classification to a wrong encoder), silenceable per-site; documented as the accepted cost of parity-based (model-free) quote tracking.
- **Accepted false negatives (documented limits).**
  - **Single-hop / cross-call.** A definition body `{{ @(X) }}` invoked as `<a title="@article_title()">` produces **no** warning at the definition — the body's `@(X)` has no attribute literal adjacent *in its own body*. The lint sees only literals physically adjacent in the block's own compiled body (R3/plan non-goals). Stated as a hard limit in the diagnostic docs.
  - **Unquoted attribute value** (`<a href=@(X)>`) is treated as `Attribute` (a true positive, quote-agnostic) — but a value split by whitespace or exotic quoting the parity test cannot follow may be missed. Conservative by design (D4).
- **Rationale.** "Inside an unclosed `<`" is the cheapest high-signal attribute heuristic and is exactly what makes the plan-mandated comment a documented false positive while keeping normal well-formed markup (every tag closed before the block → Step 2 hits `>` → element text) a clean negative. Checking `<script>` containment **first** (Step 1), anchored on a closed `<script …>` start tag rather than a bare `<`, is what keeps a genuine JavaScript context correct when the script body contains an interior `<`/`>` (comparisons, `for` loops, generics) — the common case for real scripts — instead of mis-routing it to `@attr`. Left-only scanning matches the `CollectGap` substrate and adds nothing to the render path. The URL refinement keys on a small fixed attribute set **plus** a component-position signal so that a whole-URL value (`href="@(X)"`, empty `valueSoFar`) degrades to `@attr` rather than mis-suggesting `@url` (which percent-encodes a single component and would corrupt a whole URL).
- **Alternatives rejected.** *A real HTML tokenizer / quote-balancer / tag-nesting model (Go `html/template` style)* — the gold standard, but it is a large non-additive change needing the HTML model Heddle lacks, and the monomorphization model would force per-context type-flow threading through every call shape (plan §Design direction); rejected as out of scope (R4). *Requiring a quoted value for any attribute warning* (higher precision) — would **not** fire on `<!-- @(X) -->`, contradicting the plan's mandated documented false positive; rejected. *Two-sided (left+right) scanning* — unnecessary for any covered position and adds cost/complexity; left-only suffices.
- **Grounding.** `src/Heddle/Runtime/HeddleCompiler.cs` · `CollectGap`/`ApplyGaps` (substrate + bounds pattern); plan [Validation scenarios](../../plan/phase-3-html-context-encoding-lint.md) rows for the comment and single-hop cases; [built-in-extensions.md → Encoding contexts](../../built-in-extensions.md#encoding-contexts) for the encoder-per-context table.

### D4 — Conservative / high-precision posture; silence via the explicit encoder or `@raw` (closes P3-Q2)

- **Decision.** The heuristic never suggests an encoder whose application would **mis-encode** — it never routes a position to a context encoder that would corrupt the output (e.g. it degrades a whole-URL value to `@attr` rather than mis-suggesting `@url`, which would percent-encode a single component and break the URL — D3). For a position it **cannot place at all** — element text, out of range, or no tag/script context — it returns `None` (no warning) rather than guessing; the lint favors **recall loss over precision loss** there. Where it **can** confidently place the position as *inside a tag* but cannot resolve the exact sub-context to a strictly-correct encoder (the attribute-name and tag-name positions of D3, where no shipped encoder is strictly correct), it deliberately emits the **generic in-tag signal** (`Attribute`/`@attr`) rather than `None`: “this bare `@()` is inside a tag, not element text” is itself the high-value security signal, `@attr` is a safe (non-corrupting) suggestion, and `@raw` silences a deliberate one. That is a **disclosed, bounded best-effort**, not an unconstrained guess — so D3's `Attribute`-for-name/tag result and this posture agree: the only non-`None` results the lint emits are either the correct context encoder or the safe generic in-tag signal, never a wrong-and-corrupting encoder. (The `<script>`-body case — an interior `<` such as `i < n` — is **not** an exception to this: D3 Step 1 classifies it `Script` correctly, anchored on the closed `<script …>` start tag. Nor is a literal `>` inside an earlier quoted attribute value of a `<script …>` start tag: D3 Step 1 condition (b)'s quote-aware scan skips it, so a block in a later attribute of that same start tag stays `Attribute`/`Url`, never `@js`.) An author silences any single flagged site by switching the bare `@(value)` to the matching encoder (`@attr`/`@js`/`@url`) — which also fixes the actual encoding — or to `@raw(value)` when the value is trusted; both remove candidacy per D2 with no suppression syntax, no pragma, and no options knob.
- **Rationale.** A lint authors distrust is worse than no lint; a conservative default plus a zero-ceremony, security-correct silencing path (the encoder) keeps the signal high-value and bounds the "warnings-as-errors build" blast radius.
- **Alternatives rejected.** *An inline suppression comment / pragma* — new surface for a warning-only lint; YAGNI, and `@raw`/the encoder already silence per-site. *A severity/enable options knob* — P3-Q5 fixes severity at warning with no escalation this phase; a knob is speculative configuration (coding-standards YAGNI).
- **Grounding.** Plan [Risks & mitigations](../../plan/phase-3-html-context-encoding-lint.md) row 1; D2 candidacy.

### D5 — Diagnostic `HED2004`: id, per-context message, fixed warning severity (closes P3-Q4, P3-Q5)

- **Decision.** Allocate **`HED2004`** (constant `MissingContextEncoder` on `HeddleDiagnosticIds`), warning severity, **no** escalation knob. One id covers all three positions; the **message names the detected context and suggests the specific encoder**, and the `Fix` names both the encoder and the `@raw` opt-out. Exact texts in [Diagnostics](#diagnostics--error-surface). Positioned at the output block (D7).
- **Rationale.** Next-free id in the `HED2xxx` output-profiles/encoding family it belongs to; a single id with a context-specific message keeps the surface small while giving the author the precise remedy (`@attr`/`@js`/`@url`). Warning severity honors R5 — it never blocks compilation.
- **Alternatives rejected.** *Three distinct ids (one per context)* — the plan pins one id (`HED2004`); three would multiply registry/test churn for a distinction the message already carries. *Error severity or an escalate-to-error option* — breaks R5 and P3-Q5; a new warning on valid-today templates must be warning-tier.
- **Grounding.** `src/Heddle/Data/HeddleDiagnosticIds.cs`; `src/Heddle.Tests/DiagnosticIdTests.cs`; registry [D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx).

### D6 — Diagnostic surface: runtime compiler + editor, no generator twin

- **Decision.** `HED2004` is emitted **once**, from `HeddleCompiler.CompileBody` (the runtime compile pass). It surfaces automatically in the **editor** because `DocumentAnalyzer` runs `HeddleCompiler.Compile` and forwards `CompileWarnings`. It is **not** reproduced in the build-time source generator; the generator's `DocumentShaper` deliberately ports only the byte-affecting half of the compile logic, and warning-only diagnostics (`HED3001`, `HED2003`) have no generator twin.
- **Rationale.** Single source of truth for the lint; the byte-neutral lint changes no emitted code, so the precompiled path has nothing to diverge on. Matches the established split (`HED7xxx` = generator; `HED2xxx`/`HED3xxx` compile lints = runtime/editor).
- **Alternatives rejected.** *A `HED70xx` generator twin* — pure duplication of a byte-neutral warning; the generator emits identical bytes with or without it, and the editor already covers authoring-time feedback.
- **Grounding.** `src/Heddle.Generator/Emit/DocumentShaper.cs` (doc-comment: "only the byte-affecting half"); `src/Heddle.LanguageServices/DocumentAnalyzer.cs`.

### D7 — Position semantics and the two coordinate systems

- **Decision.** The diagnostic `Position` is `leftmost.Position` (the `OutputItem` position of the `@(...)` block) — **original-source coordinates**, correct for source mapping in the compiler and editor. The **scan** indexes `workingDocument` by `extensions.BlockPosition.StartIndex` — **working-document coordinates**, kept consistent with `workingDocument` by the compiler's shift routines. The two are used strictly for their own purpose and never mixed.
- **Rationale.** This is exactly the split `CollectGap` uses (`nextLeftmost.Position` to report, `next.BlockPosition` to index `workingDocument`). `OutputItem.Position` is not touched by the shift routines (which shift `OutputChain.BlockPosition`), so it remains the stable original-source anchor a diagnostic must carry.
- **Grounding.** `src/Heddle/Runtime/HeddleCompiler.cs` · `CollectGap`, `ShiftBySkippedTokens`, `RemoveEmptyItem` (shift `BlockPosition`, not `OutputItem.Position`).

## Implementation plan

Ordered; an implementer works straight down. TDD verdict per work item is in [Testing plan](#testing-plan).

### WI1 — Claim `HED2004` (id + one-to-one test + registry)

- **Files.**
  - `src/Heddle/Data/HeddleDiagnosticIds.cs`
  - `src/Heddle.Tests/DiagnosticIdTests.cs`
  - `docs/spec/common/cross-cutting-decisions.md`
- **Change.**
  - Add, immediately after `RedundantEncodingExtension` (keeping HED2xxx contiguous):
    ```csharp
    /// <summary>A bare, bodiless unnamed <c>@(value)</c> output sits — under the Html profile — in an HTML
    /// attribute value, a <c>&lt;script&gt;</c> block, or a URL component, where element-text encoding is
    /// insufficient or wrong; the matching context encoder (<c>@attr</c>/<c>@js</c>/<c>@url</c>) is not used.
    /// Warning; heuristic (local adjacent-literal scan); never fires off the Html profile.</summary>
    public const string MissingContextEncoder = "HED2004";
    ```
  - In `DiagnosticIdTests.ConstantsMatchTheDiagnosticsTableOneToOne`, add `"HED2004"` to the `HED2001`–`HED2003` line of the `expected` set.
  - In the cross-cutting registry table, ensure the dedicated `HED2004` row (owned by this phase spec, HTML-context encoding lint) is present — it is **already added** at `cross-cutting-decisions.md:157` during authoring, so verify it rather than re-adding; the `HED2001`–`HED2003` built-in-extensions row is left intact.
- **Done when.** `DiagnosticIdTests` is green (count and set match) and the registry lists `HED2004`.

### WI2 — The scan and its hook in `CompileBody`

- **Files.** `src/Heddle/Runtime/HeddleCompiler.cs`
- **Change.**
  - Add a private enum and two private static helpers (all compile-time, `netstandard2.0`-safe, allocation-light — a single `Substring` for the classifier window is acceptable on the compile path):
    ```csharp
    private enum HtmlContext { None, Attribute, Script, Url }

    // Left-only heuristic over workingDocument; see spec D3. `leftSpans` are the BlockPositions of the
    // producing output blocks already seen to the left of blockStart (current coordinates); the scan
    // skips them so a `<`/`>`/quote inside a sibling `@(…)` source never skews the classification.
    // Returns None when the block is not in a covered position or the position is out of range.
    private static HtmlContext ClassifyHtmlContext(string workingDocument, int blockStart,
        List<BlockPosition> leftSpans) { … }

    // Gate (D1) + candidacy (D2) + classify (D3) + emit (D5/D7). Called from the OutputChains loop.
    private static void ScanHtmlContextLint(OutputChain chain, List<BlockPosition> leftSpans,
        CompileScope compileScope, string workingDocument) { … }
    ```
  - `ScanHtmlContextLint` body, in order (cheapest gate first, for zero off-profile cost):
    1. `if (compileScope.CompileContext.OutputProfile != OutputProfile.Html) return;`
    2. `var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null; if (leftmost == null) return;`
    3. `if (leftmost.ExtensionName.Length != 0 || !string.IsNullOrEmpty(leftmost.ParameterTemplate)) return;` (D2)
    4. `var ctx = ClassifyHtmlContext(workingDocument, chain.BlockPosition.StartIndex, leftSpans); if (ctx == HtmlContext.None) return;`
    5. Build the context-specific `Error`/`Fix` (see [Diagnostics](#diagnostics--error-surface)) and add a `HeddleCompileWarning { Error, Fix, Position = leftmost.Position, DiagnosticId = HeddleDiagnosticIds.MissingContextEncoder }` to `compileScope.CompileWarnings`.
  - Hook: in `CompileBody`, **before** the `foreach (var extensions in parseContext.OutputChains)` loop declare `var leftSpans = new List<BlockPosition>();`. Inside the loop, in the **producing** branch (`else` of `if (returnTypeChainedPrevious == null)`, where `documentElements.Add(element)` runs), call `ScanHtmlContextLint(extensions, leftSpans, compileScope, workingDocument);` **before** `documentElements.Add(element)`, then append `leftSpans.Add(extensions.BlockPosition);` (so each producing block's `@(…)` source span is skipped by later candidates' left scans — D3). Rationale for the site: the profile reflects all earlier `@profile()` flips (document order); `workingDocument` and `extensions.BlockPosition` are consistent for a producing block; the empty-output branch (which would shift positions via `RemoveEmptyItem`) is excluded — it adds nothing to `leftSpans` and a bare `@(value)` always produces output — so `leftSpans` holds exactly the producing blocks left of the current one, in current coordinates. The scan is **not** added to the separate `DefaultChains` loop (those synthetic default-output blocks sit at `BlockPosition(workingDocument.Length, 0)` with no authored adjacent literal — see [Deferred items](#deferred-items)).
  - `ClassifyHtmlContext` implements D3 verbatim over the sibling-span-skipping left region `L` (Step 1 `<script>` containment, checked first; Step 2 nearest-boundary backward scan; Step 3 quote-parity + `URL_ATTRS` + component signal). To skip a span while walking left, when index `i` falls within a `leftSpans` entry `[StartIndex, StartIndex + Length)` reset `i` to `StartIndex - 1`; the `"<script"`/`"</script"` searches likewise ignore any occurrence whose index lies inside a `leftSpans` span. `URL_ATTRS` is a `static readonly HashSet<string>(StringComparer.OrdinalIgnoreCase)` field or an ordinal-case-insensitive switch. Case-insensitive substring search for `"<script"`/`"</script"` uses an index loop or `IndexOf(..., StringComparison.OrdinalIgnoreCase)` (available on all target TFMs).
- **Done when.** All WI3 tests pass; the existing suite and goldens are byte-identical (WI2 adds only `CompileWarnings` entries, never mutates `workingDocument` or any `DocumentElement`).

### WI3 — Tests

- **Files.**
  - `src/Heddle.Tests/HtmlContextLintTests.cs` (new; primary vehicle — inline-template compile assertions, modeled on `DoubleEncodeWarningTests`)
  - `src/Heddle.Tests/TestTemplate/context-lint-corpus.heddle` (new fixture) + its byte-neutrality goldens `generated-context-lint-html.html` and `generated-context-lint-text.html` (siblings; `TestTemplate/**` is already LF-pinned in `.gitattributes`).
- **Change.** See [Testing plan](#testing-plan) for the full matrix. TDD: write these failing first (WI3 precedes WI2 in the loop).
- **Done when.** Every success-criterion and validation row in the plan has a passing assertion under **both** profiles, and the byte-neutrality goldens match.

### WI4 — Docs + sample gallery (user-visible change)

- **Files.**
  - `docs/built-in-extensions.md` — under **HTML encoding → Profile diagnostics**, add the `HED2004` bullet; under **Encoding contexts**, add a short "the compiler warns (`HED2004`) when a bare `@(value)` sits in an attribute/`<script>`/URL position under the Html profile — it is a best-effort, single-hop, model-free heuristic" note that enumerates the accepted documented limits from D3: single-hop/cross-call false negative; fires inside an HTML comment (`<!-- @(X) -->`) and after a stray `<`; over-fires to `Script` on a `<script>`-shaped literal inside an attribute value, a raw-text element (`<textarea>`/`<title>`), or an HTML comment / CDATA section (a document-spanning `Script` false positive); routes attribute-name/tag-name injection (`<div @(Attrs)>`, `<@(Tag)>`) to a generic `@attr` suggestion; and under-classifies a URL to `Attribute` under mixed-quote poisoning. Every flagged site is silenceable per-site by the matching encoder or `@raw`.
  - `samples/` — add a gallery item (or extend `samples/html-safe-output`) demonstrating the lint: a template with `<a title="@(X)">` under `Html` whose compile result surfaces one `HED2004`, and the rendered output (unchanged, byte-identical to the pre-lint bytes) asserted by the gallery golden.
- **Done when.** `cd docs && npm run docs:build` succeeds; the sample's golden + diagnostic assertion pass in its CI job.

## Public API / contract

- **New public constant.** `Heddle.Data.HeddleDiagnosticIds.MissingContextEncoder = "HED2004"` (a `public const string`, XML-documented per WI1). This is the only public-surface addition.
- **No new/changed types, members, options, enums, or extensions.** `TemplateOptions` is untouched (no new option → the `TemplateOptionsCompletenessTests` reflection invariant is unaffected; nothing to add to the copy ctor/`Equals`/`GetHashCode`).
- **Thread-safety.** The lint is pure compile-time logic inside `HeddleCompiler` (single-threaded compile); it adds **no** per-render state, no extension-instance state, and touches no render path. `HeddleCompileWarning` objects are created on the compile thread and collected into the existing `CompileWarnings` list exactly as every other compile diagnostic.

## Diagnostics / error surface

One new diagnostic. Severity **warning** (never blocks compilation, R5). Emitted from `HeddleCompiler.CompileBody` under the D1 gate + D2 candidacy + D3 classification. Position is `leftmost.Position` (the `@(...)` block, original-source coordinates, D7).

| ID | Detected context | Message (`Error`) | `Fix` | Trigger | Position |
|---|---|---|---|---|---|
| `HED2004` | Attribute | `A bare '@(...)' output is inside an HTML tag under the Html profile (attribute value or an unquoted/name position); element-text encoding is insufficient there.` | `Use '@attr(...)' for the attribute context, or '@raw(...)' if the value is trusted.` | Candidate (D2) under `Html` (D1) whose left-literal scan (D3) returns `Attribute`. | `@(...)` block |
| `HED2004` | Script | `A bare '@(...)' output is inside a <script> block under the Html profile; HTML element-text encoding is wrong for a JavaScript context.` | `Use '@js(...)' for the JavaScript-string context, or '@raw(...)' if the value is trusted.` | …scan returns `Script`. | `@(...)` block |
| `HED2004` | Url | `A bare '@(...)' output is in a URL component under the Html profile; element-text encoding does not percent-encode it.` | `Use '@url(...)' for the URL-component context, or '@raw(...)' if the value is trusted.` | …scan returns `Url`. | `@(...)` block |

Notes: exactly one warning per flagged block (one candidate → one classification → one add). The `ToString()` form is `[start:len]HED2004: <Error>` per the existing `HeddleCompileError` formatting pinned by `DiagnosticIdTests`.

## Testing plan

**TDD verdict.** *Test-first* for the diagnostic contract (id, per-context message/position, the R3 gate, byte-neutrality) — these assertions are written failing before WI2. *Test-with* for the D3 heuristic boundary rows (comment false positive, unquoted, whole-URL degrade) — pinned alongside implementation as the classifier is tuned, then frozen (the table is part of this spec; changing a row is a spec change per testing-standards).

**Suite home.** `src/Heddle.Tests` (xUnit; multi-TFM incl. `net48` on Windows). Compile via `new HeddleTemplate(template, new CompileContext(new TemplateOptions { OutputProfile = profile }, model.GetType()))`, assert `t.CompileResult.Success`, inspect `t.Context.CompileWarnings.Where(w => w.DiagnosticId == HeddleDiagnosticIds.MissingContextEncoder)` — the exact shape of `DoubleEncodeWarningTests`.

**Positive / negative matrix — run under BOTH `OutputProfile.Html` and `OutputProfile.Text`** (every `Text` row asserts **zero** `HED2004`, the R3 gate):

| # | Template | `Html` expectation | `Text` expectation |
|---|---|---|---|
| 1 | `<a title="@(X)">…</a>` | one `HED2004`, Attribute message, `Fix` names `@attr`; positioned at `@(`. | zero |
| 2 | `<a title="@attr(X)">…</a>` | zero (correct encoder). | zero |
| 3 | `<p>@(X)</p>` | zero (element text). | zero |
| 4 | `<a title="@raw(X)">…</a>` | zero (explicit opt-out). | zero |
| 5 | `<script>var n = "@(X)";</script>` | one `HED2004`, Script message, `Fix` names `@js`. | zero |
| 6 | `<a href="/s?q=@(X)">` | one `HED2004`, Url message, `Fix` names `@url`. | zero |
| 7 | `<!-- @(X) -->` | one `HED2004` (documented false positive; Attribute message). | zero |
| 8 | definition body `@%<t>{{ @(X) }}%@` invoked `<a title="@t()">` | zero at the definition (documented single-hop false negative). | zero |
| 9 | `<a href="@(FullUrl)">` (whole-URL value) | one `HED2004`, **Attribute** message (whole-URL degrades to `@attr`, D3). | zero |
| 10 | `<a title='@(X)'>` (single-quoted) | one `HED2004`, Attribute. | zero |
| 11 | `<script src="@(X)"></script>` (attr inside script start tag) | one `HED2004`, Attribute (`src` value-so-far empty → not Url). | zero |
| 12 | `@profile(){{html}}` then `<a title="@(X)">` under `Text` options | one `HED2004` (mid-document flip to Html honored). | n/a |
| 13 | `@profile(){{text}}` then `<a title="@(X)">` under `Html` options | zero (flip to Text before the block). | n/a |
| 14 | `<script-loader>@(X)</script-loader>` (script-prefixed custom element) | zero (Step 1 boundary requirement: char after `<script` is `-`, not `[\s/>]` → not a script open → falls through to element text → `None`). | zero |
| 15 | `<p data-x="<script>">@(X)</p>` (script literal in an attribute value) | one `HED2004`, **Script** message — documented false positive (D3 Step-1 limit). | zero |
| 16 | `<textarea><script></textarea>@(X)` (raw-text element) | one `HED2004`, **Script** message — documented false positive (D3 Step-1 limit). | zero |
| 17 | `<@(Tag)>` (tag-name position) / `<div @(Attrs)>` (attribute-name position) | one `HED2004` each, **Attribute** message (generic `@attr` suggestion; documented attribute-branch limit). | zero |
| 18 | `<a title='it"s' href="/s?q=@(X)">` (mixed-quote poisoning) | one `HED2004`, **Attribute** (under-classified from `Url`; documented limit). | zero |
| 19 | `<script>for (var i = 0; i < n; i++) { x = @(X); }</script>` (interior `<` before the block, in script body) | one `HED2004`, **Script** message, `Fix` names `@js` — the `i < n` `<` does **not** mis-route to `@attr` (D3 Step 1 anchors on the closed `<script>` start tag). Regression witness for the reordered heuristic. | zero |
| 20 | `<img src="/img/@(Id)/thumb.png">` (URL path segment) | one `HED2004`, **Url** message, `Fix` names `@url` (`valueSoFar` `"/img/"` ends with `/` → path-segment signal, `src ∈ URL_ATTRS`). | zero |
| 21 | `<span data-n="@(count > 0)@(label)">` (two interpolations; relational `>` in the first block's source) | **two** `HED2004`, both **Attribute** — the `>` inside the first block's `@(count > 0)` **source** is skipped, so `@(label)` still classifies `Attribute` (D3 sibling-span skip). Regression witness for the directive-source-bleed fix. | zero |
| 22 | `<script src="a>@(X)">` (literal `>` in an earlier quoted attribute value of the same start tag) | one `HED2004`, **Attribute** message — the `>` inside `src="a>` is inside a quoted value, so Step 1 condition (b) is not satisfied; the block is the `src` attribute value, **not** `Script` (quote-aware condition-(b) regression witness). | zero |
| 23 | `<script data-x="a>b" src="/s?q=@(X)">` (quoted `>` then a URL attribute in the same start tag) | one `HED2004`, **Url** message, `Fix` names `@url` — the quoted `>` in `data-x` does not close the start tag; `src ∈ URL_ATTRS`, `valueSoFar` `"/s?q="` has `?`/`=` → `Url` (quote-aware condition-(b) regression witness). | zero |
| 24 | `<script>x=1</scriptx>@(X)</script>` (non-boundary `</scriptx>` before the block) | one `HED2004`, **Script** message — `</scriptx>` fails the close tag-name-boundary check (char after `</script` is `x`), so it does not count as a close; `open` with no valid `close` → `Script` (close-boundary symmetry witness). | zero |
| 25 | `<!-- <script> --><p>@(X)</p>` (unmatched `<script>` inside an HTML comment) | one `HED2004`, **Script** message — documented document-spanning `Script` false positive (D3 Step-1 comment/CDATA limit). | zero |

**Positioned-diagnostic negative tests.** Rows 2, 3, 4, 8 assert **absence** by `DiagnosticId` (not merely "compilation succeeded"), and every positive row asserts `warning.Position` maps to the `@(` in the original source (e.g. `Assert.Equal(expectedIndex, warning.Position.StartIndex)`), per testing-standards "assert positioned diagnostics."

**Byte-neutrality corpus (both profiles).** `context-lint-corpus.heddle` bundles rows 1–11 and 19–25 (all shapes above, benign values; the added script/URL/multi-interpolation and quote-aware/close-boundary/comment rows are byte-neutral by the same "adds only `CompileWarnings`" argument). Assert `t.Generate(model)` is byte-identical to the checked-in golden under `Html` (`generated-context-lint-html.html`) and under `Text` (`generated-context-lint-text.html`), read with the established `.Replace("\r\n","\n")` normalization; goldens are LF-pinned. The point: the lint fires many warnings yet **renders identical bytes** to the pre-lint engine — the plan's byte-neutral criterion.

**Corpus-wide R3 gate (reframed — recorded correction).** The R3 guarantee is that the lint is inert **unless an `Html` context is in effect for the block**, not the stronger “zero `HED2004` under `Text` options for literally every corpus input.” The latter does **not** follow from the design: a fixture that flips to Html mid-document with `@profile()` (D1) legitimately fires `HED2004` on blocks after the flip even when the *base* options are `Text` (the profile is per-block, honoring the flip). So the `[Theory]` over every existing `src/Heddle.Tests/TestTemplate` fixture asserts **zero `HED2004` under `OutputProfile.Text` for every fixture that does not contain a `@profile()` flip to Html**; any fixture that does flip is excluded from the blanket assertion and instead carries an **explicit** expectation (its post-flip blocks are classified under Html by design). This states exactly what R3 guarantees — off-Html blocks never warn — rather than an incidental property of today’s corpus (which happens to keep its one flipping fixture’s `@(value)` in element text). If a future fixture flips to Html and places `@(value)` in an attribute, the blanket form would spuriously “fail”; the reframed form correctly expects the warning.

**Regression gate (one combined run).** `dotnet build -c Release` (all TFMs) → `dotnet test src/Heddle.Tests` (all TFMs, zero failures, existing goldens byte-identical) → grammar-stability check (this phase declares **no** grammar change: `src/Heddle.Language/generated/` has no diff) → docs build for the WI4 pages. No benchmark run is required by the gate (no hot path touched, see [Performance](#performance-considerations)), but the `Heddle.Performance` compile benchmark is available if a reviewer wants the compile-path delta measured.

**Sample gallery.** The WI4 sample is the user-visible demo item (compile-diagnostic assertion + unchanged-bytes golden) per testing-standards.

## Back-compat and migration

- **Rendered output — byte-identical, proven.** WI2 adds only entries to `CompileWarnings`. It never assigns `workingDocument`, never mutates any `OutputChain.BlockPosition`, never adds/removes/reorders a `DocumentElement`, and touches no emitting leaf. Proof obligations discharged by tests: the two byte-neutrality goldens (both profiles) and the untouched existing golden suite (`OutputProfileGoldenTests`, `BranchingGoldenTests`, `ContextEncodingGoldenTests`, the dynamic == precompiled differential). The precompiled backend is unaffected (D6 — no generator change), so the differential holds.
- **Compilation success — unchanged.** `HED2004` is warning-tier; every template that compiled before still compiles, and compilation succeeds even when `HED2004` fires. No path returns `null`/errors from the new code.
- **Public surface — one additive constant.** `HeddleDiagnosticIds.MissingContextEncoder`. New warnings on existing `Html`-profile templates are expected (the point of the lint) and are advisory; a warnings-as-errors build sees them — the standard, bounded cost of any additive lint, mitigated by the conservative default (D4) and per-site silencing (encoder/`@raw`).
- **Breaking window.** None. This phase schedules nothing into a breaking window (no byte or behavior change).

## Performance considerations

- **Render path: zero cost.** The lint is entirely compile-time; nothing is added to any render delegate, `Scope`, or emitting leaf. No new per-render allocation.
- **Compile path: negligible, gated cheap-first.** For non-`Html` compiles the work is a single enum comparison per output chain (the D1 gate returns immediately) — effectively free, and it is the common case for text/JSON/code generation. Under `Html`, only bare bodiless `@(...)` chains reach the scan; the classifier is a left-only character walk that stops at the nearest `<`/`>` (typically a few characters), plus the `<script`/`</script` containment check. The sibling-span skip (D3) is cheap: `leftSpans` is built in ascending `StartIndex` order as the loop advances (an `Add` per producing block), and the walk crosses at most the spans lying between the block and its nearest tag boundary — checked with a single descending pointer / binary search, not a per-index rescan. Worst case is O(distance-to-previous-tag + spans-crossed) per candidate block, on the "compile once, render many" path where moderate compile cost is acceptable (coding-standards). One `Substring` per in-tag candidate (the classifier window) is the only allocation, mirroring `CollectGap`.
- **Guarding benchmark.** No hot path is touched, so the regression gate requires no benchmark. The existing `Heddle.Performance` compile benchmarks remain the reference if a reviewer elects to measure the compile-path delta; expectation is within reported error.

## Standards compliance

- **YAGNI / simplicity (precedence rule 3).** No options knob, no suppression pragma, no second diagnostic id, no generator twin — each excluded because a scenario needs it, not "for flexibility." The smallest design meeting the success criteria: one const, one scan method, one hook.
- **DRY (knowledge, not lines).** The bare-`@(value)` shape and the profile gate are the *same knowledge* already encoded in `UnnamedCarrierName`/`WarnOnRedundantEncoding`; the lint reuses the identical predicate and gate rather than inventing a parallel notion of "unnamed output." The substrate access reuses the `CollectGap` two-coordinate pattern.
- **Security beats ergonomics (precedence rule 2), within R3/R4.** The lint raises the security signal (wrong-context encoding) but never *acts* — no auto-escape, no byte change — so it cannot itself open or alter an execution/encoding path. It is advisory only.
- **Single responsibility.** The new code lives in the compile stage where the other position/string lints (`HED3001`, `HED2003`) live; it is not shredded into ceremony classes.
- **`netstandard2.0`.** `HashSet<string>(StringComparer.OrdinalIgnoreCase)`, `string.IndexOf(string, StringComparison)`, `Substring`, and char loops are all `netstandard2.0`-available; no `#if`, no modern-BCL-only API (D7 stance).

## Deferred items

| Item | Trigger |
|---|---|
| A richer *explicit* context signal (editor/integration hint distinct from the `Html` profile) | A ratified need for finer positional context than the document-scoped profile; extends the same gate without reopening R3 (plan seam). |
| Additional covered positions (CSS context; further HTML sub-contexts) | A ratified request; layers on the same gate + adjacent-literal substrate. |
| `DefaultChains` (`-> chain` default output) coverage | Only if a real case shows a default-output block with meaningful authored adjacent literal; today they sit at `BlockPosition(document.Length, 0)` with none. |
| Build-time generator twin (`HED70xx`) | Only if the precompiled path ever needs authoring-time diagnostics independent of the editor; today byte-neutral, so nothing to diverge (D6). |
| Transitive / cross-call context propagation | **Permanently out** (R3/R4, non-additive); the single-hop limit is a documented guarantee, not a gap to close. |
| Inline suppression syntax / severity knob | Permanently out this phase (P3-Q5); revisit only if maintainer reclassifies severity policy engine-wide. |

## External references

- [built-in-extensions.md → Encoding contexts](../../built-in-extensions.md#encoding-contexts) and [→ HTML encoding / Output profiles](../../built-in-extensions.md#html-encoding) — the `@attr`/`@js`/`@url`/`@raw` semantics and the `HED2001`–`HED2003` profile diagnostics this phase extends.
- Plan: [phase-3-html-context-encoding-lint.md](../../plan/phase-3-html-context-encoding-lint.md); governance [standing-rulings.md](../../plan/common/standing-rulings.md) (R3/R4/R5), [open-questions.md](../../plan/open-questions.md) (P3-Q1..Q5).
- Spec conventions: [spec-conventions.md](../common/spec-conventions.md), [coding-standards.md](../common/coding-standards.md), [testing-standards.md](../common/testing-standards.md), [cross-cutting-decisions.md](../common/cross-cutting-decisions.md) (D1 + registry), [breaking-windows.md](../common/breaking-windows.md).
- Source seams: `src/Heddle/Runtime/HeddleCompiler.cs` (`CompileBody`, `CollectGap`, `WarnOnRedundantEncoding`, `UnnamedCarrierName`), `src/Heddle/Runtime/CompileContext.cs` (`OutputProfile`), `src/Heddle/Extensions/ProfileExtension.cs` (`InitStart`), `src/Heddle/Data/HeddleDiagnosticIds.cs`, `src/Heddle.Tests/DiagnosticIdTests.cs`, `src/Heddle.LanguageServices/DocumentAnalyzer.cs`, `src/Heddle.Generator/Emit/DocumentShaper.cs`.
- OWASP output-encoding-by-context guidance and Go `html/template` contextual autoescaping (the parsing-based contrast rejected under R4) — as cited in the plan's External grounding.
