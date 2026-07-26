# Build‑Time Pre‑compilation

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
process. The precompiled assembly is a *cache seeded at build time*, never a cage.

> The generated backend must produce **byte‑identical** output to the runtime backend — a
> differential test harness proves it across the whole fixture corpus in CI. The runtime
> path is the semantic reference.

---

## Setup

Add the generator package (it ships the emitter in `analyzers/dotnet/cs`; the core `Heddle`
package stays runtime‑only and unrestricted):

```xml
<ItemGroup>
  <PackageReference Include="Heddle" Version="..." />
  <PackageReference Include="Heddle.Generator" Version="..." PrivateAssets="all" />
</ItemGroup>
```

By default every `**/*.heddle` file in the project (excluding `bin`/`obj`) is picked up as a
template. Opt individual files out, or add extra ones, with the `HeddleTemplate` item. Three item
metadata are read:

| Metadata | Effect |
| --- | --- |
| `Key` | The item's explicit **registration key**, replacing the path‑derived one. It sets both the lookup key **and** the generated class name (via `SanitizeName`), and it is the remedy for a template outside `HeddleTemplateRoot` — an explicit key suppresses `HED7018`, because the flattened key is then what you asked for. It normalizes through the shared key rule and takes part in `HED7002`/`HED7003`. A value the normalizer refuses is `HED7004`. |
| `Name` | An **additional** name the `@<<` import map answers to — *not* a rename. The template keeps its key (path‑derived, or `Key`) **and** answers to the name, so both spellings resolve and nothing that resolved before stops resolving. It does not touch the key, the manifest row, the generated class name, the `#line` file or `HED7018`, and it is never a registry lookup, so it takes no part in `HED7002`/`HED7003`. Setting `Key` *and* `Name` is two names for one template, not a conflict. Importing a named template by its key resolves and warns (`HED7028`); a name the normalizer refuses, or one another template already answers to, is `HED7004` against the name — the key is unaffected. |
| `Precompile` | `false` opts the file out of pre‑compilation: no entry point, no manifest entry — but it **stays available to `@<<` imports**, which `Remove` cannot do. Absent or any other value means "precompile". This pairs naturally with `Name`: an import‑only partial under a friendly name. |

```xml
<ItemGroup>
  <!-- disable the default glob and be explicit -->
  <HeddleTemplate Include="Templates/**/*.heddle" />
  <!-- an import-only file: no entry point, but every @<< that imports it still resolves -->
  <HeddleTemplate Update="Templates/_layout.heddle" Precompile="false" />
  <!-- drop a file from the item set entirely (importers of it then draw HED7011) -->
  <HeddleTemplate Remove="Templates/scratch.heddle" />
  <!-- override the key (this also renames the generated class to `Home`) -->
  <HeddleTemplate Update="Templates/Home.heddle" Key="home" />
  <!-- ADD an import name: `@<<{{BuildReport}}` and `@<<{{Templates/report.heddle}}` both resolve.
       The key, and so the generated class, are unchanged. -->
  <HeddleTemplate Update="Templates/report.heddle" Name="BuildReport" />
</ItemGroup>
<PropertyGroup>
  <EnableDefaultHeddleTemplates>false</EnableDefaultHeddleTemplates>
</PropertyGroup>
```

A template whose path is **not** under `HeddleTemplateRoot` and that carries no explicit `Key`/`Name`
registers under its bare filename — the directory is dropped — and the build reports `HED7018`
naming the file, the root, and the flattened key it used.

## Compile options (MSBuild properties)

These mirror `TemplateOptions` and are baked into the generated artifact; a mismatch against
the runtime request is caught by the validation gauntlet (below). An unparsable value is a
build error (`HED7009`). The generator defaults track the engine defaults (both flipped in 2.0),
so an unset property produces the same options fingerprint as a default‑options runtime request
— an unset property never causes a gauntlet mismatch.

| Property | Values / default | Effect |
| --- | --- | --- |
| `HeddleOutputProfile` | `Html` (default) \| `Text` | Output encoding profile (part of the options fingerprint). |
| `HeddleExpressionMode` | `MemberPathsOnly` \| `Native` (default) \| `FullCSharp` | Expression tier. `FullCSharp` is the build‑time equivalent of `AllowCSharp`. |
| `HeddleTrimDirectiveLines` | `true` (default) \| `false` | Trim directive‑only lines (part of the fingerprint). |
| `HeddleMaxRecursionCount` | positive int, default `100` | Definition‑carrier recursion limit, baked at build. |
| `HeddleTemplateRoot` | dir, default `$(MSBuildProjectDirectory)` | Root the template key is made relative to. |
| `HeddleGeneratedNamespace` | default `Heddle.Generated` | Namespace of the generated entry classes. |
| `HeddleEmitUtf8Pieces` | `false` (default) \| `true` | Emit pre‑encoded `"…"u8` static pieces for the byte sink. |

---

## Typed entry points — the recommended host API

Each precompiled template emits a static class (`{HeddleGeneratedNamespace}.{SanitizedName}`,
e.g. `views/home/index.heddle` → `Views_Home_Index`). Call it directly — compile‑checked
model type, no lookup, no validation gauntlet:

```csharp
string html = Heddle.Generated.Views_Home_Index.Generate(model);
```

The signature is `Generate(TModel model, object chained = null, object callerData = null)`
where `TModel` is the declared `@model` type (`object` for `:: dynamic` **and for a
model‑less template** — the generator always emits an `object model` parameter).

## The registry — for dynamic call sites

Templates identified by a runtime value (a path, a database key) resolve through the registry
instead. Registration is repeatable and idempotent per assembly:

```csharp
using Heddle.Precompiled;

PrecompiledTemplates.Register(typeof(MyApp.Program).Assembly);   // once per assembly
// Register(assembly) is the only registration path — HeddleTemplate.Configure(assembly)
// discovers extensions but does NOT register precompiled templates.

// Discovery is a first-class, public API — keys, model types, fingerprints, capabilities:
foreach (var entry in PrecompiledTemplates.Entries)
    Console.WriteLine($"{entry.Key}  model={entry.ModelType}  precompiled={entry.IsPrecompiled}");
```

**Every** resolver arm consults the registry. For direct (`TemplatePathType.None`) lookups
`TemplateResolver.GetTemplate` consults it before the dynamic cache and file check; for the
hosted `View`/`PartialView`/`Master` arms the search ladder is three tiers — **registry, then
cache, then disk**, each walked in search‑location order — so a candidate location whose
root‑relative path maps onto a registered key is served precompiled instead of compiled. Tier
order beats location order, which is how the cache tier has always behaved (a cached template at
the second location already won over a first‑location file on disk).

On a hit the per‑request validation gauntlet runs against the arm's *real* effective options —
including the hosted arms' `ExpressionMode.FullCSharp`, so a manifest built under `Native` is
refused by the fingerprint check with no special‑casing anywhere. All pass → a `HeddleTemplate`
in precompiled‑adapter mode (zero parse, zero compile). A registry **miss** is never a failure —
the dynamic path proceeds untouched.

## The validation gauntlet and mismatch policy

Before trusting a precompiled entry the resolver checks it is compatible with the request:

- **Options fingerprint** — `(OutputProfile, ExpressionMode, TrimDirectiveLines)`. A template
  compiled under `Text` is not a valid answer for an `Html` request.
- **Extension bindings** — the `[ExtensionName]` extensions the template bound at build vs.
  the live registry (catches `[ExtensionReplace]` overrides). Default match is
  assembly‑qualified type name *without* version; supply your own via
  `PrecompiledTemplates.BindingResolver`.
- **Function bindings** — the functions the template called vs. the request's effective
  `FunctionRegistry` (below).
- **Staleness** — only under `EnableFileChangeCheck`: the root file's `ContentHash` and the
  transitive `@<<` import closure vs. disk.

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
| `UnsupportedFunction` | legitimate fallback | Not a run‑time discovery at all — the build refused *on purpose* (a delegate‑only function, warned `HED7014`) and recorded a marker entry. |
| `OptionsMismatch` | legitimate fallback | Options are per‑request degrees of freedom a host legitimately exercises; the same template served `Text` for mail and `Html` precompiled is a designed miss of the fingerprinted point, not a defect. |
| `ExtensionBindingMismatch` | **must surface** *(provisional)* | A residual mismatch means the deployed binding set genuinely differs from the one the build declared — rendering dynamically with *different bindings than the build recorded* is the hazard, not the cure. |
| `FunctionBindingMismatch` | **must surface** *(provisional)* | Declaring‑type/overload drift under the default registry signals assembly skew. A per‑request export registry (`options.Functions`) diverging by host choice is the one arguable sub‑case. |
| `SchemaVersionUnsupported` | **must surface** | A manifest outside the engine's schema window means the deployable pairs generator and engine packages out of contract — a packaging defect that today silently un‑precompiles an entire assembly. |
| `EngineVersionIncompatible` | **must surface** | The same argument for engine skew; whole‑assembly silent rejection is the worst place to be quiet. |
| `CaseMismatch` | informational | Never a gauntlet failure — a registry lookup miss is contractually never a failure; the `HED7103` event is a diagnostic aid. |
| duplicate key at registration | already surfaces | `PrecompiledTemplates.Register` throws `PrecompiledRegistrationException` — the precedent that registration defects throw. |

**Today's behavior is unchanged**: under the default `Fallback` policy every class above still
degrades silently with an `OnFallback` event, and only `Strict` throws. Making the must‑surface
classes throw *by default* (with an explicit opt‑out policy for hosts that rely on silent
degrade) is a behavioral change: it is filed as a candidate for the next breaking window and does
not ship outside one.

The build tier obeys the same principle already: an exception escaping the template emitter is a
defect, so it reds the build with a per‑template `HED7020` error naming the template and the
exception — the remaining templates and the manifest still emit — instead of being swallowed into
a silent degrade.

---

## Functions in precompiled templates

Built‑in functions bind through a public shim; **host functions must be discoverable at build
time**, which means exported declaratively from a *referenced* assembly:

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

A function that *cannot* be expressed as an exported `public static` method (a
`Register(string, Delegate)` closure) is not representable in assembly metadata: the template
draws build warning `HED7014` and is left un‑precompiled (a fallback‑marker entry) — it
compiles dynamically at run time. Wrap the closure in an exported static method to precompile
it.

## Custom extensions in precompiled templates

Custom `[ExtensionName]` extensions bind **from the referenced assembly, never inlined** — a
security or logic patch reaches precompiled templates by updating the package. See
[custom‑extensions.md](custom-extensions.md#precompiled-mode) for the requirements
(parameterless ctor; no reliance on runtime registry mutation; a `InitStart`/`CompleteInit`
override is refused as `HED7015` — **except** for a `[BranchRole]` custom branch extension, whose
`InitStart` override is its canonical shape and instead degrades quietly to the dynamic tier).

---

## Build‑time diagnostics

Each build‑time condition reports with an `HED7xxx` id. Template‑content conditions report at
their `.heddle` position; file/key/option‑level conditions report without a source location:

| Id | Meaning |
| --- | --- |
| `HED7001` | An `AdditionalFiles` `.heddle` source could not be read. |
| `HED7002` | Two templates normalize to the same key. |
| `HED7003` | Two keys differ only by case (warning). |
| `HED7004` | Unusable explicit key metadata on an item: a `Key` or `Name` value the key normalizer refuses, or a `Key` and a `Name` that name two different keys. |
| `HED7005` | Unpaired surrogate in static text — the `"…"u8` twin is suppressed (warning). |
| `HED7006` | A named extension resolves to no `[ExtensionName]` type in any reference **under the runtime's own discovery rule** (implements `IExtension` and carries an inherited `[ExtensionName]`). A name the runtime *would* find but the generator cannot bind — an `IExtension`-direct implementor, or a collision between unrelated types — degrades to the dynamic tier with a recorded reason instead (phase 3, F3). |
| `HED7007` | The `@model`/`::` type does not resolve (milestone‑2 native diagnostic). |
| `HED7008` | A member path does not resolve on the model type (milestone‑2 native diagnostic). |
| `HED7009` | An MSBuild option value is unparsable. |
| `HED7010` | Two keys sanitize to one generated class identifier. |
| `HED7011` | An `@<<` import is not among the compilation's `.heddle` `AdditionalFiles`. |
| `HED7012`/`HED7013` | A forwarded front‑end error/warning carrying no id. |
| `HED7014` | A called function is delegate‑only (not precompilable) — the template falls back (warning). |
| `HED7015` | A bound extension overrides a compile‑time hook — unevaluable at build. |
| `HED7016` | A branch continuation/terminal (`[BranchRole]`) omits `[ScopeChannel]`, so it can never read the branch state at run time (warning). |
| `HED7017` | An extension declares a malformed `[Prop]` parameter — the build‑tier twin of the dynamic tier's declaration diagnostics. |
| `HED7018` | A template is outside `HeddleTemplateRoot` and has no explicit `Key`/`Name`, so its directory is dropped and it registers under a flattened filename key (warning). |
| `HED7019` | The `Heddle` engine assembly is not visible among the compilation's references, so the manifest records the generator's own version as `engineVersion` (warning). |
| `HED7020` | The template emitter threw — a generator defect, not a template error. That one template emits nothing; the rest of the pass and the manifest are unaffected. |
| `HED7021` | An `[assembly: ExportFunctions(...)]` container is not a public static class. The runtime throws when the host assembly is registered, so the build errors rather than skipping the container silently. |
| `HED7022` | An `@profile(){{…}}` value is neither `text` nor `html`. The runtime rejects the template with `HED2001`, so the build reports it rather than pre-compiling output the dynamic tier would never produce. |
| `HED7023` | A model/prop/slot type name is ambiguous — several types answer to it and the `@using` imports do not settle it. The runtime raises the same ambiguity, so the build errors rather than binding one candidate. |
| `HED7024` | A call-site fill overrides a region the definition declares private. The runtime raises `HED5019` for the same template, so the build reports the matching error at the override's position. |
| `HED7025` | A function call the shared overload ranker proved illegal — ambiguous under Heddle's flat Pareto rank (`HED1013`), or no applicable overload (`HED1012`). Fires only when every argument estimate is typed: an argument the generator cannot describe proves nothing about the runtime and still degrades silently. |

| `HED7028` | An `@<<` import names a template by its registration key while the template also carries a `Name`. Both spellings resolve — `Name` adds an import name, it never replaces the key — so this is a warning recommending the name-first spelling for a named template. |

Member/type errors in milestone 1 arrive as C# errors remapped to the template span via
`#line`; milestone 2 replaces the covered ones with native `HED7007`/`HED7008`.

---

## The T4‑successor CLI

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
