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
| **RenderHeddle**           | **controlled** | **1,766.0 μs** |  **29.51 μs** |  **17.56 μs** |  **1.00** |    **0.01** |  **275.3906** | **109.3750** | **109.3750** |   **5.79 MB** |        **1.00** |
| RenderHeddleUtf8       | controlled |   725.3 μs |   6.52 μs |   3.88 μs |  0.41 |    0.00 |  166.0156 |        - |        - |   2.66 MB |        0.46 |
| RenderHeddleTextWriter | controlled |   610.1 μs |  17.41 μs |  10.36 μs |  0.35 |    0.01 |  166.0156 |        - |        - |   2.66 MB |        0.46 |
| RenderFluid            | controlled | 2,574.9 μs |  10.52 μs |   6.26 μs |  1.46 |    0.01 |  324.2188 | 214.8438 |  50.7813 |   5.86 MB |        1.01 |
| RenderScriban          | controlled | 6,725.7 μs | 239.80 μs | 142.70 μs |  3.81 |    0.08 |  671.8750 | 445.3125 | 101.5625 |  12.15 MB |        2.10 |
| RenderDotLiquid        | controlled | 7,823.7 μs | 374.27 μs | 222.72 μs |  4.43 |    0.13 | 1828.1250 | 828.1250 |  46.8750 |  29.99 MB |        5.18 |
| RenderHandlebars       | controlled | 1,719.5 μs |  10.84 μs |   6.45 μs |  0.97 |    0.01 |  152.3438 | 111.3281 |  52.7344 |   3.11 MB |        0.54 |
| RenderRazor            | controlled | 1,761.1 μs |  24.63 μs |  14.66 μs |  1.00 |    0.01 |  210.9375 | 207.0313 |        - |   4.91 MB |        0.85 |
|                        |            |            |           |           |       |         |           |          |          |           |             |
| **RenderHeddle**           | **idiomatic**  | **1,977.7 μs** |  **47.15 μs** |  **28.06 μs** |  **1.00** |    **0.02** |  **289.0625** | **123.0469** | **123.0469** |   **6.38 MB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |   712.3 μs |   7.81 μs |   4.65 μs |  0.36 |    0.01 |  166.0156 |        - |        - |   2.66 MB |        0.42 |
| RenderHeddleTextWriter | idiomatic  |   605.5 μs |  12.61 μs |   7.50 μs |  0.31 |    0.01 |  166.0156 |        - |        - |   2.66 MB |        0.42 |
| RenderFluid            | idiomatic  | 2,941.9 μs | 191.42 μs | 113.91 μs |  1.49 |    0.06 |  351.5625 | 242.1875 |  62.5000 |   6.47 MB |        1.01 |
| RenderScriban          | idiomatic  | 6,501.1 μs | 433.82 μs | 258.16 μs |  3.29 |    0.13 |  710.9375 | 398.4375 | 125.0000 |  12.97 MB |        2.03 |
| RenderDotLiquid        | idiomatic  | 7,900.6 μs | 157.76 μs |  93.88 μs |  4.00 |    0.07 | 1843.7500 | 921.8750 |  46.8750 |  30.61 MB |        4.80 |
| RenderHandlebars       | idiomatic  | 2,027.5 μs |  15.27 μs |   9.08 μs |  1.03 |    0.01 |  179.6875 | 128.9063 |  62.5000 |   3.64 MB |        0.57 |
| RenderRazor            | idiomatic  | 1,812.5 μs |   9.47 μs |   5.63 μs |  0.92 |    0.01 |  238.2813 | 234.3750 |        - |   5.82 MB |        0.91 |
