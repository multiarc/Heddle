# Phase 2 — Safe Output Default (OutputProfile)

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Depends on: nothing (independent of phase 1). Contains the planned **2.0 breaking change**.

## Goal

Under `OutputProfile.Html`, the unnamed `@(...)` output HTML‑encodes by default with a
`@raw(...)` opt‑out; `OutputProfile.Text` keeps today's raw behavior for text/JSON/code
generation. Default is `Text` in 1.x (fully non‑breaking); the flip to `Html` is the
documented 2.0 breaking change.

## Background

Today the shortest spelling — `@(Title)` — is the **raw** one (`EmptyExtension`, name `""`),
while encoding requires `@html(...)`/`@string(...)` (`[EncodeOutput]` extensions). The HTML
engines Heddle competes with either encode by default — Razor "automatically encodes all
output sourced from variables", Handlebars' `{{ }}` HTML‑escapes, Phoenix HEEx escapes, Go's
`html/template` auto‑escapes contextually — or are the ecosystem's canonical XSS footguns for
not doing so: raw Jinja2 ships with autoescaping **off** (Flask flips it on for
`.html`‑family templates), and Shopify Liquid outputs raw unless `| escape` is applied
(sources in [External grounding](#external-grounding)). Either way the conclusion holds, and
it is the single most disqualifying finding of the [assessment](../assessment.md) for the
HTML use case. Heddle is a *text* engine, not only HTML — hence a **profile**, not an
unconditional flip. Go's standard library is the closest precedent for exactly this split:
`text/template` and `html/template` share one template API and differ only in that the HTML
flavor escapes by default.

## Design

### Option and plumbing

- New `public enum OutputProfile { Text = 0, Html = 1 }`
  (`src/Heddle/Data/OutputProfile.cs`).
- `TemplateOptions.OutputProfile` ([TemplateOptions.cs](../../src/Heddle/Data/TemplateOptions.cs)):
  default `Text` in 1.x. Add to the copy‑ctor (lines 35–44) — **and fix the pre‑existing bug
  there: `ProvideLanguageFeatures` is not copied today**, so it never reaches child compiles.
  Add `OutputProfile` (plus `AllowCSharp`/`ExpressionMode` for consistency) to
  `Equals`/`GetHashCode` — dormant today but correct for any options‑keyed cache.
- The **effective** profile lives on `CompileContext`
  ([CompileContext.cs](../../src/Heddle/Runtime/CompileContext.cs)): initialized from
  `Options.OutputProfile` in the public ctors, copied in the private copy‑ctor. The
  `@profile()` directive mutates the `CompileContext`, never the host's `TemplateOptions`
  instance (which is held by reference — mutating it would be an observable side effect).

### The redirect seam (no new encoding code)

In `HeddleCompiler.CreateExtension`
([HeddleCompiler.cs](../../src/Heddle/Runtime/HeddleCompiler.cs) ~line 468, non‑definition
branch): when the extension name is `""` and the effective profile is `Html`, resolve
`"html"` (`EmptyHtmlExtension`) instead. This reuses the entire existing
`[EncodeOutput]` → `InitializeTemplate` → `DirectRender` → `WebUtility.HtmlEncode` pipeline.
`TemplateFactory`'s static registry is **not** touched — it is process‑global and must not be
profile‑mutated (racy, cross‑host).

Why not force `RenderType.Encode` on `EmptyExtension` instead: `EmptyExtension` derives from
`AbstractExtension`, which never consults `DirectRender`; only `AbstractHtmlExtension` does.
The name redirect is one line and uses proven code.

### `raw` opt‑out

Add `[ExtensionName("raw")]` as a second name on
[EmptyExtension](../../src/Heddle/Extensions/EmptyExtension.cs) (registry verified free;
multiple `[ExtensionName]` attributes are supported). Registered under both profiles — a
harmless alias under `Text`, the escape hatch under `Html`:

```heddle
@raw(TrustedHtmlFragment)
```

An explicit trusted‑value opt‑out is the ecosystem convention for avoiding double encoding:
Razor's `Html.Raw`/`HtmlString` (`IHtmlContent` is written without re‑encoding), Handlebars'
triple‑stash `{{{ }}}`, Jinja's `|safe`/`Markup`, Phoenix's `raw/1`.

### `@profile()` directive

New `ProfileExtension` (`src/Heddle/Extensions/ProfileExtension.cs`,
`[ExtensionName("profile")]`, name verified free) following the
[ModelExtension](../../src/Heddle/Extensions/ModelExtension.cs) compile‑time‑directive
pattern: `InitStart` reads the parameter (accept both `@profile(html)` member‑path form —
canonical — and `@profile(){{html}}` body form), `Enum.TryParse` ignore‑case, sets
`CompileScope.CompileContext.OutputProfile`, returns `null` so the block is removed
(`RemoveEmptyItem`). Unknown value → positioned compile error listing valid values.

Positional semantics identical to `@model`/`@using`: affects output blocks compiled **after**
it in document order; document "top of file"; optional compile warning if it appears after an
unnamed output has already been compiled.

### Cache identity

The live template cache is `TemplateResolver.TemplatesCache` keyed by file path
([TemplateResolver.cs](../../src/Heddle/Runtime/TemplateResolver.cs)); `DocumentsCache` is
dead (commented‑out) code. `TemplateResolver` builds its own `TemplateOptions` and never sets
a profile today, so: add an optional profile (or prototype‑options) constructor parameter,
apply it to every options instance it builds, and suffix the cache key
(`fullPath + "|" + profile`) so one resolver can serve both profiles without collisions.

### Interaction with containers, chains, and `@out()` — verified no double‑encoding

- Container extensions (`list`, `if`, `for`, `out`, `swap`, `param`) are `RenderType.Raw`
  and only forward their bodies; encoding is applied exactly once by the `[EncodeOutput]`
  **leaf** that emits the value. Making the `""` leaf encode does not introduce a second
  encoding anywhere.
- `@out()` splices its chained value verbatim (not `[EncodeOutput]`) → values encoded by a
  producer stay single‑encoded.
- The one real double‑encode pattern is explicit: `@(X):html()` under `Html` (encoding
  extension feeding the now‑encoding unnamed sink). Because the chain compiles reversed, the
  compiler can see the right neighbor's `[EncodeOutput]` when compiling the leftmost `""`
  item → emit a **compile warning** ("redundant html() feeding auto‑encoding output; remove
  it or use raw()"). No silent behavior change.

### Documentation updates (same phase)

- [built-in-extensions.md](../built-in-extensions.md): "HTML encoding" section and the
  empty/unnamed entry become profile‑conditional; **fix the false claim that `[NotEncode]`
  works per‑property** — it is dead code (`AttributeUsage(Property)` but only ever checked on
  the extension class). Decide: implement it as documented or remove the doc claim (lean:
  remove claim now; revisit with props in phase 5).
- [language-reference.md](../language-reference.md) behavioral‑nuances section;
  [custom-extensions.md](../custom-extensions.md) `[EncodeOutput]` guidance;
  `ExtensionNameAttribute` XML doc (already claims `""` is reserved for
  `EmptyHtmlExtension` — reconcile with the implemented behavior).

## Back‑compat analysis

- Default `Text` → zero behavior change until opted in.
- Under `Html`, only unnamed `@(...)` outputs change; `@html`, `@string`, `@out`, containers
  unchanged; bodies/partials inherit the profile via `CompileContext` lineage.
- A user assembly exporting an extension named `raw` via `[ExportExtensions]` would now
  collide at startup (`TemplateOverrideException`) — release note.
- `TemplateOptions.Equals` semantics change — note for hosts using options as dictionary
  keys.
- Templates that deliberately splice trusted HTML through `@()` need `@raw()` when their
  host flips to `Html` — this is exactly the migration the 2.0 note must call out.

## Risks & mitigations

- **Directive ordering surprises** (`@profile` mid‑file): same rules as `@model`; document +
  optional warning. (S)
- **Partial/import trees mixing profiles**: profile follows `CompileContext` lineage; add a
  fixture with a partial compiled under an `Html` parent. (S)
- **Under‑tested encoding today**: there are no dedicated encoding assertions in the suite
  (behavior pinned only via golden HTML) — this phase must add focused encoding tests for
  both profiles, not just goldens. (M)

## Success criteria

1. `Name = "<b>x</b>"`: `@(Name)` renders `&lt;b&gt;x&lt;/b&gt;` under `Html` and `<b>x</b>`
   under `Text` — both golden‑pinned.
2. `@raw(Name)` under `Html` renders `<b>x</b>`.
3. `@profile(html)` flips a `Text`‑default compile; `@profile(text)` flips an `Html`‑default
   one.
4. Entire existing suite passes untouched with default `Text`.
5. One `TemplateResolver` serving the same file under both profiles returns
   differently‑encoded outputs (no cache collision).
6. Copy‑ctor round‑trip preserves `OutputProfile` **and** `ProvideLanguageFeatures` (the
   adjacent bug fix is pinned by a test).

## Validation scenarios

| Scenario | Expected |
| --- | --- |
| `@profile(html)@(UserInput)` with `UserInput = "<script>alert(1)</script>"` | `&lt;script&gt;alert(1)&lt;/script&gt;` — the XSS regression test |
| `@profile(html)@raw(TrustedHtml)` | verbatim passthrough |
| `Html` + `@if(Flag){{@(UserInput)}}` | body‑inner unnamed output encodes (profile inheritance into bodies) |
| `Html` + `@list(Tags){{<span>@()</span>}}` | each tag encoded exactly once (container forwarding) |
| `Html` + `@(X):html()` | compile warning (redundant encode); output documented |
| `Html` parent importing/`@partial()`‑ing a child template | child's unnamed outputs encode (lineage) |
| **Negative** `@profile(pdf)` | compile error listing `text | html` |
| Same file via one resolver under `Text` then `Html` | different outputs, no collision |

## Size estimate

| Work item | Size |
| --- | --- |
| Enum + options + copy‑ctor/equality + `CompileContext` plumbing | S |
| Redirect + `raw` alias | S |
| `ProfileExtension` | S |
| Resolver ctor/key + double‑encode warning | S/M |
| Tests/goldens (incl. security cases) + doc updates | M |

## .NET 10 opportunities (net10.0 target)

Researched July 2026 against .NET 10 (current LTS). Honest headline first: **this phase needs no
`#if NET10_0_OR_GREATER` code.** Because the decided design routes encoding through
`System.Text.Encodings.Web.HtmlEncoder`, everything .NET 10 improved arrives for free on the
net10.0 target via the inbox library; the one genuinely conditional idea below gates on
`NET8_0_OR_GREATER`, not 10.

### Free on net10.0 — the encoder scan became SearchValues‑backed in .NET 10

- **Mechanism**: [dotnet/runtime#114494](https://github.com/dotnet/runtime/pull/114494)
  (milestone 10.0.0, merged Apr 2025) deleted the encoder's hand‑rolled 128‑bit SSSE3/AdvSimd
  vectorization (`OptimizedInboxTextEncoder.Ssse3.cs` and `.AdvSimd64.cs` removed outright) and
  replaced the ASCII fast‑path scan with `SearchValues`. Per the
  [.NET 10 performance post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/):
  "#114494 employs SearchValues in `OptimizedInboxTextEncoder`, which is the core implementation
  that backs the various encoders like `JavaScriptEncoder` and `HtmlEncoder`" — i.e. it also backs
  `Create(UnicodeRanges…)` instances, so the planned widened default is covered, not just
  `HtmlEncoder.Default`.
- **Benefit**: the hot "find the first character to encode" loop now rides CoreLib's search engine
  (AVX2/AVX‑512/`Vector512` paths) instead of the encoder's own 128‑bit‑only code — the motivating
  issue [#114437](https://github.com/dotnet/runtime/issues/114437) states the old code was "doing
  the same things as `IndexOfAnyAsciiSearcher.cs`, but now with fewer optimizations". On top of
  that, .NET 10's own `SearchValues` tuning compounds:
  [#106900](https://github.com/dotnet/runtime/pull/106900) (small ASCII byte sets with unique low
  nibbles → one shuffle+compare instead of N compares; the blog's benchmark needle is literally
  the escape‑scan set `"\0\r&<"u8`, 1.39× faster) and
  [#107798](https://github.com/dotnet/runtime/pull/107798) (AVX‑512 probabilistic‑map path).
  [AVX10.2 codegen support](https://github.com/dotnet/runtime/pull/111209) (new in .NET 10;
  AVX10.1 was .NET 9) can light these paths up on newest x86 hardware.
- **Conditionality**: zero Heddle code — the net10.0 build binds the inbox assembly and gets all
  of it. No encoder‑specific benchmark was published for #114494; expect the win to be
  hardware‑dependent (widest vectors help most) and to show on longer values, while short‑value
  encodes stay dominated by fixed call overhead. The CoreLib `SearchValues` improvements are
  runtime‑bound, not TFM‑bound (a net8.0 assembly rolled forward onto the .NET 10 runtime gets
  them too); the encoder swap itself is .NET 10's inbox library.

### Conditional fast path, if ever profiled as needed — gate is NET8, not NET10

If a hand‑rolled scan‑and‑copy path in the render pipeline ever looks attractive (scan for the
profile's must‑encode set, bulk‑copy the clean prefix, hand the remainder to the encoder), the
tool is a cached `SearchValues<char>` + `IndexOfAny` — introduced for `char`/`byte` in **.NET 8**,
`SearchValues<string>`/`OrdinalIgnoreCase` in **.NET 9** (both per the
[.NET 10 perf post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)'s
recap), so the honest guard is `#if NET8_0_OR_GREATER`; the identical source just runs faster on
net10 via the runtime improvements above. It is probably unnecessary here anyway:
`TextEncoder.Encode(string)` already returns the **original string instance** when nothing needs
encoding (`return value; // shortcut: there's no work to perform`,
[TextEncoder.cs, release/10.0](https://github.com/dotnet/runtime/blob/release/10.0/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/TextEncoder.cs)),
so the common clean‑value case is already one vectorized scan and zero allocations.

### Explicitly not .NET 10‑specific (verified, so this section stays honest)

- **The encoder library was perf‑frozen from .NET 6 through .NET 9.** The vectorized rewrite is
  [dotnet/runtime#49373](https://github.com/dotnet/runtime/pull/49373) (milestone 6.0.0); commit
  history for `src/libraries/System.Text.Encodings.Web` across the .NET 7/8/9 windows shows only
  cosmetic and Unicode‑data updates, and the one other .NET 10‑window change,
  [#109896](https://github.com/dotnet/runtime/pull/109896), self‑describes as "~zero performance
  difference". Net: `HtmlEncoder` on net6.0 ≈ net8.0; net10.0 is the first target since .NET 6
  where the encoder's scan actually changed. (The .NET 8 "SearchValues went everywhere" wave
  deliberately skipped this library — see #114437 — so no, the encoders did **not** get
  SearchValues in .NET 8.)
- **No new .NET 10 string/UTF‑8 primitive helps the encode‑into‑sink path.** The span/UTF‑8
  surface this phase and [phase 8](phase-8-streaming-async.md) rely on —
  `Encode(ReadOnlySpan<char>, Span<char>, …)`,
  [`EncodeUtf8`](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.textencoder.encodeutf8),
  `FindFirstCharacterToEncode` — dates from the netcoreapp3.0/5.0 package era and exists on every
  Heddle TFM, including netstandard2.0 via the package. The
  [.NET 10 libraries what's‑new](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
  "Strings" items (span‑based normalization, UTF‑8 hex conversion) don't touch this path. So
  phase 8's UTF‑8 sink gates on nothing new in 10 — only speed differs by target.
- **What netstandard2.0 actually misses**: the 10.0.x
  [System.Text.Encodings.Web package](https://www.nuget.org/packages/System.Text.Encodings.Web)
  ships `net462`/`netstandard2.0`/`net8.0`/`net9.0`/`net10.0` assets (verified from the nupkg);
  the vectorized scan compiles only into the .NET‑family assets, so Heddle on .NET Framework runs
  the scalar fallback regardless of package version. Same API surface everywhere — the difference
  is purely throughput.

### C# 14 conveniences (SDK‑level, not TFM‑gated)

[C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) ships with the
.NET 10 SDK and applies to all targets the SDK compiles, so these are build‑toolchain wins rather
than net10.0‑target wins: first‑class span conversions (the perf post notes `MemoryExtensions`
helpers "now naturally show up directly on types like `string`") keep any span‑plumbed encode
path free of `.AsSpan()` noise; the `field` keyword suits the pluggable
`TemplateOptions.Encoder` property (null‑guard setter without a hand‑written backing field);
null‑conditional assignment tidies options plumbing. Nothing in this phase requires C# 14.

## Open questions

1. Should the profile also be inferable from file extension (`.heddle.html` vs
   `.heddle.txt`) as sugar over the option? (Lean: later, if file‑based usage grows.)
2. Ship a `HeddleDefaults.OutputProfile` static in 1.x so hosts can flip globally ahead of
   2.0? (Lean: yes — cheap migration valve.)
3. ~~Keep `WebUtility.HtmlEncode` or move to `HtmlEncoder`?~~ — **Decided (July 2026):
   move the `[EncodeOutput]` pipeline to `System.Text.Encodings.Web.HtmlEncoder` in the 2.0
   breaking window, and make the encoder pluggable.** Specifics: the default becomes
   `HtmlEncoder` widened via `HtmlEncoder.Create(UnicodeRanges.All)` (HTML‑significant
   characters still encode; non‑Latin text stays readable, which contains the golden churn),
   bundled with the profile flip so hosts absorb **one** migration; a `TemplateOptions`
   encoder setting lets hosts pin the legacy `WebUtility` byte‑for‑byte behavior — or any
   custom `TextEncoder`. This also unblocks [phase 8](phase-8-streaming-async.md)'s UTF‑8
   sink (`EncodeUtf8`/span APIs, which `WebUtility` lacks). In 1.x nothing changes; the
   redirect seam stays encoder‑agnostic. Verified facts that drove the decision:
   - `WebUtility.HtmlEncode` escapes exactly `<` `>` `"` `'` `&` (single quote → `&#39;`)
     plus U+00A0–U+00FF and astral pairs as numeric references; every other character passes
     through. API surface is string/TextWriter only.
   - `HtmlEncoder` is safe‑list based and more aggressive: `Default` leaves only Basic Latin
     unencoded and encodes everything else (Microsoft: the defaults "use the safest encoding
     rules possible", guarding against unknown/future browser parsing bugs); it also always
     escapes `+` as UTF‑7‑attack hardening and emits `'` as hex `&#x27;`
     ([runtime source](https://github.com/dotnet/runtime/blob/release/5.0/src/libraries/System.Text.Encodings.Web/src/System/Text/Encodings/Web/HtmlEncoder.cs)).
     The safe list is widenable per host via `HtmlEncoder.Create(UnicodeRanges…)`, and it
     adds allocation‑friendly span/UTF‑8 APIs (`Encode(ReadOnlySpan<char>, Span<char>, …)`,
     `EncodeUtf8`, `FindFirstCharacterToEncode`) alongside the TextWriter overloads.
   - Switching therefore changes output bytes for **existing** `@html`/`@string` users even
     in 1.x: `&#39;` becomes `&#x27;`, `+` starts encoding, and non‑Latin text (Chinese,
     Cyrillic, …) goes from pass‑through to numeric entities unless ranges are widened —
     every HTML golden churns. Keeping `WebUtility` stays byte‑stable but leaves the
     pipeline on a weaker encoder than Razor's. (Packaging: `System.Text.Encodings.Web` is
     inbox on the `net6.0`/`net8.0`/`net10.0` targets, a package reference for
     `netstandard2.0`.)

## External grounding

Externally verified claims backing this document (checked July 2026):

- `WebUtility.HtmlEncode` "converts characters that are not allowed in HTML into
  character‑entity equivalents"
  ([WebUtility.HtmlEncode](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode));
  the exact set in the runtime source is `<` `>` `"` `'` `&` — single quote included, as
  `&#39;` — plus numeric references for U+00A0–U+00FF and surrogate pairs
  ([WebUtility.cs](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Net/WebUtility.cs)).
- Razor "automatically encodes all output sourced from variables"; its encoders default to a
  safe list "limited to the Basic Latin Unicode range" and encode everything outside it; and
  `HtmlString` is the sanctioned don't‑re‑encode escape hatch, with the same
  never‑with‑untrusted‑input warning that applies to `@raw()`
  ([Prevent XSS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cross-site-scripting)).
- `HtmlEncoder` exposes `Create(TextEncoderSettings)`/`Create(UnicodeRange[])` plus string,
  TextWriter, span, and UTF‑8 `Encode` overloads
  ([HtmlEncoder](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.htmlencoder)).
- Handlebars: "the values returned by the `{{expression}}` are HTML‑escaped"; triple‑stash
  `{{{ }}}` produces raw output
  ([Handlebars — Expressions](https://handlebarsjs.com/guide/expressions.html)).
- Phoenix (EEx/HEEx): "by default, data output in templates is not considered safe" — it is
  escaped; `raw/1` marks trusted HTML
  ([Phoenix.HTML](https://hexdocs.pm/phoenix_html/Phoenix.HTML.html)).
- Jinja2: `Environment` autoescaping is disabled unless set — the docs urge configuring it
  explicitly "instead of relying on the default"
  ([Jinja API](https://jinja.palletsprojects.com/en/stable/api/)); Flask enables it only for
  `.html`, `.htm`, `.xml`, `.xhtml`, and `.svg` templates
  ([Flask — Templates](https://flask.palletsprojects.com/en/stable/templating/)).
- Liquid: "By default output is not escaped"; escaping is the opt‑in `escape` filter
  ([LiquidJS — Escaping](https://liquidjs.com/tutorials/escaping.html)).
- Go: `html/template` "provides the same interface as text/template and should be used
  instead of text/template whenever the output is HTML", with contextual auto‑escaping —
  the stdlib precedent for a Text/Html profile split
  ([html/template](https://pkg.go.dev/html/template)).
