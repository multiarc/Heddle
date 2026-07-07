# precompiled-app

**Shows:** build-time precompilation, the differential rule (precompiled output equals its dynamically-compiled
twin), and public discovery enumeration. **Source of record:**
[phase 7](../../docs/spec/phase-7-build-time-compilation/README.md) (phase 9 D13 row 9).

## Run it

```bash
dotnet run --project samples/precompiled-app
```

`templates/*.heddle` are precompiled at build time by `Heddle.Generator` (referenced as an analyzer) into typed
entry points under `Heddle.Generated`. `Program.cs`:

1. renders `invoice.heddle` through its **precompiled** typed entry point (`Templates_Invoice.Generate(model)`) —
   zero parse, zero runtime compile;
2. runs the **differential**: renders the *same* template + data through a dynamically-compiled twin and requires
   byte-identical output (a mismatch fails the run); and
3. enumerates the public registry (`PrecompiledTemplates.Entries`) — key, model type, precompiled flag.

> The `EmitCompilerGeneratedFiles` output lands in `generated/` (gitignored). Both templates are precompiled here;
> a production app mixes precompiled and dynamically-resolved templates freely under `PrecompiledMismatchPolicy`.

## Capture mode (what CI runs)

```bash
dotnet run --project samples/precompiled-app -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/precompiled-app
```

Writes `precompiled-output.html`, `discovery.txt` (ordered registry listing), and `differential.txt`
(`identical` + the byte count).

## What the golden pins

All three files. The differential (precompiled == dynamic twin) is asserted *in capture* before the golden is
written, so a divergence between the two backends fails the job.
