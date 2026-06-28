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

## Pick your path

### I want to *write templates* (template authors)
Start with **[Getting Started](getting-started.md)**, then read the
**[Language Reference](language-reference.md)** for every construct and its behavioral
nuances, and **[Built‑in Extensions](built-in-extensions.md)** for the bundled helpers
(`list`, `if`, `date`, `money`, …).

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
| [C# API Reference](csharp-api.md) | `ITtlTemplate`/`TtlTemplate`, `TemplateOptions`, `CompileContext`, `TtlCompileResult`, registration, and error handling. |
| [Writing Custom Extensions](custom-extensions.md) | The extension contract, `Scope`, attributes, and registration. |
| [MVC Integration](mvc-integration.md) | Registering `TtlViewEngine`, view resolution, and MVC‑specific extensions. |
| [Architecture](architecture.md) | Internal pipeline, ANTLR grammar, compilation, and editor tooling. |
| [Building & Testing](building.md) | SDK, build scripts, target frameworks, tests, packaging, and CI. |

---

## A 30‑second taste

```ttl
@%
  <greeting> -> (Model)
  {{ Hello, @(Name)! You have @int(Count) messages. }} :: dynamic
%@
```

```csharp
TtlTemplate.Configure(typeof(Program).Assembly);
using var template = new TtlTemplate(
    "@%\n<greeting> -> (Model)\n{{ Hello, @(Name)! You have @int(Count) messages. }} :: dynamic\n%@",
    new CompileContext(new TemplateOptions { AllowCSharp = true }));

string html = template.Generate(new { Name = "Ada", Count = 3 });
// => " Hello, Ada! You have 3 messages. "
```

> **A note on accuracy.** Every construct documented here is derived from the authoritative
> ANTLR grammar ([TtlLexer.g4](../src/Templates.Language/TtlLexer.g4),
> [TtlParser.g4](../src/Templates.Language/TtlParser.g4)) and verified against the real
> template fixtures in [src/Templates.Tests/TestTemplate](../src/Templates.Tests/TestTemplate).
> The repository file [develop.txt](../develop.txt) is historical scratch notes and uses an
> **obsolete** syntax (`<% %>` / `[ ]`); do not rely on it.
