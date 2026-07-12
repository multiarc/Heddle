# Heddle Improvement Plan — Technical Specification

*Normative specification for every item in the [Improvement Plan](improvement-plan.md)
(all items approved 2026-07-11, including D3). Specification only — no implementation is part of
this document. Requirement IDs (`A1.1-R1`, `B2-R3`, …) are stable and referenced from the plan's
[implementation tracker](improvement-plan.md#implementation-tracker).*

**Normative language.** MUST / MUST NOT / SHOULD / MAY as in RFC 2119. Anything marked
*(verify at implementation)* is a fact this spec pinned to the current source that the
implementer MUST re-confirm before relying on it.

**Governance.** This spec is bound by the shared specs under [docs/spec/common](spec/common/) —
the [conventions](spec/common/spec-conventions.md), [coding](spec/common/coding-standards.md)
and [testing](spec/common/testing-standards.md) standards, the cross-cutting decisions
`D1`–`D9`, and the [breaking-windows policy](spec/common/breaking-windows.md). Where this
spec re-lands items that slipped the 2.0 window (B1, B2), the reconciliation is recorded in
the [amendments ledger](spec/common/cross-cutting-decisions.md#cross-spec-amendments-ledger)
and the [2.0 as-shipped record](spec/records.md#the-20-breaking-window--as-shipped-record).

---

## 0. Global invariants (apply to every workstream)

| ID | Invariant |
| --- | --- |
| G-R1 | **No grammar change.** No `.g4` file is modified by any item in this spec. |
| G-R2 | **Byte compatibility.** Every template that compiles and renders on 2.0.0 MUST compile and render byte-identically with all items implemented, given unchanged `TemplateOptions`. New behavior activates only through new, default-off/null options or new extension names. |
| G-R3 | **Differential guarantee preserved.** The precompiled backend MUST remain byte-identical to the runtime backend across the fixture corpus. Any item touching render or encode paths MUST add corpus fixtures exercising its behavior on both backends (see B2, C1, C2). |
| G-R4 | **Diagnostics discipline.** New compile-time diagnostics use the next free code in the semantically matching `HEDxxxx` range, carry a template position, are documented in the same doc page that documents their neighbors, and are claimed in the [diagnostic registry](spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) in the same change. Allocated by this spec: **`HED4003`** (B3, registry row recorded). No other new codes. |
| G-R5 | **Options identity discipline.** A new `TemplateOptions` member participates in `Equals`/`GetHashCode` **iff** it changes compiled structure or rendered bytes of a *successful* render (precedent: `OutputProfile`, `TrimDirectiveLines` participate; `MaxRecursionCount` does not). Participation in the precompiled options fingerprint ([PrecompiledOptionsFingerprint.cs](../src/Heddle/Precompiled/PrecompiledOptionsFingerprint.cs)) follows the same rule but only for options that alter *generated code or baked output* (precedent: `Functions` and `MaxRecursionCount` deliberately excluded). Decisions per item: B2 → Equals yes / fingerprint conditional (B2-R7); C1 → neither (C1-R8). |
| G-R6 | **Warning-clean first-party build.** After each phase, the solution builds with zero warnings (including the new `[Obsolete]` and `HED4003` warnings) — first-party code is migrated, not suppressed, except where a test intentionally exercises deprecated surface (then `#pragma warning disable` with a comment). |

---

## Workstream A — Documentation drift

Every A-item is an edit specification: **file, anchor, current text (verified), replacement
text**. Replacement wording below is normative in *content*; the implementer MAY adjust phrasing
for flow but MUST preserve every stated fact.

### A1.1 — README stale "no `else`/`elif`" claim

- **File:** [README.md](../README.md), line 82 (the "How Heddle compares" closing paragraph).
- **Current:** `…The language also trades some ergonomics for its small core — dense sigils and no `else`/`elif` (compose `@if`/`@ifnot` instead).`
- **Replacement (content-normative):** the trade-off sentence MUST (a) drop the `else`/`elif`
  claim entirely, (b) name a *current* trade-off instead: dense sigils and the per-extension
  descend-vs-step-back context rule (link `docs/language-reference.md#stepping-back-the-parent-context`).
- **Additionally (A1.1-R2):** re-audit the comparison table above it. The "Control flow —
  Extensions (a library)" row stays; verify no other cell asserts 1.x-only facts. The row
  `Untrusted templates: No (or run in no-C# mode)` SHOULD be revised to name
  `ExpressionMode.Native` explicitly once C1 ships; until then it stays.
- **Verification:** `grep -n "elif" README.md` returns no match asserting absence of the feature.

### A1.2 / A1.3 — csharp-api.md TemplateOptions defaults

- **File:** [docs/csharp-api.md](csharp-api.md), `TemplateOptions` table, lines 177–178.
- **Current:** `OutputProfile` default column says `Text`; `TrimDirectiveLines` default column
  says `false`, prose says "flips to `true` in the 2.0 window".
- **Replacement:** default columns MUST read `Html` and `true` respectively (source of truth:
  [TemplateOptions.cs](../src/Heddle/Data/TemplateOptions.cs) constructor, lines 80–93). Prose
  MUST present `Text` / `false` as the 1.x-compatibility settings, past tense ("since 2.0").
  All other cell content (inheritance, cache-identity participation, links) is unchanged.

### A1.4 — precompilation.md MSBuild defaults

- **File:** [docs/precompilation.md](precompilation.md), "Compile options" table.
- **Current:** `HeddleOutputProfile` — "`Text` (default) | `Html`"; `HeddleTrimDirectiveLines` —
  "`false` (default) | `true`".
- **Replacement:** `Html` and `true` marked as defaults (source of truth:
  [Heddle.Generator.props](../src/Heddle.Generator/build/Heddle.Generator.props) lines 23/25 and
  [ConfigReader.cs](../src/Heddle.Generator/Pipeline/ConfigReader.cs) lines 27/31).
- **A1.4-R2:** add one sentence to the table intro noting the generator defaults track the
  engine defaults (both flipped in 2.0), so an unset property never causes a gauntlet
  fingerprint mismatch against a default-options runtime request.

### A1.5 / A1.6 — 2.0-window tense sweep

- **Files/lines (verified):** [built-in-extensions.md](built-in-extensions.md) 274, 460, 477;
  [language-reference.md](language-reference.md) 215, 1093–1094, 1138.
- **Rule (A1.5-R1):** every occurrence of the pattern *"the 1.x default"*, *"stays `Text` in
  1.x"*, *"flips to X in the 2.0 window"* MUST be rewritten so that: the **current (2.0)
  default is stated first**, in present tense; the 1.x value is mentioned only as the
  compatibility setting. Future tense about 2.0 is forbidden.
- **Sweep command (A1.5-R2):** `grep -rn "1\.x default\|2\.0 window\|flips to" docs/*.md README.md`
  MUST return only hits that are present-tense and historically framed (or zero hits).

### A2.1 — `AllowCSharp` obsolescence wording

- **Files:** [native-expressions.md](native-expressions.md) 188–189 ("will be marked obsolete
  in the 2.0 release"); [TemplateOptions.cs](../src/Heddle/Data/TemplateOptions.cs) line 24 XML
  doc ("Scheduled for `[Obsolete]` in the 2.0 window").
- **Resolution:** both texts are replaced as part of **B1** (see B1-R3/B1-R4). This item is
  *only* the tracking alias; it MUST NOT be closed by rewording alone — the feature ships.

### A2.2 — `TemplateOptions.Encoder` promise

- **File:** [samples/html-safe-output/README.md](../samples/html-safe-output/README.md) 23–24.
- **Resolution:** replaced as part of **B2** (see B2-R9). Same rule: close by shipping, not by
  rewording.

### A3.1 — Documentation map completeness

- **File:** [docs/README.md](README.md), "Documentation map" table + "Pick your path".
- **Spec:** add three rows (alphabetical position within the existing order):
  - `Native Expressions` → `native-expressions.md` — "The sandbox-safe expression tier:
    operators, literals, registered functions, `ExpressionMode`."
  - `Build-Time Pre-compilation` → `precompilation.md` — "Compiling `.heddle` files into the
    assembly: generator setup, typed entry points, registry, mismatch policy."
  - `Editor Support` → `editor-support.md` — "LSP server, VS Code extension, Neovim setup,
    configuration."
- **A3.1-R2 (personas):** "I want to write templates" adds native-expressions.md and
  editor-support.md; "I want to use the engine from C#" adds precompilation.md.
- **Verification:** every `docs/*.md` file (excluding `node_modules`, this spec, the plan, and
  the assessment) appears in the map exactly once.

### A3.2 — Package tables completeness

- **Files:** [README.md](../README.md) "Packages" table; [docs/README.md](README.md) package table.
- **Spec:** both tables MUST list every published artifact: `Heddle`, `Heddle.Language`,
  `Heddle.Generator` (note: `PrivateAssets="all"`, analyzer package), `Heddle.LanguageServer`
  (note: dotnet tool), and `Heddle.LanguageServices` **iff** it is actually published
  *(verify at implementation: editor-support.md calls it "a public package"; confirm
  `IsPackable`/CI publish before listing)*. One-line purpose per row, consistent between the
  two tables.

### A3.3 — Docs site nav

- **File:** the VitePress config under `docs/` *(locate at implementation:
  `docs/.vitepress/config.*`)*.
- **Spec:** nav/sidebar MUST include every page the A3.1 map lists, same grouping as the
  personas. No orphan pages.

### A4.1 — `AllowCSharp = true` example hygiene

- **Files (verified usage list):** [README.md](../README.md) quick-start,
  [docs/README.md](README.md) "30-second taste", [docs/getting-started.md](getting-started.md),
  plus any sample/doc hit from `grep -rn "AllowCSharp" docs samples README.md`.
- **Rule (A4.1-R1):** an example sets `ExpressionMode`/`AllowCSharp` **only if** the example
  actually contains an inner-`@` C# expression. The two hello-world examples (member access on
  `dynamic`) MUST drop the option entirely.
- **Rule (A4.1-R2):** examples that *do* need full C# MUST use
  `ExpressionMode = ExpressionMode.FullCSharp` (not `AllowCSharp = true`) once B1 lands, so no
  doc example triggers the new obsolete warning.

### A4.2 — Dangling archived-spec links

- **Files:** [samples/README.md](../samples/README.md) "Source of record" column links
  (`../docs/spec/phase-*/README.md`) — targets deleted on this branch; sweep the whole tree.
- **Rule:** after the archive deletion lands, `grep -rn "docs/spec/\|docs/archive/" --include="*.md" .`
  (excluding `node_modules`) MUST return zero broken links. For the samples table, replace each
  spec link with the closest living doc (phase 1 → native-expressions.md, phase 2 →
  built-in-extensions.md#html-encoding, phase 3 → built-in-extensions.md#branch-sets, phase 5 →
  language-reference.md#props, phase 7 → precompilation.md, phase 8 → csharp-api.md#streaming,
  phase 6 → editor-support.md) or plain text if none fits.

---

## Workstream B — Ship the promised features

### B1 — `[Obsolete]` on `TemplateOptions.AllowCSharp`

| ID | Requirement |
| --- | --- |
| B1-R1 | Add to the property ([TemplateOptions.cs:26](../src/Heddle/Data/TemplateOptions.cs#L26)): `[Obsolete("Use ExpressionMode. AllowCSharp == true is equivalent to ExpressionMode.FullCSharp; false selects Native (or leaves MemberPathsOnly untouched).")]`. Warning, not error (`error: false` implicit). The property's behavior is unchanged — it remains a functional bridge. |
| B1-R2 | **No serialization break.** *(Verify at implementation)* if `TemplateOptions` is ever bound from config/JSON in first-party code or samples, binding to an obsolete property still works; nothing else needed. |
| B1-R3 | Replace the XML doc line "Scheduled for `[Obsolete]` in the 2.0 window" with present-tense guidance mirroring the attribute message. |
| B1-R4 | [native-expressions.md:188](native-expressions.md#L188): rewrite to "retained for compatibility and marked `[Obsolete]` since 2.x — reads/writes keep working; new code uses `ExpressionMode`." |
| B1-R5 | **First-party sweep** (from verified usage inventory): `ContextCompilation.cs`, `HeddleCompiler.cs`, `TemplateResolver.cs` internal *reads* MUST switch to `ExpressionMode` comparisons; `Heddle.Performance` runners and all `Heddle.Tests` usages migrate to `ExpressionMode`, **except** tests that exercise the bridge semantics itself (`ExpressionModeTests`, `TemplateOptionsCompletenessTests`) which wrap the access in `#pragma warning disable CS0618` + one-line comment. Docs/samples per A4.1-R2. |
| B1-R6 | **Test:** one new test asserting via reflection that `AllowCSharp` carries `ObsoleteAttribute` with `IsError == false` (guards against accidental removal or error-escalation). |

**Acceptance:** solution builds warning-clean (G-R6); `grep -rn "AllowCSharp" src samples docs`
shows only: the property itself, the bridge tests, and historical-note documentation.

### B2 — `TemplateOptions.Encoder`

**Intent.** The pluggable output encoder promised by
[samples/html-safe-output/README.md:23](../samples/html-safe-output/README.md#L23) and
pre-designed in the [HtmlEncodedRenderer.cs:24-31](../src/Heddle/Data/HtmlEncodedRenderer.cs#L24)
doc comment ("The 2.0 encoder swap upgrades exactly this method body to a span
`Encode`/`EncodeUtf8` loop; deliberately **not** an `IUtf8ScopeRenderer`, so pre-encoded bytes
can never bypass the proxy").

| ID | Requirement |
| --- | --- |
| B2-R1 | **Public contract — the ratified `TextEncoder` seam** (closing this spec's former open decision 1 against the v2.0-effort ratified design recovered in the [2.0 window record](spec/records.md#the-20-breaking-window--as-shipped-record), item 2): `public System.Text.Encodings.Web.TextEncoder Encoder { get; set; }` on `TemplateOptions`. No custom abstract class — `TextEncoder` is the BCL type designed for exactly this seam, with string, span (`FindFirstCharacterToEncode`/`Encode(TextWriter, …)`), and UTF-8 (`EncodeUtf8`) paths, and `HtmlEncoder`/`JavaScriptEncoder`/`UrlEncoder` as ready-made implementations for hosts. Dependency note: in-box on `net6.0+`; `System.Text.Encodings.Web` package reference required for the `netstandard2.0` TFM *(already accepted by the ratified design)*. |
| B2-R2 | **Default:** `null` (the default) selects the **legacy built-in path** — the current `WebUtility.HtmlEncode` behavior — not an encoder instance. Copied by the copy-constructor path (TemplateOptions.cs lines 100–110). |
| B2-R3 | **Default behavior byte-identical:** with `Encoder == null`, every existing fixture renders byte-identically to 2.0.0 (the `WebUtility` quirks included — e.g. it encodes the Latin-1 160–255 range). The v2.0-ratified *default swap* to `HtmlEncoder.Create(UnicodeRanges.All)` slipped the 2.0 window and is byte-changing, so it is **not** performed here — it sits in the [next-window candidate register](spec/common/breaking-windows.md#next-window-candidate-register); docs recommend `HtmlEncoder.Create(UnicodeRanges.All)` as the modern opt-in. |
| B2-R4 | **Single-seam routing:** every engine site that HTML-encodes output MUST route through the effective encoder. Verified inventory to migrate: `HtmlEncodedRenderer.Render(string)` ([HtmlEncodedRenderer.cs:20](../src/Heddle/Data/HtmlEncodedRenderer.cs#L20)); the `HtmlEncodedRenderer.Render(ReadOnlySpan<char>)` bridge (upgrade per its own doc comment); and any `[EncodeOutput]`/`AbstractHtmlExtension` encode path *(enumerate at implementation: `grep -rn "HtmlEncode" src/Heddle --include="*.cs"` must return only the legacy null-encoder branch afterward — this grep is the acceptance check)*. |
| B2-R5 | **Bypass-proofing preserved:** the encode proxy remains not-an-`IUtf8ScopeRenderer` (pre-encoded UTF-8 static pieces MUST NOT flow around a custom encoder). Encoding stays chars-before-transcode (encode → transcode, never reversed), matching the existing D9 contract; `TextEncoder.EncodeUtf8` MAY be used *inside* the proxy where the sink is byte-based, provided the proxy stays the only write path. |
| B2-R6 | **Cache identity:** `Encoder` participates in `TemplateOptions.Equals`/`GetHashCode` **by reference** (a different encoder instance ⇒ different rendered bytes ⇒ different cache key; G-R5). Document in csharp-api.md's options table ("participates in Equals/GetHashCode"). |
| B2-R7 | **Precompiled fingerprint — decision rule:** the generator emits `RenderType.Encode` markers and defers value encoding to render time ([TemplateEmitter.cs:1672](../src/Heddle.Generator/Emit/TemplateEmitter.cs#L1672)); static text is never value-encoded, so a custom encoder does not change generated code ⇒ `Encoder` does **not** join `PrecompiledOptionsFingerprint`. **Condition:** this holds only if the generator never constant-folds an encoded value into a literal (including under `HeddleEmitUtf8Pieces`). Implementation MUST verify by adding a differential fixture rendered with a marker encoder (e.g. wraps every encode in `[E:…]`) over both backends; if outputs diverge, the folding site is fixed (preferred) or `Encoder` identity joins the gauntlet as a binding-style check (fallback — requires a stable encoder identity string, same mechanism as extension bindings). |
| B2-R8 | **Scope guard:** the encoder applies to *encoding* sites only. It MUST NOT run for `OutputProfile.Text` bare output, `@raw`, raw blocks, or literal text. `@profile()` switching applies the same effective encoder to its Html regions. |
| B2-R9 | **Docs/sample:** csharp-api.md options table row; built-in-extensions.md HTML-encoding section paragraph; [samples/html-safe-output](../samples/html-safe-output) gains a fourth artifact `options-encoder.html` rendered via `TemplateOptions.Encoder` (keep the `@tagged` extension as the contrasted "1.x seam"), README rewritten from "is a 2.0-window feature" to present tense; golden updated. |
| B2-R10 | **Tests:** unit — custom encoder (`HtmlEncoder.Create(UnicodeRanges.All)` and a marker `TextEncoder`) invoked for `Html`-profile bare output and `[EncodeOutput]` extensions, not invoked for `@raw`/`Text`/literals; `null` path byte-identical (B2-R3); span/UTF-8 encoder paths exercised on net8+; concurrency — one options instance, parallel renders (`TextEncoder` implementations are required to be thread-safe). Differential — B2-R7 fixture. |

### B3 — `@import()` deprecation warning `HED4003`

| ID | Requirement |
| --- | --- |
| B3-R1 | **Code:** `HED4003` (next free in the `HED4xxx` semantic-warning range; 4001/4002 verified in use). Severity: warning. |
| B3-R2 | **Message (normative):** `"'@import' is a legacy compile-time include with surprising isolation semantics; use '@<<{{ path }}' to share definitions and layouts. See docs/language-reference.md#imports."` |
| B3-R3 | **Trigger:** exactly once per `@import()` call site, positioned at the call, during compilation (emission point: `ImportExtension.InitStart` or the compiler's extension-resolution step — implementer's choice, position identical either way). `@<<` MUST NOT trigger it. |
| B3-R4 | **No suppression option.** (Precedent: other `HED` warnings have no per-warning switches; hosts filter `HeddleCompileResult` warnings themselves.) |
| B3-R5 | **Generator parity:** *(verify at implementation)* if `Heddle.Generator` surfaces engine compile warnings as build diagnostics, `HED4003` flows through unchanged; if it has a separate warning map, add it (pattern: `HED3005`/`HED7016` pairing). |
| B3-R6 | **Docs:** language-reference.md imports section gains the code reference; built-in-extensions.md `@import` entry (if present) likewise; CHANGELOG "Added" entry. |
| B3-R7 | **Tests:** warning present with position for `@import()`; absent for `@<<`; first-party fixtures/samples migrate off `@import()` or pragma-annotate intentional legacy-behavior tests (G-R6). |

---

## Workstream C — Untrusted-template gap

### C1 — Render resource budgets

**Design decision (differs from the plan sketch, with rationale).** The plan proposed exposing
an iteration counter on `Scope`. Verification showed `Scope` is a hot `readonly struct`
([Scope.cs](../src/Heddle/Data/Scope.cs)) copied at every descent — adding budget state there
taxes every render, budgeted or not. This spec instead enforces budgets **entirely at the
renderer seam**: a wrapper renderer created only when a budget is configured. Zero cost when
off; no extension-author contract change; precompiled and dynamic backends covered identically
because both write through the same seam (G-R3). Consequence: the budget counts *render
operations*, not loop iterations — a loop iteration that writes nothing is invisible to the ops
counter but is still bounded by the deadline. This evasion analysis MUST be documented (C1-R9).

| ID | Requirement |
| --- | --- |
| C1-R1 | **Public contract** (namespace `Heddle.Data`): `public sealed class RenderBudget { public long? MaxOutputChars { get; set; } public long? MaxRenderOps { get; set; } public TimeSpan? MaxRenderTime { get; set; } }` and `public RenderBudget RenderBudget { get; set; }` on `TemplateOptions` (default `null` = unlimited = today's behavior, G-R2). |
| C1-R2 | **Semantics:** `MaxOutputChars` — cumulative UTF-16 chars accepted by the render sink across the whole `Generate` call (chars, not bytes: checked before UTF-8 transcode, uniform across string/`TextWriter`/`IBufferWriter` paths). `MaxRenderOps` — cumulative count of render-write operations (each `Render(string)`/`Render(span)` call = 1 op). `MaxRenderTime` — wall-clock from `Generate` entry, checked via `Stopwatch.GetTimestamp()` deltas on every op **and at least every iteration of `@list`/`@for`** *(see C1-R4)*; no timer thread. |
| C1-R3 | **Enforcement point:** a `BudgetedRenderer` wrapping the innermost sink (`ScopeRenderer`, `Utf8ScopeRenderer`, `TextWriter` adapter), created in `HeddleTemplate.Generate` when `options.RenderBudget != null`, positioned **inside** any `HtmlEncodedRenderer` proxy so budget counts post-encoding chars (what actually lands in output). It implements the same interface set as the wrapped sink minus `IUtf8ScopeRenderer` only if that would open a bypass *(verify seam composition at implementation — the invariant is: no write path reaches the sink without passing the budget check)*. |
| C1-R4 | **Empty-loop coverage:** because a zero-output loop performs no ops, `@list`/`@for` MUST perform a deadline check per iteration when a budget with `MaxRenderTime` is active. Mechanism: the loop extensions consult a per-render budget state reachable without changing the `Scope` struct layout — sanctioned options: (a) the renderer they already hold implements an internal `IBudgetProbe` the loops type-test once before the loop, or (b) the existing `Scope.Publish`/`TryRead` local-context channel carries the probe. Option (a) preferred (no per-iteration dictionary access). This is the only extension-code change in C1 and touches exactly [ListExtension.cs](../src/Heddle/Extensions/ListExtension.cs) and [ForIndexExtension.cs](../src/Heddle/Extensions/ForIndexExtension.cs). |
| C1-R5 | **Breach behavior:** throw `public class TemplateRenderBudgetException : TemplateProcessingException` (namespace `Heddle.Exceptions`) carrying `public RenderBudgetKind Kind { get; }` (`OutputChars`/`RenderOps`/`RenderTime` enum), `public long Limit { get; }`, `public long Observed { get; }`. Message pattern: `"Render budget exceeded: {Kind} limit {Limit}, observed {Observed}."` **No template position** — the renderer seam has none; recording positions per op would tax the hot path. This limitation is documented (deviation from the plan's sketch, accepted). |
| C1-R6 | **Streaming caveat (documented, not "fixed"):** on the `TextWriter`/`IBufferWriter` paths, output already written stays written — the caller owns the sink and MUST treat `TemplateRenderBudgetException` as "abort the response." Goes in csharp-api.md streaming section. |
| C1-R7 | **Thread model:** budget state is per-`Generate`-call (lives in the wrapper), never shared; concurrent renders of one template each get their own state. Matches the existing "sink is single-owner for the duration of the call" contract. |
| C1-R8 | **Identity:** `RenderBudget` participates in **neither** `TemplateOptions.Equals`/`GetHashCode` **nor** the precompiled fingerprint — it changes no compiled structure and no bytes of a successful render (G-R5; precedent `MaxRecursionCount`). Rationale comment goes on the property. |
| C1-R9 | **Docs:** csharp-api.md options table + a "Render budgets" subsection (semantics, evasion analysis: ops≠iterations, deadline as the backstop; streaming caveat); the C3 untrusted-templates checklist lists budgets as mandatory. |
| C1-R10 | **Sample:** [samples/sandboxed-user-templates](../samples/sandboxed-user-templates) gains a hostile-template scenario (e.g. `@for(range(0, 1000000000)){{x}}`) stopped by each budget kind; captured artifact records the exception kind; golden updated. |
| C1-R11 | **Tests:** unit per budget kind × per sink path (string/TextWriter/IBufferWriter) × breach and just-under-limit; empty-loop deadline test (zero-output loop terminates via `MaxRenderTime`); recursion interplay (budget fires independent of `MaxRecursionCount`); **perf gate** — BenchmarkDotNet run of `TextRenderBenchmarks` before/after MUST show no regression with `RenderBudget == null` (the wrapper must not exist on that path); differential fixtures per G-R3 (budget-completing render byte-identical on both backends; budget-breaching render throws the same exception kind on both). |

### C2 — Context-encoding extensions `@attr`, `@js`, `@url`

**Pattern.** Three new built-in extensions following the verified `StringExtension` shape
([StringExtension.cs](../src/Heddle/Extensions/StringExtension.cs)): `[ExtensionName]`, value-call
context semantics (parameter is the value; body steps back to caller's context), body = default
value when the model is `null` (mirroring `@string`).

| ID | Requirement |
| --- | --- |
| C2-R1 | **Common behavior:** input is any object; `null` model → render body-as-default in the parent scope if a body exists, else empty. Non-string input is stringified with `Convert.ToString(value, CultureInfo.InvariantCulture)` first (matching `@string`), then escaped per the tables below. All escaping is allocation-conscious (scan-then-copy; return the original string when no character needs escaping). |
| C2-R2 | **`@attr(value)` — HTML attribute context.** Escape set (normative): `&`→`&amp;`, `<`→`&lt;`, `>`→`&gt;`, `"`→`&quot;`, `'`→`&#39;`. This is a **superset** of the default encoder (which does not escape `'`), so `@attr` output is safe in single- and double-quoted attributes. Profile interplay: marked as an *encoding leaf* — under `OutputProfile.Html` its output MUST NOT be re-encoded (mechanism: same encoded-leaf marking `[EncodeOutput]` extensions use). |
| C2-R3 | **`@js(value)` — JS string-literal context.** Escape set (normative): `\`→`\\`, `"`→`\"`, `'`→`\'`, `` ` ``→``\` ``, U+000A→`\n`, U+000D→`\r`, U+2028→` `, U+2029→` `, `<`→`<` (blocks `</script>` breakout), `&`→`&`, and all C0 controls→`\u00XX`. Output is emitted **raw with respect to the Html profile** (it targets a `<script>`/handler context where HTML entity encoding would corrupt the value) — mechanism: the same raw-leaf pathway `@raw` uses *(verify the exact marking at implementation)*. Documentation MUST state it produces the *contents* of a JS string literal — the author writes the surrounding quotes. |
| C2-R4 | **`@url(value)` — URL component context.** Semantics of `Uri.EscapeDataString` (RFC 3986: unreserved characters kept, everything else percent-encoded, UTF-8 based). Output contains no HTML-special characters by construction; marked as an encoding leaf like `@attr` for uniformity. Documentation MUST state it encodes a *component* (query value, path segment), not a whole URL. |
| C2-R5 | **Naming collisions:** *(verify at implementation)* none of `attr`/`js`/`url` collides with an existing extension or default registered function; they are ordinary `[ExtensionName]` extensions, overridable via `[ExtensionReplace]` like any built-in. |
| C2-R6 | **Docs:** built-in-extensions.md gains a new "Encoding contexts" group with the three entries **plus** a normative context table: element text → profile/`@string`; attribute value → `@attr`; JS string → `@js`; URL component → `@url`; trusted markup → `@raw`. The HTML-encoding section states plainly that the Html profile covers the element-text context only. |
| C2-R7 | **Tests:** per extension — escape-set exhaustive test (every listed char + a passthrough string + null model with/without body + non-string model); profile interplay tests (`Html` profile: `@attr`/`@url` not double-encoded, `@js` not entity-encoded); golden fixture in the corpus (G-R3: precompiled twin — *(verify at implementation)* whether built-in bodied value-calls precompile; if these fall to the dynamic tier like custom branch sets, the fixture asserts the documented fallback instead). |
| C2-R8 | **Precompiled binding:** new built-ins join the extension-binding set the gauntlet checks; a 2.x-generator + older-engine pairing behaves per the existing compatibility rule (dynamic-tier fallback), noted in CHANGELOG. |

### C3 — Model-exposure guidance (docs only)

| ID | Requirement |
| --- | --- |
| C3-R1 | New section in [patterns.md](patterns.md): **"Exposing models to untrusted templates"**. Required content: (1) getters execute — expose dedicated DTOs, never live domain/EF objects; (2) `[Hidden]` — what it filters and its member-tier scope; (3) the `FunctionRegistry` freeze semantics and why registration is the entire trust boundary; (4) `ExpressionMode.Native` as the required mode; (5) render budgets (link C1) as the availability leg; (6) encoding contexts (link C2). |
| C3-R2 | The section ends with a copy-paste **checklist** (mode, registry, DTOs, budgets, profile, encoding) and is linked from: native-expressions.md "The sandbox" section, csharp-api.md, and [samples/sandboxed-user-templates](../samples/sandboxed-user-templates) README. |

---

## Workstream D — Benchmark credibility

### D1 — Competitor benchmark twins

| ID | Requirement |
| --- | --- |
| D1-R1 | **Scope:** [src/Heddle.Performance](../src/Heddle.Performance) only. New `PackageReference`s (`Fluid.Core`, `Scriban`, `DotLiquid`, `Handlebars.Net` — latest stable at implementation time, versions pinned) land in the benchmark csproj exclusively; the engine gains no dependencies. |
| D1-R2 | **Twins:** one runner per engine under `Runners/` (`FluidTest.cs`, `ScribanTest.cs`, `DotLiquidTest.cs`, `HandlebarsTest.cs`) mirroring the existing `RazorTest.cs` pattern, rendering the same composed home page (layout + reusable sections + component calls + a list loop) against the same model as `TextRenderBenchmarks`. Feature mapping table (Heddle definition ↔ Liquid include/render ↔ Scriban function ↔ Handlebars partial) goes in a `Runners/README.md` so twin fidelity is reviewable. |
| D1-R3 | **Parity rule:** before timing, each twin's output MUST be asserted *semantically identical* to Heddle's — byte-identical after a single documented normalization (collapse runs of whitespace between tags). The assertion runs in the benchmark's `GlobalSetup` so a drifted twin fails loudly rather than benchmarking different work. |
| D1-R4 | **Dimensions:** render time + allocations (`[MemoryDiagnoser]`, existing) for all engines; parse/compile cost added to `TemplateParseBenchmarks` for all engines (cold parse; and cached-render where the engine caches). |
| D1-R5 | **Baseline:** Heddle is `[Benchmark(Baseline = true)]` in the render suite so ratios read directly. |

### D2 — Published numbers

| ID | Requirement |
| --- | --- |
| D2-R1 | README "Performance" section is rewritten around a results table: engine × (mean, allocated), plus a compile-cost table, with environment header (CPU, OS, .NET version, BenchmarkDotNet version, date, commit). Same table mirrored in architecture.md's performance section or linked from it. |
| D2-R2 | **Claim wording rule:** every comparative claim carries a number and a date; unquantified superlatives ("faster", "fewer allocations") without an adjacent table are forbidden in README/docs. |
| D2-R3 | Raw BenchmarkDotNet artifacts for the published run are committed under a `docs/benchmarks/<date>/` folder (or linked release asset) so results are auditable; the README states the one-line repro command (`dotnet run -c Release --project src/Heddle.Performance`). |

### D3 — Community benchmark entries (committed; external dependency)

| ID | Requirement |
| --- | --- |
| D3-R1 | **Primary target:** the Fluid repository's benchmark suite (the de-facto .NET template-engine comparison page). Deliverable: an upstream PR adding a Heddle entry implementing their existing scenario with idiomatic Heddle (definitions for partials, `@list` for loops), engine version pinned, zero changes to their methodology. |
| D3-R2 | **Secondary targets (best-effort):** any curated template-engine benchmark list accepting entries (e.g. awesome-template-engine style indexes) — tracked, not gating. |
| D3-R3 | **Fallback:** if upstream declines or stalls > 60 days, publish the same scenario **in their harness, unmodified**, under `src/Heddle.Performance/ThirdParty/` with a README naming the upstream harness commit — so the numbers remain third-party-methodology even if not third-party-hosted. |
| D3-R4 | **Tracking:** external item; the plan tracker links the upstream PR; done = merged upstream **or** fallback published. |

---

## Workstream E — On-ramp documentation

### E1 — Translation pages

| ID | Requirement |
| --- | --- |
| E1-R1 | Two new pages: `docs/coming-from-razor.md`, `docs/coming-from-liquid.md` (the latter covers Jinja/Twig readers too — say so in its intro). Both join the A3.1 map and site nav, linked from getting-started.md's intro. |
| E1-R2 | **coming-from-razor.md required sections:** context is relative, not `Model.X` (with the `::` escape); sections/layouts → definitions + `@<<` (the directionality flip, with the same layout example expressed both ways); `@:` means *raw line* in Heddle, not Razor's text-in-code-block; where C# is allowed (three tiers vs Razor's everywhere); what has no equivalent (tag helpers, view components → extensions). Each section: two side-by-side snippets, three sentences max of prose. |
| E1-R3 | **coming-from-liquid.md required sections:** `{{ }}` is a *body*, not interpolation (the #1 misread — first section, with a wrong/right pair); filters → chains **including the direction flip** (`x | a | b` reads left-to-right; `@b():a()` composes right-to-left — show the same pipeline both ways); `include`/`render` → `@<<` vs `@partial()`; `for`/`forloop.index` → `@list` + chained index via `@out()`; tags → extensions; what Liquid authors must unlearn (whitespace dashes → `@\`/`TrimDirectiveLines`). |
| E1-R4 | Every snippet on both pages MUST be a compiling template (add to the docs-verification fixture set if one exists; otherwise hand-verify and note the engine version). |

### E2 — Descend-vs-step-back table

| ID | Requirement |
| --- | --- |
| E2-R1 | One glanceable table listing **every built-in extension** with a *Body context* column — values: `descends into parameter`, `steps back to caller`, `no body` / directive — plus the chained-value column (what `@out()` sees). Population source: the per-extension `[DataType]`/behavior in [src/Heddle/Extensions](../src/Heddle/Extensions) — classification MUST be read from source at implementation, not copied from prose. |
| E2-R2 | **Location:** built-in-extensions.md, immediately after "How to read these entries"; language-reference.md's "Stepping back" section links to it as the authoritative enumeration (single source — do not duplicate the table). |

### E3 — Multi-slot pattern recipe

| ID | Requirement |
| --- | --- |
| E3-R1 | patterns.md gains **"Components with multiple content regions"**: the sanctioned pattern for what other frameworks solve with multiple named slots. Content: a `<page_shell>` example with two regions expressed as sibling definitions (`<shell_header>`, `<shell_footer>`) that call sites override via `<shell_header:shell_header>`, plus the one-typed-slot `@out()` for the main region; a short paragraph stating explicitly that one typed slot per definition is a language rule (link the `HED5017` reference) and that region-overrides are the multi-slot idiom, not a workaround. |

### E4 — Culture / i18n recipe

| ID | Requirement |
| --- | --- |
| E4-R1 | patterns.md gains **"Culture and localization"**: culture lives on the root model (`@(::Site.Culture…)` feeding format patterns — extend the existing `@date` hint into a full example); per-call locale parameters on `@money`/`@date`/`@time` (document the actual parameter shape from source — MoneyExtension's locale handling is the reference); registering culture-aware host functions (`FunctionRegistry`) for anything beyond formatting (message lookup, pluralization) with a worked `t(key)` example; a closing note that built-ins are invariant-culture *by design* (determinism, golden-testability) and the root-culture pattern is the sanctioned escape. |

---

## Traceability

| Plan item | Spec section | New public surface | New diagnostics | Docs touched |
| --- | --- | --- | --- | --- |
| A1.1–A4.2 | WS-A | — | — | README, docs/README, csharp-api, precompilation, built-in-extensions, language-reference, native-expressions, getting-started, samples/README, site nav |
| B1 | B1-R1…R6 | `[Obsolete]` on `AllowCSharp` | CS0618 (BCL) | native-expressions, csharp-api |
| B2 | B2-R1…R10 | `TemplateOptions.Encoder` (`System.Text.Encodings.Web.TextEncoder`) | — | csharp-api, built-in-extensions, html-safe-output sample |
| B3 | B3-R1…R7 | — | **HED4003** | language-reference, built-in-extensions, CHANGELOG |
| C1 | C1-R1…R11 | `RenderBudget`, `TemplateOptions.RenderBudget`, `TemplateRenderBudgetException`, `RenderBudgetKind` | — | csharp-api, patterns, sandboxed sample |
| C2 | C2-R1…R8 | `@attr`, `@js`, `@url` extensions | — | built-in-extensions, CHANGELOG |
| C3 | C3-R1…R2 | — | — | patterns + cross-links |
| D1–D3 | WS-D | — (bench project only) | — | README, architecture, Runners/README |
| E1–E4 | WS-E | — | — | two new pages, built-in-extensions, patterns, docs map |

**Release mapping:** Phase 1 (WS-A, B1, B3) — patch/docs release; Phase 2 (B2, C2, C3, D1, D2,
E1–E4) — minor release **2.1.0** (new public surface, no breaks); Phase 3 (C1, D3) — minor
release **2.2.0**. All additions honor G-R2; nothing here requires a 3.0.

**Closed decisions** (formerly the open-decisions list; closed per the
[no-open-questions rule](spec/common/spec-conventions.md#no-open-questions)):

1. **Encoder shape — closed by prior ratification.** `TemplateOptions.Encoder` is a
   `System.Text.Encodings.Web.TextEncoder` (B2-R1), the seam the v2.0 effort ratified for
   window item 2 (recovered in the [2.0 window record](spec/records.md#the-20-breaking-window--as-shipped-record));
   a custom abstract class or `Func<string,string>` would duplicate a BCL contract that
   already carries the span/UTF-8 paths this engine needs.
2. **C1-R4 probe mechanism — option (a)** (renderer type-test via an internal `IBudgetProbe`,
   one type-test before the loop). Most-reversible choice: it is internal, touches two
   extensions, and can migrate to the `Publish`/`TryRead` channel
   ([D6](spec/common/cross-cutting-decisions.md#d6--the-scope-publishread-channel-is-public-api))
   without public-surface change. Revisit trigger: a third extension needing the probe.
3. **C2-R3 `@js` raw-leaf marking** — reclassified as a *(verify at implementation)* fact,
   not a decision: the requirement (output not entity-encoded under the Html profile) is
   decided; only the mechanism must be confirmed against `@raw`'s implementation.
4. **D1-R1 competitor list — RazorLight is not added.** The in-suite Razor twin already
   covers the Razor comparison; RazorLight measures a hosting wrapper, not an engine.
   Revisit trigger: the suite gaining an out-of-ASP.NET hosting scenario.
