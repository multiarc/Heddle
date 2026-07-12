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
| `Heddle` ([csproj](../src/Heddle/Heddle.csproj)) | `netstandard2.0; net6.0; net8.0; net10.0` |
| `Heddle.Language` ([csproj](../src/Heddle.Language/Heddle.Language.csproj)) | `netstandard2.0; net6.0; net8.0; net10.0` |
| `Heddle.Tests` | `net48` (Windows only); `net6.0; net8.0; net10.0` |

All shipping projects use `LangVersion=latest` and are **strong‑name signed** with
`heddle.snk` (`SignAssembly=true`, `AssemblyOriginatorKeyFile=..\..\heddle.snk`).
The current release line is **2.0.0**; the published version is set from the release tag
(`vX.Y.Z`) at publish time, so the version in the source tree is just a placeholder.

### Key dependencies

- `Antlr4.Runtime.Standard` 4.13.1 — runtime for the generated parser.
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) — compiles embedded C# expressions; the version is
  pinned per target framework (4.1.0 on netstandard2.0, 4.9.2 on net6.0, 4.11.0 on net8.0,
  5.3.0 on net10.0).
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

[src/Heddle.Performance](../src/Heddle.Performance) contains a **BenchmarkDotNet** suite
that compares Heddle against four other .NET template engines (Fluid, Scriban, DotLiquid,
Handlebars.Net) on byte‑identical parity‑checked output, plus ASP.NET Core **Razor** on a
comparable — but larger and not parity‑checked — page. Run it in Release:

```bash
dotnet run -c Release --project src/Heddle.Performance
```

What it measures
([TextRenderBenchmarks.cs](../src/Heddle.Performance/TextRenderBenchmarks.cs),
`[MemoryDiagnoser]` enabled):

- **`RenderHeddle`** (published in the linked 2026‑07‑11 report under its former name,
  `RenderTemplateEngine`) renders the Heddle home page
  ([TestTemplates/home.heddle](../src/Heddle.Performance/TestTemplates/home.heddle) +
  [layout.heddle](../src/Heddle.Performance/TestTemplates/layout.heddle)) through
  [`HeddleTest`](../src/Heddle.Performance/Runners/HeddleTest.cs).
- **`RenderRazor`** renders a comparable Razor page
  ([Views/home.cshtml](../src/Heddle.Performance/Views) + `layout.cshtml`) with runtime
  compilation through [`RazorTest`](../src/Heddle.Performance/Runners/RazorTest.cs). Razor's page is
  larger and renders different bytes, so — unlike the four Liquid/Handlebars twins — it is **not**
  held to the byte‑identical parity assertion; treat its row as indicative rather than
  apples‑to‑apples.

Both pages are shaped alike: one layout, several reusable templates/sections, and a dozen
component invocations — the Heddle components live in
[TestSuite/Extensions](../src/Heddle.Performance/TestSuite/Extensions) and their Razor
counterparts in [TestSuite/RazorExtensions](../src/Heddle.Performance/TestSuite/RazorExtensions).
In the published run of 2026‑07‑11 **Heddle rendered faster than Razor and allocated less memory**
(and led the four parity‑checked engines too); for the numbers see the
[README Performance section](../README.md#performance), and for *why*, see
[Architecture → Performance characteristics](architecture.md#performance-characteristics).

> Benchmark numbers are hardware‑ and workload‑specific — run the suite on your target machine
> and with a page shaped like your real one to get figures you can quote. The repository
> benchmark is a representative, component‑heavy page where the compiled document's advantage
> is most visible.

There are extra runners (compilation cost, memory) in
[src/Heddle.Performance/Runners](../src/Heddle.Performance/Runners).

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
