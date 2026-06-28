# Templater Documentation

**Templater** is a text template engine for .NET, written in C#. It compiles templates
written in a small purpose‑built language (referred to internally as **TTL** — the *Text
Template Language*) into reusable, strongly‑typed renderers. Templates can embed real C#
expressions, define and inherit reusable named blocks, compose output through extension
chains, and render with full HTML‑encoding control. The engine also ships an ASP.NET Core
MVC view engine so `.thtml` files can be used as MVC views.

The engine is published as three NuGet packages:

| Package | Project | Purpose |
| --- | --- | --- |
| `Templates` | [src/Templates](../src/Templates) | Core engine: parser host, compiler, runtime, built‑in extensions. |
| `Templates.Language` | [src/Templates.Language](../src/Templates.Language) | ANTLR grammar + generated lexer/parser, plus editor (Ace) assets. |
| `Templates.Mvc` | [src/Templates.Mvc](../src/Templates.Mvc) | ASP.NET Core MVC `IViewEngine` integration. |

Current version: **4.0.2** (see [src/Templates/Templates.csproj](../src/Templates/Templates.csproj)).

---

## Why TTL?

- **Compiled and strongly typed.** A template is compiled into an execution‑ready document —
  extension calls wired to compiled accessors (member paths to expression‑tree delegates,
  embedded C# to Roslyn delegates). Member access and embedded C# are checked at compile time,
  not discovered at render time.
- **Fast.** On a like‑for‑like home‑page benchmark in this repository, TTL renders faster than
  ASP.NET Core Razor and allocates less memory. See
  [Architecture → Performance](architecture.md#performance-characteristics) and the
  [benchmark project](../src/Templates.Performance).
- **Composable without coupling.** Reusable templates are declarative extension points, so a
  page can be split into independent pieces recombined by a layout — at no runtime cost — and
  any page can serve as a base for another. See
  [Language Reference → inheritance](language-reference.md#inheritance-and-override-childbase).
- **Extensible by design.** The language has essentially one primitive — the extension call —
  so new directives are added as classes, not grammar. See
  [Writing Custom Extensions](custom-extensions.md).

---

## Pick your path

### I want to *write templates* (template authors)
Start with **[Getting Started](getting-started.md)**, then read the
**[Language Reference](language-reference.md)** for every construct and its behavioral
nuances, and **[Built‑in Extensions](built-in-extensions.md)** for the bundled helpers
(`list`, `if`, `date`, `money`, …). For task‑oriented idioms, see
**[Patterns & Recipes](patterns.md)**.

### I want to *use the engine from C#* (integrators)
Read **[Getting Started](getting-started.md)** and the **[C# API Reference](csharp-api.md)**
(`TtlTemplate`, `TemplateOptions`, `CompileContext`, compile results, file watching).
For MVC apps, see **[MVC Integration](mvc-integration.md)**.

### I want to *extend the engine* (advanced)
Read **[Writing Custom Extensions](custom-extensions.md)** to add your own `@yourhelper(...)`
directives and register them via assembly attributes.

### I want to *understand or modify the engine* (contributors)
Read the **[Architecture](architecture.md)** (lex → parse → compile → render pipeline,
Roslyn code generation, lexer modes) and **[Building & Testing](building.md)**.

---

## Documentation map

| Document | What it covers |
| --- | --- |
| [Getting Started](getting-started.md) | Install/build prerequisites and your first inline and file‑based templates. |
| [Language Reference](language-reference.md) | Every TTL construct: output, expressions, embedded C#, definitions, inheritance, subtemplates, chaining, imports, comments, raw blocks, whitespace, and the lexer‑mode model. |
| [Built‑in Extensions](built-in-extensions.md) | Reference for every bundled extension, its expected input type, and HTML‑encoding behavior. |
| [Patterns & Recipes](patterns.md) | Task‑oriented idioms: with‑blocks, presence checks, first/last, local helper definitions, root context, JSON injection, and more. |
| [C# API Reference](csharp-api.md) | `ITtlTemplate`/`TtlTemplate`, `TemplateOptions`, `CompileContext`, `TtlCompileResult`, registration, and error handling. |
| [Writing Custom Extensions](custom-extensions.md) | The extension contract, `Scope`, attributes, and registration. |
| [MVC Integration](mvc-integration.md) | Registering `TtlViewEngine`, view resolution, and MVC‑specific extensions. |
| [Architecture](architecture.md) | Internal pipeline, ANTLR grammar, compilation, and editor tooling. |
| [Building & Testing](building.md) | SDK, build scripts, target frameworks, tests, packaging, and CI. |

---

## A 30‑second taste

```ttl
@model(){{dynamic}}
<p>Hi @(Name) — you have @int(Count) new comments.</p>
```

```csharp
TtlTemplate.Configure(typeof(Program).Assembly);

var source = "@model(){{dynamic}}\n<p>Hi @(Name) — you have @int(Count) new comments.</p>";
using var template = new TtlTemplate(source, new CompileContext(new TemplateOptions { AllowCSharp = true }));

string html = template.Generate(new { Name = "Ada", Count = 3 });
// => <p>Hi Ada — you have 3 new comments.</p>
```

> **A note on accuracy.** Every construct documented here is derived from the authoritative
> ANTLR grammar ([TtlLexer.g4](../src/Templates.Language/TtlLexer.g4),
> [TtlParser.g4](../src/Templates.Language/TtlParser.g4)). Example snippets are written to
> teach; the repository's [test fixtures](../src/Templates.Tests/TestTemplate) hold real,
> runnable templates, and [develop.txt](../develop.txt) holds terse design notes in current
> syntax.
