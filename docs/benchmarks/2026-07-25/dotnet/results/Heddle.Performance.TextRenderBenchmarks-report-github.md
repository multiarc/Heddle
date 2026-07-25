```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|----------------- |----------:|---------:|---------:|------:|--------:|--------:|--------:|--------:|-----------:|------------:|
| RenderHeddle     |  30.52 μs | 1.151 μs | 1.076 μs |  1.00 |    0.05 |  2.5024 |  2.5024 |  2.5024 |  223.26 KB |        1.00 |
| RenderRazor      |  41.66 μs | 2.108 μs | 1.972 μs |  1.37 |    0.08 |  7.8125 |  1.5869 |  0.2441 |  229.93 KB |        1.03 |
| RenderFluid      |  41.17 μs | 0.512 μs | 0.479 μs |  1.35 |    0.05 |  9.9487 |  4.6997 |  2.5635 |   227.7 KB |        1.02 |
| RenderScriban    | 312.14 μs | 4.766 μs | 4.459 μs | 10.24 |    0.37 | 55.6641 | 23.4375 | 12.6953 | 1149.78 KB |        5.15 |
| RenderDotLiquid  | 106.38 μs | 2.480 μs | 2.320 μs |  3.49 |    0.14 | 16.8457 |  8.3008 |  5.6152 |  398.26 KB |        1.78 |
| RenderHandlebars |  45.30 μs | 0.699 μs | 0.654 μs |  1.49 |    0.05 | 10.0098 |  4.3335 |  2.8687 |  223.31 KB |        1.00 |
