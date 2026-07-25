# Area 05 — `HeddleTemplateGenerator.cs` + `Pipeline/*` vs runtime options / fingerprint / path rules

**Generator side:** `src/Heddle.Generator/HeddleTemplateGenerator.cs` (506 lines), `Pipeline/ConfigReader.cs` (81), `Pipeline/GlobalConfig.cs` (60), `build/Heddle.Generator.props` / `.targets`.
**Runtime side:** `src/Heddle/Data/TemplateOptions.cs`, `src/Heddle/Precompiled/*` (gauntlet, fingerprint, registry, runtime), `src/Heddle/Runtime/TemplateResolver.cs`.

Findings ordered by drift risk. All line references verified.

---

## F1. Content-hash computation hand-rolled on both sides — and the two rules do not agree (highest risk)

**Rule.** "The staleness identity of a template file is the lowercase hex SHA-256 of its content." Both sides implement SHA-256 + `b.ToString("x2", InvariantCulture)` hex folding independently, and **the inputs differ**: the generator hashes `Encoding.UTF8.GetBytes(text.ToString())` — Roslyn's *decoded* `SourceText` re-encoded to UTF-8 without BOM — while the runtime hashes the **raw file byte stream**.

- Generator: `src/Heddle.Generator/HeddleTemplateGenerator.cs:412-422` (`ComputeContentHash`), fed at `:210`; content originates from `pair.Left.GetText(ct).ToString()` at `:65-73`.
- Runtime: `src/Heddle/Precompiled/PrecompiledGauntlet.cs:185-190` (`HashFile` over `File.OpenRead`), hex folding at `:198-205` (`ToHex`); consumed by `CheckStaleness` at `:164-183`, gated by `options.EnableFileChangeCheck` (`:46-52`).
- **`PrecompiledGauntlet.HashBytes` (`:192-196`) is dead code — zero callers repo-wide** — and is exactly the shared primitive the generator re-implemented.

**Drift risk: high, silent.** Any `.heddle` file with a UTF-8 BOM, saved as UTF-16/Latin-1, or normalized by Roslyn on decode produces `HashFile(...) != entry.ContentHash`, so every such template fails `StaleContent` on every request under `EnableFileChangeCheck` and silently takes the dynamic path (HED7101 callback only if the host wired `OnFallback`). The build reports nothing.

**Extraction: directly sharable.** `SHA256`/`StringBuilder`/`CultureInfo` are netstandard2.0, no Roslyn. Move `HashBytes`/`ToHex` plus a `HashText(string)` that pins the encoding decision (ideally BOM-stripping, or hashing raw bytes on both sides) into e.g. `src/Heddle/Precompiled/TemplateContentHash.cs`, and add one `<Compile Include>` link next to the existing `TemplateKey.cs` link. The bytes-vs-decoded-text mismatch must be resolved *as part of* the extraction.

---

## F2. The "identity-bearing options" set is stated four times, in three type systems

**Rule.** "OutputProfile + ExpressionMode + TrimDirectiveLines (and only those) determine compiled output bytes, and therefore key caches / the precompiled fingerprint."

- Runtime authority: `src/Heddle/Precompiled/PrecompiledOptionsFingerprint.cs:10-25`; compared field-wise in `PrecompiledGauntlet.cs:58-70`.
- Runtime restatement #2: `src/Heddle/Data/TemplateOptions.cs:164-167` (`Equals`) and `:188-196` (`GetHashCode`) — same triple plus `Encoder`/`RootPath`/`TemplateName`.
- Runtime restatement #3: `src/Heddle/Runtime/TemplateResolver.cs:55-58` (`CacheKey` = fullPath + profile + trim).
- Generator: `src/Heddle.Generator/Pipeline/GlobalConfig.cs:10-59` re-models the triple **as strings** with hand-rolled `Equals`/`GetHashCode` (`:30-58`); `src/Heddle.Generator/Emit/TemplateEmitter.cs:2531-2534` + `:2556-2559` re-materializes it as source text, mapping strings back to enum member names at `:2570-2580`.

**Drift risk: high.** `TemplateEmitter.Mode()` (`:2574-2579`) has `default: return "Native"` — any unrecognized string is baked as `Native`. Currently consistent only because `ConfigReader.ReadEnum` canonicalizes case-insensitively (`ConfigReader.cs:52-54`), but the two lists live 2,500 lines apart with no shared constant. If a fourth identity-bearing option is added to `PrecompiledOptionsFingerprint`, nothing forces `GlobalConfig`/`ConfigReader`/the props file to grow it — the generator keeps emitting a 3-arg fingerprint and the mismatch surfaces only as runtime `OptionsMismatch` fallbacks.

**Extraction: needs adapter, partially directly sharable.** `OutputProfile` (`Data/OutputProfile.cs:9-18`) and `ExpressionMode` (`Data/ExpressionMode.cs:6-19`) are dependency-free enums — directly linkable, letting `GlobalConfig` hold real enums, deleting the string comparisons at `TemplateEmitter.cs:121, 2015, 2070, 2570-2580`, and emitting `Enum.ToString()`. `PrecompiledOptionsFingerprint` itself is dependency-free and linkable so the generator constructs the real struct — guaranteeing arity/order agreement. `TemplateOptions` is **not** sharable (pulls `FunctionRegistry`, `TextEncoder`, `AppContext`).

---

## F3. Root-relative key derivation vs key→path reconstitution: inverse functions written independently

**Rule.** "A template key is its path relative to the template root, `/`-separated." Generator computes key = f(path, root); runtime computes path = f⁻¹(key, RootPath).

- Generator: `HeddleTemplateGenerator.cs:473-479` (`DeriveKey`), `:481-490` (`Relative` — `\`→`/`, `TrimEnd('/')`, **OrdinalIgnoreCase** prefix test, else `Path.GetFileName`).
- Runtime inverse: `PrecompiledGauntlet.cs:165-167, 174-176` — `Path.Combine(options.RootPath, entry.Key.Replace('/', Path.DirectorySeparatorChar))`.
- Runtime key source for lookups: `TemplateResolver.cs:227` passes raw `viewName` to `PrecompiledTemplates.TryResolve`; normalization happens in the shared, already-linked `TemplateKey` (`Precompiled/TemplateKey.cs:46-103`).

**Drift risk: high, silent.**
1. `Relative` falls back to `Path.GetFileName(path)` when the file is not under `HeddleTemplateRoot` (`:484, :489`) — the directory silently disappears from the key, with **no diagnostic** (comment at `:167-168`). `views/home/index.heddle` registers as `index.heddle`; runtime lookup misses; every render silently goes dynamic. Multiple such files collide into one key and trip HED7002 (`:176-181`) reporting a "duplicate" that isn't one.
2. Root prefix comparison is `OrdinalIgnoreCase` (`:487`) while keys compare `Ordinal` everywhere (`PrecompiledTemplates.cs:45-48, 101-103`, `TemplateKey.cs:11-15`) — an unshared casing policy, currently benign.
3. Default build-time root is `$(MSBuildProjectDirectory)` (`build/Heddle.Generator.props:29`); runtime root defaults to `AppContext.BaseDirectory` (`Data/TemplateOptions.cs:123`). Nothing validates the two roots imply the same relative keys.

**Extraction: directly sharable.** `Relative` uses only `Path.GetFileName` + string ops. It belongs next to `TemplateKey` as `TemplateKey.TryMakeRelative(path, root, out key)`; the runtime's reconstitution becomes the same file's `ToPath(key, root)`.

---

## F4. `.heddle` extension knowledge spread across five independent implementations

**Rule.** "The template extension is `.heddle`; a key without an extension gets `.heddle`; a key with `.heddle` strips it to become a template name."

- Generator discovery filter: `HeddleTemplateGenerator.cs:52-53` (`EndsWith(".heddle", OrdinalIgnoreCase)`).
- Generator MSBuild glob: `build/Heddle.Generator.targets:5` (`**\*.heddle`).
- Shared/linked: `TemplateKey.cs:94-97` (append when final segment has no `.`).
- Runtime resolver: `TemplateResolver.cs:16` (`const string FileExtension = ".heddle"`), applied at `:160-163`.
- Runtime partial resolution: `PrecompiledRuntime.cs:309-315` (`StripHeddleExtension`, OrdinalIgnoreCase).

Adjacent finding: `TemplateResolver.Search` (`:155-166`) re-implements three rules `TemplateKey` already owns — append extension (vs `TemplateKey.cs:94-97`), reject `..` (vs `:81-85`), strip `~/` (vs `:64-65`) — plus `\` separator conversion. Also: `ConsultPrecompiled` is only called from the `TemplatePathType.None` arm (`TemplateResolver.cs:77`); the `View`/`PartialView`/`Master` arms never consult the precompiled registry, so MVC-style lookups bypass precompilation entirely regardless of key agreement.

**Drift risk: medium.** Case-sensitivity differs across sites; the "no extension ⇒ `.heddle`" rule exists in both `TemplateKey` and `TemplateResolver.Search` with different surrounding path munging.

**Extraction: directly sharable.** A single `public const string TemplateExtension = ".heddle"` plus `HasTemplateExtension`/`StripTemplateExtension` on the already-linked `TemplateKey` covers all five sites.

---

## F5. Schema version and engine version: literals on the generator side, constants on the runtime side

**Rule.** The manifest contract version and the engine-compatibility window.

- Generator: `HeddleTemplateGenerator.cs:380` (`schemaVersion: 2` as a string literal in emitted source), `:492-501` (`ResolveEngineVersion`, `major.minor.build` formatting), `:503` (hardcoded `"2.0.0"` fallback when no `Heddle` reference is found).
- Runtime: `PrecompiledTemplates.cs:20-21` (`MinSupportedSchemaVersion = 1`, `MaxSupportedSchemaVersion = 2`), gate at `:74-82`, `IsEngineCompatible` at `:196-202` (`Version.TryParse` + same-major + `<=`).
- Contract type: `Precompiled/HeddleCompiledTemplatesAttribute.cs:13-26`.

**Drift risk: medium-high, all-or-nothing.** Bumping the emitted schema to 3 without touching `MaxSupportedSchemaVersion` (or vice versa) makes the runtime reject the **entire assembly** at `Register` — every template silently falls back, with only HED7102 via `OnFallback` that most hosts never wire. The `"2.0.0"` fallback at `:503` is a second hazard: when the `Heddle` reference isn't visible in `ReferencedAssemblySymbols` (aliased/embedded/ILMerged), the generator asserts a version it never observed, and `IsEngineCompatible` decides registration on a fabricated number.

**Extraction: directly sharable for constants; needs adapter for resolution.** Link a tiny `PrecompiledSchema.cs` (Min/Max/`CurrentSchemaVersion` ints) into the generator and interpolate into the emitted `schemaVersion:`. `ResolveEngineVersion` stays generator-side (needs `Compilation`), but the `major.minor.build` formatting rule and the compatibility predicate can share one netstandard2.0 helper taking `System.Version`.

---

## F6. Option name/default table exists three times (props ↔ ConfigReader ↔ TemplateOptions ctor)

**Rule.** The name, type, and default value of every Heddle build/runtime option.

- MSBuild: `build/Heddle.Generator.props:6-13` (property names), `:23-31` (defaults: `Html`, `Native`, `true`, `100`, `$(MSBuildProjectDirectory)`, `false`).
- Generator: `Pipeline/ConfigReader.cs:27-37` — same seven names as string literals with the same defaults re-typed in C#, allowed-value lists at `:28, :30`, parse helpers at `:40-79`.
- Runtime: `Data/TemplateOptions.cs:120-130` and duplicate ctor `:132-141` — `ExpressionMode.Native`, `MaxRecursionCount = 100`, `OutputProfile.Html`, `TrimDirectiveLines = true`. (The runtime already duplicates its own defaults between its two constructors.)

**Drift risk: medium.** Three copies of `100` and the `Html`/`Native`/`true` defaults. A one-sided default change flips `CheckOptions` (`PrecompiledGauntlet.cs:58-70`) into `OptionsMismatch` for every template — total silent de-precompilation. `ConfigReader.ReadEnum`'s allow-lists are a hand-copied mirror of the enum members: adding an `ExpressionMode` member makes the generator reject it as HED7009 while the runtime accepts it.

**Extraction: needs adapter.** `AnalyzerConfigOptions` is Roslyn, so `ConfigReader.Read` can't move; reduce it to a `Func<string,string>` lookup adapter over a shared Roslyn-free `HeddleBuildOptions` (names + defaults + `Enum.TryParse(ignoreCase: true)` against the linked enums). That deletes both the allow-list literals and the default triplication; the props-file defaults can then be removed (ConfigReader already defaults blanks identically), collapsing three tables to one.

---

## F7. Manifest/attribute emission is untyped string templating against runtime type shapes

**Rule.** The constructor signatures and member names of `HeddleCompiledTemplatesAttribute`, `IHeddleTemplateManifest`, `PrecompiledTemplateInfo`, `PrecompiledOptionsFingerprint`, `PrecompiledCapabilities`, `PrecompiledFunctionBinding`, `PrecompiledExtensionBinding`.

- Generator: `HeddleTemplateGenerator.cs:372-410` (`EmitManifest` — attribute arg order `:378-381`, `GetTemplates()` signature `:387`, interface name `:385`); `TemplateEmitter.cs:2519-2540` (13 named ctor args) and `:2546-2568` (the marker twin — same 13 args duplicated verbatim), `:2610-2616` (`CapabilitiesExpr`).
- Runtime: `Precompiled/HeddleCompiledTemplatesAttribute.cs:13-17`, `IHeddleTemplateManifest.cs`, `PrecompiledTemplateInfo.cs:37-57`, `PrecompiledCapabilities.cs:8-14`.

**Drift risk: low-medium but late-failing.** A renamed ctor parameter or reordered argument compiles fine in both `Heddle` and `Heddle.Generator`; it breaks only in the *consumer's* build, as a CS error inside `<auto-generated/>` code. `BuildMarkerManifestEntry` is a verbatim copy of `BuildManifestEntry` differing in five lines.

**Extraction: needs adapter (partial).** Codegen of a type shape is inherently textual; the sharable part is the *names*: link the dependency-free `PrecompiledCapabilities`/`PrecompiledOptionsFingerprint` types so the generator uses `nameof`/`Enum.ToString()` instead of literals; factor the duplicated fingerprint/`PrecompiledTemplateInfo` argument block into one generator-side builder taking a marker flag. An integration test compiling the emitted manifest against the real `Heddle` reference is the cheap alternative guard.

---

## F8. Declared-but-unread per-item metadata (`Precompile`, `Name`) — adjacent contract gap

Not duplication proper, but a divergence in the same option surface found during this audit.

- Declared: `build/Heddle.Generator.props:17-19` (`Key`, `Name`, `Precompile`), flowed onto items at `build/Heddle.Generator.targets:11-16`.
- Read by the pipeline: only `Key` (`HeddleTemplateGenerator.cs:58`). `TemplateFile` (`:30-48`) has no field for `Name`/`Precompile`; no other file references `build_metadata` (verified repo-wide).

**Risk: medium, user-visible, silent.** `<HeddleTemplate Update="x.heddle" Precompile="false" />` precompiles anyway; a `Name` override is ignored; `Precompile` is absent from `docs/precompilation.md`. Either wire it in `Initialize`'s `Select` (`:55-82`) or drop it from the props file.

---

## Feasibility note

Everything above classified *directly sharable* (content hash + hex, `Relative`/key↔path, `.heddle` constant, schema-version constants, the two option enums, `PrecompiledOptionsFingerprint`) depends only on `System`, `System.IO.Path`, `System.Text`, `System.Security.Cryptography` — all netstandard2.0, no Roslyn. The mechanism exists and is proven: one more `<Compile Include=... Link="Shared\...">` line per file in `Heddle.Generator.csproj`, plus deleting the generator's copy. Adapter-needing items (`ConfigReader`'s shell over `AnalyzerConfigOptions`, `ResolveEngineVersion` over `Compilation`) are thin Roslyn-side shims over a Roslyn-free core.
