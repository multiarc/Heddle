```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean        | Error       | StdDev      | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------- |----------- |------------:|------------:|------------:|-------:|--------:|--------:|-------:|----------:|------------:|
| **RenderHeddle**           | **controlled** |    **149.0 ns** |     **7.75 ns** |     **4.61 ns** |   **1.00** |    **0.04** |  **0.1028** |      **-** |    **1720 B** |        **1.00** |
| RenderHeddleUtf8       | controlled |    161.9 ns |     2.65 ns |     1.58 ns |   1.09 |    0.03 |  0.0124 |      - |     208 B |        0.12 |
| RenderHeddleTextWriter | controlled |    142.7 ns |    13.55 ns |     8.06 ns |   0.96 |    0.06 |  0.0119 |      - |     200 B |        0.12 |
| RenderFluid            | controlled |    482.9 ns |    20.47 ns |    12.18 ns |   3.24 |    0.12 |  0.1392 |      - |    2344 B |        1.36 |
| RenderScriban          | controlled | 38,805.4 ns |   639.12 ns |   380.33 ns | 260.64 |    8.20 | 20.5078 | 1.7090 |  343936 B |      199.96 |
| RenderDotLiquid        | controlled |  1,888.2 ns |    20.20 ns |    12.02 ns |  12.68 |    0.39 |  0.6199 | 0.0038 |   10384 B |        6.04 |
| RenderHandlebars       | controlled |    385.8 ns |     7.23 ns |     4.30 ns |   2.59 |    0.08 |  0.0439 |      - |     736 B |        0.43 |
| RenderRazor            | controlled |  5,523.2 ns |    63.10 ns |    37.55 ns |  37.10 |    1.14 |  0.7935 | 0.2594 |   13360 B |        7.77 |
|                        |            |             |             |             |        |         |         |        |           |             |
| **RenderHeddle**           | **idiomatic**  |    **148.1 ns** |     **5.21 ns** |     **3.10 ns** |   **1.00** |    **0.03** |  **0.1147** |      **-** |    **1920 B** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |    160.4 ns |     2.34 ns |     1.39 ns |   1.08 |    0.02 |  0.0124 |      - |     208 B |        0.11 |
| RenderHeddleTextWriter | idiomatic  |    141.6 ns |    13.85 ns |     8.24 ns |   0.96 |    0.06 |  0.0119 |      - |     200 B |        0.10 |
| RenderFluid            | idiomatic  |    491.4 ns |    21.52 ns |    12.81 ns |   3.32 |    0.10 |  0.1440 |      - |    2424 B |        1.26 |
| RenderScriban          | idiomatic  | 38,709.8 ns | 3,542.32 ns | 2,107.98 ns | 261.55 |   14.45 | 20.5078 | 1.8311 |  344024 B |      179.18 |
| RenderDotLiquid        | idiomatic  |  1,925.7 ns |    37.68 ns |    22.42 ns |  13.01 |    0.29 |  0.6218 | 0.0038 |   10464 B |        5.45 |
| RenderHandlebars       | idiomatic  |    461.2 ns |     4.16 ns |     2.48 ns |   3.12 |    0.06 |  0.0486 |      - |     816 B |        0.42 |
| RenderRazor            | idiomatic  |  4,833.0 ns |    49.84 ns |    29.66 ns |  32.65 |    0.67 |  0.7858 | 0.2594 |   13200 B |        6.88 |
