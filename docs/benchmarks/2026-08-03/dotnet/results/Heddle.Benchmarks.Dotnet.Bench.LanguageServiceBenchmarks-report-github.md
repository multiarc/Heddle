```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method         | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Gen2   | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|--------:|--------:|--------:|-------:|----------:|------------:|
| CompileFlagOff | 473.9 μs |  52.39 μs |  31.18 μs |  1.00 |    0.09 | 11.7188 |  9.7656 | 0.9766 | 186.61 KB |        1.00 |
| CompileFlagOn  | 774.6 μs | 252.74 μs | 150.40 μs |  1.64 |    0.32 | 29.2969 | 19.5313 | 1.9531 | 478.39 KB |        2.56 |
