# Building & Testing

How to build the engine, run the tests, and produce the NuGet packages.

## Prerequisites

- **.NET SDK 8.0.** The repository pins the SDK in [global.json](../global.json):

  ```json
  { "sdk": { "version": "8.0.0", "rollForward": "latestMinor" } }
  ```

- **Java + the ANTLR jar** (`lib/antlr-4.13.1-complete.jar`) — only needed if you regenerate
  the parser from the `.g4` grammar. See [Architecture → grammar](architecture.md#the-grammar-and-generated-code).
  The generated parser is checked in, so a normal build does not need Java.

## One‑shot build

From the repository root:

```
build.cmd
```

[build.cmd](../build.cmd) just invokes [build.ps1](../build.ps1) under PowerShell. The script:

1. `dotnet restore`
2. `dotnet test` in `src/Heddle.Tests`
3. `dotnet pack` (Release) for `Heddle`, `Heddle.Language`, and `Heddle.Mvc` into
   `packages/`.

You can also drive the steps manually:

```bash
dotnet restore Templating.sln
dotnet build Templating.sln -c Release
dotnet test src/Heddle.Tests
```

## Target frameworks

| Project | Targets |
| --- | --- |
| `Heddle` ([csproj](../src/Heddle/Heddle.csproj)) | `netstandard2.0; net6.0; net8.0` |
| `Heddle.Language` ([csproj](../src/Heddle.Language/Heddle.Language.csproj)) | `netstandard2.0; net6.0; net8.0` |
| `Heddle.Mvc` ([csproj](../src/Heddle.Mvc/Heddle.Mvc.csproj)) | `net6.0; net8.0` (ASP.NET Core shared framework) |
| `Heddle.Tests` | `net48; net6.0; net8.0` |

All shipping projects use `LangVersion=latest` and are **strong‑name signed** with
`heddle.snk` (`SignAssembly=true`, `AssemblyOriginatorKeyFile=..\..\heddle.snk`).
Current package version is **4.0.2** (with an optional `$(VersionSuffix)`).

### Key dependencies

- `Antlr4.Runtime.Standard` 4.13.1 — runtime for the generated parser.
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) — compiles embedded C# expressions; the version is
  pinned per target framework (4.1.0 on netstandard2.0, 4.9.2 on net6.0, 4.11.0 on net8.0).
- `Microsoft.Extensions.DependencyModel` / `Microsoft.Extensions.FileProviders.Embedded` —
  assembly discovery and embedded resources.
- `Newtonsoft.Json` (MVC project).

## Testing

Tests use **xUnit** and live in [src/Heddle.Tests](../src/Heddle.Tests). The key suite
is [HeddleTemplateTests.cs](../src/Heddle.Tests/HeddleTemplateTests.cs); other suites cover the
compiler, reflection helpers, and string builders.

```bash
dotnet test src/Heddle.Tests
```

Many tests are **golden‑file** comparisons: a `.heddle` template under
[TestTemplate/](../src/Heddle.Tests/TestTemplate) is rendered and compared against an
expected `*.html` file (e.g. `recursion.heddle` → `test-recursion.html`,
`vc-test.heddle` → `test-vc.html`). The `generated-*.html` files are the actual output written
during a run, for diffing against the `test-*.html` expectations. These fixtures double as the
authoritative examples used throughout this documentation.

## Performance benchmarks

[src/Heddle.Performance](../src/Heddle.Performance) contains a **BenchmarkDotNet** suite
that compares Heddle against ASP.NET Core **Razor** on the *same* page, head‑to‑head. Run it in
Release:

```bash
dotnet run -c Release --project src/Heddle.Performance
```

What it measures
([TextRenderBenchmarks.cs](../src/Heddle.Performance/TextRenderBenchmarks.cs),
`[MemoryDiagnoser]` enabled):

- **`RenderTemplateEngine`** renders the Heddle home page
  ([TestTemplates/home.heddle](../src/Heddle.Performance/TestTemplates/home.heddle) +
  [layout.heddle](../src/Heddle.Performance/TestTemplates/layout.heddle)) through
  [`HeddleTest`](../src/Heddle.Performance/Runners/HeddleTest.cs).
- **`RenderRazor`** renders the equivalent Razor page
  ([Views/home.cshtml](../src/Heddle.Performance/Views) + `layout.cshtml`) with runtime
  compilation through [`RazorTest`](../src/Heddle.Performance/Runners/RazorTest.cs).

Both pages are deliberately equivalent: one layout, several reusable templates/sections, and a
dozen component invocations — the Heddle components live in
[TestSuite/Extensions](../src/Heddle.Performance/TestSuite/Extensions) and their Razor
counterparts in [TestSuite/RazorExtensions](../src/Heddle.Performance/TestSuite/RazorExtensions).
On this workload **Heddle renders faster than Razor and allocates less memory**; for *why*, see
[Architecture → Performance characteristics](architecture.md#performance-characteristics).

> Benchmark numbers are hardware‑ and workload‑specific — run the suite on your target machine
> and with a page shaped like your real one to get figures you can quote. The repository
> benchmark is a representative, component‑heavy page where the compiled document's advantage
> is most visible.

Additional comparison material lives under
[performance/RazorTemplateTest](../performance/RazorTemplateTest), and there are extra runners
(compilation cost, memory) in
[src/Heddle.Performance/Runners](../src/Heddle.Performance/Runners).

## Packaging

`build.ps1` packs the three shipping projects. To pack a single project:

```bash
dotnet pack src/Heddle -c Release -o packages
```

NuGet feed configuration is in [NuGet.Config](../NuGet.Config).

## Continuous integration

CI runs on **Azure Pipelines** ([azure-pipelines.yml](../azure-pipelines.yml)), triggered on
`master`. The pipeline:

- **Qodana** static analysis (JetBrains) on `master`.
- **Build** job: install .NET/NuGet, **SonarQube** prepare/analyze/publish, restore, build,
  `dotnet test` with coverage (`XPlat Code Coverage` → Cobertura + OpenCover), publish test
  results (trx) and coverage, then `dotnet pack` for both a `-test` and a release version, and
  publish the packages as pipeline artifacts.
- **Test** stage: push the test packages to an internal NuGet feed.
- **Release** stage (on `master`): push release packages to the internal feed and apply a long
  pipeline‑retention lease.

Coverage is collected with **coverlet** and reported to SonarQube/Azure DevOps.

## Regenerating the parser

Only needed when you change the grammar. With Java available:

```
cd src/Heddle.Language
generate_cs.cmd      # C# lexer/parser into generated/
generate_js.cmd      # JS lexer/parser into js/ (for editor tooling)
```

See [Architecture](architecture.md#the-grammar-and-generated-code) for details.
