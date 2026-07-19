```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 9950X 4.30GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v4


```
| Method           | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated | Alloc Ratio |
|----------------- |-----------:|---------:|---------:|------:|--------:|----------:|---------:|--------:|----------:|------------:|
| RenderHeddle     |   612.9 μs |  3.96 μs |  3.51 μs |  1.00 |    0.01 |   62.5000 |  40.0391 | 40.0391 |   1.14 MB |        1.00 |
| RenderFluid      |   976.2 μs |  3.64 μs |  3.40 μs |  1.59 |    0.01 |   98.6328 |  55.6641 | 20.5078 |   1.63 MB |        1.42 |
| RenderScriban    | 1,300.1 μs |  9.77 μs |  7.63 μs |  2.12 |    0.02 |  156.2500 |  78.1250 | 39.0625 |   2.72 MB |        2.38 |
| RenderDotLiquid  | 4,097.8 μs | 49.66 μs | 46.45 μs |  6.69 |    0.08 | 1050.7813 | 457.0313 | 19.5313 |  16.85 MB |       14.72 |
| RenderHandlebars |   660.4 μs | 11.96 μs | 10.60 μs |  1.08 |    0.02 |   60.5469 |  35.1563 | 20.5078 |   1.01 MB |        0.88 |
