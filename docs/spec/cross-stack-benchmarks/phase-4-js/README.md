# Phase 4 — js (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-4-js.md](../../../plan/phase-4-js.md) (standing rulings in the
  [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — every resolution there is binding on
  this spec; the ones this phase consumes directly: Q4.1, Q4.2, Q2.2 = A, Q6.2, Q1.1, Q1.3,
  Q1.6, Q1.7)
- **Assumes merged:** the full [Phase 1 — cross-stack-foundation spec](../phase-1-cross-stack-foundation/README.md)
  (workloads, golden corpus, parity contract v2, metrics & publication protocol, and the
  published intra-.NET protocol run that supplies the Heddle reference rows). Phase 1's five
  documents are read-only inputs; nothing in this spec modifies them.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, gates |
| [templates-and-models.md](templates-and-models.md) | Normative JS model transcriptions and the exact template texts per engine and track, helper/partial registrations, and the report disclosure texts |
| [harness-and-run.md](harness-and-run.md) | Harness layout under `benchmarks/js/`, dependency pinning, gate implementation, mitata run shape, deopt handling, the Windows stability verification procedure, and the publication/report format including the Q4.2 side-note |

## Scope and goal

Port the eight-workload set to the JS/Node ecosystem and measure two engines — **Handlebars**
(credibility pick) and **Eta** (performance pick) — against the Heddle golden corpus on both
fairness tracks, under mitata, on the protocol machine, publishing one per-ecosystem report in a
new date-stamped `docs/benchmarks/<date>/` directory. The report carries the labeled
wall-time-only Heddle reference row (Q2.2 = A), the Heddle-anchored ratio column (Q6.2), the
explicit reach-not-fair-fight framing statement, the time-only statement (Q4.1), and the
Handlebars-vs-Handlebars.Net cross-runtime side-note (Q4.2).

Out of scope (plan non-goals, carried): any other JS engine (Pug/EJS/Nunjucks exclusions are
plan-recorded), SSR component frameworks, Bun/Deno runtimes, HTTP/DB stacks, changes to the
Heddle engine, the corpus, or contract v2, and any new cross-comparable metric.

## Assumed state

Re-verified against the current working tree and primary web sources while authoring
(2026-07-20); Phase 1's deliverables are assumed merged per the dependency-order rule and are
cited from its spec, not re-specified.

| Seam | Verified state |
|---|---|
| Golden corpus | Assumed merged per [golden-corpus.md](../phase-1-cross-stack-foundation/golden-corpus.md): eight `src/Heddle.Performance/GoldenCorpus/<id>.golden.html` (normalized, UTF-8 no BOM, no trailing newline), eight `<id>.verify.json`, `manifest.json`. Not present in the working tree at authoring time — Phase 1 is the prior item; *(verify at implementation: files exist and `verify-corpus` passes before any JS gate work starts)* |
| Contract v2 | [parity-contract-v2.md](../phase-1-cross-stack-foundation/parity-contract-v2.md) *(read)* — N1–N5 pipeline; N5 (encoded suite only) canonicalizes the five characters' spellings to `&amp; &lt; &gt; &quot; &#39;`; the contract **already names Handlebars-JS's fixed hex family as an N5-in-gate-runner case** (§Entity canonicalization rule 3); untrusted-data alphabet excludes `+`, `=`, `` ` `` and the `&#` substring; exclusion policy Q1.2 |
| Metrics protocol | [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md) *(read)* — mitata's `avg` is the wall-time statistic with the printed percentile spread as dispersion (per [Phase 1 README D12 — Wall-time statistic mapping (Q2.1)](../phase-1-cross-stack-foundation/README.md#d12--wall-time-statistic-mapping-q21), which selects this mapping from the protocol's table); Heddle reference-row label format and ratio anchor; required verbatim label texts; publication format; honest-reporting rules 1–6 |
| Handlebars.Net twins (template seeds) | *(read)* [HandlebarsTest.cs](../../../../src/Heddle.Performance/Runners/HandlebarsTest.cs) (composed-page: `layout` registered template, `area` helper writing `TwinContent.Areas[name]` as a safe string, triple-mustache literals), [SubstitutionHandlebarsTest.cs](../../../../src/Heddle.Performance/Runners/SubstitutionHandlebarsTest.cs) (`CardTemplate`), [LoopHandlebarsTest.cs](../../../../src/Heddle.Performance/Runners/LoopHandlebarsTest.cs) (`LoopTemplate`); workloads 4–8 Handlebars twin texts are normative in [workloads.md](../phase-1-cross-stack-foundation/workloads.md) |
| Composed-page model source | *(read)* [TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs) — `SectionMeta/SectionSocial/SectionPageScripts/SectionEndPageScripts` consts, `Comp*` consts, `AreaOrder`, `Areas` (delegates to `AreaComponent.Areas`), `Sections()`/`Components()` dictionaries — the literals the JS model transcribes |
| conditional-heavy helper story | *(read)* Phase 1 [D1](../phase-1-cross-stack-foundation/README.md#d1--the-five-new-workloads-and-their-exact-shapes) + workloads.md workload 5: branching is on **precomputed booleans** with chained `{{else if}}` precisely so the four-way chain stays inside vanilla Handlebars — **no comparison helpers exist anywhere in the intra-.NET suite**, and none are needed in JS (D6 below) |
| Handlebars-JS versions/facts | npm registry *(fetched 2026-07-20)*: latest `4.7.9`; weekly downloads **39,494,320** (week 2026-07-13 → 2026-07-19, `api.npmjs.org/downloads/point/last-week/handlebars`); chained `{{else if}}` ("Chained else blocks") shipped in v3.0.0 (release-notes.md); partial-with-context `{{> myPartial myOtherContext }}` documented (handlebarsjs.com/guide/partials.html); `escapeExpression` table `& < > " ' `` ` `` =` → `&amp; &lt; &gt; &quot; &#x27; &#x60; &#x3D;`, no configuration surface (lib/handlebars/utils.js, spike C); precompilation documented as the production-canonical path, `--knownOnly` "smallest generated code that also provides the fastest execution" (handlebarsjs.com/guide/installation/precompilation.html) |
| Eta versions/facts | npm registry *(fetched 2026-07-20)*: latest **4.6.0** (v3 superseded — plan-era premise corrected), dual CJS/ESM, `engines.node >= 20`. Docs (eta.js.org, v4.x.x) *(fetched)*: `new Eta({...})`; `autoEscape` default `true`; `autoTrim` default `[false, 'nl']`; `escapeFunction` overridable; `varName` default `it`; tags `<% %>`, `=` interpolate, `~` raw; `render`/`renderString`/`loadTemplate("@name", src)` (`@`-prefixed names are cached, not filesystem); `include("name", data)`; `layout("name", data)` + `<%~ it.body %>`; default escaper `XMLEscape` emits exactly `&amp; &lt; &gt; &quot; &#39;` (src/utils.ts, spike C) — **byte-canonical against the corpus, N5 is an identity transform on Eta output** |
| mitata | npm `1.0.34` *(fetched 2026-07-20)*; `src/main.d.mts` *(fetched)*: `run(opts)` → `Promise<{ context, benchmarks }>`, `format: 'json' \| 'quiet' \| 'mitata' \| 'markdown'`; exports `bench`, `group`, `do_not_optimize`, `measure`; README: flags `--expose-gc --allow-natives-syntax`, marker `!` = "benchmark was likely optimized out (dead code elimination)", memory line documented only as "estimated heap usage", **no OS-support statement of any kind**; only Windows-tagged issue is #41 "Hardware Counters for Windows" (closed — hardware counters are macOS/Linux-only and unused here) |
| Node.js | Latest 24.x LTS point release is **24.18.0** (nodejs.org/dist/latest-v24.x *(fetched 2026-07-20)*; corrects spike E's WebSearch-derived 24.11.0); 24.x is Active LTS until 2026-10-20 maintenance transition, EOL 2028-04-30 |
| Repo layout | *(listed)* No top-level `benchmarks/` directory exists; all current harness code is the .NET solution under `src/`; published reports live under `docs/benchmarks/<date>/`; no npm artifacts exist anywhere in the repo today |

## Design decisions

Every plan lean, flagged assumption, and spec-territory delegation for this phase, closed.

### D1 — Harness lives at `benchmarks/js/` (new top-level, cross-phase convention)
- **Decision.** The Node sub-project lives at `benchmarks/js/` (full layout in
  [harness-and-run.md](harness-and-run.md#harness-layout)). This establishes the cross-phase
  convention: each ecosystem phase adds `benchmarks/<ecosystem>/` (`rust`, `jvm`, `js`, `python`,
  `go`). `docs/benchmarks/` remains reports-only; `src/` remains the .NET solution.
- **Rationale.** `src/` is the `Heddle.sln` build surface — a non-.NET npm project inside it
  would sit outside the solution while polluting its tree, and .NET tooling (packing, analyzers,
  `dotnet test` globs) has no business walking `node_modules`. Phase 1 deliberately avoided
  creating a top-level `benchmarks/` for the corpus because *that* phase didn't need one; this
  phase is the first non-.NET harness — the trigger Phase 1's D6 anticipated has effectively
  fired for harnesses (the corpus itself stays where it is).
- **Alternatives rejected.** `src/Heddle.Performance.Js/` (inside the .NET solution tree, not a
  .NET project — misleading); a per-phase ad-hoc location (phase 7 wants one predictable root);
  moving the golden corpus alongside (unnecessary — the JS gate reads
  `src/Heddle.Performance/GoldenCorpus/` by repo-relative path without difficulty, so Phase 1's
  corpus-move trigger has *not* fired).
- **Grounding.** Repo listing (Assumed state); [Phase 1 D6](../phase-1-cross-stack-foundation/README.md#d6--the-corpus-stores-the-normalized-oracle-under-srcheddleperformancegoldencorpus).

### D2 — Version pins: Node 24.18.0, Handlebars 4.7.9, Eta 4.6.0, mitata 1.0.34
- **Decision.** `package.json` pins exact versions (no range operators): `handlebars 4.7.9`,
  `eta 4.6.0`, `mitata 1.0.34`; `engines.node` = `24.18.0` with `engine-strict=true` in
  `.npmrc`; `package-lock.json` is committed; the only sanctioned install command is `npm ci`.
  The environment block of the published report records `node --version`, the V8 version
  (`process.versions.v8`), and the three package versions. If a newer 24.x LTS point release
  exists at run time, bumping the pin is an ordinary versioned change made *before* the
  measurement run and reflected in the environment block — numbers are never published from a
  Node version other than the committed pin.
- **Rationale.** All four versions verified against primary sources on 2026-07-20 (Assumed
  state). Node 24.x is the Active LTS line (plan requirement "the spec pins one Node version");
  pinning the exact point release makes `engine-strict` enforceable and the environment block
  reproducible. Eta 4.6.0 corrects the plan-era v3 premise (spike E found v3 superseded; this
  spec's template and API surface is authored against the verified v4 docs).
- **Alternatives rejected.** Floating `^` ranges (violates the plan's pin-and-record
  requirement); Eta v3 (superseded; docs site no longer serves it as current); Node 22.x
  (previous LTS — no continuity value, shorter support runway).
- **Grounding.** npm registry + nodejs.org fetches (Assumed state);
  [plan §Back-compat](../../../plan/phase-4-js.md#back-compat--impact).

### D3 — Q4.1 closed: the JS suite is time-only
- **Decision.** The JS report publishes **wall time per render only**. No heap/GC column
  appears in any published table. The report carries this statement verbatim (placement in
  [harness-and-run.md §Report format](harness-and-run.md#report-format)):
  > *This suite reports wall time per render only. mitata's memory line is documented only as
  > "estimated heap usage", with no defined measurement semantics, so it does not meet this
  > program's bar for a published metric (documented semantics, stable across runs). The
  > heap/GC estimate lines visible in the raw mitata artifacts are not protocol metrics and
  > must not be compared with any ecosystem's memory figures.*
- **Rationale.** Q4.1's resolution publishes heap/GC only if the spec verifies mitata's memory
  instrumentation is credible on **both** prongs: documented semantics and cross-run stability.
  Verification against the primary source fails the first prong outright: the README's entire
  documented semantics is the phrase "estimated heap usage" — no definition of what is counted,
  no accuracy or stability claim, and the data depends on non-default V8 flags. A stability
  measurement cannot repair undocumented semantics, so no measurement campaign is run and the
  question closes on the documentary evidence alone. The protocol already makes memory
  per-ecosystem-only, so nothing cross-stack is lost.
- **Alternatives rejected.** Publishing the estimates with a caveat (a caveat cannot substitute
  for semantics — anti-credibility); a separate `process.memoryUsage()` instrumentation pass
  (new metric machinery the plan does not ask for; recorded as a deferred item with its
  trigger); flipping to B pending a stability experiment (pointless — prong one already fails).
- **Grounding.** mitata README quote (Assumed state; spike E §1);
  [open-questions Q4.1](../../../plan/open-questions.md).

### D4 — Handlebars escaper divergence: stock engine + N5 in the JS gate; no config, no monkey-patch
- **Decision.** Handlebars runs **stock**. Encoded-suite controlled templates use
  double-mustache under the default `escapeExpression`; the JS controlled gate implements
  contract v2's N5 canonicalization (encoded suite only), which maps Handlebars' `&#x27;` to
  the canonical `&#39;`. No engine configuration is attempted and `Handlebars.Utils.escapeExpression`
  is **not** overridden.
- **Rationale.** Q1.1 prefers engine configuration *wherever an engine offers it*; Handlebars-JS
  offers none — the escape table is a fixed module constant with no documented hook (spike C
  §5, primary source). Contract v2 §Entity canonicalization rule 3 explicitly names
  Handlebars-JS's fixed hex family as the case its gate runner implements N5 for; this decision
  is that clause, exercised. **Byte-gate feasibility beyond the five:** Handlebars' two
  beyond-alphabet escapes (`` ` `` → `&#x60;`, `=` → `&#x3D;`) can never fire on corpus data —
  the untrusted-data alphabet excludes `` ` `` and `=` from every encoded model value, template
  literal text is never passed through `escapeExpression`, and raw suites use triple-mustache
  (no escaping at all). The only real divergence is the apostrophe spelling `&#x27;` (rows with
  `'` in both encoded workloads), which N5 closes; `&quot;`/`&amp;`/`&lt;`/`&gt;` already
  coincide with the canonical spellings. Divergence is therefore *fully* reconciled by the
  contract's own documented pipeline — no exclusion, no carve-out.
- **Alternatives rejected.** Overriding `Handlebars.Utils.escapeExpression` or wrapping every
  value in `SafeString` after manual escaping (undocumented monkey-patching / changes what is
  measured — the suite would no longer measure Handlebars' escaping path, which is exactly the
  encoded suite's dimension); triple-mustache with pre-escaped model data (same objection);
  excluding the encoded cells (unjustified — the documented pipeline reconciles them).
- **Grounding.** [parity-contract-v2 §Entity canonicalization](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5)
  rules 1–3 and §Untrusted-data alphabet; spike C §5 (primary source table);
  [open-questions Q1.1](../../../plan/open-questions.md).

### D5 — conditional-heavy ports with zero registered helpers; the program-wide helper surface is exactly one (`area`)
- **Decision.** The Handlebars conditional-heavy controlled template registers **no helpers**:
  Phase 1 pinned the workload to precomputed booleans (`is_bronze`/`is_silver`/`is_gold` +
  toggles) specifically so the four-way chain is expressible with the built-in truthiness
  `{{#if}}` and chained `{{else if}}` (Handlebars ≥ 3.0.0, verified against the release notes).
  The JS controlled template is the Handlebars.Net twin text verbatim
  ([templates-and-models.md](templates-and-models.md#workload-5--conditional-heavy)). Across
  the whole JS suite exactly **one** helper is registered — `area` for composed-page, the same
  helper surface as the intra-.NET Handlebars.Net twin (a fragment lookup returning a
  `Handlebars.SafeString`; exact JS implementation and its .NET-twin equivalence argument in
  [templates-and-models.md §Registered helpers](templates-and-models.md#registered-helpers--the-full-disclosure-surface)).
  The report discloses this with the verbatim disclosure text specified there, satisfying the
  plan's every-helper-disclosed success criterion.
- **Rationale.** The plan's risk ("will likely require registered comparison helpers") was
  written before Phase 1 pinned the workload shapes; Phase 1's D1 retired it by design —
  "branching on precomputed booleans keeps the four-way chain inside vanilla Handlebars (no
  equality helpers — common-denominator constraint)". Verification: chained else blocks are a
  documented Handlebars feature since v3.0.0; `#if` truthiness semantics documented
  (false/undefined/null/""/0/[] are falsy — all branch operands here are genuine booleans).
  Reusing the exact .NET twin surface keeps the Q4.2 side-note symmetric.
- **Alternatives rejected.** Registering `eq`/comparison helpers (nothing to compare —
  booleans are precomputed; would add undisclosed-idiom surface for no expressive gain);
  inlining the area fragments as template literals to avoid the one helper (diverges from the
  twin authoring and from how the .NET side renders the same workload — breaks the
  equivalently-authored discipline and the side-note symmetry).
- **Grounding.** [Phase 1 D1](../phase-1-cross-stack-foundation/README.md#d1--the-five-new-workloads-and-their-exact-shapes);
  [workloads.md workload 5](../phase-1-cross-stack-foundation/workloads.md#workload-5--conditional-heavy-raw);
  handlebars.js release-notes.md v3.0.0 *(fetched)*; [HandlebarsTest.cs](../../../../src/Heddle.Performance/Runners/HandlebarsTest.cs) *(read)*.

### D6 — Controlled templates are seeded from the Handlebars.Net twins; JS models are pinned transcriptions
- **Decision.** Handlebars controlled-track template texts are the intra-.NET Handlebars.Net
  parity-passing twins, verbatim: workloads 1–3 from the twin source files
  (`HandlebarsTest.LayoutTemplate`/`HomeTemplate`, `SubstitutionHandlebarsTest.CardTemplate`,
  `LoopHandlebarsTest.LoopTemplate`), workloads 4–8 from the normative Handlebars columns of
  [workloads.md](../phase-1-cross-stack-foundation/workloads.md). All model data is transcribed
  into one JS module per workload under the transcription rules of
  [templates-and-models.md §Models](templates-and-models.md#models--transcription-rules):
  snake_case keys (the Phase 1 dictionary-view convention), .NET `string` → JS string verbatim,
  .NET `int` → JS number (all pinned numbers are integers, so `String(n)` formatting is
  culture-free and byte-identical to .NET's invariant `int` formatting), `bool` → JS boolean;
  composed-page literals transcribed from `TwinContent.cs`/`AreaComponent.Areas`. Exactness is
  machine-enforced: the byte gate against the corpus is the completion check for every
  transcription.
- **Rationale.** Plan directive verbatim ("the JS controlled-track templates start from those,
  not from scratch"); the twin texts have already passed the byte gate once inside .NET, so the
  remaining risk is transcription, which the gate catches deterministically. No float ever
  renders (the only scientific-notation figure, `4.33e67`, is inside a pinned string), so JS
  number formatting cannot diverge.
- **Alternatives rejected.** Re-authoring from the Heddle templates (discards the proven twins;
  reintroduces authoring risk the plan's twin-economy explicitly avoids); generating a model
  JSON export from .NET (new Phase 1 surface this spec may not add; transcription + byte gate
  achieves the same assurance).
- **Grounding.** [plan §Design direction](../../../plan/phase-4-js.md#design-direction); twin
  files *(read, Assumed state)*.

### D7 — Handlebars compile mode: runtime `compile` (controlled) vs `precompile` + `template` with known-helpers (idiomatic)
- **Decision.** *Controlled track:* templates are compiled once at harness startup, outside any
  timed region, with runtime `hb.compile(src)` on a `Handlebars.create()` environment —
  mirroring the Handlebars.Net twins (`Handlebars.Create().Compile(...)` once in the
  constructor). *Idiomatic track:* templates are precompiled once at harness startup via
  `Handlebars.precompile(src, { knownHelpers, knownHelpersOnly: true })`, materialized with
  `Handlebars.template(eval('(' + spec + ')'))`, and rendered through that function —
  `knownHelpers` lists only `area` (composed-page environment); all other templates use only
  built-in helpers. The report's idiomatic section states the mode and the `knownOnly` setting.
- **Rationale.** The plan delegates the choice per track with the constraint that idiomatic
  follows what the docs present as production-canonical — which is precompilation, ideally with
  known-helpers ("the smallest generated code that also provides the fastest execution",
  precompilation guide, verified). The controlled track's job is the equivalently-authored twin
  comparison, and its .NET twin is runtime-compiled — matching modes keeps the Q4.2
  cross-runtime side-note a same-compile-mode comparison instead of a compile-mode confound.
  Both modes compile once outside the timed region, so metrics rule 1 is satisfied either way.
- **Alternatives rejected.** Precompile in both tracks (breaks side-note symmetry with the
  runtime-compiled .NET twin; loses the docs-canonical-vs-baseline contrast the two tracks can
  legitimately carry); an ahead-of-time `handlebars` CLI build step emitting `.precompiled.js`
  files (adds a build artifact and a second toolchain invocation for zero measurement
  difference — the render path is `Handlebars.template(spec)` either way; in-process
  `precompile` at startup is the same compiler producing the same spec).
- **Grounding.** handlebarsjs.com precompilation guide (Assumed state; spike E §2);
  [SubstitutionHandlebarsTest.cs](../../../../src/Heddle.Performance/Runners/SubstitutionHandlebarsTest.cs) *(read)*;
  [metrics-protocol §Metric rules](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules) rule 1.

### D8 — Eta port strategy: default-config instance, `<%~ %>` raw / `<%= %>` escaped, `@`-cached templates, `eta.render` as the measured path
- **Decision.** One `new Eta()` instance per track with **defaults** (`autoEscape: true`,
  `autoTrim: [false, 'nl']`, `varName: 'it'`, default tags and `escapeFunction`). Raw-suite
  interpolations use the raw tag `<%~ %>` (the engine's documented non-encoding output path —
  the syntax-level analogue of Handlebars' triple-mustache); encoded-suite interpolations use
  `<%= %>` (default `XMLEscape`, whose five spellings are byte-canonical against the corpus, so
  N5 is an identity on Eta output). All templates are registered by name at startup via
  `eta.loadTemplate("@<id>", src)` (`@` = cached, non-filesystem); the measured render call is
  `eta.render("@<id>", model)` — Eta's documented cached-template render path. Loops are
  `<% it.xs.forEach(x => { %> … <% }) %>`; conditionals are JS `if/else if/else` blocks;
  fragment-heavy registers `@tile` and invokes `<%~ include("@tile", item) %>`; per-workload
  texts in [templates-and-models.md](templates-and-models.md#eta--controlled-track). The
  idiomatic track additionally uses Eta's native layout system (`<% layout("@…", it) %>` +
  `<%~ it.body %>`) for composed-page and mixed-page, per the plan's idiomatic definition for
  Eta.
- **Rationale.** All constructs verified against the v4 docs (Assumed state). Defaults keep the
  measured engine stock (the same posture as D4); `autoTrim`'s default is irrelevant to the
  byte gate because every controlled template is authored as a single line with no newline to
  trim, and N2–N4 erase whitespace-only variance anyway. `eta.render("@name", …)` rather than a
  captured compiled function is the engine's documented public render path — metrics rule 1
  measures "the engine's cached-template render path", which for Eta is exactly this call.
- **Alternatives rejected.** A separate `autoEscape: false` instance for raw suites with
  `<%= %>` (config-level rather than syntax-level raw output; `<%~ %>` is the documented raw
  tag and keeps one instance per track); `eta.compile()` + direct function invocation (not
  documented on the v4 API overview — relying on it would rest the harness on an unstable
  surface); `rmWhitespace`/custom `autoTrim` (unneeded — single-line authoring; every knob left
  at default is one fewer disclosure).
- **Grounding.** eta.js.org v4 configuration/cheatsheet/layouts/API pages *(fetched, Assumed
  state)*; [metrics-protocol §Metric rules](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules) rule 1.

### D9 — Idiomatic track: Handlebars files are the controlled texts re-cited; Eta files are docs-styled with layouts
- **Decision.** Idiomatic implementations are separate files (per track directories), each with
  the Q1.7 header comment citing the official doc pages it follows. For **Handlebars** the
  idiomatic template texts are the controlled texts (formatted multi-line where the docs
  present multi-line examples — a whitespace-only difference the verifier tolerates): the
  controlled twins already *are* documented-pattern Handlebars (partials, `#each`, `#if`/chained
  `else if`, triple-mustache for trusted HTML, double-mustache for untrusted data); what
  distinguishes the Handlebars idiomatic track is the production-canonical **precompiled**
  execution mode (D7). For **Eta** the idiomatic templates differ structurally where the docs
  prescribe a richer pattern: composed-page and mixed-page use the native layout system;
  remaining workloads are multi-line docs-style versions of the controlled logic. All idiomatic
  implementations pass the contract v2 functional-equivalence verifier before timing.
- **Rationale.** Q1.7 asks for official-documentation patterns with citations — it does not
  require the idiomatic text to differ from the controlled text when the controlled text is
  already the documented pattern (Handlebars' logic-less surface leaves no second idiom to
  prefer). Eta's docs do prescribe a distinct production pattern (layouts), so its idiomatic
  track exercises it, per the plan's explicit "for Eta, its native layout system".
- **Alternatives rejected.** Inventing artificial Handlebars idiom differences (each-with
  block params, custom helpers) to force the tracks apart — measures invented style, not the
  practitioner experience; skipping Eta layouts (contradicts the plan's idiomatic definition).
- **Grounding.** [open-questions Q1.7](../../../plan/open-questions.md);
  [plan §Design direction](../../../plan/phase-4-js.md#design-direction);
  [parity-contract-v2 §Idiomatic-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate).

### D10 — Gates in-process, before `run()`, per contract v2
- **Decision.** Each bench entry script executes its track's gate in the same process before
  any mitata registration: *controlled* — normalize (N1–N4, +N5 for encoded) each of the 16
  engine×workload outputs, byte-compare against the corpus files, plus the encoded security
  floor (raw `<script>alert(` zero occurrences in un-normalized output; escaped form count as
  expected); *idiomatic* — run the JS verifier (value counts, ordered markers,
  forbidden/required) fed from the corpus `<id>.verify.json` files. Any failure throws with the
  contract's failure surface (workload, engine, byte lengths, first-diff index, ±40-char
  excerpt) and exits non-zero **before** `run()` is reached — no numbers exist for a failed
  gate. Implementation shape in [harness-and-run.md §Gate implementation](harness-and-run.md#gate-implementation).
- **Rationale.** Contract v2 controlled-gate rule 2 requires the gate in the same
  process/invocation that produces the timed numbers; the JS verifier consumes the exported
  `.verify.json` exactly as Phase 1's D10 intended for phases 2–6.
- **Alternatives rejected.** A separate gate script run before the bench script (two
  invocations — violates the same-invocation rule); gating only changed workloads (the gate is
  cheap; partial gating invites stale-pass drift).
- **Grounding.** [parity-contract-v2 §Controlled-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#controlled-track-gate)
  rules 2–5; [golden-corpus §Idiomatic verifier definitions](../phase-1-cross-stack-foundation/golden-corpus.md#idiomatic-verifier-definitions).

### D11 — mitata run shape: canonical flags, `do_not_optimize`, automatic warmup, one group per workload
- **Decision.** Every measurement invocation is
  `node --expose-gc --allow-natives-syntax bench/<script>.mjs` (the README's own canonical
  flag set — `--allow-natives-syntax` powers the optimization-status/DCE detection,
  `--expose-gc` gives mitata GC control between benchmarks). Every benchmark body is
  `() => do_not_optimize(render(...))` so the rendered string is consumed via mitata's
  documented DCE guard. Benchmarks are registered as one `group('<workload-id> [<track>]')` per
  workload containing the `handlebars` and `eta` benches. mitata's automatic warmup is used
  unmodified — no manual warmup loops, no custom sampling parameters. Output: the default
  `mitata` format captured to a text artifact, plus a JSON artifact serialized from `run()`'s
  returned `{ benchmarks }` (BigInt-safe replacer) in the same single run — never two runs.
- **Rationale.** Flags and marker semantics verified against the README; `do_not_optimize` and
  `run()`'s return type verified against `main.d.mts` (Assumed state) — one process yields both
  the human-readable capture and the machine-readable numbers from the *same* samples, so the
  published tables and artifacts cannot disagree.
- **Alternatives rejected.** `format: 'json'`-only run (loses the human-readable artifact the
  established report style links); two runs with different formats (two sets of numbers — which
  one is published becomes an integrity question); manual sink variables (undocumented;
  `do_not_optimize` is the tool's own mechanism).
- **Grounding.** mitata README + `main.d.mts` *(fetched, Assumed state)*;
  [metrics-protocol §Wall-time statistic mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21).

### D12 — Deopt/DCE flag handling: a `!`-marked cell is never published unexamined
- **Decision.** After every measurement run, the captured output is scanned for mitata's `!`
  marker ("benchmark was likely optimized out"). Any marked cell blocks publication of the run
  until either (a) the cause is eliminated (the `do_not_optimize` wiring is the first suspect)
  and a clean run is produced, or (b) the marker is reproduced, investigated, and documented in
  the report's results narrative for that cell (what was flagged, why the number is or is not
  credible). The check is mechanical: the runner scans the in-process capture buffer and prints a
  `DEOPT-CHECK: clean` / `DEOPT-CHECK: flagged <bench names>` trailer that the publication
  checklist requires to read `clean` or be explained in prose.
- **Rationale.** Plan success criterion verbatim ("no published number comes from a run in
  which mitata flagged a deoptimization … that was not investigated and documented"); `!` is
  the tool's only documented pathology marker, so the procedure binds to it exactly.
- **Alternatives rejected.** Treating `!` as fatal always (over-strict — a documented,
  explained marker satisfies the plan's criterion); ignoring markers on non-headline cells
  (every published cell is a headline cell under honest-reporting rule 2).
- **Grounding.** mitata README marker documentation *(fetched)*;
  [plan §Success criteria](../../../plan/phase-4-js.md#success-criteria).

### D13 — Windows stability verification: a 5-run repeatability procedure gates first publication
- **Decision.** Because upstream documents no Windows support statement at all, mitata's
  stability on the protocol machine is verified empirically **before any published run**, by
  this procedure (full operational detail in
  [harness-and-run.md §Windows stability verification](harness-and-run.md#windows-stability-verification)):
  execute the controlled-track suite **5 consecutive times** on the idle protocol machine
  (High priority class via the launcher, High-performance power plan, no interactive use);
  compute, per benchmark cell, the relative standard deviation (RSD) of the five `avg` values.
  Verdict thresholds, per cell: **RSD ≤ 5%** — stable, publish; **5% < RSD ≤ 10%** —
  investigate and re-run once; if still above 5%, publishable but the report's environment
  section must disclose the per-cell cross-run RSD table; **RSD > 10% persisting** across the
  re-run — mitata is *not* stability-verified on this machine: no publication, the evidence is
  recorded under `docs/benchmarks/` prep notes, and a harness amendment (the plan names
  tinybench as the weighed alternative) is proposed through the cross-spec amendments ledger
  for maintainer ratification — the harness choice is a plan ruling this spec cannot silently
  change. The stability run's artifacts ship with the published report.
- **Rationale.** The plan's risk table demands exactly this ("spec-verified before any
  published run"); thresholds are chosen against the program's use of the numbers — ratios to a
  Heddle reference row where sub-5% run-to-run drift is comfortably below the cross-engine gaps
  the report interprets, and 10% is the point where a ratio column stops being meaningful. The
  escalation path exists because evidence beyond upstream silence cannot be gathered at spec
  time (the machine procedure *is* the verification); the most-reversible choice is to keep
  mitata and gate on the measurement, with the ledger as the named trigger.
- **Alternatives rejected.** Asserting Windows support from the absence of complaints
  (upstream silence is not evidence; the spike explicitly forbids citing mitata as
  Windows-supported); switching to tinybench pre-emptively (abandons the deopt-visibility
  rationale the plan selected mitata for, on no evidence); realtime priority (REALTIME on
  Windows risks starving the system and distorting timers; High is the established
  benchmark-harness posture, and BenchmarkDotNet's elevated-priority precedent on this box is
  the continuity reference).
- **Grounding.** mitata README OS silence + issue check *(fetched, Assumed state)*;
  [plan §Risks](../../../plan/phase-4-js.md#risks--mitigations);
  [spec-conventions §No open questions](../../common/spec-conventions.md#no-open-questions)
  (most-reversible + trigger); [cross-cutting-decisions — amendments ledger](../../common/cross-cutting-decisions.md#cross-spec-amendments-ledger).

### D14 — Cold compile is measured per-ecosystem for all eight workloads, both engines
- **Decision.** A third bench script measures **cold compile + first render** per controlled
  template: Handlebars — fresh `Handlebars.create()` environment (+ partial/helper
  registration where the workload has them) + `compile(src)` + one render per iteration (the
  render forces Handlebars' lazy compile, so pure-`compile()` timing would be a lie); Eta —
  fresh `new Eta()` + `renderString(src, model)` per iteration. Published as a separate
  per-ecosystem table labeled with the Q1.3 non-comparability rule, methodology stated
  ("cold compile + first render", both engines), baseline column anchored to Handlebars (the
  credibility pick, per Phase 1 D13). It never appears near the cross-comparable tables.
- **Rationale.** Q1.3 permits per-ecosystem cold-cost reporting and both JS engines are runtime
  compilers, so the datum is cheap, native, and practitioner-relevant. Including the first
  render is forced by Handlebars' documented lazy compilation; doing the same for Eta keeps the
  two cells methodologically identical. All eight workloads, uniformly — cherry-picking
  representative templates invites selection criticism.
- **Alternatives rejected.** `Handlebars.precompile` as the cold metric (measures a different
  operation than Eta's compile path and not the one the controlled track executes);
  omitting cold cost entirely (permitted by Q1.3 but discards a zero-risk per-ecosystem
  insight both engines' users care about).
- **Grounding.** [open-questions Q1.3](../../../plan/open-questions.md);
  [Phase 1 D13](../phase-1-cross-stack-foundation/README.md#d13--presentation-rules-q22--a-q62-with-the-allocation-baseline-pinned);
  handlebarsjs.com compilation API (lazy compile — External references).

### D15 — Report: framing, Heddle row, ratio anchor, side-note placement, verified download figure
- **Decision.** The published `index.md` follows the protocol's publication format with these
  phase-specific bindings (verbatim texts and table shapes in
  [harness-and-run.md §Report format](harness-and-run.md#report-format)):
  1. the **reach-not-fair-fight framing statement** appears as the first paragraph of
     `## Results — what this run actually shows`;
  2. every wall-time table carries the **Heddle reference row** labeled
     `Heddle (reference — .NET 10, same machine, from <date> run)` excerpted from the Phase 1
     protocol run, with the ratio column anchored to it (Q2.2 = A, Q6.2) and native units plus
     ns/render normalization per the protocol;
  3. the **Q4.2 side-note** is its own `###` subsection placed after both track narratives,
     outside and below every engine-ranking table, wall-time only, with the framing text of
     harness-and-run.md — it never contributes to the ecosystem verdict prose;
  4. the **time-only statement** (D3) appears immediately after the first wall-time table;
  5. wherever the report motivates the Handlebars pick it cites **39,494,320 weekly npm
     downloads (week 2026-07-13 → 2026-07-19, api.npmjs.org)** — the spec-verified figure,
     re-fetched at publication time with the then-current week substituted (same API, figure
     and week updated together; the plan's 14–22M approximation is superseded — the verified
     number is ~1.8× the plan range's top (≈2.8× its bottom), recorded here as the plan-claim
     correction with evidence);
  6. engine-beats-Heddle and engine-beats-engine callouts follow honest-reporting rule 2 and
     the plan's success criteria (prose callouts, not table-only).
- **Rationale.** Each binding is a plan success criterion or a standing-ruling consequence;
  the download figure was verified first-hand against the primary npm API on 2026-07-20.
- **Alternatives rejected.** Placing the side-note inside the controlled results section
  (adjacency to ranking tables is what Q4.2's "outside the engine-ranking tables" exists to
  prevent); citing the plan's approximate range (plan text itself orders the spec to
  re-verify).
- **Grounding.** [metrics-protocol §Presentation rules / §Publication format](../phase-1-cross-stack-foundation/metrics-protocol.md#presentation-rules-q22--a-q62);
  [open-questions Q4.2, Q2.2, Q6.2](../../../plan/open-questions.md); npm downloads API fetch
  (Assumed state).

### D16 — No excluded cells are budgeted; the Q1.2 machinery is the wired fallback
- **Decision.** This spec plans **zero** excluded controlled-track cells: every workload×engine
  cell has a demonstrated path to the byte gate (D4–D6, D8 — the only genuine divergence,
  Handlebars' apostrophe spelling, is closed by the contract's own N5). If implementation
  nevertheless surfaces divergence beyond the normalization pipeline for some cell, Phase 1's
  D11/Q1.2 machinery applies as-is: the cell prints `excluded — documented evidence` linking a
  written record (divergent bytes, authoring attempts, responsible engine behavior), the
  workload stays in that engine's idiomatic track, and no replacement engine is introduced.
  This is the wired fallback, not an expectation.
- **Rationale.** The plan's validation scenario for a failed conditional-heavy gate must have a
  specified behavior even though D5 shows the fear was retired; wiring the existing policy is
  zero new machinery.
- **Alternatives rejected.** Pre-authorizing any specific exclusion (nothing warrants one).
- **Grounding.** [parity-contract-v2 §Exclusion policy](../phase-1-cross-stack-foundation/parity-contract-v2.md#exclusion-policy);
  [plan §Validation scenarios](../../../plan/phase-4-js.md#validation-scenarios).

## Implementation plan

Ordered; exact paths, change shape, completion check. All paths are repo-relative; the harness
root is `benchmarks/js/` (D1); full file tree in
[harness-and-run.md §Harness layout](harness-and-run.md#harness-layout).

### WI1 — Project scaffold and pins
- **Files.** New: `benchmarks/js/package.json`, `benchmarks/js/.npmrc`,
  `benchmarks/js/.gitignore` (`node_modules/`, `artifacts/`), `benchmarks/js/.gitattributes`
  (`* text eol=lf`), `benchmarks/js/run.ps1`, `benchmarks/js/README.md` (pointer to this spec +
  the reproduce commands); `benchmarks/js/package-lock.json` (generated by the initial
  `npm install`, committed).
- **Change.** Exact contents per [harness-and-run.md §Pinning](harness-and-run.md#dependency-pinning-and-install-story)
  (D2 pins, `engine-strict=true`, `save-exact=true`, `"type": "module"`).
- **Done when.** On Node 24.18.0, `npm ci` succeeds inside `benchmarks/js/`; on any other Node
  version (the pin is the exact string `24.18.0`, so even a point-release difference like 24.19.0
  is refused) it refuses (engine-strict); `git status` shows no untracked files after
  `npm ci` (lockfile stable).

### WI2 — Model transcriptions
- **Files.** New: `benchmarks/js/src/models/composed-page.mjs`, `trivial-substitution.mjs`,
  `large-loop.mjs`, `mixed-page.mjs`, `conditional-heavy.mjs`, `fragment-heavy.mjs`,
  `fortunes-encoded.mjs`, `encoded-loop.mjs`.
- **Change.** One frozen export per workload built at module load (never per benchmark op),
  per the transcription rules and pinned formulas of
  [templates-and-models.md §Models](templates-and-models.md#models--transcription-rules).
- **Done when.** A smoke script confirms the pinned cardinalities (36 products, 200 rows,
  48 tiles, 12 fortunes, 5,000 loop and encoded-loop rows) and spot values
  (`Product 01`, `unit-199`, row-11 payload string byte-identical); final proof is WI4/WI5's
  byte gate.

### WI3 — Gate library (normalization, controlled gate, verifier)
- **Files.** New: `benchmarks/js/src/gate/normalize.mjs`, `src/gate/controlled.mjs`,
  `src/gate/verifier.mjs`, `src/gate/corpus.mjs` (corpus/verify-json loader),
  `src/gate/run-all.mjs` (the `npm run gate` entry: both gates, all 32 cells),
  `benchmarks/js/test/gate-selftest.mjs`.
- **Change.** N1–N5 and both gates exactly as
  [harness-and-run.md §Gate implementation](harness-and-run.md#gate-implementation) specifies
  (closed-list pipeline, N5 encoded-only replacement scan, security floor, verifier check
  kinds and failure surface).
- **Done when.** `node test/gate-selftest.mjs` passes: normalization fixtures (CRLF, BOM
  rejection, inter-tag collapse, N5 spellings incl. leading-zero/hex-case variants) and the
  calibration re-run — for each workload the verifier accepts the committed golden and rejects
  its Phase 1 canonical corruptions (two per raw, three per encoded workload).

### WI4 — Handlebars implementations, both tracks
- **Files.** New: `benchmarks/js/src/templates/handlebars/controlled/<id>.hbs` (8, plus
  `layout.partial.hbs`, `tile.partial.hbs`), `src/templates/handlebars/idiomatic/…`
  (mirror set with Q1.7 citation headers), `src/engines/handlebars.mjs` (environment
  factories: controlled runtime-compile / idiomatic precompile per D7, `area` helper, partial
  registration, per-track render table).
- **Change.** Template texts verbatim from
  [templates-and-models.md](templates-and-models.md#handlebars--controlled-track); engine
  module shape from harness-and-run.md.
- **Done when.** Controlled: all 8 Handlebars cells pass the byte gate (`npm run gate` prints
  eight `[PASS] handlebars <id>` lines). Idiomatic: all 8 pass the verifier. Encoded outputs
  contain zero raw `<script>alert(` and the intact Japanese strings (gate-enforced).

### WI5 — Eta implementations, both tracks
- **Files.** New: `benchmarks/js/src/templates/eta/controlled/<id>.eta` (8, plus
  `tile.partial.eta`, `layout.partial.eta`), `src/templates/eta/idiomatic/…` (incl.
  `shell.layout.eta`, `page.layout.eta`), `src/engines/eta.mjs` (per-track instances,
  `loadTemplate` registration, render table).
- **Change.** Template texts verbatim from
  [templates-and-models.md](templates-and-models.md#eta--controlled-track); D8 configuration.
- **Done when.** Same gate criteria as WI4, for the 8 Eta controlled cells and 8 idiomatic
  cells.

### WI6 — Bench scripts
- **Files.** New: `benchmarks/js/bench/controlled.mjs`, `bench/idiomatic.mjs`,
  `bench/cold-compile.mjs`, `bench/_shared.mjs` (registration + artifact writing),
  `benchmarks/js/src/engines/index.mjs` (aggregates the WI4/WI5 engine render tables into the
  per-track `renderers` table the bench scripts import),
  the npm `scripts` block of `package.json` exactly as pinned in
  [harness-and-run.md §Pinning](harness-and-run.md#dependency-pinning-and-install-story)
  (`gate`, `bench:controlled`, `bench:idiomatic`, `bench:cold`, `selftest` — that package.json is
  the normative source). The Windows stability procedure is **not** an npm script: it is driven by
  `run.ps1 … -Repeat 5` (D13; [harness-and-run.md §Windows stability](harness-and-run.md#windows-stability-verification)).
- **Change.** D10 gates before registration; D11 run shape (`group` per workload,
  `do_not_optimize`, single `run()`, text + JSON artifacts into `benchmarks/js/artifacts/`);
  D12 `DEOPT-CHECK` trailer; D14 cold-compile shape.
- **Done when.** Each script, launched with the canonical flags, gates green, completes, writes
  both artifacts, and prints `DEOPT-CHECK: clean` (or the flagged list) — verified on a
  development machine first, thresholds unjudged there.

### WI7 — Stability verification on the protocol machine
- **Files.** New: `benchmarks/js/artifacts/stability/…` (5 run captures + `stability-summary.md`
  RSD table — copied later into the published directory); `run.ps1` already carries the
  `-Repeat` mode (WI1).
- **Change.** Execute D13's procedure verbatim on the protocol machine; evaluate thresholds;
  record the verdict in `stability-summary.md`.
- **Done when.** The summary table exists with a per-cell RSD verdict and an overall
  `STABILITY: verified` / `verified-with-disclosure` / `failed` line; `failed` blocks WI8 and
  triggers D13's ledger escalation.

### WI8 — Measurement run and published report
- **Files.** New: `docs/benchmarks/<run-date>/index.md`, plus copied artifacts:
  `js-controlled.txt/.json`, `js-idiomatic.txt/.json`, `js-cold-compile.txt/.json`,
  `stability-summary.md`.
- **Change.** One publication run per track on the protocol machine (canonical flags, run.ps1
  launcher); author `index.md` per D15 and
  [harness-and-run.md §Report format](harness-and-run.md#report-format).
- **Done when.** The publication checklist in harness-and-run.md is fully satisfied (framing
  statement, Heddle rows + ratios, time-only statement, allocation label n/a-but-cold-table
  labeled, encoded caveat verbatim, side-note present and placed per D15, helper disclosure
  text present, download figure current-week verified, `DEOPT-CHECK: clean` or documented,
  environment block complete with reproduce commands).

## Public API / contract

No .NET or published-package API is added or changed. The harness is a private npm workspace
(`"private": true`) that ships nothing. The externally consumed artifacts this phase produces:

| Artifact | Consumers | Normative definition |
|---|---|---|
| `docs/benchmarks/<date>/` JS report + artifacts | Phase 7 consolidated report (JS rows); readers | [harness-and-run.md §Report format](harness-and-run.md#report-format) |
| `benchmarks/js/` harness convention | Phases 2, 3, 5, 6 (layout precedent per D1) | [harness-and-run.md §Harness layout](harness-and-run.md#harness-layout) |

Thread-safety: not applicable — the harness is a single-threaded Node process; models are
frozen module-level constants.

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — no compiler, grammar, or engine surface changes; the
diagnostic registry is untouched. The harness's own error surface (all host-side):

| Surface | Exit | Message shape | Trigger |
|---|---|---|---|
| Controlled byte gate | 1, before `run()` | `[FAIL] <engine> <workload>: length exp <n> / act <m>; first diff at <i>` + ±40-char excerpt (`\n`-escaped) | normalized candidate bytes ≠ corpus entry |
| Encoded security floor | 1, before `run()` | `[FAIL] <engine> <workload> security: raw "<script>alert(" found <M> times (expected 0)` | raw payload in un-normalized output, or escaped-form count mismatch |
| Idiomatic verifier | 1, before `run()` | `[FAIL] <engine> <workload> <value\|marker\|forbidden\|required>: expected <…>, found <…>` | any verifier check miss |
| Corpus loader | 1 | `corpus entry <id> not found under src/Heddle.Performance/GoldenCorpus/ — run Phase 1 export-corpus first` | missing corpus/verify files |
| Node version guard | 1 (npm) | npm engine-strict refusal | `node --version` ≠ pinned engines value |
| DEOPT-CHECK | 0 (advisory; publication-blocking via checklist) | `DEOPT-CHECK: clean` / `DEOPT-CHECK: flagged <names>` | `!` marker in captured output |
| Stability verdict | recorded, not an exit code | `STABILITY: verified \| verified-with-disclosure \| failed` | D13 thresholds |

## Testing plan

**TDD verdict.** The gates are the executable spec, red-first by construction: WI3's gate
library lands before any template (its self-test runs against Phase 1's committed goldens and
corruptions), so `npm run gate` starts all-red and turns green per cell through WI4/WI5 —
the same discipline as the intra-.NET suite. No xUnit additions; `src/` and `Heddle.Tests` are
untouched.

**Named checks.**
- Goldens: consumed read-only from `src/Heddle.Performance/GoldenCorpus/` — this phase adds
  none and never regenerates them.
- Gate commands: `npm run gate` (both tracks, all 32 cells), `node test/gate-selftest.mjs`
  (normalization fixtures + calibration re-run against the Phase 1 corruption definitions).
- Negative/security: the encoded security floor per encoded cell; the verifier corruption
  rejections; the BOM/invalid-UTF-8 normalization fixtures.
- Benchmarks: `bench/controlled.mjs`, `bench/idiomatic.mjs`, `bench/cold-compile.mjs` — each
  self-gating (D10).

**Regression gate (before merge of each WI batch and before WI8).**
1. `npm ci` clean on pinned Node (lockfile stable, WI1 check).
2. `node test/gate-selftest.mjs` — exit 0.
3. `npm run gate` — 32 `[PASS]` lines, exit 0.
4. `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- verify-corpus` —
   exit 0 (proves the corpus this phase gated against is fresh; detects any concurrent corpus
   change).
5. No diffs outside `benchmarks/js/` and (at WI8) `docs/benchmarks/<run-date>/`.
6. WI8 additionally: the publication checklist, item by item.

## Back-compat and migration

- **Purely additive.** New `benchmarks/js/` tree and one new `docs/benchmarks/<date>/`
  directory; no existing file is modified anywhere (the intra-.NET suite, the corpus, contract
  docs, and published reports are consumed read-only). No breaking window is engaged.
- **First npm surface in the repo** (plan-flagged impact): contained by `"private": true`, a
  committed lockfile, `.gitignore` for `node_modules/`, and exact-version pins; nothing outside
  `benchmarks/js/` acquires a JS toolchain dependency.
- **Corpus regeneration during this phase's lifetime** follows Phase 1's Q1.5 posture: a
  regenerated corpus is an ordinary versioned change; the JS gates simply re-run against the
  current bytes (regression-gate step 4 catches staleness).
- **Prior reports:** untouched, immutable; the Heddle reference rows are excerpts of already-
  published numbers, never recomputed.

## Performance considerations

- The harness measures competitors, not Heddle; no Heddle hot path is touched. Harness-side
  disciplines that protect measurement validity: models frozen at module load (no per-op
  allocation of model data); templates compiled/registered once at startup outside all timed
  regions (metrics rule 1); render output consumed via `do_not_optimize` (DCE guard);
  gates and artifact writing sit entirely outside `run()`'s sampling.
- The `encoded-loop` render (~1 MB output, 5,000 rows) dominates per-op allocation pressure on
  V8 for both engines equally; with the suite time-only (D3), GC noise shows up only as
  dispersion, which the protocol publishes alongside every point estimate and the D13
  procedure bounds across runs.
- Guarding benchmarks: the three bench scripts themselves; the published run is the recorded
  JS baseline for phase 7 and any future re-run.

## Standards compliance

- **DRY:** one model module per workload feeds both engines and both tracks (snake_case keys
  chosen once, per the Phase 1 dictionary convention); the normalization pipeline exists once
  in `gate/normalize.mjs` and serves the controlled gate, the verifier, and the self-test;
  template texts live once as files, referenced by both compile modes.
- **YAGNI:** no cross-engine harness abstraction layer (two engines get two small modules);
  no JSON-schema validation of `verify.json` (Phase 1's exporter is the single producer); no
  Bun/Deno hooks, no memory instrumentation pass (deferred with triggers), no auto-generated
  report tooling (Phase 1 already deferred it program-wide).
- **Deliberate duplication:** the two bench entry scripts repeat the register-gate-run shape
  per track rather than sharing a parameterized mega-runner — a failing track must be
  diagnosable at a glance, mirroring the benchmark-class discipline of the .NET suite.

## Deferred items

| Item | Trigger |
|---|---|
| JS memory metrics via a separate, explicitly-scoped instrumentation pass (`process.memoryUsage()` around forced GC) | mitata documenting memory semantics upstream, or a user request for per-ecosystem JS memory data (Q4.1 stays closed either way — any such pass would be new machinery, proposed via the amendments ledger) |
| Harness swap to tinybench | D13 stability verdict `failed` (ledger escalation as specified there) |
| Bun/Deno runtime rows | Out of plan scope by standing ruling; only a plan-level re-ruling reopens it |
| mitata `format: 'markdown'` artifacts alongside text+JSON | A phase 7 ingestion need the JSON artifact does not already satisfy |
| Moving the golden corpus out of `src/Heddle.Performance/GoldenCorpus/` | Not triggered by this phase (D1) — the JS gate consumes the current path without difficulty |

## External references

- Handlebars: <https://handlebarsjs.com/> — [built-in helpers (`#if`, `#each`)](https://handlebarsjs.com/guide/builtin-helpers.html),
  [partials + partial contexts](https://handlebarsjs.com/guide/partials.html),
  [precompilation guide (`--knownOnly`)](https://handlebarsjs.com/guide/installation/precompilation.html),
  [compilation API](https://handlebarsjs.com/api-reference/compilation.html);
  `escapeExpression` table: <https://github.com/handlebars-lang/handlebars.js/blob/master/lib/handlebars/utils.js>;
  chained else blocks (v3.0.0): <https://github.com/handlebars-lang/handlebars.js/blob/master/release-notes.md>
- Handlebars npm registry + downloads (verified 2026-07-20):
  <https://registry.npmjs.org/handlebars/latest>,
  <https://api.npmjs.org/downloads/point/last-week/handlebars>
- Eta v4 docs (verified 2026-07-20): <https://eta.js.org/docs> —
  [configuration](https://eta.js.org/docs/4.x.x/api/configuration),
  [API overview](https://eta.js.org/docs/4.x.x/api/overview),
  [syntax cheatsheet](https://eta.js.org/docs/4.x.x/syntax/cheatsheet),
  [layouts and blocks](https://eta.js.org/docs/4.x.x/syntax/layouts-and-blocks);
  escaper source: <https://github.com/eta-dev/eta/blob/main/src/utils.ts>;
  registry: <https://registry.npmjs.org/eta/latest>
- mitata (verified 2026-07-20): <https://github.com/evanwashere/mitata> (README: flags, `!`
  marker, "estimated heap usage"); type surface:
  <https://github.com/evanwashere/mitata/blob/master/src/main.d.mts>; registry:
  <https://registry.npmjs.org/mitata/latest>
- Node.js 24.x (verified 2026-07-20): <https://nodejs.org/dist/latest-v24.x/>,
  <https://github.com/nodejs/Release>
- Are-We-Fast-Yet disclosed-idiom discipline: <https://github.com/smarr/are-we-fast-yet>
  (adopted by the plan/Phase 1)
- Phase 1 spec documents (binding inputs):
  [README](../phase-1-cross-stack-foundation/README.md),
  [workloads](../phase-1-cross-stack-foundation/workloads.md),
  [parity-contract-v2](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  [golden-corpus](../phase-1-cross-stack-foundation/golden-corpus.md),
  [metrics-protocol](../phase-1-cross-stack-foundation/metrics-protocol.md)
- Repo conventions bound by this spec:
  [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [coding-standards](../../common/coding-standards.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md)
