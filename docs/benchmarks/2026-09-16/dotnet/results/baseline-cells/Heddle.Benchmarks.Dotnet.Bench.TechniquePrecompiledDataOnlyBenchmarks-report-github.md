```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=5  LaunchCount=10  
WarmupCount=3  

```
| Method | Workload      | Mean     | Error    | StdDev   | Gen0    | Gen1    | Gen2    | Allocated |
|------- |-------------- |---------:|---------:|---------:|--------:|--------:|--------:|----------:|
| String | composed-page | 31.79 μs | 0.601 μs | 1.215 μs | 14.2212 | 13.2446 | 13.2446 | 210.92 KB |
