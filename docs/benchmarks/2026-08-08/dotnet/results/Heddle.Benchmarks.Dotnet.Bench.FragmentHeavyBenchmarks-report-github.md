```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated  | Alloc Ratio |
|----------------------- |----------- |-----------:|----------:|----------:|------:|--------:|--------:|-------:|-----------:|------------:|
| **RenderHeddle**           | **controlled** |   **3.081 μs** | **0.1664 μs** | **0.0990 μs** |  **1.00** |    **0.04** |  **1.3771** | **0.0496** |   **22.54 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |   3.460 μs | 0.2422 μs | 0.1441 μs |  1.12 |    0.06 |  0.1831 |      - |    3.05 KB |        0.14 |
| RenderHeddleTextWriter | controlled |   2.817 μs | 0.1876 μs | 0.1117 μs |  0.92 |    0.04 |  0.1831 |      - |    3.04 KB |        0.13 |
| RenderFluid            | controlled |  11.220 μs | 0.4969 μs | 0.2957 μs |  3.64 |    0.14 |  2.0905 | 0.0153 |   34.37 KB |        1.52 |
| RenderScriban          | controlled |  46.476 μs | 1.3660 μs | 0.8129 μs | 15.10 |    0.53 | 11.2305 | 1.4648 |  190.42 KB |        8.45 |
| RenderDotLiquid        | controlled | 170.223 μs | 1.3890 μs | 0.8266 μs | 55.30 |    1.71 | 57.3730 | 4.1504 |  937.89 KB |       41.61 |
| RenderHandlebars       | controlled |  20.692 μs | 0.3136 μs | 0.1866 μs |  6.72 |    0.21 | 29.1443 | 2.4719 |   476.2 KB |       21.13 |
| RenderRazor            | controlled |  44.056 μs | 1.4609 μs | 0.8694 μs | 14.31 |    0.51 |  8.0566 | 1.9531 |  135.57 KB |        6.02 |
|                        |            |            |           |           |       |         |         |        |            |             |
| **RenderHeddle**           | **idiomatic**  |   **3.285 μs** | **0.4394 μs** | **0.2615 μs** |  **1.01** |    **0.10** |  **1.8349** | **0.0839** |   **30.04 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |   3.515 μs | 0.2909 μs | 0.1731 μs |  1.08 |    0.09 |  0.1831 |      - |    3.05 KB |        0.10 |
| RenderHeddleTextWriter | idiomatic  |   2.899 μs | 0.3339 μs | 0.1987 μs |  0.89 |    0.09 |  0.1831 |      - |    3.04 KB |        0.10 |
| RenderFluid            | idiomatic  |  11.798 μs | 0.2033 μs | 0.1210 μs |  3.61 |    0.26 |  2.2430 | 0.0153 |   36.82 KB |        1.23 |
| RenderScriban          | idiomatic  |  52.684 μs | 1.5357 μs | 0.9139 μs | 16.12 |    1.19 | 12.2070 | 1.9531 |  201.28 KB |        6.70 |
| RenderDotLiquid        | idiomatic  | 278.300 μs | 5.1166 μs | 3.0448 μs | 85.17 |    6.18 | 91.7969 | 7.3242 | 1503.44 KB |       50.05 |
| RenderHandlebars       | idiomatic  |  21.373 μs | 0.8808 μs | 0.5241 μs |  6.54 |    0.49 | 29.2358 | 2.5635 |  477.89 KB |       15.91 |
| RenderRazor            | idiomatic  |  36.889 μs | 0.4233 μs | 0.2519 μs | 11.29 |    0.81 |  8.2397 | 2.0752 |  135.15 KB |        4.50 |
