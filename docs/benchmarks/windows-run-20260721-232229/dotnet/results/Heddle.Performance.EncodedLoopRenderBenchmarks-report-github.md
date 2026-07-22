```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------- |---------:|----------:|----------:|------:|--------:|----------:|---------:|---------:|----------:|------------:|
| RenderHeddle     | 1.988 ms | 0.0364 ms | 0.0340 ms |  1.00 |    0.02 |  281.2500 | 121.0938 | 121.0938 |   5.72 MB |        1.00 |
| RenderFluid      | 2.964 ms | 0.0104 ms | 0.0098 ms |  1.49 |    0.03 |  335.9375 | 226.5625 |  62.5000 |   5.86 MB |        1.02 |
| RenderScriban    | 5.862 ms | 0.0209 ms | 0.0195 ms |  2.95 |    0.05 |  695.3125 | 460.9375 | 125.0000 |  12.12 MB |        2.12 |
| RenderDotLiquid  | 8.279 ms | 0.0410 ms | 0.0383 ms |  4.17 |    0.07 | 1843.7500 | 843.7500 |  62.5000 |  29.99 MB |        5.25 |
| RenderHandlebars | 1.925 ms | 0.0340 ms | 0.0284 ms |  0.97 |    0.02 |  160.1563 | 121.0938 |  62.5000 |   3.11 MB |        0.54 |
