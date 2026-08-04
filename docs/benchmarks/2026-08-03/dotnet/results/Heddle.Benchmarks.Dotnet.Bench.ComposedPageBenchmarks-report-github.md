```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8894/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=3  
WarmupCount=3  

```
| Method                 | Track      | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|----------------------- |----------- |-----------:|-----------:|-----------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| **RenderHeddle**           | **controlled** |  **32.743 μs** |  **8.5992 μs** |  **5.1173 μs** |  **1.02** |    **0.21** |  **2.8076** |  **2.8076** |  **2.8076** |  **233390 B** |       **1.000** |
| RenderHeddleUtf8       | controlled |   1.827 μs |  0.3204 μs |  0.1907 μs |  0.06 |    0.01 |  0.0095 |       - |       - |     184 B |       0.001 |
| RenderHeddleTextWriter | controlled |   1.250 μs |  0.0275 μs |  0.0163 μs |  0.04 |    0.01 |  0.0076 |       - |       - |     128 B |       0.001 |
| RenderFluid            | controlled |  59.495 μs |  2.4224 μs |  1.4415 μs |  1.86 |    0.27 | 11.1084 |  5.5542 |  3.6011 |  237548 B |       1.018 |
| RenderScriban          | controlled | 412.535 μs | 35.1875 μs | 20.9395 μs | 12.87 |    1.93 | 58.5938 | 24.4141 | 15.6250 | 1182110 B |       5.065 |
| RenderDotLiquid        | controlled | 162.243 μs | 26.5785 μs | 15.8164 μs |  5.06 |    0.86 | 18.5547 |  9.5215 |  7.0801 |  415489 B |       1.780 |
| RenderHandlebars       | controlled |  63.980 μs |  6.8241 μs |  4.0609 μs |  2.00 |    0.31 | 10.9863 |  5.3711 |  3.7842 |  233055 B |       0.999 |
| RenderRazor            | controlled |  60.316 μs | 12.9126 μs |  7.6841 μs |  1.88 |    0.35 |  8.6670 |  2.0752 |  0.7324 |  245441 B |       1.052 |
|                        |            |            |            |            |       |         |         |         |         |           |             |
| **RenderHeddle**           | **idiomatic**  |  **32.046 μs** |  **5.2019 μs** |  **3.0956 μs** |  **1.01** |    **0.13** |  **3.1433** |  **3.1433** |  **3.1433** |  **233415 B** |       **1.000** |
| RenderHeddleUtf8       | idiomatic  |   1.862 μs |  0.2146 μs |  0.1277 μs |  0.06 |    0.01 |  0.0095 |       - |       - |     184 B |       0.001 |
| RenderHeddleTextWriter | idiomatic  |   1.253 μs |  0.0072 μs |  0.0043 μs |  0.04 |    0.00 |  0.0076 |       - |       - |     128 B |       0.001 |
| RenderFluid            | idiomatic  |  59.352 μs |  1.5640 μs |  0.9307 μs |  1.87 |    0.17 | 11.1694 |  5.6763 |  3.6621 |  237763 B |       1.019 |
| RenderScriban          | idiomatic  | 422.155 μs | 19.3341 μs | 11.5054 μs | 13.28 |    1.22 | 60.5469 | 23.4375 | 16.6016 | 1212574 B |       5.195 |
| RenderDotLiquid        | idiomatic  | 182.695 μs |  5.1167 μs |  3.0449 μs |  5.75 |    0.51 | 19.5313 | 10.0098 |  7.3242 |  429753 B |       1.841 |
| RenderHandlebars       | idiomatic  |  63.347 μs |  4.4589 μs |  2.6534 μs |  1.99 |    0.19 | 10.9863 |  5.2490 |  3.7842 |  233305 B |       1.000 |
| RenderRazor            | idiomatic  |  60.701 μs | 12.2054 μs |  7.2632 μs |  1.91 |    0.28 |  8.6670 |  2.0752 |  0.7324 |  245665 B |       1.052 |
