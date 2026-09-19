# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0]

The precompilation build tier is rebuilt on a stored compiled form, and the Roslyn-generator tier is
deleted. Additive at the language and rendering level — **no rendered byte changes on either tier** —
with declared **binary and source** breaks in the precompiled-manifest contract, the removal of
assembly auto-loading, and build-time behaviours that begin to occur because they were never wired.
**3.0 is a ratified breaking window**, scoped to binary changes and minor API changes or additions;
the running window record is in
[breaking-windows.md](docs/spec/common/breaking-windows.md#30-window--pending-tag-21-retired-without-a-release)
(there is no 2.1 release: that window was retired without one, and its items release here) and the per-item judgements that predate the
window's ratification remain in
[breaking-windows.md](docs/spec/common/breaking-windows.md#explicit-not-window-gated-rulings).

### Changed (breaking)

- **Only schema 4 manifests are accepted, and every accepted manifest was built by this release.**
  `PrecompiledSchema.MinSupportedSchemaVersion` and `MaxSupportedSchemaVersion` are both `4`, and a
  manifest below 4 is not degraded — registration **throws** `PrecompiledRegistrationException`, because a
  hand-written manifest row can claim any schema number while carrying none of the behaviour the
  number promises, and a degrade path would bless it. 2.0.x manifests (schemas 1–2) and the never-released schema 3 are
  therefore rejected outright, which also retires the question the 2.1 window left open: there is no
  version of the old Roslyn generator whose output this engine runs.
  **What to do:** rebuild with the 3.0 `Heddle.Build` package (or `heddle compile`), which emits
  schema 4. `Heddle.Build` and `Heddle` are version-locked — pair the matching versions.
  **If you do not:** registration throws before any template renders; there is no dynamic-tier
  fallback for a rejected manifest, which is that throw's purpose. No compatibility shim.
- **The Roslyn-generator tier is deleted: the `Heddle.Generator` package, its analyzer, and
  everything in `Heddle.dll` whose only caller was generated 2.x code are gone** (`PrecompiledRuntime`
  and the `Precompiled*` init-site/body/accessor/manifest helper types, the runtime-operators
  adapters, the schema-gate constants, the observe mode; the full list is the phase-4 removal record
  in the program record). Precompilation is now a build step — the `Heddle.Build` package compiles
  templates through the real engine out of process and embeds the stored compiled form — not a
  source generator. The engine keeps its `Microsoft.CodeAnalysis.CSharp` reference — it is the C#
  expression tier, trimmed out of a publish behind the `Heddle.CSharpTierEnabled` feature switch —
  and only `Microsoft.Extensions.DependencyModel` went (see the assembly-loading item below).
  **What to do:** remove the `Heddle.Generator` package reference and add `Heddle.Build`; no template
  text changes are needed. A template the build cannot precompile is left out with an `HED7031`
  warning naming it and renders through the byte-identical dynamic path. Hand-written manifests and
  generator-constructed rows have no upgrade path: the loader binds only rows the 3.0 build wrote.
  Migration details, including the per-member disposition of the deleted API, are in the
  [Upgrading from 2.x](docs/precompilation.md#upgrading-from-version-2).

- **`PrecompiledFallbackEvent.Key` is removed**, replaced by `TemplateKey` and `AssemblyName` with
  exactly one populated. The single `Key` carried two different kinds of string — a template key for
  the per-request reasons, an assembly name for the registration-time ones — with no discriminator, so
  a host had to re-derive from `Reason` which of the two it held. Removal rather than narrowing is
  deliberate: narrowing would leave a 2.0 host silently reading `null` off the channel whose entire
  purpose is that failures are not silent, while removal is a compile error at the one line that has
  to change. Events are constructed through `ForTemplate`/`ForAssembly`, which each refuse the other's
  reasons, and the reason → carrier mapping is enforced by an exhaustive classifier that throws on an
  unclassified reason — so a reason added later cannot be raised until it is mapped.

- **The engine no longer loads or scans assemblies on its own.** It used to `Assembly.Load` the entry
  assembly's entire transitive reference closure — plus every `DependencyContext` default assembly
  name, loader failures swallowed — from a static constructor, and then scan all of it for
  `[assembly: ExportExtensions]`. Because that scanned set decided extension **name ownership**, an
  assembly you never chose to load could take a name, or collide with an unrelated claimant and throw
  `TemplateOverrideException` out of a type initializer.
  The set is now what your host has already loaded into the default load context, plus what you
  register — including assemblies that load later, since the engine re-checks rather than snapshotting.
  `[ExportExtensions]` is read **per assembly, at registration**.
  **What to do:** call `HeddleTemplate.Register(assembly)` for your application assembly and for every
  extension library you use. Registration is **not transitive** — an extension library you merely
  reference is not discovered. `HeddleTemplate.Configure(assembly)` is the same call under its older
  name and keeps working; its one-shot latch is gone, so a second call now takes effect instead of
  being silently dropped.
  **If you do not:** a template calling an unregistered extension reports `Cannot find extension
  <name>` (`HED0002`); a precompiled one degrades per request with an `ExtensionBindingMismatch`
  naming it. A template that names a model type by string in an assembly your host has never touched
  no longer resolves either — register that assembly too.
  The `Microsoft.Extensions.DependencyModel` package reference is **removed** along with the walk that
  was its only consumer, so the engine's dependency surface shrinks by one.

- **`<HeddleTemplate>` per-item metadata now takes effect.** `Key`, `Name` and `Precompile` were all
  inert from a real project — the targets file overwrote each with the empty string while appearing to
  map it — so only projects that never used them were unaffected. If you set `Key`, the template's
  registration key **and its generated entry-class name** now follow it, so a call to the old
  path-derived class name must be renamed. `Name` moves nothing (see *Added*). If you set
  `Precompile="false"`, that file now really stops precompiling (it remains available to `@<<` imports)
  and renders through the dynamic path.

- **Retired 2.x MSBuild properties.** Setting `HeddleObserveEngine`, `HeddleNodeFallback` or
  `HeddleEmitUtf8Pieces` warns `HED7037` naming the property; `HeddleObserveIntermediatePath` and
  `HeddleObserveImplementationPath` are ignored silently. Output is byte-identical either way.
  **What to do:** delete the element.

- **Typed entry points read their options from `PrecompiledTemplates.DefaultOptions`.** A generated
  `Heddle.Generated.{Name}.Generate(...)` binds through `PrecompiledTemplates.BindTyped(assembly, key,
  modelType)` under `DefaultOptions` — there is no per-call options parameter — and renders the
  **output profile the build baked** into the row. A gauntlet failure at that bind **throws**
  `PrecompiledMismatchException` rather than degrading, whatever `PrecompiledMismatchPolicy` says:
  a typed entry has no dynamic twin to fall back to.
  **What to do:** set `DefaultOptions` once at startup to the shape your host renders with, and read
  `ValidateAll`'s report before serving.

- **Functions must be bodiless and build-visible to precompile.** A called function the build cannot
  bind stays a late-bound site (resolved once at first render; reported by `HED7031`); a bodied or
  chained consumer over such a call — `@if(fn(x)){{…}}` with an argument the build cannot type — is a
  class (c) refusal (`HED7014`), and that one site renders through the dynamic path.
  **What to do:** export the function with `[ExportFunctions]` on a public static container the build
  can see (a referenced assembly, or the project's own intermediate compile), or give the argument a
  type the build can see.

- **The build host runs on .NET 10, and the host is not the target.** Building requires the .NET 10
  SDK. A BCL member the build bound that is absent on the target framework is a load-time gate
  fallback (`MemberBindingMismatch`), not a build error — run `ValidateAll` on the target.

- **`MemberBindingMismatch` is a new must-surface fallback reason.** A member the build bound that
  resolves differently at load (a renamed member, a changed type, `List<A>` → `List<B>`) fails the
  gauntlet under that reason; a host's `OnFallback` handler must surface it, never swallow it.

- **Build-diagnostic ids were re-keyed onto engine ids** for every fact the engine diagnoses. The
  twin → engine-id table is in the
  [Upgrading from 2.x](docs/precompilation.md#upgrading-from-version-2) (e).

- **Removed public members** (the phase-4 removal record, listed so the diff is readable without the
  spec). Types removed whole: `Heddle.Precompiled.PrecompiledRuntime` (every member: `Bind`,
  `BindDefinition` ×3, `BindExtension`, `BindOut`, `Init`, `InitDefinition`, `InitExtension`,
  `SiteFallback`, `EvaluatePartialName`, `ResolvePartial` ×3, `WithLocalsFrame`, `MemberAccessor`,
  `NativeAccessor`, `DynamicMember`, `Prop`, `RootModel`, `CarrierValue`, `GenerateString` ×2,
  `GenerateToWriter`, `GenerateUtf8`, `WritePiece`), `PrecompiledInitSite`, `PrecompiledInitBody`,
  `PrecompiledInitFault`, `PrecompiledInitFaultScope`, `PrecompiledLateAccessor`, `PrecompiledPropSetter`,
  `PrecompiledPropEvaluator`, `PrecompiledPartialName`, `PrecompiledFunctionSite`, `PrecompiledFunctions`,
  `RuntimeOperators`, `PrecompiledCapabilities`, `PrecompiledLinePathForm`, `PrecompiledCallShape`,
  `ObserveMode`, `IHeddleTemplateManifest`. Members removed: `PrecompiledTemplateInfo` — all four public
  constructors, `Capabilities`, `LinePathForm`, `InitSites`, `IsPrecompiled` (a row's presence in `Entries`
  is the fact), and `Strategy` becomes internal (`EntryPointType`, `RefusalSites` stay);
  `PrecompiledSchema` — `AmbientModelTypeSchemaVersion`, `DynamicMemberRoutingSchemaVersion`,
  `LateBoundFunctionMaxArity`, `LateBoundFunctionSchemaVersion`, `LinePathFormSchemaVersion`,
  `PerCarrierLocalsSchemaVersion`, `PropLayoutFingerprintSchemaVersion`, `RegisteredNameSchemaVersion`,
  `EmitsDynamicMemberRouting`, `EmitsLateBoundFunctions`, `EmitsPerCarrierLocals`; `HeddleBuildOptions` —
  `BuildPropertyPrefix`, `DefaultEmitUtf8Pieces`, `DefaultNodeFallback`, `DefaultObserveMode`,
  `DefaultObserveImplementationPath`, `DefaultObserveIntermediatePath`, `EmitUtf8PiecesProperty`,
  `NodeFallbackProperty`, `ObserveEngineProperty`, `ObserveImplementationPathProperty`,
  `ObserveIntermediatePathProperty`. `PublicApiSurfaceTests` pins the surface that remains.

### Added

- **Native expressions precompile from the engine's own bound tree.** Where 2.x re-derived each
  expression's C# from its syntax and degraded whatever it could not prove equivalent — shifts, ternaries
  and coalesces over mixed numeric kinds, mixed-type equality, string concatenation, same-enum and
  reference operands, `::`-rooted paths, indexers, `params` exports — the build host now compiles the
  expression through the real engine and prints the tree it bound: every promotion is already an explicit
  conversion in that tree, the chosen function overload and user-defined operator are the ones recorded
  there, and a null-safe hop is the engine's own conditional. The printed site keeps the tree's
  evaluation order and short-circuiting, compares references where the engine does, and never checks
  arithmetic for overflow, whatever the consumer's project settings say. An expression made only of
  constants is stored as its folded value, and a constant sub-expression is printed as the value the
  engine computed for it. What a printed site cannot name is declined per site and
  listed in `HED7031` ([Sites rebuilt at load](docs/precompilation.md#sites-rebuilt-at-load)); rendered
  bytes are the same either way — the two tiers are parity-checked — so this only moves templates off the
  slower load-time path.
- **`HED1018` — constant division by zero is a compile error, on both tiers.** `@(1/0)` used to compile
  and throw `DivideByZeroException` at render; rendering it can only ever throw, so both the engine and
  the build now refuse it at compile time under one id. Scoped exactly where C# draws `CS0020`'s
  lines: integral and `decimal` only, the divisor folded rather than spelled (`1/(1-1)` counts),
  floating point stays legal (`@(1.0/0)` renders `∞`), and a runtime divisor that happens to be zero
  still throws at render.
- **The engine's refusals fire at build: `HED1003`–`HED1011` forwarded as build errors.** Where the
  build can *prove* the engine would refuse a construct on every input — method-call syntax,
  declared-`dynamic` roots, logical operators over non-`bool`, ununifiable ternary/coalesce arms,
  undefined binary/unary operator pairings, a known indexer target with no matching indexer, a
  non-`bool` condition — it raises the engine's own id with the engine's own sentence at build time
  instead of degrading silently and letting the consumer's runtime say so. Where it cannot prove the
  refusal it still degrades silently: never an error on a guess.
- **`ModelType` item metadata** types a template from the project file:
  `<HeddleTemplate Update="Templates/invoice.heddle" ModelType="My.App.InvoiceModel" />` feeds the same
  pipeline the in-file `@model` directive feeds (resolution, `@using` imports, the `HED7007`
  gating), so a template can be typed without a directive in its text. When both channels are present
  they must agree: different resolved types are **`HED7032`**, a build error, because the runtime reads
  only the directive and the build refuses to type the same template differently on the two tiers.
- **A zero-allocation clean-span path in `HtmlEncodedRenderer`.** The span path used to materialize
  every write twice (span → string → encoded string) before the sink saw a byte; a span with nothing to
  encode now writes straight through to a span-accepting sink with zero allocation, and a dirty span
  pays only from the first character that needs encoding. Byte identity holds on both encoder paths; a
  fortunes-style encoded loop drops 29 % of its per-render bytes.
- **`Name` item metadata** as an **additional** name for a template — not a rename:
  `<HeddleTemplate Update="t/report.heddle" Name="BuildReport" />` leaves the key
  `t/report.heddle` and the class `Heddle.Generated.T_Report` exactly as they were, and makes
  `BuildReport` resolve **as well as** `t/report.heddle`. Nothing that resolved before stops
  resolving. It pairs naturally with `Precompile="false"`: an import-only partial under a friendly name.
  Because a name is not a registration key, it takes no part in the duplicate (`HED7002`) or
  case-only-twin (`HED7003`) checks and does not suppress the out-of-root warning (`HED7018`) — the
  path-derived key is still there and still unasked-for. Setting `Key` *and* `Name` is two names for one
  template, not a conflict. A value the normalizer refuses, or a name another template already answers
  to, is `HED7004` against the name; the key is unaffected.
- **The name works at run time too, not only for `@<<` imports.** The manifest row carries it
  (`PrecompiledTemplateInfo.RegisteredName`) and `PrecompiledTemplates.TryGet`/`TryResolve` — and so every
  resolver arm — find the same template by either spelling. Where a spelling names one template's key and
  another's registered name, **the key wins**, whichever assembly registered first: a name is an addition
  and never displaces a spelling that already resolved. Lookup is ordinal, as key lookup is.
- **`HED7028`** (warning): an `@<<` import names a template by its registration key while that template
  also carries a `Name`. Both spellings resolve — this recommends the name-first spelling for a named
  template. It cannot fire for a project that sets no `Name`.
- **`HED7104`** (`PrecompiledFallbackReason.RegisteredNameUnavailable`, via `OnFallback`): a registered
  `Name` could not become a lookup spelling — either because another *registered* template already answers
  to it, as its key or as its own name, or because the shared key rule refuses the spelling outright (a
  `..` segment, a trailing separator, whitespace). Never a throw — the template stays reachable by its key, and only the
  addition is lost. This collision is only detectable at registration, since the build tier cannot read a
  referenced assembly's manifest rows; within one build the same fault is `HED7004`.
- **`HeddleTemplate.Register(Assembly)`** — the explicit registration seam described under *Changed*.
  Idempotent per assembly and repeatable, so a host chooses its own order of precedence. Throws
  `ArgumentNullException` on null and `TemplateOverrideException` when two unrelated types claim one
  extension name — at the registering call, rather than out of a type initializer.
- **`PrecompiledTemplates.ValidateAll(TemplateOptions)`** and `PrecompiledValidationReport` — one
  post-configuration pass that runs the gauntlet over every registered entry and collects **all**
  failures, so a binding that drifted between build and deployment fails your startup once instead of
  degrading on every request. `PassedForValidatedOptions` is the green property; the report names the
  options shape it validated, because four of the gauntlet's inputs are per-request.
- **`HED7037`** (warning): setting a retired 2.x MSBuild property (`HeddleObserveEngine`,
  `HeddleNodeFallback`, `HeddleEmitUtf8Pieces`) warns naming the property. The observe-path
  properties retire silently. Output is byte-identical either way — delete the element.
- **Retired build ids stay claimed.** Every `HED70xx` id whose fact no longer exists is retired in
  place: the constant, the catalog row and the published mention stay, so the id is never reused.
  `HED7011` (import not included) joins them: the host reads an import outside the item set from disk,
  mirroring the engine's `ImportMap`, so it never fires.
  See the [Upgrading from 2.x](docs/precompilation.md#upgrading-from-version-2)
  for the twin → engine-id re-keying.
- **`Precompile="false"` items are validated and advised.** An opted-out item's `Key`/`Name` now raise the
  same `HED7004` faults an included item's would, instead of failing silently and surfacing as `HED7011`
  at whichever file imported it; and its own imports can draw the `HED7028` advisory. It still contributes
  no entry point and no manifest entry. Its *template* errors remain unreported — the file is excluded from
  this build by request, and they surface through any precompiled template that imports it.

### Fixed

- **A member a derived model type hides can no longer be read through its base class (sandbox).**
  Member resolution walked the model type and then its base classes looking for the first *accessible*
  property of the requested name, so it stepped over a `[Hidden]` override — or a `[Hidden]`, non-public
  or `static` `new` property — and bound the base class's visible declaration. For an override that
  getter dispatches virtually, so `@(Secret)` rendered the very value the derived type had hidden. The
  **most-derived declaration of a name now decides**: if it is not a visible property (a field or method
  of that name counts too), the name is the ordinary positioned `HED0001`, on the member tier, the
  native-expression tier, indexers, the build tier and editor completion alike.
  **What to do:** nothing, unless a template relied on reaching a base member *through* a derived type
  that re-declares the name inaccessibly — such a template now fails to compile with `HED0001`; expose
  the value under a name the derived type does not hide. This is a deliberate tightening, recorded in
  the [3.0 window](docs/spec/common/breaking-windows.md#the-v3-window-items).
- **A prop named like a `new`-shadowed model member no longer throws.** The prop-shadow check
  (`HED5011`) asked reflection for the property by name, which throws `AmbiguousMatchException` when a
  derived type re-declares the name with another type; it now uses the same member walk the compiler
  binds with, and warns only about a member a template could actually reach.
- **A bodiless `@list(...)` no longer throws when its result is processed rather than rendered.** A
  `@list` with no body of its own — inside a definition's caller content (`@box(){{@list(Items)}}`), or to
  the right of another call in a chain (`@len(Name):list(Items){{…}}`, where the body belongs to the
  leftmost call) — threw `NullReferenceException` for any counted collection; it now contributes nothing,
  exactly as it does when rendered directly.
- **A model the template cannot accept is refused as a Heddle fault.** The top-level render now checks
  the model against the compiled model type once per render, in every build configuration, and throws
  `TemplateProcessingException` on a mismatch; the wrong-typed value used to reach the compiled
  accessor's cast and escape as a raw `InvalidCastException`, which is not the shape any other render
  fault has. A `null` model stays legal, the recursive path is untouched, and the precompiled adapter —
  which has no compile-time model type to check against — skips the check as it always has.
  `TemplateOptions.ValidateModelType` is left in place but no longer read; removing it would be a break.
- **The build tier no longer precompiles a body the engine refuses to render.** A body containing a
  branch terminal with no opener (a bare `@else` continuation) precompiled while the engine rejects it —
  the shared branch scan's error arm was never wired to the emitter. The emitter now declines the body
  and the template falls back to the dynamic tier.
- **Lexer errors reach the compile error list.** A character the lexer could not tokenize went to
  ANTLR's console listener — a template engine writing to its host's console — and skip-recovery then
  compiled the template as if the character were never typed (`4 +` compiled as `4` with zero
  diagnostics). It is now a positioned syntax error on the compile result.
- **`&`/`|`/`^` over a lifted same-enum pair no longer throws at render.** The engine computed the
  result type from the left operand alone, so `Color & Color?` built a conversion that threw
  `InvalidOperationException` the moment the nullable side was null; the result now lifts to the
  nullable enum and the null propagates — exactly C#'s answer.
- **Three build-tier type-binding defects.** A type nested in a generic (`Outer<int>.Inner`) degraded
  because the build compared per-type arity against a cumulative spelling; a dotted spelling
  reachable through two `@using` imports was decided by declaration order on both tiers instead of
  being ambiguous (now the engine's ambiguity error at render, matching `CS0104`);
  and an assembly-qualified model spelling stating a `Version` **ahead** of the referenced assembly
  bound at build and threw at runtime — it now degrades, while exact and behind versions bind.
- **`heddle-lsp --version` and the LSP `initialize` response reported `1.0.0`** for the whole 2.0 line.
  The value is now read off the assembly rather than hand-maintained.
- **`HED7004`'s message** names the offending metadata and the reason, covering an unusable or
  already-taken `Name` as well as a malformed `Key`.

### Build and packaging

- **Generated source is safe against the names templates bring.** Every type the build writes into the
  generated namespace is `global::`-qualified and an entry class's private members start with a
  lowercase letter, so a template called `system.heddle`, `stream.heddle`, `heddle.heddle` or
  `bound.heddle` compiles; a key that sanitizes to `Generate` — the one member callers name — is the
  positioned `HED7010` build error instead of `CS0542` from generated code. A receiver's null test
  prints as a reference comparison (`(object)v0 == null`), as the engine performs it.
  **Golden changed:** `samples/codegen-t4-successor/golden/generated-source.cs.txt`, which captures the
  generated source verbatim, was regenerated through the sample's own capture path; every changed line
  is one of those three spellings (qualification, the member renames, the null test) and no rendered
  output of any sample changed.
- **The `Heddle.Build` package replaces `Heddle.Generator`.** Once the `v3.0.0` tag is pushed and
  the packages publish, the last 2.x `Heddle.Generator` is to be deprecated on NuGet naming
  `Heddle.Build` as its replacement — a post-tag step, not something this release's build does. The
  new package carries no Roslyn and no analyzer: an
  MSBuild task plus targets drive the out-of-process `heddle compile` host, which compiles templates
  through the real engine and embeds the stored compiled form. The multi-Roslyn build variants and the
  .NET Framework legs are gone with the generator they served.
- The dedicated `net6.0` target is retired: the libraries now target `netstandard2.0;net8.0;net10.0`.
  A .NET 6 host still runs Heddle — it binds the `netstandard2.0` build, as .NET 7 always has — losing
  only the modern-BCL extras that build carries anyway (`Range.FromSystemRange`, the span/UTF-8
  formatting fast paths).
- The release line is stated once, as `<VersionPrefix>` in `Directory.Build.props`, replacing nine
  per-project `<Version>` elements; a version-consistency test holds the statements that cannot live
  there (the npm manifests, the VS Code extension's pinned tool version, the LSP workflow's
  `--version`, the prose release-line sentences, the CHANGELOG section) in step with it.
- `Heddle.Demo.Models` and `Heddle.Demo.Wasm` are strong-named, so the build is `CS8002`-clean. The one
  remaining unsigned reference is third-party (Scriban) and is suppressed at the reference rather than
  by a blanket `NoWarn`.

## [2.0.0] - 2026-07-19

The **2.0** release is a single breaking window: almost everything below is additive and the dynamic
engine's rendered bytes are unchanged, except for the items under **Changed (breaking)** and
**Removed**.

### Added

- **Native expression tier** — a sandbox-safe, Roslyn-free expression language in call parens
  (`@if(Count > 0)`, `@(Price * Quantity)`, `@(Name ?? "anon")`, `@(cond ? a : b)`). Follows the C#
  precedence table and compiles to `System.Linq.Expressions`; capability is limited to
  properties/indexers/operators/literals plus host-whitelisted functions (`FunctionRegistry`,
  `[ExportFunctions]`). New `TemplateOptions.ExpressionMode` (`MemberPathsOnly` / `Native` (default) /
  `FullCSharp`); the inner-`@` form stays full C#.
- **Safe-by-default HTML output** — `OutputProfile.Html` HTML-encodes the unnamed `@(...)` carrier;
  `@raw(...)` is the explicit opt-out and `@profile(html|text)` flips mid-document. The encoder is a
  pluggable `TemplateOptions.Encoder` (`System.Text.Encodings.Web.TextEncoder`) — opt-in, with the
  default (`null`) preserving the existing `WebUtility.HtmlEncode` bytes.
- **Context-encoding extensions** `@attr` / `@js` / `@url` — encode a value for the HTML-attribute,
  JavaScript-string-literal, and URL-component contexts respectively.
- **Declarative branching** — `@elif` / `@else` (and `@ifnot`) as ordinary extensions that coordinate
  with a preceding `@if` through a published local context. Public `Heddle.Attributes.BranchRole`
  (`Opener`/`Continuation`/`Terminal`) + `[BranchRoleAttribute]` drive set classification; the
  `Scope.Publish`/`TryRead` channel, `BranchState`, and `[ScopeChannel]` let custom extensions join a
  set with the same semantics as the built-ins. Text between branch blocks is stripped with a warning.
  Optional drift diagnostics (`HED3005` runtime, `HED7016` generator) flag a continuation/terminal
  missing `[ScopeChannel]`.
- **Loop & directive ergonomics** — `@for(5)` / `@for(Count)` / `@for(range(...))` and `@list` loop
  sugar (no hand-written C# loop models); `TrimDirectiveLines` to swallow whole-line directives'
  trailing newline; a double-render warning when a default-output definition is also called by name.
- **Named props & slots** — typed named definition parameters with defaults
  (`@card(Article, style: "wide", compact: true)`), prop reads in bodies, and parameterized `@out()`
  slots, with abstract-definition composition and inheritance narrowing. Missing/unknown/mistyped
  props are positioned compile errors at the call site.
- **Streaming & UTF-8 render surface** — `Generate(data, TextWriter)` and
  `Generate(data, IBufferWriter<byte>)` sink overloads with a two-tier, surrogate-safe UTF-8 transcode
  path (O(1) allocation regardless of output size). The string path stays bit-identical. New public
  renderer surface (`IScopeRenderer` / `ISpanScopeRenderer` / `IUtf8ScopeRenderer` and implementations).
- **Render budgets** — `TemplateOptions.RenderBudget` (`MaxOutputChars` / `MaxRenderOps` /
  `MaxRenderTime`) for running untrusted templates; a breach throws
  `Heddle.Exceptions.TemplateRenderBudgetException` carrying `Kind`/`Limit`/`Observed`. Default is
  unlimited with zero render-path cost, enforced at the renderer seam so counts are uniform across the
  string/`TextWriter`/`IBufferWriter` sinks and identical on the dynamic and precompiled backends.
- **Build-time precompilation** — `Heddle.Generator`, a Roslyn incremental source generator that
  compiles `.heddle` templates to `IProcessStrategy` classes at build time, byte-for-byte identical to
  the dynamic engine and with no runtime Heddle dependency in the generated output. Covers the full
  construct set; template errors surface as positioned build diagnostics (`HED70xx`) and anything not
  yet emittable degrades safely to the dynamic path. Engine and generator are version-locked;
  `PrecompiledMismatchPolicy` governs a mismatch; a `Heddle.CSharpTierEnabled` trim switch keeps
  Roslyn out of a trimmed publish.
- **Stable diagnostic IDs** — `Heddle.Data.HeddleDiagnosticIds` (`HEDxxxx`), surfaced by both the
  runtime compiler and the generator.
- **Editor tooling & LSP** — `Heddle.LanguageServices` (flag-gated, null-cost when off) and a
  hand-rolled LSP 3.17 server (`Heddle.LanguageServer`): typed member completion inside `@(...)`, hover
  types, diagnostics at template positions, and go-to-definition across `@<<` imports (imported-file
  diagnostics re-anchored to the import site). Ships with a VS Code extension and a v2 Ace web grammar.
- **`heddle` CLI** — a T4-successor render tool (`heddle render <template> --model-json <file>`).
- **Docs, demo & samples** — an in-browser demo (Ace + typed WASM language worker + sandboxed render
  pane) on the published docs site, and a gallery of ten runnable, golden-asserted samples that double
  as end-to-end CI coverage.

### Changed (breaking, vs 1.x)

- `TemplateOptions.OutputProfile` now defaults to `OutputProfile.Html`: the unnamed `@(...)` output
  HTML-encodes by default. Opt out per output with `@raw`, or restore the 1.x behavior per template
  with `OutputProfile.Text`.
- `TemplateOptions.TrimDirectiveLines` now defaults to `true`: whole-line directives swallow their
  trailing newline. Set `TrimDirectiveLines = false` to restore the 1.x behavior.

### Deprecated

- `TemplateOptions.AllowCSharp` is obsolete — use `ExpressionMode` instead (`AllowCSharp == true` is
  equivalent to `ExpressionMode.FullCSharp`; `false` selects `Native`, or leaves `MemberPathsOnly`
  untouched). Reads and writes keep working as a compatibility bridge.

### Removed

- The loop-model class **`Heddle.Models.ForModel`** has been deleted, replaced by the immutable
  **`Heddle.Models.Range`** value type (`public readonly struct`: `Start` inclusive, `Last`
  exclusive, `Step`; value equality; a readable `ToString()`). The `range(...)` built-in, its
  precompiled twin `PrecompiledFunctions.Range`, and `@for` now produce/consume
  `Heddle.Models.Range`; the `int` fast path (`@for(5)` / `@for(Count)`) and every loop's rendered
  bytes are unchanged. **Migration is mechanical:**
  - *Constructions* — `Range`'s properties are get-only, so a `ForModel` object initializer becomes
    a constructor call: `new ForModel { Start = s, Last = l, Step = st }` → `range(s, l, st)`
    (native tier) or `new Heddle.Models.Range(s, l, st)` (C# tier); supply `0` for an omitted
    `Start` and `1` for an omitted `Step` — e.g. `new ForModel { Last = l }` →
    `new Heddle.Models.Range(0, l)` (or just `@for(l)`).
  - *Member reads* — change the declared type to `Heddle.Models.Range` (fully qualified: bare
    `Range` is ambiguous with `System.Range` under `using System;`). `Start`/`Step` are now
    non-nullable `int`, so strip every nullable operation on them (`?? 0`/`?? 1` → the bare read;
    drop `.HasValue`; `.Value`/`.GetValueOrDefault()` → the bare read) — leaving one is a compile
    error. `Last` was already `int`.
  - *Interop* — a from-start `System.Range` maps via `Heddle.Models.Range.FromSystemRange(r)`
    (net6.0+ only; step 1; a from-end `^` index throws `ArgumentException`).
  - *Rendered output (no action)* — stringifying a range (standalone `@(range(...))`,
    `str`/`format`, or C#-tier interpolation) now renders the readable call form
    `range(2, 10, 2)` instead of the useless type name `Heddle.Models.ForModel`; this is the
    change's only rendered-byte effect.
- The legacy **`@import()`** include has been removed. Any `@import` call site now fails to compile
  with a single positioned **`HED4003`** error at the call, naming both replacements: `@<<{{ path }}`
  to share definitions and layouts across files, or `@partial(){{ name }}` to embed another template's
  rendered output inline. The `import` name is retained only as a registered no-op tombstone, so the
  diagnostic is a targeted migration signpost rather than a generic "unknown extension" error.
  `@<<{{ path }}` and `@partial()` are unaffected. See docs/language-reference.md#imports--.

### Fixed

- A `@<<` composition import nested below the top level no longer crashes the compiler; it reports a
  positioned `HED4004` and skips the import.
- A default-output definition's `@out()` no longer silently drops a chained value, and the built-in
  `@out()` no longer double-renders its static default body.
- Cross-file override / definition-body position corruption caused by inner comments.
- Quick-start examples no longer crash on anonymous-type dynamic models.

### Compatibility

- Precompiled projects must be rebuilt with the matching `Heddle.Generator` when upgrading the engine:
  a manifest outside the engine's schema window, or built by an incompatible generator version, is
  rejected at registration and the assembly's templates fall back (or throw under
  `PrecompiledMismatchPolicy.Strict`). `Heddle.Generator` and `Heddle` are version-locked — pair the
  matching versions.

  **Corrected 2026-07-26.** This entry originally said *"1.x manifests are rejected by the
  engine-version gate"*. No 1.x manifest has ever existed: pre-compilation shipped **in 2.0.0**, so
  there is nothing from 1.x for the gate to reject and it has never fired for one. The rebuild
  requirement is real and stated above; the 1.x rejection path was not.

## [1.0.0]

Initial public release.

[3.0.0]: https://github.com/multiarc/Heddle/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/multiarc/Heddle/compare/v1.0.1...v2.0.0
[1.0.0]: https://github.com/multiarc/Heddle/releases/tag/v1.0.0
