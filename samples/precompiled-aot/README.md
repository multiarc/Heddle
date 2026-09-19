# precompiled-aot

**Shows:** the NativeAOT posture — a trimmed, ahead-of-time-compiled app rendering precompiled
templates under strict load. **Source of record:**
Phase 3 generated sites, P3-R9 (spec retired; the posture is in the [program record](../../docs/spec/common/cross-cutting-decisions.md#program-record--precompilation-v2-closed)).

## Run it

```bash
dotnet run --project samples/precompiled-aot
```

`templates/*.heddle` are the typed controlled workloads (`composed-page`, `large-loop`, `mixed-page`,
`conditional-heavy`, `fragment-heavy`, `fortunes-encoded`, `encoded-loop`; `trivial-substitution` is
model-less and excluded by the declared class). They precompile at build time through `Heddle.Build`
into typed entry points under `Heddle.Generated`. `Program.cs`:

1. renders each through its **typed wrapper** (`Templates_Composed_page.Generate(model)` — zero parse,
   zero runtime compile);
2. renders each through the **registry** (`PrecompiledTemplates.BindTyped` + `Generate`) under strict
   load; and
3. runs the **differential**: renders the *same* template + data through a dynamically-compiled twin
   and requires byte-identical output (a mismatch fails the run).

`@shout` in `composed-page.heddle` is exported with `[ExportFunctions]` so no site is late-bound; the
request-side registry registers the same container, so build declaration and runtime registration
agree function for function.

The project sets `PublishAot=true`, `InvariantGlobalization=true`,
`<RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" />`
(the Roslyn tier is trimmed out of the publish), and
`<RuntimeHostConfigurationOption Include="Heddle.Precompiled.StrictLoad" Value="true" />`
(load-time compilation fails fast instead of happening silently).

## Capture mode (what CI runs)

```bash
dotnet run --project samples/precompiled-aot -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/precompiled-aot
```

Writes one file per workload (`composed-page.txt`, …) plus `assemblies.txt`: the host, the engine and
its parser runtime out of the loaded assembly names, sorted. Nothing else is pinned, because the rest
describes the runtime rather than the host: the JIT reports its dynamic-methods assembly and loads BCL
and helper assemblies lazily, while a native binary reports what the AOT compiler kept. The same golden
therefore holds for the JIT run and for the published binary. The AOT claim is asserted *in capture*
before anything is written: any loaded `Microsoft.CodeAnalysis` assembly fails the run.

CI runs both: the `sample` matrix entry captures under the JIT like every other sample, and the `aot`
job builds, publishes the native binary (`linux-x64`) and captures with it, comparing against the same
goldens.

## AOT publish

```bash
dotnet build samples/precompiled-aot -c Release
dotnet publish samples/precompiled-aot -c Release --no-build
```

The publish must succeed with no trim/AOT analyzer errors, and the published app must capture
green:

```bash
./samples/precompiled-aot/bin/Release/net10.0/<rid>/publish/PrecompiledAot --capture out
bash samples/tools/compare-golden.sh samples/precompiled-aot
```

Publish is two steps on purpose: the build-time host is an executable project, which a
self-contained publish cannot reference (NETSDK1150), so the Tool reference drops out of publish
and the precompile stamp must already be up to date from the build. The dynamic twin runs in the
native binary too: expression trees compile through the interpreter where dynamic code is
unavailable, which is why the differential can be asserted there and not only under the JIT.

## What the golden pins

All eight files. The three-tier differential (typed == registry == dynamic twin) is asserted *in
capture* before the golden is written, so a divergence between the tiers fails the job; the golden
then pins the bytes and the host-side assembly list, on the JIT and in the published native binary.
