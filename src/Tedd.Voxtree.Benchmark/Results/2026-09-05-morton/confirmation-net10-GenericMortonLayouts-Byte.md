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
| Method               | Categories | Levels | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------- |---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| MortonToLinearBefore | ToLinear   | 5      | 21.79 μs | 0.591 μs | 0.703 μs | 21.80 μs |  1.00 |    0.00 |         - |          NA |
| MortonToLinear       | ToLinear   | 5      | 17.03 μs | 0.119 μs | 0.151 μs | 17.00 μs |  0.78 |    0.03 |         - |          NA |
|                      |            |        |          |          |          |          |       |         |           |             |
| LinearToMortonBefore | ToMorton   | 5      | 19.67 μs | 0.201 μs | 0.239 μs | 19.62 μs |  1.00 |    0.00 |         - |          NA |
| LinearToMorton       | ToMorton   | 5      | 18.92 μs | 1.714 μs | 2.168 μs | 20.63 μs |  0.96 |    0.11 |         - |          NA |
