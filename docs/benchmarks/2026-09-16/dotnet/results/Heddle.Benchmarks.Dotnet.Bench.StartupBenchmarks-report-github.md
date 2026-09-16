```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  Job-DKDFMQ : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

InvocationCount=1  IterationCount=1  LaunchCount=20  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                                | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Allocated   | Alloc Ratio |
|-------------------------------------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|------------:|
| CompileHeddle                         | 296.9 ms | 57.76 ms | 66.51 ms |  1.03 |    0.26 | 1000.0000 | 19886.59 KB |        1.00 |
| RegisterAndRenderCompiledForm         | 285.7 ms |  5.64 ms |  6.49 ms |  0.99 |    0.12 |         - |   935.63 KB |        0.05 |
| RegisterAndRenderCompiledFormDataOnly | 284.8 ms |  5.88 ms |  6.78 ms |  0.98 |    0.12 |         - |   935.63 KB |        0.05 |
