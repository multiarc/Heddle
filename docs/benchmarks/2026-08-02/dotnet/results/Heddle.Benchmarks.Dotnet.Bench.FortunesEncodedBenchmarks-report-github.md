```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------- |----------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **RenderHeddle**     | **controlled** |  **2.322 μs** | **0.0395 μs** | **0.0235 μs** |  **1.00** |    **0.01** | **4.0321** | **0.8049** |  **66.13 KB** |        **1.00** |
| RenderFluid      | controlled |  2.122 μs | 0.0614 μs | 0.0365 μs |  0.91 |    0.02 | 0.3433 |      - |   5.61 KB |        0.08 |
| RenderScriban    | controlled | 16.891 μs | 0.4526 μs | 0.2693 μs |  7.27 |    0.13 | 4.6387 | 0.4883 |  77.42 KB |        1.17 |
| RenderDotLiquid  | controlled | 25.612 μs | 1.6547 μs | 0.9847 μs | 11.03 |    0.42 | 4.4556 | 0.2747 |  73.22 KB |        1.11 |
| RenderHandlebars | controlled |  1.674 μs | 0.1632 μs | 0.0971 μs |  0.72 |    0.04 | 0.1335 |      - |   2.22 KB |        0.03 |
| RenderRazor      | controlled |  8.660 μs | 0.8003 μs | 0.4763 μs |  3.73 |    0.20 | 1.3275 | 0.6561 |  21.87 KB |        0.33 |
|                  |            |           |           |           |       |         |        |        |           |             |
| **RenderHeddle**     | **idiomatic**  |  **2.517 μs** | **0.0369 μs** | **0.0219 μs** |  **1.00** |    **0.01** | **4.0321** | **0.8049** |  **66.13 KB** |        **1.00** |
| RenderFluid      | idiomatic  |  2.154 μs | 0.0795 μs | 0.0473 μs |  0.86 |    0.02 | 0.3662 |      - |      6 KB |        0.09 |
| RenderScriban    | idiomatic  | 16.182 μs | 2.3114 μs | 1.3755 μs |  6.43 |    0.52 | 4.7607 | 0.3662 |  78.61 KB |        1.19 |
| RenderDotLiquid  | idiomatic  | 25.527 μs | 1.6018 μs | 0.9532 μs | 10.14 |    0.37 | 4.5471 | 0.2136 |  74.37 KB |        1.12 |
| RenderHandlebars | idiomatic  |  1.754 μs | 0.2530 μs | 0.1505 μs |  0.70 |    0.06 | 0.1488 |      - |   2.46 KB |        0.04 |
| RenderRazor      | idiomatic  |  8.633 μs | 0.8178 μs | 0.4866 μs |  3.43 |    0.19 | 1.3580 | 0.6714 |  22.26 KB |        0.34 |
