```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Mean          | Error        | StdDev       | Ratio | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|----------------------- |--------------:|-------------:|-------------:|------:|--------:|--------:|--------:|----------:|------------:|
| ListNoBranches         | 462,392.55 ns | 1,054.136 ns |   627.300 ns | 1.000 | 71.2891 | 28.3203 | 28.3203 |  925548 B |       1.000 |
| ListIfPair             | 257,427.06 ns | 5,761.358 ns | 3,428.492 ns | 0.557 | 64.4531 |  9.7656 |       - | 1083840 B |       1.171 |
| ListIfElse             | 259,522.16 ns | 5,055.352 ns | 3,008.359 ns | 0.561 | 69.3359 | 11.2305 |       - | 1163840 B |       1.257 |
| FlagshipNeverPublishes |      51.18 ns |     4.314 ns |     2.567 ns | 0.000 |  0.0181 |       - |       - |     304 B |       0.000 |
