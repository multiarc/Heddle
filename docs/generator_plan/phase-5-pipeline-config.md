# Phase 5 — pipeline & configuration (generator ↔ runtime contract surfaces)

## Header

- **Status:** proposed — not started
- **Goal (one line):** One canonical definition each for the five pipeline-level contracts the
  generator and runtime currently restate by hand — content hash, key↔path derivation, the
  `.heddle` extension rule, schema/engine versioning, and the option names/defaults table — with
  the four live bugs in that surface fixed first ([05 F1](../research/generator-code-sharing/05-pipeline-config.md),
  F3, F5, F8) and the identity-bearing option types linked into the generator so the manifest
  fingerprint is constructed, not transcribed.
- **Depends on:** nothing in this initiative — the fix-first group and every shared artifact here
  are independently shippable. Phase 5 *unblocks* other phases: it owns
  `Precompiled/ContentHash.cs` (co-reported by [06 F1](../research/generator-code-sharing/06-diagnostics-utilities.md);
  phase 6 references it), and it owns the `<Compile>` links for `OutputProfile`/`ExpressionMode`/
  `PrecompiledOptionsFingerprint`/`PrecompiledCapabilities` that phases 1 and 3 consume.
- **Changes an externally-visible contract:** no rendered byte, no option default, no schema
  version, and no public runtime API shape changes. Three externally-*observable* deltas, all
  additive or contract-restoring: (1) the runtime staleness check starts hashing the decoded
  template text instead of raw file bytes, so BOM'd/UTF-16 templates that today permanently fail
  `StaleContent` start being served precompiled (D1 — a defect fix toward the documented
  behavior, analyzed under Back-compat); (2) two new build **warnings** (`HED7018`, `HED7019`)
  claimed per the [registry rules](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry);
  (3) the engine-version fallback string in manifests built without a visible `Heddle` reference
  changes from the literal `"2.0.0"` to the generator's own assembly version (identical today,
  correct in the future — D6).

## Goal

Area 05 of the code-sharing research audited `HeddleTemplateGenerator.cs` + `Pipeline/*` against
the runtime's options, fingerprint, and path rules and found the pipeline-level contract stated
two-to-four times per rule, with the copies already disagreeing in four user-visible ways
([05](../research/generator-code-sharing/05-pipeline-config.md), all line references re-verified
against source for this plan):

1. **Content hash inputs disagree** (F1). The generator hashes Roslyn's *decoded* text re-encoded
   as UTF-8 (`ComputeContentHash`, `src/Heddle.Generator/HeddleTemplateGenerator.cs:412-422`, fed
   from `pair.Left.GetText(ct).ToString()` at `:64-72`); the runtime hashes the *raw file byte
   stream* (`PrecompiledGauntlet.HashFile`, `src/Heddle/Precompiled/PrecompiledGauntlet.cs:186-191`).
   Any template with a UTF-8 BOM or a non-UTF-8 encoding therefore fails `StaleContent` on every
   request under `EnableFileChangeCheck` and silently renders dynamic. `PrecompiledGauntlet.HashBytes`
   (`:193-197`) is verified dead — zero callers repo-wide — and is exactly the primitive the
   generator re-implemented.
2. **Key derivation silently degrades** (F3). `Relative`
   (`src/Heddle.Generator/HeddleTemplateGenerator.cs:481-490`) falls back to
   `Path.GetFileName(path)` when a template is not under `HeddleTemplateRoot` — the directory
   vanishes from the key with no diagnostic, runtime lookups miss, and multi-file collisions
   surface as spurious `HED7002` "duplicates". Its root-prefix test is `OrdinalIgnoreCase` while
   every key comparison is `Ordinal`. The runtime's inverse (`key → path`) is written
   independently at `PrecompiledGauntlet.cs:167,175`.
3. **Version literals** (F5). The manifest emits `schemaVersion: 2` as a string literal
   (`HeddleTemplateGenerator.cs:380`) against runtime consts `MinSupportedSchemaVersion = 1` /
   `MaxSupportedSchemaVersion = 2` (`src/Heddle/Precompiled/PrecompiledTemplates.cs:20-21`), and
   `ResolveEngineVersion` fabricates `"2.0.0"` when the `Heddle` reference is not visible
   (`HeddleTemplateGenerator.cs:503`) — the runtime's `IsEngineCompatible` gate
   (`PrecompiledTemplates.cs:196-202`) then decides whole-assembly registration on a number the
   generator never observed.
4. **Declared-but-unread item metadata** (F8). `Name` and `Precompile` are declared as
   compiler-visible metadata (`src/Heddle.Generator/build/Heddle.Generator.props:16-19`, flowed at
   `build/Heddle.Generator.targets:10-15`) but the pipeline reads only `Key`
   (`HeddleTemplateGenerator.cs:58`); `<HeddleTemplate Precompile="false"/>` precompiles anyway.
   [precompilation.md](../precompilation.md) already states "the only item metadata the generator
   reads is `Key`".

Beyond the bugs, the same audit found the identity-bearing option triple stated in three type
systems (F2), the option names/defaults table stated three times (F6), the `.heddle` extension
rule in five places (F4), and the manifest emitted as untyped string templating with the
marker/normal entry builders duplicated verbatim (F7,
`src/Heddle.Generator/Emit/TemplateEmitter.cs:2520-2568`).

This phase fixes the four bugs first (each independently shippable), then collapses each restated
rule to one shared netstandard2.0 source file linked into the generator by the established
`<Compile Include>` precedent (`src/Heddle.Generator/Heddle.Generator.csproj:49-63` already links
`Precompiled/TemplateKey.cs` and `Precompiled/DefaultFunctionTable.cs`), and adds the lockstep
tests that make future drift a red build instead of a silent runtime fallback.

## Non-goals / scope boundary

- **No option default changes and no option renames.** Defaults are window-governed per
  [breaking-windows.md](../spec/common/breaking-windows.md) and the
  [2.0 record](../spec/records.md#the-20-breaking-window--as-shipped-record) precedent (item 5
  flipped the generator MSBuild defaults *together with* the engine, inside a ratified window).
  This phase de-triplicates where defaults are *defined*; every effective value —
  `Html`/`Native`/`true`/`100`/`$(MSBuildProjectDirectory)`/`false` — is byte-for-byte unchanged,
  and the props↔code lockstep test (D8) exists precisely to prove that.
- **No schema bump.** Consolidating the schema-version literals into `PrecompiledSchema` keeps
  `Current = 2`, `Min = 1`, `Max = 2` — an invariant test pins `Min ≤ Current ≤ Max` and that the
  consolidation change emits `schemaVersion: 2` bytes identical to today's.
- **No Roslyn types in shared files, no `Heddle.dll` reference from the generator, everything
  netstandard2.0** — the hard constraints from the
  [research overview](../research/generator-code-sharing/00-overview.md) hold throughout.
  `AnalyzerConfigOptions` reading stays generator-side as a thin shim (D8).
- **No new behavior for the resolver's `View`/`PartialView`/`Master` arms.** Whether those arms
  should consult the precompiled registry is a genuine behavior question — recorded in Open
  questions (OQ2) with a recommendation, not decided here. `TemplateResolver.Search`'s folding
  onto `TemplateKey` (WI7) is a byte-identical refactor only.
- **No change to `TemplateOptions.Equals`/`GetHashCode` or the resolver `CacheKey`.** Verification
  for this plan found the research's restatement #2 slightly off: `TemplateOptions.Equals`
  (`src/Heddle/Data/TemplateOptions.cs:164-167`) compares `FileNamePostfix`/`TemplateName`/
  `RootPath`/`OutputProfile`/`TrimDirectiveLines`/`Encoder` and **omits `ExpressionMode`**, as
  does the resolver `CacheKey` (`src/Heddle/Runtime/TemplateResolver.cs:58-59`) — a latent
  cache-identity gap adjacent to this area but cache-behavior-affecting to touch. Recorded here as
  a verified finding for the runtime options owner; deliberately not fixed in this phase (revisit
  trigger: any report of a template served under the wrong expression mode from a shared
  resolver/options cache).
- **No `Heddle.Shared` project.** The linked-source mechanism is the precedent and the ruling of
  the [synthesis](../research/generator-code-sharing/07-recommendations.md#proposed-shared-code-layout);
  a dedicated source-only project is revisited only if the shared set outgrows it (~20 files).

## Design direction

The runtime/manifest contract is authoritative: where a rule already has one true home
(`TemplateKey` normalization, the gauntlet's check order, the registry's `Ordinal` key
comparisons), the generator converges on it. Where the canonical form is genuinely a choice, the
choice is recorded below as a decision with rationale. Only the two truly user-facing choices go
to Open questions.

### D1 — Canonical content-hash input: the decoded template text, on both sides

- **Decision.** The staleness identity of a template is the lowercase-hex SHA-256 of its
  **decoded text re-encoded as UTF-8 without BOM** — the form the generator already hashes. A new
  shared, IO-free `src/Heddle/Precompiled/ContentHash.cs` (`HashText(string)`,
  `HashBytes(byte[])`, `ToHex`) is linked into the generator next to the existing `TemplateKey.cs`
  link; `HeddleTemplateGenerator.ComputeContentHash` is deleted in favor of
  `ContentHash.HashText`, and the runtime's `PrecompiledGauntlet.HashFile` becomes
  decode-then-hash: read the file with BOM detection (BOM honored and stripped; no BOM ⇒ UTF-8),
  then `ContentHash.HashText`. The dead `HashBytes`/`ToHex` pair in the gauntlet is removed with
  the extraction. The `PrecompiledTemplateInfo.ContentHash` XML doc ("SHA-256 … of the template
  file's raw bytes", `src/Heddle/Precompiled/PrecompiledTemplateInfo.cs:56`) is corrected in the
  same change — it describes a contract no shipped manifest has ever satisfied for BOM'd files.
- **Why text, not raw bytes on both sides.** Raw-bytes-on-both-sides is not implementable: the
  generator's only input is `AdditionalText.GetText()` — a decoded `SourceText` with no raw-byte
  accessor — and `Heddle.Generator.csproj` sets `EnforceExtendedAnalyzerRules=true`, under which
  direct file IO in the generator is a banned-API violation (RS1035) and would also break
  incrementality. The text domain is additionally the *semantically* right identity: the parser
  and emitter consume decoded text, so the compiled artifact depends on exactly that form —
  re-saving a template with a BOM or as UTF-16 without changing a character does not change the
  compiled output and should not read as stale. And it is the back-compat-optimal choice: the
  generator side is byte-for-byte unchanged, so **every previously emitted manifest stays valid**
  (see Back-compat).
- **Encoding edge pinned.** A file that has no BOM and is not valid UTF-8 is outside the contract:
  Roslyn may decode it via the system ANSI code page while the runtime decodes UTF-8-with-
  replacement, so its staleness verdict is unspecified — the failure mode is the safe one (a
  `StaleContent` fallback to the byte-identical dynamic path), and the spec documents the edge
  rather than chasing Roslyn's fallback decoder.

### D2 — Key↔path derivation lives on `TemplateKey`; the case policy is two documented domains

- **Decision.** `TemplateKey` (already linked) gains the inverse pair:
  `TryMakeRelative(string path, string root, out string key)` — the generator's `Relative` logic
  (`\`→`/`, trailing-`/` trim, prefix test) followed by `TryNormalize`, returning `false` (no
  filename fallback) when the path is not under the root — and `ToPath(string key, string root)`
  — the gauntlet's reconstitution (`Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar))`).
  `HeddleTemplateGenerator.DeriveKey`/`Relative` and the two inline reconstitutions at
  `PrecompiledGauntlet.cs:167,175` are rewritten onto them.
- **Case policy, pinned.** The root-prefix test stays `OrdinalIgnoreCase` — it compares
  *filesystem paths*, where MSBuild routinely varies drive-letter and directory casing on the
  dominant (Windows) platform, and the current behavior must not change (a stricter test would
  silently re-key existing projects). Everything after the prefix — the key itself — preserves
  case exactly and compares `Ordinal`, per the `TemplateKey` doc contract
  (`src/Heddle/Precompiled/TemplateKey.cs:11-15`) and the registry
  (`PrecompiledTemplates.cs:45-48,101-102`). The XML docs on `TryMakeRelative` state the two
  domains explicitly so the mismatch the research flagged becomes a documented boundary instead of
  an accident.
- **Alternatives rejected.** `Ordinal` prefix test (silently changes which files derive rooted
  keys on case-varying Windows builds — a behavior change with no ratified window);
  `StringComparison` parameter (no second consumer; YAGNI).

### D3 — Out-of-root templates keep the filename fallback but stop being silent: `HED7018`

- **Decision.** When a template's path is not under `HeddleTemplateRoot` and no explicit `Key`
  metadata is set, the generator still derives the flattened filename key (unchanged behavior)
  but reports a new **warning `HED7018` "Heddle template outside the template root"** naming the
  file path, the effective root, and the flattened key it registered under — with the remedy in
  the message (set `HeddleTemplateRoot`, or give the item an explicit `Key`). Reported once per
  affected file, `Location.None` (a file/key-level condition, matching the existing convention in
  [precompilation.md](../precompilation.md#build-time-diagnostics)).
- **Rationale.** The silent directory-drop is the bug ([05 F3](../research/generator-code-sharing/05-pipeline-config.md);
  drift item 11 of the [synthesis](../research/generator-code-sharing/07-recommendations.md#confirmed-live-drift-fix-these-regardless-of-any-extraction)).
  Skipping such templates outright would be the cleaner rule but is a behavior change — any
  project relying on flat-key lookups of out-of-root templates would lose precompilation — so the
  warning is the additive fix; the skip becomes a candidate for a future window only if evidence
  shows the flattened keys are never used. Collided flattened keys still draw `HED7002`, now
  preceded by the `HED7018` warnings that explain them.
- **Registry.** `HED7018` is claimed from the `HED70xx` block in the
  [claimed-IDs registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  in the same change that ships it, with a row added to the
  [precompilation.md diagnostics table](../precompilation.md#build-time-diagnostics). (`HED7017`
  is the last claimed build-time ID; it is also currently missing from that docs table — WI8
  closes the gap alongside, which the new registry test then enforces.)

### D4 — One home for the `.heddle` extension rule

- **Decision.** `TemplateKey` gains `public const string TemplateExtension = ".heddle"` plus
  `HasTemplateExtension(string)` / `StripTemplateExtension(string)` with **`OrdinalIgnoreCase`**
  extension matching — the policy both existing case-sensitive-relevant sites already use
  (generator discovery `HeddleTemplateGenerator.cs:53`; `PrecompiledRuntime.StripHeddleExtension`
  `src/Heddle/Precompiled/PrecompiledRuntime.cs:309-315`). Converging sites: those two, the
  resolver's `FileExtension` const (`TemplateResolver.cs:16`), and `TryNormalizeCore`'s append
  step (`TemplateKey.cs:94-97`, which keeps its existing "no `.` in final segment" trigger —
  unchanged semantics, now expressed against the shared const). The MSBuild glob
  (`build/Heddle.Generator.targets:5`) is XML and cannot reference the const; it is annotated
  with a comment naming `TemplateKey.TemplateExtension` as its normative twin.

### D5 — `PrecompiledSchema.cs`: version constants and predicates, shared; no bump

- **Decision.** New linked `src/Heddle/Precompiled/PrecompiledSchema.cs`:
  `MinSupportedSchemaVersion = 1`, `MaxSupportedSchemaVersion = 2`,
  `CurrentSchemaVersion = 2` (what the generator emits), `IsSupported(int)`,
  `FormatEngineVersion(Version)` (the `major.minor.build` rule from
  `HeddleTemplateGenerator.cs:499`), and `IsEngineCompatible(Version manifest, Version runtime)`
  (the same-major + `<=` predicate from `PrecompiledTemplates.cs:196-202`).
  `PrecompiledTemplates` retires its private consts onto the shared ones; `EmitManifest`
  interpolates `PrecompiledSchema.CurrentSchemaVersion` instead of the `:380` literal;
  `ResolveEngineVersion` formats through `FormatEngineVersion`. A one-line invariant test pins
  `Min ≤ Current ≤ Max`, and the differential/registration tests prove the emitted attribute
  bytes are unchanged (`schemaVersion: 2`).
- **Rationale.** F5's all-or-nothing hazard — bumping the emitted schema without widening the
  runtime window rejects entire assemblies with only an `OnFallback` callback — becomes
  structurally impossible once both sides read one constant; the emission-side literal is the
  half nothing currently guards. `Version.TryParse`-based string parsing stays at the runtime
  call site (the attribute carries a string); the shared predicate takes parsed `Version`s so it
  is trivially testable on both sides.

### D6 — Engine-version resolution: observed reference, else generator self-version + `HED7019`

- **Decision.** `ResolveEngineVersion` keeps the referenced-`Heddle`-assembly scan as the primary
  source. The fabricated `"2.0.0"` literal fallback (`HeddleTemplateGenerator.cs:503`) is
  replaced by: (a) a new **warning `HED7019` "Heddle engine version could not be resolved"**
  (the `Heddle` assembly was not found among `ReferencedAssemblySymbols` — aliased, embedded, or
  ILMerged), and (b) the version of the executing generator assembly
  (`typeof(HeddleTemplateGenerator).Assembly`) formatted through
  `PrecompiledSchema.FormatEngineVersion` as the emitted value.
- **Rationale (fail vs diagnostic).** A hard build failure is wrong: compilations that alias or
  repackage the reference are legal, and precompilation is contractually additive — degrading
  must never break a build that dynamic rendering would serve. Silence is the current bug: a
  fabricated literal goes stale the release after it is written, and
  `IsEngineCompatible` then rejects (or wrongly accepts) whole assemblies invisibly. The
  generator's own version is the best available *observation* — `Heddle.Generator` versions in
  lockstep with the engine (`Heddle.Generator.csproj` `<Version>2.0.0…`, and the
  [2.0 record](../spec/records.md#the-20-breaking-window--as-shipped-record) shows engine and
  generator flipping together) — so today the emitted bytes are identical (`"2.0.0"`) and in
  every future release the fallback tracks reality instead of a fossil. The warning makes the
  heuristic visible at build time, where the runtime's `HED7102` callback today fires only for
  hosts that wired `OnFallback`. Severity warning, not error: the value is usually right, and the
  runtime gate remains the enforcement point. `HED7019` is claimed in the registry and the docs
  table in the same change (D3's rules).

### D7 — The identity-bearing option set becomes typed: link the enums and the fingerprint

- **Decision.** Four dependency-free runtime files are linked into the generator (one
  `<Compile Include>` line each, `Shared\…` link paths, matching `Heddle.Generator.csproj:49-63`):
  `Data/OutputProfile.cs`, `Data/ExpressionMode.cs`, `Precompiled/PrecompiledOptionsFingerprint.cs`,
  `Precompiled/PrecompiledCapabilities.cs`. Then:
  - `GlobalConfig` (`src/Heddle.Generator/Pipeline/GlobalConfig.cs`) holds `OutputProfile` and
    `ExpressionMode` as real enums (its hand-rolled string `Equals`/`GetHashCode` collapse to
    enum compares — still value-equatable for the incremental pipeline).
  - `ConfigReader` parses enums via `Enum.TryParse<T>(value, ignoreCase: true)` against the linked
    types, deleting the hand-copied allow-lists (`ConfigReader.cs:28,30`); the `HED7009`
    "expected" string is built from `Enum.GetNames` (today's `Text|Html` /
    `MemberPathsOnly|Native|FullCSharp` strings are reproduced verbatim — member order is
    declaration order).
  - `TemplateEmitter` deletes its string-typed profile/mode logic: `IsHtml`
    (`TemplateEmitter.cs:121`) and the mode compares (`:2015`, `:2070`) become enum compares;
    `Profile()`/`Mode()` (`:2570-2580`) — including the `default: return "Native"` bake-anything
    hazard — are replaced by a single fingerprint formatter that takes a **real**
    `PrecompiledOptionsFingerprint` constructed from `GlobalConfig` and emits each member via
    `nameof`/`Enum.ToString()`. Adding a fourth identity-bearing field to the struct then breaks
    the generator's build (missing ctor argument) instead of surfacing as runtime
    `OptionsMismatch` fallbacks; the arity test (Testing, WI3) pins formatter arity == ctor arity
    as the second guard. `CapabilitiesExpr` (`:2610-2616`) emits via
    `nameof(PrecompiledCapabilities.StringOutput)`/`Utf8Pieces`.
- **Consumers.** Phases 1 and 3 build on these links (typed `RenderType`/binding work); this
  phase owns landing them.

### D8 — One option names+defaults table: `HeddleBuildOptions`, with `ConfigReader` as a shim

- **Decision.** New linked `src/Heddle/Precompiled/HeddleBuildOptions.cs` (netstandard2.0,
  IO-free, Roslyn-free): the seven MSBuild property names as consts (`HeddleOutputProfile`, …)
  and the typed defaults stated once —
  `DefaultOutputProfile = OutputProfile.Html`, `DefaultExpressionMode = ExpressionMode.Native`,
  `DefaultTrimDirectiveLines = true`, `DefaultMaxRecursionCount = 100`,
  `DefaultEmitUtf8Pieces = false` — plus the parse helpers (enum via
  `Enum.TryParse(ignoreCase: true)`, bool, positive int) that return a parse-error signal rather
  than reporting (no diagnostics types in shared code). Consumers:
  - `ConfigReader.Read` becomes a thin adapter: one `Func<string,string>` lookup lambda over
    `AnalyzerConfigOptions.TryGetValue("build_property." + name)` feeding the shared helpers, and
    the translation of parse-error signals into `HED7009` `OptionError`s. `AnalyzerConfigOptions`
    never crosses into shared code.
  - Both `TemplateOptions` constructors (`src/Heddle/Data/TemplateOptions.cs:120-141` — which
    today duplicate the defaults *between themselves* as well) initialize from the
    `HeddleBuildOptions.Default*` consts.
  - **The props defaults stay** (`build/Heddle.Generator.props:22-29`). Two reasons the research's
    "remove the props defaults" lean does not survive verification: `HeddleTemplateRoot`'s
    default is `$(MSBuildProjectDirectory)` — expressible *only* in MSBuild (ConfigReader's blank
    fallback is `""`, which is precisely the F3 filename-flattening path — removing the props
    default would flatten every project's keys); and the properties are consumer-visible after
    import (a project's own targets may read `$(HeddleOutputProfile)`). The XML therefore remains
    a second physical statement of five scalar defaults — guarded by the **defaults lockstep
    test** (Testing): parse `Heddle.Generator.props` from the repo, assert each literal equals
    the corresponding `HeddleBuildOptions` value, and assert a default-constructed
    `TemplateOptions` matches the same table.
- **Explicitly: no default changes.** This decision moves *definitions*, not values. Changing any
  default is a breaking-window item per the
  [records.md precedent](../spec/records.md#the-20-breaking-window--as-shipped-record) (2.0 item 5)
  and is out of scope; the lockstep test is the proof the collapse changed nothing.

### D9 — Manifest emission: one entry builder, names from types where a type exists

- **Decision.** `BuildManifestEntry` and `BuildMarkerManifestEntry`
  (`TemplateEmitter.cs:2520-2568` — thirteen named arguments duplicated verbatim, differing in
  five lines) merge into one builder taking a marker flag; the marker branch supplies
  `entryPointType: null` / empty bindings / `PrecompiledCapabilities.None` / `strategy: null`
  through the same argument sequence. With D7's links, the fingerprint/capabilities member and
  enum-member names come from `nameof`; type names in emitted source come from
  `typeof(...).FullName` of the linked types. The `PrecompiledTemplateInfo` ctor argument names
  (`key:`, `entryPointType:`, …) and the attribute/interface names in `EmitManifest`
  (`HeddleTemplateGenerator.cs:372-410`) cannot use `nameof` — `PrecompiledTemplateInfo` pulls
  `IProcessStrategy` and is not linkable — so they remain string constants gathered into one
  place adjacent to the builder, guarded by the existing integration suite
  (`src/Heddle.Generator.IntegrationTests` compiles every emitted manifest against the real
  `Heddle` reference — a renamed ctor parameter is a red build there, which is the research's
  "cheap alternative guard" made mandatory).

### D10 — Build-root vs runtime-root agreement stays a host responsibility

- **Decision.** No new validation is added for F3's third point (build-time root
  `$(MSBuildProjectDirectory)` vs runtime `TemplateOptions.RootPath` defaulting to
  `AppContext.BaseDirectory`, `TemplateOptions.cs:123`). The two roots are unknowable to each
  other — the build cannot see the deployment layout, and an unconditional runtime probe would
  add per-request IO to a hot path for a misconfiguration case. The existing guard is already
  correct-by-degradation: under `EnableFileChangeCheck` the gauntlet's `File.Exists`/hash check
  (`PrecompiledGauntlet.cs:164-183`) turns a root mismatch into a `StaleContent` fallback with an
  `OnFallback` event. The spec adds one paragraph to
  [precompilation.md](../precompilation.md) documenting the pairing rule (template files deployed
  under `RootPath` must mirror their build-time root-relative layout) next to the staleness
  section — a docs deliverable, not a mechanism.

### Work items (ordered)

| # | Work item | Ships | Depends on |
| --- | --- | --- | --- |
| WI1 | **Content hash** — add `Precompiled/ContentHash.cs` + csproj link; generator `ComputeContentHash` → `ContentHash.HashText`; gauntlet `HashFile` → decode-then-hash; delete dead `HashBytes`/`ToHex`; fix `ContentHash` XML doc; content-hash lockstep + unit tests (BOM'd UTF-8, UTF-16 LE/BE, plain UTF-8 fixtures) | F1 fix (D1) | — |
| WI2 | **Key derivation** — `TemplateKey.TryMakeRelative`/`ToPath` (+ tests: round-trip identity, out-of-root false, case-domain vectors); rewrite `DeriveKey`/`Relative` and the gauntlet reconstitutions; add `HED7018` (descriptor, registry row, docs row, diagnostic test) | F3 fix (D2, D3) | — |
| WI3 | **Typed options** — link the four D7 files; retype `GlobalConfig`; `Enum.TryParse` in `ConfigReader`; single fingerprint formatter + capabilities `nameof` in `TemplateEmitter`; fingerprint arity test | F2 (D7) | — |
| WI4 | **Versioning** — `Precompiled/PrecompiledSchema.cs` + link; consts retired on both sides; `HED7019` + self-version fallback; invariant + emitted-bytes tests | F5 fix (D5, D6) | — |
| WI5 | **Options table** — `Precompiled/HeddleBuildOptions.cs` + link; `ConfigReader` shim; `TemplateOptions` ctor de-duplication; props↔code↔runtime defaults lockstep test | F6 (D8) | WI3 (enums linked) |
| WI6 | **Manifest builder** — merge the twin builders; constant consolidation; assert the existing integration compile gate covers a manifest with ≥1 marker entry | F7 (D9) | WI3 |
| WI7 | **Extension rule + resolver folding** — `TemplateExtension`/`Has`/`Strip` on `TemplateKey`; converge the four C# sites; re-express `TemplateResolver.Search`'s munging (`:160-166`) on the shared helpers, byte-identical (existing resolver tests + goldens are the gate) | F4 (D4) | — |
| WI8 | **Item metadata + docs + registry test** — implement the OQ1 ruling (wire `Precompile`, drop `Name` — or as ruled); precompilation.md updates (metadata section, diagnostics rows `HED7017`–`HED7019`, D10 root paragraph); HED7xxx registry test (code ↔ docs table ↔ registry) | F8 (OQ1) | OQ1 ruled |

WI1–WI4 are the fix-first group — independently shippable, no cross-dependencies, matching the
[synthesis sequencing](../research/generator-code-sharing/07-recommendations.md#recommended-sequencing)
("bug fixes first"). WI3 is the unblock point for phases 1 and 3.

## Dependencies & ordering

- **Depends on:** no other phase. All shared files land in `src/Heddle/Precompiled/` with
  individual `<Compile Include>` links — the folder's established pattern (`TemplateKey.cs`,
  `DefaultFunctionTable.cs`); nothing waits on the `Language/**` glob or any other phase's
  extraction.
- **Owns for others:** `Precompiled/ContentHash.cs` (phase 6 references it for its co-report of
  the hash duplication, [06 F1](../research/generator-code-sharing/06-diagnostics-utilities.md));
  the `OutputProfile`/`ExpressionMode`/`PrecompiledOptionsFingerprint`/`PrecompiledCapabilities`
  links (phases 1 and 3 consume the typed enums for `RenderType`/emitter and binding work).
  Per [D5 of the cross-cutting decisions](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order),
  those phases may assume this phase's artifacts once the initiative's declared order places
  phase 5 ahead of them; if the initiative sequences differently, WI3 is extractable as a
  standalone first item.
- **Internal ordering:** WI1–WI4 in any order (parallelizable); then WI5/WI6 (need WI3); WI7 any
  time; WI8 last (needs the OQ1 ruling and collects the docs/registry deliverables).
- **Unblocks:** phases 1, 3 (links), phase 6 (ContentHash reference, and the forwarded-diagnostic
  work that shares the `HED7018`/`HED7019` registry conventions).

## Back-compat / impact

- **Content-hash canonicalization (D1) — the one behavior delta, analyzed.** The generator side
  is unchanged, so every previously emitted manifest hash remains exactly what a rebuilt manifest
  would contain — **no rebuild is required and no staleness check is invalidated** for the
  overwhelming case (UTF-8, no BOM), where decoded-text and raw-byte hashes coincide. For BOM'd /
  UTF-16 / re-encoded templates, the runtime comparison changes verdict from "always stale" to
  "fresh when content matches": templates that today silently fall back on every request under
  `EnableFileChangeCheck` start being served precompiled. Rendered bytes are identical either way
  (the precompiled/dynamic parity contract), so the observable effect is the removal of dynamic
  compile cost and of a per-request `OnFallback` event stream — a defect fix toward the
  documented staleness contract ([precompilation.md](../precompilation.md#the-validation-gauntlet-and-mismatch-policy)),
  not window material. Real edits are still detected: any character change changes the decoded
  text. Had the opposite canonical form (raw bytes) been chosen, every shipped BOM'd-template
  manifest would stay permanently stale *and* the generator would need banned file IO — D1's
  choice is the only one with an empty migration.
- **Two new warnings** (`HED7018`, `HED7019`): additive diagnostics, claimed per
  [D1 registry rules](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx);
  builds that were clean stay clean unless they have the (currently silent) defect the warning
  names. No new errors.
- **Engine-version fallback string** (D6): byte-identical today (`"2.0.0"` from a 2.0.0
  generator); future generators emit their own version instead of a stale literal — strictly
  more correct against the runtime gate.
- **Defaults and option names: unchanged by construction** (D8) — the lockstep test is the
  regression proof, and any *future* default change remains window-governed per
  [breaking-windows.md](../spec/common/breaking-windows.md) with the
  [2.0 record item 5](../spec/records.md#the-20-breaking-window--as-shipped-record) as the
  procedural precedent (generator and engine defaults move together, in a window, never here).
- **Schema/emitted manifests:** `schemaVersion: 2` bytes unchanged (D5 invariant + emitted-bytes
  test); fingerprint/capabilities argument *values* unchanged, argument *text* unchanged
  (`Enum.ToString()` reproduces the same member names the switch emits today); `HED7009` message
  strings reproduced verbatim (D7).
- **`TemplateResolver.Search` folding (WI7):** byte-identical refactor; the host-thrown
  `ArgumentException` for `..` and all search-location semantics are preserved (side-specific
  reactions stay side-specific; only the rule expression is shared).
- **`Precompile`/`Name` metadata (OQ1):** wiring `Precompile` is additive (absent/empty metadata
  ⇒ today's behavior); dropping `Name` removes something that has never had an effect — either
  ruling has an empty migration, which is why it can sit in an Open question without blocking.

## Risks & mitigations

| Risk | Mitigation | Size (S/M/L) |
| --- | --- | --- |
| The decode-then-hash change lets a template with an *encoding-only* edit (BOM added, UTF-16 re-save) be served precompiled where it previously recompiled dynamically — masking a hypothetical decoder divergence | Parity contract: both paths consume decoded text and render byte-identically; the lockstep test's BOM'd/UTF-16 fixtures pin generator hash == runtime hash == expected, and the non-UTF-8-no-BOM edge is documented as unspecified-but-safe (D1) | S |
| Roslyn's `SourceText` decoding differs from the runtime's `StreamReader` decoding for some encoding, making the lockstep test green locally but hashes diverge on exotic files | The lockstep test runs the *real* generator (via the existing `Heddle.Generator.IntegrationTests` driver) against on-disk fixture bytes, not a re-implementation; fixtures cover BOM'd UTF-8, UTF-16 LE/BE-with-BOM, plain UTF-8; anything outside that set is outside the pinned contract and degrades safely | M |
| Retyping `GlobalConfig` to enums perturbs incremental-pipeline equality (cache invalidation storms or stale reuse) | Enums are value-equatable primitives — strictly stronger than the current `Ordinal` string compares; the existing generator test suite plus one added equality test over `GlobalConfig` pairs covers it | S |
| `Enum.GetNames`-built `HED7009` "expected" text drifts from the documented `Text\|Html` form if enum member order changes | Member order is part of the pinned enum surface (linked file, one source of truth); the diagnostic test asserts the exact expected-string | S |
| `HED7018` fires noisily in projects that deliberately keep templates outside the root (metadata-keyed layouts) | Explicit `Key` metadata suppresses it by design (the fallback path is only reached with no `Key`); the message names both remedies; warnings are suppressible by ID via standard MSBuild `NoWarn` | S |
| Merging the marker/normal manifest builders subtly changes marker-entry text | The integration suite's registration tests already exercise marker entries (HED7014 corpus); WI6 adds the explicit "manifest with ≥1 marker entry compiles and registers" assertion; diff-review of emitted text for one marker fixture is part of the PR | S |
| The props↔code defaults test reads MSBuild XML with a hand parser and goes stale if props structure changes | The test asserts presence *and* value of each of the six defaulted properties by name; a structural change that breaks the parse is a red test, which is the desired failure mode | S |
| Phases 1/3 start before WI3 lands and re-link or re-model the enums divergently | Dependencies & ordering names WI3 as the extractable first item; the initiative's declared order (cross-cutting D5) makes the links this phase's deliverable — other phases reference, never duplicate | S |

## Success criteria

- [ ] `ContentHash.HashText` is the only SHA-256/hex implementation on either side:
      `ComputeContentHash`, `PrecompiledGauntlet.HashBytes`, and the gauntlet's private `ToHex`
      are gone; `HashFile` decodes before hashing.
- [ ] The content-hash lockstep test proves, for every fixture in {plain UTF-8, UTF-8+BOM,
      UTF-16 LE+BOM, UTF-16 BE+BOM}: generator-emitted manifest hash == runtime
      `HashFile` == `ContentHash.HashText(decoded text)` — and a one-character content edit
      changes all three.
- [ ] `TemplateKey.ToPath(root, key)` ∘ `TryMakeRelative(path, root)` is identity for every
      in-root round-trip vector; `TryMakeRelative` returns `false` (never a flattened key) for
      out-of-root paths; the generator reports `HED7018` exactly once per out-of-root,
      non-`Key`-annotated template and still registers the flattened key.
- [ ] `HED7018` and `HED7019` exist in `GeneratorDiagnostics`, the
      [claimed-IDs registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry),
      and the [precompilation.md](../precompilation.md#build-time-diagnostics) table — and the new
      HED7xxx registry test fails if any of the three disagrees (including the pre-existing
      `HED7017` docs-table gap, closed in WI8).
- [ ] The generator contains no string literal for any `OutputProfile`/`ExpressionMode`/
      `PrecompiledCapabilities` member or option allow-list: `GlobalConfig` carries enums,
      `ConfigReader` parses via `Enum.TryParse` against linked types, the emitter formats one
      constructed `PrecompiledOptionsFingerprint`, and the arity test pins formatter arity ==
      fingerprint ctor arity.
- [ ] `PrecompiledSchema` is the single home of Min/Max/Current schema versions and the
      engine-version format/compat predicates; the invariant test pins `Min ≤ Current ≤ Max`;
      the emitted attribute for a reference fixture is byte-identical to pre-change output.
- [ ] A compilation with no visible `Heddle` reference emits the generator's own
      `major.minor.build` as `engineVersion` and reports `HED7019`; with a normal reference,
      output is unchanged and no `HED7019` appears.
- [ ] The defaults lockstep test passes: `Heddle.Generator.props` literals ==
      `HeddleBuildOptions` values == default-constructed `TemplateOptions` values, for every
      option in the table — and no default value differs from the pre-change tree.
- [ ] One manifest-entry builder serves normal and marker entries; the integration suite compiles
      and registers a manifest containing both kinds against the real `Heddle` reference.
- [ ] The full regression gate ([testing-standards](../spec/common/testing-standards.md#regression-gates))
      is green in one combined run: all TFMs, goldens byte-identical, no grammar diff, and the
      precompiled==dynamic differential corpus unchanged.
- [ ] Both Open questions carry a maintainer ruling recorded in the implementing spec before WI8
      completes (the plan's recommendations are the provisional defaults).

## Validation scenarios

| Input | Expected outcome |
| --- | --- |
| A `.heddle` template saved as UTF-8 **with BOM**, precompiled, rendered under `EnableFileChangeCheck` | Gauntlet staleness passes; template renders precompiled; zero `OnFallback` events (today: `StaleContent` on every request) |
| The same template, then one character edited on disk without rebuild | `StaleContent` fallback with `HED7101` event — edits are still detected in the text domain |
| A template at `../shared/banner.heddle` (outside `HeddleTemplateRoot`) with no `Key` metadata | Build succeeds with one `HED7018` warning naming path, root, and the flattened key; the key registers as before |
| Two out-of-root templates both named `index.heddle` | Two `HED7018` warnings followed by the existing `HED7002` duplicate-key report — no longer inexplicable |
| `<HeddleOutputProfile>WebForms</HeddleOutputProfile>` | `HED7009` with expected-values text `Text\|Html`, verbatim as today (now sourced from `Enum.GetNames`) |
| A consuming project that aliases its `Heddle` `PackageReference` | Manifest emits the generator's own version as `engineVersion`; one `HED7019` warning; runtime registration proceeds under the normal gate |
| A fourth field added to `PrecompiledOptionsFingerprint` (simulated in a branch) | The generator fails to compile (fingerprint construction) and/or the arity test fails — never a green build that emits a 3-arg fingerprint |
| `PrecompiledSchema.CurrentSchemaVersion` edited to 3 without touching Max (simulated) | The `Min ≤ Current ≤ Max` invariant test fails |
| A default flipped in `Heddle.Generator.props` only (simulated) | The props↔code↔runtime defaults lockstep test fails naming the property |
| The resolver serving the existing golden corpus after WI7 | Byte-identical output; all resolver/search tests green with no golden change |

## Open questions

Both are user-facing surface choices; each carries a recommendation as the provisional default
per [spec-conventions](../spec/common/spec-conventions.md#no-open-questions), to be closed as a
decision record in the implementing spec.

- **OQ1 — `Precompile`/`Name` item metadata ([05 F8](../research/generator-code-sharing/05-pipeline-config.md)): wire or remove?**
  *Recommendation: wire `Precompile`, remove `Name`.* `Precompile="false"` expresses something
  `Remove` cannot: verification shows the generator's `@<<` import map is built from
  `AdditionalFiles` (`HeddleTemplateGenerator.cs:132-138`), so `Remove`-ing an import-only layout
  file — the workaround [precompilation.md](../precompilation.md#setup) currently teaches — takes
  it out of the import map and turns every `@<<` referencing it into `HED7011`. Wiring
  `Precompile="false"` as "stays in the import map, emits no entry point and no manifest entry"
  closes that gap additively (absent/empty metadata ⇒ unchanged behavior) and makes the already
  declared, already shipped metadata truthful. `Name`, by contrast, has no defined meaning `Key`
  doesn't already cover (the docs state `Key` sets both lookup key and class name) and `Name` is
  also a reserved-feeling MSBuild metadata name — remove its `CompilerVisibleItemMetadata` and
  targets lines rather than inventing semantics. If the maintainer prefers strict minimalism,
  removing *both* is defensible (docs already say only `Key` is read) — but then the import-only
  exclusion gap should be recorded as a known limitation.
- **OQ2 — Should the `View`/`PartialView`/`Master` resolver arms consult the precompiled
  registry ([05 F4 note](../research/generator-code-sharing/05-pipeline-config.md))?**
  Today only the `TemplatePathType.None` arm consults it (`TemplateResolver.cs:77`); MVC-style
  hosted lookups always parse and compile dynamically, regardless of key agreement — documented
  behavior ([precompilation.md](../precompilation.md#the-registry--for-dynamic-call-sites):
  "hosted view/partial-view search paths stay fully dynamic"). *Recommendation: not in this
  phase, yes as a later additive item.* Doing it right requires mapping each search-location
  candidate (`\views\{controller}\{view}` …) to a root-relative key and consulting in search
  order before the file probe — a design with its own precedence questions (registry hit vs
  earlier-location file on disk) that deserves its own decision record; bolting it on here would
  couple a behavior change to a de-duplication phase. The registry-consult seam
  (`ConsultPrecompiled`) and the WI2 `TryMakeRelative` helper are the enablers this phase leaves
  in place; the revisit trigger is a host asking for zero-compile MVC view serving (the
  precompilation pitch applied to hosted lookups).

## External grounding

| Claim | Source |
| --- | --- |
| Generator hashes decoded text (`ComputeContentHash`), runtime hashes raw stream (`HashFile`); `HashBytes` dead | `src/Heddle.Generator/HeddleTemplateGenerator.cs:412-422` (+ feed at `:64-72,210`); `src/Heddle/Precompiled/PrecompiledGauntlet.cs:186-205`; repo-wide grep: zero `HashBytes` callers — all re-verified for this plan |
| `ContentHash` doc claims raw bytes — contradicted by generator emission for BOM'd files | `src/Heddle/Precompiled/PrecompiledTemplateInfo.cs:56`; [05 F1](../research/generator-code-sharing/05-pipeline-config.md) |
| Analyzers/generators must not do file IO (RS1035 banned APIs under `EnforceExtendedAnalyzerRules`); `AdditionalText` exposes only decoded `SourceText` | `src/Heddle.Generator/Heddle.Generator.csproj:12` (`EnforceExtendedAnalyzerRules`); [Roslyn banned-API rule RS1035](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/rs1035); [Microsoft.CodeAnalysis.AdditionalText docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.additionaltext) |
| `DeriveKey`/`Relative` filename fallback + `OrdinalIgnoreCase` prefix; runtime inverse; `Ordinal` keys | `src/Heddle.Generator/HeddleTemplateGenerator.cs:473-490`; `src/Heddle/Precompiled/PrecompiledGauntlet.cs:164-183`; `src/Heddle/Precompiled/TemplateKey.cs:11-15`; `src/Heddle/Precompiled/PrecompiledTemplates.cs:44-48,101-102` |
| Schema literal vs runtime consts; engine-compat predicate; fabricated `"2.0.0"` fallback | `src/Heddle.Generator/HeddleTemplateGenerator.cs:380,492-504`; `src/Heddle/Precompiled/PrecompiledTemplates.cs:20-21,74-90,196-202` |
| Options triple restated as strings; `Mode()` default-Native hazard; string-compare sites | `src/Heddle.Generator/Pipeline/GlobalConfig.cs:10-58`; `src/Heddle.Generator/Emit/TemplateEmitter.cs:121,2015,2070,2570-2580` |
| Defaults triplicated (props / ConfigReader / both `TemplateOptions` ctors); `HeddleTemplateRoot` default only expressible in MSBuild | `src/Heddle.Generator/build/Heddle.Generator.props:22-29`; `src/Heddle.Generator/Pipeline/ConfigReader.cs:27-37`; `src/Heddle/Data/TemplateOptions.cs:120-141` |
| `Precompile`/`Name` declared, never read; only `Key` consumed; `Remove` breaks the import map for import-only files | `build/Heddle.Generator.props:16-19`; `build/Heddle.Generator.targets:10-15`; `HeddleTemplateGenerator.cs:58,132-138`; [precompilation.md](../precompilation.md#setup) |
| Manifest builders duplicated verbatim (13 args, five differing lines) | `src/Heddle.Generator/Emit/TemplateEmitter.cs:2520-2568`; `src/Heddle/Precompiled/PrecompiledTemplateInfo.cs:20-31` |
| `.heddle` rule in five sites; resolver `Search` re-implements `TemplateKey` rules | `HeddleTemplateGenerator.cs:53`; `build/Heddle.Generator.targets:5`; `TemplateKey.cs:94-97`; `src/Heddle/Runtime/TemplateResolver.cs:16,155-166`; `src/Heddle/Precompiled/PrecompiledRuntime.cs:309-315` |
| Linked-`<Compile>` precedent incl. `Precompiled/` files; lockstep-test precedent | `src/Heddle.Generator/Heddle.Generator.csproj:49-63`; `src/Heddle.Tests/DefaultFunctionLockstepTests.cs` |
| MSBuild default changes are window-governed; 2.0 window item 5 flipped generator defaults with the engine | [breaking-windows.md](../spec/common/breaking-windows.md); [records.md — 2.0 as-shipped record](../spec/records.md#the-20-breaking-window--as-shipped-record) |
| `HED7017` last-claimed build-time ID; registry rules; `HED7017` absent from the precompilation.md diagnostics table | [cross-cutting-decisions.md — registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry); `src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs`; [precompilation.md](../precompilation.md#build-time-diagnostics) (table ends at `HED7016` — verified) |
| Research findings and cross-area synthesis this plan resolves | [05-pipeline-config.md](../research/generator-code-sharing/05-pipeline-config.md) (F1–F8); [07-recommendations.md](../research/generator-code-sharing/07-recommendations.md) (drift items 2, 11, 15; Tier 1/2 layout; sequencing) |

Verification corrections recorded while grounding this plan (per
[spec-conventions](../spec/common/spec-conventions.md#relationship-to-the-owning-plan), carried
here as evidence, to be restated in the implementing spec's assumed-state): the research's F2
restatement #2 overstates `TemplateOptions.Equals` — it does **not** compare `ExpressionMode`
(`TemplateOptions.cs:164-167`), and neither does the resolver `CacheKey` (`TemplateResolver.cs:58-59`);
noted in Non-goals as an adjacent finding this phase does not own. The research's F6 lean to
"remove the props defaults" is rejected with evidence in D8 (`HeddleTemplateRoot`'s default is
MSBuild-only). Line references `props:23-28` (not `:23-31`) and `HashFile` at
`PrecompiledGauntlet.cs:186-191` (not `:185-190`) drifted by 1–3 lines from the research's
citations; all substantive claims held.
