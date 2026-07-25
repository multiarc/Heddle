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

**Workload breadth.** The composition page above is one of three published workloads. A
[trivial-substitution and a large-loop workload](docs/benchmarks/2026-07-18) bracket it — the
former (scalar output, no composition) is the shape where Heddle's lead is workload-dependent
rather than universal (in the 2026‑07‑18 run Heddle rendered it fastest but Handlebars.Net
allocated less than half the memory), and the latter (one large iteration) is where the time race
is tightest (Handlebars.Net within ~8%, again allocating less); both are parity-checked against
the same four engines. No universal-superiority claim follows. Numbers are hardware- and
date-specific; reproduce with the command above filtered to `*SubstitutionRenderBenchmarks*` /
`*LoopRenderBenchmarks*`.

### Cross‑stack — 2026‑07‑22

The two runs above compare Heddle only against other .NET engines. The
[cross‑stack run](docs/benchmarks/2026-07-22) widens that to **thirteen engines across six
ecosystems** — .NET, Rust, the JVM, JS/Node, Python and Go — over **eight workloads** in two
tracks, all on one machine in one session, every controlled cell held to byte‑identical output
against a shared golden corpus.

```
Windows 11 (10.0.26200.8894/25H2) · AMD Ryzen 9 9950X 4.30GHz, 16 physical / 32 logical cores
.NET SDK 10.0.302 · Rust 1.97.1 · JDK 23.0.2 · node 26.4.0 · CPython 3.13.0 · go1.26
BenchmarkDotNet 0.15.8 · Criterion · JMH 1.37 · mitata 1.0.34 · pyperf 2.10.0 · benchstat
```

**Within .NET, Heddle is fastest on seven of the eight workloads** (controlled track, lower is
better; ratio vs Heddle):

| Engine | composed‑page | trivial‑subst. | large‑loop | mixed‑page | fortunes‑enc. | encoded‑loop |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| **Heddle** (baseline) | **25.31 μs** | **113.6 ns** | **514.7 μs** | **2.601 μs** | **669.0 ns** | **1.988 ms** |
| Fluid.Core 2.31.0 | 67.63 μs | 467.4 ns | 799.0 μs | 8.680 μs | 2,163.7 ns | 2.964 ms |
| Handlebars.Net 2.1.6 | 73.09 μs | 380.0 ns | 668.3 μs | 7.285 μs | 1,733.8 ns | **1.925 ms** |
| DotLiquid 2.3.197 | 169.96 μs | 1,944.3 ns | 3,733.7 μs | 54.698 μs | 24,952.9 ns | 8.279 ms |
| Scriban 7.2.5 | 438.12 μs | 4,027.0 ns | 1,394.4 μs | 19.539 μs | 8,528.9 ns | 5.862 ms |

**Heddle loses `encoded-loop`** — Handlebars.Net renders it in 1.925 ms against Heddle's
1.988 ms, 3.3% faster, a gap wider than either engine's dispersion. That is the largest‑output
workload in the set (831,685 B, escaping every field), where Heddle's per‑node dispatch advantage
is amortised away. `conditional-heavy` and `fragment-heavy` are omitted from the table for width;
Heddle leads both.

**Across all six ecosystems, Heddle is mid‑field — and last on the flagship workload.** Ranking
by ns/render on `composed-page`, the layout/component composition shape:

| # | Engine | Ecosystem | ns/render | vs Heddle |
| ---: | --- | --- | ---: | ---: |
| 1 | eta 4.6.0 † | JS | 331 | 0.01 |
| 2 | handlebars 4.7.9 † | JS | 1,406 | 0.06 |
| 3 | Askama 0.16.0 | Rust | 1,458 | 0.06 |
| 4 | Tera 2.0.0 | Rust | 3,638 | 0.14 |
| 5 | JTE 3.2.4 | JVM | 14,289 | 0.56 |
| 6 | Mako 1.3.12 | Python | 16,069 | 0.63 |
| 7 | templ v0.3.1020 | Go | 17,030 | 0.67 |
| 8 | Jinja2 3.1.6 | Python | 18,186 | 0.72 |
| 9 | text/template | Go | 19,400 | 0.77 |
| 10 | Thymeleaf 3.1.5 | JVM | 21,626 | 0.85 |
| 11 | **Heddle** | **.NET** | **25,310** | **1.00** |

Every one of the ten non‑.NET engines renders `composed-page` faster than Heddle. † The two JS
rows are **measurement artifacts**, not engine speed: V8 returns an unflattened `ConsString`
rope, so 330.7 ns for 34,847 B implies ~105 GB/s — above this machine's store bandwidth — and
mitata reports 3% of the output size as heap. Discounting them, the largest credible margin is
Askama's 17.4×.

Heddle's placement varies sharply by workload shape — strongest on small and encoded work,
weakest on large composition:

| Workload | Heddle | rank | Workload | Heddle | rank |
| --- | ---: | --- | --- | ---: | --- |
| fortunes‑encoded | 669.0 ns | **2 of 15** | conditional‑heavy | 13.97 μs | 4 of 15 |
| trivial‑substitution | 113.6 ns | 3 of 15 | large‑loop | 514.7 μs | 6 of 15 |
| mixed‑page | 2.601 μs | 3 of 15 | encoded‑loop | 1.988 ms | 6 of 15 |
| fragment‑heavy | 2.880 μs | 3 of 15 | composed‑page | 25.31 μs | **11 of 16** |

Read the cross‑stack rows with their evidence class in mind: Rust, JVM and Go are compiled or
same‑class **fair‑fight** peers, while JS and Python are **reach/context** — they show where a
workload lands in those ecosystems, not a like‑for‑like engine contest. Three toolchains also
drifted from their pins in this run (JDK 23.0.2 against a Temurin 25 pin, node 26.4.0 against
24.18.0, CPython 3.13.0 against 3.14.6), which leaves the within‑ecosystem comparisons intact but
makes the cross‑stack rankings provisional as claims about the ecosystems.

There is **no aggregate score, geomean or overall winner** in the full report, and none should be
inferred here. Numbers are hardware- and date-specific; reproduce them yourself with:

```
.\benchmarks\run-all.ps1
```

Full analysis, every workload and track, the per‑ecosystem tables, allocation sidebars and the
complete caveat register: **[docs/benchmarks/2026-07-22](docs/benchmarks/2026-07-22)**.

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
