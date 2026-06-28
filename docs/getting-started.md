# Getting Started

This page gets you from zero to a rendered template. It assumes you are integrating the
engine from C#. If you only want to learn the template syntax, jump to the
[Language Reference](language-reference.md).

## Prerequisites

- **.NET SDK 8.0** (the repository pins `8.0.0` with `rollForward: latestMinor` in
  [global.json](../global.json)). The core library itself targets
  `netstandard2.0;net6.0;net8.0`, so the compiled package runs on a wide range of hosts.
- A reference to the **`Templates`** package (and **`Templates.Mvc`** if you use ASP.NET Core MVC).

To build the engine from source, see [Building & Testing](building.md).

## The three steps

Using the engine always follows the same shape:

1. **Configure** — register the extensions found in your assemblies (once per process).
2. **Compile** — parse a template string or file into a `TtlTemplate`.
3. **Generate** — render the compiled template against a data object, as many times as you like.

### 1. Configure (register extensions)

`TtlTemplate.Configure` scans the given assembly (and assemblies it references) for exported
extensions. The built‑in extensions in the `Templates` assembly are picked up automatically;
call `Configure` with *your* startup assembly so any custom extensions you wrote are
discovered too.

```csharp
using System.Reflection;
using Templates;

TtlTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);
```

See [Writing Custom Extensions](custom-extensions.md) for how extensions are exported with
`[assembly: ExportExtensions]`.

### 2 & 3. Compile and generate an inline template

```csharp
using System.Dynamic;
using Templates;
using Templates.Data;

var options = new TemplateOptions
{
    AllowCSharp = true        // required for @(...) C# expressions and typed @model()
};

// A template that declares one definition named "default" with a default output chain,
// typed as `dynamic`. The "-> (Model)" picks Model off the incoming object as the body data.
var source =
    "@%\n" +
    "<default> -> (Model)\n" +
    "{{ Order #@(Id)! }} :: dynamic\n" +
    "%@";

using var template = new TtlTemplate(source, new CompileContext(options));

if (!template.CompileResult.Success)
    throw new Exception(template.CompileResult.ToString());   // human‑readable error list

dynamic model = new ExpandoObject();
model.Model = new { Id = 100 };

string result = template.Generate(model);
// result == " Order #100! "
```

This mirrors the `SubjectDynamicTest` integration test in
[TtlTemplateTests.cs](../src/Templates.Tests/TtlTemplateTests.cs).

> **`AllowCSharp`** — embedded C# (`@( ... )` expressions, `@new T{...}`, LINQ, typed
> `@model()`) is only enabled when `TemplateOptions.AllowCSharp` is `true`. Leave it `false`
> for untrusted templates that should not run arbitrary C#.

### Compile from a file

Point `TemplateOptions` at a directory and a template name; the engine reads
`RootPath + TemplateName + FileNamePostfix`:

```csharp
var options = new TemplateOptions("template")   // TemplateName = "template"
{
    RootPath = "TestTemplate",
    FileNamePostfix = ".thtml",                 // resolves TestTemplate/template.thtml
    AllowCSharp = true
};

using var template = new TtlTemplate(new CompileContext(options));
string html = template.Generate(myModel);
```

### Validate without committing (dry‑run compile)

`TryCompilation` parses and type‑checks a template but discards the compiled output. Use it
for linting/CI checks where you only care whether a template *would* compile:

```csharp
using var probe = new TtlTemplate();
TtlCompileResult result = probe.TryCompilation(
    File.ReadAllText("TestTemplate/template.thtml"),
    new TemplateOptions { AllowCSharp = true });

Assert.True(result.Success, result.ToString());
```

## Passing data: `data`, `chained`, and `callerData`

`Generate(object data, object chained = null, object callerData = null)` accepts three
inputs. For most templates you only pass `data` (the model). `chained` seeds the
"chained" value visible to `@out()` at the top level, and `callerData` is carried alongside
as caller context. See [`Scope`](csharp-api.md#scope-the-data-view-during-rendering) for how
these flow through extensions.

## Reusing a compiled template

A `TtlTemplate` is compiled once and can be rendered concurrently many times — `Generate`
is safe to call repeatedly and tracks an adaptive output buffer size across calls. Dispose
it when you are done (it owns the compiled runtime document and any file watcher).

## Where to next

- **[Language Reference](language-reference.md)** — learn the full template syntax.
- **[Built‑in Extensions](built-in-extensions.md)** — the helpers you can call.
- **[C# API Reference](csharp-api.md)** — every option and method in detail.
- **[MVC Integration](mvc-integration.md)** — use `.thtml` files as MVC views.
