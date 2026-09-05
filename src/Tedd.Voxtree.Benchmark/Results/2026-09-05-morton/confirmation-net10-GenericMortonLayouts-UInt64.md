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
| MortonToLinearBefore | ToLinear   | 5      | 21.67 μs | 0.187 μs | 0.243 μs |  1.00 |    0.00 |         - |          NA |
| MortonToLinear       | ToLinear   | 5      | 20.56 μs | 0.527 μs | 0.666 μs |  0.95 |    0.03 |         - |          NA |
|                      |            |        |          |          |          |       |         |           |             |
| LinearToMortonBefore | ToMorton   | 5      | 20.21 μs | 0.814 μs | 1.059 μs |  1.00 |    0.00 |         - |          NA |
| LinearToMorton       | ToMorton   | 5      | 22.27 μs | 2.624 μs | 3.318 μs |  1.10 |    0.17 |         - |          NA |
