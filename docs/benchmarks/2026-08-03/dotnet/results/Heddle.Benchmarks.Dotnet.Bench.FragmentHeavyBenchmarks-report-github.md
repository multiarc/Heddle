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
| **RenderHeddle**           | **controlled** |   **3.275 μs** | **0.3837 μs** | **0.2283 μs** |  **1.00** |    **0.09** |  **1.3771** | **0.0496** |   **22.54 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |   3.400 μs | 0.3080 μs | 0.1833 μs |  1.04 |    0.09 |  0.1831 |      - |    3.05 KB |        0.14 |
| RenderHeddleTextWriter | controlled |   2.970 μs | 0.2487 μs | 0.1480 μs |  0.91 |    0.07 |  0.1831 |      - |    3.04 KB |        0.13 |
| RenderFluid            | controlled |  11.450 μs | 1.2786 μs | 0.7609 μs |  3.51 |    0.32 |  2.0905 | 0.0153 |   34.37 KB |        1.52 |
| RenderScriban          | controlled |  47.295 μs | 1.8583 μs | 1.1058 μs | 14.50 |    0.98 | 11.2305 | 1.4648 |  190.42 KB |        8.45 |
| RenderDotLiquid        | controlled | 168.546 μs | 3.1179 μs | 1.8554 μs | 51.68 |    3.34 | 57.3730 | 4.1504 |  937.89 KB |       41.61 |
| RenderHandlebars       | controlled |  20.311 μs | 0.3458 μs | 0.2058 μs |  6.23 |    0.40 | 29.1443 | 2.4719 |   476.2 KB |       21.13 |
| RenderRazor            | controlled |  43.338 μs | 1.6195 μs | 0.9637 μs | 13.29 |    0.89 |  8.0566 | 1.9531 |  135.57 KB |        6.02 |
|                        |            |            |           |           |       |         |         |        |            |             |
| **RenderHeddle**           | **idiomatic**  |   **3.104 μs** | **0.0481 μs** | **0.0286 μs** |  **1.00** |    **0.01** |  **1.8349** | **0.0839** |   **30.04 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |   3.533 μs | 0.0334 μs | 0.0199 μs |  1.14 |    0.01 |  0.1831 |      - |    3.05 KB |        0.10 |
| RenderHeddleTextWriter | idiomatic  |   2.774 μs | 0.0640 μs | 0.0381 μs |  0.89 |    0.01 |  0.1831 |      - |    3.04 KB |        0.10 |
| RenderFluid            | idiomatic  |  11.748 μs | 0.4178 μs | 0.2486 μs |  3.79 |    0.08 |  2.2430 | 0.0153 |   36.82 KB |        1.23 |
| RenderScriban          | idiomatic  |  52.589 μs | 1.0664 μs | 0.6346 μs | 16.94 |    0.24 | 12.2070 | 1.9531 |  201.28 KB |        6.70 |
| RenderDotLiquid        | idiomatic  | 273.964 μs | 3.5135 μs | 2.0909 μs | 88.27 |    1.01 | 91.7969 | 7.3242 | 1503.44 KB |       50.05 |
| RenderHandlebars       | idiomatic  |  20.967 μs | 1.3732 μs | 0.8172 μs |  6.76 |    0.26 | 29.2358 | 2.5635 |  477.89 KB |       15.91 |
| RenderRazor            | idiomatic  |  36.565 μs | 0.8851 μs | 0.5267 μs | 11.78 |    0.19 |  8.2397 | 2.0752 |  135.15 KB |        4.50 |
