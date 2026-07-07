# Phase 9 — Demo Page & Integration Demos (with the demo‑driven test suite)

> Part of the [Heddle evolution roadmap](README.md). Status: **planned** (created
> July 2026, split out of phase 6's browser‑reuse question). Depends on:
> [phase 6](phase-6-tooling-lsp.md)'s language service facade for the typed in‑browser
> features; individual demos depend on their own phases and land as those ship. The
> gallery/test‑suite structure itself depends on nothing. **Zero grammar change, zero
> engine change** — this phase consumes engine surfaces, it never adds them.

## Goal

Three deliverables that reinforce each other:

1. **Evolve the live demo page** from syntax‑only checking to the real language
   experience — typed completion, hover types, real compile diagnostics — by running the
   engine's own language service in the browser, and (once
   [phase 1](phase-1-native-expressions.md) removes Roslyn from the common path)
   actually **rendering templates in the browser**.
2. **A gallery of integration demos** — small, complete, runnable projects showing each
   supported way to integrate Heddle (SSR, codegen, precompiled, sandboxed, custom
   extensions, editor tooling).
3. **The demos double as the end‑to‑end test suite.** Every demo builds, runs, and
   asserts pinned output in CI — integration‑level coverage (packaging, TFMs, resolver
   file behavior, options plumbing) that unit tests structurally miss. A demo that
   breaks *is* a failed test; a demo that works is living documentation.

## What already exists (build on, don't rebuild)

- [demo.html](../public/demo.html) — the live editor demo shipped June 2026: a
  self‑contained static page on GitHub Pages, Ace editor with the custom Heddle mode
  (bundle built by the docs workflow from
  [src/Heddle.Language](../../src/Heddle.Language)), live syntax highlighting and
  parse‑error squiggles via the ANTLR‑generated **JS parser in a Web Worker**. Its
  stated limitation is this phase's target: *"It does NOT render templates — the engine
  is .NET."*
- The docs workflow (`.github/workflows/docs.yml`) already builds the Ace bundle and
  publishes the page — the deployment pipeline exists.
- The TextMate grammar and Ace mode share the token vocabulary with
  `ProvideLanguageFeatures` (phase 6's semantic token source) — one language, several
  frontends.
- The test corpus (`src/Heddle.Tests/TestTemplate/**`) and the
  [language-reference.md](../language-reference.md) blog model provide ready‑made demo
  content.

## Design

### 9.1 Demo page: the language service in the browser

**The constraint that shapes everything: GitHub Pages is static hosting.** There is no
server process, so "add LSP to the demo" cannot mean a hosted socket server. The design
runs the service client‑side instead:

- **The phase 6 `HeddleLanguageService` facade compiled to WebAssembly**
  (`browser-wasm`), loaded into the page's worker. The facade is the single source of
  truth — the same rule phase 6 sets for the LSP server applies to the demo: every editor
  feature is a projection of engine data, never a JS re‑implementation. The thin protocol
  layer is transport‑agnostic by construction (phase 6 hand‑writes the DTOs); in the
  browser the "transport" is a `postMessage` channel to the worker instead of stdio —
  same message shapes, no protocol fork.
- **Demo models are compiled in.** The browser cannot reflect over arbitrary user
  assemblies; the WASM bundle ships the docs' blog model corpus (`Blog`/`Article`/
  `Author`/`Comment`) as the completion/hover model set, selectable in the page. That is
  a *feature* for a demo (curated, instructive models), not a limitation to apologize
  for.
- **Progressive enhancement, not replacement.** The existing JS‑parser worker stays as
  the base layer: instant syntax squiggles while the WASM bundle downloads, and the whole
  page still works if WASM fails to load (the current demo *is* the fallback). Typed
  features light up when the service is ready.
- **In‑browser rendering** — the demo's endgame: type a template, see the output.
  Gated on phase 1 (native expressions make the common path Roslyn‑free; shipping Roslyn
  to the browser is not reasonable). Under WASM, `System.Linq.Expressions` runs
  interpreted — fine at demo scale. `AllowCSharp` templates show a friendly "C# tier is
  not available in the browser demo" diagnostic instead of rendering.
- **Editor**: keep **Ace** for v1 — the custom bundle, mode, and worker pipeline exist
  and the facade protocol is editor‑agnostic. Monaco (with its richer
  completion/hover widgets) is evaluated only if Ace's UX proves limiting for typed
  features (open question 1).

### 9.2 Integration demo gallery

A top‑level `samples/` folder (in‑repo — versions move in lockstep with the engine and
CI is shared), one small, complete, README'd project per integration story:

| Demo | Shows | Lands with |
| --- | --- | --- |
| `samples/ssr-aspnetcore` | Classic SSR: resolver + layout/page composition + caching | current engine |
| `samples/definition-library` | `@<<` composition import, definition overrides, `<name:name>` narrowing | current engine |
| `samples/dynamic-models` | `:: dynamic`, runtime‑loaded template strings | current engine |
| `samples/sandboxed-user-templates` | `ExpressionMode.Native`, function registry as the trust boundary | phase 1 |
| `samples/html-safe-output` | `OutputProfile.Html`, `@raw`, encoder pluggability | phase 2 |
| `samples/custom-extensions` | The publish/read channel, a branch‑protocol participant | phase 3 |
| `samples/component-props-slots` | A small component library: typed props with defaults, parameterized slots | phase 5 |
| `samples/codegen-t4-successor` | Build‑time text/code generation, no runtime Heddle dependency | phase 7 |
| `samples/precompiled-app` | Pre-compilation + mixed mode + discovery enumeration | phase 7 |
| `samples/streaming-ssr` | `IBufferWriter<byte>` into `Response.BodyWriter` | phase 8 |
| `editors/vscode` walkthrough | LSP setup, typed completion tour | phase 6 |

This phase ships the structure plus the **current‑engine demos** (first three rows); each
later phase adds its row as part of its own success criteria — the gallery is the place
where "phase X shipped" becomes demonstrable. Phase 4 contributes no new row by design:
it **upgrades** the current‑engine samples (`ssr-aspnetcore`, `definition-library`) to use
`@for` sugar and `TrimDirectiveLines`, so the existing gallery goldens exercise its
features end‑to‑end.

### 9.3 The demos are the test suite

- **Every sample is a CI job step**: `dotnet run` (or `dotnet test` for samples with
  assertion projects) with **pinned golden output** — the rendered page, the generated
  file, the enumeration listing. Failures mean an integration regression: packaging,
  TFM‑specific behavior, resolver file semantics, options plumbing — the classes of bug
  unit tests structurally miss because they never leave the assembly.
- **The demo page gets a headless smoke test** in the docs workflow: build the bundle,
  load the page (Playwright or equivalent), type a known‑bad template, assert the
  squiggle appears; with the WASM service present, assert one completion and one render
  round‑trip. This pins the only user‑facing artifact that currently ships untested.
- **Golden discipline matches the engine suite**: demo goldens live beside each sample;
  the differential rule from phase 7 applies where both backends exist (a precompiled
  sample's output must equal its dynamic twin).

## Back‑compat analysis

None — no engine changes. New folders (`samples/`), new CI jobs, and a demo‑page
enhancement that keeps its current behavior as the no‑WASM fallback.

## Risks & mitigations

- **WASM bundle size/latency** (the .NET runtime + facade + models is megabytes, not
  kilobytes): lazy‑load after the base page is interactive; the JS‑parser layer keeps the
  page useful during and without the download; measure and budget the bundle in CI. (M)
- **Facade API drift between the LSP server and the browser host** — two hosts, one
  facade: the shared DTO layer is the contract; a facade‑level test suite (phase 6
  success criterion 6) runs against both hosts' message fixtures. (S/M)
- **Demo rot** — the standing risk of every sample gallery; here it is inverted by
  design: rotted demos fail CI. The residual risk is goldens updated ritually without
  review — mitigate with the same review discipline as engine goldens. (S)
- **CI time growth** as the gallery grows: samples build in parallel; the page smoke
  test is one headless run. Budget, don't cap. (S)

## Success criteria

1. The demo page offers **typed member completion and hover** against the blog corpus,
   and **real compile diagnostics** (not just parse errors) — served entirely from
   static hosting, with the current syntax‑only behavior as the intact fallback.
2. With phase 1 shipped: **typing a template renders live output in the browser**, with
   the C# tier declined gracefully.
3. The three current‑engine samples build, run, and pass their golden assertions in CI
   on every PR.
4. The demo‑page smoke test runs in the docs workflow and fails on a seeded regression
   (deliberately broken bundle → red build, verified once).
5. Each subsequent phase's sample row is added by that phase and inherits the CI harness
   without new infrastructure.
6. A newcomer can go from the docs to a locally running sample with `git clone` +
   `dotnet run` and no undocumented steps (tested by following each README verbatim).

## Validation scenarios

| Scenario | Expected |
| --- | --- |
| Demo page, WASM blocked (network filter) | page loads, syntax squiggles work — current behavior preserved |
| Demo page, WASM loaded, `@(` typed inside `@list(Articles)` body | `Article` members complete (typed, from the facade — not a word list) |
| Demo page, member typo | compile diagnostic squiggle at the template position |
| Demo page + phase 1, valid template | rendered output shown live |
| Demo page + phase 1, `AllowCSharp`‑style template | friendly "not available in browser" diagnostic, no crash |
| `samples/ssr-aspnetcore` in CI | serves the composed page; response equals golden |
| `samples/definition-library` in CI | override/narrowing output equals golden |
| `samples/dynamic-models` in CI | dynamic + runtime‑string render equals golden |
| Seeded break (rename a model property in a sample) | that sample's CI job fails, others pass |

## Testing

### Impact analysis

No engine change — the impact surface is the **docs workflow** (bundle build + page
publish must keep working when WASM assets fail to build; the fallback layering is a
build‑time property too) and **CI time** as the gallery grows. Because this phase's
product *is* test infrastructure, the meta‑risk is untested tests: a harness that
silently passes everything.

### Regression requirements

- The docs workflow publishes the current demo page unchanged even with the WASM bundle
  absent or broken (fallback verified in CI, not assumed).
- Engine suite untouched by anything in this phase, by construction (no engine code).
- CI wall‑time budget tracked per gallery addition (samples build in parallel; the page
  smoke test is one headless run).

### TDD verdict

**Inverted: the demos *are* the tests, so the discipline moves to testing the harness.**
Classic TDD does not apply to a sample app; what must be verified test‑first is the
harness's ability to fail: the **seeded‑failure check** (success criterion 4 — break the
bundle / rename a model property, observe red, revert) runs once per harness change as
the standing "test the tests" gate. Demo goldens follow the same review discipline as
engine goldens — captured, reviewed, never adjusted to silence a red.

### The verification loop

1. Add or update a sample; write its README first (the README *is* its spec — criterion
   6 requires a newcomer can follow it verbatim).
2. Capture its golden output; maintainer review.
3. Wire it into CI; run the seeded‑failure check for the new job (break it once, see
   red, revert).
4. For the demo page: bundle build → headless smoke (squiggle assertion; with WASM:
   completion + render round‑trip) → publish. The WASM‑blocked variant runs in the same
   smoke suite.
5. Red in any gallery job → the *sample's phase owner* fixes forward (a rotted demo is
   an integration regression in that phase's feature, not a phase 9 problem); golden
   updates are review events.

### The final suite (this phase's deliverable *is* the deferred suite)

- The gallery harness: per‑sample build/run/golden‑assert CI jobs, growing one row per
  shipped phase (each phase's Testing section names its deferred item; this phase owns
  the harness they land in).
- The headless page smoke test (with and without WASM), the cross‑host facade contract
  test shared with phase 6, the seeded‑failure verification, and the README‑walkthrough
  verification.

## Size estimate

| Work item | Size |
| --- | --- |
| WASM host for the facade + worker protocol wiring | M/L |
| Demo‑page editor integration (completion/hover/diagnostics UI in Ace) | M |
| In‑browser rendering (post‑phase 1) | M |
| `samples/` structure + three current‑engine demos + READMEs | M |
| CI harness (sample goldens + headless page smoke test) | M |

## Open questions

1. **Ace vs Monaco** for the typed features (lean: Ace v1 — the bundle and worker
   pipeline exist; the facade protocol is editor‑agnostic, so switching later is a
   frontend swap, not a redesign).
2. **In‑browser rendering timing**: strictly after phase 1 (lean), or attempt earlier
   with interpreted expression trees and no native‑expression surface? The earlier
   variant demos a language the roadmap is about to change — lean: wait.
3. **WASM toolchain choice** (plain `browser-wasm` console workload vs Blazor
   WebAssembly hosting just for the runtime): decide at implementation against bundle
   size and trimming behavior — the facade must not take dependencies that matter here.

## External grounding

This document has **not yet had its external grounding round** (it was created in the
July 2026 open‑question resolution, after the grounding rounds ran). Claims to verify
before implementation, recorded so the round is mechanical:

- .NET WASM (`browser-wasm`) hosting of a class library + worker interop patterns, and
  current bundle‑size/trimming expectations for a reflection‑using library.
- `System.Linq.Expressions` interpreted‑mode behavior under WASM (the engine's
  expression‑tree path must function, if slowly, without JIT).
- Ace completion/hover extension APIs vs Monaco, for open question 1.
- Playwright (or equivalent) headless testing inside the existing GitHub Pages docs
  workflow.

Internal facts above are repo‑grounded: [demo.html](../public/demo.html) (Ace bundle,
JS‑parser worker, "does NOT render" limitation), the docs workflow, and the
`src/Heddle.Tests/TestTemplate/**` corpus.
