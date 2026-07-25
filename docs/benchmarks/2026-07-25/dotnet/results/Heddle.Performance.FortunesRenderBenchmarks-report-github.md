```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean        | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------- |------------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| RenderHeddle     |    938.7 ns |  4.77 ns |  4.46 ns |  1.00 |    0.01 | 0.4072 | 0.0038 |   6.66 KB |        1.00 |
| RenderFluid      |  2,994.3 ns |  4.58 ns |  4.28 ns |  3.19 |    0.02 | 0.3433 |      - |   5.61 KB |        0.84 |
| RenderScriban    | 12,385.8 ns | 52.78 ns | 46.78 ns | 13.20 |    0.08 | 2.8076 | 0.2441 |  47.13 KB |        7.07 |
| RenderDotLiquid  | 35,797.0 ns | 50.03 ns | 44.35 ns | 38.14 |    0.18 | 4.4556 | 0.2441 |  73.22 KB |       10.99 |
| RenderHandlebars |  2,399.7 ns | 64.81 ns | 60.62 ns |  2.56 |    0.06 | 0.1335 |      - |   2.22 KB |        0.33 |
