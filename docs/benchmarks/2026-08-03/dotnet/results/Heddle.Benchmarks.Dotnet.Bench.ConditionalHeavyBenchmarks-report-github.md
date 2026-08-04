```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|----------------------- |----------- |----------:|----------:|---------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **RenderHeddle**           | **controlled** |  **14.74 μs** |  **0.562 μs** | **0.335 μs** |  **1.00** |    **0.03** |  **6.0120** |  **0.6561** |   **98.43 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |  18.12 μs |  0.193 μs | 0.115 μs |  1.23 |    0.03 |  2.1057 |       - |   34.55 KB |        0.35 |
| RenderHeddleTextWriter | controlled |  14.28 μs |  0.316 μs | 0.188 μs |  0.97 |    0.02 |  2.1057 |       - |   34.55 KB |        0.35 |
| RenderFluid            | controlled |  39.27 μs |  0.903 μs | 0.537 μs |  2.66 |    0.07 |  3.2959 |  0.1221 |   54.94 KB |        0.56 |
| RenderScriban          | controlled | 114.73 μs | 15.396 μs | 9.162 μs |  7.79 |    0.61 | 17.5781 |  1.9531 |  301.96 KB |        3.07 |
| RenderDotLiquid        | controlled | 344.39 μs |  4.813 μs | 2.864 μs | 23.37 |    0.53 | 90.8203 |       - | 1485.26 KB |       15.09 |
| RenderHandlebars       | controlled |  34.62 μs |  0.803 μs | 0.478 μs |  2.35 |    0.06 |  4.0894 |  0.3662 |   67.08 KB |        0.68 |
| RenderRazor            | controlled |  37.13 μs |  0.351 μs | 0.209 μs |  2.52 |    0.06 |  5.6152 |  2.8076 |   93.04 KB |        0.95 |
|                        |            |           |           |          |       |         |         |         |            |             |
| **RenderHeddle**           | **idiomatic**  |  **15.57 μs** |  **0.050 μs** | **0.030 μs** |  **1.00** |    **0.00** |  **8.3618** |  **1.3733** |  **136.98 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |  18.57 μs |  0.137 μs | 0.082 μs |  1.19 |    0.01 |  2.1057 |       - |   34.55 KB |        0.25 |
| RenderHeddleTextWriter | idiomatic  |  14.64 μs |  0.083 μs | 0.050 μs |  0.94 |    0.00 |  2.1057 |       - |   34.55 KB |        0.25 |
| RenderFluid            | idiomatic  |  41.70 μs |  1.545 μs | 0.920 μs |  2.68 |    0.06 |  4.6387 |       - |   76.83 KB |        0.56 |
| RenderScriban          | idiomatic  | 112.42 μs | 13.427 μs | 7.990 μs |  7.22 |    0.49 | 20.8740 |  4.2725 |  341.52 KB |        2.49 |
| RenderDotLiquid        | idiomatic  | 347.63 μs |  7.351 μs | 4.374 μs | 22.32 |    0.27 | 93.7500 | 17.5781 | 1538.55 KB |       11.23 |
| RenderHandlebars       | idiomatic  |  36.74 μs |  1.083 μs | 0.645 μs |  2.36 |    0.04 |  7.2021 |  0.9766 |  118.02 KB |        0.86 |
| RenderRazor            | idiomatic  |  24.51 μs |  0.796 μs | 0.474 μs |  1.57 |    0.03 |  8.9417 |  2.2278 |   146.8 KB |        1.07 |
