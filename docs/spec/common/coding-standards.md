# Coding standards (shared across all specs)

Normative for all code written under the specs in `docs/spec/` (any initiative). It has three parts:
the **repository style** (inferred from the codebase — preserved deliberately),
the **engineering rules** the engine already lives by (errors, performance, compatibility),
and the **balanced-mode application of SOLID / DRY / YAGNI** with the precedence rule used
when principles conflict.

Authority order for any style question: **(1) the surrounding code in the file being
edited, (2) this document, (3) [.editorconfig](../../../.editorconfig)** (which is editor
guidance only — deliberately not a CI gate).

## Repository style (inferred, preserved)

The maintainer keeps Heddle's established personal C# style. **Never mass-reformat**; never
run `dotnet format` or IDE whole-file cleanups as part of a change; keep diffs limited to
the lines the change needs. Concretely, the established style is:

- **Block-scoped namespaces** (`namespace Heddle.Data { … }`) — not file-scoped, even
  though `LangVersion` is `latest`. Match this in new files.
- **Allman braces** predominantly; a few files put `{` on the same line for namespaces or
  small members — match the file you are in.
- **Braceless single-statement guards** are idiomatic for early returns:

  ```csharp
  if (scope.ModelData == null)
      return string.Empty;
  ```

  Multi-line or nested bodies always take braces.
- Occasional **space before the parameter list** in member declarations
  (`public override bool Equals (object obj)`) and **space after a cast**
  (`(bool) scope.ModelData`) exist in the codebase. Do not "fix" them; in new code either
  form is acceptable — be consistent within a file.
- **`var` when the type is apparent** from the right-hand side; explicit types otherwise
  and for built-ins (`string workingDocument = document;`, `int seed = …`).
- **Naming:** `_camelCase` private instance fields; `PascalCase` private static readonly
  fields; `PascalCase` public readonly fields on performance structs (`Scope.ModelData`);
  interfaces `I`-prefixed; extension classes `<Name>Extension` carrying
  `[ExtensionName("name")]`; test methods are descriptive PascalCase sentences.
- **Usings** outside the namespace, `System` directives first (suggestion-level; existing
  files vary — don't churn them).
- **XML doc comments** (`<summary>`, `<para>`) on public API; regression tests carry a
  doc comment explaining the scenario they pin (see
  [`HeddleTemplateTests`](../../../src/Heddle.Tests/HeddleTemplateTests.cs) for the shape).
- 4-space indent, UTF-8, final newline, trimmed trailing whitespace; long parameter lists
  wrap with a continuation line at ~120 columns.
- **Generated code is never hand-edited**: everything under
  `src/Heddle.Language/generated/` comes from `generate_cs.cmd` (ANTLR 4.13.1) and is
  committed as generated. A grammar change is always `.g4` edit → regen → commit both.

## Language and target frameworks

- Libraries ([Heddle](../../../src/Heddle/Heddle.csproj),
  [Heddle.Language](../../../src/Heddle.Language/Heddle.Language.csproj)) target
  `netstandard2.0;net6.0;net8.0;net10.0`; tests add `net48` on Windows. **New engine code
  must compile and behave correctly on `netstandard2.0`** — modern BCL APIs need either a
  conditional package reference (the csproj already has per-TFM groups) or an `#if` fallback.
- `LangVersion` is `latest`: C# 14 language features are usable on **all** TFMs when they
  don't require runtime support. Per [D7](cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default), almost nothing warrants
  `#if NET10_0_OR_GREATER` — see
  [D7](cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default).

## Error handling and diagnostics

- **Compile-path problems are collected, not thrown.** Anything wrong with a template
  becomes a positioned `HeddleCompileError` / `HeddleCompileWarning` on the compile result
  (see `HeddleCompiler.Compile`'s catch-and-collect pattern); the user-facing API never
  surfaces raw exceptions for template mistakes. New work follows this pattern and claim
  stable diagnostic IDs from the
  [registry](cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx).
- **Host-programming errors throw** (`ArgumentNullException` etc.) at public API entry
  points — a null options object is the caller's bug, not a template diagnostic.
- Error messages name the construct and point at the remedy ("method calls are not
  available in native expressions — register a function or use the `@` C# tier"), and
  carry the template position.
- **Security-sensitive failures never degrade to execution**: a sandbox violation is a
  compile error, full stop.

## Performance rules (the render path is hot)

Heddle's benchmark story (faster than Razor, lower allocation) is structural; spec work
must not erode it:

- **No new per-render allocations** on the render path. `Scope` stays a `readonly struct`
  passed by `in`; per-render state that is optional must be lazily created (the branch
  locals frame is the model: zero cost until first use).
- Reflection only at compile time; render time executes pre-compiled delegates and direct
  calls.
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on tiny, provably-hot transforms
  only — with a benchmark that justifies it.
- **Perf claims are proven by BenchmarkDotNet** in
  [src/Heddle.Performance](../../../src/Heddle.Performance), not asserted. Any change that
  touches a hot path runs the suite as part of its regression gate (see
  [testing standards](testing-standards.md#regression-gates)).
- Compile-time cost is "compile once, render many" — moderate compile-path allocation is
  acceptable when it buys render-path speed or clarity, but startup-relevant paths
  (precompilation's raison d'être) get measured too.

## API design and compatibility

Anchored in the
[.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
(the Do / Consider / Avoid / Do-not vocabulary) and the
[library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes):

- **Additive by default.** New capability arrives as new extensions, new opt-in options
  properties, new overloads — existing templates and hosts compile and render identically.
  Where a spec says "existing suite byte-identical", that is the acceptance test of
  this rule.
- **Breaking changes land only in a ratified breaking window**
  ([D2](cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows),
  [breaking-windows.md](breaking-windows.md)) and each must ship with a migration note in
  the release's docs deliverable.
- **`TemplateOptions` completeness invariant:** every new option property must be added to
  the copy constructor (and to `Equals`/`GetHashCode` when it participates in identity —
  e.g. anything that keys a template cache). This has been missed before
  (`ProvideLanguageFeatures`); the reflection-based completeness test
  ([TemplateOptionsCompletenessTests](../../../src/Heddle.Tests/TemplateOptionsCompletenessTests.cs))
  guards it for every addition for free.
- Public API additions carry XML docs and a same-change update to the published docs pages
  they affect (`language-reference.md`, `custom-extensions.md`,
  `built-in-extensions.md`, `csharp-api.md` — whichever apply).
- Options enums follow the existing shape (`OutputProfile`-style simple enums);
  extension points follow the registry pattern (`[ExtensionName]`, `Configure`) rather
  than inventing parallel mechanisms.
- Thread safety contract: extension instances are shared across concurrent renders —
  **no mutable per-render state on extension instances**; per-render state lives in the
  `Scope` lineage. Every spec that adds state names where it lives and why that is safe.

## SOLID, DRY, and YAGNI in balanced mode

"Balanced mode" means the principles are **decision heuristics, not compliance
checklists**: applied where they demonstrably pay for themselves in this codebase, and
explicitly overridden where they would cost simplicity or measured performance. The
literature this stance is grounded in: over-applying SOLID produces indirection and
over-engineering, particularly on latency-sensitive paths
([Baeldung — when SOLID may not be appropriate](https://www.baeldung.com/cs/solid-principles-avoid));
DRY is about single representation of **knowledge**, not similar-looking lines
([Wikipedia — DRY](https://en.wikipedia.org/wiki/Don%27t_repeat_yourself)), and hasty
unification is worse than duplication
([Kent C. Dodds — AHA programming](https://kentcdodds.com/blog/aha-programming), Sandi
Metz's "prefer duplication over the wrong abstraction"); YAGNI applies to *presumptive
features*, never to tests, refactoring, or keeping code easy to change
([Martin Fowler — Yagni](https://martinfowler.com/bliki/Yagni.html)).

### Precedence when principles conflict

1. **Correctness and back-compat** — existing templates render byte-identically unless a
   ratified breaking-window item says otherwise ([breaking-windows.md](breaking-windows.md)).
2. **Sandbox security** — no design may open an execution path from template text that the
   options say is closed. Security beats ergonomics and beats performance.
3. **Simplicity and readability** — the smallest design that meets the spec's success
   criteria. This is the default winner.
4. **Measured hot-path performance** — may justify complexity (structs, inlining, manual
   buffers), but only with a benchmark showing the win; "it should be faster" is not a
   reason.
5. **Abstraction and extensibility** — last: a seam is added when a second consumer exists
   or the owning plan ratified one, not because one might appear.

### SOLID, applied to this codebase

- **Single responsibility** — the pipeline stages (lex → parse → walk → compile → render)
  and one-class-per-extension are the SRP backbone; keep new behavior in the stage it
  belongs to. Do **not** shred a cohesive compiler step into ceremony classes to satisfy
  the letter of SRP.
- **Open/closed** — the sanctioned open points are the extension registry
  (`[ExtensionName]`), the options object, and the sanctioned seams (function
  registry, output profiles, publish/read channel, resolver, encoder). Prefer a new extension over
  modifying the compiler. Do **not** invent new plugin seams for internals with a single
  implementation.
- **Liskov substitution** — anything deriving `AbstractExtension` must honor the
  documented `InitStart` / `ProcessData` / `RenderData` contract (type threading,
  never-null render results, no per-instance render state) so extensions stay
  interchangeable in chains. Specs state contract obligations for every new virtual.
- **Interface segregation** — keep interfaces per-capability (`IExtension`,
  `IScopeRenderer`, `IProcessStrategy`); new cross-cutting capabilities arrive as small
  separate opt-in interfaces, not new members on existing ones (which is also the
  binary-compat rule for shipped interfaces).
- **Dependency inversion** — boundaries that already have two sides (renderer sinks,
  template resolution, function/extension registries) depend on abstractions. Engine
  internals with exactly one implementation may stay concrete — indirection on the render
  path costs measured performance, which precedence rule 4 protects.

### DRY, applied

- Consolidate when two call sites must never diverge **semantically** — e.g. member-path
  resolution semantics (null-safe hops, `[Hidden]`, accessibility) must exist exactly once
  and be shared by the member tier and the native-expression tier.
- Follow the rule of three for merely similar code: duplication twice is acceptable;
  extract on the third occurrence, or earlier only when divergence would be a bug.
- Test fixtures may repeat setup for readability — tests optimize for diagnosis, not DRY.

### YAGNI, applied

- Build what the spec's success criteria require, nothing beyond. Items the owning plan
  marks deferred (async model resolution, precompiled replace flags, …) stay unbuilt;
  each spec lists them under *Deferred items / non-goals* with a revisit trigger.
- YAGNI does **not** cut: tests, benchmarks, the diagnostic-ID scheme, docs, or the small
  refactors that keep the next change cheap (Fowler: yagni is only viable when the code
  stays easy to change — and it presupposes self-testing code).
- No speculative configuration knobs: an option is added when a scenario in the spec needs
  both values, not "for flexibility".

## Sources

- [Martin Fowler — Yagni](https://martinfowler.com/bliki/Yagni.html) (presumptive
  features; malleability and tests exempt)
- [Kent C. Dodds — AHA programming](https://kentcdodds.com/blog/aha-programming) and
  Sandi Metz — [The wrong abstraction](https://sandimetz.com/blog/2016/1/20/the-wrong-abstraction)
- [Wikipedia — Don't repeat yourself](https://en.wikipedia.org/wiki/Don%27t_repeat_yourself)
  (knowledge, not lines)
- [Baeldung — When using SOLID principles may not be appropriate](https://www.baeldung.com/cs/solid-principles-avoid)
- [.NET Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
  and the [dotnet/runtime FDG digest](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/framework-design-guidelines-digest.md)
- [Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
