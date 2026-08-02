```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------- |----------- |---------:|----------:|----------:|------:|--------:|----------:|---------:|---------:|----------:|------------:|
| **RenderHeddle**     | **controlled** | **2.322 ms** | **0.0111 ms** | **0.0066 ms** |  **1.00** |    **0.00** |  **234.3750** | **101.5625** |  **70.3125** |   **4.52 MB** |        **1.00** |
| RenderFluid      | controlled | 2.632 ms | 0.0105 ms | 0.0062 ms |  1.13 |    0.00 |  328.1250 | 222.6563 |  54.6875 |   5.86 MB |        1.29 |
| RenderScriban    | controlled | 6.273 ms | 0.6917 ms | 0.4116 ms |  2.70 |    0.17 |  679.6875 | 445.3125 | 109.3750 |  12.15 MB |        2.69 |
| RenderDotLiquid  | controlled | 7.851 ms | 0.2169 ms | 0.1291 ms |  3.38 |    0.05 | 1828.1250 | 828.1250 |  46.8750 |  29.99 MB |        6.63 |
| RenderHandlebars | controlled | 1.745 ms | 0.0044 ms | 0.0026 ms |  0.75 |    0.00 |  154.2969 | 111.3281 |  54.6875 |   3.11 MB |        0.69 |
| RenderRazor      | controlled | 1.751 ms | 0.0267 ms | 0.0159 ms |  0.75 |    0.01 |  210.9375 | 207.0313 |        - |   4.91 MB |        1.08 |
|                  |            |          |           |           |       |         |           |          |          |           |             |
| **RenderHeddle**     | **idiomatic**  | **2.263 ms** | **0.0082 ms** | **0.0049 ms** |  **1.00** |    **0.00** |  **234.3750** | **101.5625** |  **70.3125** |   **4.52 MB** |        **1.00** |
| RenderFluid      | idiomatic  | 3.073 ms | 0.0412 ms | 0.0245 ms |  1.36 |    0.01 |  355.4688 | 253.9063 |  66.4063 |   6.47 MB |        1.43 |
| RenderScriban    | idiomatic  | 6.651 ms | 0.2563 ms | 0.1525 ms |  2.94 |    0.06 |  710.9375 | 445.3125 | 125.0000 |  12.97 MB |        2.87 |
| RenderDotLiquid  | idiomatic  | 8.318 ms | 0.3746 ms | 0.2229 ms |  3.68 |    0.09 | 1859.3750 | 937.5000 |  62.5000 |  30.61 MB |        6.77 |
| RenderHandlebars | idiomatic  | 2.030 ms | 0.0354 ms | 0.0211 ms |  0.90 |    0.01 |  179.6875 | 128.9063 |  62.5000 |   3.64 MB |        0.80 |
| RenderRazor      | idiomatic  | 1.826 ms | 0.1073 ms | 0.0638 ms |  0.81 |    0.03 |  238.2813 | 234.3750 |        - |   5.82 MB |        1.29 |
