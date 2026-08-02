# Building & Testing

How to build the engine, run the tests, and produce the NuGet packages.

## Prerequisites

- **.NET SDK 10.0.** The repository pins the SDK in [global.json](../global.json):

  ```json
  { "sdk": { "version": "10.0.100", "rollForward": "latestMinor" } }
  ```

- **Java** — only needed if you regenerate the parser from the `.g4` grammar; the generation
  scripts download the ANTLR 4.13.1 jar from antlr.org on first run. See
  [Architecture → grammar](architecture.md#the-grammar-and-generated-code).
  The generated parser is checked in, so a normal build does not need Java.

## Building

From the repository root (the commands pick up [Heddle.sln](../Heddle.sln)):

```bash
dotnet restore
dotnet build -c Release
dotnet test               # runs all test projects, as CI does
```

## Target frameworks

| Project | Targets |
| --- | --- |
| `Heddle` ([csproj](../src/Heddle/Heddle.csproj)) | `netstandard2.0; net8.0; net10.0` |
| `Heddle.Language` ([csproj](../src/Heddle.Language/Heddle.Language.csproj)) | `netstandard2.0; net8.0; net10.0` |
| `Heddle.Tests` | `net48` (Windows only); `net8.0; net10.0` |

All shipping projects use `LangVersion=latest` and are **strong‑name signed** with
`heddle.snk` (`SignAssembly=true`, `AssemblyOriginatorKeyFile=..\..\heddle.snk`).
The current release line is **2.1.0**; the published version is set from the release tag
(`vX.Y.Z`) at publish time, so the version in the source tree is just a placeholder.

### Key dependencies

- `Antlr4.Runtime.Standard` 4.13.1 — runtime for the generated parser.
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) — compiles embedded C# expressions; the version is
  pinned per target framework (4.1.0 on netstandard2.0, 4.11.0 on net8.0, 5.3.0 on net10.0).
- `Microsoft.Extensions.DependencyModel` / `Microsoft.Extensions.FileProviders.Embedded` —
  assembly discovery and embedded resources.

## Testing

Tests use **xUnit**. The core engine suite lives in [src/Heddle.Tests](../src/Heddle.Tests) — its
key file is [HeddleTemplateTests.cs](../src/Heddle.Tests/HeddleTemplateTests.cs), with other suites
covering the compiler, reflection helpers, and string builders. Four more test projects cover the
rest of the toolchain: `Heddle.Generator.Tests` and `Heddle.Generator.IntegrationTests` (the source
generator), `Heddle.Tool.Tests` (the `heddle` CLI), and `Heddle.LanguageServices.Tests` (the editor
language services).

Run the whole solution — this is what CI does
([dotnet.yml](../.github/workflows/dotnet.yml) runs `dotnet test -c Debug --no-restore`):

```bash
dotnet test
```

Or scope to a single project, e.g. `dotnet test src/Heddle.Tests`.

Many tests are **golden‑file** comparisons: a `.heddle` template under
[TestTemplate/](../src/Heddle.Tests/TestTemplate) is rendered and compared against an
expected `*.html` file (e.g. `recursion.heddle` → `test-recursion.html`,
`vc-test.heddle` → `test-vc.html`). The `generated-*.html` files are the actual output written
during a run, for diffing against the `test-*.html` expectations. These fixtures double as the
authoritative examples used throughout this documentation.

## Performance benchmarks

[benchmarks/dotnet](../benchmarks/dotnet) is the **BenchmarkDotNet** harness: the .NET leg of the
cross-stack benchmark program, structured like the five other ecosystem harnesses under
[benchmarks/](../benchmarks/README.md). It compares Heddle against five other .NET engines — Fluid,
Scriban, DotLiquid, Handlebars.Net and ASP.NET Core **Razor** — across eight workloads and two
fairness tracks. It is deliberately **not** in `Heddle.sln`, and it reaches the engine through its
public surface only.

**Nothing is timed until it is gated.** Run the gates first; each verb exits non-zero on failure:

```bash
cd benchmarks/dotnet
dotnet run -c Release -- gate            # every registered cell: byte gate, verifier, security floor
dotnet run -c Release -- selftest        # the gate's own checks, incl. the six-technique differential
dotnet run -c Release -- verify-corpus   # corpus freshness + verifier calibration
```

Then measure. Remaining arguments pass straight through to BenchmarkDotNet, so a single workload or
a shorter job is one flag away:

```bash
dotnet run -c Release -- bench-crossstack                          # all eight suites, both tracks
dotnet run -c Release -- bench-crossstack --filter *MixedPageBenchmarks*
dotnet run -c Release -- bench-techniques   # Heddle's six render techniques against each other
dotnet run -c Release -- bench-cold         # cold parse/compile, per engine
dotnet run -c Release -- bench-internal     # props, branching, language-service metadata
```

What the cross-stack suites measure, with `[MemoryDiagnoser]` enabled: one `[Benchmark]` per engine
per workload, on the **controlled** track (every engine authored to produce byte-identical output)
and the **idiomatic** track (every engine authored the way its own documentation teaches). Heddle is
the ratio baseline. The controlled track's byte gate runs in `[GlobalSetup]`, so a twin that drifted
fails the run rather than contributing a number for different work.

Two things about Heddle's own row are worth knowing before quoting it. It is the **UTF-8 sink**,
because that is the path comparable with the other five ecosystems — they all emit UTF-8 or Latin-1,
while a .NET `string` is UTF-16 and crosses the CLR's Large Object Heap threshold on the three
largest workloads, an allocator cliff no other ecosystem pays. And every technique is measured
through a checksum folded from the bytes the engine actually wrote, never a materialised string, so
a sink cannot win by eliding work.

The fixtures every engine renders from live in [`src/Models/`](../benchmarks/dotnet/src/Models); the
templates are files under [`templates/`](../benchmarks/dotnet/templates), one directory per track per
engine, so the idiomatic track is reviewable as templates instead of as escaped literals. No engine
carries its own copy of the data, so no twin can drift from the engine it is compared against.

In the published cross‑stack run of 2026‑07‑25 **Heddle rendered the composed page 1.37× faster
than ASP.NET Core Razor (30.52 μs vs 41.66 μs) and allocated less memory**, on byte‑identical
output under the same parity gate — and led all five other .NET engines on seven of the eight
protocol workloads. For the numbers see the
[README Performance section](../README.md#performance) and the
full report; for *why*, see
[Architecture → Performance characteristics](architecture.md#performance-characteristics).

> Benchmark numbers are hardware‑ and workload‑specific — run the suite on your target machine
> and with a page shaped like your real one to get figures you can quote. The repository
> benchmark is a representative, component‑heavy page where the compiled document's advantage
> is most visible.

## Packaging

Pack all six shipping packages (`Heddle`, `Heddle.Language`, `Heddle.Generator`,
`Heddle.LanguageServices`, `Heddle.LanguageServer`, `Heddle.Tool`) by packing the whole solution,
as CI does:

```bash
dotnet pack -c Release -o packages
```

`Heddle.Tool` and `Heddle.LanguageServer` are RID‑specific `dotnet tool` packages, so `dotnet pack`
builds and publishes each RID for them (a RID‑less `dotnet build --no-build` would miss those outputs).

NuGet feed configuration is in [NuGet.Config](../NuGet.Config).

## Continuous integration

CI runs on **GitHub Actions**. All publishing uses Trusted Publishing (OIDC, no stored
tokens) and is skipped on fork pull requests.

- **[.NET build](../.github/workflows/dotnet.yml)** — on every push and pull request to `main`,
  restores, builds, and runs the test suite on Linux and Windows. Internal pull requests also
  publish a `-beta.<run>` prerelease to **nuget.org**.
- **[Ace npm package](../.github/workflows/npm.yml)** — builds the custom Ace highlighter bundle;
  internal pull requests **stage** a `@multiarc/ace_heddle` pre-release on **npmjs.org** for
  maintainer review (`npm stage publish`).
- **Production releases are tag-driven.** Pushing a `vX.Y.Z` tag publishes that exact version
  to nuget.org and npmjs.org (as `latest`, with npm provenance) and creates a matching GitHub
  Release. Merging to `main` only builds and tests — it does not publish.
- **[Documentation](../.github/workflows/docs.yml)** — builds this site (including the WebAssembly
  demo bundle) and deploys it to GitHub Pages. Pull requests build and run the demo smoke suite but
  do not deploy.
- **[Integration samples](../.github/workflows/samples.yml)** — a `fail-fast: false` matrix, one job
  per `samples/` project, running each in capture mode and comparing against its golden.

## The integration sample gallery

The repo-root [`samples/`](../samples/README.md) folder holds ten small, complete, runnable projects —
one per supported way to integrate Heddle (SSR, definition libraries, dynamic models, sandboxed user
templates, safe output, custom extensions, component libraries, build-time codegen, precompilation, and
streaming). They double as the engine's end-to-end test suite: each captures deterministic output that CI
compares against a committed golden, so a broken sample *is* a failed integration test.

Run one interactively, or in the CI capture mode:

```bash
dotnet run --project samples/dynamic-models                       # human mode
dotnet run --project samples/dynamic-models -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/dynamic-models       # byte-compare vs golden/
```

`UPDATE_GOLDEN=1 bash samples/tools/compare-golden.sh samples/<name>` regenerates a golden (a review event,
never a silent fix). Adding a sample is one folder + one `samples.yml` matrix entry + one index row — see the
[gallery README](../samples/README.md#adding-a-sample).

## Regenerating the parser

Only needed when you change the grammar. With Java available:

```
cd src/Heddle.Language
generate_cs.cmd      # C# lexer/parser into generated/
generate_js.cmd      # JS lexer/parser into js/ (for editor tooling)
```

See [Architecture](architecture.md#the-grammar-and-generated-code) for details.
