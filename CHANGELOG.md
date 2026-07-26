# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0]

Additive at the language and rendering level — **no rendered byte changes on either tier** — with one
declared **binary** break in the precompiled-manifest contract and three build-time behaviours that
begin to occur because they were never wired. Each item's window judgement is recorded in
[breaking-windows.md](docs/spec/common/breaking-windows.md#explicit-not-window-gated-rulings).

### Changed (breaking)

- **Precompiled assemblies built by a 2.0.x generator are no longer accepted.**
  `PrecompiledSchema.MinSupportedSchemaVersion` rises `1` → `3`, and the manifest schema the generator
  emits goes `2` → `3`. 2.0.0 shipped schema 2; this release moves the prop-layout fingerprint onto
  `PrecompiledExtensionBinding` as an optional third constructor parameter, which **removes** the
  two-argument `.ctor(string, string)` that every schema 1–2 manifest calls. Those manifests therefore
  cannot run against the 2.1 engine at all, and a floor of `1` would *accept* them and then crash with
  a `MissingMethodException` out of `PrecompiledTemplates.Register` at host startup. The gate rejects
  them cleanly instead.
  Schema 3 is a **single** increment carrying everything added since 2.0: the `PrecompiledRuntime.DynamicMember`
  routing, the extension prop-layout fingerprint, the per-carrier `BindDefinition` overload, and the two
  new per-template fields below. There is no schema 4 or 5 — intermediate numbers existed only inside
  unreleased development and are not migration steps.
  **What to do:** rebuild with the 2.1 `Heddle.Generator`. `Heddle.Generator` and `Heddle` are
  version-locked — pair the matching versions.
  **If you do not:** registration raises one `PrecompiledFallbackReason.SchemaVersionUnsupported`
  callback (`HED7102`) per assembly and every template renders through the byte-identical dynamic
  path; under `TemplateOptions.PrecompiledMismatchPolicy.Strict` it throws instead, which is that
  option's purpose. No compatibility shim: restoring the two-argument constructor would keep the
  unrunnable manifests accepted, which is the defect.

- **`<HeddleTemplate>` per-item metadata now takes effect.** `Key`, `Name` and `Precompile` were all
  inert from a real project — the targets file overwrote each with the empty string while appearing to
  map it — so only projects that never used them were unaffected. If you set `Key`, the template's
  registration key **and its generated entry-class name** now follow it, so a call to the old
  path-derived class name must be renamed. `Name` moves nothing (see *Added*). If you set
  `Precompile="false"`, that file now really stops precompiling (it remains available to `@<<` imports)
  and renders through the dynamic path.

### Added

- **`Name` item metadata** as an **additional** name for a template — not a rename:
  `<HeddleTemplate Update="t/report.heddle" Name="BuildReport" />` leaves the key
  `t/report.heddle` and the class `Heddle.Generated.T_Report` exactly as they were, and makes
  `BuildReport` resolve **as well as** `t/report.heddle`. Nothing that resolved before stops
  resolving. It pairs naturally with `Precompile="false"`: an import-only partial under a friendly name.
  Because a name is not a registration key, it takes no part in the duplicate (`HED7002`) or
  case-only-twin (`HED7003`) checks and does not suppress the out-of-root warning (`HED7018`) — the
  path-derived key is still there and still unasked-for. Setting `Key` *and* `Name` is two names for one
  template, not a conflict. A value the normalizer refuses, or a name another template already answers
  to, is `HED7004` against the name; the key is unaffected.
- **The name works at run time too, not only for `@<<` imports.** The manifest row carries it
  (`PrecompiledTemplateInfo.RegisteredName`) and `PrecompiledTemplates.TryGet`/`TryResolve` — and so every
  resolver arm — find the same template by either spelling. Where a spelling names one template's key and
  another's registered name, **the key wins**, whichever assembly registered first: a name is an addition
  and never displaces a spelling that already resolved. Lookup is ordinal, as key lookup is.
- **`HED7028`** (warning): an `@<<` import names a template by its registration key while that template
  also carries a `Name`. Both spellings resolve — this recommends the name-first spelling for a named
  template. It cannot fire for a project that sets no `Name`.
- **`HED7104`** (`PrecompiledFallbackReason.RegisteredNameUnavailable`, via `OnFallback`): a registered
  `Name` could not become a lookup spelling because another *registered* template already answers to it,
  as its key or as its own name. Never a throw — the template stays reachable by its key, and only the
  addition is lost. This collision is only detectable at registration, since the build tier cannot read a
  referenced assembly's manifest rows; within one build the same fault is `HED7004`.
- **`PrecompiledTemplateInfo.LinePathForm`** records which form a template's generated `#line` file names
  are in — `RootRelative`, `TemplatePath`, or `Unspecified` for a fallback-marker row that has no
  generated source. Machine-readable, for stack-trace symbolizers and editor tooling.
- **`Precompile="false"` items are validated and advised.** An opted-out item's `Key`/`Name` now raise the
  same `HED7004` faults an included item's would, instead of failing silently and surfacing as `HED7011`
  at whichever file imported it; and its own imports can draw the `HED7028` advisory. It still contributes
  no entry point and no manifest entry. Its *template* errors remain unreported — the file is excluded from
  this build by request, and they surface through any precompiled template that imports it.

### Fixed

- **`#line` directives in generated code name the template file, not its registration key.** The two
  were always equal for a path-derived key; an explicit `Key` made the difference observable and would
  have pointed every mapped span at a path that does not exist. A template **outside**
  `HeddleTemplateRoot` now gets its own (absolute) path rather than a bare filename the compiler cannot
  open, and which form a template's `#line` names are in is recorded on its manifest row (see
  `LinePathForm` under *Added*) rather than as a comment in the generated file, so tooling can act on it.
- **`heddle-lsp --version` and the LSP `initialize` response reported `1.0.0`** for the whole 2.0 line.
  The value is now read off the assembly rather than hand-maintained.
- **`HED7004`'s message** names the offending metadata and the reason, covering an unusable or
  already-taken `Name` as well as a malformed `Key`.

### Build and packaging

- The release line is stated once, as `<VersionPrefix>` in `Directory.Build.props`, replacing nine
  per-project `<Version>` elements; a version-consistency test holds the statements that cannot live
  there (the npm manifests, the VS Code extension's pinned tool version, the LSP workflow's
  `--version`, the prose release-line sentences, the CHANGELOG section) in step with it.
- `Heddle.Demo.Models` and `Heddle.Demo.Wasm` are strong-named, so the build is `CS8002`-clean. The one
  remaining unsigned reference is third-party (Scriban) and is suppressed at the reference rather than
  by a blanket `NoWarn`.

## [2.0.0] - 2026-07-19

The **2.0** release is a single breaking window: almost everything below is additive and the dynamic
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

- The loop-model class **`Heddle.Models.ForModel`** has been deleted, replaced by the immutable
  **`Heddle.Models.Range`** value type (`public readonly struct`: `Start` inclusive, `Last`
  exclusive, `Step`; value equality; a readable `ToString()`). The `range(...)` built-in, its
  precompiled twin `PrecompiledFunctions.Range`, and `@for` now produce/consume
  `Heddle.Models.Range`; the `int` fast path (`@for(5)` / `@for(Count)`) and every loop's rendered
  bytes are unchanged. **Migration is mechanical:**
  - *Constructions* — `Range`'s properties are get-only, so a `ForModel` object initializer becomes
    a constructor call: `new ForModel { Start = s, Last = l, Step = st }` → `range(s, l, st)`
    (native tier) or `new Heddle.Models.Range(s, l, st)` (C# tier); supply `0` for an omitted
    `Start` and `1` for an omitted `Step` — e.g. `new ForModel { Last = l }` →
    `new Heddle.Models.Range(0, l)` (or just `@for(l)`).
  - *Member reads* — change the declared type to `Heddle.Models.Range` (fully qualified: bare
    `Range` is ambiguous with `System.Range` under `using System;`). `Start`/`Step` are now
    non-nullable `int`, so strip every nullable operation on them (`?? 0`/`?? 1` → the bare read;
    drop `.HasValue`; `.Value`/`.GetValueOrDefault()` → the bare read) — leaving one is a compile
    error. `Last` was already `int`.
  - *Interop* — a from-start `System.Range` maps via `Heddle.Models.Range.FromSystemRange(r)`
    (net6.0+ only; step 1; a from-end `^` index throws `ArgumentException`).
  - *Rendered output (no action)* — stringifying a range (standalone `@(range(...))`,
    `str`/`format`, or C#-tier interpolation) now renders the readable call form
    `range(2, 10, 2)` instead of the useless type name `Heddle.Models.ForModel`; this is the
    change's only rendered-byte effect.
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

[2.1.0]: https://github.com/multiarc/Heddle/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/multiarc/Heddle/compare/v1.0.1...v2.0.0
[1.0.0]: https://github.com/multiarc/Heddle/releases/tag/v1.0.0
