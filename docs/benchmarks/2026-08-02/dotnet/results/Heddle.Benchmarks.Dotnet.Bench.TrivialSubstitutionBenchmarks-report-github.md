```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method           | Track      | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------- |----------- |------------:|------------:|------------:|------:|--------:|--------:|-------:|----------:|------------:|
| **RenderHeddle**     | **controlled** |  **1,154.7 ns** |    **62.35 ns** |    **37.11 ns** |  **1.00** |    **0.04** |  **3.9215** | **0.6523** |   **65808 B** |        **1.00** |
| RenderFluid      | controlled |    530.0 ns |    32.73 ns |    19.48 ns |  0.46 |    0.02 |  0.1392 |      - |    2344 B |        0.04 |
| RenderScriban    | controlled | 40,270.3 ns | 8,621.95 ns | 5,130.79 ns | 34.91 |    4.35 | 20.5078 | 1.7090 |  343936 B |        5.23 |
| RenderDotLiquid  | controlled |  2,039.5 ns |   183.81 ns |   109.38 ns |  1.77 |    0.10 |  0.6180 | 0.0038 |   10384 B |        0.16 |
| RenderHandlebars | controlled |    399.2 ns |    35.06 ns |    20.86 ns |  0.35 |    0.02 |  0.0439 |      - |     736 B |        0.01 |
| RenderRazor      | controlled |  5,938.0 ns |   727.94 ns |   433.19 ns |  5.15 |    0.39 |  0.7935 | 0.2594 |   13360 B |        0.20 |
|                  |            |             |             |             |       |         |         |        |           |             |
| **RenderHeddle**     | **idiomatic**  |  **1,089.1 ns** |    **19.32 ns** |    **11.50 ns** |  **1.00** |    **0.01** |  **3.9215** | **0.6523** |   **65808 B** |        **1.00** |
| RenderFluid      | idiomatic  |    508.1 ns |    19.08 ns |    11.35 ns |  0.47 |    0.01 |  0.1440 |      - |    2424 B |        0.04 |
| RenderScriban    | idiomatic  | 41,415.1 ns | 7,777.84 ns | 4,628.47 ns | 38.03 |    4.05 | 20.5078 | 1.8311 |  344024 B |        5.23 |
| RenderDotLiquid  | idiomatic  |  2,075.2 ns |   421.40 ns |   250.77 ns |  1.91 |    0.22 |  0.6218 | 0.0038 |   10464 B |        0.16 |
| RenderHandlebars | idiomatic  |    486.6 ns |    25.65 ns |    15.26 ns |  0.45 |    0.01 |  0.0486 |      - |     816 B |        0.01 |
| RenderRazor      | idiomatic  |  4,971.0 ns |   169.79 ns |   101.04 ns |  4.56 |    0.10 |  0.7858 | 0.2594 |   13200 B |        0.20 |
