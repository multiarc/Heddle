```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean      | Error      | StdDev     | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------- |----------- |----------:|-----------:|-----------:|------:|--------:|--------:|-------:|----------:|------------:|
| **RenderHeddle**           | **controlled** |  **2.636 μs** |  **0.0337 μs** |  **0.0200 μs** |  **1.00** |    **0.01** |  **2.6855** | **0.1755** |  **43.96 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |  2.788 μs |  0.1047 μs |  0.0623 μs |  1.06 |    0.02 |  0.2365 |      - |   3.92 KB |        0.09 |
| RenderHeddleTextWriter | controlled |  2.265 μs |  0.0121 μs |  0.0072 μs |  0.86 |    0.01 |  0.2365 |      - |   3.91 KB |        0.09 |
| RenderFluid            | controlled |  8.918 μs |  0.2473 μs |  0.1472 μs |  3.38 |    0.06 |  1.7700 |      - |  28.99 KB |        0.66 |
| RenderScriban          | controlled | 71.202 μs | 24.5102 μs | 14.5856 μs | 27.01 |    5.25 | 27.3438 | 2.4414 | 453.25 KB |       10.31 |
| RenderDotLiquid        | controlled | 56.338 μs |  2.6957 μs |  1.6042 μs | 21.37 |    0.60 | 17.3340 | 1.3428 | 283.67 KB |        6.45 |
| RenderHandlebars       | controlled |  7.247 μs |  0.1362 μs |  0.0811 μs |  2.75 |    0.04 |  2.6779 | 0.2060 |  43.83 KB |        1.00 |
| RenderRazor            | controlled | 20.131 μs |  0.4106 μs |  0.2444 μs |  7.64 |    0.10 |  3.7842 | 1.2207 |  62.54 KB |        1.42 |
|                        |            |           |            |            |       |         |         |        |           |             |
| **RenderHeddle**           | **idiomatic**  |  **2.906 μs** |  **0.0269 μs** |  **0.0160 μs** |  **1.00** |    **0.01** |  **3.8033** | **0.3777** |   **62.3 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |  2.754 μs |  0.0365 μs |  0.0217 μs |  0.95 |    0.01 |  0.2365 |      - |   3.92 KB |        0.06 |
| RenderHeddleTextWriter | idiomatic  |  2.304 μs |  0.0205 μs |  0.0122 μs |  0.79 |    0.01 |  0.2365 |      - |   3.91 KB |        0.06 |
| RenderFluid            | idiomatic  |  9.115 μs |  0.4819 μs |  0.2868 μs |  3.14 |    0.10 |  2.1667 |      - |  35.49 KB |        0.57 |
| RenderScriban          | idiomatic  | 68.265 μs | 13.4083 μs |  7.9790 μs | 23.49 |    2.61 | 29.2969 | 0.9766 | 481.21 KB |        7.72 |
| RenderDotLiquid        | idiomatic  | 59.112 μs |  1.9223 μs |  1.1439 μs | 20.34 |    0.39 | 18.7378 | 2.1973 | 306.88 KB |        4.93 |
| RenderHandlebars       | idiomatic  | 10.260 μs |  0.2998 μs |  0.1784 μs |  3.53 |    0.06 |  4.0588 | 0.4425 |  66.41 KB |        1.07 |
| RenderRazor            | idiomatic  | 15.505 μs |  0.2166 μs |  0.1289 μs |  5.34 |    0.05 |  5.2490 | 1.3123 |  85.91 KB |        1.38 |
