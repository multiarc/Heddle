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
| CompileFlagOff | 483.3 μs |  51.50 μs |  30.65 μs |  1.00 |    0.08 | 11.7188 |  9.7656 | 0.9766 | 186.61 KB |        1.00 |
| CompileFlagOn  | 762.4 μs | 258.81 μs | 154.01 μs |  1.58 |    0.32 | 29.2969 | 19.5313 | 1.9531 | 478.38 KB |        2.56 |
