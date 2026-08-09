---
paths:
  - "src/Heddle/**/*.cs"
  - "src/Heddle.Generator/**/*.cs"
---

# API design and compatibility

Details: [coding-standards.md § API](../../docs/spec/common/coding-standards.md#api-design-and-compatibility), [breaking-windows.md](../../docs/spec/common/breaking-windows.md). Anchored in the .NET Framework Design Guidelines.

- Additive by default: new extensions, opt-in options, overloads. Existing templates and hosts compile and render byte-identically.
- Breaking changes land only in a ratified breaking window (D2), each with a migration note. Candidates go to the register in breaking-windows.md, not into code.
- Every new `TemplateOptions` property goes into the copy constructor (and `Equals`/`GetHashCode` when it keys identity) — missed before (`ProvideLanguageFeatures`); the completeness test enforces this.
- Public API additions carry XML docs + same-change updates to the affected published docs pages (`language-reference.md`, `custom-extensions.md`, `built-in-extensions.md`, `csharp-api.md`).
- Extend via the sanctioned seams (`[ExtensionName]` registry, options, function registry, output profiles, publish/read channel, resolver, encoder) — don't invent parallel mechanisms or new seams for single-implementation internals.
- The generator names no extension: no compare against a built-in name in any form, no `"Heddle.Extensions."` type literal, no `"Heddle"` assembly-name compare, no name-keyed table. Read the binder, the attributes, or the hook. `ExtensionAgnosticismTests` gates it over every file compiled into `Heddle.Generator.dll` — linked shared source included, via an embedded compile-input manifest — by Roslyn syntax walk rather than regex, so `string.Equals`, `case`, `is`, collection lookups and default parameter values are all caught, and by reading static string tables back by value. It is vocabulary-free, so a third-party name is caught too. Every declared row carries a `Why` ending in `permanent` or `retires in Stage N`.
- Extension instances are shared across concurrent renders: no mutable per-render state on them; per-render state lives in the `Scope` lineage.
