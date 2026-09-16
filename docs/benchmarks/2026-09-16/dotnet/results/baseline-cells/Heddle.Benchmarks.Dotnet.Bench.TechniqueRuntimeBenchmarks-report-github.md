```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=5  LaunchCount=10  
WarmupCount=3  

```
| Method     | Workload      | Mean      | Error    | StdDev   | Median    | Gen0    | Gen1    | Gen2    | Allocated |
|----------- |-------------- |----------:|---------:|---------:|----------:|--------:|--------:|--------:|----------:|
| **String**     | **composed-page** |  **30.13 μs** | **0.364 μs** | **0.727 μs** |  **30.48 μs** | **14.9536** | **13.9771** | **13.9771** | **211.01 KB** |
| **TextWriter** | **large-loop**    | **145.99 μs** | **0.572 μs** | **1.088 μs** | **145.82 μs** | **39.3066** |       **-** |       **-** | **645.47 KB** |
