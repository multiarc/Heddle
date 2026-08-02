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
| ParseHeddle       | 294,823.0 ns | 40,354.68 ns | 24,014.42 ns | 1.006 |    0.11 | 86.9141 | 21.9727 | 1420.48 KB |       1.000 |
| CompileHeddle     | 515,573.9 ns | 75,695.16 ns | 45,044.97 ns | 1.759 |    0.20 | 50.7813 | 48.8281 |  833.76 KB |       0.587 |
| ParseFluid        |  83,583.6 ns |  2,872.76 ns |  1,709.53 ns | 0.285 |    0.02 | 23.5596 |  5.8594 |  386.79 KB |       0.272 |
| ParseScriban      |     391.7 ns |     33.23 ns |     19.77 ns | 0.001 |    0.00 |  0.1607 |  0.0010 |    2.63 KB |       0.002 |
| ParseDotLiquid    |     827.5 ns |     24.76 ns |     14.73 ns | 0.003 |    0.00 |  0.2041 |       - |    3.34 KB |       0.002 |
| CompileHandlebars | 716,149.8 ns | 20,805.95 ns | 12,381.29 ns | 2.443 |    0.19 |  2.9297 |  1.9531 |   48.92 KB |       0.034 |
