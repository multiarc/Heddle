# Third-party benchmark fallback — Fluid harness (D3-R3)

This folder is the **D3-R3 fallback**: the Fluid repository's benchmark scenario, vendored
**unmodified**, with an idiomatic **Heddle** entry added. It exists so Heddle's numbers on the
de-facto .NET template-engine comparison stay *third-party-methodology* even while the upstream
PR (the D3-R1 deliverable) is prepared-but-not-submitted. See
[`PREP-UPSTREAM-PR.md`](PREP-UPSTREAM-PR.md) for the exact upstream change and submission steps.

## Upstream harness (recorded per D3-R3 / D3-R4)

| | |
| --- | --- |
| Repository | https://github.com/sebastienros/fluid (MIT — see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)) |
| Benchmark project | `Fluid.Benchmarks/` (at the repo root) |
| **Commit SHA** | `1fa7c0bc0d86b51279272da4438a802650b8a89b` |
| Commit date | 2026-06-22 |
| Methodology | BenchmarkDotNet `0.15.8`, `[MemoryDiagnoser]`, `ShortRunJob`, `GroupBenchmarksBy(ByCategory)` |

## Tracking status (D3-R4)

D3 is an **external** item: *done = merged upstream **or** fallback published*.

- **Fallback: published** — this folder (builds, runs, parity-verified; see below).
- **Upstream PR: prepped, not submitted** — the ready-to-submit entry + instructions are in
  [`PREP-UPSTREAM-PR.md`](PREP-UPSTREAM-PR.md). D3 stays open until that PR merges upstream.

## The scenario

`Vendor/product.liquid` renders a `<ul>` of 100 products; each row is the product name, its price,
and its description truncated to 15 characters:

```liquid
<ul id='products'>
  {% for product in products %}
    <li>
      <h2>{{ product.name }}</h2>
           Only {{ product.price }}
           {{ product.description | truncate: 15 }}
    </li>
  {% endfor %}
</ul>
```

The model is 100 `Product { Name = "Name{i}", Price = {i}, Description = <the Lorem passage> }`
objects (`Vendor/BaseBenchmarks.cs`). The upstream output oracle is
`BaseBenchmarks.CheckBenchmark()`: the render must contain `<h2>Name0</h2>`, `<h2>Name99</h2>`,
`Lorem ipsum ...`, `Only 0`, and `Only 99`. Every engine entry (including Heddle) runs it in its
constructor, so a drifted entry throws before it is ever timed — this is the upstream harness's own
parity discipline, and every competitor entry (Fluid/Scriban/DotLiquid/Handlebars) satisfies exactly
this, each on its own idiomatic template (Handlebars even uses a different `.mustache` template).

## What is vendored vs. added

**Vendored byte-for-byte from the pinned commit (do not edit — re-sync from upstream instead):**

```
Vendor/product.liquid          Vendor/BaseBenchmarks.cs      Vendor/FluidBenchmarks.cs
Vendor/product.mustache        Vendor/Product.cs             Vendor/ScribanBenchmarks.cs
Vendor/blogpost.liquid                                       Vendor/DotLiquidBenchmarks.cs
                                                             Vendor/HandlebarsBenchmarks.cs
```

**Added (the Heddle entry + local harness wiring):**

```
product.heddle                 the idiomatic Heddle template (definition partial + @list)
HeddleProductModel.cs          typed root model wrapping the exact same List<Product>
HeddleFilters.cs               the `truncate` host function (mirrors the Liquid filter)
HeddleBenchmarks.cs            the Heddle entry (: BaseBenchmarks) — the D3-R1 PR body
HeddleComparisonBenchmarks.cs  the head-to-head (Fluid baseline + Scriban/DotLiquid/Handlebars/Heddle)
Parity.cs, Program.cs          the parity self-test + console host (local, not part of the scenario)
```

Program.cs is not the upstream `Program.cs` (a bare `BenchmarkSwitcher` shim); it adds the
host-free `parity` self-test. `HeddleComparisonBenchmarks` replaces the upstream `ComparisonBenchmarks`
so this fallback does not pull in the abandoned, vulnerability-flagged **Liquid.NET** package that
upstream references but already leaves un-benchmarked ("Ignored since Liquid.NET is much slower and
not in active development"). Neither file changes the scenario, the model, or the output oracle.

## The Heddle entry (idiomatic — D3-R1)

`product.heddle` maps the scenario to idiomatic Heddle: the per-row markup is a **definition**
(`<product_row>` — the Heddle analogue of a partial), invoked once per element by **`@list(Products)`**
(the Heddle analogue of `{% for %}` / `{{#each}}`). It runs under **`ExpressionMode.Native`** with a
curated `FunctionRegistry` whose single entry, `truncate`, mirrors the Liquid `truncate` filter — the
sandbox-safe expression tier, and the direct counterpart of the other engines' filter/helper.

| Heddle construct | Liquid (Fluid / DotLiquid) | Scriban | Handlebars |
| --- | --- | --- | --- |
| `@list(Products){{ … }}` loop | `{% for product in products %}…{% endfor %}` | `{{ for … }}…{{ end }}` (`ParseLiquid`) | `{{#products}}…{{/products}}` |
| `<product_row>` definition (partial), `@product_row(this)` | inline `<li>` body | inline `<li>` body | inline `<li>` body |
| `@(Name)` / `@(Price)` member paths | `{{ product.name }}` / `{{ product.price }}` | `{{ product.name }}` | `{{ name }}` / `{{ price }}` |
| `@(truncate(Description, 15))` registered function | `{{ … \| truncate: 15 }}` filter | `{{ … \| string.truncate 15 }}` | `{{#truncate 15}}…{{/truncate}}` helper |
| model `HeddleProductModel { List<Product> Products }` | `products` context value | `products` script-object value | `products` anonymous value |

**Engine version pinned:** Heddle is referenced from source (`..\..\Heddle\Heddle.csproj`); the
released package equivalent is `Heddle` **2.2.0** (this branch). No engine dependency changes — only
a `ProjectReference`.

**Model note:** the same 100 `Product` objects the other engines receive are wrapped in a small typed
root (`HeddleProductModel`) exposing them as `Products` — the exact analogue of how DotLiquid wraps
them in a `Hash`, Scriban in a `ScriptObject`, and Handlebars in an anonymous object. Same data, no
methodology change.

## Package versions (pinned)

Every competitor version equals the upstream harness's `Directory.Packages.props` at the pinned
commit; Fluid is the released package matching the vendored (Fluid 3.0-API) benchmark code.

| Package | Version | Source of the pin |
| --- | --- | --- |
| BenchmarkDotNet | `0.15.8` | upstream `Directory.Packages.props` |
| Scriban | `7.2.5` | upstream `Directory.Packages.props` |
| DotLiquid | `2.3.197` | upstream `Directory.Packages.props` |
| Handlebars.Net | `2.1.6` | upstream `Directory.Packages.props` |
| Fluid.Core | `3.0.0-beta.5` | matches the Fluid 3.0 API the vendored `FluidBenchmarks.cs` uses (`OutputBufferSize`, `StringComparers`) |

## Parity verification (mirrors D1's discipline)

Before any numbers are published, `Parity.Report()`:

1. Constructs every engine entry, which runs each one's `CheckBenchmark()` output oracle.
2. Renders the Heddle entry and the **Fluid reference** (Fluid rendering the *unmodified*
   `product.liquid`) and asserts they are equal after one documented normalization: unify line
   endings, collapse every run of whitespace to a single space, then trim — the standard
   whitespace-insensitive HTML comparison, so only a genuine token/text difference can survive.
3. Confirms the other Liquid engines and Handlebars also match the reference (self-consistency).

Confirmed output — Heddle matches the Fluid reference (5,104 normalized chars):

```
D3-R3 parity check — Heddle entry vs Fluid reference on the upstream product scenario
  (normalized: unify newlines, collapse whitespace runs to one space, trim)
  [PASS] CheckBenchmark output oracle: Fluid, Scriban, DotLiquid, Handlebars, Heddle
  Fluid  reference: 9008 raw chars, 5104 normalized chars
  Heddle entry:     8705 raw chars, 5104 normalized chars
  [PASS] Heddle == Fluid reference (normalized)
  [PASS] Scriban    == Fluid reference (normalized)
  [PASS] DotLiquid  == Fluid reference (normalized)
  [PASS] Handlebars == Fluid reference (normalized)
PARITY OK.
```

(The raw-char difference is only insignificant leading/trailing/inter-row whitespace — legitimate
cross-engine whitespace variance, the same reason the upstream harness compares via the
`CheckBenchmark` substrings rather than byte-identity.)

## Running it

This project is **standalone** — it is intentionally not added to `Heddle.sln` (that is a
repo-root change outside this work item's scope). Build and run it directly:

```bash
# build (warning-clean)
dotnet build src/Heddle.Performance/ThirdParty/Heddle.Performance.ThirdParty.csproj -c Release

# fast, host-free parity self-test (proves the Heddle entry renders the scenario's expected output)
dotnet run -c Release --project src/Heddle.Performance/ThirdParty -- parity

# eyeball the Heddle-rendered scenario
dotnet run -c Release --project src/Heddle.Performance/ThirdParty -- dump

# the full head-to-head (BenchmarkDotNet; writes to ./BenchmarkDotNet.Artifacts under this folder)
dotnet run -c Release --project src/Heddle.Performance/ThirdParty -- --filter *HeddleComparisonBenchmarks*
```

## Re-syncing the vendored files

To refresh against a newer upstream commit: re-copy the files under `Vendor/` byte-for-byte from
`Fluid.Benchmarks/` at the new SHA, update the SHA table above and in `THIRD-PARTY-NOTICES.md`, bump
the pinned package versions to match the new `Directory.Packages.props`, and re-run `-- parity`.
