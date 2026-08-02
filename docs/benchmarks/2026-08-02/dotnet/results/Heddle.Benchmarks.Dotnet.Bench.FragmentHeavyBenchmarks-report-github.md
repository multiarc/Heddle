```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated  | Alloc Ratio |
|----------------- |----------- |-----------:|----------:|----------:|------:|--------:|--------:|-------:|-----------:|------------:|
| **RenderHeddle**     | **controlled** |   **7.102 μs** | **0.4382 μs** | **0.2608 μs** |  **1.00** |    **0.05** |  **4.0970** | **0.6790** |   **67.11 KB** |        **1.00** |
| RenderFluid      | controlled |  11.146 μs | 0.3457 μs | 0.2057 μs |  1.57 |    0.06 |  2.0905 | 0.0153 |   34.37 KB |        0.51 |
| RenderScriban    | controlled |  45.889 μs | 2.4746 μs | 1.4726 μs |  6.47 |    0.30 | 11.2305 | 1.4648 |  190.42 KB |        2.84 |
| RenderDotLiquid  | controlled | 172.587 μs | 5.3854 μs | 3.2048 μs | 24.33 |    0.94 | 57.3730 | 4.1504 |  937.89 KB |       13.98 |
| RenderHandlebars | controlled |  20.853 μs | 0.6506 μs | 0.3872 μs |  2.94 |    0.11 | 29.1443 | 2.4719 |   476.2 KB |        7.10 |
| RenderRazor      | controlled |  43.772 μs | 1.1310 μs | 0.6730 μs |  6.17 |    0.23 |  8.2397 | 2.0752 |  135.57 KB |        2.02 |
|                  |            |            |           |           |       |         |         |        |            |             |
| **RenderHeddle**     | **idiomatic**  |   **8.595 μs** | **0.2585 μs** | **0.1538 μs** |  **1.00** |    **0.02** |  **4.0894** | **0.6714** |   **67.11 KB** |        **1.00** |
| RenderFluid      | idiomatic  |  12.382 μs | 0.9371 μs | 0.5576 μs |  1.44 |    0.07 |  2.2430 | 0.0153 |   36.82 KB |        0.55 |
| RenderScriban    | idiomatic  |  54.212 μs | 3.0665 μs | 1.8248 μs |  6.31 |    0.23 | 12.2070 | 1.9531 |  201.28 KB |        3.00 |
| RenderDotLiquid  | idiomatic  | 277.201 μs | 9.8942 μs | 5.8879 μs | 32.26 |    0.85 | 91.7969 | 7.3242 | 1503.44 KB |       22.40 |
| RenderHandlebars | idiomatic  |  21.330 μs | 0.7875 μs | 0.4686 μs |  2.48 |    0.07 | 29.2358 | 2.5635 |  477.89 KB |        7.12 |
| RenderRazor      | idiomatic  |  38.142 μs | 3.8476 μs | 2.2896 μs |  4.44 |    0.26 |  8.2397 | 2.0752 |  135.15 KB |        2.01 |
