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
  cross‑stack run of 2026‑07‑25 it rendered [1.37× faster than ASP.NET Core Razor with fewer
  allocations](#performance) on **byte‑identical output** — Razor is held to the same parity gate as
  the four Liquid/Handlebars twins as of that run, so this is a like‑for‑like comparison rather than
  the indicative pairing earlier reports had to disclaim.

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
([benchmarks/dotnet](benchmarks/dotnet)) that measures Heddle head‑to‑head against four
other .NET template engines — **Fluid**, **Scriban**, **DotLiquid**, and **Handlebars.Net** — plus
ASP.NET Core **Razor**. Every one of the four Liquid/Handlebars twins is held to **byte‑identical
output** with Heddle by a parity assertion that runs before any timing, so the render and
compile numbers below compare identical work (see
[benchmarks/README.md](benchmarks/README.md)). Heddle is the ratio
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
docs/benchmarks/2026-07-11. Numbers are hardware‑ and date‑specific;
reproduce them yourself with:

```
dotnet run -c Release --project benchmarks/dotnet -- bench-crossstack
```

**Workload breadth.** The composition page above is one of three published workloads. A
trivial-substitution and a large-loop workload bracket it — the
former (scalar output, no composition) is the shape where Heddle's lead is workload-dependent
rather than universal (in the 2026‑07‑18 run Heddle rendered it fastest but Handlebars.Net
allocated less than half the memory), and the latter (one large iteration) is where the time race
is tightest (Handlebars.Net within ~8%, again allocating less); both are parity-checked against
the same four engines. No universal-superiority claim follows. Numbers are hardware- and
date-specific; reproduce with the command above filtered to `*SubstitutionRenderBenchmarks*` /
`*LoopRenderBenchmarks*`.

### Cross-stack — 2026-08-08

The two runs above compare Heddle only against other .NET engines. The
cross-stack run widens that to **fifteen engines across six
ecosystems** — .NET, Rust, the JVM, JS/Node, Python and Go — over **eight workloads** in two
tracks, all on one machine in one session, every controlled cell held to byte-identical output
against a shared golden corpus.

```
Windows 11 (10.0.26200.8894/25H2) · AMD Ryzen 9 9950X, 16 physical / 32 logical cores
.NET 10.0.10 · rustc 1.97.1 · JDK 25.0.4 · node v24.19.0 · CPython 3.14.7 · go1.26.5 · templ v0.3.1020
BenchmarkDotNet 0.15.8 · Criterion · JMH 1.37 · mitata 1.0.34 (38 passes per track) · pyperf 2.10.0 · benchstat
```

This run executed on the program's **Windows protocol machine** — the box its metrics protocol
pins for cross-compared runs — with **every toolchain on its pin, zero drift**, and it is
unreplicated: no cross-check on a second platform is currently published. Windows has no captured
tuned state (the posture is procedural: quiet machine, AC power, High Performance plan), so
absolute figures are not comparable with tuned boost-off runs on other platforms or clock states.
The report states the full caveat set.

The eight workloads split into two sizing regimes, and the report leads with the realistic one.
The boundary is not editorial: the CLR allocates any object of **85,000 bytes or more on the Large
Object Heap**, and .NET strings are UTF-16, so a page crosses that line at 42,500 characters and
every render past it drives a full Gen2 collection. Five workloads sit below it; three sit above.

#### Tier 1 — realistic sizing (338 B – 15.5 KB)

**Heddle is the fastest of the six .NET engines on all five**, by 2.25×–3.64× (controlled track,
next-fastest .NET engine shown; lower is better):

| Workload | Golden | Heddle | Next .NET engine | Margin |
| --- | ---: | ---: | --- | ---: |
| trivial-substitution | 338 B | 149 ns | Handlebars.Net 2.1.6 — 385.8 ns | **2.59×** |
| fortunes-encoded | 1,156 B | 739.1 ns | Handlebars.Net 2.1.6 — 1.66 μs | **2.25×** |
| fragment-heavy | 4,730 B | 3.081 μs | Fluid.Core 2.31.0 — 11.22 μs | **3.64×** |
| mixed-page | 9,712 B | 2.636 μs | Handlebars.Net 2.1.6 — 7.247 μs | **2.75×** |
| conditional-heavy | 15,549 B | 15.02 μs | Handlebars.Net 2.1.6 — 34.51 μs | **2.30×** |

Cross-stack it is **top-4 on all five**, beaten only by Askama (Rust, compile-time), JTE (JVM,
compiled to Java source) and, on two of the five, eta (JS):

| Workload | Golden | Heddle rank | Ahead of it |
| --- | ---: | ---: | --- |
| trivial-substitution | 338 B | **#3** of 16 | Askama, eta |
| fortunes-encoded | 1,156 B | **#2** of 16 | Askama |
| fragment-heavy | 4,730 B | **#3** of 16 | Askama, JTE |
| mixed-page | 9,712 B | **#2** of 16 | Askama |
| conditional-heavy | 15,549 B | **#4** of 16 | Askama, JTE, eta |

It is ahead of Tera, templ, `text/template`, handlebars, Thymeleaf, Jinja2 and
Mako on every one of the five. **Askama is the only engine faster than Heddle on all eight
workloads** — a Rust compile-time engine making the same architectural bet, without a managed
runtime.

#### Tier 2 — edge-case sizing (54 KB – 852 KB rendered), read with the caveats

| Workload | Golden | Heddle | .NET field | Cross-stack rank |
| --- | ---: | ---: | --- | ---: |
| composed-page | 34,847 B | 34.07 μs | 1 of 6 — 1.77× over Fluid.Core | **#11** of 16 |
| large-loop | 192,780 B | 492.1 μs | 1 of 6 — 1.51× over Handlebars.Net | #6 of 16 |
| encoded-loop | 831,685 B | 1.766 ms | **2 of 6 — Handlebars.Net 1.72 ms** | #7 of 16 |

**Heddle loses `encoded-loop`** — Handlebars.Net leads at 0.97×, and Razor sits at 1.00×, inside
overlapping dispersion. Handlebars.Net led this workload on the prior (since-removed) runs as
well, across two rebuilds of the .NET harness, so the ordering is a reproduced result rather than
noise. It is escaping throughput over an 852 KB output no page produces — a real signal about the
encoder's bulk path, not a page render.

**`composed-page`'s #11 of 16 is not a statement about composition machinery.** That workload is
17 pre-existing string fragments concatenated — no loop body, no branch, no escaping — so every
compiled engine reduces it to about 17 `memcpy` calls, and the row measures memory bandwidth and
the allocator rather than template execution. The floor measurements behind that analysis (a
hand-written .NET `string.Concat` of the same fragments sits within ~2× of Heddle's figure) were
taken on an earlier run's box and are carried in the program's ledger as prior evidence; the
report quantifies the LOH cliff (throughput falls 4.5× across the threshold) and the
whitespace-normalization effect on the published `implied B/ns` column.

**ASP.NET Core Razor is a full member of all eight workloads and both tracks**, under the same
parity gate as every other engine — the earlier single-workload Razor comparison is superseded.
Heddle leads it by 2.51×–37.07× at Tier 1 sizes and 1.90× on `composed-page`; on `encoded-loop`
Razor is marginally ahead (a 1.00× ratio in the tables, within overlapping dispersion).

Read the cross-stack rows with their evidence class in mind: Rust, JVM and Go are compiled or
same-class **fair-fight** peers, while JS and Python are **reach/context** — they show where a
workload lands in those ecosystems, not a like-for-like engine contest.

Every figure is materialised output. The JS leg ran **38 aggregated passes per render track**,
each a separate node process asserting a materialisation check before writing its artifact, so
the JS rows are real work rather than the V8 `ConsString` rope artifact earlier runs of this
suite reported. Its cross-process stability verdict is `verified-with-disclosure` on the
controlled track (one cell of sixteen at 5.11% cross-pass RSD; median 1.41%) and `verified` on
the idiomatic track.

There is **no aggregate score, geomean or overall winner** in the full report, and none should be
inferred here. Numbers are hardware-, platform- and date-specific; reproduce them yourself with:

```powershell
powershell -ExecutionPolicy Bypass -File benchmarks\run-all.ps1
```

Full analysis, every workload and track, the per-ecosystem tables, allocation sidebars and the
complete caveat register: **[docs/benchmarks/2026-08-08](docs/benchmarks/2026-08-08/index.md)**.

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
