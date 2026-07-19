# C# API Reference

This page documents the public surface used to compile and render templates from C#. The
main types live in the `Heddle` (`IHeddleTemplate`/`HeddleTemplate`), `Heddle.Data`
(`TemplateOptions`/`HeddleCompileResult`/`Scope`), and `Heddle.Runtime` (`CompileContext`)
namespaces:

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

### Streaming: `Generate` into a sink

Alongside the string overload, `HeddleTemplate` renders into a sink with **no full‑output
string materialization** — the structural allocation high‑throughput server‑side rendering
wants to remove:

```csharp
public void Generate(object data, TextWriter writer, object chained = null, object callerData = null);
public void Generate(object data, IBufferWriter<byte> writer, object chained = null, object callerData = null);
```

- The `TextWriter` overload writes characters through to whatever buffering the writer already
  has (a `StreamWriter`, ASP.NET Core's `HttpResponseStreamWriter`, a `StringWriter`).
- The `IBufferWriter<byte>` overload writes **UTF‑8** directly into the writer's own spans — a
  `PipeWriter` qualifies (`PipeWriter : IBufferWriter<byte>`), which is the ASP.NET Core
  `Response.BodyWriter` shape.

Both overloads are **write‑through**: the engine adds no buffering of its own, and it never
flushes, completes, or disposes the sink — **the caller owns the sink's lifecycle.** They share
every compile‑guard exception with the string path, treat a null model the same way, and throw
`ArgumentNullException` when `writer` is null. The string path is unchanged and byte‑identical.

There are **no async render methods** by design. The sanctioned backpressure pattern is the
ecosystem's own — render synchronously, then let the *host* flush asynchronously:

```csharp
// ASP.NET Core minimal API — render straight into the response, host flushes.
app.MapGet("/page", async (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/html; charset=utf-8";
    template.Generate(model, ctx.Response.BodyWriter);   // synchronous, no intermediate string
    await ctx.Response.BodyWriter.FlushAsync();           // the host owns the flush
});
```

For very large pages, flush periodically using `PipeWriter.UnflushedBytes` to bound the buffer
window. The sink is **single‑owner for the duration of the call**: don't touch it from another
thread until `Generate` returns, and never pass one sink to two simultaneous renders. Different
sinks on different threads against the same template are fully supported.

If you cap untrusted renders with a [`RenderBudget`](#render-budgets), note that a breach on a
streaming sink throws `TemplateRenderBudgetException` **after** some output has already been written
through — the engine buffers nothing. Treat the exception as "abort the response": stop writing and
don't flush the partial output as if it were complete.

> **Source‑compatibility note.** A call written as the positional literal
> `template.Generate(data, null)` is now **CS0121** (ambiguous between the `TextWriter` and
> `IBufferWriter<byte>` overloads — `null` is equally convertible to both). This is a
> compile‑time‑only, self‑announcing change; fix it with `Generate(data)` or
> `Generate(data, chained: null)`. Binary compatibility is unaffected — existing assemblies keep
> binding the string overload — and any argument statically typed `object` still exact‑matches it.

### Compilation methods

| Method | Description |
| --- | --- |
| `HeddleCompileResult Compile(CompileContext context)` | Compile the file described by `context.Options`; optionally starts a file watcher (see below). |
| `HeddleCompileResult Compile(string document, ExType modelType = null)` | Compile an inline string. |
| `HeddleCompileResult Recompile(string newDocument, CompileContext context = null)` | Replace the current template with a new string. |
| `HeddleCompileResult Recompile(ExType newModelType)` | Recompile the current source against a new model type. |
| `HeddleCompileResult TryCompilation(CompileContext context)` | **Dry run**: runs the parse and extension/type‑check passes but discards the compiled output. |
| `HeddleCompileResult TryCompilation(string document, TemplateOptions options = null, ExType modelType = null)` | Dry run for an inline string. |

`TryCompilation` is useful for CI/linting — it tells you whether a template's parse and
extension/type‑check passes succeed, without producing a renderer. Note it does **not** run
the deferred‑finalization or embedded‑C# (Roslyn) steps, so a `FullCSharp` template (or one
whose delayed subtemplate fails finalization) can pass the dry run and still fail a real
`Compile`. A template can only be compiled once per instance; compiling an
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
template installs an **armed** `FileSystemWatcher` on the file it actually read
(`TemplateName + FileNamePostfix`, e.g. `home.heddle`) and exposes these events:

```csharp
template.OnFileChanged += (s, e) => { /* file changed or (re)created; recompile follows */ };
template.OnFileDeleted += (s, e) => { /* file deleted; the last-good document stays renderable */ };
template.OnFileRenamed += (s, e) => { /* a rename ONTO the watched name recompiles (atomic saves) */ };
```

An in‑place edit, a create/re‑create of the watched file, or a rename **onto** the watched name
(the atomic‑save idiom: write a temp file, rename it over the target) recompiles the template
into a document equivalent to a fresh compile of the new content **honoring the original
options** — model type, `ExpressionMode`, `OutputProfile`, `Encoder`, `RenderBudget`,
`TrimDirectiveLines`, `Functions`, `MaxRecursionCount` — and subsequent renders reflect it. A
delete (or a rename away from the watched name) does not recompile: the last successfully
compiled document stays renderable. A **failed** recompile — an edit that no longer compiles, or
a transient read failure — also keeps the last‑good document published and surfaces the error on
`CompileResult` (`Success == false`, `ErrorList` populated); a subsequent good save recovers. An
empty/whitespace‑only save (a common mid‑write editor truncation) is a no‑op that keeps the
last‑good document. The event `sender` is always the `HeddleTemplate` itself, never the internal
watcher. In‑flight renders during a reload observe a coherent old‑or‑new document, and the
superseded document is released once no render holds it.

> **Scope.** File watching is a **local development‑time** reload feature built on
> `FileSystemWatcher`, and inherits its platform limits: no reliable delivery on network/UNC
> drives, possible event loss when the internal buffer overflows under change bursts, and
> OS‑dependent file‑name case sensitivity. Only the root template file is watched — an edit to
> an `@<<`‑imported file does not trigger a reload. Rapid saves of a `FullCSharp` template each
> load a fresh generated assembly (never unloaded until process exit).

### Disposal

`HeddleTemplate` owns its compiled runtime document and any file watcher. Dispose it when done.
Disposal is **best‑effort deferred** while a render is in progress (it aims to dispose once the
last in‑flight `Generate` returns); the in‑flight counter is not fully synchronized, so avoid
disposing an instance that is still being rendered concurrently on other threads.

---

## `TemplateOptions`

Controls where templates are read from and which features are enabled
([TemplateOptions.cs](../src/Heddle/Data/TemplateOptions.cs)).

| Property | Default | Purpose |
| --- | --- | --- |
| `RootPath` | `AppContext.BaseDirectory` | Base directory for template files and imports/partials. |
| `TemplateName` | `""` (ctor arg) | The template's name (file stem). Set via `new TemplateOptions("name")`. |
| `FileNamePostfix` | `""` | Suffix appended to the name, e.g. `".heddle"`. |
| `ExpressionMode` | `Native` | Selects the expression tier: `MemberPathsOnly`, `Native` (sandbox‑safe operators/functions — see [Native Expressions](native-expressions.md)), or `FullCSharp` (adds the inner‑`@` Roslyn tier). |
| `Functions` | `null` (= `FunctionRegistry.Default`) | Functions callable from native expressions; see [Native Expressions](native-expressions.md#registered-functions). |
| `OutputProfile` | `Html` | Selects whether the unnamed `@(...)` output HTML‑encodes by default: `Html` (bodiless `@(value)` encodes; `@raw` opts out) — the default since 2.0 — or `Text` (raw output; the 1.x‑compatibility setting). Inherited by bodies/partials/imports; also settable per template with [`@profile()`](built-in-extensions.md#profile). See [Output profiles](language-reference.md#output-profiles). Participates in `Equals`/`GetHashCode`. |
| `TrimDirectiveLines` | `true` | When `true` (the default since 2.0), a whole‑line directive that produces no output (`@using`, `@model`, `@profile(){{…}}`, `@% … %@` definitions, `@<<` imports, whole‑line comments, and any extension whose `InitStart` returns `null`) swallows its line — leading indentation, trailing spaces, and one line terminator. Inherited by child compiles. Set `false` to keep 1.x whitespace byte‑exact. Participates in `Equals`/`GetHashCode` (it changes rendered bytes, so it keys template caches). Compile‑time only. See [Whitespace trimming](language-reference.md#whitespace-trimming-). |
| `Encoder` | `null` (legacy `WebUtility.HtmlEncode`) | The output encoder used at HTML‑encoding sites — bare `@(value)` under `OutputProfile.Html` and `[EncodeOutput]` extensions. A `System.Text.Encodings.Web.TextEncoder`; `null` (the default) keeps the built‑in legacy path (`WebUtility.HtmlEncode`, byte‑identical to 2.0.0). Set `HtmlEncoder.Create(UnicodeRanges.All)` for a modern Unicode‑aware encoder, or supply a `JavaScriptEncoder`/`UrlEncoder`/custom `TextEncoder`. Not applied to `@raw`, raw blocks, literal text, or `OutputProfile.Text`. Participates in `Equals`/`GetHashCode` **by reference** (a different encoder instance renders different bytes, so it keys template caches). See [encoding contexts](built-in-extensions.md#encoding-contexts). |
| `AllowCSharp` | `false` | **Obsolete** bridge over `ExpressionMode` (use `ExpressionMode` directly): `true` == `FullCSharp`. Enables embedded C# (`@( @expr )`, `@new`, LINQ, typed `@model()`). Setting `false` leaves `MemberPathsOnly` untouched, otherwise selects `Native`. Reads and writes keep working; new code sets `ExpressionMode`. |
| `MaxRecursionCount` | `100` | Upper bound on definition recursion depth. |
| `RenderBudget` | `null` (unlimited) | Per‑render resource caps for untrusted templates: `RenderBudget.MaxOutputChars`, `MaxRenderOps`, and `MaxRenderTime` (each nullable — a null limit is unbounded). `null` (the default) is today's unlimited behavior with **zero render‑path cost** (no wrapper is created). A breach throws `TemplateRenderBudgetException`. Does **not** participate in `Equals`/`GetHashCode` (it changes no bytes of a successful render — same rule as `MaxRecursionCount`). See [Render budgets](#render-budgets). |
| `EnableFileChangeCheck` | `false` | Install an armed `FileSystemWatcher` on the source file and recompile on change/create/rename‑onto‑target (see [File watching](#file-watching)). |
| `ProvideLanguageFeatures` | `false` | Parse in a tooling mode that emits a token list for editors/highlighters (used by the IDE integrations). |
| `Data` | `null` | Optional ambient data carried on the options. |

The file actually read is `Path.Combine(RootPath, TemplateName + FileNamePostfix)`. The
`FullPath` property is a plain string concatenation (`RootPath + TemplateName + FileNamePostfix`,
with no path separator) and is not the resolved read path. A non‑empty `FileNamePostfix` is
**required** for a file compile — `FileReader` throws if it is empty, so the `""` default cannot
be used to compile from a file.

```csharp
var options = new TemplateOptions("home")
{
    RootPath = "Heddle",
    FileNamePostfix = ".heddle",     // resolves Heddle/home.heddle
};
```

`TemplateOptions` implements value equality over (`TemplateName`, `RootPath`,
`FileNamePostfix`, `OutputProfile`, `TrimDirectiveLines`, `Encoder`), so it can be used as a
cache key — two option sets that differ only by profile, by trimming, *or* by encoder instance
(compared by reference) are distinct keys (they render different bytes).

**Running untrusted templates?** `ExpressionMode.Native`, a curated `FunctionRegistry`,
purpose‑built DTO models, `OutputProfile.Html`, and a `RenderBudget` together form the sandbox; see
[Exposing models to untrusted templates](patterns.md#exposing-models-to-untrusted-templates)
for the full host‑side checklist.

### Render budgets

The compile‑time sandbox (`ExpressionMode.Native` + a frozen `FunctionRegistry`) stops an untrusted
template from *reading* or *calling* what it shouldn't, but a well‑formed template can still exhaust
CPU or memory — `@for(1000000000){{x}}` compiles cleanly. A `RenderBudget` is the **availability**
leg: attach one to `TemplateOptions.RenderBudget` and every `Generate` call for that template is
bounded.

```csharp
var options = new TemplateOptions
{
    ExpressionMode = ExpressionMode.Native,
    RenderBudget = new RenderBudget
    {
        MaxOutputChars = 1_000_000,           // cumulative UTF‑16 chars accepted by the sink
        MaxRenderOps   = 5_000_000,           // cumulative render‑write operations
        MaxRenderTime  = TimeSpan.FromMilliseconds(250),   // wall‑clock from Generate entry
    },
};
```

**Semantics.** All three limits are nullable; a null limit is unbounded, and a `null` budget (the
default) imposes no cap and adds **no render‑path cost** — the enforcement wrapper is only created
when a budget is present. Enforcement lives at the renderer seam (not the language level), so the
counts are uniform across the string, `TextWriter`, and `IBufferWriter` sinks and identical on the
dynamic and precompiled backends:

- **`MaxOutputChars`** — cumulative UTF‑16 characters accepted by the sink across the whole
  `Generate` call, counted *before* any UTF‑8 transcode (chars, not bytes).
- **`MaxRenderOps`** — cumulative render‑write operations (each write that reaches the sink counts
  as one).
- **`MaxRenderTime`** — wall‑clock from `Generate` entry, measured via `Stopwatch.GetTimestamp()`
  deltas (no timer thread) on every render op **and at least once per `@list`/`@for` iteration`**.

A breach throws `TemplateRenderBudgetException` (in `Heddle.Exceptions`) carrying `Kind`
(`OutputChars` / `RenderOps` / `RenderTime`), `Limit`, and `Observed`. The exception carries **no
template position** — the renderer seam has none, and recording positions per op would tax the hot
path (an accepted deviation).

**Evasion analysis.** Because enforcement is at the seam, the budget counts *render operations*, not
*loop iterations*: a loop iteration that writes nothing is invisible to the ops and chars counters.
Two cases escape `MaxOutputChars`/`MaxRenderOps` entirely and are bounded **only** by
`MaxRenderTime`: (1) a *zero‑output loop* (`@for(1000000000){{@if(false){{…}}}}`) never reaches the
sink, so no op or char is ever counted; and (2) *value‑context accumulation* — a `@for`/`@list`
evaluated in value context (its result feeds another extension rather than the sink) builds the whole
string in memory and reaches the sink as a **single** write op, so the char/op counters only see the
finished result, never the unbounded work and memory spent building it. For both, the loop extensions
check the deadline once per iteration (in the value path too), so the render still terminates on the
deadline even though the ops counter never moves. `MaxRenderTime` is therefore the **only universal
backstop**; `MaxOutputChars`/`MaxRenderOps` cap *delivered* response size and write count but do not
bound zero‑output loops or in‑memory value accumulation. For a hard bound against runaway templates,
`MaxRenderTime` is mandatory — set it for every untrusted render.

**Streaming caveat.** On the `TextWriter`/`IBufferWriter` sinks, whatever was written before the
breach **stays written** — the engine is write‑through and the caller owns the sink. Treat
`TemplateRenderBudgetException` as *abort the response*: stop, and do not treat the partial output as
complete. (See also [Streaming](#streaming-generate-into-a-sink).)

### Choosing the output profile per template

The profile is selected programmatically — there is **no** file‑extension inference. A host
that wants extension‑driven profiles maps extensions to options in its own resolver wiring:

```csharp
OutputProfile ProfileFor(string path) =>
    Path.GetExtension(path) is ".html" or ".htm" or ".xml"
        ? OutputProfile.Html
        : OutputProfile.Text;

var options = new TemplateOptions(Path.GetFileNameWithoutExtension(path))
{
    RootPath = root,
    FileNamePostfix = Path.GetExtension(path),
    OutputProfile = ProfileFor(path)
};
```

`TemplateResolver` also accepts a default profile and trimming setting:
`new TemplateResolver(rootPath, checkFileChange, OutputProfile.Html)` and the four‑parameter
`new TemplateResolver(rootPath, checkFileChange, OutputProfile.Html, trimDirectiveLines: true)`.
The two‑parameter constructor uses the 2.0 defaults `(Html, true)`; a per‑call
`CompileContext`'s options override both resolver defaults, and the resolver caches each
template under a key suffixed by profile **and** trimming so one resolver can serve every
combination without a collision.

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
| `OutputProfile OutputProfile` | The effective output profile for items compiled from here on. Initialized from `Options.OutputProfile`; flipped by `@profile()`; snapshotted by child contexts. Compile‑time only. |
| `ExType ScopeType` | The current model type. May be changed by the `@model()` extension during compile. |
| `ExType RootScopeType` | The root (level‑0) model type. |
| `bool Compiled` | Whether compile finalization (delayed‑extension completion) has run. |
| `List<HeddleCompileError> CompileErrors` | Accumulated errors. |
| `List<HeddleCompileWarning> CompileWarnings` | Accumulated warnings (e.g. an `HED2002` profile‑after‑output warning). |

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
| `string Document` | The source text that was submitted for compilation. |
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
| `Renderer` | The output sink. |

The struct also carries `RootData` (the root model — what `::Member` / `@root` resolve
against), but it is `internal`: it describes the engine semantics only and is not part of the
public surface external extensions can read.

`Scope` exposes pure transformations (`Parent()`, `Chain()`, `Model()`, …) that extensions use
to descend into elements or swap model/chained values. See
[Writing Custom Extensions](custom-extensions.md) for how this is used.

---

## Errors and diagnostics

- **Compile errors** are collected (not thrown) and surfaced through `CompileResult` — check
  `Success` and read `ErrorList` / `ToString()`.
- **Parse errors** (malformed syntax) are reported by the ANTLR error listener and appear as
  errors with positions. The parser first tries fast SLL prediction and falls back to full LL
  diagnostics on ambiguity, recording a warning when it does — that SLL→LL warning lands on
  `ParseContext.Warnings` (reachable via `CompileResult.Context.Warnings`), not
  `CompileContext.CompileWarnings` (see [Architecture](architecture.md#2-parsing)).
- **Render errors** surface as exceptions from `Generate`
  (`TemplateInitException`, `TemplateCompileException`, and — in DEBUG —
  `TemplateProcessingException` for a model‑type mismatch).

## End‑to‑end example

```csharp
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;   // CompileContext

HeddleTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);

var options = new TemplateOptions("home")
{
    RootPath = "Heddle",
    FileNamePostfix = ".heddle",     // resolves Heddle/home.heddle
};

using var template = new HeddleTemplate(new CompileContext(options));
if (!template.CompileResult.Success)
    throw new InvalidOperationException(template.CompileResult.ToString());

string html = template.Generate(myBlog);
```

---

## Build‑time pre‑compilation

Templates can be pre‑compiled into your assembly at build time so they are **looked up, not
parsed and compiled** at run time — the Razor compiled‑views shape. The runtime types above are
unchanged and remain the semantic reference; the precompiled backend produces byte‑identical
output. The additions live in the `Heddle.Precompiled` namespace:

- `PrecompiledTemplates` — the process‑wide registry: `Register(assembly)` (repeatable,
  idempotent), `Entries` (host‑visible discovery), `TryGet(key, out …)`, the `OnFallback`
  diagnostic callback, and the `BindingResolver` hook.
- `PrecompiledMismatchPolicy` on `TemplateOptions` — `Fallback` (default: recompile + warn) or
  `Strict` (throw when a precompiled entry exists but cannot be plugged).
- Typed entry points — `Heddle.Generated.{Name}.Generate(model)` (the default namespace is
  `Heddle.Generated`, overridable via `HeddleGeneratedNamespace`) — the
  recommended host API for templates known at compile time (compile‑checked model, no lookup).
  Each typed entry also gains the two **sink overloads** — `Generate(model, TextWriter)` and
  `Generate(model, IBufferWriter<byte>)` — mirroring `HeddleTemplate` (same no‑materialization
  contract, same `Generate(model, null)` CS0121 corner). Building with `HeddleEmitUtf8Pieces=true`
  emits pre‑encoded `"…"u8` static pieces that flow to the byte sink with zero transcoding.

See [Build‑Time Pre‑compilation](precompilation.md) for the generator package, the MSBuild
options, the validation gauntlet, `[ExportFunctions]` binding, and the `heddle` codegen CLI.

## Advanced: disabling the C# tier for trimmed hosts

Hosts that never use the Roslyn C# expression tier (`ExpressionMode.FullCSharp` / `AllowCSharp`) — for
example a trimmed WebAssembly bundle — can drop the entire `Microsoft.CodeAnalysis` dependency graph with the
`Heddle.CSharpTierEnabled` **feature switch**:

- **Runtime:** `AppContext.SetSwitch("Heddle.CSharpTierEnabled", false)` at startup. Templates that require the
  C# tier then fail compilation with **HED9001** (collected on the compile result, never thrown); native
  expressions, member paths, functions, and every other feature are unaffected. Unset (the default) reads as
  `true`, so ordinary hosts see identical behavior.
- **Trimmed publish:** add
  `<RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" />`. The
  linker then substitutes the switch to a constant, drops the guarded Roslyn branches, and removes
  `Microsoft.CodeAnalysis.*` from the output entirely. This is exactly how the in-browser demo ships Roslyn-free.
