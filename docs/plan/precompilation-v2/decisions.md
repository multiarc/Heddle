# Decisions

Back to the [plan](README.md). Each decision states what v3 does, a reason where it is not
obvious, and a revisit trigger where one is useful.

## PD1 — Generator removal and cutover

`Heddle.Generator` — package, three Roslyn builds, both suites, CI legs — and everything in
`Heddle.dll` that existed for generated 2.x code are removed in v3 ([window items 1–3](v3-window.md#window-items)).
The last 2.x package is deprecated on NuGet pointing at `Heddle.Build`; nothing from this plan
ships in 2.x.

## PD2 — Typed entry points

`Heddle.Generated.<SanitizedName>.Generate(model, chained, callerData)` (string, `TextWriter`,
`IBufferWriter<byte>` overloads) keep namespace, naming rule and signatures as thin wrappers over
the loaded entry; `HeddleGeneratedNamespace` survives. A wrapper renders under a **process-wide
default `TemplateOptions`** the host assigns on `PrecompiledTemplates` (unset → engine defaults),
the same options it passes to `ValidateAll`. On first call it materializes and validates its own
assembly's artifact; a failed check throws `PrecompiledMismatchException` naming the reason — a
typed entry has nothing to degrade to. *Revisit:* hosts rendering under several options shapes
from typed entries → an options-carrying overload.

## PD3 — MSBuild surface

**Retired:** `HeddleObserveEngine`, `HeddleNodeFallback`,
`HeddleEmitUtf8Pieces` (a set property draws one build warning — none changes a rendered byte),
`HeddleObserveIntermediatePath`, `HeddleObserveImplementationPath` (silently), and their
`HeddleBuildOptions` constants. Everything else in the documented MSBuild surface, the
`.heddle-lsp.json` keys and the `HeddleBuildOptions` constants for surviving properties is
unchanged. **Added:** per-item `OutputProfile` metadata. D4 is recorded as
superseded by a dated note in its owning spec (the runtime pre-encodes pieces; nothing emits
pieces as code).

## PD4 — Artifact compatibility

The compiled form is v3-only. Among v3-and-later artifacts `PrecompiledSchema.IsEngineCompatible`
applies (same major, artifact not newer than the runtime) and the schema number tracks breakage of
the form under policy rule 7, with a stored v3 artifact fixture per additive change. A 2.x-built
assembly is recognised by its pre-v3 marker and `Register` throws `PrecompiledRegistrationException`
naming the assembly and the remedy (rebuild with `Heddle.Build`). At build, `Heddle.Build`'s
version must equal the `Heddle` package the
project references (error otherwise), so an artifact is stamped with the engine its author
compiled against and consumers lifting `Heddle` transitively stay inside the predicate.

## PD5 — Embedded C# before generated sites

Under `ExpressionMode.FullCSharp` a C# site is serialized as source plus binding context and
compiled at load through the engine's Roslyn path; phase 3 generates it. Under
`Heddle.CSharpTierEnabled=false` such a template degrades with `HED9001`, as the dynamic tier does.

## PD6 — Build host

`Heddle.Build`'s targets run `heddle compile` (`Heddle.Tool`, carried in the package,
framework-dependent on the engine's newest TFM) through an MSBuild task that launches the CLI out
of process — MSBuild's node hosts its own Roslyn. Consequences, published as prerequisites: build
machines need a .NET 10 (or later) runtime; the host binds by reflection over **implementation**
images of the consumer's references (project references via their implementation path, packages
via their lib folder, framework types via its own runtime), and an assembly that cannot load costs
the templates naming its types a build error; a BCL member absent on the project's target
framework surfaces as a load-time gate fallback and a dynamic-tier compile error on that target
([window item 8](v3-window.md#window-items)). The spec owes framework-type identity normalization
across CoreLib and mscorlib. *Revisit:* builds that cannot carry a .NET 10 runtime → a
self-contained host per RID.

## PD7 — Same-project models and design-time builds

A template naming a type the consuming project declares triggers one content-addressed
intermediate compile of that project, fed **signature-only wrapper stubs** whose names and model
types come from the item list and a parse-only pass (`@model` spelling or `ModelType` metadata).
Design-time builds run only that stub pass — no engine, no implementation images — so
`Heddle.Generated.*` call sites resolve in the IDE. Models in referenced assemblies need neither.
*Revisit:* the double compile proving too slow → generated code in a satellite assembly.

## PD8 — The window

This plan constitutes the v3 breaking window; its removal items are window items, not
candidate-register rows, and the register's `ResolvePartial` row is adopted. v3 opens when 2.1 is
released and reconciled (policy rule 1); until then work lands on a v3 line and enters no release.

## PD9 — Calls the build registry cannot bind

The **bodiless rule.** In a bodiless value position the call is late-bound data — call shape and
enclosing syntax tree serialized; typing and overload selection at load against the live
`TemplateOptions.Functions` through the engine's ranker. Where the result type would shape a body
or a hook (a bodied call whose data parameter is the call, or a hook typing its body from it) the
site and its body take the dynamic path — phase 1 refusal class (c) — because the loader cannot
re-type a serialized body and parity by construction outranks coverage of that shape; remedy
`[ExportFunctions]`, else `Precompile="false"`. The build compiles with function binding deferred
for value sites and reports late-bound names; `ValidateAll` reports a name the live registry
lacks under `UnsupportedFunction`. Late-bound sites are never generated sites. *Revisit:* a
load-time body re-typing that proves byte parity over the corpus → class (c) retires in place.

## PD10 — Strict no-load-time-compilation mode

An `AppContext` switch beside `Heddle.CSharpTierEnabled`, mirrored on `TemplateOptions`, default
off: any load-time expression compilation (expression-tree compile, Roslyn, DLR call-site
creation) throws a host error naming the site. Tests prove site coverage with it; AOT hosts fail
fast with it.

## PD11 — Engine ids at build

For a fact the engine diagnoses, the build reports the engine's id at the `.heddle` position and
nothing else; `HED70xx` ids for such facts retire in place. The migration note carries the
mapping.
