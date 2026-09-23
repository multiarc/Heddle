# Building and Testing

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
dotnet test               # runs all test projects
```

## Target frameworks

| Project | Targets |
| --- | --- |
| `Heddle` ([csproj](../src/Heddle/Heddle.csproj)) | `netstandard2.0; net8.0; net10.0` |
| `Heddle.Language` ([csproj](../src/Heddle.Language/Heddle.Language.csproj)) | `netstandard2.0; net8.0; net10.0` |
| `Heddle.Tests` | `net48` (Windows only); `net8.0; net10.0` |

All shipping projects use `LangVersion=latest` and are **strong‑name signed** with
`heddle.snk` (`SignAssembly=true`, `AssemblyOriginatorKeyFile=..\..\heddle.snk`).
The current release line is **3.0.0**; the published version is set from the release tag
(`vX.Y.Z`) at publish time, so the version in the source tree is just a placeholder.

### Key dependencies

- `Antlr4.Runtime.Standard` 4.13.1 — runtime for the generated parser.
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) — compiles embedded C# expressions; the version is
  pinned per target framework (4.1.0 on netstandard2.0, 4.11.0 on net8.0, 5.3.0 on net10.0).
- `Microsoft.Extensions.FileProviders.Embedded` — embedded resources (the C#‑tier class
  template).

## Testing

Tests use **xUnit v3** on Microsoft.Testing.Platform — each suite builds as a self‑contained
test executable, and `dotnet test` drives them per project or per solution. The core engine
suite lives in [src/Heddle.Tests](../src/Heddle.Tests) — its
key file is [HeddleTemplateTests.cs](../src/Heddle.Tests/HeddleTemplateTests.cs), with other suites
covering the compiler, reflection helpers, and string builders. Three more test projects cover the
rest of the toolchain: `Heddle.Build.Tests` (the MSBuild host), `Heddle.Tool.Tests` (the `heddle` CLI),
and `Heddle.LanguageServices.Tests` (the editor
language services).

Run the whole solution:

```bash
dotnet test
```

Or scope to a single project, e.g. `dotnet test src/Heddle.Tests`. CI
([dotnet.yml](../.github/workflows/dotnet.yml)) runs each suite as its own step through a
guarded wrapper that turns a skipped test into a failed one — and each suite asserts its own
`test-classes.txt` inventory, so a whole‑solution run's aggregate count can never let a single
suite going quiet pass unnoticed.

Many tests are **golden‑file** comparisons: a `.heddle` template under
[TestTemplate/](../src/Heddle.Tests/TestTemplate) is rendered and compared against an
expected `*.html` file (e.g. `recursion.heddle` → `test-recursion.html`,
`widgets-layout.heddle` → `test-widgets-layout.html`). The `generated-*.html` files are the actual output written
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
dotnet run -c Release -- gate-precompiled # every workload precompiled and byte-equal to the runtime tier
```

`gate-precompiled` reports which arm it ran (`SITE-TABLE: on|off`): the generated site table is on by
default and off when the `"Heddle.Precompiled.UseGeneratedSites"` `AppContext` switch is `false` in the
`runtimeconfig.json` the harness is launched with: the file handed to `dotnet exec --runtimeconfig` **replaces** the built runtimeconfig wholesale, so it must be a copy of
`benchmarks/dotnet/bin/Release/net10.0/Heddle.Benchmarks.Dotnet.runtimeconfig.json` — its `Microsoft.NETCore.App` and
`Microsoft.AspNetCore.App` framework entries included — with `"Heddle.Precompiled.UseGeneratedSites": false` added under
`configProperties`.

Then measure. Remaining arguments pass straight through to BenchmarkDotNet, so a single workload or
a shorter job is one flag away:

```bash
dotnet run -c Release -- bench-crossstack                          # all eight suites, both tracks
dotnet run -c Release -- bench-crossstack --filter *MixedPageBenchmarks*
dotnet run -c Release -- bench-techniques   # Heddle's six render techniques against each other
dotnet run -c Release -- bench-cold         # cold parse/compile, per engine
dotnet run -c Release -- bench-internal     # props, branching, language-service metadata
dotnet run -c Release -- bench-startup      # cold start: fresh-process compile vs register + bind + first render
```

`bench-techniques` measures the three technique classes side by side — `TechniqueRuntimeBenchmarks`,
`TechniquePrecompiledBenchmarks` (site table on) and `TechniquePrecompiledDataOnlyBenchmarks` (table
off) — and `bench-startup` is the cold-start row. Measurements are taken and kept outside the
repository.

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

> Benchmark numbers are hardware‑ and workload‑specific — run the suite on your target machine
> and with a page shaped like your real one to get figures you can quote. The repository
> benchmark is a representative, component‑heavy page where the compiled document's advantage
> is most visible.

## Build integration

Precompilation runs out of process: the `Heddle.Build` targets collect `HeddleTemplate` items and
scalar properties, serialize them into a response file, and invoke `heddle compile`. **Inputs** are
exactly the items and properties — per-item metadata `Key`, `Name`, `ModelType`, `Precompile` and the
`OutputProfile` override, plus the scalar properties in
[src/Heddle.Build/build/Heddle.Build.props](../src/Heddle.Build/build/Heddle.Build.props). **Outputs**
are the embedded compiled-form artifact (`Heddle.CompiledForm.bin`), the generated source
(`Heddle.CompiledForm.g.cs`, joined into `Compile` before `CoreCompile`), and a stamp file the host
uses for incrementality. No `CompilerVisibleProperty`, no `AdditionalFiles` of Heddle's own: nothing
runs inside the compiler, so build output is a pure function of declared inputs. (The throwaway
intermediate model compile is handed the *project's own* `@(AdditionalFiles)`, unchanged, so the
project's source generators behave there as in the real compile; the real compile receives nothing
from these targets but the generated source.)

Custom MSBuild items extend the reach without changing the shape: `HeddleModelAssembly` and
`HeddleExtensionAssembly` append assemblies to `@(ReferencePath)` so the host can bind over their
implementations (see [Build‑Time Pre‑compilation](precompilation.md#assemblies-the-build-must-see)).

The properties `HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces`,
`HeddleObserveIntermediatePath` and `HeddleObserveImplementationPath` are not read; each of the first
three draws one `HED7037` warning when set — delete the element.

## Packaging

Pack all six shipping packages (`Heddle`, `Heddle.Language`, `Heddle.Build`,
`Heddle.LanguageServices`, `Heddle.LanguageServer`, `Heddle.Tool`) by packing the whole solution,
as CI does:

```bash
dotnet pack -c Release -o packages
```

`Heddle.Tool` and `Heddle.LanguageServer` are RID‑specific `dotnet tool` packages, so `dotnet pack`
builds and publishes each RID for them (a RID‑less `dotnet build --no-build` would miss those outputs).

NuGet feed configuration is in [NuGet.Config](../NuGet.Config).

## Continuous integration

CI runs on **GitHub Actions**. Pull requests and pushes to `main` build, test and pack; they never
publish anything. Publishing happens only for a release tag and uses Trusted Publishing (OIDC, no
stored tokens) for nuget.org and npmjs.org.

- **[.NET build](../.github/workflows/dotnet.yml)** — on every push and pull request to `main`,
  restores, builds, and runs the four test suites (Debug, each its own guarded step) on Linux and
  Windows, then packs every package without publishing it.
- **[Release suites](../.github/workflows/tests-release.yml)** — all four suites in Release on Windows
  through the same guarded wrapper, plus the JS editor-artifact harness. It is a callable workflow
  because it is also a release gate (below), and a job can only depend on a job in its own workflow.
- **[Language server](../.github/workflows/lsp.yml)** — calls the Release suites, packs the
  `heddle-lsp` tool, and packages the per-platform VS Code extensions.
- **[Ace npm package](../.github/workflows/npm.yml)** — builds the custom Ace highlighter bundle.
- **Publishing is tag-driven** ([release-tag.yml](../.github/workflows/release-tag.yml)):

  | Tag | Where it may point | Publishes |
  | --- | --- | --- |
  | `vX.Y.Z` | a commit on `main` | nuget.org and npmjs.org (`latest`, with npm provenance), the VS Code Marketplace, a GitHub Release |
  | `vX.Y.Z-alpha.N`, `vX.Y.Z-beta.N`, `vX.Y.Z-rc.N` | any branch | nuget.org pre-release, npmjs.org under the `alpha`/`beta`/`rc` dist-tag, a Marketplace pre-release, a GitHub pre-release |

  The pre-release number is dotted (`beta.10`, not `beta10`) so versions sort numerically. A `v*.*.*`
  tag of any other shape, or a bare `vX.Y.Z` tag off `main`, fails before anything is published; a tag
  with fewer than two dots (`v3`, `v3.0`) matches no trigger and starts no run at all.

  The Marketplace takes only a plain `X.Y.Z`, so both sides scale the patch component: a pre-release is
  numbered `X.Y.(Z×10000 + rank×1000 + N)`, with alpha = 1, beta = 2 and rc = 3, and a release
  `X.Y.(Z×10000 + 9999)`. So `3.0.0-beta.2` ships as extension `3.0.2002` and `3.0.0` as `3.0.9999`.
  Because the pre-release term reaches at most 3999, the numbers rise in exactly the order the tags are
  pushed — a release over its own pre-releases included — so a user on the pre-release channel is always
  offered the release they were testing, and every fix after it. Left at its bare number a release would
  sit below every pre-release of its own version, and at `Z = 0` below every pre-release in the whole
  minor line.

  Nothing publishes until the checks that matter have passed for that commit: every publishing job
  waits on the Release suites, and the NuGet packages additionally wait on the whole sample gallery.
  The Debug suites alone are not the contract — `testing-standards.md` requires both configurations,
  and the samples are the engine's end-to-end tests.
- **[Documentation](../.github/workflows/docs.yml)** — builds this site (including the WebAssembly
  demo bundle) and deploys it to GitHub Pages. Pull requests build and run the demo smoke suite but
  do not deploy.
- **[Integration samples](../.github/workflows/samples.yml)** — a `fail-fast: false` matrix, one job
  per `samples/` project, running each in capture mode and comparing against its golden.

## The integration sample gallery

The repo-root [`samples/`](../samples/README.md) folder holds eleven small, complete, runnable projects —
one per supported way to integrate Heddle (SSR, definition libraries, dynamic models, sandboxed user
templates, safe output, custom extensions, component libraries, build-time codegen, precompilation, a
NativeAOT publish under strict load, and streaming). They double as the engine's end-to-end test suite: each captures deterministic output that CI
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
