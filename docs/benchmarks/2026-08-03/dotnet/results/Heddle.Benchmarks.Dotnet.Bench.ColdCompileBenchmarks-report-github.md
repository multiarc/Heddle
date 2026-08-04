```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method            | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------ |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| ParseHeddle       | 295,567.7 ns | 29,415.47 ns | 17,504.68 ns | 1.003 |    0.08 | 86.9141 | 43.4570 | 1420.83 KB |       1.000 |
| CompileHeddle     | 519,663.9 ns | 40,527.05 ns | 24,116.99 ns | 1.763 |    0.12 | 50.7813 | 48.8281 |   835.2 KB |       0.588 |
| ParseFluid        |  82,109.8 ns |  1,759.43 ns |  1,047.01 ns | 0.279 |    0.02 | 23.5596 |  5.8594 |  386.79 KB |       0.272 |
| ParseScriban      |     390.2 ns |     17.39 ns |     10.35 ns | 0.001 |    0.00 |  0.1607 |  0.0010 |    2.63 KB |       0.002 |
| ParseDotLiquid    |     828.2 ns |     14.58 ns |      8.68 ns | 0.003 |    0.00 |  0.2041 |       - |    3.34 KB |       0.002 |
| CompileHandlebars | 711,865.0 ns | 17,232.49 ns | 10,254.78 ns | 2.416 |    0.13 |  2.9297 |  1.9531 |   48.92 KB |       0.034 |
