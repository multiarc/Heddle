```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated   | Alloc Ratio |
|----------------- |----------- |-----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|------------:|------------:|
| **RenderHeddle**     | **controlled** |   **513.5 μs** |   **4.40 μs** |   **2.62 μs** |  **1.00** |    **0.01** |   **41.9922** |  **17.5781** | **15.6250** |    **829.6 KB** |        **1.00** |
| RenderFluid      | controlled |   873.7 μs |  47.41 μs |  28.21 μs |  1.70 |    0.05 |   93.7500 |  50.7813 | 15.6250 |  1668.61 KB |        2.01 |
| RenderScriban    | controlled | 1,549.5 μs |  14.03 μs |   8.35 μs |  3.02 |    0.02 |  156.2500 |  76.1719 | 31.2500 |  2819.16 KB |        3.40 |
| RenderDotLiquid  | controlled | 4,143.7 μs | 350.68 μs | 208.68 μs |  8.07 |    0.39 | 1046.8750 | 453.1250 | 15.6250 | 17257.62 KB |       20.80 |
| RenderHandlebars | controlled |   756.0 μs |   9.65 μs |   5.74 μs |  1.47 |    0.01 |   55.6641 |  30.2734 | 15.6250 |  1033.68 KB |        1.25 |
| RenderRazor      | controlled |   794.4 μs |  90.96 μs |  54.13 μs |  1.55 |    0.10 |   58.5938 |  56.6406 |       - |  1363.37 KB |        1.64 |
|                  |            |            |           |           |       |         |           |          |         |             |             |
| **RenderHeddle**     | **idiomatic**  |   **509.7 μs** |   **8.93 μs** |   **5.31 μs** |  **1.00** |    **0.01** |   **41.9922** |  **20.5078** | **15.6250** |    **829.6 KB** |        **1.00** |
| RenderFluid      | idiomatic  |   880.8 μs |  53.77 μs |  32.00 μs |  1.73 |    0.06 |   99.6094 |  55.6641 | 17.5781 |  1790.02 KB |        2.16 |
| RenderScriban    | idiomatic  | 1,668.0 μs |  37.13 μs |  22.09 μs |  3.27 |    0.05 |  162.1094 |  82.0313 | 35.1563 |  2921.02 KB |        3.52 |
| RenderDotLiquid  | idiomatic  | 4,046.1 μs | 319.25 μs | 189.98 μs |  7.94 |    0.36 | 1046.8750 | 453.1250 | 15.6250 |  17363.3 KB |       20.93 |
| RenderHandlebars | idiomatic  |   783.0 μs |  48.42 μs |  28.82 μs |  1.54 |    0.06 |   58.5938 |  32.2266 | 16.6016 |  1104.14 KB |        1.33 |
| RenderRazor      | idiomatic  |   596.3 μs |  47.70 μs |  28.39 μs |  1.17 |    0.05 |   55.6641 |  43.9453 |       - |   1351.8 KB |        1.63 |
