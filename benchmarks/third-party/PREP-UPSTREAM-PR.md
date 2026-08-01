# Prepped upstream PR — Heddle entry for the Fluid benchmark harness (D3-R1)

**Status: prepared, NOT submitted.** This document is the ready-to-submit content for an upstream
pull request that adds a Heddle entry to the Fluid repository's benchmark suite. The same entry is
already proven here as the D3-R3 fallback (`../ThirdParty`, parity-verified). Submit when ready;
D3 stays open until this PR merges upstream (D3-R4).

## Target

| | |
| --- | --- |
| Repository | https://github.com/sebastienros/fluid |
| Benchmark project | `Fluid.Benchmarks/` (repo root) |
| Base commit | `1fa7c0bc0d86b51279272da4438a802650b8a89b` (rebase onto current `main` before submitting) |
| Methodology change | **none** — same `BaseBenchmarks` model, same `CheckBenchmark()` output oracle, same BenchmarkDotNet job/categories. The Heddle entry is purely additive. |
| Engine version to pin | `Heddle` **2.2.0** (the latest published engine package). No other dependency changes. |

The entry follows the exact pattern every other engine uses: a `*Benchmarks : BaseBenchmarks` class
whose constructor parses the template and calls `CheckBenchmark()`, implementing
`Parse` / `ParseBig` / `Render` / `ParseAndRender`, wired into `ComparisonBenchmarks` under the
`Parse` and `Render` categories. `ParseBig` throws `NotSupportedException` and is left out of the
`ParseBig` category — identical to the existing `HandlebarsBenchmarks`.

> **Namespace note.** The benchmark project's namespace is `Fluid.Benchmarks`, so an unqualified
> `TemplateOptions` binds to `Fluid.TemplateOptions`. The Heddle types below are therefore
> **fully qualified** (`Heddle.Data.TemplateOptions`, etc.) so the new file compiles in that project
> with no `using`-alias gymnastics.

---

## 1. New file — `Fluid.Benchmarks/product.heddle`

```heddle
@using(){{Fluid.Benchmarks}}
@%
<product_row>
{{    <li>
      <h2>@(Name)</h2>
           Only @(Price)
           @(truncate(Description, 15))
    </li>
}} :: Fluid.Benchmarks.Product
%@<ul id='products'>
@list(Products){{@product_row(this)}}</ul>
```

Idiomatic Heddle: `<product_row>` is a **definition** (the partial idiom) invoked per element by
**`@list(Products)`** (the loop idiom); `@(truncate(Description, 15))` calls a registered function
that mirrors the Liquid `truncate` filter.

## 2. New file — `Fluid.Benchmarks/HeddleProductModel.cs`

```csharp
using System.Collections.Generic;

namespace Fluid.Benchmarks
{
    // Typed root model: the exact same List<Product> the other engines receive, exposed as `Products`
    // (the analogue of the `products` context value Fluid/Scriban/DotLiquid/Handlebars each bind).
    public sealed class HeddleProductModel
    {
        public HeddleProductModel(List<Product> products) => Products = products;
        public List<Product> Products { get; }
    }
}
```

## 3. New file — `Fluid.Benchmarks/HeddleFilters.cs`

```csharp
using System;

namespace Fluid.Benchmarks
{
    // Registered on the template's FunctionRegistry. `truncate(value, length)` mirrors the Liquid
    // `truncate` filter: when longer than `length`, keep the first (length - 3) chars and append "...".
    public static class HeddleFilters
    {
        private const string Ellipsis = "...";

        public static string Truncate(string value, int length)
        {
            value ??= string.Empty;
            if (value.Length <= length) return value;
            var keep = Math.Max(0, length - Ellipsis.Length);
            return string.Concat(value.AsSpan(0, keep), Ellipsis);
        }
    }
}
```

## 4. New file — `Fluid.Benchmarks/HeddleBenchmarks.cs`

```csharp
using System;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class HeddleBenchmarks : BaseBenchmarks
    {
        private static readonly string ProductTemplateHeddle;
        private static readonly Heddle.Runtime.Expressions.FunctionRegistry Functions;

        private readonly HeddleProductModel _model;
        private readonly Heddle.HeddleTemplate _heddleTemplate;

        static HeddleBenchmarks()
        {
            // Built-ins are invariant by design; pin thread culture so float prices render "0".."99".
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            Heddle.HeddleTemplate.Configure(typeof(HeddleBenchmarks).Assembly);

            var assembly = typeof(HeddleBenchmarks).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Fluid.Benchmarks.product.heddle"))
            using (var streamReader = new StreamReader(stream))
            {
                ProductTemplateHeddle = streamReader.ReadToEnd();
            }

            // The whole trust boundary for a Native-mode template: exactly one host function, `truncate`.
            Functions = new Heddle.Runtime.Expressions.FunctionRegistry();
            Functions.Register("truncate", typeof(HeddleFilters).GetMethod(nameof(HeddleFilters.Truncate)));
        }

        public HeddleBenchmarks()
        {
            _model = new HeddleProductModel(Products);
            _heddleTemplate = Compile(ProductTemplateHeddle);
            CheckBenchmark();
        }

        private static Heddle.HeddleTemplate Compile(string template)
        {
            var options = new Heddle.Data.TemplateOptions
            {
                ExpressionMode = Heddle.Data.ExpressionMode.Native,   // the sandbox-safe tier — no C# escape
                Functions = Functions,
            };
            var compiled = new Heddle.HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(options, typeof(HeddleProductModel)));
            if (!compiled.CompileResult.Success)
                throw new InvalidOperationException(
                    "Heddle product template failed to compile: " +
                    string.Join("; ", compiled.CompileResult.ErrorList));
            return compiled;
        }

        [Benchmark]
        public override object Parse() => Compile(ProductTemplateHeddle);

        // Parity with HandlebarsBenchmarks (no big-template twin); excluded from the ParseBig category.
        public override object ParseBig() => throw new NotSupportedException();

        [Benchmark]
        public override string Render() => _heddleTemplate.Generate(_model);

        [Benchmark]
        public override string ParseAndRender() => Compile(ProductTemplateHeddle).Generate(_model);
    }
}
```

## 5. Edit — `Fluid.Benchmarks/ComparisonBenchmarks.cs`

Add the field and two benchmark methods (Handlebars-style: no `ParseBig`):

```csharp
        private readonly HeddleBenchmarks _heddleBenchmarks = new HeddleBenchmarks();

        [Benchmark, BenchmarkCategory("Parse")]
        public object Heddle_Parse()
        {
            return _heddleBenchmarks.Parse();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string Heddle_Render()
        {
            return _heddleBenchmarks.Render();
        }
```

## 6. Edit — `Fluid.Benchmarks/Fluid.Benchmarks.csproj`

Add the package reference and embed the template (the project uses central package management):

```xml
  <ItemGroup>
    <EmbeddedResource Include="product.heddle" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Heddle" />
  </ItemGroup>
```

## 7. Edit — `Directory.Packages.props`

Add the central version pin next to the other benchmark packages:

```xml
    <PackageVersion Include="Heddle" Version="2.2.0" />
```

---

## Submission steps

1. Fork `sebastienros/fluid`; create a branch, e.g. `benchmarks/add-heddle`.
2. Apply sections 1–7 above. (The bodies of files 1–4 are byte-identical to the parity-verified
   fallback here; only the fallback's `HeddleBenchmarks.cs`/model/filter live in a different C#
   namespace to dodge a local `Fluid.TemplateOptions` clash — upstream they belong in
   `Fluid.Benchmarks` with the Heddle types fully qualified, exactly as written above.)
3. Build: `dotnet build Fluid.Benchmarks/Fluid.Benchmarks.csproj -c Release` — expect zero warnings.
4. Smoke-test the output oracle without a full run — construct `new HeddleBenchmarks()`; its
   constructor calls `CheckBenchmark()` and throws on any drift. Then optionally run the head-to-head:
   `dotnet run -c Release --project Fluid.Benchmarks -- --filter *ComparisonBenchmarks* --category Render`.
5. Open the PR against `main`. Suggested description:
   > Adds a Heddle entry to the template-engine comparison. Implements the existing product scenario
   > idiomatically (a `@list` loop over a `<product_row>` definition, a registered `truncate` function
   > mirroring the Liquid filter), pinned to Heddle 2.2.0. No methodology changes: same
   > `BaseBenchmarks` model, same `CheckBenchmark()` oracle, same job/categories; `ParseBig` is
   > `NotSupportedException` and omitted from that category, matching `HandlebarsBenchmarks`.
6. After it merges, close D3 (D3-R4). Until then, D3 remains open with the fallback published here.
