```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated  | Alloc Ratio |
|----------------------- |----------- |-----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|-----------:|------------:|
| **RenderHeddle**           | **controlled** |   **494.5 μs** |  **13.17 μs** |   **7.84 μs** |  **1.00** |    **0.02** |   **52.7344** |  **30.2734** | **30.2734** | **1172.35 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |   155.1 μs |   1.86 μs |   1.11 μs |  0.31 |    0.01 |   23.1934 |        - |       - |  381.41 KB |        0.33 |
| RenderHeddleTextWriter | controlled |   129.2 μs |   0.76 μs |   0.46 μs |  0.26 |    0.00 |   23.1934 |        - |       - |  381.41 KB |        0.33 |
| RenderFluid            | controlled |   781.4 μs |  14.15 μs |   8.42 μs |  1.58 |    0.03 |   93.7500 |  51.7578 | 15.6250 | 1668.61 KB |        1.42 |
| RenderScriban          | controlled | 1,549.9 μs |  24.30 μs |  14.46 μs |  3.14 |    0.05 |  156.2500 |  80.0781 | 31.2500 | 2819.19 KB |        2.40 |
| RenderDotLiquid        | controlled | 3,813.7 μs | 157.86 μs |  93.94 μs |  7.71 |    0.21 | 1042.9688 | 449.2188 | 11.7188 | 17257.6 KB |       14.72 |
| RenderHandlebars       | controlled |   722.4 μs |  27.03 μs |  16.08 μs |  1.46 |    0.04 |   55.6641 |  30.2734 | 15.6250 | 1033.68 KB |        0.88 |
| RenderRazor            | controlled |   737.8 μs |   7.23 μs |   4.30 μs |  1.49 |    0.02 |   58.5938 |  56.6406 |       - | 1363.37 KB |        1.16 |
|                        |            |            |           |           |       |         |           |          |         |            |             |
| **RenderHeddle**           | **idiomatic**  |   **574.3 μs** |   **9.67 μs** |   **5.75 μs** |  **1.00** |    **0.01** |   **57.6172** |  **35.1563** | **35.1563** | **1295.42 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |   155.0 μs |   1.20 μs |   0.71 μs |  0.27 |    0.00 |   23.1934 |        - |       - |  381.41 KB |        0.29 |
| RenderHeddleTextWriter | idiomatic  |   129.3 μs |   1.43 μs |   0.85 μs |  0.23 |    0.00 |   23.1934 |        - |       - |  381.41 KB |        0.29 |
| RenderFluid            | idiomatic  |   858.1 μs |  10.19 μs |   6.06 μs |  1.49 |    0.02 |   99.6094 |  17.5781 | 17.5781 | 1790.03 KB |        1.38 |
| RenderScriban          | idiomatic  | 1,462.3 μs | 240.43 μs | 143.08 μs |  2.55 |    0.24 |  140.6250 |  62.5000 | 15.6250 | 2920.84 KB |        2.25 |
| RenderDotLiquid        | idiomatic  | 3,873.8 μs | 160.03 μs |  95.23 μs |  6.75 |    0.17 | 1046.8750 | 453.1250 | 15.6250 | 17363.3 KB |       13.40 |
| RenderHandlebars       | idiomatic  |   746.8 μs |  24.12 μs |  14.35 μs |  1.30 |    0.03 |   58.5938 |  32.2266 | 16.6016 | 1104.16 KB |        0.85 |
| RenderRazor            | idiomatic  |   562.5 μs |   7.65 μs |   4.55 μs |  0.98 |    0.01 |   55.6641 |  44.9219 |       - |  1351.8 KB |        1.04 |
