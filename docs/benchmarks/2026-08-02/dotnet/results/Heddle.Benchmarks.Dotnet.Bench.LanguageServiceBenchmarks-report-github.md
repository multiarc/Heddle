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
| CompileFlagOff | 469.1 μs |  50.73 μs |  30.19 μs |  1.00 |    0.09 | 11.7188 |  8.7891 | 0.9766 |  185.4 KB |        1.00 |
| CompileFlagOn  | 772.8 μs | 266.47 μs | 158.57 μs |  1.65 |    0.34 | 29.2969 | 19.5313 | 1.9531 | 477.15 KB |        2.57 |
