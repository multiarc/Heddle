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
| **RenderHeddle**           | **controlled** |   **492.1 μs** |   **3.46 μs** |   **2.06 μs** |  **1.00** |    **0.01** |   **53.7109** |  **30.7617** | **30.7617** | **1172.35 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |   157.3 μs |   1.83 μs |   1.09 μs |  0.32 |    0.00 |   23.1934 |        - |       - |  381.41 KB |        0.33 |
| RenderHeddleTextWriter | controlled |   131.1 μs |   0.60 μs |   0.36 μs |  0.27 |    0.00 |   23.1934 |        - |       - |  381.41 KB |        0.33 |
| RenderFluid            | controlled |   790.1 μs |  22.49 μs |  13.38 μs |  1.61 |    0.03 |   93.7500 |  50.7813 | 15.6250 | 1668.61 KB |        1.42 |
| RenderScriban          | controlled | 1,562.8 μs |  30.07 μs |  17.89 μs |  3.18 |    0.04 |  156.2500 |  80.0781 | 31.2500 | 2819.21 KB |        2.40 |
| RenderDotLiquid        | controlled | 3,735.0 μs | 231.22 μs | 137.59 μs |  7.59 |    0.27 | 1042.9688 | 449.2188 | 11.7188 | 17257.6 KB |       14.72 |
| RenderHandlebars       | controlled |   742.2 μs |  26.84 μs |  15.97 μs |  1.51 |    0.03 |   55.6641 |  30.2734 | 15.6250 | 1033.68 KB |        0.88 |
| RenderRazor            | controlled |   744.1 μs |   5.92 μs |   3.52 μs |  1.51 |    0.01 |   58.5938 |  56.6406 |       - | 1363.37 KB |        1.16 |
|                        |            |            |           |           |       |         |           |          |         |            |             |
| **RenderHeddle**           | **idiomatic**  |   **566.1 μs** |   **7.08 μs** |   **4.21 μs** |  **1.00** |    **0.01** |   **57.6172** |  **35.1563** | **35.1563** | **1295.42 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |   158.0 μs |   5.31 μs |   3.16 μs |  0.28 |    0.01 |   23.1934 |        - |       - |  381.41 KB |        0.29 |
| RenderHeddleTextWriter | idiomatic  |   131.7 μs |   2.63 μs |   1.56 μs |  0.23 |    0.00 |   23.1934 |        - |       - |  381.41 KB |        0.29 |
| RenderFluid            | idiomatic  |   859.7 μs |  10.35 μs |   6.16 μs |  1.52 |    0.01 |   99.6094 |  54.6875 | 17.5781 | 1790.02 KB |        1.38 |
| RenderScriban          | idiomatic  | 1,615.7 μs |  75.75 μs |  45.08 μs |  2.85 |    0.08 |  156.2500 |  78.1250 | 31.2500 | 2920.92 KB |        2.25 |
| RenderDotLiquid        | idiomatic  | 3,795.1 μs |  28.20 μs |  16.78 μs |  6.70 |    0.05 | 1050.7813 | 453.1250 | 15.6250 | 17363.3 KB |       13.40 |
| RenderHandlebars       | idiomatic  |   780.0 μs |  33.19 μs |  19.75 μs |  1.38 |    0.03 |   58.5938 |  32.2266 | 16.6016 | 1104.14 KB |        0.85 |
| RenderRazor            | idiomatic  |   569.7 μs |   8.76 μs |   5.21 μs |  1.01 |    0.01 |   54.6875 |  41.0156 |       - |  1351.8 KB |        1.04 |
