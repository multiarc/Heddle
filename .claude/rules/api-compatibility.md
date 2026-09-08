---
paths:
  - "src/Heddle/**/*.cs"
  - "src/Heddle.Tool/**/*.cs"
---

# API design and compatibility

Details: [coding-standards.md § API](../../docs/spec/common/coding-standards.md#api-design-and-compatibility), [breaking-windows.md](../../docs/spec/common/breaking-windows.md). Anchored in the .NET Framework Design Guidelines.

- Additive by default: new extensions, opt-in options, overloads. Existing templates and hosts compile and render byte-identically.
- Breaking changes land only in a ratified breaking window (D2), each with a migration note. Candidates go to the register in breaking-windows.md, not into code.
- Every new `TemplateOptions` property goes into the copy constructor (and `Equals`/`GetHashCode` when it keys identity) — missed before (`ProvideLanguageFeatures`); the completeness test enforces this.
- Public API additions carry XML docs + same-change updates to the affected published docs pages (`language-reference.md`, `custom-extensions.md`, `built-in-extensions.md`, `csharp-api.md`).
- Extend via the sanctioned seams (`[ExtensionName]` registry, options, function registry, output profiles, publish/read channel, resolver, encoder) — don't invent parallel mechanisms or new seams for single-implementation internals.
- The build names no extension: no compare against a built-in name in any form, no `"Heddle.Extensions."` type literal, no `"Heddle"` assembly-name compare, no name-keyed table. Read the binder, the attributes, or the hook.
- The build never predicts a compile-time hook either: the loader runs the extension's real `InitStart`/`CompleteInit` inside the consumer's assembly at static-init, so a bodied, hook-overriding, `[Prop]`-declaring or `[BranchRole]` custom extension precompiles in a default build. A call the build cannot serve costs **that template** (an `HED7031` decline with a machine-readable class), not the build — reach for a build error only where the template must not precompile silently.
- Extension instances are shared across concurrent renders: no mutable per-render state on them; per-render state lives in the `Scope` lineage.
