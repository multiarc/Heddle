```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated | Alloc Ratio |
|----------------- |-----------:|---------:|---------:|------:|--------:|----------:|---------:|--------:|----------:|------------:|
| RenderHeddle     |   514.7 μs |  5.18 μs |  4.84 μs |  1.00 |    0.01 |   54.6875 |  32.2266 | 32.2266 |   1.14 MB |        1.00 |
| RenderFluid      |   799.0 μs | 10.35 μs |  9.68 μs |  1.55 |    0.02 |   94.7266 |  50.7813 | 16.6016 |   1.63 MB |        1.42 |
| RenderScriban    | 1,394.4 μs |  6.64 μs | 15.78 μs |  2.71 |    0.04 |  140.6250 |  62.5000 | 31.2500 |   2.72 MB |        2.38 |
| RenderDotLiquid  | 3,733.7 μs | 24.99 μs | 20.87 μs |  7.25 |    0.08 | 1046.8750 | 453.1250 | 15.6250 |  16.85 MB |       14.72 |
| RenderHandlebars |   668.3 μs | 12.95 μs | 15.42 μs |  1.30 |    0.03 |   55.6641 |  30.2734 | 15.6250 |   1.01 MB |        0.88 |
