# codegen-t4-successor

**Shows:** Heddle as a build-time code/text generator — a T4 successor. `templates/report.heddle` is compiled at
**build time** by `Heddle.Build` (the out-of-process host) into a typed entry point; the program calls it with zero runtime parse or
compile. **Source of record:** [Build-Time Pre-compilation](../../docs/precompilation.md).

## Run it

```bash
dotnet run --project samples/codegen-t4-successor
```

`Heddle.Build` is a build-time-only package (`ReferenceOutputAssembly="false"`).
It turns the template into `Heddle.Generated.Templates_Report` (emitted under `generated/` via
`EmitCompilerGeneratedFiles`), which `Program.cs` invokes to render the report.

It also demonstrates the per-item `<HeddleTemplate>` metadata that only work end to end from a real project.
`templates/report.heddle` carries **no** `@model` directive — its item declares
`ModelType="Heddle.Samples.Codegen.BuildInfo"`, which types the template from the project file: the generated
entry point takes a `BuildInfo` model (the golden pins the typed signature).
`templates/_banner.heddle` carries `Precompile="false"` (import-only: no entry point, no manifest row) and
`Name="Banner"`, an **additional** import name — the partial keeps its path-derived key *and* answers to `Banner`,
so `report.heddle`'s `@<<{{Banner}}` and `@<<{{templates/_banner.heddle}}` both resolve. `Name` is not a rename:
the generated entry class is still `Templates_Report`. This sample is the **behavioural gate** for that wiring —
the unit suites inject the metadata directly and never cross `Heddle.Build.targets`, so if a
metadatum stops flowing from MSBuild again the import stops resolving and this build fails with `HED7011`.

The program also runs a **structural dependency check**: `Heddle.Build.dll` (the build host) must not be
present in the runtime output — it is a build-time dependency only.

> Precompilation moves the *parse/compile* to build time, not the render: the generated entry points serve
> sites from the compiled-form table through the render **runtime** (`Heddle.dll`), which is intentionally
> linked. What the sample asserts structurally is the honest, meaningful property: the **build host**
> (`Heddle.Build`) is never a runtime dependency.

## Capture mode (what CI runs)

```bash
dotnet run --project samples/codegen-t4-successor -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/codegen-t4-successor
```

Writes `codegen-output.txt` (the rendered report), `dependency-report.txt` (the structural check result), and
`generated-source.cs.txt` (the emitted entry-point source, with the engine version and content digests masked).

## What the golden pins

The rendered output, the dependency report, and the shape of the generated source — build-output changes are
reviewed diffs.
