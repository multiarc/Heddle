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
| **RenderHeddle**           | **controlled** |    **149.0 ns** |     **5.98 ns** |     **3.56 ns** |   **1.00** |    **0.03** |  **0.1028** |      **-** |    **1720 B** |        **1.00** |
| RenderHeddleUtf8       | controlled |    161.3 ns |     3.09 ns |     1.84 ns |   1.08 |    0.03 |  0.0124 |      - |     208 B |        0.12 |
| RenderHeddleTextWriter | controlled |    142.8 ns |    12.03 ns |     7.16 ns |   0.96 |    0.05 |  0.0119 |      - |     200 B |        0.12 |
| RenderFluid            | controlled |    497.3 ns |     5.78 ns |     3.44 ns |   3.34 |    0.08 |  0.1392 |      - |    2344 B |        1.36 |
| RenderScriban          | controlled | 39,503.0 ns | 4,883.57 ns | 2,906.13 ns | 265.33 |   19.52 | 20.5078 | 1.7090 |  343936 B |      199.96 |
| RenderDotLiquid        | controlled |  1,877.9 ns |    23.00 ns |    13.69 ns |  12.61 |    0.30 |  0.6199 | 0.0038 |   10384 B |        6.04 |
| RenderHandlebars       | controlled |    380.6 ns |     4.56 ns |     2.71 ns |   2.56 |    0.06 |  0.0439 |      - |     736 B |        0.43 |
| RenderRazor            | controlled |  5,510.2 ns |    74.67 ns |    44.43 ns |  37.01 |    0.90 |  0.7935 | 0.2594 |   13360 B |        7.77 |
|                        |            |             |             |             |        |         |         |        |           |             |
| **RenderHeddle**           | **idiomatic**  |    **143.9 ns** |     **2.75 ns** |     **1.63 ns** |   **1.00** |    **0.02** |  **0.1147** |      **-** |    **1920 B** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |    160.1 ns |     5.52 ns |     3.28 ns |   1.11 |    0.02 |  0.0124 |      - |     208 B |        0.11 |
| RenderHeddleTextWriter | idiomatic  |    140.0 ns |    12.74 ns |     7.58 ns |   0.97 |    0.05 |  0.0119 |      - |     200 B |        0.10 |
| RenderFluid            | idiomatic  |    491.1 ns |     9.21 ns |     5.48 ns |   3.41 |    0.05 |  0.1440 |      - |    2424 B |        1.26 |
| RenderScriban          | idiomatic  | 37,077.3 ns |   373.64 ns |   222.35 ns | 257.67 |    3.16 | 20.5078 | 1.7090 |  343965 B |      179.15 |
| RenderDotLiquid        | idiomatic  |  1,900.3 ns |    56.52 ns |    33.63 ns |  13.21 |    0.26 |  0.6237 | 0.0038 |   10464 B |        5.45 |
| RenderHandlebars       | idiomatic  |    457.6 ns |     5.56 ns |     3.31 ns |   3.18 |    0.04 |  0.0486 |      - |     816 B |        0.42 |
| RenderRazor            | idiomatic  |  4,781.1 ns |    55.55 ns |    33.06 ns |  33.23 |    0.42 |  0.7858 | 0.2594 |   13200 B |        6.88 |
