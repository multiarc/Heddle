# Phase 2 specification — safe output default (OutputProfile)

Status: **Specified — ready for implementation.**
Roadmap source: [phase 2 — safe output default](../../roadmap/phase-2-safe-output.md).
Prior phases: [phase 1 — native expressions](../phase-1-native-expressions/README.md) is
assumed merged ([D5, sequential order](../common/cross-cutting-decisions.md#d5--sequential-implementation-order));
this spec binds to phase 1's `ExpressionMode`/`Functions` options, the `HEDxxxx`
diagnostic-ID plumbing (`DiagnosticId`, `HeddleDiagnosticIds`, `ToError` overloads), and
the standalone registered-function compilation path (phase 1 D11). This phase claims IDs
from the `HED2xxx` block per
[cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)
and contributes two of the three ratified breaking changes to the
[single 2.0 window](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window).

This spec is a single document — no supplementary files.

## Scope and goal

Introduce `OutputProfile { Text, Html }`. Under `Html`, the unnamed `@(...)` output
HTML-encodes by default — closing the XSS-by-default gap that is the assessment's most
disqualifying finding for the HTML use case — with `@raw(...)` as the explicit trusted-value
opt-out. `Text` keeps today's raw behavior byte-for-byte and remains the 1.x default; the
flip to `Html` and the encoder swap (`WebUtility` → pluggable `HtmlEncoder`) are 2.0-window
items this phase prepares but does not execute.

In scope: the enum and `TemplateOptions` plumbing (including the pre-existing
`ProvideLanguageFeatures` copy-constructor bug fix and the reflection-based copy-constructor
completeness test named in the [coding standards](../common/coding-standards.md#api-design-and-compatibility)),
the effective profile on `CompileContext`, the `CreateExtension` redirect seam, the `raw`
alias, the `@profile()` directive (`ProfileExtension`), the `TemplateResolver` cache-key
fix, the double-encode compile warning, the phase's diagnostics, focused per-profile
encoding tests (paying down the encoding-test debt), and the published-docs updates. No
grammar change (`src/Heddle.Language/generated/` must have no diff).

## Assumed state

Every seam below was **re-verified against the current source** (July 2026). Stale roadmap
claims found during verification are marked ⚠ and corrected in the decision records.

| Seam | Verified state |
| --- | --- |
| [TemplateOptions](../../../src/Heddle/Data/TemplateOptions.cs) | Copy-ctor (lines 35–44, `TemplateOptions(TemplateOptions, string)`) copies `FileNamePostfix`, `RootPath`, `TemplateName`, `EnableFileChangeCheck`, `AllowCSharp`, `MaxRecursionCount`, `Data` — **`ProvideLanguageFeatures` is missing**, exactly as the roadmap states. `Equals` compares only `FileNamePostfix`/`TemplateName`/`RootPath`; `GetHashCode` hashes `TemplateName` only. Phase 1 (merged) additionally adds `ExpressionMode` and `Functions` (both copied in the copy-ctor, neither in `Equals`) and turns `AllowCSharp` into a bridge over `ExpressionMode` — phase 1's work items do **not** fix the `ProvideLanguageFeatures` omission, so that fix lands here (D7). |
| [CompileContext](../../../src/Heddle/Runtime/CompileContext.cs) | Private copy-ctor (line 73) builds child options via `Options = new TemplateOptions(context.Options, fileName)` (line 82) — child compiles (bodies, partials, imports) inherit whatever the options copy-ctor copies. Two public ctors (lines 87, 102). No profile state exists. `ScopeType`/`RootScopeType` are the precedent for compile-time mutable per-context state (the `@model` pattern). |
| [HeddleCompiler.CreateExtension](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Private, line 436. Non-definition branch calls `TemplateFactory.Create(extensionItem.ExtensionName, …)` at line 468 — the roadmap's "~line 468" checks out exactly. `InitializeTemplate` (line 550) computes `RenderType` from `[EncodeOutput]`/`[NotEncode]` at lines 556–558 and calls `extension.SetUpRenderType(...)`. |
| [HeddleCompiler.CompileItem](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Line 254. The chain-parameter branch (`else`, lines 337–343) compiles nested chains via `CompileParameterChain` and wraps them in `ChainedParameter` — the double-encode warning site (D9). Phase 1's standalone registered-function branch (its D11) creates the value carrier via `TemplateFactory.Create("")` — a second unnamed-carrier site this phase must redirect (D3). |
| [HeddleCompiler.Compile](../../../src/Heddle/Runtime/HeddleCompiler.cs) | Iterates `parseContext.OutputChains` in document order (line 32), then `DefaultChains`; each chain's items compile **reversed** (line 36), so the right neighbor is compiled before the leftmost sink. A chain whose threaded return type ends `null` is removed from the document via `RemoveEmptyItem` (line 55–58) — the directive-removal mechanism the roadmap names, verified. |
| [TemplateChain](../../../src/Heddle/Runtime/TemplateChain.cs) / [EmptyParameter](../../../src/Heddle/Runtime/Parameters/EmptyParameter.cs) | `RenderData` executes items rightmost-first; only the **last** list item (= leftmost source call) renders; each item receives the previous result as `ChainedData` (`scope.Chain(result)`), and its `ModelData` comes from **its own parameter**. ⚠ Consequence: in `@(X):html()` the `html()` result lands in the bodiless unnamed sink's `ChainedData`, which `EmptyExtension`/`EmptyHtmlExtension` ignore — the roadmap's double-encode construct is corrected in D9. |
| [EmptyExtension](../../../src/Heddle/Extensions/EmptyExtension.cs) | `[ExtensionName("")]`, derives `AbstractExtension` (never consults `DirectRender` — confirming the roadmap's "why not force `RenderType.Encode`" note). Bodiless: renders `ModelData` string / `ToString()`, nothing for `null`. With a body (`InnerExist`): renders the body **raw** — `@(X){{…}}` is a rescoping container (D3 conditions the redirect on this). |
| [EmptyHtmlExtension](../../../src/Heddle/Extensions/EmptyHtmlExtension.cs) | `[ExtensionName("html")]` + `[EncodeOutput]`, derives [AbstractHtmlExtension](../../../src/Heddle/Core/AbstractHtmlExtension.cs): `ProcessData` returns `WebUtility.HtmlEncode(...)` of the inner result; `RenderData` proxies the renderer through [HtmlEncodedRenderer](../../../src/Heddle/Data/HtmlEncodedRenderer.cs) (also `WebUtility.HtmlEncode`). No `[DataType]`/`[ChainedType]` restrictions; `InitStart` is inherited, so the threaded return type (`string`) matches `EmptyExtension` — the redirect is type-transparent to chains. These two call sites are the **only** `WebUtility.HtmlEncode` uses in the engine (verified by search) — the 2.0 encoder swap touches exactly them. |
| [TemplateFactory](../../../src/Heddle/Runtime/TemplateFactory.cs) | Static registry `Dictionary<string, Type>` (ordinal, case-sensitive). `LoadExtensions` (line 167) yields one entry **per** `[ExtensionName]` attribute and [ExtensionNameAttribute](../../../src/Heddle/Attributes/ExtensionNameAttribute.cs) is `AllowMultiple = true` — multi-name is supported, as the roadmap claims. Registered names verified: `""`, `html`, `date`, `time`, `int`, `money`, `guid`, `string`, `list`, `if`, `ifnot`, `for`, `out`, `swap`, `param`, `partial`, `import`, `using`, `model` — **`raw` and `profile` are both free**. A colliding `[ExportExtensions]` assembly hits `TemplateOverrideException` at static-init time (line 105). |
| [ExtensionNameAttribute](../../../src/Heddle/Attributes/ExtensionNameAttribute.cs) | ⚠ Its XML doc claims `""` "is reserved for `EmptyHtmlExtension`" — false today (it is `EmptyExtension`); fixed in WI9 to describe the profile-dependent resolution. |
| [NotEncodeAttribute](../../../src/Heddle/Attributes/NotEncodeAttribute.cs) | `AttributeUsage(AttributeTargets.Property)` — but its only consumer is `InitializeTemplate`'s check on the extension **class** (`HeddleCompiler.cs` line 557), which can never be true. **Dead code confirmed** (D12); [built-in-extensions.md](../../built-in-extensions.md) lines 316–318 and [custom-extensions.md](../../custom-extensions.md) line 160 document it as working — both corrected in WI9. |
| [TemplateResolver](../../../src/Heddle/Runtime/TemplateResolver.cs) | `TemplatesCache` is `Dictionary<string, HeddleTemplate>` keyed by full path (`StringComparer.OrdinalIgnoreCase`), probed in `GetTemplate` (line 37) and the private `Search` (line 133), written in `Create` (line 158). It builds `TemplateOptions` at lines 42–47, 67–73, 79–85 and never sets a profile. [ITemplateResolver](../../../src/Heddle/Runtime/ITemplateResolver.cs) fixes the `GetTemplate`/`Search`/`Create` signatures — the profile threads through internals, not the interface (D8). `DocumentsCache` ([DocumentsCache.cs](../../../src/Heddle/Runtime/DocumentsCache.cs)) is entirely commented out — **dead code confirmed**, untouched. |
| Directive extensions | [ModelExtension](../../../src/Heddle/Extensions/ModelExtension.cs), [UsingExtension](../../../src/Heddle/Extensions/UsingExtension.cs), [PartialExtension](../../../src/Heddle/Extensions/PartialExtension.cs), [ImportExtension](../../../src/Heddle/Extensions/ImportExtension.cs) all read their argument from the **body** (`GetInnerResult(Scope.Null)` over `@x(){{value}}`). `model`/`using`/`import` return `null` from `InitStart` so the block is removed; `partial` reads the child-template name the same way but returns `typeof(string)` and renders the child template's output in place — the child compiles from a copied `CompileContext` (`PartialExtension.InitStart` → `new CompileContext(context, …)` into `AddDelayedCompileTemplate`), which is the lineage seam D4 and corpus row X05 bind to. ⚠ No mechanism lets `InitStart` read a paren argument (`@model(My.Type)` would be compiled as a member path by `CompileModelAccessor` and fail property resolution before the extension exists) — the roadmap's `@profile(html)` paren form is corrected in D2. |
| Containers | `list`, `if`, `ifnot`, `for`, `out`, `swap`, `param` carry no `[EncodeOutput]` (verified by attribute search) → `RenderType.Raw`; they forward bodies/chained values verbatim, so encoding happens exactly once at the `[EncodeOutput]` leaf. `@out()` splices its chained value without re-encoding. |
| Phase 1 artifacts used | `TemplateOptions.ExpressionMode`/`Functions` (copy-ctor covered by phase 1), `HeddleCompileError.DiagnosticId` + [HeddleDiagnosticIds](../phase-1-native-expressions/README.md#public-api-contract) + `ToError(position, diagnosticId)` overloads, `HED0001`–`HED0003` claimed, `ConstantParameter` folding for literal-only native expressions, and the standalone-function carrier (`TemplateFactory.Create("")` + function-typed `ExType`). |

## Design decisions

Numbering is local to this spec; cross-cutting decisions are cited by link. Every roadmap
lean, ratified item, and "decide" note for this phase is closed below.

### D1 — Profile selection is programmatic-only

**Decision.** The effective profile comes from exactly two places: the host
(`TemplateOptions.OutputProfile`, default `Text` in 1.x) and the template
(`@profile()` directive). File extensions carry no semantics — no `.heddle.html`-style
inference, ever; a host wanting extension-driven profiles maps extensions to options in its
own resolver wiring (documented in `csharp-api.md`).
**Rationale.** Maintainer-ratified (July 2026, recorded in the roadmap). File naming is
presentation, not configuration; inference would also make `TemplateResolver` cache
identity depend on parsing file names.
**Alternatives rejected.** Flask-style extension inference (couples behavior to naming
conventions Heddle never had; trivially reproducible host-side); a global static default
(a `HeddleDefaults.OutputProfile`-style process global — mutable parallel state consulted
behind the host's back, racy across hosts in one process, and a second migration surface;
its 1.x-valve variant is the maintainer-ratified rejection recorded in D11).

### D2 — `@profile()` takes its value from the body: `@profile(){{html}}`

**Decision.** The directive's canonical (and only) form is the body form —
`@profile(){{html}}` / `@profile(){{text}}` — exactly like `@model(){{Type}}` and
`@using(){{Namespace}}`. The value is read in `InitStart` via `GetInnerResult(Scope.Null)`,
trimmed, and matched ordinal-case-insensitively against the strings `"text"` and `"html"`
(explicit comparison, **not** `Enum.TryParse`, which would also accept numeric strings like
`"1"`). Empty, whitespace-only, or unmatched values → error `HED2001`. On success the
extension sets `initContext.CompileScope.CompileContext.OutputProfile` and returns `null`,
so the block is removed via the verified `RemoveEmptyItem` path.
**Rationale.** ⚠ This corrects the roadmap's claim that `InitStart` can read the
`@profile(html)` paren form: verified against `CompileItem`/`CompileModelAccessor`, a paren
argument is compiled as a member path **before** the extension is created (producing
`HED0001` "Property html not found in Type […]" on typed scopes) and `InitStart` receives
only the body text (`InitContext.ParameterTemplate`) — no seam exposes the call parameter
to the extension at compile time. Every existing compile-time directive uses the body form,
so this is the more consistent surface, not a compromise.
**Alternatives rejected.** A compiler special case for `@profile(…)` paren text (breaks the
one-class-per-extension pattern for cosmetics); a new marker-interface seam handing raw
parameter text to extensions (a new open point with a single consumer — precedence rule 5
of the [standards](../common/coding-standards.md#precedence-when-principles-conflict);
phase 5's props design is the natural revisit point); `Enum.TryParse(ignoreCase: true)`
(accepts `"0"`/`"1"` and any future enum member silently).

### D3 — The redirect seam: bodiless unnamed calls resolve `"html"` under `Html`

**Decision.** In `HeddleCompiler`, unnamed-carrier resolution becomes one private helper:

```csharp
private static string UnnamedCarrierName(OutputItem item, CompileContext context)
{
    if (!string.IsNullOrEmpty(item.ParameterTemplate))
        return string.Empty;                       // @(X){{…}} stays the raw rescoping container
    context.UnnamedOutputCompiled = true;          // D13 tracking
    return context.OutputProfile == OutputProfile.Html ? "html" : string.Empty;
}
```

used at **both** unnamed-carrier sites: (a) `CreateExtension`'s non-definition branch
(line 468) when `extensionItem.ExtensionName.Length == 0`, and (b) phase 1's standalone
registered-function branch, which currently calls `TemplateFactory.Create("")` — so
`@upper(Name)` encodes its result under `Html` exactly like the equivalent
`@(upper(Name))`. `TemplateFactory`'s process-global registry is not touched. The redirect
reuses the entire proven `[EncodeOutput]` → `InitializeTemplate` → `DirectRender` →
`WebUtility.HtmlEncode` pipeline; both extensions thread `string` as their return type, so
chains and `ExType` flow are unaffected.
**Rationale.** The roadmap's redirect design, verified: forcing `RenderType.Encode` on
`EmptyExtension` cannot work because `AbstractExtension` never consults `DirectRender` —
only `AbstractHtmlExtension` does. The **bodiless condition is a refinement of the roadmap**
forced by verified `EmptyExtension` semantics: `@(X){{…}}` renders its body (a rescoping
container), and redirecting it to `EmptyHtmlExtension` would encode the body's **static
markup** (the `HtmlEncodedRenderer` proxy encodes everything the body writes) — violating
the phase's own container-forwarding rule. With the condition, unnamed leaves *inside* such
bodies still redirect via their own compilation. Covering the function-carrier site keeps
the value-output rule uniform: "a value emitted through the unnamed carrier encodes under
`Html`", regardless of which tier produced the value.
**Alternatives rejected.** Mutating the `TemplateFactory` registry per profile (roadmap:
racy, process-global, cross-host); unconditional redirect including bodied calls (encodes
static markup in `@(X){{…}}` bodies — a correctness break); redirecting only top-level
chain sinks (leaves nested unnamed producers raw — non-uniform, and unreachable knowledge
in `CompileItem`).

### D4 — Effective profile lives on `CompileContext`; `@model`-style scoping

**Decision.** `CompileContext` gains `public OutputProfile OutputProfile { get; set; }`,
initialized to `Options.OutputProfile` in both public ctors and copied from
`context.OutputProfile` (the **effective**, possibly directive-flipped value — not the
options value) in the private copy-ctor. `@profile()` mutates only the `CompileContext`,
never the host's `TemplateOptions` instance (held by reference — mutating it would be an
observable host side effect). Scoping therefore matches `@model`/`@using` exactly: the flip
affects items compiled **after** the directive in document order within the same context,
and every child context created afterwards (bodies via `AbstractExtension.InitSubTemplate`,
partials via `PartialExtension`, imports parse into the same context so their blocks follow
document order). A flip inside a body affects that body's subtree only. Definition default
chains (`-> chain`) compile after all output chains, so any directive precedes them.
**Rationale.** Roadmap design, with the lineage mechanics pinned to the verified
`CompileContext` copy chain (line 82) — this is the same inheritance route `@model` uses
for `ScopeType`, so the mental model is one rule, not two.
**Alternatives rejected.** Profile on `CompileScope` (a thin façade over `CompileContext`;
state belongs on the context that children copy); mutating `Options.OutputProfile` (host
side effect; also breaks `Equals`-participating identity mid-compile).

### D5 — `raw` is a second name on `EmptyExtension`, registered under both profiles

**Decision.** `EmptyExtension` gains `[ExtensionName("raw")]` alongside `[ExtensionName("")]`.
Verified: the registry yields one entry per attribute (`AllowMultiple = true` +
`LoadExtensions`' per-attribute loop), and `raw` is unregistered today. Because the D3
redirect keys on the resolved name `""`, `@raw(...)` always instantiates `EmptyExtension` —
verbatim passthrough under `Html`, a harmless alias under `Text`. `@raw(X){{…}}` behaves
exactly like today's `@(X){{…}}` (raw rescoping container).
**Rationale.** The trusted-value opt-out is the ecosystem convention (Razor
`HtmlString`/`Html.Raw`, Handlebars `{{{ }}}`, Jinja `|safe`, Phoenix `raw/1` — grounding
carried from the roadmap); one attribute line reuses everything.
**Alternatives rejected.** A separate `RawExtension` class (duplicate of `EmptyExtension`
with no divergent behavior — DRY violation with zero payoff); registering `raw` only under
`Html` (registry is process-global and profile-agnostic by D3; conditional registration is
exactly the race the roadmap rules out).

### D6 — Options identity: `OutputProfile` joins `Equals`/`GetHashCode`; phase 1's exclusions stand

**Decision.** `TemplateOptions.Equals` additionally compares `OutputProfile`;
`GetHashCode` becomes `((TemplateName?.GetHashCode() ?? 0) * 397) ^ (int) OutputProfile`.
`ExpressionMode`, `Functions`, and `AllowCSharp` stay **out** of identity.
**Rationale.** The profile changes rendered output for identical inputs, so any
options-keyed cache must distinguish it — the same reasoning that keys the resolver cache
(D8). ⚠ The roadmap's parenthetical "plus `AllowCSharp`/`ExpressionMode` for consistency"
is rejected as a reconciliation with merged phase 1, whose D6 explicitly decided those
properties do not participate (changing shipped `Equals` semantics again in the very next
phase would be a second observable behavior change for hosts, with no keying scenario
needing it — expression mode selects *how* a template compiles, not *what* identical
compiles produce; the profile changes bytes).
**Alternatives rejected.** Adding all option properties to `Equals` (turns every host
options tweak into a cache miss and re-breaks `Equals` users each phase); leaving
`OutputProfile` out and relying on the resolver's string key alone (any *other*
options-keyed cache a host builds would silently collide — the exact defect class this
phase fixes in the resolver).

### D7 — Copy-constructor: fix `ProvideLanguageFeatures` here; reflection completeness test

**Decision.** The copy-ctor gains `ProvideLanguageFeatures = value.ProvideLanguageFeatures;`
and `OutputProfile = value.OutputProfile;`. **Reconciliation with phase 1:** its spec (WI3,
D6) adds only `ExpressionMode` and `Functions` to the copy-ctor and never mentions
`ProvideLanguageFeatures` — the roadmap's bug fix is therefore still outstanding and lands
in this phase, as the [coding standards](../common/coding-standards.md#api-design-and-compatibility)
already record ("phase 2 adds a reflection-based completeness test"). That test,
`TemplateOptionsCompletenessTests` (new, `src/Heddle.Tests/TemplateOptionsCompletenessTests.cs`):
for **every** public instance property of `TemplateOptions` with a setter, on a fresh
instance, set a synthesized non-default value (`bool` → `true`; `string` → `"probe-" + name`;
`int` → current + 41; enums → a defined value different from the current one; known
reference types via a factory map, e.g. `Functions` → `new FunctionRegistry()`, `Data` →
`new object()`; an **unknown property type fails the test with "add a synthesizer"** — that
failure *is* the omission guard for future types), run `new TemplateOptions(source)`, and
assert the copy's getter returns an equal value (reference-equal for reference types — the
copy is shallow). Get-only computed properties (`FullPath`) are skipped; `TemplateName`
(get-only, ctor-set) has a dedicated fact asserting it copies when no override name is
passed. A second dedicated fact pins `ProvideLanguageFeatures` round-trip by name
(roadmap success criterion 6). Derived properties are safe in the loop: setting
`AllowCSharp = true` flips `ExpressionMode`, which is copied, so the bridge reads back
`true` — one property per fresh instance avoids interference.
**Rationale.** Reflection makes the invariant self-extending: any later phase adding an
options property inherits the guard for free, exactly as the standards promise.
**Alternatives rejected.** A hand-maintained property list (the omission bug, re-created as
a test); comparing via `Equals` (identity deliberately excludes most properties — D6).

### D8 — Resolver: additive constructor + profile-suffixed cache key

**Decision.** `TemplateResolver` gains a **new** constructor
`public TemplateResolver(string rootPath, bool checkFileChange, OutputProfile defaultProfile)`;
the existing `(string, bool = false)` ctor remains and chains to it with
`OutputProfile.Text` (adding a defaulted third parameter to the existing ctor would be a
binary breaking change — compiled callers bind the two-parameter signature). The field
`_defaultProfile` is applied to every `TemplateOptions` the resolver builds (the three
initializer sites). Cache identity: one private helper
`static string CacheKey(string fullPath, OutputProfile profile) => fullPath + "|" + profile;`
used for every probe and write. The effective profile per operation is
`context?.Options.OutputProfile ?? _defaultProfile`, computed in `GetTemplate` and threaded
to a **private** `Search(viewName, controllerName, locations, profile, out …)` overload;
the public `Search` (fixed by `ITemplateResolver`) keeps its signature and forwards
`_defaultProfile`. `Create(viewName, context)` writes under
`CacheKey(Path.Combine(_rootPath, viewName), context.Options.OutputProfile)` — probe and
write therefore always agree, including the `PartialView`-with-context path where creation
uses the caller's context rather than resolver-built options. `RemoveFromCache` is
value-based and needs no change. `DocumentsCache` is dead code (verified — the entire file
is commented out) and stays untouched.
**Rationale.** A wrong key silently serves cross-profile output — a correctness *and*
security defect (an `Html` host receiving the `Text`-compiled raw template). The string
suffix keeps the existing `Dictionary<string, HeddleTemplate>` and its
`OrdinalIgnoreCase` comparer (enum `ToString()` has fixed casing, so case-insensitivity is
harmless). Between the roadmap's two options (profile parameter vs prototype options), the
profile parameter wins: it is the only field the resolver needs, and a prototype-options
seam would invite divergence between prototype fields and the resolver's own
per-template fields (`FileNamePostfix`, `RootPath`).
**Alternatives rejected.** Defaulted parameter on the existing ctor (binary break, per the
[library breaking-change rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes));
a prototype `TemplateOptions` ctor parameter (wider seam, one consumer field — YAGNI;
revisit trigger in [Deferred items](#deferred-items)); a composite `(string, OutputProfile)`
tuple key (allocates no less, changes the dictionary type, and complicates
`RemoveFromCache` for zero gain); changing `ITemplateResolver` (public interface —
binary-compat rule for shipped interfaces).

### D9 — Double-encode warning: the construct is `@(html(X))`, not `@(X):html()`

**Decision.** Warning `HED2003` triggers when a **bodiless unnamed** item compiles under
effective `Html` and its parameter is a **nested chain** whose final producer (the
leftmost call of the parenthesized chain — `CallParameter.ChainParameter[0]`, the last
`TemplateItem` added to the compiled `TemplateChain`) resolves to an extension whose type
carries `[EncodeOutput]` (via the existing `IsHaveAttribute<EncodeOutputAttribute>(true)`
check). The check sits in `CompileItem`'s chain-parameter branch right after the
`ChainedParameter` is built; the warning is positioned at the nested producer's
`OutputItem.Position` and names it. Output behavior is not changed — the value renders
double-encoded, as the message says.
**Rationale.** ⚠ This corrects the roadmap's construct with source evidence: in the
top-level chain `@(X):html()`, `TemplateChain.RenderData` hands `html()`'s result to the
sink as `ChainedData`, and both `EmptyExtension` and `EmptyHtmlExtension` ignore
`ChainedData` when bodiless — the `html()` output is *discarded*, under both profiles,
today and after this phase. No double encoding occurs there (and when the sink has a body,
the D3 redirect does not apply, so `…{{@out()}}` splices the chained, once-encoded value —
also correct). The construct that genuinely double-encodes is the nested chain parameter:
`@(html(X))` computes the encoded string, feeds it to the sink as **ModelData**, and the
redirected sink encodes it again. The roadmap's mechanism note ("the chain compiles
reversed, so the compiler can see the right neighbor") remains true and is what makes the
nested check one dictionary-free type test.
**Alternatives rejected.** Warning on top-level `@(X):html()` (that pattern is inert —
pre-existing, profile-independent, unchanged by this phase; flagging useless chain
producers in general is a phase 6 lint, recorded in [Deferred items](#deferred-items));
suppressing the second encode silently (a value-shape behavior change keyed on attribute
sniffing — surprising and irreversible; the warning keeps behavior explainable).

### D10 — Encoding semantics per value category (phase 1 interaction)

**Decision.** Encoding is a **render-time leaf concern**, applied exactly once by the
resolved carrier, uniformly across value tiers. The pinned rules, all funneling through
`EmptyHtmlExtension`'s existing pipeline:

| Value category | Under `Html`, bodiless unnamed carrier |
| --- | --- |
| Member path (`@(Name)`) | String values encoded verbatim; non-string values `ToString()` (current-culture, today's behavior) then encoded; `null` renders nothing. |
| Native expression (`@(Price * Quantity)`, phase 1 `CompiledParameter`) | The boxed result follows the same rule — `ToString()` then encode. Operators run **before** encoding (they act on values, never on encoded text). |
| Folded literal (`@("a" + "<b>")` → phase 1 `ConstantParameter`) | The constant is delivered to the carrier at render time and encoded there — **no compile-time pre-encoding** (pre-encoding would bake encoder knowledge into compiled documents and complicate the 2.0 encoder swap; recorded as a deferred optimization). |
| Registered-function result (`@(upper(Name))` and standalone `@upper(Name)`) | Encoded — the standalone form uses the same carrier via D3's helper. Function bodies never receive encoded text. |
| C# tier result (`@( @Model.X )`, Roslyn `CompiledParameter`) | Same rule as native expressions. |
| Chained value into `@out()` / containers (`list`/`if`/`ifnot`/`for`/`swap`/`param`) | Forwarded raw (verified: no `[EncodeOutput]` on any container) — encoding happens only at the emitting leaf, so nesting depth never multiplies encoding. |
| Static template text | Unchanged under both profiles — literal markup is written verbatim by the renderer; the profile touches only values emitted through the unnamed carrier, never document text. |
| `@raw(...)` | Unchanged under both profiles — always `EmptyExtension` (D5): bodiless renders the value verbatim, bodied is the raw rescoping container, exactly like today's `@(X){{…}}`. |
| Named `[EncodeOutput]` extensions — the **full set**, verified by attribute search: `html` (`EmptyHtmlExtension`), `string` (`StringExtension`), `date` (`DateExtension`), `time` (`TimeExtension`), `int` (`IntegerExtension`), `money` (`MoneyExtension`); all derive `AbstractHtmlExtension` | Unchanged under both profiles — they already encode through the existing `DirectRender` pipeline; the profile neither adds a second encode nor removes the first. |
| Named raw value extension `guid` (`GuidExtension` — the one value formatter with **no** `[EncodeOutput]`, derives `AbstractExtension`) | Unchanged under both profiles — renders `Guid.ToString(format)` raw; every GUID format's output alphabet (hex digits, dashes, braces, parentheses, commas, `0x` prefixes) contains no HTML-significant character, so raw is safe by construction. |
| Directives `model`/`using`/`import`/`profile` | Emit nothing — their blocks are removed at compile time, so there is nothing to encode. `partial` renders its child template's output in place; encoding decisions happen **inside** the child under its own inherited profile (D4) and are never re-applied by the parent. |

**Rationale.** "Encode at the point of output" is the standard guidance
([Prevent XSS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting));
one rule at one seam means phase 1's tiers need zero knowledge of profiles — the
interaction surface is exactly the carrier-name resolution of D3.
**Alternatives rejected.** Type-sensitive skipping (e.g. never encode numeric results —
custom `ToString()` overrides can emit markup; a non-uniform rule is a documentation and
audit burden for negligible gain given `TextEncoder`'s no-op fast path in 2.0);
compile-time pre-encoding of folded constants (above).

### D11 — 1.x prepares, 2.0 executes: default flip and encoder swap

**Decision.** In this phase (1.x): the default stays `Text` (initialized explicitly in both
`TemplateOptions` ctors so the 2.0 flip is a two-line, greppable change); encoding stays
`WebUtility.HtmlEncode`, confined to its two verified call sites
(`AbstractHtmlExtension.ProcessData`, `HtmlEncodedRenderer.Render`) — no new call site may
be introduced; **no** `TemplateOptions.Encoder` property ships now (no speculative knobs);
**no 1.x migration valve** ships (maintainer-ratified: no `HeddleDefaults.OutputProfile`
global — one documented migration at 2.0, no parallel global state). The focused encoding
assertions this phase adds (the [encoding-pin rows E01–E14](#testing-plan)) pin the exact
`WebUtility` byte behavior — the five entities (`<` → `&lt;`, `>` → `&gt;`, `"` → `&quot;`,
`&` → `&amp;`, `'` → `&#39;`), U+00A0–U+00FF and astral surrogate pairs as **decimal**
numeric references, and everything else passing through raw (`+`, control characters
including tab/CR/LF, and all BMP text ≥ U+0100) — so the 2.0 swap's
golden churn is *measured*, not discovered. In the 2.0 window (executed then, specified
here as the contract this phase must not obstruct): the default flips to `Html`; the two
call sites switch to a pluggable `System.Text.Encodings.Web.TextEncoder` defaulting to
`HtmlEncoder.Create(UnicodeRanges.All)` (HTML-significant characters still encode;
non-Latin text stays readable, containing golden churn vs `HtmlEncoder.Default`'s
Basic-Latin-only safe list); `TemplateOptions.Encoder` arrives to pin legacy or custom
encoders; `System.Text.Encodings.Web` is inbox on `net6.0+` and a package reference for
`netstandard2.0`. Known byte deltas at the swap — enumerated per character in the E-table,
whose third column is the sanctioned churn budget:

- `'`: `&#39;` → `&#x27;` (decimal → hex form; E05).
- `+`: raw → `&#x2B;` (starts encoding — UTF-7-attack hardening; E06).
- U+00A0–U+00FF: the defined letters/symbols of the band **stop** encoding (`&#233;` → raw
  `é` — `UnicodeRanges.All` allows them; E08); the band's one Zs member U+00A0 (NBSP) stays
  encoded but changes form, `&#160;` → `&#xA0;` (E07).
- Control characters (category Cc — tab, CR, LF included): raw → hex references
  (`&#x9;`/`&#xD;`/`&#xA;`), because `ForbidUndefinedCharacters` keeps Cc outside every
  safe list regardless of ranges — **multi-line string values are golden churn** (E12, E13).
- Astral pairs: decimal → hex form (`&#128512;` → `&#x1F600;` — both encoders always
  encode supplementary code points; E11).
- Byte-identical across the swap: `<` `>` `&` `"` (same named entities; E01–E04) and all
  defined BMP text outside the sets above — `Ā`, `中`, `=`, `/`, the rest of Basic Latin
  (E09, E10, E14).

**Rationale.** [Cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)
bundles both into one migration; keeping 1.x encoder-agnostic (redirect resolves a *name*,
not an encoder) means the swap later touches two lines plus options plumbing.
**Alternatives rejected.** Executing the flip or the encoder swap in 1.x (a breaking
change outside the ratified window — cross-cutting D2; existing `@html`/`@string` outputs
would change bytes in a minor release); `HtmlEncoder.Default` as the 2.0 default (its
safe list is Basic Latin only, so every non-Latin character becomes a numeric reference —
unreadable non-English output and unbounded golden churn for zero security gain over
`Create(UnicodeRanges.All)`, which still encodes every HTML-significant character);
keeping `WebUtility.HtmlEncode` beyond 2.0 (not pluggable; string/TextWriter surface only —
no span/UTF-8 `Encode` overloads for phase 8's sink; fixed Latin-1 numeric-reference
behavior rather than a widenable safe-list design); shipping `TemplateOptions.Encoder`
already in 1.x (a speculative knob — both 1.x profiles run the fixed `WebUtility` path, so
no spec scenario needs a second value; the standards'
[no-speculative-knobs rule](../common/coding-standards.md#yagni-applied)); the 1.x
migration valve (`HeddleDefaults.OutputProfile` global) — maintainer-ratified out, as
recorded in the decision text.
**Grounding.** [WebUtility.HtmlEncode](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode)
and the exact character set in [WebUtility.cs](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Net/WebUtility.cs)
(five-entity switch, `char.IsBetween(ch, (char)160, (char)255)` decimal references,
`Rune`-paired astral decimal references, everything else passes);
[HtmlEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.htmlencoder)
(`Create(UnicodeRange[])`/`Create(TextEncoderSettings)`, string/TextWriter/span/UTF-8
overloads); the [DefaultHtmlEncoder source](https://github.com/dotnet/runtime/blob/release/5.0/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/HtmlEncoder.cs)
(`ForbidHtmlCharacters` = `<` `>` `&` `'` `"` `+` in every safe list; four named entities,
all other encodes as `&#xHEX;`; `ForbidUndefinedCharacters` — "includes categories Cc, Cs,
Co, Cn, Zs [except U+0020 SPACE], Zl, Zp"; supplementary code points always encode) and
the [defined-character list generator](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Encodings.Web/tools/GenDefinedCharList/Program.cs)
(allowed general categories: letters, marks, numbers, punctuation, symbols, Cf minus
U+FEFF); default-encoder safe
list "limited to the Basic Latin Unicode range" and `HtmlEncoder.Create(allowedRanges: …)`
widening ([Prevent XSS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting), re-verified July 2026).

### D12 — `[NotEncode]` is dead code: docs claim removed, attribute retained

**Decision.** The documentation claim that `[NotEncode]` marks a model property as
pre-trusted is **removed** in WI9 (`built-in-extensions.md` "HTML encoding" section,
`custom-extensions.md` attribute table). The attribute type itself ships unchanged
(removing a public type is a binary break; sealing its fate belongs to a ratified window).
Its potential per-property meaning is re-evaluated when phase 5 introduces typed props.
**Rationale.** Verified dead: `AttributeUsage(AttributeTargets.Property)` versus a check
performed only on extension **classes** (`HeddleCompiler.cs:557`) — the condition is
unsatisfiable, so the documented behavior has never existed. The roadmap's lean ("remove
claim now; revisit with props in phase 5") is confirmed and adopted.
**Alternatives rejected.** Implementing per-property encoding suppression now (needs
property-level render metadata that only phase 5's typed props design can place cleanly;
also widens the trusted-output surface in the same release that introduces safe-by-default —
security precedence says no); deleting the attribute type (public API removal outside the
2.0 window).

### D13 — The directive-position warning ships (`HED2002`)

**Decision.** The roadmap's "optional compile warning" is implemented. `CompileContext`
gains `internal bool UnnamedOutputCompiled`, set by D3's helper whenever a bodiless unnamed
carrier is resolved (either site, both profiles — a later `@profile(){{html}}` after a
`Text`-compiled `@(X)` must warn too). `ProfileExtension` emits `HED2002` when the flag is
already set, then **still applies** the flip (matching the documented "affects output
compiled after it" semantics). The flag is per-`CompileContext` and deliberately **not**
copied to child contexts: the warning scope is "earlier output in the same template scope";
an unnamed output inside an earlier body belongs to that body's context.
**Rationale.** The failure mode (top half of a page raw, bottom half encoded) is silent and
security-relevant; a positioned warning with a `Fix` string is cheap. Per-context tracking
is one bool with zero sharing hazards.
**Alternatives rejected.** No warning (silent mixed-profile pages); an error (mid-file
flips are legitimate — e.g. a `Text` email template embedding an HTML section); a
document-global flag threaded through child contexts (an `ObjectReference<bool>` plumbing
exercise to catch a strictly less accurate scope).

## Implementation plan

Work items in execution order; each runs the
[canonical loop](../common/testing-standards.md#the-canonical-loop). "Check" is the
completion gate.

**WI1 — Failing executable spec (security first).**
Files: `src/Heddle.Tests/OutputProfileEncodingTests.cs` (the profile × construct matrix
M01–M15, the XSS corpus X01–X12, and the encoding-pin rows E01–E14 from the
[testing plan](#testing-plan), written **before** the
redirect exists), `src/Heddle.Tests/TemplateOptionsCompletenessTests.cs` (D7),
`src/Heddle.Tests/ProfileDirectiveTests.cs`, `src/Heddle.Tests/TemplateResolverProfileTests.cs`,
`src/Heddle.Tests/DoubleEncodeWarningTests.cs`, plus fixtures
`profile-flagship.heddle`, `profile-directive.heddle`, `profile-partial-parent.heddle`,
`profile-partial-child.heddle` under `src/Heddle.Tests/TestTemplate/` (goldens captured in
WI8). Check: everything compiles; all new tests fail for the right reason (raw output,
missing members, unknown extension `profile`).

**WI2 — `OutputProfile` + `TemplateOptions` (D6, D7).**
Files: `src/Heddle/Data/OutputProfile.cs` (new enum),
`src/Heddle/Data/TemplateOptions.cs` (property + explicit `OutputProfile.Text` init in both
ctors; copy-ctor gains `ProvideLanguageFeatures` and `OutputProfile`; `Equals`/`GetHashCode`
per D6). Check: `TemplateOptionsCompletenessTests` and the options rows of
`OutputProfileOptionsTests` green; full existing suite green (identity change verified
against in-repo usages — none key on `TemplateOptions`).

**WI3 — `CompileContext` plumbing (D4, D13).**
Files: `src/Heddle/Runtime/CompileContext.cs` — `public OutputProfile OutputProfile { get; set; }`
(init in both public ctors from `Options.OutputProfile`; private copy-ctor copies
`context.OutputProfile`), `internal bool UnnamedOutputCompiled { get; set; }` (not copied).
Check: `OutputProfileOptionsTests` context rows green (init, copy, child-context snapshot
ordering).

**WI4 — Redirect + `raw` alias (D3, D5).**
Files: `src/Heddle/Runtime/HeddleCompiler.cs` (add `UnnamedCarrierName` per D3; use it at
the `CreateExtension` line-468 site and at phase 1's standalone-function carrier site),
`src/Heddle/Extensions/EmptyExtension.cs` (`[ExtensionName("raw")]` added). Check: XSS
corpus rows X01, X02, X09, X11 green; matrix rows for `Text` byte-identical to pre-phase
behavior; `@raw` resolves under both profiles.

**WI5 — `ProfileExtension` (D2, D13; diagnostics HED2001/HED2002).**
Files: `src/Heddle/Extensions/ProfileExtension.cs` (new — `[ExtensionName("profile")]`,
`AbstractExtension`; `InitStart` mirrors `ModelExtension`: null-guard, `base.InitStart`,
`GetInnerResult(Scope.Null)`, trim, match `"text"`/`"html"` ordinal-ignore-case, on failure
add `HED2001` via `ToError(Position, …)`, on success warn `HED2002` if
`UnnamedOutputCompiled` then set `CompileContext.OutputProfile`, return `null`;
`ProcessData` → `null`, `RenderData` → no-op), `src/Heddle/Data/HeddleDiagnosticIds.cs`
(three new constants). Check: `ProfileDirectiveTests` green — both flip directions,
positioned `HED2001` with the valid-values list, `HED2002` position + `Fix`, body/partial
lineage rows, block removal (no residual text where the directive stood).

**WI6 — Double-encode warning (D9; HED2003).**
Files: `src/Heddle/Runtime/HeddleCompiler.cs` (`CompileItem` chain-parameter branch: the
D9 check over `CallParameter.ChainParameter[0]` / the compiled producer's extension type).
Check: `DoubleEncodeWarningTests` green — `@(html(X))` warns with ID + producer position
under `Html`, not under `Text`; `@out():html(X)`, top-level `@(X):html()` (M15), and
`@html(X)` never warn; output bytes
match the documented double-encoded result.

**WI7 — Resolver cache identity (D8).**
Files: `src/Heddle/Runtime/TemplateResolver.cs` (new three-parameter ctor, existing ctor
chains; `_defaultProfile` into the three options initializers; `CacheKey` helper; private
profile-taking `Search` overload; `GetTemplate` computes the effective profile; `Create`
keys on `context.Options.OutputProfile`). Check: `TemplateResolverProfileTests` green —
same file served under both profiles returns differently encoded output with two cache
entries and no `Dictionary.Add` collision; repeat requests hit the cache (same instance
returned); public `Search`/`Create` signatures unchanged.

**WI8 — Goldens, combined gate.**
Files: goldens `generated-profile-flagship-text.html`,
`generated-profile-flagship-html.html`, `generated-profile-directive.html`,
`generated-profile-partial.html` (captured by rendering, reviewed, committed — the
generative step of the TDD verdict). Check: the full
[regression gate](../common/testing-standards.md#regression-gates) in one combined run —
build all TFMs; full suite (all TFMs, `net48` on Windows); every pre-existing golden
byte-identical (default `Text`); **grammar-stability**: `src/Heddle.Language/generated/`
has no diff; render benchmarks
([TextRenderBenchmarks](../../../src/Heddle.Performance/TextRenderBenchmarks.cs)) run once
before/after confirming no change (render path untouched for `Text`); roadmap success
criteria 1–6 asserted together.

**WI9 — Published docs.**
Files: [built-in-extensions.md](../../built-in-extensions.md) — the "Empty / unnamed" entry
and `html` entry become profile-conditional, new `raw` entry (Output and context) and
`profile` entry (Declarations), summary-table rows, and the "HTML encoding" section
rewritten around profiles with the `[NotEncode]` paragraph **removed** (D12);
[language-reference.md](../../language-reference.md) — the behavioral-nuances encoding
bullet (lines 910–913) rewritten, a short "Output profiles" subsection added, `@profile`
cross-linked; [custom-extensions.md](../../custom-extensions.md) — `[EncodeOutput]`
guidance updated for profiles, `[NotEncode]` table row replaced with a "reserved, currently
inert" note; [csharp-api.md](../../csharp-api.md) — `TemplateOptions.OutputProfile` row,
`TemplateResolver` ctor note, the host-side extension-mapping recipe (D1);
[patterns.md](../../patterns.md) — the JSON-splice pattern gains the profile caveat
(`@raw` or `Text` under an `Html` host); source XML docs —
[ExtensionNameAttribute](../../../src/Heddle/Attributes/ExtensionNameAttribute.cs)'s false
`""`-reservation claim fixed. Check: `cd docs && npm run docs:build` passes
(uppercase-drive cwd on Windows); no dangling anchors.

## Public API contract

All additions are additive
([API design rules](../common/coding-standards.md#api-design-and-compatibility)); XML docs
required on every member. Thread-safety notes inline.

```csharp
namespace Heddle.Data
{
    /// <summary>Selects how the unnamed @(...) output emits values.</summary>
    public enum OutputProfile
    {
        /// <summary>Raw text output — today's behavior. Default in 1.x.</summary>
        Text = 0,
        /// <summary>The unnamed @(...) output HTML-encodes its value; @raw(...) opts out.
        /// Becomes the default in the 2.0 window.</summary>
        Html = 1
    }

    public class TemplateOptions
    {
        /// <summary>Output profile for this template and its child compiles (bodies,
        /// partials, imports). Default: OutputProfile.Text (1.x). Participates in
        /// Equals/GetHashCode — the profile keys template caches.</summary>
        public OutputProfile OutputProfile { get; set; }
    }

    public static class HeddleDiagnosticIds
    {
        public const string UnknownOutputProfile        = "HED2001";
        public const string ProfileDirectiveAfterOutput = "HED2002";
        public const string RedundantEncodingExtension  = "HED2003";
    }
}

namespace Heddle.Runtime
{
    public class CompileContext
    {
        /// <summary>The effective output profile for items compiled from this context
        /// onward. Initialized from Options.OutputProfile; flipped by @profile();
        /// snapshotted by child contexts at creation. Compile-time state on a
        /// single-threaded compile — never read at render time.</summary>
        public OutputProfile OutputProfile { get; set; }
    }

    public class TemplateResolver
    {
        /// <summary>Creates a resolver whose templates compile under the given default
        /// profile; per-call CompileContext options override it. The existing
        /// two-parameter constructor keeps OutputProfile.Text.</summary>
        public TemplateResolver(string rootPath, bool checkFileChange, OutputProfile defaultProfile);
    }
}

namespace Heddle.Extensions
{
    /// <summary>Compile-time directive: @profile(){{html}} / @profile(){{text}} sets the
    /// effective output profile for output compiled after it. Emits nothing; the block is
    /// removed from the document. Stateless — safe for concurrent renders like every
    /// directive extension.</summary>
    [ExtensionName("profile")]
    public class ProfileExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType,
            ExType chainedType, ExType parent);   // per D2/D13; returns null
        public override object ProcessData(in Scope scope);  // null
        public override void RenderData(in Scope scope);     // no-op
    }

    [ExtensionName("")]
    [ExtensionName("raw")]   // new second name — @raw(...) is the trusted-value opt-out
    // Class body deliberately not restated: D5 adds only the attribute line above; no
    // member of EmptyExtension changes in this phase.
    public class EmptyExtension : AbstractExtension { }
}
```

Internal additions (listed for the implementer): `HeddleCompiler.UnnamedCarrierName`
(private static, D3), `CompileContext.UnnamedOutputCompiled` (internal, D13),
`TemplateResolver.CacheKey` + the private profile-taking `Search` overload (D8).

Thread-safety summary: the profile is compile-time state on `CompileContext` (one compile,
one thread — the existing contract); no extension gains per-render state
(`ProfileExtension` acts only in `InitStart`; `EmptyExtension`'s second name adds no
state); render-path types are untouched; `TemplateResolver`'s cache threading contract is
unchanged (same dictionary, same access points).

## Diagnostics

Claimed from the `HED2xxx` block
([registry](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)).
Phase 2 touches **no** pre-existing diagnostic message, so no new `HED0xxx` ID is claimed
(consistent with phase 1's `HED0001`–`HED0003`); the `AllowCSharp` guard message phase 1
left unclaimed remains unclaimed — its trigger is in [Deferred items](#deferred-items).
`<…>` are message parameters; positions are absolute template positions.

| ID | Severity | Message text | Trigger / position |
| --- | --- | --- | --- |
| HED2001 | Error | `Unknown output profile '<value>'. Valid values: text, html.` | `@profile()` body is empty, whitespace, or not `text`/`html` (ordinal, case-insensitive). For a missing body the message uses `''` as the value. Position: the directive block (`Position` of the extension). Note: the discouraged paren form `@profile(html)` additionally produces the pre-existing `HED0001` property-resolution error on typed scopes before this one — documented in WI9, message unchanged. |
| HED2002 | Warning | `@profile() appears after output has already been compiled; earlier output keeps the previous profile.` `Fix`: `Move @profile() to the top of the template.` | `ProfileExtension` runs while `CompileContext.UnnamedOutputCompiled` is set (same template scope, per D13). The flip is still applied. Position: the directive block. |
| HED2003 | Warning | `'<name>()' output feeds the unnamed @(...) output, which already HTML-encodes under the Html profile — the value is encoded twice.` `Fix`: `Remove '<name>()', or output the trusted value through @raw(...).` | Bodiless unnamed item under effective `Html` whose parenthesized chain's final producer carries `[EncodeOutput]` (D9). Position: the producer call (`ChainParameter[0].Position`). |

## Testing plan

Instantiates the [testing standards](../common/testing-standards.md).

**TDD verdict (carried from the roadmap, unweakened).** Yes — the behavior is a crisply
enumerable matrix (profile × extension name × container nesting), and the **XSS regression
scenarios are written before the redirect seam exists** (WI1), so safety fails closed from
the first commit. The exception the roadmap grants: capturing `Html`-profile goldens is
generative (render → review → commit), not test-first — the focused assertions are what
get written first. This phase pays down the encoding-test debt: encoding is pinned by
focused assertions from now on, never only by golden HTML.

**Profile × construct matrix** (executable spec in `OutputProfileEncodingTests`, one
`[Theory]` row per cell; `V = "<b>x</b>"` unless stated):

| # | Template | `Text` output | `Html` output |
| --- | --- | --- | --- |
| M01 | `@(V)` | `<b>x</b>` | `&lt;b&gt;x&lt;/b&gt;` |
| M02 | `@raw(V)` | `<b>x</b>` | `<b>x</b>` |
| M03 | `@html(V)` | `&lt;b&gt;x&lt;/b&gt;` | `&lt;b&gt;x&lt;/b&gt;` |
| M04 | `@string(V)` | `&lt;b&gt;x&lt;/b&gt;` | `&lt;b&gt;x&lt;/b&gt;` |
| M05 | `@()` with model `V` | `<b>x</b>` | `&lt;b&gt;x&lt;/b&gt;` |
| M06 | `@(Price * Quantity)` (`2m`, `3m`) | `6` | `6` (native tier, non-string boxing — D10) |
| M07 | `@("<i>" + "a")` (folded constant) | `<i>a` | `&lt;i&gt;a` |
| M08 | `@upper(V)` (standalone function carrier) | `<B>X</B>` | `&lt;B&gt;X&lt;/B&gt;` |
| M09 | `@(upper(V))` | `<B>X</B>` | `&lt;B&gt;X&lt;/B&gt;` |
| M10 | `@(NullProp)` | *(empty)* | *(empty)* |
| M11 | `@(Item){{[@(Name)]}}` — bodied unnamed = rescoping container; `Item.Name = "<i>"` | `[<i>]` | `[&lt;i&gt;]` — body static text raw, inner leaf encoded (D3) |
| M12 | `@out():html(V)` | `&lt;b&gt;x&lt;/b&gt;` | `&lt;b&gt;x&lt;/b&gt;` — chained splice stays single-encoded |
| M13 | `@(html(V))` | `&lt;b&gt;x&lt;/b&gt;` | `&amp;lt;b&amp;gt;x&amp;lt;/b&amp;gt;` double-encoded + `HED2003` (D9) |
| M14 | `<div data-v="@(Q)">`, `Q = "a\"b"` | `a"b` | `a&quot;b` (attribute-context row) |
| M15 | `@(V):html()` | `<b>x</b>` | `&lt;b&gt;x&lt;/b&gt;` — the top-level chained `html()` result lands in the bodiless sink's ignored `ChainedData` and is discarded; the sink renders its own `ModelData` once-encoded. No warning under either profile (D9: the construct is inert, not double-encoding — this row is the roadmap validation scenario's "output documented") |

**XSS corpus** (same class; every row asserts **exact output bytes** and, where marked,
diagnostic ID + position. Templates are inline `[Theory]` data unless a row names a
fixture. Data defaults: `UserInput = "<script>alert(1)</script>"`. "`Html` options" means
`TemplateOptions.OutputProfile = Html`; directive rows compile under default `Text`
options unless stated):

| # | Template | Data / options | Expected |
| --- | --- | --- | --- |
| X01 | `@profile(){{html}}@(UserInput)` | default `Text` options | `&lt;script&gt;alert(1)&lt;/script&gt;` — the flagship regression; the removed directive block leaves no residue before the payload |
| X02 | `@raw(TrustedHtml)` | `Html` options; `TrustedHtml = "<em>ok</em>"` | `<em>ok</em>` — verbatim passthrough |
| X03 | `@if(Flag){{<p>@(UserInput)</p>}}` | `Html` options; `Flag = true` (and a second run `Flag = false`) | `<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>` — profile inherits into the body subtree (D4); static `<p>` markup raw per the container-forwarding rule. `Flag = false` → empty output |
| X04 | `@list(Tags){{<span>@()</span>}}` | `Html` options; `Tags = new[] { "<b>", "&" }` | `<span>&lt;b&gt;</span><span>&amp;</span>` — each element encoded exactly once at the `@()` leaf; static `<span>` raw; no separator bytes between iterations |
| X05 | Fixture pair — `profile-partial-parent.heddle`: `<div>@partial(){{profile-partial-child}}</div>`; `profile-partial-child.heddle`: `<p>@(UserInput)</p>` | parent compiled with `Html` options | `<div><p>&lt;script&gt;alert(1)&lt;/script&gt;</p></div>` — the child's `CompileContext` snapshots the parent's effective profile at the `@partial` site (D4 lineage) |
| X06 | `@(html(UserInput))` | `Html` options; `UserInput = "<b>x</b>"` | `&amp;lt;b&amp;gt;x&amp;lt;/b&amp;gt;` double-encoded + warning `HED2003` positioned at the nested `html(` producer call (D9) |
| X07 | `@profile(){{pdf}}` | default options | error `HED2001`, message exactly `Unknown output profile 'pdf'. Valid values: text, html.`, positioned at the directive block |
| X08 | One `TemplateResolver`, same file (`profile-flagship.heddle`) | requested under a `Text`-options context, then an `Html`-options context | raw bytes, then encoded bytes; `TemplatesCache` holds two entries (keys `<fullPath>\|Text` and `<fullPath>\|Html` per D8's `CacheKey`); repeat requests per profile return the same cached instance; no `Dictionary.Add` collision |
| X09 | `@upper(UserInput)` | `Html` options; `UserInput = "<b>x</b>"` | `&lt;B&gt;X&lt;/B&gt;` — the phase 1 standalone-function carrier redirects too (D3); `upper` runs on the raw value, encoding applies once to its result |
| X10 | `@(X)@profile(){{html}}@(Y)` | default `Text` options; `X = Y = "<i>"` | `<i>&lt;i&gt;` — `X` raw (compiled before the flip), `Y` encoded; warning `HED2002` at the directive with `Fix` text `Move @profile() to the top of the template.` |
| X11 | Control: the rows whose `Html`-ness comes from options — X02, X03, X04, X05, X06, X09 — re-run with `Text` options | same data per row | today's raw behavior, byte-exact per row: X02 `<em>ok</em>`; X03 `<p><script>alert(1)</script></p>`; X04 `<span><b></span><span>&</span>`; X05 `<div><p><script>alert(1)</script></p></div>`; X06 `&lt;b&gt;x&lt;/b&gt;` (single-encoded by `html()`, no `HED2003`); X09 `<B>X</B>` |
| X12 | `@profile(){{text}}@(UserInput)` | `Html` options; `UserInput = "<b>x</b>"` | `<b>x</b>` raw — criterion 3's opposite direction: the directive overrides an `Html`-default compile |

**Encoding-pin assertions** (same class, `[Theory]` rows E01–E14 — the concrete
instantiation of D11's "pre-measured, not discovered" claim). Each row renders `@(V)`
under `Html` options with `V` set to the input column and asserts the exact 1.x bytes of
the middle column (`WebUtility.HtmlEncode` behavior, verified against the runtime source —
see D11's grounding). The right column is **documentation, not a 1.x assertion**: the
bytes the same row must produce after the 2.0 swap to
`HtmlEncoder.Create(UnicodeRanges.All)`. At the swap the E-rows are re-pinned to that
column, and the diff between the two columns is the entire sanctioned golden churn. Rows
E12/E13 build `V` programmatically (not from fixture files) so the suite's line-ending
normalization cannot touch the control characters under test.

| # | Input `V` | 1.x pinned output (`WebUtility.HtmlEncode`) | 2.0 output (`HtmlEncoder.Create(UnicodeRanges.All)`) | Delta class |
| --- | --- | --- | --- | --- |
| E01 | `<` | `&lt;` | `&lt;` | unchanged |
| E02 | `>` | `&gt;` | `&gt;` | unchanged |
| E03 | `&` | `&amp;` | `&amp;` | unchanged |
| E04 | `"` | `&quot;` | `&quot;` | unchanged |
| E05 | `'` | `&#39;` | `&#x27;` | decimal → hex form |
| E06 | `+` | `+` (raw) | `&#x2B;` | starts encoding (UTF-7-attack hardening) |
| E07 | U+00A0 (NBSP) | `&#160;` | `&#xA0;` | decimal → hex form (Zs stays outside every safe list) |
| E08 | `é` (U+00E9) | `&#233;` | `é` (raw) | stops encoding — the defined letters/symbols of the U+00A0–U+00FF band are allowed by `UnicodeRanges.All` |
| E09 | `Ā` (U+0100) | `Ā` (raw) | `Ā` (raw) | unchanged — outside `WebUtility`'s band, inside the widened safe list |
| E10 | `中` (U+4E2D) | `中` (raw) | `中` (raw) | unchanged under `Create(All)` — the row that would become `&#x4E2D;` under `HtmlEncoder.Default`, i.e. the measured reason D11 picks the widened safe list |
| E11 | `😀` (U+1F600, surrogate pair) | `&#128512;` | `&#x1F600;` | decimal → hex form (both encoders always encode supplementary code points) |
| E12 | `"\r\n"` | raw CR LF | `&#xD;&#xA;` | starts encoding — category Cc is outside every encoder safe list; multi-line string values are churn |
| E13 | tab (U+0009) | raw tab | `&#x9;` | starts encoding (Cc) |
| E14 | `a=b/c` | `a=b/c` (raw) | `a=b/c` (raw) | unchanged control row — `=`, `/`, and the rest of printable Basic Latin stay raw in both |

**Named test assets** (all in [src/Heddle.Tests](../../../src/Heddle.Tests)):

| Asset | Content |
| --- | --- |
| `OutputProfileEncodingTests` | M01–M15 + X01–X12 + E01–E14 as `[Theory]` rows over inline templates (X05 and X08 use the named fixtures); asserts exact bytes — the E-rows are the `WebUtility` baseline (`&#39;`, raw `+`, decimal U+00A0-range and astral references, raw Cc controls) that the 2.0 swap re-pins, per D11. |
| `ProfileDirectiveTests` | Flip both directions; `HED2001` (unknown/empty/whitespace); `HED2002` position + `Fix` + flip-still-applied; body-scoped flip stays in its subtree (D4); import in document order; directive block leaves no residue in output. |
| `OutputProfileOptionsTests` | Enum values/defaults; `Equals`/`GetHashCode` rows (differ only by profile → unequal; equal → equal hashes); `CompileContext` init/copy semantics including effective-vs-options precedence. |
| `TemplateOptionsCompletenessTests` | The D7 reflection round-trip (self-extending omission guard) + the named `ProvideLanguageFeatures` regression fact (roadmap criterion 6) + the `TemplateName` copy fact. |
| `TemplateResolverProfileTests` | X08 mechanics: both ctors, cache-key separation, cache hits per profile, `RemoveFromCache` still removes by instance. |
| `DoubleEncodeWarningTests` | M13/X06 plus negatives: `@html(V)`, `@out():html(V)`, top-level `@(V):html()` (M15 — a chained producer, not a nested-chain parameter), `Text`-profile `@(html(V))` — no warning; each negative also asserts its exact output bytes. |
| Fixtures/goldens | `profile-flagship.heddle` → `generated-profile-flagship-text.html` + `generated-profile-flagship-html.html` (roadmap criterion 1 golden-pinned both ways); `profile-directive.heddle` → `generated-profile-directive.html`; `profile-partial-parent.heddle` + `profile-partial-child.heddle` → `generated-profile-partial.html`. Line endings normalized per the standards. |

**Regression gate** (one combined run): build all TFMs; `dotnet test src/Heddle.Tests`
zero failures on all TFMs; all pre-existing goldens byte-identical (default `Text` —
roadmap criterion 4); grammar-stability check (`generated/` no diff — phase 2 is a
no-grammar phase); render benchmarks re-run once confirming the untouched `Text` render
path; docs build green after WI9. Roadmap success criteria 1–6 map to: M01 + flagship
goldens (1), M02/X02 (2), X01/X10/X12 (3), the untouched-suite gate (4), X08 (5), and
`TemplateOptionsCompletenessTests` (6).

**Concurrency note.** This phase adds no render-time or cross-render state (the profile is
resolved at compile time into which extension type is instantiated), so no new
parallel-render test is required by the standards' concurrency rule; the existing suite's
render behavior under `Text` is the isolation proof, and `TemplateResolver`'s threading
contract is unchanged.

**Deferred phase 9 gallery item** (recorded per the standards): `samples/html-safe-output`
— profile flip, `@raw`, and (post-2.0) pluggable-encoder demo, golden-asserted in CI and
run under both defaults as the standing 2.0-migration rehearsal. Owned by this phase,
delivered when the phase 9 harness exists.

## Back-compat and migration

Default `Text` means zero behavior change until a host opts in — proven by the untouched
suite + byte-identical goldens.

| Surface | Behavior | Proof / note |
| --- | --- | --- |
| All existing templates, default options | Byte-identical | Full suite + goldens (gate), X11 controls |
| `@html`, `@string`, other `[EncodeOutput]` extensions, containers, `@out()` | Unchanged under both profiles | M03/M04/M12 |
| New registry names `raw`, `profile` | A user assembly exporting either name via `[ExportExtensions]` now throws `TemplateOverrideException` at startup | Release note (verified failure mode, `TemplateFactory.AddExtensions`) |
| `TemplateOptions.Equals`/`GetHashCode` | Now include `OutputProfile` | Release note for hosts using options as dictionary keys (D6) |
| `TemplateResolver` ctor surface | Existing ctor intact; new overload added | D8 (no binary break) |
| Copy-ctor now copies `ProvideLanguageFeatures` | Child compiles of editor-mode hosts start inheriting it — the roadmap-ratified bug-fix direction | `TemplateOptionsCompletenessTests` pins it |
| Templates splicing trusted HTML through `@()` | Under a host's `Html` opt-in they need `@raw()` | The 2.0 migration note's centerpiece; documented now in WI9 |
| 2.0 window (executed later, per D11) | Default flips to `Html`; encoder swaps to pluggable `HtmlEncoder.Create(UnicodeRanges.All)`; `TemplateOptions.Encoder` added; no 1.x valve exists by ratified decision | [Cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window); byte deltas pre-measured by encoding-pin rows E01–E14 (testing plan) |

## Performance considerations

- **Render path: untouched for existing behavior.** Under `Text` the compiled object graph
  is bit-for-bit today's; under `Html` a bodiless unnamed call renders through the
  pre-existing `EmptyHtmlExtension` path (`HtmlEncodedRenderer` allocates the encoded
  string `WebUtility` produces — the same cost `@html(...)` has always paid; no new
  per-render allocation is introduced by this phase,
  [performance rules](../common/coding-standards.md#performance-rules-the-render-path-is-hot)).
  Render benchmarks run once in the gate to confirm the `Text` path.
- **Compile path:** one string-length/null check and one enum comparison per unnamed item
  (D3), one attribute probe per nested-chain unnamed sink (D9), and one extra copied field
  per child context — noise against reflection and Roslyn costs; no benchmark budget
  required, and the compile-cost runners in
  [src/Heddle.Performance/Runners](../../../src/Heddle.Performance/Runners) remain
  available if a question arises.
- **Resolver:** one string concatenation per cache probe/write (compile-frequency, not
  render-frequency).
- Pre-encoding folded constants at compile time is a recorded deferred optimization
  (D10) — rejected now to keep encoder knowledge in two call sites for the 2.0 swap.
- No `[MethodImpl(AggressiveInlining)]` additions; nothing here is a hot tiny transform.

## Standards compliance

The [balanced-mode](../common/coding-standards.md#solid-dry-and-yagni-in-balanced-mode)
calls actually made in this phase:

- **DRY where knowledge must not diverge:** unnamed-carrier resolution exists once
  (`UnnamedCarrierName`, D3) and serves both the output-item and function-carrier sites —
  the alternative was the `@upper(Name)` vs `@(upper(Name))` encoding divergence found
  during spec verification. Encoder knowledge stays at its two existing call sites (D11).
- **YAGNI cutting scope:** no `TemplateOptions.Encoder` in 1.x, no prototype-options
  resolver ctor, no per-property `[NotEncode]` implementation, no attribute/JS/URL
  contextual profiles, no migration valve — each with a trigger in
  [Deferred items](#deferred-items) or a ratified rejection (D1, D11).
- **Open/closed at the sanctioned seam:** the profile is an options enum + a directive
  extension — both existing open points; the compiler change is a name-resolution redirect,
  not a new plugin mechanism, and `TemplateFactory`'s registry contract is untouched.
- **Security beats ergonomics:** the bodied-unnamed case stays raw (D3) because encoding
  static markup would push authors toward `@raw` wrappers, eroding the default; the
  double-encode case warns instead of silently un-encoding (D9) because attribute-sniffed
  behavior changes are unauditable (precedence rule 2 over 3).
- **SRP along pipeline stages:** profile state lives in the compile stage
  (`CompileContext`), selection in name resolution (`HeddleCompiler`), encoding in the
  existing render leaves — no stage learns another's job.

## Deferred items

| Item | Trigger to revisit |
| --- | --- |
| Default flip `Text` → `Html`, encoder swap, `TemplateOptions.Encoder` | The 2.0 window (D11, [cross-cutting D2](../common/cross-cutting-decisions.md#d2--the-single-20-breaking-window)) |
| Per-property `[NotEncode]` (or its removal) | Phase 5 typed props design (D12) |
| Contextual escaping profiles (attribute/JS/URL, Go-style) | User demand; phase 8's sink work is the natural seam |
| Marker-interface seam for paren-argument directives (`@profile(html)`) | Phase 5 props parsing, which needs compile-time argument access anyway (D2) |
| Lint for inert top-level chain producers (`@(X):html()` discarding its result) | Phase 6 diagnostics/LSP (D9) |
| Compile-time pre-encoding of folded constants | A profiled render win on constant-heavy `Html` templates, after the 2.0 encoder settles (D10) |
| Prototype-options `TemplateResolver` constructor | A second resolver-configured option beyond the profile (D8) |
| `HED0xxx` ID for the `AllowCSharp` guard message | The phase that next touches that message ([cross-cutting D1](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx)) |
| Document-global `@profile`-position tracking across body scopes | Evidence of real templates flipping profiles across body boundaries (D13) |
| `samples/html-safe-output` gallery item | Phase 9 harness (testing plan) |

## External references

Primary sources verified for this spec (July 2026); the roadmap's grounding set was
re-checked where load-bearing:

- [WebUtility.HtmlEncode](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode) — string/TextWriter surface; remarks re-verified. Exact encoded set (`<` `>` `"` `'`→`&#39;` `&`, U+00A0–U+00FF and surrogate pairs as **decimal** numeric references; `+`, control characters including tab/CR/LF, and all BMP ≥ U+0100 pass through raw) from the runtime source: [WebUtility.cs](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Net/WebUtility.cs) (re-read for the E01–E14 table, July 2026).
- [Prevent XSS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting) — re-verified quotes: Razor "automatically encodes all output sourced from variables"; the default encoders "use the safest encoding rules possible" with a safe list "limited to the Basic Latin Unicode range"; `HtmlEncoder.Create(allowedRanges: …)` widening; `HtmlString` as the never-with-untrusted-input escape hatch (the `@raw` precedent); "encoding takes place at the point of output" (D10).
- [HtmlEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.htmlencoder) — `Create(TextEncoderSettings)`/`Create(UnicodeRange[])`, string/TextWriter/span/UTF-8 `Encode` overloads (2.0 contract, D11); behavior details in the [runtime HtmlEncoder source](https://github.com/dotnet/runtime/blob/release/5.0/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/HtmlEncoder.cs): `ForbidHtmlCharacters` forbids `<` `>` `&` `'` `"` `+` in every safe list; four named entities (`&lt;` `&gt;` `&amp;` `&quot;`), all other encodes emitted as hex `&#xHHHH;`-form references; `ForbidUndefinedCharacters` always disallows "categories Cc, Cs, Co, Cn, Zs [except U+0020 SPACE], Zl, Zp" (ctor comment, quoted); supplementary code points always encode.
- [Defined-character list generator](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Encodings.Web/tools/GenDefinedCharList/Program.cs) — the source of the encoders' "defined" bitmap: allowed general categories are letters, marks, numbers, punctuation, symbols, and Cf minus U+FEFF — confirming Cc (tab/CR/LF) is always encoded even under `UnicodeRanges.All` (E12/E13 basis, newly consulted July 2026).
- [Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) — the ctor-overload-not-defaulted-parameter rule (D8).
- Ecosystem raw/escape conventions carried from the roadmap's verified set (D5): [Handlebars expressions](https://handlebarsjs.com/guide/expressions.html) (`{{{ }}}`), [Phoenix.HTML](https://hexdocs.pm/phoenix_html/Phoenix.HTML.html) (`raw/1`), [Jinja API](https://jinja.palletsprojects.com/en/stable/api/) + [Flask templating](https://flask.palletsprojects.com/en/stable/templating/) (autoescape defaults), [LiquidJS escaping](https://liquidjs.com/tutorials/escaping.html), [Go html/template](https://pkg.go.dev/html/template) (the Text/Html split precedent).
- [.NET 10 performance post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/) and [dotnet/runtime#114494](https://github.com/dotnet/runtime/pull/114494) — the `SearchValues`-backed encoder scan that makes the 2.0 `HtmlEncoder` default fast on net10.0 with zero Heddle code (carried; informs D11's "prepare, don't build" split).
