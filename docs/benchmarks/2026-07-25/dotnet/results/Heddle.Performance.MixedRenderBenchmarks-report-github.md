```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------- |----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| RenderHeddle     |  3.591 μs | 0.0026 μs | 0.0025 μs |  1.00 |    0.00 |  2.6779 | 0.1907 |  43.88 KB |        1.00 |
| RenderFluid      | 11.939 μs | 0.0161 μs | 0.0150 μs |  3.32 |    0.00 |  1.7700 |      - |  28.99 KB |        0.66 |
| RenderScriban    | 28.637 μs | 0.0926 μs | 0.0866 μs |  7.97 |    0.02 |  7.3242 | 0.9766 | 121.71 KB |        2.77 |
| RenderDotLiquid  | 79.000 μs | 0.3185 μs | 0.2979 μs | 22.00 |    0.08 | 17.3340 | 1.3428 | 283.67 KB |        6.47 |
| RenderHandlebars |  9.854 μs | 0.0229 μs | 0.0191 μs |  2.74 |    0.01 |  2.6703 | 0.1984 |  43.83 KB |        1.00 |
