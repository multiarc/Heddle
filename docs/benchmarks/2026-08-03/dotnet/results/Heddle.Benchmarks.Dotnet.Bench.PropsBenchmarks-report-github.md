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
| DefinitionNoProps |  74.04 ns | 1.860 ns | 1.107 ns |  1.00 |    0.02 | 0.0248 |     416 B |        1.00 |
| AllConstantProps  |  93.17 ns | 2.839 ns | 1.689 ns |  1.26 |    0.03 | 0.0310 |     520 B |        1.25 |
| DynamicProps      | 116.89 ns | 4.033 ns | 2.400 ns |  1.58 |    0.04 | 0.0329 |     552 B |        1.33 |
| ParameterizedSlot | 219.86 ns | 5.047 ns | 3.003 ns |  2.97 |    0.06 | 0.0591 |     992 B |        2.38 |
