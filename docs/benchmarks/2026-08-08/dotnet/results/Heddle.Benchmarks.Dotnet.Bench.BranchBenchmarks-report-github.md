```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Mean          | Error         | StdDev        | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|----------------------- |--------------:|--------------:|--------------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| ListNoBranches         | 462,605.83 ns |  1,180.952 ns |    702.766 ns | 1.000 |    0.00 | 71.2891 | 28.3203 | 28.3203 |  925548 B |       1.000 |
| ListIfPair             | 274,382.76 ns | 26,593.574 ns | 15,825.409 ns | 0.593 |    0.03 | 64.4531 |  9.7656 |       - | 1083840 B |       1.171 |
| ListIfElse             | 262,863.86 ns |  1,979.206 ns |  1,177.794 ns | 0.568 |    0.00 | 69.3359 | 11.2305 |       - | 1163840 B |       1.257 |
| FlagshipNeverPublishes |      51.62 ns |      5.037 ns |      2.997 ns | 0.000 |    0.00 |  0.0181 |       - |       - |     304 B |       0.000 |
