# codegen-t4-successor

**Shows:** Heddle as a build-time code/text generator — a T4 successor. `templates/report.heddle` is compiled at
**build time** by `Heddle.Generator` into a typed entry point; the program calls it with zero runtime parse or
compile. **Source of record:** [Pre-compilation](../../docs/precompilation.md) (phase 9 D13 row 8).

## Run it

```bash
dotnet run --project samples/codegen-t4-successor
```

`Heddle.Generator` is referenced as an **analyzer** (`ReferenceOutputAssembly="false"`) — a build-time-only tool.
It turns the template into `Heddle.Generated.Templates_Report` (emitted under `generated/` via
`EmitCompilerGeneratedFiles`), which `Program.cs` invokes to render the report.

The program also runs a **structural dependency check**: `Heddle.Generator.dll` (the code generator) must not be
present in the runtime output — it is a build-time dependency only.

> Reconciliation of the roadmap's "no runtime engine dependency" claim: the phase 7 generator emits *precompiled
> render strategies* that call `Heddle.Precompiled.PrecompiledRuntime`, so the render **runtime** (`Heddle.dll`) is
> intentionally linked — precompilation moves the *parse/compile* to build time, not the render. What the sample
> asserts structurally is the honest, meaningful property: the **code generator** (`Heddle.Generator`) is never a
> runtime dependency. (Recorded in the amendments ledger.)

## Capture mode (what CI runs)

```bash
dotnet run --project samples/codegen-t4-successor -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/codegen-t4-successor
```

Writes `codegen-output.txt` (the rendered report), `dependency-report.txt` (the structural check result), and
`generated/Templates_Report.g.cs` (the emitted entry-point source, with non-deterministic manifest hashes stripped).

## What the golden pins

The rendered output, the dependency report, and the shape of the generated source — generator-output changes are
reviewed diffs, exactly like the phase 7 snapshot suite.
