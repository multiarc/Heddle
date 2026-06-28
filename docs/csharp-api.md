# C# API Reference

This page documents the public surface used to compile and render templates from C#. The
main types live in the `Templates` namespace:

- [`ITtlTemplate`](../src/Templates/ITtlTemplate.cs) / [`TtlTemplate`](../src/Templates/TtlTemplate.cs) — compile and render.
- [`TemplateOptions`](../src/Templates/Data/TemplateOptions.cs) — where to read templates and which features are on.
- [`CompileContext`](../src/Templates/Runtime/CompileContext.cs) — the model type and compilation state.
- [`TtlCompileResult`](../src/Templates/Data/TtlCompileResult.cs) — success/errors with source positions.
- [`Scope`](../src/Templates/Data/Scope.cs) — the data view extensions see while rendering.

The library targets `netstandard2.0;net6.0;net8.0`
([Templates.csproj](../src/Templates/Templates.csproj)).

---

## `TtlTemplate`

The concrete engine entry point ([TtlTemplate.cs](../src/Templates/TtlTemplate.cs)). It is
`sealed` and implements `IDisposable`. A single instance is compiled once and can be rendered
many times, including concurrently.

### Registration: `Configure`

```csharp
public static void Configure(Assembly startupAssembly);
```

Scans `startupAssembly` (and its references) for extensions and registers them. Call once at
startup, passing your application's assembly so custom extensions are discovered. The built‑in
extensions are registered automatically.

```csharp
TtlTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);
```

### Constructors

| Constructor | Use |
| --- | --- |
| `TtlTemplate()` | Empty; compile later with `Compile`/`TryCompilation`/`Recompile`. |
| `TtlTemplate(string document, CompileContext context = null)` | Compile an inline template string. |
| `TtlTemplate(CompileContext context)` | Compile a **file** described by `context.Options` (`RootPath`+`TemplateName`+`FileNamePostfix`). |
| `TtlTemplate(TemplateOptions options)` | Shorthand for `new TtlTemplate(new CompileContext(options))`. |
| `TtlTemplate(TemplateOptions options, ExType modelType)` | As above, with an explicit model type. |

Constructors that take a context compile immediately and set `CompileResult`. Always check
`CompileResult.Success` before rendering.

### Rendering: `Generate`

```csharp
public string Generate(object data, object chained = null, object callerData = null);
```

Renders the compiled template and returns the result string.

- `data` — the model (the root model for the whole render).
- `chained` — seeds the top‑level chained value (what `@out()` sees at the root).
- `callerData` — extra caller context carried alongside in [`Scope`](#scope-the-data-view-during-rendering).

Behavior notes:

- Throws `TemplateInitException` if you never compiled, or `TemplateCompileException` if
  compilation failed — guard with `CompileResult.Success`.
- In **`DEBUG`** builds, `Generate` validates that `data`'s type matches the compiled model
  type and throws `TemplateProcessingException` on a mismatch. Release builds skip this check.
- The output buffer auto‑sizes: each call grows an internal capacity estimate (with ~10 %
  margin) so repeated renders avoid reallocations.

```csharp
string html = template.Generate(model);
```

### Compilation methods

| Method | Description |
| --- | --- |
| `TtlCompileResult Compile(CompileContext context)` | Compile the file described by `context.Options`; optionally starts a file watcher (see below). |
| `TtlCompileResult Compile(string document, ExType modelType = null)` | Compile an inline string. |
| `TtlCompileResult Recompile(string newDocument, CompileContext context = null)` | Replace the current template with a new string. |
| `TtlCompileResult Recompile(ExType newModelType)` | Recompile the current source against a new model type. |
| `TtlCompileResult TryCompilation(CompileContext context)` | **Dry run**: parse + type‑check a file but discard the compiled output. |
| `TtlCompileResult TryCompilation(string document, TemplateOptions options = null, ExType modelType = null)` | Dry run for an inline string. |

`TryCompilation` is ideal for CI/linting — it tells you whether a template *would* compile
without producing a renderer. A template can only be compiled once per instance; compiling an
already‑compiled instance throws `TemplateInitException`.

### State properties

| Member | Meaning |
| --- | --- |
| `TtlCompileResult CompileResult` | Result of the most recent compile. |
| `bool Compiled` | True once a runtime document exists. |
| `bool Empty` | True if the compiled template produces no output. |
| `CompileContext Context` | The context the template was compiled with. |

### File watching

When `TemplateOptions.EnableFileChangeCheck` is `true` and you compiled from a file, the
template installs a `FileSystemWatcher` and recompiles on change. Subscribe to these events:

```csharp
template.OnFileChanged += (s, e) => { /* recompiled */ };
template.OnFileDeleted += (s, e) => { /* … */ };
template.OnFileRenamed += (s, e) => { /* … */ };
```

### Disposal

`TtlTemplate` owns its compiled runtime document and any file watcher. Dispose it when done.
Disposal is deferred safely if a render is in progress (it disposes once the last concurrent
`Generate` returns).

---

## `TemplateOptions`

Controls where templates are read from and which features are enabled
([TemplateOptions.cs](../src/Templates/Data/TemplateOptions.cs)).

| Property | Default | Purpose |
| --- | --- | --- |
| `RootPath` | `AppContext.BaseDirectory` | Base directory for template files and imports/partials. |
| `TemplateName` | `""` (ctor arg) | The template's name (file stem). Set via `new TemplateOptions("name")`. |
| `FileNamePostfix` | `""` | Suffix appended to the name, e.g. `".thtml"`. |
| `AllowCSharp` | `false` | Enables embedded C# (`@(...)` expressions, `@new`, LINQ, typed `@model()`). Required for most non‑trivial templates. |
| `MaxRecursionCount` | `100` | Upper bound on definition recursion depth. |
| `EnableFileChangeCheck` | `false` | Install a `FileSystemWatcher` and recompile on file change. |
| `ProvideLanguageFeatures` | `false` | Parse in a tooling mode that emits a token list for editors/highlighters (used by the IDE integrations). |
| `Data` | `null` | Optional ambient data carried on the options. |

The resolved file path is `RootPath + TemplateName + FileNamePostfix` (`FullPath`).

```csharp
var options = new TemplateOptions("template")
{
    RootPath = "TestTemplate",
    FileNamePostfix = ".thtml",
    AllowCSharp = true
};
```

`TemplateOptions` implements value equality over (`TemplateName`, `RootPath`,
`FileNamePostfix`), so it can be used as a cache key.

---

## `CompileContext`

Holds the model type and shared compilation state
([CompileContext.cs](../src/Templates/Runtime/CompileContext.cs)). You usually create one from
`TemplateOptions`; the engine creates nested contexts internally for subtemplates and partials.

```csharp
public CompileContext(ExType modelType = null);
public CompileContext(TemplateOptions options, ExType modelType = null);
```

Key members:

| Member | Meaning |
| --- | --- |
| `TemplateOptions Options` | The options this context compiles with. |
| `ExType ScopeType` | The current model type. May be changed by the `@model()` extension during compile. |
| `ExType RootScopeType` | The root (level‑0) model type. |
| `bool Compiled` | Whether the Roslyn compilation has run. |
| `List<TtlCompileError> CompileErrors` | Accumulated errors. |
| `List<TtlCompileWarning> CompileWarnings` | Accumulated warnings (e.g. SLL→LL fallback). |
| `string ControllerName` | Used by the MVC integration to resolve views. |

`ExType` is the engine's type wrapper; it can be constructed from a `System.Type`
(implicitly), or from a type *name* plus a set of imported namespaces (this is how
`@model(){{TypeName}}` and `:: Type` resolve). `ExType.Dynamic` represents `dynamic`.

---

## `TtlCompileResult`

The outcome of a compile ([TtlCompileResult.cs](../src/Templates/Data/TtlCompileResult.cs)).

| Member | Meaning |
| --- | --- |
| `bool Success` | Whether compilation succeeded. |
| `IReadOnlyCollection<TtlCompileError> ErrorList` | Errors, each with a resolved line/column position. |
| `string Document` | The (optimized) source that was compiled. |
| `ParseContext Context` | The parse tree/context (tokens, definitions, output chains). |
| `override string ToString()` | A newline‑joined, human‑readable error report. |

```csharp
var result = template.CompileResult;
if (!result.Success)
{
    foreach (var error in result.ErrorList)
        Console.Error.WriteLine(error);   // includes line/column
    // or simply:
    Console.Error.WriteLine(result.ToString());
}
```

Each error carries a `LinePosition` (line, column offset, length) computed from the source, so
you can surface precise diagnostics in editors or logs.

---

## `Scope` (the data view during rendering)

Extensions receive a [`Scope`](../src/Templates/Data/Scope.cs) — a small readonly struct that
carries the values visible at a point in the render. Template authors don't use it directly,
but it explains the semantics of `@(Name)`, `::`, `@out()`, and the loop index.

| Field | Meaning |
| --- | --- |
| `ModelData` | The current model — what `@(Name)` resolves against. |
| `ChainedData` | The chained value — what `@out()` emits and what a chain passes along (also the loop index for `@for`/`@list`). |
| `ParentModelData` | The enclosing scope's model. |
| `CallerData` | Caller context (the `callerData` passed to `Generate`). |
| `RootData` | The root model — what `::Member` / `@root` resolve against. |
| `Renderer` | The output sink. |

`Scope` exposes pure transformations (`Parent()`, `Chain()`, `Model()`, …) that extensions use
to descend into elements or swap model/chained values. See
[Writing Custom Extensions](custom-extensions.md) for how this is used.

---

## Errors and diagnostics

- **Compile errors** are collected (not thrown) and surfaced through `CompileResult` — check
  `Success` and read `ErrorList` / `ToString()`.
- **Parse errors** (malformed syntax) are reported by the ANTLR error listener and appear as
  errors with positions. The parser first tries fast SLL prediction and falls back to full LL
  diagnostics on ambiguity, recording a warning when it does (see
  [Architecture](architecture.md#2-parsing)).
- **Render errors** surface as exceptions from `Generate`
  (`TemplateInitException`, `TemplateCompileException`, and — in DEBUG —
  `TemplateProcessingException` for a model‑type mismatch).

## End‑to‑end example

```csharp
using System.Reflection;
using Templates;
using Templates.Data;

TtlTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);

var options = new TemplateOptions("template")
{
    RootPath = "TestTemplate",
    FileNamePostfix = ".thtml",
    AllowCSharp = true
};

using var template = new TtlTemplate(new CompileContext(options));
if (!template.CompileResult.Success)
    throw new InvalidOperationException(template.CompileResult.ToString());

string html = template.Generate(myModel);
```

For ASP.NET Core MVC, you typically don't call `TtlTemplate` directly — register
[`TtlViewEngine`](mvc-integration.md) and return views from controllers instead.
