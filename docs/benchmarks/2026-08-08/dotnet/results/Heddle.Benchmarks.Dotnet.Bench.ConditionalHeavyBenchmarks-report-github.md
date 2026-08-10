```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|----------------------- |----------- |----------:|---------:|---------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **RenderHeddle**           | **controlled** |  **15.02 μs** | **0.852 μs** | **0.507 μs** |  **1.00** |    **0.05** |  **6.0120** |  **0.6561** |   **98.43 KB** |        **1.00** |
| RenderHeddleUtf8       | controlled |  18.18 μs | 0.277 μs | 0.165 μs |  1.21 |    0.04 |  2.1057 |       - |   34.55 KB |        0.35 |
| RenderHeddleTextWriter | controlled |  14.53 μs | 0.332 μs | 0.197 μs |  0.97 |    0.03 |  2.1057 |       - |   34.55 KB |        0.35 |
| RenderFluid            | controlled |  39.32 μs | 0.387 μs | 0.230 μs |  2.62 |    0.08 |  3.2959 |  0.1221 |   54.94 KB |        0.56 |
| RenderScriban          | controlled | 105.60 μs | 2.632 μs | 1.566 μs |  7.04 |    0.24 | 18.4326 |  3.0518 |  301.81 KB |        3.07 |
| RenderDotLiquid        | controlled | 343.11 μs | 5.678 μs | 3.379 μs | 22.86 |    0.76 | 90.8203 |       - | 1485.26 KB |       15.09 |
| RenderHandlebars       | controlled |  34.51 μs | 0.641 μs | 0.382 μs |  2.30 |    0.08 |  4.0894 |  0.3662 |   67.08 KB |        0.68 |
| RenderRazor            | controlled |  37.65 μs | 0.257 μs | 0.153 μs |  2.51 |    0.08 |  5.6152 |  2.8076 |   93.04 KB |        0.95 |
|                        |            |           |          |          |       |         |         |         |            |             |
| **RenderHeddle**           | **idiomatic**  |  **15.70 μs** | **0.395 μs** | **0.235 μs** |  **1.00** |    **0.02** |  **8.3618** |  **1.3733** |  **136.98 KB** |        **1.00** |
| RenderHeddleUtf8       | idiomatic  |  18.87 μs | 0.466 μs | 0.277 μs |  1.20 |    0.02 |  2.1057 |       - |   34.55 KB |        0.25 |
| RenderHeddleTextWriter | idiomatic  |  14.86 μs | 0.205 μs | 0.122 μs |  0.95 |    0.02 |  2.1057 |       - |   34.55 KB |        0.25 |
| RenderFluid            | idiomatic  |  40.62 μs | 0.369 μs | 0.219 μs |  2.59 |    0.04 |  4.6387 |       - |   76.83 KB |        0.56 |
| RenderScriban          | idiomatic  | 112.02 μs | 4.699 μs | 2.796 μs |  7.13 |    0.20 | 20.7520 |  4.1504 |  341.52 KB |        2.49 |
| RenderDotLiquid        | idiomatic  | 354.09 μs | 7.533 μs | 4.483 μs | 22.55 |    0.42 | 93.7500 | 17.5781 | 1538.55 KB |       11.23 |
| RenderHandlebars       | idiomatic  |  37.04 μs | 0.174 μs | 0.103 μs |  2.36 |    0.03 |  7.2021 |  0.9766 |  118.02 KB |        0.86 |
| RenderRazor            | idiomatic  |  25.25 μs | 0.395 μs | 0.235 μs |  1.61 |    0.03 |  8.9417 |  2.2278 |   146.8 KB |        1.07 |
