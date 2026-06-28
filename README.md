# Templater

A text template engine for .NET. Templater compiles templates written in a small, purpose‑built
language (**TTL**) into reusable, strongly‑typed renderers. Templates can embed real C#
expressions, define and inherit reusable named blocks, compose output through extension chains,
and control HTML encoding per directive. An ASP.NET Core MVC view engine is included so
`.thtml` files can be used as MVC views.

```ttl
@%
  <greeting> -> (Model)
  {{ Hello, @(Name)! You have @int(Count) messages. }} :: dynamic
%@
```

```csharp
TtlTemplate.Configure(typeof(Program).Assembly);
using var template = new TtlTemplate(source, new CompileContext(new TemplateOptions { AllowCSharp = true }));
string html = template.Generate(new { Name = "Ada", Count = 3 });
```

## Packages

| Package | Purpose |
| --- | --- |
| `Templates` | Core engine: parser host, compiler, runtime, built‑in extensions. |
| `Templates.Language` | ANTLR grammar + generated lexer/parser and editor assets. |
| `Templates.Mvc` | ASP.NET Core MVC view engine. |

## Documentation

Full documentation lives in **[docs/](docs/README.md)**:

- [Getting Started](docs/getting-started.md) — build and render your first template.
- [Language Reference](docs/language-reference.md) — every TTL construct and its nuances.
- [Built‑in Extensions](docs/built-in-extensions.md) — `list`, `if`, `date`, `money`, and more.
- [C# API Reference](docs/csharp-api.md) — `TtlTemplate`, options, contexts, results.
- [Writing Custom Extensions](docs/custom-extensions.md) — add your own directives.
- [MVC Integration](docs/mvc-integration.md) — use `.thtml` files as MVC views.
- [Architecture](docs/architecture.md) — the lex → parse → compile → render pipeline.
- [Building & Testing](docs/building.md) — SDK, scripts, tests, packaging, CI.

## Building

```
build.cmd
```

Requires the .NET SDK 8.0 (see [global.json](global.json)). See
[docs/building.md](docs/building.md) for details.

## License / authorship

Author: Aliaksandr Kukrash. Version 4.0.2.
