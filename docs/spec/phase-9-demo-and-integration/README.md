# Phase 9 specification — demo page & integration demos

Status: **Specified — ready for implementation.**
Roadmap source: [phase 9 — demo page & integration demos](../../roadmap/phase-9-demo-and-integration.md).
Prior phases assumed merged
([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
[phase 1 — native expressions](../phase-1-native-expressions/README.md),
[phase 2 — safe output](../phase-2-safe-output/README.md),
[phase 3 — branching](../phase-3-branching/README.md),
[phase 4 — ergonomics](../phase-4-ergonomics/README.md),
[phase 5 — props & slots](../phase-5-props-and-slots/README.md),
[phase 6 — tooling / LSP](../phase-6-tooling-lsp/README.md),
[phase 7 — build-time compilation](../phase-7-build-time-compilation/README.md),
[phase 8 — streaming & async](../phase-8-streaming-async/README.md).

Because this phase is **last** in the sequential order, every prior phase's "deferred
phase 9 gallery item" comes due here: this spec is both the harness spec and the
collection point for all eight phases' deferred demo/integration deliverables. The
roadmap's "the gallery structure can start any time" flexibility is therefore moot under
D5 and is recorded as such (D13), not specified as an independent-shipping path.

### Documents

| Document | Purpose |
| --- | --- |
| README.md (this file) | The spec proper: assumed state, decision records (all three of the roadmap's recorded implementation choices closed), implementation plan, API/diagnostics contract, testing, back-compat, performance. |
| [wasm-demo.md](wasm-demo.md) | The typed in-browser demo supplement: WASM host project shape, trimming/feature-switch mechanics, the worker `postMessage` protocol, Ace binding, render loop, deploy staging, and the Playwright smoke scenarios. |
| [gallery.md](gallery.md) | The integration gallery supplement: `samples/` layout, the per-sample authoring template, the full sample inventory (one entry per collected deferred item), golden mechanics, and the `samples.yml` CI harness. |

## Scope and goal

Two deliverables that close the roadmap:

1. **The typed in-browser demo.** [docs/public/demo.html](../../public/demo.html) grows
   from syntax-level checking (ANTLR JS parser in the Ace worker, plus a hard-coded
   word-list completer) to the real language experience: typed member completion, hover
   types, real positioned `HED*` compile diagnostics, and **live template rendering** —
   all served from GitHub Pages static hosting by running the phase 6
   `Heddle.LanguageServices` facade and the engine's Roslyn-free native tier inside a
   `browser-wasm` module worker. The current page behavior remains intact as the
   no-WASM fallback layer.
2. **The integration demo gallery as the end-to-end test suite.** A repo-root `samples/`
   folder with ten small, complete, README'd projects — the three current-engine demos
   from the roadmap plus the seven deferred items collected from the phase 1–8 specs —
   each golden-asserted in CI by a new matrix workflow. A broken sample *is* a failed
   integration test; the gallery green plus the demo smoke test green is the roadmap's
   combined end-to-end validation (D15).

Zero grammar change. The engine change surface is **one additive trim-time feature
switch** (D4) — a verified correction to the roadmap's "zero engine change" claim, with
the evidence recorded there. Everything else is applications (samples, the WASM host,
the demo page) and CI.

## Assumed state

Every seam below was re-verified against the current source (July 2026; searches
excluded `bin/`, `obj/`, `node_modules/`, `generated/`). Corrections and refinements to
roadmap claims are marked ⚠ and closed in the decision records.

| Seam | Verified state |
| --- | --- |
| [docs/public/demo.html](../../public/demo.html) | Self-contained static page. Loads `ace/ace.js` (`ace.config.set("basePath", "ace")`), sets `ace/mode/heddle`, `useWorker: true`, ships a starter template using `@model(){{dynamic}}`, has a light/dark toggle persisted to `localStorage`, and states in its header comment and visible note that it "does NOT render templates — the engine is .NET". ⚠ "Highlight-only" is imprecise: the Ace mode already ships a **static word-list completer** and multi-language linting (next row) — the upgrade is *word-list → typed*, not *nothing → typed* (D7). |
| [src/Heddle.Language/js/src/mode](../../../src/Heddle.Language/js/src/mode) | `heddle_worker.js` is an Ace `Mirror` worker: parses via the ANTLR-generated JS `DocumentParser`, emits `annotate` with positioned parse errors (using `doc.indexToPosition` — the offset↔row/col conversion the typed layer reuses), and additionally lints embedded HTML (SAX), CSS (CSSLint), and JS (JSHint). `heddle_completions.js` hard-codes 22 extension names with `name($0)` snippets — a divergence risk from the live registry that D7 retires to fallback duty. |
| Ace bundle pipeline | [build_ace.sh](../../../src/Heddle.Language/build_ace.sh) clones **Ace v1.32.6**, injects `js/src/` + the ANTLR JS runtime, and builds with dryice; `generate_js.cmd` regenerates the JS parser from the grammar (ANTLR 4.13.1) — the JS parser under `js/` is committed, so CI needs no Java (matches [architecture.md](../../architecture.md) and the workflow comment). The pinned build **contains `HoverTooltip`** (`ace_build/ace/lib/ace/tooltip.js`, exported in the built `ace.js`) and `ext-language_tools.js` with `getDocTooltip` — verified in the working tree, the D1 hover/completion seams need no Ace upgrade. |
| [.github/workflows/docs.yml](../../../.github/workflows/docs.yml) | Push-to-main + dispatch only; paths: `docs/**`, the workflow itself, `src/Heddle.Language/js/**`, `build_ace.sh`. Build job: checkout (full history) → Node 22 (npm cache on `docs/package-lock.json`) → `bash build_ace.sh` → stage `ace_build/ace/build/src-noconflict/.` into `docs/public/ace/` → `npm ci` → `npm run docs:build` → upload `docs/.vitepress/dist` → separate deploy job (`deploy-pages@v5`, `github-pages` environment, `pages` concurrency group). ⚠ No .NET SDK setup, no test step, no PR trigger — D9/D10 add all three. |
| [.github/workflows/dotnet.yml](../../../.github/workflows/dotnet.yml) | Build+test matrix (ubuntu/windows) on push-main/tags/PR; internal PRs publish beta packages, tags publish releases. ⚠ Entangled with NuGet publishing — the sample harness gets its own workflow instead of joining it (D12). |
| `docs/public/ace/` staging posture | Gitignored (`.gitignore` roots `/docs/public/ace/` and `/src/Heddle.Language/ace_build`); only `demo.html` is tracked under `docs/public/`. Build outputs under `docs/public/` are workflow-staged, never committed — the WASM bundle follows the same posture (D9). |
| [docs/.vitepress/config.mts](../../.vitepress/config.mts) | `base: '/Heddle/'`; `docs/public/**` is copied verbatim to the site root; the Demo nav link opens `/demo.html` with a real navigation; `srcExclude: ['assessment.md']` (roadmap/spec exclusions remain the pending [D9 integration step](../common/cross-cutting-decisions.md#d9--spec-and-roadmap-pages-stay-unpublished)); `vite.resolve.preserveSymlinks: true` is the Windows drive-casing fix — docs builds on Windows must run from an uppercase-drive cwd. [docs/package.json](../../package.json): VitePress `^1.6.3`, mermaid plugin; scripts `docs:dev`/`docs:build`/`docs:preview`. |
| [src/Heddle/Heddle.csproj](../../../src/Heddle/Heddle.csproj) | ⚠ **`Heddle` hard-references `Microsoft.CodeAnalysis.CSharp` on every TFM** (4.1.0 → 5.3.0 per-TFM groups), and the Roslyn pass `ContextCompilation.Compile` is statically reachable from every compile entry point — so a naive `browser-wasm` publish ships Roslyn (~14 MB of assemblies) despite the demo never executing the C# tier. This is the load-bearing verification behind D4's feature switch; the roadmap's "zero engine change" was written without it. |
| Phase 1 artifacts used | `ExpressionMode` (`Native` keeps the C# tier out of the compile; C#-tier constructs produce positioned `HED1xxx` diagnostics — the "declined gracefully" surface), `FunctionRegistry` with frozen invariant-culture defaults, the native tier compiling to `System.Linq.Expressions` trees — the property that makes in-browser rendering possible at all. |
| Phase 2–5 artifacts used | `OutputProfile.Html` + `raw` (the demo renders under the Html profile; `samples/html-safe-output`); branch extensions + the public `Scope.Publish`/`TryRead` channel (`samples/custom-extensions`); `@for`/`range` + `TrimDirectiveLines` (gallery upgrades per phase 4's deferred note); props/slots (`samples/component-props-slots`, completion fixture C04). |
| Phase 6 artifacts used | The **facade is deliberately offset-based** (phase 6 D11: "reusable by the phase 9 WASM host, which has no LSP positions"): `HeddleLanguageService` (`Analyze`, `GetCompletions`, `GetHover`, `GetDefinition`, `Close`), immutable `DocumentAnalysis`/`HeddleDiagnostic`/`CompletionResult`/`HoverResult`, options (`AssemblyPaths`, `RootPath`, `OutputProfile`, `ExpressionMode`, `FileNamePostfix`), TFMs `net8.0;net10.0` (browser-wasm on `net10.0` resolves it), no StreamJsonRpc dependency (that is the server's). The 300 ms debounce lives in the *host* (server `DocumentStore`), not the facade — the browser host implements its own (D8). The completion worked examples C01–C10 in [protocol.md](../phase-6-tooling-lsp/protocol.md#completion) are the cross-host fixture source (D14). |
| Phase 7/8 artifacts used | `PrecompiledTemplates.Entries`/`TryGet`/`Register` + `PrecompiledMismatchPolicy` + `HeddleEmitUtf8Pieces` (`samples/precompiled-app`, `samples/codegen-t4-successor`, differential rule); `HeddleTemplate.Generate(data, TextWriter/IBufferWriter<byte>, …)` (`samples/streaming-ssr`). |
| Render entry points | [HeddleTemplate](../../../src/Heddle/HeddleTemplate.cs): `HeddleTemplate(string document, CompileContext)` compiles an inline string; `string Generate(object data, object chained = null, object callerData = null)`; `HeddleTemplate.Configure(Assembly)` seeds the engine's assembly list for type resolution by name — the browser host's model-registration path (models are compiled in, no ALC loading). Documented in [csharp-api.md](../../csharp-api.md). |
| Blog model corpus | [language-reference.md](../../language-reference.md): `Blog { Title, Year, Articles }`, `Article { Title, Author, PublishedOn, IsFeatured, Tags, Comments }`, `Author { Name, Email }`, `Comment { Author, Text, Replies }` — the published docs model, compiled into the demo model assembly (D5). Phase 6's *test* corpus uses a slight variant (`Summary`/`Rating`); the demo ships the published shape and the cross-host fixtures are re-pointed at it where member names differ (D14). |
| Test corpus | `src/Heddle.Tests/TestTemplate/**` sibling-pair fixtures/goldens with the `.Replace("\r\n", "\n")` normalization convention — the golden discipline the gallery inherits (D11). |

## Design decisions

Numbering is local to this spec; cross-cutting decisions are cited by link. The
roadmap's three recorded implementation choices are closed in D1–D3; every remaining
roadmap lean/pin is closed in D4–D16.

### D1 — Editor: Ace stays for v1; typed features bind to `language_tools` + `HoverTooltip` (roadmap choice 1, closed)

**Decision.** The typed demo keeps **Ace** at the existing **v1.32.6** pin. The three
feature bindings, all verified available in the pinned build:

- **Completion** — a dynamic `Completer` registered via `ext-language_tools`
  (`getCompletions(editor, session, pos, prefix, callback)` calling the WASM worker;
  `getDocTooltip` renders the item's `detail`/signature documentation), replacing the
  hard-coded word list in `heddle_completions.js`, which is retired to the no-WASM
  fallback layer (D7).
- **Hover** — the `HoverTooltip` class from `ace/tooltip` (present in the pinned build:
  `ace_build/ace/lib/ace/tooltip.js`, exported by the built `ace.js`), showing the
  facade's hover markdown rendered as text + signature line.
- **Diagnostics** — `session.setAnnotations` merge: base-layer parse annotations from
  the existing Ace worker plus typed `HED*` diagnostics from the WASM worker (D7).

Monaco is rejected for v1. The facade protocol is editor-agnostic and offset-based, so a
later swap is a frontend exchange, not a redesign — that reversibility is the recorded
escape hatch, with the trigger below.
**Rationale (grounded, July 2026).** Both editors are actively maintained — `ace-builds`
1.44.0 published ≈ May 2026, `monaco-editor` 0.55.1 published ≈ December 2025 — so
maintenance state does not discriminate. What does: (a) the custom Ace bundle, Heddle
mode, worker pipeline, and theme wiring already exist and are exercised in production
(the current demo); Monaco would discard all of it; (b) bundle weight — Ace's core is
sub-megabyte gzipped (~98 KB min+gz measured by Replit's comparison) while Monaco's
editor bundle is multiple MB (its 0.55 bundle discussion reports 6+ MB artifacts) — on a
page that must *also* lazy-load a multi-MB .NET runtime, the editor must stay light;
(c) Ace's extension APIs demonstrably support LSP-grade UX — the `ace-linters` project
ships completion/hover/diagnostics over **WebWorker transports** on stock Ace, the exact
topology this phase builds.
**Alternatives rejected.** Monaco (above — weight and pipeline discard for richer
widgets the demo does not need); upgrading the Ace pin to 1.44.x in this phase (the
pinned build already contains every API this spec binds; a pin bump is an independent
maintenance change with its own dryice-build risk — trigger: an Ace defect surfacing in
the smoke suite); a from-scratch tooltip/popup layer (re-implements what the pinned
bundle ships).
**Grounding.** [ace-builds — npm](https://www.npmjs.com/package/ace-builds) (1.44.0
current); [HoverTooltip — Ace API reference](https://ajaxorg.github.io/ace-api-docs/classes/src_tooltip.HoverTooltip.html);
[Ace autocompletion wiki](https://github.com/ajaxorg/ace/wiki/How-to-enable-Autocomplete-in-the-Ace-editor)
and [Completer interface](https://ace.c9.io/api/interfaces/ace.Ace.Completer.html)
(`getCompletions`, `getDocTooltip`); [ace-linters](https://github.com/mkslanc/ace-linters)
(WebWorker LSP client precedent); [monaco-editor — npm](https://www.npmjs.com/package/monaco-editor)
(0.55.1); [monaco-editor #5154](https://github.com/microsoft/monaco-editor/issues/5154)
(0.55 bundle > 6 MB); [Replit — comparing code editors](https://blog.replit.com/code-editors)
(Ace ≈ 98 KB min+gz); repo verification of the pinned build (Assumed state).

### D2 — In-browser rendering ships in this phase (roadmap choice 2, closed)

**Decision.** The demo renders templates live, in this phase. The roadmap's question —
render "strictly after phase 1" or attempt earlier against the pre-phase-1 language —
is closed **by construction** under
[cross-cutting D5](../common/cross-cutting-decisions.md#d5--sequential-implementation-order):
phase 9 is implemented last, phases 1–8 are merged, so the "strictly after phase 1" lean
is automatically satisfied and the "earlier variant that demos a language about to
change" no longer exists as an option. Rendering runs the engine's native tier
(`ExpressionMode.Native`), whose compiled `System.Linq.Expressions` trees execute
**interpreted** under the WASM runtime — verified viable without JIT (D3 grounding).
**Rationale.** Formal closure of a choice the sequential order dissolved; recorded so
the roadmap's remaining-choices list is fully accounted for.
**Alternatives rejected.** None remain reachable — the alternative required a world
(phase 9 before phase 1) the ratified build order forbids.

### D3 — WASM toolchain: plain `Microsoft.NET.Sdk.WebAssembly` browser bundle in a module worker; no Blazor, no AOT (roadmap choice 3, closed)

**Decision.** The WASM host is a **plain .NET WebAssembly browser app** (the
`wasmbrowser` template shape): SDK `Microsoft.NET.Sdk.WebAssembly`, `net10.0`,
`AllowUnsafeBlocks` (required by the interop source generator), `[JSExport]` methods as
the API surface, booted inside a **module Web Worker** by importing
`./_framework/dotnet.js` and dispatching `postMessage` envelopes — exactly the pattern
Microsoft's ".NET on Web Workers" guidance documents for .NET 8–10. Build prerequisites:
the `wasm-tools` workload (MSBuild targets; installed in CI by D9) and the
`Microsoft.NET.Runtime.WebAssembly.Templates.net10` template package for local
scaffolding. Runtime posture:

- **No AOT.** The IL interpreter (+ the runtime's built-in Jiterpreter tiering) runs the
  engine; AOT would multiply download size and *still* force `System.Linq.Expressions`
  into interpreted form — it buys nothing for this workload.
- **`System.Linq.Expressions` works interpreted** — verified: `LambdaExpression`
  compilation respects `RuntimeFeature.IsDynamicCodeSupported` and falls back to the
  expression interpreter where IL emission is unavailable, and Microsoft's AOT guidance
  documents the always-interpreted behavior; on the default (interpreter) runtime,
  `Expression.Compile()` functions without JIT. Demo-scale latency is acceptable
  (Performance).
- **Trimming stays on** (the wasm publish default), with reflection roots per D4/D5;
  `InvariantGlobalization=true` (≈ 30 % size cut, verified numbers below; consistent
  with phase 1's invariant-culture function defaults and deterministic smoke goldens);
  **Webcil** default packaging kept (assemblies travel as `.wasm` — static-host,
  firewall, and MIME friendly on GitHub Pages).
- **Size expectation (grounded baseline):** a hello-world `wasmbrowser` release publish
  measures ≈ 6.8 MB raw / 2.5 MB gzip / 2.0 MB brotli, dropping to ≈ 4.3 / 1.7 / 1.4 MB
  with invariant globalization. The Heddle bundle adds the engine, facade, ANTLR runtime,
  and demo models on top; D9 pins the CI budget gate at **12 MB raw publish size** with
  the measured number recorded at implementation as the standing baseline.

Blazor WebAssembly is **not** used, not even "just for hosting the runtime".
**Rationale.** The host is headless — it needs a runtime, JS interop, and a worker; it
renders no UI, routes nothing, and must boot inside a worker where Blazor's component
pipeline is dead weight. The plain browser bundle is the officially supported,
officially documented way to run a .NET class library from JS (including in workers),
and it keeps the download to runtime + assemblies with no framework tax.
**Alternatives rejected.** Blazor WASM hosting (component framework, router, and boot
UI shipped to run zero components; its lazy-assembly and render features are
main-thread/UI concepts that do not apply in a worker); AOT compilation (size explosion
for interpreted-anyway expression trees); `wasmconsole`/Node targeting (wrong host —
the demo runs in browsers); a self-built Emscripten/NativeAOT-LLVM toolchain (unsupported
for this shape; the engine's runtime reflection is structurally incompatible with
NativeAOT — posture carried from phases 6/7).
**Grounding.** [JSImport/JSExport interop with a WebAssembly browser app](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0)
(`wasm-tools` required, template packages, csproj shape, `dotnet.js` boot,
`getAssemblyExports`); [.NET on Web Workers](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers?view=aspnetcore-10.0)
(module-worker `worker.js` + `postMessage` command envelope, .NET 8–10 monikers, updated
April 2026); [Andrew Lock — running .NET in the browser without Blazor](https://andrewlock.net/running-dotnet-in-the-browser-without-blazor/)
(.NET 10-era size measurements above); [dotnet/runtime #80759](https://github.com/dotnet/runtime/pull/80759)
(`CanCompileToIL` respects `IsDynamicCodeSupported` — the interpreter fallback);
[Blazor WebAssembly build tools and AOT](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0)
(interpreter default; under AOT `System.Linq.Expressions` always interprets);
[Webcil packaging](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0)
(default `.wasm` assembly packaging).

### D4 — Roslyn stays out of the bundle: the `Heddle.CSharpTierEnabled` feature switch (roadmap correction; the phase's one engine touch)

**Decision.** `Heddle` gains the standard library **feature-switch** triple — the only
engine change this phase makes, and a verified correction to the roadmap's "zero engine
change" claim:

1. `internal static class HeddleFeatures` (new, `src/Heddle/Runtime/HeddleFeatures.cs`):
   `internal static bool CSharpTierEnabled => !AppContext.TryGetSwitch("Heddle.CSharpTierEnabled", out bool enabled) || enabled;`
   (`AppContext.TryGetSwitch` exists on `netstandard2.0` — all engine TFMs compile it).
2. A guard at the single Roslyn entry: `ContextCompilation.Compile(this CompileScope)`
   (and its `CompileContext` overload) early-returns when the switch is off and at least
   one C# method is registered, collecting **HED9001** (Diagnostics) on the context —
   compile-path problems are collected, never thrown, per the
   [coding standards](../common/coding-standards.md#error-handling-and-diagnostics). All
   Roslyn-typed code stays reachable **only** through the guarded branch (members whose
   signatures mention `Microsoft.CodeAnalysis` types remain private to
   `ContextCompilation` and are called only past the guard) so link-time branch removal
   eliminates the entire Roslyn reference graph.
3. `ILLink.Substitutions.xml` embedded in `Heddle`, mapping feature
   `Heddle.CSharpTierEnabled` = `false` to a constant-`false` stub of
   `HeddleFeatures.CSharpTierEnabled` — the documented feature-switch pattern the BCL
   itself uses. The WASM host opts in with
   `<RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" />`;
   every other consumer is untouched (switch unset ⇒ `true` ⇒ behavior identical, and
   the substitution never activates outside trimmed publishes that set the switch).

The publish gate greps the staged `_framework` for `Microsoft.CodeAnalysis` and fails
the build if present (D9) — the "Roslyn-free in-browser" claim is asserted, not assumed.
**Rationale.** Verified in Assumed state: `Heddle.csproj` references
`Microsoft.CodeAnalysis.CSharp` on **all four TFMs** and the Roslyn pass is statically
reachable from every compile entry, so the trimmer must keep it absent a dead branch —
shipping ~14 MB of Roslyn contradicts the roadmap's own "shipping Roslyn to the browser
is not reasonable". The feature switch is additive, behavior-preserving by default,
carries no public API, and follows phase 6's precedent of a roadmap "zero engine change"
phase landing additive internal plumbing with the correction recorded.
**Alternatives rejected.** Shipping Roslyn and lazy-loading it (≈ +4–5 MB compressed
that no browser code path can ever execute; contradicts ratified direction); app-side
linker surgery (`_ExtraTrimmerArgs --substitutions`, post-publish file deletion, boot
manifest editing — all unsupported/fragile mechanisms against documented guidance);
splitting `Heddle` into engine + Roslyn packages (a 2.0-scale packaging break for a
demo-driven need; trigger recorded in Deferred items).
**Grounding.** [Heddle.csproj](../../../src/Heddle/Heddle.csproj) (per-TFM Roslyn
references — the evidence); [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
(feature switches + substitutions); [Trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options)
(`RuntimeHostConfigurationOption` with `Trim="true"`).

### D5 — What compiles to WASM: two new projects, compiled-in models, selective trim roots

**Decision.** Two new projects, applications/content only (no NuGet packages):

| Project | Contents | Notes |
| --- | --- | --- |
| `src/Heddle.Demo.Models/` | The published blog corpus (`Blog`/`Article`/`Author`/`Comment` exactly as [language-reference.md](../../language-reference.md) defines them), `DemoCatalog` — named model sets, each = root type + deterministic canned instance + starter template text | `net10.0` class library; **fully trim-rooted** (`TrimmerRootAssembly`) — completion/hover/render reflect over it |
| `src/Heddle.Demo.Wasm/` | The browser host: `DemoInterop` (`[JSExport]` surface), `DemoHost` (facade + render orchestration), `DemoJsonContext` (source-generated STJ), `wwwroot/heddle-demo-worker.js` (module worker), staging script | `Microsoft.NET.Sdk.WebAssembly`, `net10.0`, references `Heddle.LanguageServices` + `Heddle.Demo.Models` |

Assemblies shipped in `_framework`: the host, `Heddle`, `Heddle.Language`,
`Antlr4.Runtime.Standard`, `Heddle.LanguageServices`, `Heddle.Demo.Models`, and the
trimmed BCL — **no Roslyn** (D4), no StreamJsonRpc (server-only, never referenced by the
facade — verified phase 6 D21). Model registration uses the existing public seam:
`HeddleTemplate.Configure(typeof(Blog).Assembly)` at host startup (the browser cannot
load user assemblies; `HeddleLanguageServiceOptions.AssemblyPaths` stays empty and the
facade's ALC/watcher machinery is simply never invoked). Trim roots beyond the models
assembly, via one `TrimmerRootDescriptor` (`linker/Heddle.Demo.roots.xml`): the engine's
`[ExtensionName]` extension types (reflection-instantiated via `TemplateFactory`) and
the `FunctionRegistry` default-method holders (reflection-invoked). The descriptor
deliberately does **not** root `ContextCompilation` (D4's branch removal must strip it).
The boot smoke test (D10) is the completeness check for the root set: a trimmed-away
member fails completion/render visibly there.
**Rationale.** Curated compiled-in models are the roadmap's design ("a feature, not a
limitation"); the public `Configure` seam already exists for exactly this registration;
selective rooting keeps the trimmer effective where the engine reflects.
**Alternatives rejected.** Rooting all of `Heddle` (keeps the Roslyn-typed helpers and
defeats D4); `MetadataLoadContext`-style model description (forks the resolution path —
the phase 6 maintenance rule forbids parallel implementations); user-supplied model
JSON→type synthesis in the browser (`Reflection.Emit` under the interpreter is possible
but a new engine-adjacent surface with no roadmap mandate — deferred with trigger).

### D6 — Worker topology and the facade-shaped `postMessage` protocol

**Decision.** Three-layer page, strictly progressive:

1. **Base layer (unchanged):** Ace + the existing mode worker — instant syntax
   squiggles, HTML/CSS/JS lint, word-list completion. This layer is byte-for-byte
   today's behavior and remains the whole experience when WASM is absent, blocked, slow,
   or unsupported.
2. **Typed layer:** one **module worker** (`demo/heddle-demo-worker.js`) boots
   `dotnet.js`, resolves `getAssemblyExports`, and dispatches request envelopes to the
   `[JSExport]` surface. The page lazy-creates it after the base page is interactive
   (`requestIdleCallback` with a load-event fallback).
3. **Render pane:** fed by the same worker (`render` command), displayed in a sandboxed
   iframe (D8).

The protocol is **facade-shaped, not LSP-shaped**: absolute UTF-16 offsets and the
facade's own result vocabulary — exactly what phase 6 D11 kept the facade offset-based
*for*; no LSP `Position`/`Range` DTOs exist in the browser. Envelope:
`{ id, cmd, ...args }` → `{ id, ok, result | error }`, plus worker-initiated
`{ evt: "ready" | "boot-failed", ... }`. Commands: `init`, `analyze(path, text,
version)`, `complete(path, offset)`, `hover(path, offset)`, `render(path, modelId)` —
full message table, TypeScript-annotated shapes, and the JSON examples in
[wasm-demo.md](wasm-demo.md#the-worker-protocol). Serialization on the .NET side is a
source-generated `System.Text.Json` context (trim-safe, no reflection warm-up —
consistent with phase 6 D6). Ace's `doc.positionToIndex`/`indexToPosition` (already used
by the mode worker) convert at the page boundary and nowhere else — the phase 6 D11
single-conversion-boundary rule transplanted.
**Rationale.** The roadmap's "same message shapes, no protocol fork" resolves to the
facade contract (the transport-agnostic layer); the module-worker + envelope pattern is
Microsoft's documented worker guidance (D3 grounding); one worker keeps analysis and
render on one engine state (the analyzed document is the render input).
**Alternatives rejected.** Reusing phase 6's LSP DTOs over `postMessage` (drags
line/character mapping and protocol ceremony into a host with no LSP client; the facade
*is* the shared contract, the LSP layer is the *server's* projection); running dotnet.js
on the main thread (multi-second boot and interpreter renders would jank the editor);
extending the Ace mode worker to also boot dotnet.js (couples the dryice-built bundle to
the .NET asset graph; breaks the fallback layering — layer 1 must survive layer 2's
failure by construction).

### D7 — Ace feature binding: typed items replace the word list; annotations merge; the word list becomes the fallback

**Decision.** With the typed layer ready, the page (a) registers the D1 dynamic
completer and **removes the static word-list completer from the active set** — extension
names now come from the live engine registry via the facade, ending the hard-coded
22-name divergence risk (stale-claim correction recorded in Assumed state); (b) attaches
`HoverTooltip` bound to `hover` round-trips; (c) merges diagnostics: typed `HED*`
diagnostics (id shown as `[HED1003]` prefix in the annotation text) are combined with
the base worker's parse/lint annotations, de-duplicated by span+message, via a small
annotation-merge module in the page script. When the typed layer is not (yet) active,
nothing is registered or merged — the base layer runs exactly as today, including the
word-list completer. The page's status pill reflects the active layer
(`Syntax` → `Typed` → `Typed + Render`); the "does NOT render" copy in
[demo.html](../../public/demo.html) is replaced by layer-aware text (the no-WASM state
keeps an honest "syntax checking only — rendering requires WebAssembly" note).
**Rationale.** Progressive enhancement with a single visible source of truth per
feature per state; the facade projection rule (phase 6) extends to the page — no JS
re-implementation of engine knowledge once the engine is present.
**Alternatives rejected.** Keeping both completers active simultaneously (duplicate,
contradictory items); deleting the word-list completer outright (it is the documented
fallback experience and costs nothing to keep for layer 1).

### D8 — The render loop: shared 300 ms debounce, render-on-clean, Html profile, native-tier sandbox as the showcase

**Decision.** One debounce, **300 ms** after the last edit — deliberately identical to
phase 6 D8's server debounce (same facade, same freshness semantics; divergence would
need a reason and none exists). On fire: `analyze` → publish annotations → if zero
**errors** (warnings render), `render` with the selected model → the pane's sandboxed
iframe (`<iframe sandbox>` — no scripts, no same-origin: rendered template output is
untrusted by posture even though the Html profile encodes by default) gets the result
via `srcdoc`. Compile options in the browser: `OutputProfile.Html`,
`ExpressionMode.Native`, `TrimDirectiveLines` on. **`AllowCSharp` is off and framed as
the showcase**: the demo's banner copy states that the sandboxed native tier is what
runs — user-supplied templates, no C# execution surface — and a C#-tier construct
produces the engine's own positioned phase 1 diagnostic plus a friendly render-pane note
("the C# tier is not available in the browser demo — this is the sandbox working"), the
exact "declined gracefully" scenario the roadmap pins. Render exceptions (e.g. a
`:: dynamic` template exercising runtime-binder paths the trimmed runtime lacks) are
caught in `DemoHost.Render` and returned as `{ error }` — the pane shows the message,
the page never crashes. Renders are serialized per document (a new debounce cancels an
unstarted render; an in-flight interpreted render at demo scale is milliseconds —
Performance).
**Rationale.** Consistency with the pinned tooling cadence; render-on-clean avoids
flashing half-broken output; the sandbox-as-feature framing is the roadmap's, executed.
**Alternatives rejected.** A second, longer render debounce (two freshness states for
one document — the confusion phase 6 D9 rejected); rendering into a live DOM node
(untrusted-output posture; iframe sandbox is one attribute); `AllowCSharp` behind a
toggle (no browser-side Roslyn exists to honor it — D4 made that structural).

### D9 — Bundle and deploy: the docs workflow publishes the WASM layer; staged, gitignored, budget-gated; fallback doubles as local-dev mode

**Decision.** [docs.yml](../../../.github/workflows/docs.yml) build-job changes, in step
order (full YAML diff in [wasm-demo.md](wasm-demo.md#docs-workflow-changes)):

1. After the Ace staging step: `actions/setup-dotnet@v5` (10.0.x) →
   `dotnet workload install wasm-tools` →
   `dotnet publish src/Heddle.Demo.Wasm -c Release` →
   stage `bin/Release/net10.0/publish/wwwroot/.` into **`docs/public/demo/`** (worker JS
   + `_framework`), so VitePress copies it to the site root beside `demo.html` (base
   `/Heddle/`, worker URL `demo/heddle-demo-worker.js`, same-origin).
2. **Budget + purity gate** (one script step): fail if the staged `demo/` exceeds
   **12 MB** raw (D3 baseline; the measured size is recorded in the PR and becomes the
   revised budget) or contains any `Microsoft.CodeAnalysis*` asset (D4 assertion).
3. `npm ci` → `npm run docs:build` → **the Playwright smoke suite (D10)** → upload →
   deploy.
4. Triggers: add `pull_request` (same paths) and extend `paths` with `src/Heddle/**`,
   `src/Heddle.Language/**`, `src/Heddle.LanguageServices/**`, `src/Heddle.Demo.*/**`
   — the WASM bundle embeds the engine, so engine changes must rebuild/smoke the demo.
   The deploy job gains `if: github.event_name != 'pull_request'` — PRs build and smoke,
   only main deploys (the `pages` concurrency group already serializes deploys).

Staging posture matches the Ace bundle exactly: `/docs/public/demo/` joins `.gitignore`;
nothing generated is committed. Locally, `docs:dev` without a staged bundle shows the
**fallback layer** — the no-WASM state doubles as the local docs-dev mode, and
`src/Heddle.Demo.Wasm/stage_demo.sh` (publish + copy) lights the typed layer up locally
when wanted. If the WASM publish step fails on main, the workflow fails **before**
deploy — the previously deployed site (with its working bundle) stays live; the roadmap's
"publishes unchanged even with the WASM bundle absent or broken" is satisfied by the
page-level fallback (a deployed page whose `demo/` assets 404 or fail to boot degrades
to layer 1 — asserted by the WASM-blocked smoke scenario, D10).
**Rationale.** One workflow owns the site; PR smoke coverage closes the "only user-facing
artifact that ships untested" gap; the budget gate makes the roadmap's "measure and
budget the bundle in CI" mitigation mechanical.
**Alternatives rejected.** Committing the published bundle (10+ MB of binaries in
git, drift risk — and contrary to the verified ace/ posture); a separate wasm workflow
(two workflows racing to deploy one Pages site; the `pages` concurrency group exists
precisely because deploys must serialize); deploying the bundle to a separate CDN
(foreign infrastructure for a docs page; Pages is the ratified host).

### D10 — The demo smoke test: Playwright (chromium-only) inside the docs workflow

**Decision.** A Playwright suite at `docs/demo-smoke/` (`@playwright/test` dev-dependency
in [docs/package.json](../../package.json); `npx playwright install chromium
--with-deps` in CI — chromium only, headless default). It runs against the **built
site**: `npm run docs:preview` serves `.vitepress/dist` with the `/Heddle/` base, tests
navigate to `/Heddle/demo.html`. Scenarios (full scripts in
[wasm-demo.md](wasm-demo.md#smoke-scenarios)):

| # | Scenario | Asserts |
| --- | --- | --- |
| S1 | **WASM blocked** — routes matching `**/demo/**` aborted | Page loads; editor visible; deleting a `)` produces a parse annotation — today's behavior intact (the standing fallback regression) |
| S2 | Typed boot | Status pill reaches `Typed`; a member typo produces an annotation carrying a `HED` code |
| S3 | Completion round-trip | Cursor inside `@list(Articles){{ @( ` → popup contains `Title`, `Author` — the C01 cross-host fixture (D14) |
| S4 | Render round-trip | Starter blog template → iframe `srcdoc` contains the pinned golden fragment |
| S5 | C# tier declined | A C#-tier construct → positioned diagnostic annotation + the friendly pane note; no page error |

The suite is the **cross-host contract test's browser half** (D14) and the workflow's
red/green gate before deploy (D9). The **seeded-failure verification** (roadmap success
criterion 4, TDD verdict) is procedural: once per harness change, break the bundle
deliberately (e.g. corrupt the staged `dotnet.js` in a scratch branch), observe S2–S4
red and S1 green, revert, and record the run in the PR — the "test the tests" gate.
**Rationale.** Playwright's GitHub Actions integration is first-party-documented; one
headless chromium run keeps the CI cost to the roadmap's "one headless run" budget;
asserting against the real built site (base path included) catches deploy-shape bugs a
dev-server run would hide.
**Alternatives rejected.** Full browser matrix (webkit/firefox triple the install/runtime
for a demo page; trigger: a user-reported browser-specific defect); testing against
`docs:dev` (unbundled, wrong base path); a standing "seeded broken bundle" test fixture
(a permanently red-by-design job desensitizes; the procedural check per harness change
is the roadmap's own verdict).
**Grounding.** [Playwright — setting up CI](https://playwright.dev/docs/ci-intro)
(install commands, headless default, GitHub Actions example).

### D11 — Gallery mechanics: per-sample layout, the `--capture` convention, golden policy

**Decision.** Repo-root `samples/`, one folder per sample:

```
samples/
  README.md                  ← gallery index + the authoring template (gallery.md mirrors it)
  tools/compare-golden.sh    ← the shared golden comparer
  Heddle.Samples.sln         ← all sample projects + engine project references
  <sample-name>/
    README.md                ← the sample's spec: what it shows, run instructions (verbatim-followable)
    <SampleName>.csproj      ← ProjectReference to the engine (version lockstep, roadmap-ratified)
    Program.cs, templates/, …
    golden/                  ← expected outputs, committed, LF line endings
    out/                     ← written by --capture; gitignored
```

**The `--capture` convention** — every sample supports two modes:
`dotnet run` = the human mode (serve the site, print to console — what the README
walks through); `dotnet run -- --capture out` = the CI mode: write every deterministic
artifact to `out/` and exit 0. Web samples (`ssr-aspnetcore`, `streaming-ssr`) bind port
0 in capture mode, self-request via `HttpClient`, write the response bodies, and shut
down. Determinism rules (normative for every sample): fixed model data (no
`DateTime.Now`), invariant culture, ordered enumerations.
**Golden policy** — `tools/compare-golden.sh <sample-dir>` normalizes CRLF→LF on both
sides (the [testing-standards](../common/testing-standards.md#fixtures-and-goldens)
convention) and requires byte-identical content per file **and** an exactly-matching
file set (extra or missing `out/` files fail). `UPDATE_GOLDEN=1` regenerates — and a
golden diff in a PR is a review event, never a fix (the standards' golden-change policy,
restated as the gallery's law). The phase 7 **differential rule** applies where two
backends exist: `precompiled-app` asserts precompiled output == dynamic output
in-process *and* pins the shared golden. Per-sample details, the authoring template, and
the full inventory: [gallery.md](gallery.md).
**Rationale.** One convention makes every sample a CI job with zero per-sample harness
code, keeps README-as-spec honest (the human mode is the README's path), and reuses the
engine suite's golden discipline verbatim.
**Alternatives rejected.** Per-sample xUnit assertion projects (a test project per
sample doubles the project count and hides the "runnable app" story the gallery exists
to tell; samples needing in-process asserts — the differential, streaming parity — do
them inside capture mode); stdout-capture-only goldens (web samples produce multiple
artifacts; files are diffable in review); a .NET-based comparer tool (the shell script
is 30 lines, CI is Linux, and contributors have git-bash — precedent: `build_ace.sh`).

### D12 — Gallery CI: a dedicated `samples.yml` matrix workflow; engine CI untouched

**Decision.** New workflow `.github/workflows/samples.yml` (full YAML in
[gallery.md](gallery.md#the-ci-workflow)): triggers on push-to-main and `pull_request`
with paths `samples/**`, `src/**`, and the workflow itself (engine changes must re-prove
the integrations — that is the suite's purpose); one job with
`strategy: { fail-fast: false, matrix: { sample: [ …ten names… ] } }` on
`ubuntu-latest`: checkout → `actions/setup-dotnet@v5` (10.0.x) →
`dotnet run --project samples/${{ matrix.sample }} -c Release -- --capture out` →
`bash samples/tools/compare-golden.sh samples/${{ matrix.sample }}`. Per-sample jobs
give the roadmap's isolation scenario mechanically: a seeded break in one sample reds
exactly that job. [dotnet.yml](../../../.github/workflows/dotnet.yml) is untouched —
samples never enter `Heddle.sln`, never build under the NuGet publishing jobs, and add
zero minutes to the engine's PR loop. The `codegen-t4-successor` job additionally proves
the "no runtime Heddle dependency" claim structurally (its capture step inspects the
publish output for engine assemblies — gallery.md). The **seeded-failure procedure**
(rename a model property in one sample on a scratch branch → exactly one red job →
revert; recorded in the PR) runs once when the harness lands and once per harness
change, per the TDD verdict.
**Rationale.** Matrix-per-sample is the smallest shape that satisfies "every sample is a
CI job", parallelizes by default (the roadmap's CI-time mitigation), and keeps failure
attribution one-to-one.
**Alternatives rejected.** Steps-in-one-job (first failure masks the rest;
`fail-fast: false` on a matrix is free isolation); adding samples to `dotnet.yml`
(entangles the publish pipeline and doubles its wall time); one workflow file per sample
(ten near-identical files to maintain; the matrix list *is* the registry).

### D13 — The sample inventory: ten samples + one walkthrough row; every deferred item lands; roadmap suggestions reconciled

**Decision.** The gallery content is the union of (a) the roadmap's three
current-engine demos and (b) the seven deferred gallery items recorded by the phase 1–8
specs — all due now because this phase is last (header note). Pinned inventory (detail
per sample in [gallery.md](gallery.md#the-inventory)):

| # | Sample | Source of record | Demonstrates |
| --- | --- | --- | --- |
| 1 | `samples/ssr-aspnetcore` | Roadmap (current engine); upgraded per [phase 4's deferred item](../phase-4-ergonomics/README.md#testing-plan) | Minimal-API SSR, resolver + layout/page composition, caching, `@for` + `TrimDirectiveLines` |
| 2 | `samples/definition-library` | Roadmap (current engine); upgraded per phase 4 | `@<<` composition import, definition overrides, `<name:name>` narrowing |
| 3 | `samples/dynamic-models` | Roadmap (current engine) | `:: dynamic`, runtime-loaded template strings |
| 4 | `samples/sandboxed-user-templates` | [Phase 1 deferred item](../phase-1-native-expressions/README.md#testing-plan) | `ExpressionMode.Native`, `FunctionRegistry` as the trust boundary, rejected constructs with positioned `HED1xxx` |
| 5 | `samples/html-safe-output` | [Phase 2 deferred item](../phase-2-safe-output/README.md#testing-plan) | `OutputProfile.Html`, `@raw`, encoder pluggability; runs under both defaults — the standing 2.0-migration rehearsal |
| 6 | `samples/custom-extensions` | [Phase 3 deferred item](../phase-3-branching/README.md#testing-plan) | The public `Scope.Publish`/`TryRead` channel + a branch-protocol participant |
| 7 | `samples/component-props-slots` | [Phase 5 deferred item](../phase-5-props-and-slots/README.md#testing-plan) | The card/layout/picker component library: typed props with defaults, parameterized slots |
| 8 | `samples/codegen-t4-successor` | [Phase 7 deferred item](../phase-7-build-time-compilation/README.md#testing-plan) | Build-time text/code generation, no runtime Heddle dependency (asserted structurally) |
| 9 | `samples/precompiled-app` | Phase 7 deferred item | Pre-compilation + mixed mode + discovery enumeration; the differential rule (precompiled == dynamic twin) |
| 10 | `samples/streaming-ssr` | [Phase 8 deferred item](../phase-8-streaming-async/README.md#testing-plan) | `Generate(data, IBufferWriter<byte>)` into `Response.BodyWriter` + `FlushAsync`; string-parity asserted in capture |
| — | `editors/vscode` walkthrough | [Phase 6 deferred item](../phase-6-tooling-lsp/README.md#testing-plan) | LSP setup + typed completion tour — an index **row**, not a `samples/` project: it links `docs/editor-support.md` (created by phase 6's D22) and `editors/vscode`, and is verified by phase 6's standing extension smoke test |

Reconciliation of the roadmap's named suggestions — each maps onto an existing row
rather than duplicating: *ASP.NET Core minimal API integration* → 1 and 10; *console
codegen* → 8; *precompiled startup* → 9; *streaming-to-response* → 10; *LSP-in-editor
walkthrough* → the walkthrough row. Phase 4 contributes **no row by design** (its
deferred item upgrades 1 and 2 — the roadmap's recorded note, honored). Phase 1's second
deferred item (in-browser rendering as the standing Roslyn-free proof) and phase 6's
WASM-host item are the **demo deliverable**, not gallery rows. The roadmap's "this phase
ships the structure plus the first three rows; later phases add theirs as they ship"
staggering is recorded as superseded: under D5 all owners' items land here, in this
phase, through this harness — ownership stays with the source phases for fix-forward
purposes (a rotted sample is that phase's integration regression, per the roadmap's
verification loop).
**Rationale.** This table is the discoverability payoff the sequential spec effort
promised: every phase's Testing plan names an item, and every item resolves to a row
here with a source-of-record link.
**Alternatives rejected.** Separate samples per roadmap suggestion (pure duplication —
e.g. a second minimal-API sample distinct from `ssr-aspnetcore` demonstrates nothing
new); folding the walkthrough into a `samples/` project (an editor tour is
documentation + the phase 6 extension, not a runnable golden project).

### D14 — The cross-host facade contract: one fixture set, two hosts

**Decision.** Phase 6's deferred "cross-host contract test" lands as **one shared
fixture set asserted by both hosts**: `src/Heddle.Demo.Wasm/contract-fixtures/*.json` —
request/expected-result pairs in the D6 protocol shapes, derived from the phase 6
[worked examples](../phase-6-tooling-lsp/protocol.md#completion) C01 (typed `@list`
member completion), C02 (`::` root members), C04 (prop named arguments), C09 (`@if`
step-back), plus one diagnostics pair (member typo → `HED0001` at the exact offset) and
one render pair (starter template → golden HTML) — member names adjusted to the
published blog corpus where phase 6's test corpus differs (Assumed state). Asserted
twice: (a) `DemoContractTests` (new file in
`src/Heddle.LanguageServices.Tests`) runs the fixtures against `DemoHost` on ordinary
CoreCLR — the host orchestration classes are plain `net10.0` code; only the
`[JSExport]`/worker shell is browser-only, and it contains no logic (wasm-demo.md pins
that split); (b) the Playwright scenarios S3/S4 (D10) assert the same fixtures through
the real browser stack. Facade drift between the LSP server and the browser host now
breaks a test on both sides of the boundary — the roadmap's named risk, mitigated as
specified there.
**Rationale.** The fixture set is the contract; running it editor-less *and*
browser-real is what "two hosts, one facade" needs — no new abstraction, just shared
JSON.
**Alternatives rejected.** A browser-hosted xUnit runner (heavy infrastructure to
re-assert what S3/S4 already prove in situ); duplicating expectations by hand in the
smoke suite (drift between the copies is exactly the failure mode this exists to catch).

### D15 — Phase exit is the roadmap's combined end-to-end run

**Decision.** This phase's exit gate *is* the roadmap-wide integration proof: **all ten
gallery jobs green + the five smoke scenarios green + the engine regression gate green,
in one combined run** — the "phase exit = combined run" rule instantiated over the whole
roadmap's surface. The traceability from roadmap phases to what the gallery/demo
re-verifies end-to-end:

| Roadmap phase | Success-criteria surface re-verified | Re-verified by |
| --- | --- | --- |
| 1 — native expressions | Sandbox boundary (rejected constructs never execute, positioned diagnostics); native-tier operator/render semantics; the Roslyn-free common path | Sample 4 goldens + its rejected-template diagnostics listing; demo render (S4) with the D9 purity gate proving **no Roslyn in the host** — the standing structural proof phase 1's spec deferred here |
| 2 — safe output | Html profile encodes the unnamed output; `@raw` opt-out; the 2.0 default flip remains rehearsed | Sample 5 run under both defaults; the demo page itself rendering under `OutputProfile.Html` (S4) |
| 3 — branching | `@elif`/`@else` adjacency semantics incl. stripping warning; the public channel as extension API | Sample 6 (a third-party-shaped extension consuming the channel + branch protocol) |
| 4 — ergonomics | `@for` sugar and `TrimDirectiveLines` in real templates | Samples 1–2 goldens (the upgrade is the coverage — phase 4's own recorded plan) |
| 5 — props & slots | Typed props (defaults/required), parameterized slots, component composition | Sample 7 goldens; demo completion fixture C04 (S3/D14) |
| 6 — tooling / LSP | Facade correctness (completion/hover/diagnostics as engine projections); cross-host stability | Demo typed features S2–S3 + `DemoContractTests` (D14); the walkthrough row |
| 7 — build-time compilation | Precompiled == dynamic (differential), discovery enumeration, codegen without runtime dependency | Sample 9 (differential + discovery golden); sample 8 (structural no-dependency assert) |
| 8 — streaming & async | Byte-sink parity with string rendering; the `Response.BodyWriter` pattern | Sample 10 (in-capture parity assert + response-bytes golden) |

**Rationale.** The roadmap defines phase 9's success as making "phase X shipped"
demonstrable for every X; the table is that claim made checkable.
**Alternatives rejected.** Citing per-phase criterion numbers verbatim (the criteria
live in eight roadmap docs with independent numbering; naming the surface keeps the
table stable against roadmap renumbering while staying auditable through the sample
links in D13).

### D16 — Diagnostics posture: exactly one ID (HED9001); page/tooling conditions are not compile diagnostics

**Decision.** This phase claims **HED9001** only (D4's guard — a genuine compile-path
condition: the compile cannot proceed as authored). Everything else phase 9 surfaces is
either an existing engine diagnostic projected faithfully (the demo shows the engine's
own `HED1xxx`/`HED0xxx`/… — never invents), or a host/page condition (WASM boot failure,
budget breach, worker timeout) that follows phase 6 D20's rule: host conditions surface
through host channels (status pill, pane note, CI step failure), never as fake
positioned compile errors. The `HED9xxx` block otherwise stays reserved.
**Rationale.** Phase 6 and phase 8 set the precedent (zero IDs claimed for
tooling/sink work); HED9001 is the one condition here that genuinely lives on the
compile path and needs a stable code.
**Alternatives rejected.** No ID for the D4 guard (a collected error without a stable
ID regresses the D1 cross-cutting scheme the moment any host disables the switch);
IDs for boot/budget conditions (no template position exists — phase 6 D20's exact
reasoning).

## Implementation plan

Work items in execution order; each runs the canonical loop per the
[TDD verdict](#testing-plan). "Check" is the completion gate.

**WI1 — Gallery scaffolding and harness (D11, D12).**
Files: `samples/README.md` (index table + authoring template), `samples/tools/compare-golden.sh`,
`samples/Heddle.Samples.sln`, `.gitignore` additions (`samples/**/out/`,
`/docs/public/demo/`), `.github/workflows/samples.yml` (matrix initially over the WI2
samples, growing per WI).
Check: workflow green on a trivial placeholder run; the comparer's own behavior proven
by a scripted self-test (mismatch, extra file, missing file, CRLF-only difference → the
first three fail, the fourth passes).

**WI2 — Current-engine samples (D13 rows 1–3, with phase 4 upgrades).**
Files: `samples/ssr-aspnetcore/`, `samples/definition-library/`,
`samples/dynamic-models/` — each README-first (the README is the sample's spec, per the
verification loop), then implementation, then captured + reviewed goldens; matrix rows
added.
Check: three green jobs; seeded-failure procedure run once (rename a model property in
`ssr-aspnetcore` on a scratch branch → exactly that job red → revert; recorded in PR).

**WI3 — Phase-feature samples (D13 rows 4–7).**
Files: `samples/sandboxed-user-templates/`, `samples/html-safe-output/`,
`samples/custom-extensions/`, `samples/component-props-slots/` + goldens + matrix rows.
Check: four green jobs; sample 4's diagnostics listing pins `HED1xxx` IDs + positions;
sample 5 captures under both profile defaults.

**WI4 — Build-time and streaming samples (D13 rows 8–10) + the walkthrough row.**
Files: `samples/codegen-t4-successor/` (with `EmitCompilerGeneratedFiles` so the
generated source itself is a golden), `samples/precompiled-app/` (differential assert in
capture), `samples/streaming-ssr/` (parity assert in capture); the walkthrough row added
to `samples/README.md`.
Check: three green jobs; sample 8's publish-output inspection proves no engine
assemblies; sample 9's differential red-tested by a seeded golden edit (reverted).

**WI5 — Engine feature switch (D4) + HED9001.**
Files: `src/Heddle/Runtime/HeddleFeatures.cs` (new),
`src/Heddle/Runtime/ContextCompilation.cs` (guard + Roslyn-typed member layering),
`src/Heddle/ILLink.Substitutions.xml` (new, embedded), `Heddle.csproj` (embed item),
`src/Heddle.Tests/FeatureSwitchTests.cs` (new: switch off + C# methods registered →
HED9001 collected, nothing thrown, no Roslyn types touched on that path; switch
unset/on → byte-identical existing behavior).
Check: full existing suite green unmodified; `PublicApiSurfaceTests` goldens unchanged
(no public surface); the new tests green on all TFMs.

**WI6 — WASM host projects (D3–D6, D14).**
Files: `src/Heddle.Demo.Models/` (corpus + `DemoCatalog`), `src/Heddle.Demo.Wasm/`
(csproj with `RuntimeHostConfigurationOption`, `TrimmerRootAssembly`,
`TrimmerRootDescriptor` + `linker/Heddle.Demo.roots.xml`, `InvariantGlobalization`;
`DemoInterop`/`DemoHost`/`DemoJsonContext`; `wwwroot/heddle-demo-worker.js`;
`stage_demo.sh`), `src/Heddle.Demo.Wasm/contract-fixtures/*.json`,
`src/Heddle.LanguageServices.Tests/DemoContractTests.cs`; `Heddle.sln` gains the two
projects.
Check: `DemoContractTests` green on CoreCLR (fixtures written **failing first** — they
are this WI's executable spec); local publish succeeds; published `_framework` contains
no `Microsoft.CodeAnalysis*`; `stage_demo.sh` + `docs:dev` boots the typed layer
locally.

**WI7 — Demo page upgrade (D1, D6–D8).**
Files: [docs/public/demo.html](../../public/demo.html) (worker bridge, dynamic
completer, `HoverTooltip` wiring, annotation merge, render pane + sandboxed iframe,
model picker, status pill, layer-aware copy replacing the "does NOT render" text).
Check: manual pass of the roadmap's five demo validation scenarios recorded in the PR;
fallback confirmed by loading the page with `demo/` assets absent.

**WI8 — Docs workflow + smoke suite (D9, D10).**
Files: [.github/workflows/docs.yml](../../../.github/workflows/docs.yml) (steps,
triggers, deploy gating per D9), `docs/package.json` (+`@playwright/test`, `demo:smoke`
script), `docs/demo-smoke/*.spec.ts` (S1–S5), the budget/purity gate script.
Check: PR run shows build + smoke green without deploying; S1–S5 green against the
built site; the bundle-break seeded failure run once and recorded; budget number
captured into the gate.

**WI9 — Combined gate and docs.**
Files: `docs/building.md` (samples how-to section), `docs/README.md` (gallery pointer),
[spec index](../README.md) row (done with this spec).
Check: the full [regression gate](../common/testing-standards.md#regression-gates) in
one combined run — build all TFMs; `dotnet test src/Heddle.Tests` +
`src/Heddle.LanguageServices.Tests` green; grammar-stability check (no-grammar phase:
`src/Heddle.Language/generated/` diff-free); docs build green (uppercase-drive cwd on
Windows); all ten sample jobs green; S1–S5 green; the D15 traceability run recorded in
the PR as the roadmap's closing artifact.

## Public API contract

**No public API is added to any shipped package.** The gallery samples and the demo
host are applications; `Heddle.Demo.Models`/`Heddle.Demo.Wasm` are not packed. The one
engine change (D4) is an internal class, an embedded linker file, and a documented
**host-configuration contract** rather than an API:

- `AppContext` switch `"Heddle.CSharpTierEnabled"` — default (unset) `true`; a host
  setting it to `false` (or trimming with
  `<RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" />`)
  disables the C# expression tier: templates requiring it fail compilation with
  HED9001, and trimmed publishes drop the Roslyn dependency graph. Documented in
  [csharp-api.md](../../csharp-api.md) (same-phase docs rule) as an advanced hosting
  option.

Thread-safety notes: `HeddleFeatures.CSharpTierEnabled` is a stateless read of an
`AppContext` switch (hosts set switches at startup, per `AppContext` convention);
`DemoHost` runs single-threaded inside one worker (the WASM runtime is single-threaded
here) and serializes analyze/render per document by construction. The
`PublicApiSurfaceTests` goldens (phase 6) are the enforcement that nothing public moved.

## Diagnostics

One ID claimed from the `HED9xxx` block
([registry](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx));
prior claims verified against the merged specs: `HED0001`–`HED0004`,
`HED1001`–`HED1017`, `HED2001`–`HED2003`, `HED3001`–`HED3004`, `HED4001`–`HED4002`,
`HED5001`–`HED5018`, `HED7001`–`HED7013` + `HED7101`–`HED7103`; phases 6 and 8 claimed
none.

| ID | Severity | Message | Trigger | Position |
| --- | --- | --- | --- | --- |
| `HED9001` | Error | `The C# expression tier is not available in this host: the 'Heddle.CSharpTierEnabled' feature switch is disabled. Rewrite the template using native expressions (ExpressionMode.Native), or run it in a host with the C# tier enabled.` | `ContextCompilation.Compile` entered with ≥ 1 registered C# method while `HeddleFeatures.CSharpTierEnabled` is `false` | `default(BlockPosition)` — a host-capability condition on the whole compile (the engine's no-position convention); per-construct positions are already carried by the phase 1 `ExpressionMode` diagnostics, which fire first in `Native` mode |

In the browser demo HED9001 is expected never to fire (the host compiles with
`ExpressionMode.Native`, so phase 1 diagnostics intercept C#-tier constructs earlier);
it exists so a misconfigured host (`fullCSharp` mode + switch off) fails loudly with a
stable code instead of silently skipping the Roslyn pass.

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md). Suite homes: the
standard set, plus the two this phase creates — the `samples/` gallery (the standards
table's third home, now real) and the `docs/demo-smoke/` Playwright suite.

**TDD verdict (carried from the roadmap, unweakened).** *Inverted: the demos are the
tests, so the discipline moves to testing the harness.* Concretely: the comparer
self-test (WI1) and the D14 contract fixtures (WI6) are written **failing first** — they
are the harness's executable spec; each sample's README is written before its code (the
README is the sample's spec, criterion 6); the **seeded-failure checks** — one sample
break (WI2), one golden break (WI4), one bundle break (WI8) — are the standing "test the
tests" gates, run once per harness change and recorded in the PR; goldens follow the
engine suite's review discipline (captured, reviewed, never adjusted to silence a red).

**Named test assets:**

| Asset | Home | Content |
| --- | --- | --- |
| Ten sample golden sets | `samples/*/golden/` | The D13 inventory; each sample's own README documents its golden shape ([gallery.md](gallery.md#the-inventory)) |
| `compare-golden.sh` + self-test | `samples/tools/` | Normalization + byte-identical + file-set rules (D11), proven able to fail |
| `samples.yml` matrix | `.github/workflows/` | One job per sample, `fail-fast: false` (D12) |
| `FeatureSwitchTests` | Heddle.Tests | D4: switch off → HED9001 collected (ID + document-start position asserted), switch on/unset → existing behavior byte-identical |
| `DemoContractTests` + `contract-fixtures/*.json` | LanguageServices.Tests / Demo.Wasm | The D14 cross-host fixture set on CoreCLR: C01/C02/C04/C09 completions, the diagnostics pair, the render pair |
| `demo-smoke` S1–S5 | `docs/demo-smoke/` | D10 scenarios incl. the WASM-blocked fallback regression; asserts the same fixtures browser-side |
| Budget/purity gate | docs workflow step | ≤ 12 MB staged bundle; zero `Microsoft.CodeAnalysis*` assets (D4/D9) |
| Seeded-failure records | PR artifacts | The three procedural breaks (sample, golden, bundle) with red/green evidence |

**Regression gate** (one combined run, per the standards): build all TFMs;
`dotnet test src/Heddle.Tests` — unmodified except the one new `FeatureSwitchTests`
file, all goldens byte-identical; `dotnet test src/Heddle.LanguageServices.Tests`;
grammar-stability check (no-grammar phase — `src/Heddle.Language/generated/` has no
diff); no benchmark run required (no hot path touched — Performance) beyond a one-time
flag-off compile benchmark confirming the D4 guard is a single branch; docs build;
all sample jobs; S1–S5. Roadmap success criteria: 1 → S2/S3 + D14 fixtures; 2 → S4/S5;
3 → WI2's three jobs (and criterion 5's "inherits the harness" is answered structurally:
rows 4–10 did); 4 → the WI8 bundle-break record; 6 → the README-verbatim walkthrough
pass at phase exit, recorded in the PR.

**Deferred phase 9 gallery item:** none — this phase *is* the harness; the ledger closes
here (D13 enumerates every prior phase's item landing).

## Back-compat and migration

| Surface | Behavior | Proof |
| --- | --- | --- |
| Engine public API | Unchanged — no new/changed public members | `PublicApiSurfaceTests` goldens untouched |
| Engine behavior, switch unset (every existing host) | Identical: the D4 guard reads an unset switch as `true` and falls through | `FeatureSwitchTests` on/unset rows; full suite unmodified |
| Existing demo page behavior | Preserved verbatim as layer 1; identical when WASM is absent/blocked | Smoke S1 (the standing regression) |
| Docs site & workflow | Same site, same deploy path; PRs gain build+smoke without deploying | D9 gating; a failed WASM step blocks deploy, never publishes a broken site |
| Engine CI (`dotnet.yml`) | Untouched — samples live in their own solution and workflow | D12 |
| NuGet packages | No new packages; `Heddle` gains only an embedded substitutions resource (inert without the switch) | Pack output review |
| 2.0 window | Nothing added; sample 5 *rehearses* the ratified phase 2 flip under both defaults | [D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window) unchanged |

## Performance considerations

- **Engine hot paths: untouched.** The D4 guard adds one branch on the *compile* path's
  Roslyn entry — verified free by a one-time flag-off compile benchmark run (gate rule
  4 satisfied by that single check; no standing benchmark is added).
- **Page load.** The base layer is interactive at today's speed (Ace bundle only); the
  WASM layer lazy-loads afterward (D6). Budget: staged bundle ≤ 12 MB raw (grounded
  baseline ≈ 6.8 MB for an empty app, invariant-globalization ≈ −30 %); the measured
  publish size and transfer size are recorded at implementation as the standing
  baselines. GitHub Pages' transparent compression is outside our control — the budget
  gates the raw asset size we own.
- **In-browser latency.** Analysis is the facade's compile (sub-100 ms class on
  page-sized templates, per phase 6's numbers) running interpreted — slower, bounded by
  the 300 ms debounce cadence; interpreted expression-tree renders at demo scale
  (dozens of expressions) are milliseconds. The Jiterpreter tiers hot paths without any
  action on our side. No numeric pin is claimed for interpreted speed; S2–S4 assert
  round-trips complete within Playwright's default timeouts, which is the demo's real
  requirement.
- **CI wall time.** Samples: one parallel matrix (≈ 2–4 min/job — budgeted, not
  capped, per the roadmap). Docs job additions: dotnet setup + `wasm-tools` install
  (~2–3 min), publish (~1–2 min), chromium install (~1 min, cacheable), smoke (< 1 min).
  Tracked per the roadmap's regression requirement; growth is reviewed at each gallery
  addition.

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made:

- **DRY as knowledge consolidation:** one fixture set drives both hosts (D14) instead of
  two expectation copies; the gallery reuses the engine suite's golden/normalization
  rules verbatim (D11) rather than inventing a second golden dialect; the demo takes
  extension/function/member knowledge from the live engine (D7), retiring the hand-coded
  word list.
- **YAGNI cutting scope:** no Monaco, no Ace pin bump, no browser VFS import seeding, no
  model-set upload, no full browser matrix, no per-sample test projects, no new
  workflow-per-sample — each cut is in [Deferred items](#deferred-items) with a trigger.
- **Open/closed at the sanctioned seam:** the engine opens nothing new for the demo —
  the browser host consumes the phase 6 facade and the public `Configure` seam; the one
  engine touch (D4) is the standard library feature-switch pattern, not a new
  extensibility mechanism.
- **Security beats ergonomics:** rendered output goes into a sandboxed iframe even
  though the Html profile already encodes (defense in depth); the C# tier is
  structurally absent from the browser (trimmed, not just disabled — D4); the sandbox
  posture is presented as the product, matching precedence rule 2.
- **Simplicity as default winner:** the harness is a shell script + a matrix + a
  convention (`--capture`), not a framework; the smallest design that satisfies "every
  sample is a CI job with a golden".

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Monaco frontend for the demo | Ace UX proves limiting for typed features in practice (roadmap's own trigger); the facade protocol already permits the swap (D1) |
| Ace pin bump (1.32.6 → current) | An Ace defect surfacing in the smoke suite or a needed API absent from the pinned build (D1 verified none is today) |
| Browser VFS seeding for `@<<`/`@partial` demo imports | A curated multi-file demo scenario; today imports surface their normal file-not-found diagnostics in-browser ([wasm-demo.md](wasm-demo.md#imports-in-the-browser)) |
| User-supplied model shapes in the demo (JSON → synthesized types) | User demand; would need a Reflection.Emit-under-interpreter design review |
| Multithreaded WASM / AOT experiments | Measured interpreter latency failing S2–S4 budgets on real templates |
| Splitting Roslyn out of the `Heddle` package | 2.0-window packaging review; D4's feature switch removes the urgency |
| WebKit/Firefox smoke coverage | A user-reported browser-specific demo defect (D10) |
| File-set/lazy assembly download tuning (`dotnet.js` resource hints) | Budget-gate pressure as the bundle grows |
| Per-sample Windows CI legs | A sample demonstrating OS-specific behavior (none does; resolver path semantics are phase 7's suite concern) |
| Auto-publishing gallery outputs as docs pages | Maintainer demand for rendered-sample docs; today READMEs are the documentation |

## External references

Primary sources verified July 2026. Web notes: all four grounding areas the roadmap
listed were verified; searches succeeded without exhausting retries (one npm-adjacent
query answered via registry search results rather than the npmjs HTML page — same
workaround phase 6 recorded).

- [JavaScript `[JSImport]`/`[JSExport]` interop with a WebAssembly browser app](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0) — `wasm-tools` workload, `Microsoft.NET.Sdk.WebAssembly` csproj shape, `dotnet.js` boot, `getAssemblyExports`, template packages (D3).
- [.NET on Web Workers](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers?view=aspnetcore-10.0) — the module-worker + `postMessage` envelope pattern for .NET 8–10 (D3/D6; updated April 2026).
- [Andrew Lock — Running .NET in the browser without Blazor](https://andrewlock.net/running-dotnet-in-the-browser-without-blazor/) — .NET 10-era wasmbrowser publish sizes: 6.8 MB raw / 2.5 gz / 2.0 br; invariant globalization −30 % (D3/D9 budget baseline).
- [Blazor WebAssembly build tools and AOT compilation](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0) — interpreter default, Jiterpreter, `System.Linq.Expressions` always interpreted under AOT (D2/D3).
- [dotnet/runtime PR #80759 — `LambdaExpression.CanCompileToIL` respects `IsDynamicCodeSupported`](https://github.com/dotnet/runtime/pull/80759) and [dotnet/runtime #38439 — Ref.Emit in System.Linq.Expressions on WASM](https://github.com/dotnet/runtime/issues/38439) — the expression-interpreter fallback evidence (D2).
- [Prepare .NET libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming) and [Trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options) — feature switches, `ILLink.Substitutions.xml`, `TrimmerRootAssembly`/`TrimmerRootDescriptor`, `RuntimeHostConfigurationOption Trim="true"` (D4/D5).
- [Configure the trimmer for Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/configure-trimmer?view=aspnetcore-9.0) — root-descriptor mechanics for reflection-dependent apps (D5).
- [Host and deploy Blazor WebAssembly — Webcil](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0) — Webcil default `.wasm` assembly packaging; firewall/static-host rationale (D3).
- [ace-builds — npm](https://www.npmjs.com/package/ace-builds) — 1.44.0 current (≈ May 2026); Ace actively maintained (D1).
- [Ace API — HoverTooltip](https://ajaxorg.github.io/ace-api-docs/classes/src_tooltip.HoverTooltip.html), [Completer interface](https://ace.c9.io/api/interfaces/ace.Ace.Completer.html), [autocomplete wiki](https://github.com/ajaxorg/ace/wiki/How-to-enable-Autocomplete-in-the-Ace-editor) — `getCompletions`/`getDocTooltip`/hover seams (D1); presence in the pinned v1.32.6 build verified in-repo (Assumed state).
- [ace-linters](https://github.com/mkslanc/ace-linters) — LSP-grade completion/hover/diagnostics on Ace over WebWorker transport; the topology precedent (D1).
- [monaco-editor — npm](https://www.npmjs.com/package/monaco-editor) (0.55.1, ≈ Dec 2025), [monaco-editor #5154](https://github.com/microsoft/monaco-editor/issues/5154) (0.55 bundle > 6 MB), [Replit — comparing code editors](https://blog.replit.com/code-editors) (Ace ≈ 98 KB min+gz) — the D1 size evidence.
- [Playwright — setting up CI](https://playwright.dev/docs/ci-intro) — `npx playwright install chromium --with-deps`, headless-default GitHub Actions runs (D10).
- Repo-grounded: [demo.html](../../public/demo.html), [docs.yml](../../../.github/workflows/docs.yml), [dotnet.yml](../../../.github/workflows/dotnet.yml), [build_ace.sh](../../../src/Heddle.Language/build_ace.sh), [config.mts](../../.vitepress/config.mts), [Heddle.csproj](../../../src/Heddle/Heddle.csproj), the pinned `ace_build` tree, and the phase 1–8 specs' Testing plans (the deferred-item ledger).
