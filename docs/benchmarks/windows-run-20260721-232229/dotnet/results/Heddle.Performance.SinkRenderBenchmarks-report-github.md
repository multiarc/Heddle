```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------- |---------:|---------:|---------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| RenderString | 38.76 μs | 33.57 μs | 1.840 μs |  1.00 |    0.06 | 2.9297 | 2.9297 | 2.9297 | 227.88 KB |        1.00 |
