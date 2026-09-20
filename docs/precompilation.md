# Build-Time Pre-compilation

Heddle can pre‑compile your `.heddle` templates **at build time** into your application
assembly — the Razor compiled‑views shape. A precompiled template is **looked up, not parsed
and compiled** at run time: no ANTLR parse, no expression trees, no Roslyn work at startup,
and when every template an app renders is precompiled `Microsoft.CodeAnalysis` never even
loads. Templates are validated by the build (a broken template fails compilation at its
`.heddle` position) and become debuggable (real generated C#, real stack traces).

Pre‑compilation is **purely additive and opt‑in**. The runtime stays fully dynamic:
runtime‑loaded template strings, `:: dynamic`, `AllowCSharp`/`ExpressionMode.FullCSharp`
runtime compilation, and hot
reload all keep working, and precompiled and runtime‑compiled templates coexist in one
process. The precompiled assembly is a *cache seeded at build time*, never a cage. A template the
build cannot fully bind renders through the dynamic tier instead of failing, and a single *call* the
build cannot bind costs that call rather than the file — see
[What precompiles, and what falls back](#what-precompiles-and-what-falls-back) for where those two
lines run.

> The generated backend must produce **byte‑identical** output to the runtime backend — a
> differential test harness proves it across the whole fixture corpus in CI. The runtime
> path is the semantic reference.

---

## Setup

Add the build package (MSBuild targets driving the out‑of‑process `heddle compile` host; the core
`Heddle` package stays runtime‑only):

```xml
<ItemGroup>
  <PackageReference Include="Heddle" Version="..." />
  <PackageReference Include="Heddle.Build" Version="..." />
</ItemGroup>
```

## Templates and metadata

By default every `**/*.heddle` file in the project (excluding `bin`/`obj`) is picked up as a
template. Opt individual files out, or add extra ones, with the `HeddleTemplate` item. Five item
metadata are read:

| Metadata | Effect |
| --- | --- |
| `Key` | The item's explicit **registration key**, replacing the path‑derived one. It sets both the lookup key **and** the generated class name (via `SanitizeName`), and it is the remedy for a template outside `HeddleTemplateRoot` — an explicit key suppresses `HED7018`, because the flattened key is then what you asked for. It normalizes through the shared key rule and takes part in `HED7002`/`HED7003`. A value the normalizer refuses is `HED7004`. |
| `Name` | An **additional** name the template answers to — *not* a rename. The template keeps its key (path‑derived, or `Key`) **and** answers to the name, so both spellings resolve and nothing that resolved before stops resolving. It works at **both tiers**: the `@<<` import map at build time, and the precompiled registry at run time (the manifest row carries it, so `TryGet`/`TryResolve` find the same template by either spelling). It does not touch the key, the generated class name, the `#line` file or `HED7018`, and it takes no part in `HED7002`/`HED7003`, which are about keys. Where a spelling names one template's key and another's name, **the key wins** — a name is an addition and never displaces an existing spelling. Setting `Key` *and* `Name` is two names for one template, not a conflict. Importing a named template by its key resolves and warns (`HED7028`); a name the normalizer refuses, or one another template in the same build already answers to, is `HED7004` against the name — the key is unaffected. Across assemblies the same collision is `HED7104` at registration. |
| `ModelType` | The template's **model type**, declared from the project file instead of an in‑file `@model` directive. A template with neither is untyped; the metadata gives it the same typed emission the directive gives — the typed `Generate(...)` entry‑point signature, native member paths, `this` — without a directive in the template text. The spelling resolves exactly as the directive's does (the template's `@using` imports apply, and an unresolvable spelling reports `HED7007`). The directive wins nothing — **agreement is required**: when both are present they must name the same resolved type, and a disagreement is the `HED7032` build error. Equal spellings, or two spellings resolving to the same type, are fine. |
| `Precompile` | `false` opts the file out of pre‑compilation: no entry point, no manifest entry — but it **stays available to `@<<` imports**, which `Remove` cannot do. Absent or any other value means "precompile". This pairs naturally with `Name`: an import‑only partial under a friendly name. Its `Key`/`Name` are validated and its own imports advised like any other item's, even though it emits nothing; note that with no manifest row, an opted‑out template's `Name` is a **build‑time** import spelling only — there is no registry entry for the runtime to answer with. |
| `OutputProfile` | A per‑file override of `HeddleOutputProfile` for this item only. |

```xml
<ItemGroup>
  <!-- disable the default glob and be explicit -->
  <HeddleTemplate Include="Templates/**/*.heddle" />
  <!-- an import-only file: no entry point, but every @<< that imports it still resolves -->
  <HeddleTemplate Update="Templates/_layout.heddle" Precompile="false" />
  <!-- drop a file from the item set entirely (an @<< importer then reads it from disk, as the engine does) -->
  <HeddleTemplate Remove="Templates/scratch.heddle" />
  <!-- override the key (this also renames the generated class to `Home`) -->
  <HeddleTemplate Update="Templates/Home.heddle" Key="home" />
  <!-- ADD an import name: `@<<{{BuildReport}}` and `@<<{{Templates/report.heddle}}` both resolve.
       The key, and so the generated class, are unchanged. -->
  <HeddleTemplate Update="Templates/report.heddle" Name="BuildReport" />
  <!-- type the model from the project file: no @model directive needed in the template text -->
  <HeddleTemplate Update="Templates/invoice.heddle" ModelType="My.App.InvoiceModel" />
</ItemGroup>
<PropertyGroup>
  <EnableDefaultHeddleTemplates>false</EnableDefaultHeddleTemplates>
</PropertyGroup>
```

A template whose path is **not** under `HeddleTemplateRoot` and that carries no explicit `Key`
registers under its bare filename — the directory is dropped — and the build reports `HED7018`
naming the file, the root, and the flattened key it used.

### Assemblies the build must see

A `@model` spelling is resolved at **compile** time by both tiers, so both tiers have to be able to see the
assembly it names — and neither finds it by magic. The engine [loads nothing on its own](csharp-api.md#registration-register)
and takes an assembly only by registration; the build host resolves over the **compilation's reference closure**
and loads nothing either. The two questions have one answer:

```csharp
// in the consuming project — ONE declaration, read by both tiers
[assembly: Heddle.Attributes.HeddleModelAssembly(typeof(Acme.Models.Invoice))]
```

The `typeof` is the point. You cannot spell a type in a project that does not reference its assembly, so
"referenced at build, registered at run" stops being a rule your project can quietly violate and becomes
**CS0246 in your own source**. There is no `HED` id for a missing reference because the C# compiler already
owns that diagnostic. The reference then reaches the compiler, the build host indexes it with everything else the
compilation references, and `HeddleTemplate.Register` reads the same attribute at startup and registers the
assembly for engine type resolution. One declaration, both tiers, no way to configure one and forget the other.

**The escape hatch, for projects that cannot carry a `typeof`** — a templates-only project that must not take a
source dependency, or a model assembly with no compile-time-nameable public type:

```xml
<ItemGroup>
  <HeddleModelAssembly Include="$(SomeDir)Acme.Models.dll" />
  <HeddleExtensionAssembly Include="$(SomeDir)Acme.Extensions.dll" />
</ItemGroup>
```

The targets append `@(HeddleModelAssembly)` and `@(HeddleExtensionAssembly)` to `@(ReferencePath)`, so the item's
job is to reach the **compiler**. An item carries an obligation the attribute discharges for
you: **it configures the build only**, so the host still makes its own `HeddleTemplate.Register` call, and, not
being an ordinary reference, the file is absent from the deps file and deployment still has to put it where the
process can load it.

> **Configuration is an input, not a discovery.** The item deliberately does not name a path for the build host to
> read. Build output has to be a function of the compilation's declared inputs — a host that opened files
> mid-build would make the output depend on when it looked, and would break incrementality, which is why there is
> no `CompilerVisibleItem` here and none is needed. Routing through `@(ReferencePath)` means MSBuild's own
> `ResolveAssemblyReferences` stays the loader and `SymbolTypeIndex` indexes the result with zero extra machinery.
> For the same reason **assembly configuration is not part of the options fingerprint**: it changes *whether* a
> template precompiles, never a rendered byte, so adding or removing a declaration can never produce an
> `OptionsMismatch` and never changes what a template renders.

Two consequences worth stating plainly:

- **Assembly configuration adds nothing to the run-tier fallback taxonomy.** No new `PrecompiledFallbackReason`, no new `HED` id.
  Configuring the build can move a template from "degraded at build" to "precompiled"; it cannot invent a way for
  a registered template to fail at run time that did not already exist.
- **Ambiguity that configuration creates is not a regression.** Making a second assembly visible that declares the
  same type spelling is a build error — and a runtime with both assemblies registered raises its own
  "the type name is ambigous" error for the same input. The tiers agree in the failure, which is the same
  agreement they have in the success.

What this does *not* reach: types that do not exist at build time (`TypeBuilder`, scripting hosts); collectible or
reloadable model contexts, which have deliberately **no** build-time twin — baking one generation of a reloadable
type into IL that the next reload invalidates is worse than degrading; types your compilation may not *name*
(the remedy is `[InternalsVisibleTo]`, not a Heddle setting); `@model dynamic`; assembly-qualified
spellings whose version a reference does not carry; and registration that is imperative by nature
(`TemplateFactory.AddExtensions` over live `Type`s, `FunctionRegistry.Register` over a delegate).

## Compile options (MSBuild properties)

These mirror `TemplateOptions` and are baked into the generated artifact; a mismatch against
the runtime request is caught by the validation gauntlet (below). An unparsable value is a
build error (`HED7009`). The build defaults track the engine defaults,
so an unset property produces the same options fingerprint as a **default‑options** runtime request.
It does not follow that an unset property is always safe: the fingerprint compares what the build
baked against what the request carries, so a host that sets a non‑default `OutputProfile`,
`ExpressionMode` or `TrimDirectiveLines` at run time while the build left the property unset gets an
`OptionsMismatch` and a per‑request degrade. Set the property to match the host, or leave both at
their defaults.

The **runtime counterpart** column is the option the request has to carry to match. Where it says *(build only)*
there is nothing to match: the property decides how the artifact is emitted, not what a render produces.

| Property | Values / default | Runtime counterpart | Effect |
| --- | --- | --- | --- |
| `HeddleOutputProfile` | `Html` (default) \| `Text` | `TemplateOptions.OutputProfile` | Output encoding profile (part of the options fingerprint). |
| `HeddleExpressionMode` | `MemberPathsOnly` \| `Native` (default) \| `FullCSharp` | `TemplateOptions.ExpressionMode` | Expression tier. `FullCSharp` is the build‑time equivalent of `AllowCSharp`. |
| `HeddleTrimDirectiveLines` | `true` (default) \| `false` | `TemplateOptions.TrimDirectiveLines` | Trim directive‑only lines (part of the fingerprint). |
| `HeddleMaxRecursionCount` | positive int, default `100` | `TemplateOptions.MaxRecursionCount` | Definition‑carrier recursion limit, baked at build. |
| `HeddleTemplateRoot` | dir, default `$(MSBuildProjectDirectory)` | `TemplateOptions.RootPath` | Root the template key is made relative to. |
| `HeddleGeneratedNamespace` | default `Heddle.Generated` | *(build only)* | Namespace of the generated entry classes. |

The properties `HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces`,
`HeddleObserveIntermediatePath` and `HeddleObserveImplementationPath` are not read. Each of the
first three draws one `HED7037` warning when set and is otherwise ignored; delete it from the
project. The other two are ignored silently. A set property changes no byte: the build never
branches on it.

The same mapping is what the editor uses: each of the first five has a `.heddle-lsp.json` key spelled as the
camelCased **runtime** name (`outputProfile`, `expressionMode`, …), so the three tiers name one option three ways
and mean the same thing. See [editor‑support.md](editor-support.md#configuring).

Assembly configuration is deliberately **not** in this table — it is not an option, it is a reference. See
[Assemblies the build must see](#assemblies-the-build-must-see).

---

## Upgrading from version 2

1. **Swap the package.** Replace the `Heddle.Generator` reference with `Heddle.Build` ([Setup](#setup)).
   `<HeddleTemplate>` items, their metadata and the option properties carry over.
2. **Rebuild every precompiled assembly.** An assembly built by `Heddle.Generator` is refused at
   `PrecompiledTemplates.Register` with `PrecompiledRegistrationException`; there is no mixed deployment.
3. **Delete the unsupported properties.** `HeddleObserveEngine`, `HeddleNodeFallback` and
   `HeddleEmitUtf8Pieces` each warn `HED7037` while set; `HeddleObserveIntermediatePath` and
   `HeddleObserveImplementationPath` are ignored. Output is byte‑identical without them.
4. **Typed entry points.** A `Heddle.Generated.*` entry renders under `PrecompiledTemplates.DefaultOptions`
   and throws `PrecompiledMismatchException` when its own artifact fails validation: assign the options
   and call `Register` + `ValidateAll` at startup
   ([Typed entry points](#typed-entry-points)).
5. **Build machines need the .NET 10 SDK.** `heddle compile` runs on .NET 10 whatever the project
   targets. The build host is not the target runtime: a BCL member absent on the target framework is a
   load‑time gate fallback, not a build error — run `ValidateAll` on the target before serving.
6. **Re‑key `WarningsAsErrors` and suppressions** from the build‑twin ids onto the engine ids the build
   reports at the `.heddle` position:

   | Suppression on | Re‑key onto |
   | --- | --- |
   | `HED7006` | `HED0002` (unknown extension) |
   | `HED7007` | Unchanged — the build raises it |
   | `HED7008` | `HED0001` (unresolvable member path) |
   | `HED7016` | `HED3005` (branch role missing scope channel) |
   | `HED7022` | `HED2001` (profile value) |
   | `HED7024` | `HED5019` (private region override) |
   | `HED7025` | `HED1012` / `HED1013` (overload verdicts) |
   | `HED7030` | `HED7031` (a site rebuilt at load) |
   | `HED7015` | Remove — not raised |
   | `HED7017` | Remove — the engine's extension‑declaration diagnostics report it |
   | `HED7023` | Remove — the runtime throws; there is no id to suppress on |
   | `HED7005`, `HED7019`, `HED7034` | Remove — not raised |
   | `HED7011` | `HED4009` (unreadable import) |

7. **Code against removed names.** `PrecompiledRuntime`, hand‑written `IHeddleTemplateManifest`
   manifests, `PrecompiledFunctions` and the public `PrecompiledTemplateInfo` constructors do not exist:
   render through `HeddleTemplate` or a typed entry point, and take manifests only from the build. A
   `Precompile="false"` kept only to work around a declined template can go — read the `HED7031` notice
   if a site still declines.

---

### Models declared in the project being built

A template may name a model that the project it lives in declares — the usual shape for a class
library. The build cannot see such a type without compiling it once, so when the first (parse-only)
pass leaves a model spelling unresolved, the targets run an **intermediate compile**: the project's
whole `@(Compile)` set — the SDK's generated global usings and assembly attributes included, so
`ImplicitUsings` works — plus throwing stubs for the entry points, compiled as the project's own
output type (a library needs no entry point) into `obj/…/heddle/models/<digest>/<AssemblyName>.dll`.
The host binds the templates against that image, and identities recorded from it equal the final
assembly's because it carries the project's own name **and strong name**: the pass signs with the
project's `AssemblyOriginatorKeyFile` / key container, `PublicSign` and `DelaySign` exactly as the real
compile does, so a signed project that reads a friend assembly's internals (an `[InternalsVisibleTo]`
naming its public key) compiles here too. It also receives the real compile's defines, language
version, nullable context, analyzer configuration and the project's own `AdditionalFiles`, so a source
generator — Razor's, for one — produces here what it will produce in the real compile, and a project
that names a `.razor` component from C# compiles in this pass too. Heddle adds no additional file of
its own, and nothing is handed to the real compile. Warnings never fail the pass.

- The `<digest>` covers every input of that pass: the sources' contents, the references, the
  analyzers, the analyzer-configuration and additional files, the strong-name key, and the compiler
  options (assembly name, output type, defines, language version, nullable context, signing mode,
  features). The pass runs the compiler only when no image exists for the current digest, so a
  rebuild that changed none of them compiles nothing. The digest is also an input of the template
  compile, so **editing a model recompiles the templates** (and so does reverting the edit), while a
  rebuild that changed nothing still skips that compile too. Only the current digest's
  directory is kept, and `dotnet clean` removes it.
- A `@model` spelling is resolved by the engine's own resolver under the template's `@using`
  imports, so everything the runtime accepts is accepted at build: a dotted nested type
  (`Shop.Outer.Inner`), a closed generic (`Shop.Box<Shop.Item>`), an alias.
- An **internal** model is supported: see the accessibility note under
  [Typed entry points](#typed-entry-points), and the strict-load consequence under
  [Limitations](#limitations).
- **Template items are ordinary MSBuild items**: a relative `Include` is relative to the project
  directory, whatever `HeddleTemplateRoot` says — the root only decides what the key is measured from.
  (Builds before 3.0's final targets joined the item path onto the root, so some projects spell the
  item relative to the root instead; a path that names no file from the project directory but does
  from the root is still honoured.)
- **When the pass fails, the build fails** — there is no fallback to building without it. The
  compiler's errors are reported as they are, from a compiler run the project did not start, so they
  are followed by one `HED7038` error that says so: whose pass it was, that the project's own compile
  was not reached, and the ways out. There is no switch that turns the pass off on its own; it runs only
  while some precompiled template binds a model type that no referenced assembly declares — in its
  own `@model`, or in the `@model` of a library it imports with `@<<`, whether or not that library is
  itself precompiled — so `Precompile="false"` on every such importing template, or moving the models
  to a referenced project, removes it. Opting out the library alone does not: its text, model
  directive included, is still compiled into the template that imports it.

**Incrementality.** The compile runs again whenever one of its inputs moved and skips when none did,
and every input is declared — nothing the artifact's bytes depend on is read without being one.

- The **template items' contents**, the implementation images, the intermediate model digest and the
  project files, the usual way — and every option value, `HeddleTemplateRoot` among them. The root
  earns its own mention: it decides every key the artifact records, and so every generated class name,
  while the item rows name absolute paths, so nothing else moves when only the root does.
- The **item list itself**, with every metadatum on it. A template deleted from a glob, or renamed
  while keeping its timestamp, changes no file MSBuild compares — its check is newest-input against
  oldest-output over the items that are *still there* — so the ordered list is written into
  `obj/…/heddle/options.txt` beside the option values, and that file is an input. Removing, renaming,
  re-keying or opting out a template therefore recompiles, and a rebuild that changed none of them
  still skips.
- The **libraries reached only through the import disk fallback**: a file an `@<<` names that no
  `HeddleTemplate` item declares — outside the project directory, or simply not globbed — is read from
  disk under `HeddleTemplateRoot` and compiled into the importing template. No item names it, so the
  host records what it actually read in `obj/…/heddle/disk-imports.txt`; the targets read that list
  back as compile inputs, and the host's own stamp covers those files' content. Editing such a library
  recompiles; deleting it is the ordinary unreadable-import error (`HED4009`) at the `@<<` directive.

A disk-served library is hashed from the text the compile was **handed**, never re-read once the
compile has finished: a library saved while the build was running would otherwise be certified as
content the artifact was never built from. When a compile cannot describe what it read — the same
file answered twice with different text, so the artifact mixes both — it writes no stamp at all, and
the build that follows compiles again rather than inheriting it. An input the build cannot read is
likewise never hashed to a placeholder, which would make every such input look unchanged for ever; it
makes the stamp unique instead, so the compile runs. A file that is merely **absent** is a different
fact and keeps a stable identity, so a build whose input is simply not there can still be up to date.

**Editors and `dotnet watch`.** Templates, and the `HeddleModelAssembly` / `HeddleExtensionAssembly`
items, are declared as `UpToDateCheckInput`, and templates as `Watch` items — so Visual Studio's fast
up-to-date check rebuilds after a template edit instead of serving the stale embedded artifact, and
`dotnet watch` restarts on one. The disk-served import libraries above join both item sets from the
collection hooks, and `dotnet watch` follows them — including a library **outside** the project
directory, which `dotnet watch --list` shows by absolute path. They come from what a build recorded
under `obj/$(Configuration)/…/heddle/`, so a configuration that has not been built yet contributes
none of them. `dotnet clean` removes what the build wrote under `obj/…/heddle/`, the response file,
the recorded import list and the intermediate compile's stubs included.

## Typed entry points

Each precompiled template emits a static class (`{HeddleGeneratedNamespace}.{SanitizedName}`,
e.g. `views/home/index.heddle` → `Views_Home_Index`). Call it directly — compile‑checked
model type, no lookup, no validation gauntlet:

```csharp
string html = Heddle.Generated.Views_Home_Index.Generate(model);
```

The signature is `Generate(TModel model, object chained = null, object callerData = null)`
where `TModel` is the declared `@model` type (`object` for `:: dynamic` **and for a
model‑less template** — the build host always emits an `object model` parameter). Generic arguments
and nested types are spelled in full (`global::Shop.Box<global::Shop.Item>`,
`global::Shop.Outer.Inner`).

The entry class is `public` when `TModel` is public, and **`internal` when it is not** — a public
method cannot take an internal parameter (`CS0051`), so a template over an internal model gets an
entry point the declaring assembly (and any `[InternalsVisibleTo]` friend) can call, and the registry
path serves everyone else. An internal model is named only when the project being built provably sees
it: it is the project's own type, or its assembly carries an `[InternalsVisibleTo]` naming the project's
assembly (`$(AssemblyName)`, which the targets pass to the host as `--assembly-name`). A grant that
carries a public key is not taken as proof — the build cannot know the consumer's key. A model the
project cannot name — another assembly's internal type with no such grant, a `private` nested class —
leaves the entry point taking `object`; the template still precompiles, binds and renders, and the
model‑type check at render is unchanged.

> **This path binds once, through the gauntlet, and it has no per‑request fallback.** A typed entry
> point resolves its template through `PrecompiledTemplates.BindTyped`, which runs the gauntlet for
> that model type and throws `PrecompiledMismatchException` on a failed validation instead of
> degrading — so an extension or function binding that drifted between build and deployment fails
> fast at bind time rather than rendering stale. It renders under the process‑wide default options:
> assign `PrecompiledTemplates.DefaultOptions` before the first bind. The remedy for drift you want
> to hear about once, rather than per bind, is the aggregate pass below: call
> [`PrecompiledTemplates.ValidateAll`](#validating-everything-once-after-configuration) once at startup.

## The registry for dynamic call sites

Templates identified by a runtime value (a path, a database key) resolve through the registry
instead. Registration is repeatable and idempotent per assembly:

```csharp
using Heddle.Precompiled;

PrecompiledTemplates.Register(typeof(MyApp.Program).Assembly);   // once per assembly
// Register(assembly) is the only registration path. HeddleTemplate.Register(assembly) is the
// separate call for extensions, and does NOT register precompiled templates.

// Discovery is a first-class, public API — keys, model types, fingerprints:
foreach (var entry in PrecompiledTemplates.Entries)
    Console.WriteLine($"{entry.Key}  model={entry.ModelType}");
```

### Lookup by key, and by registered name

A lookup resolves **keys first, registered `Name`s second**. Both spellings reach the same entry, so
a template built with `Name="BuildReport"` is found by `BuildReport` and by its key, and the entry it
returns reports its **key** either way — the key is the identity the staleness check and every
diagnostic message are written against. A name is not a registry *entry*: `Entries` does not
double‑count it.

The order is deliberate. Where a spelling names one template's key and another's
registered name, the **key owner wins**, whichever assembly registered first: a `Name` is an
*addition*, and an addition that displaced a spelling which already resolved would be a rename by the
back door. The losing name is simply not registered (or, if a key claims its spelling later, it stops
being registered), and the host hears about it once through `OnFallback` as `HED7104` —
`PrecompiledFallbackReason.RegisteredNameUnavailable`. Nothing throws, and the template whose name lost
stays fully reachable by its key. Contrast a duplicate **key**, which does throw
`PrecompiledRegistrationException`: two templates claiming one registration has no resolvable answer,
while a name/key collision already has one.

`HED7104` also covers a manifest `Name` the shared key rule refuses outright — a `..` segment, a
trailing separator, whitespace. The build host cannot emit one (that is `HED7004` at build time), so the
population is manifests no build tier vetted: third‑party, or emitted by a tool that skipped the rule.
It is reported rather than dropped in silence, through the same reason and id as a collision, because
from the host's side the outcome is identical — a name it expected to resolve does not, and the
template is still reachable by its key.

### What a fallback event names

`PrecompiledFallbackEvent` carries **two carriers and populates exactly one**:

| Carrier | Populated for | Reasons |
| --- | --- | --- |
| `TemplateKey` | a per‑request event, which is about one resolved template | `UnsupportedFunction`, `OptionsMismatch`, `ModelTypeMismatch`, `ExtensionBindingMismatch`, `FunctionBindingMismatch`, `MemberBindingMismatch`, `ExtensionInitCompileError`, `ExtensionInitTypingMismatch`, `StaleContent`, `StaleImport`, `CaseMismatch` |
| `AssemblyName` | a registration‑time event, which is about an assembly and has no one template to name | `SchemaVersionUnsupported`, `EngineVersionIncompatible`, `RegisteredNameUnavailable` |

Branch on the carrier, not on the reason. Events are constructed through `PrecompiledFallbackEvent.ForTemplate` /
`ForAssembly`, each of which refuses a reason belonging to the other carrier — the mapping above is
enforced, not documented.

Name lookup is ordinal, exactly as key lookup is, so a case‑sloppy spelling misses rather than serving
the wrong template.

**Every** resolver arm consults the registry. For direct (`TemplatePathType.None`) lookups
`TemplateResolver.GetTemplate` consults it before the dynamic cache and file check; for the
hosted `View`/`PartialView`/`Master` arms the search ladder is three tiers — **registry, then
cache, then disk**, each walked in search‑location order — so a candidate location whose
root‑relative path maps onto a registered key is served precompiled instead of compiled. Tier
order beats location order, which is how the cache tier has always behaved (a cached template at
the second location already won over a first‑location file on disk).

The hosted arms' candidate locations (`views/{controller}/{view}` and its `partial`/`base`
siblings) are root‑relative and `/`‑separated, joined to the resolver's root with
`Path.Combine`, so the disk tier of the ladder works on every platform.

On a hit the per‑request validation gauntlet runs against the arm's *real* effective options —
including the hosted arms' `ExpressionMode.FullCSharp`, so a manifest built under `Native` is
refused by the fingerprint check with no special‑casing anywhere. All pass → a `HeddleTemplate`
in precompiled‑adapter mode (zero parse, zero compile). A registry **miss** is never a failure —
the dynamic path proceeds untouched.

## The validation gauntlet and mismatch policy

Before trusting a precompiled entry the resolver checks it is compatible with the request:

- **Options fingerprint** — `(OutputProfile, ExpressionMode, TrimDirectiveLines)`. A template
  compiled under `Text` is not a valid answer for an `Html` request.
- **Model type** — only for an entry whose model type is *ambient*
  (`PrecompiledTemplateInfo.ModelTypeIsAmbient`), meaning the template declares no `@model` and the build
  chose the type: the `ModelType` item metadatum, else `object`. The engine types such a template from the
  requesting `CompileContext` instead, so the two answers have to be the same type or the entry is not an
  answer to this request. A template that declares `@model` is typed by its directive on both tiers and
  this step does not look at the request at all — the *value* is still checked at render, where a model the
  entry's `ModelType` cannot hold raises the dynamic tier's own `TemplateProcessingException`.
- **Extension bindings** — the `[ExtensionName]` extensions the template bound at build vs.
  the live registry (catches `[ExtensionReplace]` overrides). Default match is
  assembly‑qualified type name *without* version; supply your own via
  `PrecompiledTemplates.BindingResolver`.
- **Function bindings** — the functions the template called vs. the request's effective
  `FunctionRegistry` (below).
- **Member bindings** — every type and member the compiled form bound (the row's model type, each member
  path's start type and, per hop, the declaring and member types) must resolve to the same identity in this
  process; anything else is `MemberBindingMismatch` naming the member and both identities. Types resolve
  by name against the assemblies **loaded so far**, and nothing is loaded to find one — so a model whose
  assembly has not loaded yet reports `live=<unresolved>` and falls back. That verdict is not kept: the
  lookup repeats on the next request, and the entry serves as soon as the assembly is there.
- **Staleness** — only under `EnableFileChangeCheck`: the root file's `ContentHash` and the
  transitive `@<<` import closure vs. disk.

Two further reasons are raised at **materialization** rather than by the gauntlet, through the same channel: an
extension's own `InitStart`/`CompleteInit`, run for real at load, reports compile errors for a call site
(`ExtensionInitCompileError`), or hands its body a different model or chained type than the build recorded
(`ExtensionInitTypingMismatch`).

On any failure, behavior is controlled by `TemplateOptions.PrecompiledMismatchPolicy`:

- **`Fallback` (default)** — take the unchanged dynamic path and recompile, plus one
  `PrecompiledTemplates.OnFallback` callback whose event carries diagnostic id `HED7101` and
  the reason. A silent fallback that quietly re‑adds compile cost is the failure mode this
  design guards against.
- **`Strict`** — an entry that *exists* but fails the gauntlet **throws**
  `PrecompiledMismatchException` (for deployments that must never pay dynamic‑compile cost). A
  plain registry **miss** is not a `Strict` failure — strictness polices divergence, not
  coverage.

An assembly whose manifest schema/engine version is incompatible is ignored wholesale (every
template falls back), with one `HED7102` callback per manifest.

### Validating everything once, after configuration

The gauntlet above runs **per request, and only on the dynamic call path** — typed entry points never
reach it. `PrecompiledTemplates.ValidateAll` runs the same checks over **every** registered entry at a
time of the host's choosing, and reports *all* the failures rather than the first:

```csharp
// Once, after registration/extension/function configuration is complete and before serving:
var report = PrecompiledTemplates.ValidateAll(options);
if (!report.PassedForValidatedOptions) {
    foreach (var failure in report.Failures)          // ordered by template key
        log.Warn("{0}: {1} — {2}", failure.TemplateKey, failure.Reason, failure.Detail);
    // Your call what a failure costs: log it, or refuse to start.
}
```

Each failure is an ordinary `PrecompiledFallbackEvent` carrying the same reason, `Detail` and
`HED7101` id the per‑request gate produces, so existing logging handles it unchanged.

**The model‑type step is skipped here**, and deliberately: it judges the requesting `CompileContext`'s
model type, which `TemplateOptions` does not carry and a pre‑render pass does not have. An entry with
`ModelTypeIsAmbient` set can therefore still fall back at a request this report passed.

**It is a report, not a gate.** Nothing about per‑request behaviour changes: the gauntlet still runs
where it ran before. The pass does not raise `OnFallback` — nothing degraded, because no render
happened — and `PrecompiledMismatchPolicy.Strict` does not make it throw, because that policy polices
requests. What a failure costs is the caller's decision.

**The verdict is scoped to the options you pass, and the report says so.** Four gauntlet inputs are
per‑request rather than per‑configuration: `OutputProfile`, `ExpressionMode` and `TrimDirectiveLines`
(the fingerprint), and the effective `Functions` registry. One pass can therefore only be complete for
one options shape, so the report records the shape it used and names its green property accordingly:

| Member | Meaning |
| --- | --- |
| `PassedForValidatedOptions` | No entry failed **under the validated options**. Not an unscoped "is valid". |
| `Failures` | Every failing entry, ordered by template key (ordinal). |
| `EntriesChecked` | How many registered entries were examined — a snapshot of `Entries`, markers included. |
| `ValidatedFingerprint` | The `(OutputProfile, ExpressionMode, TrimDirectiveLines)` triple compared against — a snapshot, so mutating your `TemplateOptions` afterwards cannot make the report describe a shape it did not check. |
| `ValidatedFunctions` | The effective `FunctionRegistry`, by reference; `null` means the default‑registry shape. |
| `ValidatedStaleness` | Whether the staleness step ran (`EnableFileChangeCheck`). When false, the report says nothing about files changing on disk. |

A host that renders under more than one shape — two output profiles, or a request‑scoped
`FunctionRegistry` — calls the pass once per shape. There is no parameterless overload, deliberately: a
verdict with no options to scope it could only be misread.

An `UnsupportedFunction` failure is reported like any other. It has one cause: a **late‑bound**
entry whose recorded function name the live registry does not know — a real configuration gap between
the build and the deployment (register the function before serving). A call the build refused on purpose
is not a gauntlet failure at all: it is a refusal site, named by `HED7014`/`HED7033` at build and by
`HED7031`, and it renders through the dynamic path while the rest of the template stays precompiled.

### The staleness identity

`ContentHash` is the lowercase‑hex SHA‑256 of the template's **decoded text** re‑encoded as UTF‑8
without a BOM — not of the file's raw bytes. Both tiers compute it the same way
(`Heddle.Precompiled.ContentHash.HashText`): the build hashes the text Roslyn decoded, and the
staleness check decodes the file (honoring and stripping a BOM; no BOM means UTF‑8) before
hashing. An **encoding‑only** re‑save — adding a BOM, switching to UTF‑16 — therefore does not
read as stale, because it does not change the compiled output; any character change still does.
A file with no BOM that is not valid UTF‑8 is outside this contract, and its verdict is
unspecified — it degrades safely to the dynamic path.

### Deploying template files next to a precompiled assembly

Staleness compares the manifest key against a file at `TemplateOptions.RootPath`, so under
`EnableFileChangeCheck` the deployed templates must mirror their **build‑time** root‑relative
layout: a template built at `$(HeddleTemplateRoot)/views/home.heddle` must be deployed at
`{RootPath}/views/home.heddle`. The two roots cannot see each other (the build does not know the
deployment layout), so a mismatch is not validated — it simply reads as `StaleContent` and takes
the byte‑identical dynamic path, with an `OnFallback` event naming the key.

### Which fallbacks are legitimate

Falling back is the right answer for *stale data and change tracking*, and a defect everywhere
else: silently rendering dynamically because the deployed assemblies disagree with what the build
saw hides a packaging bug behind identical output. The classification:

| Reason | Class | Why |
| --- | --- | --- |
| `StaleContent` | legitimate fallback | The template changed on disk after the build — dynamic recompile is the correct semantics, and `EnableFileChangeCheck` exists to ask for exactly this tracking. |
| `StaleImport` | legitimate fallback | The same, one hop out: an import changed under an unchanged root template. |
| `UnsupportedFunction` | **must surface** | One cause: a **late‑bound** entry names a function the live registry does not know. That is a configuration gap between build and deployment — register the function before serving — never a designed miss. (A call the build refused on purpose is a refusal site, reported at build by `HED7014`/`HED7033`, not a gauntlet reason.) |
| `OptionsMismatch` | legitimate fallback | Options are per‑request degrees of freedom a host legitimately exercises; the same template served `Text` for mail and `Html` precompiled is a designed miss of the fingerprinted point, not a defect. |
| `ModelTypeMismatch` | legitimate fallback | The host typed its `CompileContext` differently from the build's assumption for a template that names no model of its own. Both types are legitimate — one is the deployment's, one is the build's — and only the dynamic tier can honour the deployment's. Precompiling for that host is a *build configuration* opportunity (`ModelType` item metadata), not a run‑time defect. |
| `ExtensionBindingMismatch` | **must surface** | A residual mismatch means the deployed binding set genuinely differs from the one the build declared — rendering dynamically with *different bindings than the build recorded* is the hazard, not the cure. |
| `FunctionBindingMismatch` | **must surface** | Declaring‑type/overload drift under the default registry signals assembly skew. A per‑request export registry (`options.Functions`) diverging by host choice is the one arguable sub‑case. |
| `MemberBindingMismatch` | **must surface** | A type or member the form bound resolves to a different identity here — the deployed assemblies differ from what the build compiled against, which is assembly skew, never a designed miss. |
| `ExtensionInitCompileError` | surfaces anyway | An extension's compile‑time hook, run at load, refused a call site. The dynamic tier runs the same hook and refuses the same template, so the fallback compile reports the engine's own errors rather than hiding anything. |
| `ExtensionInitTypingMismatch` | **must surface** | The hook typed a body differently at load than at build, so the recorded bodies would bind members to the wrong type: a build/deployment skew in the extension itself, reported instead of rendered. |
| `SchemaVersionUnsupported` | **must surface** | A manifest outside the engine's schema window means the deployable pairs build-host and engine packages out of contract — a packaging defect that silently un‑precompiles an entire assembly. |
| `EngineVersionIncompatible` | **must surface** | The same argument for engine skew; whole‑assembly silent rejection is the worst place to be quiet. |
| `CaseMismatch` | informational | Never a gauntlet failure — a registry lookup miss is contractually never a failure; the `HED7103` event is a diagnostic aid. |
| `RegisteredNameUnavailable` | informational | Never a gauntlet failure and never a throw (`HED7104`): a registered `Name` is an *addition*, and an addition whose spelling is already taken — or that the key rule refuses outright — costs the addition and nothing more — the template stays registered under its key, so no resolution that worked before changes meaning. Unlike a duplicate key there is nothing unresolvable to refuse, because key precedence already decides which template the spelling means. |
| duplicate key at registration | already surfaces | `PrecompiledTemplates.Register` throws `PrecompiledRegistrationException` — the precedent that registration defects throw. |

Under the default `Fallback` policy every class marked *must
surface* degrades to the dynamic tier and reports it through `OnFallback`, and only `Strict` throws.
"Silent" here means silent to the *render* — the output is correct and nothing fails — not
unreported: a host that leaves `OnFallback` unset is what makes it silent. The two rows marked
*informational* do not degrade at all; they report an addition that was not available and leave the
template reachable by its key.

The build host obeys the same principle: an exception escaping the compile of one template is a
defect, so it reds the build with a per‑template `HED7020` error naming the template and the
exception — the remaining templates and the artifact still emit — instead of being swallowed into
a silent degrade.

---

## Functions in precompiled templates

**Host functions must be discoverable at build time**, which means exported declaratively from a
*referenced* assembly. Calls to the engine's own built‑ins (`upper`, `len`, …) are late‑bound
sites: their target lives inside the engine, where a generated site cannot name it, so they bind at
first render and are listed in `HED7031` like any other declined site:

```csharp
[assembly: Heddle.Attributes.ExportFunctions(typeof(Acme.Web.TemplateFunctions))]

namespace Acme.Web
{
    public static class TemplateFunctions
    {
        public static string TitleCase(string s) => /* … */;   // -> @(titlecase(x))
    }
}
```

The build binds `@(titlecase(x))` **directly** to `Acme.Web.TemplateFunctions.TitleCase` — no
registry dispatch, a real stack frame, and a logic fix in the function package reaches
precompiled templates by updating the reference. **Parity rule:** the build must reference the
assemblies the host registers from — every `options.Functions.RegisterFrom(assembly)` in
startup pairs with a build‑time reference to the same assembly; both read the same
`[ExportFunctions]` metadata.

**Binding is static, and the build‑time inventory is the only scope that can be correct.** A
precompiled call is emitted as a direct call to the chosen method. Illegality is proved against
the overload set the build can see (the default registry plus every `[ExportFunctions]` method) and
draws the engine's own rank verdicts (`HED1012`/`HED1013`) at build, rather than against some wider
scope: there is no wider one for what precompiles. A host that registers extra overloads —
which can make an ambiguous set unambiguous, or an inapplicable set applicable — is handled at the
gate instead: the gauntlet's function check sees the divergence and the template renders
through the dynamic tier, where the host's registrations are live. The one case the gate cannot
rescue is a build **error**, since it fails before any tier is chosen; the escape hatch there is
`Precompile="false"` on the item, which skips key derivation and emit entirely while keeping the
template in the `@<<` import map.

### Functions the build cannot see

A function registered only at run time — a `Register(string, Delegate)` closure — is not representable
in assembly metadata, so the build cannot bind it. It does not have to: the **call's shape** is known
even where its target is not, so the call is emitted as a *late‑bound site* that resolves **once, at
first render**, through the engine's own overload ranker, and caches the bound delegate. The same
overload wins on both tiers by construction, and steady‑state renders cost one delegate call and no
allocation.

Three consequences worth knowing:

- **The manifest records the name with no target.** The gauntlet checks it per request and falls back
  to the dynamic tier where the live registry cannot serve the name — unregistered, or registered as an
  *extension*, whose render protocol a value site cannot reproduce. `ValidateAll(options)` reports both
  before any render.
- **Failure is the engine's failure.** A name nothing registers, an ambiguous call, and a call no
  overload accepts each raise the engine's own positioned error (`HED1001`, `HED1013`, `HED1012`) from
  the precompiled tier too, rather than rendering something the engine would not.
- **The registry is part of the request.** A render under a different `TemplateOptions.Functions`
  re‑binds, matching the dynamic tier, which compiles per options.

`HED7014` still fires for the calls this cannot serve — chiefly an argument whose static type has no
build‑time answer (an argument that is itself a call to an unknown function), so no overload can be
selected against it. Exporting the function with `[ExportFunctions]` on a public static container binds
it at build time and avoids the first‑render work entirely.

## Custom extensions in precompiled templates

Custom `[ExtensionName]` extensions bind **from the referenced assembly, never inlined** — a
security or logic patch reaches precompiled templates by updating the package. See
[custom‑extensions.md](custom-extensions.md#precompiled-mode) for the requirements (chiefly a
parameterless ctor and no reliance on runtime registry mutation).

**A compile‑time hook is not a reason to fall back.** The build does not predict what
`InitStart`/`CompleteInit` does; the artifact records the body, and the hooks *run* at materialization
over the recorded body — so a bodied call to a third‑party extension, and an extension that re‑types its
body, precompile in a **default build**, with no property to set and no name list to be on. A hook that
types its body differently at load than the build recorded degrades that template to the dynamic tier
(`ExtensionInitTypingMismatch`); a hook that reports compile errors at load degrades it too, and the
dynamic tier reports the same errors (`ExtensionInitCompileError`).

An extension whose compile‑time behaviour genuinely cannot survive load‑time execution — chiefly one
whose hook walks the enclosing document through `InitContext.ParseContext` — says so with
`[PrecompileUnsupported("…")]`, and its calls cost **one call site each** under `HED7033`. The
declaration is read on both sides: off the bound type at build, and off the **live type** at run, so an
extension package that adds it after your assembly was built still falls back rather than binding
through a seam its author has disowned.

### Startup order: a suggestion, not a rule

The build tier binds every extension the compilation could see; the run tier holds only the ones you
registered. So a precompiled template survives the gauntlet's extension step exactly when the extension assemblies it
was built against are registered **before** the template renders — and which assemblies those are, and
when they load, is the host's decision. The engine does not make it: it will not load an assembly to
satisfy a binding, and it will not defer a render waiting for one. It reports.

The pattern that keeps the reporting out of the request path:

```csharp
// 1. Extensions first — every assembly that exports any, in the precedence order you want.
HeddleTemplate.Register(typeof(Program).Assembly);
HeddleTemplate.Register(typeof(SomeLibrary.WidgetExtension).Assembly);

// 2. Functions next. TemplateOptions.Functions defaults to null, which means the frozen built-ins —
//    so create a registry, fill it, and assign it. Registering into the default one throws.
var functions = new FunctionRegistry();
functions.RegisterFrom(typeof(Program).Assembly);
options.Functions = functions;

// 3. Then the precompiled manifests.
PrecompiledTemplates.Register(typeof(Program).Assembly);

// 4. Then prove it, before serving a request.
var report = PrecompiledTemplates.ValidateAll(options);
if (!report.PassedForValidatedOptions)
    throw new InvalidOperationException(report.ToString());   // fail the deployment, not the request
```

Registering an extension assembly *after* a template has already rendered is legal and takes effect,
but anything rendered in between degraded to the dynamic tier and said so through `OnFallback`. Step 4
is what turns "said so, per request, in production" into "failed at startup, once".

**The build‑side mirror.** Each of the first three steps has a build‑time counterpart, and a template only
precompiles when the build's answer is at least as complete as the run tier's — so it is worth reading the two
side by side:

```xml
<!-- 1. Extensions: a reference is what the build binds from. Add an item only for an assembly this project
        does not reference (see "Assemblies the build must see"). -->
<ItemGroup>
  <HeddleExtensionAssembly Include="$(SomeDir)SomeLibrary.dll" />
</ItemGroup>
```
```csharp
// 2. Functions: the build reads [ExportFunctions] off the reference closure — the same declaration
//    RegisterFrom reads at run time, so exporting rather than registering imperatively is what makes a
//    function-calling template precompile.
[assembly: ExportFunctions(typeof(MyApp.TemplateFunctions))]

// 3. Models: one declaration for both tiers — the typeof forces the reference the build resolves against,
//    and HeddleTemplate.Register reads the same attribute at startup (step 1 above already makes that call).
[assembly: Heddle.Attributes.HeddleModelAssembly(typeof(Acme.Models.Invoice))]
```

Step 4 has no build twin, and deliberately so: `ValidateAll` proves what *this process* is configured to serve,
which is the question the build cannot answer. What the build does instead is refuse to claim a template it
could not fully bind — the degrade — so the two never disagree about a template they both accepted.

---

## What precompiles, and what falls back

**The headline: custom and third‑party extensions precompile in a default build.** Bodied,
hook‑overriding, `[Prop]`‑declaring, branch‑role — all of them, with no property to set, no probe to
enable and no name list to be on. The artifact is the engine's own compile of the template; the loader
runs the real `InitStart`/`CompleteInit` at materialization over the recorded bodies. See
[Custom extensions in precompiled templates](#custom-extensions-in-precompiled-templates).

**There are two sizes of fallback, and only one of them is about the template.**

- A **template** fallback: the validation gauntlet or materialization refuses the entry for this request
  and the dynamic tier renders it, reported through `OnFallback` (see
  [Which fallbacks are legitimate](#which-fallbacks-are-legitimate)).
- A **site** fallback: the file precompiles, and one site inside it is rebuilt from the recorded form at
  load, or compiles its own source text at first render. The rest of the template is unaffected. `HED7031`
  names every such site at build.

Falling back is never a correctness question, at either size. Both tiers are parity‑checked to the
byte, so a fallen‑back template — or a fallen‑back site — renders exactly what a precompiled one would;
what it costs is the build‑time work. `HED7031` is how you hear about it at build (an Info notice), and
`TemplateOptions.PrecompiledStrictLoad` is how you make it matter at load.

### Refusal sites

A site the build cannot bind is recorded as a **refusal site** on the template's row
(`PrecompiledTemplateInfo.RefusalSites`: site ordinal, class, detail, position) and compiles its own
source text at first render. The class is one of `PrecompiledRefusalClass`:

| Class | The refusal it names | Reported at build |
| --- | --- | --- |
| `UnsupportedExtension` | The bound extension declares `[PrecompileUnsupported]`. | `HED7033`, quoting the declared reason |
| `ReflectionOrderValue` | A value the engine types by reflection enumeration order. | `HED7031` |
| `UnbindableCallTyping` | A bodied or chained consumer over a call no build‑time registration binds: the call's result would have to type a body or a hook, and the loader cannot re‑type a recorded body. Export the function with `[ExportFunctions]`, else set `Precompile="false"`. | `HED7014` |

A bodiless value‑position call the build registry cannot bind is not a refusal: it is a **late‑bound**
site, typed at first render against the live `TemplateOptions.Functions`
([Functions the build cannot see](#functions-the-build-cannot-see)).

### Sites rebuilt at load

The build prints a generated site for a member accessor, native expression or embedded C# expression
only when the consumer's assembly can spell everything the site names. It declines — the site is
recorded as data and rebuilt from the compiled form at load — for a type or member a printed site does
not name (any non‑public type or getter, `internal` included whatever `[InternalsVisibleTo]` grants — a
site is printed without knowing who may see it; an `[Obsolete(error: true)]` member; a
compiler‑generated name, an open generic, a pointer or by‑ref type), an expression containing a
late‑bound call, a function bound to a delegate or to anything but a public static method, a member
path through a `dynamic` hop, a constant with no C# literal (`NaN`, an infinity), a constant
sub‑expression that throws when evaluated (`@(A + (79228162514264337593543950335M + 1M))` — it throws at
render on the dynamic tier as well, if it is reached), and an integral or `decimal` division by a
constant zero with a non‑constant dividend (`@(A / (1 - 1))`: the engine compiles it and throws at
render, C# refuses to compile it). Operators never decline a site: the printer prints the tree the
engine bound, operand conversions included, keeps its evaluation order and short‑circuiting exactly,
and prints every constant sub‑expression as the value the engine computed, so the C# compiler never
folds one by rules of its own. Declines are listed in the
template's `HED7031` notice; under strict load such a site is refused instead
([Generated sites and strict mode](#generated-sites-and-strict-mode)).

### What does not precompile

- **Embedded C# outside `FullCSharp`.** The engine refuses the same template under the same options;
  the build reproduces the refusal.
- **A `ref struct` in a boxing sink** — a model, an operand, a function argument, a slot value. Both
  tiers refuse it.
- **An open generic model type.** The dynamic tier accepts no model value for one and cannot build a
  member accessor for it.
- **Types that do not exist at build time** — `TypeBuilder`, scripting hosts. The spelling resolves to
  nothing and the build reports `HED7007`.
- **Collectible or reloadable model contexts.** There is no build‑time twin of `UnregisterModelAssemblies`.
- **Assembly‑qualified spellings whose version a reference does not carry.**
- **Imperative‑only registration** — `TemplateFactory.AddExtensions` over live `Type`s,
  `FunctionRegistry.Register` over a delegate. Functions have the late‑bound site above; extensions
  have the declarative form ([`ExportExtensions`](custom-extensions.md#registering-your-extensions)).

`@model dynamic` and a model‑less template both precompile — the entry point takes `object` and member
reads route through the engine's own dynamic binder, pinned to Heddle's assembly so the two tiers bind
alike — and an `internal` type or member of the **consumer's own** assembly is nameable by a generated
site.

---

## Generated sites and strict mode

Beside the stored compiled form, the build prints one **generated site** per member accessor,
native expression and embedded C# expression it bound — a typed C# method keyed by the site's id
(template content hash, row index, walk ordinal) — into the assembly's generated source, and the
artifact class implements `IPrecompiledSiteTable` over them. At materialization the loader asks the
table for each site before rebuilding it from its serialized form, and consults a table only when its
recorded artifact digest matches the artifact it loaded, so a table can never serve a site of a
different compilation.

- **`"Heddle.Precompiled.UseGeneratedSites"`** (`AppContext` switch, default `true`) — set it to
  `false` to make the loader ignore the table and rebuild every site from data: the comparison arm for
  a technique measurement, and a diagnostic aid when a generated site is suspected.
- **`TemplateOptions.PrecompiledStrictLoad`** (seeded from the **`"Heddle.Precompiled.StrictLoad"`**
  `AppContext` switch, default `false`) — under strict load a site the table does not serve
  **throws** `PrecompiledStrictLoadException` at materialization, naming the template key, the site
  ordinal and the site kind (`MemberAccessor`, `NativeExpression`, `CSharp`, `LateBound` or
  `RefusalSite`), instead of compiling it at load. A trimmed or NativeAOT host sets it so that
  nothing is ever compiled at run time. An expression made only of constants (`@(1 + 2)`,
  `@("a" + "b")`) is not a site: the build stores its folded value, and the loader folds it again
  while materializing — once, never on the render path — so it never trips strict load.
- **Declared classes.** A strict host that hits `LateBound` has a call the build could not bind:
  export the function with `[ExportFunctions]` on a container the build can see (a referenced
  assembly, or the project's own intermediate compile — the `samples/precompiled-aot` project
  exports its own `SampleFunctions` this way), and the site is bound at build. A `RefusalSite` is a
  class (a)/(b)/(c) refusal named by `HED7031`/`HED7033`/`HED7014` at build; the remedy is the one
  those ids name. A `CSharp` site is served only when the build printed it (an `@using`-resolvable
  expression); otherwise it is data and strict load refuses it.
- **Extensions in a trimmed host** are declared in the typed form — `[assembly: ExportExtensions(typeof(MyExtension))]`
  — whose constructor parameter roots each extension type through trimming; the parameterless scan-all
  form enumerates the assembly's types at registration, which trimming empties, and carries
  `[RequiresUnreferencedCode]` so a trimmed build says so instead of silently losing extensions.

### Limitations

Two things the build deliberately does not do. Neither changes a rendered byte; both matter to a host
that sets `PrecompiledStrictLoad`.

**Non-public model types and members are not printed, so strict load refuses them.**

- *What is refused.* A generated site is C# compiled into **your** assembly, and the printer names only
  what any assembly may name: `public` types, and properties with a `public` getter. A site that reads
  an `internal` (or otherwise non-public) model type, or a property whose getter is not public, is
  declined — whatever `[InternalsVisibleTo]` grants, and even when the type is the project's own. The
  engine binds such members by reflection, so the template still compiles, precompiles and renders
  identically: the declined site is rebuilt from the stored form when the template is first bound.
- *What you see.* At build, the template's `HED7031` notice — a `message`, printed by an ordinary
  `dotnet build` at its default verbosity, once per template and never a warning — lists each declined
  site with its reason and ends with the way out:
  `ledger.heddle(1,1): message HED7031: not fully precompiled: 2 sites rebuilt at load: MemberAccessor
  site at @33 (start type: non-public type 'Books.Ledger'); …`. A member rather than the type reads
  `(hop 0 'Secret' is not a public instance property)` for a plain member path, or `(member 'Secret'
  is not a public instance property)` inside an expression. At run time nothing, unless the host is strict: then binding the template
  throws `PrecompiledStrictLoadException` naming the template, the site ordinal and its kind, because
  rebuilding a site is exactly the load-time compilation strict load exists to forbid. A NativeAOT
  host cannot rebuild it at all.
- *Why.* Printing `internal` names would make the generated source compile or not depending on who
  compiles it; the printer has no way to know, site by site, and a site that fails to compile fails the
  consumer's whole build. Declining costs one template its strict-load eligibility instead.
- *Workaround.* Make the model type and the members the template reads `public`; or type the template
  with a `public` interface or a DTO that exposes just those members, and pass the internal object
  through it. The template no longer appearing under `HED7031` in the build output is the confirmation.
- *Embedded C# is stricter, and says so as an error.* Under `ExpressionMode=FullCSharp` an `@( … )` C#
  expression over a non-public model type is not a declined site: the expression is compiled as C#
  against the model, the compiler refuses the name, and the build **fails** with `HED7012` —
  `'Ledger' is inaccessible due to its protection level` — for a model the project itself declares as
  much as for a referenced one. The ways out are the same two.

**An `[InternalsVisibleTo]` grant that carries a public key is not taken as proof.**

- *What is refused.* An entry point names an `internal` model type in its signature only when the
  project provably sees it: the type is the project's own, or its assembly grants
  `[InternalsVisibleTo("YourAssembly")]` **without** a `PublicKey`. With a key in the grant, the entry
  point takes `object` instead (see [Typed entry points](#typed-entry-points)).
- *What you see.* `Generate(object model, …)` in the generated source where you expected your type.
  No diagnostic: the template precompiles, binds and renders, and the model-type check at render is
  unchanged — only the compile-time typing of that one parameter is lost.
- *Why.* A keyed grant is honoured only for a consumer signed with that key, and the build host is
  told the consumer's name, not its key. Guessing wrong is `CS0281` in your build.
- *Workaround.* Make the model `public`, or call the template through the registry
  (`PrecompiledTemplates.TryResolve`), which needs no typed signature.

---

## Build-time diagnostics

Each build‑time condition reports with an `HED7xxx` id. Template‑content conditions report at
their `.heddle` position; file/key/option‑level conditions report without a source location:

| Id | Meaning |
| --- | --- |
| `HED7001` | A `.heddle` source could not be read. |
| `HED7002` | Two templates normalize to the same key. |
| `HED7003` | Two keys differ only by case (warning). |
| `HED7004` | Unusable explicit key metadata on an item: a `Key` or `Name` value the key normalizer refuses, or a `Key` and a `Name` that name two different keys. |
| `HED7005` | **Reserved; the build does not raise it.** Unpaired surrogate in static text. |
| `HED7006` | **Reserved; the build does not raise it.** A named extension resolves to no `[ExtensionName]` type in any reference; the engine's own `HED0002` reports it. |
| `HED7007` | The `@model` or `ModelType` spelling does not resolve to a type (error). |
| `HED7008` | **Reserved; the build does not raise it.** A member path does not resolve on the model type; the engine's own `HED0001` reports it. |
| `HED7009` | An MSBuild option value is unparsable. |
| `HED7010` | Two keys sanitize to one generated class identifier, or a key sanitizes to a name the generated code uses itself: `HeddleArtifact`, or `Generate` (the entry point every entry class declares — `generate.heddle` at the template root). Rename the file or set `Key`. |
| `HED7011` | **Reserved; the build does not raise it.** An `@<<` import outside the item set is read from disk under the template root, as the engine reads it; an unreadable spelling is the engine's own `HED4009`. |
| `HED7012`/`HED7013` | A forwarded front‑end error/warning carrying no id. |
| `HED7014` | A called function no build‑time registration binds, in a call shape a late‑bound site cannot serve either (chiefly an argument whose static type has no build‑time answer) — the template falls back (warning). A delegate‑only registration alone is a late‑bound site, not this: see *Functions the build cannot see*. |
| `HED7015` | **Reserved; the build does not raise it.** A bound extension overrides a compile‑time hook the build has not read; hooks run at materialization, so there is nothing to report. |
| `HED7016` | **Reserved; the build does not raise it.** A `[BranchRole]` continuation or terminal omits `[ScopeChannel]`; the engine's own `HED3005` reports it. |
| `HED7017` | **Reserved; the build does not raise it.** A malformed `[Prop]` declaration; the engine's own declaration diagnostics report it. |
| `HED7018` | A template is outside `HeddleTemplateRoot` and has no explicit `Key`, so its directory is dropped and it registers under a flattened filename key (warning). Only `Key` suppresses it: a `Name` is additive and leaves the flattened key in place, so the warning is still about something real. |
| `HED7019` | **Reserved; the build does not raise it.** The `Heddle` engine assembly is not among the references; the version lock (`HED7035`) covers the pairing. |
| `HED7020` | The build host faulted (error). Compiling one template: a host defect, not a template error — that template emits nothing, the rest of the pass and the artifact are unaffected. Writing the outputs after every template compiled: nothing is written and the build fails (exit 1). |
| `HED7021` | An `[assembly: ExportFunctions(...)]` container is not a public static class. The runtime throws when the host assembly is registered, so the build errors rather than skipping the container silently. |
| `HED7022` | **Reserved; the build does not raise it.** An `@profile()` value is neither `text` nor `html`; the engine's own `HED2001` reports it. |
| `HED7023` | **Reserved; the build does not raise it.** An ambiguous type name; the engine's own error is forwarded under `HED7012`. |
| `HED7024` | **Reserved; the build does not raise it.** A call‑site fill overrides a private region; the engine's own `HED5019` reports it. |
| `HED7025` | **Reserved; the build does not raise it.** An illegal function call; the engine's own `HED1012`/`HED1013` report it. |
| `HED7028` | An `@<<` import names a template by its registration key while the template also carries a `Name`. Both spellings resolve — `Name` adds an import name, it never replaces the key — so this is a warning recommending the name-first spelling for a named template. |
| `HED7030` | **Reserved; the build does not raise it.** A type or member the consumer's assembly cannot name (a referenced assembly's `internal` member, an `[Obsolete(error: true)]` member); such a site is rebuilt from the compiled form at load and listed in `HED7031`. |
| `HED7031` | Info — a `message` an ordinary build prints, never a warning — once per template that is not fully precompiled, naming every site that rebuilds at load or renders through the dynamic path: refusal sites (with their class and reason — see `HED7014`/`HED7033`), late‑bound functions, C# sites carried as data, printer declines. The output is identical either way; a strict host (`PrecompiledStrictLoad`) refuses such a template at load instead. |
| `HED7032` | A template carries **both** an in‑file `@model` directive and `ModelType` item metadata, and the two spellings resolve to **different types**. The runtime reads only the directive, so the build refuses to pick one rather than typing the same template differently on the two tiers. Equal spellings, or different spellings resolving to the same type, agree and raise nothing. Reported at the file's start — item metadata has no in‑file position. |
| `HED7033` | A bound extension declares `[PrecompileUnsupported]` — its author's own statement that load‑time execution cannot reproduce its compile‑time behaviour (warning). The declared reason is quoted **verbatim**. Cost is **one call site**: that call binds dynamically (it renders by compiling its own source text at first render) while the rest of the template stays precompiled. Read on both sides — the build reads the declaration off the bound type, the loader reads it off the live type — so an extension package that adds the declaration *after* your assembly was built still falls back rather than binding through a seam its author has disowned. |
| `HED7034` | **Reserved; the build does not raise it.** Engine observation; the build compiles through the engine itself, so there is nothing to observe. |
| `HED7035` | The `Heddle.Build` package's engine version differs from the `Heddle` package the project references (error, at the project file). The artifact is stamped with the engine that compiled it, so the two must be equal — reference the same version of both packages. An engine reference the version lock cannot read fails the same way rather than stamping the artifact with an unverified engine. |
| `HED7036` | An implementation assembly the build must bind over — a project reference's output, a package's runtime image, a declared `HeddleModelAssembly`/`HeddleExtensionAssembly` item — could not be loaded (error, at the project file). The diagnostic names the path and the loader's message; templates naming its types cannot be compiled, so fix the reference or exclude the templates with `Precompile="false"`. |
| `HED7037` | An unsupported MSBuild property (`HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces`) is set (warning). It is ignored — delete it from the project. `HeddleObserveIntermediatePath` and `HeddleObserveImplementationPath` are ignored silently. |
| `HED7038` | The [intermediate model compile](#models-declared-in-the-project-being-built) failed (error). It follows the compiler's own `CS…` errors, which are the real cause: that pass compiles the project's own sources ahead of the project's compile, so the project's compile **was not reached**. Fix the errors above it; or set `Precompile="false"` on every template that binds such a model, in its own `@model` or through a library it imports with `@<<` (they render through the dynamic path and need no model at build time, so the pass does not run); or declare the model types in a referenced project. |

**Engine ids that fire at build.** A refusal the build can *prove* the engine repeats at its own
template compile is not a `HED7xxx` twin but the engine's id **forwarded** as a build error — same
fact, same id, same sentence, at the `.heddle` position: `HED1003` (method‑call syntax in a native
expression), `HED1004` (a native expression under a declared‑`dynamic` model, or a path crossing a
`dynamic` member), `HED1005` (`&&`/`||` over a non‑`bool` operand), `HED1007` (ternary arms or a `??`
pair with no common type), `HED1008`/`HED1009` (a binary/unary operator not defined for its operand
types), `HED1010` (no accessible indexer on a known target type), `HED1011` (a non‑`bool` ternary
condition), and `HED1018` (constant division by zero). The template still degrades to the dynamic
tier; the build fails the way the engine's compile would, instead of hiding the error until the
first runtime compile. A refusal the build host cannot prove — an operand it cannot type, a verdict
that depends on runtime binding, a template whose model type is simply undeclared — keeps degrading
silently under `HED7031`.

---

## Reserved build ids

`HED7005`, `HED7006`, `HED7008`, `HED7011`, `HED7015`, `HED7016`, `HED7017`, `HED7019`, `HED7022`,
`HED7023`, `HED7024`, `HED7025`, `HED7030` and `HED7034` are reserved: the build does not raise them,
each keeps its row above, and the number is never reused for another fact. Where the engine diagnoses
the same fact, the build reports the engine's id.

## The T4-successor CLI

The `heddle` CLI hosts the full dynamic engine — the T4‑successor codegen story. Installed as a
`dotnet tool`, it renders a template against a JSON model:

```bash
dotnet tool install --global Heddle.Tool
heddle render Templates/Enum.heddle --model-json color.json --out Color.cs
```

Invoked from an MSBuild `<Exec>` step it turns data into source; the produced artifact carries
the generated code but **no runtime Heddle dependency**. `color.json` objects become member
access, arrays drive `@list`/`@for`, and scalars map to CLR primitives.

```
heddle render <template> [--model-json <file>] [--out <file>] [--root <dir>]
```

---

*Verified against source at `f8a9497c`.* Claims marked ✓ are gated by a test:
the `HED70xx`/`HED71xx` tables ✓ (`DiagnosticIdTests`: descriptor ⇄ registry ⇄ this page, both
directions); the gauntlet's steps ✓ (`PrecompiledGauntletTests`); the compiled form's parity with
the dynamic tier on three sinks, three passes (in-memory, file-backed, staged with the staleness
step) under `PrecompiledMismatchPolicy.Strict` and a fallback sentinel ✓ (`CompiledFormParityTests`,
`FallbackGuard`); allocation no higher than the dynamic tier on every corpus row and sink ✓
(`CompiledFormAllocationTests`); the stored `compiled-form-v3.bin` fixture registering and rendering
byte-identically ✓ (`CompiledFormFixtureTests`); a host-built assembly registering and rendering ✓
(`HostBuiltArtifactTests`); the refusal classes ✓ (declared per corpus row in `CorpusIntent`, asserted
by set equality, `CompiledFormHarness.ExpectRefusal` for a declared extra). The MSBuild
option table, the startup-order guidance and the coverage/boundary tables are verified against source, not
gated — the coverage tables were written against the shared corpus's declared intent rows
(`src/TestCorpus/CorpusIntent.cs`), which *are* set-equality gated, but nothing checks the prose
against them.
