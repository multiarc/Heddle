# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

The **2.0** release is being formed on this branch and has **not** shipped yet (the latest released
tag is `v1.0.1`). It is a single breaking window: almost everything below is additive and the dynamic
engine's rendered bytes are unchanged, except for the items under **Changed (breaking)** and
**Removed**.

### Added

- **Native expression tier** — a sandbox-safe, Roslyn-free expression language in call parens
  (`@if(Count > 0)`, `@(Price * Quantity)`, `@(Name ?? "anon")`, `@(cond ? a : b)`). Follows the C#
  precedence table and compiles to `System.Linq.Expressions`; capability is limited to
  properties/indexers/operators/literals plus host-whitelisted functions (`FunctionRegistry`,
  `[ExportFunctions]`). New `TemplateOptions.ExpressionMode` (`MemberPathsOnly` / `Native` (default) /
  `FullCSharp`); the inner-`@` form stays full C#.
- **Safe-by-default HTML output** — `OutputProfile.Html` HTML-encodes the unnamed `@(...)` carrier;
  `@raw(...)` is the explicit opt-out and `@profile(html|text)` flips mid-document. The encoder is a
  pluggable `TemplateOptions.Encoder` (`System.Text.Encodings.Web.TextEncoder`) — opt-in, with the
  default (`null`) preserving the existing `WebUtility.HtmlEncode` bytes.
- **Context-encoding extensions** `@attr` / `@js` / `@url` — encode a value for the HTML-attribute,
  JavaScript-string-literal, and URL-component contexts respectively.
- **Declarative branching** — `@elif` / `@else` (and `@ifnot`) as ordinary extensions that coordinate
  with a preceding `@if` through a published local context. Public `Heddle.Attributes.BranchRole`
  (`Opener`/`Continuation`/`Terminal`) + `[BranchRoleAttribute]` drive set classification; the
  `Scope.Publish`/`TryRead` channel, `BranchState`, and `[ScopeChannel]` let custom extensions join a
  set with the same semantics as the built-ins. Text between branch blocks is stripped with a warning.
  Optional drift diagnostics (`HED3005` runtime, `HED7016` generator) flag a continuation/terminal
  missing `[ScopeChannel]`.
- **Loop & directive ergonomics** — `@for(5)` / `@for(Count)` / `@for(range(...))` and `@list` loop
  sugar (no hand-written C# loop models); `TrimDirectiveLines` to swallow whole-line directives'
  trailing newline; a double-render warning when a default-output definition is also called by name.
- **Named props & slots** — typed named definition parameters with defaults
  (`@card(Article, style: "wide", compact: true)`), prop reads in bodies, and parameterized `@out()`
  slots, with abstract-definition composition and inheritance narrowing. Missing/unknown/mistyped
  props are positioned compile errors at the call site.
- **Streaming & UTF-8 render surface** — `Generate(data, TextWriter)` and
  `Generate(data, IBufferWriter<byte>)` sink overloads with a two-tier, surrogate-safe UTF-8 transcode
  path (O(1) allocation regardless of output size). The string path stays bit-identical. New public
  renderer surface (`IScopeRenderer` / `ISpanScopeRenderer` / `IUtf8ScopeRenderer` and implementations).
- **Render budgets** — `TemplateOptions.RenderBudget` (`MaxOutputChars` / `MaxRenderOps` /
  `MaxRenderTime`) for running untrusted templates; a breach throws
  `Heddle.Exceptions.TemplateRenderBudgetException` carrying `Kind`/`Limit`/`Observed`. Default is
  unlimited with zero render-path cost, enforced at the renderer seam so counts are uniform across the
  string/`TextWriter`/`IBufferWriter` sinks and identical on the dynamic and precompiled backends.
- **Build-time precompilation** — `Heddle.Generator`, a Roslyn incremental source generator that
  compiles `.heddle` templates to `IProcessStrategy` classes at build time, byte-for-byte identical to
  the dynamic engine and with no runtime Heddle dependency in the generated output. Covers the full
  construct set; template errors surface as positioned build diagnostics (`HED70xx`) and anything not
  yet emittable degrades safely to the dynamic path. Engine and generator are version-locked;
  `PrecompiledMismatchPolicy` governs a mismatch; a `Heddle.CSharpTierEnabled` trim switch keeps
  Roslyn out of a trimmed publish.
- **Stable diagnostic IDs** — `Heddle.Data.HeddleDiagnosticIds` (`HEDxxxx`), surfaced by both the
  runtime compiler and the generator.
- **Editor tooling & LSP** — `Heddle.LanguageServices` (flag-gated, null-cost when off) and a
  hand-rolled LSP 3.17 server (`Heddle.LanguageServer`): typed member completion inside `@(...)`, hover
  types, diagnostics at template positions, and go-to-definition across `@<<` imports (imported-file
  diagnostics re-anchored to the import site). Ships with a VS Code extension and a v2 Ace web grammar.
- **`heddle` CLI** — a T4-successor render tool (`heddle render <template> --model-json <file>`).
- **Docs, demo & samples** — an in-browser demo (Ace + typed WASM language worker + sandboxed render
  pane) on the published docs site, and a gallery of ten runnable, golden-asserted samples that double
  as end-to-end CI coverage.

### Changed (breaking, vs 1.x)

- `TemplateOptions.OutputProfile` now defaults to `OutputProfile.Html`: the unnamed `@(...)` output
  HTML-encodes by default. Opt out per output with `@raw`, or restore the 1.x behavior per template
  with `OutputProfile.Text`.
- `TemplateOptions.TrimDirectiveLines` now defaults to `true`: whole-line directives swallow their
  trailing newline. Set `TrimDirectiveLines = false` to restore the 1.x behavior.

### Deprecated

- `TemplateOptions.AllowCSharp` is obsolete — use `ExpressionMode` instead (`AllowCSharp == true` is
  equivalent to `ExpressionMode.FullCSharp`; `false` selects `Native`, or leaves `MemberPathsOnly`
  untouched). Reads and writes keep working as a compatibility bridge.

### Removed

- The legacy **`@import()`** include has been removed. Any `@import` call site now fails to compile
  with a single positioned **`HED4003`** error at the call, naming both replacements: `@<<{{ path }}`
  to share definitions and layouts across files, or `@partial(){{ name }}` to embed another template's
  rendered output inline. The `import` name is retained only as a registered no-op tombstone, so the
  diagnostic is a targeted migration signpost rather than a generic "unknown extension" error.
  `@<<{{ path }}` and `@partial()` are unaffected. See docs/language-reference.md#imports--.

### Fixed

- A `@<<` composition import nested below the top level no longer crashes the compiler; it reports a
  positioned `HED4004` and skips the import.
- A default-output definition's `@out()` no longer silently drops a chained value, and the built-in
  `@out()` no longer double-renders its static default body.
- Cross-file override / definition-body position corruption caused by inner comments.
- Quick-start examples no longer crash on anonymous-type dynamic models.

### Compatibility

- Precompiled projects must be rebuilt with the 2.0 `Heddle.Generator` when upgrading the engine; 1.x
  manifests are rejected by the engine-version gate and fall back (or throw under
  `PrecompiledMismatchPolicy.Strict`). `Heddle.Generator` and `Heddle` are version-locked — pair the
  matching versions.

## [1.0.0]

Initial public release.

[Unreleased]: https://github.com/multiarc/Heddle/compare/v1.0.1...HEAD
[1.0.0]: https://github.com/multiarc/Heddle/releases/tag/v1.0.0
