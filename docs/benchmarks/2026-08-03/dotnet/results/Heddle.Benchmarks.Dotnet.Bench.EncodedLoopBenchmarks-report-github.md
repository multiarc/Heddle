```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------- |----------- |-----------:|----------:|----------:|------:|--------:|----------:|---------:|---------:|----------:|------------:|
| **RenderHeddle**           | **controlled** | **1,773.7 μs** |  **29.97 μs** |  **17.83 μs** |  **1.00** |    **0.01** |  **275.3906** | **109.3750** | **109.3750** |   **5.79 MB** |        **1.00** |
| RenderHeddleUtf8       | controlled |   724.2 μs |  19.90 μs |  11.84 μs |  0.41 |    0.01 |  166.0156 |        - |        - |   2.66 MB |        0.46 |
| RenderHeddleTextWriter | controlled |   596.3 μs |   5.52 μs |   3.28 μs |  0.34 |    0.00 |  166.0156 |        - |        - |   2.66 MB |        0.46 |
| RenderFluid            | controlled | 2,557.4 μs |  42.60 μs |  25.35 μs |  1.44 |    0.02 |  324.2188 | 218.7500 |  50.7813 |   5.86 MB |        1.01 |
| RenderScriban          | controlled | 6,822.5 μs |  94.76 μs |  56.39 μs |  3.85 |    0.05 |  671.8750 | 445.3125 | 101.5625 |  12.15 MB |        2.10 |
| RenderDotLiquid        | controlled | 7,701.1 μs | 180.18 μs | 107.22 μs |  4.34 |    0.07 | 1828.1250 | 828.1250 |  46.8750 |  29.99 MB |        5.18 |
| RenderHandlebars       | controlled | 1,722.8 μs |  12.35 μs |   7.35 μs |  0.97 |    0.01 |  152.3438 | 113.2813 |  52.7344 |   3.11 MB |        0.54 |
| RenderRazor            | controlled | 1,758.7 μs |  62.27 μs |  37.05 μs |  0.99 |    0.02 |  210.9375 | 207.0313 |        - |   4.91 MB |        0.85 |
|                        |            |            |           |           |       |         |           |          |          |           |             |
| **RenderHeddle**           | **idiomatic**  | **1,993.2 μs** |  **75.10 μs** |  **44.69 μs** |  **1.00** |    **0.03** |  **285.1563** | **121.0938** | **121.0938** |   **6.38 MB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |   708.7 μs |  10.45 μs |   6.22 μs |  0.36 |    0.01 |  166.0156 |        - |        - |   2.66 MB |        0.42 |
| RenderHeddleTextWriter | idiomatic  |   603.5 μs |   4.90 μs |   2.92 μs |  0.30 |    0.01 |  166.0156 |        - |        - |   2.66 MB |        0.42 |
| RenderFluid            | idiomatic  | 2,358.1 μs | 348.17 μs | 207.19 μs |  1.18 |    0.10 |  351.5625 | 226.5625 |  62.5000 |   6.47 MB |        1.01 |
| RenderScriban          | idiomatic  | 6,366.1 μs | 100.39 μs |  59.74 μs |  3.20 |    0.07 |  710.9375 | 445.3125 | 125.0000 |  12.97 MB |        2.03 |
| RenderDotLiquid        | idiomatic  | 7,806.4 μs | 185.68 μs | 110.49 μs |  3.92 |    0.10 | 1843.7500 | 921.8750 |  46.8750 |  30.61 MB |        4.80 |
| RenderHandlebars       | idiomatic  | 2,027.7 μs |  25.65 μs |  15.27 μs |  1.02 |    0.02 |  179.6875 | 125.0000 |  62.5000 |   3.64 MB |        0.57 |
| RenderRazor            | idiomatic  | 1,788.9 μs |  39.26 μs |  23.36 μs |  0.90 |    0.02 |  238.2813 | 234.3750 |        - |   5.82 MB |        0.91 |
