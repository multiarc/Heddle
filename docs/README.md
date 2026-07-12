# Heddle Documentation

**Heddle** is a text template engine for .NET, written in C#. It compiles templates
written in its small, purpose‑built language into reusable, strongly‑typed renderers. Heddle can embed real C#
expressions, define and inherit reusable named blocks, compose output through extension
chains, and render with full HTML‑encoding control.

The engine is published as a set of NuGet packages:

| Package | Project | Purpose |
| --- | --- | --- |
| `Heddle` | [src/Heddle](../src/Heddle) | Core engine: parser host, compiler, runtime, built‑in extensions. |
| `Heddle.Language` | [src/Heddle.Language](../src/Heddle.Language) | ANTLR grammar + generated lexer/parser, plus editor (Ace) assets. |
| `Heddle.Generator` | [src/Heddle.Generator](../src/Heddle.Generator) | Build‑time source generator that pre‑compiles `.heddle` files into your assembly. Reference with `PrivateAssets="all"` (an analyzer package). |
| `Heddle.LanguageServices` | [src/Heddle.LanguageServices](../src/Heddle.LanguageServices) | Editor language‑service facade (completion, diagnostics, hover, go‑to‑definition) you can host yourself. |
| `Heddle.LanguageServer` | [src/Heddle.LanguageServer](../src/Heddle.LanguageServer) | LSP server for editors, shipped as a `dotnet tool` (`heddle-lsp`). |
| `Heddle.Tool` | [src/Heddle.Tool](../src/Heddle.Tool) | The `heddle` CLI — a `dotnet tool` for rendering templates and build‑time code generation (the T4 successor). |

Current version: **2.0.0**. The published version always follows the latest
[release tag](https://github.com/multiarc/Heddle/releases) /
[nuget.org](https://www.nuget.org/packages/Heddle).

---

## Why Heddle?

- **Compiled and strongly typed.** A template is compiled into an execution‑ready document —
  extension calls wired to compiled accessors (member paths to expression‑tree delegates,
  embedded C# to Roslyn delegates). With a typed model, member access and embedded C# are checked
  at compile time rather than discovered at render time; declaring `@model(){{dynamic}}` instead
  opts into render‑time member binding.
- **Fast.** In this repository's benchmark run of 2026‑07‑11, Heddle rendered faster than ASP.NET
  Core Razor and allocated less memory (Razor's page is larger and not parity‑checked — the
  like‑for‑like comparison is against the four parity‑checked Liquid/Handlebars engines, which
  Heddle leads on render time and where it allocates the least or tied‑least memory —
  Handlebars.Net is within ~0.3 KB). See [Architecture → Performance](architecture.md#performance-characteristics)
  and the [benchmark project](../src/Heddle.Performance).
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
(`list`, `if`, `date`, `money`, …). For the sandbox‑safe expression tier — operators, literals,
registered functions, `ExpressionMode` — see **[Native Expressions](native-expressions.md)**.
For task‑oriented idioms, see **[Patterns & Recipes](patterns.md)**, and for editor tooling see
**[Editor Support](editor-support.md)**. Arriving from another engine? Start with
**[Coming from Razor](coming-from-razor.md)** or **[Coming from Liquid](coming-from-liquid.md)**
(the latter also covers Jinja/Twig).

### I want to *use the engine from C#* (integrators)
Read **[Getting Started](getting-started.md)** and the **[C# API Reference](csharp-api.md)**
(`HeddleTemplate`, `TemplateOptions`, `CompileContext`, compile results, file watching). To compile
`.heddle` files into your assembly at build time, see
**[Build‑Time Pre‑compilation](precompilation.md)**. For complete,
runnable integration examples — SSR, definition libraries, dynamic models, sandboxing, safe output,
custom extensions, component libraries, codegen, precompilation, streaming — see the
**[integration sample gallery](../samples/README.md)**. Replacing a Razor view layer? The
**[Coming from Razor](coming-from-razor.md)** guide maps the concepts across.

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
| [Coming from Liquid](coming-from-liquid.md) | Migration guide for Liquid/Jinja/Twig authors: `{{ }}` as a body, filter chains, `include`/`render`, loops, tags, whitespace control. |
| [Coming from Razor](coming-from-razor.md) | Migration guide for ASP.NET Core Razor authors: relative context, layouts, `@:`, the three C# tiers, tag helpers → extensions. |
| [Language Reference](language-reference.md) | Every Heddle construct: output, expressions, embedded C#, definitions, inheritance, subtemplates, chaining, imports, comments, raw blocks, whitespace, and the lexer‑mode model. |
| [Native Expressions](native-expressions.md) | The sandbox‑safe expression tier: operators, literals, registered functions, `ExpressionMode`. |
| [Built‑in Extensions](built-in-extensions.md) | Reference for every bundled extension, its expected input type, and HTML‑encoding behavior. |
| [Patterns & Recipes](patterns.md) | Task‑oriented idioms: with‑blocks, presence checks, first/last, local helper definitions, root context, JSON injection, and more. |
| [C# API Reference](csharp-api.md) | `IHeddleTemplate`/`HeddleTemplate`, `TemplateOptions`, `CompileContext`, `HeddleCompileResult`, registration, and error handling. |
| [Build‑Time Pre‑compilation](precompilation.md) | Compiling `.heddle` files into the assembly: generator setup, typed entry points, registry, mismatch policy. |
| [Writing Custom Extensions](custom-extensions.md) | The extension contract, `Scope`, attributes, and registration. |
| [Architecture](architecture.md) | Internal pipeline, ANTLR grammar, compilation, and editor tooling. |
| [Syntax Highlighting](syntax-highlighting.md) | The portable TextMate grammar, its token→scope mapping, and how editors/sites consume it. |
| [Editor Support](editor-support.md) | LSP server, VS Code extension, Neovim setup, configuration. |
| [Building & Testing](building.md) | SDK, build scripts, target frameworks, tests, packaging, and CI. |

---

## A 30‑second taste

```heddle
@model(){{dynamic}}
<p>Hi @(Name) — you have @int(Count) new comments.</p>
```

```csharp
HeddleTemplate.Configure(typeof(Program).Assembly);

var source = "@model(){{dynamic}}\n<p>Hi @(Name) — you have @int(Count) new comments.</p>";
using var template = new HeddleTemplate(source, new CompileContext(new TemplateOptions()));

string html = template.Generate(new Greeting { Name = "Ada", Count = 3 });
// => <p>Hi Ada — you have 3 new comments.</p>

// A named type, not an anonymous one: the runtime binder behind @(Name)/@int(Count) can't
// see another assembly's anonymous types.
public class Greeting { public string Name { get; set; } public int Count { get; set; } }
```
