# Phase 3 — html-context-encoding-lint

## Header

- **Status:** ready — Plan DoR
- **Goal (one line):** Under an explicitly declared HTML output profile, emit a warning-tier diagnostic when a bare `@(value)` output sits in an HTML attribute / `<script>` / URL position without the matching context encoder, so authors don't silently element-text-encode in a non-element context.
- **Depends on:** The existing output-profiles / encoding work (`OutputProfile.Html`, `@profile(html)`, and the `@attr`/`@js`/`@url` encoders) — all already shipped; this phase adds a diagnostic on top of them.
- **Changes an externally-visible contract:** yes — it adds one new compile-time diagnostic id (candidate `HED2004`, warning severity) to the public diagnostics surface. It changes no rendered output and no runtime behavior.

## Goal

Heddle 2.0 makes bare `@(value)` element-text-encode by default under `OutputProfile.Html`, and ships per-context encoders — `@attr`, `@js`, `@url` — for the attribute, JavaScript-string, and URL-component contexts where element-text encoding is insufficient or actively wrong (see [built-in-extensions.md → Encoding contexts](../built-in-extensions.md#encoding-contexts)). Today those encoders are purely a matter of *author discipline*: someone who writes `<a title="@(Tooltip)">` gets element-text encoding in an attribute context and receives no signal that they picked the wrong context. The assessment names this — "a template author who writes `<a title="@(Tooltip)">` gets element-text encoding in an attribute context with no diagnostic" — as the single most valuable security improvement the engine could make ([§3.8](../language-assessment.md), [§8 item 2](../language-assessment.md)).

This phase delivers the tractable, additive slice of closing that gap: a compile-time **warning** that flags a bare `@(value)` output whose immediately adjacent literal text places it in an attribute (fast-follow: `<script>` and URL) position, and points the author at the matching encoder. The user-visible outcome is a diagnostic in the compiler and editor — nothing else. It renders every existing template to the exact same bytes it does today, and it fires only when the author has explicitly declared an HTML output context. It is a lint, not a fix: it never selects or applies an encoder for the author.

## Non-goals / scope boundary

Per the standing rulings, this phase is **diagnostic-only** and deliberately excludes:

- **Auto-escaping / auto-selecting an encoder.** The lint never rewrites `@(value)` into `@attr(value)`, and never changes which encoder runs. Full contextual auto-escaping in the Go `html/template` style is out of scope **permanently** (ruling R4): it changes rendered bytes and is therefore not additive. This phase plans no path toward it.
- **Any HTML parsing.** The engine has no HTML model and this phase adds none (see [Design direction](#design-direction)). The scan is a local heuristic over literal characters, not a tokenizer.
- **Transitive / cross-call context propagation.** The lint sees only the literal text physically adjacent to an output block *in its own body*. It cannot know that a definition's `@(value)` output lands inside an attribute three call-layers up. This is a **single-hop** lint by construction.
- **CSS-context encoding**, and **any behavior under `OutputProfile.Text`, CSV, JSON, plain/whitespace-significant, or byte output** — the lint is silent for all of these (ruling R3).

The seam this marks to later phases: a richer *explicit* context signal (an editor/integration hint distinct from the `Html` profile), and additional covered positions (CSS, further sub-contexts), can be layered on the same gate later without reopening this phase. Transitive/per-context modelling is explicitly *not* that seam — it is ruled out as non-additive (see [Design direction](#design-direction)).

## Design direction

**Chosen approach: a local, best-effort heuristic scan of the literal text adjacent to each output block, gated strictly on an explicitly declared HTML context.**

Why this shape, and why it beats the alternatives:

- **Context must be explicit (ruling R3, the crux).** Heddle is a universal, byte-significant text engine — it generates HTML, CSS, CSV, JSON, whitespace-significant plain text, and mixed/byte output. It has exactly one notion of output context: the document-scoped `OutputProfile` (`Text` / `Html`), set by the host or by `@profile()` ([built-in-extensions.md → Output profiles](../built-in-extensions.md#output-profiles)). There is no positional HTML context in the engine. Therefore this lint fires **only** when an HTML context is explicitly declared — `OutputProfile.Html` or `@profile(html)` — and is **never** inferred from content and **never** active under `Text`/CSV/byte output. A `<a title="@(X)">` string under `OutputProfile.Text` may well be CSV, a code generator, or literal documentation *about* HTML; the engine has no license to assume otherwise.

- **The engine has no HTML model, and this phase does not build one.** A read-only audit (a scoper verified this) confirms the 13-mode lexer is a *Heddle-syntax* lexer — it tokenizes `@`, `{{`, `::`, and embedded C#; HTML characters are opaque TEXT to it. There is no `AttributeContext`/`ScriptContext` notion anywhere in the engine. Building a real HTML tokenizer, quote-balancer, and tag-nesting model — the substrate Go's `html/template` uses — is a large, non-additive change, and the abstract-definition **monomorphization** model (a section is compiled once per call-site model shape, [§3.5](../language-assessment.md)) means a per-context type-flow model would have to be threaded through every call shape. That is exactly why a *lint* is the tractable tier and a stronger per-context model is not additive.

- **The needed substrate already exists, at zero render-path cost.** The same audit confirms the raw literal characters immediately adjacent to each output block *are* available at compile time — the compiler already builds literal gap-pieces (via `document.Substring` around each output block's position) to assemble the template. A best-effort scan of those adjacent literal pieces reuses the identical substrate as existing position- and string-based lints — the `HED3001` stripped-gap warning and the `HED4002` double-render warning ([cross-cutting-decisions.md → diagnostic registry](../spec/common/cross-cutting-decisions.md)) — and adds nothing to the render path.

- **Precedent and the honest trade-off.** OWASP per-context output-encoding guidance and Heddle's own `@attr`/`@js`/`@url` split establish that attribute/script/URL are distinct contexts requiring distinct encoders. Go's `html/template` closes the same gap by *parsing* the surrounding context and auto-picking the escaper ([contextual autoescaping](https://blog.vghaisas.com/go-autoescaping/)); that is the gold standard, but it changes rendered bytes and needs the HTML model Heddle lacks — so it is the contrast, not the target. This phase deliberately trades completeness for additivity: a heuristic scan accepts **real false positives** (e.g. an output inside an HTML comment, `<!-- @(x) -->`, that the scan cannot distinguish) and **false negatives** (anything the local scan cannot see, including every cross-call case), in exchange for being warning-only, byte-neutral, and shippable on the existing substrate.

**Diagnostic id.** Candidate `HED2004`, the next free id in the `HED2001`–`HED2003` output-profiles / encoding family owned by [built-in-extensions.md#html-encoding](../built-in-extensions.md#html-encoding) (confirmed free by audit; see [Open questions](#open-questions) Q4).

## Dependencies & ordering

**Must land first (already shipped, not part of this phase):** the output-profile machinery (`OutputProfile.Html`, the `@profile()` directive), the `@attr`/`@js`/`@url` encoders the diagnostic recommends, and the `@raw` opt-out the author uses to silence a false positive. This phase is purely additive on top of them.

**Ordering within the phase:** attribute-position detection first; `<script>`- and URL-position detection as a fast-follow on the same gate and substrate (see [Open questions](#open-questions) Q1). Each position is independently shippable because each reuses the identical gate and adjacent-literal substrate.

**What this phase unblocks:** it is the concrete, additive down-payment on the assessment's "highest-value gap" ([§8 item 2](../language-assessment.md)). It also establishes the *explicit-context gate* as a reusable seam that a future richer explicit-context signal, or additional covered contexts, can extend without re-deciding R3.

## Back-compat / impact

- **Rendered output:** unchanged for every template under every profile. This phase adds no auto-escaping and touches no emitting leaf — zero rendered bytes change (ruling R3, R4). This is a measurable success criterion below.
- **Compilation success:** unchanged. The diagnostic is warning-tier and must **never** block compilation (ruling R5). Every template that compiles today still compiles.
- **Public surface:** one addition — the `HED2004` diagnostic id and its message join the public diagnostics registry. New warnings on existing templates are possible (that is the point), but they are advisory only; a build that treats warnings as errors would see them, which is the standard cost of any new lint and is bounded by the conservative-by-default posture (see [Open questions](#open-questions) Q2).
- **`OutputProfile.Text` and all non-HTML output:** no impact whatsoever — the lint is inert there by construction.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
|---|---|---|
| False positives from the heuristic (e.g. `<!-- @(x) -->`, an output already inside a comment or an odd quoting shape) annoy authors and erode trust in the lint. | Conservative, high-precision default (favor recall loss over precision loss); make it silenceable per-site by using the explicit encoder or `@raw`; document the known false-positive shapes honestly as a stated limit. | M |
| Authors over-trust the lint and read "no warning" as "safe", not seeing the false negatives (especially every cross-call case). | State the single-hop limit explicitly in the diagnostic docs; frame it as a best-effort aid, not a guarantee; never imply completeness. | M |
| Context creep — pressure to grow the heuristic toward real parsing or cross-call tracking, re-litigating R3/R4. | Scope boundary and non-goals pin the heuristic/single-hop nature; the monomorphization argument documents *why* the stronger model is non-additive. | S |
| The lint accidentally fires under `Text` / CSV / byte output, breaking byte-neutral universal-engine promise. | Gate strictly on the explicit `Html` profile; a measurable success criterion asserts zero firings under `Text` for any input. | S |
| A new warning breaks a downstream "warnings-as-errors" build. | Warning-tier only; bounded by conservative default; suppressible via the encoder or `@raw`; standard, documented cost of any additive lint. | S |

## Success criteria

Measurable, checkable statements a spec could turn into a test.

- [ ] **R3 — inert off-HTML:** Under `OutputProfile.Text` (and any non-`Html` profile), the lint fires **zero** `HED2004` diagnostics for **every** input in the test corpus, including inputs containing `<a title="@(X)">`-shaped literals.
- [ ] **R3 — content never infers context:** No `HED2004` is ever produced from content inspection alone; every firing is conditioned on an explicitly declared `Html` context (`OutputProfile.Html` / `@profile(html)`).
- [ ] **Attribute positive:** Under `OutputProfile.Html`, `<a title="@(X)">` produces exactly one `HED2004` warning whose message names the attribute context and suggests `@attr`.
- [ ] **Element-text negative:** Under `OutputProfile.Html`, `<p>@(X)</p>` produces **no** `HED2004` warning.
- [ ] **Silenceable:** Under `OutputProfile.Html`, replacing the bare output at a flagged site with the matching encoder (`@attr`/`@js`/`@url`) or with `@raw(...)` produces **no** `HED2004` warning at that site.
- [ ] **Byte-neutral:** For every template in the corpus, rendered output is byte-identical before and after this phase, under **both** profiles (diagnostic-only; no emitting leaf touched).
- [ ] **Never blocks compilation:** `HED2004` is warning severity; every template that compiled before still compiles, and compilation succeeds even when `HED2004` fires.
- [ ] **Single-hop honesty:** A documented false-negative case (a definition body `@(X)` whose output lands in an attribute at the call site) produces **no** warning, and this limit is stated in the diagnostic's documentation.

## Validation scenarios

| Input | Expected outcome |
|---|---|
| `OutputProfile.Html`: `<a title="@(X)">…</a>` | One `HED2004` warning, attribute context, suggests `@attr`. Compilation succeeds. |
| `OutputProfile.Html`: `<a title="@attr(X)">…</a>` | No warning (correct encoder already used). |
| `OutputProfile.Html`: `<p>@(X)</p>` | No warning (element-text context — the default encoder is correct). |
| `OutputProfile.Html`: `<a title="@raw(X)">…</a>` | No warning (explicit author opt-out). |
| `OutputProfile.Html`: `<script>var n = "@(X)";</script>` | `HED2004` suggesting `@js` (script fast-follow; see Q1). |
| `OutputProfile.Html`: `<a href="/s?q=@(X)">` | `HED2004` suggesting `@url` (URL fast-follow; see Q1). |
| `OutputProfile.Html`: `<!-- @(X) -->` | Known false positive — a `HED2004` fires though the output is in a comment; documented as a stated heuristic limit. |
| `OutputProfile.Html`: definition body `{{ @(X) }}` invoked as `<a title="@article_title()">` | No warning at the definition (single-hop limit — documented false negative). |
| `OutputProfile.Text`: `<a title="@(X)">…</a>` | **No** warning — the lint never fires off the `Html` profile (R3). |
| `OutputProfile.Text`: any input in the corpus | Zero `HED2004` diagnostics. |

## Open questions

None — resolved (see [open-questions.md](open-questions.md)). Every P3 question (Q1–Q5) is folded into the design direction, risks, and success criteria above.

## External grounding

| Claim | Source |
|---|---|
| Per-context encoders exist (`@attr`/`@js`/`@url`) and element-text encoding is insufficient/wrong in attribute, script, and URL positions. | [built-in-extensions.md → Encoding contexts](../built-in-extensions.md#encoding-contexts) |
| Output context in Heddle is document-scoped `OutputProfile` (`Text`/`Html`), set by host or `@profile()` — there is no positional HTML context. | [built-in-extensions.md → Output profiles](../built-in-extensions.md#output-profiles) |
| The manual-context-encoding gap is the assessment's single highest-value security improvement, and `<a title="@(Tooltip)">` gets element-text encoding in an attribute context with no diagnostic today. | [language-assessment.md §3.8](../language-assessment.md), [§8 item 2](../language-assessment.md) |
| `HED2001`–`HED2003` is the output-profiles / encoding diagnostic family; `HED2004` is the next free id in it. | [built-in-extensions.md → HTML encoding](../built-in-extensions.md#html-encoding), [cross-cutting-decisions.md → diagnostic registry](../spec/common/cross-cutting-decisions.md) |
| Existing position/string-based lints (`HED3001` stripped-gap, `HED4002` double-render) share the substrate this lint reuses. | [built-in-extensions.md → Branch sets](../built-in-extensions.md#branch-sets), [cross-cutting-decisions.md](../spec/common/cross-cutting-decisions.md) |
| Full contextual auto-escaping (the contrast) parses surrounding HTML/JS/URI context and auto-picks the escaper — the gold standard, but it changes rendered bytes and needs an HTML model Heddle lacks, so it is out of scope here (R4). | [Go `html/template` contextual autoescaping](https://blog.vghaisas.com/go-autoescaping/), [Google security blog — automatic XSS reduction](https://security.googleblog.com/2009/03/reducing-xss-by-way-of-automatic.html) |
