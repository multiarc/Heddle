```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------- |----------:|----------:|----------:|------:|--------:|----------:|---------:|---------:|----------:|------------:|
| RenderHeddle     |  1.882 ms | 0.0122 ms | 0.0102 ms |  1.00 |    0.01 |  273.4375 | 111.3281 | 111.3281 |   5.72 MB |        1.00 |
| RenderFluid      |  2.781 ms | 0.0026 ms | 0.0022 ms |  1.48 |    0.01 |  335.9375 | 230.4688 |  62.5000 |   5.86 MB |        1.02 |
| RenderScriban    |  7.197 ms | 0.0154 ms | 0.0144 ms |  3.82 |    0.02 |  695.3125 | 468.7500 | 125.0000 |  12.12 MB |        2.12 |
| RenderDotLiquid  | 10.984 ms | 0.0425 ms | 0.0398 ms |  5.84 |    0.04 | 1843.7500 | 843.7500 |  62.5000 |  29.99 MB |        5.25 |
| RenderHandlebars |  1.460 ms | 0.0094 ms | 0.0084 ms |  0.78 |    0.01 |  154.2969 | 111.3281 |  54.6875 |   3.11 MB |        0.54 |
