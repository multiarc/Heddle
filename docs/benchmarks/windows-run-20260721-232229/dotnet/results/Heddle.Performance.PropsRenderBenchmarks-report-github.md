```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host] : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  Dry    : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=Dry  IterationCount=1  LaunchCount=1  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=1  

```
| Method                     | Mean      | Error | Ratio | Allocated | Alloc Ratio |
|--------------------------- |----------:|------:|------:|----------:|------------:|
| RenderDefinitionNoProps    |  7.526 ms |    NA |  1.00 |   3.81 MB |        1.00 |
| RenderAllConstantProps10K  |  9.748 ms |    NA |  1.30 |   4.73 MB |        1.24 |
| RenderDynamicProps10K      | 10.135 ms |    NA |  1.35 |   5.04 MB |        1.32 |
| RenderParameterizedSlot10K | 19.262 ms |    NA |  2.56 |      9 MB |        2.36 |
