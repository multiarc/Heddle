```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------- |-----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| RenderHeddle     |   3.907 μs | 0.0031 μs | 0.0029 μs |  1.00 |    0.00 |  1.3733 | 0.0458 |  22.45 KB |        1.00 |
| RenderFluid      |  14.294 μs | 0.0238 μs | 0.0223 μs |  3.66 |    0.01 |  2.0905 | 0.0153 |  34.37 KB |        1.53 |
| RenderScriban    |  47.704 μs | 0.1881 μs | 0.1760 μs | 12.21 |    0.04 |  9.7656 |      - | 160.13 KB |        7.13 |
| RenderDotLiquid  | 221.247 μs | 0.7034 μs | 0.6580 μs | 56.63 |    0.17 | 54.1992 | 3.9063 | 887.27 KB |       39.53 |
| RenderHandlebars |  29.368 μs | 0.1957 μs | 0.1830 μs |  7.52 |    0.05 | 29.1443 | 2.4719 |  476.2 KB |       21.22 |
