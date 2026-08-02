```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|----------------- |----------- |----------:|----------:|----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **RenderHeddle**     | **controlled** |  **27.52 μs** |  **1.488 μs** |  **0.886 μs** |  **1.00** |    **0.04** |  **6.0120** |  **1.1902** |   **98.62 KB** |        **1.00** |
| RenderFluid      | controlled |  40.10 μs |  0.934 μs |  0.556 μs |  1.46 |    0.05 |  3.2959 |  0.1221 |   54.94 KB |        0.56 |
| RenderScriban    | controlled | 114.25 μs | 16.997 μs | 10.115 μs |  4.16 |    0.37 | 17.5781 |  1.9531 |  301.96 KB |        3.06 |
| RenderDotLiquid  | controlled | 349.54 μs | 18.277 μs | 10.876 μs | 12.71 |    0.53 | 90.8203 |       - | 1485.26 KB |       15.06 |
| RenderHandlebars | controlled |  34.61 μs |  0.915 μs |  0.545 μs |  1.26 |    0.04 |  4.0894 |  0.3662 |   67.08 KB |        0.68 |
| RenderRazor      | controlled |  38.80 μs |  2.846 μs |  1.694 μs |  1.41 |    0.07 |  5.6152 |  2.8076 |   93.04 KB |        0.94 |
|                  |            |           |           |           |       |         |         |         |            |             |
| **RenderHeddle**     | **idiomatic**  |  **33.28 μs** |  **0.929 μs** |  **0.553 μs** |  **1.00** |    **0.02** |  **5.9814** |  **1.1597** |   **98.62 KB** |        **1.00** |
| RenderFluid      | idiomatic  |  41.38 μs |  1.350 μs |  0.803 μs |  1.24 |    0.03 |  4.6387 |       - |   76.83 KB |        0.78 |
| RenderScriban    | idiomatic  | 115.70 μs | 16.954 μs | 10.089 μs |  3.48 |    0.29 | 20.8740 |  4.2725 |  341.52 KB |        3.46 |
| RenderDotLiquid  | idiomatic  | 358.51 μs | 23.967 μs | 14.263 μs | 10.77 |    0.44 | 93.7500 | 17.5781 | 1538.55 KB |       15.60 |
| RenderHandlebars | idiomatic  |  37.08 μs |  1.333 μs |  0.793 μs |  1.11 |    0.03 |  7.2021 |  0.9766 |  118.02 KB |        1.20 |
| RenderRazor      | idiomatic  |  25.43 μs |  2.565 μs |  1.527 μs |  0.76 |    0.05 |  8.9417 |  2.2278 |   146.8 KB |        1.49 |
