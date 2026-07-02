# Phase 7 — Build‑Time Compilation Backend (AOT / T4 successor)

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> **Hard dependency: phase 1** — expressions must be Roslyn‑free (or directly emittable as
> C#). No grammar change — a second *backend* over the shared front end.

## Goal

Compile `.heddle` templates to C# **at build time** via a Roslyn source generator (with an
MSBuild‑task fallback), so that:

- **Native AOT and trimming work** — no runtime Roslyn emit (runtime code generation and
  dynamic assembly loading are
  [unsupported under AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)),
  no runtime `Expression.Compile` (`System.Linq.Expressions`
  [always runs interpreted under AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)
  — works, but slow), no reflection‑based member resolution
  ([trim‑hostile, IL3050‑class warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings));
- **startup cost disappears** — no ANTLR/expression‑tree/Roslyn work at process start;
- **templates become debuggable** — real generated C# to step through, real stack traces;
- Heddle becomes a credible **T4 successor** for build‑time text/code generation with a
  vastly better language (composition, typing, imports).

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
  - **(b) upgrade path — `ISymbol` binding for first‑class diagnostics.** Resolve member
    paths against the `Compilation` (`ITypeSymbol.GetMembers` walks mirroring the runtime
    tier order) and report native Heddle diagnostics (exact template span) before the C#
    compiler ever sees the generated code. Strictly additive polish over (a): same
    emitted code, better errors.
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
  - extension calls → generated invocations against pre‑constructed extension instances
    (no `Activator`, no registry lookup at runtime); built‑ins may later get inlined
    fast paths (`if`, `list`) — an optimization, not v1 scope;
  - definitions/imports/overrides → resolved at generation time exactly as the runtime
    compiler resolves them (document‑order layering preserved).
- **Generated API shape** (sketch): one static class per template —
  `GeneratedTemplates.HomePage.Generate(Blog model)` / `Render(Blog model, TextWriter sink)`
  (sink overload aligns with phase 8) — strongly typed against the declared `@model`. A
  template without a typed model generates an `object`/`dynamic` entry point with a
  documented AOT caveat.
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
  packaging rule); the core `Heddle` package stays runtime‑only. NuGet does **not**
  restore package dependencies for analyzer assemblies, so generation‑time dependencies —
  the parse front end and `Antlr4.Runtime.Standard` — must be packed *inside* the
  analyzer folder alongside the generator (the cookbook's `GeneratePathProperty="true"` +
  `PrivateAssets="all"` recipe); `Microsoft.CodeAnalysis` itself must never be shipped —
  the host compiler provides it. The MSBuild‑task fallback covers non‑SDK or
  generator‑hostile build setups (same emitter, different driver).

### What stays runtime‑only

`:: dynamic` member access (runtime binder), template hot‑reload/file‑watching scenarios,
and templates loaded from data at runtime. The generator rejects `:: dynamic` under AOT
intent with a clear diagnostic; hosts needing it keep the runtime path.

## Back‑compat analysis

- Purely additive — a new package and generated code path; runtime behavior untouched.
- Semantics coupling risk is *drift*, not breakage: every future language change (phases
  1–5) must land in both backends. Gate: the differential harness runs in CI from the day
  this phase ships; the runtime path is authoritative on divergence.
- The extension model is unchanged for authors: custom extensions work in generated mode as
  ordinary instantiated classes (documented requirement: parameterless ctor, no reliance on
  registry mutation at runtime).

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
  design unknown. (M)
- **v1 type‑error UX rides on `#line` fidelity**: under strategy (a) a template member
  typo is reported by the C# compiler against generated code — if the span mapping is
  off, the user sees an error inside a file they never wrote. Mitigations: span‑form
  `#line` per emitted expression; diagnostics fixtures asserting reported
  file/line/column for representative mistakes; strategy (b) as the upgrade that
  pre‑empts these errors with native Heddle diagnostics. (M)
- **`#line` debugger‑stepping quality** (distinct from error mapping above): treat as
  progressive enhancement; correctness first, stepping fidelity second. (S)

## Success criteria

1. A sample **Native AOT** console app (new `samples/AotRender`) renders the benchmark
   home‑page template with zero runtime Roslyn and zero `Expression.Compile` — verified by
   the app publishing with `PublishAot=true` + `TrimMode=full` with no trim warnings from
   Heddle, and rendering correctly.
2. **Differential harness**: byte‑identical output between generated and runtime backends
   across the full fixture corpus (`src/Heddle.Tests/TestTemplate/**`), running in CI.
3. Startup: time‑to‑first‑render for the benchmark page is near‑zero (measured; compare
   against the runtime path's first‑compile cost — expect orders of magnitude).
4. A compile error in a template fails the **build** with a diagnostic pointing at the
   `.heddle` file and line — parse errors as native Heddle diagnostics from the
   generator; member/type errors in v1 as C# errors remapped through `#line` spans (the
   file‑and‑line requirement holds either way; the `ISymbol` upgrade path later replaces
   the remapped ones with native Heddle diagnostics).
5. A custom extension authored per [custom-extensions.md](../custom-extensions.md) works
   identically under the generated backend (fixture‑verified).
6. T4‑style usage proven: a codegen sample (e.g. generating a C# enum from data) runs as
   part of build with no runtime Heddle dependency in the output assembly.

## Validation scenarios

- Fixture‑corpus differential run (every `.heddle` fixture: generate, render both paths,
  byte‑compare) — the phase's backbone test.
- AOT publish smoke test in CI (`dotnet publish -r win-x64 /p:PublishAot=true` on the
  sample; run the binary; assert output).
- Trimmed non‑AOT publish (`PublishTrimmed=true`) — same assertions.
- Composition‑heavy case: the benchmark's `layout.heddle` + `home.heddle` with `<body:body>`
  override — generated code preserves document‑order override semantics.
- Diagnostics: template with a member typo → build error with `.heddle` path/line (in v1
  a remapped C# error, e.g. CS1061, whose *reported* location must be the template span —
  fixture asserts file/line/column); template using `:: dynamic` under AOT intent →
  targeted diagnostic.
- Incremental build: touching one template regenerates only that template (generator
  incrementality test).
- Options parity: output profile (phase 2) and expression mode (phase 1) set via MSBuild
  properties affect generated code identically to `TemplateOptions` at runtime.

## Size estimate

| Work item | Size |
| --- | --- |
| Emitter (structure → C#): text/members/expressions/extensions/definitions | **L** |
| Generator host (incremental, AdditionalFiles, options, diagnostics) | M |
| Packaging (props/targets, `Heddle.Generator` NuGet, MSBuild‑task fallback) | M |
| Differential harness + CI (AOT/trim publish jobs) | M |
| Samples (AOT app, T4‑style codegen) + docs | M |

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
    image's read‑only data. Under Native AOT the bytes live in the binary's data
    section: zero startup cost, zero GC objects, shared across processes.

  This is the single best interaction between this phase and phase 8: build‑time
  compilation is the only backend that can pre‑encode at all, so it should reserve the
  piece‑level emission hook **now** (emit pieces via one helper in the emitter) even
  though the u8 tier only pays off once the byte sink exists.
  *Conditionality:* `LangVersion` ≥ 11 **and** the phase 8 sink overload; fallback is
  string constants plus the same runtime encoding the runtime backend uses. Emitting both
  forms duplicates static data — mitigations: emit per configured output profile, or emit
  both and let trimming drop the unused entry point (the AOT success criterion already
  publishes with `TrimMode=full`). *Correctness notes for the differential harness:*
  u8 literals "aren't compile‑time constants; they're runtime constants" — fine here
  (they're only ever written to a sink), but they can't be `const`, can't be default
  parameter values, and can't be interpolated. Compiler‑side UTF‑8 encoding is
  byte‑identical to runtime `Encoding.UTF8` for well‑formed text; text containing an
  unpaired surrogate is a *compile error* in a u8 literal where the runtime path would
  substitute a replacement character — the generator should pre‑validate pieces and
  report its own diagnostic rather than leak a C# error (fixture: template static text
  with a lone surrogate).

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

- **Native AOT in .NET 10 — what concretely strengthens the headline scenario.**
  Honestly: .NET 10's AOT release notes are incremental, not headline‑quantified. What's
  verifiable: (1) the NativeAOT **type preinitializer** now handles all `conv.*` and
  `neg` opcode variants, so more static initialization runs at *compile* time instead of
  at startup
  ([What's new in the .NET 10 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime#nativeaot-type-preinitializer-improvements))
  — generated templates should keep their static state preinitializer‑friendly (constants
  and u8 spans qualify trivially; avoid static ctors in emitted code). (2) The release
  announcement claims "smaller, faster ahead‑of‑time compiled apps" without published
  numbers ([Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/))
  — cite it as direction, not data; our own success criterion 3 produces the numbers that
  matter for Heddle. (3) Most relevant to the T4‑successor story: the .NET 10 SDK ships
  **native‑AOT‑published dotnet tools** and file‑based apps with AOT publish
  ([What's new in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview),
  [Andrew Lock's preview series](https://andrewlock.net/exploring-dotnet-10-preview-features-7-packaging-self-contained-and-native-aot-dotnet-tools-for-nuget/))
  — a Heddle‑powered codegen CLI can ship as a native single binary on NuGet, installed
  via `dotnet tool install` (developer‑initiated `dotnet tool exec` also works; the
  auto‑download concern rejected in phase 6 applies to *editor‑launched* servers, not a
  developer explicitly invoking a CLI). That distribution channel exists *only* because
  this phase removes runtime Roslyn; worth a sample alongside `samples/AotRender`.

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
  devirtualize, inline, and stack‑allocate through. The runtime backend's compiled
  documents dispatch through interfaces, delegates, and interpreted expression trees
  (under AOT), which these passes largely cannot see through. The honest caveat: the JIT
  gains accrue to *any* code with concrete shapes, not to generated code per se — the
  point is that this phase's output is structurally positioned to collect them, and
  under Native AOT (no profile‑guided devirtualization at run time) concrete/sealed call
  shapes matter even more.

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
  API; (2) `GetInterceptableLocation` raises the generator's minimum
  `Microsoft.CodeAnalysis` reference (4.11+), narrowing host‑compiler compatibility for a
  convenience; (3) the v1 entry points (`GeneratedTemplates.X.Generate`) are explicit,
  debuggable, and honest about what is compiled. Verdict: note as a possible
  post‑v1 migration aid; not in scope.

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

1. Generated entry‑point naming/namespace convention and collision policy for templates
   with identical file names in different folders.
2. Whether built‑in extensions get inlined fast paths in v1 or all go through instances
   (lean: instances first — correctness and the differential gate; inline later behind the
   same harness).
3. MSBuild‑task fallback: ship in v1 or only if generator‑hostile environments materialize?
   (Lean: defer; generators cover SDK‑style projects, which is the audience.)
4. When to invest in the `ISymbol` binding upgrade (b): triggered by real reports of
   confusing remapped C# errors, or bundled into the first diagnostics‑polish milestone
   after v1 ships?

## External grounding

- [Native AOT deployment overview — limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment)
  (Microsoft Learn) — "No runtime code generation, for example, `System.Reflection.Emit`";
  "No dynamic loading, for example, `Assembly.LoadFile`"; "`System.Linq.Expressions`
  always use their interpreted form, which is slower than runtime generated compiled
  code" — the three facts this phase's Goal rests on.
- [Introduction to AOT warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)
  (Microsoft Learn) — `RequiresDynamicCodeAttribute`/IL3050: reflection‑emit and other
  runtime‑codegen paths warn at publish and throw at runtime under AOT.
- [Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities)
  (Microsoft Learn) — runtime code generation via JIT (`System.Reflection.Emit`) and
  dynamic assembly loading are incompatible with trimming, which Native AOT requires.
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
- [Razor compilation in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/view-compilation)
  and the [ASP.NET Core 6.0 breaking‑change note](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/6.0/razor-compiler-doesnt-produce-views-assembly)
  — the strongest same‑ecosystem precedent: since ASP.NET Core 6.0 the Razor compiler is
  based on C# source generators; `.cshtml` compiles during build into the single app
  assembly, improving build performance and enabling hot reload.
- Build‑time template compilation elsewhere: [templ](https://templ.guide/) (Go —
  "components are compiled into performant Go code") and
  [Askama](https://docs.rs/askama/latest/askama/) (Rust — "a type‑safe compiler for
  Jinja‑like templates" driven by a derive macro).
