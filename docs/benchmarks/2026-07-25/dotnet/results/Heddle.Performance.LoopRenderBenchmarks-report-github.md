```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated | Alloc Ratio |
|----------------- |-----------:|---------:|---------:|------:|--------:|----------:|---------:|--------:|----------:|------------:|
| RenderHeddle     |   379.3 μs |  3.66 μs |  3.42 μs |  1.00 |    0.01 |   50.2930 |  27.3438 | 27.3438 |   1.14 MB |        1.00 |
| RenderFluid      |   944.6 μs |  1.99 μs |  1.86 μs |  2.49 |    0.02 |   93.7500 |  49.8047 | 15.6250 |   1.63 MB |        1.42 |
| RenderScriban    | 1,632.1 μs | 13.24 μs | 12.38 μs |  4.30 |    0.05 |  140.6250 |  62.5000 | 31.2500 |   2.72 MB |        2.38 |
| RenderDotLiquid  | 4,886.8 μs | 11.19 μs | 10.47 μs | 12.89 |    0.12 | 1046.8750 | 453.1250 | 15.6250 |  16.85 MB |       14.72 |
| RenderHandlebars |   651.3 μs |  1.21 μs |  1.07 μs |  1.72 |    0.02 |   55.6641 |  30.2734 | 15.6250 |   1.01 MB |        0.88 |
