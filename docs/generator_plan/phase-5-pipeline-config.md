# Phase 5 — pipeline & configuration (generator ↔ runtime contract surfaces)

## Header

- **Status:** implemented (2026-07-25) — WI1–WI10 landed; see [Implementation record](#implementation-record).
  Both open questions were resolved (user, 2026-07-25) and folded in as committed scope (D11, D12,
  WI8–WI10) — see the [open-questions register](open-questions.md#phase-5--pipeline-config).
  All three diagnostics ship as planned: `HED7018`, `HED7019` and `HED7020` are claimed to this
  phase in the [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  (registry order is claim order, and this phase reached spec first).
- **Goal (one line):** One canonical definition each for the five pipeline-level contracts the
  generator and runtime currently restate by hand — content hash, key↔path derivation, the
  `.heddle` extension rule, schema/engine versioning, and the option names/defaults table — with
  the four live bugs in that surface fixed first ([05 F1](../research/generator-code-sharing/05-pipeline-config.md),
  F3, F5, F8) and the identity-bearing option types linked into the generator so the manifest
  fingerprint is constructed, not transcribed; plus the two ruled behavior items this phase now
  owns — precompiled-registry consultation in every resolver arm (D11) and the
  fallback-legitimacy overhaul of the generator's blanket catch and the gauntlet's degrade
  policy (D12).
- **Depends on:** nothing in this initiative — the fix-first group and every shared artifact here
  are independently shippable. Phase 5 *unblocks* other phases: it owns
  `Precompiled/ContentHash.cs` (co-reported by [06 F1](../research/generator-code-sharing/06-diagnostics-utilities.md);
  phase 6 references it), and it owns the `<Compile>` links for `OutputProfile`/`ExpressionMode`/
  `PrecompiledOptionsFingerprint`/`PrecompiledCapabilities` that phases 1 and 3 consume. Phase 5
  additionally **feeds phases 1–3 the fallback-legitimacy taxonomy** (D12): phases 1/2 supply
  the intentional-refusal taxonomy it consumes, and phase 3 coordinates the binding-mismatch
  classes; WI10's generation-time half is sequenced after phase 2's WI1 clamp fix (D12a).
- **Changes an externally-visible contract:** no rendered byte, no option default, no schema
  version, and no public runtime API shape changes ship directly from this phase. Five
  externally-*observable* deltas: (1) the runtime staleness check starts hashing the decoded
  template text instead of raw file bytes, so BOM'd/UTF-16 templates that today permanently fail
  `StaleContent` start being served precompiled (D1 — a defect fix toward the documented
  behavior, analyzed under Back-compat); (2) three new build diagnostics — warnings `HED7018`,
  `HED7019` and error `HED7020` —
  claimed per the [registry rules](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry);
  (3) the engine-version fallback string in manifests built without a visible `Heddle` reference
  changes from the literal `"2.0.0"` to the generator's own assembly version (identical today,
  correct in the future — D6); (4) the `View`/`PartialView`/`Master` resolver arms start
  consulting the precompiled registry, so hosted lookups that key- and options-match a manifest
  are served precompiled instead of always compiling dynamically (D11 — byte-identical output
  under the parity contract; the documented "stays fully dynamic" sentence is updated); (5) the
  **default** gauntlet/registration behavior for the must-surface fallback classes changes from
  silent degrade to a surfaced error — behavioral and window-governed, so it is *proposed* here
  and routed through the [next-window candidate register](../spec/common/breaking-windows.md#next-window-candidate-register),
  not shipped directly (D12b).

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

The 2026-07-25 rulings added two committed behavior items on top of the de-duplication scope:
registry consultation in every resolver arm (Q5.2 → D11/WI9), and — because this phase owns both
the generator's blanket `catch (Exception)` site and the runtime's gauntlet-policy machinery —
the cross-phase **fallback-legitimacy** work ruled under Q2.2 (→ D12/WI10): replace
catch-and-degrade with a small researched legitimate-fallback set, and make everything else
surface as an error.

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
- **The must-surface default flip does not ship outside a window.** D12b's change to the
  *default* fallback behavior (surfaced error instead of silent degrade for the must-surface
  classes) is behavioral and window-governed per
  [breaking-windows.md](../spec/common/breaking-windows.md); this phase ships the taxonomy, the
  generation-time fix, and the candidate-register entry with the proposed mechanism — the
  default flip itself lands inside a ratified window. (The former non-goal excluding the
  `View`/`PartialView`/`Master` arms is superseded by the Q5.2 ruling — that work is now
  committed scope, D11/WI9; `TemplateResolver.Search`'s folding onto `TemplateKey` in WI7
  remains a byte-identical refactor that WI9 then builds on.)
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
choice is recorded below as a decision with rationale. The two truly user-facing choices that
originally went to Open questions are now ruled (user, 2026-07-25) and folded in as D-items
(OQ1 → WI8; OQ2 → D11), alongside the cross-phase fallback-legitimacy responsibility assigned
by Q2.2's ruling (D12).

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

### D11 — Registry consultation in every resolver arm (Q5.2 ruling)

- **Decision.** The registry-first posture of the `TemplatePathType.None` arm
  (`ConsultPrecompiled`, `src/Heddle/Runtime/TemplateResolver.cs:73-80,214-234`) is extended to
  `View`/`PartialView`/`Master`. The seam is the private `Search` probe ladder
  (`TemplateResolver.cs:182-208`), which all three arms share (Master is reached only through the
  public `Search`): a **registry sweep** over the arm's search locations, in location order, is
  added ahead of the existing cache sweep and disk sweep — a three-tier ladder mirroring today's
  two-tier cache-then-disk shape. A registry hit runs the normal gauntlet against the arm's
  *real* effective options (the same `TemplateOptions` the arm would hand to `Create`, including
  the MVC arms' `ExpressionMode.FullCSharp`, `TemplateResolver.cs:118,132`) and, on a pass,
  returns the precompiled-adapter `HeddleTemplate` through the existing `out cached` parameter —
  so `View`/`PartialView` return it from `GetTemplate` and `Master` callers receive it from
  `Search`, with no public-signature change. A miss or gauntlet failure falls through to the
  unchanged cache/disk ladder, exactly as the `None` arm behaves today.
- **Key mapping, pinned (this is where the "easy fix" can go wrong).** The consult runs *after*
  `Search`'s existing munging (extension append, `..` rejection, `~/` fold, `/`→`\`,
  `TemplateResolver.cs:160-166`). For each location pattern, the candidate **root-relative** path
  is `string.Format(pattern, viewName, controllerName)` — the same string today combined with
  `_rootPath` for the cache/disk probes — and it maps to a registry key by trimming the leading
  separator and normalizing via the shared `TemplateKey` rules (`\`→`/` + `TryNormalize`;
  equivalently `TemplateKey.TryMakeRelative(Path.Combine(_rootPath, rel), _rootPath, out key)` —
  the WI2 helper, which is why WI9 depends on it). No second key grammar is introduced: build
  side and resolver side derive keys through the same shared code, which is the whole point of
  this phase.
- **Precedence, pinned.** Tier order beats location order: registry sweep, then cache sweep,
  then disk sweep, each in location order. A registry hit at a later location therefore wins
  over an earlier location's on-disk file — the precedence question OQ2 flagged — because that
  is already the resolver's shape today: a *cached* template at location 2 beats a location-1
  disk file (the two-loop ladder at `:187-204`), and the `None` arm already places the registry
  above both (`:73-85`). Option agreement needs no special-casing: the gauntlet's fingerprint
  step refuses a `Native`-built manifest for a `FullCSharp` request by construction
  (`PrecompiledGauntlet.CheckOptions`).
- **Docs.** The [precompilation.md](../precompilation.md#the-registry--for-dynamic-call-sites)
  sentence "hosted view/partial-view search paths stay fully dynamic" is rewritten to describe
  the consult (a documented-behavior update carried in WI9; behavior analysis under
  Back-compat). The user's expectation that this is a small fix is noted — the mechanism is
  small; the key-mapping and precedence rules above are made explicit precisely because they are
  the parts a small fix would get subtly wrong.

### D12 — Fallback legitimacy: surface defects, degrade only for the researched set (Q2.2 ruling)

The ruling (the register's
[fallback-legitimacy principle](open-questions.md#phase-5--pipeline-config)): catch-and-degrade
is legitimate only for a small, researched set of conditions; everything else is an error that
must surface. Phase 5 owns both halves — the generation-time catch site and the runtime
gauntlet-policy machinery.

**D12a — Generation-time: the blanket catch is removed; emitter defects become `HED7020` errors.**

- **Verified current state.** `HeddleTemplateGenerator.cs:249-252` wraps the whole per-template
  emit in `catch (Exception) { /* degrade to the dynamic path */ }`. Every *intentional* degrade
  already flows through refusal **return** paths, not exceptions: `result.IsMarker` (fallback-
  marker entry + `HED7014`, `:231-245`) and `result.UnsupportedReason` (no entry, no source,
  `:246-247`). Phase 2 makes the same observation from the shaping side ("after the WI1 clamp
  fix, any exception out of the shaping code is a defect") — so once phase 2's WI1 clamp fix
  lands, an exception escaping the emitter has no legitimate meaning: it is a defect and must
  surface.
- **Surfacing mechanism, verified.** With no catch at all, the Roslyn driver wraps a generator
  exception and the compiler reports **`CS8785` "Generator failed to generate source" — a
  *warning*, and the generator's entire contribution is discarded** (every template's source
  *and* the manifest). That fails the ruling twice: warning severity does not reliably surface
  (clean-build gates pass), and one defective template silently un-precompiles every other one.
- **Decision.** Catch per-template, report a new **error `HED7020` "Heddle template emitter
  failed"** naming the template path and the exception type/message (at the template's location
  where a position is recoverable, else `Location.None`), emit nothing for that template, and
  continue the pass — the error reds the build (surfacing guaranteed, unlike `CS8785`), while
  the remaining templates and the manifest still emit, which keeps IDE/incremental behavior
  sane. Rejected alternatives: *bare pass-through* (downgrades to the `CS8785` warning and
  cancels the whole pass, per above); *report-then-rethrow* (reds the build twice and still
  cancels the pass — the rethrow adds nothing the error diagnostic doesn't already guarantee).
  `HED7020` is claimed from the registry and added to the docs table per D3's rules.

**D12b — Runtime: the `PrecompiledFallbackReason` taxonomy and the default-policy change.**

Every reason (`src/Heddle/Precompiled/PrecompiledFallbackReason.cs`), classified against the
gauntlet/registration sites (`PrecompiledGauntlet.cs:21-56`, `PrecompiledTemplates.cs:63-119,162-181`),
with the argument each classification rests on:

| Reason (site) | Class | Argument |
| --- | --- | --- |
| `StaleContent` (gauntlet step 4) | **Legitimate fallback** | The canonical case the ruling names: the on-disk template changed after build — stale cached data; dynamic recompile is the *correct* semantics, and `EnableFileChangeCheck` exists to request exactly this tracking |
| `StaleImport` (gauntlet step 4) | **Legitimate fallback** | Same argument, one hop out: an import changed under an unchanged root template — genuine change tracking |
| `UnsupportedFunction` (gauntlet step 0, marker entry) | **Legitimate fallback** | Not a runtime discovery at all — the *build* refused intentionally (delegate-only function, warned `HED7014`) and recorded the marker; the phases-1/2 intentional-refusal taxonomy is the upstream authority that keeps this class closed |
| `OptionsMismatch` (gauntlet step 1) | **Legitimate fallback** (by-request divergence) | Options are per-request degrees of freedom the host legitimately exercises — the same template served `Text` for mail and `Html` precompiled is a designed miss of the fingerprinted point, not a defect; the mechanism cannot distinguish a deliberate off-fingerprint request from a misconfigured one, so the class stays legitimate with the `OnFallback` event as the visibility channel |
| `ExtensionBindingMismatch` (gauntlet step 2) | **Must-surface** (provisional; phase 3 coordinates) | After phase 3's Q3.3 work the generator binds through the full runtime replacement precedence — a residual mismatch then means the deployed binding set genuinely differs from what was built (assembly/package skew); silently rendering dynamic with *different bindings than the build declared* is exactly the hazard the ruling targets |
| `FunctionBindingMismatch` (gauntlet step 3) | **Must-surface** for default-registry divergence (provisional; phase 3 coordinates) | Declaring-type/overload drift under the *default* registry signals assembly skew — a defect. The one arguable sub-case: a *per-request* export registry (`options.Functions`) diverging by host choice is options-shaped legitimacy; WI10's research with phase 3 decides whether the class splits on that detail |
| `SchemaVersionUnsupported` (registration) | **Must-surface** | A manifest outside the runtime's schema window means the deployable pairs generator and engine packages out of contract — a packaging defect that today silently un-precompiles an *entire assembly* behind an opt-in callback (`Hed7102`); nothing about it is stale data or change tracking |
| `EngineVersionIncompatible` (registration) | **Must-surface** | Same argument: version skew between the manifest's engine and the running engine is a deployment/packaging defect, and whole-assembly silent rejection is the worst place to be quiet |
| `CaseMismatch` (lookup shadow index) | **Informational — out of scope** | Never a gauntlet failure and never degrades anything: a registry lookup *miss* is contractually never a failure; the `HED7103` event is a diagnostic aid and stays as-is |
| duplicate key (registration) | **Already surfaces** | `PrecompiledTemplates.Register` throws `PrecompiledRegistrationException` (`PrecompiledTemplates.cs:110`) — the existing precedent that registration defects throw; D12b extends that posture to the two version gates above |

- **Behavior change proposed for the must-surface classes, under the DEFAULT policy.** Today the
  default is `PrecompiledMismatchPolicy.Fallback` (`TemplateOptions.cs:47-54`): every gauntlet
  failure silently recompiles dynamically, with visibility only via the opt-in `OnFallback`
  callback; only opt-in `Strict` throws (`PrecompiledTemplates.TryResolve`,
  `PrecompiledTemplates.cs:170-180`). Proposed end state: per-request must-surface classes throw
  `PrecompiledMismatchException` **by default**; the registration-time classes make `Register`
  throw instead of silently ignoring the assembly; legitimate classes keep today's fallback +
  event. Mechanism recommendation: a **default-policy change with an explicit opt-out** (a
  policy value preserving blanket degrade, e.g. per-class or `DegradeAll`) rather than a
  new mandatory diagnostic surface — because a diagnostic surface still requires host wiring,
  and opt-in-silent-by-default is precisely the shape of the current bug. The implementing spec
  makes the final mechanism call.
- **Back-compat routing.** Changing the *default* fallback behavior is behavioral and a breaking
  change for hosts relying on silent degrade — it is filed in the
  [next-window candidate register](../spec/common/breaking-windows.md#next-window-candidate-register)
  by WI10 with the taxonomy as its evidence, and the default flip lands only inside a ratified
  window. What ships from this phase now: the taxonomy (spec'd next to the mismatch-policy
  section of [precompilation.md](../precompilation.md#the-validation-gauntlet-and-mismatch-policy)),
  the D12a generation-time fix, and any purely additive visibility improvements.
- **Cross-phase contract.** Phases 1/2 own the intentional-refusal taxonomy (everything the
  build refuses on purpose must reach the runtime as a *return-shaped* refusal — marker entry or
  no-entry — never an exception); phase 3 co-owns the final classification of the two binding
  classes; phases 1–3 consume the taxonomy table as the closed list of legitimate degrades.

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
| WI8 | **Item metadata + docs + registry test** — implement the OQ1 ruling (committed 2026-07-25: wire `Precompile` as the per-item opt-out — stays in the `@<<` import map, emits no entry point and no manifest entry; remove `Name`'s `CompilerVisibleItemMetadata`/targets lines); precompilation.md updates (metadata section, diagnostics rows `HED7017`–`HED7020`, D10 root paragraph); HED7xxx registry test (code ↔ docs table ↔ registry) | F8 fix (OQ1 ruling) | — |
| WI9 | **Resolver registry consultation** — registry sweep as the first tier of the private `Search` ladder for `View`/`PartialView`/`Master`; key mapping via the shared `TemplateKey` helpers; hit returned through `out cached` as the precompiled adapter; tests per arm (hit/miss/gauntlet-failure, tier-precedence vectors incl. later-location registry hit vs earlier-location disk file, `FullCSharp`-vs-`Native` fingerprint refusal); precompilation.md "stays fully dynamic" sentence rewritten | Q5.2 ruling (D11) | WI2 (`TryMakeRelative`), WI7 (shared extension/munging helpers) |
| WI10 | **Fallback legitimacy** — delete the blanket `catch (Exception)`; `HED7020` error descriptor + registry/docs rows + fault-injection test proving per-template error, continued pass, intact manifest; the D12b taxonomy researched path-by-path with phases 1/2 (intentional refusals) and phase 3 (binding classes) and spec'd into precompilation.md; next-window candidate entry filed with the proposed default-policy mechanism | Q2.2 ruling (D12) | phase 2 WI1 (clamp fix) for the catch removal; phases 1–3 review for the final taxonomy |

WI1–WI4 are the fix-first group — independently shippable, no cross-dependencies, matching the
[synthesis sequencing](../research/generator-code-sharing/07-recommendations.md#recommended-sequencing)
("bug fixes first"). WI3 is the unblock point for phases 1 and 3; WI10's taxonomy is the unblock
point for the phases-1–3 fallback-legitimacy consumers.

## Dependencies & ordering

- **Phase 0 posture (landed):** every test in this phase runs under the gauntlet-crossing guardrails — see the
  [precompiled-tier posture](../spec/common/testing-standards.md#precompiled-tier-posture) rule. This phase's
  fix-first group un-skips phase 0's quarantined
  `BomTemplate_StaysOnThePrecompiledTier_UnderFileBackedStaleness` fixture (F1) as acceptance evidence.

- **Depends on:** no other phase for WI1–WI9. All shared files land in `src/Heddle/Precompiled/`
  with individual `<Compile Include>` links — the folder's established pattern (`TemplateKey.cs`,
  `DefaultFunctionTable.cs`); nothing waits on the `Language/**` glob or any other phase's
  extraction. WI10 has two soft cross-phase edges: the catch removal (D12a) is sequenced after
  phase 2's WI1 clamp fix (the last known legitimate thrower inside the emit path), and the
  final taxonomy classification is reviewed with phases 1/2 (intentional-refusal taxonomy) and
  phase 3 (binding-mismatch classes) — the research and drafting need not wait.
- **Owns for others:** `Precompiled/ContentHash.cs` (phase 6 references it for its co-report of
  the hash duplication, [06 F1](../research/generator-code-sharing/06-diagnostics-utilities.md));
  the `OutputProfile`/`ExpressionMode`/`PrecompiledOptionsFingerprint`/`PrecompiledCapabilities`
  links (phases 1 and 3 consume the typed enums for `RenderType`/emitter and binding work); and
  the **fallback-legitimacy taxonomy** (D12b) — the closed list of legitimate degrades that
  phases 1–3 consume (phases 1/2 keep intentional refusals return-shaped against it; phase 3
  aligns its binding-mismatch handling to it).
  Per [D5 of the cross-cutting decisions](../spec/common/cross-cutting-decisions.md#d5--implementation-follows-the-owning-plans-declared-order),
  those phases may assume this phase's artifacts once the initiative's declared order places
  phase 5 ahead of them; if the initiative sequences differently, WI3 is extractable as a
  standalone first item.
- **Internal ordering:** WI1–WI4 in any order (parallelizable); then WI5/WI6 (need WI3); WI7 any
  time; WI9 after WI2/WI7; WI10's taxonomy research any time, its catch removal after phase 2
  WI1; WI8 last (collects the docs/registry deliverables, now including the `HED7020` row).
- **Unblocks:** phases 1, 3 (links; taxonomy), phase 2 (taxonomy), phase 6 (ContentHash
  reference, and the forwarded-diagnostic work that shares the `HED7018`–`HED7020` registry
  conventions).

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
- **Three new diagnostics** (`HED7018`, `HED7019` warnings; `HED7020` error): additive, claimed
  per [D1 registry rules](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx);
  builds that were clean stay clean unless they have the (currently silent) defect the
  diagnostic names. `HED7020` is the deliberate exception to "no new errors": a build that today
  goes green while an emitter defect silently un-precompiles a template goes red instead —
  defect-surfacing per the fallback-legitimacy ruling, precedented as a fix (the degrade it
  replaces was never contractual; the dynamic path it hid is unchanged and still available by
  removing the template from precompilation).
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
- **`Precompile`/`Name` metadata (OQ1, ruled):** wiring `Precompile` is additive (absent/empty
  metadata ⇒ today's behavior); dropping `Name` removes something that has never had an effect —
  the ruled outcome has an empty migration.
- **Resolver registry consultation (D11):** behavioral but parity-protected — hosted lookups
  that key- and options-match a manifest start serving precompiled where they always compiled
  dynamically. Rendered bytes are identical (the precompiled==dynamic parity contract); the
  observable effects are the removal of dynamic compile cost and new `OnFallback` events on
  gauntlet failures along hosted paths. The one precedence delta — a later-location registry
  hit now wins over an earlier-location disk file — mirrors the cache tier's existing behavior
  (D11) and is documented with the precompilation.md rewrite of the "stays fully dynamic"
  sentence. Not window material by the D1 argument (defect-fix/additive toward the
  precompilation pitch), but the documented-behavior change is called out in the Header.
- **Fallback-legitimacy default flip (D12b): window-governed, not shipped here.** The proposal
  to surface must-surface classes under the *default* policy is a breaking-window candidate —
  hosts relying on silent degrade (deploying skewed assemblies knowingly, or serving mixed
  registries) would start seeing thrown `PrecompiledMismatchException`s / failed `Register`
  calls. WI10 files the candidate with the taxonomy as evidence and the proposed opt-out
  mechanism; until a window ratifies it, runtime behavior under the default policy is unchanged.

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
| WI9's key mapping diverges from the build-side derivation, so hosted lookups miss (or hit the wrong entry) despite matching layouts | No second key grammar: the consult goes through the same shared `TemplateKey` helpers the generator uses (WI2/WI7); per-arm round-trip vectors (pattern → key → manifest hit) are part of WI9's tests | M |
| WI9's tier precedence (registry over disk across locations) surprises a host that overrides a view by dropping a file at an earlier search location | Precedence mirrors the existing cache tier (a cached location-2 template already beats a location-1 file today); gauntlet staleness under `EnableFileChangeCheck` still yields to changed files; documented in the precompilation.md rewrite | S |
| Removing the blanket catch (D12a) reds builds on latent emitter defects that today degrade silently | That is the ruling's intent — but sequencing after phase 2's WI1 clamp fix removes the known thrower first, phase 0's corpus sweep flushes latent defects pre-release, and `HED7020` names the template and exception so the failure is actionable; per-template catch keeps one defect from cancelling the pass | M |
| A must-surface classification in D12b is wrong (a genuinely legitimate degrade starts throwing after the window) | Each class carries its argument in the taxonomy table; phases 1/2/3 review their classes before the spec lands; the default flip is window-gated with an explicit opt-out policy, so a misclassification is recoverable without a hotfix | M |

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
- [ ] `HED7018`, `HED7019`, and `HED7020` exist in `GeneratorDiagnostics`, the
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
- [ ] `<HeddleTemplate Precompile="false"/>` keeps the template in the `@<<` import map (no
      `HED7011` on importers) while emitting no entry point and no manifest entry; absent/empty
      metadata is byte-identical to today; no `Name` metadata declaration remains in the
      props/targets.
- [ ] Every resolver arm consults the precompiled registry: for each of
      `None`/`View`/`PartialView`/`Master`, a key- and options-matched request returns the
      precompiled adapter with zero dynamic compiles; tier precedence (registry > cache > disk,
      each in location order) holds on the WI9 vectors; a `Native`-built manifest is refused for
      the arms' `FullCSharp` requests by the fingerprint step; misses and gauntlet failures fall
      through to today's byte-identical dynamic path.
- [ ] The generator contains no blanket `catch (Exception)`: a fault-injected emitter exception
      produces exactly one `HED7020` **error** naming the template and exception, the build
      fails, and every other template's source plus the manifest still emit.
- [ ] The D12b taxonomy is spec'd with every `PrecompiledFallbackReason` member (plus
      registration duplicate-key) classified legitimate-fallback / must-surface / informational
      with its argument, reviewed by phases 1/2 (intentional refusals) and phase 3 (binding
      classes) — and the must-surface default-flip candidate is filed in the
      [next-window candidate register](../spec/common/breaking-windows.md#next-window-candidate-register)
      with the proposed opt-out mechanism.
- [ ] The full regression gate ([testing-standards](../spec/common/testing-standards.md#regression-gates))
      is green in one combined run: all TFMs, goldens byte-identical, no grammar diff, and the
      precompiled==dynamic differential corpus unchanged.

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
| An MVC `View` lookup whose `\views\{controller}\{view}` candidate key- and options-matches a registered manifest entry | Precompiled adapter returned through the `Search` ladder's registry tier; zero parses/compiles; rendered bytes identical to the dynamic serve |
| A registry hit at search location 2 while location 1 has the view on disk | The registry entry serves (tier precedence, matching today's cache-over-disk behavior); under `EnableFileChangeCheck` a stale hash still falls through to the disk ladder |
| A `Native`-fingerprinted manifest entry requested through a `View` arm (`FullCSharp`) | Gauntlet `OptionsMismatch`; `OnFallback` event; the dynamic path serves — no special-casing in the resolver |
| `<HeddleTemplate Precompile="false"/>` on an import-only layout file | No entry point, no manifest entry; every `@<<` referencing it still resolves (no `HED7011`); dropping the metadata restores today's output byte-for-byte |
| A fault-injected exception from `TemplateEmitter.Emit` for one template in a ten-template project | One `HED7020` error naming that template and the exception; the build fails; the other nine sources and the manifest are still produced (no `CS8785`, no whole-pass cancellation) |
| A deployable pairing a schema-3 manifest with this runtime, after the D12b window lands the default flip (simulated) | `Register` throws (must-surface class) unless the host opted into the degrade policy; before the window: today's silent ignore + `HED7102` event, unchanged |

## Open questions

None remain. Both questions were ruled by the user on 2026-07-25; the rulings are folded into
the decisions and work items above, and the
[open-questions register](open-questions.md#phase-5--pipeline-config) records them (with Q2.2's
ruling assigning this phase a new cross-phase responsibility). Closure notes:

- **OQ1 — resolved (user, 2026-07-25): "Wire pre-compilation properly."** The plan's
  recommendation is applied: `Precompile` is implemented as the per-item opt-out — the template
  stays in the `@<<` import map, emits no entry point and no manifest entry — and `Name` is
  removed (its `CompilerVisibleItemMetadata` and targets lines deleted rather than inventing
  semantics). WI8, formerly conditional on this ruling, is committed. The original analysis
  (import-map rationale, `HeddleTemplateGenerator.cs:132-138`; `Remove`-workaround gap) stands
  as the design record for the WI.
- **OQ2 — resolved (user, 2026-07-25): "Yes, we should work with pre-compiled templates
  everywhere, most likely this is an easy fix."** The `View`/`PartialView`/`Master` arms consult
  the precompiled registry — in scope as D11/WI9, superseding this plan's earlier
  not-in-this-phase recommendation. The enablers the plan had left in place
  (`ConsultPrecompiled`, `TemplateResolver.cs:77,214-234`; the WI2 `TryMakeRelative` helper) are
  exactly what WI9 builds on. The user's easy-fix expectation is noted — and D11 spells out the
  key mapping and tier precedence explicitly because those are where the small fix can go wrong.
- **New cross-phase responsibility (from Q2.2's ruling — the register's fallback-legitimacy
  principle):** this phase owns the blanket-catch site (`HeddleTemplateGenerator.cs:249-252`)
  and the gauntlet-policy machinery, so it owns the path-by-path research and the
  legitimate-fallback vs must-surface taxonomy — D12/WI10, feeding phases 1–3 and routing the
  default-policy flip through the breaking-windows register.

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
| Only the `None` arm consults the registry; `Search` munges then runs a two-tier (cache, disk) location ladder; MVC arms set `ExpressionMode.FullCSharp` | `src/Heddle/Runtime/TemplateResolver.cs:72-85` (`ConsultPrecompiled` at `:77`, method at `:214-234`), `:118,132`, `:148-208` — re-verified for the D11 fold |
| Blanket `catch (Exception)` degrade; intentional refusals are return-shaped (`IsMarker` / `UnsupportedReason`), not exceptions | `src/Heddle.Generator/HeddleTemplateGenerator.cs:207-252` (catch at `:249-252`, marker branch `:231-245`, unsupported comment `:246-247`) |
| An uncaught generator exception surfaces as compiler **warning** `CS8785` and discards the generator's entire contribution | [Roslyn source-generators design doc](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md) (`GeneratorDriver` wraps user-code exceptions; the generator no longer contributes to the output) — basis for D12a's error-diagnostic decision |
| Gauntlet check order (marker, options, extensions, functions, staleness) and per-reason failure sites; `PrecompiledFallbackReason` members incl. informational `CaseMismatch` | `src/Heddle/Precompiled/PrecompiledGauntlet.cs:21-56,58-184`; `src/Heddle/Precompiled/PrecompiledFallbackReason.cs` |
| Default policy is `Fallback` (silent degrade + opt-in `OnFallback`); `Strict` throws in `TryResolve`; `Register` silently ignores version-gated assemblies but **throws** on duplicate keys | `src/Heddle/Data/TemplateOptions.cs:47-54`; `src/Heddle/Precompiled/PrecompiledTemplates.cs:63-119` (gates `:74-90`, duplicate-key throw `:110`), `:162-181` |
| Phase 2 pins "after the WI1 clamp fix, any exception out of the shaping code is a defect" — D12a's sequencing premise | [phase-2-document-shaper.md](phase-2-document-shaper.md) (Goal/D-item notes around its WI1 clamp fix) |
| Research findings and cross-area synthesis this plan resolves | [05-pipeline-config.md](../research/generator-code-sharing/05-pipeline-config.md) (F1–F8); [07-recommendations.md](../research/generator-code-sharing/07-recommendations.md) (drift items 2, 11, 15; Tier 1/2 layout; sequencing) |
| The rulings this revision folds (Q5.1, Q5.2, and Q2.2's cross-phase assignment) | [open-questions register](open-questions.md#phase-5--pipeline-config) (user, 2026-07-25; fallback-legitimacy principle in the register preamble) |

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


## Implementation record

Landed 2026-07-25. `dotnet build Heddle.sln -c Debug` green; `dotnet test Heddle.sln -c Debug` green
with the four remaining phase-1/3/4 quarantined skips (×2 TFMs) and no others — phase 0's
`BomTemplate_StaysOnThePrecompiledTier_UnderFileBackedStaleness` fixture is **un-skipped and green**,
this phase's acceptance evidence for WI1.

### Where the work landed

| WI | Files |
| --- | --- |
| WI1 | new `src/Heddle/Precompiled/ContentHash.cs` + csproj link; `PrecompiledGauntlet.HashFile` decodes before hashing (dead `HashBytes`/`ToHex` deleted); `HeddleTemplateGenerator.ComputeContentHash` deleted; `PrecompiledTemplateInfo.ContentHash` doc corrected; `ContentHashLockstepTests.cs`; BOM fixture un-skipped in `QuarantinedDriftFixtures.cs` |
| WI2 | `TemplateKey.TryMakeRelative`/`ToPath`; `HeddleTemplateGenerator.DeriveKey` rewritten (`Relative` deleted) with the out-of-root signal; both gauntlet reconstitutions on `ToPath`; `HED7018` descriptor + registry row + docs row; `PipelineContractTests.cs`, `PipelineDiagnosticsTests.cs` |
| WI3 | four `<Compile>` links (`OutputProfile`, `ExpressionMode`, `PrecompiledOptionsFingerprint`, `PrecompiledCapabilities`); `GlobalConfig` retyped to enums; `ConfigReader` parses through the shared table; `TemplateEmitter.IsHtml`/mode compares are enum compares; `FingerprintExpr` constructs a real fingerprint; `CapabilitiesExpr` uses `nameof`; arity test in `PipelineContractTests` |
| WI4 | new `src/Heddle/Precompiled/PrecompiledSchema.cs` + link; `PrecompiledTemplates` consts retired onto it; `EmitManifest` interpolates `CurrentSchemaVersion`; `ResolveEngineVersion` formats through `FormatEngineVersion` and falls back to the generator's own version with `HED7019` |
| WI5 | new `src/Heddle/Precompiled/HeddleBuildOptions.cs` + link; `ConfigReader` reduced to a lookup-lambda adapter; `TemplateOptions()` chains the named ctor and both read the shared defaults; props↔code↔runtime lockstep test |
| WI6 | `BuildManifestEntry`/`BuildMarkerManifestEntry` merged into one marker-flagged builder (+ `FingerprintExpr`, `MarkerFunctionBindingsArray`); `MixedManifestCompileGateTests.cs` |
| WI7 | `TemplateKey.TemplateExtension`/`HasTemplateExtension`/`StripTemplateExtension`; generator discovery, `PrecompiledRuntime.StripHeddleExtension`, `TemplateResolver`'s `FileExtension` const and `TryNormalizeCore`'s append step converged; MSBuild glob annotated |
| WI8 | `Precompile` wired as the per-item opt-out and `Name` removed (props/targets); precompilation.md metadata section, `HED7017`–`HED7019` diagnostics rows, registry-consult rewrite, staleness/root-pairing/taxonomy sections; HED7xxx registry lockstep test |
| WI9 | `TemplateResolver.Search`'s private ladder gains a registry tier for `View`/`PartialView`/`Master` (+ `HostedOptions`); `HostedResolverRegistryTests.cs` |
| WI10 | blanket `catch (Exception)` replaced by a per-template `HED7020` **error** (descriptor + registry row + docs row); `TemplateEmitter.FaultInjector` test seam; the D12b taxonomy spec'd into precompilation.md and the default-flip filed in the next-window candidate register |

### Corrections to this plan, recorded against source

- **`TemplateResolver.Search`'s munging does not fold onto `TemplateKey` byte-identically** (WI7).
  The three rules are genuinely different: the resolver appends the extension only when the name has
  *no* extension at all (`Path.HasExtension`, so a `.txt` view stays `.txt`) while `TemplateKey`
  appends on a dot-less final segment; the resolver rejects `..` as a *substring* while `TemplateKey`
  rejects it per segment; the resolver folds `~/` *anywhere* while `TemplateKey` strips only a leading
  one. Only the extension const is shared; the divergence is now documented at the call site. The key
  grammar is shared where it actually governs identity — WI9's registry consult normalizes each
  candidate's root-relative path through `TemplateKey.TryNormalize`, which is exactly
  `TryMakeRelative(Path.Combine(_rootPath, rel), _rootPath)` without the redundant round trip.
- **Linking the option enums into the analyzer breaks any project that references both assemblies.**
  `OutputProfile`/`ExpressionMode`/`PrecompiledOptionsFingerprint`/`PrecompiledCapabilities` now exist
  in `Heddle` *and* `Heddle.Generator`, so `CS0433` fires wherever both are referenced. Consumers are
  unaffected (the generator ships as an analyzer, never as a reference), but three test-side fixes were
  needed and are the pattern for any future link: the integration suite's generator reference is
  aliased (`Aliases="generator"`, one `extern alias` in `DifferentialHarness`), and both generator test
  harnesses filter `Heddle.Generator.dll` out of the reference set they hand to the compilations they
  create. The plan did not anticipate this.
- **The snapshot goldens changed by exactly one line each** (`HED7018`). Every snapshot fixture lives
  at `views/<name>.heddle` with no `HeddleTemplateRoot` set, which is precisely the out-of-root
  condition D3 makes visible. The **generated code is byte-identical** in all eight snapshots — that
  diff is the byte-neutrality evidence for WI3/WI6's emitter rework.
- **`Precompile`'s parse rule is stated here, not in the plan.** Only an explicit boolean `false` opts
  out; absent, empty, and unparsable metadata all mean "precompile", so the change is additive by
  construction (no new diagnostic for a typo — an item-level `HED7009` twin was not in scope).
- **`PrecompiledTemplateInfo.ContentHash`'s XML doc described a contract no shipped manifest ever
  satisfied for BOM'd files**, exactly as D1 predicted; corrected in the same change.
- Line references verified accurate except: the blanket catch was at `:249-252` as cited but the
  `Relative` helper ran `:481-490` (cited `:473-490` covers `DeriveKey` too), and `HashFile`/`HashBytes`/
  `ToHex` occupied `:186-205`.

### Byte-neutrality evidence

Every extraction WI is byte-neutral by acceptance gate, verified in one combined run: the eight
generator snapshots differ only in the new HED7018 diagnostic line (generated C# byte-identical); the
corpus differential and render-parity suites, the resolver-path corpus sweep, and the golden corpus are
unchanged; `src/Heddle.Language/generated/` has no diff. Two goldens changed, both spec-backed and
additive: the `Heddle` public-API surface (the four new shared types plus the three new `TemplateKey`
members) and the snapshot diagnostic lines above.

### Not implemented

Nothing from the work-item table. Two items deliberately remain proposals rather than shipped
behavior, as the plan requires: D12b's default-policy flip (filed in the next-window candidate register, runtime behavior
unchanged) and the provisional classification of the two binding-mismatch classes, which phase 3
co-owns and finalizes.

## Post-audit work items (2026-07-26) — Q8.2, Q8.11, Q8.12

Three ruled items, landed together because the version bump and the gate are only correct as a pair: a
version that advertises a break the metadata does not enforce is a lie in one direction, and a gate that
enforces one the version does not declare is a lie in the other. Full rulings, evidence and mutation
results in the [register](open-questions.md); what follows is what changed here.

### Q8.12 — `Name` restored as an optional additional import name, and the metadata made to work at all

**This section records two landings, because the first was wrong.** The history is kept legible rather than
rewritten, the same way the twice-reshaped quarantine fixture is.

**Landing 1 (superseded).** `Name` shipped as a **second spelling of `Key`** — one setting, one set of
downstream rules: the same normalisation, the same `HED7002`/`HED7003` participation, the same `HED7018`
suppression, and a `Key`+`Name` disagreement reported as `HED7004`. That is an **override**. It replaced the
path-derived key, so `@<<{{ templates/report.heddle }}` stopped resolving on a named item and the importer
drew `HED7011` — a silent break of every existing import that named a file. Phase 5 had *removed* the
metadata on a review record reading "`Name` removed per the recommendation"; removal was never the ask (the
ask was to wire `Precompile`), and this plan's own record carried that overreach. Landing 1 then carried a
second one.

**Landing 2 (Q8.25's correction, shipped).** `Name` is **additive**. A template keeps its path-derived (or
explicit `Key`) registration key **and** gains the registered name; both spellings resolve, and nothing that
resolved before stops resolving. Concretely:

| Concern | `Key` | `Name` |
| --- | --- | --- |
| Registration key / manifest row / entry class / `#line` file | sets it | **untouched** |
| `@<<` import resolution | resolves | resolves, **in addition to** the key |
| `HED7002` duplicate, `HED7003` case-only twin | participates | **does not** — a name is not a key, registers no manifest row and is never a registry lookup |
| `HED7018` out-of-root | suppresses | **does not** — the flattened key still exists and is still unasked-for |
| Both set together | key is `Key` | name is `Name` — **two names for one template, not a conflict** |
| Unusable value | `HED7004`, item un-precompiled | `HED7004`, **key unaffected**: a broken addition costs the addition |

The import map is built keys-first, names-second, which is what makes the additivity structural rather than
conditional: a registered name can never displace a real key spelling.

**`HED7028` is claimed** (landing 2 reversed landing 1 here too). Landing 1 declined it because both of its
new faults were instances of `HED7004`'s "this item's explicit key metadata is unusable" — true, and that
generalised `HED7004` message stays. Landing 2's diagnostic is not of that class: it fires where an import
**resolved**, through the key spelling of a template that also has a name, and advises the name-first
spelling. Nothing is unusable, the severity is Warning rather than Error, and the position is the importer's
`@<<{{…}}` block rather than the item — a genuinely new fault class, so a new descriptor.

**The defect underneath.** Wiring the feature revealed that **none** of the three metadata worked from a real
project. `Heddle.Generator.targets` restated each as `<Key>%(HeddleTemplate.Key)</Key>` inside an
`Include="@(HeddleTemplate)"` transform. The transform already copies every metadatum; outside a target a
cross-item `%()` reference evaluates to the empty string — so each element *overwrote* the copied value with
`""`. `Key` and `Precompile="false"` were therefore inert too, and no test crossed the file: every suite
injects `build_metadata.*` directly. That is the same shape as this program's central finding one level down —
a mapping that reads as fixed and is not, with the only coverage on the side of the seam that cannot fail.

**Fallout, all handled here rather than deferred.** (a) The emitted `#line` directives named the
*registration key*, which is indistinguishable from the file path only while every key is path-derived; an
explicit key pointed them at a path that exists nowhere. `#line` now names the template's own file. Landing 1
found this through `Name`; landing 2 made `Name` additive, so only `Key` can reach it now — the separation is
the same separation and is still needed. (b) `samples/codegen-t4-successor` is where the metadata is gated
behaviourally, and the gate had to move with the semantics. Landing 1 used `Name="BuildReport"` to rename the
generated entry class, which `Program.cs` then called by name; with `Name` additive the class name no longer
moves, so that gate evaporated. It is replaced by a real use of the feature: an import-only partial
(`templates/_banner.heddle`, `Precompile="false"`) carrying `Name="Banner"`, imported as `@<<{{Banner}}`. A
metadatum that stops flowing from a real csproj now fails the sample's build with `HED7011`. The entry class
reverts to `Templates_Report` and the rendered output is byte-identical; only the generated-source golden
moves. (c) The `#line` path *form* is Q8.27, below.

### Q8.27 — the `#line` path form: absolute where it costs nothing, relative-and-labelled where it does

The ruling prefers absolute paths and asks that a relative form be **marked** rather than churned. Both halves
landed:

- **Outside `HeddleTemplateRoot` there is no anchor**, and the old fallback was the template's *bare filename* —
  a name the compiler cannot open and that collides across directories. It is now the template's own
  `AdditionalText.Path`, which in a real build is absolute. This is the "absolute where it costs nothing" half:
  the five affected `Verify` snapshots use synthetic relative paths (`views/version.heddle`), so accepting them
  introduced no machine-specific text.
- **Under the root the form stays root-relative**, because an absolute path there is correct for exactly one
  machine and would bake that machine's layout into `samples/codegen-t4-successor`'s generated-source golden
  and into any rooted snapshot — the pinned artifacts would stop being comparable. Instead every generated file
  now **states which form its `#line` names are in**, as a header comment directly under `// <auto-generated/>`:
  `RELATIVE to HeddleTemplateRoot`, or `the template's own path`. That is the flag the ruling asked for, and it
  costs one line per generated file with no machine-dependence.

Cost paid: five of the eight generator snapshots moved (header line plus the `views/` prefix on the `#line`
file), and the sample's generated-source golden gained the header line. Three snapshots emit only the manifest,
which carries no `#line` and no header.

### Q8.2 — `MinSupportedSchemaVersion` 1 → 4, and a manifest fixture that is genuinely old

The break had already shipped in 2.0.0: schema 4 put the prop-layout fingerprint on
`PrecompiledExtensionBinding` as an optional third constructor parameter, removing the two-argument
constructor every schema 1–3 manifest's IL names. `Min = 1` then *accepted* precisely the manifests that
cannot run, and the fault landed as a `MissingMethodException` out of `PrecompiledTemplates.Register` at host
startup. `4` excludes exactly the faulting set.

The demonstration is the deliverable. `OldSchemaManifestFixture` compiles a manifest against a **reference
facade** carrying the pre-schema-4 surface under the real assembly's identity (name, version, public key),
with the real `Heddle` excluded from the reference set — so the emitted IL genuinely names
`.ctor(string, string)` and *cannot* bind to the current form. This is the one thing the superseded pin could
not do: `new PrecompiledExtensionBinding("a", "b")` compiles against today's assembly and silently binds to
the three-parameter constructor, so it exercised a new-schema call wearing an old-schema shape — the
substitution through which the break reached release behind a green suite.

### Q8.11 — one `<VersionPrefix>`, every first-party assembly signed, and a gate

Nine `<Version>` elements become one `<VersionPrefix>` in `Directory.Build.props`. The statements that cannot
live there — four npm manifests, the VS Code extension's pinned tool version, the LSP workflow's
`--version`, four prose release-line sentences, the CHANGELOG section — are held in step by
`VersionConsistencyTests`. One statement was **deleted rather than gated**: the language server's
`InformationalVersion`, a hand-maintained `"1.0.0"` that `heddle-lsp --version` and the LSP `initialize`
response reported for the whole 2.0 line, is now read off the assembly.

`Heddle.Demo.Models`, `Heddle.Demo.Wasm` and `Heddle.LanguageServices.Tests.Corpus` are strong-named: eight
`CS8002` → zero. Scriban is third-party; its warning is accepted through a **declared list** in a new root
`Directory.Build.targets`, keyed on the named assembly and applied only to signed projects. Stated plainly
because it bounds the claim: Roslyn has no per-reference suppression for `CS8002`, so the mechanism is keyed
*on* the reference rather than scoped to it.
