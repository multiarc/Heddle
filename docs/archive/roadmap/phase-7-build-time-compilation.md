# Phase 7 — Build‑Time Template Pre‑compilation (Razor‑style / T4 successor)

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Soft dependency: phase 1 (native expressions map 1:1 into emitted C#; without it, member
> paths emit directly and the embedded‑C# tier pastes verbatim). No grammar change — a
> second *backend* over the shared front end.

## Goal

Pre‑compile `.heddle` templates **at build time** into the application assembly plus
discovery metadata — exactly the shape of Razor's compiled views — so that at runtime a
precompiled template is **looked up, not parsed and compiled**:

- **Startup cost disappears for precompiled templates** — no ANTLR parse, no
  expression‑tree building, no Roslyn work at process start; when every template an app
  uses is precompiled, `Microsoft.CodeAnalysis` is never even loaded;
- **templates are validated by the build** — a broken template fails compilation with a
  diagnostic at the `.heddle` position, not a render‑time surprise;
- **templates become debuggable** — real generated C# to step through, real stack traces;
- Heddle becomes a credible **T4 successor** for build‑time text/code generation with a
  vastly better language (composition, typing, imports).

**Ratified direction (July 2026): the runtime stays fully dynamic — no AOT constraints on
the engine.** An earlier draft of this phase framed Native AOT compatibility of the runtime
libraries as the headline. That is now an explicit **non‑goal**: making `Heddle`/
`Heddle.Language` AOT‑safe would mean giving up runtime Roslyn emit, compiled expression
trees, and reflection‑driven binding
([all unsupported or degraded under Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment))
— a significant restriction on the *whole engine* to optimize a one‑time startup cost that
template pre‑compilation eliminates anyway. The engine remains dynamic and flexible by
design: runtime template loading, hot reload, `:: dynamic`, full‑C# expressions all keep
working, and precompiled and runtime‑compiled templates **coexist in one process**.

The runtime path remains fully supported and is the **semantic reference**: the generated
backend must produce byte‑identical output, proven continuously by a differential harness.

## Design

### Architecture

- **Shared front end.** The generator reuses the existing parse pipeline —
  [DocumentParser](../../src/Heddle/Language/DocumentParser.cs) → `ParseContext`
  (definitions, chains, imports) — unchanged. This works precisely because the front end
  is pure text → structure (ANTLR parse, no reflection, no live types). One grammar, one
  parse, two backends.
- **Type binding is *not* shared — it cannot be.** Inside a source generator the user's
  model types exist only as Roslyn `ITypeSymbol`s in the `Compilation` being built; the
  target assembly does not exist yet, so `System.Reflection` over it is impossible.
  Generators introspect user code through the compilation and symbol APIs, never by
  loading it
  ([source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)).
  The runtime backend's binding is reflection over live types —
  [HeddleCompiler](../../src/Heddle/Runtime/HeddleCompiler.cs) resolves member paths via
  `Type.GetProperty`,
  [ModelParameter](../../src/Heddle/Runtime/Parameters/ModelParameter.cs) compiles
  expression trees — so it **cannot run in‑generator**. Replacement strategies:
  - **(a) v1 — emit typed C#, let the compiler bind.** The generator emits member
    accesses and expressions as ordinary typed C# against the declared `@model`; the
    project's own compilation type‑checks them. A bad member path surfaces as a C# error
    inside generated code — acceptable *only* with precise `#line` span mapping back to
    the `.heddle` position (see Diagnostics). No symbol‑walking logic to maintain, and
    binding semantics are guaranteed by the same compiler that builds the user's project.
  - **(b) committed follow‑up — `ISymbol` binding for first‑class diagnostics.**
    Resolve member paths against the `Compilation` (`ITypeSymbol.GetMembers` walks
    mirroring the runtime tier order) and report native Heddle diagnostics (exact
    template span) before the C# compiler ever sees the generated code. Strictly
    additive over (a): same emitted code, better errors. **Decided (July 2026): (b) is
    the committed migration path for pre‑compilation, planned as this phase's second
    milestone** — not contingent on user complaints about remapped errors. (a) exists so
    v1 can ship correct behavior early; (b) is where the diagnostics story is meant to
    land.
- **New backend** (`src/Heddle.SourceGenerator`, netstandard2.0 — generator assemblies
  load into the compiler/IDE process and must target netstandard2.0): walks the
  `ParseContext` produced by the shared front end and **emits C#** instead of an object
  graph:
  - static text pieces → string constants (emitted in document‑piece order, mirroring
    `RuntimeDocument.GetDocumentPieces`);
  - member paths → direct property access code (null‑conditional chains reproducing the
    member tier's null‑safe semantics), emitted per strategy (a) — the project's own
    compilation binds them;
  - native expressions (phase 1 AST) → the equivalent C# expression — the AST is a strict
    C# subset, so this mapping is 1:1 by construction;
  - embedded C# tier → pasted through verbatim into the generated method (at build time
    Roslyn compiles it as part of the project — the `AllowCSharp` trust decision becomes a
    build‑time property);
  - `:: dynamic` templates → emitted as C# `dynamic` member access (the C# compiler wires
    the same runtime binder the engine uses) — **precompilable like everything else**;
  - extension calls → generated invocations against pre‑constructed extension instances
    (no `Activator`, no registry lookup at runtime). **Decided (July 2026): extension
    logic is never inlined into generated code — always bound from the referenced
    assemblies.** Extensions are compiled assemblies the app references; invoking their
    instances means a logic fix or security patch in a compatible interface reaches
    precompiled templates by updating the package, without regenerating anything.
    Inlining copies of extension bodies would be both inefficient (duplicated per
    template) and a patching hazard — rejected outright, including the earlier idea of
    "inlined fast paths" for built‑ins;
  - definitions/imports/overrides → resolved at generation time exactly as the runtime
    compiler resolves them (document‑order layering preserved).
- **Generated API shape** (sketch): one static class per template —
  `GeneratedTemplates.HomePage.Generate(Blog model)` / `Render(Blog model, TextWriter sink)`
  (sink overload aligns with phase 8) — strongly typed against the declared `@model`.
  **Decided (July 2026): the typed entry points are the *recommended* host API for
  templates known at compile time** (compile‑checked model type, no lookup, no
  validation gauntlet); the key‑based registry lookup is for dynamic call sites — both
  ship, the typed form is free. How the runtime discovers, validates, and hooks the
  registry up is a design of its own — see
  [Metadata, discovery & runtime hookup](#metadata-discovery--runtime-hookup) below.
- **Runtime integration — lookup first, dynamic always available.** The engine gains a
  precompiled‑template registry populated through the *existing* registration pattern
  (`HeddleTemplate.Configure(assembly)` already scans assemblies for extension attributes —
  the same scan collects the manifest below). Resolution order in
  `TemplateResolver`/`HeddleTemplate`:
  1. precompiled registry hit **and** validation passes → generated renderer; **zero
     parse, zero compile**;
  2. miss or any validation failure → today's dynamic path, completely unchanged
     (parse + compile + cache).

  Mixed mode is first‑class, not an edge case: an app can ship precompiled page templates
  while compiling user‑supplied or database‑loaded templates at runtime in the same
  process — the precompiled assembly is a *cache seeded at build time*, never a cage
  (this mirrors Razor's runtime‑compilation dev add‑on).
- **Wiring:** templates registered as `AdditionalFiles` with
  `<HeddleTemplate Include="Templates/**/*.heddle" />` sugar in a props/targets file shipped
  by the NuGet package, consumed via the `IIncrementalGenerator` `AdditionalTextsProvider`
  pipeline with value‑equatable intermediate models so per‑file caching actually holds
  (per the
  [incremental generators design doc](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md):
  extract data out of inputs early, never cache symbols/compilations). Compile options
  (output profile, expression mode, root path for imports) come from MSBuild properties /
  an `AdditionalFiles` config, mirroring `TemplateOptions`.
- **Diagnostics & `#line` mapping:** parse/structure errors (`HeddleCompileError`) surface
  as Roslyn diagnostics from the generator with `.heddle` file/position. Under strategy
  (a), member/type errors arrive as C# errors in generated code and are remapped to the
  template via the span form of `#line` — `#line (l,c) - (l,c) offset "file.heddle"` —
  which the C# reference documents as designed for exactly this DSL remapping (it is how
  Razor maps `.cshtml` errors); `#line hidden` marks generator scaffolding so debugger
  stepping skips it and lands on template lines. Limits: mapping is only as good as the
  spans the emitter records, and stepping granularity follows emitted statements — treat
  fidelity as progressive enhancement.
- **Packaging:** the generator ships as its own NuGet (`Heddle.Generator`) with the
  generator assembly in the `analyzers/dotnet/cs` folder (the
  [cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
  packaging rule); the core `Heddle` package stays runtime‑only and **unrestricted**.
  NuGet does **not** restore package dependencies for analyzer assemblies, so
  generation‑time dependencies — the parse front end and `Antlr4.Runtime.Standard` — must
  be packed *inside* the analyzer folder alongside the generator (the cookbook's
  `GeneratePathProperty="true"` + `PrivateAssets="all"` recipe); `Microsoft.CodeAnalysis`
  itself must never be shipped — the host compiler provides it. **Decided (July 2026): no
  MSBuild‑task fallback.** SDK‑style projects with source‑generator support are the
  audience; legacy build setups are out of scope, and a second driver for the same emitter
  would be maintenance without a customer. Revisit only if a real generator‑hostile
  environment materializes with a real user attached.

### Metadata, discovery & runtime hookup

The attribute alone is **not** enough. The runtime must answer four questions before it can
trust a precompiled entry over a fresh compile: *is this artifact compatible with me? was it
compiled under the options this request uses? against the extensions this host has
registered? and is it stale?* The metadata design answers all four in two layers, so the
attribute stays a tiny discovery pointer and the real metadata is **typed generated code**
(no attribute‑blob parsing, one reflection touch per assembly):

**Layer 1 — the discovery marker, one per assembly:**

```csharp
[assembly: HeddleCompiledTemplates(
    manifest:      typeof(GeneratedTemplates.__Manifest),
    schemaVersion: 1,          // metadata contract version
    engineVersion: "2.3.0")]   // Heddle version the emitter targeted
```

An unknown/newer `schemaVersion` or an incompatible `engineVersion` causes the **whole
manifest to be ignored** — every template in it silently falls back to the dynamic path
(safe by default, surfaced through the diagnostic hook below). This is the version gate
that lets old precompiled assemblies keep working — or degrade gracefully — across engine
upgrades.

**Layer 2 — the generated manifest**, a static class yielding one record per template:

| Field | Purpose / consumer |
| --- | --- |
| `Key` | Resolver identity per the [naming policy](#template-identity--naming-policy-consolidated-july-2026): file‑driven → full `RootPath`‑relative normalized path (matching `TemplateResolver` semantics and `FileNamePostfix` handling); non‑file → integration‑supplied via the key‑policy seam. `@partial()` resolution uses the same key space |
| `EntryPoint` | The generated type; a small adapter presents it as the same template surface the dynamic path returns, so host code cannot tell the difference |
| `ModelType` + `IsDynamic` | The `Generate(model)` type check (same guard the runtime path performs), host/LSP introspection, and typed‑entry‑point discovery |
| `ContentHash` | Staleness of the root source file (dev override) |
| `Imports` | The **transitive `@<<` closure** as `(path, contentHash)` pairs — staleness must cover imported files, not just the root: editing `layout.heddle` must invalidate `home.heddle`'s precompiled copy. (`@<<` needs no runtime linkage — definitions were flattened at build time; only the hashes matter here) |
| `OptionsFingerprint` | The salient compile options baked into the artifact: `OutputProfile` (phase 2), `ExpressionMode`/`AllowCSharp` (phase 1), later `TrimDirectiveLines` (phase 4). A template precompiled under `Text` is *not* a valid answer for an `Html` request — mismatch → dynamic recompile under the requested options |
| `ExtensionBindings` | `(name, type reference)` for every extension the template bound at build time — extensions are **bound from the referenced assemblies at runtime, never inlined** (decided July 2026), so this is the record of *what* to bind. Detects host registry divergence — `[ExtensionReplace]` overrides or custom extensions registered at runtime — where the precompiled wiring would silently behave differently from a fresh compile. **How a recorded binding is matched to a live assembly is an integration concern** (decided July 2026): the integration supplies the assembly search schema mapped to the template (a binding‑resolution seam at `Configure` time); the engine default is assembly‑qualified type name *without* version — an overridden extension is the divergence that matters, patch‑version churn is not. Mismatch → per `PrecompiledMismatchPolicy` |
| `Capabilities` | Which entry points were emitted: string / `TextWriter` / UTF‑8 sink (phase 8 tiers, output‑profile‑gated u8 pieces) — the resolver picks per requested API and falls back per capability |

**The hookup flow** in `TemplateResolver`/`HeddleTemplate`:

1. Registry lookup by `Key` (registry populated once per `Configure(assembly)` call).
2. **Validation gauntlet** — manifest‑level version gate (checked once at registration);
   per‑request: `OptionsFingerprint` vs the request's `TemplateOptions`,
   `ExtensionBindings` vs the live `TemplateFactory` registry, and — only when
   `EnableFileChangeCheck` is on — `ContentHash` + `Imports` closure vs disk.
3. All pass → adapter over the generated entry point; zero parse, zero compile.
4. Any failure → policy‑controlled (**decided July 2026** — an explicit option, sketch:
   `PrecompiledMismatchPolicy { Fallback, Strict }`):
   - **`Fallback` (default):** the unchanged dynamic path, **plus a warning through the
     diagnostic callback** (sketch: `HeddleDiagnostics.PrecompiledFallback(key, reason)`)
     so hosts can see *why* a precompiled template wasn't used — a silent fallback that
     quietly re‑adds compile cost is the failure mode to design against.
   - **`Strict`:** a precompiled entry that *exists* but cannot be plugged (options
     mismatch, extension‑binding divergence, staleness) **throws hard** instead of
     recompiling — for deployments that must never pay dynamic‑compile cost or silently
     render from a different compilation than the one they shipped. A plain registry
     *miss* (template was never precompiled) is not a `Strict` failure — strictness
     polices divergence, not coverage.

**Partials route through the resolver at render time.** Generated code for `@partial("x")`
calls back into the resolver rather than binding statically — so a precompiled template can
render a runtime‑compiled partial and vice versa, and the registry serves both directions
of the mixed‑mode matrix.

### Template identity & naming policy (consolidated July 2026)

The policy mirrors extension‑name registration — registered, unique, replaceable only
explicitly — with one caveat the extension model doesn't have: **the engine does not know
template names unless a file drives them.** Consolidated rules:

- **File‑driven templates: the key is the full path + file name** — concretely, the full
  `RootPath`‑relative path (absolute build‑machine paths cannot be the key: they don't
  exist on the runtime machine, so "full path" is anchored at the resolver root the same
  way `TemplateResolver` and `@partial()` already anchor paths), normalized (separators
  unified, `FileNamePostfix` applied the same way the resolver applies it). This is the
  default the generator emits for `AdditionalFiles` templates with zero configuration.
- **Non‑file templates: naming belongs to the integration layer, through an open
  interface.** The engine ships the mechanics, not a policy: the manifest `Key` is
  produced by a key‑policy seam — at build time, MSBuild item metadata
  (`<HeddleTemplate Include="…" Key="explicit-key" />`) or a key‑provider hook for
  integrations that materialize templates for pre‑compilation from non‑file sources; at
  runtime, hosts resolve whatever key space they chose. Heddle's task is supporting
  mechanics + auto‑discovery, not covering every naming scheme.
- **Uniqueness is required for pre‑compilation, required nowhere else.** Duplicate keys
  across registered manifests are a **registration error** (same posture as
  `TemplateOverrideException` for extension names) unless one entry explicitly replaces
  another. Fully dynamic templates have no naming requirement at all — unchanged.
- **The integration layer MUST be able to see what is precompiled.** Discovery is a
  public, first‑class API, not an internal cache: the registry exposes enumeration of
  registered entries (key, model type, options fingerprint, capabilities) after
  `Configure(assembly)`, plus the `PrecompiledFallback` callback for per‑request
  visibility. An integration that doesn't consult either still works — but one that wants
  to route, validate, or report on precompiled coverage has everything it needs.

Consolidation review — surfaced items that need a maintainer call (also listed under Open
questions): (1) **key normalization details** — case sensitivity (a Windows build machine
vs a Linux runtime host disagree about `Home.heddle` vs `home.heddle`) and separator
canonicalization must be pinned as one documented rule, and it should be the rule
`TemplateResolver` already lives by; (2) **explicit replacement mechanics** — does v1 need
a `Replace`‑style flag for precompiled entries (mirroring `[ExtensionReplace]`), or is
"duplicate = error" enough until a real layering use case appears?

This two‑layer shape is the
[`RazorCompiledItemAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.razor.hosting.razorcompileditemattribute)
model taken one step further: Razor's attribute points at a compiled item and its loader
reads attributes per assembly; Heddle additionally needs the options fingerprint and
extension bindings because — unlike Razor views, which are all‑or‑nothing per app — Heddle
runs precompiled and dynamic templates side by side under host‑configurable options and a
host‑extensible registry.

### Explicitly out of scope: Native AOT of the runtime

Recorded so it is not re‑proposed: the runtime libraries will **not** take on AOT/trimming
constraints. Under Native AOT the engine's core mechanisms are unsupported (Roslyn emit,
dynamic assembly loading) or permanently degraded (`System.Linq.Expressions` runs
interpreted) — the
[official limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)
are structural, so "AOT support" would in practice mean forking or crippling the dynamic
engine. The benefit it chased (startup) is delivered by pre‑compilation itself; the
flexibility it would cost (runtime templates, hot reload, `:: dynamic`, full C#) is the
engine's identity. Revisit only on concrete, repeated demand — and then as a separate
slimmed package, never as constraints on `Heddle`/`Heddle.Language`.

## Back‑compat analysis

- Purely additive — a new package, generated code, and a registry consulted before the
  existing path; runtime behavior for non‑precompiled templates is untouched.
- Semantics coupling risk is *drift*, not breakage: every future language change (phases
  1–5) must land in both backends. Gate: the differential harness runs in CI from the day
  this phase ships; the runtime path is authoritative on divergence.
- The extension model is unchanged for authors: custom extensions work in generated mode as
  ordinary instantiated classes (documented requirement: parameterless ctor, no reliance on
  registry mutation at runtime).
- Precedence surprise guard: a host that precompiles a template *and* edits it on disk
  without `EnableFileChangeCheck` renders the precompiled version — document loudly (and
  the `contentHash` makes the staleness detectable/loggable even when override is off).

## Risks & mitigations

- **Second backend = permanent double maintenance** — the phase's defining risk for a
  one‑maintainer project. Mitigations: share the front end *and* the text‑level resolution
  logic (definition layering, import ordering); type binding necessarily diverges
  (reflection over live types at runtime vs. the project compilation type‑checking the
  emitted C# at build time), which is exactly the divergence the differential harness
  gates; land this phase **after** the language‑shaping phases (1, 3, 5) so the surface
  being mirrored is stable. (L)
- **Generator performance** on large template sets: incremental generator design
  (`IIncrementalGenerator`), per‑file caching keyed on content + options — cacheable only
  if pipeline models are value‑equatable and hold no symbols (design‑doc rule). (M)
- **netstandard2.0 front‑end constraint** — now fact, not hedge: generator assemblies
  load into the compiler/IDE process and must target netstandard2.0. Both
  [Heddle](../../src/Heddle/Heddle.csproj) and
  [Heddle.Language](../../src/Heddle.Language/Heddle.Language.csproj) already multi‑target
  netstandard2.0, **but** `DocumentParser`/`ParseContext` live in the main `Heddle`
  assembly, whose netstandard2.0 build references `Microsoft.CodeAnalysis.CSharp` and
  `Microsoft.Extensions.*` — a generator must not carry its own Roslyn into the compiler
  process. So the parse front end moves into (or beside) the parse‑only `Heddle.Language`
  package, which already targets netstandard2.0 with `Antlr4.Runtime.Standard` as its only
  dependency (packed as an analyzer asset per Packaging above). A mechanical split, not a
  design unknown — and note it constrains only the *generator's* dependency graph, never
  the runtime's. (M)
- **v1 type‑error UX rides on `#line` fidelity**: under strategy (a) a template member
  typo is reported by the C# compiler against generated code — if the span mapping is
  off, the user sees an error inside a file they never wrote. Mitigations: span‑form
  `#line` per emitted expression; diagnostics fixtures asserting reported
  file/line/column for representative mistakes; strategy (b) as the upgrade that
  pre‑empts these errors with native Heddle diagnostics. (M)
- **Registry/override precedence bugs** (precompiled vs file‑changed vs runtime‑loaded):
  pin the resolution order with fixtures before wiring; `contentHash` staleness must be
  covered by the mixed‑mode scenarios below. (M)
- **`#line` debugger‑stepping quality** (distinct from error mapping above): treat as
  progressive enhancement; correctness first, stepping fidelity second. (S)

## Success criteria

1. **Zero‑compile startup:** a sample app whose templates are all precompiled renders the
   benchmark home page with **no ANTLR parse and no Roslyn work at startup** — verified
   both by time‑to‑first‑render (orders of magnitude below the runtime path's first
   compile, measured) and by asserting `Microsoft.CodeAnalysis*` assemblies are never
   loaded (AssemblyLoad‑event probe).
2. **Differential harness**: byte‑identical output between generated and runtime backends
   across the full fixture corpus (`src/Heddle.Tests/TestTemplate/**`), running in CI.
3. **Mixed mode works**: one process renders a precompiled template, a runtime‑compiled
   template, and — with `EnableFileChangeCheck` on — an edited precompiled template whose
   on‑disk change wins over the stale precompiled copy (contentHash mismatch → runtime
   fallback), all fixture‑verified.
4. A compile error in a template fails the **build** with a diagnostic pointing at the
   `.heddle` file and line — parse errors as native Heddle diagnostics from the
   generator; member/type errors in v1 as C# errors remapped through `#line` spans (the
   file‑and‑line requirement holds either way; the `ISymbol` upgrade path later replaces
   the remapped ones with native Heddle diagnostics).
5. A custom extension authored per [custom-extensions.md](../custom-extensions.md) works
   identically under the generated backend (fixture‑verified).
6. T4‑style usage proven: a codegen sample (e.g. generating a C# enum from data) runs as
   part of build with no runtime Heddle dependency in the output assembly.
7. **The runtime keeps every dynamic capability** with the generator package installed:
   runtime‑loaded template strings, `:: dynamic`, `AllowCSharp` runtime compilation — the
   existing suite passes unchanged in a project that also uses the generator.
8. **Discovery is host‑visible:** after `Configure(assembly)`, host code enumerates the
   precompiled registry (keys, model types, options fingerprints, capabilities) and the
   duplicate‑key registration error fires for two manifests claiming one key.

## Validation scenarios

- Fixture‑corpus differential run (every `.heddle` fixture: generate, render both paths,
  byte‑compare) — the phase's backbone test.
- Startup probe: precompiled‑only app renders with zero `Microsoft.CodeAnalysis*` /
  ANTLR activity; the same app plus one runtime‑compiled template loads them lazily,
  on first dynamic compile only.
- Composition‑heavy case: the benchmark's `layout.heddle` + `home.heddle` with `<body:body>`
  override — generated code preserves document‑order override semantics.
- Mixed‑mode precedence matrix: precompiled hit; registry miss → dynamic; file edited +
  `EnableFileChangeCheck` → runtime recompile wins; file edited without the flag →
  precompiled renders + staleness detectable via `contentHash`.
- Validation‑gauntlet fallbacks (each firing the diagnostic callback with the right
  reason): options mismatch — template precompiled under `Text`, host requests `Html` →
  dynamic recompile under `Html`; extension mismatch — host applies `[ExtensionReplace]`
  to an extension named in `ExtensionBindings` → dynamic path picks up the override;
  import‑closure staleness — edit only `layout.heddle` → `home.heddle`'s precompiled copy
  invalidates under `EnableFileChangeCheck`; version gate — an assembly with a newer
  `schemaVersion` is ignored wholesale, everything falls back, one callback per manifest.
- `Strict` policy: options mismatch under `PrecompiledMismatchPolicy.Strict` → throws
  with the mismatch reason (no dynamic recompile); the same fixture under `Fallback`
  recompiles + warns via the callback; a plain registry miss under `Strict` still takes
  the dynamic path (strictness polices divergence, not coverage).
- Registry discovery: host enumerates precompiled entries after `Configure`; two
  assemblies registering the same `Key` → registration error (unless explicit
  replacement, if that ships — see Open questions).
- Partial routing both directions: a precompiled template whose `@partial("x")` resolves
  to a runtime‑compiled template, and a runtime‑compiled template whose partial resolves
  to a precompiled one — byte‑identical to the all‑dynamic render.
- `:: dynamic` template precompiled (emitted as C# `dynamic`) → renders identically to the
  runtime path against an `ExpandoObject` model.
- Diagnostics: template with a member typo → build error with `.heddle` path/line (in v1
  a remapped C# error, e.g. CS1061, whose *reported* location must be the template span —
  fixture asserts file/line/column).
- Incremental build: touching one template regenerates only that template (generator
  incrementality test).
- Options parity: output profile (phase 2) and expression mode (phase 1) set via MSBuild
  properties affect generated code identically to `TemplateOptions` at runtime.

## Testing

### Impact analysis

The build‑time side is all new (generator, emitter, packaging). The runtime side touches
**template resolution for everyone**: the registry consult + validation gauntlet sit in
front of the dynamic path, so they must be provably inert when no manifests are
registered. The front‑end move (parse pipeline to/beside `Heddle.Language`) is a
mechanical split with engine‑wide compile impact — observable behavior must not change
at all. The permanent exposure is **backend drift**: every future language change must
land in both backends, which is why the harness below is CI‑resident, not a one‑off.

### Regression requirements

- **Inertness**: with zero precompiled assemblies registered, resolver behavior is
  byte‑identical and shows no measurable overhead (benchmark).
- The existing suite passes unchanged in a project that also uses the generator
  (success criterion 7).
- After the front‑end split: the full engine suite + goldens — the split must be
  observably nothing.
- The differential harness runs in CI **from the day the phase ships** (Back‑compat's
  drift gate) — it is the regression system for both backends forever after.

### TDD verdict

**The emitter uses differential‑driven development — a stricter oracle than classic
TDD.** Every emitter feature starts by selecting the corpus fixtures it must render
byte‑identically against the runtime backend; the harness is the test, written first by
construction, and the runtime path is authoritative on divergence — a fix goes to the
emitter, never to the runtime to match it (unless the divergence exposes a runtime bug,
which is a maintainer ratification event). Classic TDD applies where behavior is
enumerable: the validation gauntlet (options/extensions/staleness × `Fallback`/`Strict`
matrix), registry duplicate/discovery rules, and key normalization. `#line` mapping is
fixture‑first: seeded template mistakes assert reported file/line/column before any
polish.

### The verification loop

1. Pick the next fixture slice from the corpus (start: static text + member paths).
2. Emit; run the differential — **red is the expected starting state**.
3. Implement the emitter feature until the slice is byte‑identical.
4. Widen the slice; repeat until the full corpus differentials green.
5. In parallel, the runtime side loops classically: gauntlet matrix tests first →
   registry/policy implementation → inertness + suite‑with-generator gates.
6. Every loop iteration ends with the CI trio: differential run, inertness gate,
   startup probe. Red → fix the emitter (see authority rule above). Exit: success
   criteria 1–8.

### The final suite (including deferred phase 9 items)

- The differential harness (backbone, CI‑resident), startup probes (AssemblyLoad event
  for `Microsoft.CodeAnalysis*`), the mixed‑mode precedence matrix, the gauntlet fallback
  matrix including `Strict`, discovery/duplicate‑key tests, the generator incrementality
  test, diagnostics span fixtures, and u8‑literal parity checks (lone‑surrogate fixture).
- **Deferred to [phase 9](phase-9-demo-and-integration.md):** `samples/precompiled-app`
  and `samples/codegen-t4-successor`, both golden‑asserted in CI — and the precompiled
  sample's output must equal its dynamic twin, enforcing the differential rule at the
  gallery level too.

## Size estimate

| Work item | Size |
| --- | --- |
| Emitter (structure → C#): text/members/expressions/extensions/definitions | **L** |
| Generator host (incremental, AdditionalFiles, options, diagnostics) | M |
| Runtime integration: marker attribute + generated manifest, registry + discovery enumeration, validation gauntlet (versions/options/extensions/staleness), mismatch policy, key‑policy + binding‑resolution seams, resolver precedence, diagnostic callback | M/L |
| Packaging (props/targets, `Heddle.Generator` NuGet — no MSBuild‑task fallback) | M |
| Differential harness + CI | M |
| Samples (precompiled app, T4‑style codegen) + docs | M |

## .NET 10 opportunities (net10.0 target)

> Scope guard — the split that governs everything in this section: these items concern the
> **emitted** code, which compiles inside the *user's* project against the user's TFM and
> `LangVersion`. The generator itself stays netstandard2.0 and gains none of these
> features (see the last item). The emitter therefore treats every feature below as
> **conditional emission**: the incremental pipeline reads the language version from
> `context.ParseOptionsProvider` (cast to `CSharpParseOptions`, read `.LanguageVersion` —
> an enum, safe to fold into the value‑equatable options model per the
> [design‑doc caching rules](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md))
> and picks the highest emission tier the project allows, with a plain down‑level fallback
> for every tier. On the headline `net10.0` target all tiers are on by default
> (`LangVersion` defaults to C# 14 there); on older TFMs the same templates still generate,
> just plainer C#.

- **Pre‑encoded UTF‑8 static pieces via `u8` literals (C# 11) — the cross‑phase
  headline.** *Mechanism:* C# 11's
  [UTF‑8 string literals](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#utf-8-string-literals)
  (`"..."u8`) are stored by the compiler as `ReadOnlySpan<byte>` — the UTF‑8 bytes land in
  the assembly's static data and the span wraps them directly: no allocation, no
  encoding call, no static constructor
  ([language reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types#utf-8-string-literals)).
  *Benefit:* this moves static‑piece transcoding from **run time to the C# compiler**.
  The runtime backend, when phase 8's byte/UTF‑8 sink lands, must either call
  `Encoding.UTF8.GetBytes` per render or cache a `byte[]` per piece (heap + first‑render
  warmup). The generated backend instead emits each static text piece *twice*, gated by
  output profile:
  - `internal const string Piece3 = "...";` — the `string`/`TextWriter` path (v1 shape,
    unconditional);
  - `internal static ReadOnlySpan<byte> Piece3Utf8 => "..."u8;` — the byte‑sink path,
    where `Render(model, sink)` writing a piece becomes a straight copy out of the
    assembly's read‑only data: zero startup cost, zero GC objects.

  This is the single best interaction between this phase and phase 8: build‑time
  compilation is the only backend that can pre‑encode at all, so it should reserve the
  piece‑level emission hook **now** (emit pieces via one helper in the emitter) even
  though the u8 tier only pays off once the byte sink exists.
  *Conditionality:* `LangVersion` ≥ 11 **and** the phase 8 sink overload; fallback is
  string constants plus the same runtime encoding the runtime backend uses. Emitting both
  forms duplicates static data — mitigation: emit per configured output profile.
  *Correctness notes for the differential harness:* u8 literals "aren't compile‑time
  constants; they're runtime constants" — fine here (they're only ever written to a
  sink), but they can't be `const`, can't be default parameter values, and can't be
  interpolated. Compiler‑side UTF‑8 encoding is byte‑identical to runtime
  `Encoding.UTF8` for well‑formed text; text containing an unpaired surrogate is a
  *compile error* in a u8 literal where the runtime path would substitute a replacement
  character — the generator should pre‑validate pieces and report its own diagnostic
  rather than leak a C# error (fixture: template static text with a lone surrogate).

- **`file`‑scoped types for generated scaffolding (C# 11).** *Mechanism:* the
  [`file` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/file)
  makes a type visible only within its own source file — a feature designed for
  source‑generated code. *Benefit:* per‑template helper types (piece tables,
  pre‑constructed extension instances, sink adapters) can't collide with user code or
  with other generated templates, without name‑mangling conventions. *Conditionality:*
  the *user's* `LangVersion` ≥ 11 (the generated file compiles in their project — the
  generator's own language version is irrelevant here); fallback is `internal` types
  under a mangled namespace.

- **Collection expressions (C# 12) and first‑class spans (C# 14) — modest, real.**
  *Mechanism/benefit:* piece tables and argument arrays emitted as
  [collection expressions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#collection-expressions)
  (`[...]`) let the compiler pick the optimal initialization (including span‑targeted,
  allocation‑free forms) instead of the emitter hand‑choosing one;
  [C# 14 implicit span conversions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#implicit-span-conversions)
  let emitted helpers take `ReadOnlySpan<char>`/`ReadOnlySpan<byte>` parameters without
  `AsSpan()` noise or parallel array overloads. Both are emission‑tier niceties, not
  architecture: the down‑level fallback is ordinary arrays and `string` parameters.
  *Conditionality:* user `LangVersion` ≥ 12 / ≥ 14 respectively.

- **.NET 10 JIT rewards exactly the code shape this backend emits.** One honest
  paragraph: .NET 10's JIT work is dominated by *de‑abstraction* — devirtualization of
  array interface methods, a late‑devirtualization pass, inlining that unlocks further
  devirtualization, escape analysis extended to delegates and struct fields, and stack
  allocation of small arrays
  ([runtime what's‑new](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#jit-compiler-improvements),
  [Performance Improvements in .NET 10](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
  — e.g. `ReadOnlyCollection<T>` indexed iteration dropped from 1,960.5 ns to 624.6 ns).
  These optimizations fire when call targets are statically knowable. The generated
  backend emits concrete static methods, direct property access, and direct calls on
  pre‑constructed (ideally `sealed`) extension instances — shapes the optimizer can
  devirtualize, inline, and stack‑allocate through, where the runtime backend's compiled
  documents dispatch through interfaces and delegates. The honest caveat: the JIT gains
  accrue to *any* code with concrete shapes, not to generated code per se — the point is
  that this phase's output is structurally positioned to collect them.

- **Codegen CLI distribution (T4‑successor story).** A Heddle‑powered codegen CLI hosts
  the full dynamic engine, so it ships like the phase 6 LSP server: a normal dotnet tool,
  optionally as the .NET 10 SDK's **per‑RID, ReadyToRun tool packages** for fast cold
  start ([What's new in the .NET 10 SDK](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk))
  — installed explicitly via `dotnet tool install` (developer‑initiated `dotnet tool exec`
  also works; the auto‑download concern rejected in phase 6 applies to *editor‑launched*
  servers, not a developer explicitly invoking a CLI). Native‑AOT tool publishing exists
  in the .NET 10 SDK but does not apply here — the CLI embeds the unrestricted dynamic
  runtime (see the out‑of‑scope section).

- **Interceptors — evaluated, deferred past v1.** *Status:* a C# *compiler* feature (not
  a C# 14 language feature), "first shipped experimentally in .NET 8, with stable support
  in .NET 9.0.2xx SDK and later"; still opt‑in via the `<InterceptorsNamespaces>` MSBuild
  property in the consuming project, with the checksum‑based `[InterceptsLocation]` v1
  encoding produced via Roslyn's `GetInterceptableLocation` API
  ([dotnet/roslyn interceptors.md](https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md)).
  *The tempting scenario:* intercept runtime‑API calls (`template.Generate(model)`) and
  rewrite them to the generated static method at compile time — zero‑diff migration from
  the runtime path, no registry/lookup. *Why not v1:* (1) interception is per **call
  site**, and Heddle call sites identify templates by runtime values (paths, loaded
  content) — only a narrow literal‑argument pattern is statically resolvable, so the
  feature would silently cover some calls and not others, which is worse than an explicit
  API (and the metadata registry already gives runtime call sites the precompiled fast
  path without any compiler magic); (2) `GetInterceptableLocation` raises the generator's
  minimum `Microsoft.CodeAnalysis` reference (4.11+), narrowing host‑compiler
  compatibility for a convenience; (3) the v1 entry points (`GeneratedTemplates.X.Generate`)
  are explicit, debuggable, and honest about what is compiled. Verdict: note as a
  possible post‑v1 migration aid; not in scope.

- **Generator‑side reality check: nothing changed, and that's the finding.** As of the
  .NET 10 SDK era, compiler extensions must still target **netstandard2.0**
  ([RS1041](https://github.com/dotnet/roslyn/blob/main/docs/roslyn-analyzers/rules/RS1041.md):
  the compiler may run on .NET Framework in Visual Studio or on .NET in the SDK, and
  netstandard2.0 is the only target loadable in both) — the architecture above already
  assumes this. The one .NET‑10‑era gate to manage deliberately: a generator referencing
  a `Microsoft.CodeAnalysis` **newer** than the host compiler fails to load, and the
  .NET 10 SDK/VS 2026 compilers are the 5.x line
  ([supported Roslyn version mappings](https://learn.microsoft.com/en-us/visualstudio/extensibility/roslyn-version-support)).
  v1 needs nothing newer than the long‑stable 4.x incremental‑generator API surface, so
  the generator should reference the *oldest* `Microsoft.CodeAnalysis` that has what it
  uses — maximizing reach across VS 2022/older SDKs while loading fine on 5.x. Features
  that would raise that floor (interceptors' location API) are exactly the ones deferred
  above.

## Open questions

The July 2026 round resolved the previous seven (naming policy consolidated into the
[identity section](#template-identity--naming-policy-consolidated-july-2026); extensions
bind from assemblies, never inlined; no MSBuild‑task fallback; the `ISymbol` upgrade is
the committed second milestone; typed entry points are the recommended host API;
mismatch handling is the `Fallback`/`Strict` policy option; binding‑match mechanics are
integration‑supplied with an engine default). Two new questions surfaced by the naming
consolidation:

1. **Key normalization rule** — one documented rule for case sensitivity and separator
   canonicalization that survives a Windows build machine → Linux runtime host (and vice
   versa). It should be exactly the rule `TemplateResolver` already lives by; if the
   resolver's current behavior is OS‑dependent (`Path` semantics differ per OS), this
   phase must pin an explicit normalization (lean: ordinal‑case‑sensitive keys with `/`
   separators, normalized at both emit and lookup time) rather than inherit ambient OS
   behavior.
2. **Explicit replacement mechanics for precompiled keys** — v1 ships "duplicate key =
   registration error"; does it also need an explicit replace flag (mirroring
   `[ExtensionReplace]`) so a host can deliberately layer one precompiled assembly over
   another, or does that wait for a real layering use case? (Lean: wait — the error
   message can point at the future mechanism.)

One defaulting note recorded for review: `PrecompiledMismatchPolicy` defaults to
`Fallback` (recompile + warning) rather than `Strict` — safe‑by‑default for mixed‑mode
hosts; flip per deployment where compile cost must be impossible.

## External grounding

- [Native AOT deployment overview — limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)
  (Microsoft Learn) — "No runtime code generation, for example, `System.Reflection.Emit`";
  "No dynamic loading, for example, `Assembly.LoadFile`"; "`System.Linq.Expressions`
  always use their interpreted form, which is slower than runtime generated compiled
  code" — the structural facts behind the **ratified non‑goal**: AOT‑compatibility would
  demand removing exactly the machinery that keeps the runtime dynamic.
- [Introduction to AOT warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)
  and [known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities)
  (Microsoft Learn) — the IL3050‑class warning model and JIT/dynamic‑loading trimming
  incompatibilities; background for the same decision.
- [Source generators overview](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
  (Microsoft Learn) — generators do compile‑time metaprogramming by reading "the contents
  of the compilation" plus additional files; user code is introspected through the
  compilation/symbol APIs, not loaded and reflected over.
- [Roslyn source generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
  (dotnet/roslyn) — packaging rule ("place the generator in the `analyzers\dotnet\cs`
  folder") and the `GeneratePathProperty="true"` + `PrivateAssets="all"` recipe for
  packing generation‑time dependencies inside the analyzer folder.
- [Incremental generators design doc](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
  (dotnet/roslyn) — `IIncrementalGenerator` pipeline, `AdditionalTextsProvider`, and the
  caching rules the wiring above relies on (extract data out of inputs early,
  value‑equatable models, never cache symbols/compilations).
- [Andrew Lock — Creating an incremental generator](https://andrewlock.net/creating-a-source-generator-part-1-creating-an-incremental-source-generator/)
  (Dec 2021, series) — "Source generators must target netstandard 2.0"; user code is
  inspected via syntax trees and the semantic model (`ISymbol`), never runtime
  reflection.
- [C# preprocessor directives — `#line`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/preprocessor-directives#error-and-warning-information)
  (Microsoft Learn) — `#line hidden` for debugger stepping, and the span form
  `#line (l,c) - (l,c) offset "file"`: "The most common use of this extended `#line`
  directive is to remap warnings or errors that appear in a generated file to the
  original source" (illustrated with a Razor example).
- [Razor compilation in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/view-compilation),
  [`RazorCompiledItemAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.razor.hosting.razorcompileditemattribute),
  and the [ASP.NET Core 6.0 breaking‑change note](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/6.0/razor-compiler-doesnt-produce-views-assembly)
  — the model this phase now follows end‑to‑end: `.cshtml` compiles during build into the
  app assembly, compiled items are discovered at runtime through assembly‑level metadata
  attributes, and runtime compilation remains an optional dev‑time add‑on rather than the
  default path.
- Build‑time template compilation elsewhere: [templ](https://templ.guide/) (Go —
  "components are compiled into performant Go code") and
  [Askama](https://docs.rs/askama/latest/askama/) (Rust — "a type‑safe compiler for
  Jinja‑like templates" driven by a derive macro).
