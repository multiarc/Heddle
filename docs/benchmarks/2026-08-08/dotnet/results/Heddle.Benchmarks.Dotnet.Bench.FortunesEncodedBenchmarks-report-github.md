```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------- |----------- |------------:|------------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| **RenderHeddle**           | **controlled** |    **739.1 ns** |     **5.16 ns** |     **3.07 ns** |  **1.00** |    **0.01** | **0.4253** | **0.0038** |   **6.95 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |    726.3 ns |     4.99 ns |     2.97 ns |  0.98 |    0.01 | 0.1373 |      - |   2.25 KB |        0.32 |
| RenderHeddleTextWriter | controlled |    640.6 ns |     8.16 ns |     4.86 ns |  0.87 |    0.01 | 0.1364 |      - |   2.24 KB |        0.32 |
| RenderFluid            | controlled |  2,182.3 ns |   120.30 ns |    71.59 ns |  2.95 |    0.09 | 0.3433 |      - |   5.61 KB |        0.81 |
| RenderScriban          | controlled | 15,308.6 ns | 3,805.09 ns | 2,264.35 ns | 20.71 |    2.91 | 4.7302 | 0.5188 |  77.42 KB |       11.13 |
| RenderDotLiquid        | controlled | 25,655.3 ns |   386.31 ns |   229.89 ns | 34.71 |    0.33 | 4.4556 | 0.2747 |  73.22 KB |       10.53 |
| RenderHandlebars       | controlled |  1,660.4 ns |   194.35 ns |   115.66 ns |  2.25 |    0.15 | 0.1354 |      - |   2.22 KB |        0.32 |
| RenderRazor            | controlled |  8,279.2 ns |   154.23 ns |    91.78 ns | 11.20 |    0.13 | 1.3275 | 0.6561 |  21.87 KB |        3.15 |
|                        |            |             |             |             |       |         |        |        |           |             |
| **RenderHeddle**           | **idiomatic**  |    **763.2 ns** |     **7.03 ns** |     **4.19 ns** |  **1.00** |    **0.01** | **0.5007** | **0.0057** |   **8.18 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |    734.9 ns |    22.65 ns |    13.48 ns |  0.96 |    0.02 | 0.1373 |      - |   2.25 KB |        0.28 |
| RenderHeddleTextWriter | idiomatic  |    648.3 ns |    27.29 ns |    16.24 ns |  0.85 |    0.02 | 0.1364 |      - |   2.24 KB |        0.27 |
| RenderFluid            | idiomatic  |  2,189.8 ns |    69.50 ns |    41.36 ns |  2.87 |    0.05 | 0.3662 |      - |      6 KB |        0.73 |
| RenderScriban          | idiomatic  | 16,562.8 ns |   485.71 ns |   289.04 ns | 21.70 |    0.38 | 4.7607 | 0.3662 |  78.61 KB |        9.61 |
| RenderDotLiquid        | idiomatic  | 25,677.9 ns |   571.67 ns |   340.19 ns | 33.65 |    0.46 | 4.5471 | 0.2136 |  74.37 KB |        9.09 |
| RenderHandlebars       | idiomatic  |  1,563.6 ns |    23.87 ns |    14.21 ns |  2.05 |    0.02 | 0.1488 |      - |   2.46 KB |        0.30 |
| RenderRazor            | idiomatic  |  8,434.6 ns |   183.55 ns |   109.22 ns | 11.05 |    0.15 | 1.3580 | 0.6714 |  22.26 KB |        2.72 |
