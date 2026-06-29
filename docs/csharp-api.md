# C# API Reference

This page documents the public surface used to compile and render templates from C#. The
main types live in the `Heddle` namespace:

- [`IHeddleTemplate`](../src/Heddle/IHeddleTemplate.cs) / [`HeddleTemplate`](../src/Heddle/HeddleTemplate.cs) — compile and render.
- [`TemplateOptions`](../src/Heddle/Data/TemplateOptions.cs) — where to read templates and which features are on.
- [`CompileContext`](../src/Heddle/Runtime/CompileContext.cs) — the model type and compilation state.
- [`HeddleCompileResult`](../src/Heddle/Data/HeddleCompileResult.cs) — success/errors with source positions.
- [`Scope`](../src/Heddle/Data/Scope.cs) — the data view extensions see while rendering.

The library targets `netstandard2.0;net6.0;net8.0;net10.0`
([Heddle.csproj](../src/Heddle/Heddle.csproj)).

---

## `HeddleTemplate`

The concrete engine entry point ([HeddleTemplate.cs](../src/Heddle/HeddleTemplate.cs)). It is
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
HeddleTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);
```

### Constructors

| Constructor | Use |
| --- | --- |
| `HeddleTemplate()` | Empty; compile later with `Compile`/`TryCompilation`/`Recompile`. |
| `HeddleTemplate(string document, CompileContext context = null)` | Compile an inline template string. |
| `HeddleTemplate(CompileContext context)` | Compile a **file** described by `context.Options` (`RootPath`+`TemplateName`+`FileNamePostfix`). |
| `HeddleTemplate(TemplateOptions options)` | Shorthand for `new HeddleTemplate(new CompileContext(options))`. |
| `HeddleTemplate(TemplateOptions options, ExType modelType)` | As above, with an explicit model type. |

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
| `HeddleCompileResult Compile(CompileContext context)` | Compile the file described by `context.Options`; optionally starts a file watcher (see below). |
| `HeddleCompileResult Compile(string document, ExType modelType = null)` | Compile an inline string. |
| `HeddleCompileResult Recompile(string newDocument, CompileContext context = null)` | Replace the current template with a new string. |
| `HeddleCompileResult Recompile(ExType newModelType)` | Recompile the current source against a new model type. |
| `HeddleCompileResult TryCompilation(CompileContext context)` | **Dry run**: parse + type‑check a file but discard the compiled output. |
| `HeddleCompileResult TryCompilation(string document, TemplateOptions options = null, ExType modelType = null)` | Dry run for an inline string. |

`TryCompilation` is ideal for CI/linting — it tells you whether a template *would* compile
without producing a renderer. A template can only be compiled once per instance; compiling an
already‑compiled instance throws `TemplateInitException`.

### State properties

| Member | Meaning |
| --- | --- |
| `HeddleCompileResult CompileResult` | Result of the most recent compile. |
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

`HeddleTemplate` owns its compiled runtime document and any file watcher. Dispose it when done.
Disposal is deferred safely if a render is in progress (it disposes once the last concurrent
`Generate` returns).

---

## `TemplateOptions`

Controls where templates are read from and which features are enabled
([TemplateOptions.cs](../src/Heddle/Data/TemplateOptions.cs)).

| Property | Default | Purpose |
| --- | --- | --- |
| `RootPath` | `AppContext.BaseDirectory` | Base directory for template files and imports/partials. |
| `TemplateName` | `""` (ctor arg) | The template's name (file stem). Set via `new TemplateOptions("name")`. |
| `FileNamePostfix` | `""` | Suffix appended to the name, e.g. `".heddle"`. |
| `AllowCSharp` | `false` | Enables embedded C# (`@(...)` expressions, `@new`, LINQ, typed `@model()`). Required for most non‑trivial templates. |
| `MaxRecursionCount` | `100` | Upper bound on definition recursion depth. |
| `EnableFileChangeCheck` | `false` | Install a `FileSystemWatcher` and recompile on file change. |
| `ProvideLanguageFeatures` | `false` | Parse in a tooling mode that emits a token list for editors/highlighters (used by the IDE integrations). |
| `Data` | `null` | Optional ambient data carried on the options. |

The resolved file path is `RootPath + TemplateName + FileNamePostfix` (`FullPath`).

```csharp
var options = new TemplateOptions("home")
{
    RootPath = "Heddle",
    FileNamePostfix = ".heddle",     // resolves Heddle/home.heddle
    AllowCSharp = true
};
```

`TemplateOptions` implements value equality over (`TemplateName`, `RootPath`,
`FileNamePostfix`), so it can be used as a cache key.

---

## `CompileContext`

Holds the model type and shared compilation state
([CompileContext.cs](../src/Heddle/Runtime/CompileContext.cs)). You usually create one from
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
| `bool Compiled` | Whether the embedded‑C# (Roslyn) compilation step has run. |
| `List<HeddleCompileError> CompileErrors` | Accumulated errors. |
| `List<HeddleCompileWarning> CompileWarnings` | Accumulated warnings (e.g. SLL→LL fallback). |

`ExType` is the engine's type wrapper; it can be constructed from a `System.Type`
(implicitly), or from a type *name* plus a set of imported namespaces (this is how
`@model(){{TypeName}}` and `:: Type` resolve). `ExType.Dynamic` represents `dynamic`.

---

## `HeddleCompileResult`

The outcome of a compile ([HeddleCompileResult.cs](../src/Heddle/Data/HeddleCompileResult.cs)).

| Member | Meaning |
| --- | --- |
| `bool Success` | Whether compilation succeeded. |
| `IReadOnlyCollection<HeddleCompileError> ErrorList` | Errors, each with a resolved line/column position. |
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

Extensions receive a [`Scope`](../src/Heddle/Data/Scope.cs) — a small readonly struct that
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
using Heddle;
using Heddle.Data;

HeddleTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);

var options = new TemplateOptions("home")
{
    RootPath = "Heddle",
    FileNamePostfix = ".heddle",     // resolves Heddle/home.heddle
    AllowCSharp = true
};

using var template = new HeddleTemplate(new CompileContext(options));
if (!template.CompileResult.Success)
    throw new InvalidOperationException(template.CompileResult.ToString());

string html = template.Generate(myBlog);
```
