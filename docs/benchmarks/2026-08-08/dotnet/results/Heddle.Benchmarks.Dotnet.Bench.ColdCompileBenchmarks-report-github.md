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
| ParseHeddle       | 301,347.8 ns | 31,046.30 ns | 18,475.16 ns | 1.003 |    0.08 | 86.9141 | 43.4570 | 1420.83 KB |       1.000 |
| CompileHeddle     | 527,285.2 ns | 50,852.80 ns | 30,261.69 ns | 1.755 |    0.14 | 50.7813 | 48.8281 |  834.97 KB |       0.588 |
| ParseFluid        |  83,061.3 ns |  3,913.52 ns |  2,328.88 ns | 0.277 |    0.02 | 23.5596 |  5.8594 |  386.79 KB |       0.272 |
| ParseScriban      |     393.8 ns |     18.77 ns |     11.17 ns | 0.001 |    0.00 |  0.1607 |  0.0010 |    2.63 KB |       0.002 |
| ParseDotLiquid    |     824.0 ns |     22.96 ns |     13.66 ns | 0.003 |    0.00 |  0.2041 |       - |    3.34 KB |       0.002 |
| CompileHandlebars | 726,066.2 ns | 11,931.16 ns |  7,100.04 ns | 2.417 |    0.13 |  2.9297 |  1.9531 |   48.92 KB |       0.034 |
