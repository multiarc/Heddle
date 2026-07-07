# Build‑Time Pre‑compilation

Heddle can pre‑compile your `.heddle` templates **at build time** into your application
assembly — the Razor compiled‑views shape. A precompiled template is **looked up, not parsed
and compiled** at run time: no ANTLR parse, no expression trees, no Roslyn work at startup,
and when every template an app renders is precompiled `Microsoft.CodeAnalysis` never even
loads. Templates are validated by the build (a broken template fails compilation at its
`.heddle` position) and become debuggable (real generated C#, real stack traces).

Pre‑compilation is **purely additive and opt‑in**. The runtime stays fully dynamic:
runtime‑loaded template strings, `:: dynamic`, `@AllowCSharp` runtime compilation, and hot
reload all keep working, and precompiled and runtime‑compiled templates coexist in one
process. The precompiled assembly is a *cache seeded at build time*, never a cage.

> The generated backend must produce **byte‑identical** output to the runtime backend — a
> differential harness proves it across the whole fixture corpus on every build. The runtime
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
template. Opt individual files out, or add extra ones, with the `HeddleTemplate` item:

```xml
<ItemGroup>
  <!-- disable the default glob and be explicit -->
  <HeddleTemplate Include="Templates/**/*.heddle" />
  <!-- an import-only file: hashed for staleness, but no entry point emitted -->
  <HeddleTemplate Update="Templates/_layout.heddle" Precompile="false" />
  <!-- override the key or generated class name -->
  <HeddleTemplate Update="Templates/Home.heddle" Key="home" Name="HomePage" />
</ItemGroup>
<PropertyGroup>
  <EnableDefaultHeddleTemplates>false</EnableDefaultHeddleTemplates>
</PropertyGroup>
```

## Compile options (MSBuild properties)

These mirror `TemplateOptions` and are baked into the generated artifact; a mismatch against
the runtime request is caught by the validation gauntlet (below). An unparsable value is a
build error (`HED7009`).

| Property | Values / default | Effect |
| --- | --- | --- |
| `HeddleOutputProfile` | `Text` (default) \| `Html` | Output encoding profile (part of the options fingerprint). |
| `HeddleExpressionMode` | `MemberPathsOnly` \| `Native` (default) \| `FullCSharp` | Expression tier. `FullCSharp` is the build‑time equivalent of `AllowCSharp`. |
| `HeddleTrimDirectiveLines` | `false` (default) \| `true` | Trim directive‑only lines (part of the fingerprint). |
| `HeddleMaxRecursionCount` | positive int, default `100` | Definition‑carrier recursion limit, baked at build. |
| `HeddleTemplateRoot` | dir, default `$(MSBuildProjectDirectory)` | Root the template key is made relative to. |
| `HeddleGeneratedNamespace` | default `$(RootNamespace).HeddleTemplates` | Namespace of the generated entry classes. |
| `HeddleEmitUtf8Pieces` | `false` (default) \| `true` | Emit pre‑encoded `"…"u8` static pieces for the byte sink. |

---

## Typed entry points — the recommended host API

Each precompiled template emits a static class (`{HeddleGeneratedNamespace}.{SanitizedName}`,
e.g. `views/home/index.heddle` → `Views_Home_Index`). Call it directly — compile‑checked
model type, no lookup, no validation gauntlet:

```csharp
string html = MyApp.HeddleTemplates.Views_Home_Index.Generate(model);
```

The signature is `Generate(TModel model, object chained = null, object callerData = null)`
where `TModel` is the declared `@model` type (`object` for `:: dynamic`, none for a
model‑less template).

## The registry — for dynamic call sites

Templates identified by a runtime value (a path, a database key) resolve through the registry
instead. Registration is repeatable and idempotent per assembly:

```csharp
using Heddle.Precompiled;

PrecompiledTemplates.Register(typeof(MyApp.Program).Assembly);   // once per assembly
// (HeddleTemplate.Configure(assembly) also registers, for convenience)

// Discovery is a first-class, public API — keys, model types, fingerprints, capabilities:
foreach (var entry in PrecompiledTemplates.Entries)
    Console.WriteLine($"{entry.Key}  model={entry.ModelType}  precompiled={entry.IsPrecompiled}");
```

`TemplateResolver.GetTemplate` consults the registry **before** the dynamic cache and file
check. On a hit it runs the per‑request validation gauntlet; all pass → a `HeddleTemplate` in
precompiled‑adapter mode (zero parse, zero compile). A registry **miss** is never a failure —
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
  `PrecompiledTemplates.OnFallback` callback and an `HED7101` warning naming the reason. A
  silent fallback that quietly re‑adds compile cost is the failure mode this design guards
  against.
- **`Strict`** — an entry that *exists* but fails the gauntlet **throws**
  `PrecompiledMismatchException` (for deployments that must never pay dynamic‑compile cost). A
  plain registry **miss** is not a `Strict` failure — strictness polices divergence, not
  coverage.

An assembly whose manifest schema/engine version is incompatible is ignored wholesale (every
template falls back), with one `HED7102` callback per manifest.

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
override is refused as `HED7015`).

---

## Build‑time diagnostics

Every build‑time condition reports at its `.heddle` position with an `HED7xxx` id:

| Id | Meaning |
| --- | --- |
| `HED7001` | An `AdditionalFiles` `.heddle` source could not be read. |
| `HED7002` | Two templates normalize to the same key. |
| `HED7003` | Two keys differ only by case (warning). |
| `HED7004` | Invalid explicit `Key` metadata. |
| `HED7005` | Unpaired surrogate in static text — the `"…"u8` twin is suppressed (warning). |
| `HED7006` | A named extension resolves to no `[ExtensionName]` type in any reference. |
| `HED7007` | The `@model`/`::` type does not resolve (milestone‑2 native diagnostic). |
| `HED7008` | A member path does not resolve on the model type (milestone‑2 native diagnostic). |
| `HED7009` | An MSBuild option value is unparsable. |
| `HED7010` | Two keys sanitize to one generated class identifier. |
| `HED7011` | An `@<<` import is not among the compilation's `.heddle` `AdditionalFiles`. |
| `HED7012`/`HED7013` | A forwarded front‑end error/warning carrying no id. |
| `HED7014` | A called function is delegate‑only (not precompilable) — the template falls back (warning). |
| `HED7015` | A bound extension overrides a compile‑time hook — unevaluable at build. |

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
