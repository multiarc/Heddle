```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method            | Mean      | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| DefinitionNoProps |  72.67 ns |  3.321 ns | 1.976 ns |  1.00 |    0.04 | 0.0238 |     400 B |        1.00 |
| AllConstantProps  |  89.61 ns |  3.455 ns | 2.056 ns |  1.23 |    0.04 | 0.0296 |     496 B |        1.24 |
| DynamicProps      | 116.61 ns |  7.701 ns | 4.583 ns |  1.61 |    0.07 | 0.0315 |     528 B |        1.32 |
| ParameterizedSlot | 221.07 ns | 15.278 ns | 9.092 ns |  3.04 |    0.14 | 0.0563 |     944 B |        2.36 |
