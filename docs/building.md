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
2. `dotnet test` in `src/Templates.Tests`
3. `dotnet pack` (Release) for `Templates`, `Templates.Language`, and `Templates.Mvc` into
   `packages/`.

You can also drive the steps manually:

```bash
dotnet restore Templating.sln
dotnet build Templating.sln -c Release
dotnet test src/Templates.Tests
```

## Target frameworks

| Project | Targets |
| --- | --- |
| `Templates` ([csproj](../src/Templates/Templates.csproj)) | `netstandard2.0; net6.0; net8.0` |
| `Templates.Language` ([csproj](../src/Templates.Language/Templates.Language.csproj)) | `netstandard2.0; net6.0; net8.0` |
| `Templates.Mvc` ([csproj](../src/Templates.Mvc/Templates.Mvc.csproj)) | `net6.0; net8.0` (ASP.NET Core shared framework) |
| `Templates.Tests` | `net48; net6.0; net8.0` |

All shipping projects use `LangVersion=latest` and are **strong‑name signed** with
`templater.snk` (`SignAssembly=true`, `AssemblyOriginatorKeyFile=..\..\templater.snk`).
Current package version is **4.0.2** (with an optional `$(VersionSuffix)`).

### Key dependencies

- `Antlr4.Runtime.Standard` 4.13.1 — runtime for the generated parser.
- `Microsoft.CodeAnalysis.CSharp` (Roslyn) — compiles embedded C# expressions; the version is
  pinned per target framework (4.1.0 on netstandard2.0, 4.9.2 on net6.0, 4.11.0 on net8.0).
- `Microsoft.Extensions.DependencyModel` / `Microsoft.Extensions.FileProviders.Embedded` —
  assembly discovery and embedded resources.
- `Newtonsoft.Json` (MVC project).

## Testing

Tests use **xUnit** and live in [src/Templates.Tests](../src/Templates.Tests). The key suite
is [TtlTemplateTests.cs](../src/Templates.Tests/TtlTemplateTests.cs); other suites cover the
compiler, reflection helpers, and string builders.

```bash
dotnet test src/Templates.Tests
```

Many tests are **golden‑file** comparisons: a `.thtml` template under
[TestTemplate/](../src/Templates.Tests/TestTemplate) is rendered and compared against an
expected `*.html` file (e.g. `recursion.thtml` → `test-recursion.html`,
`vc-test.thtml` → `test-vc.html`). The `generated-*.html` files are the actual output written
during a run, for diffing against the `test-*.html` expectations. These fixtures double as the
authoritative examples used throughout this documentation.

## Performance benchmarks

[src/Templates.Performance](../src/Templates.Performance) contains a **BenchmarkDotNet** suite
that compares TTL against ASP.NET Core **Razor** on the *same* page, head‑to‑head. Run it in
Release:

```bash
dotnet run -c Release --project src/Templates.Performance
```

What it measures
([TextRenderBenchmarks.cs](../src/Templates.Performance/TextRenderBenchmarks.cs),
`[MemoryDiagnoser]` enabled):

- **`RenderTemplateEngine`** renders the TTL home page
  ([TestTemplates/home.ttl](../src/Templates.Performance/TestTemplates/home.ttl) +
  [layout.ttl](../src/Templates.Performance/TestTemplates/layout.ttl)) through
  [`TemplaterTest`](../src/Templates.Performance/Runners/TemplaterTest.cs).
- **`RenderRazor`** renders the equivalent Razor page
  ([Views/home.cshtml](../src/Templates.Performance/Views) + `layout.cshtml`) with runtime
  compilation through [`RazorTest`](../src/Templates.Performance/Runners/RazorTest.cs).

Both pages are deliberately equivalent: one layout, several reusable templates/sections, and a
dozen component invocations — the TTL components live in
[TestSuite/Extensions](../src/Templates.Performance/TestSuite/Extensions) and their Razor
counterparts in [TestSuite/RazorExtensions](../src/Templates.Performance/TestSuite/RazorExtensions).
On this workload **TTL renders faster than Razor and allocates less memory**; for *why*, see
[Architecture → Performance characteristics](architecture.md#performance-characteristics).

> Benchmark numbers are hardware‑ and workload‑specific — run the suite on your target machine
> and with a page shaped like your real one to get figures you can quote. The repository
> benchmark is a representative, component‑heavy page where the compiled document's advantage
> is most visible.

Additional comparison material lives under
[performance/RazorTemplateTest](../performance/RazorTemplateTest), and there are extra runners
(compilation cost, memory) in
[src/Templates.Performance/Runners](../src/Templates.Performance/Runners).

## Packaging

`build.ps1` packs the three shipping projects. To pack a single project:

```bash
dotnet pack src/Templates -c Release -o packages
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
cd src/Templates.Language
generate_cs.cmd      # C# lexer/parser into generated/
generate_js.cmd      # JS lexer/parser into js/ (for editor tooling)
```

See [Architecture](architecture.md#the-grammar-and-generated-code) for details.
