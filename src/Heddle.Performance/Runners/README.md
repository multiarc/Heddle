# Competitor benchmark twins (D1)

These runners render the **same output** as Heddle's `TextRenderBenchmarks` home page through
four other .NET template engines, so the render/allocation/parse numbers in
`TextRenderBenchmarks` and `TemplateParseBenchmarks` are an apples-to-apples comparison.

| Runner | Engine | Package (pinned) |
| --- | --- | --- |
| `HeddleTest` | Heddle (baseline) | — |
| `RazorTest` | ASP.NET Core Razor | `Microsoft.AspNetCore.Mvc.Razor.*` |
| `FluidTest` | Fluid (Liquid) | `Fluid.Core` 2.31.0 |
| `ScribanTest` | Scriban | `Scriban` 7.2.5 — net8.0+ legs only: 7.2.5 (the only release with the 2026 GHSA advisory fixes) requires System.Text.Json 10, which dropped net6.0, so the Scriban twins are compiled out of the net6.0 leg (`#if !NET6_0`) |
| `DotLiquidTest` | DotLiquid (Liquid) | `DotLiquid` 2.3.197 |
| `HandlebarsTest` | Handlebars.Net | `Handlebars.Net` 2.1.6 |

## Feature mapping

Every twin composes the page from the same four building blocks. `FluidTest` and `DotLiquidTest`
share a single Liquid source (`LiquidTemplates.cs`) because both consume the same dialect.

| Heddle construct | Liquid (Fluid / DotLiquid) | Scriban | Handlebars |
| --- | --- | --- | --- |
| Layout inheritance — `@<<{{layout.heddle}}` (home pulls in the layout) | `{% include 'layout' %}` (Fluid via `IFileProvider`, DotLiquid via `IFileSystem`) | `{{ include 'layout' }}` via `ITemplateLoader` | `{{> layout }}` registered partial |
| Reusable section defaults — `<meta>`, `<socialmeta>`, `<page_scripts>` … in the `@% … %@` block | `{{ section.meta }}` members | `{{ section.meta }}` members | `{{{section.meta}}}` members |
| Component call, **with argument** — `@area_component(@"Footer Links")` | `{{ areas[name] }}` map lookup | `{{ area name }}` imported .NET function | `{{area name}}` helper (`WriteSafeString`) |
| Component call, **no argument** — `@head_scripts()`, `@custom_styles()` | `{{ comp.head_scripts }}` map lookup | `{{ comp.head_scripts }}` member | `{{{comp.head_scripts}}}` member |
| List loop over the ordered area menus — Heddle issues the `@area_component` calls in document order | `{% for name in area_names %}…{% endfor %}` | `{{ for name in area_names }}…{{ end }}` | `{{#each area_names}}…{{/each}}` |

The area-menu fragments are not transcribed into any template language: every twin reads them from
`AreaComponent.Areas` (the exact dictionary Heddle renders from) via `TwinContent.Areas`, so a twin
cannot silently drift from the engine it is compared against.

Handlebars uses triple mustaches (`{{{ }}}`) for the HTML fragments because its double-mustache form
HTML-encodes; Fluid renders with its default raw encoder, and Scriban/DotLiquid do not encode — all
matching Heddle's raw output.

## Parity rule (D1-R3)

Before any timing, `ParityCheck.Assert()` runs inside `TextRenderBenchmarks.GlobalSetup` and throws
if any twin diverges from Heddle, so a drifted twin fails loudly instead of benchmarking different
work. The comparison is byte-for-byte after **one documented normalization** (`TwinContent.Normalize`):

> Unify line endings to `\n`, collapse every run of whitespace that sits **between two tags**
> (`>` … `<`) to nothing, then trim. Nothing else is altered, so only a genuine tag/text
> difference can survive.

Run the check on its own (fast, no BenchmarkDotNet/host spin-up):

```
dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- parity
```

Confirmed output — all four twins equal Heddle (34,837 normalized chars):

```
[PASS] Fluid       55456 raw chars, 34837 normalized chars == Heddle
[PASS] Scriban     55456 raw chars, 34837 normalized chars == Heddle
[PASS] DotLiquid   55456 raw chars, 34837 normalized chars == Heddle
[PASS] Handlebars  55456 raw chars, 34837 normalized chars == Heddle
```

## Dimensions measured (D1-R4/R5)

* **Render time + allocations** — `TextRenderBenchmarks` (`[MemoryDiagnoser]`), one `[Benchmark]` per
  engine, Heddle marked `[Benchmark(Baseline = true)]` so ratios read directly. Each twin reuses its
  parsed/compiled template across iterations (the cached-render path).
* **Cold parse/compile cost** — `TemplateParseBenchmarks`, one `[Benchmark]` per engine
  (`ParseHeddle` baseline, `ParseFluid`, `ParseScriban`, `ParseDotLiquid`, `ParseHandlebars`), each
  parsing/compiling the layout + home templates from source.

## Fidelity note — what the composed page actually contains

The rendered page is fully static: the model is an empty `object` and every Heddle extension returns
a fixed string. Under the current engine, rendering `home.heddle` (which extends `layout.heddle`)
emits the reusable-section defaults and the component/area calls **in order**, but not the layout's
literal HTML skeleton, the account-link block, or the overridden `<body:body>` slider (the
`@body()` call resolves to the empty default). The twins therefore reproduce exactly that ordered
fragment sequence:

```
section.meta → section.social
→ assets_styles → custom_styles → head_scripts → body_scripts
→ [loop] Alert-Above, Secondary-Wholesale, Secondary-Retail, Wholesale-Mega,
         Retail-Mega, Alert-Below (empty), Footer-Links
→ assets_scripts → page_scripts (empty) → endpage_scripts (empty) → body_end_scripts
```

This keeps the parity comparison honest (all five engines render byte-identical output). Note that
`RazorTest` is a pre-existing twin that renders the full HTML page from `Views/home.cshtml` and is
**not** under the parity assertion; bringing the Heddle corpus and the Razor view back into a single
composed page is a separate template-authoring concern outside D1's scope.

### Root cause (investigated for D2 — no engine change)

The omission is **specific to the `@<<` layout-extend path when the extending document is the
compile entry point**, not to the templates or the harness wiring. Confirmed empirically: compiling
`layout.heddle` *directly* as the entry point (`TemplateOptions("layout")`) renders the full ~60 KB
composed page — `<!DOCTYPE html>`, the header/account-link chrome, `@body()`, and the footer all
present. Compiling `home.heddle` (which does `@<<{{layout.heddle}}`) as the entry point instead
emits only the section defaults + component/area calls (output begins at `<title>Title</title>`,
not `<!DOCTYPE html>`), and `@body()` resolves to its empty default so the `<body:body>` slider is
absent. Making `home` render the composed page therefore requires an engine change to the `@<<`
composition semantics, which is out of scope for the D benchmark workstream. Swapping the entry to
`layout` would render more chrome but drops the `<body:body>` override and would force every twin to
embed the layout's literal HTML verbatim (parity-drift risk) — so the workload is left as the
parity-asserted fragment sequence and documented precisely here and in the published numbers. The
non-negotiable property (all five engines do identical, parity-checked work) holds regardless.

## Contract v2

The parity rule above (contract v1) remains authoritative for the intra-.NET raw suites exactly
as written. The cross-stack benchmark program extends it — not replaces it — with
[parity contract v2](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md)
(defined output encoding, the N3b whitespace strip applied to both sides at comparison time, and
entity canonicalization for the encoded suite) and with the committed
[golden oracle corpus](../GoldenCorpus/README.md) that phases 2–6 gate against
(`export-corpus` / `verify-corpus` verbs, freshness-asserted in every render benchmark's
`[GlobalSetup]` via `GoldenCorpus.AssertFresh`).
