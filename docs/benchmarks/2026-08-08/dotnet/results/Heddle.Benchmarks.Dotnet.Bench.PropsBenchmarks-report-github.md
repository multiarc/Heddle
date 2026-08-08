```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method            | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| DefinitionNoProps |  77.95 ns | 4.089 ns | 2.434 ns |  1.00 |    0.04 | 0.0248 |     416 B |        1.00 |
| AllConstantProps  |  91.44 ns | 1.692 ns | 1.007 ns |  1.17 |    0.04 | 0.0310 |     520 B |        1.25 |
| DynamicProps      | 118.12 ns | 2.798 ns | 1.665 ns |  1.52 |    0.05 | 0.0329 |     552 B |        1.33 |
| ParameterizedSlot | 218.24 ns | 3.371 ns | 2.006 ns |  2.80 |    0.08 | 0.0591 |     992 B |        2.38 |
