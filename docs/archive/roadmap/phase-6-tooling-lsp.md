# Phase 6 — Editor Tooling / LSP

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Depends on: best started after phases 1 and 5 settle the grammar (phase 3 adds none).
> **Zero grammar change** — consumes existing compiler surfaces.

## Goal

A Language Server Protocol implementation and a VS Code extension that give `.heddle` authors
what makes or breaks adoption of a niche language: **typed member completion**, live
diagnostics at template positions, hover types, and go‑to‑definition across imports. Heddle
is unusually well positioned here — because the compiler threads real `ExType` information
through every scope, completion can be *correct*, not heuristic; few template languages can
offer that. Per‑use‑site type errors (abstract definitions) *need* this: the error's cause
and location are in different files by design. Precedent that the lever works: templ (Go's
typed HTML template language) ships its LSP inside the `templ` CLI itself, and its docs
treat editor support as a first‑class feature of the language rather than an add‑on.

## What already exists (build on, don't rebuild)

- `TemplateOptions.ProvideLanguageFeatures` makes
  [DocumentParser](../../src/Heddle/Language/DocumentParser.cs) parse in full‑LL mode and
  emit a classified token list (`ParseContext.AddToken`, `HeddleTokenType`) — the semantic
  token source.
- `HeddleCompileError`/`HeddleCompileWarning` carry `BlockPosition` + `LinePosition` — the
  diagnostics source (compile errors are *collected*, not thrown — ideal for an IDE loop).
- The TextMate grammar
  ([heddle.tmLanguage.json](../coloring-scheme/heddle.tmLanguage.json)) provides instant
  syntax highlighting for the VS Code client while semantic tokens warm up — VS Code layers
  semantic tokens *on top of* grammar highlighting, so falling back to TextMate colors when
  the server is absent is platform behavior, not something we build; the Ace mode and
  JS parser remain web‑editor assets, out of scope here.
- The compiler knows the scope's model type at every point (`CompileScope.ScopeType`,
  definition `:: Type`s, chained `ExType`s) — the completion source. What's missing is an
  **API that maps a source position to that knowledge**.

## Design

### 6.1 Language service facade — separate `Heddle.LanguageServices` package (decided July 2026)

**Decided: the facade ships as its own package, `Heddle.LanguageServices`, referencing
`Heddle` — and the extraction breaks nothing.** The concern was that the language services
already shipped in their infancy would force breaking changes; research against the source
says no:

- Everything tooling‑adjacent that exists today is public surface **in the `Heddle`
  assembly** and *stays there unchanged*: `TemplateOptions.ProvideLanguageFeatures`, the
  static [DocumentParser](../../src/Heddle/Language/DocumentParser.cs), `ParseContext`
  (public, with its public `Tokens` list), `HeddleToken`/`HeddleTokenType`, and
  `HeddleCompileError`/`Warning` positions. Existing consumers (tests,
  `Heddle.Performance`, any host using `ProvideLanguageFeatures`) keep compiling against
  `Heddle` with zero change.
- The facade (`HeddleLanguageService`, `AnalysisResult`, the scope map) is **new code** —
  it can live in a new package by construction. The one piece that touches the engine, the
  scope‑map retention during compilation, is *additive internal* plumbing in `Heddle`,
  surfaced to the facade via `InternalsVisibleTo("Heddle.LanguageServices")` (the
  "integrated properly" part — same repo, same version lockstep, no public API grown on the
  engine for tooling's sake).
- **Breaking‑change count: zero.** The only extraction that *would* break consumers —
  physically moving `HeddleToken`/`ParseContext`/`DocumentParser` out of `Heddle` — is not
  needed for the split and is explicitly not done. (If the engine should ever shed that
  surface, it is a 2.0‑window move with `[TypeForwardedTo]` shims — optional hygiene,
  not a prerequisite for this phase.)

A new `HeddleLanguageService` layer over the existing pipeline — the LSP server must contain
no compiler logic of its own:

- `Analyze(source, options) → AnalysisResult` — parse + compile (with
  `ProvideLanguageFeatures`), returning tokens, errors/warnings, definition table
  (name → declaration span, base, `:: Type`), import graph, and a **position‑indexed scope
  map**.
- The scope map is the one genuinely new piece: during compilation, record for each body
  span the effective model `ExType` (and chained type). The compiler already computes these
  in `InitStart` threading — the work is *retaining* them keyed by `BlockPosition` instead
  of discarding them.
- `GetCompletions(position)` — members of the scope's `ExType` at that position (public
  readable properties, minus `[Hidden]`), definition names, extension names, registered
  function names (phase 1), props in scope (phase 5).
- **Abstract‑definition completion (decided July 2026):** when the definition is **pinned**
  (`:: Type`), complete from that type. When it is **abstract** (late‑bound), build an
  *artificial completion type* from the observed call sites: offer only the member **names
  present at every call‑site type** (name‑based intersection — a member missing at even
  one call site is excluded, because referencing it would break that site). The
  intersection is by *name*, not by type: a member whose type differs across call sites is
  still offered, with hover showing the per‑site types ("varies by call site"). Zero
  observed call sites → no member completion (only definitions/extensions/functions).
- Model types for completion come from the host project's assemblies: v1 loads the
  assemblies the workspace configuration points at (a `.heddle-lsp.json` or VS Code setting
  listing assembly paths / a project to build) — same resolution the engine itself uses
  (`ExType(name, imports)` / `ReflectionHelper.ResolveType`). Document the limitation:
  stale until rebuild.

### 6.2 LSP server (`src/Heddle.LanguageServer`, new project)

- .NET tool (`dotnet tool install heddle-lsp`), stdio JSON‑RPC.
  **Decided (July 2026): a thin hand‑rolled LSP layer over `StreamJsonRpc`** (Microsoft,
  actively maintained — 2.25.x in June 2026, 32.6M downloads). Rationale: the 2026 .NET LSP
  framework landscape has no dependable winner — the long‑time default
  `OmniSharp.Extensions.LanguageServer` is dormant (last release 0.19.9, September 2023;
  an April 2024 "is this repo maintained?" issue never got a maintainer answer),
  `EmmyLua.LanguageServer.Framework` is actively maintained (0.9.2, April 2026; LSP 3.18;
  zero‑dep; AOT‑ready) but pre‑1.0 with tiny adoption, and Microsoft's own stacks are not
  consumable (CLaSP is prerelease‑only "no compatibility guarantees";
  `Microsoft.VisualStudio.LanguageServer.Protocol` last shipped publicly in May 2022).
  Heddle needs only ~6 capabilities, so owning a small protocol layer removes the
  framework‑longevity bet entirely: `StreamJsonRpc` handles transport/dispatch, and the
  server hand‑writes `System.Text.Json` DTOs for exactly the LSP messages it implements
  (initialize, diagnostics, semantic tokens, completion, hover, definition). The facade
  rule bounds the cost: every capability is a projection of `HeddleLanguageService` data,
  and the DTO subset grows only when a capability ships. `EmmyLua.LanguageServer.Framework`
  and OmniSharp remain the documented fallbacks if the protocol layer ever outgrows
  "thin"; the diagnostics‑only milestone (capability 1 below) is the checkpoint to
  sanity‑check the layer's size before building further capabilities.
- Capabilities, in delivery order:
  1. **Diagnostics** (publish on open/change; debounce; map `LinePosition` → LSP ranges).
  2. **Semantic tokens** (from the token list; falls back gracefully — TextMate still colors).
  3. **Completion** (scope map; triggered after `@(`, `.`, `@`, and inside call parens).
  4. **Hover** (member/definition type info).
  5. **Go‑to‑definition** (definition calls → declaration, across the `@<<` import graph;
     `@partial` targets → files).
  6. Later: rename (definitions), find references, document symbols.
- Import graph: reuse the `import` machinery's path resolution (`TemplateOptions.RootPath`)
  so cross‑file navigation matches engine behavior exactly.

### 6.3 VS Code extension (`editors/vscode`, new folder)

- Thin client: activates on `.heddle`, ships the TextMate grammar (copied at build from
  `docs/coloring-scheme/` — single source of truth), starts the server, contributes settings
  (assembly paths, root path, output profile, expression mode — so diagnostics match the
  host's compile options).
- Publish to the marketplace under the project's publisher; CI packaging job.

## Back‑compat analysis

No engine behavior changes. Additive API surface (`HeddleLanguageService`, scope‑map
retention behind `ProvideLanguageFeatures` so production compiles pay nothing). New projects
only; the NuGet package layout gains `Heddle.LanguageServices` and the tool package —
the existing public language‑feature surface stays in `Heddle` untouched (see 6.1: the
extraction is zero‑breaking‑change by construction).

## Risks & mitigations

- **Model assembly loading** (versioning, locking, reload on rebuild): load into a
  collectible `AssemblyLoadContext` (supported since .NET Core 3.0), watch for rebuilds,
  document staleness. Caveat — unlike AppDomains, ALC unload is *cooperative*: `Unload()`
  only initiates it, and collection completes only when nothing references the loaded
  assemblies' types. Cached `Type`/`MemberInfo`s (so `ExType`s and the scope map), statics,
  strong GC handles, threads executing loaded code, and even JIT‑introduced stack slots all
  keep the old context alive — the facade must drop every type‑derived cache on reload and
  verify collection via `WeakReference` polling (`GC.Collect` loop), or the server slowly
  leaks a context per rebuild. ReadyToRun code is also ignored in collectible contexts.
  Worst‑case fallback: restart the server process on rebuild. The v1 answer is "configure
  assembly paths"; project‑system integration (MSBuild evaluation) is a follow‑up. (M/L)
- **Compile cost per keystroke**: debounce + reuse — parse is cheap (SLL for clean docs);
  scope‑map compilation can skip Roslyn entirely when the document avoids the C# tier
  (phase 1 makes this the common case); cache per‑import compilation. (M)
- **Position mapping drift** (hidden‑channel tokens, `@\`, CRLF): a dedicated
  position‑mapping test suite using the whitespace‑torture fixtures. (M)
- **Maintenance surface** for a one‑maintainer project: strictly thin server over the
  facade; every feature must be a projection of engine data, never a re‑implementation. (—)

## Success criteria

1. In VS Code, typing `@(` inside a `@list(Articles)` body lists `Article`'s members
   (typed, not textual), and `@(::` lists root‑model members.
2. A member typo produces a squiggle at the exact template position within one second of
   typing pause; fixing it clears the squiggle.
3. F12 on `@article_card()` jumps to the definition — including when it was pulled in via
   `@<<{{ layout.heddle }}` from another file.
4. Hover on a member expression shows its CLR type; hover on a definition call shows its
   `:: Type` (or "abstract — bound per use").
5. Semantic coloring works with TextMate fallback when the server is down.
6. The language service facade is covered by editor‑less unit tests (position → completion
   list assertions on the blog‑model fixtures).

## Validation scenarios

- The [language-reference.md](../language-reference.md) blog model (`Blog`/`Article`/
  `Author`/`Comment`) as the editor test corpus: completion inside nested `@list` bodies
  (context narrowing), inside `@if` bodies (step‑back — completion must show the *caller's*
  model, not `bool`), after `::` (root), inside a definition with `:: Article`, and inside an
  abstract definition — the artificial‑type behavior per the 6.1 decision: two call sites
  whose types share `Title` but where only one has `Rating` must complete `Title` and not
  `Rating`; hover on a shared member with differing types shows both.
- Diagnostics scenario matrix: syntax error (unclosed `{{`), member typo, type mismatch on
  definition inheritance, `AllowCSharp=false` + C# expression — each mapped to the right
  span.
- Cross‑file: layout + page fixture from the language reference; F12 both directions;
  rename‑safe behavior documented if rename ships.
- Cold start on the benchmark home‑page template: server responsive < 2s after open.

## Testing

### Impact analysis

Engine impact is additive only: scope‑map retention behind `ProvideLanguageFeatures`
(must be provably free when the flag is off — production compiles pay nothing). The new
projects (facade package, LSP server, VS Code extension) cannot regress the engine, but
they create a **cross‑host contract**: the facade serves the LSP server *and* (phase 9)
the demo page's WASM host — drift between hosts is the new failure mode.

### Regression requirements

- Engine suite untouched; compile‑cost benchmark with the flag **off** shows zero change,
  and with it **on** stays within a documented budget.
- ALC reload: a `WeakReference`‑verified collection test pins the unload path (the leak
  risk from Risks) — load, analyze, rebuild, reload, assert the old context collects.
- Position‑mapping: the whitespace‑torture fixtures (hidden channel, `@\`, CRLF) as a
  dedicated suite — mapping drift is the classic LSP regression.

### TDD verdict

**Yes for the facade; contract‑style for the protocol; smoke‑only for the client.**

- The facade is developed against **editor‑less position → completion/hover/diagnostic
  assertions on the blog corpus** (success criterion 6 mandates them) — written first,
  they *are* the spec of the scope map, including the artificial‑type rule for abstract
  definitions.
- The protocol layer uses **recorded‑message contract tests** (JSON in → JSON out
  fixtures from real client traffic) rather than classic TDD — that is what keeps
  hand‑rolled DTOs honest against actual clients.
- The VS Code client gets a thin activation smoke test only; the facade rule keeps logic
  out of it, so there is nothing else to test there.

### The verification loop

1. Write the failing facade assertions for one capability (positions → expected data) on
   the fixture corpus.
2. Implement facade + scope‑map plumbing until green; run the engine gates (suite,
   flag‑off benchmark).
3. Wrap the capability in the protocol layer; record/craft its message fixtures; round‑trip
   until green.
4. Wire into the client; manual + smoke verification in VS Code.
5. Repeat per capability **in the delivery order of 6.2** — the diagnostics‑only
   milestone is the explicit checkpoint to reassess the protocol layer's size before
   continuing.
6. Red → fix the layer where the red lives (facade reds are engine‑data bugs; protocol
   reds are DTO bugs — never patch one to mask the other). Exit: success criteria 1–6.

### The final suite (including deferred phase 9 items)

- The facade suite on the blog corpus, the position‑mapping torture suite, protocol
  round‑trip fixtures, the ALC leak test, and the cold‑start benchmark (< 2 s criterion).
- **Deferred to [phase 9](phase-9-demo-and-integration.md):** the demo page's WASM host
  consumes the same facade — its headless smoke test (one completion + one diagnostic
  round‑trip) becomes the standing **cross‑host contract test**; plus the
  `editors/vscode` walkthrough sample in the gallery.

## Size estimate

| Work item | Size |
| --- | --- |
| Language service facade + scope map retention | M/L |
| LSP server: StreamJsonRpc layer + protocol DTO subset + diagnostics + semantic tokens | M/L |
| Completion + hover + go‑to‑definition | M/L |
| VS Code extension + packaging CI | M |
| Position‑mapping + facade test suites | M |

## .NET 10 opportunities (net10.0 target)

Verified July 2026 against the .NET 10 SDK (current LTS, Nov 2025) and .NET 10 libraries. The
tooling angle dominates this phase: `heddle-lsp` is a dotnet tool, and .NET 10 changed how
tools are packaged. The engine multi‑targets up to `net10.0` already; the tool single‑targets
`net10.0`.

### Tool packaging: per‑RID packages, framework‑dependent + ReadyToRun (recommended)

- **Mechanism**: the .NET 10 SDK packs a tool with `<RuntimeIdentifiers>` into one top‑level
  manifest package plus per‑RID packages, and *any* publish option now applies to tools —
  framework‑dependent platform‑specific, self‑contained, trimmed, or Native AOT; an `any` RID
  adds the classic platform‑agnostic package alongside the RID‑specific ones
  ([What's new in the SDK for .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk#platform-specific-net-tools)).
- **Benefit**: publish `heddle-lsp` framework‑dependent, platform‑specific with
  `PublishReadyToRun` — precompiled server + engine code directly serves the < 2 s cold‑start
  criterion while keeping JIT, Roslyn, and collectible‑ALC model loading fully intact (see the
  AOT analysis below). The R2R caveat already noted in Risks — R2R images ignored in
  *collectible* contexts — only affects the user's model assemblies; the server's own
  assemblies load into the default ALC and keep the benefit.
- **Conditionality**: RID‑split packages install only with a .NET 10+ SDK — older SDKs fail
  with "The settings file in the tool's NuGet package is invalid" (the split uses the
  `Version=2` `DotnetToolSettings.xml` / `Runner="executable"` schema)
  ([Lock, part 8](https://andrewlock.net/exploring-dotnet-10-preview-features-8-supporting-platform-specific-dotnet-tools-on-old-sdks/)).
  Acceptable here: a `net10.0` tool needs the .NET 10 SDK anyway. Self‑contained/AOT variants
  cannot cross‑compile, so packing those needs a per‑OS CI matrix; framework‑dependent + R2R
  per‑RID packs can come off one runner
  ([Lock, part 7](https://andrewlock.net/exploring-dotnet-10-preview-features-7-packaging-self-contained-and-native-aot-dotnet-tools-for-nuget/)).

### Native AOT server — evaluated; conflicts with the model‑reflection design (posture: no AOT in v1)

The .NET 10 SDK can pack the tool as Native AOT per RID (smallest, fastest‑starting variant —
~2.5 MB vs ~30 MB self‑contained in published measurements). For `heddle-lsp` it is the wrong
trade, and the conflict is structural, not a porting chore:

- The server hosts the *real* compiler pipeline. That pipeline (a) loads user model assemblies
  into a collectible `AssemblyLoadContext` for completion/hover (Risks above) and (b) in the
  C# tier, compiles with Roslyn and `Assembly.Load`s the emitted image
  ([ContextCompilation.cs](../../src/Heddle/Runtime/ContextCompilation.cs)). Native AOT's
  documented limitations exclude both in‑process: "No dynamic loading, for example,
  `Assembly.LoadFile`" and "No runtime code generation, for example, `System.Reflection.Emit`"
  ([Native AOT overview — limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)).
  An AOT `heddle-lsp` could not reflect over model assemblies at all.
- Escape hatches considered, and why not now:
  - **`MetadataLoadContext` model inspection** — inspection‑only reflection that never executes
    loaded code, the AOT‑era answer to "read someone else's assembly". But
    `System.Reflection.MetadataLoadContext` carries no `IsAotCompatible` annotation (verified
    against dotnet/runtime `main`), so its AOT‑compatibility is empirical, not guaranteed; it
    returns non‑invokable `Type`s, so `ExType` and the scope map would need a parallel
    metadata‑only code path — exactly the engine‑behavior re‑implementation the maintenance
    rule forbids; and the C# expression tier still cannot run in an AOT process.
  - **AOT front end + out‑of‑process JIT compile host** — preserves fidelity but doubles the
    process and protocol surface of a per‑workspace dev tool whose bottleneck is not the front
    end.
- **Posture**: framework‑dependent + R2R for v1 (above). Revisit AOT only if phase 1's
  no‑Roslyn expression tier becomes the only tier the LSP compiles *and* model inspection moves
  to a metadata‑only path — two large, unforced bets today, made even less likely by the
  engine‑wide decision in [phase 7](phase-7-build-time-compilation.md) that the runtime
  libraries stay dynamic and take on no AOT constraints.
- The < 2 s target is reachable without AOT: R2R server assemblies + source‑generated STJ
  (below — no reflection‑metadata warm‑up on the first message) + deferring the Roslyn assembly
  load until the first document that actually uses the C# tier.

### One‑shot execution (`dotnet tool exec`) — evaluated and REJECTED (July 2026)

- .NET 10's [`dotnet tool exec`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec)
  runs a tool straight from the NuGet cache without installing (the rejection covers the
  command and any alias script the SDK ships for it). **Rejected for security**: the
  editor extension must never trigger download‑and‑execute of a package at launch — an
  unacceptable supply‑chain surface for a language server; distribution is explicit‑install
  only. (Also mechanically unfit, measured on SDK 10.0.301: a cold run with an open non‑TTY
  stdin — a stdio LSP client's exact topology — blocks indefinitely on the download
  confirmation prompt, the GA SDK ships no `--yes` flag, and even warm runs add ~0.5 s of CLI
  overhead before the server process exists.)

### Distribution posture

- Explicit `dotnet tool install -g heddle-lsp` (user‑initiated, version‑pinnable) as the
  documented path, with a VS Code setting for the server executable path.
- Optionally bundle per‑platform server binaries inside **platform‑specific VSIXes**
  (`vsce publish --target win32-x64 …`; win32/linux/alpine/darwin × x64/arm64 targets,
  supported since VS Code 1.61) — version‑locks the server to the extension, ships through
  marketplace signing, and pairs naturally with the per‑RID publish outputs above
  ([Publishing extensions — platform‑specific](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#platformspecific-extensions)).

### DTO layer: System.Text.Json

- **Source‑generated `JsonSerializerContext` over the hand‑written LSP DTOs** (source‑gen since
  .NET 6): no reflection‑metadata generation on the first (de)serialize — a cold‑start win —
  and it keeps the DTO layer trim/AOT‑clean by construction, so the AOT door stays open at zero
  ongoing cost.
- .NET 10 additions, assessed for LSP interop
  ([What's new in .NET 10 libraries — serialization](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries#serialization)):
  `JsonSerializerOptions.Strict` is the *wrong* preset here — it disallows unmapped members,
  while LSP clients legitimately send properties the hand‑written DTO subset doesn't model;
  take only `AllowDuplicateProperties = false` as cheap payload hardening. The new
  `ReferenceHandler` support in `JsonSourceGenerationOptions` and the `PipeReader`
  serializer overloads don't apply (no cycles in LSP DTOs; StreamJsonRpc owns the transport).

### StreamJsonRpc wiring

- Use `SystemTextJsonFormatter` + `HeaderDelimitedMessageHandler` — LSP *is* UTF‑8 JSON with
  `Content-Length` framing, and the official guidance is direct: "When the remote party does
  not support MessagePack but does support UTF‑8 encoded JSON, SystemTextJsonFormatter offers
  the most performant choice available"
  ([extensibility docs](https://microsoft.github.io/vs-streamjsonrpc/docs/extensibility.html)).
  Plug the source‑generated context in via the formatter's
  `JsonSerializerOptions.TypeInfoResolver`; the legacy Newtonsoft `JsonMessageFormatter` stays
  out of the dependency graph.
- If AOT is ever revisited: StreamJsonRpc's NativeAOT guidance requires exactly that
  `TypeInfoResolver` wiring plus source‑generated proxies (`[JsonRpcContract]` with the
  `EnableStreamJsonRpcInterceptors` MSBuild property) in place of dynamic proxies
  ([nativeAOT.md](https://github.com/microsoft/vs-streamjsonrpc/blob/main/docfx/docs/nativeAOT.md)).
  A hand‑rolled server registering local RPC methods directly needs no dynamic proxies under
  JIT, so v1 changes nothing.

## Open questions

None — all resolved July 2026 and integrated above: the facade ships as the separate
`Heddle.LanguageServices` package with a zero‑breaking‑change extraction (6.1);
abstract‑definition completion uses the pinned type when pinned and the name‑intersection
artificial type across call sites when abstract (6.1); browser/demo‑page reuse of the LSP
is now its own roadmap phase — [phase 9](phase-9-demo-and-integration.md) (demo page,
integration demos, and the demo‑driven test suite); and the LSP library decision
(hand‑rolled over `StreamJsonRpc`) lives in 6.2 with its landscape record.

## External grounding

- [OmniSharp.Extensions.LanguageServer — NuGet](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer)
  — latest release 0.19.9, September 21, 2023; 2.4M total downloads; no newer release since.
- [OmniSharp/csharp-language-server-protocol, issue #1221 "Repo Support Status?"](https://github.com/OmniSharp/csharp-language-server-protocol/issues/1221)
  — April 2024 question about maintenance (unmerged version bumps, unanswered issues) with
  no maintainer resolution; notes Bicep as a production consumer.
- [EmmyLua.LanguageServer.Framework — NuGet](https://www.nuget.org/packages/EmmyLua.LanguageServer.Framework)
  — 0.9.2, April 15, 2026; full LSP 3.18, zero dependencies (`System.Text.Json`), AOT‑ready;
  MIT ([repo](https://github.com/CppCXY/LanguageServer.Framework)).
- [Microsoft.CommonLanguageServerProtocol.Framework — NuGet](https://www.nuget.org/packages/Microsoft.CommonLanguageServerProtocol.Framework)
  — CLaSP, the Roslyn/Razor LSP base; prerelease‑only source package "subject to arbitrary
  changes… no compatibility guarantees".
- [Microsoft.VisualStudio.LanguageServer.Protocol — NuGet](https://www.nuget.org/packages/Microsoft.VisualStudio.LanguageServer.Protocol)
  — last public release 17.2.8, May 2022; newer versions are not published publicly.
- [StreamJsonRpc — NuGet](https://www.nuget.org/packages/StreamJsonRpc)
  — Microsoft; 2.25.29, June 2026; 32.6M downloads — the actively maintained JSON‑RPC base
  for the hand‑rolled escape hatch.
- [Draco.Lsp — NuGet](https://www.nuget.org/packages/Draco.Lsp)
  — prerelease‑only (0.4.14‑pre, October 2024); built for the Draco language project.
- [How to use and debug assembly unloadability in .NET — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)
  — collectible `AssemblyLoadContext` semantics: unload is cooperative, what keeps a context
  alive, `WeakReference` verification, ReadyToRun ignored in collectible contexts.
- [Semantic Highlight Guide — VS Code API](https://code.visualstudio.com/api/language-extensions/semantic-highlight-guide)
  — "The editor applies the highlighting from semantic tokens on top of the highlighting
  from grammars", with TextMate scopes as the theming fallback.
- [IDE support — templ docs](https://templ.guide/developer-tools/ide-support/)
  — the `templ lsp` subcommand shipped in the CLI drives the VS Code and Neovim
  integrations.
