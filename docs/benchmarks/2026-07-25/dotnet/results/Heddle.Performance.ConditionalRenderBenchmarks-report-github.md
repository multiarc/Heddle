```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated  | Alloc Ratio |
|----------------- |----------:|---------:|---------:|------:|--------:|--------:|-------:|-----------:|------------:|
| RenderHeddle     |  19.78 μs | 0.021 μs | 0.017 μs |  1.00 |    0.00 |  6.0120 | 0.6409 |   98.33 KB |        1.00 |
| RenderFluid      |  55.77 μs | 0.177 μs | 0.166 μs |  2.82 |    0.01 |  3.2959 | 0.1221 |   54.94 KB |        0.56 |
| RenderScriban    | 136.82 μs | 0.493 μs | 0.437 μs |  6.92 |    0.02 | 15.6250 | 1.9531 |  271.73 KB |        2.76 |
| RenderDotLiquid  | 483.23 μs | 3.235 μs | 3.026 μs | 24.43 |    0.15 | 90.8203 |      - | 1485.26 KB |       15.11 |
| RenderHandlebars |  46.72 μs | 0.099 μs | 0.088 μs |  2.36 |    0.00 |  4.0894 | 0.3662 |   67.08 KB |        0.68 |
