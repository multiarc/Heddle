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
| `Name` | An **additional** name the template answers to — *not* a rename. The template keeps its key (path‑derived, or `Key`) **and** answers to the name, so both spellings resolve and nothing that resolved before stops resolving. It works at **both tiers**: the `@<<` import map at build time, and the precompiled registry at run time (the manifest row carries it, so `TryGet`/`TryResolve` find the same template by either spelling). It does not touch the key, the generated class name, the `#line` file or `HED7018`, and it takes no part in `HED7002`/`HED7003`, which are about keys. Where a spelling names one template's key and another's name, **the key wins** — a name is an addition and never displaces an existing spelling. Setting `Key` *and* `Name` is two names for one template, not a conflict. Importing a named template by its key resolves and warns (`HED7028`); a name the normalizer refuses, or one another template in the same build already answers to, is `HED7004` against the name — the key is unaffected. Across assemblies the same collision is `HED7104` at registration. |
| `Precompile` | `false` opts the file out of pre‑compilation: no entry point, no manifest entry — but it **stays available to `@<<` imports**, which `Remove` cannot do. Absent or any other value means "precompile". This pairs naturally with `Name`: an import‑only partial under a friendly name. Its `Key`/`Name` are validated and its own imports advised like any other item's, even though it emits nothing; note that with no manifest row, an opted‑out template's `Name` is a **build‑time** import spelling only — there is no registry entry for the runtime to answer with. |

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

A template whose path is **not** under `HeddleTemplateRoot` and that carries no explicit `Key`
registers under its bare filename — the directory is dropped — and the build reports `HED7018`
naming the file, the root, and the flattened key it used.

## Compile options (MSBuild properties)

These mirror `TemplateOptions` and are baked into the generated artifact; a mismatch against
the runtime request is caught by the validation gauntlet (below). An unparsable value is a
build error (`HED7009`). The generator defaults track the engine defaults (both flipped in 2.0),
so an unset property produces the same options fingerprint as a **default‑options** runtime request.
It does not follow that an unset property is always safe: the fingerprint compares what the build
baked against what the request carries, so a host that sets a non‑default `OutputProfile`,
`ExpressionMode` or `TrimDirectiveLines` at run time while the build left the property unset gets an
`OptionsMismatch` and a per‑request degrade. Set the property to match the host, or leave both at
their defaults.

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

> **This path runs no validation gauntlet, and it has no fallback either.** A typed entry point calls
> `PrecompiledRuntime.GenerateString` directly: it never looks the template up, so it never reaches the
> gauntlet, so an extension or function binding that drifted between build and deployment is not
> detected and not degraded — it renders precompiled against the stale binding. That is the price of
> the fast path, and the remedy is the aggregate pass below: call
> [`PrecompiledTemplates.ValidateAll`](#validating-everything-once-after-configuration) once at startup.

## The registry — for dynamic call sites

Templates identified by a runtime value (a path, a database key) resolve through the registry
instead. Registration is repeatable and idempotent per assembly:

```csharp
using Heddle.Precompiled;

PrecompiledTemplates.Register(typeof(MyApp.Program).Assembly);   // once per assembly
// Register(assembly) is the only registration path. HeddleTemplate.Register(assembly) is the
// separate call for extensions, and does NOT register precompiled templates.

// Discovery is a first-class, public API — keys, model types, fingerprints, capabilities:
foreach (var entry in PrecompiledTemplates.Entries)
    Console.WriteLine($"{entry.Key}  model={entry.ModelType}  precompiled={entry.IsPrecompiled}");
```

### Lookup by key, and by registered name

A lookup resolves **keys first, registered `Name`s second**. Both spellings reach the same entry, so
a template built with `Name="BuildReport"` is found by `BuildReport` and by its key, and the entry it
returns reports its **key** either way — the key is the identity the staleness check and every
diagnostic message are written against. A name is not a registry *entry*: `Entries` does not
double‑count it.

The order is a decision, not an accident. Where a spelling names one template's key and another's
registered name, the **key owner wins**, whichever assembly registered first: a `Name` is an
*addition*, and an addition that displaced a spelling which already resolved would be a rename by the
back door. The losing name is simply not registered (or, if a key claims its spelling later, it stops
being registered), and the host hears about it once through `OnFallback` as `HED7104` —
`PrecompiledFallbackReason.RegisteredNameUnavailable`. Nothing throws, and the template whose name lost
stays fully reachable by its key. Contrast a duplicate **key**, which does throw
`PrecompiledRegistrationException`: two templates claiming one registration has no resolvable answer,
while a name/key collision already has one.

`HED7104` also covers a manifest `Name` the shared key rule refuses outright — a `..` segment, a
trailing separator, whitespace. The generator cannot emit one (that is `HED7004` at build time), so the
population is manifests no build tier vetted: hand‑written, third‑party, or emitted by a tool that
skipped the rule. It is reported rather than dropped in silence, through the same reason and id as a
collision, because from the host's side the outcome is identical — a name it expected to resolve does
not, and the template is still reachable by its key.

### What a fallback event names

`PrecompiledFallbackEvent` carries **two carriers and populates exactly one**:

| Carrier | Populated for | Reasons |
| --- | --- | --- |
| `TemplateKey` | a per‑request event, which is about one resolved template | `UnsupportedFunction`, `OptionsMismatch`, `ExtensionBindingMismatch`, `FunctionBindingMismatch`, `StaleContent`, `StaleImport`, `CaseMismatch` |
| `AssemblyName` | a registration‑time event, which is about an assembly and has no one template to name | `SchemaVersionUnsupported`, `EngineVersionIncompatible`, `RegisteredNameUnavailable` |

Branch on the carrier, not on the reason. The 2.0 event had a single `Key` property holding either
meaning, so a host had to re‑derive from `Reason` which of the two it had; that property is **removed in
2.1** rather than narrowed, so the change surfaces as a compile error at the reading site instead of a
null at run time. Events are constructed through `PrecompiledFallbackEvent.ForTemplate` /
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

A `UnsupportedFunction` marker entry (`HED7014`, a delegate‑only function the build refused on purpose)
is reported like any other failure. That is not noise: the entry is registered and will never render
precompiled, and excluding it would make the pass quieter than the truth.

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
| `RegisteredNameUnavailable` | informational | Never a gauntlet failure and never a throw (`HED7104`): a registered `Name` is an *addition*, and an addition whose spelling is already taken — or that the key rule refuses outright — costs the addition and nothing more — the template stays registered under its key, so no resolution that worked before changes meaning. Unlike a duplicate key there is nothing unresolvable to refuse, because key precedence already decides which template the spelling means. |
| duplicate key at registration | already surfaces | `PrecompiledTemplates.Register` throws `PrecompiledRegistrationException` — the precedent that registration defects throw. |

**Today's behavior is unchanged**: under the default `Fallback` policy every class marked *must
surface* degrades to the dynamic tier and reports it through `OnFallback`, and only `Strict` throws.
"Silent" here means silent to the *render* — the output is correct and nothing fails — not
unreported: a host that leaves `OnFallback` unset is what makes it silent. The two rows marked
*informational* do not degrade at all; they report an addition that was not available and leave the
template reachable by its key. Making the must‑surface
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

**Binding is static, and the build‑time inventory is the only scope that can be correct.** A
precompiled call is emitted as a direct call to the chosen method — nothing in `PrecompiledRuntime`
consults `options.Functions` at render, so precompiled code cannot resolve a function a host
registers at run time *at all*. That is why `HED7025` proves illegality against the overload set the
build can see (the shipped built‑in table plus every `[ExportFunctions]` method) rather than against
some wider one: there is no wider one for what precompiles. A host that registers extra overloads —
which can make an ambiguous set unambiguous, or an inapplicable set applicable — is handled at the
gate instead: the gauntlet's `FunctionBindings` check sees the divergence and the template renders
through the dynamic tier, where the host's registrations are live. The one case the gate cannot
rescue is a build **error**, since it fails before any tier is chosen; the escape hatch there is
`Precompile="false"` on the item, which skips key derivation and emit entirely while keeping the
template in the `@<<` import map.

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

### Startup order: a suggestion, not a rule

The build tier binds every extension the compilation could see; the run tier holds only the ones you
registered. So a precompiled template survives gauntlet step 2 exactly when the extension assemblies it
was built against are registered **before** the template renders — and which assemblies those are, and
when they load, is the host's decision. The engine does not make it: it will not load an assembly to
satisfy a binding, and it will not defer a render waiting for one. It reports.

The pattern that keeps the reporting out of the request path:

```csharp
// 1. Extensions first — every assembly that exports any, in the precedence order you want.
HeddleTemplate.Register(typeof(Program).Assembly);
HeddleTemplate.Register(typeof(SomeLibrary.WidgetExtension).Assembly);

// 2. Functions next, into the registry the request options will carry.
options.Functions.RegisterFrom(typeof(Program).Assembly);

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
| `HED7018` | A template is outside `HeddleTemplateRoot` and has no explicit `Key`, so its directory is dropped and it registers under a flattened filename key (warning). Only `Key` suppresses it: a `Name` is additive and leaves the flattened key in place, so the warning is still about something real. |
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

**How many of these one build reports.** The body walk **collects** refusals rather than abandoning
at the first: two sibling elements that each refuse produce two diagnostics, each at its own
`.heddle` position, for every id an element can raise (`HED7025`, `HED7008`, `HED7006`, `HED7015`,
`HED7017`, `HED7014`, `HED7022`). Refusal still propagates — a refused element fails its body up to
the template, so the template emits no generated source and no manifest row; collecting changes how
many diagnostics a build *surfaces*, never what it emits. Within **one expression** the walk still
reports once: expression writing is string-or-`null` composition, where a parent has nothing to
compose once a child fails, so `@(min(1, 2u)) @(max(1, 2u))` reports twice while
`@(min(1, 2u) + max(1, 2u))` reports once. That asymmetry is deliberate.

This is the one place the build tier deliberately does not mirror the dynamic engine's *shape*: the
dynamic engine raises the first such error and stops, so a build can report a set no single dynamic
compile produces. The match principle is untouched — every collected report is one the runtime
raises for the same input once the earlier fault is fixed. What differs is how many surface per
build, never which verdict either tier reaches.

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

---

*Verified against source at `6639f6f` (2026-07-26).* Claims marked ✓ are gated by a test:
the `HED70xx`/`HED71xx` tables ✓ (descriptor ⇄ registry ⇄ this page, both directions); the
fallback-reason taxonomy ✓ (an exhaustive classifier that throws on an unmapped reason); the
gauntlet's four steps ✓ (`PrecompiledGauntletTests`); the schema support window ✓
(`OldSchemaManifestRejectionTests`, over both released schemas). The MSBuild option table and the
startup-order guidance are dated-verified, not gated.
