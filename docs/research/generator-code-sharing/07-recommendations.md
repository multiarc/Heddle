# 07 — Synthesis: shared-library layout and priority order

This document consolidates the six area reports into one plan-shaped view. It is still research output — nothing has been changed in the engine.

## The headline pattern

The generator was built as a *reimplementation* of the byte-affecting half of the runtime compile pipeline, with the parse front end shared via linked sources but nearly everything after parse duplicated by hand. The duplication falls into four tiers with different remedies, and **the copies have already drifted in at least a dozen verified places** — several of them user-visible bugs today, not hypothetical risk.

## Confirmed live drift (fix these regardless of any extraction)

Ordered by severity class:

| # | Divergence | Consequence | Where documented |
|---|---|---|---|
| 1 | `WidenToWholeLine` lacks the runtime's bounds clamp | `IndexOutOfRangeException` swallowed → silent loss of precompilation; or byte divergence | [02](02-document-shaper.md) F1 |
| 2 | Content hash: generator hashes decoded text, runtime hashes raw file bytes | Any BOM/non-UTF-8 template permanently fails staleness → silent fallback | [05](05-pipeline-config.md) F1, [06](06-diagnostics-utilities.md) F1 |
| 3 | `needsLocals`: generator scans only `chain.Chain[0]`, runtime recurses into chained parameters + carriers; generator also ORs one flag across both carriers | Precompiled body renders without a locals frame → branch state misreads (behavioral, not whitespace) | [01](01-template-emitter.md) F11, [02](02-document-shaper.md) F6 |
| 4 | Member-path visibility: `protected internal`, inherited non-public, base-interface members, `[Hidden]` by unqualified name | Precompiled tier renders values the dynamic tier rejects (sandbox contract bypass) | [03](03-binding-layer.md) F7, [04](04-expression-writers.md) F1 |
| 5 | "Deviations from C#" unguarded: `==` on mixed types, enum arithmetic, etc. | Consumer build errors (CS0019) for templates the runtime accepts; or opposite verdicts that render | [04](04-expression-writers.md) F2 |
| 6 | AQN formatting disagrees for nested/generic types | Permanent gauntlet mismatch → silent full fallback per extension | [03](03-binding-layer.md) F1 |
| 7 | Inherited `[ExtensionName]` not seen (declared-only read) | False `HED7006` **build error** for templates the runtime renders fine; manifest/live name mismatch | [03](03-binding-layer.md) F3 |
| 8 | Forwarded warnings collapsed to HED7013, dropping ID and Fix | Same warning has different IDs in build vs LSP; suppression by real ID broken at build time | [06](06-diagnostics-utilities.md) F2 |
| 9 | `double`/`float` literal round-trip via `ToString("R")` | One-ULP numeric divergence, dependent on the build host runtime | [04](04-expression-writers.md) F6 |
| 10 | Line/column mapping: three implementations, two `\r` rules | Same offset → different column in build error vs LSP vs `CompileResult` | [06](06-diagnostics-utilities.md) F4 |
| 11 | `DeriveKey` falls back to `Path.GetFileName` with no diagnostic; case policy mismatch (IgnoreCase strip → Ordinal compare) | Wrong keys silently registered; spurious HED7002 "duplicates"; case-mangled keys | [05](05-pipeline-config.md) F3, [06](06-diagnostics-utilities.md) F6 |
| 12 | `@profile(<unknown>)`: hard HED2001 at runtime, silently ignored by the emitter | Build tier accepts a template the run tier rejects | [01](01-template-emitter.md) F1, [06](06-diagnostics-utilities.md) F8 |
| 13 | `DefaultConvertible` omits the `S?→W?` row; internal `ConstructedFrom` vs `OriginalDefinition` inconsistency | Latent asymmetry in prop-default acceptance | [01](01-template-emitter.md) F7 |
| 14 | Overload ranking: runtime's flat Pareto rank vs C# betterness | Ambiguity error at runtime where precompiled code renders (e.g. `min(1, 2u)`) | [04](04-expression-writers.md) F3 |
| 15 | `Precompile`/`Name` item metadata declared in props/targets but never read | User opt-out silently ignored | [05](05-pipeline-config.md) F8 |

Special attention: items 4/5 and the `[EncodeOutput]` render-type rule ([01](01-template-emitter.md) F1/F4, [03](03-binding-layer.md) F9) are **encoding/sandbox-relevant** — divergence there is an XSS-class or contract-bypass regression, and none of it is covered by the gauntlet.

A structural observation that raises the stakes: the gauntlet (`PrecompiledGauntlet`) covers options, extension identity, functions, and staleness — but **not** prop-slot layouts, member-binding decisions, render-type derivation, or expression semantics. For those, drift is silent wrong output, not a fallback.

## Proposed shared-code layout

The mechanism already exists and is proven: linked `<Compile Include>` items in `Heddle.Generator.csproj` ("Shared front-end sources, D4 step 2"), with the convenient property that anything placed under `src/Heddle/Language/**` is picked up by the existing glob with **zero csproj edits**. A dedicated `Heddle.Shared` source-only project is an option, but the linked-file pattern is lower-friction and matches precedent; revisit only if the shared set grows past ~20 files.

### Tier 1 — link existing dependency-free files (one csproj line each, delete the generator's copy)

| File to link | Kills |
|---|---|
| `Attributes/BranchRole` (the enum) | hand-mirrored enum at `ExtensionBinder.cs:10` |
| `Data/OutputProfile.cs`, `Data/ExpressionMode.cs`, `Data/RenderType.cs` | `GlobalConfig` string triple, `Mode()`/`Profile()`/`IsHtml` string mapping, `RenderType` string literals |
| `Precompiled/PrecompiledOptionsFingerprint.cs`, `PrecompiledCapabilities.cs` | hand-typed fingerprint arity/order in two manifest builders; `nameof` becomes available |

### Tier 2 — extract directly-sharable imperative code into new shared files

| New shared file | Contents | Sources merged |
|---|---|---|
| `Language/DocumentShaping.cs` | `WidenToWholeLine` (+ clamp), the five rebasing machines + pass order, safe `ApplyRemove`/`Replace`, branch-strip machine with `BranchKind` (incl. `Participant`) + injected classifiers, generic `SlicePieces<T>` | [02](02-document-shaper.md) F1–F3, F5; [01](01-template-emitter.md) F2 |
| `Language/ParticipantScan.cs` | full-chain, carrier-unwrapping, parameter-recursing `[ScopeChannel]` scan over `OutputChain` with injected `Func<string,bool>` | [01](01-template-emitter.md) F11; [02](02-document-shaper.md) F6 |
| `Precompiled/ContentHash.cs` | `HashText`/`HashBytes`/`HashFile` + one `ToHex`; pins the BOM/encoding decision | [05](05-pipeline-config.md) F1; [06](06-diagnostics-utilities.md) F1 |
| `Precompiled/TemplateKey.cs` (extend) | `TryMakeRelative(path, root)` / `ToPath(key, root)` with documented case policy; `TemplateExtension` const + `Has`/`Strip` helpers | [05](05-pipeline-config.md) F3, F4; [06](06-diagnostics-utilities.md) F6 |
| `Precompiled/PrecompiledSchema.cs` | `Min`/`Max`/`CurrentSchemaVersion` consts; version-format + compatibility predicate over `System.Version` | [05](05-pipeline-config.md) F5 |
| `Precompiled/AqnFormatter.cs` | version-less AQN from `(ns, nesting chain, arity, assembly)`; both sides map into it | [03](03-binding-layer.md) F1; [01](01-template-emitter.md) F18 |
| `Language/Expressions/OperatorLexeme.cs` | `ExprOperator → lexeme` + the supported set, derived once | [04](04-expression-writers.md) F5 |
| `Language/Expressions/LiteralFormatter.cs` (relocated) | literal→C# round-trip with `G17`/`G9`; documented inverse of the AST decoder; shared `CSharpEscape` | [04](04-expression-writers.md) F6, F9 |
| `Data/OutputProfileRules.cs` + `RenderTypeRules.cs` | `TryParseProfile`, `ResolveUnnamedCarrier(profile, hasBody)`, `RenderType Derive(bool,bool)` | [01](01-template-emitter.md) F1, F4; [06](06-diagnostics-utilities.md) F8 |
| `Language/RegionFillResolver.cs` | the four-step fill-matching rule returning a verdict enum; sides keep their own reactions | [02](02-document-shaper.md) F7; [01](01-template-emitter.md) F12 |
| `Language/SlotRules.cs` | `HasOutValue(CallParameter)` (canonical five-way), slot-type base-chain walk over `DefinitionItem` | [01](01-template-emitter.md) F13 |
| `Data/LineIndex.cs` | one line-start index + search, `ZeroBased`/`OneBased`, one `\r` rule | [06](06-diagnostics-utilities.md) F4 |
| `Data/HeddleDiagnosticCatalog.cs` | `id → (title, messageFormat, severity)` data table beside the linked IDs | [06](06-diagnostics-utilities.md) F2, F3 |
| `Helpers/CSharpTypeNames.cs` + spelling parser split out of `ReflectionHelper` | alias↔type tables; the reflection-free generic/array/tuple spelling parser (`ReflectionHelper.cs:319-410`) | [03](03-binding-layer.md) F8; [06](06-diagnostics-utilities.md) F5 |
| Shared `HeddleBuildOptions` (names + defaults) | collapses props/ConfigReader/TemplateOptions default triplication; ConfigReader becomes a `Func<string,string>` shim | [05](05-pipeline-config.md) F6 |

### Tier 3 — shared rule-cores with `ITypeFacts`/`IMemberFacts` adapters

Same rule over `ISymbol` vs `System.Type`; the shared file holds the algorithm and decision tables, each side supplies a small facts adapter (Roslyn adapter in the generator, reflection adapter in Heddle; neither leaks into the shared file):

1. **`PrimitiveKind` + conversion tables** — the §10.2.3 widening table, keyword aliases, null-default legality. The enabler for everything below. ([01] F7, [03] F5, [04] F7)
2. **`PropLayoutCore<TType>`** — layer walk, slot indexing, ordered fault enum. The highest-payoff core: slot indices are the wire format, drift is silent wrong output, and the gauntlet doesn't cover it. ([01] F5/F6, [03] F4)
3. **`MemberPathWalk` + `MemberVisibility`** — the accessibility decision table over a shared `MemberAccess` enum; fixes the three live visibility divergences by construction. ([03] F7, [04] F1)
4. **`NativeOperatorRules.Classify`** — the deviations-from-C# set as a decision table; generator degrades on non-`Supported` instead of emitting divergent C#. ([04] F2)
5. **Overload rank core** — `ConversionRank`/`TryRank`/`Dominates` over `NumericKind`+`TypeRef`; generator uses it as an emit guard. ([04] F3)
6. **Extension/function discovery + precedence** — eligibility predicates, name normalization, merge/override bookkeeping over facts records. ([03] F2, F3)
7. **Assignability relation** — `ITypeFacts.IsAssignableFrom` with the nullable corrections stated once in the Roslyn adapter; plus a shared `(source, target, expected)` conformance corpus run by both a reflection test and a symbol test. ([01] F8, [03] F6)

### Tier 4 — spec/attribute-driven (can't be shared as code)

- **Body model-typing table** (`extensionName → BodyModelSource`) as linked data — currently exists only as prose comments. ([01] F3)
- **Zero-output classification** → a `[ZeroOutput]`/`[Directive]` attribute the runtime asserts against and `ExtensionBinder` reads symbolically, replacing the hardcoded four-name list. ([01] F17, [02] F4)
- **Embedded-C# identifier contract** (`model`/`chained`/`root`) as three shared consts referenced by the `.tcs` templates and the emitter. ([01] F16)
- **Strategy-shape and coercion-rail parity** (`as string ?? string.Empty`) — pinned spec + differential tests. ([01] F2)
- **Dynamic binder context** — route generated dynamic hops through one public `PrecompiledRuntime.DynamicMember` helper so the binder-context choice exists once. ([04] F8)

## Recommended sequencing

1. **Bug fixes first** (no extraction needed): the clamp (drift #1), content-hash input (#2), `needsLocals` scan (#3), forwarded-warning IDs (#8), inherited `[ExtensionName]` (#7), `DeriveKey` diagnostics (#11). Each is small and independently shippable.
2. **Tier 1 links + the two convergent top extractions**: `ContentHash` and `DocumentShaping` (with lockstep tests following the `DefaultFunctionLockstepTests` precedent).
3. **Tier 2** in the order listed — `ParticipantScan`, `OperatorLexeme`, `TemplateKey` extensions, and `LineIndex` are the best effort-to-value.
4. **Tier 3** starting with `PrimitiveKind` (the enabler), then `PropLayoutCore` and `MemberPathWalk` (the two silent-wrong-output surfaces), then `NativeOperatorRules`.
5. **Tier 4** opportunistically, alongside whichever area is being touched.

## Test guardrails worth adding independently of extraction

- Build/run **content-hash lockstep test** (generator output vs `PrecompiledGauntlet.HashFile`).
- **HED7xxx registry test** mirroring `DiagnosticIdTests` (code ↔ docs registry ↔ `GeneratorDiagnostics`), which would have caught the HED7017 doc gap.
- **Assignability conformance corpus** driven from one data file by both tiers.
- Differential corpus entries for: nested/generic extension types (AQN), `protected internal`/interface-inherited members, non-leftmost `[ScopeChannel]` participants, BOM'd template files, `@profile(<typo>)`, and `min(1, 2u)`-style overload ties.
