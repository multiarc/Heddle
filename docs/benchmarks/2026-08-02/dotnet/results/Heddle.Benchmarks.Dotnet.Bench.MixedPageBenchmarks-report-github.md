```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean      | Error      | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------- |----------- |----------:|-----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| **RenderHeddle**     | **controlled** | **11.055 μs** |  **1.9116 μs** | **1.1376 μs** |  **1.01** |    **0.13** |  **4.1351** | **0.8240** |  **67.98 KB** |        **1.00** |
| RenderFluid      | controlled |  9.114 μs |  0.3494 μs | 0.2079 μs |  0.83 |    0.08 |  1.7700 |      - |  28.99 KB |        0.43 |
| RenderScriban    | controlled | 59.695 μs |  2.3748 μs | 1.4132 μs |  5.45 |    0.50 | 27.3438 | 2.4414 | 453.25 KB |        6.67 |
| RenderDotLiquid  | controlled | 56.490 μs |  4.7344 μs | 2.8173 μs |  5.15 |    0.52 | 17.3340 | 1.3428 | 283.67 KB |        4.17 |
| RenderHandlebars | controlled |  7.382 μs |  0.1830 μs | 0.1089 μs |  0.67 |    0.06 |  2.6703 | 0.1984 |  43.83 KB |        0.64 |
| RenderRazor      | controlled | 19.762 μs |  0.1375 μs | 0.0818 μs |  1.80 |    0.16 |  3.7842 | 1.2207 |  62.54 KB |        0.92 |
|                  |            |           |            |           |       |         |         |        |           |             |
| **RenderHeddle**     | **idiomatic**  | **13.944 μs** |  **0.2180 μs** | **0.1297 μs** |  **1.00** |    **0.01** |  **4.1199** | **0.8240** |  **67.98 KB** |        **1.00** |
| RenderFluid      | idiomatic  |  9.325 μs |  0.6530 μs | 0.3886 μs |  0.67 |    0.03 |  2.1667 |      - |  35.49 KB |        0.52 |
| RenderScriban    | idiomatic  | 73.779 μs | 14.3365 μs | 8.5314 μs |  5.29 |    0.58 | 29.2969 | 0.9766 | 481.21 KB |        7.08 |
| RenderDotLiquid  | idiomatic  | 57.984 μs |  5.6611 μs | 3.3689 μs |  4.16 |    0.23 | 18.7378 | 2.1973 | 306.88 KB |        4.51 |
| RenderHandlebars | idiomatic  | 10.599 μs |  0.2741 μs | 0.1631 μs |  0.76 |    0.01 |  4.0588 | 0.4425 |  66.41 KB |        0.98 |
| RenderRazor      | idiomatic  | 15.918 μs |  1.3387 μs | 0.7966 μs |  1.14 |    0.06 |  5.2490 | 1.3123 |  85.91 KB |        1.26 |
