# Phase 2 — Build integration

**Status:** Specified — ready for implementation. **Plan item:**
[phase-2-build-integration.md](../../plan/precompilation-v2/phase-2-build-integration.md). **Entry document:**
[README.md](README.md). **Contract:** [artifact-contract.md](artifact-contract.md). **Depends on:**
[phase 1](phase-1-compiled-form.md) merged (writer, loader, lazy rows, binding gate, deferral, refusal classes, the
generator out of the build).

## Scope and goal

Ship the compiled form from the consumer's own build through one package, `Heddle.Build`, with the MSBuild surface
hosts already use. Its targets launch `heddle compile` (`Heddle.Tool`) out of process; the host compiles every
template through the real engine with deferred function binding over the consumer's implementation images and writes
the artifact, the generated source (marker, `HeddleArtifact`, typed wrappers) and diagnostics MSBuild parses. The
registry and gauntlet carry over. In-repo consumers return on the package; a new suite evaluates real MSBuild.

## Assumed state

Verified against source (commit `654959b5`) with phase 1 merged:

- The MSBuild surface is `src/Heddle.Generator/build/Heddle.Generator.props` and `.targets` (on disk, out of the
  build): `CompilerVisibleProperty` for `HeddleOutputProfile`, `HeddleExpressionMode`, `HeddleTrimDirectiveLines`,
  `HeddleMaxRecursionCount`, `HeddleTemplateRoot`, `HeddleGeneratedNamespace`, `HeddleEmitUtf8Pieces`,
  `HeddleNodeFallback`, `HeddleObserveEngine`, `HeddleObserveIntermediatePath`, `HeddleObserveImplementationPath`;
  `CompilerVisibleItemMetadata` `Key`, `ModelType`, `Name`, `Precompile` on `AdditionalFiles`; defaults `Html`,
  `Native`, `true`, `100`, `$(MSBuildProjectDirectory)`; the default glob
  `<HeddleTemplate Include="**\*.heddle" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder);bin\**;obj\**" />`
  under `EnableDefaultHeddleTemplates != false`; `@(HeddleModelAssembly)`/`@(HeddleExtensionAssembly)` appended to
  `@(ReferencePath)`; `@(ReferencePath)` items with `%(ReferenceAssembly)` non-empty are project references whose
  `ReferencePath` is the implementation.
- `HeddleBuildOptions` (`src/Heddle/Precompiled/HeddleBuildOptions.cs`) is the option-name/default table the targets
  and the LSP read; `WorkspaceConfig` (`src/Heddle.LanguageServices/WorkspaceConfig.cs`) reads `.heddle-lsp.json`
  keys `rootPath`, `outputProfile`, `expressionMode`, `fileNamePostfix`, `trimDirectiveLines`, `maxRecursionCount`,
  `assemblies` with `HeddleBuildOptions.Default*` defaults; `HeddleDiagnosticProjection` is the one drain rule the
  LSP and `HeddleCompileResult` share.
- `src/Heddle.Tool` is `net10.0`, `PackAsTool`, `ToolCommandName=heddle`, one verb `render`
  (`Program.Run(string[], TextWriter, TextWriter)`), `InternalsVisibleTo("Heddle.Tool.Tests")`; `Heddle.Tool.Tests`
  is `net10.0` with two classes.
- `HeddleTemplateGenerator.SanitizeName(string key)` (on disk): per `/` segment (extension dropped from the last),
  skip empty segments, upper-case the first character, keep `_`/letters/digits (a leading digit gets `_` prefixed),
  replace every other character with `_`, join segments with `_` — `views/home/index.heddle` → `Views_Home_Index`.
- `samples/precompiled-app/PrecompiledApp.csproj` declares three `HeddleTemplate` items, a `HeddleModelAssembly` item
  for `Acme.Models.dll`, calls `PrecompiledTemplates.Register` and typed entries
  `Heddle.Generated.Templates_Invoice.Generate(model)`; goldens `differential.txt`, `discovery.txt`
  (`key  model=…  precompiled=True`), `external-model-output.txt`, `precompiled-output.html`.
  `samples/codegen-t4-successor` uses `ModelType`, `Name`, `Precompile="false"`. Both are out of the `samples.yml`
  matrix since phase 1.
- `benchmarks/dotnet/Heddle.Benchmarks.Dotnet.csproj` (`HeddleOutputProfile=Text`) carries no precompiled tier since
  phase 1; `PrecompiledBackend.Entries()` keys coverage on row presence (`CoveredWorkloads`/`UncoveredWorkloads`) and
  `gate-precompiled` fails on every uncovered workload.
- `.github/workflows/dotnet.yml` runs three guarded Debug legs; `samples.yml` runs eight samples on .NET 10.
- `Directory.Build.props` states `<VersionPrefix>2.1.0</VersionPrefix>` once; every first-party assembly is signed
  with `heddle.snk`.

## Requirements

### P2-R1 — One package, version-locked to the engine

**Decision.** New packable project `src/Heddle.Build/Heddle.Build.csproj` (`PackageId=Heddle.Build`,
`netstandard2.0` task assembly `Heddle.Build.Tasks.dll`, `DevelopmentDependency=true`), packing
`buildTransitive/Heddle.Build.props`, `buildTransitive/Heddle.Build.targets`, `tasks/netstandard2.0/` and the
framework-dependent publish output of `src/Heddle.Tool` under `tools/net10.0/any/` (`Heddle.Tool.dll`,
`Heddle.dll`, `Heddle.Language.dll`, `Antlr4.Runtime.Standard.dll`, `Microsoft.CodeAnalysis*.dll`,
`Heddle.Tool.deps.json`, `Heddle.Tool.runtimeconfig.json`). The consumer references `Heddle` and `Heddle.Build`;
the host compares the resolved `Heddle.dll` reference's `AssemblyName.Version` (targets pass `--engine-reference
<path>`) with its own `Heddle.dll` and reports `HED7035` (error) on inequality. Build machines need a .NET 10 (or
later) runtime; a missing runtime surfaces as MSBuild's own `MSB6006` for the `heddle` process with the runtime's
message on stderr, documented as the prerequisite.

**Rationale.** [PD4](../../plan/precompilation-v2/decisions.md#pd4--artifact-compatibility),
[PD6](../../plan/precompilation-v2/decisions.md#pd6--build-host): the artifact is stamped with the engine that
compiled it, and the MSBuild node must not host the engine's Roslyn.
**Alternatives rejected.** An in-process task (Roslyn version conflicts with MSBuild's node); a version check in the
targets (a project reference to `Heddle` has no package version to read).

### P2-R2 — The MSBuild surface

**Decision.** `Heddle.Build.props`/`.targets` keep, spelling for spelling: `HeddleTemplate` items with the default
glob and `EnableDefaultHeddleTemplates`; metadata `Key`, `Name`, `ModelType`, `Precompile`, plus new `OutputProfile`
(`Html`|`Text`, overriding `$(HeddleOutputProfile)` for that item); properties `HeddleOutputProfile`,
`HeddleExpressionMode`, `HeddleTrimDirectiveLines`, `HeddleMaxRecursionCount`, `HeddleTemplateRoot`,
`HeddleGeneratedNamespace` with unchanged defaults; items `HeddleModelAssembly`, `HeddleExtensionAssembly`
(appended to `@(ReferencePath)` and to the implementation set). No `CompilerVisibleProperty`,
`CompilerVisibleItemMetadata` or `AdditionalFiles` transform exists — nothing runs in the compiler. The five
properties `HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces`, `HeddleObserveIntermediatePath`,
`HeddleObserveImplementationPath` are not read; the warning for the first three is window item 4 and lands in
[phase 4](phase-4-removal-and-release-tail.md#p4-r5--retired-properties-warn). `HeddleBuildOptions` keeps the
surviving property names and defaults; `.heddle-lsp.json` spellings are untouched because `WorkspaceConfig` reads only
those.

**Rationale.** [PD3](../../plan/precompilation-v2/decisions.md#pd3--msbuild-surface); one migration.
**Alternatives rejected.** Erroring on a retired property (none changes a rendered byte).

### P2-R3 — Implementation images and the host process

**Decision.** The targets compute `@(_HeddleImplementationImage)` = project references' `@(ReferencePath)` items
(`%(ReferenceSourceTarget) == 'ProjectReference'`, the real output) ∪ `@(RuntimeCopyLocalItems)` `.dll` runtime
assets from `ResolvePackageAssets` (packages' `lib` images, transitively — independent of
`CopyLocalLockFileAssemblies`, which the SDK sets `false` for class libraries) ∪ `@(HeddleModelAssembly)` ∪
`@(HeddleExtensionAssembly)` ∪ the intermediate model assembly (P2-R6) when present. The `HeddleCompile` task (`ToolTask`) writes a response file and
runs `"$(DOTNET_HOST_PATH)" <tools>/Heddle.Tool.dll compile @<rsp>` (falling back to `dotnet` on `PATH`), parsing
stdout in the canonical MSBuild diagnostic format (`LogEventsFromTextOutput` default). The host loads the image set
into one isolated `AssemblyLoadContext` per invocation whose resolver serves `Heddle`, `Heddle.Language`,
`Antlr4.Runtime.Standard` and `Microsoft.CodeAnalysis*` from the host's own default context (so engine types unify)
and every other name from the image set by simple name; framework names fall to the host runtime, which is what
makes an assembly `framework` per [AC-4](artifact-contract.md#ac-4--type-identity). For the invocation's duration the
host also subscribes `AssemblyLoadContext.Default.Resolving` and answers image simple names (never the engine set) from
the image set, because the engine's C# tier loads its emitted assembly with `Assembly.Load(byte[])` and that
assembly's model-type references resolve through the default context. Each image is registered:
`HeddleTemplate.Register(image)` (extensions by `[ExportExtensions]`, model assemblies by `[HeddleModelAssembly]`),
`AssemblyHelper.RegisterModelAssemblies(all images)`, and a build `FunctionRegistry` filled by `RegisterFrom(image)`
for every image carrying `[ExportFunctions]` (an ineligible container throws the engine's `ArgumentException`,
reported as `HED7021`). An image that fails to load is `HED7036` (error). A BCL member the template reads that exists
on the host runtime but not on the project's target framework compiles at build and is a `MemberBindingMismatch` at
load on that target (window item 8).

**Rationale.** PD6; D11 at build is the same rule as at run — the build registers what the project declares.
**Alternatives rejected.** Binding over reference assemblies (`ref/` images strip bodies and internals, so hooks cannot
run); a shared load context (an extension package referencing another `Heddle` version would load two engines).

### P2-R4 — The `heddle compile` contract

**Decision.** `heddle compile @<rsp>` — one argument per line, UTF-8, verbatim:
`--project <path>`; `--root <dir>`; `--output-profile`, `--expression-mode`, `--trim-directive-lines`,
`--max-recursion-count`, `--generated-namespace` (values as the MSBuild properties; parsed through
`HeddleBuildOptions.TryRead*`, an unparsable value is `HED7009`); `--template <path>|<key>|<name>|<modelType>|<outputProfile>`
per precompiling item (empty fields allowed; `<key>` empty means path-derived); `--import-only <path>|<key>|<name>`
per `Precompile="false"` item (in the `@<<` import map, validated for `HED7004`, no entry); `--reference <path>` per
image; `--engine-reference <path>`; `--build-version <ver>`; `--artifact-out <path>`; `--source-out <path>`;
`--stamp <path>`; modes `--probe <json-out>` and `--stubs-only <source-out>`. Outputs: the artifact file, one `.g.cs`
source file, the stamp. Diagnostics go to stdout as
`{file}({line},{col},{endLine},{endCol}): {error|warning|info} HED{nnnn}: {message}` — `file` is the `.heddle` path
for template facts and `--project` for project facts, positions 1-based from `LineIndex` (a diagnostic without a
position prints `(1,1)`). Exit codes: `0` no errors; `1` errors reported; `2` usage/response-file error; `3` host
fault before any template compiled (message on stderr). `heddle render` is unchanged.

**Rationale.** A response file survives long item lists on Windows; the canonical format is what `ToolTask` parses
without custom code, and `info` lines become messages.
**Alternatives rejected.** JSON diagnostics (needs a parser in the task).

### P2-R5 — Incrementality

**Decision.** The `_HeddleCompile` target declares `Inputs="@(HeddleTemplate);@(_HeddleImplementationImage);$(MSBuildAllProjects)"`
and `Outputs="$(_HeddleArtifactPath);$(_HeddleSourcePath);$(_HeddleStampPath)"` under
`$(IntermediateOutputPath)heddle/`. Inside, the host computes the **stamp**: SHA-256 over `--build-version`, every
option value, every `--template`/`--import-only` line with the item's `ContentHash`, and the MVID
(`System.Reflection.Metadata` `ModuleDefinition.Mvid`) of every `--reference` image, in response-file order. When
the stamp file equals the digest and both outputs exist, the host exits `0` writing nothing (message `up to date`);
otherwise it compiles and rewrites all three. Artifact bytes are therefore a function of exactly those inputs
([AC-9](artifact-contract.md#ac-9--determinism)). Outputs join `@(FileWrites)`; the artifact joins
`@(EmbeddedResource)` with `LogicalName="Heddle.CompiledForm"` and the source joins `@(Compile)` before
`CoreCompile`. The stubs pass (P2-R6) writes none of these three outputs.

**Rationale.** Plan exit criterion 5: an unchanged rebuild runs no compile; a rebuilt referenced project with an
unchanged MVID (deterministic build) runs the host but compiles nothing; a changed one compiles.
**Alternatives rejected.** Timestamps alone (a touched reference recompiles everything).

### P2-R6 — Same-project models and design-time builds

**Decision.** ([PD7](../../plan/precompilation-v2/decisions.md#pd7--same-project-models-and-design-time-builds))
Target `_HeddleProbe` runs `heddle compile --probe` first: a parse-only pass that collects each item's model spelling
(`@model` directive or `ModelType` metadata, resolved through the engine's `TypeSpelling` over the image set) and
writes `probe.json` with `stubs` (sanitized name, model spelling, generated namespace) and `unresolved` (spellings no
image resolves). When `unresolved` is non-empty and `@(Compile)` is non-empty, target `_HeddleIntermediateCompile`
runs the `Csc` task over `@(Compile)` (minus the generated source) plus a stub file declaring every
`Heddle.Generated.<Name>` wrapper as `public static class` with `Generate` overloads whose bodies `throw`, against
`@(ReferencePathWithRefAssemblies)`, into `$(IntermediateOutputPath)heddle/models/<digest>/$(AssemblyName).dll` —
the project's own assembly name, so identities recorded from it equal the identities the built assembly carries at
run time; `<digest>` is SHA-256 over the source paths and content hashes, the reference MVIDs and the analyzer paths,
and an existing directory is reused. The target runs `AfterTargets="BeforeCompile"` (so `GenerateGlobalUsings` and
`GenerateAssemblyInfo` have produced their sources) and `BeforeTargets="_HeddleCompile"`. The `Csc` call forwards
`DefineConstants`, `LangVersion`, `Nullable`, `AllowUnsafeBlocks`, `CheckForOverflowUnderflow` and `@(Analyzer)` (so
source generators run) from the project, sets `TreatWarningsAsErrors=false` and suppresses all warnings; a `CS` error
fails the build as the compiler's own error (the project's real compile fails the same way). That assembly joins the implementation set. When `$(DesignTimeBuild) == 'true'` or `$(BuildingProject) != 'true'`,
only `--stubs-only` runs, writing the wrapper stubs to `$(IntermediateOutputPath)heddle/stubs/Heddle.Generated.Stubs.g.cs`,
which only the design-time path adds to `@(Compile)`; no engine compile, image load, artifact, source or stamp is
produced, so a following real build finds `_HeddleCompile` out of date. Models in referenced assemblies need neither
step.

**Rationale.** The build cannot see a type the project it is building declares without compiling it once; the
probe makes the double compile conditional and content-addressed, and keeping the stubs off the real build's output
set keeps the up-to-date check honest.
**Alternatives rejected.** Roslyn-compiling the sources inside the host (reproduces csc's argument surface).

### P2-R7 — Generated source and typed entry points

**Decision.** The source file contains: (1) `[assembly: HeddleCompiledTemplates(typeof(<ns>.HeddleArtifact),
PrecompiledSchema.CompiledFormSchemaVersion, "<engine version>")]`; (2) `internal sealed class HeddleArtifact :
IHeddleCompiledArtifact` returning the resource stream; (3) per template a `public static class <SanitizedName>` in
`$(HeddleGeneratedNamespace)` with

```csharp
public static string Generate(TModel model, object chained = null, object callerData = null);
public static void Generate(TModel model, System.IO.TextWriter writer, object chained = null, object callerData = null);
public static void Generate(TModel model, System.Buffers.IBufferWriter<byte> writer, object chained = null, object callerData = null);
```

where `TModel` is the declared model type (`object` for `:: dynamic` and model-less). Each wrapper binds once, on
first call, through `PrecompiledTemplates.BindTyped(typeof(HeddleArtifact).Assembly, "<key>", typeof(TModel))` and
keeps the returned `HeddleTemplate`; the three overloads forward to `HeddleTemplate.Generate`. `BindTyped` registers
the assembly (idempotent), resolves the key, runs the gauntlet — the model-type step against `TModel`; the options
step comparing `ExpressionMode` and `TrimDirectiveLines` against `PrecompiledTemplates.DefaultOptions ?? new
TemplateOptions()`, not `OutputProfile` — materializes, and returns the adapter
`HeddleTemplate(strategy, options.Encoder, options.RenderBudget, effectiveOptions, entry.ModelType)` where
`effectiveOptions` is `DefaultOptions` with `OutputProfile` set to the row's baked profile; a gauntlet failure throws
`PrecompiledMismatchException(key, reason, detail)`; a key the assembly's artifact lacks throws
`InvalidOperationException`. Nothing is cached on failure, so a later call after the host fixes configuration binds.
`SanitizeName` keeps its rule; the name `HeddleArtifact` is reserved and a template sanitizing to it, or two templates
sanitizing to one name, is `HED7010`.

**Rationale.** [PD2](../../plan/precompilation-v2/decisions.md#pd2--typed-entry-points): a typed entry has nothing
to degrade to, so it validates once and throws; the item declared its own profile, so a typed call to it renders that
profile whatever the process default is, while `TryResolve` keeps comparing the full triple because a registry lookup
is a request for the requested profile.
**Alternatives rejected.** Static-field initialization (a throw becomes `TypeInitializationException` and the class
is dead for the process).

### P2-R8 — Build diagnostics

**Decision.** For every template the host runs the engine's compile and reports each `HeddleCompileError`/`Warning`
with its own id at its `.heddle` position; an engine diagnostic without an id is reported under `HED7012` (error) /
`HED7013` (warning) carrying the engine's sentence. Build-only facts keep their ids: `HED7001`, `HED7002`, `HED7003`,
`HED7004`, `HED7009`, `HED7010`, `HED7011`, `HED7014` (refusal class (c), warning, at the call), `HED7018`, `HED7020`
(the host threw compiling one template — reported, the pass continues), `HED7021`, `HED7028`, `HED7031`, `HED7032`,
`HED7033` (class (a), verbatim reason), `HED7035`, `HED7036`. `HED7031` is `Info`, once per template whose row
carries a refusal site, a late-bound site or a C# site carried as data, naming each: `not fully precompiled: refusal
site at (12,4) UnbindableCallTyping 'toItems'; late-bound functions: toUpper, slug; 1 C# site carried as data`. The
host does not raise `HED7005`, `HED7006`, `HED7007`, `HED7008`, `HED7016`, `HED7017`, `HED7019`, `HED7022`,
`HED7023`, `HED7024`, `HED7025`, `HED7030` or `HED7034`; phase 4 retires them in place with the mapping.

**Rationale.** [PD11](../../plan/precompilation-v2/decisions.md#pd11--engine-ids-at-build); PD9 asks the build to
report late-bound names.
**Alternatives rejected.** Making `HED7031` a warning (late binding is designed behaviour, and strict mode is the AOT
host's gate).

### P2-R9 — Registry and gauntlet carry over

**Decision.** `Register`, `TryGet`, `TryResolve` (both overloads), `Validate`, `ValidateAll`, `Entries`,
`OnFallback`, `BindingResolver` keep their signatures and semantics. `ValidateAll` runs every gauntlet step except
model type over rows without materializing. `TryResolve` materializes on a gauntlet pass; a materialization fault is
reported through `OnFallback` as `ExtensionInitCompileError`, `ExtensionInitTypingMismatch` or
`MemberBindingMismatch` and throws under `Strict`.

**Rationale.** Plan outcome: same registry, gauntlet, taxonomy and ids; hosts change no call.

### P2-R10 — In-repo consumers

**Decision.** `samples/precompiled-app` references `Heddle.Build` through the in-repo project (`ProjectReference`
to `src/Heddle.Build` with the props/targets imported from `src/Heddle.Build/build/` and `HeddleToolPath` pointing at
the built `Heddle.Tool`), adds `PrecompiledTemplates.ValidateAll` before rendering, returns to the `samples.yml`
matrix and keeps its four goldens byte-identical (`discovery.txt` drops the `precompiled=` column in phase 4 with
`IsPrecompiled`). `benchmarks/dotnet` returns its precompiled tier through `Heddle.Build` with
`<HeddleTemplate Update="templates\controlled\heddle\fortunes-encoded.heddle" OutputProfile="Html" />` and likewise
for `encoded-loop.heddle`; `gate-precompiled` reports 8/8 on three sinks and its usage text lists the verb.
`samples/codegen-t4-successor` moves the same way, keeping `ModelType`, `Name`, `Precompile="false"`. The benchmark
backend (`benchmarks/dotnet/src/Engines/PrecompiledBackend.cs`) renders through the public typed route —
`PrecompiledTemplates.DefaultOptions` set to the cell's options, `PrecompiledTemplates.BindTyped(assembly, key,
modelType)` once per workload, then `HeddleTemplate.Generate` on the three sinks — the same objects a generated wrapper
uses; it holds no `InternalsVisibleTo`.

**Rationale.** The consumers are the plan's exit criterion 1 and the byte-identity proof for the package; the typed
route is the public one that materializes exactly once.
**Alternatives rejected.** The registry-first `TemplateResolver` path (adds a resolver and file-system ladder to a
render measurement).

### P2-R11 — Tests evaluate real MSBuild

**Decision.** New suite `src/Heddle.Build.Tests` (`net10.0`, xUnit v3 on MTP, its own `test-classes.txt`,
`TestInventory.props`) that writes fixture projects to a temp directory, runs `dotnet build` against the in-repo
`Heddle.Build` props/targets and the built `Heddle.Tool`, and asserts on the binary log and outputs: every public
item, metadatum and property changes the artifact or a diagnostic; an unchanged rebuild skips `_HeddleCompile`; a
rebuilt referenced model project runs it; a design-time build yields compiling stubs and a real build after it runs
`_HeddleCompile`; `HED7035` and `HED7036` fire with their positions; `net48`, `net8.0`, `net10.0` targets build with
one package; `netstandard2.0` and `net8.0` class-library fixtures whose model type comes from a NuGet package
precompile (the image set holds the package's runtime image); an `ImplicitUsings` project with a `[GeneratedRegex]`
source generator and a same-project model completes its intermediate compile. `Heddle.Tool.Tests` gains `CompileVerbTests`. The phase 1 harness gains a pass over host-built
artifacts.

**Rationale.** A build-surface contract verified only through injected values is unverified; something must evaluate
MSBuild.

## Implementation plan

| # | Work item | Files | Done when |
| --- | --- | --- | --- |
| P2-W1 | `compile` verb | edit `src/Heddle.Tool/Program.cs` (dispatch); add `src/Heddle.Tool/Compile/{CompileCommand,ResponseFile,ImageLoadContext,DiagnosticWriter,Stamp,Probe,SourceEmitter,SanitizeName}.cs` | corpus compiles through the verb; `CompileVerbTests` green |
| P2-W2 | `BindTyped`/`DefaultOptions` | edit `src/Heddle/Precompiled/PrecompiledTemplates.cs`, `PrecompiledGauntlet.cs` (typed options step) | `PrecompiledTypedEntryTests` green |
| P2-W3 | Package | add `src/Heddle.Build/Heddle.Build.csproj`, `src/Heddle.Build/Tasks/HeddleCompile.cs`, `src/Heddle.Build/build/Heddle.Build.props`, `src/Heddle.Build/build/Heddle.Build.targets`; edit `Heddle.sln` | `dotnet pack` yields the layout in P2-R1 |
| P2-W4 | Consumers | edit `samples/precompiled-app/PrecompiledApp.csproj`, `Program.cs`; `samples/codegen-t4-successor/CodegenT4Successor.csproj`; `samples/Heddle.Samples.slnx`; `.github/workflows/samples.yml`; `benchmarks/dotnet/Heddle.Benchmarks.Dotnet.csproj`; `benchmarks/dotnet/src/Engines/PrecompiledBackend.cs` (typed route), `benchmarks/dotnet/src/Bench/TechniqueBenchmarks.cs` (`TechniquePrecompiledBenchmarks` over the compiled-form tier, row names unchanged), `benchmarks/dotnet/Program.cs` | sample goldens unchanged; `gate-precompiled` 8/8; `TechniquePrecompiledBenchmarks` allocated bytes equal `TechniqueRuntimeBenchmarks`' per workload × sink |
| P2-W5 | Suite | add `src/Heddle.Build.Tests/` (csproj, `test-classes.txt`, `MsBuildFixture.cs`, `SurfaceTests.cs`, `IncrementalityTests.cs`, `DesignTimeBuildTests.cs`, `VersionLockTests.cs`, `MultiTargetTests.cs`, `ClassLibraryImageTests.cs`, `IntermediateCompileTests.cs`, `TestClassInventoryTests.cs`); edit `.github/workflows/dotnet.yml` (leg), `.gitattributes` (`src/Heddle.Build.Tests/Fixtures/** text eol=lf`), `CLAUDE.md` (gate names four suites) | suite green on Linux and Windows |
| P2-W6 | Ids and docs | edit `src/Heddle/Data/HeddleDiagnosticIds.cs`, `HeddleDiagnosticCatalog.cs` (`HED7035`, `HED7036`), `docs/precompilation.md` (rows for both so `DiagnosticIdTests` passes; the registry rows are claimed with this spec) | `DiagnosticIdTests` green |

## Public API contract

```csharp
namespace Heddle.Precompiled
{
    public static class PrecompiledTemplates
    {
        /// <summary>The process-wide options typed entry points render under and validate against; null means
        /// engine defaults. Assign before the first typed call — the same instance a host passes to ValidateAll.
        /// A typed entry renders its item's baked OutputProfile whatever this instance says.</summary>
        public static TemplateOptions DefaultOptions { get; set; }

        /// <summary>Binds a typed entry: registers the assembly, resolves the key, runs the gauntlet (model-type step
        /// against modelType; options step on ExpressionMode and TrimDirectiveLines only) under DefaultOptions,
        /// materializes, and returns a precompiled-adapter template rendering the item's baked profile.
        /// Throws PrecompiledMismatchException on a gauntlet failure and InvalidOperationException when the
        /// assembly's artifact has no such key. Thread-safe.</summary>
        public static HeddleTemplate BindTyped(System.Reflection.Assembly assembly, string key, System.Type modelType);
    }

    public static class HeddleBuildOptions
    {
        /// <summary>The per-item metadatum overriding HeddleOutputProfile for one HeddleTemplate item.</summary>
        public const string OutputProfileMetadata = "OutputProfile";
    }
}
```

Generated wrappers (P2-R7) are the consumer's public API, not `Heddle.dll`'s.

## Diagnostics

| Id | Severity | Trigger and message | Position |
| --- | --- | --- | --- |
| `HED7035` | Error | The host's engine version ≠ the referenced `Heddle.dll` version. `Heddle.Build {build} compiles with Heddle {host} but the project references Heddle {referenced}; reference the same version of both packages.` | project file `(1,1)` |
| `HED7036` | Error | An implementation image cannot be loaded. `Implementation assembly '{path}' could not be loaded: {loader message}. Templates naming its types cannot be compiled; fix the reference or exclude the templates with Precompile="false".` | project file `(1,1)` |

Both are claimed in the [registry](../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry) with owner
[precompilation.md](../../precompilation.md); `HED7037` is claimed there too and specified by
[phase 4](phase-4-removal-and-release-tail.md#diagnostics). `HED7031` is `Info` (P2-R8). Engine ids are forwarded
verbatim.

## Testing plan

**TDD verdict.** Test-first: the response-file/diagnostic-line contract, incrementality (skip/run), the version lock,
`BindTyped` throwing `PrecompiledMismatchException`, per-item `OutputProfile` through a typed entry, the LSP/build
id-and-position agreement. Test-with: the task and targets plumbing (asserted through `Heddle.Build.Tests` binary logs).

- `Heddle.Build.Tests` per P2-R11; the plan's validation scenarios (referenced-project models without an intermediate
  compile; same-project models with one, reused on rebuild, its identities equal to the built assembly's; `net48`/
  `net8.0`/`net10.0` on one machine; a BCL member absent on `net48` building and `ValidateAll` on a `net48` host
  reporting `MemberBindingMismatch`); a `FullCSharp` template naming a package-sourced model type compiles at build
  (the default-context `Resolving` hook).
- `PrecompiledTypedEntryTests` (`Heddle.Tests`) — a wrapper whose artifact fails validation throws
  `PrecompiledMismatchException`; with `DefaultOptions` assigned, a late-bound function binds against its registry;
  two items in one assembly with `OutputProfile="Html"` and `"Text"` under `DefaultOptions.OutputProfile = Text`:
  both typed entries render their own profile's bytes while `TryResolve` under `Text` options serves only the
  `Text` item and reports `OptionsMismatch` for the other.
- `BuildAndEditorDiagnosticParityTests` — one template with a `HED1004` and one with a `HED5002`: the host's
  diagnostic line and `HeddleDiagnosticProjection`'s LSP projection carry the same id, line and column.
- Gauntlet exit-criterion matrix — stale template, changed import, different `OutputProfile` request, swapped
  `[ExtensionReplace]`, renamed model member, missing function: each a `PrecompiledFallbackEvent` with reason and
  `HED7101`, none an exception, through `TryResolve` and `ValidateAll`.
- LSP parity: `WorkspaceOptionParityTests` green (the surviving constants are the same objects).
- Sample: `samples/precompiled-app` goldens byte-identical; `samples.yml` rows restored.
- Benchmarks: `gate` and `gate-precompiled` green, 8/8, three sinks.
- `test-classes.txt` for `Heddle.Tests`, `Heddle.Tool.Tests`, `Heddle.Build.Tests`.

## Back-compat and migration

Window item 5 (typed entries render under `DefaultOptions` and throw on a failed validation) and item 8 (host ≠
target: a load-time fallback, not a build error) land here. Additive to `Heddle.dll` (three members). The MSBuild
surface is spelling-compatible; the five retired properties are inert here and warned or silent in phase 4 (item 4).
No rendered byte changes: sample and benchmark goldens are the proof.

## Performance considerations

Build time: one host process per compiling project per changed input, plus a probe process; the intermediate compile
only when a same-project model exists. Render path: untouched; a typed wrapper's steady state is one field read.
Guarded by `gate-precompiled` and `TechniquePrecompiledBenchmarks` (retargeted to the compiled-form tier in P2-W4 with its row names unchanged; equal allocation, mean within error against `TechniqueRuntimeBenchmarks`).

## Standards compliance

DRY: option names and defaults stay single-sourced in `HeddleBuildOptions`, read by targets (lockstep test), host and
LSP; `SanitizeName` moves to `Heddle.Tool` unchanged. YAGNI: no `heddle compile` options beyond what the targets
pass, no in-process task, no per-RID host. Simplicity over abstraction: one task, one verb, one response file.

## Deferred items / non-goals

A self-contained host per RID (trigger: a build machine that cannot carry a .NET 10 runtime); a satellite assembly
for generated code (trigger: the intermediate compile measured too slow); an options-carrying typed overload (trigger:
hosts rendering under several shapes from typed entries); generated sites (phase 3).

## External references

- [`ToolTask`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.utilities.tooltask) /
  [canonical diagnostic format](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks)
- [MSBuild incremental builds](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-build-incrementally)
- [Design-time builds](https://github.com/dotnet/project-system/blob/main/docs/design-time-builds.md)
- [`Csc` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/csc-task)
- [`AssemblyLoadContext`](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [`ModuleDefinition.Mvid`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.moduledefinition.mvid)
- [`DOTNET_HOST_PATH`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_host_path)
- [NuGet `buildTransitive`](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets)
