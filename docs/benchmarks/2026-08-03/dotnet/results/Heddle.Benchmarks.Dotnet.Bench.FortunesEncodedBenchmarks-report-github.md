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
| **RenderHeddle**           | **controlled** |    **729.6 ns** |    **15.24 ns** |     **9.07 ns** |  **1.00** |    **0.02** | **0.4253** | **0.0038** |   **6.95 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |    727.5 ns |    10.04 ns |     5.97 ns |  1.00 |    0.01 | 0.1373 |      - |   2.25 KB |        0.32 |
| RenderHeddleTextWriter | controlled |    651.4 ns |    12.18 ns |     7.25 ns |  0.89 |    0.01 | 0.1364 |      - |   2.24 KB |        0.32 |
| RenderFluid            | controlled |  2,132.8 ns |    29.58 ns |    17.60 ns |  2.92 |    0.04 | 0.3433 |      - |   5.61 KB |        0.81 |
| RenderScriban          | controlled | 16,132.0 ns | 1,704.17 ns | 1,014.12 ns | 22.11 |    1.34 | 4.6387 | 0.4883 |  77.42 KB |       11.13 |
| RenderDotLiquid        | controlled | 25,046.4 ns |   223.93 ns |   133.25 ns | 34.33 |    0.44 | 4.4556 | 0.2747 |  73.22 KB |       10.53 |
| RenderHandlebars       | controlled |  1,686.4 ns |    86.08 ns |    51.23 ns |  2.31 |    0.07 | 0.1354 |      - |   2.22 KB |        0.32 |
| RenderRazor            | controlled |  8,232.7 ns |    90.09 ns |    53.61 ns | 11.29 |    0.15 | 1.3275 | 0.6561 |  21.87 KB |        3.15 |
|                        |            |             |             |             |       |         |        |        |           |             |
| **RenderHeddle**           | **idiomatic**  |    **769.4 ns** |     **8.01 ns** |     **4.77 ns** |  **1.00** |    **0.01** | **0.5007** | **0.0057** |   **8.18 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |    729.1 ns |    13.58 ns |     8.08 ns |  0.95 |    0.01 | 0.1373 |      - |   2.25 KB |        0.28 |
| RenderHeddleTextWriter | idiomatic  |    664.5 ns |    30.51 ns |    18.15 ns |  0.86 |    0.02 | 0.1364 |      - |   2.24 KB |        0.27 |
| RenderFluid            | idiomatic  |  2,173.4 ns |    59.90 ns |    35.64 ns |  2.82 |    0.05 | 0.3662 |      - |      6 KB |        0.73 |
| RenderScriban          | idiomatic  | 16,694.1 ns |   590.35 ns |   351.31 ns | 21.70 |    0.45 | 4.7607 | 0.3662 |  78.61 KB |        9.61 |
| RenderDotLiquid        | idiomatic  | 25,178.1 ns |   198.39 ns |   118.06 ns | 32.73 |    0.24 | 4.5471 | 0.2136 |  74.37 KB |        9.09 |
| RenderHandlebars       | idiomatic  |  1,643.1 ns |   157.76 ns |    93.88 ns |  2.14 |    0.12 | 0.1488 |      - |   2.46 KB |        0.30 |
| RenderRazor            | idiomatic  |  8,315.5 ns |    75.53 ns |    44.94 ns | 10.81 |    0.08 | 1.3580 | 0.6714 |  22.26 KB |        2.72 |
