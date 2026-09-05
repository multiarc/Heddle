# Phase 3 — Generated sites and evidence

**Status:** Specified — ready for implementation. **Plan item:**
[phase-3-aot-sites.md](../../plan/precompilation-v2/phase-3-aot-sites.md). **Entry document:** [README.md](README.md).
**Contract:** [artifact-contract.md](artifact-contract.md). **Depends on:** [phase 1](phase-1-compiled-form.md) (site
ids, loader) and [phase 2](phase-2-build-integration.md) (`heddle compile`, generated source) merged.

## Scope and goal

Remove load-time compilation for the delegate classes the compiled form can only describe. The host prints, from the
engine's bound trees held in memory at build, one typed static method per member accessor, native expression and
embedded C# site into the consumer's generated source, addressable by site id through a site table; the loader
prefers a generated site and rebuilds any declined or absent site from data. A strict mode turns any remaining
load-time compilation into a fail-fast host error so AOT hosts and the corpus gate can prove coverage. Then the tier's
claims are measured: render parity per workload × sink, a cold-start row, artifact size and materialization time, and
a NativeAOT sample.

## Assumed state

With phases 1–2 merged and verified against source (commit `654959b5`):

- Native expressions compile through `NativeExpressionCompiler.Visit` over the closed vocabulary `LiteralNode`,
  `ThisNode`, `PathNode`, `IndexNode`, `CallNode`, `MethodCallNode` (always `HED1003`), `UnaryNode`, `BinaryNode`,
  `TernaryNode`; typing goes through `NativeOperatorRules.Classify`/`ClassifyUnary`/`ClassifyTernary` with
  `OperatorVerdict { Supported, RequiresRuntimeSemantics, NotDefined }`, `NumericTable.TryPromote`/`UnaryPromote`,
  overload selection through `OverloadRank.Bind` over `FunctionEntry { Method, Target, ParameterTypes,
  HasParamsArray, ParamsElementType }`, arguments through `BuildCallArguments`; the result is a `CompiledParameter`
  (`Func<object,object,object,object>` over model, chained, root), a `PropsCompiledParameter`
  (`Func<object,object,object,object[],object>`) when props are read, or a folded `ConstantParameter`.
- Member accessors are `ModelParameter.BuildNullSafePropertyChain(Expression, IEnumerable<(Type, PropertyInfo)>)`
  applying `MemberHopRule.Form(receiverIsValueType, propertyIsNonNullableValueType)` →
  `HopForm { Direct, NullConditional, NullDefaultConditional }`; the `NullDefaultConditional` arm is
  `Expression.Condition(receiver == null, Expression.Default(propertyType), receiver.Property)`; a dynamic hop is
  `DynamicParameter`'s DLR site bound in `Heddle`'s binder context.
- Embedded C# compiles as `Heddle.Runtime.CSE_{guid:N}.ProcessData_{ExtensionName}{n}(model, chained, root)` through
  `CSharpContext.PushCompileExpression` and `ContextCompilation.CompileCSharp`, a value with no static type declared
  `dynamic`, behind `HeddleFeatures.CSharpTierEnabled`.
- Reflection the engine performs after compile: `TemplateFactory.CreateExtension` (`Activator.CreateInstance` over a
  registered extension type; built-ins enumerated from `typeof(TemplateFactory).Assembly` in `LoadBaseExtensions`),
  `ListExtension.InitStart` (`Activator.CreateInstance(typeof(CountReader<>).MakeGenericType(elementType))`, whose
  count only pre-sizes the result array), `DynamicParameter` DLR sites.
- `TechniquePrecompiledBenchmarks` renders the compiled-form tier over `Engines.Precompiled.CoveredWorkloads()`;
  `ColdCompileBenchmarks` (`[MemoryDiagnoser]`, default job) has `ParseHeddle` (baseline) and `CompileHeddle`
  (`composed-page` under `Text`/`Native`) and no precompiled row; the report lives under `docs/benchmarks/<date>/`
  and cites `benchmarks/docs/metrics-protocol.md`.
- `Heddle.csproj` has `ILLink.Substitutions.xml` stubbing `HeddleFeatures.get_CSharpTierEnabled` to `false` under the
  `Heddle.CSharpTierEnabled` feature and declares neither `IsTrimmable` nor `IsAotCompatible`.
- `samples/README.md` conventions: two modes (human, `--capture out`), `golden/` committed, `compare-golden.sh`,
  a `samples.yml` matrix row per sample.

## Requirements

### P3-R1 — The site table

**Decision.** The generated `HeddleArtifact` class additionally implements

```csharp
public interface IPrecompiledSiteTable
{
    string ArtifactDigest { get; }                                    // AC-6, of the artifact it was printed from
    bool TryGetSite(string templateContentHash, int templateIndex, int siteOrdinal, out System.Delegate site);
}
```

returning, per site id, a delegate of the site kind's shape whose creation happens once per process. The loader, at
materialization, casts the artifact object to `IPrecompiledSiteTable`; when the cast succeeds, `ArtifactDigest`
equals the loaded artifact's digest, and the AppContext switch `Heddle.Precompiled.UseGeneratedSites` (default `true`)
is not `false`, every delegate-bearing site first asks the table and uses the delegate when its type matches the
kind's shape; otherwise the site is rebuilt from data as in phase 1. A digest mismatch or a delegate of the wrong
shape is a host fault (`InvalidOperationException` naming the site) — both mean the source and the artifact came
from different compilations, which the build never produces.

**Rationale.** A table keyed by the site id is exactly the [AC-6](artifact-contract.md#ac-6--site-ids-and-the-artifact-digest)
contract; the digest ties the table to one artifact; the switch gives the evidence runs their "without" arm through the
public surface.
**Alternatives rejected.** A dictionary populated by reflection over method names (allocation and a name contract).

### P3-R2 — The printer and what it declines

**Decision.** `Heddle.Tool/Compile/Sites/` holds the printer (`SitePrinter`, `MemberAccessorPrinter`,
`NativeExpressionPrinter`, `CSharpSitePrinter`, `TypeNamePrinter`); `src/Heddle/Properties/AssemblyInfo.cs` grants
`InternalsVisibleTo("Heddle.Tool, PublicKey=…")` so the printer reads the compiler's bound tree records the phase 1
recording produces (per site: the resolved `MemberPathResolution`, the typed bound expression with the chosen
overload, promotion kinds, hop forms and result type, the C# source with its scope types). The printer never reads
template text. It **declines** a site — records the decline on the artifact row and prints nothing for it — when:
a type or member it must name is not nameable from the consumer's assembly (a non-public type, an `internal` member
of another assembly without `[InternalsVisibleTo]`, an `[Obsolete(error: true)]` member, a type with no C# spelling
such as a compiler-generated name); the expression contains a deferred call (late-bound sites are never generated);
a chosen `FunctionEntry` has a delegate `Target` rather than a public static `Method`; a path contains a
`DynamicHop`; an operator's verdict is `OperatorVerdict.RequiresRuntimeSemantics`; a bound node kind has no printer
arm. Declines are listed in the template's `HED7031` notice (`n sites rebuilt at load: <kinds and positions>`).

**Rationale.** Generated code is an optimisation over a complete data path; a decline costs one site and never the
template.
**Alternatives rejected.** `HED7030` for unnameable members (a warning for a zero-cost outcome).

### P3-R3 — Member accessor sites

**Decision.** Printed shape per site (member kind `Func<object,object>`, native-path kind
`Func<object,object,object,object>`):

```csharp
private static object M_<ordinal>(object model)
{
    var v0 = (Acme.Order)model;                              // the engine's start cast: InvalidCastException on mismatch
    var v1 = v0.Customer;                                    // Direct
    var v2 = v1?.Address;                                    // NullConditional
    return v2 == null ? (object)default(decimal) : (object)v2.Value; // NullDefaultConditional: Expression.Default(TValue)
}
```

Each hop uses the `HopForm` the engine chose for that hop; a `NullDefaultConditional` hop yields the boxed
`default(TValue)` on a null receiver exactly as `Expression.Default(propertyType)` does; the result is boxed exactly
once when the last member is a value type; a `null` receiver of a `NullConditional` hop propagates `null`. Static,
`[Hidden]` and inaccessible members never reach the printer (the engine did not bind them).

**Rationale.** Plan exit criterion 5 — no allocation beyond the engine's value-type box — and the hop semantics are
the engine's table, not C#'s defaults.

### P3-R4 — Native-expression sites

**Decision.** Printed from the typed bound tree: every operand cast to its promoted `NumericKind` CLR type before the
C# operator is applied, so C#'s own promotion never decides; an operator whose verdict is
`RequiresRuntimeSemantics` is declined (P3-R2) and `NotDefined` is the engine's build error; `Coalesce` and
`Ternary` print with the engine's common type as an explicit cast; string concatenation prints `string.Concat` as
the engine's `ConcatStringString`/`ConcatObjectObject` choice; function calls print as a direct call to the chosen
public static method with `BuildCallArguments`' conversions and `params` expansion spelled out; indexers print the
indexer the engine bound; `ThisNode` prints the scope parameter; prop reads print `props[i]` from the `object[]`
parameter (kind `Func<object,object,object,object[],object>`). A folded constant is data in the artifact (phase 1),
so it is not a site. A constant division by zero is the engine's `HED1018` at build; the printer never sees it. A
prop argument's site includes the slot conversion the engine applies (`Convert.ChangeType` to the slot's underlying
type under `InvariantCulture`) so the loader installs an identity converter.

**Rationale.** Semantics come from the bound tree the engine produced, so the printed method is the compiled
expression tree spelled in C#; anything the tree does not decide is declined rather than guessed.
**Alternatives rejected.** Printing from the syntax tree (re-derives typing).

### P3-R5 — Embedded C# sites

**Decision.** A `CSharp` site prints as the same static method the engine's `CSharpClassTemplate.tcs` emits —
`ProcessData_<ExtensionName><n>(<model type> model, <chained type> chained, <root type> root)` with `dynamic` where the
engine used it — inside `namespace Heddle.Runtime { internal static class CSE_<sanitized template name> { … } }` in
the generated source, preceded by the site's `@using` namespaces; the wrapper the table exposes boxes the result. The
consumer's compiler compiles it with `#pragma warning disable` over the generated file. A template whose C# sites all
print carries none as data, and a host rendering only such templates loads no `Microsoft.CodeAnalysis` type
(`Heddle.CSharpTierEnabled=false` then trims it). Sites the consumer's compiler would reject for a name it cannot
spell are declined by P3-R2's rule, using the same reflection view the engine binds with.

**Rationale.** [PD5](../../plan/precompilation-v2/decisions.md#pd5--embedded-c-before-generated-sites) completes
here; the enclosing namespace keeps name lookup identical to the engine's generated assembly.

### P3-R6 — Strict no-load-time-compilation mode

**Decision.** ([PD10](../../plan/precompilation-v2/decisions.md#pd10--strict-no-load-time-compilation-mode))
AppContext switch `Heddle.Precompiled.StrictLoad` (default unset = `false`) seeds
`TemplateOptions.PrecompiledStrictLoad` (bool; copied by the copy constructor; not part of `Equals`/`GetHashCode`).
When the materializing request's options carry `PrecompiledStrictLoad == true`, any of the following during
materialization throws `PrecompiledStrictLoadException(templateKey, siteOrdinal, siteKind)`: an expression-tree
`Compile()` for a member accessor or native expression the table did not serve; a Roslyn compile for a C# site the
table did not serve; a refusal site's text compile; the late-bound site's expression-tree compile against the request's
registry (`siteKind` `"LateBound"` — a strict host binds its functions at build through `[ExportFunctions]` or a
build-visible declaration). **Declared exceptions, permitted under strict mode:** DLR call-site creation for a
`DynamicHop` (`:: dynamic` and model-less templates — a declared class outside the NativeAOT claim) and the hook-level
reflection the engine performs on every tier — `Activator.CreateInstance` over a registered extension type and
`ListExtension`'s count reader — which is not compilation. `ValidateAll` does not materialize and is unaffected; a
typed entry under strict mode throws the exception from `BindTyped`.

**Rationale.** A test can prove site coverage only by making the alternative fail; an AOT host fails at startup rather
than on the first request.
**Alternatives rejected.** Reporting through `OnFallback` (nothing degraded — the site would compile fine).

### P3-R7 — Parity with and without the table

**Decision.** The phase 1 harness runs every corpus row twice — `Heddle.Precompiled.UseGeneratedSites` on and off —
both byte-identical to the dynamic tier on three sinks. Under `Heddle.Precompiled.StrictLoad` the set of rows that
throw `PrecompiledStrictLoadException` equals, by set equality, the rows whose `refusals` are non-empty, the rows
declaring `lateBound` (an intent-row field naming the late-bound function names), and rows declaring `printerDeclines`
(an optional field naming the declined site kinds with a `Why`); the corpus is expected to declare none of the last,
and a decline the printer records on a row without the field is a red gate.

**Rationale.** The "without" arm proves the data path is complete under the table; the strict-mode set equality is
plan exit criterion 1 in executable form.

### P3-R8 — Evidence

**Decision.** `TechniquePrecompiledBenchmarks` renders the compiled-form tier over 8 workloads × 3 sinks with the
site table on, and a new `TechniquePrecompiledDataOnlyBenchmarks` with it off; the published table places each beside
`TechniqueRuntimeBenchmarks` on the same machine and job: mean within BenchmarkDotNet's reported error, allocated bytes
equal. A new `StartupBenchmarks` class under `RunStrategy.ColdStart` (`launchCount` 20, one invocation per launch)
carries `CompileHeddle` (text: `new HeddleTemplate(new CompileContext(…))` for `composed-page`),
`RegisterAndRenderCompiledForm` (`PrecompiledTemplates.Register` + `BindTyped` + first `HeddleTemplate.Generate`,
table on) and its data-only twin; the artifact path must be strictly below `CompileHeddle`. The `gate` verb's materialisation trailer
prints artifact size in bytes and materialization time per workload. The report (`docs/benchmarks/<date>/`) carries
these tables labelled per [metrics-protocol.md](../../../benchmarks/docs/metrics-protocol.md).

**Rationale.** Plan success criteria 3–4; cold start is a per-process fact, so it is measured per process.
**Alternatives rejected.** Measuring registration in-process with a registry reset (needs a non-public reset, and
warm-JIT numbers are not startup).

### P3-R9 — NativeAOT posture and sample

**Decision.** `Heddle.csproj` sets `IsTrimmable=true` and `IsAotCompatible=true` and the build carries zero trim/AOT
analyzer warnings: `TemplateFactory.LoadBaseExtensions` is rooted by one `[DynamicDependency]` per built-in extension
type (so `LoadExtensions` over the engine assembly finds them after trimming) and host extensions are rooted by the
`typeof` in `[ExportExtensions]`/`[ExportFunctions]`/`[HeddleModelAssembly]`; `ListExtension` keeps `CountReader<T>` for
reference-type element types (shared instantiation) and takes a non-generic `System.Collections.ICollection.Count`
path — else no count — for value-type element types, which changes only the pre-sizing of the result array, never a
byte; `DynamicParameter`'s DLR sites and the Roslyn tier are declared outside the AOT claim and trimmed by their
switches. `samples/precompiled-aot/` (`net10.0`, `PublishAot=true`, `InvariantGlobalization=true`,
`<RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" />`,
`<RuntimeHostConfigurationOption Include="Heddle.Precompiled.StrictLoad" Value="true" />`) references `Heddle` and
`Heddle.Build`, precompiles the typed controlled workloads (`composed-page`, `large-loop`, `mixed-page`,
`conditional-heavy`, `fragment-heavy`, `fortunes-encoded`, `encoded-loop`; `trivial-substitution` is model-less and
excluded by the declared class), exports its functions with `[ExportFunctions]` so no site is late-bound, renders each through its typed wrapper and
the registry under strict mode, and in `--capture` mode writes the rendered output plus `assemblies.txt` (loaded
assembly names, asserted to contain no `Microsoft.CodeAnalysis`). `samples.yml` gains the row; the publish log of the run that produced the report's numbers
is kept at `docs/benchmarks/<date>/precompiled-aot-publish.log`.

**Rationale.** Plan success criterion 5 needs a rooting story, not a hope: trimming removes what nothing references,
and the engine's two post-compile reflection sites are made either rooted or reflection-free for value types.
**Alternatives rejected.** `TrimmerRootAssembly` for `Heddle` (keeps the Roslyn call graph alive).

## Implementation plan

| # | Work item | Files | Done when |
| --- | --- | --- | --- |
| P3-W1 | Table contract and loader preference | add `src/Heddle/Precompiled/IPrecompiledSiteTable.cs`, `src/Heddle/Precompiled/PrecompiledStrictLoadException.cs`; edit `src/Heddle/Runtime/HeddleCompiler.Form.cs` (table lookup per site kind; strict-mode throws incl. `"LateBound"`), `src/Heddle/Runtime/HeddleFeatures.cs` (`UseGeneratedSites`, `StrictLoad` switches), `src/Heddle/Data/TemplateOptions.cs` (`PrecompiledStrictLoad`), `src/Heddle/Precompiled/PrecompiledTemplates.cs` (`BindTyped` propagates the exception) | a hand-written table serves a site; digest mismatch throws |
| P3-W2 | Printer | add `src/Heddle.Tool/Compile/Sites/*.cs`; edit `src/Heddle/Properties/AssemblyInfo.cs` (IVT to `Heddle.Tool`), `src/Heddle.Tool/Compile/SourceEmitter.cs` (table + methods), `src/Heddle/Runtime/CompileContext.cs` (record chosen overload, promotion kinds, hop forms) | corpus `Compiles` rows print every accessor and expression; declines recorded |
| P3-W3 | Strict mode and harness | edit `src/Heddle.Tests/CompiledFormHarness.cs`, `CompiledFormParityTests.cs`; add `StrictLoadTests.cs`, `GeneratedSiteTests.cs`, `GeneratedAccessorAllocationTests.cs`; edit `src/TestCorpus/CorpusIntent.cs` (`lateBound`, `printerDeclines`), `test-classes.txt` | P3-R7 set equality green |
| P3-W4 | AOT posture | edit `src/Heddle/Heddle.csproj` (`IsTrimmable`, `IsAotCompatible`), `src/Heddle/Runtime/TemplateFactory.cs` (`[DynamicDependency]` roots), `src/Heddle/Extensions/ListExtension.cs` (value-type count path) | zero IL2xxx/IL3xxx warnings on `dotnet build -c Release` |
| P3-W5 | Benchmarks | edit `benchmarks/dotnet/src/Bench/TechniqueBenchmarks.cs`, `benchmarks/dotnet/src/Bench/ColdBenchmarks.cs`; add `benchmarks/dotnet/src/Bench/StartupBenchmarks.cs`; edit `benchmarks/dotnet/Program.cs` (trailer, `bench-startup` verb), `benchmarks/docs/metrics-protocol.md` (the startup row's label and non-comparability) | tables published under `docs/benchmarks/<date>/` |
| P3-W6 | Sample | add `samples/precompiled-aot/` (csproj, `Program.cs`, `README.md`, `templates/`, `golden/`); edit `samples/README.md`, `.github/workflows/samples.yml` | publish and capture green on `ubuntu-latest` |

## Public API contract

```csharp
namespace Heddle.Precompiled
{
    /// <summary>Implemented beside IHeddleCompiledArtifact by a generated artifact class that carries generated
    /// sites. Stateless after type init; thread-safe.</summary>
    public interface IPrecompiledSiteTable
    {
        string ArtifactDigest { get; }
        bool TryGetSite(string templateContentHash, int templateIndex, int siteOrdinal, out System.Delegate site);
    }

    /// <summary>Thrown at materialization under PrecompiledStrictLoad when a site would compile at load.</summary>
    public sealed class PrecompiledStrictLoadException : System.InvalidOperationException
    {
        public PrecompiledStrictLoadException(string templateKey, int siteOrdinal, string siteKind);
        public string TemplateKey { get; }
        public int SiteOrdinal { get; }
        /// <summary>"MemberAccessor", "NativeExpression", "CSharp", "LateBound" or "RefusalSite".</summary>
        public string SiteKind { get; }
    }
}

namespace Heddle.Data
{
    public class TemplateOptions
    {
        /// <summary>Fail materialization instead of compiling a site at load. Default: the
        /// Heddle.Precompiled.StrictLoad AppContext switch, else false. Not part of options identity.</summary>
        public bool PrecompiledStrictLoad { get; set; }
    }
}
```

AppContext switches (documented in [csharp-api.md](../../csharp-api.md) by phase 4): `Heddle.Precompiled.StrictLoad`
(default `false`), `Heddle.Precompiled.UseGeneratedSites` (default `true`). Neither needs an
`ILLink.Substitutions.xml` entry: they are runtime decisions, not trim decisions.

## Diagnostics

No new `HED` id. `HED7031` (Info, phase 2) lists printer declines by kind and position. `PrecompiledStrictLoadException`
is a host error, not a diagnostic. The engine's `HED1018` and every other native-expression id keep firing at build.

## Testing plan

**TDD verdict.** Test-first: strict mode throwing for each site kind and permitting the declared classes; table digest
mismatch; hop-form semantics per `HopForm` including the `default(TValue)` box; mixed-numeric promotion
(`@(price * 1.2m + fee)` with `int`/`decimal`/`double` operands); decline rules (a referenced assembly's `internal`
member, a delegate-target function, a deferred call, a `RequiresRuntimeSemantics` operator). Test-with: the printer's
per-node arms (driven by the corpus parity run with the table on).

- `GeneratedSiteTests` — for each corpus `Compiles` row with the table on: every accessor and native expression is
  served from the table (probe: strict mode does not throw); rendered bytes equal the dynamic tier's; the hop-form
  parity test compares a printed `NullDefaultConditional` accessor against `Expression.Default(propertyType)`
  compiled by the engine over a null receiver.
- `StrictLoadTests` — P3-R7 set equality; `:: dynamic` and model-less templates render under strict; a late-bound
  function throws `"LateBound"` under strict and renders once the function is exported with `[ExportFunctions]` at
  build; a refusal site throws naming its ordinal and kind; a typed wrapper throws from `BindTyped`.
- Embedded C# scenario — `root` and `chained` with a `@using`: compiles in the consumer's assembly; an
  assembly-load probe (`AppDomain.CurrentDomain.GetAssemblies()`) after rendering shows no `Microsoft.CodeAnalysis`.
- Hook-typed body scenario — a custom extension whose `InitStart` chooses the body model type: member sites inside
  the body are served from the table when the hook answers the recorded type; a hook answering a different type at
  load is `ExtensionInitTypingMismatch` through the gauntlet (phase 1), never a decline.
- `GeneratedAccessorAllocationTests` (`GC.GetAllocatedBytesForCurrentThread` around 1,000 reads): a reference-type
  result allocates zero bytes; a value-type result allocates one box.
- AOT — `dotnet build -c Release` of `Heddle.csproj` reports zero trim/AOT analyzer warnings (asserted by the CI leg
  with `-warnaserror:IL2026;IL2067;IL2070;IL2072;IL2075;IL3050`); `ListExtension` over `List<int>` and `HashSet<int>`
  renders byte-identically to its pre-change bytes.
- Benchmarks and sample per P3-R8/P3-R9; `gate-precompiled` passes with the switch on and off (two runs in the
  benchmark CI leg).
- `test-classes.txt` for `Heddle.Tests`; `.gitattributes` covers `samples/precompiled-aot/golden/**` through the
  existing `samples/**/golden/**` pin.

## Back-compat and migration

Additive: one interface, one exception, one `TemplateOptions` property (copy constructor updated;
`TemplateOptionsCompletenessTests` covers it), two AppContext switches, two csproj properties, engine rooting
attributes. `ListExtension`'s value-type count path changes allocation shape, not bytes, and the change lands on both
tiers at once. No rendered byte changes.

## Performance considerations

Render path: a generated accessor is a direct call plus at most one box; the table lookup happens once per site at
materialization. Strict mode costs one bool read per site at materialization and nothing at render.
`ListExtension`'s value-type path is measured by `TechniqueRuntimeBenchmarks` (`large-loop` iterates value types) —
allocated bytes must not increase. Guarded by `TechniquePrecompiledBenchmarks` / `TechniquePrecompiledDataOnlyBenchmarks`
(equal allocation, mean within error), `StartupBenchmarks` (strictly below `CompileHeddle`), and
`GeneratedAccessorAllocationTests`.

## Standards compliance

DRY: the printer consumes the same bound records the loader consumes; hop forms, promotion and overload choice exist
once in the engine. YAGNI: no generated sites for constants (data), props binders (data plus `Convert.ChangeType`),
or late-bound calls (data by decision); no MSBuild property for the "without table" arm — the switch serves the
evidence runs. Sandbox: the printer names only what the engine bound; strict mode narrows, never widens.

## Deferred items / non-goals

Generated sites for late-bound calls (trigger: PD9's revisit); AOT of the dynamic tier; a `:: dynamic` model inside
the NativeAOT claim (trigger: a DLR-free dynamic hop); per-site generation opt-out (trigger: a printed site proved
wrong that the decline rules cannot express).

## External references

- [BenchmarkDotNet `RunStrategy.ColdStart`](https://benchmarkdotnet.org/articles/guides/choosing-run-strategy.html)
- [NativeAOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/),
  [`IsAotCompatible`/`IsTrimmable`](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers),
  [`DynamicDependencyAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.dynamicdependencyattribute)
  and [`RuntimeHostConfigurationOption`](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/)
- [C# numeric promotions (ECMA-334 §12.4.7)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1247-numeric-promotions)
- [`AppContext.TryGetSwitch`](https://learn.microsoft.com/en-us/dotnet/api/system.appcontext.trygetswitch)
