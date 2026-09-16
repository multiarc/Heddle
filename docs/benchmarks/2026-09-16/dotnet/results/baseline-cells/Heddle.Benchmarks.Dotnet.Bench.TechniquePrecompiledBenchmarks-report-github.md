```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=5  LaunchCount=10  
WarmupCount=3  

```
| Method     | Workload   | Mean     | Error   | StdDev  | Median   | Gen0    | Allocated |
|----------- |----------- |---------:|--------:|--------:|---------:|--------:|----------:|
| TextWriter | large-loop | 142.8 μs | 1.42 μs | 2.88 μs | 144.1 μs | 39.3066 | 645.38 KB |
