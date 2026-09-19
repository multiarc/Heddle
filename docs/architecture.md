# Architecture

This page is for contributors who want to understand or modify the engine. It traces a
template from text to rendered output and points at the types that do each job.

## High-level pipeline

```text
Template text
  │
  ▼
1. LEX ── HeddleLexer (ANTLR, mode stack) ── tokens ──► 2. PARSE ── HeddleParser (SLL, fallback LL)
  │                                                        │  ▲
  │ syntax errors                                          │  │ parse tree
  ▼                                                        ▼  │
HeddleSyntaxErrorListener ── collected ──► HeddleCompileResult ◄── 3. WALK ── HeddleMainListener + ParseContext
                                                              │
                                              definitions, output chains
                                                              ▼
                        4. COMPILE ── HeddleCompiler (extensions + compiled accessors)
                          ├── member paths ──► expression-tree delegates
                          └── `@(...)` C# ──► Roslyn delegates
                                                              │
                                                              ▼
                        RuntimeDocument (+ IProcessStrategy)
                                                              │
                                                              ▼
                        6. RENDER ── HeddleTemplate.Generate(data) ── ScopeRenderer ──► string
```

The orchestration entry points are
[`DocumentParser.Parse`](../src/Heddle/Language/DocumentParser.cs) (steps 1–3) and
[`HeddleTemplate.Compile`](../src/Heddle/HeddleTemplate.cs) → `HeddleCompiler.Compile` (steps 4–5).

### The MSBuild build host

Precompilation runs the same pipeline out of process. The `Heddle.Build` targets serialize items
and properties into a response file; `heddle compile` parses, compiles with form recording,
and writes two outputs: the embedded compiled-form artifact
(`Heddle.CompiledForm.bin`) and the generated source (`Heddle.CompiledForm.g.cs`) — one typed entry
class per template plus one static site method per printable site. At run time the loader reads the
artifact rows through `PrecompiledTemplateInfo`, serves printable sites from the table, and rebuilds
declined sites from the recorded form. Nothing runs inside the compiler. See
[Build‑Time Pre‑compilation](precompilation.md) and
[Build integration](building.md#build-integration).

### Under the hood: form and gauntlet

The compiled form (`src/Heddle/Precompiled/CompiledForm/`) is a versioned binary section layout —
templates, documents, extensions, functions, members — written by `CompiledFormWriter` and read by
`CompiledFormReader` (schema 4 is the only readable shape; the reader rejects anything else before
any row is trusted). The run-time gauntlet (`PrecompiledGauntlet.Validate`) checks a row against the
*live* request — options fingerprint, ambient model type, extension identities, function targets,
member bindings, content staleness — and any failure degrades that template to the dynamic tier with
a `PrecompiledFallbackEvent`. Regeneration is byte-exact by construction: `CompiledFormWriter`
stamps a content digest, and `CompiledFormFixtureTests` pins a stored real-build artifact
(`src/Heddle.Tests/TestTemplate/compiled-form-v4.bin`) byte-for-byte through read and re-encode, and registers and renders it against the text compile of the same fixture.

---

## Stage 1 lexing

The lexer is generated from [HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4) (which
imports [CSharp.g4](../src/Heddle.Language/CSharp.g4) for C# tokens). It is **mode‑based**:
a stack of lexer modes makes the language context‑sensitive so that the same characters mean
different things in different places (e.g. `}}` ends a subtemplate but is plain text at the
top level).

Modes (entered/exited via `pushMode`/`popMode`/`mode`):

| Mode | Entered by | Purpose |
| --- | --- | --- |
| *(default)* | — | Top‑level text + directive starts (`@%`, `@<<`, `@`, raw). A top‑level `{{` here is a plain unconnected token — it does **not** push `SUB_BLOCK`. |
| `SUB_BLOCK` | `{{` after a call (`CALL_RETURNED`), inside a definition (`DEF`), or after an extension name (`OUT_MODE`) | Subtemplate body; `}}` pops. |
| `DEF` | `@%` | Definition block: `<name>`, `:` base, `::` type, `->` default, `(` opens a prop list, `%@` pops. |
| `DEF_PROPS` | `(` in `DEF` | Definition prop list (v2 typed props): prop names/types between `( … )`. |
| `IMPORT_MODE` | `@<<` | Import path between `{{ }}`. |
| `OUT_MODE` | `@` | Extension name; `(` opens parameter. |
| `CALL` | `(` | Parameter tokens (ids, `.`, `::`, `:`), nested `(`; inner `@` → `CS`. |
| `CALL_RETURNED` | `)` | What follows a call: `:` chain, `@` next, `{{` body, raw, etc. |
| `CS` | inner `@` in a parameter | Embedded C# expression; balances `(`/`)`, ends at matching `)`. |
| `CS_NESTED` | nested `(` in `CS` | Nested parentheses inside an embedded C# expression. |
| `INTERP_STR` / `INTERP_VERBATIM_STR` / `INTERP_HOLE` | `$"…"` / `$@"…"` inside `CS` | C# interpolated‑string literals and their `{ … }` holes. |

The mode transitions (solid = push, dashed = pop/return to a mode):

```mermaid
stateDiagram-v2
    state "DEFAULT" as Top
    [*] --> Top
    Top --> DEF: "@%"
    Top --> IMPORT_MODE: "@&lt;&lt;"
    Top --> OUT_MODE: "@"

    DEF --> SUB_BLOCK: "{{"
    DEF --> OUT_MODE: "-&gt; (default output)"
    DEF --> Top: "%@"

    OUT_MODE --> SUB_BLOCK: "{{ (after ext name)"

    SUB_BLOCK --> Top: "}}"
    IMPORT_MODE --> Top: "}}"

    OUT_MODE --> CALL: "("
    CALL --> CS: "inner @"
    CALL --> CALL_RETURNED: ")"
    CS --> CALL_RETURNED: "matching )"

    CALL_RETURNED --> OUT_MODE: ": (chain) or @"
    CALL_RETURNED --> SUB_BLOCK: "{{ (call body)"
    CALL_RETURNED --> Top: "text / raw"
```

Comments (`@* … *@`), trimmed whitespace (`@\`), and definition whitespace are routed to the
**hidden channel** so they never reach the parser but are still available for tooling (the
listener collects hidden‑channel positions as `SkippedTokens`). This is why comments can
appear mid‑construct — between any two tokens (e.g. inside a call) — though never inside a single
token. See the
[Language Reference → lexer modes](language-reference.md#how-the-lexer-reads-a-template-modes)
for the author‑facing view.

---

## Stage 2 parsing

The parser is generated from [HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4). The
top‑level rule is `heddle`; the interesting rules are `definition`, `outblock`, `chain`, `call`,
`member_expression`, `csharp_expression`, and `subtemplate`.

[`DocumentParser`](../src/Heddle/Language/DocumentParser.cs) controls prediction strategy:

- It first parses in **SLL** mode (fast). If SLL throws `ParseCanceledException` (ambiguity) it
  **falls back** to `LL_EXACT_AMBIG_DETECTION` (full LL) via `ParseDiagnosticMode` and records a
  `HeddleCompileWarning` noting the SLL failure. If SLL instead merely reported errors, it re‑parses
  the same way in full LL mode, but records **no** warning on that path.
- When `TemplateOptions.ProvideLanguageFeatures` is set (editor/tooling mode), it parses
  directly in LL mode and produces a token list for syntax highlighting instead of optimizing
  for throughput.

Syntax errors are gathered by
[`HeddleSyntaxErrorListener`](../src/Heddle/Language/HeddleSyntaxErrorListener.cs) into the
`ParseContext`, then copied onto `CompileContext.CompileErrors`.

---

## Stage 3 tree walking

A `ParseTreeWalker` drives
[`HeddleMainListener`](../src/Heddle/Language/HeddleMainListener.cs), which builds the
[`ParseContext`](../src/Heddle/Language/ParseContext.cs): the set of **definitions**
(`DefinitionBlock`/`DefinitionItem`), **output chains** (`OutputChain`/`OutputItem`),
imports, and the raw/text spans. This is the structured representation the compiler consumes.

---

## Stages 4 and 5 compilation

[`HeddleCompiler`](../src/Heddle/Runtime/HeddleCompiler.cs) turns the parse context into an
**execution‑ready document** ([`RuntimeDocument`](../src/Heddle/Runtime/RuntimeDocument.cs)):
a tree of extension instances, each wired to a **compiled** value accessor. The template itself
is *not* transpiled to C# or to IL — its structure becomes an object graph; only the value
accessors are compiled. For statically‑typed models nothing is interpreted or reflected at render
time; dynamic (`dynamic`/`ExpandoObject`) models instead bind their member paths through cached
DLR call sites (see below).

- It instantiates the right **extension** for each call (resolved by name from the registered
  set), and calls `InitStart` to thread types through the chain and compile nested
  subtemplates; extensions needing a second pass use `CompleteInit` (e.g.
  [`PartialExtension`](../src/Heddle/Extensions/PartialExtension.cs)).
- **Member paths** (`@(A.B.C)`) compile to **expression‑tree delegates**:
  [`ModelParameter`](../src/Heddle/Runtime/Parameters/ModelParameter.cs) builds an
  `Expression<Func<object,object>>` over the resolved property chain (null‑safe for reference
  types) and `.Compile()`s it. Reflection is used **only here, at compile time**, to locate the
  properties — never at render time for statically‑typed models. (Dynamic models compile instead to
  DLR call sites that resolve members at render time, cached per receiver type.)
- **Embedded C# expressions** (`@( … )`) are emitted into C# source via the `.tcs` templates in
  [src/Heddle/LanguageTemplates](../src/Heddle/LanguageTemplates)
  (`CSharpPreparseTemplate.tcs`, `CSharpClassTemplate.tcs`) and compiled by **Roslyn**
  (`Microsoft.CodeAnalysis.CSharp`) into `(model, chained, root)` delegates
  ([`CompiledParameter`](../src/Heddle/Runtime/Parameters/CompiledParameter.cs)). This is
  also where C# expressions are **bound to the model's types**; the
  [`CompileScope`](../src/Heddle/Runtime)/`CSharpContext` track imported namespaces
  (`@using`) and the model type (`@model`, `:: Type`). The Roslyn pass is run by
  `ContextCompilation.Compile`, an internal extension method over `CompileScope` — not a member of
  `CompileScope`, which exposes no compile entry point of its own.
- Errors from either path are collected as `HeddleCompileError`s rather than thrown, and surfaced
  through [`HeddleCompileResult`](../src/Heddle/Data/HeddleCompileResult.cs).

The result is a `RuntimeDocument` exposing an `IProcessStrategy` (`Strategy`) — the
execution‑ready render tree. So "compiling a template" means **two** kinds of code generation
(expression‑tree delegates for member access, Roslyn delegates for embedded C#) wired into one
document — not a single whole‑template Roslyn compile.

---

## Stage 6 rendering

[`HeddleTemplate.Generate`](../src/Heddle/HeddleTemplate.cs) creates a
[`ScopeRenderer`](../src/Heddle/Data) and a root [`Scope`](../src/Heddle/Data/Scope.cs),
then invokes `_processStrategy.Render(scope)`. Extensions write through
`scope.Renderer.Render(...)` (the streaming path, `RenderData`) or return strings
(`ProcessData`) when a parent needs the value (e.g. inside a chain). The renderer's buffer is
size‑adaptive across calls (a per‑instance high‑water mark with ~10 % margin; the field is
length‑based on net8+ and count‑based on older targets).

`Scope` is a small readonly struct carrying `ModelData`, `ChainedData`, `ParentModelData`,
`CallerData`, `RootData`, and the `Renderer`; its pure transforms (`Model`, `Parent`, `Chain`,
…) construct the child scopes extensions hand to their subtemplates. See
[Writing Custom Extensions](custom-extensions.md) for the author‑facing contract.

---

## Performance characteristics

The repository's [BenchmarkDotNet suite](../benchmarks/dotnet) measures Heddle against five
other .NET template engines (Fluid, Scriban, DotLiquid, Handlebars.Net and ASP.NET Core Razor) over
eight workloads, every one of them rendering byte‑identical parity‑checked output
(`[MemoryDiagnoser]` enabled); measurements are taken and kept outside the repository
([benchmarks/README.md](../benchmarks/README.md)). The design of the render path is what the harness
measures:

- **Execution‑ready document, not per‑call activation.** Each template becomes a
  `RuntimeDocument` / `IProcessStrategy` with extension instances already resolved and typed,
  and every value accessor pre‑**compiled** (expression‑tree delegates for member paths, Roslyn
  delegates for embedded C#) — no reflection or re‑parsing per render. Rendering a dozen
  components is a dozen direct `RenderData` calls — it does not spin up partials, view
  components, section buffers, or DI scopes per component the way Razor does. The benchmark
  page exercises exactly this: many `@area_component(...)`, `@assets_component(...)`,
  `@head_scripts()`, etc. calls.
- **Streaming, low‑allocation output.** Extensions write directly to a single
  `ScopeRenderer` whose buffer is **size‑adaptive across renders** (a per‑instance high‑water
  mark), so a warmed‑up template stops reallocating its output buffer. `list` pre‑sizes from
  `ICollection<T>.Count` when available.
- **Struct `Scope`, aggressive inlining.** The per‑node data view is a readonly `struct` with
  `[MethodImpl(AggressiveInlining)]` transforms, avoiding per‑scope heap allocation as the
  renderer descends into elements and subtemplates.
- **Composition is near‑free at run time.** Splitting a page into independent reusable templates
  recombined by a layout (see the benchmark's `@<<{{shared/layout.heddle}}` import + `<body:body>`
  override) renders through one pre‑built extension node per definition invocation — no per‑render
  lookup, activation, or buffer indirection — unlike Razor sections, whose layout/section binding
  adds indirection. See
  [Language Reference → inheritance](language-reference.md#inheritance-and-override-with-child-and-base).

The trade‑off is **up‑front compilation**: the first compile runs ANTLR (parse), expression‑tree
compilation (member accessors), and Roslyn (embedded C#), so it is not cheap — the model is
"compile once, render many" — a cost amortized across every cached render. Compile cost is benchmarked by the harness's cold sidebar
(`dotnet run -c Release --project benchmarks/dotnet -- bench-cold`), which measures parse and
compile as separate rows because they are separate steps.

---

## Source layout (`src/`)

| Path | Contents |
| --- | --- |
| [src/Heddle](../src/Heddle) | Core engine. |
| `Heddle/Core` | Extension base classes, `InitContext`. |
| `Heddle/Extensions` | The 24 built‑in extensions. |
| `Heddle/Language` | Parser host: `DocumentParser`, `HeddleMainListener`, `ParseContext`, `OutputItem`, `DefinitionItem`, `CallParameter`. |
| `Heddle/Runtime` | `HeddleCompiler`, `RuntimeDocument`, `CompileContext`, `CompileScope`, `IExtension`, `TemplateResolver`. |
| `Heddle/Data` | `Scope`, `TemplateOptions`, `HeddleCompileResult`, `ExType`, `LinearList`, render types. |
| `Heddle/Attributes` | The extension/model attributes. |
| `Heddle/Strings` | Fast string building (`ExStringBuilder`). |
| `Heddle/LanguageTemplates` | `.tcs` resources used to emit C# for Roslyn. |
| [src/Heddle.Language](../src/Heddle.Language) | ANTLR grammar + generated lexer/parser + editor assets. |
| [src/Heddle.Build](../src/Heddle.Build) | MSBuild task + targets/props: the out-of-process build host. |
| [src/Heddle.Tool](../src/Heddle.Tool) | The `heddle` CLI incl. `compile`: form writer, site printers, source emitter. |
| [src/Heddle.Tests](../src/Heddle.Tests) | xUnit tests + `.heddle` fixtures. |
| [benchmarks/dotnet](../benchmarks/dotnet) | BenchmarkDotNet benchmarks — the cross-stack .NET leg. Not in `Heddle.sln`. |

---

## The grammar and generated code

The grammar lives in [src/Heddle.Language](../src/Heddle.Language):

- [HeddleLexer.g4](../src/Heddle.Language/HeddleLexer.g4) — lexer (modes, tokens).
- [HeddleParser.g4](../src/Heddle.Language/HeddleParser.g4) — parser rules.
- [CSharp.g4](../src/Heddle.Language/CSharp.g4) — C# token fragments imported by the lexer.

Generated C# (checked in under `generated/`) is produced by ANTLR 4.13.1. To regenerate after
editing the `.g4` files, run [generate_cs.cmd](../src/Heddle.Language/generate_cs.cmd)
(requires Java; the script downloads the ANTLR 4.13.1 jar from antlr.org on first run):

```
java -jar "antlr-4.13.1-complete.jar" -Dlanguage=CSharp "HeddleLexer.g4" "HeddleParser.g4" -o "generated" -lib "generated" -package Heddle.Language
```

The project references `Antlr4.Runtime.Standard` 4.13.1 at run time. The `js/` and
`ace_build/` directories are excluded from the C# compile
([Heddle.Language.csproj](../src/Heddle.Language/Heddle.Language.csproj)).

---

## Editor and tooling integrations

- **JavaScript parser** — `generate_js.cmd` produces a JS lexer/parser under `js/` from the
  same grammar (for in‑browser editing).
- **Ace editor** — `ace_build/` and `build_ace.sh` build an
  [Ace](https://ace.c9.io/) mode using the JS parser for syntax highlighting on the web.
- **Language features (tooling)** — setting `TemplateOptions.ProvideLanguageFeatures` makes
  `DocumentParser` emit a token list — the basis for editor classification (highlighting), error
  tagging, and completion / quick info for `.heddle` files.
- **TextMate grammar** — [coloring-scheme/heddle.tmLanguage.json](https://github.com/multiarc/Heddle/blob/main/docs/coloring-scheme/heddle.tmLanguage.json)
  is a portable highlighter (HTML + inline C# embedded) whose scopes mirror the Ace mode, for
  VS Code/Monaco/Shiki and documentation sites. See [Syntax Highlighting](syntax-highlighting.md).

For build/test/packaging mechanics, continue to [Building & Testing](building.md).
