```

BenchmarkDotNet v0.16.0-preview.1, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
AMD Ryzen 9 5950X 3.40GHz, 1 CPU, 32 logical and 16 physical cores
Memory: 56.67 GB Total, 28.69 GB Available
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  Job-ATWSAP : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

IterationCount=12  IterationTime=250ms  LaunchCount=2
WarmupCount=5

```
| Method               | Categories | Levels | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| MortonToLinearBefore | ToLinear   | 5      | 31.14 μs | 0.699 μs | 0.884 μs |  1.00 |    0.00 |         - |          NA |
| MortonToLinear       | ToLinear   | 5      | 32.96 μs | 1.599 μs | 2.079 μs |  1.06 |    0.07 |         - |          NA |
|                      |            |        |          |          |          |       |         |           |             |
| LinearToMortonBefore | ToMorton   | 5      | 31.77 μs | 1.599 μs | 2.079 μs |  1.00 |    0.00 |         - |          NA |
| LinearToMorton       | ToMorton   | 5      | 31.99 μs | 1.490 μs | 1.830 μs |  1.01 |    0.08 |         - |          NA |
