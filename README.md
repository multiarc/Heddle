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
| `Heddle.Generator` | Build‑time source generator that pre‑compiles `.heddle` files into your assembly. Add with `PrivateAssets="all"` (an analyzer package). |
| `Heddle.LanguageServices` | Editor language‑service facade (completion, diagnostics, hover, go‑to‑definition) you can host yourself. |
| `Heddle.LanguageServer` | LSP server for editors, shipped as a `dotnet tool` (`heddle-lsp`). |
| `Heddle.Tool` | The `heddle` CLI — a `dotnet tool` for rendering templates and build‑time code generation (the T4 successor). |

## Documentation

Full documentation lives in **[docs/](docs/README.md)**:

- [Getting Started](docs/getting-started.md) — build and render your first template.
- [Language Reference](docs/language-reference.md) — every Heddle construct and its nuances.
- [Built‑in Extensions](docs/built-in-extensions.md) — `list`, `if`, `date`, `money`, and more.
- [C# API Reference](docs/csharp-api.md) — `HeddleTemplate`, options, contexts, results.
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
  embedded C# to Roslyn delegates — so nothing is reflected or re‑parsed per render. In the
  benchmark run of 2026‑07‑11 it rendered [faster than Razor with fewer allocations](#performance)
  (Razor's page is larger and not parity‑checked, so treat that pairing as indicative — the four
  parity‑checked engines are the like‑for‑like comparison).

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

The repository includes a [BenchmarkDotNet](https://benchmarkdotnet.org/) suite
([src/Heddle.Performance](src/Heddle.Performance)) that measures Heddle head‑to‑head against four
other .NET template engines — **Fluid**, **Scriban**, **DotLiquid**, and **Handlebars.Net** — plus
ASP.NET Core **Razor**. Every one of the four Liquid/Handlebars twins is held to **byte‑identical
output** with Heddle by a parity assertion that runs before any timing, so the render and
compile numbers below compare identical work (see
[src/Heddle.Performance/Runners](src/Heddle.Performance/Runners/README.md)). Heddle is the ratio
baseline (`[Benchmark(Baseline = true)]`, `[MemoryDiagnoser]` enabled).

**The measured workload.** The parity‑checked page is the static composition of
`home.heddle` + `layout.heddle`: the layout's reusable‑section defaults, ~a dozen
component/extension calls (`@assets_component`, `@head_scripts`, …), and a list loop over seven
area‑menu fragments (≈55.5 KB raw output). Because `home.heddle` extends the layout via
`@<<{{layout.heddle}}`, the current engine emits that **ordered fragment sequence** rather than the
full HTML page skeleton (the `@body()` slot resolves to its empty default); the four twins
reproduce exactly those bytes. This keeps the comparison honest — all five engines do the same
work — at the cost of not exercising the literal page chrome. The `RenderRazor` row below renders
the full `Views/home.cshtml` page (larger, different output) and is **not** under the parity
assertion, so treat it as indicative rather than apples‑to‑apples.

### Results — 2026‑07‑11 (commit `8341bb67`)

```
BenchmarkDotNet v0.15.8 · Windows 11 (10.0.26200.8655/25H2)
AMD Ryzen 9 9950X 4.30GHz, 16 physical / 32 logical cores
.NET SDK 10.0.301 · .NET 10.0.9 runtime, X64 RyuJIT x86-64-v4
```

**Render** (cached‑template path; lower is better; ratio vs Heddle):

| Engine | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: |
| **Heddle** (baseline) | **32.50 μs** | **1.00** | **227.86 KB** | **1.00** |
| Fluid 2.31.0 | 64.88 μs | 2.02 | 231.98 KB | 1.02 |
| Handlebars.Net 2.1.6 | 69.76 μs | 2.17 | 227.59 KB | 1.00 |
| DotLiquid 2.3.197 | 178.21 μs | 5.55 | 404.69 KB | 1.78 |
| Scriban 7.2.5 | 376.71 μs | 11.73 | 1,154.34 KB | 5.07 |
| Razor (full page)† | 65.55 μs | 2.04 | 263.51 KB | 1.16 |

On this workload Heddle rendered fastest of the six — 2.0× ahead of the next engine (Fluid, 64.88 μs)
and 11.7× ahead of Scriban — while allocating the least or tied‑least memory (227.86 KB; Handlebars.Net
is within 0.3 KB, Scriban allocates 5.07×). † Razor renders a different, larger page and is not parity‑checked.

**Compile / parse** (cold, one‑time cost; lower is better; ratio vs Heddle):

| Engine | Mean | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| **Heddle** (baseline) | **264.99 μs** | **1.00** | **1,339.67 KB** |
| Fluid 2.31.0 | 3.65 μs | 0.01 | 5.31 KB |
| Scriban 7.2.5 | 4.68 μs | 0.02 | 22.95 KB |
| DotLiquid 2.3.197 | 7.21 μs | 0.03 | 36.01 KB |
| Handlebars.Net 2.1.6 | 8,287.09 μs | 31.27 | 260.65 KB |

Heddle's model is **compile‑once, render‑many**: its first compile runs ANTLR, expression‑tree
compilation, and (for embedded C#) Roslyn, so at 264.99 μs it is ~70× the cold cost of the Liquid
engines and allocates far more up front — a cost amortized across every subsequent cached render,
where it leads. Handlebars.Net compiles slower still (8.29 ms, 31.3× Heddle).

Raw BenchmarkDotNet artifacts (md/csv/html) for this run are committed under
[docs/benchmarks/2026-07-11](docs/benchmarks/2026-07-11). Numbers are hardware‑ and date‑specific;
reproduce them yourself with:

```
dotnet run -c Release --project src/Heddle.Performance
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
