# Heddle Improvement Plan — Minimal Change, Maximum Outcome

*Companion to the [Language & Engine Assessment](language-assessment.md). Every item below was
verified against the 2.0.0 source before being listed — nothing here is speculative. The plan is
ordered by leverage: what closes the largest assessment gap for the smallest, least invasive
change. File/line references point at the exact drift.*

> **Status: ALL items approved for implementation (2026-07-11)** — including D3 (community
> benchmarks), which is promoted from stretch to committed scope. Track progress in the
> [Implementation tracker](#implementation-tracker) below.

---

## Implementation tracker

Every item is in scope. Check off as each lands; an item is "done" when its workstream's exit
criteria hold for it.

**Phase 1 — hygiene (docs + trivial code):**

- [ ] A1.1 README stale "no `else`/`elif`" claim + comparison-table sweep
- [ ] A1.2 csharp-api.md `OutputProfile` default → `Html`
- [ ] A1.3 csharp-api.md `TrimDirectiveLines` default → `true`
- [ ] A1.4 precompilation.md MSBuild defaults → `Html`/`true`
- [ ] A1.5 built-in-extensions.md 2.0-window tense sweep (lines 274/460/477)
- [ ] A1.6 language-reference.md 2.0-window tense sweep (lines 215/1093–1094/1138)
- [ ] A2.1 native-expressions.md `AllowCSharp` obsolescence wording (resolved by B1)
- [ ] A2.2 html-safe-output README `TemplateOptions.Encoder` wording (resolved by B2)
- [ ] A3.1 docs/README.md documentation map: add native-expressions, precompilation, editor-support
- [ ] A3.2 Package tables complete (Heddle.Generator, Heddle.LanguageServer, LanguageServices)
- [ ] A3.3 Docs site nav aligned with A3.1
- [ ] A4.1 Drop gratuitous `AllowCSharp = true` from hello-world examples + sweep
- [ ] A4.2 Dangling `docs/spec/`/`docs/archive/` link sweep after archive removal lands
- [ ] B1 `[Obsolete]` on `AllowCSharp` + warning-clean first-party sweep
- [ ] B3 `@import()` deprecation warning (new `HED4xxx`)

**Phase 2 — additive features + credibility:**

- [ ] B2 `TemplateOptions.Encoder` (byte-identical default; fingerprint check vs `HeddleEmitUtf8Pieces`)
- [ ] C2 `@attr` / `@js` / `@url` context-encoding extensions + encoding-contexts doc section
- [ ] C3 Model-exposure guidance (DTOs, getters, `[Hidden]`) collected in one place
- [ ] D1 Benchmark twins: Fluid, Scriban, DotLiquid, Handlebars.Net (compile + render)
- [ ] D2 Publish quantified numbers in README Performance section
- [ ] E1 "Coming from Razor" + "Coming from Liquid/Jinja" translation pages
- [ ] E2 Descend-vs-step-back rule as a first-class per-extension table
- [ ] E3 Multi-slot pattern recipe in patterns.md
- [ ] E4 Culture/i18n recipe ("culture lives on the root model")

**Phase 3 — engine + ecosystem:**

- [ ] C1 Render resource budgets (`RenderBudget`: MaxOutputChars, MaxIterations, Deadline) + sandboxed sample + differential fixtures
- [ ] D3 Community benchmark entries (third-party harnesses, e.g. Fluid's) — **committed**

---

## Guiding principles

1. **Additive only.** No item in this plan changes the grammar, the rendering semantics, or the
   byte output of any existing template. Anything that would is listed under
   [Non-goals](#non-goals--things-deliberately-not-fixed).
2. **Protect the five pillars.** The assessment identified what makes Heddle unique; none of it
   may be diluted: (a) abstract late-bound definitions with per-call-site typing, (b) role-based
   branch sets, (c) the three-tier expression surface with compile-time sandbox, (d) the
   byte-identical precompilation differential, (e) the unified `chained` channel.
3. **Ship what was already promised.** Three features are *documented as shipping in the 2.0
   window* but absent from 2.0.0. Keeping documented promises outranks new ideas.
4. **Docs are release artifacts.** Half of this plan is documentation drift; every fix is
   cheap, zero-risk, and directly improves reviewer/adopter perception (the assessment's
   "reviewers notice" point).
5. **Use the architecture to fix the architecture.** Where a capability gap exists (contextual
   encoding, culture), the fix is a *new extension or option* — exactly the extensibility story
   the language sells — never a new grammar construct.

---

## Workstream A — Documentation drift (P0: zero risk, highest credibility-per-hour)

All verified against source on this branch. Each is a one-line to one-paragraph edit.

### A1. Stale claims that contradict shipped 2.0 features

| # | Location | Current text (verified) | Fix |
| --- | --- | --- | --- |
| A1.1 | [README.md:82](../README.md#L82) | "…dense sigils and no `else`/`elif` (compose `@if`/`@ifnot` instead)" | 2.0 ships `@elif`/`@else` branch sets. Replace with an honest current trade-off (e.g. "dense sigils and a per-extension context rule"). Also re-check the comparison table two paragraphs above for the same staleness. |
| A1.2 | [docs/csharp-api.md:177](csharp-api.md#L177) | `OutputProfile` default documented as `Text` | Code default is `Html` ([TemplateOptions.cs:81](../src/Heddle/Data/TemplateOptions.cs#L81)). Update the default column and reword "the 1.x default" framing. |
| A1.3 | [docs/csharp-api.md:178](csharp-api.md#L178) | `TrimDirectiveLines` default documented as `false`, "flips to `true` in the 2.0 window" | Code default is `true` ([TemplateOptions.cs:82](../src/Heddle/Data/TemplateOptions.cs#L82)). State `true` as the default; describe `false` as the 1.x-compat setting. |
| A1.4 | [docs/precompilation.md:58](precompilation.md#L58), [:60](precompilation.md#L60) | MSBuild table: `HeddleOutputProfile` "`Text` (default)", `HeddleTrimDirectiveLines` "`false` (default)" | Generator defaults are `Html`/`true` ([Heddle.Generator.props:23,25](../src/Heddle.Generator/build/Heddle.Generator.props), [ConfigReader.cs:27,31](../src/Heddle.Generator/Pipeline/ConfigReader.cs)). Update both rows. |
| A1.5 | [docs/built-in-extensions.md:477](built-in-extensions.md#L477) | "the default profile stays `Text` in 1.x. In the 2.0 window it flips to `Html`" (future tense) | 2.0 is current. Rewrite in past/present tense: "`Html` since 2.0; `Text` was the 1.x default." Sweep lines 274 and 460 ("the 1.x default") for the same tense fix. |
| A1.6 | [docs/language-reference.md:1093–1094](language-reference.md#L1093-L1094) | "**Default:** `false` in 1.x … It flips to `true` in the 2.0 window" | Same tense fix: default is `true` now. Also sweep line 215 and 1138 ("the 1.x default"). |

### A2. Promises the docs make that 2.0.0 did not ship (fix text *or* ship the feature — see Workstream B)

| # | Location | Promise | Status in code |
| --- | --- | --- | --- |
| A2.1 | [docs/native-expressions.md:188–189](native-expressions.md#L188) | `AllowCSharp` "will be marked obsolete in the 2.0 release" | Not marked. [TemplateOptions.cs:24](../src/Heddle/Data/TemplateOptions.cs#L24) XML doc still says "Scheduled for `[Obsolete]` in the 2.0 window." → resolve via **B1**. |
| A2.2 | [samples/html-safe-output/README.md:23–24](../samples/html-safe-output/README.md#L23) | "A dedicated `TemplateOptions.Encoder` is a 2.0-window feature" | No `Encoder` property exists; `HtmlEncodedRenderer` hard-codes `WebUtility.HtmlEncode` ([HtmlEncodedRenderer.cs:20](../src/Heddle/Data/HtmlEncodedRenderer.cs#L20)). → resolve via **B2**. |

### A3. Index and navigation misses

| # | Location | Miss | Fix |
| --- | --- | --- | --- |
| A3.1 | [docs/README.md — Documentation map](README.md#documentation-map) | Omits three existing docs: `native-expressions.md`, `precompilation.md`, `editor-support.md` | Add three rows; also link them from the relevant "Pick your path" personas (authors → native-expressions; integrators → precompilation; authors/integrators → editor-support). |
| A3.2 | [docs/README.md](README.md) + [README.md](../README.md) packages table | Lists only `Heddle` + `Heddle.Language`; omits `Heddle.Generator` (instructed as a `PackageReference` in [precompilation.md](precompilation.md)) and `Heddle.LanguageServer` (instructed as a `dotnet tool install` in [editor-support.md](editor-support.md)); `Heddle.LanguageServices` is called "a public package" in editor-support.md | Make the package tables complete and consistent with what is actually published. |
| A3.3 | Docs site nav (VitePress config under `docs/`) | If the site nav mirrors the documentation map, the three missing pages are missing there too | Verify and align nav with A3.1. |

### A4. Example-hygiene nits (minor, but security-posture-relevant)

| # | Location | Issue | Fix |
| --- | --- | --- | --- |
| A4.1 | [README.md](../README.md) + [docs/README.md — 30-second taste](README.md#a-30second-taste) | The hello-world sets `AllowCSharp = true`, but the example uses only member-tier access on a `dynamic` model. First-contact code teaches the maximal-trust switch as a reflex. | Drop the option (defaults suffice), or use `ExpressionMode` explicitly if an option must be shown. Sweep other docs/samples for gratuitous `AllowCSharp = true`. |
| A4.2 | Docs referencing deleted paths | [samples/README.md](../samples/README.md) rows link `../docs/spec/phase-*/README.md`, which this branch deletes (the `docs/archive`/`docs/spec` purge in the working tree). | After the archive removal lands, sweep for dangling `docs/spec/`/`docs/archive/` links (samples table "Source of record" column is the known instance). |

**Exit criteria for Workstream A:** a `grep` for `1.x default`, `2.0 window`, `will be marked`,
`Scheduled for` across `docs/`, `samples/`, `README.md`, and XML doc comments returns only
intentional, present-tense statements; every published package appears in both package tables;
every file in `docs/*.md` is reachable from the documentation map.

---

## Workstream B — Keep the shipped-promise features (P1: small code, already designed)

These are not new ideas; they are the documented 2.0 plan, completed. All are additive.

### B1. Mark `AllowCSharp` `[Obsolete]` *(effort: trivial)*

- Add `[Obsolete("Use TemplateOptions.ExpressionMode. AllowCSharp = true == ExpressionMode.FullCSharp.")]`
  (warning, not error) to the property; update [TemplateOptions.cs:24](../src/Heddle/Data/TemplateOptions.cs#L24)
  XML doc and [native-expressions.md:188](native-expressions.md#L188) to past tense.
- Sweep first-party code/samples/tests so the build stays warning-clean; keep the property
  functional (it is a bridge, not a removal).

### B2. `TemplateOptions.Encoder` — the promised pluggable encoder *(effort: small)*

- One property: `Func<string, string>` (or a tiny `IOutputEncoder` with a span overload to keep
  the UTF-8 path allocation-free), defaulting to the current `WebUtility.HtmlEncode` behavior so
  output is byte-identical when unset.
- Thread it through `HtmlEncodedRenderer`/the UTF-8 sink; include it in the precompilation
  options fingerprint **only if** the generator bakes encoding (verify against
  `HeddleEmitUtf8Pieces` pre-encoded static pieces — if pieces are encoded at build time, the
  encoder must join the gauntlet's options fingerprint, same rationale as `TrimDirectiveLines`).
- Update [samples/html-safe-output](../samples/html-safe-output) to demonstrate it (the sample
  README already narrates it) and keep the `@tagged` extension as the "1.x seam" contrast.

### B3. Deprecation diagnostic for `@import()` *(effort: trivial)*

- The docs already say "new templates should always use `@<<`" and document `@import()`'s
  surprising failure modes. Make the compiler say it too: one new warning (next free `HED4xxx`)
  on every `@import()` call — *"`@import()` is a legacy compile-time include; use `@<<{{ path }}`
  to share definitions."*
- No behavior change, no removal. This is the cheapest possible way to drain the wart the
  assessment flagged (§ Weaknesses #5) without a breaking window.

**Exit criteria for Workstream B:** zero doc statements promise unshipped behavior; compiling a
template that uses `@import()` produces a positioned warning; a host can swap the HTML encoder
via options with byte-identical default behavior.

---

## Workstream C — Close the untrusted-template gap (P1–P2: the one real capability gap)

The assessment's security verdict (B+) hinged on two things. Both are addressable without
touching the sandbox design — which is already best-in-class and must not change.

### C1. Render resource budgets *(effort: moderate — the highest-value engine change in this plan)*

**Gap (verified):** `TemplateOptions` has exactly one limit, `MaxRecursionCount`. A sandboxed
(`Native`-mode) but hostile template can still emit unbounded output or iterate enormous ranges
(`@for(range(0, 2_000_000_000))`). Shopify Liquid ships enforced render limits for precisely
this reason; this is the single blocker the assessment named before Heddle's sandbox story
covers availability as well as confidentiality.

**Minimal design (additive, render-time only, no compile or cache-identity impact):**

- `TemplateOptions.RenderBudget` (nullable; `null` = today's unlimited behavior, so nothing
  changes for existing hosts): `MaxOutputChars` (checked in `ScopeRenderer`/UTF-8 sink at the
  buffer high-water points already instrumented), `MaxIterations` (one shared counter,
  incremented in `@list`/`@for` item loops — the two built-in iterators — and exposed on `Scope`
  so custom iterating extensions can participate), and optionally `Deadline`
  (a `TimeSpan`, checked at the same iteration points; cheaper and more deterministic than a
  timer thread).
- On breach: throw `TemplateRenderBudgetException` naming the budget and the template position
  of the active node — consistent with the engine's positioned-diagnostics culture.
- Because budgets are render-scope state (a natural fit for the existing `Scope`/renderer
  plumbing), the precompiled backend inherits them through the same renderer — the
  byte-identical differential is unaffected (budgeted renders either complete identically or
  throw identically; add corpus fixtures for both).
- Document in [csharp-api.md](csharp-api.md) and wire into
  [samples/sandboxed-user-templates](../samples/sandboxed-user-templates) — that sample is the
  natural home and currently demonstrates only compile-time rejection.

### C2. Context-encoding built-ins: `@attr`, `@js`, `@url` *(effort: small)*

**Gap:** `OutputProfile.Html` encodes uniformly for the HTML-element context; attribute, JS, and
URL sinks are author responsibility. Full Go-style contextual autoescaping would require an HTML
parser in the pipeline — a large, architecture-bending change. **Do not do that.**

**Minimal design:** three new `[EncodeOutput]`-style extensions using the existing extension
seam (the exact mechanism the language exists to showcase):

- `@attr(value)` — HTML-attribute encoding (quotes always encoded).
- `@js(value)` — JS-string escaping (`\u` escaping of `</`, quotes, line separators).
- `@url(value)` — `Uri.EscapeDataString`.

Plus one short "encoding contexts" section in [built-in-extensions.md → HTML encoding](built-in-extensions.md#html-encoding)
stating plainly which contexts the profile covers and which need these extensions. This converts
an undocumented author trap into a documented, tool-supported decision at ~1% of the cost of
contextual autoescaping.

### C3. Model-exposure guidance *(effort: docs only)*

One page (or a section in [patterns.md](patterns.md)): for untrusted templates, expose dedicated
DTOs, never live domain objects; getters execute; `[Hidden]` exists and how to use it. The
engine already behaves correctly — the guidance just isn't collected in one place.

**Exit criteria for Workstream C:** the sandboxed-user-templates sample demonstrates a hostile
template being stopped by a budget; the docs contain a single authoritative "untrusted
templates" checklist (Native mode + registry + DTOs + budgets + encoding contexts).

---

## Workstream D — Benchmark credibility (P1: no engine change at all)

**Gap (verified):** the performance claim rests solely on the in-repo Heddle-vs-Razor
BenchmarkDotNet suite. The .NET template-engine conversation is anchored on the
Fluid/Scriban/DotLiquid/Handlebars.Net comparisons; Heddle is absent from all of them.

- **D1.** Add Fluid, Scriban, DotLiquid, and Handlebars.Net twins of the existing benchmark page
  to [src/Heddle.Performance](../src/Heddle.Performance) (same model, same output, like-for-like
  — the discipline the Razor twin already established). Compile-cost and render-cost tables both.
- **D2.** Publish the numbers (with environment and date) in README's Performance section
  instead of the current unquantified "faster than Razor" claim; link the runnable project.
- **D3.** Contribute a Heddle entry to the community benchmark suites the ecosystem
  actually reads (e.g. the Fluid repo's benchmark harness), where methodology is theirs, not
  Heddle's — third-party-verifiable by construction. *(Committed scope — promoted from stretch.)*

**Exit criteria:** the README performance claim carries numbers against the engines adopters
would otherwise pick, not only against Razor.

---

## Workstream E — On-ramp ergonomics (P2: docs only, addresses the C+ learnability grade)

The syntax is not changing (see Non-goals), so the on-ramp must do the work:

- **E1. "Heddle for X users" translation pages** — two short pages: *Coming from
  Razor* (absolute→relative context, sections→definitions, `@:` differences) and *Coming from
  Liquid/Jinja* (`{{ }}` means body not interpolation; filters→chains **and the direction
  flip**; include→`@<<`). The assessment's "first week vs first hour" problem is mostly these
  two audiences' priors; a targeted unlearning page is the cheapest fix available.
- **E2. Document the descend-vs-step-back rule as a first-class table** — one glanceable table
  of every built-in: *descends* / *steps back* / *root-only*. The rule is per-extension
  knowledge (assessment §4.3); today it's prose spread across two docs. (The LSP already shows
  scope types; this is the paper equivalent.)
- **E3. Multi-slot pattern recipe** — the language caps definitions at one typed slot; the
  composition answer (multiple named definitions + overrides, or chained content) exists but is
  undocumented as a recipe. Add to [patterns.md](patterns.md). *Explicitly not* a grammar
  change.
- **E4. Culture/i18n recipe** — per-call locale on `@money`/`@date` plus the `::`-root culture
  pattern ([language-reference.md](language-reference.md#root-reference-member) already hints at
  it) collected into one recipe: "culture lives on the root model." Positions the invariant
  built-ins as a feature (determinism) with a documented escape hatch.

---

## Non-goals — things deliberately *not* fixed

Each was weighed in the assessment and belongs to the language's identity. Changing them would
spend uniqueness to buy familiarity — the wrong trade for a language whose only moat is design:

| Not changing | Why |
| --- | --- |
| Right-to-left chains | Composition semantics (`a(b(c))`) are load-bearing for typed chains; a direction flip is a new language. Mitigate via E1. |
| The unified `chained` channel | Pillar (e). Its economy is what keeps the extension contract small. |
| No async render | A considered architectural position with a documented host-flush pattern; async-in-template invites the data-access-in-view antipattern StringTemplate's doctrine correctly warns about. |
| Sigil inventory / `:` vs `::` overloading | Grammar-stable since 2.0; churn here breaks every template, editor mode, and the JS grammar for ergonomic gains E1/E2 can substantially deliver anyway. |
| Full contextual (Go-style) autoescaping | Requires an HTML-aware pipeline — architecture-bending. C2 covers the practical need at ~1% of the cost. |
| Multiple slots per definition, non-literal prop defaults | Real grammar/compiler surface for modest gain; pattern-documented instead (E3). Revisit only with adoption evidence. |
| `where`-style constraints on abstract definitions | Would trade away pillar (a)'s structural model for nominal ceremony. The LSP's per-use-site errors are the intended answer. |
| Removing `@import()` outright | Breaking. B3's warning drains it gradually; removal is a 3.0 decision. |

---

## Sequencing and effort

| Phase | Items | Effort | Risk | Outcome |
| --- | --- | --- | --- | --- |
| **1 (immediate)** | A1–A4 (doc drift), B1 (`[Obsolete]`), B3 (`@import` warning) | ~1–2 days total | none | Docs match the shipped product; both stale in-code promises resolved; the wart drains itself. |
| **2 (next minor, e.g. 2.1)** | B2 (`Encoder`), C2 (`@attr`/`@js`/`@url`), C3, D1–D2, E1–E4 | ~1–2 weeks | low (all additive; B2 needs the fingerprint check) | The promised 2.0 encoder ships; the untrusted-template checklist is complete on the encoding side; performance claims become quantified; the on-ramp addresses the two biggest incoming audiences. |
| **3 (2.2)** | C1 (render budgets), D3 (third-party benchmarks, committed) | ~2–4 weeks | moderate (C1 touches the render hot path — benchmark before/after; differential corpus additions; D3 depends on upstream maintainers accepting the entry — submit early, track as external) | The assessment's security grade blocker is removed: Heddle becomes recommendable for untrusted templates without caveats, the position no .NET engine currently holds with static typing. |

**The single highest-leverage item overall** is **C1 (render budgets)**: it is the only entry in
this plan that changes what Heddle can be *used for* — unlocking the
typed-and-sandboxed-user-templates niche the assessment identified as an unoccupied industry
position — and it does so with a nullable option and two counters, no grammar, no semantics, no
pillar touched. Everything in Phase 1 is pure hygiene and should precede any release activity,
since each drift item is visible to exactly the evaluating audience Heddle most needs to
convince.
