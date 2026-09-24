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

- **Only schema 3 manifests are accepted, and every accepted manifest was built by this release.**
  `PrecompiledSchema.MinSupportedSchemaVersion` and `MaxSupportedSchemaVersion` are both `3`, and a
  manifest outside that point is not degraded — registration **throws**
  `PrecompiledRegistrationException`, because a hand-written manifest row can claim any schema number
  while carrying none of the behaviour the number promises, and a degrade path would bless it. The 2.x
  manifest shapes (schemas 1–2) are therefore rejected outright: no version of the old Roslyn
  generator's output runs on this engine.
  **What to do:** rebuild with the 3.0 `Heddle.Build` package (or `heddle compile`), which emits
  schema 3. `Heddle.Build` and `Heddle` are version-locked — pair the matching versions.
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

- **The stored compiled form's own types are internal.** The 42 types under
  `Heddle.Precompiled.CompiledForm` that describe the binary artifact — the row model
  (`CompiledArtifact` and every `Compiled*` row, kind and enum), the type-identity table
  (`CompiledTypeRef`, `NamedTypeRef`, `GenericTypeRef`, `ArrayTypeRef`, `DynamicTypeRef`,
  `TypeIdentityTable`) and the codec (`CompiledFormReader`, `CompiledFormWriter`) — are no longer
  exported. They are a serialization format's shape, not a seam: nothing outside this assembly needs
  to construct or read one, the build host reaches them through the existing `InternalsVisibleTo`
  grant, and `Heddle.Build` does not reference `Heddle.dll` at all. What stays public is the contract
  a host and a generated artifact class actually name: `IHeddleCompiledArtifact`,
  `IPrecompiledSiteTable`, `HeddleCompiledTemplatesAttribute`, `PrecompiledTemplates` and its
  entries, events, fingerprints and exceptions.
  **What to do:** nothing, unless you decoded an artifact yourself. **If you did:** there is no
  supported replacement — the artifact is an implementation detail of the pair of versions that wrote
  and read it, and `heddle compile` is the supported way to produce one.

- **`TemplateOptions.ValidateModelType` is removed.** The model-type check it once opted into became
  unconditional (see the render-fault item under *Fixed*), leaving a settable property nothing read.
  That is worse than no property at all: a host that set it to `false` to opt **out** of the check was
  told nothing and got the check anyway. Removal turns that silent no-op into a compile error at the
  one line that has to change.
  **What to do:** delete the assignment. There is no replacement and nothing to re-enable — every
  top-level `Generate`/`Render` validates the model against the template's compiled model type and
  throws `TemplateProcessingException` on a mismatch, in every build configuration, whatever the
  property used to say. A `null` model stays legal, the recursive path is untouched, and a
  precompiled-adapter template without a compile-time model type still skips the check.
  **If you do not:** the host does not compile. Nothing about rendering changes either way, because
  the property had stopped selecting anything.

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

- **The build no longer embeds a stale artifact after a template is deleted or renamed, or after a
  library it imports but does not declare is edited.** The precompile declared as inputs only the
  templates that were *still* in the item set, so MSBuild's newest-input-against-oldest-output check
  had nothing to compare when an item left it: deleting a template from a glob, or renaming one while
  its timestamp stayed put, skipped the compile with exit 0 and shipped the departed template's row
  and entry point. The ordered item list and its metadata now join the options fingerprint file, which
  is already an input and already only rewritten when it differs — so an item removed, renamed,
  re-keyed or opted out recompiles, and an unchanged rebuild still skips. Separately, a file an `@<<`
  import names that no item declares is read off disk and compiled into the importing template, and
  nothing recorded that: editing it skipped the compile, and forcing the compile to run wrote nothing,
  because the host's stamp covered the importer's content and not the import's. The host now records
  every file it read through the import disk fallback in `obj/…/heddle/disk-imports.txt`, folds their
  content into the stamp, and the targets read the list back as compile inputs — and as editor
  up-to-date-check and watch inputs. Only `Clean`/`Rebuild` used to recover.
- **`HeddleTemplateRoot` is a compile input.** The root decides every key the artifact records, and
  so every generated class name, but the stamp covered no part of it while the item rows it hashes
  name absolute paths — so changing only the root left the digest identical: the targets ran the
  compile and the host threw the work away, and an incremental build then disagreed with a clean one
  over byte-identical sources. It is in the digest now.
- **A library saved while the build is reading it is no longer certified as something it is not.**
  The stamp is computed after the compile, and it re-read each disk-served import there — so a
  library saved mid-compile produced an artifact built partly from each version while the stamp
  described it as the final one, and neither the host nor MSBuild ever corrected it. The hash is now
  taken from the text the reader handed the parse; when a file answers twice with different text the
  compile writes no stamp at all, so its outputs are incomplete and the next build compiles again.
- **An input the build cannot read no longer looks unchanged for ever.** The stamp hashed an
  unreadable file to the fixed string `<unreadable>`, so a file that stayed unreadable kept the digest
  identical — the mistake the intermediate model digest documents avoiding and salts against. Both
  halves agree now: an unreadable input salts the digest so the compile runs, while a file that is
  merely *absent* keeps a stable token and invalidates the moment it appears. In the same pass, the
  opted-out (`Precompile="false"`) items stopped being read and hashed a second time in response-file
  order — every item is one ordinal-sorted row carrying the content hash taken at intake, so neither
  item nor response-file order reaches the digest, which is what the artifact's own ordering already
  promised. Every site in the stamp that hashes a file now classifies through one rule, so a
  *missing* reference image no longer salts where a missing import library does not.
- **The language server no longer loads a second engine, and offers members at the top level of a
  document.** A `Heddle.dll` beside the workspace's model assemblies was loaded into the models' own
  context, so the workspace's `[Hidden]` and export attributes belonged to an engine the server did not
  recognize: hidden members were offered by completion. Engine references now resolve to the server's
  engine (a version difference is logged once), and `[Hidden]` is matched by name as a second line of
  defence. Separately, an expression written outside every body was offered functions but none of the
  `@model` type's members; the document is now a scope like any body. Lines the server logged while it
  was still being constructed were dropped; they are now delivered when the host attaches its log.
- **The language server survives a workspace it cannot fully read, and rescans exports when
  `assemblies` changes.** A workspace built against a newer engine — an exported function whose
  signature names a type the server's engine lacks, an export attribute constructor it lacks, a model
  property typed or attributed with one — threw out of `initialize`, `didChangeConfiguration` or
  completion and left the server unusable. Each such export or member is now skipped and named in the
  log; a member whose attributes cannot be listed stays hidden unless its metadata proves none of them
  is `[Hidden]`. A consumer assembly whose name merely starts with `Heddle` is no longer dropped whole
  when one of its references is missing, and an engine assembly listed in `assemblies` is skipped
  with a log line instead of becoming a second engine that swallows the workspace's exports. The export scan ran once per server process, so exports of a
  workspace configured after the first were never offered; exports are now read from the loaded model
  assemblies on every workspace load, and the previous workspace's are withdrawn. A failed background
  analysis is logged and the document's diagnostics cleared instead of going stale in silence.
- **Completion no longer loses the document's members after a literal `{{`,** in a string, a comment, a
  raw block, plain text or after `@@`, and a body that is still being typed is offered its element's
  members: body boundaries come from the parser, and open bodies are closed from the lexer's own state
  before the completion compile.
- **A page over a definitions library no longer costs the calls times the square of the definitions to
  parse, in time or in allocation.** Every output chain keeps an isolated view of the definitions visible
  where it was written, and that view was built eagerly: a copy of every definition's body, each with its
  own copy of every definition before it, for every chain — and a copy of every chain written so far. The
  view now shares its source's definition history up to the point it was taken, and a definition's body,
  the chains and the raw items are copied when they are first read, which for most chains is never. What
  is read late is read as it stood when the view was taken: the definition table, each definition's
  position, body and whole base chain (an in-place override of a base is not seen through an earlier
  call, however the derived definition is overridden afterwards), and each chain's position (an import
  moving its chains into the importer, and a compile moving positions, are not seen either). Diagnostics,
  rendered output and the stored compiled form are unchanged across the test corpus. Reading a compiled
  template's parse tree from several threads at once is safe, as it was.
  *Behaviour note for hosts that edit a parse tree by hand:* a view is no longer a finished copy at the
  moment it is taken. Entries a host **removes from or inserts into** a context's `OutputChains`,
  `DefaultChains`, `RawOutputItems`, `SkippedTokens` or `DefinitionsBlock.Positions` lists — and writes
  made straight into a `DefinitionsBlock.Definitions` table *after* the view was taken from a table
  already handed out — are seen through a view of that context that has not been read yet; entries
  appended to a definition body's `SkippedTokens` or `Positions` are too. Such a view takes the first *n*
  entries of the list as it stands when it is first read, where *n* is what the list held when the view
  was taken — so a host that removes entries leaves the view with what is left of its prefix, not with
  the entries it no longer has. Appended chains and raw items, a definition's `Position` or `Context`
  being set, and everything the engine itself does, are not.
  Measured on one machine (Release, net10.0, x64; bytes allocated on the compiling thread by the second
  compile of the same document in the process — the first compile in a process adds 25–40 MB of parser
  and JIT warm-up whatever the document), a page of N definitions and 4N calls: 50 definitions allocated
  547 MB in 0.2 s, 100 allocated 6.6 GB in 1.5 s and 200 allocated 91 GB in 16 s; they now allocate
  5.9 MB, 11.9 MB and 24.3 MB — linear — and 200 definitions with 800 calls compile in about 0.07 s.
  `bench-cold` carries the definitions × calls rows, capped at 50 definitions. Separately, a member path
  read many times in one compile (`Title` in every row) is compiled to a delegate once per compile rather
  than once per site.
- **The editor's parse tries the fast prediction mode first, as a compile always has.** A document that
  parses without an error — most of a large document's life — is no longer parsed in the exact
  ambiguity-detecting mode; one with an error is tokenized and parsed again in that mode, so the editor's
  diagnostics, and their order, are those of the single exact parse it ran before. One completion over a
  23 KB document went from 71 MB allocated to 23 MB (about 55 ms).
- **A syntax error in an `@<<` import no longer removes the importing document's diagnostics.** A document
  with a syntax error is parsed a second time, and the second parse began by clearing the error list —
  which an import shares with its importer, so everything reported before the import was lost, at run
  time as well as in the editor. Only the first attempt's own errors are dropped now.
- **Completion, hover, go-to-definition and semantic tokens answer with nothing instead of failing** when
  the request throws: an unbalanced `}}` anywhere in the document made every completion request fail,
  because tokenizing the buffer for its open bodies threw out of the request. That case is handled, and
  any other fault in a request is logged once and answered empty.
- **A namespace, type or member named like a C# keyword** (`Shop.event.class`, a property `@event`) **no
  longer breaks generated source or the C# tier's `using` directives**; the build's printers and the
  engine share one identifier escaping, which the generated file's own `using` lines and its
  `HeddleGeneratedNamespace` go through as well.
- **The build host shares a loaded image by identity, not by name,** so a process that compiles twice
  is not handed the first of two images that share a name. The one-shot host still loads images from
  their paths — consumer code that runs during the build finds its satellite resources and native
  libraries as it would at run time — while a caller that stays alive gets them loaded from bytes, so
  nothing it compiled over stays locked, with the same two lookups answered from the image's directory
  for as long as the process lives, a later invocation over the same image included.
- **Embedded C# over a model the project itself declares precompiles, and the build host never fails
  in silence.** Under `FullCSharp`, every template whose model came from an image the build host loaded
  for itself — which is what a same-project model is — failed the build with nothing but *"heddle
  compile exited with code 1 without reporting a diagnostic"*. Two causes. The host loaded its images
  into a collectible context, which the C# tier's emitted assembly may not reference, so the reference
  was answered by loading the image a second time and the emitted method met a different copy of the
  model type. And the fault that raised is kept by the engine on the compile result, while the host
  forwarded only the compile context's list. The host's image context is no longer collectible (it is
  a one-shot process), a model reference resolves to the image already loaded, and any failure the
  engine reports on the result is forwarded as `HED7020` with its cause.
- **Embedded C# over a nested model type compiles.** The C# tier declared its generated method's
  parameters by simple type name and relied on an imported namespace, so a model that is a nested type
  (`Shop.Outer.Inner`), a generic of one, or a generic argument from a namespace nothing imported,
  failed with `CS0246`. The model, chained and root types are now spelled in full.
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
  `TemplateOptions.ValidateModelType`, which used to opt into the check, is removed — see *Changed
  (breaking)*.
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
- **An artifact recording an unreadable option value is refused instead of silently losing HTML
  encoding.** A row's `OutputProfile` and `ExpressionMode` were read with `Enum.TryParse`, which
  writes `default(TEnum)` into its output when it fails — so a value that was present but named no
  member did not leave the seeded default in place, it became member zero. For `OutputProfile` that
  is `Text`, which encodes nothing, and every unnamed `@(...)` in that template stopped being
  HTML-encoded. Such a value is now refused through the gauntlet as
  `PrecompiledFallbackReason.OptionsMismatch`, naming the value and the members it could have been;
  the entry falls back to the dynamic tier (or throws under `PrecompiledMismatchPolicy.Strict`) and
  never materializes. A value that is simply absent is still the build default, as before.
- **Repeat resolution of a precompiled view no longer re-runs the whole validation walk, and an
  artifact is decoded once rather than once per template.** A hosted resolve ran the per-request
  gauntlet on every request: for each recorded member row it walked the whole `AppDomain` assembly
  list and took an `AssemblyName` off every assembly to resolve one start type, rebuilt each bound
  extension's `[Prop]` layout reflectively, and built a diagnostic path string that only a failure
  reads. The verdict is now memoized per entry, per request shape and per engine assembly generation,
  and dropped when that generation moves; resolved type identities and prop-layout fingerprints are
  kept alongside. Separately, every template of an assembly shares one merged artifact, and each row
  re-decoded that whole artifact — every string, type, document, definition and expression tree — to
  materialize itself; it is now decoded once per image and shared, with the per-template site index
  and the import map built once instead of scanned per template, and the digest verified by hashing
  the image in place rather than over a full copy of it. Measured on this repo's benchmark artifact:
  repeat resolution of a registered view falls from 106.3 µs and 135,846 B per call to 259 ns and
  336 B, the hosted `View` probe ladder from 476 ns and 2,608 B to 273 ns and 1,848 B, and cold
  register-bind-first-render allocation from 445 KB to 338 KB. Rendered bytes are unchanged on both
  tiers.
- **A refused site is recompiled at load under the context it actually sits in.** When the build
  cannot precompile one call — an extension declaring `[PrecompileUnsupported]`, a consumer over an
  unbindable call — it records that call's source and the loader recompiles it. The recompile was given
  a bare context built from the options and the current scope alone, so the fragment lost the document
  root a `::` read resolves against (it was overwritten with the scope the call sits in), the prop
  layout and slot type of the definition body around it, the call's region fills, the reader an `@<<`
  import expands through, and the compile's shared member-accessor cache. It now inherits its place in
  the document exactly as every other nested compile does, and still inherits none of what the build
  decided — no form record, no unbound-function deferral, its own error, warning, item and layout
  caches, and no scope map, because its positions are fragment-local until the enclosing compile
  re-anchors them. Rendered bytes are unchanged on both tiers.
- **A refused site nested inside a body is recorded as the source it actually is, so it precompiles
  instead of costing the whole template.** The build records a refused call's source by slicing the
  enclosing document's raw text, but an item's position is absolute in the text the parser ran over
  while a nested body's raw text is only that body's span of it — a `@list` element body, an `@if`
  body, a definition body. The two agree only at a document's top level, which is where every fixture
  that exercised a refusal happened to put one. Anywhere else the slice fell outside the text and the
  recorded source degraded to the call's name with no data parameter, which parses nowhere: the
  load-time recompile failed, the whole template faulted with
  `PrecompiledFallbackReason.ExtensionInitCompileError` and rendered through the dynamic tier — or, on
  a strict-load host, threw. The position is now translated into the coordinates of the text it
  indexes, and a body whose recorded text dropped the source's hidden tokens (a comment, a `@\`
  whitespace eater) is resolved against the document those positions are native to. Rendered bytes are
  unchanged on both tiers.
- **A refused call inside an `@<<` composition import precompiles.** A composition import expands
  inline, so the imported file's calls compile into the importing document and get no document of
  their own, while their positions stay absolute in the imported file. The parse keeps the text it
  consumed at each expansion, so a refused call's own source is cut from the file it was written in
  and recorded like any other refusal; the loader recompiles it at load and both tiers render the same
  bytes. This holds whether the imported file is a template the build compiled or a shared partial
  that only ever gets imported — the second is not a row of the artifact and is read from disk on
  both sides.
  <br/>Each item records the `@<<` block that composed its file, so two imports in one document — or
  the same file imported twice — keep their own sites and their own bodies, which a position alone
  cannot distinguish once both files expand into one document. That block is the position the build
  reports for the site and the position a fault inside the recompiled fragment is reported at: an
  offset into a file the template does not contain is never published as a position in it, including
  when the refused call sits in a body inside the imported file. `HED7020` guards a refusal whose
  source no text carries at all.

### Build and packaging

- **Two limitations of the generated sites are now documented, and the build reports the first.**
  A site that reads a non-public model type or member is not printed (generated code is compiled into
  the consumer's assembly and names only public things), so a strict-load or NativeAOT host refuses
  the template; and an `[InternalsVisibleTo]` grant carrying a public key is not accepted as proof
  that an entry point may name an internal model — that second case has no diagnostic of its own; the
  template's sites are declined as in the first. The `HED7031` notice now gives each declined site's
  reason and, for an accessibility decline, the way out, and an ordinary build prints it: it was a
  low-importance message that only `-v:detailed` showed. Embedded C# (`FullCSharp`) over a non-public
  model is a build error (`HED7012`), not a decline.
  See [Limitations](docs/precompilation.md#limitations).
- **A failed intermediate model compile is attributed (`HED7038`).** The compiler's errors are followed
  by one Heddle error saying they came from Heddle's pass over the project's own sources, that the
  project's compile was not reached, and how to avoid the pass. The build fails as before. A template
  set to `Precompile="false"` no longer starts that pass on its own.
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
