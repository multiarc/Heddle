```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD Ryzen 9 9950X 0.62GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.110
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=15  LaunchCount=1  
WarmupCount=7  

```
| Method           | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------- |-----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| RenderHeddle     |   164.6 ns |  0.27 ns |  0.21 ns |  1.00 |    0.00 | 0.0961 |      - |    1608 B |        1.00 |
| RenderFluid      |   660.8 ns |  9.30 ns |  8.70 ns |  4.01 |    0.05 | 0.1392 |      - |    2344 B |        1.46 |
| RenderScriban    | 5,836.8 ns | 15.45 ns | 14.45 ns | 35.46 |    0.10 | 2.0752 | 0.1831 |   35198 B |       21.89 |
| RenderDotLiquid  | 2,628.7 ns |  9.65 ns |  9.03 ns | 15.97 |    0.06 | 0.6180 | 0.0038 |   10384 B |        6.46 |
| RenderHandlebars |   517.3 ns |  0.30 ns |  0.27 ns |  3.14 |    0.00 | 0.0439 |      - |     736 B |        0.46 |
