# Phase 7 — template identity & metadata format

Supplementary document of the [phase 7 spec](README.md); normative for the key policy
([D1](README.md#d1--key-normalization-ordinal-case-sensitive--separated-root-relative--closed),
[D2](README.md#d2--precompiled-key-replacement-duplicate--registration-error-no-replace-flag-in-v1--closed)),
the metadata format
([D6](README.md#d6--metadata-discovery-attribute--typed-generated-manifest-the-fingerprint-pinned)),
and the validation gauntlet
([D7](README.md#d7--runtime-hookup-repeatable-registration-gauntlet-order-lazy-partial-routing),
[D8](README.md#d8--precompiledmismatchpolicy--fallback-strict--on-templateoptions-default-fallback--ratified)).
Phase 7 owns template identity for the whole roadmap
([cross-cutting D8](../common/cross-cutting-decisions.md#d8--template-identity--naming-policy-is-owned-by-phase-7)) —
phases that need a template's identity (phase 6 go-to-definition, phase 9 gallery)
reference these definitions.

## The identity policy (consolidated)

- **File-driven templates key on the full resolver-relative path** — anchored at the
  resolver root the same way `TemplateResolver` and `@partial()` anchor paths, because
  absolute build-machine paths do not exist on the runtime machine. This is the zero-
  configuration default the generator emits for every `AdditionalFiles` template.
- **Non-file templates are named by the integration, through the open key-policy seam:**
  at build time, `<HeddleTemplate Include="…" Key="explicit-key" />` item metadata (the
  value still passes `TemplateKey.Normalize` — `HED7004` on invalid input); at run time,
  hosts look up whatever key space they chose. The engine ships mechanics, not naming
  policy.
- **Uniqueness is required for pre-compilation only.** Duplicate keys are a build error
  within one compilation (`HED7002`) and a registration error across manifests (D2
  below). Fully dynamic templates have no naming requirement at all — unchanged.
- **Discovery is public API:** `PrecompiledTemplates.Entries`, `TryGet`, and the
  `OnFallback` callback (README contract section) — an integration can route, validate,
  and report precompiled coverage.

## The key normalization algorithm

`TemplateKey.Normalize(string relativePath)` — one pure function, one source file,
compiled into both `Heddle` (lookup) and `Heddle.Generator` (emit). Steps, in order:

1. **Reject** null/whitespace input (`ArgumentException`; `TryNormalize` returns false).
2. Replace every `\` with `/` (Windows item specs, `~/`-style host paths).
3. Collapse runs of `/` into one.
4. Strip a leading `~/`, then every leading `./`, then any leading `/`.
5. Split on `/`; **reject** any `.` or `..` segment (path escape — same posture as
   `TemplateResolver.Search`'s existing `..` rejection) and any empty trailing segment
   (input ended in `/`).
6. If the final segment has no extension (`.` after the last `/`), append `.heddle`
   (mirroring `TemplateResolver`'s `FileExtension` behavior for extension-less view
   names).
7. **Preserve case exactly.** No folding, no culture, ever.
8. Rejoin with `/`. The result is the key; comparisons are `StringComparer.Ordinal`.

Derivations on each side:

- **Emit time (generator):** the input is the `AdditionalFiles` path made relative to
  `HeddleTemplateRoot` (default `$(MSBuildProjectDirectory)`); explicit `Key` metadata
  replaces the derived input, not the normalization. The manifest stores only normalized
  keys.
- **Lookup time (runtime):** `TemplateResolver.GetTemplate` normalizes `viewName` (the
  `TemplatePathType.None` flow — `viewName` is already root-relative there) or the
  matched search-location relative path (`views/{controller}/{view}.heddle` — the
  hard-coded backslashes are handled by step 2) before the registry probe. The registry's
  own `TryGet` normalizes defensively too, so host code may pass either form.
  `PartialExtension` and generated `@partial` code normalize
  `templateName + FileNamePostfix`. Staleness file access resolves a key back to a path
  with `Path.Combine(options.RootPath, key)` — `/` is a valid separator on every
  supported OS.
- **Alignment contract (documented in `docs/precompilation.md`):** the build-time root
  and the runtime `RootPath` must denote the same folder of the template tree; keys are
  the invariant, roots are per-machine. A precompiled-only deployment needs no template
  files on disk at all — the key is then a pure name (note: `TemplateResolver`'s
  constructor applies `Path.GetDirectoryName` to its argument — a verified quirk hosts
  already live with; the key rule is agnostic to it because both probe and staleness use
  the resulting `_rootPath`/`RootPath` value).

**What the key never contains:** the `|profile` / `|trim` suffixes phases 2/4 added to
the *dynamic* resolver cache key. Those are compatibility facts, recorded in the
`OptionsFingerprint` and enforced by the gauntlet. One template, one key; the dynamic
cache's composite keys are an implementation detail of the dynamic path and are
unchanged by this phase.

**Case handling (the cross-OS rule):** ordinal case-sensitive, per D1's rationale —
folding collides legitimately distinct Linux files; sensitivity only makes case-sloppy
Windows lookups miss, which degrades safely (`Fallback` recompile) and loudly: the
registry maintains an `OrdinalIgnoreCase` **shadow index** built at registration, used
exclusively to fire `HED7103` (`CaseMismatch`) when an ordinal miss would have been a
case-insensitive hit. The shadow index never serves entries. At build, `HED7003` warns
when two keys differ only by case.

### Normalization test table

Part of the spec (`TemplateKeyNormalizationTests`, xUnit `[Theory]`); rows run on every
OS — the function is pure, so cross-OS round-trip holds by construction and the table
proves it stays that way. Each row names the algorithm step(s) it exercises:

| # | Input | Result | Rule exercised / rationale |
| --- | --- | --- | --- |
| N01 | `views\home\index.heddle` (Windows build) | `views/home/index.heddle` | Step 2 — separator unification; the Windows-shaped item-spec input. |
| N02 | `views/home/index.heddle` (Linux build) | `views/home/index.heddle` (= N01) | The round-trip row: both build OSes produce one key space — D1's whole point. |
| N03 | `~/views//home/index.heddle` | `views/home/index.heddle` | Step 4 (`~/` strip — the resolver's own host-path idiom) + step 3 (duplicate-separator collapse). |
| N04 | `./views/home/index` | `views/home/index.heddle` | Step 4 (`./` strip) + step 6 (`.heddle` appended when the final segment has no extension — mirrors `TemplateResolver`'s `FileExtension`). |
| N05 | `/views\home/index.heddle` | `views/home/index.heddle` | Step 4 (leading `/` strip) + step 2 (mixed separators in one input). |
| N06 | `Views/Home/Index.heddle` | `Views/Home/Index.heddle` (≠ N01 ordinally) | Step 7 — case preserved, no folding; the `HED7003`/`HED7103` companion row. |
| N07 | `views/../secrets.heddle` | rejected (`ArgumentException` / `TryNormalize` false) | Step 5 — `..` is a path escape; same posture as `TemplateResolver.Search`'s existing rejection. |
| N08 | `views/./home.heddle` | rejected (`.` segment) | Step 5 — `.` segments are ambiguity, not identity; rejected rather than silently collapsed (an explicit `Key` should be written canonically). |
| N09 | `emails/receipt.v2.heddle` | `emails/receipt.v2.heddle` | Step 6 no-op — an existing extension (any extension, not just `.heddle`) is kept; only extension-*less* names gain `.heddle`. |
| N10 | `   ` | rejected | Step 1 — null/whitespace input. |
| N11 | `home.heddle` | `home.heddle` | Root-level template — zero directory segments is a legal key. |
| N12 | `key-from-integration` | `key-from-integration.heddle` | Explicit `Key` metadata passes the same function (step 6 appends) — the key-policy seam ships mechanics, not exemptions. |

Registry-level companion rows: N06 vs N01 both register (distinct keys) but trigger
`HED7003` at build and `HED7103` on a crossed lookup; N01 registered twice from two
assemblies triggers the D2 registration error.

## Replacement mechanics (closed — D2)

v1 has **no** replace flag. `PrecompiledTemplates.Register`:

1. Reads `[HeddleCompiledTemplates]`; absent → no-op (not an error — ordinary
   assemblies pass through `Configure` freely).
2. Same assembly already registered → idempotent no-op.
3. Schema/engine gate (below); failure → whole manifest ignored + one `HED7102`
   callback.
4. Instantiates the manifest once; validates every entry key (defense in depth — the
   generator already normalized); stages all entries.
5. Any staged key ordinally present in the registry → **`PrecompiledRegistrationException`**
   with the D2 message (verbatim in the README), and **nothing** from the staged
   manifest is published (transactional — no half-registered assembly).
6. Publishes the merged dictionary copy-on-write (lock-free readers).

The reserved future mechanism (named in the error text, built only on the recorded
trigger): a `Replace` boolean on the manifest entry, schema-version bump, resolved with
"replacements register last" ordering exactly as `TemplateFactory.AddExtensions` orders
`[ExtensionReplace]` types.

## The metadata format

Two layers; layer 1 is the only thing read by reflection, layer 2 is typed generated
code.

**Layer 1 — the discovery marker** (one per assembly):

```csharp
[assembly: global::Heddle.Precompiled.HeddleCompiledTemplates(
    manifestType:  typeof(SampleApp.HeddleTemplates.__HeddleManifest),
    schemaVersion: 1,
    engineVersion: "1.0.0")]   // the Heddle assembly version the generator saw in the compilation's references
```

`engineVersion` is the referenced `Heddle` assembly's version
(`IAssemblySymbol.Identity.Version`, `major.minor.patch` string) — not the generator
package's own version; compatibility is between artifact and *runtime engine*.

**Layer 2 — the generated manifest** (`__HeddleManifest.g.cs`), worked for the
[example 1](generated-code.md#example-1--static-text-and-typed-member-paths) and
[example 3](generated-code.md#example-3--embedded-c-verbatim-fullcsharp-build)
templates (neither calls a registry function, so both `FunctionBindings` sections are
empty; a populated section — one shim-bound default row and one directly-bound export
row — and a fallback-marker entry are worked in
[example 7](generated-code.md#example-7--function-calls-shim-bound-built-ins-directly-bound-exports-and-the-unresolvable-negative-oq1--d21)). **Hash convention for every worked hash in this spec:** the values are the
real SHA-256 of the template sources exactly as printed in generated-code.md — UTF-8, LF
line endings, one trailing newline (a real build hashes whatever bytes are on disk; the
convention only makes the examples reproducible). Example 3's source imports
`views/layout.heddle`, whose full content is, for the same reproducibility:

```heddle
@%<footer>{{<p>(c) Heddle</p>}} :: dynamic%@
```

so its hash below is verifiable too. One correction recorded during the audit: an
earlier draft gave the two entries different `ExpressionMode` fingerprints — impossible,
because the fingerprint options are compilation-wide MSBuild properties and per-item
overrides are deliberately absent (README D14). One compilation, one fingerprint: this
manifest is the output of a single project built with `HeddleExpressionMode=FullCSharp`
(example 3 requires it; example 1 compiles identically under it — `FullCSharp` is a
superset), so **every** entry carries `(Text, FullCSharp, false)`. Example 1's inline
manifest entry in generated-code.md shows `(Text, Native, false)` because there it is
built standalone under the default mode:

```csharp
namespace SampleApp.HeddleTemplates
{
    internal sealed class __HeddleManifest : global::Heddle.Precompiled.IHeddleTemplateManifest
    {
        public global::System.Collections.Generic.IReadOnlyList<global::Heddle.Precompiled.PrecompiledTemplateInfo> GetTemplates()
        {
            return new[]
            {
                new global::Heddle.Precompiled.PrecompiledTemplateInfo(
                    key:              "views/product.heddle",
                    entryPointType:   typeof(Views_Product),
                    modelType:        typeof(global::Shop.Product),
                    isDynamic:        false,
                    contentHash:      "e7bb5b9c393419da6b73abb37a6a7ac1c1e6254462277141fabb27036237b266",
                    imports:          new global::Heddle.Precompiled.PrecompiledImport[0],
                    optionsFingerprint: new global::Heddle.Precompiled.PrecompiledOptionsFingerprint(
                        global::Heddle.Data.OutputProfile.Text,
                        global::Heddle.Data.ExpressionMode.FullCSharp,
                        trimDirectiveLines: false),
                    extensionBindings: new[]
                    {
                        new global::Heddle.Precompiled.PrecompiledExtensionBinding(
                            "", "Heddle.Extensions.EmptyExtension, Heddle"),
                    },
                    // no function calls in this template — empty, never null (D21):
                    functionBindings: new global::Heddle.Precompiled.PrecompiledFunctionBinding[0],
                    capabilities:     global::Heddle.Precompiled.PrecompiledCapabilities.StringOutput,
                    strategy:         Views_Product.Root),

                new global::Heddle.Precompiled.PrecompiledTemplateInfo(
                    key:              "views/products.heddle",
                    entryPointType:   typeof(Views_Products),
                    modelType:        typeof(global::Heddle.Tests.Data.TestDataStructure),
                    isDynamic:        false,
                    contentHash:      "bbeeb7240d43a79092ba2913926a91f5b6535ed9a802a1b0eb69c0cce806096f",
                    imports:          new[]
                    {
                        // transitive @<< closure — key + SHA-256 of the imported file's raw bytes:
                        new global::Heddle.Precompiled.PrecompiledImport(
                            "views/layout.heddle",
                            "39ace0f27ef1496afbbbf61f659c62e15dea15d3cd71ae17e56149d851f00e27"),
                    },
                    optionsFingerprint: new global::Heddle.Precompiled.PrecompiledOptionsFingerprint(
                        global::Heddle.Data.OutputProfile.Text,
                        global::Heddle.Data.ExpressionMode.FullCSharp,
                        trimDirectiveLines: false),
                    extensionBindings: new[]
                    {
                        new global::Heddle.Precompiled.PrecompiledExtensionBinding(
                            "list", "Heddle.Extensions.ListExtension, Heddle"),
                        new global::Heddle.Precompiled.PrecompiledExtensionBinding(
                            "", "Heddle.Extensions.EmptyExtension, Heddle"),
                    },
                    // the @list parameter is embedded C#, not a registry function (D21):
                    functionBindings: new global::Heddle.Precompiled.PrecompiledFunctionBinding[0],
                    capabilities:     global::Heddle.Precompiled.PrecompiledCapabilities.StringOutput,
                    strategy:         Views_Products.Root),
            };
        }
    }
}
```

Field semantics — contract type, nullability, and pinned definition (each field's
consumer in parentheses; the C# declarations are in the
[README's public API contract](README.md#public-api-contract)):

| Field | Type / nullability | Pinned definition |
| --- | --- | --- |
| `Key` | `string` — never null or empty | Normalized per the algorithm above; the generator emits only already-normalized keys, and `Register` re-validates defensively (resolver lookup, discovery, `@partial` resolution — one key space). |
| `EntryPointType` | `Type` — null **iff** `IsPrecompiled == false` | The generated static class (host/LSP introspection; the adapter uses `Strategy` directly, no reflection). A fallback-marker entry (README D21 — the template calls a function resolvable from neither the default set nor any referenced export, `HED7014`) has no entry class. |
| `ModelType` / `IsDynamic` | `Type` — null **iff** model-less / `bool` | The declared `@model`/`::` type; `typeof(object)` + `IsDynamic=true` for `:: dynamic` and for model-less-but-member-reading (dynamic-root) templates; `null` + `false` for templates that never read a model. (Adapter's model guard — mirroring the runtime's `#if DEBUG` type check — and typed-entry discovery.) |
| `ContentHash` | `string` — exactly 64 lowercase hex chars, never null | SHA-256 over the template file's **raw bytes**, no normalization — any byte change (including line endings) changes rendered output, so byte-exact is the correct staleness signal. (Gauntlet staleness, host logging even when the check is off.) |
| `Imports` | `IReadOnlyList<PrecompiledImport>` — empty list when no imports, never null | The transitive `@<<` closure as `(key, contentHash)` pairs, recorded by the generator's `ImportReader` (D4). Definitions were flattened at generation time — only hashes matter at run time: editing `layout.heddle` must invalidate `home.heddle`'s precompiled copy. |
| `OptionsFingerprint` | `PrecompiledOptionsFingerprint` — a value struct, never absent | The typed triple `(OutputProfile, ExpressionMode, TrimDirectiveLines)` — exactly the output-byte-changing option set phases 2/4 put into the dynamic cache key; `AllowCSharp` is `ExpressionMode.FullCSharp` by phase 1's bridge and is deliberately not duplicated. **Recorded exclusions:** `Functions` stays out — a `FunctionRegistry` is a runtime object with no build-time value representation, it is in neither `TemplateOptions.Equals` nor the dynamic cache key, and function divergence is policed per-template, per-name by the finer-grained `FunctionBindings` section instead (README D21 — a fingerprint field would kick every template of a compilation to dynamic on any host registration, including templates that call no functions). `MaxRecursionCount` stays out too — baked at build (README D23), in neither `Equals` nor the cache key, so the dynamic path does not distinguish it either. Compilation-wide: every entry of one manifest carries the same value (D14 — no per-item overrides). Field-wise comparison in declaration order; the first differing field names the fallback reason detail (why a hash was rejected: it cannot explain itself). |
| `ExtensionBindings` | `IReadOnlyList<PrecompiledExtensionBinding>` — empty for pure-static templates, never null | `(name, assembly-qualified type name without version)` for every **named registry extension** bound at generation time — the record of *what* to bind, since extension logic is never inlined (D9). The unnamed-output carrier binds under `""`. Engine-internal machinery installed by `PrecompiledRuntime.BindDefinition` (the definition carrier) is not a binding — it is not resolved by name and cannot diverge from the registry. Also empty on fallback-marker entries: no code was emitted, so nothing was bound, and the gauntlet never reaches step 2 for them (README D21). |
| `FunctionBindings` | `IReadOnlyList<PrecompiledFunctionBinding>` — empty when the template calls no functions, never null | Mirrors `ExtensionBindings` (README D21, per the OQ1 resolution): one row per **distinct `(name, target)` pair the generated code calls**, ordered ordinally by name then target — `(name, target type name, overload count)` with the target = the **actual bound type** as AQN **without version**: the discovered exporting container (`"Acme.Web.TemplateFunctions, Acme.Web"`) for a directly-bound export, or the shim's forwarding-target type (`"Heddle.Runtime.Expressions.BuiltInFunctions, Heddle"`) for a shim-bound default. A name whose merged overload set spans targets (a partial default replacement, two exporting assemblies sharing a name) carries one row per target; `OverloadCount` is the build-time merged table's per-target overload count for the name — recorded because, with exports bindable at build, the runtime can no longer recompute the expected set from `FunctionRegistry.Default` alone. A **null target** (`OverloadCount` 0) marks a name resolvable from neither the default table nor any referenced export — the delegate-only remainder — and appears only on fallback-marker entries, which the gauntlet short-circuits with `UnsupportedFunction`. The gauntlet checks the non-null rows against the request's effective registry (`options.Functions ?? FunctionRegistry.Default`); the null/`Default` case passes without enumeration **only when every row targets `BuiltInFunctions`** (`Default` is frozen and cannot have diverged from the default rows — but it equally cannot contain an export, so an export-bound row fails there as `<missing>`: the host never called `RegisterFrom`). Otherwise, per called name: every live registration's target must be declared on a recorded target type **and** each row's live overload count must equal its recorded count; any violation is `FunctionBindingMismatch`. Parity is the integration's responsibility (build references = runtime `RegisterFrom` registrations); these rows are the safety net that turns every violation into a detected divergence. |
| `Capabilities` | `PrecompiledCapabilities` — `[Flags]`, `StringOutput` always set in v1 **except** on fallback-marker entries (`None` — nothing renders) | `Utf8Pieces` added when the u8 tier was emitted (D15). Phase 8 adds sink flags under schema 2; sink flags never apply to marker entries (no strategy to render through). |
| `Strategy` | `IProcessStrategy` — null **iff** `IsPrecompiled == false` | The generated root body (already decorator-wrapped via `WithLocalsFrame` when the document hosts branch participants — the adapter renders through it with no reflection after the single attribute read). Null only on a fallback-marker entry, which the gauntlet fails before anything dereferences it. |
| `IsPrecompiled` | `bool` — `== (Strategy != null)` | `false` marks a fallback-marker entry (README D21): the template is *known* but not precompiled; `Entries` consumers get truthful coverage reporting, and the per-request gauntlet short-circuits with `UnsupportedFunction` (loud under `Strict`, one `HED7101` under `Fallback`). |

## The validation gauntlet

**Registration-time gate** (once per manifest):

- `schemaVersion == 1` — an unknown (newer or older) schema ignores the **whole
  manifest**; every template in it silently uses the dynamic path, surfaced by one
  `HED7102` callback naming the assembly and reason. This is what lets old precompiled
  assemblies degrade gracefully across engine upgrades.
- Engine gate: `manifest.engineVersion.Major == runtime.Major` **and**
  `manifest.engineVersion <= runtime version`. An older engine never trusts newer
  artifacts; a newer engine within one major accepts older artifacts — safe because all
  breaking changes land in the single 2.0 window
  ([cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)),
  so within a major the engine only grows additively. Failure → same `HED7102`
  treatment.

**Per-request gauntlet** (on every registry hit, in this order — the first failure is
the reported reason):

0. **Marker short-circuit:** an entry with `IsPrecompiled == false` (a fallback-marker
   entry, README D21 — it carries a null-target `FunctionBindings` row and no strategy)
   fails immediately with the build-recorded reason: nothing later in the gauntlet
   could save it, and the recorded cause is the truthful one even when the request's
   options also happen to differ. → `UnsupportedFunction`.
1. **Options:** `OptionsFingerprint` vs the request's effective `TemplateOptions`
   (`Profile`, `ExpressionMode`, `TrimDirectiveLines` — the same effective values the
   dynamic path would put in its cache key). A template precompiled under `Text` is not
   a valid answer for an `Html` request. → `OptionsMismatch`.
2. **Extensions:** every `ExtensionBindings` entry vs the live `TemplateFactory`
   registry: resolve the name (internal `TryGetExtensionType`), match the live type via
   `BindingResolver` (engine default: AQN-sans-version equality). Detects
   `[ExtensionReplace]` overrides and runtime-registered customs whose precompiled
   wiring would silently diverge from a fresh compile. Unresolvable name = mismatch too.
   → `ExtensionBindingMismatch`.
3. **Functions** (README D21): skipped entirely when `options.Functions` is null
   **and** every `FunctionBindings` row targets `BuiltInFunctions` — the request
   compiles against the frozen, immutable `FunctionRegistry.Default`, which cannot
   have diverged from the recorded default bindings. When `options.Functions` is null
   but an **export-bound row** exists, the check fails immediately for that row: the
   frozen `Default` cannot contain the export, so the request's fresh dynamic compile
   would reject the very name the baked code calls — the "host forgot `RegisterFrom`"
   parity violation, reported as `<missing>`. Otherwise, per distinct called name:
   enumerate the live registry's registrations under the name (the internal
   `EnumerateOverloads`, phase 6 amendment); the check passes iff **every** live
   registration's target is a method declared on one of the name's recorded target
   types (matched AQN-sans-version like `BindingResolver`'s default) **and** each
   recorded `(name, target)` row's live overload count equals its recorded
   `OverloadCount`. A live registration from an unrecorded container (a
   different exact-signature replacement — e.g. a host's own `range` from a type the
   build never saw, phase 1 D12's replace-on-exact-signature), a delegate registration
   under a bound name, an added overload, or a recorded target with fewer live
   registrations than recorded (including zero — the bound export was never
   registered) each fail: a fresh dynamic compile could bind differently than the
   baked call, which is exactly the divergence the gauntlet exists to catch. Names the
   template does not call are never checked. → `FunctionBindingMismatch`.
4. **Staleness** — only when `EnableFileChangeCheck` is on: `ContentHash` vs the disk
   file at `Path.Combine(RootPath, key)`, then each `Imports` entry likewise. Verdict
   cached at first lookup; a `FileSystemWatcher` on the root file and every import file
   invalidates the cached verdict (the same watcher model `EnableFileChangeCheck` means
   on the dynamic path — never per-request hashing). Missing file counts as stale.
   → `StaleContent` / `StaleImport`.

All pass → the adapter (`HeddleTemplate` in precompiled mode) — zero parse, zero
compile. Any failure → `PrecompiledMismatchPolicy` (D8): `Fallback` = dynamic path +
`OnFallback` + `HED7101` warning on the recompile result; `Strict` =
`PrecompiledMismatchException(key, reason, detail)`. A registry **miss** bypasses the
gauntlet and the policy entirely (strictness polices divergence, not coverage); the one
miss-time diagnostic is the shadow-index `CaseMismatch` callback (`HED7103`), which is
informational under both policies.

### Reason and detail strings (pinned)

Every `PrecompiledFallbackReason` has one detail-string format. These strings are the
`PrecompiledFallbackEvent.Detail` value, the `{detail}` slot of the `HED7101` message,
and the `Detail` property (and message tail) of `PrecompiledMismatchException` — pinned
here so hosts can parse and tests can assert them verbatim. `{…}` slots are
`string.Format`-style placeholders; literal text is exact, invariant culture, no
trailing period:

| Reason | Diagnostic | Fires | `Detail` format (exact) |
| --- | --- | --- | --- |
| `SchemaVersionUnsupported` | `HED7102` | Registration — once per ignored manifest | `SchemaVersion: manifest={manifestSchema} supported=1` |
| `EngineVersionIncompatible` | `HED7102` | Registration — once per ignored manifest | `EngineVersion: manifest={manifestVersion} runtime={runtimeVersion}` |
| `UnsupportedFunction` | `HED7101` | Per-request gauntlet step 0 — the fallback-marker short-circuit (README D21) | `Function '{name}': not precompiled (no default or exported binding; build warning HED7014)` — `{name}` is the **first** null-target `FunctionBindings` row in manifest order |
| `OptionsMismatch` | `HED7101` | Per-request gauntlet step 1 | `{Field}: manifest={manifestValue} request={requestValue}` — `{Field}` is the **first** differing field in declaration order: `OutputProfile`, `ExpressionMode`, `TrimDirectiveLines` (e.g. `OutputProfile: manifest=Text request=Html`) |
| `ExtensionBindingMismatch` | `HED7101` | Per-request gauntlet step 2 | `Extension '{name}': manifest={recordedTypeName} live={liveTypeName}` — `{liveTypeName}` is `<unresolved>` when the name no longer resolves in `TemplateFactory` |
| `FunctionBindingMismatch` | `HED7101` | Per-request gauntlet step 3 (README D21; runs whenever a checked row exists — an export-bound row is checked even under a null `options.Functions`) | `Function '{name}': manifest={recordedTypeName} live={liveDescription}` — `{recordedTypeName}` is the divergent row's recorded target (the exporting container's AQN sans version, or `Heddle.Runtime.Expressions.BuiltInFunctions, Heddle` for a shim-bound default); `{liveDescription}` is the divergent registration's declaring type as AQN sans version for a `MethodInfo` registration from an unrecorded container, `<delegate>` for a delegate registration under the name, `<overloads added>` when every target matches but a row's live overload count exceeds its recorded `OverloadCount`, or `<missing>` when a recorded target has fewer live registrations than recorded — including zero, the "bound export never registered" case and its bare-`Default`-request variant; the first divergent name in manifest order is the one reported (e.g. `Function 'range': manifest=Heddle.Runtime.Expressions.BuiltInFunctions, Heddle live=Acme.Web.TemplateFunctions, Acme.Web`; `Function 'titlecase': manifest=Acme.Web.TemplateFunctions, Acme.Web live=<missing>`) |
| `StaleContent` | `HED7101` | Per-request gauntlet step 4 (only under `EnableFileChangeCheck`) | `Content: '{path}' hash mismatch` — or `Content: '{path}' missing` when the file is gone (missing counts as stale) |
| `StaleImport` | `HED7101` | Per-request gauntlet step 4, import closure | `Import: '{importKey}' hash mismatch` — or `Import: '{importKey}' missing`; `{importKey}` is the `Imports` entry's key, and the first stale import in manifest order is the one reported |
| `CaseMismatch` | `HED7103` | Lookup miss with a case-insensitive shadow hit — informational, both policies | `Key: requested '{requested}' registered '{actual}'` |

The two registration-time reasons never reach `PrecompiledMismatchException` (`Strict`
polices per-request divergence; an ignored manifest is a registration-time event whose
templates simply stay dynamic — surfaced by the `HED7102` callback). `CaseMismatch` is
never a gauntlet failure either — it annotates a miss, so it throws under neither
policy.

## Schema evolution rules

- `schemaVersion` covers the manifest record shape **and** the generated↔engine calling
  convention (`PrecompiledRuntime` members the generated code calls). Any change to
  either bumps it; the runtime keeps accepting every schema it ever shipped support for,
  or ignores the manifest wholesale — never partial reads.
- Reserved next uses (recorded, not built): schema 2 = phase 8 sink capabilities +
  entry points; the D2 `Replace` flag whenever its trigger fires.
- **schemaVersion-1 contents, clarified after the July 2026 gap round and its OQ1
  resolution:** the `FunctionBindings` section in its direct-binding shape —
  `(name, target, overloadCount)` rows recording actual bound targets, exporting
  containers included — fallback-marker entries (`IsPrecompiled == false`,
  README D21), and `BindDefinition`'s `maxRecursionCount` parameter (README D23) are
  **schemaVersion-1 surface** — this spec is pre-implementation, so v1 ships them from
  the start; no bump. Composition with phase 8's schemaVersion 2 (its D7, via the
  amendments ledger): the v2 delta — u8 twins normative, sink entry points,
  `WritePiece` — is orthogonal; `FunctionBindings` rows, marker-entry semantics, and
  the gauntlet's function step carry into v2 **unchanged**, and v2's sink capability
  flags never appear on a marker entry (no strategy to render through).
- The attribute itself is the stable outermost contract — its constructor signature
  never changes; evolution happens behind `manifestType`.
