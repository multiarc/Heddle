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
| **RenderHeddle**           | **controlled** |  **34.066 μs** |  **5.3035 μs** |  **3.1560 μs** |  **1.01** |    **0.13** |  **3.1128** |  **3.1128** |  **3.1128** |  **233392 B** |       **1.000** |
| RenderHeddleUtf8       | controlled |   1.798 μs |  0.2713 μs |  0.1614 μs |  0.05 |    0.01 |  0.0095 |       - |       - |     184 B |       0.001 |
| RenderHeddleTextWriter | controlled |   1.238 μs |  0.0085 μs |  0.0051 μs |  0.04 |    0.00 |  0.0076 |       - |       - |     128 B |       0.001 |
| RenderFluid            | controlled |  60.234 μs |  3.6891 μs |  2.1953 μs |  1.78 |    0.18 | 11.1694 |  5.6152 |  3.6621 |  237548 B |       1.018 |
| RenderScriban          | controlled | 419.333 μs | 30.5925 μs | 18.2051 μs | 12.41 |    1.29 | 58.5938 | 26.3672 | 15.6250 | 1182107 B |       5.065 |
| RenderDotLiquid        | controlled | 153.755 μs | 13.3191 μs |  7.9260 μs |  4.55 |    0.49 | 18.7988 |  9.5215 |  7.3242 |  415490 B |       1.780 |
| RenderHandlebars       | controlled |  62.869 μs |  3.8804 μs |  2.3092 μs |  1.86 |    0.19 | 10.9863 |  5.3711 |  3.7842 |  233056 B |       0.999 |
| RenderRazor            | controlled |  64.815 μs | 10.3742 μs |  6.1735 μs |  1.92 |    0.25 |  8.6670 |  1.9531 |  0.7324 |  245440 B |       1.052 |
|                        |            |            |            |            |       |         |         |         |         |           |             |
| **RenderHeddle**           | **idiomatic**  |  **32.761 μs** |  **4.1901 μs** |  **2.4935 μs** |  **1.01** |    **0.10** |  **3.1738** |  **3.1738** |  **3.1738** |  **233414 B** |       **1.000** |
| RenderHeddleUtf8       | idiomatic  |   1.883 μs |  0.1831 μs |  0.1089 μs |  0.06 |    0.01 |  0.0095 |       - |       - |     184 B |       0.001 |
| RenderHeddleTextWriter | idiomatic  |   1.260 μs |  0.0123 μs |  0.0073 μs |  0.04 |    0.00 |  0.0076 |       - |       - |     128 B |       0.001 |
| RenderFluid            | idiomatic  |  60.527 μs |  3.2964 μs |  1.9616 μs |  1.86 |    0.15 | 11.1694 |  5.5542 |  3.6621 |  237764 B |       1.019 |
| RenderScriban          | idiomatic  | 431.057 μs | 40.3621 μs | 24.0189 μs | 13.23 |    1.21 | 59.5703 | 21.4844 | 15.6250 | 1212660 B |       5.195 |
| RenderDotLiquid        | idiomatic  | 194.211 μs |  9.8814 μs |  5.8802 μs |  5.96 |    0.48 | 19.5313 |  9.7656 |  7.3242 |  429755 B |       1.841 |
| RenderHandlebars       | idiomatic  |  64.713 μs |  6.4197 μs |  3.8203 μs |  1.99 |    0.19 | 10.9863 |  5.2490 |  3.7842 |  233305 B |       1.000 |
| RenderRazor            | idiomatic  |  59.550 μs | 13.8188 μs |  8.2233 μs |  1.83 |    0.28 |  8.6670 |  2.0752 |  0.7324 |  245665 B |       1.052 |
