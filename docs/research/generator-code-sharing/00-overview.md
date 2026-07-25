# Generator ↔ Engine Code-Sharing Research

**Date:** 2026-07-25
**Scope:** `src/Heddle.Generator` (the phase-7 Roslyn incremental source generator, ~5.2k lines) compared area-by-area against the rest of the engine — `src/Heddle` (runtime, ~19.4k lines), `src/Heddle.Language`, and secondary consumers (`src/Heddle.Tool`, `src/Heddle.LanguageServices`).
**Goal:** document every place where logic is maintained as two (or more) hand-kept copies — one in the generator, one in the engine — and could instead live in a common library / shared source and be reused by both.

This is research only. No engine code was changed.

## Documents in this set

| File | Area |
|------|------|
| [01-template-emitter.md](01-template-emitter.md) | `Emit/TemplateEmitter.cs` vs runtime rendering/encoding (`Data/*Renderer*`, `Runtime/RuntimeDocument`, value formatting) |
| [02-document-shaper.md](02-document-shaper.md) | `Emit/DocumentShaper.cs` vs runtime document composition (`RuntimeDocument`, `TemplateChain`, region/definition materialization) |
| [03-binding-layer.md](03-binding-layer.md) | `Emit/ExtensionBinder.cs` + `Binding/*` (ISymbol world) vs runtime reflection binding (`PropsBinder`, `ExtensionParameterMap`, `Precompiled/*`) |
| [04-expression-writers.md](04-expression-writers.md) | `Emit/NativeExpressionWriter.cs` + `Emit/MemberPathWriter.cs` vs runtime expression compilation (`ExpressionCompilation`, `CSharpContext`) |
| [05-pipeline-config.md](05-pipeline-config.md) | `HeddleTemplateGenerator.cs` + `Pipeline/*` vs runtime options/fingerprint/path rules (`TemplateOptions`, `PrecompiledOptionsFingerprint`, `FileReader`) |
| [06-diagnostics-utilities.md](06-diagnostics-utilities.md) | `Diagnostics/GeneratorDiagnostics.cs`, `Emit/CodeWriter.cs`/`PieceWriter.cs`/`LineMapper.cs` vs engine diagnostics; plus third copies in `Heddle.Tool` / `Heddle.LanguageServices` |
| [07-recommendations.md](07-recommendations.md) | Cross-cutting synthesis: proposed shared-library layout, priority order |

## What is already shared (baseline)

`Heddle.Generator.csproj` already avoids one whole class of duplication by **source-linking** runtime files (D4 step 2 in the project's plan language). Linked today:

- `Heddle/Language/**` — the entire parse pipeline (except the runtime-only partial `DocumentParser.Runtime.cs`), so parse knowledge exists once.
- `Heddle/Strings/Core/BlockPosition.cs`
- `Heddle/Data/CompileError.cs`, `HeddleCompileError.cs`, `HeddleCompileWarning.cs`, `HeddleDiagnosticIds.cs`, `LinePosition.cs`
- `Heddle/Exceptions/TemplateCompileException.cs`, `TemplateParseException.cs`
- `Heddle/Precompiled/TemplateKey.cs`, `DefaultFunctionTable.cs`

This linked-source mechanism is the **established precedent** for sharing: any finding in these documents classified "directly sharable" can use the same mechanism (or a dedicated `Heddle.Shared` source-only project) without new packaging work.

## Hard constraints on any sharing proposal

1. **netstandard2.0** — the generator targets netstandard2.0 (Roslyn analyzer requirement); shared code must compile there (no modern-BCL-only APIs without polyfill guards).
2. **No runtime-assembly reference** — analyzers cannot `ProjectReference` the runtime `Heddle.dll` for execution; sharing must be **linked `<Compile>` items** (current approach) or a shared source project whose output is packed into `analyzers/dotnet/cs`.
3. **No Roslyn types in shared code** — the runtime must not depend on `Microsoft.CodeAnalysis`; anything shared must be free of ISymbol/SyntaxNode types.
4. **Two kinds of duplication, two remedies:**
   - *Same imperative algorithm* (e.g. string escaping, path normalization, hashing) → extract as-is into a shared file.
   - *Same rule, different representation* (e.g. type-compatibility decided over `ISymbol` in the generator and `System.Type` at runtime; render behavior executed at runtime but emitted as C# text by the generator) → cannot share the imperative code directly; needs a shared **rule core** (decision tables, name-normalization functions, format-string constants) with thin per-side adapters. These are the highest-drift-risk items because nothing forces the copies to agree except tests.

## Why drift matters here

The precompiled output is contractually **byte-identical** to runtime-compiled rendering (parity contract; `PrecompiledGauntlet` / mismatch policy at runtime, golden-corpus verification in `benchmarks/`). Every hand-maintained pair below is a place where an edit to one side silently breaks that parity or, worse, changes binding behavior between precompiled and fallback paths.
