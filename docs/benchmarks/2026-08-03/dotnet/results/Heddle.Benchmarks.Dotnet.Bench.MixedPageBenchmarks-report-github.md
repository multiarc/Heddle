```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------- |----------- |----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| **RenderHeddle**           | **controlled** |  **2.634 μs** | **0.0442 μs** | **0.0263 μs** |  **1.00** |    **0.01** |  **2.6855** | **0.1755** |  **43.96 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |  2.785 μs | 0.1109 μs | 0.0660 μs |  1.06 |    0.03 |  0.2365 |      - |   3.92 KB |        0.09 |
| RenderHeddleTextWriter | controlled |  2.251 μs | 0.0128 μs | 0.0076 μs |  0.85 |    0.01 |  0.2365 |      - |   3.91 KB |        0.09 |
| RenderFluid            | controlled |  9.021 μs | 0.1812 μs | 0.1078 μs |  3.43 |    0.05 |  1.7700 |      - |  28.99 KB |        0.66 |
| RenderScriban          | controlled | 58.213 μs | 0.4857 μs | 0.2890 μs | 22.10 |    0.23 | 27.3438 | 2.4414 | 453.25 KB |       10.31 |
| RenderDotLiquid        | controlled | 55.131 μs | 1.3857 μs | 0.8246 μs | 20.93 |    0.36 | 17.3340 | 1.3428 | 283.67 KB |        6.45 |
| RenderHandlebars       | controlled |  7.184 μs | 0.1556 μs | 0.0926 μs |  2.73 |    0.04 |  2.6779 | 0.2060 |  43.83 KB |        1.00 |
| RenderRazor            | controlled | 20.014 μs | 0.3720 μs | 0.2214 μs |  7.60 |    0.11 |  3.7842 | 1.2207 |  62.54 KB |        1.42 |
|                        |            |           |           |           |       |         |         |        |           |             |
| **RenderHeddle**           | **idiomatic**  |  **2.853 μs** | **0.0204 μs** | **0.0121 μs** |  **1.00** |    **0.01** |  **3.8033** | **0.3777** |   **62.3 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |  2.730 μs | 0.0340 μs | 0.0202 μs |  0.96 |    0.01 |  0.2365 |      - |   3.92 KB |        0.06 |
| RenderHeddleTextWriter | idiomatic  |  2.318 μs | 0.0410 μs | 0.0244 μs |  0.81 |    0.01 |  0.2365 |      - |   3.91 KB |        0.06 |
| RenderFluid            | idiomatic  |  9.196 μs | 0.2404 μs | 0.1430 μs |  3.22 |    0.05 |  2.1667 |      - |  35.49 KB |        0.57 |
| RenderScriban          | idiomatic  | 59.114 μs | 0.5779 μs | 0.3439 μs | 20.72 |    0.14 | 29.2969 | 0.9766 | 481.21 KB |        7.72 |
| RenderDotLiquid        | idiomatic  | 56.129 μs | 0.8642 μs | 0.5143 μs | 19.68 |    0.19 | 18.7378 | 2.1973 | 306.88 KB |        4.93 |
| RenderHandlebars       | idiomatic  | 10.366 μs | 0.3297 μs | 0.1962 μs |  3.63 |    0.07 |  4.0588 | 0.4425 |  66.41 KB |        1.07 |
| RenderRazor            | idiomatic  | 15.428 μs | 0.0819 μs | 0.0487 μs |  5.41 |    0.03 |  5.2490 | 1.3123 |  85.91 KB |        1.38 |
