# precompiled-app

**Shows:** build-time precompilation, the differential rule (precompiled output equals its dynamically-compiled
twin), public discovery enumeration, and configuring both tiers for a model assembly the project does not
reference. **Source of record:**
[Pre-compilation](../../docs/precompilation.md) (phase 9 D13 row 9).

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
3. renders `ticket.heddle`, whose `@model` type lives in `external-models/` — an assembly this project takes **no
   compile-time reference on**. `@(HeddleModelAssembly)` appends it to `@(ReferencePath)`, which is the only reason
   the generator can bind `Acme.Models.Ticket` and the only reason `Program.cs` can name it; the assembly attribute
   is what registers the same assembly with the engine so the dynamic twin compiles too. Both differentials must
   hold; and
4. enumerates the public registry (`PrecompiledTemplates.Entries`) — key, model type, precompiled flag.

> The `EmitCompilerGeneratedFiles` output lands in `generated/` (gitignored). All three templates are precompiled
> here; a production app mixes precompiled and dynamically-resolved templates freely under
> `PrecompiledMismatchPolicy`.

> **Why the item and not a `ProjectReference`.** The escape-hatch item is for projects that cannot take a source
> dependency, and this sample stands in for one: the `ProjectReference` on `external-models` carries
> `ReferenceOutputAssembly="false"`, so it sequences the build and contributes nothing to the compile. Delete the
> `@(HeddleModelAssembly)` item and the sample stops building — which is the point of having it here at all, since
> the targets are otherwise only covered by suites that inject build metadata directly. Because the item is not an
> ordinary reference the assembly is absent from the deps file, so `Program.cs` also has to load it — the
> obligation the attribute form does not carry. See
> [Assemblies the build must see](../../docs/precompilation.md#assemblies-the-build-must-see).

## Capture mode (what CI runs)

```bash
dotnet run --project samples/precompiled-app -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/precompiled-app
```

Writes `precompiled-output.html`, `discovery.txt` (ordered registry listing), `differential.txt`
(`identical` + the byte count), and `external-model-output.txt`.

## What the golden pins

All four files. The differential (precompiled == dynamic twin) is asserted *in capture* before the golden is
written, so a divergence between the two backends fails the job.
