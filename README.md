# Heddle

A text template engine for .NET. Heddle compiles templates written in its small, purpose‑built
language into reusable, strongly‑typed renderers. Templates can embed real C#
expressions, define and inherit reusable named blocks, compose output through extension chains,
and control HTML encoding per directive.

```heddle
@model(){{dynamic}}
<p>Hi @(Name) — you have @int(Count) new comments.</p>
```

```csharp
HeddleTemplate.Configure(typeof(Program).Assembly);

var source = "@model(){{dynamic}}\n<p>Hi @(Name) — you have @int(Count) new comments.</p>";
using var template = new HeddleTemplate(source, new CompileContext(new TemplateOptions()));

string html = template.Generate(new Greeting { Name = "Ada", Count = 3 });
// <p>Hi Ada — you have 3 new comments.</p>

// A named type, not an anonymous one: the runtime binder behind @(Name)/@int(Count) can't
// see another assembly's anonymous types.
public class Greeting { public string Name { get; set; } public int Count { get; set; } }
```

## Packages

| Package | Purpose |
| --- | --- |
| `Heddle` | Core engine: parser host, compiler, runtime, built‑in extensions. |
| `Heddle.Language` | ANTLR grammar + generated lexer/parser and editor assets. |
| `Heddle.Build` | Build‑time host that pre‑compiles `.heddle` files into your assembly (MSBuild targets driving the out‑of‑process `heddle compile`). Replaces `Heddle.Generator` — see [Upgrading from 2.x](docs/precompilation.md#upgrading-from-version-2). |
| `Heddle.LanguageServices` | Editor language‑service facade (completion, diagnostics, hover, go‑to‑definition) you can host yourself. |
| `Heddle.LanguageServer` | LSP server for editors, shipped as a `dotnet tool` (`heddle-lsp`). |
| `Heddle.Tool` | The `heddle` CLI — a `dotnet tool` for rendering templates and build‑time code generation (the T4 successor). |

## Documentation

Full documentation lives in **[docs/](docs/README.md)**:

- [Getting Started](docs/getting-started.md) — build and render your first template.
- [Language Reference](docs/language-reference.md) — every Heddle construct and its nuances.
- [Built‑in Extensions](docs/built-in-extensions.md) — `list`, `if`, `date`, `money`, and more.
- [C# API Reference](docs/csharp-api.md) — `HeddleTemplate`, options, contexts, results.
- [Build‑Time Pre‑compilation](docs/precompilation.md) — compiling `.heddle` files into the assembly with `Heddle.Build`.
- [Writing Custom Extensions](docs/custom-extensions.md) — add your own directives.
- [Architecture](docs/architecture.md) — the lex → parse → compile → render pipeline.
- [Building & Testing](docs/building.md) — SDK, scripts, tests, packaging, CI.

## How Heddle compares

Heddle sits in a small niche: a **compiled, statically‑typed** template language whose entire
control‑flow vocabulary is *library*, not grammar. Here is where it lands against the engines
people usually weigh it against.

| | **Heddle** | Razor | Liquid / Scriban | Handlebars / Mustache | Go `text/template` |
| --- | --- | --- | --- | --- | --- |
| Execution | Compiled to an exec‑ready document | Compiled | Interpreted | Interpreted | Interpreted |
| Typing | **Static, per use site** | Static | Dynamic | Dynamic | Dynamic |
| Logic in templates | Full C# (opt‑in) | Full C# | Sandboxed filters | Logic‑less + helpers | Limited + funcs |
| Control flow | **Extensions (a library)** | Keywords | Tags | Block helpers | Keywords |
| Context model | Relative: current / `::`root | Absolute (`Model.X`) | Mostly global | Relative stack | Relative (`.` / `$`) |
| Composition | Definition inheritance + override | Layouts / sections / partials | Partials / includes | Partials | Heddle / blocks |
| Untrusted templates | No (or run in no‑C# mode) | No | **Yes (sandboxed)** | **Yes (logic‑less)** | Partial |
| Reach / ecosystem | .NET only, small | .NET, excellent | Large | Large, polyglot | Large |

**What's genuinely distinctive (the combination, more than any single trait):**

- **Extension‑only core.** There are no `if`/`for`/`include` keywords — those are extensions, so
  the language grows by adding a class, not by changing the grammar. The `:` chain composes them
  right‑to‑left, with a single *chained* channel carrying the loop index, the piped value, and
  `@out()` content.
- **Abstract, late‑bound sections.** A definition with no `:: Type` is compiled against the
  concrete model at *each call site* — closer to a **C++ template** (monomorphised, type‑checked
  per use) than a C# generic (compiled once behind constraints). Reuse one section across many
  model shapes, each statically checked.
- **Composition without coupling.** Definition inheritance/override are *declarative extension
  points*: unlike Razor sections (which bind "backwards" and pin the rendered page as the
  layout's final consumer), a Heddle page can be split into independent templates recombined by a
  layout with **no runtime cost**, and any page can serve as a base for another.
- **Compiled to an execution‑ready document.** A template becomes an in‑memory tree of
  extension calls wired to **compiled** accessors — member paths to expression‑tree delegates,
  embedded C# to Roslyn delegates — so nothing is reflected or re‑parsed per render.

**Best fit:** performance‑sensitive, first‑party .NET rendering by a team that values typed
templates and component‑style composition. **Poor fit:** untrusted user‑supplied templates
(the security model is all‑or‑nothing on `AllowCSharp`, not a sandbox), polyglot stacks, or
teams wanting a large ecosystem and batteries‑included tooling. The language also trades some
ergonomics for its small core — dense sigils, and a context rule you track per call: each
extension either descends into its parameter or steps back to the caller's context (see
[Stepping back the parent context](docs/language-reference.md#stepping-back-the-parent-context)).
See the [Language Reference](docs/language-reference.md) for the full picture.

## Performance

Heddle compiles each template into an **execution‑ready document** — an in‑memory tree of
extension calls wired to compiled accessors (member paths to expression‑tree delegates,
embedded C# to Roslyn delegates). Rendering walks that document, so it does not re‑parse, reflect,
or pay per‑call activation, section, or dependency‑injection overhead at run time.

The repository carries a cross‑stack benchmark harness — a [BenchmarkDotNet](https://benchmarkdotnet.org/)
leg ([benchmarks/dotnet](benchmarks/dotnet)) measuring Heddle beside Fluid, Scriban, DotLiquid,
Handlebars.Net and ASP.NET Core Razor under a byte‑identical parity gate, plus Rust, JVM, JS, Python
and Go legs — and publishes no numbers: measurements are taken and kept outside this repository.
Run it yourself per [benchmarks/README.md](benchmarks/README.md):

```
dotnet run -c Release --project benchmarks/dotnet -- gate
dotnet run -c Release --project benchmarks/dotnet -- bench-crossstack
```

## Building

```bash
dotnet build -c Release
```

Requires the .NET SDK 10.0 (see [global.json](global.json)). See
[docs/building.md](docs/building.md) for details.

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) first — it
covers the development setup, the pull-request flow, the required
[DCO](https://developercertificate.org/) sign-off (`git commit -s`), and the policy on
AI-assisted contributions. By contributing you agree your work is licensed under Apache-2.0.

## License / authorship

Copyright © Aliaksandr Kukrash and the Heddle contributors.

Licensed under the **Apache License, Version 2.0** — see [LICENSE](LICENSE),
[NOTICE](NOTICE), and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
