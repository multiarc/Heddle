```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|----------------- |----------- |----------:|----------:|----------:|------:|--------:|--------:|--------:|--------:|-----------:|------------:|
| **RenderHeddle**     | **controlled** |  **43.71 μs** |  **1.810 μs** |  **1.077 μs** |  **1.00** |    **0.03** |  **3.9063** |  **0.6104** |       **-** |   **64.24 KB** |        **1.00** |
| RenderFluid      | controlled |  59.00 μs |  0.618 μs |  0.368 μs |  1.35 |    0.03 | 11.1694 |  5.6152 |  3.6621 |  231.98 KB |        3.61 |
| RenderScriban    | controlled | 432.13 μs | 31.554 μs | 18.777 μs |  9.89 |    0.47 | 58.5938 | 23.4375 | 15.6250 | 1154.39 KB |       17.97 |
| RenderDotLiquid  | controlled | 140.89 μs | 17.326 μs | 10.310 μs |  3.23 |    0.24 | 18.7988 | 10.0098 |  7.3242 |  405.75 KB |        6.32 |
| RenderHandlebars | controlled |  61.85 μs |  2.110 μs |  1.256 μs |  1.42 |    0.04 | 10.9863 |  5.3711 |  3.7842 |  227.59 KB |        3.54 |
| RenderRazor      | controlled |  61.27 μs | 10.957 μs |  6.520 μs |  1.40 |    0.15 |  8.6670 |  2.0752 |  0.7324 |  239.69 KB |        3.73 |
|                  |            |           |           |           |       |         |         |         |         |            |             |
| **RenderHeddle**     | **idiomatic**  |  **42.19 μs** |  **0.107 μs** |  **0.064 μs** |  **1.00** |    **0.00** |  **3.9063** |  **0.6104** |       **-** |   **64.24 KB** |        **1.00** |
| RenderFluid      | idiomatic  |  59.02 μs |  0.702 μs |  0.418 μs |  1.40 |    0.01 | 11.1694 |  5.5542 |  3.6621 |  232.19 KB |        3.61 |
| RenderScriban    | idiomatic  | 426.08 μs | 29.308 μs | 17.441 μs | 10.10 |    0.39 | 60.5469 | 23.4375 | 16.6016 |  1184.2 KB |       18.43 |
| RenderDotLiquid  | idiomatic  | 178.77 μs | 10.571 μs |  6.291 μs |  4.24 |    0.14 | 19.5313 | 10.0098 |  7.3242 |  419.68 KB |        6.53 |
| RenderHandlebars | idiomatic  |  62.21 μs |  1.228 μs |  0.731 μs |  1.47 |    0.02 | 10.9863 |  5.2490 |  3.7842 |  227.84 KB |        3.55 |
| RenderRazor      | idiomatic  |  59.67 μs | 11.588 μs |  6.896 μs |  1.41 |    0.16 |  8.6670 |  2.0752 |  0.7324 |  239.91 KB |        3.73 |
